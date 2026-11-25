using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Siege.Cards;

/// <summary>
/// Connects the player's customized deck (4 Units + 8 Powerups) from CardInventoryManager
/// into the live battle gameplay in BattleScene:
/// 1. Maps the 4 selected Army cards to their actual character prefabs and assigns them to PeasantButtonSpawner.
/// 2. Updates the 4 UI buttons under box/Image with card portraits, titles, and elixir costs.
/// 3. Updates SegmentedEnergySliderController with each unit's exact elixir cost.
/// 4. Maps the 8 selected Power cards to PowerupType and configures PowerupUIBar to draw and cycle through them.
/// </summary>
public class BattleDeckLoader : MonoBehaviour
{
    private static readonly Dictionary<string, string> UnitPrefabMap = new Dictionary<string, string>
    {
        { "swordsman", "TT_Swordman" },
        { "heavy_knight", "TT_HeavySwordman" },
        { "greatsword_knight", "TT_Paladin" },
        { "spearman", "TT_Spearman" },
        { "halberdier", "TT_Halberdier" },
        { "axeman", "TT_Commander" },
        { "mace_warrior", "TT_HighPriest" },
        { "hammer_warrior", "TT_Knight" },
        { "archer", "TT_Archer" },
        { "crossbowman", "TT_Crossbowman" },
        { "rogue", "TT_Scout" },
        { "worker", "TT_Settler" },
        { "king", "TT_King" },
        { "wizard", "TT_Mage" },
        { "cleric", "TT_Priest" },
        { "sword_cavalry", "TT_Light_Cavalry" },
        { "axe_cavalry", "TT_Heavy_Cavalry" },
        { "lancer", "TT_Mounted_Knight" },
        { "horse_archer", "TT_Mounted_Scout" },
        { "mounted_king", "TT_Mounted_King" },
        { "mounted_wizard", "TT_Mounted_Mage" },
        { "mounted_cleric", "TT_Mounted_Priest" },
        { "battering_ram", "ram" },
        { "ballista", "ballista" },
        { "catapult", "catapult" },
        { "supply_wagon", "TroopWagon_Adjustable" }
    };

    private static readonly Dictionary<string, PowerupType> PowerupTypeMap = new Dictionary<string, PowerupType>
    {
        { "arrow_shower", PowerupType.ArrowShower },
        { "fire_arrows", PowerupType.FireArrows },
        { "big_fireball", PowerupType.BigFireball },
        { "archer_tower", PowerupType.ArcherTower },
        { "reinforcement_cart", PowerupType.ReinforcementCart },
        { "wood_rolling", PowerupType.WoodRolling },
        { "steel_trap", PowerupType.SteelTrap },
        { "stone_shower", PowerupType.StoneShower },
        { "fence", PowerupType.Fence },
        { "shield_dome", PowerupType.ShieldDome }
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitInBattleScene()
    {
        if (SceneManager.GetActiveScene().name == "BattleScene")
        {
            if (FindFirstObjectByType<BattleDeckLoader>() == null)
            {
                GameObject go = new GameObject("[BattleDeckLoader]");
                go.AddComponent<BattleDeckLoader>();
            }
        }
    }

    private void Start()
    {
        ApplyDeckToBattle();
    }

    public void ApplyDeckToBattle()
    {
        var inventoryMgr = CardInventoryManager.Instance;
        if (inventoryMgr == null)
        {
            // Auto instantiate if missing (e.g. opened BattleScene directly in Editor)
            var invGo = new GameObject("[CardInventoryManager]");
            inventoryMgr = invGo.AddComponent<CardInventoryManager>();
        }
        inventoryMgr.EnsureLoaded();

        var library = inventoryMgr.Library;
        if (library == null)
        {
#if UNITY_EDITOR
            // Editor only: AssetDatabase is not available in builds.
            var guids = RuntimeAssets.FindAssets("t:CardLibrary");
            if (guids != null && guids.Length > 0)
            {
                string path = RuntimeAssets.GUIDToAssetPath(guids[0]);
                library = RuntimeAssets.LoadAssetAtPath<CardLibrary>(path);
            }
#else
            // Runtime (Android/any build): the CardLibrary must live in a Resources folder as "CardLibrary".
            library = Resources.Load<CardLibrary>("CardLibrary");
#endif
        }

        ApplyUnitsLoadout(inventoryMgr, library);
        ApplyPowerupsLoadout(inventoryMgr);
    }

    private void ApplyUnitsLoadout(CardInventoryManager inventoryMgr, CardLibrary library)
    {
        var unitDeck = inventoryMgr.UnitDeck;
        if (unitDeck == null || unitDeck.Count == 0) return;

        var spawner = FindFirstObjectByType<PeasantButtonSpawner>();
        var slider = FindFirstObjectByType<SegmentedEnergySliderController>();
        var box = GameObject.Find("box");
        Transform imageContainer = box != null ? box.transform.Find("Image") : null;

        // Prepare prefabs array
        GameObject[] unitPrefabs = new GameObject[Mathf.Min(4, unitDeck.Count)];

        for (int i = 0; i < unitPrefabs.Length; i++)
        {
            string cardId = unitDeck[i];
            var cardDef = library != null ? library.GetCard(cardId) : null;

            // Load matching character prefab
            if (UnitPrefabMap.TryGetValue(cardId, out string prefabName))
            {
                unitPrefabs[i] = LoadUnitPrefab(prefabName);
            }

            // Fallback if not found
            if (unitPrefabs[i] == null)
            {
                unitPrefabs[i] = Resources.Load<GameObject>("Powerups/TT_Halberdier");
            }

            // Update UI troop card visuals under box/Image
            if (imageContainer != null && i < imageContainer.childCount)
            {
                Transform troopSlot = imageContainer.GetChild(i);
                if (cardDef != null)
                {
                    // Main card artwork image on troops
                    Image cardImg = troopSlot.GetComponent<Image>();
                    if (cardImg != null && cardDef.cardSprite != null)
                    {
                        cardImg.sprite = cardDef.cardSprite;
                    }

                    // Title label
                    Transform titleT = troopSlot.Find("Title");
                    if (titleT != null)
                    {
                        TMP_Text titleText = titleT.GetComponent<TMP_Text>();
                        if (titleText != null) titleText.text = cardDef.displayName;
                    }

                    // Energy slider cost binding
                    if (slider != null && i < slider.ButtonConfigs.Count)
                    {
                        var cfg = slider.ButtonConfigs[i];
                        if (cfg != null)
                        {
                            cfg.energyCost = cardDef.elixirCost;
                            cfg.label = cardDef.displayName;
                            if (cfg.costText != null)
                            {
                                cfg.costText.text = cardDef.elixirCost.ToString();
                            }
                        }
                    }
                }
            }
        }

        // Apply to PeasantButtonSpawner
        if (spawner != null)
        {
            spawner.SetPeasantPrefabs(unitPrefabs);
        }

        Debug.Log($"<color=#00FF99>[BattleDeckLoader] Successfully loaded {unitPrefabs.Length} battle units into PeasantButtonSpawner and HUD.</color>");
    }

    private void ApplyPowerupsLoadout(CardInventoryManager inventoryMgr)
    {
        var powerupDeck = inventoryMgr.PowerupDeck;
        if (powerupDeck == null || powerupDeck.Count == 0) return;

        List<PowerupType> powerupTypes = new List<PowerupType>();
        foreach (var cardId in powerupDeck)
        {
            if (PowerupTypeMap.TryGetValue(cardId, out PowerupType pType))
            {
                powerupTypes.Add(pType);
            }
        }

        if (powerupTypes.Count == 0) return;

        var powerupBar = FindFirstObjectByType<PowerupUIBar>();
        if (powerupBar != null)
        {
            powerupBar.SetDeck(powerupTypes);
            Debug.Log($"<color=#00FF99>[BattleDeckLoader] Successfully loaded {powerupTypes.Count} powerups into PowerupUIBar deck.</color>");
        }
    }

    private static GameObject LoadUnitPrefab(string prefabName)
    {
        string[] searchPaths = new string[]
        {
            $"Assets/prefabs/characters/{prefabName}.prefab",
            $"Assets/prefabs/{prefabName}.prefab",
            $"Assets/Resources/Powerups/{prefabName}.prefab",
            $"Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/prefabs/{prefabName}.prefab"
        };

        foreach (var path in searchPaths)
        {
            GameObject edP = RuntimeAssets.LoadAssetAtPath<GameObject>(path);
            if (edP != null) return edP;
        }
        GameObject p = Resources.Load<GameObject>("Powerups/" + prefabName);
        if (p != null) return p;

        string[] guids = RuntimeAssets.FindAssets($"t:Prefab {prefabName}");
        if (guids != null && guids.Length > 0)
        {
            return RuntimeAssets.LoadAssetAtPath<GameObject>(RuntimeAssets.GUIDToAssetPath(guids[0]));
        }
        return null;
    }
}
