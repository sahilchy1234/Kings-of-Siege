using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class WarCompleteSetupTool
{
    // Trigger via Unity Editor menu: Tools -> WarComplete -> Snap & Align War Complete UI Layout
    [MenuItem("Tools/WarComplete/Snap & Align War Complete UI Layout")]
    public static void SnapAndAlignUI()
    {
        // 1. Align Scene Instance
        var scene = EditorSceneManager.GetActiveScene();
        WarCompleteUIController wc = Object.FindFirstObjectByType<WarCompleteUIController>(FindObjectsInactive.Include);
        if (wc != null)
        {
            Undo.RecordObject(wc.gameObject, "Align WarComplete UI");
            wc.AutoLayoutUI();
            EditorUtility.SetDirty(wc.gameObject);
            EditorUtility.SetDirty(wc);

            // Connect GameManager
            GameManager gm = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
            if (gm != null)
            {
                SerializedObject soGM = new SerializedObject(gm);
                SerializedProperty spWC = soGM.FindProperty("warCompleteUI");
                if (spWC != null)
                {
                    spWC.objectReferenceValue = wc;
                    soGM.ApplyModifiedProperties();
                    EditorUtility.SetDirty(gm);
                    Debug.Log("[WarCompleteSetupTool] ✓ Linked WarCompleteUIController to GameManager in Scene.");
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[WarCompleteSetupTool] ✓ WarComplete UI successfully snapped, aligned, and saved in active Scene!");
        }
        else
        {
            Debug.LogWarning("[WarCompleteSetupTool] No WarCompleteUIController found in active scene.");
        }

        // 2. Align Prefab Instance
        string prefabPath = "Assets/UIPACK/Prefabs/Prefabs_DemoScene_Panels/Reward.prefab";
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        if (prefabRoot != null)
        {
            try
            {
                WarCompleteUIController prefabWC = prefabRoot.GetComponent<WarCompleteUIController>();
                if (prefabWC == null) prefabWC = prefabRoot.AddComponent<WarCompleteUIController>();

                prefabWC.AutoLayoutUI();
                EditorUtility.SetDirty(prefabRoot);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                Debug.Log("[WarCompleteSetupTool] ✓ Reward.prefab aligned and saved successfully!");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        AssetDatabase.SaveAssets();
    }
}
