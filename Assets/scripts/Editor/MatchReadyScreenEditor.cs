using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Custom Editor & Tooling for MatchReadyScreen:
/// - Provides a one-click button to clean legacy UI panels (Header, VSContainer, BottomControls)
///   and activate the new portrait BattelVS component with DOTween animations.
/// - Automatically binds and marks scene dirty in BattleScene.
/// </summary>
[CustomEditor(typeof(MatchReadyScreen))]
public class MatchReadyScreenEditor : Editor
{
    [MenuItem("Tools/Siege/Clean Old Ready UI & Setup BattelVS", priority = 10)]
    public static void MenuCleanAndSetupBattelVS()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name != "BattleScene")
        {
            EditorUtility.DisplayDialog("Setup BattelVS", "Please open 'BattleScene' before running this command.", "OK");
            return;
        }

        MatchReadyScreen screen = Object.FindFirstObjectByType<MatchReadyScreen>();
        if (screen == null)
        {
            GameObject go = new GameObject("MatchReadyScreen");
            screen = go.AddComponent<MatchReadyScreen>();
            Undo.RegisterCreatedObjectUndo(go, "Create MatchReadyScreen");
        }

        screen.CleanOldMatchReadyElements();
        EditorUtility.SetDirty(screen);
        EditorSceneManager.MarkSceneDirty(activeScene);

        EditorUtility.DisplayDialog("BattelVS Activated!", 
            "✓ Old UI elements (Header, VSContainer, BottomControls) have been removed.\n" +
            "✓ BattelVS component is active and configured with DOTween.\n" +
            "✓ Portrait scaling & RectMask2D are enabled.", "Awesome!");
    }

    public override void OnInspectorGUI()
    {
        MatchReadyScreen screen = (MatchReadyScreen)target;
        SerializedProperty vsProp = serializedObject.FindProperty("battelVS");

        EditorGUILayout.Space(8);

        bool hasVS = vsProp != null && vsProp.objectReferenceValue != null;
        if (hasVS)
        {
            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.4f, 1f);
            EditorGUILayout.HelpBox("✓ BattelVS is ACTIVE & ANIMATED with DOTween for Portrait Mode!\nLegacy panels have been removed.", MessageType.Info);
        }
        else
        {
            GUI.backgroundColor = new Color(1f, 0.75f, 0.2f, 1f);
            EditorGUILayout.HelpBox("⚠ BattelVS is not bound yet. Click the button below to clean old UI and attach BattelVS!", MessageType.Warning);
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(4);

        GUI.backgroundColor = new Color(0.25f, 0.75f, 1f, 1f);
        if (GUILayout.Button("✨ Clean Old UI & Use BattelVS (Portrait + DOTween)", GUILayout.Height(36)))
        {
            screen.CleanOldMatchReadyElements();
            EditorUtility.SetDirty(screen);
            EditorSceneManager.MarkSceneDirty(screen.gameObject.scene);
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(4);

        if (GUILayout.Button("🔄 Update Scene HUD Player Names ('playername' & 'opponentname')", GUILayout.Height(28)))
        {
            screen.UpdateHUDPlayerNames();
            EditorUtility.SetDirty(screen);
        }

        EditorGUILayout.Space(8);

        DrawDefaultInspector();
    }
}
