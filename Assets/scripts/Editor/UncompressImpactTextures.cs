using UnityEditor;
using UnityEngine;

// Sets the Android override of impact/VFX particle textures to uncompressed RGBA32 so
// ASTC compression can't make the arrow and stone impact particles invisible on device.
public static class UncompressImpactTextures
{
    static readonly string[] Roots =
    {
        "Assets/PolygonParticles",
        "Assets/JMO Assets/Cartoon FX Remaster",
    };

    [MenuItem("Tools/Build/Uncompress Impact Textures (Android)")]
    public static void Run()
    {
        int changed = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", Roots))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) continue;
            var android = imp.GetPlatformTextureSettings("Android");
            if (android.overridden && android.format == TextureImporterFormat.RGBA32) continue;
            android.overridden = true;
            android.format = TextureImporterFormat.RGBA32;
            android.maxTextureSize = Mathf.Max(android.maxTextureSize, 512);
            imp.SetPlatformTextureSettings(android);
            imp.SaveAndReimport();
            changed++;
        }
        Debug.Log("[UncompressImpact] Set " + changed + " textures to RGBA32 on Android.");
    }
}
