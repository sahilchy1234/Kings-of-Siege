using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom Inspector for BattelVS providing instant 1-click test buttons for the DOTween animations
/// that work both in Play Mode AND directly in Edit Mode without playing!
/// </summary>
[CustomEditor(typeof(BattelVS))]
public class BattelVSEditor : Editor
{
    public override void OnInspectorGUI()
    {
        BattelVS bvs = (BattelVS)target;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("🎬 DOTween Animation Tester (Works in Edit Mode & Play Mode!)", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.25f, 0.75f, 1f, 1f); // Sky Blue
        if (GUILayout.Button("⚔️ Test Entrance Slam", GUILayout.Height(34)))
        {
            DOTweenEditModePreview.StartPreview(8f);
            bvs.EditorTestEntrance();
        }

        GUI.backgroundColor = new Color(0.3f, 0.9f, 0.45f, 1f); // Green
        if (GUILayout.Button("👤 Test Opponent Joined", GUILayout.Height(34)))
        {
            DOTweenEditModePreview.StartPreview(4f);
            bvs.EditorTestOpponentJoined();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(1f, 0.85f, 0.2f, 1f); // Gold
        if (GUILayout.Button("⏱️ Test Countdown (3-2-1-BATTLE)", GUILayout.Height(34)))
        {
            DOTweenEditModePreview.StartPreview(6f);
            bvs.EditorTestCountdown();
        }

        GUI.backgroundColor = new Color(1f, 0.35f, 0.35f, 1f); // Red
        if (GUILayout.Button("🚀 Test Dismiss Blast", GUILayout.Height(34)))
        {
            DOTweenEditModePreview.StartPreview(3f);
            bvs.EditorTestDismiss();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.55f, 0.58f, 0.65f, 1f); // Slate
        if (GUILayout.Button("🔄 Reset to Resting Layout", GUILayout.Height(28)))
        {
            DOTweenEditModePreview.StopPreview();
            bvs.ResetToRestingState();
        }

        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.6f, 1f); // Mint Green
        if (GUILayout.Button("💾 Lock Current Layout as Resting", GUILayout.Height(28)))
        {
            bvs.RecordRestingTransforms();
            EditorUtility.SetDirty(bvs);
            Debug.Log("<color=#00FFAA>[BattelVS] Current scene positions and scales locked as resting state!</color>");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);
        GUI.backgroundColor = new Color(0.95f, 0.75f, 0.2f, 1f); // Amber Gold
        if (GUILayout.Button("👑 Rebuild Kings of Siege UI Layout & Assets", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("Rebuild BattleVS Layout?",
                "Do you want to regenerate the BattleVS layout using the automatic template tool?\n\n(Choose Cancel if you want to keep your manual scene settings.)",
                "Rebuild", "Cancel"))
            {
                BattleVSSetupTool.RebuildAll();
            }
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(8);

        DrawDefaultInspector();
    }
}
