#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editor utility that configures the 4 Power-ups system in the active scene:
/// Creates PowerupManager, Target Indicator, and PowerupUIBar on the Canvas with 1 click.
/// </summary>
public static class PowerupSystemSetup
{
    [MenuItem("Tools/SEIGE/Setup Powerups System in Active Scene", false, 10)]
    public static void SetupPowerupsInScene()
    {
        // 1. Setup PowerupManager
        PowerupManager manager = Object.FindFirstObjectByType<PowerupManager>();
        if (manager == null)
        {
            GameObject managerObj = new GameObject("[PowerupManager]");
            manager = managerObj.AddComponent<PowerupManager>();
            Undo.RegisterCreatedObjectUndo(managerObj, "Create PowerupManager");
        }

        // 2. Setup Target Indicator
        PowerupTargetIndicator indicator = Object.FindFirstObjectByType<PowerupTargetIndicator>();
        if (indicator == null)
        {
            GameObject indObj = new GameObject("[PowerupTargetIndicator]");
            indicator = indObj.AddComponent<PowerupTargetIndicator>();
            Undo.RegisterCreatedObjectUndo(indObj, "Create PowerupTargetIndicator");
        }

        SerializedObject managerSo = new SerializedObject(manager);
        SerializedProperty indProp = managerSo.FindProperty("targetIndicator");
        if (indProp != null)
        {
            indProp.objectReferenceValue = indicator;
            managerSo.ApplyModifiedProperties();
        }

        manager.PopulateDefaultPresets();

        // 3. Find Canvas
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas");
        }

        // 4. Setup PowerupUIBar under Canvas
        PowerupUIBar uiBar = canvas.GetComponentInChildren<PowerupUIBar>(true);
        if (uiBar == null)
        {
            GameObject barObj = new GameObject("[PowerupsBar]");
            barObj.transform.SetParent(canvas.transform, false);
            uiBar = barObj.AddComponent<PowerupUIBar>();
            Undo.RegisterCreatedObjectUndo(barObj, "Create PowerupUIBar");
        }

        // Setup active 4-category loadout displaying on the HUD
        uiBar.BuildUIHierarchy();

        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(uiBar);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("<color=#00FF88><b>[PowerupSystemSetup]</b> Successfully setup all 10 Powerups in the active scene!</color>");
    }

    [MenuItem("Tools/SEIGE/Setup 4 Classic Powerups on HUD", false, 11)]
    public static void SetupClassic4Powerups()
    {
        PowerupUIBar uiBar = Object.FindFirstObjectByType<PowerupUIBar>();
        if (uiBar != null)
        {
            uiBar.BuildUIHierarchy();
            EditorUtility.SetDirty(uiBar);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }

    [MenuItem("Tools/SEIGE/Setup All 10 Powerups on HUD", false, 12)]
    public static void SetupAll10Powerups()
    {
        SetupPowerupsInScene();
    }
}
#endif

