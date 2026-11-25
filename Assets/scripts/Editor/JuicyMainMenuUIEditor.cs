#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(JuicyMainMenuUI))]
public class JuicyMainMenuUIEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        JuicyMainMenuUI ui = (JuicyMainMenuUI)target;
        if (ui == null) return;

        EditorGUILayout.Space(14);
        EditorGUILayout.LabelField("Vertical Slot Reel Live Preview Controls", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.2f, 0.75f, 1f, 1f);
        if (GUILayout.Button("▶ Start Slot Reel Search", GUILayout.Height(34)))
        {
            ui.TransitionToMatchmaking();
        }

        GUI.backgroundColor = new Color(1f, 0.35f, 0.35f, 1f);
        if (GUILayout.Button("◀ Cancel Matchmaking", GUILayout.Height(34)))
        {
            ui.TransitionToMainPanel();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        GUI.backgroundColor = new Color(1f, 0.85f, 0.25f, 1f);
        if (GUILayout.Button("★ Test Deceleration (tick... tick... SLAM!)", GUILayout.Height(36)))
        {
            ui.PlayMatchFoundSequence("Red Baron Commander", () =>
            {
                Debug.Log("[JuicyMainMenuUI] Match Found Sequence Complete!");
            });
        }

        EditorGUILayout.Space(6);

        GUI.backgroundColor = new Color(0.4f, 0.8f, 1f, 1f);
        if (GUILayout.Button("⚡ Auto-Wire Candidate Avatars & Slot Reel References", GUILayout.Height(28)))
        {
            AutoWireCandidateAvatars(ui);
        }

        GUI.backgroundColor = Color.white;
    }

    private static void AutoWireCandidateAvatars(JuicyMainMenuUI ui)
    {
        SerializedObject so = new SerializedObject(ui);

        string[] candidatePaths = new string[] {
            "Assets/UIPACK/battlevs/arena_matchmaking_transparent_sprites/Avatars/red_knight_commander.png",
            "Assets/UIPACK/battlevs/arena_matchmaking_transparent_sprites/Avatars/rival_shadow_portrait.png",
            "Assets/UIPACK/battlevs/png/avatar_red.png",
            "Assets/UIPACK/battlevs/png/battle_mounted_knight.png",
            "Assets/UIPACK/battlevs/png/battle_sword_soldier.png",
            "Assets/UIPACK/battlevs/png/battle_spearman.png",
            "Assets/UIPACK/Kings_of_Siege_Home_UI_PNGs/Characters/king_avatar.png",
            "Assets/UIPACK/ResourcesData/Sprites/Demo/Demo_Character/SampleCharacter_Archer.png",
            "Assets/UIPACK/ResourcesData/Sprites/Demo/Demo_Character/SampleCharacter_Knight01.png",
            "Assets/UIPACK/ResourcesData/Sprites/Demo/Demo_Character/SampleCharacter_Knight02.png",
            "Assets/UIPACK/ResourcesData/Sprites/Demo/Demo_Character/SampleCharacter_Witch.png",
            "Assets/UIPACK/ResourcesData/Sprites/Demo/Demo_Character/SampleCharacter_Bard.png"
        };

        var candidateArrayProp = so.FindProperty("candidateAvatars");
        if (candidateArrayProp != null)
        {
            candidateArrayProp.arraySize = candidatePaths.Length;
            for (int i = 0; i < candidatePaths.Length; i++)
            {
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(candidatePaths[i]);
                candidateArrayProp.GetArrayElementAtIndex(i).objectReferenceValue = sp;
            }
        }

        var defSpriteProp = so.FindProperty("defaultOpponentSprite");
        if (defSpriteProp != null)
        {
            defSpriteProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/battlevs/arena_matchmaking_transparent_sprites/Avatars/rival_shadow_portrait.png");
        }

        var confSpriteProp = so.FindProperty("confirmedOpponentSprite");
        if (confSpriteProp != null)
        {
            confSpriteProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/battlevs/arena_matchmaking_transparent_sprites/Avatars/red_knight_commander.png");
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(ui);
        Debug.Log("[JuicyMainMenuUIEditor] Successfully wired " + candidatePaths.Length + " candidate avatars!");
    }
}
#endif
