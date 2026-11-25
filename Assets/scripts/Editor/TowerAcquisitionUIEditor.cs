#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TowerAcquisitionUI))]
public class TowerAcquisitionUIEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        TowerAcquisitionUI ui = (TowerAcquisitionUI)target;
        if (ui == null) return;

        EditorGUILayout.Space(14);
        EditorGUILayout.LabelField("Live DOTween HUD Preview Controls", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.2f, 0.7f, 1f, 1f);
        if (GUILayout.Button("▶ Preview Player (Blue)", GUILayout.Height(36)))
        {
            ui.PreviewToastPlayer();
            DOTweenEditModePreview.StartPreview(4f);
        }

        GUI.backgroundColor = new Color(1f, 0.35f, 0.35f, 1f);
        if (GUILayout.Button("▶ Preview Enemy (Red)", GUILayout.Height(36)))
        {
            ui.PreviewToastEnemy();
            DOTweenEditModePreview.StartPreview(4f);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.3f, 0.85f, 0.45f, 1f);
        if (GUILayout.Button("👁 Show in Scene (Edit Mode)", GUILayout.Height(28)))
        {
            Undo.RecordObject(ui.gameObject, "Show Toast in Scene");
            ui.ShowInSceneEditMode();
            EditorUtility.SetDirty(ui.gameObject);
        }

        GUI.backgroundColor = new Color(0.7f, 0.7f, 0.75f, 1f);
        if (GUILayout.Button("✕ Hide (Gameplay Ready)", GUILayout.Height(28)))
        {
            Undo.RecordObject(ui.gameObject, "Hide Toast for Gameplay");
            ui.HideToastImmediate();
            EditorUtility.SetDirty(ui.gameObject);
        }

        EditorGUILayout.EndHorizontal();
        GUI.backgroundColor = Color.white;
    }
}
#endif
