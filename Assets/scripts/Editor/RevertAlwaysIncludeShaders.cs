using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Trims Always Included Shaders back to the 6 entries the project started with.
// AlwaysIncludeRuntimeShaders appended its shaders at the end, so this removes them.
public static class RevertAlwaysIncludeShaders
{
    const int OriginalCount = 6;

    [MenuItem("Tools/Build/Revert Always Include Runtime Shaders")]
    public static void Run()
    {
        var so = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
        var arr = so.FindProperty("m_AlwaysIncludedShaders");
        int before = arr.arraySize;
        if (before > OriginalCount)
            arr.arraySize = OriginalCount;
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Debug.Log("[RevertAlwaysInclude] " + before + " -> " + arr.arraySize + " entries.");
    }
}
