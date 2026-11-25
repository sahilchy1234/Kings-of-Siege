using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom Inspector for MainMenuController providing 1-click DOTween animation testing
/// that works both directly in Edit Mode (no Play Mode required!) and in Play Mode.
/// </summary>
[CustomEditor(typeof(MainMenuController))]
public class MainMenuControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        MainMenuController controller = (MainMenuController)target;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("🎬 DOTween Animation Tester (Works in Edit Mode & Play Mode!)", EditorStyles.boldLabel);

        // 1. Entrance & Exit Testing
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.2f, 0.75f, 1f, 1f); // Sky Blue
        if (GUILayout.Button("▶️ Play Entrance Animation", GUILayout.Height(34)))
        {
            DOTweenEditModePreview.StartPreview(8f);
            controller.EditorPreviewEntrance();
        }

        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f, 1f); // Coral Red
        if (GUILayout.Button("🚪 Test Exit Animation", GUILayout.Height(34)))
        {
            DOTweenEditModePreview.StartPreview(4f);
            controller.EditorPreviewExit();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // 2. Interactive Element Testing
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(1f, 0.85f, 0.2f, 1f); // Gold
        if (GUILayout.Button("⚔️ Test Battle Button Click", GUILayout.Height(32)))
        {
            DOTweenEditModePreview.StartPreview(4f);
            controller.EditorPreviewBattleClick();
        }

        GUI.backgroundColor = new Color(0.35f, 0.9f, 0.45f, 1f); // Emerald
        if (GUILayout.Button("👑 Test King Tap Jump", GUILayout.Height(32)))
        {
            DOTweenEditModePreview.StartPreview(4f);
            controller.EditorPreviewKingJump();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // 3. Idle Animations
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.6f, 0.85f, 1f, 1f);
        if (GUILayout.Button("🌀 Start Idle Breathing", GUILayout.Height(28)))
        {
            DOTweenEditModePreview.StartPreview(15f);
            controller.StartIdleAnimations();
        }

        GUI.backgroundColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        if (GUILayout.Button("⏸️ Stop Idle / Reset", GUILayout.Height(28)))
        {
            DOTweenEditModePreview.StopPreview();
            controller.ResetToRestingState();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        // 4. Hierarchy Auto-Wiring & Layout Locking
        EditorGUILayout.LabelField("🛠️ Hierarchy Configuration & Bedrock Layout", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.95f, 0.65f, 0.2f, 1f); // Amber
        if (GUILayout.Button("🔍 Auto-Wire UI References", GUILayout.Height(30)))
        {
            controller.AutoWireHierarchy();
            EditorUtility.SetDirty(controller);
        }

        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.6f, 1f); // Mint Green
        if (GUILayout.Button("💾 Lock Scene Layout As Resting", GUILayout.Height(30)))
        {
            controller.RecordRestingTransforms();
            EditorUtility.SetDirty(controller);
            Debug.Log("<color=#00FFAA>[MainMenuController] Current scene positions and scales locked as resting state!</color>");
        }
        EditorGUILayout.EndHorizontal();

        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(10);

        DrawDefaultInspector();
    }
}

