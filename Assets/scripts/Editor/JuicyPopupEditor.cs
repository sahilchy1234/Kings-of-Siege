#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(JuicyPopup))]
public class JuicyPopupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        JuicyPopup popup = (JuicyPopup)target;
        if (popup == null) return;

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Juicy Live Preview Controls", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.2f, 0.85f, 0.35f, 1f);
        if (GUILayout.Button("▶ Play Pop In", GUILayout.Height(36)))
        {
            popup.PlayPopIn();
        }

        GUI.backgroundColor = new Color(1f, 0.35f, 0.35f, 1f);
        if (GUILayout.Button("◀ Play Pop Out", GUILayout.Height(36)))
        {
            popup.PlayPopOut();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.3f, 0.65f, 1f, 1f);
        if (GUILayout.Button("↺ Reset Resting State", GUILayout.Height(28)))
        {
            popup.ResetToResting();
        }

        GUI.backgroundColor = new Color(1f, 0.85f, 0.25f, 1f);
        if (GUILayout.Button("💾 Record Current As Resting", GUILayout.Height(28)))
        {
            Undo.RecordObject(popup, "Record Resting Transforms");
            popup.RecordRestingTransforms();
            EditorUtility.SetDirty(popup);
        }
        EditorGUILayout.EndHorizontal();

        GUI.backgroundColor = Color.white;
    }
}
#endif

