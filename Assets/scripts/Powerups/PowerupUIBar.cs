using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Manages the on-screen Powerups HUD bar containing exactly 4 active category buttons:
///   Slot 0: AirAssault     (Arrow Shower, Fire Arrows, Stone Shower)
///   Slot 1: HeavyDamage    (The Log / Wood Rolling, Fireball)
///   Slot 2: TacticalDefense(Fence, Spike Trap)
///   Slot 3: SupportSummon  (Shield Dome, Archer Tower, Troop Wagon)
///
/// Only 4 buttons can ever be active at a time, each uniquely representing one category.
/// When a powerup is selected (or clicked to cycle), it cycles to another powerup within the same category!
/// </summary>
[DisallowMultipleComponent]
public class PowerupUIBar : MonoBehaviour
{
    [Header("4-Category Active Loadout")]
    [Tooltip("The 4 currently active powerups (one for each unique category).")]
    [SerializeField] private List<PowerupType> activeLoadout = new List<PowerupType>
    {
        PowerupType.ArrowShower,    // Category 0: Air Assault
        PowerupType.WoodRolling,    // Category 1: Heavy Damage
        PowerupType.Fence,          // Category 2: Tactical Defense
        PowerupType.ShieldDome      // Category 3: Support Summon
    };

    [Header("Selected 8-Powerup Deck")]
    [SerializeField] private List<PowerupType> powerupDeck = new List<PowerupType>();
    private int nextDeckIndex = 4;

    [Header("Cycle On Use Setting")]
    [Tooltip("If true, when a powerup is deployed on the battlefield, its slot automatically cycles to the next powerup in its category.")]
    [SerializeField] private bool cycleToNextInCategoryOnDeploy = true;

    [Header("Layout Settings")]
    [SerializeField] private Vector2 buttonSize = new Vector2(74f, 82f);
    [SerializeField] private float buttonSpacing = 24f;
    [Tooltip("Cards are never placed closer than this, even if the scene's serialized spacing is smaller.")]
    [SerializeField] private float minButtonSpacing = 24f;
    private float Spacing => Mathf.Max(buttonSpacing, minButtonSpacing);
    [SerializeField] private Vector2 barAnchoredPosition = new Vector2(0f, 62f);

    [Header("Sprite Assets")]
    [SerializeField] private Sprite buttonBgSprite;
    [SerializeField] private Sprite circleBadgeSprite;

    private readonly List<PowerupButtonUI> spawnedButtons = new List<PowerupButtonUI>();

    public IReadOnlyList<PowerupType> ActiveLoadout => activeLoadout;
    public IReadOnlyList<PowerupButtonUI> SpawnedButtons => spawnedButtons;

    private void Awake()
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName != "BattleScene")
        {
            Destroy(gameObject);
            return;
        }

        ValidateFourCategoryLoadout();
        EnsureButtonsReady();
    }

    private void Start()
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName != "BattleScene") return;

        EnsureButtonsReady();

        if (PowerupManager.Instance != null)
        {
            PowerupManager.Instance.OnPowerupActivated += OnPowerupDeployedOnField;
        }

        PlayDeckEntranceAnimation();
    }

    /// <summary>
    /// Staggered juicy entrance bounce for each powerup card when initialized.
    /// Uses Vector3.one as clean target so multiple calls cannot compound scale shrink!
    /// </summary>
    public void PlayDeckEntranceAnimation()
    {
        for (int i = 0; i < spawnedButtons.Count; i++)
        {
            if (spawnedButtons[i] == null) continue;
            RectTransform rt = spawnedButtons[i].CardRect;
            if (rt == null) continue;

            rt.DOKill();
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one * 0.4f;
            float delay = i * 0.06f;

            rt.DOScale(Vector3.one, 0.35f)
                .SetDelay(delay)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
        }
    }

    private void OnDestroy()
    {
        if (PowerupManager.Instance != null)
        {
            PowerupManager.Instance.OnPowerupActivated -= OnPowerupDeployedOnField;
        }
    }

    /// <summary>
    /// When a powerup is successfully used on the battlefield, advance its category slot to the next powerup.
    /// </summary>
    private void OnPowerupDeployedOnField(PowerupType deployedType)
    {
        if (!cycleToNextInCategoryOnDeploy) return;

        for (int i = 0; i < activeLoadout.Count; i++)
        {
            if (activeLoadout[i] == deployedType)
            {
                CycleCategorySlot(i);
                break;
            }
        }
    }

    /// <summary>
    /// Enforces that the loadout contains exactly 4 distinct items, one from each category.
    /// </summary>
    private void ValidateFourCategoryLoadout()
    {
        if (activeLoadout == null || activeLoadout.Count != 4)
        {
            activeLoadout = new List<PowerupType>
            {
                PowerupType.ArrowShower,
                PowerupType.WoodRolling,
                PowerupType.Fence,
                PowerupType.ShieldDome
            };
        }
        else
        {
            // Ensure slots 0..3 map to categories 0..3
            activeLoadout[0] = EnsureMatchesCategory(activeLoadout[0], PowerupCategory.AirAssault, PowerupType.ArrowShower);
            activeLoadout[1] = EnsureMatchesCategory(activeLoadout[1], PowerupCategory.HeavyDamage, PowerupType.WoodRolling);
            activeLoadout[2] = EnsureMatchesCategory(activeLoadout[2], PowerupCategory.TacticalDefense, PowerupType.Fence);
            activeLoadout[3] = EnsureMatchesCategory(activeLoadout[3], PowerupCategory.SupportSummon, PowerupType.ShieldDome);
        }
    }

    private PowerupType EnsureMatchesCategory(PowerupType candidate, PowerupCategory requiredCategory, PowerupType fallback)
    {
        if (PowerupManager.GetCategoryForType(candidate) == requiredCategory)
            return candidate;
        return fallback;
    }

    public void EnsureButtonsReady()
    {
        ValidateFourCategoryLoadout();
        EnsureLayoutConfigured();

        PowerupButtonUI[] existing = GetComponentsInChildren<PowerupButtonUI>(true);

        // If exactly 4 buttons already exist in the scene hierarchy, preserve them and their badges
        if (existing.Length == 4)
        {
            spawnedButtons.Clear();
            spawnedButtons.AddRange(existing);
            PositionAndBindButtons();
            return;
        }

        // Otherwise generate the UI
        BuildUIHierarchy();
    }

    /// <summary>
    /// Calculates the center-relative anchored position for a button slot so cards are
    /// properly spaced horizontally across the bar, even without relying solely on LayoutGroups.
    /// </summary>
    public Vector2 GetSlotAnchoredPosition(int slotIndex, int totalSlots = 4)
    {
        float totalWidth = totalSlots * buttonSize.x + (totalSlots - 1) * Spacing;
        float startX = -totalWidth * 0.5f + buttonSize.x * 0.5f;
        float x = startX + slotIndex * (buttonSize.x + Spacing);
        return new Vector2(x, 0f);
    }

    /// <summary>
    /// Ensures HorizontalLayoutGroup is attached and enabled with the correct spacing and alignment.
    /// </summary>
    public void EnsureLayoutConfigured()
    {
        RectTransform barRect = GetComponent<RectTransform>();
        if (barRect != null)
        {
            float totalWidth = Mathf.Max(336f, activeLoadout.Count * buttonSize.x + (activeLoadout.Count - 1) * Spacing);
            barRect.sizeDelta = new Vector2(totalWidth, Mathf.Max(84f, buttonSize.y));
        }

        HorizontalLayoutGroup layout = GetComponent<HorizontalLayoutGroup>();
        if (layout == null) layout = gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.enabled = true;
        layout.spacing = Spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }

    /// <summary>
    /// Sets each button's anchored position according to its slot index and binds powerup data.
    /// </summary>
    private void PositionAndBindButtons()
    {
        for (int i = 0; i < spawnedButtons.Count; i++)
        {
            if (spawnedButtons[i] == null) continue;
            spawnedButtons[i].SetSlotIndex(i, this);
            RectTransform rt = spawnedButtons[i].CardRect;
            if (rt == null) rt = spawnedButtons[i].GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = buttonSize;
                rt.anchoredPosition = GetSlotAnchoredPosition(i, spawnedButtons.Count);
                rt.localScale = Vector3.one;
                rt.localRotation = Quaternion.identity;
            }
            KeyCode key = (i < 4) ? (KeyCode.Alpha1 + i) : KeyCode.None;
            if (i < activeLoadout.Count)
            {
                spawnedButtons[i].BindPowerup(activeLoadout[i], key);
            }
        }

        RectTransform barRect = GetComponent<RectTransform>();
        if (barRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(barRect);
        }
    }

    /// <summary>
    /// Sets the full deck of powerups (e.g. 8 selected powerups from CardInventoryManager).
    /// Puts the first 4 in the active hand and prepares the remaining for cyclic rotation.
    /// </summary>
    public void SetDeck(IEnumerable<PowerupType> deck)
    {
        if (deck != null)
        {
            powerupDeck = new List<PowerupType>(deck);
            if (powerupDeck.Count >= 4)
            {
                activeLoadout = new List<PowerupType>();
                for (int i = 0; i < 4; i++) activeLoadout.Add(powerupDeck[i]);
                nextDeckIndex = 4 % powerupDeck.Count;
            }
            else
            {
                activeLoadout = new List<PowerupType>(powerupDeck);
            }
        }

        PowerupButtonUI[] existing = GetComponentsInChildren<PowerupButtonUI>(true);
        if (existing.Length == 4)
        {
            EnsureButtonsReady();
        }
        else
        {
            BuildUIHierarchy();
        }
    }

    /// <summary>
    /// Sets the powerup types displayed on the UI bar and rebuilds it.
    /// </summary>
    public void SetLoadout(IEnumerable<PowerupType> powerupTypes)
    {
        if (powerupTypes != null)
        {
            activeLoadout = new List<PowerupType>(powerupTypes);
        }

        PowerupButtonUI[] existing = GetComponentsInChildren<PowerupButtonUI>(true);
        if (existing.Length == 4)
        {
            EnsureButtonsReady();
        }
        else
        {
            BuildUIHierarchy();
        }
    }

    /// <summary>
    /// Cycles a button slot to the next powerup from the selected deck (or within category) and updates the UI button
    /// with an ultra-juicy 3D card flip animation.
    /// </summary>
    public void CycleCategorySlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= activeLoadout.Count) return;

        PowerupType current = activeLoadout[slotIndex];
        PowerupType next;

        if (powerupDeck != null && powerupDeck.Count > 4)
        {
            int attempts = 0;
            next = powerupDeck[nextDeckIndex % powerupDeck.Count];
            while (activeLoadout.Contains(next) && attempts < powerupDeck.Count)
            {
                nextDeckIndex++;
                next = powerupDeck[nextDeckIndex % powerupDeck.Count];
                attempts++;
            }
            nextDeckIndex = (nextDeckIndex + 1) % powerupDeck.Count;
        }
        else
        {
            next = PowerupManager.GetNextInCategory(current);
        }

        activeLoadout[slotIndex] = next;

        if (slotIndex < spawnedButtons.Count && spawnedButtons[slotIndex] != null)
        {
            KeyCode key = (slotIndex < 4) ? (KeyCode.Alpha1 + slotIndex) : KeyCode.None;
            PowerupButtonUI btn = spawnedButtons[slotIndex];

            // 3D card flip halfway swap
            btn.PlayCardCycleAnimation(() =>
            {
                btn.BindPowerup(next, key);
            });
        }

        Debug.Log($"<color=#FFD700>[PowerupUIBar] Slot {slotIndex} ({PowerupManager.GetCategoryForType(next)}) changed from {current} -> <b>{next}</b></color>");
    }

    /// <summary>
    /// Cycles any slot matching the given category.
    /// </summary>
    public void CycleCategory(PowerupCategory category)
    {
        int slotIndex = (int)category;
        CycleCategorySlot(slotIndex);
    }

    /// <summary>
    /// Builds the 4 powerup buttons dynamically according to the 4 categories.
    /// </summary>
    [ContextMenu("Rebuild 4-Category Powerups UI")]
    public void BuildUIHierarchy()
    {
        LoadDefaultAssets();
        ValidateFourCategoryLoadout();

        RectTransform barRect = GetComponent<RectTransform>();
        if (barRect == null) barRect = gameObject.AddComponent<RectTransform>();

        // Set bar layout above the bottom troops deck
        if (transform.parent != null && transform.parent.name == "box")
        {
            // Reparent to Canvas so it floats cleanly above box and never conflicts with troops
            Transform canvasT = transform.parent.parent;
            if (canvasT != null) transform.SetParent(canvasT, false);
        }

        barRect.anchorMin = new Vector2(0.5f, 0.5f);
        barRect.anchorMax = new Vector2(0.5f, 0.5f);
        barRect.pivot = new Vector2(0.5f, 0.5f);
        barRect.anchoredPosition = new Vector2(1.39f, -125f);
        float totalWidth = Mathf.Max(336f, activeLoadout.Count * buttonSize.x + (activeLoadout.Count - 1) * Spacing);
        barRect.sizeDelta = new Vector2(totalWidth, Mathf.Max(84f, buttonSize.y));
        barRect.localScale = Vector3.one;
        barRect.localRotation = Quaternion.identity;

        EnsureLayoutConfigured();

        // Clean previous children if any
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }

        spawnedButtons.Clear();

        // Build 4 distinct category buttons
        for (int i = 0; i < 4; i++)
        {
            int slotIdx = i;
            PowerupType pType = activeLoadout[slotIdx];
            KeyCode hotKey = KeyCode.Alpha1 + slotIdx;
            PowerupButtonUI btn = CreateButtonCard(pType, GetShortLabel(pType), hotKey, slotIdx);
            spawnedButtons.Add(btn);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(barRect);
    }

    private string GetShortLabel(PowerupType type)
    {
        switch (type)
        {
            case PowerupType.ArrowShower: return "Arrow";
            case PowerupType.Fence: return "Fence";
            case PowerupType.StoneShower: return "Stones";
            case PowerupType.ShieldDome: return "Dome";
            case PowerupType.FireArrows: return "Fire Arrow";
            case PowerupType.WoodRolling: return "Log";
            case PowerupType.BigFireball: return "Fireball";
            case PowerupType.ArcherTower: return "Tower";
            case PowerupType.ReinforcementCart: return "Cart";
            case PowerupType.SteelTrap: return "Trap";
            default: return type.ToString();
        }
    }

    private PowerupButtonUI CreateButtonCard(PowerupType type, string label, KeyCode key, int slotIndex)
    {
        GameObject cardObj = new GameObject($"Btn_Slot_{slotIndex}_{type}");
        cardObj.transform.SetParent(transform, false);

        RectTransform rect = cardObj.AddComponent<RectTransform>();
        rect.sizeDelta = buttonSize;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = GetSlotAnchoredPosition(slotIndex, 4);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        // Card background Image
        Image bgImg = cardObj.AddComponent<Image>();

        Sprite customBtnImg = null;
        if (PowerupManager.Instance != null)
        {
            PowerupData cfg = PowerupManager.Instance.GetConfig(type);
            if (cfg != null) customBtnImg = cfg.buttonImage;
        }
        if (customBtnImg == null)
        {
            customBtnImg = PowerupManager.GetDefaultCardSprite(type);
        }

        if (customBtnImg != null)
        {
            bgImg.sprite = customBtnImg;
            bgImg.type = Image.Type.Simple;
            bgImg.color = Color.white;
        }
        else if (buttonBgSprite != null)
        {
            bgImg.sprite = buttonBgSprite;
            bgImg.type = Image.Type.Sliced;
            bgImg.color = new Color(0.18f, 0.22f, 0.28f, 0.95f);
        }
        else
        {
            bgImg.color = new Color(0.15f, 0.18f, 0.22f, 0.95f);
        }

        Button btn = cardObj.AddComponent<Button>();
        btn.targetGraphic = bgImg;
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        btn.colors = colors;

        cardObj.AddComponent<CanvasGroup>();

        // 1. Icon Image
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(cardObj.transform, false);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(0f, 6f);
        iconRect.sizeDelta = new Vector2(44f, 44f);

        Image img = iconObj.AddComponent<Image>();
        img.raycastTarget = false;

        Sprite icon = null;
        if (PowerupManager.Instance != null)
        {
            PowerupData cfg = PowerupManager.Instance.GetConfig(type);
            if (cfg != null) icon = cfg.icon;
        }

        if (icon != null)
        {
            img.sprite = icon;
            img.color = Color.white;
        }
        else
        {
            img.color = new Color(1f, 1f, 1f, 0.75f);
        }

        // 2. Title Text
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(cardObj.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0f);
        titleRect.anchorMax = new Vector2(1f, 0f);
        titleRect.pivot = new Vector2(0.5f, 0f);
        titleRect.anchoredPosition = new Vector2(0f, 4f);
        titleRect.sizeDelta = new Vector2(0f, 18f);

        TextMeshProUGUI tmpTitle = titleObj.AddComponent<TextMeshProUGUI>();
        tmpTitle.raycastTarget = false;
        tmpTitle.text = label;
        tmpTitle.fontSize = 11f;
        tmpTitle.fontStyle = FontStyles.Bold;
        tmpTitle.alignment = TextAlignmentOptions.Center;
        tmpTitle.color = new Color(0.92f, 0.95f, 1f);

        // 3. Hotkey badge (e.g. [1])
        GameObject hotkeyObj = new GameObject("HotkeyBadge");
        hotkeyObj.transform.SetParent(cardObj.transform, false);
        RectTransform hotkeyRect = hotkeyObj.AddComponent<RectTransform>();
        hotkeyRect.anchorMin = new Vector2(0f, 1f);
        hotkeyRect.anchorMax = new Vector2(0f, 1f);
        hotkeyRect.pivot = new Vector2(0f, 1f);
        hotkeyRect.anchoredPosition = new Vector2(4f, -4f);
        hotkeyRect.sizeDelta = new Vector2(16f, 16f);

        TextMeshProUGUI tmpHotkey = hotkeyObj.AddComponent<TextMeshProUGUI>();
        tmpHotkey.raycastTarget = false;
        tmpHotkey.text = key == KeyCode.None ? "" : ((int)key - (int)KeyCode.Alpha0).ToString();
        tmpHotkey.fontSize = 10f;
        tmpHotkey.fontStyle = FontStyles.Bold;
        tmpHotkey.alignment = TextAlignmentOptions.Center;
        tmpHotkey.color = new Color(1f, 0.85f, 0.3f, 0.9f);

        // 4. Category cycle button / indicator (top right small cycle indicator)
        GameObject cycleObj = new GameObject("CycleBadge");
        cycleObj.transform.SetParent(cardObj.transform, false);
        RectTransform cycleRect = cycleObj.AddComponent<RectTransform>();
        cycleRect.anchorMin = new Vector2(1f, 1f);
        cycleRect.anchorMax = new Vector2(1f, 1f);
        cycleRect.pivot = new Vector2(1f, 1f);
        cycleRect.anchoredPosition = new Vector2(-4f, -4f);
        cycleRect.sizeDelta = new Vector2(16f, 16f);

        TextMeshProUGUI tmpCycle = cycleObj.AddComponent<TextMeshProUGUI>();
        tmpCycle.raycastTarget = false;
        tmpCycle.text = "";
        cycleObj.SetActive(false);
        tmpCycle.fontSize = 11f;
        tmpCycle.fontStyle = FontStyles.Bold;
        tmpCycle.alignment = TextAlignmentOptions.Center;
        tmpCycle.color = new Color(0.7f, 0.85f, 1f, 0.65f);

        // 5. Cooldown Overlay (Filled Radial Image)
        GameObject cdObj = new GameObject("CooldownOverlay");
        cdObj.transform.SetParent(cardObj.transform, false);
        RectTransform cdRect = cdObj.AddComponent<RectTransform>();
        cdRect.anchorMin = Vector2.zero;
        cdRect.anchorMax = Vector2.one;
        cdRect.sizeDelta = Vector2.zero;

        Image cdImg = cdObj.AddComponent<Image>();
        cdImg.raycastTarget = false;
        cdImg.color = new Color(0f, 0f, 0f, 0.72f);
        cdImg.type = Image.Type.Filled;
        cdImg.fillMethod = Image.FillMethod.Radial360;
        cdImg.fillOrigin = (int)Image.Origin360.Top;
        cdImg.fillClockwise = false;
        cdImg.fillAmount = 0f;
        cdObj.SetActive(false);

        // 6. Cooldown Text
        GameObject cdTextObj = new GameObject("CooldownText");
        cdTextObj.transform.SetParent(cardObj.transform, false);
        RectTransform cdTextRect = cdTextObj.AddComponent<RectTransform>();
        cdTextRect.anchorMin = Vector2.zero;
        cdTextRect.anchorMax = Vector2.one;
        cdTextRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmpCd = cdTextObj.AddComponent<TextMeshProUGUI>();
        tmpCd.raycastTarget = false;
        tmpCd.text = "";
        tmpCd.fontSize = 15f;
        tmpCd.fontStyle = FontStyles.Bold;
        tmpCd.alignment = TextAlignmentOptions.Center;
        tmpCd.color = Color.white;
        cdTextObj.SetActive(false);

        // 7. Circular Energy Cost Badge (counterbadge)
        GameObject badgeObj = new GameObject("counterbadge");
        badgeObj.transform.SetParent(cardObj.transform, false);
        RectTransform badgeRect = badgeObj.AddComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0.5f, 0.5f);
        badgeRect.anchorMax = new Vector2(0.5f, 0.5f);
        badgeRect.pivot = new Vector2(0.5f, 0.5f);
        badgeRect.anchoredPosition = new Vector2(27.6f, 36.6f);
        badgeRect.sizeDelta = new Vector2(28f, 28f);

        Image badgeImg = badgeObj.AddComponent<Image>();
        badgeImg.sprite = circleBadgeSprite;
        badgeImg.color = new Color(0.2f, 0.45f, 1f, 1f); // Blue tint matching counterbadge style
        badgeImg.raycastTarget = false;

        GameObject costObj = new GameObject("CostText");
        costObj.transform.SetParent(badgeObj.transform, false);
        RectTransform costRect = costObj.AddComponent<RectTransform>();
        costRect.anchorMin = Vector2.zero;
        costRect.anchorMax = Vector2.one;
        costRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmpCost = costObj.AddComponent<TextMeshProUGUI>();
        tmpCost.raycastTarget = false;
        tmpCost.text = "0";
        tmpCost.fontSize = 16f;
        tmpCost.fontStyle = FontStyles.Bold;
        tmpCost.alignment = TextAlignmentOptions.Center;
        tmpCost.color = Color.white;

        // Attach PowerupButtonUI script and bind slot
        PowerupButtonUI pButton = cardObj.AddComponent<PowerupButtonUI>();
        pButton.BindPowerup(type, key);
        pButton.SetSlotIndex(slotIndex, this);

        return pButton;
    }

    private void LoadDefaultAssets()
    {
        if (buttonBgSprite == null)
        {
            string[] guids = RuntimeAssets.FindAssets("Button_Square01_Demo t:Sprite");
            if (guids != null && guids.Length > 0)
            {
                buttonBgSprite = RuntimeAssets.LoadAssetAtPath<Sprite>(
                    RuntimeAssets.GUIDToAssetPath(guids[0]));
            }
        }
        if (circleBadgeSprite == null)
        {
            string path = RuntimeAssets.GUIDToAssetPath("90ad77f86d3f340988ade060dbf09fac");
            if (!string.IsNullOrEmpty(path))
            {
                circleBadgeSprite = RuntimeAssets.LoadAssetAtPath<Sprite>(path);
            }
        }
    }
}
