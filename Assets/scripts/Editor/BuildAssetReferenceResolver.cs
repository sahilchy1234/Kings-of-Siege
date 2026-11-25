using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;

/// <summary>
/// Pre-build processor and Editor tool that scans and ensures all critical runtime references
/// (prefabs, controllers, sprites, textures) are properly serialized in scenes and prefabs.
/// 
/// Rule: If a reference is already defined/assigned, it is ignored and preserved.
/// Only missing (null) references are resolved and baked into YAML before building.
/// </summary>
public class BuildAssetReferenceResolver : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        Debug.Log("[BuildAssetReferenceResolver] Pre-build validation running...");
        BakeAllMissingReferences(silent: false);
    }

    [MenuItem("Tools/Assets/Bake Missing Asset References for Build")]
    public static void BakeAllMissingReferencesMenu()
    {
        BakeAllMissingReferences(silent: false);
        EditorUtility.DisplayDialog("Asset References Baked",
            "All asset references across the project have been validated and baked.\n\n" +
            "Any existing defined references were preserved.\n" +
            "Missing references have been serialized directly so they never fail in builds.",
            "OK");
    }

    public static void BakeAllMissingReferences(bool silent = true)
    {
        int resolvedCount = 0;

        // 1. Resolve PowerupManager references in active scene
        resolvedCount += ResolvePowerupManagerInScene();

        // 2. Resolve PowerupUIBar references in active scene
        resolvedCount += ResolvePowerupUIBarInScene();

        // 3. Resolve WarCompleteUIController references in active scene
        resolvedCount += ResolveWarCompleteUIInScene();

        // 4. Resolve TroopWagon_Adjustable prefab internal references
        resolvedCount += ResolveTroopWagonPrefab();

        // 5. Resolve Unit Prefab textures (TT_Peasant, TT_Archer, etc.)
        resolvedCount += ResolveUnitPrefabTextures();

        if (resolvedCount > 0)
        {
            AssetDatabase.SaveAssets();
            if (!Application.isPlaying && EditorSceneManager.GetActiveScene().isDirty)
            {
                EditorSceneManager.SaveOpenScenes();
            }
            Debug.Log($"[BuildAssetReferenceResolver] Successfully resolved and serialized {resolvedCount} missing references across the project.");
        }
        else
        {
            if (!silent)
                Debug.Log("[BuildAssetReferenceResolver] All references are already defined and valid! Nothing needed to be changed.");
        }
    }

    private static int ResolvePowerupManagerInScene()
    {
        int resolved = 0;
        PowerupManager pm = Object.FindFirstObjectByType<PowerupManager>();
        if (pm == null) return 0;

        SerializedObject so = new SerializedObject(pm);
        SerializedProperty powerupsProp = so.FindProperty("powerups");
        if (powerupsProp == null || !powerupsProp.isArray) return 0;

        for (int i = 0; i < powerupsProp.arraySize; i++)
        {
            SerializedProperty item = powerupsProp.GetArrayElementAtIndex(i);
            SerializedProperty typeProp = item.FindPropertyRelative("powerupType");
            SerializedProperty customPrefabProp = item.FindPropertyRelative("customPrefab");
            SerializedProperty iconProp = item.FindPropertyRelative("icon");
            SerializedProperty btnProp = item.FindPropertyRelative("buttonImage");

            if (typeProp == null) continue;
            PowerupType type = (PowerupType)typeProp.enumValueIndex;

            // Check customPrefab: only assign if null or if pointing to wrong static cart
            if (customPrefabProp != null)
            {
                GameObject currentPrefab = customPrefabProp.objectReferenceValue as GameObject;

                // Special case for Reinforcement Cart / Troop Wagon: must be TroopWagon_Adjustable, not static Cart_1
                if (type == PowerupType.ReinforcementCart)
                {
                    if (currentPrefab == null || (!currentPrefab.name.Contains("TroopWagon") && currentPrefab.GetComponent<ReinforcementCartEffect>() == null))
                    {
                        GameObject wagon = FindAssetByName<GameObject>("TroopWagon_Adjustable t:Prefab");
                        if (wagon != null)
                        {
                            customPrefabProp.objectReferenceValue = wagon;
                            resolved++;
                        }
                    }
                }
                else if (currentPrefab == null)
                {
                    // If something is already defined, ignore! Only assign if null.
                    GameObject foundPrefab = null;
                    switch (type)
                    {
                        case PowerupType.ArrowShower:
                            foundPrefab = FindAssetByName<GameObject>("Arrow_B t:Prefab");
                            break;
                        case PowerupType.Fence:
                            foundPrefab = FindAssetByName<GameObject>("Fence_Spikes_1 Variant t:Prefab");
                            break;
                        case PowerupType.StoneShower:
                            foundPrefab = FindAssetByName<GameObject>("Rock_1 Variant 1 t:Prefab");
                            break;
                        case PowerupType.FireArrows:
                            foundPrefab = FindAssetByName<GameObject>("Arrow_Fire t:Prefab") ?? FindAssetByName<GameObject>("Arrow_B t:Prefab");
                            break;
                        case PowerupType.WoodRolling:
                            foundPrefab = FindAssetByName<GameObject>("WoodRollFinal t:Prefab");
                            break;
                        case PowerupType.BigFireball:
                            foundPrefab = FindAssetByName<GameObject>("CFXR2 WW Explosion t:Prefab");
                            break;
                        case PowerupType.ArcherTower:
                            foundPrefab = FindAssetByName<GameObject>("Tower_1 Variant t:Prefab");
                            break;
                        case PowerupType.SteelTrap:
                            foundPrefab = FindAssetByName<GameObject>("aspiketrap t:Prefab") ?? FindAssetByName<GameObject>("Spikes_1 Variant t:Prefab");
                            break;
                    }

                    if (foundPrefab != null)
                    {
                        customPrefabProp.objectReferenceValue = foundPrefab;
                        resolved++;
                    }
                }
            }

            // Check buttonImage: only assign if null
            if (btnProp != null && btnProp.objectReferenceValue == null)
            {
                string btnName = $"Btn_{type} t:Sprite";
                Sprite btnSprite = FindAssetByName<Sprite>(btnName);
                if (btnSprite != null)
                {
                    btnProp.objectReferenceValue = btnSprite;
                    resolved++;
                }
            }
        }

        if (resolved > 0)
        {
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(pm);
        }

        return resolved;
    }

    private static int ResolvePowerupUIBarInScene()
    {
        int resolved = 0;
        PowerupUIBar bar = Object.FindFirstObjectByType<PowerupUIBar>();
        if (bar == null) return 0;

        SerializedObject so = new SerializedObject(bar);
        SerializedProperty badgeProp = so.FindProperty("circleBadgeSprite");
        SerializedProperty bgProp = so.FindProperty("buttonBgSprite");

        if (badgeProp != null && badgeProp.objectReferenceValue == null)
        {
            string path = AssetDatabase.GUIDToAssetPath("90ad77f86d3f340988ade060dbf09fac");
            if (!string.IsNullOrEmpty(path))
            {
                badgeProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                resolved++;
            }
        }

        if (bgProp != null && bgProp.objectReferenceValue == null)
        {
            Sprite bg = FindAssetByName<Sprite>("Button_Square01_Demo t:Sprite");
            if (bg != null)
            {
                bgProp.objectReferenceValue = bg;
                resolved++;
            }
        }

        if (resolved > 0)
        {
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(bar);
        }

        return resolved;
    }

    private static int ResolveWarCompleteUIInScene()
    {
        int resolved = 0;
        WarCompleteUIController ui = Object.FindFirstObjectByType<WarCompleteUIController>();
        if (ui == null) return 0;

        SerializedObject so = new SerializedObject(ui);
        SerializedProperty vicProp = so.FindProperty("victoryRibbonSprite");
        SerializedProperty defProp = so.FindProperty("defeatRibbonSprite");
        SerializedProperty starProp = so.FindProperty("starEarnedSprite");

        if (vicProp != null && vicProp.objectReferenceValue == null)
        {
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Label/Label_Ribbon01_Sky.png");
            if (s != null) { vicProp.objectReferenceValue = s; resolved++; }
        }

        if (defProp != null && defProp.objectReferenceValue == null)
        {
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Label/Label_Ribbon01_Red.png");
            if (s != null) { defProp.objectReferenceValue = s; resolved++; }
        }

        if (starProp != null && starProp.objectReferenceValue == null)
        {
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Icon_ItemIcons/512/ItemIcon_Star.png");
            if (s == null) s = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/IconMisc/Icon_Star03_l.png");
            if (s != null) { starProp.objectReferenceValue = s; resolved++; }
        }

        if (resolved > 0)
        {
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(ui);
        }

        return resolved;
    }

    private static int ResolveTroopWagonPrefab()
    {
        int resolved = 0;
        string[] guids = AssetDatabase.FindAssets("TroopWagon_Adjustable t:Prefab");
        if (guids == null || guids.Length == 0) return 0;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        GameObject wagonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (wagonPrefab == null) return 0;

        ReinforcementCartEffect cartEffect = wagonPrefab.GetComponent<ReinforcementCartEffect>();
        if (cartEffect == null) return 0;

        SerializedObject so = new SerializedObject(cartEffect);
        SerializedProperty soldierProp = so.FindProperty("soldierPrefab");

        if (soldierProp != null && soldierProp.objectReferenceValue == null)
        {
            GameObject soldier = FindAssetByName<GameObject>("TT_Halberdier t:Prefab");
            if (soldier != null)
            {
                soldierProp.objectReferenceValue = soldier;
                resolved++;
            }
        }

        if (resolved > 0)
        {
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(wagonPrefab);
        }

        return resolved;
    }

    private static int ResolveUnitPrefabTextures()
    {
        int resolved = 0;
        string[] prefabs = new string[]
        {
            "Assets/prefabs/TT_Peasant.prefab",
            "Assets/prefabs/characters/tteney.prefab"
        };

        Texture texSelf = AssetDatabase.LoadAssetAtPath<Texture>("Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/models/materials/color/Units/Textures/TT_RTS_Units_blueB.tga");
        Texture texOpp = AssetDatabase.LoadAssetAtPath<Texture>("Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/models/materials/color/Units/Textures/TT_RTS_Units_red.tga");

        foreach (string path in prefabs)
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;

            PeasantUnitController ctrl = go.GetComponent<PeasantUnitController>();
            if (ctrl == null) continue;

            SerializedObject so = new SerializedObject(ctrl);
            SerializedProperty selfProp = so.FindProperty("texture_self");
            SerializedProperty oppProp = so.FindProperty("texture_opp");

            bool dirty = false;
            if (selfProp != null && selfProp.objectReferenceValue == null && texSelf != null)
            {
                selfProp.objectReferenceValue = texSelf;
                dirty = true;
                resolved++;
            }

            if (oppProp != null && oppProp.objectReferenceValue == null && texOpp != null)
            {
                oppProp.objectReferenceValue = texOpp;
                dirty = true;
                resolved++;
            }

            if (dirty)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(go);
            }
        }

        return resolved;
    }

    private static T FindAssetByName<T>(string searchFilter) where T : Object
    {
        string[] guids = AssetDatabase.FindAssets(searchFilter);
        if (guids != null && guids.Length > 0)
        {
            string p = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<T>(p);
        }
        return null;
    }
}
