using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Adds every shader the battle scripts look up with Shader.Find to
// Graphics Settings > Always Included Shaders, so Android builds keep them.
public static class AlwaysIncludeRuntimeShaders
{
    static readonly string[] Names =
    {
        "Universal Render Pipeline/Unlit",
        "Universal Render Pipeline/Lit",
        "Sprites/Default",
        "Standard",
        "Particles/Standard Unlit",
        "Mobile/Particles/Additive",
        "Mobile/Particles/Alpha Blended",
        "Unlit/Color",
        "Unlit/Transparent",
        "Legacy Shaders/Transparent/Diffuse",
        "Hidden/Kino/Bloom"
    };

    [MenuItem("Tools/Build/Always Include Runtime Shaders")]
    public static void Run()
    {
        var so = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
        var arr = so.FindProperty("m_AlwaysIncludedShaders");
        int added = 0;
        foreach (var n in Names)
        {
            var s = Shader.Find(n);
            if (s == null) { Debug.LogWarning("[AlwaysInclude] Not found: " + n); continue; }
            bool present = false;
            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == s) { present = true; break; }
            if (present) continue;
            arr.arraySize++;
            arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = s;
            added++;
        }
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Debug.Log("[AlwaysInclude] Added " + added + " shaders.");
    }
}
