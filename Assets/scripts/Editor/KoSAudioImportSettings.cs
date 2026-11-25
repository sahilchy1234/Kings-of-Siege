#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Phone-friendly import settings for every game sound, applied automatically on import.
/// - Short SFX: mono, Vorbis, decompressed on load (no CPU decode cost while playing).
/// - Long SFX / fanfares (over ~3 s): mono, Vorbis, compressed in memory.
/// - Music (Resources/Audio/KoS/music and long tracks): stereo Vorbis, streamed, not preloaded.
/// Menu: Kings of Siege > Audio > Reimport Sounds (Phone Settings) re-applies it to existing files.
/// </summary>
public class KoSAudioImportSettings : AssetPostprocessor
{
    static readonly string[] Folders =
    {
        "Assets/Resources/Audio",
        "Assets/Resources/Sounds",
        "Assets/Resources/Sound",
        "Assets/audioMohamad",
    };

    static bool IsManaged(string path)
    {
        foreach (var f in Folders)
            if (path.StartsWith(f + "/")) return true;
        return false;
    }

    void OnPreprocessAudio()
    {
        if (!IsManaged(assetPath)) return;
        var imp = (AudioImporter)assetImporter;

        long bytes = 0;
        try { bytes = new FileInfo(assetPath).Length; } catch { }
        string lower = assetPath.ToLowerInvariant();
        bool isMusic = lower.Contains("/kos/music/") || lower.Contains("music") || lower.Contains("theme")
                       || lower.Contains("trailer") || lower.Contains("cinematic");
        bool isLong = bytes > 150_000;

        var s = imp.defaultSampleSettings;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;

        if (isMusic)
        {
            imp.forceToMono = false;
            imp.loadInBackground = true;
            s.loadType = AudioClipLoadType.Streaming;
            s.quality = 0.45f;
            s.preloadAudioData = false;
        }
        else
        {
            imp.forceToMono = true;
            imp.loadInBackground = false;
            s.loadType = isLong ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.quality = 0.6f;
            s.preloadAudioData = true;
        }
        imp.defaultSampleSettings = s;

        // Android override: same as default but explicit, so platform overrides set by hand don't linger.
        imp.SetOverrideSampleSettings("Android", s);
    }

    [MenuItem("Kings of Siege/Audio/Reimport Sounds (Phone Settings)")]
    public static void ReimportAll()
    {
        int n = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            var existing = System.Array.FindAll(Folders, AssetDatabase.IsValidFolder);
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", existing))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsManaged(p)) continue;
                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
                n++;
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        Debug.Log("[KoSAudio] Reimported " + n + " sounds with phone settings.");
    }
}
#endif
