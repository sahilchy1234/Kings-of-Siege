#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editor setup utility to ensure GameManager and SmartAIPlayer exist in BattleScene
/// with seamless automatic configuration and wiring.
/// </summary>
public static class GameManagerSceneSetup
{
    [MenuItem("Tools/SEIGE/Setup Smart AI & Game Manager in Scene", false, 10)]
    public static void SetupInScene()
    {
        // 1. Setup GameManager
        GameObject gmObj = GameObject.Find("[GameManager]");
        if (gmObj == null)
        {
            gmObj = new GameObject("[GameManager]");
            Undo.RegisterCreatedObjectUndo(gmObj, "Create [GameManager]");
        }

        GameManager gm = gmObj.GetComponent<GameManager>();
        if (gm == null)
        {
            gm = Undo.AddComponent<GameManager>(gmObj);
        }

        // 2. Setup SmartAIPlayer
        GameObject aiObj = GameObject.Find("[SmartAIPlayer]");
        if (aiObj == null)
        {
            aiObj = new GameObject("[SmartAIPlayer]");
            Undo.RegisterCreatedObjectUndo(aiObj, "Create [SmartAIPlayer]");
        }

        SmartAIPlayer ai = aiObj.GetComponent<SmartAIPlayer>();
        if (ai == null)
        {
            ai = Undo.AddComponent<SmartAIPlayer>(aiObj);
        }

        // 3. Setup warcomplete UI & wire to GameManager
        GameObject warCompleteObj = GameObject.Find("warcomplete");
        if (warCompleteObj == null)
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                Transform found = canvas.transform.Find("warcomplete");
                if (found != null) warCompleteObj = found.gameObject;
            }
        }

        if (warCompleteObj != null)
        {
            WarCompleteUIController wcController = warCompleteObj.GetComponent<WarCompleteUIController>();
            if (wcController == null)
            {
                wcController = Undo.AddComponent<WarCompleteUIController>(warCompleteObj);
            }
            wcController.AutoFindReferences();

            SerializedObject gmSO = new SerializedObject(gm);
            SerializedProperty wcProp = gmSO.FindProperty("warCompleteUI");
            if (wcProp != null)
            {
                wcProp.objectReferenceValue = wcController;
                gmSO.ApplyModifiedProperties();
            }

            Debug.Log("<color=#00FFAA>[GameManagerSceneSetup] Connected 'warcomplete' panel to GameManager!</color>");
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("<color=#00FF99>[GameManagerSceneSetup] Successfully configured [GameManager], [SmartAIPlayer], and [warcomplete] in Scene!</color>");
    }
}
#endif

