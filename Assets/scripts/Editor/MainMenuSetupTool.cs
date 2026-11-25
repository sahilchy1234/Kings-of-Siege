#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 1-Click setup tool to configure and wire up MainMenuController on the 'Panel' UI in the MainMenu scene.
/// </summary>
public static class MainMenuSetupTool
{
    private const string BATTLE_CLICK_SOUND_PATH = "Assets/audioMohamad/dragon-studio-sword-slice-393847.mp3";
    private const string KING_TAP_SOUND_PATH = "Assets/audioMohamad/dragon-studio-hammer-smash-effect-382731.mp3";

    [MenuItem("Tools/Main Menu/Setup & Auto-Wire MainMenuController in Scene", false, 1)]
    public static void SetupMainMenuControllerInScene()
    {
        Debug.Log("<color=#00FFAA>[MainMenuSetupTool] Setting up MainMenuController...</color>");

        // 1. Locate Panel under Canvas
        GameObject panelObj = GameObject.Find("Canvas/Panel");
        if (panelObj == null)
        {
            panelObj = GameObject.Find("Panel");
        }

        if (panelObj == null)
        {
            Debug.LogError("[MainMenuSetupTool] Could not find GameObject named 'Panel' in the active scene.");
            EditorUtility.DisplayDialog("Panel Not Found", "Could not locate 'Panel' in the active scene. Please make sure the MainMenu scene is open.", "OK");
            return;
        }

        Undo.RecordObject(panelObj, "Setup MainMenuController");

        // 2. Add or Get MainMenuController
        MainMenuController controller = panelObj.GetComponent<MainMenuController>();
        if (controller == null)
        {
            controller = Undo.AddComponent<MainMenuController>(panelObj);
        }

        // 3. Ensure CanvasGroup on Panel
        CanvasGroup cg = panelObj.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = Undo.AddComponent<CanvasGroup>(panelObj);
        }

        // 4. Auto-Wire Hierarchy References
        controller.AutoWireHierarchy();

        // 5. Wire optional audio clips if present
        AudioClip battleSound = AssetDatabase.LoadAssetAtPath<AudioClip>(BATTLE_CLICK_SOUND_PATH);
        AudioClip kingSound = AssetDatabase.LoadAssetAtPath<AudioClip>(KING_TAP_SOUND_PATH);

        SerializedObject so = new SerializedObject(controller);
        if (battleSound != null)
        {
            SerializedProperty prop = so.FindProperty("battleClickSound");
            if (prop != null && prop.objectReferenceValue == null)
            {
                prop.objectReferenceValue = battleSound;
            }
        }

        if (kingSound != null)
        {
            SerializedProperty prop = so.FindProperty("kingTapSound");
            if (prop != null && prop.objectReferenceValue == null)
            {
                prop.objectReferenceValue = kingSound;
            }
        }
        so.ApplyModifiedProperties();

        // 6. Record Bedrock Resting Transforms
        controller.RecordRestingTransforms();

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(panelObj);
        EditorSceneManager.MarkSceneDirty(panelObj.scene);

        Debug.Log("<color=#00FFAA>[MainMenuSetupTool] SUCCESS: MainMenuController configured with bedrock layout and DOTween animations on 'Panel'!</color>");
        
        Selection.activeGameObject = panelObj;

        EditorUtility.DisplayDialog("Main Menu Controller Setup Complete",
            "MainMenuController is now attached to 'Panel' and configured!\n\n" +
            "• Top status bar slides down from the top\n" +
            "• Bottom navigation bar slides up from the bottom\n" +
            "• League banner drops with punchy bounce\n" +
            "• King character pops up and gently bobs with idle breathing\n" +
            "• BATTLE button pulses continuously and squashes on click\n" +
            "• Missions & Rewards buttons slide in with periodic attention wiggle\n" +
            "• Tapping the King makes him do a royal jump\n\n" +
            "You can test animations in Edit Mode using the Inspector buttons on 'Panel'!",
            "Awesome!");
    }

    [MenuItem("Tools/Main Menu/Preview Entrance Animation in Scene", false, 2)]
    public static void PreviewEntranceInScene()
    {
        MainMenuController controller = Object.FindFirstObjectByType<MainMenuController>();
        if (controller == null)
        {
            SetupMainMenuControllerInScene();
            controller = Object.FindFirstObjectByType<MainMenuController>();
        }

        if (controller != null)
        {
            DOTweenEditModePreview.StartPreview(8f);
            controller.EditorPreviewEntrance();
        }
    }
}
#endif

