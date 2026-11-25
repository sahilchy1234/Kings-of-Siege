using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Player settings for Kings of Siege, saved in PlayerPrefs and applied on every scene load.
/// Audio volumes live in KoSAudio (KoS_MusicVol / KoS_SfxVol); this class adds mute memory,
/// vibration, graphics quality and frame rate.
///
/// Keys: KoS_MusicPrev, KoS_SfxPrev (volume to restore on unmute), KoS_Vibration (0/1),
/// KoS_Graphics (0 Low, 1 Medium, 2 High), KoS_Fps (30 or 60).
/// </summary>
public static class KoSSettings
{
    const string VIBRATION_KEY = "KoS_Vibration";
    const string GRAPHICS_KEY = "KoS_Graphics";
    const string FPS_KEY = "KoS_Fps";
    const string MUSIC_PREV_KEY = "KoS_MusicPrev";
    const string SFX_PREV_KEY = "KoS_SfxPrev";

    public static readonly string[] GraphicsNames = { "Low", "Medium", "High" };
    public static readonly int[] FpsOptions = { 30, 60 };

    public static event System.Action OnChanged;

    // ───────────────────────────── values ─────────────────────────────

    public static bool Vibration
    {
        get => PlayerPrefs.GetInt(VIBRATION_KEY, 1) == 1;
        set { PlayerPrefs.SetInt(VIBRATION_KEY, value ? 1 : 0); PlayerPrefs.Save(); OnChanged?.Invoke(); }
    }

    /// <summary>0 = Low, 1 = Medium, 2 = High.</summary>
    public static int Graphics
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(GRAPHICS_KEY, 2), 0, 2);
        set { PlayerPrefs.SetInt(GRAPHICS_KEY, Mathf.Clamp(value, 0, 2)); PlayerPrefs.Save(); Apply(); OnChanged?.Invoke(); }
    }

    public static int TargetFps
    {
        get { int f = PlayerPrefs.GetInt(FPS_KEY, 60); return f <= 30 ? 30 : 60; }
        set { PlayerPrefs.SetInt(FPS_KEY, value <= 30 ? 30 : 60); PlayerPrefs.Save(); Apply(); OnChanged?.Invoke(); }
    }

    public static float MusicVolume
    {
        get => KoSAudio.MusicVolume;
        set { KoSAudio.MusicVolume = value; if (value > 0.01f) PlayerPrefs.SetFloat(MUSIC_PREV_KEY, value); }
    }

    public static float SfxVolume
    {
        get => KoSAudio.SfxVolume;
        set { KoSAudio.SfxVolume = value; if (value > 0.01f) PlayerPrefs.SetFloat(SFX_PREV_KEY, value); }
    }

    public static bool MusicMuted => KoSAudio.MusicVolume <= 0.001f;
    public static bool SfxMuted => KoSAudio.SfxVolume <= 0.001f;

    /// <summary>Mutes, or restores the volume the player had before muting.</summary>
    public static void ToggleMusicMute()
    {
        if (MusicMuted) KoSAudio.MusicVolume = Mathf.Max(0.1f, PlayerPrefs.GetFloat(MUSIC_PREV_KEY, 1f));
        else { PlayerPrefs.SetFloat(MUSIC_PREV_KEY, KoSAudio.MusicVolume); KoSAudio.MusicVolume = 0f; }
        PlayerPrefs.Save();
    }

    public static void ToggleSfxMute()
    {
        if (SfxMuted) KoSAudio.SfxVolume = Mathf.Max(0.1f, PlayerPrefs.GetFloat(SFX_PREV_KEY, 1f));
        else { PlayerPrefs.SetFloat(SFX_PREV_KEY, KoSAudio.SfxVolume); KoSAudio.SfxVolume = 0f; }
        PlayerPrefs.Save();
    }

    // ───────────────────────────── haptics ─────────────────────────────

    /// <summary>Short tap of vibration (only when the player has vibration on). Safe to call anywhere.</summary>
    public static void Haptic(long milliseconds = 25)
    {
        if (!Vibration) return;
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
            {
                if (vibrator != null) { vibrator.Call("vibrate", milliseconds); return; }
            }
        }
        catch (System.Exception) { }
        Handheld.Vibrate();   // fallback; this call also makes Unity add the VIBRATE permission
#elif UNITY_IOS && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }

    // ───────────────────────────── apply ─────────────────────────────

#if !UNITY_EDITOR
    static float s_baseRenderScale = -1f;
#endif
    static bool s_hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (s_hooked) return;
        s_hooked = true;
        // sceneLoaded runs after the scene's Awake calls, so this wins over AndroidPerformanceInit.
        SceneManager.sceneLoaded += (s, m) => Apply();
    }

    /// <summary>Pushes frame rate and graphics settings to the engine.</summary>
    public static void Apply()
    {
#if !UNITY_EDITOR
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFps;

        // Graphics: scale the 3D render resolution relative to the project's own setting.
        // (Editor play mode is skipped so the URP asset on disk is never changed.)
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null)
        {
            if (s_baseRenderScale < 0f) s_baseRenderScale = urp.renderScale;
            float mul = Graphics == 0 ? 0.7f : Graphics == 1 ? 0.85f : 1f;
            urp.renderScale = Mathf.Clamp(s_baseRenderScale * mul, 0.5f, 2f);
        }
        QualitySettings.lodBias = Graphics == 0 ? 0.4f : Graphics == 1 ? 0.6f : 0.9f;
#endif
    }
}
