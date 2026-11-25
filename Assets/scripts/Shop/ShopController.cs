using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Siege.Cards;
using Siege.Chests;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Siege.Shop
{
    public enum ShopRewardKind { Chest, Coins, CardCopies, GemPack }

    [Serializable]
    public class ShopOffer
    {
        public string offerId;
        public string title;
        public ShopRewardKind kind;
        public int gemPrice;              // cost in gems (Chest / Coins / CardCopies)
        public string realMoneyLabel;     // GemPack price label, e.g. "$4.99"
        public int amount;                // coins / gems / card copies
        public ChestType chestType;
        public string cardId;
        public bool limitedDaily;         // one purchase per 24h cycle
        [NonSerialized] public Sprite icon;
    }

    /// <summary>Visible-area trim for a sprite with transparent padding (fractions 0..1, Unity UV space).</summary>
    [Serializable]
    public class ShopSpriteTrim
    {
        public Sprite sprite;
        public Rect visible = new Rect(0f, 0f, 1f, 1f);
    }

    /// <summary>
    /// Kings of Siege shop page (Daily Offers, Cards, Coins, Gems).
    /// The whole UI is generated at runtime from the sprites assigned in the inspector
    /// (Kings of Siege > Build Shop Page wires everything), sized from the live canvas so it
    /// fits any portrait resolution. All motion is DOTween.
    ///
    /// Integration (no edits to existing scripts):
    ///  - Currency: PlayerWallet (PlayerCoins / PlayerGems prefs keys).
    ///  - Cards: CardInventoryManager.AddCardCopies.
    ///  - Chests: rolled with the ChestSystemController's ChestConfig (coins range + copies).
    ///  - Bottom nav: listens to the existing "downpanel" tabs (tab 0 = Shop).
    /// </summary>
    public class ShopController : MonoBehaviour
    {
        public static ShopController Instance { get; private set; }

        // ───────────────────────────── Inspector ─────────────────────────────
        [Header("Sprites (auto-assigned by Kings of Siege > Build Shop Page)")]
        public Sprite backgroundSprite;
        public Sprite offerPanelSprite;
        public Sprite cardPanelSprite;
        public Sprite coinPanelSprite;
        public Sprite buyButtonSprite;
        public Sprite plusButtonSprite;
        public Sprite plusGlyphSprite;
        public Sprite chevronSprite;
        public Sprite coinSprite;
        public Sprite gemSprite;
        public Sprite shopIconSprite;
        public Sprite calendarSprite;
        public Sprite cardsIconSprite;
        public Sprite silverChestSprite;
        public Sprite goldChestSprite;
        public Sprite silverChestOpenSprite;
        public Sprite goldChestOpenSprite;
        public Sprite coinStackSprite;
        public Sprite coinBagSprite;
        public Sprite dividerSprite;
        public Sprite circleSprite;
        public Sprite roundedSprite;
        public Sprite popupPanelSprite;
        public Sprite closeSprite;
        public Sprite raysSprite;
        public Sprite glowSprite;
        public Sprite sparkleSprite;
        public TMP_FontAsset font;
        public List<ShopSpriteTrim> spriteTrims = new List<ShopSpriteTrim>();

        [Header("Daily Offers (gem prices)")]
        public int silverChestPrice = 250;
        public int goldChestPrice = 750;
        public int dailyCoinsAmount = 2000;
        public int dailyCoinsPrice = 300;
        [Tooltip("Hours between daily offer refreshes.")]
        public float refreshHours = 24f;

        [Header("Cards (rotate every refresh; day one shows these ids)")]
        public string[] firstCycleCardIds = { "swordsman", "archer", "mace_warrior" };
        public int[] cardOfferCopies = { 3, 3, 4 };
        public int[] cardOfferPrices = { 500, 700, 1000 };
        public CardLibrary cardLibraryOverride;

        [Header("Coins (amount for gem price)")]
        public int[] coinPackAmounts = { 5000, 15000, 50000 };
        public int[] coinPackPrices = { 100, 250, 800 };

        [Header("Gems (real-money packs)")]
        public int[] gemPackAmounts = { 500, 1200, 2500 };
        public string[] gemPackPriceLabels = { "$4.99", "$9.99", "$19.99" };
        [Tooltip("No store/IAP is connected yet: when ON, gem packs are granted straight away after the confirm popup. " +
                 "Turn OFF and call CompleteGemPackPurchase(offerId) from your IAP callback when billing is added.")]
        public bool grantGemPacksWithoutPayment = true;

        [Header("Scene Hooks")]
        [Tooltip("The main menu bottom navigation bar (auto-found by name 'downpanel').")]
        public RectTransform bottomNav;
        public int shopTabIndex = 0;
        public int cardsTabIndex = 1;
        public bool hookBottomNav = true;
        [Tooltip("Tapping the top-bar coin / gem counters on the home screen opens the shop.")]
        public bool hookHomeCurrencyCounters = true;

        [Header("Audio (optional; falls back to the chest system sounds)")]
        public AudioSource audioSource;
        public AudioClip clickSound;
        public AudioClip purchaseSound;
        public AudioClip coinSound;
        public AudioClip errorSound;
        public AudioClip chestOpenSound;

        // ───────────────────────────── Constants ─────────────────────────────
        private const float REF_W = 941f;                  // store reference layout width
        private const string PREF_CYCLE_START = "KoS_Shop_DailyCycleStartUtc";
        private const string PREF_CYCLE_INDEX = "KoS_Shop_DailyCycleIndex";
        private const string PREF_PURCHASED = "KoS_Shop_PurchasedThisCycle";

        private static readonly Color TitleWhite = new Color(1f, 0.98f, 0.92f, 1f);
        private static readonly Color PriceRed = new Color(1f, 0.55f, 0.55f, 1f);
        private static readonly Color SoldGrey = new Color(0.55f, 0.58f, 0.66f, 1f);
        private static readonly Color BadgePurple = new Color(0.56f, 0.22f, 0.86f, 1f);
        private static readonly Color BgBlue = new Color(0.07f, 0.17f, 0.47f, 1f);

        // ───────────────────────────── Runtime UI ─────────────────────────────
        private class OfferCellView
        {
            public ShopOffer offer;
            public RectTransform cell;
            public RectTransform visual;
            public CanvasGroup group;
            public Image panel;
            public TextMeshProUGUI title;
            public RectTransform productHolder;
            public Image product;
            public RectTransform rays;
            public Button button;
            public Image buttonImage;
            public ShopPressFeedback buttonFeedback;
            public Image priceGem;
            public TextMeshProUGUI priceText;
            public LayoutElement priceLayout;
            public RectTransform badge;
            public TextMeshProUGUI badgeText;
            public Vector2 productRestPos;
        }

        private class SectionView
        {
            public RectTransform row;
            public RectTransform inner;
            public CanvasGroup group;
            public RectTransform icon;
        }

        private RectTransform canvasRect;
        private Canvas rootCanvas;
        private RectTransform pageRoot;
        private CanvasGroup pageGroup;
        private RectTransform headerInner;
        private RectTransform scrollRectRT;
        private ScrollRect scrollRect;
        private RectTransform content;
        private RectTransform popupLayer;

        private TextMeshProUGUI coinText;
        private TextMeshProUGUI gemText;
        private RectTransform coinIconRT;
        private RectTransform gemIconRT;
        private TextMeshProUGUI timerText;

        private SectionView dailySection, cardsSection, coinsSection, gemsSection;
        private LayoutElement gemsBody;
        private RectTransform gemsRow;
        private RectTransform gemsChevron;
        private bool gemsExpanded;
        private float gemsBodyHeight;

        private readonly List<OfferCellView> cells = new List<OfferCellView>();
        private readonly List<SectionView> sections = new List<SectionView>();
        private readonly List<Tween> idleTweens = new List<Tween>();
        private readonly Dictionary<Sprite, Rect> trimLookup = new Dictionary<Sprite, Rect>();

        private Material outlineMaterial;
        private float u = 1f;            // reference px -> canvas units
        private float builtWidth = -1f;
        private bool isOpen;
        private bool isBusy;             // a purchase popup/sequence is running
        private float timerTick;

        private TMP_Text homeCoinText;
        private TMP_Text homeGemText;

        private int shownCoins;
        private int shownGems;
        private int coinDisplayHold;
        private int gemDisplayHold;
        private Tween coinCountTween;
        private Tween gemCountTween;

        // Daily cycle state
        private DateTime cycleStartUtc;
        private int cycleIndex;
        private readonly HashSet<string> purchased = new HashSet<string>();

        public bool IsOpen => isOpen;

        // ═════════════════════════════ Lifecycle ═════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            PlayerWallet.EnsureSeeded();
            LoadDailyState();

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
            PlayerWallet.OnCoinsChanged += HandleCoinsChanged;
            PlayerWallet.OnGemsChanged += HandleGemsChanged;
        }

        private void OnDisable()
        {
            PlayerWallet.OnCoinsChanged -= HandleCoinsChanged;
            PlayerWallet.OnGemsChanged -= HandleGemsChanged;
        }

        private void Start()
        {
            shownCoins = PlayerWallet.Coins;
            shownGems = PlayerWallet.Gems;
            FindHomeCounters();
            SetHomeCounters(shownCoins, shownGems);
            StartCoroutine(HookSceneNextFrame());
        }

        private void OnDestroy()
        {
            KillIdle();
            coinCountTween?.Kill();
            gemCountTween?.Kill();
            if (popupLayer != null) Destroy(popupLayer.gameObject);
            if (outlineMaterial != null) Destroy(outlineMaterial);
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            timerTick -= Time.unscaledDeltaTime;
            if (timerTick > 0f) return;
            timerTick = 0.5f;

            if (CheckCycleElapsed())
            {
                if (pageRoot != null) OnDailyRefresh(isOpen);
            }
            if (isOpen) UpdateTimerText();
        }

        // ═════════════════════════════ Public API ═════════════════════════════

        public void Open() => OpenInternal(null);
        public void OpenAtCoins() => OpenInternal(() => coinsSection);
        public void OpenAtGems() => OpenInternal(() => gemsSection);

        public void Toggle()
        {
            if (isOpen) Close(); else Open();
        }

        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;
            KillIdle();
            if (pageRoot == null) return;

            pageGroup.DOKill();
            pageGroup.interactable = false;
            pageGroup.blocksRaycasts = false;
            pageGroup.DOFade(0f, 0.18f).SetEase(Ease.InQuad).SetUpdate(true)
                .OnComplete(() => { if (!isOpen && pageRoot != null) pageRoot.gameObject.SetActive(false); });
        }

        /// <summary>Call from your IAP success callback when grantGemPacksWithoutPayment is off.</summary>
        public void CompleteGemPackPurchase(string offerId)
        {
            var cell = cells.FirstOrDefault(c => c.offer.offerId == offerId);
            if (cell == null) return;
            GrantGemPack(cell);
        }

        // ═════════════════════════════ Open / Build ═════════════════════════════

        private void OpenInternal(Func<SectionView> focusSelector)
        {
            if (canvasRect == null) return;
            Canvas.ForceUpdateCanvases();

            float w = canvasRect.rect.width;
            if (pageRoot == null || Mathf.Abs(w - builtWidth) > 1f) Build();

            EnsureSiblingBelowNav();
            pageRoot.gameObject.SetActive(true);
            UpdateViewportBottom();
            RefreshCountersInstant();
            RefreshAllCellStates();
            UpdateTimerText();

            bool wasOpen = isOpen;
            isOpen = true;
            SectionView focus = focusSelector != null ? focusSelector() : null;

            pageGroup.DOKill();
            pageGroup.interactable = true;
            pageGroup.blocksRaycasts = true;

            if (!wasOpen)
            {
                if (focus == null) scrollRect.verticalNormalizedPosition = 1f;
                PlayEntrance();
                Sfx(clickSound);
            }
            else
            {
                pageGroup.alpha = 1f;
            }

            if (focus != null) StartCoroutine(FocusSectionRoutine(focus, wasOpen ? 0f : 0.35f));
        }

        private void Build()
        {
            if (pageRoot != null)
            {
                KillTree(pageRoot);
                Destroy(pageRoot.gameObject);
            }
            cells.Clear();
            sections.Clear();
            KillIdle();

            float W = canvasRect.rect.width;
            float H = canvasRect.rect.height;
            float contentW = Mathf.Min(W, H * 0.62f);
            u = contentW / REF_W;
            builtWidth = W;

            if (font == null) font = ResolveFont();
            if (outlineMaterial == null && font != null) outlineMaterial = CreateOutlineMaterial(font);

            // Root
            pageRoot = NewRect("Page", transform);
            Stretch(pageRoot);
            pageGroup = pageRoot.gameObject.AddComponent<CanvasGroup>();

            var solid = pageRoot.gameObject.AddComponent<Image>();
            solid.color = BgBlue;
            solid.raycastTarget = true;   // block clicks to the home screen underneath

            if (backgroundSprite != null)
            {
                var bg = NewImage("Background", pageRoot, backgroundSprite, Color.white, false);
                bg.rectTransform.anchorMin = bg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                bg.rectTransform.sizeDelta = new Vector2(W, H);
                var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = backgroundSprite.rect.width / Mathf.Max(1f, backgroundSprite.rect.height);
            }

            float headerH = 128f * u;
            BuildHeader(headerH, contentW);
            BuildScroll(headerH, W, contentW);

            // Popup layer lives on the root canvas so it covers the bottom nav too.
            if (popupLayer == null)
            {
                popupLayer = NewRect("[ShopPopupLayer]", canvasRect);
                Stretch(popupLayer);
            }

            pageRoot.gameObject.SetActive(false);
        }

        private void BuildHeader(float headerH, float contentW)
        {
            var header = NewRect("Header", pageRoot);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, headerH);
            header.anchoredPosition = Vector2.zero;
            var headerBg = header.gameObject.AddComponent<Image>();
            headerBg.color = new Color(0.03f, 0.08f, 0.28f, 0.55f);
            headerBg.raycastTarget = false;

            headerInner = NewRect("Inner", header);
            headerInner.anchorMin = headerInner.anchorMax = new Vector2(0.5f, 0.5f);
            headerInner.sizeDelta = new Vector2(contentW - 52f * u, headerH);

            var hl = headerInner.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.spacing = 10f * u;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;

            var shopIcon = NewIcon("ShopIcon", headerInner, shopIconSprite, new Vector2(84f, 72f) * u);
            AddLayout(shopIcon, 84f * u, 72f * u);
            IdleWiggle(shopIcon, 4.5f);

            var shopTitle = NewText("Title", headerInner, "SHOP", 52f * u, TitleWhite, TextAlignmentOptions.MidlineLeft);
            AddLayout(shopTitle.rectTransform, 150f * u, 70f * u);

            AddFlexibleSpacer(headerInner);

            var divider = NewImage("Divider", headerInner, dividerSprite, new Color(1f, 1f, 1f, 0.35f), false);
            AddLayout(divider.rectTransform, 4f * u, 64f * u);

            AddSpacer(headerInner, 6f * u);
            var coinPill = BuildCurrencyPill("Coins", coinSprite, out coinText, out coinIconRT, OpenAtCoinsFromHeader);
            AddLayout(coinPill, 262f * u, 72f * u);

            AddSpacer(headerInner, 4f * u);
            var gemPill = BuildCurrencyPill("Gems", gemSprite, out gemText, out gemIconRT, OpenAtGemsFromHeader);
            AddLayout(gemPill, 240f * u, 72f * u);
        }

        private RectTransform BuildCurrencyPill(string name, Sprite icon, out TextMeshProUGUI valueText,
                                                 out RectTransform iconRT, Action onPlus)
        {
            var pill = NewRect(name + "Pill", headerInner);

            iconRT = NewIcon(name + "Icon", pill, icon, new Vector2(62f, 62f) * u);
            iconRT.anchorMin = iconRT.anchorMax = new Vector2(0f, 0.5f);
            iconRT.anchoredPosition = new Vector2(33f * u, 0f);

            valueText = NewText(name + "Value", pill, "0", 40f * u, Color.white, TextAlignmentOptions.MidlineLeft);
            var vt = valueText.rectTransform;
            vt.anchorMin = new Vector2(0f, 0f);
            vt.anchorMax = new Vector2(1f, 1f);
            vt.offsetMin = new Vector2(70f * u, 0f);
            vt.offsetMax = new Vector2(-56f * u, 0f);
            valueText.enableAutoSizing = true;
            valueText.fontSizeMin = 22f * u;
            valueText.fontSizeMax = 40f * u;

            var plus = NewImage(name + "Plus", pill, plusButtonSprite, Color.white, true);
            var prt = plus.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(1f, 0.5f);
            prt.sizeDelta = new Vector2(50f, 50f) * u;
            prt.anchoredPosition = new Vector2(-26f * u, 0f);
            plus.raycastTarget = true;
            if (plusGlyphSprite != null)
            {
                var glyph = NewImage("Glyph", prt, plusGlyphSprite, Color.white, true);
                glyph.rectTransform.anchorMin = glyph.rectTransform.anchorMax = new Vector2(0.5f, 0.55f);
                glyph.rectTransform.sizeDelta = new Vector2(26f, 26f) * u;
            }
            else
            {
                var t = NewText("Glyph", prt, "+", 40f * u, Color.white, TextAlignmentOptions.Center);
                Stretch(t.rectTransform);
            }
            var btn = plus.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            plus.gameObject.AddComponent<ShopPressFeedback>();
            btn.onClick.AddListener(() => { Sfx(clickSound); onPlus?.Invoke(); });

            // Gentle attention pulse on the + button
            var pulse = DOTween.Sequence()
                .AppendInterval(2.2f + UnityEngine.Random.value)
                .Append(prt.DOPunchScale(Vector3.one * 0.18f, 0.5f, 6, 0.6f))
                .SetLoops(-1).SetUpdate(true);
            // Owned by the pill (not killed on close) – cheap and keeps the header alive.
            pulse.SetTarget(prt);
            return pill;
        }

        private void BuildScroll(float headerH, float W, float contentW)
        {
            scrollRectRT = NewRect("Scroll", pageRoot);
            Stretch(scrollRectRT);
            scrollRectRT.offsetMax = new Vector2(0f, -headerH);

            scrollRect = scrollRectRT.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.12f;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.135f;
            scrollRect.scrollSensitivity = 40f * u;

            var viewport = NewRect("Viewport", scrollRectRT);
            Stretch(viewport);
            var vpImg = viewport.gameObject.AddComponent<Image>();
            vpImg.color = new Color(0f, 0f, 0f, 0f);
            vpImg.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            scrollRect.viewport = viewport;

            content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            content.anchoredPosition = Vector2.zero;
            scrollRect.content = content;

            float side = (W - contentW) * 0.5f + 31f * u;
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(Mathf.RoundToInt(side), Mathf.RoundToInt(side),
                                        Mathf.RoundToInt(8f * u), Mathf.RoundToInt(240f * u));
            vl.spacing = 12f * u;
            vl.childAlignment = TextAnchor.UpperCenter;
            vl.childControlWidth = true;
            vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // ── Daily Offers ──
            RectTransform timerSlot;
            dailySection = BuildSectionHeader("Daily Offers", calendarSprite, new Vector2(92f, 92f), out timerSlot);
            timerText = NewText("Timer", timerSlot, "", 28f * u, new Color(0.78f, 0.88f, 1f, 1f), TextAlignmentOptions.MidlineRight);
            Stretch(timerText.rectTransform);
            var dailyRow = BuildRow(274f);
            BuildOfferCell(dailyRow, offerPanelSprite, 274f, MakeChestOffer("daily_silver", "Silver Chest", ChestType.Silver, silverChestPrice, silverChestSprite));
            BuildOfferCell(dailyRow, offerPanelSprite, 274f, MakeChestOffer("daily_gold", "Gold Chest", ChestType.Gold, goldChestPrice, goldChestSprite));
            BuildOfferCell(dailyRow, offerPanelSprite, 274f, new ShopOffer
            {
                offerId = "daily_coins", title = "Coins x" + dailyCoinsAmount.ToString("N0"), kind = ShopRewardKind.Coins,
                gemPrice = dailyCoinsPrice, amount = dailyCoinsAmount, limitedDaily = true, icon = coinStackSprite
            });

            // ── Cards ──
            RectTransform cardsSlot;
            cardsSection = BuildSectionHeader("Cards", cardsIconSprite, new Vector2(92f, 92f), out cardsSlot);
            var cardsRow = BuildRow(338f);
            var cardOffers = BuildCardOffersForCycle();
            foreach (var offer in cardOffers) BuildCardCell(cardsRow, offer);

            // ── Coins ──
            RectTransform coinsSlot;
            coinsSection = BuildSectionHeader("Coins", coinStackSprite, new Vector2(92f, 80f), out coinsSlot);
            var coinsRow = BuildRow(248f);
            for (int i = 0; i < 3 && i < coinPackAmounts.Length; i++)
            {
                BuildOfferCell(coinsRow, coinPanelSprite, 248f, new ShopOffer
                {
                    offerId = "coins_" + i, title = coinPackAmounts[i].ToString("N0") + " Coins", kind = ShopRewardKind.Coins,
                    gemPrice = SafeGet(coinPackPrices, i, 100), amount = coinPackAmounts[i],
                    icon = i == 2 && coinBagSprite != null ? coinBagSprite : coinStackSprite
                }, i == 0 ? 0.78f : 1f);
            }

            // ── Gems (collapsible) ──
            RectTransform gemsSlot;
            gemsSection = BuildSectionHeader("Gems", gemSprite, new Vector2(84f, 84f), out gemsSlot);
            var chev = NewImage("Chevron", gemsSlot, chevronSprite, Color.white, true);
            gemsChevron = chev.rectTransform;
            gemsChevron.anchorMin = gemsChevron.anchorMax = new Vector2(1f, 0.5f);
            gemsChevron.pivot = new Vector2(0.5f, 0.5f);
            gemsChevron.sizeDelta = new Vector2(40f, 30f) * u;
            gemsChevron.anchoredPosition = new Vector2(-24f * u, 0f);

            // Gems section is always open (no collapse toggle); the chevron is hidden.
            gemsChevron.gameObject.SetActive(false);

            var body = NewRect("GemsBody", content);
            body.gameObject.AddComponent<RectMask2D>();
            gemsBody = body.gameObject.AddComponent<LayoutElement>();
            gemsBodyHeight = 262f * u;
            gemsBody.minHeight = 0f;
            gemsBody.preferredHeight = gemsBodyHeight;

            gemsRow = NewRect("Row", body);
            gemsRow.anchorMin = new Vector2(0f, 1f);
            gemsRow.anchorMax = new Vector2(1f, 1f);
            gemsRow.pivot = new Vector2(0.5f, 1f);
            gemsRow.sizeDelta = new Vector2(0f, 248f * u);
            gemsRow.anchoredPosition = new Vector2(0f, -6f * u);
            ConfigureRowLayout(gemsRow);
            for (int i = 0; i < 3 && i < gemPackAmounts.Length; i++)
            {
                BuildOfferCell(gemsRow, coinPanelSprite, 248f, new ShopOffer
                {
                    offerId = "gems_" + i, title = gemPackAmounts[i].ToString("N0") + " Gems", kind = ShopRewardKind.GemPack,
                    amount = gemPackAmounts[i], realMoneyLabel = SafeGet(gemPackPriceLabels, i, "$0.99"), icon = gemSprite
                }, 0.62f + 0.19f * i);
            }
            gemsExpanded = true;
        }

        private SectionView BuildSectionHeader(string title, Sprite icon, Vector2 iconSize, out RectTransform rightSlot)
        {
            var row = NewRect(title.Replace(" ", "") + "Header", content);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 104f * u;
            le.minHeight = 104f * u;

            var inner = NewRect("Inner", row);
            Stretch(inner);
            var group = inner.gameObject.AddComponent<CanvasGroup>();
            var hl = inner.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.spacing = 16f * u;
            hl.padding = new RectOffset(Mathf.RoundToInt(4f * u), 0, Mathf.RoundToInt(8f * u), 0);
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;

            var iconRT = NewIcon("Icon", inner, icon, iconSize * u);
            AddLayout(iconRT, iconSize.x * u, iconSize.y * u);
            IdleWiggle(iconRT, 5f + sections.Count * 0.7f);

            var t = NewText("Title", inner, title, 58f * u, TitleWhite, TextAlignmentOptions.Center);
            AddLayout(t.rectTransform, 430f * u, 80f * u);

            AddFlexibleSpacer(inner);

            rightSlot = NewRect("Right", inner);
            AddLayout(rightSlot, 300f * u, 60f * u);

            var view = new SectionView { row = row, inner = inner, group = group, icon = iconRT };
            sections.Add(view);
            return view;
        }

        private RectTransform BuildRow(float refHeight)
        {
            var row = NewRect("Row", content);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = refHeight * u;
            le.minHeight = refHeight * u;
            ConfigureRowLayout(row);
            return row;
        }

        private void ConfigureRowLayout(RectTransform row)
        {
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.UpperCenter;
            hl.spacing = 13f * u;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
        }

        private OfferCellView CreateCellShell(RectTransform row, Sprite panelSprite, float refHeight, ShopOffer offer)
        {
            var cell = NewRect("Offer_" + offer.offerId, row);
            AddLayout(cell, 284f * u, refHeight * u);

            var visual = NewRect("Visual", cell);
            Stretch(visual);
            var group = visual.gameObject.AddComponent<CanvasGroup>();

            var panel = NewImage("Panel", visual, panelSprite, Color.white, false);
            Stretch(panel.rectTransform);

            var view = new OfferCellView { offer = offer, cell = cell, visual = visual, group = group, panel = panel };
            cells.Add(view);
            return view;
        }

        private void BuildOfferCell(RectTransform row, Sprite panelSprite, float refHeight, ShopOffer offer, float productScale = 1f)
        {
            var v = CreateCellShell(row, panelSprite, refHeight, offer);

            v.title = NewText("Title", v.visual, offer.title, 32f * u, TitleWhite, TextAlignmentOptions.Center);
            var tr = v.title.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.sizeDelta = new Vector2(-20f * u, 46f * u);
            tr.anchoredPosition = new Vector2(0f, -12f * u);
            v.title.enableAutoSizing = true;
            v.title.fontSizeMin = 18f * u;
            v.title.fontSizeMax = 32f * u;

            // Product art area between title and button
            float top = 58f * u;
            float bottom = 84f * u;
            float areaH = refHeight * u - top - bottom;
            v.productHolder = NewRect("ProductHolder", v.visual);
            v.productHolder.anchorMin = v.productHolder.anchorMax = new Vector2(0.5f, 1f);
            v.productHolder.pivot = new Vector2(0.5f, 0.5f);
            v.productHolder.sizeDelta = new Vector2(240f * u, areaH);
            v.productHolder.anchoredPosition = new Vector2(0f, -top - areaH * 0.5f);
            v.productRestPos = v.productHolder.anchoredPosition;

            if (offer.kind == ShopRewardKind.Chest && raysSprite != null)
            {
                var rays = NewImage("Rays", v.productHolder, raysSprite, new Color(1f, 0.93f, 0.6f, 0.28f), true);
                v.rays = rays.rectTransform;
                v.rays.anchorMin = v.rays.anchorMax = new Vector2(0.5f, 0.5f);
                v.rays.sizeDelta = new Vector2(areaH * 1.45f, areaH * 1.45f);
            }

            var productRT = NewIcon("Product", v.productHolder, offer.icon, new Vector2(230f * u, areaH * 1.05f) * productScale);
            productRT.anchorMin = productRT.anchorMax = new Vector2(0.5f, 0.5f);
            v.product = productRT.GetComponentInChildren<Image>();

            BuildBuyButton(v);
        }

        private void BuildCardCell(RectTransform row, ShopOffer offer)
        {
            var v = CreateCellShell(row, cardPanelSprite, 338f, offer);

            // Card art
            v.productHolder = NewRect("ProductHolder", v.visual);
            v.productHolder.anchorMin = v.productHolder.anchorMax = new Vector2(0.5f, 1f);
            v.productHolder.pivot = new Vector2(0.5f, 0.5f);
            v.productHolder.sizeDelta = new Vector2(200f * u, 196f * u);
            v.productHolder.anchoredPosition = new Vector2(0f, -12f * u - 98f * u);
            v.productRestPos = v.productHolder.anchoredPosition;
            var art = NewIcon("Product", v.productHolder, offer.icon, new Vector2(200f, 196f) * u);
            art.anchorMin = art.anchorMax = new Vector2(0.5f, 0.5f);
            v.product = art.GetComponentInChildren<Image>();

            // Copies badge (purple circle, top-left)
            v.badge = NewRect("CopiesBadge", v.visual);
            v.badge.anchorMin = v.badge.anchorMax = new Vector2(0f, 1f);
            v.badge.sizeDelta = new Vector2(64f, 64f) * u;
            v.badge.anchoredPosition = new Vector2(38f * u, -30f * u);
            var ring = NewImage("Ring", v.badge, circleSprite, Color.white, true);
            Stretch(ring.rectTransform);
            var fill = NewImage("Fill", v.badge, circleSprite, BadgePurple, true);
            Stretch(fill.rectTransform, 4f * u, 4f * u, 4f * u, 4f * u);
            v.badgeText = NewText("Count", v.badge, offer.amount.ToString(), 38f * u, Color.white, TextAlignmentOptions.Center);
            Stretch(v.badgeText.rectTransform);

            // Name
            v.title = NewText("Name", v.visual, offer.title, 32f * u, TitleWhite, TextAlignmentOptions.Center);
            var tr = v.title.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.sizeDelta = new Vector2(-16f * u, 44f * u);
            tr.anchoredPosition = new Vector2(0f, -208f * u);
            v.title.enableAutoSizing = true;
            v.title.fontSizeMin = 18f * u;
            v.title.fontSizeMax = 32f * u;

            BuildBuyButton(v);
        }

        private void BuildBuyButton(OfferCellView v)
        {
            v.buttonImage = NewImage("BuyButton", v.visual, buyButtonSprite, Color.white, false);
            var br = v.buttonImage.rectTransform;
            br.anchorMin = br.anchorMax = new Vector2(0.5f, 0f);
            br.pivot = new Vector2(0.5f, 0f);
            br.sizeDelta = new Vector2(236f * u, 66f * u);
            br.anchoredPosition = new Vector2(0f, 14f * u);
            v.buttonImage.raycastTarget = true;

            v.button = v.buttonImage.gameObject.AddComponent<Button>();
            v.button.transition = Selectable.Transition.None;
            v.buttonFeedback = v.buttonImage.gameObject.AddComponent<ShopPressFeedback>();
            var captured = v;
            v.button.onClick.AddListener(() => OnBuyPressed(captured));

            var inner = NewRect("Content", br);
            Stretch(inner, 0f, 6f * u, 0f, 0f);
            var hl = inner.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.spacing = 6f * u;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;

            var gem = NewIcon("Gem", inner, gemSprite, new Vector2(44f, 44f) * u);
            AddLayout(gem, 44f * u, 44f * u);
            v.priceGem = gem.GetComponentInChildren<Image>();

            v.priceText = NewText("Price", inner, "", 38f * u, Color.white, TextAlignmentOptions.Center);
            v.priceText.enableAutoSizing = false;
            v.priceText.rectTransform.anchorMin = v.priceText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            v.priceText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            v.priceLayout = v.priceText.gameObject.AddComponent<LayoutElement>();

            // Also make the whole card tappable (forwards to the buy button)
            var cellBtn = v.panel.gameObject.AddComponent<Button>();
            cellBtn.transition = Selectable.Transition.None;
            v.panel.raycastTarget = true;
            cellBtn.onClick.AddListener(() => { v.buttonFeedback.Punch(0.12f); OnBuyPressed(captured); });

            RefreshCellState(v);
        }

        // ═════════════════════════════ Offers data ═════════════════════════════

        private ShopOffer MakeChestOffer(string id, string title, ChestType type, int price, Sprite icon)
        {
            var cfg = ResolveChestConfig(type);
            Sprite closed = icon != null ? icon : cfg?.closedSprite;
            return new ShopOffer
            {
                offerId = id, title = title, kind = ShopRewardKind.Chest, chestType = type,
                gemPrice = price, limitedDaily = true, icon = closed
            };
        }

        private List<ShopOffer> BuildCardOffersForCycle()
        {
            var list = new List<ShopOffer>();
            var lib = ResolveLibrary();
            var ids = new List<string>();

            if (cycleIndex == 0 && firstCycleCardIds != null)
            {
                foreach (var id in firstCycleCardIds)
                    if (lib == null || lib.GetCard(id) != null) ids.Add(id);
            }

            if (ids.Count < 3 && lib != null)
            {
                var pool = lib.GetCardsByType(CardType.Army).Select(c => c.cardId).Where(c => !ids.Contains(c)).ToList();
                var rng = new System.Random(cycleIndex * 7919 + 17);
                for (int i = pool.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    var tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
                }
                foreach (var id in pool)
                {
                    if (ids.Count >= 3) break;
                    ids.Add(id);
                }
            }

            while (ids.Count < 3) ids.Add(firstCycleCardIds != null && firstCycleCardIds.Length > ids.Count ? firstCycleCardIds[ids.Count] : "swordsman");

            for (int i = 0; i < 3; i++)
            {
                var def = lib != null ? lib.GetCard(ids[i]) : null;
                list.Add(new ShopOffer
                {
                    offerId = "card_" + i,
                    title = def != null ? def.displayName : PrettyName(ids[i]),
                    kind = ShopRewardKind.CardCopies,
                    cardId = ids[i],
                    amount = SafeGet(cardOfferCopies, i, 3),
                    gemPrice = SafeGet(cardOfferPrices, i, 500),
                    limitedDaily = true,
                    icon = def != null ? def.cardSprite : null
                });
            }
            return list;
        }

        // ═════════════════════════════ Cell state ═════════════════════════════

        private bool IsSoldOut(ShopOffer o) => o.limitedDaily && purchased.Contains(o.offerId);

        private void RefreshAllCellStates()
        {
            foreach (var c in cells) RefreshCellState(c);
        }

        private void RefreshCellState(OfferCellView v)
        {
            if (v == null || v.priceText == null) return;
            bool sold = IsSoldOut(v.offer);

            if (sold)
            {
                v.buttonImage.color = SoldGrey;
                v.priceGem.gameObject.SetActive(false);
                v.priceText.text = "PURCHASED";
                v.priceText.fontSize = 26f * u;
                v.priceLayout.ignoreLayout = true;
                Stretch(v.priceText.rectTransform);
                var soldGemRoot = v.priceText.rectTransform.parent.Find("Gem");
                if (soldGemRoot != null) soldGemRoot.gameObject.SetActive(false);
                v.priceText.color = Color.white;
                if (v.product != null) v.product.color = new Color(0.62f, 0.62f, 0.7f, 1f);
                return;
            }

            v.buttonImage.color = Color.white;
            if (v.product != null) v.product.color = Color.white;
            v.priceText.fontSize = 38f * u;
            v.priceLayout.ignoreLayout = false;
            var activeGemRoot = v.priceText.rectTransform.parent.Find("Gem");
            if (activeGemRoot != null) activeGemRoot.gameObject.SetActive(v.offer.kind != ShopRewardKind.GemPack);

            if (v.offer.kind == ShopRewardKind.GemPack)
            {
                v.priceGem.gameObject.SetActive(false);
                v.priceText.text = v.offer.realMoneyLabel;
                v.priceText.color = Color.white;
            }
            else
            {
                v.priceGem.gameObject.SetActive(true);
                v.priceText.text = v.offer.gemPrice.ToString("N0");
                v.priceText.color = PlayerWallet.CanAffordGems(v.offer.gemPrice) ? Color.white : PriceRed;
            }
        }

        private void ApplyOfferToCell(OfferCellView v, ShopOffer offer)
        {
            v.offer = offer;
            if (v.title != null) v.title.text = offer.title;
            if (v.badgeText != null) v.badgeText.text = offer.amount.ToString();
            if (v.product != null && offer.icon != null) v.product.sprite = offer.icon;
            RefreshCellState(v);
        }

        // ═════════════════════════════ Animations ═════════════════════════════

        private void PlayEntrance()
        {
            KillIdle();
            pageGroup.alpha = 0f;
            pageGroup.DOFade(1f, 0.22f).SetEase(Ease.OutQuad).SetUpdate(true);

            headerInner.DOKill();
            headerInner.anchoredPosition = new Vector2(0f, 150f * u);
            headerInner.DOAnchorPos(Vector2.zero, 0.5f).SetEase(Ease.OutBack, 1.3f).SetUpdate(true);

            for (int i = 0; i < sections.Count; i++)
            {
                var s = sections[i];
                s.inner.DOKill();
                s.group.DOKill();
                s.inner.anchoredPosition = new Vector2(-90f * u, 0f);
                s.group.alpha = 0f;
                float d = 0.08f + i * 0.12f;
                s.inner.DOAnchorPos(Vector2.zero, 0.45f).SetDelay(d).SetEase(Ease.OutCubic).SetUpdate(true);
                s.group.DOFade(1f, 0.3f).SetDelay(d).SetUpdate(true);
            }

            int idx = 0;
            foreach (var c in cells)
            {
                if (c.offer.kind == ShopRewardKind.GemPack && !gemsExpanded) { ResetCellVisual(c); continue; }
                AnimateCellIn(c, 0.16f + idx * 0.05f);
                idx++;
            }

            float startIdleAt = 0.16f + idx * 0.05f + 0.4f;
            var starter = DOVirtual.DelayedCall(startIdleAt, StartIdle).SetUpdate(true);
            idleTweens.Add(starter);
        }

        private void AnimateCellIn(OfferCellView c, float delay)
        {
            c.visual.DOKill();
            c.group.DOKill();
            c.visual.localScale = Vector3.one * 0.6f;
            c.visual.anchoredPosition = new Vector2(0f, -60f * u);
            c.group.alpha = 0f;
            c.visual.DOScale(1f, 0.5f).SetDelay(delay).SetEase(Ease.OutBack, 1.7f).SetUpdate(true);
            c.visual.DOAnchorPos(Vector2.zero, 0.42f).SetDelay(delay).SetEase(Ease.OutCubic).SetUpdate(true);
            c.group.DOFade(1f, 0.25f).SetDelay(delay).SetUpdate(true);
        }

        private void ResetCellVisual(OfferCellView c)
        {
            c.visual.DOKill();
            c.group.DOKill();
            c.visual.localScale = Vector3.one;
            c.visual.anchoredPosition = Vector2.zero;
            c.group.alpha = 1f;
        }

        private void StartIdle()
        {
            if (!isOpen) return;
            int i = 0;
            foreach (var c in cells)
            {
                bool daily = c.offer.offerId.StartsWith("daily_");
                if (daily && c.productHolder != null)
                {
                    c.productHolder.DOKill();
                    c.productHolder.anchoredPosition = c.productRestPos;
                    var t = c.productHolder.DOAnchorPosY(c.productRestPos.y + 9f * u, 1.25f)
                        .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetDelay(i * 0.3f).SetUpdate(true);
                    idleTweens.Add(t);

                    var r = c.productHolder.DOScale(1.035f, 1.25f)
                        .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetDelay(i * 0.3f + 0.6f).SetUpdate(true);
                    idleTweens.Add(r);
                    i++;
                }
                if (c.rays != null)
                {
                    c.rays.DOKill();
                    var spin = c.rays.DOLocalRotate(new Vector3(0f, 0f, -360f), 9f, RotateMode.FastBeyond360)
                        .SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart).SetUpdate(true);
                    idleTweens.Add(spin);
                }
            }
        }

        private void IdleWiggle(RectTransform target, float interval)
        {
            if (target == null) return;
            var seq = DOTween.Sequence()
                .AppendInterval(interval)
                .Append(target.DOPunchRotation(new Vector3(0f, 0f, 10f), 0.6f, 8, 0.6f))
                .SetLoops(-1).SetUpdate(true);
            seq.SetTarget(target);
        }

        private void KillIdle()
        {
            foreach (var t in idleTweens) if (t != null && t.IsActive()) t.Kill();
            idleTweens.Clear();
            foreach (var c in cells)
            {
                if (c.productHolder != null)
                {
                    c.productHolder.DOKill();
                    c.productHolder.anchoredPosition = c.productRestPos;
                    c.productHolder.localScale = Vector3.one;
                }
            }
        }

        private void SetGemsExpanded(bool expand, bool animate)
        {
            gemsExpanded = expand;
            float target = expand ? gemsBodyHeight : 0f;
            DOTween.Kill(gemsBody);
            gemsChevron.DOKill();

            if (!animate)
            {
                gemsBody.preferredHeight = target;
                gemsChevron.localEulerAngles = new Vector3(0f, 0f, expand ? 180f : 0f);
                return;
            }

            DOTween.To(() => gemsBody.preferredHeight, h => gemsBody.preferredHeight = h, target, expand ? 0.42f : 0.3f)
                .SetEase(expand ? Ease.OutBack : Ease.InOutCubic, 1.1f)
                .SetTarget(gemsBody).SetUpdate(true)
                .OnUpdate(() => LayoutRebuilder.MarkLayoutForRebuild(content))
                .OnComplete(() =>
                {
                    if (expand) ScrollToBottom();
                });
            gemsChevron.DOLocalRotate(new Vector3(0f, 0f, expand ? 180f : 0f), 0.35f).SetEase(Ease.OutBack).SetUpdate(true);
            gemsSection.icon.DOPunchScale(Vector3.one * 0.2f, 0.4f, 6, 0.6f).SetUpdate(true);

            if (expand)
            {
                int i = 0;
                foreach (var c in cells.Where(x => x.offer.kind == ShopRewardKind.GemPack))
                    AnimateCellIn(c, 0.06f + 0.07f * i++);
            }
        }

        private void ScrollToBottom()
        {
            DOTween.Kill(scrollRect);
            DOTween.To(() => scrollRect.verticalNormalizedPosition, p => scrollRect.verticalNormalizedPosition = p, 0f, 0.45f)
                .SetEase(Ease.OutCubic).SetTarget(scrollRect).SetUpdate(true);
        }

        private IEnumerator FocusSectionRoutine(SectionView section, float delay)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            if (section == gemsSection && !gemsExpanded)
            {
                SetGemsExpanded(true, true);
                yield break;   // expanding scrolls to the bottom on its own
            }
            Canvas.ForceUpdateCanvases();
            float contentH = content.rect.height;
            float viewH = scrollRect.viewport.rect.height;
            if (contentH <= viewH) yield break;
            float y = -section.row.anchoredPosition.y - section.row.rect.height * 0.5f; // distance from top
            float normalized = 1f - Mathf.Clamp01(y / (contentH - viewH));
            DOTween.Kill(scrollRect);
            DOTween.To(() => scrollRect.verticalNormalizedPosition, p => scrollRect.verticalNormalizedPosition = p, normalized, 0.45f)
                .SetEase(Ease.OutCubic).SetTarget(scrollRect).SetUpdate(true);
            section.icon.DOPunchScale(Vector3.one * 0.25f, 0.45f, 6, 0.6f).SetUpdate(true);
        }

        // ═════════════════════════════ Purchasing ═════════════════════════════

        private void OnBuyPressed(OfferCellView v)
        {
            if (isBusy || v == null) return;
            var offer = v.offer;

            if (IsSoldOut(offer))
            {
                v.buttonFeedback.Deny();
                Sfx(errorSound);
                ShowToast("Sold out! New offers in " + FormatRemaining(), new Color(0.25f, 0.3f, 0.45f, 0.95f));
                return;
            }

            Sfx(clickSound);

            if (offer.kind == ShopRewardKind.GemPack)
            {
                ShowConfirm(v, "Get " + offer.amount.ToString("N0") + " Gems?", offer.realMoneyLabel, false, () =>
                {
                    if (grantGemPacksWithoutPayment) GrantGemPack(v);
                    else ShowToast("Store not connected yet", new Color(0.45f, 0.2f, 0.25f, 0.95f));
                });
                return;
            }

            if (!PlayerWallet.CanAffordGems(offer.gemPrice))
            {
                NotEnoughGems(v);
                return;
            }

            string question;
            switch (offer.kind)
            {
                case ShopRewardKind.Chest: question = "Buy " + offer.title + "?"; break;
                case ShopRewardKind.CardCopies: question = "Buy " + offer.amount + "x " + offer.title + "?"; break;
                default: question = "Buy " + offer.amount.ToString("N0") + " Coins?"; break;
            }

            ShowConfirm(v, question, offer.gemPrice.ToString("N0"), true, () => ExecutePurchase(v));
        }

        private void NotEnoughGems(OfferCellView v)
        {
            v.buttonFeedback.Deny();
            Sfx(errorSound);
            if (gemText != null)
            {
                gemText.DOKill();
                gemText.color = PriceRed;
                gemText.DOColor(Color.white, 0.6f).SetDelay(0.25f).SetUpdate(true);
                gemText.rectTransform.DOKill();
                gemText.rectTransform.DOShakeAnchorPos(0.45f, 10f * u, 18, 90f, false, true).SetUpdate(true);
            }
            if (gemIconRT != null) gemIconRT.DOPunchScale(Vector3.one * 0.35f, 0.45f, 8, 0.5f).SetUpdate(true);
            ShowToast("Not enough gems!", new Color(0.55f, 0.12f, 0.2f, 0.95f));
            StartCoroutine(FocusSectionRoutine(gemsSection, 0.55f));
        }

        private void ExecutePurchase(OfferCellView v)
        {
            var offer = v.offer;
            if (!PlayerWallet.TrySpendGems(offer.gemPrice))
            {
                NotEnoughGems(v);
                return;
            }

            if (offer.limitedDaily)
            {
                purchased.Add(offer.offerId);
                SaveDailyState();
            }

            Sfx(purchaseSound);
            v.buttonFeedback.Punch();
            v.visual.DOPunchScale(Vector3.one * 0.08f, 0.4f, 6, 0.6f).SetUpdate(true);
            SpawnSparkles(v.visual, Vector2.zero, 10, null);

            switch (offer.kind)
            {
                case ShopRewardKind.Coins:
                    // Commit now, animate the counter once the coins land.
                    coinDisplayHold++;
                    PlayerWallet.AddCoins(offer.amount);
                    FlyIcons(coinSprite, v.productHolder != null ? v.productHolder : v.visual, coinIconRT, 12, 64f * u,
                        () => ReleaseCoinHold());
                    break;

                case ShopRewardKind.CardCopies:
                    GrantCardCopies(offer.cardId, offer.amount);
                    ShowCardReward(offer);
                    break;

                case ShopRewardKind.Chest:
                    var reward = RollChest(offer.chestType);
                    // Commit everything immediately so nothing is lost if the app closes mid-animation.
                    coinDisplayHold++;
                    PlayerWallet.AddCoins(reward.coins);
                    if (reward.army != null) GrantCardCopies(reward.army.cardId, reward.armyCopies);
                    if (reward.power != null) GrantCardCopies(reward.power.cardId, reward.powerCopies);
                    ShowChestReward(offer, reward);
                    break;
            }

            RefreshAllCellStates();
        }

        private void GrantGemPack(OfferCellView v)
        {
            gemDisplayHold++;
            PlayerWallet.AddGems(v.offer.amount);
            Sfx(purchaseSound);
            v.buttonFeedback.Punch();
            SpawnSparkles(v.visual, Vector2.zero, 12, new Color(1f, 0.6f, 1f, 1f));
            FlyIcons(gemSprite, v.productHolder != null ? v.productHolder : v.visual, gemIconRT, 12, 58f * u,
                () => ReleaseGemHold());
            RefreshAllCellStates();
        }

        private void GrantCardCopies(string cardId, int copies)
        {
            var inv = ResolveInventory();
            if (inv == null)
            {
                Debug.LogWarning("[Shop] CardInventoryManager not found – card copies could not be granted.");
                return;
            }
            inv.AddCardCopies(cardId, copies);
            Debug.Log($"<color=#2ECC71>[Shop] +{copies}x '{cardId}' added to collection.</color>");
        }

        private class ChestRoll
        {
            public ChestType type;
            public int coins;
            public CardDefinition army;
            public int armyCopies;
            public CardDefinition power;
            public int powerCopies;
        }

        private ChestRoll RollChest(ChestType type)
        {
            var cfg = ResolveChestConfig(type);
            var roll = new ChestRoll
            {
                type = type,
                coins = UnityEngine.Random.Range(cfg.minCoins, cfg.maxCoins + 1),
                armyCopies = Mathf.Max(1, cfg.armyCopies),
                powerCopies = Mathf.Max(1, cfg.powerCopies)
            };
            var lib = ResolveLibrary();
            if (lib != null)
            {
                var army = lib.GetCardsByType(CardType.Army);
                var power = lib.GetCardsByType(CardType.Power);
                if (army.Count > 0) roll.army = army[UnityEngine.Random.Range(0, army.Count)];
                if (power.Count > 0) roll.power = power[UnityEngine.Random.Range(0, power.Count)];
            }
            return roll;
        }

        // ═════════════════════════════ Popups ═════════════════════════════

        private RectTransform CreatePopupRoot(string name, out Image dim, float dimAlpha = 0.72f)
        {
            popupLayer.SetAsLastSibling();
            var root = NewRect(name, popupLayer);
            Stretch(root);
            dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0.01f, 0.02f, 0.08f, 0f);
            dim.raycastTarget = true;
            dim.DOFade(dimAlpha, 0.22f).SetUpdate(true);
            return root;
        }

        private void ClosePopup(RectTransform root, Image dim, RectTransform panel, Action after = null)
        {
            if (root == null) return;
            var buttons = root.GetComponentsInChildren<Button>();
            foreach (var b in buttons) b.interactable = false;
            if (panel != null)
            {
                panel.DOKill();
                panel.DOScale(0.6f, 0.2f).SetEase(Ease.InBack).SetUpdate(true);
                var cg = panel.GetComponent<CanvasGroup>();
                if (cg == null) cg = panel.gameObject.AddComponent<CanvasGroup>();
                cg.DOFade(0f, 0.2f).SetUpdate(true);
            }
            dim.DOKill();
            dim.DOFade(0f, 0.22f).SetUpdate(true).OnComplete(() =>
            {
                KillTree(root);
                Destroy(root.gameObject);
                after?.Invoke();
            });
        }

        private void ShowConfirm(OfferCellView v, string question, string priceLabel, bool gemPrice, Action onConfirm)
        {
            isBusy = true;
            Image dim;
            var root = CreatePopupRoot("ConfirmPopup", out dim);

            var panel = NewRect("Panel", root);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(740f, 560f) * u;
            var panelImg = NewImage("Bg", panel, popupPanelSprite != null ? popupPanelSprite : offerPanelSprite, Color.white, false);
            Stretch(panelImg.rectTransform);
            panelImg.raycastTarget = true;

            var title = NewText("Title", panel, question, 42f * u, TitleWhite, TextAlignmentOptions.Center);
            var tr = title.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.sizeDelta = new Vector2(-120f * u, 110f * u);
            tr.anchoredPosition = new Vector2(0f, -26f * u);
            title.enableAutoSizing = true;
            title.fontSizeMin = 24f * u;
            title.fontSizeMax = 42f * u;
            title.textWrappingMode = TextWrappingModes.Normal;

            // Item preview with rotating rays
            var holder = NewRect("Item", panel);
            holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0.5f);
            holder.sizeDelta = new Vector2(300f, 210f) * u;
            holder.anchoredPosition = new Vector2(0f, 10f * u);
            if (raysSprite != null)
            {
                var rays = NewImage("Rays", holder, raysSprite, new Color(1f, 0.95f, 0.7f, 0.35f), true);
                rays.rectTransform.sizeDelta = new Vector2(380f, 380f) * u;
                rays.rectTransform.DOLocalRotate(new Vector3(0, 0, -360f), 8f, RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear).SetLoops(-1).SetUpdate(true);
            }
            var icon = NewIcon("Icon", holder, v.offer.icon, new Vector2(300f, 210f) * u);
            icon.DOAnchorPosY(10f * u, 1.1f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);

            if (v.offer.kind == ShopRewardKind.CardCopies)
            {
                var amt = NewText("Amount", holder, "x" + v.offer.amount, 52f * u, Color.white, TextAlignmentOptions.Center);
                amt.rectTransform.anchorMin = amt.rectTransform.anchorMax = new Vector2(1f, 0f);
                amt.rectTransform.sizeDelta = new Vector2(120f, 70f) * u;
                amt.rectTransform.anchoredPosition = new Vector2(-10f * u, 20f * u);
            }

            // Buy button
            var buy = NewImage("Buy", panel, buyButtonSprite, Color.white, false);
            var br = buy.rectTransform;
            br.anchorMin = br.anchorMax = new Vector2(0.5f, 0f);
            br.pivot = new Vector2(0.5f, 0f);
            br.sizeDelta = new Vector2(330f, 92f) * u;
            br.anchoredPosition = new Vector2(0f, 40f * u);
            buy.raycastTarget = true;
            var buyContent = NewRect("Content", br);
            Stretch(buyContent, 0f, 8f * u, 0f, 0f);
            var hl = buyContent.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.spacing = 8f * u;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            if (gemPrice)
            {
                var g = NewIcon("Gem", buyContent, gemSprite, new Vector2(58f, 58f) * u);
                AddLayout(g, 58f * u, 58f * u);
            }
            NewText("Price", buyContent, priceLabel, 50f * u, Color.white, TextAlignmentOptions.Center);
            var buyBtn = buy.gameObject.AddComponent<Button>();
            buyBtn.transition = Selectable.Transition.None;
            buy.gameObject.AddComponent<ShopPressFeedback>();
            br.DOScale(1.05f, 0.7f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetDelay(0.45f).SetUpdate(true);

            // Close (X)
            var close = NewImage("Close", panel, closeSprite, Color.white, true);
            var cr = close.rectTransform;
            cr.anchorMin = cr.anchorMax = new Vector2(1f, 1f);
            cr.sizeDelta = new Vector2(76f, 76f) * u;
            cr.anchoredPosition = new Vector2(-20f * u, -20f * u);
            close.raycastTarget = true;
            var closeBtn = close.gameObject.AddComponent<Button>();
            closeBtn.transition = Selectable.Transition.None;
            close.gameObject.AddComponent<ShopPressFeedback>();

            bool done = false;
            Action cancel = () =>
            {
                if (done) return;
                done = true;
                Sfx(clickSound);
                ClosePopup(root, dim, panel, () => isBusy = false);
            };
            closeBtn.onClick.AddListener(() => cancel());
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(() => cancel());

            buyBtn.onClick.AddListener(() =>
            {
                if (done) return;
                done = true;
                ClosePopup(root, dim, panel, () =>
                {
                    isBusy = false;
                    onConfirm?.Invoke();
                });
            });

            // Entrance
            panel.localScale = Vector3.one * 0.5f;
            panel.DOScale(1f, 0.4f).SetEase(Ease.OutBack, 1.6f).SetUpdate(true);
            holder.localScale = Vector3.zero;
            holder.DOScale(1f, 0.45f).SetDelay(0.12f).SetEase(Ease.OutBack, 2f).SetUpdate(true);
        }

        private void ShowCardReward(ShopOffer offer)
        {
            isBusy = true;
            Image dim;
            var root = CreatePopupRoot("CardReward", out dim, 0.8f);
            var stage = NewRect("Stage", root);
            stage.anchorMin = stage.anchorMax = new Vector2(0.5f, 0.5f);
            stage.sizeDelta = new Vector2(700f, 900f) * u;

            AddGlowAndRays(stage, new Vector2(0f, 60f * u), 620f * u);

            var title = NewText("Title", stage, "NEW CARDS!", 64f * u, new Color(1f, 0.86f, 0.3f, 1f), TextAlignmentOptions.Center);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.sizeDelta = new Vector2(700f, 100f) * u;
            title.rectTransform.anchoredPosition = new Vector2(0f, -40f * u);

            var card = NewIcon("Card", stage, offer.icon, new Vector2(330f, 440f) * u);
            card.anchoredPosition = new Vector2(0f, 60f * u);

            var count = NewText("Count", stage, "x" + offer.amount, 72f * u, Color.white, TextAlignmentOptions.Center);
            count.rectTransform.sizeDelta = new Vector2(400f, 100f) * u;
            count.rectTransform.anchoredPosition = new Vector2(0f, -220f * u);
            var nameText = NewText("Name", stage, offer.title, 44f * u, TitleWhite, TextAlignmentOptions.Center);
            nameText.rectTransform.sizeDelta = new Vector2(600f, 70f) * u;
            nameText.rectTransform.anchoredPosition = new Vector2(0f, -290f * u);

            var collect = BuildCollectButton(stage);

            // Sequence: title drop, card flip-in, count pop, collect
            title.rectTransform.localScale = Vector3.zero;
            card.localScale = new Vector3(0f, 1.2f, 1f);
            count.rectTransform.localScale = Vector3.zero;
            nameText.alpha = 0f;
            collect.localScale = Vector3.zero;

            var seq = DOTween.Sequence().SetUpdate(true).SetTarget(root);
            seq.Append(title.rectTransform.DOScale(1f, 0.35f).SetEase(Ease.OutBack, 2f));
            seq.Append(card.DOScale(1f, 0.45f).SetEase(Ease.OutBack, 1.8f));
            seq.AppendCallback(() =>
            {
                Sfx(chestOpenSound != null ? chestOpenSound : purchaseSound);
                SpawnSparkles(stage, card.anchoredPosition, 22, null);
            });
            seq.Append(count.rectTransform.DOScale(1f, 0.3f).SetEase(Ease.OutBack, 3f));
            seq.Join(DOTween.To(() => nameText.alpha, a => nameText.alpha = a, 1f, 0.3f));
            seq.Append(collect.DOScale(1f, 0.3f).SetEase(Ease.OutBack, 2f));
            seq.AppendCallback(() => collect.DOScale(1.06f, 0.6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true));
            card.DOAnchorPosY(card.anchoredPosition.y + 12f * u, 1.2f).SetDelay(0.8f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);

            var btn = collect.GetComponent<Button>();
            bool done = false;
            btn.onClick.AddListener(() =>
            {
                if (done) return;
                done = true;
                Sfx(clickSound);
                // Card flies toward the Cards tab of the bottom nav
                var target = GetNavTab(cardsTabIndex);
                if (target != null) FlyIcons(offer.icon, card, target, 1, 160f * u, null);
                card.gameObject.SetActive(false);
                ClosePopup(root, dim, stage, () => isBusy = false);
            });
        }

        private void ShowChestReward(ShopOffer offer, ChestRoll roll)
        {
            isBusy = true;
            Image dim;
            var root = CreatePopupRoot("ChestReward", out dim, 0.82f);
            var stage = NewRect("Stage", root);
            stage.anchorMin = stage.anchorMax = new Vector2(0.5f, 0.5f);
            stage.sizeDelta = new Vector2(860f, 1100f) * u;

            var glowGroup = AddGlowAndRays(stage, new Vector2(0f, 150f * u), 700f * u);
            glowGroup.alpha = 0f;

            var title = NewText("Title", stage, offer.title.ToUpperInvariant(), 64f * u, new Color(1f, 0.86f, 0.3f, 1f), TextAlignmentOptions.Center);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.sizeDelta = new Vector2(820f, 100f) * u;
            title.rectTransform.anchoredPosition = new Vector2(0f, -40f * u);

            var chest = NewIcon("Chest", stage, offer.icon, new Vector2(460f, 330f) * u);
            chest.anchoredPosition = new Vector2(0f, 150f * u);
            var chestImg = chest.GetComponentInChildren<Image>();
            Sprite openSprite = ResolveChestOpenSprite(offer.chestType);

            var tapHint = NewText("Tap", stage, "Tap to open!", 40f * u, Color.white, TextAlignmentOptions.Center);
            tapHint.rectTransform.sizeDelta = new Vector2(600f, 70f) * u;
            tapHint.rectTransform.anchoredPosition = new Vector2(0f, -60f * u);
            tapHint.alpha = 0f;

            // Rewards row
            var items = new List<RectTransform>();
            var itemSprites = new List<Sprite>();
            var row = NewRect("Rewards", stage);
            row.sizeDelta = new Vector2(820f, 300f) * u;
            row.anchoredPosition = new Vector2(0f, -170f * u);
            items.Add(BuildRewardItem(row, coinStackSprite != null ? coinStackSprite : coinSprite, "+" + roll.coins.ToString("N0"), "Coins", new Vector2(-270f * u, 0f), false));
            itemSprites.Add(coinSprite);
            if (roll.army != null)
            {
                items.Add(BuildRewardItem(row, roll.army.cardSprite, "x" + roll.armyCopies, roll.army.displayName, new Vector2(0f, 0f), true));
                itemSprites.Add(roll.army.cardSprite);
            }
            if (roll.power != null)
            {
                items.Add(BuildRewardItem(row, roll.power.cardSprite, "x" + roll.powerCopies, roll.power.displayName, new Vector2(270f * u, 0f), true));
                itemSprites.Add(roll.power.cardSprite);
            }
            foreach (var it in items) it.localScale = Vector3.zero;

            var collect = BuildCollectButton(stage);
            collect.localScale = Vector3.zero;

            // Entrance: chest drops in, then wobbles until tapped (or auto-opens)
            title.rectTransform.localScale = Vector3.zero;
            chest.localScale = Vector3.zero;
            var intro = DOTween.Sequence().SetUpdate(true).SetTarget(root);
            intro.Append(title.rectTransform.DOScale(1f, 0.35f).SetEase(Ease.OutBack, 2f));
            intro.Append(chest.DOScale(1f, 0.5f).SetEase(Ease.OutBounce));
            intro.Join(DOTween.To(() => tapHint.alpha, a => tapHint.alpha = a, 1f, 0.3f).SetDelay(0.3f));

            var wobble = DOTween.Sequence().SetUpdate(true).SetTarget(root)
                .PrependInterval(0.9f)
                .Append(chest.DOPunchRotation(new Vector3(0, 0, 8f), 0.5f, 10, 0.5f))
                .Join(chest.DOPunchScale(new Vector3(0.06f, -0.06f, 0f), 0.5f, 8, 0.5f))
                .AppendInterval(0.6f)
                .SetLoops(-1);

            bool opened = false;
            bool collected = false;

            Action open = () =>
            {
                if (opened) return;
                opened = true;
                wobble.Kill();
                chest.localRotation = Quaternion.identity;
                DOTween.To(() => tapHint.alpha, a => tapHint.alpha = a, 0f, 0.15f).SetUpdate(true);

                var seq = DOTween.Sequence().SetUpdate(true).SetTarget(root);
                // three escalating shakes
                for (int i = 0; i < 3; i++)
                {
                    float s = 0.08f + i * 0.05f;
                    seq.Append(chest.DOPunchScale(new Vector3(s, -s, 0f), 0.22f, 10, 0.4f));
                    seq.Join(chest.DOShakeAnchorPos(0.22f, (6f + i * 6f) * u, 30, 90f, false, false));
                    seq.AppendInterval(0.05f);
                }
                seq.AppendCallback(() =>
                {
                    Sfx(chestOpenSound != null ? chestOpenSound : purchaseSound);
                    PlayChestSystemSound(c => c.PlayOpenChestSound());
                    if (openSprite != null && chestImg != null)
                    {
                        chestImg.sprite = openSprite;
                        chestImg.preserveAspect = true;
                    }
                    SpawnFlash(root);
                    SpawnSparkles(stage, chest.anchoredPosition, 28, null);
                    chest.localScale = Vector3.one;
                    chest.DOPunchScale(Vector3.one * 0.3f, 0.5f, 6, 0.6f).SetUpdate(true);
                    glowGroup.DOFade(1f, 0.3f).SetUpdate(true);
                });
                seq.AppendInterval(0.35f);
                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i];
                    seq.Append(it.DOScale(1f, 0.38f).SetEase(Ease.OutBack, 2.4f));
                    seq.Join(it.DOPunchRotation(new Vector3(0, 0, 12f), 0.38f, 8, 0.6f));
                    int idx = i;
                    seq.AppendCallback(() =>
                    {
                        PlayChestSystemSound(c => c.PlayCardFlipSound());
                        SpawnSparkles(row, items[idx].anchoredPosition, 8, null);
                    });
                    seq.AppendInterval(0.08f);
                }
                seq.Append(collect.DOScale(1f, 0.3f).SetEase(Ease.OutBack, 2f));
                seq.AppendCallback(() => collect.DOScale(1.06f, 0.6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true));
            };

            // Tap anywhere to open
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(() => open());
            var chestBtn = chestImg.gameObject.AddComponent<Button>();
            chestBtn.transition = Selectable.Transition.None;
            chestImg.raycastTarget = true;
            chestBtn.onClick.AddListener(() => open());
            DOVirtual.DelayedCall(2.6f, () => open()).SetUpdate(true).SetTarget(root);

            collect.GetComponent<Button>().onClick.AddListener(() =>
            {
                if (collected || !opened) return;
                collected = true;
                Sfx(clickSound);
                PlayChestSystemSound(c => c.PlayCollectSound());
                // coins to header, cards to the Cards tab
                FlyIcons(coinSprite, items[0], coinIconRT, 14, 60f * u, () => ReleaseCoinHold());
                var cardsTab = GetNavTab(cardsTabIndex);
                for (int i = 1; i < items.Count; i++)
                {
                    var img = items[i].Find("Icon") as RectTransform;
                    Sprite s = itemSprites[i];
                    if (cardsTab != null && s != null) FlyIcons(s, img != null ? img : items[i], cardsTab, 1, 120f * u, null);
                }
                foreach (var it in items) it.gameObject.SetActive(false);
                ClosePopup(root, dim, stage, () => isBusy = false);
            });
        }

        private RectTransform BuildRewardItem(RectTransform row, Sprite sprite, string amount, string label, Vector2 pos, bool isCard)
        {
            var item = NewRect("Reward", row);
            item.sizeDelta = new Vector2(240f, 300f) * u;
            item.anchoredPosition = pos;

            var icon = NewIcon("Icon", item, sprite, isCard ? new Vector2(150f, 200f) * u : new Vector2(190f, 150f) * u);
            icon.anchoredPosition = new Vector2(0f, 40f * u);

            var amt = NewText("Amount", item, amount, 46f * u, Color.white, TextAlignmentOptions.Center);
            amt.rectTransform.sizeDelta = new Vector2(240f, 60f) * u;
            amt.rectTransform.anchoredPosition = new Vector2(0f, -88f * u);

            var lbl = NewText("Label", item, label, 28f * u, new Color(0.8f, 0.88f, 1f, 1f), TextAlignmentOptions.Center);
            lbl.rectTransform.sizeDelta = new Vector2(240f, 44f) * u;
            lbl.rectTransform.anchoredPosition = new Vector2(0f, -132f * u);
            lbl.enableAutoSizing = true;
            lbl.fontSizeMin = 16f * u;
            lbl.fontSizeMax = 28f * u;
            return item;
        }

        private RectTransform BuildCollectButton(RectTransform parent)
        {
            var img = NewImage("Collect", parent, buyButtonSprite, Color.white, false);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(360f, 100f) * u;
            rt.anchoredPosition = new Vector2(0f, 30f * u);
            img.raycastTarget = true;
            var t = NewText("Label", rt, "COLLECT", 48f * u, Color.white, TextAlignmentOptions.Center);
            Stretch(t.rectTransform, 0f, 8f * u, 0f, 0f);
            var b = img.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            img.gameObject.AddComponent<ShopPressFeedback>();
            return rt;
        }

        private CanvasGroup AddGlowAndRays(RectTransform parent, Vector2 center, float size)
        {
            var holder = NewRect("GlowRays", parent);
            holder.anchoredPosition = center;
            holder.sizeDelta = new Vector2(size, size);
            var cg = holder.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            if (glowSprite != null)
            {
                var glow = NewImage("Glow", holder, glowSprite, new Color(1f, 0.85f, 0.4f, 0.9f), true);
                glow.rectTransform.sizeDelta = new Vector2(size * 1.1f, size * 1.1f);
                glow.rectTransform.DOScale(1.08f, 1.2f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
            }
            if (raysSprite != null)
            {
                var rays = NewImage("Rays", holder, raysSprite, new Color(1f, 0.95f, 0.7f, 0.45f), true);
                rays.rectTransform.sizeDelta = new Vector2(size * 1.3f, size * 1.3f);
                rays.rectTransform.DOLocalRotate(new Vector3(0, 0, -360f), 10f, RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear).SetLoops(-1).SetUpdate(true);
                var rays2 = NewImage("Rays2", holder, raysSprite, new Color(1f, 1f, 1f, 0.2f), true);
                rays2.rectTransform.sizeDelta = new Vector2(size * 1.05f, size * 1.05f);
                rays2.rectTransform.DOLocalRotate(new Vector3(0, 0, 360f), 14f, RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear).SetLoops(-1).SetUpdate(true);
            }
            return cg;
        }

        // ═════════════════════════════ FX ═════════════════════════════

        private void ShowToast(string message, Color bg)
        {
            if (popupLayer == null) return;
            popupLayer.SetAsLastSibling();
            var toast = NewRect("Toast", popupLayer);
            toast.anchorMin = toast.anchorMax = new Vector2(0.5f, 1f);
            toast.pivot = new Vector2(0.5f, 1f);
            toast.sizeDelta = new Vector2(640f, 96f) * u;
            toast.anchoredPosition = new Vector2(0f, -200f * u);
            var img = toast.gameObject.AddComponent<Image>();
            img.sprite = roundedSprite;
            img.type = roundedSprite != null && roundedSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            img.color = bg;
            img.raycastTarget = false;
            var cg = toast.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            var t = NewText("Text", toast, message, 38f * u, Color.white, TextAlignmentOptions.Center);
            Stretch(t.rectTransform, 20f * u, 0f, 20f * u, 0f);
            t.enableAutoSizing = true;
            t.fontSizeMin = 20f * u;
            t.fontSizeMax = 38f * u;

            toast.localScale = Vector3.one * 0.6f;
            cg.alpha = 0f;
            var seq = DOTween.Sequence().SetUpdate(true);
            seq.Append(toast.DOScale(1f, 0.3f).SetEase(Ease.OutBack, 2f));
            seq.Join(cg.DOFade(1f, 0.15f));
            seq.AppendInterval(1.4f);
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

        private void SpawnSparkles(RectTransform parent, Vector2 at, int count, Color? tint)
        {
            if (sparkleSprite == null || parent == null) return;
            for (int i = 0; i < count; i++)
            {
                var sp = NewImage("Sparkle", parent, sparkleSprite, tint ?? new Color(1f, 0.92f, 0.55f, 1f), true);
                var rt = sp.rectTransform;
                float size = UnityEngine.Random.Range(26f, 58f) * u;
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = at;
                rt.localScale = Vector3.zero;
                float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float dist = UnityEngine.Random.Range(90f, 260f) * u;
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

        /// <summary>Spawns icons that burst out of <paramref name="from"/> and home in on <paramref name="target"/>.</summary>
        private void FlyIcons(Sprite sprite, RectTransform from, RectTransform target, int count, float size, Action onAllArrived)
        {
            if (popupLayer == null || sprite == null || from == null || target == null)
            {
                onAllArrived?.Invoke();
                return;
            }
            popupLayer.SetAsLastSibling();
            Vector2 start = popupLayer.InverseTransformPoint(from.TransformPoint(from.rect.center));
            Vector2 end = popupLayer.InverseTransformPoint(target.TransformPoint(target.rect.center));
            int remaining = count;

            for (int i = 0; i < count; i++)
            {
                var holder = NewIcon("Fly", popupLayer, sprite, new Vector2(size, size));
                holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0.5f);
                // popupLayer is stretched: convert local point (relative to pivot) into anchored position
                holder.anchoredPosition = start - (Vector2)popupLayer.rect.center;
                holder.localScale = Vector3.zero;
                Vector2 scatter = holder.anchoredPosition + UnityEngine.Random.insideUnitCircle * (count > 1 ? 130f * u : 0f);
                Vector2 dest = end - (Vector2)popupLayer.rect.center;

                var seq = DOTween.Sequence().SetUpdate(true);
                seq.AppendInterval(i * 0.045f);
                seq.Append(holder.DOScale(1f, 0.2f).SetEase(Ease.OutBack));
                seq.Join(holder.DOAnchorPos(scatter, 0.28f).SetEase(Ease.OutCubic));
                seq.AppendInterval(0.06f);
                seq.Append(holder.DOAnchorPos(dest, 0.48f).SetEase(Ease.InCubic));
                seq.Join(holder.DOScale(0.55f, 0.48f).SetEase(Ease.InQuad));
                seq.OnComplete(() =>
                {
                    Destroy(holder.gameObject);
                    if (target != null)
                    {
                        target.DOKill(true);
                        target.DOPunchScale(Vector3.one * 0.28f, 0.25f, 6, 0.6f).SetUpdate(true);
                    }
                    if (coinSound != null) Sfx(coinSound); else PlayChestSystemSound(c => c.PlayCoinTickSound());
                    remaining--;
                    if (remaining == 0) onAllArrived?.Invoke();
                });
            }
        }

        // ═════════════════════════════ Counters ═════════════════════════════

        private void HandleCoinsChanged(int oldValue, int newValue)
        {
            if (coinDisplayHold > 0) return;
            AnimateCoins(newValue);
            RefreshAllCellStates();
        }

        private void HandleGemsChanged(int oldValue, int newValue)
        {
            if (gemDisplayHold > 0) { RefreshAllCellStates(); return; }
            AnimateGems(newValue);
            RefreshAllCellStates();
        }

        private void ReleaseCoinHold()
        {
            coinDisplayHold = Mathf.Max(0, coinDisplayHold - 1);
            if (coinDisplayHold == 0) AnimateCoins(PlayerWallet.Coins);
        }

        private void ReleaseGemHold()
        {
            gemDisplayHold = Mathf.Max(0, gemDisplayHold - 1);
            if (gemDisplayHold == 0) AnimateGems(PlayerWallet.Gems);
            RefreshAllCellStates();
        }

        private void AnimateCoins(int to)
        {
            coinCountTween?.Kill();
            int from = shownCoins;
            bool up = to > from;
            coinCountTween = DOTween.To(() => shownCoins, x =>
            {
                shownCoins = x;
                SetCounterTexts(shownCoins, shownGems);
            }, to, up ? 0.9f : 0.5f).SetEase(Ease.OutCubic).SetUpdate(true)
            .OnComplete(() => BounceCounter(coinText, coinIconRT, up));
        }

        private void AnimateGems(int to)
        {
            gemCountTween?.Kill();
            int from = shownGems;
            bool up = to > from;
            gemCountTween = DOTween.To(() => shownGems, x =>
            {
                shownGems = x;
                SetCounterTexts(shownCoins, shownGems);
            }, to, up ? 0.9f : 0.5f).SetEase(Ease.OutCubic).SetUpdate(true)
            .OnComplete(() => BounceCounter(gemText, gemIconRT, up));
        }

        private void BounceCounter(TextMeshProUGUI text, RectTransform icon, bool up)
        {
            if (text != null)
            {
                text.rectTransform.DOKill(true);
                text.rectTransform.DOPunchScale(Vector3.one * (up ? 0.32f : 0.15f), 0.45f, 7, 0.6f).SetUpdate(true);
            }
            if (icon != null)
            {
                icon.DOKill(true);
                icon.DOPunchScale(Vector3.one * (up ? 0.35f : 0.15f), 0.45f, 7, 0.6f).SetUpdate(true);
            }
        }

        private void RefreshCountersInstant()
        {
            coinCountTween?.Kill();
            gemCountTween?.Kill();
            if (coinDisplayHold == 0) shownCoins = PlayerWallet.Coins;
            if (gemDisplayHold == 0) shownGems = PlayerWallet.Gems;
            SetCounterTexts(shownCoins, shownGems);
        }

        private void SetCounterTexts(int coins, int gems)
        {
            if (coinText != null) coinText.text = coins.ToString("N0");
            if (gemText != null) gemText.text = gems.ToString("N0");
            SetHomeCounters(coins, gems);
        }

        private void FindHomeCounters()
        {
            if (canvasRect == null) return;
            var top = FindDeep(canvasRect, "toppanel");
            if (top == null) return;
            var c = FindDeep(top, "coinCounter");
            var g = FindDeep(top, "gemsCounter");
            if (c != null) homeCoinText = c.GetComponentInChildren<TMP_Text>(true);
            if (g != null) homeGemText = g.GetComponentInChildren<TMP_Text>(true);

            if (hookHomeCurrencyCounters)
            {
                HookCounterButton(c, () => OpenViaNav(() => coinsSection));
                HookCounterButton(g, () => OpenViaNav(() => gemsSection));
            }
        }

        private void HookCounterButton(Transform t, Action action)
        {
            if (t == null || t.GetComponent<Graphic>() == null) return;
            var btn = t.GetComponent<Button>();
            if (btn == null)
            {
                btn = t.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
            }
            btn.onClick.AddListener(() => action());
        }

        /// <summary>Opens the shop through the Shop nav tab (keeps the nav highlight in sync), then focuses a section.</summary>
        private void OpenViaNav(Func<SectionView> section)
        {
            if (isOpen) { StartCoroutine(FocusSectionRoutine(section(), 0f)); return; }
            var tab = GetNavTab(shopTabIndex);
            var btn = tab != null ? tab.GetComponent<Button>() : null;
            if (btn != null && hookBottomNav)
            {
                btn.onClick.Invoke();   // MainMenuController highlight + our Open()
                StartCoroutine(FocusSectionRoutine(section(), 0.35f));
            }
            else OpenInternal(section);
        }

        private void SetHomeCounters(int coins, int gems)
        {
            if (homeCoinText != null) homeCoinText.text = coins.ToString("N0");
            if (homeGemText != null) homeGemText.text = gems.ToString("N0");
        }

        private void OpenAtCoinsFromHeader() => StartCoroutine(FocusSectionRoutine(coinsSection, 0f));
        private void OpenAtGemsFromHeader() => StartCoroutine(FocusSectionRoutine(gemsSection, 0f));

        // ═════════════════════════════ Daily cycle ═════════════════════════════

        private void LoadDailyState()
        {
            long ticks;
            if (long.TryParse(PlayerPrefs.GetString(PREF_CYCLE_START, ""), out ticks) && ticks > 0)
                cycleStartUtc = new DateTime(ticks, DateTimeKind.Utc);
            else
            {
                cycleStartUtc = DateTime.UtcNow;
                PlayerPrefs.SetString(PREF_CYCLE_START, cycleStartUtc.Ticks.ToString());
            }
            cycleIndex = PlayerPrefs.GetInt(PREF_CYCLE_INDEX, 0);
            purchased.Clear();
            foreach (var id in PlayerPrefs.GetString(PREF_PURCHASED, "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                purchased.Add(id);
            CheckCycleElapsed();
        }

        private void SaveDailyState()
        {
            PlayerPrefs.SetString(PREF_CYCLE_START, cycleStartUtc.Ticks.ToString());
            PlayerPrefs.SetInt(PREF_CYCLE_INDEX, cycleIndex);
            PlayerPrefs.SetString(PREF_PURCHASED, string.Join(",", purchased));
            PlayerPrefs.Save();
        }

        private TimeSpan Period => TimeSpan.FromHours(Mathf.Max(0.01f, refreshHours));

        /// <summary>Returns true when a new 24h cycle started (and resets the purchased flags).</summary>
        private bool CheckCycleElapsed()
        {
            var now = DateTime.UtcNow;
            if (now < cycleStartUtc) { cycleStartUtc = now; SaveDailyState(); return false; } // clock moved back
            var elapsed = now - cycleStartUtc;
            if (elapsed < Period) return false;

            long cyclesPassed = elapsed.Ticks / Period.Ticks;
            cycleStartUtc = cycleStartUtc.AddTicks(cyclesPassed * Period.Ticks);
            cycleIndex += (int)Mathf.Min(cyclesPassed, 10000);
            purchased.Clear();
            SaveDailyState();
            return true;
        }

        private string FormatRemaining()
        {
            var remaining = (cycleStartUtc + Period) - DateTime.UtcNow;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            if (remaining.TotalHours >= 1) return $"{(int)remaining.TotalHours}h {remaining.Minutes:00}m";
            return $"{remaining.Minutes}m {remaining.Seconds:00}s";
        }

        private void UpdateTimerText()
        {
            if (timerText != null) timerText.text = "New in " + FormatRemaining();
        }

        private void OnDailyRefresh(bool animate)
        {
            // Rotate the card offers in place
            var newCards = BuildCardOffersForCycle();
            var cardCells = cells.Where(c => c.offer.kind == ShopRewardKind.CardCopies).ToList();
            for (int i = 0; i < cardCells.Count && i < newCards.Count; i++) ApplyOfferToCell(cardCells[i], newCards[i]);
            RefreshAllCellStates();

            if (!animate) return;
            int k = 0;
            foreach (var c in cells.Where(x => x.offer.limitedDaily))
            {
                var vis = c.visual;
                vis.DOKill(true);
                DOTween.Sequence().SetUpdate(true)
                    .AppendInterval(k++ * 0.06f)
                    .Append(vis.DOScaleX(0f, 0.15f).SetEase(Ease.InQuad))
                    .Append(vis.DOScaleX(1f, 0.3f).SetEase(Ease.OutBack, 2f));
            }
            ShowToast("New daily offers!", new Color(0.15f, 0.45f, 0.2f, 0.95f));
        }

        // ═════════════════════════════ Scene hooks ═════════════════════════════

        private IEnumerator HookSceneNextFrame()
        {
            yield return null;   // let MainMenuController finish wiring its own tab listeners
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
                        var hit = tab.gameObject.AddComponent<Image>();
                        hit.color = new Color(0f, 0f, 0f, 0f);
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
            if (index == shopTabIndex) Open();
            else Close();
        }

        private RectTransform GetNavTab(int index)
        {
            if (bottomNav == null || index < 0 || index >= bottomNav.childCount) return null;
            return bottomNav.GetChild(index) as RectTransform;
        }

        private void EnsureSiblingBelowNav()
        {
            if (bottomNav == null || bottomNav.parent != transform.parent) return;
            int navIndex = bottomNav.GetSiblingIndex();
            if (transform.GetSiblingIndex() > navIndex) transform.SetSiblingIndex(navIndex);
        }

        private void UpdateViewportBottom()
        {
            if (scrollRectRT == null) return;
            float bottom = 0f;
            if (bottomNav != null && bottomNav.gameObject.activeInHierarchy)
            {
                var corners = new Vector3[4];
                bottomNav.GetWorldCorners(corners);
                float navTop = Mathf.Max(corners[1].y, corners[2].y);
                Vector3 local = pageRoot.InverseTransformPoint(new Vector3(corners[1].x, navTop, corners[1].z));
                // Let the shop scroll under the bottom nav (drawn on top); content keeps bottom padding to clear it.
                bottom = 0f;
            }
            scrollRectRT.offsetMin = new Vector2(0f, bottom);
        }

        // ═════════════════════════════ Resolvers ═════════════════════════════

        private CardInventoryManager ResolveInventory()
        {
            if (CardInventoryManager.Instance != null) return CardInventoryManager.Instance;
            return FindFirstObjectByType<CardInventoryManager>(FindObjectsInactive.Include);
        }

        private CardLibrary ResolveLibrary()
        {
            if (cardLibraryOverride != null) return cardLibraryOverride;
            var inv = ResolveInventory();
            return inv != null ? inv.Library : null;
        }

        private ChestSystemController ResolveChestSystem()
        {
            if (ChestSystemController.Instance != null) return ChestSystemController.Instance;
            return FindFirstObjectByType<ChestSystemController>(FindObjectsInactive.Include);
        }

        private ChestConfig ResolveChestConfig(ChestType type)
        {
            ChestConfig cfg = null;
            var chest = ResolveChestSystem();
            if (chest != null) cfg = chest.GetConfig(type);
            if (cfg == null || cfg.maxCoins <= 0) cfg = ChestConfig.CreateDefault(type);
            return cfg;
        }

        private Sprite ResolveChestOpenSprite(ChestType type)
        {
            var cfg = ResolveChestConfig(type);
            Sprite mine = type == ChestType.Gold ? goldChestOpenSprite : silverChestOpenSprite;
            if (mine != null) return mine;
            return cfg != null ? cfg.openSprite : null;
        }

        private void PlayChestSystemSound(Action<ChestSystemController> play)
        {
            var c = ResolveChestSystem();
            if (c != null && c.isActiveAndEnabled) play(c);
        }

        private void Sfx(AudioClip clip)
        {
            if (clip != null && audioSource != null) KoSAudio.PlayOneShot(audioSource, clip, 1f, "shop");
        }

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
            var m = new Material(f.material) { name = f.name + " (Shop Outline)" };
            m.EnableKeyword("OUTLINE_ON");
            if (m.HasProperty("_OutlineWidth")) m.SetFloat("_OutlineWidth", 0.22f);
            if (m.HasProperty("_OutlineColor")) m.SetColor("_OutlineColor", new Color(0.06f, 0.08f, 0.24f, 1f));
            if (m.HasProperty("_FaceDilate")) m.SetFloat("_FaceDilate", 0.12f);
            m.EnableKeyword("UNDERLAY_ON");
            if (m.HasProperty("_UnderlayColor")) m.SetColor("_UnderlayColor", new Color(0f, 0f, 0.05f, 0.6f));
            if (m.HasProperty("_UnderlayOffsetY")) m.SetFloat("_UnderlayOffsetY", -0.9f);
            if (m.HasProperty("_UnderlaySoftness")) m.SetFloat("_UnderlaySoftness", 0.15f);
            return m;
        }

        // ═════════════════════════════ UI helpers ═════════════════════════════

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
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

        /// <summary>
        /// Creates an icon whose VISIBLE pixels fill <paramref name="box"/> (compensates transparent padding
        /// using the trims baked by the editor tool), centred, keeping aspect ratio.
        /// Returns the holder rect (size = box); the Image is a grandchild.
        /// </summary>
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
            img.preserveAspect = false;
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
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                t.DOKill();
                var g = t.GetComponent<Graphic>();
                if (g != null) g.DOKill();
                var cg = t.GetComponent<CanvasGroup>();
                if (cg != null) cg.DOKill();
            }
            DOTween.Kill(root);
        }

        private static T SafeGet<T>(T[] arr, int i, T fallback) => arr != null && i >= 0 && i < arr.Length ? arr[i] : fallback;

        private static string PrettyName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "Card";
            var parts = id.Split('_');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0) parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            return string.Join(" ", parts);
        }
    }
}
