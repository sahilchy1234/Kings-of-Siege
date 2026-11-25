using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Siege.Cards;
using Siege.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Siege.CardsScreen
{
    /// <summary>
    /// Kings of Siege "Cards" screen (Decks + Collection), Clash-Royale-style layout.
    ///
    /// The whole page is generated at runtime from the sprites wired by
    /// Kings of Siege > Cards > Build Cards Screen (MainMenu), sized from the live canvas so it
    /// fits any portrait resolution (reference layout 941 px wide, same as the shop).
    ///
    /// It only reads and calls the existing public APIs, so no existing script is modified:
    ///  - Data: CardInventoryManager (UnitDeck / PowerupDeck / GetEntry / SwapUnitSlot /
    ///    SwapPowerupSlot / CanUpgradeCard / TryUpgradeCard / ClearNewFlag) + CardLibrary.
    ///  - Battle: BattleDeckLoader keeps reading the same UnitDeck / PowerupDeck lists.
    ///  - Currency: PlayerWallet (PlayerCoins / PlayerGems prefs keys).
    ///  - Level-up: re-uses the existing CardUpgradeCelebrationOverlay when it is wired.
    ///  - Bottom nav: listens to the "downpanel" tabs (tab 1 = Cards), like the shop does.
    /// </summary>
    public class CardsScreenController : MonoBehaviour
    {
        public static CardsScreenController Instance { get; private set; }

        // ───────────────────────────── Inspector ─────────────────────────────
        [Header("Sprites (auto-assigned by Kings of Siege > Cards > Build Cards Screen)")]
        public Sprite backgroundSprite;
        public Sprite roundedSprite;
        public Sprite circleSprite;
        public Sprite cardsIconSprite;
        public Sprite coinSprite;
        public Sprite gemSprite;
        public Sprite elixirSprite;
        public Sprite barTrackSprite;
        public Sprite barFillSprite;
        public Sprite arrowUpSprite;
        public Sprite newRibbonSprite;
        public Sprite popupPanelSprite;
        public Sprite closeSprite;
        public Sprite goldButtonSprite;
        public Sprite greenButtonSprite;
        public Sprite greyButtonSprite;
        public Sprite glowSprite;
        public Sprite raysSprite;
        public Sprite levelUpRibbonSprite;
        public Sprite sparkleSprite;
        public Sprite kingPortraitSprite;
        public Sprite towerSprite;
        public Sprite armyIconSprite;
        public Sprite powerIconSprite;
        public Sprite sortIconSprite;
        public Sprite lockSprite;
        public TMP_FontAsset font;
        [Tooltip("Material using the 'Kings of Siege/UI Grayscale' shader (Not found cards).")]
        public Material grayscaleMaterial;
        public List<ShopSpriteTrim> spriteTrims = new List<ShopSpriteTrim>();

        [Header("Data")]
        public CardLibrary cardLibraryOverride;
        [Tooltip("Existing level-up celebration (moved here from the old cards page by the builder). Optional.")]
        public CardUpgradeCelebrationOverlay celebrationOverlay;

        [Header("Scene Hooks")]
        [Tooltip("The main menu bottom navigation bar (auto-found by name 'downpanel').")]
        public RectTransform bottomNav;
        public int cardsTabIndex = 1;
        public bool hookBottomNav = true;

        [Header("Audio (optional)")]
        public AudioSource audioSource;
        public AudioClip clickSound;
        public AudioClip tabSound;
        public AudioClip swapSound;
        public AudioClip upgradeSound;
        public AudioClip errorSound;

        // ───────────────────────────── Constants ─────────────────────────────
        private const float REF_W = 941f;
        private const int COLUMNS = 4;

        private static readonly Color BgBlue = new Color(0.06f, 0.15f, 0.42f, 1f);
        private static readonly Color PanelNavy = new Color(0.04f, 0.10f, 0.28f, 0.88f);
        private static readonly Color TabActive = new Color(0.16f, 0.52f, 0.98f, 1f);
        private static readonly Color TabInactive = new Color(0.05f, 0.13f, 0.34f, 0.95f);
        private static readonly Color BandBlue = new Color(0.10f, 0.42f, 0.92f, 1f);
        private static readonly Color TextWhite = new Color(1f, 0.98f, 0.94f, 1f);
        private static readonly Color TextDim = new Color(0.62f, 0.72f, 0.88f, 1f);
        private static readonly Color FoundPink = new Color(0.98f, 0.45f, 0.95f, 1f);
        private static readonly Color BarBlue = new Color(0.20f, 0.72f, 1f, 1f);
        private static readonly Color BarGreen = new Color(0.30f, 0.92f, 0.32f, 1f);
        private static readonly Color BarGold = new Color(1f, 0.80f, 0.22f, 1f);
        private static readonly Color BadgeRed = new Color(0.92f, 0.12f, 0.22f, 1f);
        private static readonly Color GoldRim = new Color(1f, 0.80f, 0.25f, 1f);

        public enum Tab { Decks = 0, Collection = 1 }
        public enum Filter { All, Army, Power, Upgradable }
        public enum SortMode { Level, Rarity, Cost, Name }

        // ───────────────────────────── Runtime ─────────────────────────────
        private Canvas rootCanvas;
        private RectTransform canvasRect;
        private RectTransform pageRoot;
        private CanvasGroup pageGroup;
        private RectTransform popupLayer;
        private RectTransform dragLayer;
        private Material outlineMaterial;
        private Sprite shineSprite;
        private Sprite generatedBarSprite;
        private Sprite capsuleSprite;
        private const float CapsuleBorder = 31f;
        private readonly Dictionary<Sprite, Rect> trimLookup = new Dictionary<Sprite, Rect>();

        private float u = 1f;
        private float W, contentW, builtWidth = -1f;
        private float cardW, cardH, gapX, gapY;
        private bool isOpen;
        private Tab currentTab = Tab.Decks;
        private Filter filter = Filter.All;
        private SortMode sortMode = SortMode.Level;
        private bool sortDescending = true;

        // Header / tabs
        private RectTransform header;
        private TextMeshProUGUI coinText, gemText;
        private readonly RectTransform[] tabRects = new RectTransform[2];
        private readonly Image[] tabBgs = new Image[2];
        private readonly TextMeshProUGUI[] tabLabels = new TextMeshProUGUI[2];
        private RectTransform collectionBadge;
        private TextMeshProUGUI collectionBadgeText;
        private int lastBadgeCount = -1;

        // Pages
        private RectTransform viewport;
        private RectTransform track;
        private readonly CanvasGroup[] pageGroups = new CanvasGroup[2];
        private ScrollRect deckScroll, collectionScroll;

        // Decks
        private readonly CardsScreenCardView[] deckViews = new CardsScreenCardView[CardInventoryManager.MAX_DECK_SIZE];
        private TextMeshProUGUI elixirText, deckLevelText, deckPowerText, kingLevelText;
        private RectTransform elixirIcon;
        private float shownElixir;

        // Collection
        private RectTransform foundGrid, notFoundGrid, foundHeader, notFoundHeader, emptyNote;
        private TextMeshProUGUI foundCountText, sortLabel;
        private RectTransform sortDirIcon;
        private readonly List<CardsScreenCardView> collectionViews = new List<CardsScreenCardView>();
        private readonly Image[] filterBgs = new Image[4];
        private readonly RectTransform[] filterChips = new RectTransform[4];

        // Swap / place flow
        private RectTransform swapBanner;
        private TextMeshProUGUI swapBannerText;
        private string placeCardId;          // card chosen with "Use" waiting for a deck slot
        private int pendingEmptySlot = -1;   // empty deck slot waiting for a card from the collection

        // Drag
        private CardsScreenCardView dragView;
        private CardsScreenCardView dragHover;
        private Vector2 dragGrabOffset;

        // Popup
        private RectTransform detailRoot;
        private Image detailDim;
        private RectTransform detailPanel;
        private RectTransform detailHero;
        private CardsScreenCardView detailSource;
        private bool popupBusy;

        private bool suppressDeckEvent;
        private CardInventoryManager inventory;
        private CardLibrary library;

        public bool IsOpen => isOpen;

        // ═════════════════════════════ Lifecycle ═════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            PlayerWallet.EnsureSeeded();

            rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas != null) rootCanvas = rootCanvas.rootCanvas;
            canvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : transform.parent as RectTransform;

            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }

            foreach (var t in spriteTrims)
                if (t != null && t.sprite != null && !trimLookup.ContainsKey(t.sprite)) trimLookup.Add(t.sprite, t.visible);
        }

        private void OnEnable()
        {
            PlayerWallet.OnCoinsChanged += HandleWalletChanged;
            PlayerWallet.OnGemsChanged += HandleWalletChanged;
        }

        private void OnDisable()
        {
            PlayerWallet.OnCoinsChanged -= HandleWalletChanged;
            PlayerWallet.OnGemsChanged -= HandleWalletChanged;
        }

        private void Start()
        {
            StartCoroutine(HookSceneNextFrame());
        }

        private void OnDestroy()
        {
            UnhookInventory();
            if (pageRoot != null) KillTree(pageRoot);
            if (popupLayer != null) { KillTree(popupLayer); Destroy(popupLayer.gameObject); }
            if (outlineMaterial != null) Destroy(outlineMaterial);
            if (shineSprite != null) { Destroy(shineSprite.texture); Destroy(shineSprite); }
            if (generatedBarSprite != null) { Destroy(generatedBarSprite.texture); Destroy(generatedBarSprite); }
            if (capsuleSprite != null) { Destroy(capsuleSprite.texture); Destroy(capsuleSprite); }
            if (Instance == this) Instance = null;
        }

        // ═════════════════════════════ Public API ═════════════════════════════

        public void Open() => Open(Tab.Decks);

        public void Open(Tab tab)
        {
            if (canvasRect == null) return;
            ResolveData();
            Canvas.ForceUpdateCanvases();

            float w = canvasRect.rect.width;
            if (pageRoot == null || Mathf.Abs(w - builtWidth) > 1f) Build();

            EnsureSiblingBelowNav();
            pageRoot.gameObject.SetActive(true);
            UpdateViewportBottom();

            bool wasOpen = isOpen;
            isOpen = true;
            pageGroup.DOKill();
            pageGroup.interactable = true;
            pageGroup.blocksRaycasts = true;

            RefreshAll(false);
            SwitchTab(tab, false);

            if (!wasOpen)
            {
                deckScroll.verticalNormalizedPosition = 1f;
                collectionScroll.verticalNormalizedPosition = 1f;
                PlayEntrance();
                Sfx(clickSound);
            }
            else pageGroup.alpha = 1f;
        }

        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;
            CancelPlaceMode(false);
            CancelDrag();
            CloseDetail(true);
            if (pageRoot == null) return;

            pageGroup.DOKill();
            pageGroup.interactable = false;
            pageGroup.blocksRaycasts = false;
            pageGroup.DOFade(0f, 0.18f).SetEase(Ease.InQuad).SetUpdate(true)
                .OnComplete(() => { if (!isOpen && pageRoot != null) pageRoot.gameObject.SetActive(false); });
        }

        public void ShowCollection() => Open(Tab.Collection);

        // ═════════════════════════════ Data ═════════════════════════════

        private void ResolveData()
        {
            var inv = CardInventoryManager.Instance;
            if (inv == null) inv = FindFirstObjectByType<CardInventoryManager>(FindObjectsInactive.Include);
            if (inv != inventory)
            {
                UnhookInventory();
                inventory = inv;
                if (inventory != null)
                {
                    inventory.OnInventoryChanged += HandleInventoryChanged;
                    inventory.OnDeckChanged += HandleDeckChanged;
                }
            }
            if (inventory != null) inventory.EnsureLoaded();
            library = inventory != null && inventory.Library != null ? inventory.Library : cardLibraryOverride;
        }

        private void UnhookInventory()
        {
            if (inventory == null) return;
            inventory.OnInventoryChanged -= HandleInventoryChanged;
            inventory.OnDeckChanged -= HandleDeckChanged;
        }

        private void HandleInventoryChanged()
        {
            if (!isOpen) return;
            RefreshAll(false);
        }

        private void HandleDeckChanged()
        {
            if (!isOpen || suppressDeckEvent) return;
            RefreshDeck(false);
            RefreshCollectionStates();
        }

        private void HandleWalletChanged(int oldValue, int newValue)
        {
            if (!isOpen) return;
            RefreshCounters(true);
            RefreshBadge();
        }

        private PlayerCardEntry EntryOf(string id) => inventory != null ? inventory.GetEntry(id) : null;
        private static bool IsFound(PlayerCardEntry e) => e != null && e.isUnlocked;

        private static bool CopiesReady(CardDefinition def, PlayerCardEntry e)
        {
            if (def == null || !IsFound(e) || def.IsMaxLevel(e.level)) return false;
            return e.copiesOwned >= def.CopiesToUpgrade(e.level);
        }

        private bool InDeck(string id) => inventory != null && inventory.IsInDeck(id);

        // ═════════════════════════════ Build ═════════════════════════════

        private void Build()
        {
            if (pageRoot != null)
            {
                KillTree(pageRoot);
                Destroy(pageRoot.gameObject);
            }
            collectionViews.Clear();
            Array.Clear(deckViews, 0, deckViews.Length);

            W = canvasRect.rect.width;
            float H = canvasRect.rect.height;
            contentW = Mathf.Min(W, H * 0.62f);
            u = contentW / REF_W;
            builtWidth = W;

            if (font == null) font = ResolveFont();
            if (outlineMaterial == null && font != null) outlineMaterial = CreateOutlineMaterial(font);
            if (shineSprite == null) shineSprite = CreateShineSprite();
            if (generatedBarSprite == null) generatedBarSprite = CreateBarSprite();
            if (capsuleSprite == null) capsuleSprite = CreateCapsuleSprite();

            gapX = 22f * u;
            gapY = 26f * u;
            cardW = (contentW - 56f * u - gapX * (COLUMNS - 1)) / COLUMNS;
            cardH = CardHeightFor(cardW);

            // Root
            pageRoot = NewRect("Page", transform);
            Stretch(pageRoot);
            pageGroup = pageRoot.gameObject.AddComponent<CanvasGroup>();
            var solid = pageRoot.gameObject.AddComponent<Image>();
            solid.color = BgBlue;
            solid.raycastTarget = true; // block clicks to the home screen underneath

            if (backgroundSprite != null)
            {
                var bg = NewImage("Background", pageRoot, backgroundSprite, Color.white, false);
                bg.rectTransform.anchorMin = bg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                bg.rectTransform.sizeDelta = new Vector2(W, H);
                var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = backgroundSprite.rect.width / Mathf.Max(1f, backgroundSprite.rect.height);
            }
            var shade = NewImage("Shade", pageRoot, null, new Color(0.02f, 0.06f, 0.20f, 0.35f), false);
            Stretch(shade.rectTransform);

            float headerH = 124f * u;
            float tabsH = 112f * u;
            float bandH = 12f * u;

            BuildHeader(headerH);
            BuildTabs(headerH, tabsH);

            var band = NewImage("TabBand", pageRoot, null, BandBlue, false);
            var brt = band.rectTransform;
            brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(0.5f, 1f);
            brt.sizeDelta = new Vector2(0f, bandH);
            brt.anchoredPosition = new Vector2(0f, -(headerH + tabsH));

            // Viewport + sliding track holding the two pages
            viewport = NewRect("Viewport", pageRoot);
            Stretch(viewport, 0f, 0f, 0f, headerH + tabsH + bandH);
            viewport.gameObject.AddComponent<RectMask2D>();

            track = NewRect("Track", viewport);
            track.anchorMin = new Vector2(0f, 0f);
            track.anchorMax = new Vector2(0f, 1f);
            track.pivot = new Vector2(0f, 0.5f);
            track.sizeDelta = new Vector2(W * 2f, 0f);
            track.anchoredPosition = Vector2.zero;

            var decksPage = BuildPage("DecksPage", 0, out deckScroll);
            var collectionPage = BuildPage("CollectionPage", 1, out collectionScroll);
            BuildDecksContent(decksPage);
            BuildCollectionContent(collectionPage);

            BuildSwapBanner(headerH + tabsH + bandH);

            dragLayer = NewRect("DragLayer", pageRoot);
            Stretch(dragLayer);
            var dragGroup = dragLayer.gameObject.AddComponent<CanvasGroup>();
            dragGroup.blocksRaycasts = false;
            dragGroup.interactable = false;

            // Popup layer sits on the root canvas so it covers the bottom nav too.
            if (popupLayer == null)
            {
                popupLayer = NewRect("[CardsPopupLayer]", canvasRect);
                Stretch(popupLayer);
            }
            if (celebrationOverlay != null)
            {
                var ort = celebrationOverlay.transform as RectTransform;
                if (ort != null)
                {
                    ort.SetParent(popupLayer, false);
                    Stretch(ort);
                }
                // Let the overlay run its own Awake once (it wires its tap-to-dismiss and hides itself).
                celebrationOverlay.gameObject.SetActive(true);
                if (celebrationOverlay.gameObject.activeSelf) celebrationOverlay.gameObject.SetActive(false);
            }

            pageRoot.gameObject.SetActive(false);
        }

        private float CardHeightFor(float w)
        {
            // art + level band (overlapping the art) + gap + progress bar
            return w * 1.40f + w * 0.22f * 0.55f + w * 0.06f + w * 0.19f + w * 0.03f;
        }

        // ───────────────────────────── Header ─────────────────────────────

        private void BuildHeader(float headerH)
        {
            header = NewRect("Header", pageRoot);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, headerH);
            var hbg = header.gameObject.AddComponent<Image>();
            hbg.color = new Color(0.02f, 0.06f, 0.22f, 0.6f);
            hbg.raycastTarget = false;

            var inner = NewRect("Inner", header);
            inner.anchorMin = inner.anchorMax = new Vector2(0.5f, 0.5f);
            inner.sizeDelta = new Vector2(contentW - 48f * u, headerH);
            var hl = inner.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.spacing = 12f * u;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;

            var icon = NewIcon("CardsIcon", inner, cardsIconSprite, new Vector2(86f, 78f) * u);
            AddLayout(icon, 86f * u, 78f * u);
            icon.DORotate(new Vector3(0f, 0f, 6f), 1.6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);

            var title = NewText("Title", inner, "CARDS", 52f * u, TextWhite, TextAlignmentOptions.MidlineLeft);
            AddLayout(title.rectTransform, 170f * u, 70f * u);

            AddFlexibleSpacer(inner);

            var coinPill = BuildCurrencyPill(inner, "Coins", coinSprite, out coinText);
            AddLayout(coinPill, 250f * u, 70f * u);
            AddSpacer(inner, 6f * u);
            var gemPill = BuildCurrencyPill(inner, "Gems", gemSprite, out gemText);
            AddLayout(gemPill, 220f * u, 70f * u);
        }

        private RectTransform BuildCurrencyPill(Transform parent, string name, Sprite icon, out TextMeshProUGUI valueText)
        {
            var pill = NewRect(name + "Pill", parent);
            var bg = NewRounded("Bg", pill, new Color(0.01f, 0.04f, 0.14f, 0.85f), 30f);
            Stretch(bg.rectTransform, 24f * u, 4f * u, 0f, 4f * u);

            var iconRT = NewIcon(name + "Icon", pill, icon, new Vector2(64f, 64f) * u);
            iconRT.anchorMin = iconRT.anchorMax = new Vector2(0f, 0.5f);
            iconRT.anchoredPosition = new Vector2(30f * u, 0f);

            valueText = NewText(name + "Value", pill, "0", 40f * u, Color.white, TextAlignmentOptions.MidlineRight);
            Stretch(valueText.rectTransform, 66f * u, 0f, 18f * u, 0f);
            valueText.enableAutoSizing = true;
            valueText.fontSizeMin = 20f * u;
            valueText.fontSizeMax = 40f * u;
            return pill;
        }

        // ───────────────────────────── Tabs ─────────────────────────────

        private void BuildTabs(float headerH, float tabsH)
        {
            var tabs = NewRect("Tabs", pageRoot);
            tabs.anchorMin = new Vector2(0.5f, 1f);
            tabs.anchorMax = new Vector2(0.5f, 1f);
            tabs.pivot = new Vector2(0.5f, 1f);
            tabs.sizeDelta = new Vector2(contentW, tabsH);
            tabs.anchoredPosition = new Vector2(0f, -headerH);

            string[] names = { "Decks", "Collection" };
            float tabW = (contentW - 70f * u) / 2f;
            for (int i = 0; i < 2; i++)
            {
                var t = NewRect(names[i] + "Tab", tabs);
                t.anchorMin = t.anchorMax = new Vector2(0.5f, 0f);
                t.pivot = new Vector2(0.5f, 0f);
                t.sizeDelta = new Vector2(tabW, tabsH - 14f * u);
                t.anchoredPosition = new Vector2((i == 0 ? -1f : 1f) * (tabW / 2f + 10f * u), -6f * u);
                tabRects[i] = t;

                var bg = NewRounded("Bg", t, TabInactive, 26f);
                Stretch(bg.rectTransform);
                bg.raycastTarget = true;
                tabBgs[i] = bg;
                var gloss = NewRounded("Gloss", t, new Color(1f, 1f, 1f, 0.12f), 22f);
                gloss.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                gloss.rectTransform.anchorMax = new Vector2(1f, 1f);
                gloss.rectTransform.offsetMin = new Vector2(8f * u, 0f);
                gloss.rectTransform.offsetMax = new Vector2(-8f * u, -6f * u);

                var label = NewText("Label", t, names[i], 48f * u, TextWhite, TextAlignmentOptions.Center);
                Stretch(label.rectTransform, 0f, 6f * u, 0f, 0f);
                tabLabels[i] = label;

                var btn = t.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = bg;
                int idx = i;
                btn.onClick.AddListener(() => OnTabClicked((Tab)idx));
            }

            // Upgrade-ready badge on the Collection tab
            collectionBadge = NewRect("Badge", tabRects[1]);
            collectionBadge.anchorMin = collectionBadge.anchorMax = new Vector2(1f, 1f);
            collectionBadge.sizeDelta = new Vector2(58f, 58f) * u;
            collectionBadge.anchoredPosition = new Vector2(-14f * u, -4f * u);
            var badgeBg = NewImage("Bg", collectionBadge, circleSprite, BadgeRed, true);
            Stretch(badgeBg.rectTransform);
            collectionBadgeText = NewText("Count", collectionBadge, "0", 34f * u, Color.white, TextAlignmentOptions.Center);
            Stretch(collectionBadgeText.rectTransform);
            collectionBadge.gameObject.SetActive(false);
        }

        private void OnTabClicked(Tab tab)
        {
            if (tab == currentTab) { PunchTab(tab); return; }
            if (tab == Tab.Collection && !string.IsNullOrEmpty(placeCardId)) CancelPlaceMode(false);
            if (tab == Tab.Decks && pendingEmptySlot >= 0) CancelPlaceMode(false);
            Sfx(tabSound != null ? tabSound : clickSound);
            SwitchTab(tab, true);
        }

        private void PunchTab(Tab tab)
        {
            var t = tabRects[(int)tab];
            t.DOKill(true);
            t.DOPunchScale(Vector3.one * 0.06f, 0.25f, 6, 0.6f).SetUpdate(true);
        }

        private void SwitchTab(Tab tab, bool animate)
        {
            bool changed = tab != currentTab;
            currentTab = tab;
            int idx = (int)tab;
            float targetX = -W * idx;

            track.DOKill();
            if (animate && changed)
            {
                track.DOAnchorPosX(targetX, 0.42f).SetEase(Ease.OutQuint).SetUpdate(true);
                for (int i = 0; i < 2; i++)
                {
                    var g = pageGroups[i];
                    g.DOKill();
                    g.DOFade(i == idx ? 1f : 0.35f, 0.3f).SetUpdate(true);
                }
            }
            else
            {
                track.anchoredPosition = new Vector2(targetX, 0f);
                for (int i = 0; i < 2; i++) pageGroups[i].alpha = i == idx ? 1f : 0.35f;
            }
            for (int i = 0; i < 2; i++) pageGroups[i].blocksRaycasts = i == idx;

            for (int i = 0; i < 2; i++)
            {
                bool on = i == idx;
                tabBgs[i].DOKill();
                tabLabels[i].DOKill();
                tabRects[i].DOKill();
                if (animate)
                {
                    tabBgs[i].DOColor(on ? TabActive : TabInactive, 0.2f).SetUpdate(true);
                    tabLabels[i].DOColor(on ? TextWhite : TextDim, 0.2f).SetUpdate(true);
                    tabRects[i].DOScale(on ? 1f : 0.94f, 0.3f).SetEase(on ? Ease.OutBack : Ease.OutQuad).SetUpdate(true);
                }
                else
                {
                    tabBgs[i].color = on ? TabActive : TabInactive;
                    tabLabels[i].color = on ? TextWhite : TextDim;
                    tabRects[i].localScale = Vector3.one * (on ? 1f : 0.94f);
                }
            }

            if (animate && changed)
            {
                if (tab == Tab.Decks) PopInViews(deckViews, 0.12f);
                else PopInViews(VisibleCollectionViews(), 0.12f);
            }
        }

        // ───────────────────────────── Pages ─────────────────────────────

        private RectTransform BuildPage(string name, int index, out ScrollRect scroll)
        {
            var page = NewRect(name, track);
            page.anchorMin = new Vector2(0f, 0f);
            page.anchorMax = new Vector2(0f, 1f);
            page.pivot = new Vector2(0f, 0.5f);
            page.sizeDelta = new Vector2(W, 0f);
            page.anchoredPosition = new Vector2(W * index, 0f);
            pageGroups[index] = page.gameObject.AddComponent<CanvasGroup>();
            var hit = page.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);

            scroll = page.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.12f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 40f * u;
            scroll.viewport = page;

            var content = NewRect("Content", page);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.childAlignment = TextAnchor.UpperCenter;
            vl.spacing = 16f * u;
            vl.padding = new RectOffset(0, 0, Mathf.RoundToInt(26f * u), Mathf.RoundToInt(240f * u));
            vl.childControlWidth = vl.childControlHeight = true;
            vl.childForceExpandWidth = vl.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            return content;
        }

        private RectTransform AddBlock(Transform parent, string name, float height, float width = -1f)
        {
            var rt = NewRect(name, parent);
            AddLayout(rt, width > 0f ? width : contentW - 40f * u, height);
            return rt;
        }

        private RectTransform BuildSectionLabel(Transform parent, string text, out TextMeshProUGUI label)
        {
            var row = AddBlock(parent, text + "Label", 60f * u);
            label = NewText("Text", row, text, 38f * u, TextWhite, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            for (int s = -1; s <= 1; s += 2)
            {
                var line = NewImage("Line", row, null, new Color(1f, 1f, 1f, 0.22f), false);
                var lrt = line.rectTransform;
                lrt.anchorMin = lrt.anchorMax = new Vector2(s < 0 ? 0f : 1f, 0.5f);
                lrt.pivot = new Vector2(s < 0 ? 0f : 1f, 0.5f);
                lrt.sizeDelta = new Vector2((contentW - 40f * u) * 0.3f, 5f * u);
            }
            return row;
        }

        private RectTransform BuildGrid(Transform parent, string name)
        {
            var grid = NewRect(name, parent);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(cardW, cardH);
            g.spacing = new Vector2(gapX, gapY);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = COLUMNS;
            g.childAlignment = TextAnchor.UpperCenter;
            g.startAxis = GridLayoutGroup.Axis.Horizontal;
            g.padding = new RectOffset(0, 0, Mathf.RoundToInt(14f * u), Mathf.RoundToInt(6f * u));
            return grid;
        }

        // ───────────────────────────── Decks page ─────────────────────────────

        private void BuildDecksContent(RectTransform content)
        {
            // "Battle Deck" ribbon with shield icon
            var ribbon = AddBlock(content, "BattleDeckRibbon", 132f * u, contentW);
            var bandImg = NewRounded("Band", ribbon, BandBlue, 20f);
            bandImg.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            bandImg.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            bandImg.rectTransform.sizeDelta = new Vector2(-40f * u, 92f * u);
            var bandGloss = NewRounded("Gloss", bandImg.rectTransform, new Color(1f, 1f, 1f, 0.14f), 16f);
            bandGloss.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            bandGloss.rectTransform.anchorMax = Vector2.one;
            bandGloss.rectTransform.offsetMin = new Vector2(6f * u, 0f);
            bandGloss.rectTransform.offsetMax = new Vector2(-6f * u, -5f * u);

            var shield = NewRect("Shield", ribbon);
            shield.anchorMin = shield.anchorMax = new Vector2(0f, 0.5f);
            shield.sizeDelta = new Vector2(128f, 128f) * u;
            shield.anchoredPosition = new Vector2(130f * u, 0f);
            var rim = NewRounded("Rim", shield, GoldRim, 26f);
            Stretch(rim.rectTransform);
            var face = NewRounded("Face", shield, new Color(0.10f, 0.36f, 0.86f, 1f), 22f);
            Stretch(face.rectTransform, 9f * u, 9f * u, 9f * u, 9f * u);
            var sIcon = NewIcon("Icon", shield, cardsIconSprite, new Vector2(92f, 92f) * u);
            sIcon.anchorMin = sIcon.anchorMax = new Vector2(0.5f, 0.5f);
            sIcon.DOScale(1.07f, 1.1f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);

            var title = NewText("Title", ribbon, "Battle Deck", 58f * u, TextWhite, TextAlignmentOptions.MidlineLeft);
            Stretch(title.rectTransform, 230f * u, 0f, 30f * u, 0f);

            // Army row
            BuildSectionLabel(content, "Army", out _);
            var armyGrid = BuildGrid(content, "ArmyGrid");
            for (int i = 0; i < CardInventoryManager.MAX_UNIT_SLOTS; i++)
                deckViews[i] = BuildCardView(armyGrid, CardViewContext.DeckSlot, i, deckScroll);

            // Power rows
            BuildSectionLabel(content, "Power Cards", out _);
            var powerGrid = BuildGrid(content, "PowerGrid");
            for (int i = 0; i < CardInventoryManager.MAX_POWERUP_SLOTS; i++)
            {
                int slot = CardInventoryManager.MAX_UNIT_SLOTS + i;
                deckViews[slot] = BuildCardView(powerGrid, CardViewContext.DeckSlot, slot, deckScroll);
            }

            // Footer: average cost + drag hint
            var footer = AddBlock(content, "Footer", 104f * u);
            var pill = NewRect("ElixirPill", footer);
            pill.anchorMin = pill.anchorMax = new Vector2(0f, 0.5f);
            pill.pivot = new Vector2(0f, 0.5f);
            pill.sizeDelta = new Vector2(250f, 88f) * u;
            var pbg = NewRounded("Bg", pill, PanelNavy, 26f);
            Stretch(pbg.rectTransform);
            elixirIcon = NewIcon("Drop", pill, elixirSprite, new Vector2(70f, 70f) * u);
            elixirIcon.anchorMin = elixirIcon.anchorMax = new Vector2(0f, 0.5f);
            elixirIcon.anchoredPosition = new Vector2(52f * u, 0f);
            elixirText = NewText("Avg", pill, "0.0", 50f * u, TextWhite, TextAlignmentOptions.MidlineLeft);
            Stretch(elixirText.rectTransform, 100f * u, 0f, 10f * u, 0f);
            var avgCaption = NewText("Caption", pill, "Avg. Cost", 22f * u, TextDim, TextAlignmentOptions.BottomLeft);
            Stretch(avgCaption.rectTransform, 102f * u, 6f * u, 10f * u, 0f);
            avgCaption.fontStyle = FontStyles.Normal;

            var hint = NewText("Hint", footer, "Hold & drag to reorder\nTap a card for details", 26f * u, TextDim, TextAlignmentOptions.MidlineRight);
            Stretch(hint.rectTransform, 280f * u, 0f, 6f * u, 0f);
            hint.fontStyle = FontStyles.Normal;
            hint.textWrappingMode = TextWrappingModes.Normal;

            // (King "Royal Army" preview panel removed: the deck page ends with the footer.)
        }

        private void BuildKingPanel(RectTransform content)
        {
            var divider = AddBlock(content, "Divider", 8f * u);
            var line = NewImage("Line", divider, null, new Color(1f, 1f, 1f, 0.18f), false);
            Stretch(line.rectTransform, 0f, 2f * u, 0f, 2f * u);

            var panel = AddBlock(content, "KingPanel", 300f * u);
            var bg = NewRounded("Bg", panel, PanelNavy, 34f);
            Stretch(bg.rectTransform);

            if (towerSprite != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    var tower = NewImage("Tower" + i, panel, towerSprite, new Color(1f, 1f, 1f, i == 0 ? 0.95f : 0.8f), true);
                    var trt = tower.rectTransform;
                    trt.anchorMin = trt.anchorMax = new Vector2(1f, 0f);
                    trt.pivot = new Vector2(0.5f, 0f);
                    float s = i == 0 ? 230f : 170f;
                    trt.sizeDelta = new Vector2(s, s) * u;
                    trt.anchoredPosition = new Vector2((i == 0 ? -150f : -300f) * u, 18f * u);
                    if (i == 1) trt.SetAsFirstSibling();
                }
                bg.rectTransform.SetAsFirstSibling();
            }

            var portrait = NewRect("King", panel);
            portrait.anchorMin = portrait.anchorMax = new Vector2(0f, 0.5f);
            portrait.sizeDelta = new Vector2(230f, 230f) * u;
            portrait.anchoredPosition = new Vector2(150f * u, 0f);
            if (glowSprite != null)
            {
                var g = NewImage("Glow", portrait, glowSprite, new Color(0.45f, 0.75f, 1f, 0.55f), true);
                g.rectTransform.sizeDelta = new Vector2(320f, 320f) * u;
                g.rectTransform.DOScale(1.1f, 1.4f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
            }
            var kimg = NewImage("Portrait", portrait, kingPortraitSprite, Color.white, true);
            Stretch(kimg.rectTransform);
            portrait.DOAnchorPosY(8f * u, 1.8f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);

            var lvl = NewRect("LevelBadge", portrait);
            lvl.anchorMin = lvl.anchorMax = new Vector2(0.5f, 0f);
            lvl.sizeDelta = new Vector2(84f, 84f) * u;
            lvl.anchoredPosition = new Vector2(0f, 8f * u);
            var lrim = NewImage("Rim", lvl, circleSprite, GoldRim, true);
            Stretch(lrim.rectTransform);
            var lface = NewImage("Face", lvl, circleSprite, new Color(0.10f, 0.32f, 0.80f, 1f), true);
            Stretch(lface.rectTransform, 7f * u, 7f * u, 7f * u, 7f * u);
            kingLevelText = NewText("Level", lvl, "1", 42f * u, Color.white, TextAlignmentOptions.Center);
            Stretch(kingLevelText.rectTransform);

            var title = NewText("Title", panel, "Royal Army", 46f * u, TextWhite, TextAlignmentOptions.TopLeft);
            Stretch(title.rectTransform, 290f * u, 0f, 20f * u, 60f * u);
            deckLevelText = NewText("DeckLevel", panel, "", 32f * u, new Color(0.65f, 0.86f, 1f), TextAlignmentOptions.TopLeft);
            Stretch(deckLevelText.rectTransform, 292f * u, 0f, 20f * u, 125f * u);
            deckPowerText = NewText("DeckPower", panel, "", 32f * u, new Color(0.55f, 1f, 0.55f), TextAlignmentOptions.TopLeft);
            Stretch(deckPowerText.rectTransform, 292f * u, 0f, 20f * u, 172f * u);
        }

        // ───────────────────────────── Collection page ─────────────────────────────

        private void BuildCollectionContent(RectTransform content)
        {
            // Filter bar
            var bar = AddBlock(content, "FilterBar", 150f * u, contentW);
            var bandImg = NewRounded("Band", bar, BandBlue, 20f);
            Stretch(bandImg.rectTransform, 20f * u, 10f * u, 20f * u, 10f * u);

            Sprite[] icons = { cardsIconSprite, armyIconSprite, powerIconSprite, arrowUpSprite };
            string[] labels = { "All", "Army", "Power", "Upgrade" };
            float chipW = 170f * u, chipH = 118f * u;
            float startX = -1.5f * (chipW + 34f * u);
            for (int i = 0; i < 4; i++)
            {
                var chip = NewRect(labels[i] + "Chip", bar);
                chip.anchorMin = chip.anchorMax = new Vector2(0.5f, 0.5f);
                chip.sizeDelta = new Vector2(chipW, chipH);
                chip.anchoredPosition = new Vector2(startX + i * (chipW + 34f * u), 0f);
                filterChips[i] = chip;

                var cbg = NewRounded("Bg", chip, new Color(0.04f, 0.16f, 0.46f, 0.9f), 24f);
                Stretch(cbg.rectTransform);
                cbg.raycastTarget = true;
                filterBgs[i] = cbg;

                var ic = NewIcon("Icon", chip, icons[i], new Vector2(70f, 64f) * u);
                ic.anchorMin = ic.anchorMax = new Vector2(0.5f, 0.62f);
                if (i == 3)
                {
                    var g = ic.GetComponentInChildren<Image>();
                    if (g != null) g.color = BarGreen;
                }
                var t = NewText("Label", chip, labels[i], 26f * u, TextWhite, TextAlignmentOptions.Bottom);
                Stretch(t.rectTransform, 0f, 8f * u, 0f, 0f);

                var btn = chip.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = cbg;
                chip.gameObject.AddComponent<ShopPressFeedback>();
                int idx = i;
                btn.onClick.AddListener(() =>
                {
                    Sfx(clickSound);
                    SetFilter((Filter)idx, true);
                });
            }

            // Info panel: title, found count, sort controls
            var info = AddBlock(content, "InfoPanel", 150f * u);
            var ibg = NewRounded("Bg", info, PanelNavy, 30f);
            Stretch(ibg.rectTransform);
            var title = NewText("Title", info, "Card Collection", 48f * u, TextWhite, TextAlignmentOptions.TopLeft);
            Stretch(title.rectTransform, 30f * u, 0f, 380f * u, 22f * u);
            foundCountText = NewText("Found", info, "Found: 0/0", 36f * u, FoundPink, TextAlignmentOptions.BottomLeft);
            Stretch(foundCountText.rectTransform, 30f * u, 22f * u, 380f * u, 0f);

            var sortBtn = NewRect("SortButton", info);
            sortBtn.anchorMin = sortBtn.anchorMax = new Vector2(1f, 0.5f);
            sortBtn.pivot = new Vector2(1f, 0.5f);
            sortBtn.sizeDelta = new Vector2(240f, 92f) * u;
            sortBtn.anchoredPosition = new Vector2(-22f * u, 0f);
            var sbg = NewRounded("Bg", sortBtn, new Color(0.18f, 0.40f, 0.78f, 1f), 22f);
            Stretch(sbg.rectTransform);
            sbg.raycastTarget = true;
            var sgloss = NewRounded("Gloss", sortBtn, new Color(1f, 1f, 1f, 0.14f), 18f);
            sgloss.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            sgloss.rectTransform.anchorMax = Vector2.one;
            sgloss.rectTransform.offsetMin = new Vector2(6f * u, 0f);
            sgloss.rectTransform.offsetMax = new Vector2(-6f * u, -5f * u);
            sortLabel = NewText("Label", sortBtn, "By Level", 34f * u, TextWhite, TextAlignmentOptions.Center);
            Stretch(sortLabel.rectTransform);
            var sb = sortBtn.gameObject.AddComponent<Button>();
            sb.transition = Selectable.Transition.None;
            sb.targetGraphic = sbg;
            sortBtn.gameObject.AddComponent<ShopPressFeedback>();
            sb.onClick.AddListener(() =>
            {
                Sfx(clickSound);
                sortMode = (SortMode)(((int)sortMode + 1) % 4);
                sortDescending = sortMode == SortMode.Level || sortMode == SortMode.Rarity;
                RebuildCollection(true);
            });

            var dirBtn = NewRect("DirButton", info);
            dirBtn.anchorMin = dirBtn.anchorMax = new Vector2(1f, 0.5f);
            dirBtn.pivot = new Vector2(1f, 0.5f);
            dirBtn.sizeDelta = new Vector2(92f, 92f) * u;
            dirBtn.anchoredPosition = new Vector2(-276f * u, 0f);
            var dbg = NewRounded("Bg", dirBtn, new Color(0.18f, 0.40f, 0.78f, 1f), 22f);
            Stretch(dbg.rectTransform);
            dbg.raycastTarget = true;
            sortDirIcon = NewIcon("Arrow", dirBtn, arrowUpSprite, new Vector2(48f, 48f) * u);
            sortDirIcon.anchorMin = sortDirIcon.anchorMax = new Vector2(0.5f, 0.5f);
            var dimg = sortDirIcon.GetComponentInChildren<Image>();
            if (dimg != null) dimg.color = BarGreen;
            var db = dirBtn.gameObject.AddComponent<Button>();
            db.transition = Selectable.Transition.None;
            db.targetGraphic = dbg;
            dirBtn.gameObject.AddComponent<ShopPressFeedback>();
            db.onClick.AddListener(() =>
            {
                Sfx(clickSound);
                sortDescending = !sortDescending;
                RebuildCollection(true);
            });

            foundHeader = BuildSectionLabel(content, "Found", out _);
            foundGrid = BuildGrid(content, "FoundGrid");
            emptyNote = AddBlock(content, "EmptyNote", 120f * u);
            var en = NewText("Text", emptyNote, "No cards match this filter yet.", 32f * u, TextDim, TextAlignmentOptions.Center);
            Stretch(en.rectTransform);
            notFoundHeader = BuildSectionLabel(content, "Not found", out _);
            notFoundGrid = BuildGrid(content, "NotFoundGrid");
            UpdateFilterChips(false);
        }

        private void SetFilter(Filter f, bool animate)
        {
            filter = f;
            UpdateFilterChips(animate);
            RebuildCollection(animate);
        }

        private void UpdateFilterChips(bool animate)
        {
            for (int i = 0; i < 4; i++)
            {
                bool on = i == (int)filter;
                filterBgs[i].DOKill();
                filterChips[i].DOKill();
                var col = on ? new Color(0.98f, 0.76f, 0.20f, 1f) : new Color(0.04f, 0.16f, 0.46f, 0.9f);
                if (animate)
                {
                    filterBgs[i].DOColor(col, 0.18f).SetUpdate(true);
                    filterChips[i].DOScale(on ? 1.08f : 1f, 0.28f).SetEase(Ease.OutBack).SetUpdate(true);
                }
                else
                {
                    filterBgs[i].color = col;
                    filterChips[i].localScale = Vector3.one * (on ? 1.08f : 1f);
                }
            }
        }

        // ───────────────────────────── Swap banner ─────────────────────────────

        private void BuildSwapBanner(float top)
        {
            swapBanner = NewRect("SwapBanner", pageRoot);
            swapBanner.anchorMin = swapBanner.anchorMax = new Vector2(0.5f, 1f);
            swapBanner.pivot = new Vector2(0.5f, 1f);
            swapBanner.sizeDelta = new Vector2(contentW - 40f * u, 112f * u);
            swapBanner.anchoredPosition = new Vector2(0f, -top - 8f * u);
            var bg = NewRounded("Bg", swapBanner, new Color(0.10f, 0.08f, 0.22f, 0.96f), 28f);
            Stretch(bg.rectTransform);
            bg.raycastTarget = true;
            var rim = NewRounded("Rim", swapBanner, GoldRim, 28f);
            Stretch(rim.rectTransform);
            rim.fillCenter = false;

            swapBannerText = NewText("Text", swapBanner, "", 32f * u, TextWhite, TextAlignmentOptions.MidlineLeft);
            Stretch(swapBannerText.rectTransform, 30f * u, 0f, 220f * u, 0f);
            swapBannerText.textWrappingMode = TextWrappingModes.Normal;
            swapBannerText.enableAutoSizing = true;
            swapBannerText.fontSizeMin = 20f * u;
            swapBannerText.fontSizeMax = 32f * u;

            var cancel = BuildButton(swapBanner, "Cancel", greyButtonSprite, new Color(0.6f, 0.62f, 0.7f), new Vector2(180f, 78f) * u, 32f * u);
            cancel.anchorMin = cancel.anchorMax = new Vector2(1f, 0.5f);
            cancel.pivot = new Vector2(1f, 0.5f);
            cancel.anchoredPosition = new Vector2(-18f * u, 0f);
            cancel.GetComponent<Button>().onClick.AddListener(() => { Sfx(clickSound); CancelPlaceMode(true); });

            swapBanner.gameObject.SetActive(false);
        }

        private void ShowSwapBanner(string message)
        {
            swapBannerText.text = message;
            swapBanner.gameObject.SetActive(true);
            swapBanner.SetAsLastSibling();
            dragLayer.SetAsLastSibling();
            swapBanner.DOKill();
            swapBanner.localScale = new Vector3(1f, 0f, 1f);
            swapBanner.DOScaleY(1f, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        private void HideSwapBanner()
        {
            if (swapBanner == null || !swapBanner.gameObject.activeSelf) return;
            swapBanner.DOKill();
            swapBanner.DOScaleY(0f, 0.16f).SetEase(Ease.InQuad).SetUpdate(true)
                .OnComplete(() => swapBanner.gameObject.SetActive(false));
        }

        // ═════════════════════════════ Card view ═════════════════════════════

        private CardsScreenCardView BuildCardView(Transform parent, CardViewContext context, int slotIndex, ScrollRect scroll, float width = -1f)
        {
            float cw = width > 0f ? width : cardW;
            float ch = CardHeightFor(cw);

            var cell = NewRect(context == CardViewContext.DeckSlot ? "Slot" + slotIndex : "Card", parent);
            cell.sizeDelta = new Vector2(cw, ch);
            var hit = cell.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            var v = cell.gameObject.AddComponent<CardsScreenCardView>();
            v.root = cell;
            v.context = context;
            v.slotIndex = slotIndex;
            v.owner = this;
            v.scrollParent = scroll;

            // Visual is centred with a fixed size so it can be re-parented while dragging.
            var visual = NewRect("Visual", cell);
            visual.anchorMin = visual.anchorMax = new Vector2(0.5f, 0.5f);
            visual.sizeDelta = new Vector2(cw, ch);
            v.visual = visual;
            v.visualGroup = visual.gameObject.AddComponent<CanvasGroup>();

            float artH = cw * 1.40f;
            float bandH = cw * 0.22f;
            float bandTop = artH - bandH * 0.45f;
            float barTop = bandTop + bandH + cw * 0.06f;
            float barH = cw * 0.19f;

            // Glow (drag / hover)
            if (glowSprite != null)
            {
                v.glow = NewImage("Glow", visual, glowSprite, new Color(1f, 0.85f, 0.35f, 0.9f), false);
                PlaceTop(v.glow.rectTransform, 0f, -cw * 0.25f, cw * 1.6f, artH + cw * 0.5f);
                v.glow.gameObject.SetActive(false);
            }

            // Rarity plate (frame rim around art + band)
            v.plate = NewRounded("Plate", visual, Color.grey, 16f * cw / cardW);
            PlaceTop(v.plate.rectTransform, 0f, 0f, cw, bandTop + bandH);

            // Art
            v.art = NewImage("Art", visual, null, Color.white, true);
            PlaceTop(v.art.rectTransform, 0f, cw * 0.035f, cw * 0.93f, artH - cw * 0.035f);
            v.artFallbackText = NewText("Name", v.art.rectTransform, "", cw * 0.13f, TextWhite, TextAlignmentOptions.Center);
            Stretch(v.artFallbackText.rectTransform, cw * 0.05f, 0f, cw * 0.05f, 0f);
            v.artFallbackText.textWrappingMode = TextWrappingModes.Normal;

            // Level band
            v.levelBand = NewRounded("LevelBand", visual, Color.grey, 12f * cw / cardW);
            PlaceTop(v.levelBand.rectTransform, 0f, bandTop, cw * 0.97f, bandH);
            var bandGloss = NewRounded("Gloss", v.levelBand.rectTransform, new Color(1f, 1f, 1f, 0.18f), 10f * cw / cardW);
            bandGloss.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            bandGloss.rectTransform.anchorMax = Vector2.one;
            bandGloss.rectTransform.offsetMin = new Vector2(cw * 0.02f, 0f);
            bandGloss.rectTransform.offsetMax = new Vector2(-cw * 0.02f, -cw * 0.015f);
            v.levelText = NewText("Level", v.levelBand.rectTransform, "Level 1", cw * 0.15f, TextWhite, TextAlignmentOptions.Center);
            Stretch(v.levelText.rectTransform);

            // Progress bar
            v.barRoot = NewRect("Bar", visual);
            PlaceTop(v.barRoot, 0f, barTop, cw * 0.95f, barH);
            var trackImg = NewImage("Track", v.barRoot, barTrackSprite, barTrackSprite != null ? Color.white : new Color(0f, 0.04f, 0.15f, 0.9f), false);
            Stretch(trackImg.rectTransform);
            v.barFill = NewImage("Fill", v.barRoot, BarFill, BarBlue, false);
            Stretch(v.barFill.rectTransform, barH * 0.14f, barH * 0.16f, barH * 0.14f, barH * 0.16f);
            v.barFill.type = Image.Type.Filled;
            v.barFill.fillMethod = Image.FillMethod.Horizontal;
            v.barFill.fillOrigin = 0;
            v.barFill.fillAmount = 0.5f;
            v.barText = NewText("Count", v.barRoot, "0/2", barH * 0.78f, TextWhite, TextAlignmentOptions.Center);
            Stretch(v.barText.rectTransform, barH * 0.6f, 0f, 0f, 0f);
            var arrow = NewIcon("Arrow", v.barRoot, arrowUpSprite, new Vector2(barH * 1.35f, barH * 1.35f));
            arrow.anchorMin = arrow.anchorMax = new Vector2(0f, 0.5f);
            arrow.anchoredPosition = new Vector2(barH * 0.45f, 0f);
            v.barArrow = arrow;

            // Cost (elixir drop)
            v.costBadge = NewRect("Cost", visual);
            v.costBadge.anchorMin = v.costBadge.anchorMax = new Vector2(0f, 1f);
            v.costBadge.sizeDelta = new Vector2(cw * 0.33f, cw * 0.33f);
            v.costBadge.anchoredPosition = new Vector2(cw * 0.06f, -cw * 0.07f);
            var drop = NewImage("Drop", v.costBadge, elixirSprite, elixirSprite != null ? Color.white : new Color(0.85f, 0.2f, 0.85f), true);
            Stretch(drop.rectTransform);
            v.costText = NewText("Value", v.costBadge, "3", cw * 0.17f, Color.white, TextAlignmentOptions.Center);
            Stretch(v.costText.rectTransform, 0f, 0f, 0f, cw * 0.06f);

            // New! ribbon
            v.newBadge = NewRect("New", visual);
            v.newBadge.anchorMin = v.newBadge.anchorMax = new Vector2(1f, 1f);
            v.newBadge.pivot = new Vector2(0.75f, 0.6f);
            v.newBadge.sizeDelta = new Vector2(cw * 0.62f, cw * 0.24f);
            v.newBadge.anchoredPosition = new Vector2(-cw * 0.06f, -cw * 0.04f);
            if (glowSprite != null)
            {
                v.newGlow = NewImage("Glow", v.newBadge, glowSprite, new Color(1f, 0.35f, 0.6f, 0.6f), false);
                Stretch(v.newGlow.rectTransform, -cw * 0.15f, -cw * 0.12f, -cw * 0.15f, -cw * 0.12f);
            }
            var rib = newRibbonSprite != null
                ? NewImage("Ribbon", v.newBadge, newRibbonSprite, Color.white, false)
                : NewRounded("Ribbon", v.newBadge, BadgeRed, 10f);
            Stretch(rib.rectTransform);
            var newText = NewText("Text", v.newBadge, "New!", cw * 0.13f, Color.white, TextAlignmentOptions.Center);
            Stretch(newText.rectTransform, 0f, cw * 0.02f, 0f, 0f);

            // In-deck tag (collection only)
            v.inDeckTag = NewRect("InDeck", visual);
            v.inDeckTag.anchorMin = v.inDeckTag.anchorMax = new Vector2(1f, 1f);
            v.inDeckTag.pivot = new Vector2(1f, 1f);
            v.inDeckTag.sizeDelta = new Vector2(cw * 0.46f, cw * 0.16f);
            v.inDeckTag.anchoredPosition = new Vector2(-cw * 0.06f, -cw * 0.07f);
            var tagBg = NewRounded("Bg", v.inDeckTag, new Color(0.12f, 0.55f, 0.98f, 0.95f), 10f * cw / cardW);
            Stretch(tagBg.rectTransform);
            var tagText = NewText("Text", v.inDeckTag, "In Deck", cw * 0.1f, Color.white, TextAlignmentOptions.Center);
            Stretch(tagText.rectTransform);

            // Locked (Not found)
            v.lockedRoot = NewRect("Locked", visual);
            PlaceTop(v.lockedRoot, 0f, artH * 0.62f, cw * 0.93f, artH * 0.38f);
            var lockFade = NewImage("Fade", v.lockedRoot, null, new Color(0f, 0f, 0f, 0.45f), false);
            Stretch(lockFade.rectTransform);
            if (lockSprite != null)
            {
                var lk = NewImage("Lock", v.lockedRoot, lockSprite, new Color(1f, 1f, 1f, 0.9f), true);
                lk.rectTransform.anchorMin = lk.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                lk.rectTransform.sizeDelta = new Vector2(cw * 0.2f, cw * 0.2f);
                lk.rectTransform.anchoredPosition = new Vector2(0f, cw * 0.02f);
            }
            v.lockedText = NewText("Text", v.lockedRoot, "", cw * 0.12f, Color.white, TextAlignmentOptions.Center);
            Stretch(v.lockedText.rectTransform, 0f, cw * 0.03f, 0f, cw * 0.12f);
            v.lockedText.textWrappingMode = TextWrappingModes.Normal;

            // Empty slot
            v.emptyRoot = NewRect("Empty", visual);
            PlaceTop(v.emptyRoot, 0f, 0f, cw, bandTop + bandH);
            var ebg = NewRounded("Bg", v.emptyRoot, new Color(0.02f, 0.07f, 0.22f, 0.75f), 18f * cw / cardW);
            Stretch(ebg.rectTransform);
            var erim = NewRounded("Rim", v.emptyRoot, new Color(0.45f, 0.65f, 1f, 0.55f), 18f * cw / cardW);
            Stretch(erim.rectTransform);
            erim.fillCenter = false;
            var plus = NewText("Plus", v.emptyRoot, "+", cw * 0.5f, new Color(0.55f, 0.75f, 1f, 0.9f), TextAlignmentOptions.Center);
            Stretch(plus.rectTransform, 0f, cw * 0.2f, 0f, 0f);
            var eText = NewText("Text", v.emptyRoot, "Empty", cw * 0.13f, TextDim, TextAlignmentOptions.Bottom);
            Stretch(eText.rectTransform, 0f, cw * 0.12f, 0f, 0f);

            // Swap highlight ring
            v.highlight = NewRounded("Highlight", visual, GoldRim, 18f * cw / cardW);
            PlaceTop(v.highlight.rectTransform, 0f, -cw * 0.03f, cw * 1.06f, bandTop + bandH + cw * 0.06f);
            v.highlight.fillCenter = false;
            v.highlight.gameObject.SetActive(false);

            return v;
        }

        private void ApplyCard(CardsScreenCardView v, CardDefinition def)
        {
            if (v == null) return;
            bool empty = def == null;
            var entry = empty ? null : EntryOf(def.cardId);
            bool found = !empty && IsFound(entry);
            bool isNew = found && entry.isNew;
            bool ready = CopiesReady(def, entry);
            bool max = found && def.IsMaxLevel(entry.level);
            bool inDeck = !empty && InDeck(def.cardId);

            v.emptyRoot.gameObject.SetActive(empty);
            v.plate.gameObject.SetActive(!empty);
            v.art.gameObject.SetActive(!empty);
            v.levelBand.gameObject.SetActive(found);
            v.barRoot.gameObject.SetActive(found);
            v.costBadge.gameObject.SetActive(!empty);
            v.newBadge.gameObject.SetActive(isNew);
            v.inDeckTag.gameObject.SetActive(v.context == CardViewContext.Collection && inDeck && !isNew);
            v.lockedRoot.gameObject.SetActive(!empty && !found);
            if (v.glow != null) v.glow.gameObject.SetActive(false);

            if (!empty)
            {
                v.art.sprite = def.cardSprite;
                v.art.enabled = true;
                v.artFallbackText.text = def.cardSprite == null ? def.displayName : "";
                if (def.cardSprite == null) v.art.color = new Color(0.15f, 0.2f, 0.35f, 1f);

                if (found)
                {
                    v.art.material = null;
                    if (def.cardSprite != null) v.art.color = Color.white;
                    v.plate.color = RarityPlate(def.rarity);
                    v.levelBand.color = RarityBand(def.rarity);
                    v.levelText.text = max ? "Max Level" : "Level " + entry.level;

                    if (max)
                    {
                        v.barFill.fillAmount = 1f;
                        v.barFill.color = BarGold;
                        v.barText.text = "MAX";
                    }
                    else
                    {
                        int need = def.CopiesToUpgrade(entry.level);
                        float fill = need > 0 ? Mathf.Clamp01(entry.copiesOwned / (float)need) : 1f;
                        v.barFill.fillAmount = fill;
                        v.barFill.color = ready ? BarGreen : BarBlue;
                        v.barText.text = entry.copiesOwned + "/" + need;
                    }
                    var arrowImg = v.barArrow.GetComponentInChildren<Image>();
                    if (arrowImg != null) arrowImg.color = ready ? BarGreen : new Color(0.35f, 0.75f, 1f, 1f);
                }
                else
                {
                    if (grayscaleMaterial != null)
                    {
                        v.art.material = grayscaleMaterial;
                        if (def.cardSprite != null) v.art.color = Color.white;
                    }
                    else if (def.cardSprite != null) v.art.color = new Color(0.32f, 0.34f, 0.40f, 1f);
                    v.plate.color = new Color(0.22f, 0.24f, 0.30f, 1f);
                    v.lockedText.text = "Unlocks at\n<size=135%><color=#E58BFF>Arena " + Mathf.Max(1, def.arenaUnlockLevel) + "</color></size>";
                }
                v.costText.text = def.elixirCost.ToString();
            }

            v.SetState(def, entry, found, isNew, ready);
        }

        // ═════════════════════════════ Refresh ═════════════════════════════

        private void RefreshAll(bool animate)
        {
            RefreshCounters(false);
            RefreshDeck(animate);
            RebuildCollection(false);
            RefreshBadge();
        }

        private CardDefinition DeckCardAt(int slot)
        {
            if (inventory == null || library == null) return null;
            if (slot < CardInventoryManager.MAX_UNIT_SLOTS)
                return slot < inventory.UnitDeck.Count ? library.GetCard(inventory.UnitDeck[slot]) : null;
            int p = slot - CardInventoryManager.MAX_UNIT_SLOTS;
            return p < inventory.PowerupDeck.Count ? library.GetCard(inventory.PowerupDeck[p]) : null;
        }

        private void RefreshDeck(bool animate)
        {
            for (int i = 0; i < deckViews.Length; i++)
                ApplyCard(deckViews[i], DeckCardAt(i));

            float avg = inventory != null ? inventory.GetDeckElixirAverage() : 0f;
            if (elixirText != null)
            {
                DOTween.Kill(elixirText);
                if (animate && Mathf.Abs(avg - shownElixir) > 0.01f)
                {
                    DOTween.To(() => shownElixir, x => { shownElixir = x; elixirText.text = x.ToString("0.0"); }, avg, 0.4f)
                        .SetTarget(elixirText).SetUpdate(true);
                    elixirIcon.DOKill(true);
                    elixirIcon.DOPunchScale(Vector3.one * 0.25f, 0.4f, 6, 0.6f).SetUpdate(true);
                }
                else
                {
                    shownElixir = avg;
                    elixirText.text = avg.ToString("0.0");
                }
            }

            // King panel stats (average level of the deck, stat bonus)
            int count = 0, levelSum = 0;
            for (int i = 0; i < deckViews.Length; i++)
            {
                var v = deckViews[i];
                if (v == null || v.Entry == null) continue;
                count++;
                levelSum += v.Entry.level;
            }
            float avgLevel = count > 0 ? levelSum / (float)count : 1f;
            if (kingLevelText != null) kingLevelText.text = Mathf.FloorToInt(avgLevel).ToString();
            if (deckLevelText != null) deckLevelText.text = "Deck level " + avgLevel.ToString("0.0") + "  ·  " + count + "/" + CardInventoryManager.MAX_DECK_SIZE + " cards";
            if (deckPowerText != null) deckPowerText.text = "Power bonus +" + Mathf.RoundToInt((avgLevel - 1f) * 10f) + "%";
        }

        private void RebuildCollection(bool animate)
        {
            if (foundGrid == null) return;
            foreach (var v in collectionViews) if (v != null) { KillTree(v.transform); Destroy(v.gameObject); }
            collectionViews.Clear();

            var all = library != null ? library.GetAllCards() : new List<CardDefinition>();
            int foundTotal = all.Count(d => IsFound(EntryOf(d.cardId)));
            foundCountText.text = "Found: " + foundTotal + "/" + all.Count;

            IEnumerable<CardDefinition> pool = all;
            switch (filter)
            {
                case Filter.Army: pool = pool.Where(d => d.cardType == CardType.Army); break;
                case Filter.Power: pool = pool.Where(d => d.cardType == CardType.Power); break;
                case Filter.Upgradable: pool = pool.Where(d => CopiesReady(d, EntryOf(d.cardId))); break;
            }

            var found = pool.Where(d => IsFound(EntryOf(d.cardId))).ToList();
            var notFound = pool.Where(d => !IsFound(EntryOf(d.cardId)))
                .OrderBy(d => d.arenaUnlockLevel).ThenBy(d => d.displayName).ToList();
            found = SortCards(found);

            foreach (var d in found)
            {
                var v = BuildCardView(foundGrid, CardViewContext.Collection, -1, collectionScroll);
                ApplyCard(v, d);
                collectionViews.Add(v);
            }
            foreach (var d in notFound)
            {
                var v = BuildCardView(notFoundGrid, CardViewContext.Collection, -1, collectionScroll);
                ApplyCard(v, d);
                collectionViews.Add(v);
            }

            foundHeader.gameObject.SetActive(found.Count > 0);
            foundGrid.gameObject.SetActive(found.Count > 0);
            notFoundHeader.gameObject.SetActive(notFound.Count > 0);
            notFoundGrid.gameObject.SetActive(notFound.Count > 0);
            emptyNote.gameObject.SetActive(found.Count == 0 && notFound.Count == 0);

            if (sortLabel != null)
            {
                sortLabel.text = "By " + sortMode;
                sortDirIcon.DOKill();
                if (animate) sortDirIcon.DOLocalRotate(new Vector3(0f, 0f, sortDescending ? 180f : 0f), 0.25f).SetEase(Ease.OutBack).SetUpdate(true);
                else sortDirIcon.localRotation = Quaternion.Euler(0f, 0f, sortDescending ? 180f : 0f);
            }

            if (animate && isOpen && currentTab == Tab.Collection)
            {
                Canvas.ForceUpdateCanvases();
                PopInViews(VisibleCollectionViews(), 0f);
            }
        }

        private List<CardDefinition> SortCards(List<CardDefinition> list)
        {
            Func<CardDefinition, int> level = d => EntryOf(d.cardId)?.level ?? 1;
            IOrderedEnumerable<CardDefinition> o;
            switch (sortMode)
            {
                case SortMode.Rarity:
                    o = sortDescending ? list.OrderByDescending(d => (int)d.rarity) : list.OrderBy(d => (int)d.rarity);
                    o = o.ThenByDescending(level);
                    break;
                case SortMode.Cost:
                    o = sortDescending ? list.OrderByDescending(d => d.elixirCost) : list.OrderBy(d => d.elixirCost);
                    break;
                case SortMode.Name:
                    o = sortDescending ? list.OrderByDescending(d => d.displayName) : list.OrderBy(d => d.displayName);
                    break;
                default:
                    o = sortDescending ? list.OrderByDescending(level) : list.OrderBy(level);
                    o = o.ThenByDescending(d => (int)d.rarity);
                    break;
            }
            return o.ThenBy(d => d.displayName).ToList();
        }

        /// <summary>Re-applies state to existing collection views (in-deck tags, bars) without rebuilding.</summary>
        private void RefreshCollectionStates()
        {
            foreach (var v in collectionViews) if (v != null && v.Def != null) ApplyCard(v, v.Def);
        }

        private void RefreshBadge()
        {
            int n = inventory != null ? inventory.GetUpgradeableCardsCount() : 0;
            if (collectionBadge == null) return;
            collectionBadge.gameObject.SetActive(n > 0);
            collectionBadgeText.text = n > 9 ? "9+" : n.ToString();
            if (n > 0 && n != lastBadgeCount && isOpen)
            {
                collectionBadge.DOKill(true);
                collectionBadge.localScale = Vector3.one;
                collectionBadge.DOPunchScale(Vector3.one * 0.35f, 0.45f, 7, 0.6f).SetUpdate(true);
            }
            lastBadgeCount = n;
        }

        private void RefreshCounters(bool punch)
        {
            if (coinText != null) coinText.text = PlayerWallet.Coins.ToString("N0");
            if (gemText != null) gemText.text = PlayerWallet.Gems.ToString("N0");
            if (punch && coinText != null)
            {
                coinText.rectTransform.DOKill(true);
                coinText.rectTransform.DOPunchScale(Vector3.one * 0.18f, 0.3f, 6, 0.6f).SetUpdate(true);
            }
        }

        // ═════════════════════════════ Animation ═════════════════════════════

        private void PlayEntrance()
        {
            pageGroup.alpha = 0f;
            pageGroup.DOFade(1f, 0.2f).SetUpdate(true);

            header.DOKill(true);
            var hp = header.anchoredPosition;
            header.anchoredPosition = hp + new Vector2(0f, 60f * u);
            header.DOAnchorPos(hp, 0.35f).SetEase(Ease.OutCubic).SetUpdate(true);

            for (int i = 0; i < 2; i++)
            {
                var t = tabRects[i];
                float target = i == (int)currentTab ? 1f : 0.94f;
                t.localScale = Vector3.zero;
                t.DOScale(target, 0.35f).SetDelay(0.05f + i * 0.06f).SetEase(Ease.OutBack).SetUpdate(true);
            }

            if (currentTab == Tab.Decks) PopInViews(deckViews, 0.15f);
            else PopInViews(VisibleCollectionViews(), 0.15f);
        }

        private void PopInViews(IList<CardsScreenCardView> views, float delay)
        {
            int k = 0;
            for (int i = 0; i < views.Count; i++)
            {
                var v = views[i];
                if (v == null || v.visual == null) continue;
                var vis = v.visual;
                vis.DOKill();
                vis.localScale = Vector3.zero;
                vis.localRotation = Quaternion.Euler(0f, 0f, (k % 2 == 0 ? -1f : 1f) * 6f);
                float d = delay + k * 0.035f;
                vis.DOScale(1f, 0.38f).SetDelay(d).SetEase(Ease.OutBack, 1.8f).SetUpdate(true);
                vis.DOLocalRotate(Vector3.zero, 0.38f).SetDelay(d).SetEase(Ease.OutBack).SetUpdate(true);
                k++;
            }
        }

        /// <summary>Collection views currently inside (or just below) the visible scroll area, top first.</summary>
        private List<CardsScreenCardView> VisibleCollectionViews()
        {
            var result = new List<CardsScreenCardView>();
            if (collectionScroll == null) return result;
            var vp = collectionScroll.viewport;
            var corners = new Vector3[4];
            vp.GetWorldCorners(corners);
            float minY = corners[0].y, maxY = corners[1].y;
            float margin = (maxY - minY) * 0.1f;
            foreach (var v in collectionViews)
            {
                if (v == null || !v.gameObject.activeInHierarchy) continue;
                float y = v.root.position.y;
                if (y < minY - margin || y > maxY + margin) continue;
                result.Add(v);
            }
            if (result.Count == 0) result.AddRange(collectionViews.Take(16));
            return result;
        }

        // ═════════════════════════════ Card interactions ═════════════════════════════

        internal void OnCardTapped(CardsScreenCardView v)
        {
            if (popupBusy || dragView != null) return;

            if (v.context == CardViewContext.DeckSlot)
            {
                if (!string.IsNullOrEmpty(placeCardId))
                {
                    TryPlaceInto(v);
                    return;
                }
                if (v.IsEmpty)
                {
                    Sfx(clickSound);
                    pendingEmptySlot = v.slotIndex;
                    var type = v.SlotType;
                    SetFilter(type == CardType.Army ? Filter.Army : Filter.Power, false);
                    SwitchTab(Tab.Collection, true);
                    ShowSwapBanner("Pick a" + (type == CardType.Army ? "n Army" : " Power") + " card and tap Use to fill the empty slot.");
                    return;
                }
            }
            Sfx(clickSound);
            ShowDetail(v);
        }

        internal bool CanDragCard(CardsScreenCardView v)
        {
            return isOpen && !popupBusy && detailRoot == null && string.IsNullOrEmpty(placeCardId)
                   && currentTab == Tab.Decks && v != null && !v.IsEmpty && v.context == CardViewContext.DeckSlot;
        }

        internal void PlayLiftSound() => Sfx(clickSound);

        // ───────────────────────────── Drag & drop ─────────────────────────────

        internal void BeginCardDrag(CardsScreenCardView v, PointerEventData e)
        {
            dragView = v;
            dragHover = null;
            var vis = v.visual;
            vis.DOKill();
            vis.SetParent(dragLayer, true);
            v.visualGroup.blocksRaycasts = false;
            if (v.glow != null)
            {
                v.glow.gameObject.SetActive(true);
                v.glow.color = new Color(1f, 0.85f, 0.35f, 0f);
                v.glow.DOFade(0.85f, 0.2f).SetUpdate(true);
            }
            vis.DOScale(1.14f, 0.18f).SetEase(Ease.OutBack).SetUpdate(true);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, e.position, e.pressEventCamera, out var lp);
            dragGrabOffset = (Vector2)vis.localPosition - lp;

            // Dim slots that can't take this card
            foreach (var other in deckViews)
            {
                if (other == null || other == v) continue;
                bool valid = other.SlotType == v.SlotType && !other.IsEmpty;
                other.visualGroup.DOKill();
                other.visualGroup.DOFade(valid ? 1f : 0.4f, 0.15f).SetUpdate(true);
            }
            Sfx(swapSound != null ? swapSound : clickSound);
        }

        internal void DragCard(CardsScreenCardView v, PointerEventData e)
        {
            if (dragView != v) return;
            var vis = v.visual;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, e.position, e.pressEventCamera, out var lp))
                vis.localPosition = lp + dragGrabOffset;
            float tilt = Mathf.Clamp(-e.delta.x * 0.6f, -14f, 14f);
            vis.localRotation = Quaternion.Lerp(vis.localRotation, Quaternion.Euler(0f, 0f, tilt), 0.25f);

            var hover = FindDeckSlotAt(e.position, e.pressEventCamera, v);
            if (hover != dragHover)
            {
                if (dragHover != null)
                {
                    dragHover.visual.DOKill();
                    dragHover.visual.DOScale(1f, 0.15f).SetUpdate(true);
                    dragHover.SetSwapTarget(false);
                }
                dragHover = hover;
                if (dragHover != null)
                {
                    dragHover.visual.DOKill();
                    dragHover.visual.DOScale(1.07f, 0.18f).SetEase(Ease.OutBack).SetUpdate(true);
                    dragHover.SetSwapTarget(true);
                }
            }

            // Auto-scroll the deck when dragging near the edges
            var vpRT = deckScroll.viewport;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(vpRT, e.position, e.pressEventCamera, out var vpPoint))
            {
                float edge = vpRT.rect.height * 0.12f;
                float dir = vpPoint.y > vpRT.rect.yMax - edge ? 1f : vpPoint.y < vpRT.rect.yMin + edge ? -1f : 0f;
                if (dir != 0f)
                {
                    float contentH = deckScroll.content.rect.height - vpRT.rect.height;
                    if (contentH > 1f)
                        deckScroll.verticalNormalizedPosition = Mathf.Clamp01(deckScroll.verticalNormalizedPosition + dir * Time.unscaledDeltaTime * 900f * u / contentH);
                }
            }
        }

        private CardsScreenCardView FindDeckSlotAt(Vector2 screen, Camera cam, CardsScreenCardView exclude)
        {
            foreach (var other in deckViews)
            {
                if (other == null || other == exclude || other.IsEmpty) continue;
                if (other.SlotType != exclude.SlotType) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(other.root, screen, cam)) return other;
            }
            return null;
        }

        internal void EndCardDrag(CardsScreenCardView v, PointerEventData e)
        {
            if (dragView != v) return;
            var target = FindDeckSlotAt(e.position, e.pressEventCamera, v);
            Vector3 dropWorld = v.visual.position;
            FinishDrag(v);

            if (target != null && inventory != null && v.Def != null)
            {
                string draggedId = v.Def.cardId;
                int targetLocal = target.slotIndex < CardInventoryManager.MAX_UNIT_SLOTS
                    ? target.slotIndex
                    : target.slotIndex - CardInventoryManager.MAX_UNIT_SLOTS;
                Vector3 sourceWorld = v.root.position;
                Vector3 targetWorld = target.root.position;

                suppressDeckEvent = true;
                bool ok = v.SlotType == CardType.Army
                    ? inventory.SwapUnitSlot(targetLocal, draggedId)
                    : inventory.SwapPowerupSlot(targetLocal, draggedId);
                suppressDeckEvent = false;

                if (ok)
                {
                    RefreshDeck(false);
                    RefreshCollectionStates();
                    // Target slot now shows the dragged card: fly it in from where it was dropped.
                    target.visual.position = dropWorld;
                    target.visual.localScale = Vector3.one * 1.14f;
                    target.visual.DOLocalMove(Vector3.zero, 0.22f).SetEase(Ease.OutCubic).SetUpdate(true);
                    target.visual.DOScale(1f, 0.32f).SetEase(Ease.OutBack, 2.5f).SetUpdate(true);
                    // Source slot now shows the displaced card: slide it over from the target slot.
                    v.visual.position = targetWorld;
                    v.visual.DOLocalMove(Vector3.zero, 0.3f).SetEase(Ease.OutBack, 1.2f).SetUpdate(true);
                    SpawnSparkles(target.root, 8, GoldRim);
                    Sfx(swapSound != null ? swapSound : clickSound);
                    return;
                }
            }

            // Invalid drop: fly home with a little shake
            v.visual.position = dropWorld;
            v.visual.DOLocalMove(Vector3.zero, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);
            v.visual.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        private void FinishDrag(CardsScreenCardView v)
        {
            var vis = v.visual;
            vis.DOKill();
            vis.SetParent(v.root, true);
            vis.SetAsFirstSibling();
            vis.localRotation = Quaternion.identity;
            vis.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetUpdate(true);
            v.visualGroup.blocksRaycasts = true;
            if (v.glow != null) { v.glow.DOKill(); v.glow.gameObject.SetActive(false); }

            if (dragHover != null)
            {
                dragHover.SetSwapTarget(false);
                dragHover.visual.DOKill();
                dragHover.visual.DOScale(1f, 0.15f).SetUpdate(true);
            }
            foreach (var other in deckViews)
            {
                if (other == null) continue;
                other.visualGroup.DOKill();
                other.visualGroup.DOFade(1f, 0.15f).SetUpdate(true);
            }
            dragView = null;
            dragHover = null;
        }

        private void CancelDrag()
        {
            if (dragView == null) return;
            var v = dragView;
            FinishDrag(v);
            v.visual.localPosition = Vector3.zero;
        }

        // ───────────────────────────── Use / place flow ─────────────────────────────

        private void StartPlaceMode(CardDefinition def)
        {
            placeCardId = def.cardId;
            SwitchTab(Tab.Decks, true);
            string type = def.cardType == CardType.Army ? "Army" : "Power";
            ShowSwapBanner("Tap a highlighted " + type + " card to swap it with <color=#FFD54A>" + def.displayName + "</color>.");
            foreach (var v in deckViews)
            {
                if (v == null) continue;
                bool valid = v.SlotType == def.cardType;
                v.visualGroup.DOKill();
                v.visualGroup.DOFade(valid ? 1f : 0.35f, 0.2f).SetUpdate(true);
                v.SetSwapTarget(valid);
            }
            // Scroll so the matching row is visible
            deckScroll.DOKill();
            float target = def.cardType == CardType.Army ? 1f : 0.55f;
            DOTween.To(() => deckScroll.verticalNormalizedPosition, x => deckScroll.verticalNormalizedPosition = x, target, 0.35f)
                .SetEase(Ease.OutCubic).SetUpdate(true).SetTarget(deckScroll);
        }

        private void TryPlaceInto(CardsScreenCardView slot)
        {
            var def = library != null ? library.GetCard(placeCardId) : null;
            if (def == null || slot.SlotType != def.cardType)
            {
                Sfx(errorSound);
                slot.visual.DOKill(true);
                slot.visual.DOPunchRotation(new Vector3(0f, 0f, 10f), 0.4f, 12, 0.5f).SetUpdate(true);
                return;
            }
            PlaceCard(def, slot.slotIndex);
        }

        private void PlaceCard(CardDefinition def, int slotIndex)
        {
            if (inventory == null || def == null) return;
            int local = slotIndex < CardInventoryManager.MAX_UNIT_SLOTS ? slotIndex : slotIndex - CardInventoryManager.MAX_UNIT_SLOTS;
            suppressDeckEvent = true;
            bool ok = def.cardType == CardType.Army ? inventory.SwapUnitSlot(local, def.cardId) : inventory.SwapPowerupSlot(local, def.cardId);
            suppressDeckEvent = false;
            CancelPlaceMode(false);
            if (currentTab != Tab.Decks) SwitchTab(Tab.Decks, true);

            RefreshDeck(true);
            RefreshCollectionStates();
            if (!ok) { Sfx(errorSound); return; }

            // Find where the card landed (an empty slot appends at the end of its row)
            CardsScreenCardView landed = null;
            foreach (var v in deckViews) if (v != null && v.Def == def) { landed = v; break; }
            if (landed != null)
            {
                var vis = landed.visual;
                vis.DOKill();
                vis.localScale = Vector3.one * 1.5f;
                vis.localRotation = Quaternion.Euler(0f, 0f, -8f);
                landed.visualGroup.alpha = 0f;
                vis.DOScale(1f, 0.45f).SetEase(Ease.OutBounce).SetUpdate(true);
                vis.DOLocalRotate(Vector3.zero, 0.35f).SetEase(Ease.OutBack).SetUpdate(true);
                landed.visualGroup.DOFade(1f, 0.15f).SetUpdate(true);
                SpawnSparkles(landed.root, 12, GoldRim);
            }
            Sfx(swapSound != null ? swapSound : clickSound);
        }

        private void CancelPlaceMode(bool animate)
        {
            bool had = !string.IsNullOrEmpty(placeCardId) || pendingEmptySlot >= 0;
            placeCardId = null;
            pendingEmptySlot = -1;
            if (!had) return;
            foreach (var v in deckViews)
            {
                if (v == null) continue;
                v.SetSwapTarget(false);
                v.visualGroup.DOKill();
                if (animate) v.visualGroup.DOFade(1f, 0.15f).SetUpdate(true);
                else v.visualGroup.alpha = 1f;
            }
            HideSwapBanner();
        }

        // ═════════════════════════════ Detail popup ═════════════════════════════

        private void ShowDetail(CardsScreenCardView source)
        {
            var def = source.Def;
            if (def == null) return;
            CloseDetail(true);
            popupBusy = true;
            detailSource = source;

            var entry = EntryOf(def.cardId);
            bool found = IsFound(entry);
            if (found && entry.isNew && inventory != null) inventory.ClearNewFlag(def.cardId);
            Color rc = found ? RarityMain(def.rarity) : new Color(0.62f, 0.64f, 0.72f);

            popupLayer.SetAsLastSibling();
            detailRoot = NewRect("CardDetail", popupLayer);
            Stretch(detailRoot);
            detailDim = detailRoot.gameObject.AddComponent<Image>();
            detailDim.color = new Color(0.01f, 0.02f, 0.08f, 0f);
            detailDim.raycastTarget = true;
            detailDim.DOFade(0.86f, 0.25f).SetUpdate(true);
            var dimBtn = detailRoot.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(() => CloseDetail(false));

            // ── Geometry: hero card overhangs the top of the panel ──
            float panelW = Mathf.Min(contentW - 48f * u, 860f * u);
            float heroW = cardW * 1.5f;
            float heroH = CardHeightFor(heroW);
            float heroVisH = heroW * 1.521f;          // art + level band (the hero's own bar is hidden)
            float overhang = heroVisH * 0.45f;
            float contentTop = heroVisH - overhang + 22f * u;
            float panelH = contentTop + 610f * u;
            float totalH = panelH + overhang;
            float panelCenterY = 10f * u - totalH / 2f + panelH / 2f;
            float panelTopY = panelCenterY + panelH / 2f;
            Vector2 heroTarget = new Vector2(0f, panelTopY + overhang - heroH / 2f);
            Vector2 heroVisCenter = new Vector2(0f, panelTopY + overhang - heroVisH / 2f);

            // ── Light burst behind the hero card ──
            var fx = NewRect("HeroFX", detailRoot);
            fx.anchorMin = fx.anchorMax = new Vector2(0.5f, 0.5f);
            fx.anchoredPosition = heroVisCenter;
            fx.sizeDelta = new Vector2(heroW * 2f, heroW * 2f);
            var fxGroup = fx.gameObject.AddComponent<CanvasGroup>();
            fxGroup.blocksRaycasts = false;
            fxGroup.alpha = 0f;
            if (raysSprite != null)
            {
                var rays = NewImage("Rays", fx, raysSprite, new Color(rc.r, rc.g, rc.b, 0.5f), true);
                rays.rectTransform.sizeDelta = new Vector2(heroW * 3f, heroW * 3f);
                rays.rectTransform.DOLocalRotate(new Vector3(0f, 0f, -360f), 18f, RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear).SetLoops(-1).SetUpdate(true);
                var rays2 = NewImage("Rays2", fx, raysSprite, new Color(1f, 1f, 1f, 0.18f), true);
                rays2.rectTransform.sizeDelta = new Vector2(heroW * 2.3f, heroW * 2.3f);
                rays2.rectTransform.DOLocalRotate(new Vector3(0f, 0f, 360f), 26f, RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear).SetLoops(-1).SetUpdate(true);
            }
            if (glowSprite != null)
            {
                var g = NewImage("Glow", fx, glowSprite, new Color(rc.r, rc.g, rc.b, 0.8f), true);
                g.rectTransform.sizeDelta = new Vector2(heroW * 2.1f, heroW * 2.1f);
                g.rectTransform.DOScale(1.1f, 1.2f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
            }
            fxGroup.DOFade(1f, 0.4f).SetDelay(0.15f).SetUpdate(true);

            // ── Panel: rarity rim + glow, dark gradient body ──
            detailPanel = NewRect("Panel", detailRoot);
            detailPanel.anchorMin = detailPanel.anchorMax = new Vector2(0.5f, 0.5f);
            detailPanel.sizeDelta = new Vector2(panelW, panelH);
            detailPanel.anchoredPosition = new Vector2(0f, panelCenterY);
            BuildPremiumPanel(detailPanel, rc);
            var panelGroup = detailPanel.gameObject.AddComponent<CanvasGroup>();
            panelGroup.alpha = 0f;
            detailPanel.localScale = new Vector3(0.9f, 0.6f, 1f);
            panelGroup.DOFade(1f, 0.22f).SetDelay(0.12f).SetUpdate(true);
            detailPanel.DOScale(1f, 0.45f).SetDelay(0.12f).SetEase(Ease.OutBack, 1.4f).SetUpdate(true);

            BuildDetailContents(def, entry, found, rc, contentTop);

            // Close button on the panel corner
            var close = NewRect("Close", detailPanel);
            close.anchorMin = close.anchorMax = new Vector2(1f, 1f);
            close.sizeDelta = new Vector2(84f, 84f) * u;
            close.anchoredPosition = new Vector2(-14f * u, -14f * u);
            var cImg = closeSprite != null
                ? NewImage("Icon", close, closeSprite, Color.white, true)
                : NewRounded("Icon", close, BadgeRed, 20f);
            Stretch(cImg.rectTransform);
            cImg.raycastTarget = true;
            if (closeSprite == null)
            {
                var xText = NewText("X", close, "X", 46f * u, Color.white, TextAlignmentOptions.Center);
                Stretch(xText.rectTransform);
            }
            var cBtn = close.gameObject.AddComponent<Button>();
            cBtn.transition = Selectable.Transition.None;
            close.gameObject.AddComponent<ShopPressFeedback>();
            cBtn.onClick.AddListener(() => CloseDetail(false));

            // ── Hero card: flies out of the tapped tile with a flip ──
            var heroHolder = NewRect("Hero", detailRoot);
            heroHolder.anchorMin = heroHolder.anchorMax = new Vector2(0.5f, 0.5f);
            heroHolder.sizeDelta = new Vector2(heroW, heroH);
            var hero = BuildHeroCard(heroHolder, def, heroW, rc);
            detailHero = heroHolder;

            Vector2 startLocal = detailRoot.InverseTransformPoint(source.visual.position);
            heroHolder.anchoredPosition = startLocal - (Vector2)detailRoot.rect.center;
            heroHolder.localScale = Vector3.one * (cardW / heroW);
            source.visualGroup.alpha = 0f;

            var heroSeq = DOTween.Sequence().SetUpdate(true).SetTarget(heroHolder);
            heroSeq.Append(heroHolder.DOAnchorPos(heroTarget, 0.42f).SetEase(Ease.OutCubic));
            heroSeq.Join(heroHolder.DOScale(1.08f, 0.36f).SetEase(Ease.OutQuad));
            heroSeq.Join(heroHolder.DOLocalRotate(new Vector3(0f, 360f, 0f), 0.5f, RotateMode.FastBeyond360).SetEase(Ease.OutCubic));
            heroSeq.Append(heroHolder.DOScale(1f, 0.22f).SetEase(Ease.OutBack, 3f));
            heroSeq.OnComplete(() =>
            {
                if (heroHolder == null) return;
                heroHolder.localRotation = Quaternion.identity;
                heroHolder.DOAnchorPosY(heroTarget.y + 10f * u, 1.6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
            });

            DOVirtual.DelayedCall(0.45f, () => popupBusy = false).SetUpdate(true);
        }

        /// <summary>Large card used by the popup and the level-up screen: glow, glossy rarity frame, light sweep.</summary>
        private CardsScreenCardView BuildHeroCard(RectTransform holder, CardDefinition def, float heroW, Color rc)
        {
            var hero = BuildCardView(holder, CardViewContext.Collection, -1, null, heroW);
            hero.enabled = false;
            hero.GetComponent<Image>().raycastTarget = false;
            ApplyCard(hero, def);
            Stretch(hero.root);
            hero.newBadge.gameObject.SetActive(false);
            hero.inDeckTag.gameObject.SetActive(false);
            hero.barRoot.gameObject.SetActive(false);

            if (hero.glow != null)
            {
                hero.glow.gameObject.SetActive(true);
                hero.glow.color = new Color(rc.r, rc.g, rc.b, 0.55f);
                hero.glow.DOFade(0.95f, 1.1f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
            }
            // Thin bright inner rim over the plate edge for a polished metal frame look
            var rim = NewRounded("RimLight", hero.visual, new Color(1f, 1f, 1f, 0.55f), 16f * heroW / cardW);
            rim.fillCenter = false;
            rim.rectTransform.anchorMin = rim.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            rim.rectTransform.pivot = new Vector2(0.5f, 1f);
            rim.rectTransform.sizeDelta = hero.plate.rectTransform.sizeDelta;
            rim.rectTransform.anchoredPosition = hero.plate.rectTransform.anchoredPosition;
            rim.rectTransform.SetSiblingIndex(hero.art.rectTransform.GetSiblingIndex());

            AddCardShine(hero.art.rectTransform);
            return hero;
        }

        /// <summary>Periodic diagonal light sweep across a card's art.</summary>
        private void AddCardShine(RectTransform art)
        {
            if (shineSprite == null || art == null) return;
            var mask = NewRect("ShineMask", art);
            Stretch(mask);
            mask.gameObject.AddComponent<RectMask2D>();
            var shine = NewImage("Shine", mask, shineSprite, new Color(1f, 1f, 1f, 0.55f), false);
            var srt = shine.rectTransform;
            srt.anchorMin = srt.anchorMax = new Vector2(0f, 0.5f);
            float w = art.rect.width > 1f ? art.rect.width : art.sizeDelta.x;
            float h = art.rect.height > 1f ? art.rect.height : art.sizeDelta.y;
            srt.sizeDelta = new Vector2(w * 0.35f, h * 1.6f);
            srt.localRotation = Quaternion.Euler(0f, 0f, -24f);
            srt.anchoredPosition = new Vector2(-w * 0.4f, 0f);
            DOTween.Sequence().SetUpdate(true).SetTarget(srt).SetLoops(-1)
                .AppendInterval(0.9f)
                .Append(srt.DOAnchorPosX(w * 1.4f, 0.8f).SetEase(Ease.InOutSine))
                .AppendInterval(1.6f)
                .AppendCallback(() => srt.anchoredPosition = new Vector2(-w * 0.4f, 0f));
        }

        /// <summary>Rounded dark-gradient panel with a rarity-coloured rim and soft outer glow.</summary>
        private Image BuildPremiumPanel(RectTransform panel, Color rc)
        {
            if (glowSprite != null)
            {
                var outer = NewImage("OuterGlow", panel, glowSprite, new Color(rc.r, rc.g, rc.b, 0.35f), false);
                Stretch(outer.rectTransform, -90f * u, -90f * u, -90f * u, -90f * u);
                outer.DOFade(0.6f, 1.3f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
            }
            var shadow = NewRounded("Shadow", panel, new Color(0f, 0f, 0.04f, 0.6f), 44f);
            Stretch(shadow.rectTransform, 0f, -14f * u, 0f, 14f * u);
            var rim = NewRounded("Rim", panel, rc, 44f);
            Stretch(rim.rectTransform);
            var rimGloss = NewRounded("RimGloss", panel, new Color(1f, 1f, 1f, 0.35f), 44f);
            rimGloss.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            rimGloss.rectTransform.anchorMax = Vector2.one;
            rimGloss.rectTransform.offsetMin = Vector2.zero;
            rimGloss.rectTransform.offsetMax = Vector2.zero;
            var body = NewRounded("Body", panel, new Color(0.04f, 0.09f, 0.26f, 1f), 38f);
            Stretch(body.rectTransform, 8f * u, 8f * u, 8f * u, 8f * u);
            body.raycastTarget = true;
            var swallow = body.gameObject.AddComponent<Button>(); // taps on the panel must not reach the dim's close button
            swallow.transition = Selectable.Transition.None;
            var top = NewRounded("TopLight", panel, new Color(0.22f, 0.42f, 0.95f, 0.45f), 38f);
            top.rectTransform.anchorMin = new Vector2(0f, 0.45f);
            top.rectTransform.anchorMax = Vector2.one;
            top.rectTransform.offsetMin = new Vector2(8f * u, 0f);
            top.rectTransform.offsetMax = new Vector2(-8f * u, -8f * u);
            var tint = NewRounded("RarityTint", panel, new Color(rc.r, rc.g, rc.b, 0.12f), 38f);
            tint.rectTransform.anchorMin = new Vector2(0f, 0.7f);
            tint.rectTransform.anchorMax = Vector2.one;
            tint.rectTransform.offsetMin = new Vector2(8f * u, 0f);
            tint.rectTransform.offsetMax = new Vector2(-8f * u, -8f * u);
            var bottom = NewRounded("BottomShade", panel, new Color(0f, 0f, 0.03f, 0.35f), 38f);
            bottom.rectTransform.anchorMin = Vector2.zero;
            bottom.rectTransform.anchorMax = new Vector2(1f, 0.3f);
            bottom.rectTransform.offsetMin = new Vector2(8f * u, 8f * u);
            bottom.rectTransform.offsetMax = new Vector2(-8f * u, 0f);
            return body;
        }

        private void BuildDetailContents(CardDefinition def, PlayerCardEntry entry, bool found, Color rc, float contentTop)
        {
            var p = detailPanel;
            float pw = p.sizeDelta.x;

            var nameText = NewText("Name", p, def.displayName, 64f * u, TextWhite, TextAlignmentOptions.Top);
            PlaceTop(nameText.rectTransform, 0f, contentTop, pw - 120f * u, 76f * u);
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 34f * u;
            nameText.fontSizeMax = 64f * u;

            // Rarity pill + type
            var subRow = NewRect("Sub", p);
            PlaceTop(subRow, 0f, contentTop + 80f * u, pw, 50f * u);
            var rarityPill = NewRect("Rarity", subRow);
            rarityPill.anchorMin = rarityPill.anchorMax = new Vector2(0.5f, 0.5f);
            rarityPill.sizeDelta = new Vector2(220f, 46f) * u;
            rarityPill.anchoredPosition = new Vector2(-80f * u, 0f);
            var rpBg = NewRounded("Bg", rarityPill, RarityBand(def.rarity), 22f);
            Stretch(rpBg.rectTransform);
            var rpGloss = NewRounded("Gloss", rarityPill, new Color(1f, 1f, 1f, 0.2f), 20f);
            rpGloss.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            rpGloss.rectTransform.anchorMax = Vector2.one;
            rpGloss.rectTransform.offsetMin = rpGloss.rectTransform.offsetMax = Vector2.zero;
            var rpText = NewText("Text", rarityPill, def.rarity.ToString(), 30f * u, Color.white, TextAlignmentOptions.Center);
            Stretch(rpText.rectTransform);
            var typeText = NewText("Type", subRow, def.cardType == CardType.Army ? "Army" : "Power", 32f * u, TextDim, TextAlignmentOptions.MidlineLeft);
            typeText.rectTransform.anchorMin = typeText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            typeText.rectTransform.pivot = new Vector2(0f, 0.5f);
            typeText.rectTransform.sizeDelta = new Vector2(220f * u, 46f * u);
            typeText.rectTransform.anchoredPosition = new Vector2(48f * u, 0f);

            // ── Stat tiles ──
            int level = found ? entry.level : 1;
            int bonus = Mathf.RoundToInt((def.GetStatMultiplier(level) - 1f) * 100f);
            Sprite[] icons = { elixirSprite, arrowUpSprite, armyIconSprite, towerSprite };
            string[] labels = { "Cost", "Level", "Bonus", "Arena" };
            string[] values =
            {
                def.elixirCost.ToString(),
                found ? (def.IsMaxLevel(level) ? "MAX" : level.ToString()) : "-",
                "+" + bonus + "%",
                Mathf.Max(1, def.arenaUnlockLevel).ToString()
            };
            Color[] valueColors = { TextWhite, TextWhite, bonus > 0 ? new Color(0.55f, 1f, 0.55f) : TextWhite, TextWhite };
            float gap = 14f * u;
            float tileW = (pw - 70f * u - 3f * gap) / 4f;
            float tileH = 140f * u;
            for (int i = 0; i < 4; i++)
            {
                var tile = NewRect("Stat" + i, p);
                tile.anchorMin = tile.anchorMax = new Vector2(0f, 1f);
                tile.pivot = new Vector2(0.5f, 1f);
                tile.sizeDelta = new Vector2(tileW, tileH);
                tile.anchoredPosition = new Vector2(35f * u + tileW / 2f + i * (tileW + gap), -(contentTop + 150f * u));

                var tBg = NewRounded("Bg", tile, new Color(0.01f, 0.04f, 0.15f, 0.85f), 22f);
                Stretch(tBg.rectTransform);
                var tTop = NewRounded("Accent", tile, new Color(rc.r, rc.g, rc.b, 0.9f), 8f);
                tTop.rectTransform.anchorMin = new Vector2(0f, 1f);
                tTop.rectTransform.anchorMax = new Vector2(1f, 1f);
                tTop.rectTransform.pivot = new Vector2(0.5f, 1f);
                tTop.rectTransform.sizeDelta = new Vector2(-28f * u, 8f * u);
                tTop.rectTransform.anchoredPosition = new Vector2(0f, -6f * u);
                var tGloss = NewRounded("Gloss", tile, new Color(1f, 1f, 1f, 0.06f), 20f);
                tGloss.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                tGloss.rectTransform.anchorMax = Vector2.one;
                tGloss.rectTransform.offsetMin = new Vector2(4f * u, 0f);
                tGloss.rectTransform.offsetMax = new Vector2(-4f * u, -4f * u);

                var label = NewText("Label", tile, labels[i], 25f * u, TextDim, TextAlignmentOptions.Top);
                Stretch(label.rectTransform, 0f, 0f, 0f, 22f * u);
                label.fontStyle = FontStyles.Normal;

                // icon + value centred together
                var value = NewText("Value", tile, values[i], 48f * u, valueColors[i], TextAlignmentOptions.MidlineLeft);
                float vw = value.GetPreferredValues(values[i]).x;
                float iconS = 44f * u;
                float groupW = iconS + 8f * u + vw;
                var icon = NewIcon("Icon", tile, icons[i], new Vector2(iconS, iconS));
                icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0f);
                icon.anchoredPosition = new Vector2(-groupW / 2f + iconS / 2f, 44f * u);
                if (i == 1)
                {
                    var ii = icon.GetComponentInChildren<Image>();
                    if (ii != null) ii.color = BarGreen;
                }
                value.rectTransform.anchorMin = value.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                value.rectTransform.pivot = new Vector2(0f, 0.5f);
                value.rectTransform.sizeDelta = new Vector2(vw + 10f * u, 60f * u);
                value.rectTransform.anchoredPosition = new Vector2(-groupW / 2f + iconS + 8f * u, 44f * u);

                tile.localScale = Vector3.zero;
                tile.DOScale(1f, 0.35f).SetDelay(0.22f + i * 0.06f).SetEase(Ease.OutBack, 2f).SetUpdate(true);
            }

            float barTop = contentTop + 318f * u;
            if (!found)
            {
                var lockRow = NewRect("Locked", p);
                PlaceTop(lockRow, 0f, barTop, pw - 80f * u, 200f * u);
                var lbg = NewRounded("Bg", lockRow, new Color(0f, 0.02f, 0.1f, 0.6f), 26f);
                Stretch(lbg.rectTransform);
                if (lockSprite != null)
                {
                    var lk = NewImage("Lock", lockRow, lockSprite, Color.white, true);
                    lk.rectTransform.anchorMin = lk.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                    lk.rectTransform.sizeDelta = new Vector2(80f, 80f) * u;
                    lk.rectTransform.anchoredPosition = new Vector2(80f * u, 0f);
                    lk.rectTransform.DOPunchRotation(new Vector3(0f, 0f, 12f), 0.6f, 8, 0.5f).SetDelay(0.5f).SetUpdate(true);
                }
                var locked = NewText("Text", lockRow, "Not found yet\n<size=125%><color=#E58BFF>Unlocks at Arena " + Mathf.Max(1, def.arenaUnlockLevel) + "</color></size>",
                    36f * u, TextWhite, TextAlignmentOptions.Center);
                locked.textWrappingMode = TextWrappingModes.Normal;
                Stretch(locked.rectTransform, 140f * u, 0f, 30f * u, 0f);
                return;
            }

            // ── Copies progress: capsule bar with gradient fill and a glowing leading edge ──
            bool max = def.IsMaxLevel(entry.level);
            int need = max ? 0 : def.CopiesToUpgrade(entry.level);
            bool copiesOk = !max && entry.copiesOwned >= need;
            Color fillA, fillB, edgeCol;
            if (max)
            {
                fillA = new Color(1f, 0.92f, 0.45f); fillB = new Color(0.98f, 0.62f, 0.10f); edgeCol = new Color(1f, 0.95f, 0.65f);
            }
            else if (copiesOk)
            {
                fillA = new Color(0.62f, 1f, 0.42f); fillB = new Color(0.16f, 0.80f, 0.26f); edgeCol = new Color(0.80f, 1f, 0.62f);
            }
            else
            {
                fillA = new Color(0.20f, 0.92f, 0.95f); fillB = new Color(0.22f, 0.46f, 1f); edgeCol = new Color(0.62f, 0.96f, 1f);
            }

            float barW = pw - 110f * u;
            float barH = 60f * u;
            float barX = 10f * u;   // nudged right so the arrow badge can overhang the left end

            var caption = NewText("Caption", p, "Cards", 30f * u, TextDim, TextAlignmentOptions.BottomLeft);
            PlaceTop(caption.rectTransform, barX, barTop, barW - 30f * u, 40f * u);
            caption.rectTransform.anchoredPosition += new Vector2(30f * u, 0f);
            string statusStr = max ? "Max level"
                : copiesOk ? "Ready to upgrade!"
                : "Collect <color=#FFFFFF>" + (need - entry.copiesOwned) + "</color> more";
            var status = NewText("Status", p, statusStr, 27f * u,
                max ? BarGold : copiesOk ? new Color(0.62f, 1f, 0.5f) : new Color(0.66f, 0.76f, 0.98f, 0.9f), TextAlignmentOptions.BottomRight);
            PlaceTop(status.rectTransform, barX, barTop, barW, 40f * u);
            if (copiesOk) status.rectTransform.DOScale(1.06f, 0.6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);

            var barRow = NewRect("Copies", p);
            PlaceTop(barRow, barX, barTop + 50f * u, barW, barH);

            // Pulsing glow behind the bar when it's full
            if ((copiesOk || max) && glowSprite != null)
            {
                var halo = NewImage("Halo", barRow, glowSprite, new Color(fillB.r, fillB.g, fillB.b, 0.35f), false);
                Stretch(halo.rectTransform, -60f * u, -45f * u, -60f * u, -45f * u);
                halo.DOFade(0.85f, 0.75f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
            }

            // Dark frame + recessed track (darker at the top reads as an inner shadow)
            var frame = NewCapsule("Frame", barRow, new Color(0.01f, 0.02f, 0.09f, 1f), barH + 10f * u);
            Stretch(frame.rectTransform, -5f * u, -5f * u, -5f * u, -5f * u);
            var frameHi = NewCapsule("FrameHighlight", barRow, new Color(1f, 1f, 1f, 0.14f), barH + 10f * u);
            Stretch(frameHi.rectTransform, -5f * u, -7f * u, -5f * u, -3f * u);
            frameHi.transform.SetSiblingIndex(frame.transform.GetSiblingIndex());
            var track = NewCapsule("Track", barRow, Color.white, barH);
            Stretch(track.rectTransform);
            AddGradient(track, new Color(0.02f, 0.05f, 0.16f), new Color(0.07f, 0.15f, 0.36f));

            // Fill: width-animated capsule so the leading edge stays round
            var fillArea = NewRect("FillArea", barRow);
            Stretch(fillArea, 5f * u, 5f * u, 5f * u, 5f * u);
            float innerW = barW - 10f * u;
            float innerH = barH - 10f * u;
            var fill = NewCapsule("Fill", fillArea, Color.white, innerH);
            AddGradient(fill, fillA, fillB, true);
            var frt = fill.rectTransform;
            frt.anchorMin = new Vector2(0f, 0f);
            frt.anchorMax = new Vector2(0f, 1f);
            frt.pivot = new Vector2(0f, 0.5f);
            frt.anchoredPosition = Vector2.zero;

            var gloss = NewCapsule("Gloss", frt, Color.white, innerH * 0.45f);
            gloss.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            gloss.rectTransform.anchorMax = Vector2.one;
            gloss.rectTransform.offsetMin = new Vector2(innerH * 0.3f, 0f);
            gloss.rectTransform.offsetMax = new Vector2(-innerH * 0.3f, -4f * u);
            AddGradient(gloss, new Color(1f, 1f, 1f, 0.6f), new Color(1f, 1f, 1f, 0.06f));
            var under = NewCapsule("Shade", frt, Color.white, innerH * 0.4f);
            under.rectTransform.anchorMin = Vector2.zero;
            under.rectTransform.anchorMax = new Vector2(1f, 0.4f);
            under.rectTransform.offsetMin = new Vector2(innerH * 0.25f, 3f * u);
            under.rectTransform.offsetMax = new Vector2(-innerH * 0.25f, 0f);
            AddGradient(under, new Color(0f, 0f, 0.1f, 0f), new Color(0f, 0f, 0.1f, 0.25f));

            if (glowSprite != null)
            {
                var edge = NewImage("EdgeGlow", frt, glowSprite, new Color(edgeCol.r, edgeCol.g, edgeCol.b, 0.9f), false);
                edge.rectTransform.anchorMin = edge.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                edge.rectTransform.sizeDelta = new Vector2(innerH * 2.2f, innerH * 2.2f);
                edge.rectTransform.anchoredPosition = new Vector2(-innerH * 0.35f, 0f);
                edge.DOFade(0.45f, 0.55f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
            }
            var edgeCore = NewCapsule("EdgeCore", frt, new Color(1f, 1f, 1f, 0.9f), 9f * u);
            edgeCore.rectTransform.anchorMin = edgeCore.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            edgeCore.rectTransform.sizeDelta = new Vector2(9f * u, innerH * 0.62f);
            edgeCore.rectTransform.anchoredPosition = new Vector2(-innerH * 0.32f, 0f);

            float target = max ? 1f : need > 0 ? Mathf.Clamp01(entry.copiesOwned / (float)need) : 1f;
            float targetW = target <= 0f ? 0f : Mathf.Max(innerH, innerW * target);
            if (targetW <= 0f) frt.gameObject.SetActive(false);
            else
            {
                frt.sizeDelta = new Vector2(innerH, 0f);
                DOTween.To(() => frt.sizeDelta.x, x => frt.sizeDelta = new Vector2(x, 0f), targetW, 0.8f)
                    .SetDelay(0.35f).SetEase(Ease.OutCubic).SetTarget(frt).SetUpdate(true);
                AddBarSweep(frt);
            }

            var ct = NewText("Count", barRow, max ? "MAX" : entry.copiesOwned + " / " + need, 38f * u, Color.white, TextAlignmentOptions.Center);
            Stretch(ct.rectTransform, 0f, 2f * u, 0f, 0f);
            if (copiesOk)
                ct.rectTransform.DOPunchScale(Vector3.one * 0.25f, 0.45f, 6, 0.6f).SetDelay(1.1f).SetUpdate(true);

            // Arrow badge overhanging the left end
            var arrowBadge = NewRect("ArrowBadge", barRow);
            arrowBadge.anchorMin = arrowBadge.anchorMax = new Vector2(0f, 0.5f);
            arrowBadge.sizeDelta = new Vector2(barH + 26f * u, barH + 26f * u);
            arrowBadge.anchoredPosition = new Vector2(-6f * u, 0f);
            var ab = NewImage("Bg", arrowBadge, circleSprite, new Color(0.01f, 0.03f, 0.12f, 1f), true);
            Stretch(ab.rectTransform);
            var abRim = NewImage("Rim", arrowBadge, circleSprite, Color.white, true);
            Stretch(abRim.rectTransform, 5f * u, 5f * u, 5f * u, 5f * u);
            AddGradient(abRim, fillA, fillB);
            var abFace = NewImage("Face", arrowBadge, circleSprite, Color.white, true);
            Stretch(abFace.rectTransform, 11f * u, 11f * u, 11f * u, 11f * u);
            AddGradient(abFace, new Color(0.10f, 0.24f, 0.55f), new Color(0.03f, 0.09f, 0.28f));
            var abIcon = NewIcon("Arrow", arrowBadge, arrowUpSprite, new Vector2(40f, 40f) * u);
            abIcon.anchorMin = abIcon.anchorMax = new Vector2(0.5f, 0.5f);
            var abImg = abIcon.GetComponentInChildren<Image>();
            if (abImg != null) abImg.color = edgeCol;
            if (copiesOk) abIcon.DOAnchorPosY(5f * u, 0.45f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);

            // ── Buttons ──
            bool inDeck = InDeck(def.cardId);
            float btnW = 340f * u, btnH = 132f * u;
            var useBtn = inDeck ? null : BuildButton3D(p, "UseButton", "Use", GreenBtn, new Vector2(btnW, btnH), 54f * u, out _);
            RectTransform upBtn = null;
            RectTransform inDeckBadge = null;
            if (!max)
            {
                int coins = def.CoinsToUpgrade(entry.level);
                upBtn = BuildUpgradeButton(p, coins, new Vector2(btnW + 30f * u, btnH), copiesOk, coins <= PlayerWallet.Coins);
            }
            if (inDeck)
            {
                inDeckBadge = BuildButton3D(p, "InDeckButton", "", BlueBtn, new Vector2(btnW, btnH), 0f, out var idFace);
                var check = BuildCheckBadge(idFace, 70f * u);
                check.anchorMin = check.anchorMax = new Vector2(0f, 0.5f);
                check.anchoredPosition = new Vector2(58f * u, 0f);
                var idText = NewText("Text", idFace, "In Battle\nDeck", 34f * u, Color.white, TextAlignmentOptions.Center);
                idText.lineSpacing = -18f;
                Stretch(idText.rectTransform, 100f * u, 0f, 14f * u, 0f);
                var badgeRef = inDeckBadge;
                inDeckBadge.GetComponent<Button>().onClick.AddListener(() =>
                {
                    Sfx(clickSound);
                    var fb = badgeRef.GetComponent<ShopPressFeedback>();
                    if (fb != null) fb.Punch(0.14f);
                    check.DOKill(true);
                    check.DOPunchRotation(new Vector3(0f, 0f, 20f), 0.45f, 8, 0.6f).SetUpdate(true);
                    SpawnSparkles(check, 8, new Color(0.6f, 1f, 0.6f));
                });
            }

            var row = new List<RectTransform>();
            if (useBtn != null) row.Add(useBtn);
            if (inDeckBadge != null) row.Add(inDeckBadge);
            if (upBtn != null) row.Add(upBtn);
            if (max)
            {
                var maxLabel = NewRect("MaxLabel", p);
                maxLabel.sizeDelta = new Vector2(btnW, btnH);
                var ml = NewText("Text", maxLabel, "Max level\nreached!", 36f * u, BarGold, TextAlignmentOptions.Center);
                Stretch(ml.rectTransform);
                row.Add(maxLabel);
            }
            float rowGap = 30f * u;
            float totalW = 0f;
            foreach (var r in row) totalW += r.sizeDelta.x;
            totalW += (row.Count - 1) * rowGap;
            float bx = -totalW / 2f;
            for (int i = 0; i < row.Count; i++)
            {
                var r = row[i];
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
                r.pivot = new Vector2(0f, 0f);
                r.anchoredPosition = new Vector2(bx, 44f * u);
                bx += r.sizeDelta.x + rowGap;
                r.localScale = Vector3.zero;
                r.DOScale(1f, 0.4f).SetDelay(0.4f + i * 0.08f).SetEase(Ease.OutBack, 2f).SetUpdate(true);
            }

            if (useBtn != null)
            {
                useBtn.GetComponent<Button>().onClick.AddListener(() =>
                {
                    Sfx(clickSound);
                    var ufb = useBtn.GetComponent<ShopPressFeedback>();
                    if (ufb != null) ufb.Punch(0.12f);
                    int empty = pendingEmptySlot;
                    CloseDetail(false);
                    if (empty >= 0 && (empty < CardInventoryManager.MAX_UNIT_SLOTS ? CardType.Army : CardType.Power) == def.cardType)
                        PlaceCard(def, empty);
                    else
                    {
                        CancelPlaceMode(false);
                        StartPlaceMode(def);
                    }
                });
            }
            if (upBtn != null)
                upBtn.GetComponent<Button>().onClick.AddListener(() => OnUpgradePressed(def, upBtn));
        }

        /// <summary>Soft highlight that sweeps along a progress fill.</summary>
        private void AddBarSweep(RectTransform fill)
        {
            if (shineSprite == null) return;
            var mask = NewRect("SweepMask", fill);
            Stretch(mask);
            mask.gameObject.AddComponent<RectMask2D>();
            var sweep = NewImage("Sweep", mask, shineSprite, new Color(1f, 1f, 1f, 0.5f), false);
            var srt = sweep.rectTransform;
            srt.anchorMin = new Vector2(0f, 0f);
            srt.anchorMax = new Vector2(0f, 1f);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(120f * u, 0f);
            srt.anchoredPosition = new Vector2(-80f * u, 0f);
            float w = fill.parent is RectTransform pr ? pr.rect.width : 800f * u;
            DOTween.Sequence().SetUpdate(true).SetTarget(srt).SetLoops(-1)
                .AppendInterval(1f)
                .Append(srt.DOAnchorPosX(w + 80f * u, 1.1f).SetEase(Ease.InOutSine))
                .AppendCallback(() => srt.anchoredPosition = new Vector2(-80f * u, 0f));
        }

        // ═════════════════════════════ Level-up celebration ═════════════════════════════

        private void PlayLevelUp(CardDefinition def, int oldLevel, int newLevel, Action onDone)
        {
            popupLayer.SetAsLastSibling();
            Color rc = RarityMain(def.rarity);
            var root = NewRect("LevelUp", popupLayer);
            Stretch(root);
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0.01f, 0.03f, 0.10f, 0f);
            dim.raycastTarget = true;
            dim.DOFade(0.9f, 0.3f).SetUpdate(true);
            var rootBtn = root.gameObject.AddComponent<Button>();
            rootBtn.transition = Selectable.Transition.None;

            bool canContinue = false;
            bool finished = false;

            float cw = cardW * 1.7f;
            float ch = CardHeightFor(cw);
            float visH = cw * 1.521f;
            float cardVisCenterY = 180f * u;
            float cardCenterY = cardVisCenterY + visH / 2f - ch / 2f;

            // ── Light burst ──
            var burst = NewRect("Burst", root);
            burst.anchorMin = burst.anchorMax = new Vector2(0.5f, 0.5f);
            burst.anchoredPosition = new Vector2(0f, cardVisCenterY);
            burst.sizeDelta = new Vector2(1200f, 1200f) * u;
            burst.localScale = Vector3.zero;
            if (raysSprite != null)
            {
                var r1 = NewImage("Rays", burst, raysSprite, new Color(rc.r, rc.g, rc.b, 0.65f), true);
                r1.rectTransform.sizeDelta = new Vector2(1500f, 1500f) * u;
                r1.rectTransform.DOLocalRotate(new Vector3(0f, 0f, -360f), 16f, RotateMode.FastBeyond360).SetEase(Ease.Linear).SetLoops(-1).SetUpdate(true);
                var r2 = NewImage("Rays2", burst, raysSprite, new Color(1f, 0.97f, 0.85f, 0.3f), true);
                r2.rectTransform.sizeDelta = new Vector2(1100f, 1100f) * u;
                r2.rectTransform.DOLocalRotate(new Vector3(0f, 0f, 360f), 22f, RotateMode.FastBeyond360).SetEase(Ease.Linear).SetLoops(-1).SetUpdate(true);
            }
            if (glowSprite != null)
            {
                var g1 = NewImage("Glow", burst, glowSprite, new Color(rc.r, rc.g, rc.b, 0.85f), true);
                g1.rectTransform.sizeDelta = new Vector2(1000f, 1000f) * u;
                g1.rectTransform.DOScale(1.12f, 1.1f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
                var g2 = NewImage("Core", burst, glowSprite, new Color(1f, 1f, 0.9f, 0.6f), true);
                g2.rectTransform.sizeDelta = new Vector2(560f, 560f) * u;
            }
            burst.DOScale(1f, 0.7f).SetDelay(0.35f).SetEase(Ease.OutBack, 1.3f).SetUpdate(true);

            // ── Card (starts at the old level, flips in) ──
            var holder = NewRect("Card", root);
            holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0.5f);
            holder.sizeDelta = new Vector2(cw, ch);
            holder.anchoredPosition = new Vector2(0f, cardCenterY);
            var card = BuildHeroCard(holder, def, cw, rc);
            card.levelText.text = "Level " + oldLevel;
            holder.localScale = Vector3.zero;

            // ── Banner ──
            var banner = NewRect("Banner", root);
            banner.anchorMin = banner.anchorMax = new Vector2(0.5f, 0.5f);
            banner.sizeDelta = new Vector2(660f, 170f) * u;
            banner.anchoredPosition = new Vector2(0f, cardVisCenterY + visH / 2f + 100f * u);
            var bImg = levelUpRibbonSprite != null
                ? NewImage("Ribbon", banner, levelUpRibbonSprite, Color.white, false)
                : NewRounded("Ribbon", banner, BarGold, 30f);
            Stretch(bImg.rectTransform);
            var bText = NewText("Text", banner, "LEVEL UP!", 74f * u, Color.white, TextAlignmentOptions.Center);
            Stretch(bText.rectTransform, 0f, 26f * u, 0f, 0f);
            var bGroup = banner.gameObject.AddComponent<CanvasGroup>();
            bGroup.alpha = 0f;
            bGroup.blocksRaycasts = false;

            // ── Level transition row ──
            float rowY = cardVisCenterY - visH / 2f - 80f * u;
            var oldT = NewText("Old", root, "Level " + oldLevel, 50f * u, new Color(0.75f, 0.8f, 0.9f), TextAlignmentOptions.MidlineRight);
            oldT.rectTransform.anchorMin = oldT.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            oldT.rectTransform.pivot = new Vector2(1f, 0.5f);
            oldT.rectTransform.sizeDelta = new Vector2(300f, 80f) * u;
            oldT.rectTransform.anchoredPosition = new Vector2(-60f * u, rowY);
            var arrow = NewIcon("Arrow", root, arrowUpSprite, new Vector2(64f, 64f) * u);
            arrow.anchorMin = arrow.anchorMax = new Vector2(0.5f, 0.5f);
            arrow.anchoredPosition = new Vector2(0f, rowY);
            arrow.localRotation = Quaternion.Euler(0f, 0f, -90f);
            var arrowImg = arrow.GetComponentInChildren<Image>();
            if (arrowImg != null) arrowImg.color = BarGreen;
            var newT = NewText("New", root, "Level " + newLevel, 66f * u, BarGreen, TextAlignmentOptions.MidlineLeft);
            newT.rectTransform.anchorMin = newT.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            newT.rectTransform.pivot = new Vector2(0f, 0.5f);
            newT.rectTransform.sizeDelta = new Vector2(320f, 90f) * u;
            newT.rectTransform.anchoredPosition = new Vector2(60f * u, rowY);
            oldT.rectTransform.localScale = Vector3.zero;
            arrow.localScale = Vector3.zero;
            newT.rectTransform.localScale = Vector3.zero;

            // ── Stat gain rows ──
            int gain = Mathf.RoundToInt((def.GetStatMultiplier(newLevel) - def.GetStatMultiplier(oldLevel)) * 100f);
            int total = Mathf.RoundToInt((def.GetStatMultiplier(newLevel) - 1f) * 100f);
            string[] statNames = def.cardType == CardType.Army
                ? new[] { "Hitpoints", "Damage" }
                : new[] { "Effect Power", "Damage" };
            var statRows = new List<RectTransform>();
            var statValues = new List<TextMeshProUGUI>();
            float rowW = Mathf.Min(contentW - 120f * u, 720f * u);
            for (int i = 0; i < statNames.Length; i++)
            {
                var r = NewRect("Stat" + i, root);
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(rowW, 100f * u);
                r.anchoredPosition = new Vector2(0f, rowY - 120f * u - i * 114f * u);
                var bg = NewRounded("Bg", r, new Color(0.02f, 0.07f, 0.22f, 0.92f), 26f);
                Stretch(bg.rectTransform);
                var accent = NewRounded("Accent", r, new Color(BarGreen.r, BarGreen.g, BarGreen.b, 0.9f), 8f);
                accent.rectTransform.anchorMin = new Vector2(0f, 0f);
                accent.rectTransform.anchorMax = new Vector2(0f, 1f);
                accent.rectTransform.pivot = new Vector2(0f, 0.5f);
                accent.rectTransform.sizeDelta = new Vector2(10f * u, -24f * u);
                accent.rectTransform.anchoredPosition = new Vector2(14f * u, 0f);
                var gl = NewRounded("Gloss", r, new Color(1f, 1f, 1f, 0.07f), 24f);
                gl.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                gl.rectTransform.anchorMax = Vector2.one;
                gl.rectTransform.offsetMin = new Vector2(4f * u, 0f);
                gl.rectTransform.offsetMax = new Vector2(-4f * u, -4f * u);
                var statLabel = NewText("Name", r, statNames[i], 42f * u, TextWhite, TextAlignmentOptions.MidlineLeft);
                Stretch(statLabel.rectTransform, 46f * u, 0f, rowW * 0.45f, 0f);
                var val = NewText("Value", r, "+0%", 54f * u, BarGreen, TextAlignmentOptions.MidlineRight);
                Stretch(val.rectTransform, rowW * 0.45f, 0f, 96f * u, 0f);
                var up = NewIcon("Up", r, arrowUpSprite, new Vector2(52f, 52f) * u);
                up.anchorMin = up.anchorMax = new Vector2(1f, 0.5f);
                up.anchoredPosition = new Vector2(-54f * u, 0f);
                var upImg = up.GetComponentInChildren<Image>();
                if (upImg != null) upImg.color = BarGreen;
                up.DOAnchorPosY(6f * u, 0.4f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
                r.localScale = Vector3.zero;
                statRows.Add(r);
                statValues.Add(val);
            }
            var totalT = NewText("Total", root, "Total bonus  +" + total + "%", 32f * u, TextDim, TextAlignmentOptions.Center);
            totalT.rectTransform.anchorMin = totalT.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            totalT.rectTransform.sizeDelta = new Vector2(rowW, 50f * u);
            totalT.rectTransform.anchoredPosition = new Vector2(0f, rowY - 120f * u - statNames.Length * 114f * u + 30f * u);
            var totalGroup = totalT.gameObject.AddComponent<CanvasGroup>();
            totalGroup.alpha = 0f;

            // ── Continue ──
            var cont = BuildButton3D(root, "ContinueButton", "CONTINUE", GreenBtn, new Vector2(460f, 136f) * u, 50f * u, out _);
            cont.anchorMin = cont.anchorMax = new Vector2(0.5f, 0.5f);
            cont.anchoredPosition = new Vector2(0f, totalT.rectTransform.anchoredPosition.y - 120f * u);
            cont.localScale = Vector3.zero;

            void Finish()
            {
                if (finished || !canContinue) return;
                finished = true;
                Sfx(clickSound);
                foreach (var b in root.GetComponentsInChildren<Button>()) b.interactable = false;
                var cg = root.gameObject.AddComponent<CanvasGroup>();
                holder.DOKill();
                holder.DOScale(0.2f, 0.3f).SetEase(Ease.InBack).SetUpdate(true);
                cg.DOFade(0f, 0.3f).SetUpdate(true).OnComplete(() =>
                {
                    KillTree(root);
                    Destroy(root.gameObject);
                    onDone?.Invoke();
                });
            }
            cont.GetComponent<Button>().onClick.AddListener(Finish);
            rootBtn.onClick.AddListener(Finish);

            // ── Timeline ──
            Sfx(swapSound != null ? swapSound : clickSound);
            var seq = DOTween.Sequence().SetUpdate(true).SetTarget(root);
            seq.AppendInterval(0.12f);
            seq.Append(holder.DOScale(1.18f, 0.5f).SetEase(Ease.OutCubic));
            seq.Join(holder.DOLocalRotate(new Vector3(0f, 720f, 0f), 0.65f, RotateMode.FastBeyond360).SetEase(Ease.OutCubic));
            seq.Append(holder.DOScale(1f, 0.16f).SetEase(Ease.InQuad));
            seq.AppendCallback(() =>
            {
                // Impact!
                holder.localRotation = Quaternion.identity;
                Sfx(upgradeSound != null ? upgradeSound : clickSound);
                SpawnFlash(card.visual);
                SpawnShockwave(root, new Vector2(0f, cardVisCenterY), rc);
                SpawnConfetti(root, new Vector2(0f, cardVisCenterY + visH * 0.3f), 70);
                SpawnSparkles(card.root, 22, Color.white);
                holder.DOShakeAnchorPos(0.4f, 22f * u, 24, 90f, false, true).SetUpdate(true);

                card.levelText.text = "Level " + newLevel;
                card.levelBand.rectTransform.DOKill(true);
                card.levelBand.rectTransform.DOPunchScale(Vector3.one * 0.35f, 0.5f, 6, 0.6f).SetUpdate(true);
                var bandCol = card.levelBand.color;
                card.levelBand.color = Color.white;
                card.levelBand.DOColor(bandCol, 0.5f).SetUpdate(true);

                banner.localScale = Vector3.one * 2.6f;
                bGroup.DOFade(1f, 0.15f).SetUpdate(true);
                banner.DOScale(1f, 0.45f).SetEase(Ease.OutBack, 2.2f).SetUpdate(true).OnComplete(() =>
                {
                    banner.DOScale(1.05f, 0.8f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
                });
            });
            seq.AppendInterval(0.3f);
            seq.Append(oldT.rectTransform.DOScale(1f, 0.3f).SetEase(Ease.OutBack, 2f));
            seq.Append(arrow.DOScale(1f, 0.25f).SetEase(Ease.OutBack, 2f));
            seq.Join(arrow.DOAnchorPosX(10f * u, 0.4f).SetEase(Ease.InOutSine).SetLoops(2, LoopType.Yoyo));
            seq.Append(newT.rectTransform.DOScale(1.25f, 0.25f).SetEase(Ease.OutBack, 3f));
            seq.Append(newT.rectTransform.DOScale(1f, 0.15f));
            for (int i = 0; i < statRows.Count; i++)
            {
                var r = statRows[i];
                var val = statValues[i];
                float fromX = (i % 2 == 0 ? -1f : 1f) * 500f * u;
                Vector2 home = r.anchoredPosition;
                seq.AppendCallback(() =>
                {
                    r.anchoredPosition = home + new Vector2(fromX, 0f);
                    Sfx(clickSound);
                });
                seq.Append(r.DOScale(1f, 0.3f).SetEase(Ease.OutBack, 2f));
                seq.Join(r.DOAnchorPos(home, 0.35f).SetEase(Ease.OutBack, 1.2f));
                int shown = 0;
                seq.Join(DOTween.To(() => shown, x => { shown = x; val.text = "+" + x + "%"; }, gain, 0.45f).SetEase(Ease.OutCubic));
                seq.AppendCallback(() => val.rectTransform.DOPunchScale(Vector3.one * 0.3f, 0.3f, 6, 0.6f).SetUpdate(true));
            }
            seq.Append(totalGroup.DOFade(1f, 0.25f));
            seq.Append(cont.DOScale(1f, 0.4f).SetEase(Ease.OutBack, 2f));
            seq.AppendCallback(() =>
            {
                canContinue = true;
                AddShine(cont);
            });
        }

        private void SpawnShockwave(RectTransform layer, Vector2 at, Color tint)
        {
            if (glowSprite == null) return;
            var ring = NewImage("Shockwave", layer, glowSprite, new Color(1f, 1f, 1f, 0.95f), true);
            var rt = ring.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(600f, 600f) * u;
            rt.anchoredPosition = at;
            rt.localScale = Vector3.one * 0.2f;
            rt.DOScale(2.6f, 0.6f).SetEase(Ease.OutCubic).SetUpdate(true);
            ring.DOColor(new Color(tint.r, tint.g, tint.b, 0f), 0.6f).SetEase(Ease.InQuad).SetUpdate(true)
                .OnComplete(() => { if (ring != null) Destroy(ring.gameObject); });
        }

        private static readonly Color[] ConfettiColors =
        {
            new Color(1f, 0.82f, 0.2f), new Color(1f, 0.35f, 0.55f), new Color(0.3f, 0.85f, 1f),
            new Color(0.4f, 0.95f, 0.4f), new Color(0.75f, 0.45f, 1f), Color.white
        };

        private void SpawnConfetti(RectTransform layer, Vector2 at, int count)
        {
            float floor = -(layer.rect.height / 2f) - 100f * u;
            for (int i = 0; i < count; i++)
            {
                var piece = NewImage("Confetti", layer, null, ConfettiColors[UnityEngine.Random.Range(0, ConfettiColors.Length)], false);
                var rt = piece.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(UnityEngine.Random.Range(12f, 20f), UnityEngine.Random.Range(22f, 36f)) * u;
                rt.anchoredPosition = at;
                rt.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
                float ang = UnityEngine.Random.Range(15f, 165f) * Mathf.Deg2Rad;     // mostly upward
                float dist = UnityEngine.Random.Range(260f, 720f) * u;
                Vector2 peak = at + new Vector2(Mathf.Cos(ang) * dist, Mathf.Sin(ang) * dist * 0.8f);
                float up = UnityEngine.Random.Range(0.35f, 0.55f);
                float fall = UnityEngine.Random.Range(1.4f, 2.4f);
                var seq = DOTween.Sequence().SetUpdate(true);
                seq.Append(rt.DOAnchorPos(peak, up).SetEase(Ease.OutCubic));
                seq.Append(rt.DOAnchorPos(new Vector2(peak.x + UnityEngine.Random.Range(-120f, 120f) * u, floor), fall).SetEase(Ease.InQuad));
                seq.Insert(0f, rt.DOLocalRotate(new Vector3(0f, 0f, UnityEngine.Random.Range(-720f, 720f)), up + fall, RotateMode.FastBeyond360).SetEase(Ease.Linear));
                seq.Insert(0f, rt.DOScaleX(-1f, UnityEngine.Random.Range(0.18f, 0.35f)).SetLoops(Mathf.CeilToInt((up + fall) / 0.25f), LoopType.Yoyo));
                seq.Insert(up + fall * 0.7f, piece.DOFade(0f, fall * 0.3f));
                seq.OnComplete(() => { if (piece != null) Destroy(piece.gameObject); });
            }
        }

        private RectTransform BuildUpgradeButton(RectTransform parent, int coins, Vector2 size, bool copiesOk, bool canAfford)
        {
            bool ready = copiesOk && canAfford;
            var btn = BuildButton3D(parent, "UpgradeButton", "", GoldBtn, size, 0f, out var face);

            var title = NewText("Title", face, "Upgrade", 34f * u, Color.white, TextAlignmentOptions.Top);
            Stretch(title.rectTransform, 0f, 0f, 0f, 12f * u);

            var row = NewRect("Cost", face);
            row.anchorMin = new Vector2(0f, 0f);
            row.anchorMax = new Vector2(1f, 0.6f);
            row.offsetMin = new Vector2(0f, 6f * u);
            row.offsetMax = Vector2.zero;
            float coinS = 64f * u;
            var coin = NewIcon("Coin", row, coinSprite, new Vector2(coinS, coinS));
            coin.anchorMin = coin.anchorMax = new Vector2(0.5f, 0.5f);
            string costStr = coins.ToString("N0");
            var ct = NewText("Value", row, costStr, 48f * u, canAfford ? Color.white : new Color(1f, 0.62f, 0.58f), TextAlignmentOptions.MidlineLeft);
            ct.rectTransform.anchorMin = ct.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            ct.rectTransform.sizeDelta = new Vector2(size.x, 60f * u);
            float textW = ct.GetPreferredValues(costStr).x;
            float groupW = coinS + 8f * u + textW;
            coin.anchoredPosition = new Vector2(-groupW / 2f + coinS / 2f, 0f);
            ct.rectTransform.pivot = new Vector2(0f, 0.5f);
            ct.rectTransform.anchoredPosition = new Vector2(-groupW / 2f + coinS + 8f * u, 0f);

            // Coin hops and squashes every so often
            DOTween.Sequence().SetUpdate(true).SetTarget(coin).SetLoops(-1)
                .AppendInterval(1.1f)
                .Append(coin.DOAnchorPosY(12f * u, 0.18f).SetEase(Ease.OutQuad))
                .Join(coin.DOLocalRotate(new Vector3(0f, 0f, 12f), 0.18f))
                .Append(coin.DOAnchorPosY(0f, 0.32f).SetEase(Ease.OutBounce))
                .Join(coin.DOLocalRotate(Vector3.zero, 0.32f))
                .Append(coin.DOPunchScale(new Vector3(0.18f, -0.12f, 0f), 0.25f, 6, 0.5f));

            AddShine(btn, ready);
            return btn;
        }

        // ── 3D buttons: outline, darker lip underneath, gradient face, gloss, top highlight, inner shadow ──
        private struct ButtonPalette
        {
            public Color top, bottom, lip, outline;
            public ButtonPalette(Color top, Color bottom, Color lip, Color outline)
            {
                this.top = top; this.bottom = bottom; this.lip = lip; this.outline = outline;
            }
        }

        private static readonly ButtonPalette GoldBtn = new ButtonPalette(
            new Color(1f, 0.88f, 0.38f), new Color(0.96f, 0.56f, 0.07f), new Color(0.64f, 0.31f, 0.02f), new Color(0.26f, 0.12f, 0.02f));
        private static readonly ButtonPalette BlueBtn = new ButtonPalette(
            new Color(0.50f, 0.78f, 1f), new Color(0.13f, 0.42f, 0.95f), new Color(0.07f, 0.21f, 0.58f), new Color(0.02f, 0.06f, 0.22f));
        private static readonly ButtonPalette GreenBtn = new ButtonPalette(
            new Color(0.64f, 0.98f, 0.40f), new Color(0.19f, 0.72f, 0.16f), new Color(0.08f, 0.40f, 0.08f), new Color(0.03f, 0.16f, 0.04f));

        private RectTransform BuildButton3D(RectTransform parent, string name, string label, ButtonPalette pal, Vector2 size, float fontSize, out RectTransform face)
        {
            var b = NewRect(name, parent);
            b.sizeDelta = size;
            float lipH = Mathf.Round(size.y * 0.09f);
            const float corner = 30f;

            var shadow = NewRounded("Shadow", b, new Color(0f, 0f, 0.05f, 0.45f), corner);
            Stretch(shadow.rectTransform, 2f * u, -12f * u, 2f * u, 10f * u);
            var outline = NewRounded("Outline", b, pal.outline, corner + 3f);
            Stretch(outline.rectTransform, -4f * u, -4f * u, -4f * u, -4f * u);
            outline.raycastTarget = true;
            var lip = NewRounded("Lip", b, pal.lip, corner);
            Stretch(lip.rectTransform);

            face = NewRect("Face", b);
            Stretch(face, 0f, lipH, 0f, 0f);
            var body = NewRounded("Body", face, Color.white, corner);
            Stretch(body.rectTransform);
            AddGradient(body, pal.top, pal.bottom);
            var inner = NewRounded("InnerShadow", face, Color.white, corner - 4f);
            inner.rectTransform.anchorMin = Vector2.zero;
            inner.rectTransform.anchorMax = new Vector2(1f, 0.4f);
            inner.rectTransform.offsetMin = new Vector2(5f * u, 4f * u);
            inner.rectTransform.offsetMax = new Vector2(-5f * u, 0f);
            AddGradient(inner, new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0.05f, 0.28f));
            var gloss = NewRounded("Gloss", face, Color.white, corner - 6f);
            gloss.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            gloss.rectTransform.anchorMax = Vector2.one;
            gloss.rectTransform.offsetMin = new Vector2(7f * u, 0f);
            gloss.rectTransform.offsetMax = new Vector2(-7f * u, -6f * u);
            AddGradient(gloss, new Color(1f, 1f, 1f, 0.42f), new Color(1f, 1f, 1f, 0.04f));
            var hi = NewCapsule("TopHighlight", face, new Color(1f, 1f, 1f, 0.75f), 5f * u);
            hi.rectTransform.anchorMin = new Vector2(0f, 1f);
            hi.rectTransform.anchorMax = new Vector2(1f, 1f);
            hi.rectTransform.pivot = new Vector2(0.5f, 1f);
            hi.rectTransform.sizeDelta = new Vector2(-40f * u, 5f * u);
            hi.rectTransform.anchoredPosition = new Vector2(0f, -5f * u);

            if (!string.IsNullOrEmpty(label))
            {
                var t = NewText("Label", face, label, fontSize, Color.white, TextAlignmentOptions.Center);
                Stretch(t.rectTransform, 8f * u, 2f * u, 8f * u, 0f);
                t.enableAutoSizing = true;
                t.fontSizeMin = fontSize * 0.6f;
                t.fontSizeMax = fontSize;
            }

            var btn = b.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = outline;
            b.gameObject.AddComponent<ShopPressFeedback>();
            var sink = b.gameObject.AddComponent<CardsScreenButtonSink>();
            sink.face = face;
            sink.depth = lipH * 0.7f;
            return b;
        }

        /// <summary>Round green badge with a drawn check mark.</summary>
        private RectTransform BuildCheckBadge(RectTransform parent, float size)
        {
            var c = NewRect("Check", parent);
            c.sizeDelta = new Vector2(size, size);
            var ring = NewImage("Ring", c, circleSprite, new Color(0.02f, 0.10f, 0.05f, 1f), true);
            Stretch(ring.rectTransform);
            var disc = NewImage("Disc", c, circleSprite, Color.white, true);
            Stretch(disc.rectTransform, size * 0.07f, size * 0.07f, size * 0.07f, size * 0.07f);
            AddGradient(disc, new Color(0.62f, 1f, 0.45f), new Color(0.16f, 0.66f, 0.18f));
            float k = size / 70f;
            AddStroke(c, new Vector2(-16f, 1f) * k, new Vector2(-4f, -11f) * k, 8f * k);
            AddStroke(c, new Vector2(-7f, -11f) * k, new Vector2(17f, 13f) * k, 8f * k);
            return c;
        }

        private void AddStroke(RectTransform parent, Vector2 from, Vector2 to, float thickness)
        {
            var s = NewCapsule("Stroke", parent, Color.white, thickness);
            var rt = s.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            Vector2 d = to - from;
            rt.sizeDelta = new Vector2(d.magnitude + thickness * 0.5f, thickness);
            rt.anchoredPosition = from;
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        private void OnUpgradePressed(CardDefinition def, RectTransform btn)
        {
            if (inventory == null || popupBusy) return;
            var fb = btn.GetComponent<ShopPressFeedback>();
            if (!inventory.CanUpgradeCard(def.cardId, out _, out _, out string reason))
            {
                Sfx(errorSound);
                if (fb != null) fb.Deny();
                ShowToast(reason, new Color(0.55f, 0.1f, 0.15f, 0.95f));
                return;
            }

            int oldLevel = EntryOf(def.cardId)?.level ?? 1;
            if (!inventory.TryUpgradeCard(def.cardId)) { Sfx(errorSound); return; }
            int newLevel = EntryOf(def.cardId)?.level ?? oldLevel + 1;

            Sfx(upgradeSound != null ? upgradeSound : clickSound);
            if (fb != null) fb.Punch(0.25f);
            RefreshCounters(true);
            RefreshDeck(false);
            RefreshCollectionStates();
            RefreshBadge();

            var source = detailSource;
            popupBusy = true;
            if (detailHero != null)
            {
                SpawnFlash(detailHero);
                SpawnSparkles(detailHero, 18, GoldRim);
            }

            DOVirtual.DelayedCall(0.35f, () =>
            {
                popupBusy = false;
                CloseDetail(true);
                PlayLevelUp(def, oldLevel, newLevel, () => CelebrateTile(source));
            }).SetUpdate(true);
        }

        private void CelebrateTile(CardsScreenCardView v)
        {
            if (v == null || v.visual == null || !v.gameObject.activeInHierarchy) return;
            v.visual.DOKill(true);
            v.visual.DOPunchScale(Vector3.one * 0.25f, 0.5f, 7, 0.6f).SetUpdate(true);
            SpawnSparkles(v.root, 10, GoldRim);
        }

        private void CloseDetail(bool instant)
        {
            if (detailRoot == null) return;
            var root = detailRoot;
            var dim = detailDim;
            var panel = detailPanel;
            var hero = detailHero;
            var source = detailSource;
            detailRoot = null;
            detailPanel = null;
            detailHero = null;
            detailSource = null;

            if (instant)
            {
                KillTree(root);
                Destroy(root.gameObject);
                if (source != null) source.visualGroup.alpha = 1f;
                popupBusy = false;
                return;
            }

            foreach (var b in root.GetComponentsInChildren<Button>()) b.interactable = false;
            if (panel != null)
            {
                panel.DOKill();
                panel.DOScale(0.7f, 0.18f).SetEase(Ease.InBack).SetUpdate(true);
                var cg = panel.GetComponent<CanvasGroup>();
                if (cg != null) { cg.DOKill(); cg.DOFade(0f, 0.16f).SetUpdate(true); }
            }
            var fx = root.Find("HeroFX");
            if (fx != null) { var cg = fx.GetComponent<CanvasGroup>(); if (cg != null) { cg.DOKill(); cg.DOFade(0f, 0.15f).SetUpdate(true); } }

            // Hero card flies back into its tile
            float dur = 0.3f;
            if (hero != null && source != null && source.visual != null && source.gameObject.activeInHierarchy)
            {
                DOTween.Kill(hero);
                hero.DOKill();
                Vector2 back = (Vector2)root.InverseTransformPoint(source.root.position) - (Vector2)root.rect.center;
                hero.DOAnchorPos(back, dur).SetEase(Ease.InOutCubic).SetUpdate(true);
                hero.DOScale(cardW / hero.sizeDelta.x, dur).SetEase(Ease.InOutCubic).SetUpdate(true);
            }
            else if (hero != null)
            {
                hero.DOKill();
                hero.DOScale(0f, 0.2f).SetEase(Ease.InBack).SetUpdate(true);
            }

            dim.DOKill();
            dim.DOFade(0f, dur).SetUpdate(true).OnComplete(() =>
            {
                if (source != null && source.visualGroup != null)
                {
                    source.visualGroup.alpha = 1f;
                    source.visual.DOKill(true);
                    source.visual.DOPunchScale(Vector3.one * 0.08f, 0.25f, 6, 0.6f).SetUpdate(true);
                }
                KillTree(root);
                Destroy(root.gameObject);
                if (isOpen)
                {
                    RefreshDeck(false);          // clears "New!" on both the deck and collection copies
                    RefreshCollectionStates();
                }
            });
        }

        // ═════════════════════════════ FX helpers ═════════════════════════════

        private RectTransform BuildButton(RectTransform parent, string label, Sprite sprite, Color fallback, Vector2 size, float fontSize)
        {
            var b = NewRect((string.IsNullOrEmpty(label) ? "Upgrade" : label) + "Button", parent);
            b.sizeDelta = size;
            var img = sprite != null ? NewImage("Bg", b, sprite, Color.white, false) : NewRounded("Bg", b, fallback, 30f);
            Stretch(img.rectTransform);
            img.raycastTarget = true;
            if (!string.IsNullOrEmpty(label))
            {
                var t = NewText("Label", b, label, fontSize, Color.white, TextAlignmentOptions.Center);
                Stretch(t.rectTransform, 0f, 6f * u, 0f, 0f);
            }
            var btn = b.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;
            b.gameObject.AddComponent<ShopPressFeedback>();
            return b;
        }

        /// <summary>Diagonal light streak that sweeps across a button every couple of seconds.</summary>
        private void AddShine(RectTransform btn, bool breathe = true)
        {
            if (shineSprite == null) return;
            var mask = NewRect("ShineMask", btn);
            Stretch(mask, 10f * u, 10f * u, 10f * u, 12f * u);
            mask.gameObject.AddComponent<RectMask2D>();
            var shine = NewImage("Shine", mask, shineSprite, new Color(1f, 1f, 1f, 0.75f), false);
            var srt = shine.rectTransform;
            srt.anchorMin = srt.anchorMax = new Vector2(0f, 0.5f);
            float w = btn.sizeDelta.x;
            srt.sizeDelta = new Vector2(70f * u, btn.sizeDelta.y * 2f);
            srt.localRotation = Quaternion.Euler(0f, 0f, -22f);
            srt.anchoredPosition = new Vector2(-80f * u, 0f);
            DOTween.Sequence().SetUpdate(true).SetTarget(srt).SetLoops(-1)
                .AppendInterval(0.6f)
                .Append(srt.DOAnchorPosX(w + 80f * u, 0.65f).SetEase(Ease.InOutSine))
                .AppendInterval(1.2f)
                .AppendCallback(() => srt.anchoredPosition = new Vector2(-80f * u, 0f));

            // Soft breathing so the call to action pops
            if (breathe) btn.DOScale(1.04f, 0.7f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetDelay(0.9f).SetUpdate(true);
        }

        /// <summary>Fill sprite for progress bars (a white glossy capsule so rarity / state tints stay true).</summary>
        private Sprite BarFill => barFillSprite != null ? barFillSprite : generatedBarSprite;

        private Sprite CreateBarSprite()
        {
            const int w = 128, h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "CardsBarFill" };
            var px = new Color32[w * h];
            float r = h / 2f;
            for (int y = 0; y < h; y++)
            {
                float ty = y / (float)(h - 1);                 // 0 bottom .. 1 top
                float shade = Mathf.Lerp(0.72f, 1f, ty);
                if (ty > 0.62f && ty < 0.84f) shade = 1f;     // glossy highlight stripe
                for (int x = 0; x < w; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, r, w - r);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, r));
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    float hl = ty > 0.62f && ty < 0.84f ? 1f : shade;
                    px[y * w + x] = new Color(hl, hl, hl, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>White anti-aliased circle with 31px borders: sliced, it draws a perfect capsule at any size.</summary>
        private Sprite CreateCapsuleSprite()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "CardsCapsule" };
            var px = new Color32[n * n];
            float r = n / 2f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(CapsuleBorder, CapsuleBorder, CapsuleBorder, CapsuleBorder));
        }

        /// <summary>Sliced capsule whose rounded ends have radius thickness/2 (thickness in canvas units).</summary>
        private Image NewCapsule(string name, Transform parent, Color color, float thickness)
        {
            var img = NewImage(name, parent, capsuleSprite, color, false);
            if (capsuleSprite != null)
            {
                img.type = Image.Type.Sliced;
                float canvasPPU = rootCanvas != null ? rootCanvas.referencePixelsPerUnit : 100f;
                float radius = Mathf.Max(1f, thickness * 0.5f);
                img.pixelsPerUnitMultiplier = Mathf.Max(0.01f, CapsuleBorder * canvasPPU / (capsuleSprite.pixelsPerUnit * radius));
            }
            return img;
        }

        private static CardsScreenGradient AddGradient(Graphic g, Color from, Color to, bool horizontal = false)
        {
            var grad = g.gameObject.AddComponent<CardsScreenGradient>();
            grad.Set(from, to, horizontal);
            return grad;
        }

        private Sprite CreateShineSprite()
        {
            const int w = 32, h = 4;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "CardsShine" };
            var px = new Color32[w * h];
            for (int x = 0; x < w; x++)
            {
                float t = x / (float)(w - 1);
                float a = Mathf.Pow(Mathf.Sin(t * Mathf.PI), 2f);
                for (int y = 0; y < h; y++) px[y * w + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        private void ShowToast(string message, Color bg)
        {
            if (popupLayer == null) return;
            popupLayer.SetAsLastSibling();
            var toast = NewRect("Toast", popupLayer);
            toast.anchorMin = toast.anchorMax = new Vector2(0.5f, 1f);
            toast.pivot = new Vector2(0.5f, 1f);
            toast.sizeDelta = new Vector2(700f, 100f) * u;
            toast.anchoredPosition = new Vector2(0f, -260f * u);
            var img = NewRounded("Bg", toast, bg, 26f);
            Stretch(img.rectTransform);
            var cg = toast.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            var t = NewText("Text", toast, message, 36f * u, Color.white, TextAlignmentOptions.Center);
            Stretch(t.rectTransform, 20f * u, 0f, 20f * u, 0f);
            t.enableAutoSizing = true;
            t.fontSizeMin = 20f * u;
            t.fontSizeMax = 36f * u;
            t.textWrappingMode = TextWrappingModes.Normal;

            toast.localScale = Vector3.one * 0.6f;
            cg.alpha = 0f;
            var seq = DOTween.Sequence().SetUpdate(true);
            seq.Append(toast.DOScale(1f, 0.3f).SetEase(Ease.OutBack, 2f));
            seq.Join(cg.DOFade(1f, 0.15f));
            seq.AppendInterval(1.5f);
            seq.Append(cg.DOFade(0f, 0.3f));
            seq.Join(toast.DOAnchorPosY(toast.anchoredPosition.y + 40f * u, 0.3f));
            seq.OnComplete(() => { if (toast != null) Destroy(toast.gameObject); });
        }

        private void SpawnFlash(RectTransform parent)
        {
            var flash = NewRect("Flash", parent);
            Stretch(flash);
            var img = flash.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 1f, 0.92f, 0.85f);
            img.raycastTarget = false;
            img.DOFade(0f, 0.4f).SetEase(Ease.OutQuad).SetUpdate(true).OnComplete(() => Destroy(flash.gameObject));
        }

        private void SpawnSparkles(RectTransform target, int count, Color tint)
        {
            if (sparkleSprite == null || target == null || popupLayer == null) return;
            var layer = dragLayer != null && pageRoot != null && target.IsChildOf(pageRoot) ? dragLayer : popupLayer;
            Vector2 at = (Vector2)layer.InverseTransformPoint(target.position) - (Vector2)layer.rect.center;
            for (int i = 0; i < count; i++)
            {
                var sp = NewImage("Sparkle", layer, sparkleSprite, tint, true);
                var rt = sp.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                float size = UnityEngine.Random.Range(22f, 50f) * u;
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = at;
                rt.localScale = Vector3.zero;
                float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float dist = UnityEngine.Random.Range(80f, 220f) * u;
                Vector2 to = at + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist;
                float dur = UnityEngine.Random.Range(0.45f, 0.8f);
                var seq = DOTween.Sequence().SetUpdate(true);
                seq.Append(rt.DOScale(1f, dur * 0.3f).SetEase(Ease.OutBack));
                seq.Join(rt.DOAnchorPos(to, dur).SetEase(Ease.OutCubic));
                seq.Join(rt.DOLocalRotate(new Vector3(0, 0, UnityEngine.Random.Range(-180f, 180f)), dur));
                seq.Insert(dur * 0.55f, sp.DOFade(0f, dur * 0.45f));
                seq.OnComplete(() => { if (sp != null) Destroy(sp.gameObject); });
            }
        }

        private void Sfx(AudioClip clip)
        {
            if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
        }

        // ═════════════════════════════ Rarity colours ═════════════════════════════

        public static Color RarityMain(CardRarity r)
        {
            switch (r)
            {
                case CardRarity.Rare: return new Color(0.30f, 0.62f, 1f);
                case CardRarity.Epic: return new Color(0.76f, 0.38f, 1f);
                case CardRarity.Legendary: return new Color(1f, 0.80f, 0.22f);
                default: return new Color(0.72f, 0.78f, 0.88f);
            }
        }

        private static Color RarityBand(CardRarity r)
        {
            switch (r)
            {
                case CardRarity.Rare: return new Color(0.12f, 0.42f, 0.90f);
                case CardRarity.Epic: return new Color(0.52f, 0.18f, 0.82f);
                case CardRarity.Legendary: return new Color(0.92f, 0.60f, 0.06f);
                default: return new Color(0.38f, 0.46f, 0.60f);
            }
        }

        private static Color RarityPlate(CardRarity r)
        {
            switch (r)
            {
                case CardRarity.Rare: return new Color(0.16f, 0.44f, 0.95f);
                case CardRarity.Epic: return new Color(0.58f, 0.24f, 0.90f);
                case CardRarity.Legendary: return new Color(1f, 0.74f, 0.16f);
                default: return new Color(0.55f, 0.62f, 0.74f);
            }
        }

        // ═════════════════════════════ Scene hooks ═════════════════════════════

        private IEnumerator HookSceneNextFrame()
        {
            yield return null; // let MainMenuController / ShopController wire their own listeners first
            if (bottomNav == null && canvasRect != null)
            {
                var nav = FindDeep(canvasRect, "downpanel");
                if (nav != null) bottomNav = nav as RectTransform;
            }
            EnsureSiblingBelowNav();
            if (!hookBottomNav || bottomNav == null) yield break;

            for (int i = 0; i < bottomNav.childCount; i++)
            {
                var tab = bottomNav.GetChild(i);
                var btn = tab.GetComponent<Button>();
                if (btn == null)
                {
                    if (tab.GetComponent<Graphic>() == null)
                    {
                        var hitImg = tab.gameObject.AddComponent<Image>();
                        hitImg.color = new Color(0f, 0f, 0f, 0f);
                    }
                    btn = tab.gameObject.AddComponent<Button>();
                    btn.transition = Selectable.Transition.None;
                }
                int idx = i;
                btn.onClick.AddListener(() => OnNavTabClicked(idx));
            }
        }

        private void OnNavTabClicked(int index)
        {
            if (index == cardsTabIndex)
            {
                if (isOpen) PunchTab(currentTab);
                else Open();
            }
            else Close();
        }

        private void EnsureSiblingBelowNav()
        {
            if (bottomNav == null || bottomNav.parent != transform.parent) return;
            int navIndex = bottomNav.GetSiblingIndex();
            if (transform.GetSiblingIndex() > navIndex) transform.SetSiblingIndex(navIndex);
        }

        private void UpdateViewportBottom()
        {
            if (viewport == null) return;
            float bottom = 0f;
            if (bottomNav != null && bottomNav.gameObject.activeInHierarchy)
            {
                var corners = new Vector3[4];
                bottomNav.GetWorldCorners(corners);
                float navTop = Mathf.Max(corners[1].y, corners[2].y);
                Vector3 local = pageRoot.InverseTransformPoint(new Vector3(corners[1].x, navTop, corners[1].z));
                // Let the pages scroll under the bottom nav (drawn on top); content keeps bottom padding to clear it.
                bottom = 0f;
            }
            viewport.offsetMin = new Vector2(viewport.offsetMin.x, bottom);
        }

        // ═════════════════════════════ UI helpers ═════════════════════════════

        private TMP_FontAsset ResolveFont()
        {
            if (canvasRect != null)
            {
                var top = FindDeep(canvasRect, "toppanel");
                if (top != null)
                {
                    var t = top.GetComponentInChildren<TMP_Text>(true);
                    if (t != null && t.font != null) return t.font;
                }
                foreach (var t in canvasRect.GetComponentsInChildren<TMP_Text>(true))
                    if (t.font != null) return t.font;
            }
            return TMP_Settings.defaultFontAsset;
        }

        private static Material CreateOutlineMaterial(TMP_FontAsset f)
        {
            if (f == null || f.material == null) return null;
            var m = new Material(f.material) { name = f.name + " (Cards Outline)" };
            m.EnableKeyword("OUTLINE_ON");
            if (m.HasProperty("_OutlineWidth")) m.SetFloat("_OutlineWidth", 0.22f);
            if (m.HasProperty("_OutlineColor")) m.SetColor("_OutlineColor", new Color(0.04f, 0.06f, 0.20f, 1f));
            if (m.HasProperty("_FaceDilate")) m.SetFloat("_FaceDilate", 0.12f);
            m.EnableKeyword("UNDERLAY_ON");
            if (m.HasProperty("_UnderlayColor")) m.SetColor("_UnderlayColor", new Color(0f, 0f, 0.05f, 0.6f));
            if (m.HasProperty("_UnderlayOffsetY")) m.SetFloat("_UnderlayOffsetY", -0.9f);
            if (m.HasProperty("_UnderlaySoftness")) m.SetFloat("_UnderlaySoftness", 0.15f);
            return m;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static void Stretch(RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>Anchors to the parent's top-centre: x offset, distance from top, size.</summary>
        private static void PlaceTop(RectTransform rt, float x, float top, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -top);
        }

        private Image NewImage(string name, Transform parent, Sprite sprite, Color color, bool preserveAspect)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.preserveAspect = preserveAspect;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>9-sliced rounded rectangle; cornerRef is the corner radius in reference pixels.</summary>
        private Image NewRounded(string name, Transform parent, Color color, float cornerRef)
        {
            var img = NewImage(name, parent, roundedSprite, color, false);
            if (roundedSprite != null && roundedSprite.border != Vector4.zero)
            {
                img.type = Image.Type.Sliced;
                float border = Mathf.Max(roundedSprite.border.x, roundedSprite.border.y);
                float canvasPPU = rootCanvas != null ? rootCanvas.referencePixelsPerUnit : 100f;
                float spritePPU = roundedSprite.pixelsPerUnit;
                // corner size on canvas = border * canvasPPU / (spritePPU * multiplier)
                float desired = Mathf.Max(1f, cornerRef * u);
                img.pixelsPerUnitMultiplier = Mathf.Max(0.01f, border * canvasPPU / (spritePPU * desired));
            }
            return img;
        }

        private RectTransform NewIcon(string name, Transform parent, Sprite sprite, Vector2 box)
        {
            var holder = NewRect(name, parent);
            Rect vis = new Rect(0f, 0f, 1f, 1f);
            if (sprite != null && trimLookup.TryGetValue(sprite, out var t) && t.width > 0.01f && t.height > 0.01f) vis = t;

            Vector2 fitted = box;
            if (sprite != null)
            {
                float visAspect = (vis.width * sprite.rect.width) / Mathf.Max(1f, vis.height * sprite.rect.height);
                float boxAspect = box.x / Mathf.Max(0.001f, box.y);
                fitted = visAspect > boxAspect ? new Vector2(box.x, box.x / visAspect) : new Vector2(box.y * visAspect, box.y);
            }
            holder.sizeDelta = box;

            var fitRT = NewRect("Fit", holder);
            fitRT.anchorMin = fitRT.anchorMax = new Vector2(0.5f, 0.5f);
            fitRT.sizeDelta = fitted;

            var imgRT = NewRect("Image", fitRT);
            imgRT.anchorMin = new Vector2(-vis.xMin / vis.width, -vis.yMin / vis.height);
            imgRT.anchorMax = new Vector2((1f - vis.xMin) / vis.width, (1f - vis.yMin) / vis.height);
            imgRT.offsetMin = imgRT.offsetMax = Vector2.zero;
            var img = imgRT.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            if (sprite == null) img.color = new Color(1f, 1f, 1f, 0f);
            return holder;
        }

        private TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            if (outlineMaterial != null) t.fontSharedMaterial = outlineMaterial;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = FontStyles.Bold;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static void AddLayout(RectTransform rt, float w, float h)
        {
            var le = rt.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = w;
            le.preferredHeight = h;
            le.minWidth = w;
            le.minHeight = h;
        }

        private static void AddFlexibleSpacer(Transform parent)
        {
            var rt = NewRect("Spacer", parent);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
        }

        private static void AddSpacer(Transform parent, float width)
        {
            var rt = NewRect("Gap", parent);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.minWidth = width;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent == null) return null;
            if (string.Equals(parent.name, name, StringComparison.OrdinalIgnoreCase)) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var r = FindDeep(parent.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        private static void KillTree(Transform root)
        {
            if (root == null) return;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                t.DOKill();
                DOTween.Kill(t);
                var g = t.GetComponent<Graphic>();
                if (g != null) { g.DOKill(); DOTween.Kill(g); }
                var cg = t.GetComponent<CanvasGroup>();
                if (cg != null) cg.DOKill();
            }
        }
    }
}
