using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Siege.Cards;

/// <summary>
/// AAA-quality, Clash Royale-inspired Cards Page controller.
/// Manages two tabs: "Decks" (Battle Deck editor) and "Collection" (card grid).
/// Powered by DOTween for all transitions, card animations, and ultra-smooth, zero-lag swapping.
/// </summary>
[DisallowMultipleComponent]
public class CardsPageController : MonoBehaviour
{
    private static CardsPageController instance;
    public static CardsPageController Instance => instance;

    // ─── Root ──────────────────────────────────────────────────────────────
    [Header("Root")]
    [SerializeField] private GameObject pageRoot;
    [SerializeField] private CanvasGroup pageCanvasGroup;
    [SerializeField] private Image pageBackground;

    // ─── Tab Bar ───────────────────────────────────────────────────────────
    [Header("Tab Bar")]
    [SerializeField] private Button decksTabButton;
    [SerializeField] private Button collectionTabButton;
    [SerializeField] private RectTransform decksTabRect;
    [SerializeField] private RectTransform collectionTabRect;
    [SerializeField] private RectTransform tabUnderline;
    [SerializeField] private TMP_Text decksTabLabel;
    [SerializeField] private TMP_Text collectionTabLabel;
    [SerializeField] private Image decksTabBackground;
    [SerializeField] private Image collectionTabBackground;
    [SerializeField] private Image collectionTabNotifBadge;
    [SerializeField] private TMP_Text collectionTabNotifText;

    // ─── Decks Panel ───────────────────────────────────────────────────────
    [Header("Decks Panel")]
    [SerializeField] private RectTransform decksPanel;
    [SerializeField] private TMP_Text deckNameText;
    [SerializeField] private TMP_Text elixirAverageText;
    [SerializeField] private Image elixirDotImage;

    [Header("Deck Slots (12 slots: 4 Units + 8 Powerups)")]
    [SerializeField] private DeckSlotUI[] deckSlots;

    [Header("Deck Panel King/Tower Preview")]
    [SerializeField] private RectTransform kingTowerPreviewRoot;
    [SerializeField] private Image kingTowerImage;
    [SerializeField] private Image princessTowerImage;
    [SerializeField] private TMP_Text kingTowerLevelText;
    [SerializeField] private Image kingTowerLevelBar;

    // ─── Collection Panel ──────────────────────────────────────────────────
    [Header("Collection Panel")]
    [SerializeField] private RectTransform collectionPanel;
    [SerializeField] private TMP_Text collectionTitleText;
    [SerializeField] private TMP_Text foundCountText;

    [Header("Collection Filter Bar")]
    [SerializeField] private Button filterAllButton;
    [SerializeField] private Button filterArmyButton;
    [SerializeField] private Button filterPowerButton;
    [SerializeField] private Button sortButton;
    [SerializeField] private TMP_Text sortButtonLabel;

    [Header("Collection Scroll View")]
    [SerializeField] private ScrollRect collectionScrollRect;
    [SerializeField] private RectTransform collectionContentRoot;
    [SerializeField] private RectTransform foundCardsGrid;
    [SerializeField] private RectTransform notFoundCardsGrid;
    [SerializeField] private GameObject foundSectionHeader;
    [SerializeField] private GameObject notFoundSectionHeader;

    [Header("Section Headers (Legacy Prefabs)")]
    [SerializeField] private GameObject foundSectionHeaderPrefab;
    [SerializeField] private GameObject notFoundSectionHeaderPrefab;

    // ─── Card Prefabs ──────────────────────────────────────────────────────
    [Header("Prefabs")]
    [SerializeField] private CollectionCardUI collectionCardPrefab;
    [SerializeField] private DeckSlotUI deckSlotPrefab;

    // ─── Card Detail Popup ─────────────────────────────────────────────────
    [Header("Card Detail Popup")]
    [SerializeField] private RectTransform cardDetailPopup;
    [SerializeField] private CanvasGroup cardDetailCanvasGroup;
    [SerializeField] private Image cardDetailArt;
    [SerializeField] private TMP_Text cardDetailName;
    [SerializeField] private TMP_Text cardDetailLevel;
    [SerializeField] private TMP_Text cardDetailCopies;
    [SerializeField] private TMP_Text cardDetailDescription;
    [SerializeField] private Button cardDetailAddToDeckButton;
    [SerializeField] private Button cardDetailRemoveButton;
    [SerializeField] private Button cardDetailCloseButton;
    [SerializeField] private TMP_Text cardDetailAddButtonLabel;
    [SerializeField] private Image cardDetailDimmerBackground;
    [SerializeField] private Button cardDetailUpgradeButton;
    [SerializeField] private TMP_Text cardDetailUpgradeLabel;
    [SerializeField] private TMP_Text cardDetailUpgradeCostText;
    [SerializeField] private Image cardDetailUpgradeCoinIcon;

    [Header("Upgrade Celebration Overlay")]
    [SerializeField] private CardUpgradeCelebrationOverlay upgradeCelebrationOverlay;

    // ─── HUD Currency ──────────────────────────────────────────────────────
    [Header("HUD (Top Bar)")]
    [SerializeField] private TMP_Text hudCoinsText;
    [SerializeField] private TMP_Text hudGemsText;
    [SerializeField] private Button hudClaimButton;

    // ─── References ────────────────────────────────────────────────────────
    [Header("References")]
    [SerializeField] private CardInventoryManager inventoryManager;
    [SerializeField] private CardLibrary cardLibrary;

    // ─── Visual Settings ───────────────────────────────────────────────────
    [Header("Visual Settings")]
    [SerializeField] private Color activeTabColor       = new Color(0.12f, 0.55f, 0.96f, 1f);
    [SerializeField] private Color inactiveTabColor     = new Color(0.06f, 0.15f, 0.30f, 0.95f);
    [SerializeField] private Color activeTabTextColor   = Color.white;
    [SerializeField] private Color inactiveTabTextColor = new Color(0.6f, 0.68f, 0.78f);
    [SerializeField] private float panelSlideDistance   = 60f;

    // ─── Enums & State ─────────────────────────────────────────────────────
    public enum ActiveTab { Decks, Collection }
    public enum FilterMode { All, Army, Power }

    private enum SwapModeType
    {
        None,
        PlacingFromCollection,   // Player clicked "USE" in Collection -> picking a deck slot in Decks tab
        ReplacingDeckCard,       // Player clicked "SWAP" on a deck card -> picking replacement from Collection tab
        FillingEmptySlot         // Player clicked an empty deck slot -> picking card from Collection tab
    }

    private ActiveTab currentTab = ActiveTab.Decks;
    private FilterMode currentFilter = FilterMode.All;

    // Swap State
    private SwapModeType currentSwapMode = SwapModeType.None;
    private CardDefinition pendingCollectionCard;
    private int targetDeckSlotIndex = -1;
    private CardType targetSlotType = CardType.Army;

    // Cached Collection Cards (Zero-GC Pooling)
    private bool collectionGridBuilt = false;
    private readonly List<CollectionCardUI> allSpawnedCollectionCards = new List<CollectionCardUI>();

    // Detail Popup Context
    private CollectionCardUI selectedDetailCard;
    private DeckSlotUI selectedDeckSlot;
    private int selectedDeckSlotIndex = -1;
    private CardDefinition currentDetailDef;
    private PlayerCardEntry currentDetailEntry;
    private bool isDeckCardContext;

    // Dynamic Swap Banner UI
    private GameObject swapModeBanner;
    private TMP_Text swapModeBannerText;
    private Button swapModeBannerCancelBtn;

    // ─────────────────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;

        if (inventoryManager == null) inventoryManager = CardInventoryManager.Instance;
        if (cardLibrary == null && inventoryManager != null) cardLibrary = inventoryManager.Library;
    }

    private void Start()
    {
        SetupButtonListeners();
        RefreshHUD();

        if (inventoryManager != null)
        {
            inventoryManager.OnInventoryChanged += OnInventoryChanged;
            inventoryManager.OnDeckChanged += OnDeckChanged;
        }

        // Initialize panels and deck slots
        InitDeckSlots();
        RefreshDeckSlots();
        RefreshElixirAverage();
        EnsureCollectionGridBuilt();
        UpdateCollectionDisplay();
        EnsureSwapModeBanner();
        EnsureUpgradeCelebrationOverlay();
    }

    private void OnDestroy()
    {
        if (inventoryManager != null)
        {
            inventoryManager.OnInventoryChanged -= OnInventoryChanged;
            inventoryManager.OnDeckChanged -= OnDeckChanged;
        }
    }

    private void SetupButtonListeners()
    {
        decksTabButton?.onClick.AddListener(() =>
        {
            if (currentSwapMode != SwapModeType.None && currentSwapMode != SwapModeType.PlacingFromCollection)
                ExitSwapMode();
            SwitchTab(ActiveTab.Decks);
        });

        collectionTabButton?.onClick.AddListener(() =>
        {
            if (currentSwapMode != SwapModeType.None)
                ExitSwapMode();

            SetFilter(FilterMode.All);
            SwitchTab(ActiveTab.Collection);

            if (collectionScrollRect != null)
                collectionScrollRect.normalizedPosition = new Vector2(0f, 1f);
        });

        filterAllButton?.onClick.AddListener(() => SetFilter(FilterMode.All));
        filterArmyButton?.onClick.AddListener(() => SetFilter(FilterMode.Army));
        filterPowerButton?.onClick.AddListener(() => SetFilter(FilterMode.Power));

        cardDetailCloseButton?.onClick.AddListener(() => HideCardDetailPopup());
        cardDetailAddToDeckButton?.onClick.AddListener(OnDetailAddToDeckClicked);
        cardDetailRemoveButton?.onClick.AddListener(OnDetailRemoveFromDeckClicked);
        cardDetailUpgradeButton?.onClick.AddListener(OnDetailUpgradeClicked);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Public Open/Close

    public void OpenPage()
    {
        if (inventoryManager == null) inventoryManager = CardInventoryManager.Instance;
        inventoryManager?.EnsureLoaded();
        if (cardLibrary == null && inventoryManager != null) cardLibrary = inventoryManager.Library;

        if (pageRoot != null) pageRoot.SetActive(true);

        if (pageCanvasGroup != null)
        {
            pageCanvasGroup.DOKill();
            if (Application.isPlaying)
            {
                pageCanvasGroup.alpha = 0f;
                pageCanvasGroup.DOFade(1f, 0.22f).SetEase(Ease.OutQuad).SetUpdate(true);
            }
            else
            {
                pageCanvasGroup.alpha = 1f;
            }
        }

        SwitchTab(ActiveTab.Decks, instant: true);
        RefreshDeckSlots();
        RefreshElixirAverage();
        SetFilter(FilterMode.All);
        UpdateCollectionDisplay();
        RefreshHUD();
    }

    public void ClosePage()
    {
        ExitSwapMode();
        HideCardDetailPopup(instant: true);

        if (pageCanvasGroup != null)
        {
            pageCanvasGroup.DOKill();
            pageCanvasGroup.DOFade(0f, 0.18f).SetEase(Ease.InQuad).SetUpdate(true)
                .OnComplete(() => { if (pageRoot != null) pageRoot.SetActive(false); });
        }
        else if (pageRoot != null) pageRoot.SetActive(false);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Tab Switching

    private void SwitchTab(ActiveTab tab, bool instant = false)
    {
        if (currentTab == tab && !instant) return;
        currentTab = tab;

        bool showDecks = tab == ActiveTab.Decks;

        if (decksPanel != null)
        {
            decksPanel.DOKill();
            var cg = decksPanel.GetComponent<CanvasGroup>() ?? decksPanel.gameObject.AddComponent<CanvasGroup>();
            cg.DOKill();

            if (instant)
            {
                decksPanel.gameObject.SetActive(showDecks);
                decksPanel.anchoredPosition = new Vector2(0f, decksPanel.anchoredPosition.y);
                cg.alpha = showDecks ? 1f : 0f;
            }
            else
            {
                decksPanel.gameObject.SetActive(true);
                float fromX = showDecks ? -panelSlideDistance : panelSlideDistance;
                decksPanel.anchoredPosition = new Vector2(fromX, decksPanel.anchoredPosition.y);
                decksPanel.DOAnchorPosX(0f, 0.22f).SetEase(Ease.OutCubic).SetUpdate(true);
                cg.DOFade(showDecks ? 1f : 0f, 0.22f).SetUpdate(true)
                    .OnComplete(() => decksPanel.gameObject.SetActive(showDecks));
            }
        }

        if (collectionPanel != null)
        {
            collectionPanel.DOKill();
            var cg = collectionPanel.GetComponent<CanvasGroup>() ?? collectionPanel.gameObject.AddComponent<CanvasGroup>();
            cg.DOKill();

            if (instant)
            {
                collectionPanel.gameObject.SetActive(!showDecks);
                collectionPanel.anchoredPosition = new Vector2(0f, collectionPanel.anchoredPosition.y);
                cg.alpha = !showDecks ? 1f : 0f;
            }
            else
            {
                collectionPanel.gameObject.SetActive(true);
                float fromX = showDecks ? panelSlideDistance : -panelSlideDistance;
                collectionPanel.anchoredPosition = new Vector2(fromX, collectionPanel.anchoredPosition.y);
                collectionPanel.DOAnchorPosX(0f, 0.22f).SetEase(Ease.OutCubic).SetUpdate(true);
                cg.DOFade(showDecks ? 0f : 1f, 0.22f).SetUpdate(true)
                    .OnComplete(() => collectionPanel.gameObject.SetActive(!showDecks));
            }
        }

        UpdateTabVisuals(tab, instant);

        // Slide tab underline
        if (tabUnderline != null && decksTabRect != null && collectionTabRect != null)
        {
            tabUnderline.DOKill();
            RectTransform targetTab = showDecks ? decksTabRect : collectionTabRect;
            if (instant)
                tabUnderline.anchoredPosition = new Vector2(targetTab.anchoredPosition.x, tabUnderline.anchoredPosition.y);
            else
                tabUnderline.DOAnchorPosX(targetTab.anchoredPosition.x, 0.22f).SetEase(Ease.OutCubic).SetUpdate(true);
        }
    }

    private void UpdateTabVisuals(ActiveTab active, bool instant)
    {
        bool isDecks = active == ActiveTab.Decks;
        float dur = instant ? 0f : 0.16f;

        if (decksTabBackground != null)
        {
            decksTabBackground.DOKill();
            if (instant) decksTabBackground.color = isDecks ? activeTabColor : inactiveTabColor;
            else decksTabBackground.DOColor(isDecks ? activeTabColor : inactiveTabColor, dur).SetUpdate(true);
        }

        if (collectionTabBackground != null)
        {
            collectionTabBackground.DOKill();
            if (instant) collectionTabBackground.color = !isDecks ? activeTabColor : inactiveTabColor;
            else collectionTabBackground.DOColor(!isDecks ? activeTabColor : inactiveTabColor, dur).SetUpdate(true);
        }

        if (decksTabLabel != null)
        {
            decksTabLabel.DOKill();
            decksTabLabel.DOColor(isDecks ? activeTabTextColor : inactiveTabTextColor, dur).SetUpdate(true);
        }

        if (collectionTabLabel != null)
        {
            collectionTabLabel.DOKill();
            collectionTabLabel.DOColor(!isDecks ? activeTabTextColor : inactiveTabTextColor, dur).SetUpdate(true);
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Deck Panel & Slot Handling

    private void InitDeckSlots()
    {
        if (deckSlots == null) return;
        for (int i = 0; i < deckSlots.Length; i++)
        {
            if (deckSlots[i] != null)
                deckSlots[i].Setup(OnDeckSlotClicked);
        }
    }

    private void BuildDeckPanel()
    {
        InitDeckSlots();
        RefreshDeckSlots();
        RefreshElixirAverage();
    }

    private void RefreshDeckSlots()
    {
        if (inventoryManager == null || deckSlots == null) return;
        var unitIds = inventoryManager.UnitDeck;
        var powerupIds = inventoryManager.PowerupDeck;

        // 4 Unit slots (0..3)
        for (int i = 0; i < 4; i++)
        {
            if (i >= deckSlots.Length) break;
            var slot = deckSlots[i];
            if (slot == null) continue;

            if (i < unitIds.Count)
            {
                var def = cardLibrary?.GetCard(unitIds[i]);
                var entry = inventoryManager.GetEntry(unitIds[i]);
                if (def != null) slot.SetCard(def, entry);
                else slot.SetEmpty();
            }
            else slot.SetEmpty();
        }

        // 8 Powerup slots (4..11)
        for (int i = 0; i < 8; i++)
        {
            int slotIdx = 4 + i;
            if (slotIdx >= deckSlots.Length) break;
            var slot = deckSlots[slotIdx];
            if (slot == null) continue;

            if (i < powerupIds.Count)
            {
                var def = cardLibrary?.GetCard(powerupIds[i]);
                var entry = inventoryManager.GetEntry(powerupIds[i]);
                if (def != null) slot.SetCard(def, entry);
                else slot.SetEmpty();
            }
            else slot.SetEmpty();
        }
    }

    private void RefreshElixirAverage()
    {
        if (inventoryManager == null) return;
        float avg = inventoryManager.GetDeckElixirAverage();

        if (elixirAverageText != null)
            elixirAverageText.text = avg.ToString("F1");

        if (elixirDotImage != null)
        {
            elixirDotImage.DOKill();
            elixirDotImage.transform.DOPunchScale(Vector3.one * 0.15f, 0.20f, 4, 0.5f).SetUpdate(true);
        }
    }

    private void OnDeckSlotClicked(DeckSlotUI slot)
    {
        if (slot == null) return;
        int slotIndex = System.Array.IndexOf(deckSlots, slot);
        if (slotIndex < 0) return;

        // ── If actively placing a card chosen from Collection ──
        if (currentSwapMode == SwapModeType.PlacingFromCollection)
        {
            if (pendingCollectionCard == null) { ExitSwapMode(); return; }

            bool isUnitSlot = slotIndex < 4;
            bool isPowerupSlot = slotIndex >= 4;

            if (pendingCollectionCard.cardType == CardType.Army && isUnitSlot)
            {
                inventoryManager.SwapUnitSlot(slotIndex, pendingCollectionCard.cardId);
                slot.PlaySwapAnim();
                RefreshDeckSlots();
                RefreshElixirAverage();
                ExitSwapMode();
                return;
            }
            else if (pendingCollectionCard.cardType == CardType.Power && isPowerupSlot)
            {
                inventoryManager.SwapPowerupSlot(slotIndex - 4, pendingCollectionCard.cardId);
                slot.PlaySwapAnim();
                RefreshDeckSlots();
                RefreshElixirAverage();
                ExitSwapMode();
                return;
            }
            else
            {
                // Incompatible slot type
                slot.PlaySwapAnim();
                ShowSwapBanner(
                    pendingCollectionCard.cardType == CardType.Army 
                        ? "CAN ONLY PLACE IN A BATTLE UNIT SLOT (TOP 4)!" 
                        : "CAN ONLY PLACE IN A POWERUP SLOT (BOTTOM 8)!"
                );
                return;
            }
        }

        // ── Normal slot tap ──
        if (slot.IsEmpty)
        {
            // Tapped an empty slot: prompt to choose a card from Collection
            StartFillingEmptySlot(slotIndex);
        }
        else
        {
            // Tapped an occupied slot: show detail popup with SWAP / REMOVE
            var def = cardLibrary?.GetCard(slot.OccupiedCardId);
            if (def != null)
            {
                selectedDeckSlot = slot;
                selectedDeckSlotIndex = slotIndex;
                var entry = inventoryManager?.GetEntry(slot.OccupiedCardId);
                ShowCardDetailPopup(def, entry, isDeckContext: true);
            }
        }
    }

    private void OnDeckChanged()
    {
        RefreshDeckSlots();
        RefreshElixirAverage();
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Collection Grid (Zero-GC Pooling)

    private void EnsureCollectionGridBuilt()
    {
        if (collectionGridBuilt && allSpawnedCollectionCards.Count > 0) return;
        if (cardLibrary == null || collectionCardPrefab == null) return;
        if (foundCardsGrid == null || notFoundCardsGrid == null) return;

        // Clean out legacy instances once
        for (int i = foundCardsGrid.childCount - 1; i >= 0; i--)
            SafeDestroy(foundCardsGrid.GetChild(i).gameObject);
        for (int i = notFoundCardsGrid.childCount - 1; i >= 0; i--)
            SafeDestroy(notFoundCardsGrid.GetChild(i).gameObject);
        allSpawnedCollectionCards.Clear();

        var allCards = cardLibrary.GetAllCards();
        foreach (var def in allCards)
        {
            bool hasCard = inventoryManager != null && inventoryManager.HasCard(def.cardId);
            Transform parent = hasCard ? foundCardsGrid : notFoundCardsGrid;
            var entry = inventoryManager?.GetEntry(def.cardId);

            var cardGO = Instantiate(collectionCardPrefab.gameObject, parent, false);
            cardGO.transform.localScale = Vector3.one;
            var cardUI = cardGO.GetComponent<CollectionCardUI>();
            cardUI.Setup(def, entry, OnCollectionCardClicked);
            allSpawnedCollectionCards.Add(cardUI);
        }

        collectionGridBuilt = true;
    }

    private void UpdateCollectionDisplay()
    {
        EnsureCollectionGridBuilt();

        int foundCount = 0;
        int totalCount = 0;

        foreach (var cardUI in allSpawnedCollectionCards)
        {
            if (cardUI == null) continue;
            var def = cardUI.Definition;
            if (def == null) continue;

            bool isFound = inventoryManager != null && inventoryManager.HasCard(def.cardId);
            var entry = inventoryManager?.GetEntry(def.cardId);
            cardUI.Setup(def, entry, OnCollectionCardClicked);

            // Re-parent if unlocked status changed
            Transform targetParent = isFound ? foundCardsGrid : notFoundCardsGrid;
            if (cardUI.transform.parent != targetParent && targetParent != null)
                cardUI.transform.SetParent(targetParent, false);

            bool matchesFilter = (currentFilter == FilterMode.All) ||
                (currentFilter == FilterMode.Army && def.cardType == CardType.Army) ||
                (currentFilter == FilterMode.Power && def.cardType == CardType.Power);

            cardUI.gameObject.SetActive(matchesFilter);

            if (matchesFilter)
            {
                totalCount++;
                if (isFound) foundCount++;
            }
        }

        if (foundCountText != null)
            foundCountText.text = $"<color=#6EDAFF>Found: {foundCount}/{totalCount}</color>";

        if (foundSectionHeader != null)
            foundSectionHeader.SetActive(foundCount > 0);
        if (notFoundSectionHeader != null)
            notFoundSectionHeader.SetActive(totalCount > foundCount);

        // Update notification count
        int newCount = inventoryManager?.GetFoundCards().Count(e => e.isNew) ?? 0;
        if (collectionTabNotifBadge != null)
        {
            collectionTabNotifBadge.gameObject.SetActive(newCount > 0);
            if (collectionTabNotifText != null) collectionTabNotifText.text = newCount.ToString();
        }
    }

    private void OnCollectionCardClicked(CollectionCardUI card)
    {
        if (card == null || card.Definition == null) return;

        // ── If actively picking a replacement or filling an empty slot ──
        if (currentSwapMode == SwapModeType.ReplacingDeckCard || currentSwapMode == SwapModeType.FillingEmptySlot)
        {
            if (!card.IsFound) return; // Cannot add locked cards

            string cardId = card.Definition.cardId;
            bool isUnitSlot = targetDeckSlotIndex < 4;
            bool isPowerupSlot = targetDeckSlotIndex >= 4;

            if (card.Definition.cardType == CardType.Army && isUnitSlot)
            {
                inventoryManager.SwapUnitSlot(targetDeckSlotIndex, cardId);
                SwitchTab(ActiveTab.Decks);
                deckSlots[targetDeckSlotIndex]?.PlaySwapAnim();
                card.PlayDeckAddedAnim();
                RefreshDeckSlots();
                RefreshElixirAverage();
                ExitSwapMode();
                return;
            }
            else if (card.Definition.cardType == CardType.Power && isPowerupSlot)
            {
                inventoryManager.SwapPowerupSlot(targetDeckSlotIndex - 4, cardId);
                SwitchTab(ActiveTab.Decks);
                deckSlots[targetDeckSlotIndex]?.PlaySwapAnim();
                card.PlayDeckAddedAnim();
                RefreshDeckSlots();
                RefreshElixirAverage();
                ExitSwapMode();
                return;
            }
            else
            {
                ShowSwapBanner(
                    isUnitSlot 
                        ? "PLEASE SELECT A BATTLE UNIT CARD!" 
                        : "PLEASE SELECT A POWERUP CARD!"
                );
                return;
            }
        }

        // ── Normal card tap: show detail popup ──
        selectedDetailCard = card;
        ShowCardDetailPopup(card.Definition, card.Entry, isDeckContext: false);
    }

    private void SetFilter(FilterMode mode)
    {
        currentFilter = mode;
        UpdateCollectionDisplay();

        AnimateFilterButton(filterAllButton,   mode == FilterMode.All);
        AnimateFilterButton(filterArmyButton,  mode == FilterMode.Army);
        AnimateFilterButton(filterPowerButton, mode == FilterMode.Power);
    }

    private static void AnimateFilterButton(Button btn, bool isActive)
    {
        if (btn == null) return;
        var rt = btn.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.DOKill();
            float scale = isActive ? 1.08f : 1f;
            rt.DOScale(Vector3.one * scale, 0.16f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        var img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.DOKill();
            img.DOColor(isActive ? new Color(0.12f, 0.55f, 0.96f) : new Color(0.25f, 0.35f, 0.48f), 0.16f).SetUpdate(true);
        }
    }

    private void OnInventoryChanged()
    {
        UpdateCollectionDisplay();
        RefreshHUD();
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Swap Mode Engine

    private void StartPlacingCardIntoDeck(CardDefinition cardDef)
    {
        if (cardDef == null) return;
        currentSwapMode = SwapModeType.PlacingFromCollection;
        pendingCollectionCard = cardDef;
        targetSlotType = cardDef.cardType;

        HideCardDetailPopup(instant: true, keepSwapMode: true);
        SwitchTab(ActiveTab.Decks);

        ShowSwapBanner(
            cardDef.cardType == CardType.Army 
                ? $"TAP A UNIT SLOT TO PLACE {cardDef.displayName.ToUpper()}" 
                : $"TAP A POWERUP SLOT TO PLACE {cardDef.displayName.ToUpper()}"
        );

        HighlightValidDeckSlots(cardDef.cardType);
    }

    private void StartReplacingDeckCard(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 12) return;
        currentSwapMode = SwapModeType.ReplacingDeckCard;
        targetDeckSlotIndex = slotIndex;
        targetSlotType = slotIndex < 4 ? CardType.Army : CardType.Power;

        HideCardDetailPopup(instant: true, keepSwapMode: true);
        SwitchTab(ActiveTab.Collection);
        SetFilter(targetSlotType == CardType.Army ? FilterMode.Army : FilterMode.Power);

        ShowSwapBanner(
            targetSlotType == CardType.Army 
                ? "SELECT A BATTLE UNIT TO SWAP IN" 
                : "SELECT A POWERUP TO SWAP IN"
        );
    }

    private void StartFillingEmptySlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 12) return;
        currentSwapMode = SwapModeType.FillingEmptySlot;
        targetDeckSlotIndex = slotIndex;
        targetSlotType = slotIndex < 4 ? CardType.Army : CardType.Power;

        SwitchTab(ActiveTab.Collection);
        SetFilter(targetSlotType == CardType.Army ? FilterMode.Army : FilterMode.Power);

        ShowSwapBanner(
            targetSlotType == CardType.Army 
                ? "SELECT A BATTLE UNIT TO PLACE IN SLOT" 
                : "SELECT A POWERUP TO PLACE IN SLOT"
        );
    }

    private void HighlightValidDeckSlots(CardType cardType)
    {
        if (deckSlots == null) return;
        for (int i = 0; i < deckSlots.Length; i++)
        {
            if (deckSlots[i] == null) continue;
            bool isValid = (cardType == CardType.Army && i < 4) || (cardType == CardType.Power && i >= 4);
            deckSlots[i].SetSelected(isValid);
        }
    }

    public void ExitSwapMode()
    {
        currentSwapMode = SwapModeType.None;
        pendingCollectionCard = null;
        targetDeckSlotIndex = -1;

        if (swapModeBanner != null)
            swapModeBanner.SetActive(false);

        if (deckSlots != null)
        {
            for (int i = 0; i < deckSlots.Length; i++)
                if (deckSlots[i] != null) deckSlots[i].SetSelected(false);
        }

        // When leaving swap mode while in Collection tab, restore full collection view (all units & powerups)
        if (currentTab == ActiveTab.Collection)
        {
            SetFilter(FilterMode.All);
        }
    }

    private void EnsureSwapModeBanner()
    {
        if (swapModeBanner != null) return;

        var existing = transform.Find("SwapModeBanner");
        if (existing != null)
        {
            swapModeBanner = existing.gameObject;
            swapModeBannerText = swapModeBanner.GetComponentInChildren<TMP_Text>();
            swapModeBannerCancelBtn = swapModeBanner.GetComponentInChildren<Button>();
            swapModeBannerCancelBtn?.onClick.RemoveAllListeners();
            swapModeBannerCancelBtn?.onClick.AddListener(ExitSwapMode);
            return;
        }

        var bannerGO = new GameObject("SwapModeBanner", typeof(RectTransform), typeof(Image));
        bannerGO.transform.SetParent(transform, false);
        var rt = bannerGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.03f, 1f);
        rt.anchorMax = new Vector2(0.97f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, 44f);
        rt.anchoredPosition = new Vector2(0f, -182f);

        var img = bannerGO.GetComponent<Image>();
        img.color = new Color(0.08f, 0.16f, 0.32f, 0.98f);

        // Text
        var textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(bannerGO.transform, false);
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0.04f, 0f);
        textRT.anchorMax = new Vector2(0.76f, 1f);
        textRT.offsetMin = Vector2.zero; textRT.offsetMax = Vector2.zero;
        swapModeBannerText = textGO.GetComponent<TextMeshProUGUI>();
        swapModeBannerText.fontSize = 17;
        swapModeBannerText.fontStyle = FontStyles.Bold;
        swapModeBannerText.color = new Color(1f, 0.88f, 0.25f);
        swapModeBannerText.alignment = TextAlignmentOptions.MidlineLeft;

        // Cancel Button
        var btnGO = new GameObject("CancelBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(bannerGO.transform, false);
        var btnRT = btnGO.GetComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0.78f, 0.10f);
        btnRT.anchorMax = new Vector2(0.98f, 0.90f);
        btnRT.offsetMin = Vector2.zero; btnRT.offsetMax = Vector2.zero;

        var btnImg = btnGO.GetComponent<Image>();
        btnImg.color = new Color(0.85f, 0.22f, 0.25f, 1f);

        swapModeBannerCancelBtn = btnGO.GetComponent<Button>();
        swapModeBannerCancelBtn.targetGraphic = btnImg;
        swapModeBannerCancelBtn.onClick.AddListener(ExitSwapMode);

        var btnTextGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        btnTextGO.transform.SetParent(btnGO.transform, false);
        var btnTextRT = btnTextGO.GetComponent<RectTransform>();
        btnTextRT.anchorMin = Vector2.zero; btnTextRT.anchorMax = Vector2.one;
        btnTextRT.offsetMin = Vector2.zero; btnTextRT.offsetMax = Vector2.zero;
        var btnTmp = btnTextGO.GetComponent<TextMeshProUGUI>();
        btnTmp.text = "CANCEL";
        btnTmp.fontSize = 15;
        btnTmp.fontStyle = FontStyles.Bold;
        btnTmp.color = Color.white;
        btnTmp.alignment = TextAlignmentOptions.Center;

        swapModeBanner = bannerGO;
        swapModeBanner.SetActive(false);
    }

    private void ShowSwapBanner(string message)
    {
        EnsureSwapModeBanner();
        if (swapModeBanner != null)
        {
            swapModeBanner.SetActive(true);
            if (swapModeBannerText != null) swapModeBannerText.text = message;

            var rt = swapModeBanner.GetComponent<RectTransform>();
            if (rt != null && Application.isPlaying)
            {
                rt.DOKill();
                rt.localScale = Vector3.one * 0.96f;
                rt.DOScale(Vector3.one, 0.18f).SetEase(Ease.OutBack).SetUpdate(true);
            }
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Card Detail Popup

    private void ShowCardDetailPopup(CardDefinition def, PlayerCardEntry entry, bool isDeckContext)
    {
        currentDetailDef = def;
        currentDetailEntry = entry;
        isDeckCardContext = isDeckContext;

        if (cardDetailPopup == null) return;
        cardDetailPopup.gameObject.SetActive(true);
        if (cardDetailDimmerBackground != null) cardDetailDimmerBackground.gameObject.SetActive(true);

        if (cardDetailArt != null) cardDetailArt.sprite = def.cardSprite;
        if (cardDetailName != null) cardDetailName.text = def.displayName;

        bool isMax = def.IsMaxLevel(entry?.level ?? 1);
        if (cardDetailLevel != null)
        {
            if (entry == null)
            {
                cardDetailLevel.text = "Locked";
            }
            else if (isMax)
            {
                cardDetailLevel.text = $"<color=#FFD700>Level {entry.level} (MAX LEVEL)</color>";
            }
            else
            {
                cardDetailLevel.text = $"Level {entry.level}  →  <color=#2ECC71>Level {entry.level + 1}</color>";
            }
        }

        if (cardDetailCopies != null && entry != null)
        {
            if (isMax)
            {
                cardDetailCopies.text = "<color=#FFD700>Maximum Level Reached</color>";
            }
            else
            {
                int needed = def.CopiesToUpgrade(entry.level);
                bool hasCopies = entry.copiesOwned >= needed;
                cardDetailCopies.text = hasCopies
                    ? $"<color=#2ECC71><b>{entry.copiesOwned}/{needed} Copies (Ready!)</b></color>"
                    : $"{entry.copiesOwned}/{needed} to upgrade";
            }
        }

        // Configure Upgrade Button
        if (cardDetailUpgradeButton != null)
        {
            if (entry == null || !entry.isUnlocked)
            {
                cardDetailUpgradeButton.gameObject.SetActive(false);
            }
            else if (isMax)
            {
                cardDetailUpgradeButton.gameObject.SetActive(true);
                cardDetailUpgradeButton.interactable = false;
                if (cardDetailUpgradeLabel != null) cardDetailUpgradeLabel.text = "MAX LEVEL";
                if (cardDetailUpgradeCostText != null) cardDetailUpgradeCostText.gameObject.SetActive(false);
                if (cardDetailUpgradeCoinIcon != null) cardDetailUpgradeCoinIcon.gameObject.SetActive(false);
                var btnImg = cardDetailUpgradeButton.GetComponent<Image>();
                if (btnImg != null) btnImg.color = new Color(0.85f, 0.68f, 0.15f); // Gold
            }
            else
            {
                cardDetailUpgradeButton.gameObject.SetActive(true);
                int neededCopies = def.CopiesToUpgrade(entry.level);
                int neededCoins = def.CoinsToUpgrade(entry.level);
                int currentCoins = PlayerPrefs.GetInt("PlayerCoins", 0);

                bool hasCopies = entry.copiesOwned >= neededCopies;
                bool hasCoins = currentCoins >= neededCoins;

                if (cardDetailUpgradeCostText != null)
                {
                    cardDetailUpgradeCostText.gameObject.SetActive(true);
                    cardDetailUpgradeCostText.text = neededCoins.ToString("N0");
                    cardDetailUpgradeCostText.color = hasCoins ? Color.white : new Color(1f, 0.45f, 0.45f);
                }
                if (cardDetailUpgradeCoinIcon != null)
                {
                    cardDetailUpgradeCoinIcon.gameObject.SetActive(true);
                }

                var btnImg = cardDetailUpgradeButton.GetComponent<Image>();

                if (hasCopies && hasCoins)
                {
                    cardDetailUpgradeButton.interactable = true;
                    if (cardDetailUpgradeLabel != null) cardDetailUpgradeLabel.text = "UPGRADE";
                    if (btnImg != null) btnImg.color = new Color(0.18f, 0.82f, 0.35f); // Vibrant emerald green
                }
                else if (!hasCopies)
                {
                    cardDetailUpgradeButton.interactable = false;
                    if (cardDetailUpgradeLabel != null) cardDetailUpgradeLabel.text = $"NEED {neededCopies - entry.copiesOwned} MORE";
                    if (btnImg != null) btnImg.color = new Color(0.20f, 0.32f, 0.48f); // Dim navy
                }
                else
                {
                    cardDetailUpgradeButton.interactable = false;
                    if (cardDetailUpgradeLabel != null) cardDetailUpgradeLabel.text = "NEED COINS";
                    if (btnImg != null) btnImg.color = new Color(0.45f, 0.35f, 0.20f); // Dim warning
                }
            }
        }

        if (isDeckContext)
        {
            // Deck context: show SWAP and REMOVE
            if (cardDetailAddToDeckButton != null)
            {
                cardDetailAddToDeckButton.gameObject.SetActive(true);
                cardDetailAddToDeckButton.interactable = true;
                if (cardDetailAddButtonLabel != null) cardDetailAddButtonLabel.text = "SWAP";
            }

            if (cardDetailRemoveButton != null)
            {
                cardDetailRemoveButton.gameObject.SetActive(true);
                var rmTxt = cardDetailRemoveButton.GetComponentInChildren<TMP_Text>();
                if (rmTxt != null) rmTxt.text = "REMOVE";
            }
        }
        else
        {
            // Collection context: show USE
            if (cardDetailRemoveButton != null) cardDetailRemoveButton.gameObject.SetActive(false);

            if (cardDetailAddToDeckButton != null)
            {
                cardDetailAddToDeckButton.gameObject.SetActive(true);
                bool inDeck = inventoryManager != null && inventoryManager.IsInDeck(def.cardId);

                if (inDeck)
                {
                    cardDetailAddToDeckButton.interactable = true;
                    if (cardDetailAddButtonLabel != null) cardDetailAddButtonLabel.text = "IN DECK (SWAP)";
                }
                else
                {
                    cardDetailAddToDeckButton.interactable = true;
                    if (cardDetailAddButtonLabel != null) cardDetailAddButtonLabel.text = "USE";
                }
            }
        }

        // Animate popup in
        if (Application.isPlaying)
        {
            if (cardDetailCanvasGroup != null)
            {
                cardDetailCanvasGroup.DOKill();
                cardDetailCanvasGroup.alpha = 0f;
                cardDetailCanvasGroup.DOFade(1f, 0.18f).SetEase(Ease.OutQuad).SetUpdate(true);
            }
            if (cardDetailDimmerBackground != null)
            {
                cardDetailDimmerBackground.DOKill();
                cardDetailDimmerBackground.color = new Color(0, 0, 0, 0);
                cardDetailDimmerBackground.DOFade(0.72f, 0.18f).SetUpdate(true);
            }
            cardDetailPopup.DOKill();
            cardDetailPopup.localScale = Vector3.one * 0.88f;
            cardDetailPopup.DOScale(Vector3.one, 0.20f).SetEase(Ease.OutBack).SetUpdate(true);
        }
        else
        {
            if (cardDetailCanvasGroup != null) cardDetailCanvasGroup.alpha = 1f;
            if (cardDetailDimmerBackground != null) cardDetailDimmerBackground.color = new Color(0, 0, 0, 0.72f);
            cardDetailPopup.localScale = Vector3.one;
        }
    }

    private void HideCardDetailPopup(bool instant = false, bool keepSwapMode = false)
    {
        if (cardDetailPopup == null) return;

        if (!keepSwapMode && currentSwapMode != SwapModeType.None)
        {
            ExitSwapMode();
        }

        if (instant)
        {
            cardDetailPopup.gameObject.SetActive(false);
            if (cardDetailCanvasGroup != null) cardDetailCanvasGroup.alpha = 0f;
            if (cardDetailDimmerBackground != null) cardDetailDimmerBackground.gameObject.SetActive(false);
        }
        else
        {
            if (cardDetailCanvasGroup != null)
            {
                cardDetailCanvasGroup.DOKill();
                cardDetailCanvasGroup.DOFade(0f, 0.15f).SetEase(Ease.InQuad).SetUpdate(true);
            }
            if (cardDetailDimmerBackground != null)
            {
                cardDetailDimmerBackground.DOKill();
                cardDetailDimmerBackground.DOFade(0f, 0.15f).SetUpdate(true)
                    .OnComplete(() => cardDetailDimmerBackground.gameObject.SetActive(false));
            }

            cardDetailPopup.DOKill();
            cardDetailPopup.DOScale(Vector3.one * 0.88f, 0.15f)
                .SetEase(Ease.InBack)
                .SetUpdate(true)
                .OnComplete(() => cardDetailPopup.gameObject.SetActive(false));
        }

        selectedDetailCard = null;
        selectedDeckSlot = null;
        selectedDeckSlotIndex = -1;
        currentDetailDef = null;
    }

    private void OnDetailAddToDeckClicked()
    {
        if (currentDetailDef == null || inventoryManager == null) return;

        if (isDeckCardContext)
        {
            // Player clicked "SWAP" on a deck slot
            StartReplacingDeckCard(selectedDeckSlotIndex);
        }
        else
        {
            // Player clicked "USE" or "IN DECK (SWAP)" on a collection card
            StartPlacingCardIntoDeck(currentDetailDef);
        }
    }

    private void OnDetailRemoveFromDeckClicked()
    {
        if (currentDetailDef == null || inventoryManager == null) return;
        string id = currentDetailDef.cardId;

        var slot = selectedDeckSlot ?? System.Array.Find(deckSlots, s => s != null && s.OccupiedCardId == id);
        if (slot != null)
        {
            slot.PlayRemoveAnim(() =>
            {
                inventoryManager.RemoveFromDeck(id);
                RefreshDeckSlots();
                RefreshElixirAverage();
            });
        }
        else
        {
            inventoryManager.RemoveFromDeck(id);
            RefreshDeckSlots();
            RefreshElixirAverage();
        }

        HideCardDetailPopup(instant: true);
    }

    private void EnsureUpgradeCelebrationOverlay()
    {
        if (upgradeCelebrationOverlay != null)
        {
            if (upgradeCelebrationOverlay.transform.Find("StatShowcaseCard") == null)
            {
                Destroy(upgradeCelebrationOverlay.gameObject);
                upgradeCelebrationOverlay = null;
            }
            else
            {
                return;
            }
        }

        upgradeCelebrationOverlay = GetComponentInChildren<CardUpgradeCelebrationOverlay>(true);
        if (upgradeCelebrationOverlay != null && upgradeCelebrationOverlay.transform.Find("StatShowcaseCard") == null)
        {
            Destroy(upgradeCelebrationOverlay.gameObject);
            upgradeCelebrationOverlay = null;
        }

        if (upgradeCelebrationOverlay == null)
        {
            upgradeCelebrationOverlay = CardUpgradeCelebrationOverlay.Create(transform);
        }
    }

    private void OnDetailUpgradeClicked()
    {
        if (currentDetailDef == null || inventoryManager == null) return;

        int oldLevel = currentDetailEntry != null ? currentDetailEntry.level : 1;
        bool success = inventoryManager.TryUpgradeCard(currentDetailDef.cardId);
        if (success)
        {
            // Instantly hide detail popup and dimmer so the celebration stage has the entire screen clean!
            if (cardDetailPopup != null) cardDetailPopup.gameObject.SetActive(false);
            if (cardDetailDimmerBackground != null) cardDetailDimmerBackground.gameObject.SetActive(false);

            // Refresh current entry
            currentDetailEntry = inventoryManager.GetEntry(currentDetailDef.cardId);
            int newLevel = currentDetailEntry != null ? currentDetailEntry.level : oldLevel + 1;

            EnsureUpgradeCelebrationOverlay();

            if (upgradeCelebrationOverlay != null)
            {
                // Play AAA Clash Royale Level-Up Celebration Animation!
                upgradeCelebrationOverlay.PlayUpgradeSequence(currentDetailDef, oldLevel, newLevel, () =>
                {
                    // Re-render popup content for new level
                    ShowCardDetailPopup(currentDetailDef, currentDetailEntry, isDeckCardContext);

                    // Refresh entire page
                    RefreshHUD();
                    RefreshDeckSlots();
                    UpdateCollectionDisplay();

                    // Play celebratory upgrade bounce on the upgraded card item
                    PlayCardUpgradeEffect(currentDetailDef.cardId);
                });
            }
            else
            {
                // Fallback punch animation
                if (cardDetailArt != null)
                {
                    cardDetailArt.rectTransform.DOKill();
                    cardDetailArt.rectTransform.localScale = Vector3.one;
                    cardDetailArt.rectTransform.DOPunchScale(Vector3.one * 0.22f, 0.36f, 7, 0.6f).SetUpdate(true);
                }
                if (cardDetailUpgradeButton != null)
                {
                    cardDetailUpgradeButton.transform.DOKill();
                    cardDetailUpgradeButton.transform.localScale = Vector3.one;
                    cardDetailUpgradeButton.transform.DOPunchScale(Vector3.one * 0.16f, 0.30f, 6, 0.5f).SetUpdate(true);
                }

                ShowCardDetailPopup(currentDetailDef, currentDetailEntry, isDeckCardContext);
                RefreshHUD();
                RefreshDeckSlots();
                UpdateCollectionDisplay();
                PlayCardUpgradeEffect(currentDetailDef.cardId);
            }
        }
    }

    private void PlayCardUpgradeEffect(string cardId)
    {
        // On collection cards
        var collectionCard = allSpawnedCollectionCards.Find(c => c != null && c.Definition != null && c.Definition.cardId == cardId);
        collectionCard?.PlayUpgradeCelebrationAnim();

        // On deck slots
        if (deckSlots != null)
        {
            for (int i = 0; i < deckSlots.Length; i++)
            {
                if (deckSlots[i] != null && deckSlots[i].OccupiedCardId == cardId)
                {
                    deckSlots[i].PlayUpgradeCelebrationAnim();
                }
            }
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region HUD

    private void RefreshHUD()
    {
        int coins = PlayerPrefs.GetInt("PlayerCoins", 0);
        int gems  = PlayerPrefs.GetInt("PlayerGems", 0);
        if (hudCoinsText != null) hudCoinsText.text = coins.ToString("N0");
        if (hudGemsText  != null) hudGemsText.text  = gems.ToString("N0");

        // Update Collection notification badge with ready upgrades
        if (inventoryManager != null && collectionTabNotifBadge != null)
        {
            int readyCount = inventoryManager.GetUpgradeableCardsCount();
            collectionTabNotifBadge.gameObject.SetActive(readyCount > 0);
            if (collectionTabNotifText != null)
            {
                collectionTabNotifText.text = readyCount.ToString();
            }
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Public API

    public void RefreshAll()
    {
        RefreshDeckSlots();
        RefreshElixirAverage();
        UpdateCollectionDisplay();
        RefreshHUD();
    }

    private static void SafeDestroy(GameObject go)
    {
        if (go == null) return;
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }

    #endregion
}
