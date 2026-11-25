using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Central manager for battlefield power-ups.
/// Handles targeting drag-and-drop raycasts, battlefield bounds checking,
/// target indicator lifecycle, cooldown tracking, and power-up execution.
/// </summary>
[DisallowMultipleComponent]
public class PowerupManager : MonoBehaviour
{
    private const string BATTLE_SCENE_NAME = "BattleScene";

    private static PowerupManager instance;
    public static PowerupManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<PowerupManager>();
                if (instance == null)
                {
                    string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                    // Only auto-spawn GameObject in the battle scene ("BattleScene")
                    if (sceneName == BATTLE_SCENE_NAME)
                    {
                        GameObject go = new GameObject("[PowerupManager]");
                        instance = go.AddComponent<PowerupManager>();
                    }
                }
            }
            return instance;
        }
    }

    [Header("Powerup Presets")]
    [SerializeField] private List<PowerupData> powerups = new List<PowerupData>();

    [Header("Ground & Raycasting")]
    [SerializeField] private LayerMask groundLayerMask = ~0;
    [SerializeField] private float groundPlaneY = 0f;

    [Header("Battlefield Boundaries (Optional Strict Filter)")]
    [Tooltip("If false (recommended), any ground point hit by the raycast is a valid drop point. If true, clamps strictly to the coordinates below.")]
    [SerializeField] private bool useStrictBounds = false;
    [SerializeField] private float minBattlefieldX = -60f;
    [SerializeField] private float maxBattlefieldX = 350f;
    [SerializeField] private float minBattlefieldZ = -60f;
    [SerializeField] private float maxBattlefieldZ = 60f;

    [Header("Target Indicator")]
    [SerializeField] private PowerupTargetIndicator targetIndicator;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip powerupReadyAudio;
    [SerializeField] private AudioClip dropSuccessAudio;
    [SerializeField] private AudioClip dropCancelAudio;

    // Runtime state
    private bool isAiming = false;
    private PowerupType currentAimingType;
    private Vector3 currentWorldTarget;
    private bool isCurrentTargetValid = false;

    public bool IsAiming => isAiming;
    public PowerupType CurrentAimingType => currentAimingType;
    public static bool IsDraggingPowerup => Instance != null && Instance.IsAiming;

    public event Action<PowerupType> OnPowerupActivated;

    private void Awake()
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName != BATTLE_SCENE_NAME)
        {
            Destroy(gameObject);
            return;
        }

        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        SetupDefaultPowerups();
        SetupTargetIndicator();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
    }

    private void Reset()
    {
        PopulateDefaultPresets();
    }

    private void OnValidate()
    {
        if (powerups == null || powerups.Count == 0)
        {
            PopulateDefaultPresets();
        }
    }

    [ContextMenu("Populate Default Powerup Presets")]
    public void PopulateDefaultPresets()
    {
        powerups = new List<PowerupData>
        {
            new PowerupData
            {
                powerupType = PowerupType.ArrowShower,
                category = PowerupCategory.AirAssault,
                displayName = "Arrow Shower",
                description = "Rains down a deadly barrage of arrows on enemy forces.",
                energyCost = 2f,
                cooldown = 10f,
                targetingRadius = 5.5f,
                themeColor = new Color(0f, 0.88f, 1f, 0.95f), // Radiant Cyan
                quantity = 35,
                damage = 24f,
                duration = 2.4f
            },
            new PowerupData
            {
                powerupType = PowerupType.Fence,
                category = PowerupCategory.TacticalDefense,
                displayName = "Fence",
                description = "Deploys a sturdy wooden spike barricade that blocks enemy paths.",
                energyCost = 1f,
                cooldown = 14f,
                targetingRadius = 3.6f,
                themeColor = new Color(0f, 0.95f, 0.60f, 0.95f), // Electric Mint
                quantity = 2,
                damage = 12f,
                duration = 25f
            },
            new PowerupData
            {
                powerupType = PowerupType.StoneShower,
                category = PowerupCategory.AirAssault,
                displayName = "Stone Shower",
                description = "Hurls massive catapult boulders that crush and stun enemies.",
                energyCost = 3f,
                cooldown = 12f,
                targetingRadius = 4.8f,
                themeColor = new Color(1f, 0.20f, 0.35f, 0.95f), // Searing Crimson
                quantity = 1,
                damage = 999f,
                duration = 2.2f
            },
            new PowerupData
            {
                powerupType = PowerupType.ShieldDome,
                category = PowerupCategory.SupportSummon,
                displayName = "Shield Dome",
                description = "Projects an impenetrable dome shielding and healing friendly allies.",
                energyCost = 3f,
                cooldown = 16f,
                targetingRadius = 6.0f,
                themeColor = new Color(0f, 0.88f, 1f, 0.95f), // Radiant Cyan
                quantity = 1,
                damage = 12f, // Heals 12 HP/s
                duration = 12f
            },
            new PowerupData
            {
                powerupType = PowerupType.FireArrows,
                category = PowerupCategory.AirAssault,
                displayName = "Fire Arrows",
                description = "Rains burning fire arrows dealing heavy impact damage and burning enemies over time.",
                energyCost = 3f,
                cooldown = 12f,
                targetingRadius = 5.5f,
                themeColor = new Color(1f, 0.42f, 0.05f, 0.95f), // Blaze Orange
                quantity = 40,
                damage = 35f,
                duration = 2.4f
            },
            new PowerupData
            {
                powerupType = PowerupType.WoodRolling,
                category = PowerupCategory.HeavyDamage,
                displayName = "The Log",
                description = "Rolls a spiked wood trunk forward crushing and knocking back enemy units.",
                energyCost = 2f,
                cooldown = 10f,
                targetingRadius = 4.0f,
                themeColor = new Color(1f, 0.58f, 0.12f, 0.95f), // Sunset Amber
                quantity = 1,
                damage = 95f,
                duration = 2.5f
            },
            new PowerupData
            {
                powerupType = PowerupType.BigFireball,
                category = PowerupCategory.HeavyDamage,
                displayName = "Fireball",
                description = "Hurls a massive fiery meteor causing huge blast damage and knockback.",
                energyCost = 4f,
                cooldown = 15f,
                targetingRadius = 5.2f,
                themeColor = new Color(1f, 0.15f, 0.25f, 0.95f), // Plasma Red
                quantity = 1,
                damage = 180f,
                duration = 1.5f
            },
            new PowerupData
            {
                powerupType = PowerupType.ArcherTower,
                category = PowerupCategory.SupportSummon,
                displayName = "Archer Tower",
                description = "Deploys a fortified watchtower with an archer that attacks incoming enemies. Can be destroyed.",
                energyCost = 3f,
                cooldown = 20f,
                targetingRadius = 3.2f,
                themeColor = new Color(0f, 0.70f, 1f, 0.95f), // Royal Azure
                quantity = 1,
                damage = 32f,
                duration = 30f
            },
            new PowerupData
            {
                powerupType = PowerupType.ReinforcementCart,
                category = PowerupCategory.SupportSummon,
                displayName = "Troop Wagon",
                description = "A horse-drawn carriage rushes to the selected point, drops 4 soldiers to fight, and leaves.",
                energyCost = 4f,
                cooldown = 22f,
                targetingRadius = 3.5f,
                themeColor = new Color(1f, 0.84f, 0f, 0.95f), // Radiant Gold
                quantity = 4,
                damage = 0f,
                duration = 4f
            },
            new PowerupData
            {
                powerupType = PowerupType.SteelTrap,
                category = PowerupCategory.TacticalDefense,
                displayName = "Spike Trap",
                description = "Deploys a hidden corridor spike trap that snaps up, heavily damaging and crippling enemies who step over it.",
                energyCost = 2f,
                cooldown = 11f,
                targetingRadius = 3.2f,
                themeColor = new Color(0.5f, 0.85f, 1f, 0.95f), // Diamond Cyan
                quantity = 5,
                damage = 130f,
                duration = 45f
            }
        };

        // Auto-assign icons and prefabs from project - ONLY populate if missing/null (ignore if already defined)
        string[] bowIcons = RuntimeAssets.FindAssets("Icon_Bow01 t:Sprite");
        if (powerups[0].icon == null && bowIcons != null && bowIcons.Length > 0)
            powerups[0].icon = RuntimeAssets.LoadAssetAtPath<Sprite>(RuntimeAssets.GUIDToAssetPath(bowIcons[0]));

        string[] arrowPrefabs = RuntimeAssets.FindAssets("Arrow_B t:Prefab");
        if (powerups[0].customPrefab == null && arrowPrefabs != null && arrowPrefabs.Length > 0)
            powerups[0].customPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>(RuntimeAssets.GUIDToAssetPath(arrowPrefabs[0]));

        string[] fenceIcons = RuntimeAssets.FindAssets("Skill2 t:Sprite");
        if (powerups[1].icon == null && fenceIcons != null && fenceIcons.Length > 0)
            powerups[1].icon = RuntimeAssets.LoadAssetAtPath<Sprite>(RuntimeAssets.GUIDToAssetPath(fenceIcons[0]));

        string[] fencePrefabs = RuntimeAssets.FindAssets("Fence_Spikes_1 Variant t:Prefab");
        if (powerups[1].customPrefab == null && fencePrefabs != null && fencePrefabs.Length > 0)
            powerups[1].customPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>(RuntimeAssets.GUIDToAssetPath(fencePrefabs[0]));

        string[] stoneIcons = RuntimeAssets.FindAssets("Icon_Fire01 t:Sprite");
        if (powerups[2].icon == null && stoneIcons != null && stoneIcons.Length > 0)
            powerups[2].icon = RuntimeAssets.LoadAssetAtPath<Sprite>(RuntimeAssets.GUIDToAssetPath(stoneIcons[0]));

        string[] rockPrefabs = RuntimeAssets.FindAssets("Rock_1 Variant 1 t:Prefab");
        if (powerups[2].customPrefab == null && rockPrefabs != null && rockPrefabs.Length > 0)
            powerups[2].customPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>(RuntimeAssets.GUIDToAssetPath(rockPrefabs[0]));

        string[] shieldIcons = RuntimeAssets.FindAssets("Icon_Shield01 t:Sprite");
        if (powerups[3].icon == null && shieldIcons != null && shieldIcons.Length > 0)
            powerups[3].icon = RuntimeAssets.LoadAssetAtPath<Sprite>(RuntimeAssets.GUIDToAssetPath(shieldIcons[0]));

        // Fire Arrows (index 4)
        string[] fire2Icons = RuntimeAssets.FindAssets("Icon_Fire02 t:Sprite");
        if (powerups[4].icon == null)
        {
            if (fire2Icons != null && fire2Icons.Length > 0)
                powerups[4].icon = RuntimeAssets.LoadAssetAtPath<Sprite>(RuntimeAssets.GUIDToAssetPath(fire2Icons[0]));
            else if (bowIcons != null && bowIcons.Length > 0)
                powerups[4].icon = RuntimeAssets.LoadAssetAtPath<Sprite>(RuntimeAssets.GUIDToAssetPath(bowIcons[0]));
        }
        if (powerups[4].customPrefab == null && arrowPrefabs != null && arrowPrefabs.Length > 0)
            powerups[4].customPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>(RuntimeAssets.GUIDToAssetPath(arrowPrefabs[0]));

        // Wood Rolling Log (index 5)
        if (powerups[5].customPrefab == null)
        {
            string[] logPrefabs = RuntimeAssets.FindAssets("WoodRollFinal t:Prefab");
            if (logPrefabs != null && logPrefabs.Length > 0)
                powerups[5].customPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>(RuntimeAssets.GUIDToAssetPath(logPrefabs[0]));
        }
        if (powerups[5].icon == null && fenceIcons != null && fenceIcons.Length > 0)
            powerups[5].icon = RuntimeAssets.LoadAssetAtPath<Sprite>(RuntimeAssets.GUIDToAssetPath(fenceIcons[0]));

        // Big Fireball (index 6)
        if (powerups[6].icon == null && stoneIcons != null && stoneIcons.Length > 0)
            powerups[6].icon = RuntimeAssets.LoadAssetAtPath<Sprite>(RuntimeAssets.GUIDToAssetPath(stoneIcons[0]));

        // Archer Tower (index 7)
        if (powerups[7].customPrefab == null)
        {
            string[] towerPrefabs = RuntimeAssets.FindAssets("Tower_1 Variant t:Prefab");
            if (towerPrefabs != null && towerPrefabs.Length > 0)
                powerups[7].customPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>(RuntimeAssets.GUIDToAssetPath(towerPrefabs[0]));
        }
        if (powerups[7].icon == null)
        {
            string[] helmetIcons = RuntimeAssets.FindAssets("Icon_Helmet01 t:Sprite");
            if (helmetIcons != null && helmetIcons.Length > 0)
                powerups[7].icon = RuntimeAssets.LoadAssetAtPath<Sprite>(RuntimeAssets.GUIDToAssetPath(helmetIcons[0]));
        }

        // Reinforcement Cart / Troop Wagon (index 8)
        if (powerups[8].customPrefab == null)
        {
            string[] cartPrefabs = RuntimeAssets.FindAssets("TroopWagon_Adjustable t:Prefab");
            if (cartPrefabs != null && cartPrefabs.Length > 0)
                powerups[8].customPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>(RuntimeAssets.GUIDToAssetPath(cartPrefabs[0]));
        }
        if (powerups[8].icon == null)
        {
            string[] bootsIcons = RuntimeAssets.FindAssets("Icon_Boots01_Purple t:Sprite");
            if (bootsIcons != null && bootsIcons.Length > 0)
                powerups[8].icon = RuntimeAssets.LoadAssetAtPath<Sprite>(RuntimeAssets.GUIDToAssetPath(bootsIcons[0]));
        }

        // Steel Trap / Spike Trap (index 9)
        if (powerups[9].customPrefab == null)
        {
            string[] spikePrefabs = RuntimeAssets.FindAssets("aspiketrap t:Prefab");
            if (spikePrefabs == null || spikePrefabs.Length == 0)
                spikePrefabs = RuntimeAssets.FindAssets("Spikes_1 Variant t:Prefab");
            if (spikePrefabs != null && spikePrefabs.Length > 0)
                powerups[9].customPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>(RuntimeAssets.GUIDToAssetPath(spikePrefabs[0]));
        }
        if (powerups[9].icon == null)
        {
            string[] swordIcons = RuntimeAssets.FindAssets("Icon_Sword01 t:Sprite");
            if (swordIcons != null && swordIcons.Length > 0)
                powerups[9].icon = RuntimeAssets.LoadAssetAtPath<Sprite>(RuntimeAssets.GUIDToAssetPath(swordIcons[0]));
        }

        // Auto-assign default button images from UIPACK (can be customized manually in Inspector)
        for (int i = 0; i < powerups.Count; i++)
        {
            if (powerups[i].buttonImage == null)
            {
                string btnName = $"Btn_{powerups[i].powerupType} t:Sprite";
                string[] btnGuids = RuntimeAssets.FindAssets(btnName);
                if (btnGuids != null && btnGuids.Length > 0)
                {
                    powerups[i].buttonImage = RuntimeAssets.LoadAssetAtPath<Sprite>(
                        RuntimeAssets.GUIDToAssetPath(btnGuids[0]));
                }
            }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitializeInScene()
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName != BATTLE_SCENE_NAME)
        {
            // Clean up any stray instances or UI bars in non-battle scenes
            if (instance != null)
            {
                Destroy(instance.gameObject);
                instance = null;
            }
            PowerupManager existing = FindFirstObjectByType<PowerupManager>();
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }
            PowerupUIBar existingBar = FindFirstObjectByType<PowerupUIBar>();
            if (existingBar != null)
            {
                Destroy(existingBar.gameObject);
            }
            return;
        }

        // 1. Ensure PowerupManager exists in BattleScene
        PowerupManager manager = Instance;

        // 2. Ensure Canvas exists and has PowerupUIBar in BattleScene
        Canvas canvas = null;
        Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var c in allCanvases)
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.name == "Canvas")
            {
                canvas = c;
                break;
            }
        }
        if (canvas == null)
        {
            foreach (var c in allCanvases)
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvas = c;
                    break;
                }
            }
        }

        if (canvas != null)
        {
            PowerupUIBar uiBar = canvas.GetComponentInChildren<PowerupUIBar>(true);
            if (uiBar == null)
            {
                GameObject barObj = new GameObject("[PowerupsBar]");
                barObj.transform.SetParent(canvas.transform, false);
                uiBar = barObj.AddComponent<PowerupUIBar>();
            }
            uiBar.EnsureButtonsReady();

            // Ensure the troops container inside box/Image is always active and never hidden
            GameObject boxObj = GameObject.Find("box");
            if (boxObj != null)
            {
                Transform troopDeck = boxObj.transform.Find("Image");
                if (troopDeck != null && !troopDeck.gameObject.activeSelf)
                {
                    troopDeck.gameObject.SetActive(true);
                }
            }
        } 
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void SetupDefaultPowerups()
    {
        if (powerups == null || powerups.Count == 0)
        {
            PopulateDefaultPresets();
        }
    }

    private void SetupTargetIndicator()
    {
        if (targetIndicator == null)
        {
            targetIndicator = FindFirstObjectByType<PowerupTargetIndicator>();
            if (targetIndicator == null)
            {
                GameObject indObj = new GameObject("[PowerupTargetIndicator]");
                targetIndicator = indObj.AddComponent<PowerupTargetIndicator>();
            }
        }
    }

    public PowerupData GetConfig(PowerupType type)
    {
        for (int i = 0; i < powerups.Count; i++)
        {
            if (powerups[i].powerupType == type)
            {
                EnsureDefaultButtonImage(powerups[i]);
                return powerups[i];
            }
        }
        return null;
    }

    private void EnsureDefaultButtonImage(PowerupData data)
    {
        if (data.buttonImage == null)
        {
            data.buttonImage = GetDefaultCardSprite(data.powerupType);
        }
    }

    /// <summary>
    /// Returns the corresponding default GUI card button sprite for each powerup.
    /// </summary>
    public static Sprite GetDefaultCardSprite(PowerupType type)
    {
#if UNITY_EDITOR
        // Cache hits AND misses: FindAssets scans the whole project and was being
        // re-run on every GetConfig call when no sprite existed (freezes on rapid clicks).
        if (defaultSpriteCache.TryGetValue(type, out Sprite cached) && (cached != null || defaultSpriteMissing.Contains(type)))
            return cached;

        Sprite found = null;
        string[] guids = RuntimeAssets.FindAssets($"Btn_{type} t:Sprite");
        if (guids != null && guids.Length > 0)
        {
            found = RuntimeAssets.LoadAssetAtPath<Sprite>(
                RuntimeAssets.GUIDToAssetPath(guids[0]));
        }

        defaultSpriteCache[type] = found;
        if (found == null) defaultSpriteMissing.Add(type);
        return found;
#else
        return null;
#endif
    }

#if UNITY_EDITOR
    private static readonly System.Collections.Generic.Dictionary<PowerupType, Sprite> defaultSpriteCache =
        new System.Collections.Generic.Dictionary<PowerupType, Sprite>();
    private static readonly System.Collections.Generic.HashSet<PowerupType> defaultSpriteMissing =
        new System.Collections.Generic.HashSet<PowerupType>();
#endif

    /// <summary>
    /// Gets the category that the specified powerup belongs to.
    /// </summary>
    public static PowerupCategory GetCategoryForType(PowerupType type)
    {
        switch (type)
        {
            case PowerupType.ArrowShower:
            case PowerupType.FireArrows:
            case PowerupType.StoneShower:
                return PowerupCategory.AirAssault;

            case PowerupType.BigFireball:
            case PowerupType.WoodRolling:
                return PowerupCategory.HeavyDamage;

            case PowerupType.Fence:
            case PowerupType.SteelTrap:
                return PowerupCategory.TacticalDefense;

            case PowerupType.ShieldDome:
            case PowerupType.ArcherTower:
            case PowerupType.ReinforcementCart:
            default:
                return PowerupCategory.SupportSummon;
        }
    }

    /// <summary>
    /// Returns all powerup types that belong to the given category.
    /// </summary>
    public static List<PowerupType> GetPowerupsInCategory(PowerupCategory category)
    {
        switch (category)
        {
            case PowerupCategory.AirAssault:
                return new List<PowerupType> { PowerupType.ArrowShower, PowerupType.FireArrows, PowerupType.StoneShower };

            case PowerupCategory.HeavyDamage:
                return new List<PowerupType> { PowerupType.BigFireball, PowerupType.WoodRolling };

            case PowerupCategory.TacticalDefense:
                return new List<PowerupType> { PowerupType.Fence, PowerupType.SteelTrap };

            case PowerupCategory.SupportSummon:
            default:
                return new List<PowerupType> { PowerupType.ShieldDome, PowerupType.ArcherTower, PowerupType.ReinforcementCart };
        }
    }

    /// <summary>
    /// Given a current powerup, cycles to the next powerup within the same category.
    /// </summary>
    public static PowerupType GetNextInCategory(PowerupType currentType)
    {
        PowerupCategory cat = GetCategoryForType(currentType);
        List<PowerupType> list = GetPowerupsInCategory(cat);
        int idx = list.IndexOf(currentType);
        if (idx < 0) return list[0];
        return list[(idx + 1) % list.Count];
    }

    public float GetRemainingCooldown(PowerupType type)
    {
        return 0f;
    }

    public bool IsOnCooldown(PowerupType type)
    {
        return false;
    }

    public void TriggerCooldown(PowerupType type, float duration)
    {
        // Cooldowns disabled
    }

    #region Drag & Drop Aiming

    /// <summary>
    /// Begins targeting aiming when user starts dragging from a UI powerup button.
    /// </summary>
    public void StartAiming(PowerupType type, Vector2 screenPos)
    {
        isAiming = true;
        currentAimingType = type;
        CameraSwipeController.SuppressSwipe = true;

        PowerupData config = GetConfig(type);
        float radius = config != null ? config.targetingRadius : 5f;
        Color color = config != null ? config.themeColor : Color.cyan;

        if (targetIndicator != null)
        {
            targetIndicator.Show(radius, color);
        }

        UpdateAiming(screenPos);
    }

    /// <summary>
    /// Updates targeting position as user drags across screen / field.
    /// </summary>
    public void UpdateAiming(Vector2 screenPos)
    {
        if (!isAiming) return;
        bool hit = TryGetGroundPosition(screenPos, out Vector3 worldPos, out bool valid);
        if (hit)
        {
            // Fence can't go on top of buildings: shows the red invalid preview
            if (valid) valid = IsDropAllowed(currentAimingType, worldPos);

            currentWorldTarget = worldPos;
            isCurrentTargetValid = valid;

            if (targetIndicator != null)
            {
                targetIndicator.UpdatePosition(worldPos, valid);
            }
        }
    }

    /// <summary>
    /// Finishes drag aiming. Executes powerup if released on valid battlefield point.
    /// </summary>
    public bool FinishAiming(Vector2 screenPos)
    {
        if (!isAiming) return false;

        isAiming = false;
        CameraSwipeController.SuppressSwipe = false;

        if (targetIndicator != null)
        {
            targetIndicator.Hide();
        }

        // Try getting ground position at release screen position
        Vector3 targetDropPos = currentWorldTarget;
        bool hasValidPos = isCurrentTargetValid;

        if (TryGetGroundPosition(screenPos, out Vector3 releaseGroundHit, out bool releaseValid))
        {
            targetDropPos = releaseGroundHit;
            hasValidPos = releaseValid;
        }

        if (hasValidPos) hasValidPos = IsDropAllowed(currentAimingType, targetDropPos);

        if (!hasValidPos)
        {
            Debug.LogWarning($"[PowerupManager] Powerup drop cancelled: target position was invalid.");
            PlaySound(dropCancelAudio, 0.6f);
            return false;
        }

        // The energy bar is the single source of truth for what powerups cost.
        PowerupData config = GetConfig(currentAimingType);
        float cost = config != null ? config.energyCost : 0f;
        SegmentedEnergySliderController energy = SegmentedEnergySliderController.Instance;

        if (energy != null && !energy.CanAfford(cost))
        {
            Debug.LogWarning($"[PowerupManager] Powerup {currentAimingType} cancelled: needs {cost} energy.");
            energy.PlayInsufficientEnergyFeedback();
            PlaySound(dropCancelAudio, 0.6f);
            return false;
        }

        // Execute powerup at the field location!
        Debug.Log($"<color=#00FFAA>[PowerupManager] Activated powerup <b>{currentAimingType}</b> at field position {targetDropPos}!</color>");

        int castingTeam = GameModeManager.LocalTeamId;
        bool isMulti = NetworkBattleBridge.Instance != null && NetworkBattleBridge.Instance.IsMultiplayer;

        if (isMulti)
        {
            NetworkBattleBridge.Instance.RequestCastPowerup(currentAimingType, targetDropPos);
        }
        else if (!ExecutePowerup(currentAimingType, targetDropPos, castingTeam))
        {
            // Blocked (e.g. outside territory): nothing cast, so nothing is spent.
            PlaySound(dropCancelAudio, 0.6f);
            return false;
        }

        // Spend only after the cast went through.
        if (energy != null)
        {
            energy.TrySpendEnergy(cost);
        }

        PlaySound(dropSuccessAudio, 0.75f);
        OnPowerupActivated?.Invoke(currentAimingType);

        return true;
    }

    /// <summary>
    /// Extra per-powerup placement rules on top of the territory check.
    /// Fence cannot be placed on top of castles or towers.
    /// </summary>
    private bool IsDropAllowed(PowerupType type, Vector3 worldPos)
    {
        if (type == PowerupType.Fence &&
            FenceBarrier.IsPlacementBlocked(worldPos, FenceBarrier.GetPlacementRotation(),
                FenceBarrier.PlacementWidth, FenceBarrier.PlacementDepth, FenceBarrier.PlacementHeight))
        {
            return false;
        }

        // Reinforcement cart can't be targeted on top of towers or castles either
        if (type == PowerupType.ReinforcementCart &&
            FenceBarrier.IsPlacementBlocked(worldPos, Quaternion.identity, 3.5f, 3.5f, 1.8f))
        {
            return false;
        }

        // Rolling log can't be dropped on top of towers, castles or other buildings
        if (type == PowerupType.WoodRolling &&
            FenceBarrier.IsPlacementBlocked(worldPos, Quaternion.identity, 3f, 3f, 1.8f))
        {
            return false;
        }
        return true;
    }

    public void CancelAiming()
    {
        if (!isAiming) return;
        isAiming = false;
        CameraSwipeController.SuppressSwipe = false;
        if (targetIndicator != null) targetIndicator.Hide();
        PlaySound(dropCancelAudio, 0.5f);
    }

    #endregion

    #region Powerup Execution

    public bool ExecutePowerup(PowerupType type, Vector3 worldPosition, int castingTeam = 0)
    {
        // Enforce territory drop boundary check
        if (BattleSceneSetup.Instance != null && !BattleSceneSetup.Instance.IsInTeamTerritory(worldPosition, castingTeam))
        {
            Debug.LogWarning($"[PowerupManager] Blocked powerup {type} execution for Team {castingTeam}: target position {worldPosition} is outside permitted territory!");
            return false;
        }

        PowerupData config = GetConfig(type);
        float radius = config != null ? config.targetingRadius : 5f;

        switch (type)
        {
            case PowerupType.ArrowShower:
                ExecuteArrowShower(worldPosition, radius, config, castingTeam);
                return true;

            case PowerupType.Fence:
                // Also enforced here so AI and network casts can't bypass the building check
                if (!IsDropAllowed(type, worldPosition))
                {
                    Debug.LogWarning($"[PowerupManager] Blocked Fence for Team {castingTeam}: position {worldPosition} overlaps a building.");
                    return false;
                }
                ExecuteFence(worldPosition, radius, config, castingTeam);
                return true;

            case PowerupType.StoneShower:
                ExecuteStoneShower(worldPosition, radius, config, castingTeam);
                return true;

            case PowerupType.ShieldDome:
                ExecuteShieldDome(worldPosition, radius, config, castingTeam);
                return true;

            case PowerupType.FireArrows:
                ExecuteFireArrows(worldPosition, radius, config, castingTeam);
                return true;

            case PowerupType.WoodRolling:
                ExecuteWoodRolling(worldPosition, radius, config, castingTeam);
                return true;

            case PowerupType.BigFireball:
                ExecuteBigFireball(worldPosition, radius, config, castingTeam);
                return true;

            case PowerupType.ArcherTower:
                ExecuteArcherTower(worldPosition, radius, config, castingTeam);
                return true;

            case PowerupType.ReinforcementCart:
                if (!IsDropAllowed(type, worldPosition))
                {
                    Debug.LogWarning($"[PowerupManager] Blocked ReinforcementCart for Team {castingTeam}: position {worldPosition} is on a building.");
                    return false;
                }
                ExecuteReinforcementCart(worldPosition, radius, config, castingTeam);
                return true;

            case PowerupType.SteelTrap:
                ExecuteSteelTrap(worldPosition, radius, config, castingTeam);
                return true;
        }

        return false;
    }

    private void ExecuteArrowShower(Vector3 position, float radius, PowerupData config, int team = 0)
    {
        GameObject showerObj = new GameObject("[ArrowShower]");
        ArrowShowerEffect shower = showerObj.AddComponent<ArrowShowerEffect>();
        shower.Initialize(position, radius, team, config?.customPrefab, config?.activationSound,
            config != null ? config.quantity : 35,
            config != null ? config.damage : 24f,
            config != null ? config.duration : 2.4f);
    }

    private void ExecuteFence(Vector3 position, float radius, PowerupData config, int team = 0)
    {
        // Align fence perpendicular to the arena length (across X axis)
        Quaternion rotation = Quaternion.identity;
        if (BattleSceneSetup.Instance != null)
        {
            // Face along the forward push axis so width stretches across arena
            rotation = BattleSceneSetup.Instance.transform.rotation;
        }

        GameObject fenceObj = new GameObject("[FenceBarrier]");
        FenceBarrier barrier = fenceObj.AddComponent<FenceBarrier>();
        barrier.Initialize(position, rotation, team, config?.customPrefab, config?.activationSound,
            config != null ? config.quantity : 2,
            config != null ? config.damage : 12f,
            config != null ? config.duration : 25f);
    }

    private void ExecuteStoneShower(Vector3 position, float radius, PowerupData config, int team = 0)
    {
        GameObject stoneShowerObj = new GameObject("[StoneShower]");
        StoneShowerEffect stoneShower = stoneShowerObj.AddComponent<StoneShowerEffect>();
        stoneShower.Initialize(position, radius, team, config?.customPrefab, config?.activationSound,
            config != null ? config.quantity : 1,
            config != null ? config.damage : 999f,
            config != null ? config.duration : 2.2f);
    }

    private void ExecuteShieldDome(Vector3 position, float radius, PowerupData config, int team = 0)
    {
        GameObject domeObj = new GameObject("[ShieldDome]");
        ShieldDomeEffect dome = domeObj.AddComponent<ShieldDomeEffect>();
        dome.Initialize(position, radius, team, config?.activationSound,
            config != null ? config.duration : 12f,
            config != null ? config.damage : 12f);
    }

    private void ExecuteFireArrows(Vector3 position, float radius, PowerupData config, int team = 0)
    {
        GameObject fireArrowsObj = new GameObject("[FireArrows]");
        FireArrowsEffect fa = fireArrowsObj.AddComponent<FireArrowsEffect>();
        fa.Initialize(position, radius, team, config?.customPrefab, config?.activationSound,
            config != null ? config.quantity : 40,
            config != null ? config.damage : 35f,
            config != null ? config.duration : 2.4f);
    }

    private void ExecuteWoodRolling(Vector3 position, float radius, PowerupData config, int team = 0)
    {
        GameObject woodObj = new GameObject("[WoodRolling]");
        WoodRollingEffect wr = woodObj.AddComponent<WoodRollingEffect>();

        // Roll straight according to the camera's perspective
        Vector3 forward = Vector3.right;
        Camera cam = Camera.main;
        if (cam != null)
        {
            forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        }
        else if (CameraSwipeController.Instance != null)
        {
            forward = CameraSwipeController.Instance.ForwardDirection;
        }

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = (team == 0) ? Vector3.right : Vector3.left;
        }
        else if (team != GameModeManager.LocalTeamId)
        {
            forward = -forward;
        }

        wr.Initialize(position, forward, team, config?.customPrefab, config?.activationSound,
            config != null ? config.quantity : 1,
            config != null ? config.damage : 95f,
            config != null ? config.duration : 2.5f);
    }

    private void ExecuteBigFireball(Vector3 position, float radius, PowerupData config, int team = 0)
    {
        GameObject fireballObj = new GameObject("[BigFireball]");
        BigFireballEffect fb = fireballObj.AddComponent<BigFireballEffect>();
        fb.Initialize(position, radius, team, config?.customPrefab, config?.activationSound,
            config != null ? config.quantity : 1,
            config != null ? config.damage : 180f,
            config != null ? config.duration : 1.5f);
    }

    private void ExecuteArcherTower(Vector3 position, float radius, PowerupData config, int team = 0)
    {
        GameObject towerObj = new GameObject("[ArcherTower]");
        ArcherTowerEffect tower = towerObj.AddComponent<ArcherTowerEffect>();
        tower.Initialize(position, team, config?.customPrefab, config?.activationSound,
            config != null ? config.quantity : 1,
            config != null ? config.damage : 32f,
            config != null ? config.duration : 30f);
    }

    private void ExecuteReinforcementCart(Vector3 position, float radius, PowerupData config, int team = 0)
    {
        GameObject wagonPrefab = config?.customPrefab;

        // If wagonPrefab is null or accidentally referencing an empty static prop without TroopWagon components
        if (wagonPrefab == null || (!wagonPrefab.name.Contains("TroopWagon") && wagonPrefab.GetComponent<ReinforcementCartEffect>() == null))
        {
            // 1. Try Resources.Load (guaranteed in builds)
            wagonPrefab = Resources.Load<GameObject>("Powerups/TroopWagon_Adjustable");

            // 2. Editor AssetDatabase fallback
            if (wagonPrefab == null)
            {
                string[] guids = RuntimeAssets.FindAssets("TroopWagon_Adjustable t:Prefab");
                if (guids != null && guids.Length > 0)
                {
                    wagonPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>(
                        RuntimeAssets.GUIDToAssetPath(guids[0]));
                }
            }
            // 3. Fallback to config's customPrefab if still null
            if (wagonPrefab == null) wagonPrefab = config?.customPrefab;
        }

        GameObject cartObj;
        ReinforcementCartEffect cart;

        if (wagonPrefab != null)
        {
            cartObj = Instantiate(wagonPrefab);
            cartObj.name = "[ReinforcementCart]";
            cart = cartObj.GetComponent<ReinforcementCartEffect>();
            if (cart == null) cart = cartObj.AddComponent<ReinforcementCartEffect>();
        }
        else
        {
            cartObj = new GameObject("[ReinforcementCart]");
            cart = cartObj.AddComponent<ReinforcementCartEffect>();
        }

        cart.Initialize(position, team, wagonPrefab, config?.activationSound,
            config != null ? config.quantity : 4,
            config != null ? config.damage : 0f,
            config != null ? config.duration : 4f);
    }

    private void ExecuteSteelTrap(Vector3 position, float radius, PowerupData config, int team = 0)
    {
        GameObject trapObj = new GameObject("[SteelTrap]");
        SteelTrapEffect trap = trapObj.AddComponent<SteelTrapEffect>();
        trap.Initialize(position, radius, team, config?.customPrefab, config?.activationSound,
            config != null ? config.quantity : 5,
            config != null ? config.damage : 130f,
            config != null ? config.duration : 45f);
    }

    #endregion

    #region Raycasting & Bounds

    public bool TryGetGroundPosition(Vector2 screenPosition, out Vector3 worldPoint, out bool isValid)
    {
        Camera cam = Camera.main;
        if (cam == null) cam = FindFirstObjectByType<Camera>();
        if (cam == null)
        {
            worldPoint = Vector3.zero;
            isValid = false;
            return false;
        }

        Ray ray = cam.ScreenPointToRay(screenPosition);

        // 1. Raycast against physical colliders
        if (Physics.Raycast(ray, out RaycastHit hit, 300f, groundLayerMask, QueryTriggerInteraction.Ignore))
        {
            worldPoint = hit.point;
            isValid = IsPointInBattlefield(worldPoint);
            return true;
        }

        // 2. Fallback against ground plane
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, groundPlaneY, 0f));
        if (groundPlane.Raycast(ray, out float enter))
        {
            worldPoint = ray.GetPoint(enter);
            isValid = IsPointInBattlefield(worldPoint);
            return true;
        }

        worldPoint = Vector3.zero;
        isValid = false;
        return false;
    }

    public bool IsPointInBattlefield(Vector3 pt)
    {
        // Enforce territory restriction if BattleSceneSetup is present
        if (BattleSceneSetup.Instance != null)
        {
            // Human player targeting belongs to their assigned team territory (Blue border for local player)
            return BattleSceneSetup.Instance.IsInTeamTerritory(pt, GameModeManager.LocalTeamId);
        }

        if (!useStrictBounds) return true;

        return pt.x >= minBattlefieldX && pt.x <= maxBattlefieldX &&
               pt.z >= minBattlefieldZ && pt.z <= maxBattlefieldZ;
    }

    private void PlaySound(AudioClip clip, float volume = 0.8f)
    {
        if (clip != null && audioSource != null)
        {
            KoSAudio.PlayOneShot(audioSource, clip, volume, "powerup");
        }
    }

    #endregion
}
