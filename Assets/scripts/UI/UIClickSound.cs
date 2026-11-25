using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Plays a click sound for every Button, Toggle and Dropdown in every scene.
/// It starts itself automatically, so no setup is needed.
/// Sound: Assets/Resources/Audio/UIClick (replace that file to change it).
/// </summary>
public class UIClickSound : MonoBehaviour
{
    public static UIClickSound Instance { get; private set; }

    [Range(0f, 1f)] public float volume = 0.8f;
    public float pitchVariation = 0.05f;
    public float minInterval = 0.05f;

    private AudioSource source;
    private AudioClip clip;
    private float lastPlayTime = -1f;
    private float scanTimer;
    private readonly HashSet<int> hooked = new HashSet<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("UIClickSound");
        DontDestroyOnLoad(go);
        go.AddComponent<UIClickSound>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        clip = Resources.Load<AudioClip>("Audio/UIClick");
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
        HookAll();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    private void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        hooked.Clear();
        HookAll();
    }

    private void Update()
    {
        // Catch buttons created at runtime (popups, shop items, etc.)
        scanTimer -= Time.unscaledDeltaTime;
        if (scanTimer <= 0f)
        {
            scanTimer = 0.5f;
            HookAll();
        }
    }

    private void HookAll()
    {
        foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (hooked.Add(b.GetInstanceID())) b.onClick.AddListener(Play);
        foreach (var t in FindObjectsByType<Toggle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (hooked.Add(t.GetInstanceID())) t.onValueChanged.AddListener(OnToggle);
        foreach (var d in FindObjectsByType<TMPro.TMP_Dropdown>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (hooked.Add(d.GetInstanceID())) d.onValueChanged.AddListener(OnIndex);
    }

    private void OnToggle(bool v) { Play(); }
    private void OnIndex(int v) { Play(); }

    /// <summary>Call this from any custom UI that does not use Button/Toggle.</summary>
    public void Play()
    {
        if (clip == null || source == null) return;
        if (Time.unscaledTime - lastPlayTime < minInterval) return;
        lastPlayTime = Time.unscaledTime;
        source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        source.PlayOneShot(clip, volume * KoSAudio.SfxVolume);
    }
}
