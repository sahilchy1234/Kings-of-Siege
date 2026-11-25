#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TowerWorldBanner))]
public class TowerWorldBannerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        TowerWorldBanner banner = (TowerWorldBanner)target;
        if (banner == null) return;

        EditorGUILayout.Space(14);
        EditorGUILayout.LabelField("Live 3D Banner Preview Controls", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.2f, 0.7f, 1f, 1f);
        if (GUILayout.Button("▶ Preview Player Banner (Blue)", GUILayout.Height(36)))
        {
            banner.PreviewPlayer();
            DOTweenEditModePreview.StartPreview(4.5f);
        }

        GUI.backgroundColor = new Color(1f, 0.35f, 0.35f, 1f);
        if (GUILayout.Button("▶ Preview Enemy Banner (Red)", GUILayout.Height(36)))
        {
            banner.PreviewEnemy();
            DOTweenEditModePreview.StartPreview(4.5f);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.3f, 0.85f, 0.45f, 1f);
        if (GUILayout.Button("👁 Show Banner (Edit Mode)", GUILayout.Height(28)))
        {
            Undo.RecordObject(banner.gameObject, "Show Banner in Scene");
            banner.ShowInSceneEditMode();
            EditorUtility.SetDirty(banner.gameObject);
        }

        GUI.backgroundColor = new Color(0.7f, 0.7f, 0.75f, 1f);
        if (GUILayout.Button("✕ Hide Banner (Gameplay Ready)", GUILayout.Height(28)))
        {
            Undo.RecordObject(banner.gameObject, "Hide Banner for Gameplay");
            banner.HideImmediate();
            EditorUtility.SetDirty(banner.gameObject);
        }

        EditorGUILayout.EndHorizontal();
        GUI.backgroundColor = Color.white;
    }
}
#endif
