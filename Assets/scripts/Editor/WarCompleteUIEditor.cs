using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom Inspector for WarCompleteUIController providing instant 1-click buttons
/// to test the ultra-juicy DOTween Victory and Defeat animations in Play Mode OR directly in Edit Mode,
/// plus a 1-click Auto Layout button to snap and center all elements perfectly.
/// </summary>
[CustomEditor(typeof(WarCompleteUIController))]
public class WarCompleteUIEditor : Editor
{
    public override void OnInspectorGUI()
    {
        WarCompleteUIController controller = (WarCompleteUIController)target;

        EditorGUILayout.Space(8);

        // Auto Layout Section
        GUI.backgroundColor = new Color(0.25f, 0.7f, 1f, 1f); // Vibrant Cyan
        if (GUILayout.Button("🎯 Snap & Center All Elements (Auto Layout)", GUILayout.Height(36)))
        {
            Undo.RecordObject(controller.gameObject, "Auto Layout WarComplete UI");
            controller.AutoLayoutUI();
            EditorUtility.SetDirty(controller);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("🎬 DOTween Animation Tester (Works in Edit Mode & Play Mode!)", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.2f, 0.9f, 0.45f, 1f); // Vibrant Green
        if (GUILayout.Button("🏆 Test VICTORY (3 Stars)", GUILayout.Height(36)))
        {
            DOTweenEditModePreview.StartPreview(10f);
            controller.PlayWarCompleteAnimation(true, 3, true);
        }

        GUI.backgroundColor = new Color(1f, 0.85f, 0.2f, 1f); // Gold
        if (GUILayout.Button("⭐ Test (2 Stars)", GUILayout.Height(36)))
        {
            DOTweenEditModePreview.StartPreview(10f);
            controller.PlayWarCompleteAnimation(true, 2, true);
        }

        GUI.backgroundColor = new Color(0.9f, 0.75f, 0.3f, 1f); // Amber
        if (GUILayout.Button("⭐ Test (1 Star)", GUILayout.Height(36)))
        {
            DOTweenEditModePreview.StartPreview(10f);
            controller.PlayWarCompleteAnimation(true, 1, true);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(1f, 0.3f, 0.35f, 1f); // Crimson Red
        if (GUILayout.Button("💀 Test DEFEAT", GUILayout.Height(32)))
        {
            DOTweenEditModePreview.StartPreview(8f);
            controller.PlayWarCompleteAnimation(false, 0, true);
        }

        GUI.backgroundColor = new Color(0.55f, 0.58f, 0.65f, 1f); // Slate
        if (GUILayout.Button("🔄 Reset / Hide Modal", GUILayout.Height(32)))
        {
            DOTweenEditModePreview.StopPreview();
            controller.ResetModal();
        }
        EditorGUILayout.EndHorizontal();

        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(8);

        DrawDefaultInspector();
    }
}
