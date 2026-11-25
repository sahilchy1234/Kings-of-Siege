using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Photon.Pun;

/// <summary>
/// Ensures WebGL and desktop clients never drop connection or freeze when losing focus:
/// 1. Prevents Chrome / Edge / Firefox from suspending the WebGL canvas (Error 1006 / AppOutOfFocus)
///    via active Web Audio stream and background Web Worker heartbeat.
/// 2. Guarantees an AudioListener exists across all scenes (fixing the 'no audio listener' warning).
/// 3. Continuously keeps Application.runInBackground enabled.
/// 4. Extends Photon disconnect timeout to 60 seconds and SentCountAllowance to 25.
/// 5. Actively pumps ACKs and outgoing commands so sockets never stall.
/// </summary>
public class WebGLBackgroundKeepAlive : MonoBehaviour
{
    private static WebGLBackgroundKeepAlive instance;
    private AudioSource keepAliveSource;
    private float updateTimer = 0f;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void PhotonWebGL_StartKeepAliveWorker();
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance == null)
        {
            GameObject go = new GameObject("[WebGLBackgroundKeepAlive]");
            instance = go.AddComponent<WebGLBackgroundKeepAlive>();
            DontDestroyOnLoad(go);
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        Application.runInBackground = true;

        // Ensure AudioListener exists
        if (FindFirstObjectByType<AudioListener>() == null)
        {
            gameObject.AddComponent<AudioListener>();
        }

        SetupKeepAliveAudio();
        ConfigurePhotonTimeouts();

#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            PhotonWebGL_StartKeepAliveWorker();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[WebGLKeepAlive] Could not start native JS worker: " + ex.Message);
        }
#endif
    }

    private void Update()
    {
        if (!Application.runInBackground)
        {
            Application.runInBackground = true;
        }

        ConfigurePhotonTimeouts();

        // High frequency heartbeat to prevent socket idle timeout
        updateTimer += Time.unscaledDeltaTime;
        if (updateTimer >= 0.15f)
        {
            updateTimer = 0f;
            PumpKeepAlive();
        }
    }

    /// <summary>
    /// Invoked directly by JavaScript Web Worker via unityInstance.SendMessage("[WebGLBackgroundKeepAlive]", "OnKeepAliveTick").
    /// Runs even when Chrome has suspended requestAnimationFrame for background tabs!
    /// </summary>
    public void OnKeepAliveTick()
    {
        PumpKeepAlive();
    }

    private void PumpKeepAlive()
    {
        if (PhotonNetwork.IsConnected && !PhotonNetwork.OfflineMode)
        {
            if (PhotonNetwork.NetworkingClient != null && PhotonNetwork.NetworkingClient.LoadBalancingPeer != null)
            {
                PhotonNetwork.NetworkingClient.LoadBalancingPeer.SendAcksOnly();
            }
            PhotonNetwork.SendAllOutgoingCommands();
        }
    }

    private void SetupKeepAliveAudio()
    {
        keepAliveSource = gameObject.AddComponent<AudioSource>();
        keepAliveSource.loop = true;
        keepAliveSource.volume = 0.0001f;
        keepAliveSource.playOnAwake = false;
        keepAliveSource.spatialBlend = 0f;

        int sampleRate = 44100;
        AudioClip silentClip = AudioClip.Create("SilentKeepAlive", sampleRate, 1, sampleRate, false);
        float[] silentSamples = new float[sampleRate];
        for (int i = 0; i < silentSamples.Length; i++)
        {
            silentSamples[i] = 0.0001f; // Subtle non-zero to satisfy browser audio analysis
        }
        silentClip.SetData(silentSamples, 0);

        keepAliveSource.clip = silentClip;
        keepAliveSource.Play();
    }

    private void ConfigurePhotonTimeouts()
    {
        if (PhotonNetwork.NetworkingClient != null && PhotonNetwork.NetworkingClient.LoadBalancingPeer != null)
        {
            if (PhotonNetwork.NetworkingClient.LoadBalancingPeer.DisconnectTimeout < 60000)
            {
                PhotonNetwork.NetworkingClient.LoadBalancingPeer.DisconnectTimeout = 60000; // 60 seconds
            }
            if (PhotonNetwork.NetworkingClient.LoadBalancingPeer.SentCountAllowance < 25)
            {
                PhotonNetwork.NetworkingClient.LoadBalancingPeer.SentCountAllowance = 25;
            }
        }
    }
}
