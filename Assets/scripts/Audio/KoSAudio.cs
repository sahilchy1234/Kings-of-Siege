using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central sound player and mixer for Kings of Siege. It starts itself, so no scene setup is needed.
///
/// What it does:
/// - Plays every gameplay sound through one pool of voices (no more temp GameObjects per arrow).
/// - Per-event mixing: volume, random pitch range, max overlapping voices, minimum gap between plays.
///   Big battles stay readable: once an event hits its voice limit, extra plays are dropped, and
///   each extra copy of the same sound playing at once is quieter.
/// - Library sounds live in Resources/Audio/KoS/&lt;folder&gt;/ (one folder per event, every clip in the
///   folder is a random variant), so they load in Android builds without AssetDatabase.
/// - Screen-based panning and off-screen attenuation instead of 3D roll-off, which keeps sounds
///   audible on a phone with a far-away battle camera.
/// - Music: menu and battle themes with crossfade, ducking under results/fanfares, and a small
///   tempo lift for the final seconds.
/// - Volume settings saved in PlayerPrefs: KoS_MusicVol, KoS_SfxVol (0..1).
///
/// Existing scripts call KoSAudio.PlayOneShot(audioSource, clip, volume, "event") in place of
/// audioSource.PlayOneShot(clip, volume). When the event has library sounds they replace (or, for
/// "layer" events, add to) the original clip; otherwise the original clip is played with the
/// event's mixing rules.
/// </summary>
public class KoSAudio : MonoBehaviour
{
    // ---------------------------------------------------------------- event table

    public class EventDef
    {
        public string folder;          // Resources/Audio/KoS/<folder>; null = no library clips
        public float volume = 1f;      // multiplied with the caller's volume
        public float pitchMin = 0.95f, pitchMax = 1.05f;
        public int maxVoices = 4;      // simultaneous copies of this event
        public float minInterval = 0.04f;
        public int priority = 128;     // lower = more important (like AudioSource.priority)
        public bool layer;             // true: play library clip AND the caller's clip
        public bool ui;                // 2D, ignores battle panning
        public float duck;             // >0: duck music to this level for duckTime seconds
        public float duckTime = 2.5f;
    }

    static readonly Dictionary<string, EventDef> Events = new Dictionary<string, EventDef>
    {
        // --- units
        { "unit_attack",   new EventDef { volume = 0.55f, pitchMin = 0.9f,  pitchMax = 1.12f, maxVoices = 4, minInterval = 0.05f } },
        { "swing",         new EventDef { folder = "swing", volume = 0.35f, pitchMin = 0.9f, pitchMax = 1.15f, maxVoices = 3, minInterval = 0.06f } },
        { "bow_release",   new EventDef { folder = "arrow_release", volume = 0.45f, pitchMin = 0.9f, pitchMax = 1.12f, maxVoices = 2, minInterval = 0.12f, priority = 180 } },
        { "melee_hit",     new EventDef { folder = "melee_hit", volume = 0.5f, pitchMin = 0.88f, pitchMax = 1.12f, maxVoices = 4, minInterval = 0.05f } },
        { "arrow_hit",     new EventDef { folder = "arrow_hit", volume = 0.4f, pitchMin = 0.85f, pitchMax = 1.15f, maxVoices = 2, minInterval = 0.1f, priority = 190 } },
        { "scream",        new EventDef { volume = 0.2f,  pitchMin = 0.92f, pitchMax = 1.1f,  maxVoices = 1, minInterval = 3f, priority = 230 } },
        { "unit_death",    new EventDef { folder = "unit_death", volume = 0.55f, pitchMin = 0.85f, pitchMax = 1.1f, maxVoices = 3, minInterval = 0.08f } },
        { "unit_deploy",   new EventDef { folder = "unit_deploy", volume = 0.6f, pitchMin = 0.95f, pitchMax = 1.1f, maxVoices = 2, minInterval = 0.12f, priority = 64 } },
        { "siege_fire",    new EventDef { volume = 0.75f, pitchMin = 0.9f, pitchMax = 1.05f, maxVoices = 2, minInterval = 0.15f, priority = 80 } },
        { "siege_impact",  new EventDef { folder = "stone_impact", volume = 0.8f, pitchMin = 0.85f, pitchMax = 1.05f, maxVoices = 2, minInterval = 0.12f, layer = false, priority = 80 } },
        { "machine_hit",   new EventDef { folder = "wood_smash", volume = 0.55f, pitchMin = 0.9f, pitchMax = 1.1f, maxVoices = 2, minInterval = 0.1f } },

        // --- towers
        { "tower_shoot",     new EventDef { folder = "arrow_release", volume = 0.4f, pitchMin = 0.95f, pitchMax = 1.1f, maxVoices = 2, minInterval = 0.15f, priority = 170 } },
        { "tower_land",      new EventDef { volume = 0.5f, pitchMin = 1.0f,  pitchMax = 1.2f,  maxVoices = 2, minInterval = 0.1f } },
        { "tower_capture",   new EventDef { volume = 0.9f, pitchMin = 1f, pitchMax = 1f, maxVoices = 1, minInterval = 1.5f, priority = 20, ui = true, duck = 0.25f, duckTime = 3f } },
        { "tower_destroyed", new EventDef { folder = "big_explosion", volume = 1f, pitchMin = 0.85f, pitchMax = 0.95f, maxVoices = 2, minInterval = 0.3f, layer = false, priority = 10, duck = 0.45f, duckTime = 1.5f } },
        { "structure_hit",   new EventDef { folder = "wood_hit", volume = 0.6f, pitchMin = 0.8f, pitchMax = 1f, maxVoices = 2, minInterval = 0.1f, layer = false } },
        { "rubble",          new EventDef { folder = "rubble", volume = 0.8f, pitchMin = 0.8f, pitchMax = 0.95f, maxVoices = 2, minInterval = 0.3f, priority = 30 } },

        // --- powerups
        { "volley_release",  new EventDef { folder = "arrow_release", volume = 0.55f, pitchMin = 0.8f, pitchMax = 0.9f, maxVoices = 2, minInterval = 0.12f, priority = 40 } },
        { "fireball_launch", new EventDef { volume = 0.85f, pitchMin = 1f, pitchMax = 1f, maxVoices = 2, minInterval = 0.1f, priority = 40 } },
        { "fireball_explode",new EventDef { folder = "big_explosion", volume = 1f, pitchMin = 0.8f, pitchMax = 0.9f, maxVoices = 2, minInterval = 0.1f, layer = false, priority = 10, duck = 0.5f, duckTime = 1.2f } },
        { "fence_place",     new EventDef { folder = "fence_place", volume = 0.9f, pitchMin = 0.9f, pitchMax = 1.05f, maxVoices = 2, minInterval = 0.08f, priority = 40 } },
        { "wood_hit",        new EventDef { folder = "wood_hit", volume = 0.6f, pitchMin = 0.9f, pitchMax = 1.1f, maxVoices = 2, minInterval = 0.08f } },
        { "wood_smash",      new EventDef { folder = "wood_smash", volume = 0.85f, pitchMin = 0.85f, pitchMax = 1.05f, maxVoices = 2, minInterval = 0.08f, priority = 50 } },
        { "log_roll",        new EventDef { volume = 0.8f, pitchMin = 1f, pitchMax = 1f, maxVoices = 1, minInterval = 0.2f, priority = 50 } },
        { "stone_throw",     new EventDef { volume = 0.7f, pitchMin = 0.85f, pitchMax = 1f, maxVoices = 2, minInterval = 0.08f } },
        { "stone_impact",    new EventDef { folder = "stone_impact", volume = 0.85f, pitchMin = 0.85f, pitchMax = 1.1f, maxVoices = 3, minInterval = 0.06f, layer = false, priority = 50 } },
        { "trap_snap",       new EventDef { folder = "trap_snap", volume = 0.9f, pitchMin = 0.95f, pitchMax = 1.05f, maxVoices = 2, minInterval = 0.08f, priority = 40 } },
        { "trap_strike",     new EventDef { folder = "trap_strike", volume = 0.7f, pitchMin = 0.9f, pitchMax = 1.1f, maxVoices = 2, minInterval = 0.08f } },
        { "shield_up",       new EventDef { folder = "shield_up", volume = 0.75f, pitchMin = 1.15f, pitchMax = 1.25f, maxVoices = 1, minInterval = 0.3f, layer = false, priority = 40 } },
        { "tower_build",     new EventDef { folder = "tower_build", volume = 0.9f, pitchMin = 0.9f, pitchMax = 1f, maxVoices = 2, minInterval = 0.2f, layer = false, priority = 40 } },
        { "cart_horn",       new EventDef { volume = 0.9f, pitchMin = 1f, pitchMax = 1f, maxVoices = 1, minInterval = 0.5f, priority = 30 } },
        { "cart_rattle",     new EventDef { folder = "cart_rattle", volume = 0.6f, pitchMin = 0.9f, pitchMax = 1.05f, maxVoices = 1, minInterval = 0.4f } },
        { "powerup",         new EventDef { volume = 0.9f, pitchMin = 0.97f, pitchMax = 1.03f, maxVoices = 2, minInterval = 0.05f, priority = 40 } },

        // --- battle flow
        { "banner_gong",     new EventDef { folder = "banner_gong", volume = 0.7f, pitchMin = 1f, pitchMax = 1f, maxVoices = 1, minInterval = 1f, ui = true, priority = 5 } },
        { "final_gong",      new EventDef { folder = "final_gong", volume = 0.8f, pitchMin = 0.9f, pitchMax = 0.9f, maxVoices = 1, minInterval = 1f, ui = true, priority = 5 } },
        { "countdown_tick",  new EventDef { folder = "countdown_tick", volume = 0.7f, pitchMin = 1f, pitchMax = 1f, maxVoices = 1, minInterval = 0.5f, ui = true, priority = 5 } },
        { "card_select",     new EventDef { folder = "card_select", volume = 0.6f, pitchMin = 0.95f, pitchMax = 1.08f, maxVoices = 1, minInterval = 0.05f, ui = true } },
        { "card_play",       new EventDef { folder = "card_play", volume = 0.7f, pitchMin = 0.95f, pitchMax = 1.05f, maxVoices = 1, minInterval = 0.08f, ui = true } },
        { "result",          new EventDef { volume = 1f, pitchMin = 1f, pitchMax = 1f, maxVoices = 2, minInterval = 0f, ui = true, priority = 0, duck = 0.15f, duckTime = 4f } },
        { "victory_jingle",  new EventDef { folder = "victory_jingle", volume = 0.9f, pitchMin = 1f, pitchMax = 1f, maxVoices = 1, minInterval = 1f, ui = true, priority = 0, duck = 0.15f, duckTime = 4f } },
        { "defeat_jingle",   new EventDef { folder = "defeat_jingle", volume = 0.9f, pitchMin = 1f, pitchMax = 1f, maxVoices = 1, minInterval = 1f, ui = true, priority = 0, duck = 0.15f, duckTime = 4f } },
        { "star_pop",        new EventDef { volume = 0.9f, pitchMin = 1f, pitchMax = 1f, maxVoices = 3, minInterval = 0f, ui = true, priority = 10 } },

        // --- menus / meta
        { "ui",            new EventDef { volume = 1f, pitchMin = 0.97f, pitchMax = 1.03f, maxVoices = 3, minInterval = 0.03f, ui = true } },
        { "chest",         new EventDef { volume = 1f, pitchMin = 1f, pitchMax = 1f, maxVoices = 3, minInterval = 0.03f, ui = true, priority = 10 } },
        { "shop",          new EventDef { volume = 1f, pitchMin = 0.98f, pitchMax = 1.02f, maxVoices = 3, minInterval = 0.03f, ui = true } },
        { "coins",         new EventDef { folder = "coins", volume = 0.8f, pitchMin = 0.95f, pitchMax = 1.08f, maxVoices = 2, minInterval = 0.08f, ui = true } },
        { "reward_pop",    new EventDef { folder = "reward_pop", volume = 0.7f, pitchMin = 0.98f, pitchMax = 1.05f, maxVoices = 2, minInterval = 0.06f, ui = true } },
        { "reward_jingle", new EventDef { folder = "reward_jingle", volume = 0.85f, pitchMin = 1f, pitchMax = 1f, maxVoices = 1, minInterval = 0.5f, ui = true, duck = 0.3f, duckTime = 2f } },
        { "ui_error",      new EventDef { folder = "ui_error", volume = 0.6f, pitchMin = 1f, pitchMax = 1f, maxVoices = 1, minInterval = 0.15f, ui = true } },
    };

    const string LibraryRoot = "Audio/KoS/";
    const string MenuMusic = "Audio/KoS/music/menu_town_theme";
    const string BattleMusic = "Audio/KoS/music/battle_theme_a";
    const int VoiceCount = 24;
    const float MenuMusicLevel = 0.45f;
    const float BattleMusicLevel = 0.35f;

    // ---------------------------------------------------------------- settings

    public static float MusicVolume
    {
        get => s_musicVol;
        set { s_musicVol = Mathf.Clamp01(value); PlayerPrefs.SetFloat("KoS_MusicVol", s_musicVol); }
    }

    public static float SfxVolume
    {
        get => s_sfxVol;
        set { s_sfxVol = Mathf.Clamp01(value); PlayerPrefs.SetFloat("KoS_SfxVol", s_sfxVol); }
    }

    static float s_musicVol = 1f, s_sfxVol = 1f;

    public static KoSAudio Instance { get; private set; }

    // ---------------------------------------------------------------- state

    class Voice
    {
        public AudioSource src;
        public string key;
        public int priority;
        public float endTime;
    }

    readonly List<Voice> voices = new List<Voice>(VoiceCount);
    readonly Dictionary<string, float> lastPlay = new Dictionary<string, float>();
    readonly Dictionary<string, AudioClip[]> library = new Dictionary<string, AudioClip[]>();
    readonly Dictionary<string, int> lastVariant = new Dictionary<string, int>();
    static readonly EventDef DefaultDef = new EventDef { volume = 1f, pitchMin = 0.97f, pitchMax = 1.03f, maxVoices = 3, minInterval = 0.04f };

    AudioSource musicA, musicB;
    bool musicOnA = true;
    string currentMusic;
    float musicLevel;          // target level of the current track (before user volume)
    float duckLevel = 1f, duckUntil;
    float duckCurrent = 1f;
    float fadeT = 1f, fadeDuration = 1.5f;
    float musicPitchTarget = 1f;
    float fadeOutFrom;

    // ---------------------------------------------------------------- bootstrap

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("KoSAudio");
        DontDestroyOnLoad(go);
        go.AddComponent<KoSAudio>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        s_musicVol = PlayerPrefs.GetFloat("KoS_MusicVol", 1f);
        s_sfxVol = PlayerPrefs.GetFloat("KoS_SfxVol", 1f);

        for (int i = 0; i < VoiceCount; i++)
        {
            var go = new GameObject("Voice" + i);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f; // panning is done by screen position (see Play)
            s.dopplerLevel = 0f;
            voices.Add(new Voice { src = s });
        }

        musicA = MakeMusicSource("MusicA");
        musicB = MakeMusicSource("MusicB");
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    AudioSource MakeMusicSource(string n)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = true;
        s.spatialBlend = 0f;
        s.priority = 0;
        s.ignoreListenerPause = true;
        s.volume = 0f;
        return s;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive) return;
        lastPlay.Clear();
        musicPitchTarget = 1f;
        string n = scene.name.ToLowerInvariant();
        if (n.Contains("menu")) PlayMusic(MenuMusic, MenuMusicLevel);
        else if (n.Contains("battle") || n.Contains("sample")) PlayMusic(BattleMusic, BattleMusicLevel);
        muteScanUntil = Time.unscaledTime + 3f;
        MuteOldMusic();
    }

    float muteScanUntil, nextMuteScan;

    /// <summary>
    /// Stops music/ambience that scenes play on their own AudioSources (clips longer than 20 s),
    /// so it never plays on top of the KoSAudio music. Short one-shot SFX sources are left alone.
    /// </summary>
    void MuteOldMusic()
    {
        if (currentMusic == null) return;
        foreach (var a in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (a == null || a.transform.IsChildOf(transform)) continue;
            bool musicLike = a.clip != null && a.clip.length > 20f;
            if (!musicLike) continue;
            if (a.isPlaying) a.Stop();
            a.playOnAwake = false;
            a.mute = true;
        }
    }

    // ---------------------------------------------------------------- public API

    /// <summary>Plays a library event at a world position (Vector3.zero / ui events play in 2D).</summary>
    public static void Play(string id, Vector3 position, float volume = 1f)
    {
        if (Instance == null) return;
        Instance.PlayInternal(id, null, position, volume, 1f, true);
    }

    /// <summary>Plays a 2D library event (UI, banners, jingles).</summary>
    public static void Play2D(string id, float volume = 1f)
    {
        if (Instance == null) return;
        Instance.PlayInternal(id, null, Vector3.zero, volume, 1f, false);
    }

    /// <summary>
    /// Drop-in for audioSource.PlayOneShot(clip, volume). Keeps the source's pitch as the base pitch,
    /// applies the event's mixing rules, and swaps in / layers library sounds for the event.
    /// </summary>
    public static void PlayOneShot(AudioSource source, AudioClip clip, float volume = 1f, string id = null)
    {
        float basePitch = source != null ? source.pitch : 1f;
        if (Instance == null)
        {
            if (source != null && clip != null) source.PlayOneShot(clip, volume);
            return;
        }
        bool world = source != null && source.spatialBlend > 0.01f;
        Vector3 pos = source != null ? source.transform.position : Vector3.zero;
        Instance.PlayInternal(id, clip, pos, volume, basePitch, world);
    }

    /// <summary>Drop-in for AudioSource.PlayClipAtPoint.</summary>
    public static void PlayClipAtPoint(AudioClip clip, Vector3 position, float volume = 1f, string id = null, float pitch = 1f)
    {
        if (Instance == null)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, position, volume);
            return;
        }
        Instance.PlayInternal(id, clip, position, volume, pitch, true);
    }

    /// <summary>Lowers the music to level (0..1) for the given seconds.</summary>
    public static void DuckMusic(float level, float seconds)
    {
        if (Instance == null) return;
        Instance.duckLevel = Mathf.Min(Instance.duckUntil > Time.unscaledTime ? Instance.duckLevel : 1f, level);
        Instance.duckUntil = Mathf.Max(Instance.duckUntil, Time.unscaledTime + seconds);
    }

    /// <summary>1 = normal; slightly above 1 speeds the music up for the final countdown.</summary>
    public static void SetMusicIntensity(float pitch)
    {
        if (Instance != null) Instance.musicPitchTarget = Mathf.Clamp(pitch, 0.8f, 1.2f);
    }

    /// <summary>Fades out the music (e.g. when the match ends).</summary>
    public static void StopMusic(float fadeSeconds = 1f)
    {
        if (Instance == null) return;
        var k = Instance;
        if (k.currentMusic == null) return;
        k.currentMusic = null;
        k.fadeOutFrom = (k.musicOnA ? k.musicA : k.musicB).volume;
        k.musicOnA = !k.musicOnA;
        var silent = k.musicOnA ? k.musicA : k.musicB;
        silent.Stop();
        silent.clip = null;
        k.fadeDuration = Mathf.Max(0.05f, fadeSeconds);
        k.fadeT = 0f;
    }

    public static bool HasLibrary(string id)
    {
        if (Instance == null || id == null || !Events.TryGetValue(id, out var d) || d.folder == null) return false;
        var c = Instance.GetLibrary(d.folder);
        return c != null && c.Length > 0;
    }

    // ---------------------------------------------------------------- core

    void PlayInternal(string id, AudioClip fallback, Vector3 pos, float volume, float basePitch, bool world)
    {
        EventDef def = DefaultDef;
        if (id != null && Events.TryGetValue(id, out var d)) def = d;
        if (def.ui) world = false;

        AudioClip lib = def.folder != null ? PickVariant(def.folder) : null;
        string key = id ?? (fallback != null ? "clip:" + fallback.GetInstanceID() : null);
        if (key == null) return;

        float now = Time.unscaledTime;
        if (lastPlay.TryGetValue(key, out float last) && now - last < def.minInterval) return;

        // Count voices of this event still playing; enforce limit and density falloff.
        int active = 0;
        for (int i = 0; i < voices.Count; i++)
            if (voices[i].key == key && voices[i].endTime > now) active++;
        if (active >= def.maxVoices) return;
        float density = 1f / Mathf.Sqrt(1f + active);

        lastPlay[key] = now;

        float pitchJitter = Random.Range(def.pitchMin, def.pitchMax);
        // Callers that set their own pitch keep it; library clips use the event's range only.
        float pitch = (basePitch > 0.01f && Mathf.Abs(basePitch - 1f) > 0.001f) ? basePitch * Mathf.Lerp(1f, pitchJitter, 0.5f) : pitchJitter;

        float pan = 0f, screenAtten = 1f;
        if (world) ScreenMix(pos, out pan, out screenAtten);

        float vol = volume * def.volume * density * screenAtten * s_sfxVol;
        if (vol <= 0.001f) return;

        if (lib != null)
        {
            Emit(key, lib, vol, pitchJitter, pan, def.priority);
            if (def.layer && fallback != null) Emit(key, fallback, vol * 0.8f, pitch, pan, def.priority + 1);
        }
        else if (fallback != null)
        {
            Emit(key, fallback, vol, pitch, pan, def.priority);
        }
        else return;

        if (def.duck > 0f) DuckMusic(def.duck, def.duckTime);
    }

    void Emit(string key, AudioClip clip, float vol, float pitch, float pan, int priority)
    {
        if (clip == null) return;
        float now = Time.unscaledTime;
        Voice v = null;
        // Free voice first
        for (int i = 0; i < voices.Count; i++)
            if (voices[i].endTime <= now || !voices[i].src.isPlaying) { v = voices[i]; break; }
        // Otherwise steal the least important / oldest voice
        if (v == null)
        {
            Voice worst = null;
            for (int i = 0; i < voices.Count; i++)
            {
                var c = voices[i];
                if (worst == null || c.priority > worst.priority || (c.priority == worst.priority && c.endTime < worst.endTime)) worst = c;
            }
            if (worst == null || worst.priority < priority) return; // everything playing is more important
            v = worst;
        }
        var s = v.src;
        s.Stop();
        s.clip = clip;
        s.volume = Mathf.Clamp01(vol);
        s.pitch = Mathf.Clamp(pitch, 0.3f, 3f);
        s.panStereo = pan;
        s.priority = Mathf.Clamp(priority, 0, 256);
        s.Play();
        v.key = key;
        v.priority = priority;
        v.endTime = now + clip.length / Mathf.Max(0.3f, s.pitch);
    }

    static void ScreenMix(Vector3 pos, out float pan, out float atten)
    {
        pan = 0f; atten = 1f;
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 vp = cam.WorldToViewportPoint(pos);
        if (vp.z < 0f) { atten = 0.35f; return; }
        pan = Mathf.Clamp((vp.x - 0.5f) * 0.7f, -0.35f, 0.35f);
        // How far outside the visible screen the sound is (0 = on screen)
        float dx = Mathf.Max(0f, Mathf.Abs(vp.x - 0.5f) - 0.5f);
        float dy = Mathf.Max(0f, Mathf.Abs(vp.y - 0.5f) - 0.5f);
        float off = Mathf.Sqrt(dx * dx + dy * dy);
        atten = Mathf.Lerp(1f, 0.3f, Mathf.Clamp01(off / 0.6f));
    }

    AudioClip PickVariant(string folder)
    {
        var clips = GetLibrary(folder);
        if (clips == null || clips.Length == 0) return null;
        if (clips.Length == 1) return clips[0];
        lastVariant.TryGetValue(folder, out int prev);
        int i = Random.Range(0, clips.Length - 1);
        if (i >= prev) i++; // never repeat the same variant twice in a row
        lastVariant[folder] = i;
        return clips[i];
    }

    AudioClip[] GetLibrary(string folder)
    {
        if (!library.TryGetValue(folder, out var clips))
        {
            clips = Resources.LoadAll<AudioClip>(LibraryRoot + folder);
            library[folder] = clips;
        }
        return clips;
    }

    // ---------------------------------------------------------------- music

    void PlayMusic(string resourcePath, float level)
    {
        if (currentMusic == resourcePath) { musicLevel = level; return; }
        var clip = Resources.Load<AudioClip>(resourcePath);
        if (clip == null) return;
        currentMusic = resourcePath;
        musicLevel = level;
        fadeOutFrom = (musicOnA ? musicA : musicB).volume;
        musicOnA = !musicOnA;
        var incoming = musicOnA ? musicA : musicB;
        incoming.clip = clip;
        incoming.volume = 0f;
        incoming.pitch = 1f;
        incoming.Play();
        fadeDuration = 1.5f;
        fadeT = 0f;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (Time.unscaledTime < muteScanUntil && Time.unscaledTime >= nextMuteScan) { nextMuteScan = Time.unscaledTime + 0.5f; MuteOldMusic(); }
        fadeT = Mathf.Min(1f, fadeT + dt / fadeDuration);

        float targetDuck = Time.unscaledTime < duckUntil ? duckLevel : 1f;
        // Duck down fast, recover slowly
        duckCurrent = Mathf.MoveTowards(duckCurrent, targetDuck, dt * (targetDuck < duckCurrent ? 4f : 0.6f));

        var incoming = musicOnA ? musicA : musicB;
        var outgoing = musicOnA ? musicB : musicA;
        float master = s_musicVol * duckCurrent;
        incoming.volume = musicLevel * master * fadeT;
        outgoing.volume = fadeOutFrom * (1f - fadeT);
        if (fadeT >= 1f && outgoing.isPlaying) outgoing.Stop();

        incoming.pitch = Mathf.MoveTowards(incoming.pitch, musicPitchTarget, dt * 0.05f);
    }
}
