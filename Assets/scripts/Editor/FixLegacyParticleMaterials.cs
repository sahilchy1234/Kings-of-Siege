using UnityEditor;
using UnityEngine;

// Legacy built-in particle shaders (Legacy Shaders/Particles/*) are not drawn by URP on device,
// so arrow/stone impact particles are invisible in the Android build. This swaps the
// PolygonParticles materials to URP's Particles/Unlit and copies the main texture and colour.
public static class FixLegacyParticleMaterials
{
    [MenuItem("Tools/Build/Fix Legacy Particle Materials (URP)")]
    public static void Run()
    {
        var urp = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (urp == null) { Debug.LogError("[FixLegacyParticles] URP Particles/Unlit not found."); return; }

        int changed = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/PolygonParticles/Materials" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == urp) continue;
            if (mat.shader == null || !mat.shader.name.StartsWith("Legacy Shaders/Particles")) continue;

            var mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            var color = mat.HasProperty("_TintColor") ? mat.GetColor("_TintColor")
                      : mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;

            mat.shader = urp;
            mat.SetTexture("_BaseMap", mainTex);
            mat.SetTexture("_MainTex", mainTex);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Surface", 1f);      // transparent
            mat.SetFloat("_Blend", 0f);        // alpha
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            changed++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[FixLegacyParticles] Converted " + changed + " materials to URP Particles/Unlit.");
    }
}
