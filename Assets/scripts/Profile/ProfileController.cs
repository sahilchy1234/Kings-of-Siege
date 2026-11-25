using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Siege.Cards;
using Siege.CardsScreen;
using Siege.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Siege.Profile
{
    /// <summary>
    /// Kings of Siege "Player Profile" page (Clash-Royale-style): header with back + coins/gems, title banner,
    /// player card (avatar, level, name + edit, tag, XP, rank banner, trophies / best / cards found),
    /// clan panel and the active Battle Deck with real card levels + average elixir.
    ///
    /// Like the shop and the cards screen, the whole page is generated at runtime from the sprites wired by
    /// Kings of Siege > Profile > Build Profile Page (MainMenu) and is sized from the live canvas.
    /// It only reads existing data (see <see cref="PlayerProfileData"/>); no existing script is modified.
    ///
    /// Navigation: tap the avatar in the main-menu top bar to open it. The back button, or any bottom-nav tab, closes it.
    /// </summary>
    public class ProfileController : MonoBehaviour
    {
        public static ProfileController Instance { get; private set; }

        // ───────────────────────────── Inspector ─────────────────────────────
        [Header("Sprites (auto-assigned by Kings of Siege > Profile > Build Profile Page)")]
        public Sprite backgroundSprite;
        public Sprite roundedSprite;
        public Sprite circleSprite;
        public Sprite coinSprite;
        public Sprite gemSprite;
        public Sprite elixirSprite;
        public Sprite trophySprite;
        public Sprite crownSprite;
        public Sprite shieldSprite;
        public Sprite cardsIconSprite;
        public Sprite backSprite;
        public Sprite glowSprite;
        public Sprite sparkleSprite;
        public Sprite lockSprite;
        public Sprite kingPortraitSprite;
        [Tooltip("Home-screen league banner frame (Panels/league_panel.png).")]
        public Sprite leaguePanelSprite;
        [Tooltip("28 rank badges from UIPACK/badges, indexed by PlayerProfileData.RankTier.badgeIndex: " +
                 "0-2 Wood I-III, 3-5 Silver, 6-8 Gold, 9-11 Platinum, 12-14 Teal, 15-17 Epic, 18-20 Super, " +
                 "21-23 Legend, 24-26 Champions, 27 Kings.")]
        public Sprite[] rankBadgeByDivision = new Sprite[PlayerProfileData.RANK_BADGE_COUNT];
        [Tooltip("Fallback rank badge when a division slot is empty.")]
        public Sprite rankBadgeSprite;
        [Tooltip("Blue shield used for the level badge (Icons/level_badge.png).")]
        public Sprite levelShieldSprite;
        public Sprite swordsSprite;
        public Sprite starSprite;
        [Tooltip("Selectable avatars (index is saved in the KoS_ProfileAvatar pref). Index 0 is the default.")]
        public Sprite[] avatarSprites;
        public TMP_FontAsset font;

        [Header("Data")]
        public CardLibrary cardLibraryOverride;

        [Header("Scene Hooks")]
        [Tooltip("The main menu bottom navigation bar (auto-found by name 'downpanel').")]
        public RectTransform bottomNav;
        public bool hookBottomNav = true;
        [Tooltip("Tapping the avatar in the main-menu top bar opens the profile.")]
        public bool hookTopBarAvatar = true;

        [Header("Audio (optional)")]
        public AudioSource audioSource;
        public AudioClip clickSound;
        public AudioClip successSound;
        public AudioClip errorSound;

        // ───────────────────────────── Constants ─────────────────────────────
        private const float REF_W = 941f;
        private const int COLUMNS = 4;

        private static readonly Color BgBlue = new Color(0.04f, 0.11f, 0.34f, 1f);
        private static readonly Color TextWhite = new Color(1f, 0.98f, 0.94f, 1f);
        private static readonly Color TextDim = new Color(0.62f, 0.74f, 0.92f, 1f);
        private static readonly Color Gold = new Color(1f, 0.80f, 0.25f, 1f);
        private static readonly Color GoldDark = new Color(0.78f, 0.46f, 0.06f, 1f);
        private static readonly Color Navy = new Color(0.02f, 0.06f, 0.20f, 0.72f);

        // ───────────────────────────── Runtime ─────────────────────────────
        // Marked [SerializeField] so Unity preserves references through domain reload (entering Play mode).
        // This lets Open() skip Build() when a page was already baked by BuildInEditor(), preserving
        // any inspector tweaks the user made in Edit mode.
        private Canvas rootCanvas;
        private RectTransform canvasRect;
        [SerializeField] private RectTransform pageRoot;
        [SerializeField] private CanvasGroup pageGroup;
        private Material outlineMaterial;
        private float u = 1f;
        private float W, contentW, cw;
        [SerializeField] private float builtWidth = -1f;
        private bool isOpen;
        private bool hooked;

        [SerializeField] private RectTransform header;
        [SerializeField] private TextMeshProUGUI coinText, gemText;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private RectTransform viewport, content;

        // Player card widgets
        [SerializeField] private Image avatarImage;
        [SerializeField] private TextMeshProUGUI avatarFallbackText;
        [SerializeField] private RectTransform avatarHolder, levelBadge, pencil, rankBanner, crownIcon, trophyIcon;
        [SerializeField] private Image avatarGlow, xpFill;
        [SerializeField] private RectTransform xpFillRT;
        [SerializeField] private TextMeshProUGUI nameText, tagText, levelText, levelLabel, xpText, rankTierText, rankTitleText, nextRankText;
        [SerializeField] private TextMeshProUGUI trophiesValue, bestValue, cardsValue, winsValue, winRateValue, threeStarValue;
        [SerializeField] private Image rankBadgeImage, rankBadgeGlow;
        [SerializeField] private TextMeshProUGUI rankPillValue;
        private RectTransform rankPillFill;
        private readonly List<Image> sparkles = new List<Image>();

        // Clan widgets
        [SerializeField] private TextMeshProUGUI clanNameText, clanSubText, clanTrophiesText;
        [SerializeField] private RectTransform clanTrophyBox, clanRoleChip, clanSoonChip;
        [SerializeField] private Image clanRoleBg;
        [SerializeField] private TextMeshProUGUI clanRoleText;

        // Deck widgets
        [SerializeField] private List<Tile> unitTiles = new List<Tile>();
        [SerializeField] private List<Tile> powerTiles = new List<Tile>();
        [SerializeField] private TextMeshProUGUI elixirValue;
        [SerializeField] private RectTransform elixirDrop;

        // Reveal / animation
        private class Reveal { public RectTransform rt; public CanvasGroup cg; public Vector2 final; public Vector2 from; public float delay; public float overshoot = 1.1f; }
        private readonly List<Reveal> reveals = new List<Reveal>();
        private readonly List<Tween> idleTweens = new List<Tween>();
        private Tween xpTween;

        // Data snapshot
        private CardInventoryManager inventory;
        private CardLibrary library;
        private int dataLevel, dataXp, dataXpNeeded;
        private float dataAvgElixir;

        // Popups
        [SerializeField] private RectTransform popupLayer, editorRoot, editorPanel;
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private TextMeshProUGUI nameCounter;
        private int pendingAvatar;
        private readonly List<Image> avatarRings = new List<Image>();
        [SerializeField] private RectTransform toast;

        public bool IsOpen => isOpen;

        [System.Serializable]
        private class Tile
        {
            public RectTransform root, visual, emptyRoot, costBadge;
            public Image plate, art, levelBand;
            public TextMeshProUGUI levelText, nameText, costText, artFallback;
        }

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
        }

        private void OnEnable()
        {
            PlayerProfileData.OnProfileChanged += SyncHomeAvatar;
            PlayerWallet.OnCoinsChanged += HandleWalletChanged;
            PlayerWallet.OnGemsChanged += HandleWalletChanged;
        }

        private void OnDisable()
        {
            PlayerProfileData.OnProfileChanged -= SyncHomeAvatar;
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
            KillIdle();
            if (pageRoot != null) KillTree(pageRoot);
            if (popupLayer != null) { KillTree(popupLayer); Destroy(popupLayer.gameObject); }
            if (outlineMaterial != null) Destroy(outlineMaterial);
            if (Instance == this) Instance = null;
        }

        // ═════════════════════════════ Public API ═════════════════════════════

        public void Open()
        {
            if (canvasRect == null) return;
            ResolveData();
            Canvas.ForceUpdateCanvases();

            float w = canvasRect.rect.width;
            if (pageRoot == null || Mathf.Abs(w - builtWidth) > 1f) Build();
            WireBackButton();

            EnsureSiblingBelowNav();
            pageRoot.gameObject.SetActive(true);
            UpdateViewportBottom();

            bool wasOpen = isOpen;
            isOpen = true;
            pageGroup.DOKill();
            pageGroup.interactable = true;
            pageGroup.blocksRaycasts = true;

            Refresh();
            if (!wasOpen)
            {
                scroll.verticalNormalizedPosition = 1f;
                PlayEntrance();
                Sfx(clickSound);
            }
            else pageGroup.alpha = 1f;
        }

        /// <summary>
        /// The page can be baked into the scene by BuildInEditor(), and those saved buttons have no runtime
        /// listeners, so the back button did nothing. Wire it here every time the page opens.
        /// </summary>
        private void WireBackButton()
        {
            if (pageRoot == null) return;
            foreach (var b in pageRoot.GetComponentsInChildren<Button>(true))
            {
                if (b.name != "BackButton") continue;
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => { Sfx(clickSound); Close(); });
            }
        }

        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;
            CloseEditor(true);
            if (pageRoot == null) return;

            pageGroup.DOKill();
            pageGroup.interactable = false;
            pageGroup.blocksRaycasts = false;
            pageGroup.DOFade(0f, 0.18f).SetEase(Ease.InQuad).SetUpdate(true)
                .OnComplete(() =>
                {
                    if (!isOpen && pageRoot != null)
                    {
                        KillIdle();
                        pageRoot.gameObject.SetActive(false);
                    }
                });
        }

        public void Toggle() { if (isOpen) Close(); else Open(); }

#if UNITY_EDITOR
        /// <summary>Builds the full UI hierarchy immediately in Edit mode so you can inspect it without entering Play mode.
        /// Called by Kings of Siege > Profile > Build Profile Page after wiring sprites.</summary>
        public void BuildInEditor()
        {
            rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas != null) rootCanvas = rootCanvas.rootCanvas;
            canvasRect = rootCanvas != null ? (RectTransform)rootCanvas.transform : transform.parent as RectTransform;
            if (canvasRect == null) { Debug.LogWarning("[ProfileController] BuildInEditor: no Canvas found."); return; }
            if (audioSource == null) { audioSource = GetComponent<AudioSource>(); }

            // Force layout so canvasRect.rect is populated; fall back to CanvasScaler reference resolution
            // if still zero (common in Edit mode before the first layout pass).
            UnityEngine.Canvas.ForceUpdateCanvases();
            if (canvasRect.rect.width < 1f)
            {
                var scaler = rootCanvas != null ? rootCanvas.GetComponent<UnityEngine.UI.CanvasScaler>() : null;
                Vector2 refRes = scaler != null ? scaler.referenceResolution : new Vector2(1080f, 1920f);
                canvasRect.sizeDelta = refRes;
                UnityEngine.Canvas.ForceUpdateCanvases();
            }

            try
            {
                Build();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[ProfileController] BuildInEditor failed: " + e.Message + "\n" + e.StackTrace);
                return;
            }
            if (pageRoot != null) pageRoot.gameObject.SetActive(true);
            else { Debug.LogWarning("[ProfileController] BuildInEditor: pageRoot is null after Build()"); return; }
            UnityEditor.EditorUtility.SetDirty(gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            Debug.Log("[ProfileController] BuildInEditor: done — Page has " + pageRoot.childCount + " direct children.");
        }
#endif

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
                    inventory.OnDeckChanged += HandleInventoryChanged;
                    inventory.OnInventoryChanged += HandleInventoryChanged;
                }
            }
            library = cardLibraryOverride != null ? cardLibraryOverride : (inventory != null ? inventory.Library : null);
        }

        private void UnhookInventory()
        {
            if (inventory == null) return;
            inventory.OnDeckChanged -= HandleInventoryChanged;
            inventory.OnInventoryChanged -= HandleInventoryChanged;
            inventory = null;
        }

        private void HandleInventoryChanged()
        {
            if (!isOpen || pageRoot == null) return;
            Refresh();
        }

        private void HandleWalletChanged(int oldValue, int newValue)
        {
            if (pageRoot == null) return;
            if (coinText != null) coinText.text = PlayerProfileData.Format(PlayerWallet.Coins);
            if (gemText != null) gemText.text = PlayerProfileData.Format(PlayerWallet.Gems);
        }

        // ═════════════════════════════ Build ═════════════════════════════

        private void Build()
        {
            if (pageRoot != null)
            {
                KillIdle();
                KillTree(pageRoot);
                Destroy(pageRoot.gameObject);
            }

            // Destroy any Page child that was baked into the scene by BuildInEditor(), so it
            // doesn't duplicate when Play mode runs Build() with pageRoot == null.
            var stale = transform.Find("Page");
            if (stale != null) DestroyImmediate(stale.gameObject);

            reveals.Clear();
            unitTiles.Clear();
            powerTiles.Clear();

            W = canvasRect.rect.width;
            float H = canvasRect.rect.height;
            contentW = Mathf.Min(W, H * 0.62f);
            u = contentW / REF_W;
            cw = contentW - 56f * u;
            builtWidth = W;

            if (font == null) font = ResolveFont();
            if (outlineMaterial == null && font != null) outlineMaterial = CreateOutlineMaterial(font);

            pageRoot = NewRect("Page", transform);
            Stretch(pageRoot);
            pageGroup = pageRoot.gameObject.AddComponent<CanvasGroup>();
            var solid = pageRoot.gameObject.AddComponent<Image>();
            solid.color = BgBlue;
            solid.raycastTarget = true;

            if (backgroundSprite != null)
            {
                var bg = NewImage("Background", pageRoot, backgroundSprite, Color.white, false);
                bg.rectTransform.anchorMin = bg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                bg.rectTransform.sizeDelta = new Vector2(W, H);
                var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = backgroundSprite.rect.width / Mathf.Max(1f, backgroundSprite.rect.height);
            }

            // Dark blue vertical gradient over the artwork
            var shade = NewImage("Shade", pageRoot, null, Color.white, false);
            Stretch(shade.rectTransform);
            shade.gameObject.AddComponent<CardsScreenGradient>().Set(new Color(0.05f, 0.14f, 0.46f, 0.30f), new Color(0.01f, 0.03f, 0.14f, 0.90f));

            if (glowSprite != null)
            {
                var topGlow = NewImage("TopGlow", pageRoot, glowSprite, new Color(0.30f, 0.62f, 1f, 0.35f), false);
                PlaceTop(topGlow.rectTransform, 0f, -contentW * 0.25f, contentW * 1.5f, contentW * 1.2f);
            }

            float headerH = 96f * u;
            BuildHeader(headerH);

            // Standard Unity ScrollView hierarchy: ScrollRect > Viewport (RectMask2D) > Content
            var scrollRT = NewRect("Scroll", pageRoot);
            Stretch(scrollRT, 0f, 0f, 0f, headerH);
            scroll = scrollRT.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.08f;
            scroll.scrollSensitivity = 40f;
            scroll.decelerationRate = 0.12f;
            // Transparent image so the scroll rect can receive pointer events on empty areas
            var scrollBg = scrollRT.gameObject.AddComponent<Image>();
            scrollBg.color = Color.clear;
            scrollBg.raycastTarget = true;

            viewport = NewRect("Viewport", scrollRT);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            scroll.viewport = viewport;
            scroll.content = content;

            float y = BuildContent();
            content.sizeDelta = new Vector2(0f, y);

            toast = null;
            pageRoot.gameObject.SetActive(false);
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
            hbg.color = new Color(0.02f, 0.06f, 0.22f, 0.7f);
            hbg.raycastTarget = true;

            var line = NewImage("Line", header, null, new Color(1f, 0.80f, 0.36f, 0.75f), false);
            line.rectTransform.anchorMin = new Vector2(0f, 0f);
            line.rectTransform.anchorMax = new Vector2(1f, 0f);
            line.rectTransform.pivot = new Vector2(0.5f, 0f);
            line.rectTransform.sizeDelta = new Vector2(0f, 4f * u);

            var inner = NewRect("Inner", header);
            inner.anchorMin = inner.anchorMax = new Vector2(0.5f, 0.5f);
            inner.sizeDelta = new Vector2(contentW - 48f * u, headerH);
            var hl = inner.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.spacing = 12f * u;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;

            var back = BuildBackButton(inner);
            AddLayout(back, 90f * u, 80f * u);

            AddFlexibleSpacer(inner);

            var coinPill = BuildCurrencyPill(inner, "Coins", coinSprite, out coinText);
            AddLayout(coinPill, 250f * u, 70f * u);
            AddSpacer(inner, 6f * u);
            var gemPill = BuildCurrencyPill(inner, "Gems", gemSprite, out gemText);
            AddLayout(gemPill, 220f * u, 70f * u);
        }

        private RectTransform BuildBackButton(Transform parent)
        {
            var btn = NewRect("BackButton", parent);
            var lip = NewRounded("Lip", btn, new Color(0.03f, 0.10f, 0.35f, 1f), 24f);
            Stretch(lip.rectTransform, 0f, 0f, 0f, 8f * u);
            lip.rectTransform.anchoredPosition += new Vector2(0f, -4f * u);
            var face = NewRounded("Face", btn, Color.white, 24f);
            Stretch(face.rectTransform, 0f, 8f * u, 0f, 0f);
            face.gameObject.AddComponent<CardsScreenGradient>().Set(new Color(0.34f, 0.66f, 1f), new Color(0.12f, 0.38f, 0.90f));
            var gloss = NewRounded("Gloss", face.rectTransform, new Color(1f, 1f, 1f, 0.22f), 18f);
            gloss.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            gloss.rectTransform.anchorMax = Vector2.one;
            gloss.rectTransform.offsetMin = new Vector2(6f * u, 0f);
            gloss.rectTransform.offsetMax = new Vector2(-6f * u, -5f * u);

            if (backSprite != null)
            {
                var icon = NewImage("Arrow", face.rectTransform, backSprite, Color.white, true);
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                icon.rectTransform.sizeDelta = new Vector2(46f, 46f) * u;
            }
            else
            {
                var t = NewText("Arrow", face.rectTransform, "<", 60f * u, Color.white, TextAlignmentOptions.Center);
                Stretch(t.rectTransform);
            }
            MakeButton(btn, () => { Sfx(clickSound); Close(); });
            // Wire the click explicitly: the MakeButton listener was not reaching the Button at runtime,
            // so the back button did nothing. Clear first so it can never fire twice.
            var backBtn = btn.GetComponent<Button>();
            if (backBtn != null)
            {
                backBtn.onClick.RemoveAllListeners();
                backBtn.onClick.AddListener(() => { Sfx(clickSound); Close(); });
            }
            return btn;
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

        // ───────────────────────────── Content ─────────────────────────────

        private float BuildContent()
        {
            float y = 74f * u;   // room for the crown above the title ribbon

            // 1. Title banner
            float titleH = 96f * u;
            var title = NewRect("TitlePanel", content);
            PlaceTop(title, 0f, y, cw * 0.78f, titleH);
            var tShadow = NewRounded("Shadow", title, new Color(0f, 0.02f, 0.10f, 0.75f), 30f);
            Stretch(tShadow.rectTransform, 4f * u, -10f * u, 4f * u, 0f);
            Framed(title, 30f, 5f, GoldHi, GoldLo, RoyalHi, RoyalLo);
            if (crownSprite != null)
            {
                var crown = NewIcon("Crown", title, crownSprite, new Vector2(136f, 102f) * u);
                crown.anchorMin = crown.anchorMax = new Vector2(0.5f, 1f);
                crown.anchoredPosition = new Vector2(0f, 14f * u);
            }
            var tText = NewText("Text", title, "PLAYER PROFILE", 54f * u, TextWhite, TextAlignmentOptions.Center);
            Stretch(tText.rectTransform);
            tText.characterSpacing = 4f;
            AddReveal(title, 0.05f, new Vector2(0f, 50f * u), 1.4f);
            y += titleH + 34f * u;

            // 2. Player card
            y = BuildPlayerCard(y);
            y += 28f * u;

            // 3. Clan
            y = BuildClanPanel(y);
            y += 34f * u;

            // 4. Battle deck
            y = BuildDeckSection(y);

            y += 220f * u; // clearance for the bottom nav
            return y;
        }

        // ───────────────────────────── Player card ─────────────────────────────

        private float BuildPlayerCard(float y)
        {
            float cardH = 1000f * u;
            var card = NewRect("PlayerCard", content);
            PlaceTop(card, 0f, y, cw, cardH);

            var shadow = NewRounded("Shadow", card, new Color(0f, 0.02f, 0.10f, 0.6f), 40f);
            Stretch(shadow.rectTransform, 4f * u, -14f * u, 4f * u, 14f * u);

            Framed(card, 40f, 6f, GoldHi, GoldLo, NavyHi, NavyLo);

            // inner glow + top sheen
            if (glowSprite != null)
            {
                var glow = NewImage("InnerGlow", card, glowSprite, new Color(0.45f, 0.75f, 1f, 0.28f), false);
                PlaceTop(glow.rectTransform, -cw * 0.18f, -cw * 0.15f, cw * 0.9f, cw * 0.9f);
            }

            // ── Avatar ──
            float avSize = 250f * u;
            avatarHolder = NewRect("Avatar", card);
            PlaceTopLeft(avatarHolder, 40f * u, 44f * u, avSize, avSize);

            if (glowSprite != null)
            {
                avatarGlow = NewImage("Glow", avatarHolder, glowSprite, new Color(1f, 0.82f, 0.3f, 0.55f), false);
                avatarGlow.rectTransform.anchorMin = avatarGlow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                avatarGlow.rectTransform.sizeDelta = new Vector2(avSize * 1.7f, avSize * 1.7f);
            }
            var ring = NewImage("Ring", avatarHolder, circleSprite, Color.white, false);
            Stretch(ring.rectTransform);
            ring.gameObject.AddComponent<CardsScreenGradient>().Set(new Color(1f, 0.92f, 0.5f), new Color(0.86f, 0.5f, 0.08f));
            var plate = NewImage("Plate", avatarHolder, circleSprite, new Color(0.05f, 0.12f, 0.38f, 1f), false);
            Stretch(plate.rectTransform, 9f * u, 9f * u, 9f * u, 9f * u);

            var maskRT = NewRect("Mask", avatarHolder);
            Stretch(maskRT, 14f * u, 14f * u, 14f * u, 14f * u);
            var maskImg = maskRT.gameObject.AddComponent<Image>();
            maskImg.sprite = circleSprite;
            maskImg.color = Color.white;
            var mask = maskRT.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            avatarImage = NewImage("Portrait", maskRT, null, Color.white, false);
            avatarImage.rectTransform.anchorMin = avatarImage.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            avatarImage.rectTransform.sizeDelta = new Vector2(avSize, avSize);
            var fit = avatarImage.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 1f;
            avatarFallbackText = NewText("Initial", maskRT, "?", 120f * u, TextWhite, TextAlignmentOptions.Center);
            Stretch(avatarFallbackText.rectTransform);
            MakeButton(avatarHolder, OpenEditor, false);

            // Level badge (blue shield from the home UI pack)
            float lvSize = 118f * u;
            levelBadge = NewRect("LevelBadge", card);
            PlaceTopLeft(levelBadge, 40f * u + avSize - lvSize * 0.80f, 44f * u + avSize - lvSize * 0.86f, lvSize, lvSize);
            var lbShadow = NewImage("Shadow", levelBadge, circleSprite, new Color(0f, 0f, 0.08f, 0.45f), false);
            lbShadow.rectTransform.anchorMin = lbShadow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            lbShadow.rectTransform.sizeDelta = new Vector2(lvSize * 0.8f, lvSize * 0.8f);
            lbShadow.rectTransform.anchoredPosition = new Vector2(0f, -lvSize * 0.08f);
            if (levelShieldSprite != null)
            {
                var shieldImg = NewImage("Shield", levelBadge, levelShieldSprite, Color.white, false);
                shieldImg.rectTransform.anchorMin = shieldImg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                float imgH = lvSize / 0.60f;
                shieldImg.rectTransform.sizeDelta = new Vector2(imgH * levelShieldSprite.rect.width / Mathf.Max(1f, levelShieldSprite.rect.height), imgH);
            }
            else
            {
                var fallback = NewImage("Fallback", levelBadge, circleSprite, new Color(0.18f, 0.42f, 0.95f), false);
                Stretch(fallback.rectTransform);
            }
            levelText = NewText("Level", levelBadge, "1", 56f * u, Color.white, TextAlignmentOptions.Center);
            Stretch(levelText.rectTransform, 0f, 4f * u, 0f, 4f * u);
            levelText.enableAutoSizing = true;
            levelText.fontSizeMin = 24f * u;
            levelText.fontSizeMax = 56f * u;

            // sparkles around the avatar
            if (sparkleSprite != null)
            {
                Vector2[] spots = { new Vector2(-0.1f, 0.08f), new Vector2(1.02f, 0.02f), new Vector2(0.92f, 0.62f), new Vector2(0.02f, 0.78f) };
                for (int i = 0; i < spots.Length; i++)
                {
                    float sz = (34f + 14f * (i % 2)) * u;
                    var sp = NewImage("Sparkle" + i, avatarHolder, sparkleSprite, new Color(1f, 0.95f, 0.7f, 0f), true);
                    sp.rectTransform.anchorMin = sp.rectTransform.anchorMax = new Vector2(0f, 1f);
                    sp.rectTransform.sizeDelta = new Vector2(sz, sz);
                    sp.rectTransform.anchoredPosition = new Vector2(spots[i].x * avSize, -spots[i].y * avSize);
                    sparkles.Add(sp);
                }
            }

            // ── Name block ──
            float blockX = 40f * u + avSize + 34f * u;
            float blockW = cw - blockX - 40f * u;
            float pencilSize = 72f * u;

            nameText = NewText("Name", card, "Commander", 58f * u, TextWhite, TextAlignmentOptions.MidlineLeft);
            PlaceTopLeft(nameText.rectTransform, blockX, 52f * u, blockW - pencilSize - 12f * u, 76f * u);
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 28f * u;
            nameText.fontSizeMax = 58f * u;

            pencil = BuildPencilButton(card, pencilSize);
            PlaceTopLeft(pencil, blockX + blockW - pencilSize, 50f * u, pencilSize, pencilSize);

            // tag (tap to copy)
            var tagRT = NewRect("TagButton", card);
            PlaceTopLeft(tagRT, blockX, 132f * u, blockW, 50f * u);
            tagText = NewText("Tag", tagRT, "#00000000", 34f * u, TextDim, TextAlignmentOptions.MidlineLeft);
            Stretch(tagText.rectTransform);
            MakeButton(tagRT, () =>
            {
                GUIUtility.systemCopyBuffer = PlayerProfileData.PlayerTag;
                Sfx(clickSound);
                ShowToast("Player tag copied!", new Color(0.12f, 0.45f, 0.25f, 0.96f));
            }, false);

            // XP bar
            xpText = NewText("XpText", card, "0 / 150 XP", 30f * u, TextDim, TextAlignmentOptions.MidlineRight);
            PlaceTopLeft(xpText.rectTransform, blockX, 196f * u, blockW, 40f * u);
            var xpTitle = NewText("XpTitle", card, "KING XP", 30f * u, TextWhite, TextAlignmentOptions.MidlineLeft);
            PlaceTopLeft(xpTitle.rectTransform, blockX, 196f * u, blockW * 0.5f, 40f * u);

            var xpTrack = NewRect("XpTrack", card);
            PlaceTopLeft(xpTrack, blockX, 244f * u, blockW, 36f * u);
            var trackBg = NewRounded("Bg", xpTrack, new Color(0.01f, 0.04f, 0.16f, 0.9f), 18f);
            Stretch(trackBg.rectTransform);
            xpFillRT = NewRect("Fill", xpTrack);
            xpFillRT.anchorMin = new Vector2(0f, 0f);
            xpFillRT.anchorMax = new Vector2(0.5f, 1f);
            xpFillRT.offsetMin = new Vector2(4f * u, 4f * u);
            xpFillRT.offsetMax = new Vector2(-4f * u, -4f * u);
            xpFill = NewRounded("FillImg", xpFillRT, Color.white, 14f);
            Stretch(xpFill.rectTransform);
            xpFill.gameObject.AddComponent<CardsScreenGradient>().Set(new Color(0.55f, 1f, 0.55f), new Color(0.12f, 0.72f, 0.42f));
            var xpGloss = NewRounded("Gloss", xpFillRT, new Color(1f, 1f, 1f, 0.28f), 10f);
            xpGloss.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            xpGloss.rectTransform.anchorMax = Vector2.one;
            xpGloss.rectTransform.offsetMin = new Vector2(6f * u, 0f);
            xpGloss.rectTransform.offsetMax = new Vector2(-6f * u, -3f * u);

            // ── Rank banner (home league style: gold frame, navy plate, crest badge) ──
            float rbW = cw - 80f * u;
            float bnW = Mathf.Min(rbW, 640f * u);
            float bnH = bnW / 3.0f;
            rankBanner = NewRect("RankBanner", card);
            PlaceTopLeft(rankBanner, (cw - bnW) * 0.5f, 312f * u, bnW, bnH);
            var bnShadow = NewImage("Shadow", rankBanner, null, Color.clear, false);
            Stretch(bnShadow.rectTransform);
            if (leaguePanelSprite != null)
            {
                var plateImg = NewImage("Plate", rankBanner, leaguePanelSprite, Color.white, false);
                Stretch(plateImg.rectTransform, -bnW * 0.02f, -bnH * 0.04f, -bnW * 0.02f, -bnH * 0.04f);
            }
            else
            {
                var plateImg = NewRounded("Plate", rankBanner, new Color(0.04f, 0.12f, 0.40f), 40f);
                Stretch(plateImg.rectTransform);
            }

            float badgeH = bnH * 1.12f;
            var badgeHolder = NewRect("BadgeHolder", rankBanner);
            badgeHolder.anchorMin = badgeHolder.anchorMax = new Vector2(0f, 0.5f);
            badgeHolder.sizeDelta = new Vector2(badgeH, badgeH);
            badgeHolder.anchoredPosition = new Vector2(bnW * 0.14f, bnH * 0.02f);
            crownIcon = badgeHolder;
            rankBadgeGlow = glowSprite != null ? NewImage("Glow", badgeHolder, glowSprite, new Color(1f, 0.85f, 0.3f, 0.5f), false) : null;
            if (rankBadgeGlow != null)
            {
                rankBadgeGlow.rectTransform.anchorMin = rankBadgeGlow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rankBadgeGlow.rectTransform.sizeDelta = new Vector2(badgeH * 1.5f, badgeH * 1.5f);
            }
            var rankSprite = RankSpriteFor(PlayerProfileData.GetRank(PlayerProfileData.Trophies).badgeIndex);
            if (rankSprite != null)
            {
                rankBadgeImage = NewImage("Badge", badgeHolder, rankSprite, Color.white, false);
                rankBadgeImage.rectTransform.anchorMin = rankBadgeImage.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                float ih = badgeH / 0.80f;
                rankBadgeImage.rectTransform.sizeDelta = new Vector2(ih * rankSprite.rect.width / Mathf.Max(1f, rankSprite.rect.height), ih);
            }
            else
            {
                var ci = NewIcon("Crown", badgeHolder, crownSprite, new Vector2(badgeH, badgeH));
                ci.anchorMin = ci.anchorMax = new Vector2(0.5f, 0.5f);
                ci.anchoredPosition = Vector2.zero;
            }

            float txL = bnW * 0.30f;
            float txW = bnW * 0.62f;
            rankTierText = NewText("Tier", rankBanner, "SQUIRE", 26f * u, new Color(0.75f, 0.85f, 1f, 0.9f), TextAlignmentOptions.Center);
            PlaceTopLeft(rankTierText.rectTransform, txL, bnH * 0.13f, txW, 32f * u);
            rankTierText.characterSpacing = 6f;
            rankTitleText = NewText("Title", rankBanner, "BRONZE I", 58f * u, Color.white, TextAlignmentOptions.Center);
            PlaceTopLeft(rankTitleText.rectTransform, txL, bnH * 0.13f + 28f * u, txW, 66f * u);
            rankTitleText.enableAutoSizing = true;
            rankTitleText.fontSizeMin = 28f * u;
            rankTitleText.fontSizeMax = 58f * u;

            // Trophy progress bar towards the next division (kept inside the plate, above its gold border)
            float pillW = txW * 0.74f, pillH = 38f * u;
            var pill = NewRect("TrophyPill", rankBanner);
            PlaceTopLeft(pill, txL + (txW - pillW) * 0.5f + 14f * u, bnH * 0.13f + 28f * u + 68f * u, pillW, pillH);
            Framed(pill, 19f, 3f, new Color(1f, 0.84f, 0.42f), new Color(0.72f, 0.42f, 0.06f), new Color(0.01f, 0.04f, 0.16f), new Color(0.03f, 0.08f, 0.26f));
            var fillHolder = NewRect("FillArea", pill);
            Stretch(fillHolder, 5f * u, 5f * u, 5f * u, 5f * u);
            rankPillFill = NewRect("Fill", fillHolder);
            rankPillFill.anchorMin = Vector2.zero;
            rankPillFill.anchorMax = new Vector2(0f, 1f);
            rankPillFill.offsetMin = rankPillFill.offsetMax = Vector2.zero;
            var fillImg = NewRounded("Img", rankPillFill, Color.white, 14f);
            Stretch(fillImg.rectTransform);
            fillImg.gameObject.AddComponent<CardsScreenGradient>().Set(new Color(1f, 0.86f, 0.36f), new Color(0.92f, 0.52f, 0.06f));
            var pillIcon = NewIcon("Icon", pill, trophySprite, new Vector2(52f, 52f) * u);
            pillIcon.anchorMin = pillIcon.anchorMax = new Vector2(0f, 0.5f);
            pillIcon.anchoredPosition = new Vector2(-4f * u, 2f * u);
            rankPillValue = NewText("Value", pill, "0", 26f * u, Color.white, TextAlignmentOptions.Center);
            Stretch(rankPillValue.rectTransform, 26f * u, 0f, 10f * u, 0f);
            rankPillValue.enableAutoSizing = true;
            rankPillValue.fontSizeMin = 16f * u;
            rankPillValue.fontSizeMax = 26f * u;

            nextRankText = NewText("Next", card, "", 28f * u, TextDim, TextAlignmentOptions.Center);
            PlaceTopLeft(nextRankText.rectTransform, 40f * u, 312f * u + bnH + 22f * u, rbW, 40f * u);

            // ── Stats grid (2 x 3) ──
            float gap = 16f * u;
            float tileW = (rbW - gap * 2f) / 3f;
            float tileH = 128f * u;
            float row1 = 312f * u + bnH + 78f * u;
            float row2 = row1 + tileH + gap;
            trophiesValue = BuildStatTile(card, 0, 40f * u, row1, tileW, tileH, "TROPHIES", trophySprite, out trophyIcon);
            bestValue = BuildStatTile(card, 1, 40f * u + tileW + gap, row1, tileW, tileH, "BEST", crownSprite, out _);
            cardsValue = BuildStatTile(card, 2, 40f * u + (tileW + gap) * 2f, row1, tileW, tileH, "CARDS", cardsIconSprite, out _);
            winsValue = BuildStatTile(card, 3, 40f * u, row2, tileW, tileH, "WINS", swordsSprite != null ? swordsSprite : crownSprite, out _);
            winRateValue = BuildStatTile(card, 4, 40f * u + tileW + gap, row2, tileW, tileH, "WIN RATE", shieldSprite, out _);
            threeStarValue = BuildStatTile(card, 5, 40f * u + (tileW + gap) * 2f, row2, tileW, tileH, "3-STAR WINS", starSprite != null ? starSprite : crownSprite, out _);
            cardH = row2 + tileH + 36f * u;
            card.sizeDelta = new Vector2(cw, cardH);

            AddReveal(card, 0.18f, new Vector2(0f, -110f * u), 1.15f);
            return y + cardH;
        }

        private TextMeshProUGUI BuildStatTile(RectTransform card, int index, float x, float y, float w, float h, string label, Sprite icon, out RectTransform iconRT)
        {
            var tile = NewRect("Stat" + index, card);
            PlaceTopLeft(tile, x, y, w, h);
            Framed(tile, 22f, 3f, new Color(0.52f, 0.74f, 1f), new Color(0.22f, 0.40f, 0.86f), new Color(0.07f, 0.15f, 0.42f), new Color(0.02f, 0.06f, 0.22f));

            var lab = NewText("Label", tile, label, 24f * u, new Color(0.82f, 0.90f, 1f), TextAlignmentOptions.Center);
            PlaceTop(lab.rectTransform, 0f, 12f * u, w, 32f * u);

            float iconBox = 58f * u;
            iconRT = NewIcon("Icon", tile, icon, new Vector2(iconBox, iconBox));
            iconRT.anchorMin = iconRT.anchorMax = new Vector2(0f, 0f);
            iconRT.anchoredPosition = new Vector2(16f * u + iconBox * 0.5f, 18f * u + iconBox * 0.5f);

            var val = NewText("Value", tile, "0", 44f * u, Color.white, TextAlignmentOptions.MidlineRight);
            val.rectTransform.anchorMin = new Vector2(0f, 0f);
            val.rectTransform.anchorMax = new Vector2(1f, 0f);
            val.rectTransform.pivot = new Vector2(0.5f, 0f);
            val.rectTransform.offsetMin = new Vector2(16f * u + iconBox + 4f * u, 14f * u);
            val.rectTransform.offsetMax = new Vector2(-14f * u, 14f * u + 70f * u);
            val.enableAutoSizing = true;
            val.fontSizeMin = 20f * u;
            val.fontSizeMax = 44f * u;
            return val;
        }

        private RectTransform BuildPencilButton(Transform parent, float size)
        {
            var btn = NewRect("EditButton", parent);
            var bg = NewImage("Bg", btn, circleSprite, new Color(0.02f, 0.07f, 0.24f, 0.85f), false);
            Stretch(bg.rectTransform);
            var rim = NewImage("Rim", btn, circleSprite, new Color(0.62f, 0.82f, 1f, 0.55f), false);
            Stretch(rim.rectTransform);
            rim.type = Image.Type.Simple;
            rim.color = new Color(0.62f, 0.82f, 1f, 0.0f);

            var icon = NewRect("Pencil", btn);
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
            icon.sizeDelta = new Vector2(size, size);
            icon.localRotation = Quaternion.Euler(0f, 0f, 45f);
            float s = size * 0.78f;

            var tip = NewImage("Tip", icon, null, new Color(0.96f, 0.84f, 0.62f), false);
            tip.rectTransform.anchorMin = tip.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            tip.rectTransform.sizeDelta = new Vector2(s * 0.26f, s * 0.26f);
            tip.rectTransform.anchoredPosition = new Vector2(0f, -s * 0.33f);
            tip.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var lead = NewImage("Lead", icon, null, new Color(0.22f, 0.22f, 0.28f), false);
            lead.rectTransform.anchorMin = lead.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            lead.rectTransform.sizeDelta = new Vector2(s * 0.11f, s * 0.11f);
            lead.rectTransform.anchoredPosition = new Vector2(0f, -s * 0.435f);
            lead.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var body = NewImage("Body", icon, null, new Color(1f, 0.82f, 0.22f), false);
            body.rectTransform.anchorMin = body.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            body.rectTransform.sizeDelta = new Vector2(s * 0.26f, s * 0.58f);
            body.rectTransform.anchoredPosition = new Vector2(0f, s * 0.02f);
            var stripe = NewImage("Stripe", icon, null, new Color(0.9f, 0.55f, 0.08f), false);
            stripe.rectTransform.anchorMin = stripe.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            stripe.rectTransform.sizeDelta = new Vector2(s * 0.07f, s * 0.58f);
            stripe.rectTransform.anchoredPosition = new Vector2(s * 0.07f, s * 0.02f);
            var band = NewImage("Band", icon, null, new Color(0.78f, 0.82f, 0.9f), false);
            band.rectTransform.anchorMin = band.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            band.rectTransform.sizeDelta = new Vector2(s * 0.26f, s * 0.07f);
            band.rectTransform.anchoredPosition = new Vector2(0f, s * 0.34f);
            var eraser = NewImage("Eraser", icon, null, new Color(1f, 0.5f, 0.6f), false);
            eraser.rectTransform.anchorMin = eraser.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            eraser.rectTransform.sizeDelta = new Vector2(s * 0.26f, s * 0.13f);
            eraser.rectTransform.anchoredPosition = new Vector2(0f, s * 0.415f);

            MakeButton(btn, OpenEditor);
            return btn;
        }

        // ───────────────────────────── Clan ─────────────────────────────

        private float BuildClanPanel(float y)
        {
            float h = 212f * u;
            var panel = NewRect("ClanPanel", content);
            PlaceTop(panel, 0f, y, cw, h);

            var shadow = NewRounded("Shadow", panel, new Color(0f, 0.02f, 0.1f, 0.55f), 32f);
            Stretch(shadow.rectTransform, 0f, -8f * u, 0f, 8f * u);
            Framed(panel, 32f, 5f, GoldHi, GoldLo, NavyHi, NavyLo);

            // Emblem
            float em = 118f * u;
            var emblem = NewRect("Emblem", panel);
            emblem.anchorMin = emblem.anchorMax = new Vector2(0f, 0.5f);
            emblem.sizeDelta = new Vector2(em, em);
            emblem.anchoredPosition = new Vector2(34f * u + em * 0.5f, 0f);
            var eBg = NewImage("Bg", emblem, circleSprite, new Color(0.08f, 0.20f, 0.52f, 1f), false);
            Stretch(eBg.rectTransform);
            var eRim = NewImage("Rim", emblem, circleSprite, Color.white, false);
            Stretch(eRim.rectTransform, -4f * u, -4f * u, -4f * u, -4f * u);
            eRim.rectTransform.SetAsFirstSibling();
            eRim.gameObject.AddComponent<CardsScreenGradient>().Set(new Color(0.7f, 0.88f, 1f), new Color(0.28f, 0.5f, 0.92f));
            var shield = NewIcon("Shield", emblem, shieldSprite, new Vector2(em * 0.62f, em * 0.62f));
            shield.anchorMin = shield.anchorMax = new Vector2(0.5f, 0.5f);
            shield.anchoredPosition = Vector2.zero;

            float tx = 34f * u + em + 30f * u;
            var cap = NewText("Caption", panel, "CLAN", 24f * u, Gold, TextAlignmentOptions.MidlineLeft);
            PlaceTopLeft(cap.rectTransform, tx, 22f * u, 300f * u, 32f * u);
            cap.characterSpacing = 6f;

            clanNameText = NewText("ClanName", panel, "No Clan", 50f * u, TextWhite, TextAlignmentOptions.MidlineLeft);
            PlaceTopLeft(clanNameText.rectTransform, tx, 52f * u, cw - tx - 250f * u, 66f * u);
            clanNameText.enableAutoSizing = true;
            clanNameText.fontSizeMin = 26f * u;
            clanNameText.fontSizeMax = 50f * u;

            clanSubText = NewText("ClanSub", panel, "Join a clan to battle together", 28f * u, TextDim, TextAlignmentOptions.MidlineLeft);
            PlaceTopLeft(clanSubText.rectTransform, tx, 124f * u, cw - tx - 40f * u, 40f * u);
            clanSubText.fontStyle = FontStyles.Normal;

            // Role chip (when in a clan)
            clanRoleChip = NewRect("RoleChip", panel);
            PlaceTopLeft(clanRoleChip, tx, 118f * u, 200f * u, 40f * u);
            clanRoleBg = NewRounded("Bg", clanRoleChip, Gold, 20f);
            Stretch(clanRoleBg.rectTransform);
            clanRoleText = NewText("Text", clanRoleChip, "Member", 26f * u, Color.white, TextAlignmentOptions.Center);
            Stretch(clanRoleText.rectTransform);

            // Trophy box (right)
            clanTrophyBox = NewRect("ClanTrophies", panel);
            clanTrophyBox.anchorMin = clanTrophyBox.anchorMax = new Vector2(1f, 0.5f);
            clanTrophyBox.sizeDelta = new Vector2(210f * u, 100f * u);
            clanTrophyBox.anchoredPosition = new Vector2(-30f * u - 105f * u, 0f);
            var tbBg = NewRounded("Bg", clanTrophyBox, new Color(0.01f, 0.05f, 0.18f, 0.7f), 24f);
            Stretch(tbBg.rectTransform);
            var tIcon = NewIcon("Icon", clanTrophyBox, trophySprite, new Vector2(60f, 60f) * u);
            tIcon.anchorMin = tIcon.anchorMax = new Vector2(0f, 0.5f);
            tIcon.anchoredPosition = new Vector2(14f * u + 30f * u, 0f);
            clanTrophiesText = NewText("Value", clanTrophyBox, "0", 40f * u, Color.white, TextAlignmentOptions.MidlineRight);
            Stretch(clanTrophiesText.rectTransform, 80f * u, 0f, 16f * u, 0f);
            clanTrophiesText.enableAutoSizing = true;
            clanTrophiesText.fontSizeMin = 20f * u;
            clanTrophiesText.fontSizeMax = 40f * u;

            // "Soon" chip (no clan)
            clanSoonChip = NewRect("SoonChip", panel);
            clanSoonChip.anchorMin = clanSoonChip.anchorMax = new Vector2(1f, 0.5f);
            clanSoonChip.sizeDelta = new Vector2(170f * u, 70f * u);
            clanSoonChip.anchoredPosition = new Vector2(-30f * u - 85f * u, 0f);
            var scBg = NewRounded("Bg", clanSoonChip, new Color(0.10f, 0.20f, 0.45f, 0.9f), 22f);
            Stretch(scBg.rectTransform);
            if (lockSprite != null)
            {
                var lk = NewIcon("Lock", clanSoonChip, lockSprite, new Vector2(34f, 34f) * u);
                lk.anchorMin = lk.anchorMax = new Vector2(0f, 0.5f);
                lk.anchoredPosition = new Vector2(30f * u, 0f);
            }
            var scText = NewText("Text", clanSoonChip, "SOON", 30f * u, TextDim, TextAlignmentOptions.Center);
            Stretch(scText.rectTransform, lockSprite != null ? 40f * u : 0f, 0f, 0f, 0f);

            AddReveal(panel, 0.34f, new Vector2(-90f * u, 0f), 1.1f);
            return y + h;
        }

        // ───────────────────────────── Battle deck ─────────────────────────────

        private float BuildDeckSection(float y)
        {
            // Section heading
            float headH = 84f * u;
            var head = NewRect("DeckHeading", content);
            PlaceTop(head, 0f, y, cw, headH);
            head.sizeDelta = new Vector2(cw * 0.66f, headH);
            Framed(head, 30f, 5f, GoldHi, GoldLo, RoyalHi, RoyalLo);
            if (swordsSprite != null)
            {
                var sw = NewIcon("Swords", head, swordsSprite, new Vector2(62f, 62f) * u);
                sw.anchorMin = sw.anchorMax = new Vector2(0f, 0.5f);
                sw.anchoredPosition = new Vector2(52f * u, 0f);
            }
            var headText = NewText("Text", head, "BATTLE DECK", 42f * u, TextWhite, TextAlignmentOptions.Center);
            Stretch(headText.rectTransform, 80f * u, 0f, 24f * u, 0f);
            headText.characterSpacing = 3f;
            AddReveal(head, 0.5f, new Vector2(0f, -40f * u), 1.1f);
            y += headH + 20f * u;

            // Deck panel
            float pad = 24f * u;
            float gap = 16f * u;
            float tileW = (cw - pad * 2f - gap * (COLUMNS - 1)) / COLUMNS;
            float tileH = tileW * 1.30f + tileW * 0.24f;        // plate + name
            float labelH = 40f * u;
            float armyRows = 1, powerRows = 2;
            float panelH = pad + labelH + armyRows * (tileH + gap) + labelH + powerRows * (tileH + gap) - gap + 30f * u;

            var panel = NewRect("DeckPanel", content);
            PlaceTop(panel, 0f, y, cw, panelH);
            var shadow = NewRounded("Shadow", panel, new Color(0f, 0.02f, 0.1f, 0.55f), 34f);
            Stretch(shadow.rectTransform, 0f, -8f * u, 0f, 8f * u);
            Framed(panel, 34f, 5f, GoldHi, GoldLo, NavyHi, NavyLo);

            float cy = pad;
            // Army label
            BuildGroupLabel(panel, "ARMY", pad, cy, labelH);
            cy += labelH;
            for (int i = 0; i < CardInventoryManager.MAX_UNIT_SLOTS; i++)
            {
                var t = BuildTile(panel, tileW, tileH);
                PlaceTopLeft(t.root, pad + i * (tileW + gap), cy, tileW, tileH);
                unitTiles.Add(t);
            }
            cy += tileH + gap + 6f * u;

            BuildGroupLabel(panel, "POWERS", pad, cy, labelH);
            cy += labelH;
            for (int i = 0; i < CardInventoryManager.MAX_POWERUP_SLOTS; i++)
            {
                int row = i / COLUMNS, col = i % COLUMNS;
                var t = BuildTile(panel, tileW, tileH);
                PlaceTopLeft(t.root, pad + col * (tileW + gap), cy + row * (tileH + gap), tileW, tileH);
                powerTiles.Add(t);
            }
            cy += powerRows * (tileH + gap) - gap + 26f * u;

            AddReveal(panel, 0.6f, new Vector2(0f, -90f * u), 1.1f);
            y += panelH + 28f * u;

            // Edit deck button
            float btnH = 120f * u;
            var edit = BuildGoldButton(content, "EDIT DECK", new Vector2(cw * 0.62f, btnH), OpenDeckEditor);
            PlaceTop(edit, 0f, y, cw * 0.62f, btnH);
            AddReveal(edit, 0.8f, new Vector2(0f, -60f * u), 1.3f);
            return y + btnH;
        }

        private void BuildGroupLabel(RectTransform parent, string text, float x, float y, float h)
        {
            var t = NewText("Label_" + text, parent, text, 28f * u, Gold, TextAlignmentOptions.MidlineLeft);
            PlaceTopLeft(t.rectTransform, x + 4f * u, y, 400f * u, h);
            t.characterSpacing = 6f;
        }

        private Tile BuildTile(Transform parent, float w, float h)
        {
            var t = new Tile();
            t.root = NewRect("Tile", parent);
            t.root.sizeDelta = new Vector2(w, h);
            t.visual = NewRect("Visual", t.root);
            Stretch(t.visual);
            t.visual.pivot = new Vector2(0.5f, 0.5f);

            float artH = w * 1.10f;
            float bandH = w * 0.26f;
            float bandTop = artH - bandH * 0.40f;
            float plateH = bandTop + bandH;

            t.plate = NewRounded("Plate", t.visual, Color.grey, 16f * w / (cw * 0.25f));
            PlaceTop(t.plate.rectTransform, 0f, 0f, w, plateH);

            t.art = NewImage("Art", t.visual, null, Color.white, true);
            PlaceTop(t.art.rectTransform, 0f, w * 0.035f, w * 0.93f, artH - w * 0.035f);
            t.artFallback = NewText("Name", t.art.rectTransform, "", w * 0.13f, TextWhite, TextAlignmentOptions.Center);
            Stretch(t.artFallback.rectTransform, w * 0.05f, 0f, w * 0.05f, 0f);
            t.artFallback.textWrappingMode = TextWrappingModes.Normal;

            t.levelBand = NewRounded("LevelBand", t.visual, Color.grey, 12f * w / (cw * 0.25f));
            PlaceTop(t.levelBand.rectTransform, 0f, bandTop, w * 0.97f, bandH);
            var gloss = NewRounded("Gloss", t.levelBand.rectTransform, new Color(1f, 1f, 1f, 0.18f), 10f * w / (cw * 0.25f));
            gloss.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            gloss.rectTransform.anchorMax = Vector2.one;
            gloss.rectTransform.offsetMin = new Vector2(w * 0.02f, 0f);
            gloss.rectTransform.offsetMax = new Vector2(-w * 0.02f, -w * 0.015f);
            t.levelText = NewText("Level", t.levelBand.rectTransform, "Level 1", w * 0.16f, TextWhite, TextAlignmentOptions.Center);
            Stretch(t.levelText.rectTransform);

            // elixir cost drop
            t.costBadge = NewRect("Cost", t.visual);
            t.costBadge.anchorMin = t.costBadge.anchorMax = new Vector2(0f, 1f);
            t.costBadge.sizeDelta = new Vector2(w * 0.34f, w * 0.34f);
            t.costBadge.anchoredPosition = new Vector2(w * 0.06f + w * 0.17f, -w * 0.07f - w * 0.17f);
            var drop = NewImage("Drop", t.costBadge, elixirSprite, elixirSprite != null ? Color.white : new Color(0.85f, 0.2f, 0.85f), true);
            Stretch(drop.rectTransform);
            t.costText = NewText("Value", t.costBadge, "3", w * 0.17f, Color.white, TextAlignmentOptions.Center);
            Stretch(t.costText.rectTransform, 0f, 0f, 0f, w * 0.06f);

            // card name under the tile
            t.nameText = NewText("CardName", t.visual, "", w * 0.145f, TextWhite, TextAlignmentOptions.Top);
            PlaceTop(t.nameText.rectTransform, 0f, plateH + w * 0.04f, w * 1.08f, w * 0.22f);
            t.nameText.enableAutoSizing = true;
            t.nameText.fontSizeMin = w * 0.08f;
            t.nameText.fontSizeMax = w * 0.145f;

            // empty slot
            t.emptyRoot = NewRect("Empty", t.visual);
            PlaceTop(t.emptyRoot, 0f, 0f, w, plateH);
            var ebg = NewRounded("Bg", t.emptyRoot, new Color(0.02f, 0.07f, 0.22f, 0.75f), 18f * w / (cw * 0.25f));
            Stretch(ebg.rectTransform);
            var erim = NewRounded("Rim", t.emptyRoot, new Color(0.45f, 0.65f, 1f, 0.5f), 18f * w / (cw * 0.25f));
            Stretch(erim.rectTransform);
            erim.fillCenter = false;
            var plus = NewText("Plus", t.emptyRoot, "+", w * 0.5f, new Color(0.55f, 0.75f, 1f, 0.8f), TextAlignmentOptions.Center);
            Stretch(plus.rectTransform, 0f, w * 0.15f, 0f, 0f);
            var et = NewText("Text", t.emptyRoot, "Empty", w * 0.14f, TextDim, TextAlignmentOptions.Bottom);
            Stretch(et.rectTransform, 0f, w * 0.12f, 0f, 0f);

            // tap = little squish, long term could open the card
            var hit = t.root.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            var fb = t.root.gameObject.AddComponent<ShopPressFeedback>();
            return t;
        }

        private void ApplyTile(Tile t, CardDefinition def)
        {
            bool empty = def == null;
            t.emptyRoot.gameObject.SetActive(empty);
            t.plate.gameObject.SetActive(!empty);
            t.art.gameObject.SetActive(!empty);
            t.levelBand.gameObject.SetActive(!empty);
            t.costBadge.gameObject.SetActive(!empty);
            t.nameText.gameObject.SetActive(!empty);
            if (empty) return;

            var entry = inventory != null ? inventory.GetEntry(def.cardId) : null;
            int level = entry != null ? Mathf.Max(1, entry.level) : 1;
            bool max = level >= CardDefinition.MAX_CARD_LEVEL;

            t.art.sprite = def.cardSprite;
            t.art.color = def.cardSprite != null ? Color.white : new Color(0.15f, 0.2f, 0.35f, 1f);
            t.artFallback.text = def.cardSprite == null ? def.displayName : "";
            t.plate.color = RarityPlate(def.rarity);
            t.levelBand.color = RarityBand(def.rarity);
            t.levelText.text = max ? "Max Level" : "Level " + level;
            t.costText.text = def.elixirCost.ToString();
            t.nameText.text = def.displayName;
        }

        // ═════════════════════════════ Refresh ═════════════════════════════

        private void Refresh()
        {
            if (pageRoot == null) return;

            if (coinText != null) coinText.text = PlayerProfileData.Format(PlayerWallet.Coins);
            if (gemText != null) gemText.text = PlayerProfileData.Format(PlayerWallet.Gems);

            // Identity
            nameText.text = PlayerProfileData.PlayerName;
            tagText.text = PlayerProfileData.PlayerTag;
            ApplyAvatar();

            // Level / XP
            PlayerProfileData.ComputeLevel(inventory, out dataLevel, out dataXp, out dataXpNeeded);
            levelText.text = dataLevel.ToString();
            bool maxLevel = dataLevel >= PlayerProfileData.MAX_LEVEL;
            xpText.text = maxLevel ? "MAX" : PlayerProfileData.Format(dataXp) + " / " + PlayerProfileData.Format(dataXpNeeded) + " XP";
            SetXpFraction(maxLevel ? 1f : Mathf.Clamp01(dataXp / (float)Mathf.Max(1, dataXpNeeded)));

            // Trophies / rank
            int trophies = PlayerProfileData.Trophies;
            var rank = PlayerProfileData.GetRank(trophies);
            ApplyRank(rank, trophies);
            trophiesValue.text = PlayerProfileData.Format(trophies);
            bestValue.text = PlayerProfileData.Format(PlayerProfileData.BestTrophies);

            int found = inventory != null ? inventory.GetFoundCards().Count : 0;
            int total = inventory != null ? inventory.TotalCardCount : 0;
            cardsValue.text = found + "/" + total;
            winsValue.text = PlayerProfileData.Format(PlayerProfileData.Wins);
            winRateValue.text = PlayerProfileData.WinRatePercent + "%";
            threeStarValue.text = PlayerProfileData.Format(PlayerProfileData.ThreeStarWins);

            RefreshClan();
            RefreshDeck();
        }

        private void ApplyAvatar()
        {
            Sprite sp = null;
            int idx = PlayerProfileData.AvatarIndex;
            if (avatarSprites != null && avatarSprites.Length > 0) sp = avatarSprites[Mathf.Clamp(idx, 0, avatarSprites.Length - 1)];
            if (sp == null) sp = kingPortraitSprite;
            avatarImage.sprite = sp;
            avatarImage.enabled = sp != null;
            avatarFallbackText.gameObject.SetActive(sp == null);
            if (sp == null)
            {
                string n = PlayerProfileData.PlayerName;
                avatarFallbackText.text = n.Length > 0 ? n.Substring(0, 1).ToUpperInvariant() : "?";
            }
            else
            {
                var fit = avatarImage.GetComponent<AspectRatioFitter>();
                if (fit != null) fit.aspectRatio = sp.rect.width / Mathf.Max(1f, sp.rect.height);
            }
        }

        /// <summary>Badge for a rank slot (0..27), falling back to rankBadgeSprite if that slot is not assigned.</summary>
        public Sprite RankSpriteFor(int badgeIndex)
        {
            if (rankBadgeByDivision != null && badgeIndex >= 0 && badgeIndex < rankBadgeByDivision.Length
                && rankBadgeByDivision[badgeIndex] != null)
                return rankBadgeByDivision[badgeIndex];
            return rankBadgeSprite;
        }

        private void ApplyRank(RankTier rank, int trophies)
        {
            rankTierText.text = rank.nextAt > 0 || rank.label != rank.tierName ? "LEAGUE" : "TOP LEAGUE";
            rankTitleText.text = rank.label.ToUpperInvariant();
            rankTitleText.color = Color.Lerp(Color.white, rank.top, 0.55f);
            float frac = 1f;
            if (rank.nextAt > rank.minTrophies)
            {
                frac = Mathf.Clamp01((trophies - rank.minTrophies) / (float)(rank.nextAt - rank.minTrophies));
                rankPillValue.text = PlayerProfileData.Format(trophies) + " / " + PlayerProfileData.Format(rank.nextAt);
            }
            else rankPillValue.text = PlayerProfileData.Format(trophies);
            if (rankPillFill != null)
            {
                rankPillFill.gameObject.SetActive(frac > 0.001f);
                rankPillFill.anchorMax = new Vector2(Mathf.Max(0.06f, frac), 1f);
            }
            if (rankBadgeImage != null)
            {
                // Show the badge for this player's league + division (same slot everywhere the rank is shown).
                var s = RankSpriteFor(rank.badgeIndex);
                if (s != null) rankBadgeImage.sprite = s;
                rankBadgeImage.color = Color.Lerp(Color.white, rank.top, 0.35f);
            }
            if (rankBadgeGlow != null) rankBadgeGlow.color = new Color(rank.top.r, rank.top.g, rank.top.b, 0.5f);
            nextRankText.text = rank.nextAt > 0
                ? "<color=#FFD54A>" + PlayerProfileData.Format(rank.nextAt - trophies) + "</color> more trophies to reach " + rank.nextLabel
                : "Top rank reached!";
        }

        private void SetXpFraction(float f)
        {
            if (xpFillRT == null) return;
            // keep a minimum so the capsule never collapses visually
            f = Mathf.Clamp01(f);
            float min = 0.04f;
            float v = Mathf.Lerp(min, 1f, f);
            xpFillRT.anchorMax = new Vector2(v, 1f);
        }

        private void RefreshClan()
        {
            var clan = PlayerProfileData.GetClan();
            clanNameText.text = clan.hasClan ? clan.name : "No Clan";
            clanNameText.color = clan.hasClan ? TextWhite : TextDim;
            clanSubText.gameObject.SetActive(!clan.hasClan);
            clanSoonChip.gameObject.SetActive(!clan.hasClan);
            clanRoleChip.gameObject.SetActive(clan.hasClan);
            clanTrophyBox.gameObject.SetActive(clan.hasClan);
            if (clan.hasClan)
            {
                clanRoleText.text = clan.role;
                clanRoleBg.color = RoleColor(clan.role);
                clanTrophiesText.text = PlayerProfileData.Format(clan.trophies);
            }
        }

        private static Color RoleColor(string role)
        {
            switch ((role ?? "").ToLowerInvariant())
            {
                case "leader": return new Color(0.95f, 0.66f, 0.08f);
                case "co-leader": return new Color(0.92f, 0.42f, 0.18f);
                case "elder": return new Color(0.22f, 0.72f, 0.32f);
                default: return new Color(0.20f, 0.52f, 0.95f);
            }
        }

        private void RefreshDeck()
        {
            if (unitTiles.Count == 0) return;
            for (int i = 0; i < unitTiles.Count; i++)
                ApplyTile(unitTiles[i], DefAt(inventory != null ? inventory.UnitDeck : null, i));
            for (int i = 0; i < powerTiles.Count; i++)
                ApplyTile(powerTiles[i], DefAt(inventory != null ? inventory.PowerupDeck : null, i));

            dataAvgElixir = inventory != null ? inventory.GetDeckElixirAverage() : 0f;
            if (elixirValue != null) elixirValue.text = dataAvgElixir.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        }

        private CardDefinition DefAt(IReadOnlyList<string> ids, int index)
        {
            if (ids == null || index < 0 || index >= ids.Count || library == null) return null;
            return string.IsNullOrEmpty(ids[index]) ? null : library.GetCard(ids[index]);
        }

        // ═════════════════════════════ Animation ═════════════════════════════

        private void AddReveal(RectTransform rt, float delay, Vector2 from, float overshoot)
        {
            var cg = rt.gameObject.GetComponent<CanvasGroup>();
            if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();
            reveals.Add(new Reveal { rt = rt, cg = cg, final = rt.anchoredPosition, from = from, delay = delay, overshoot = overshoot });
        }

        private void KillIdle()
        {
            foreach (var t in idleTweens) if (t != null && t.IsActive()) t.Kill();
            idleTweens.Clear();
            if (xpTween != null && xpTween.IsActive()) xpTween.Kill();
        }

        private void PlayEntrance()
        {
            KillIdle();

            // header drops in
            header.DOKill();
            float hh = header.sizeDelta.y;
            header.anchoredPosition = new Vector2(0f, hh);
            header.DOAnchorPosY(0f, 0.4f).SetEase(Ease.OutCubic).SetUpdate(true);

            foreach (var r in reveals)
            {
                r.rt.DOKill();
                r.cg.DOKill();
                r.cg.alpha = 0f;
                r.rt.anchoredPosition = r.final + r.from;
                r.rt.DOAnchorPos(r.final, 0.55f).SetDelay(r.delay).SetEase(Ease.OutBack, r.overshoot).SetUpdate(true);
                r.cg.DOFade(1f, 0.35f).SetDelay(r.delay).SetUpdate(true);
            }

            // avatar + badge pop
            Pop(avatarHolder, 0.32f, 0.5f);
            Pop(levelBadge, 0.55f, 0.6f);
            Pop(pencil, 0.62f, 0.4f);

            // rank banner wipes in
            rankBanner.DOKill();
            rankBanner.localScale = new Vector3(0.2f, 1f, 1f);
            rankBanner.DOScale(Vector3.one, 0.5f).SetDelay(0.4f).SetEase(Ease.OutBack, 1.4f).SetUpdate(true);
            foreach (var sp in sparkles) sp.DOKill();
            for (int i = 0; i < sparkles.Count; i++)
            {
                var sp = sparkles[i];
                sp.color = new Color(1f, 0.95f, 0.7f, 0f);
                idleTweens.Add(sp.DOFade(1f, 0.7f).SetDelay(0.6f + 0.35f * i).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true));
                idleTweens.Add(sp.rectTransform.DORotate(new Vector3(0f, 0f, 90f), 2.4f, RotateMode.FastBeyond360).SetRelative().SetLoops(-1, LoopType.Incremental).SetEase(Ease.Linear).SetUpdate(true));
            }
            crownIcon.DOKill();
            crownIcon.localScale = Vector3.zero;
            crownIcon.DOScale(Vector3.one, 0.5f).SetDelay(0.78f).SetEase(Ease.OutBack, 2.4f).SetUpdate(true);

            // counters
            CountUp(trophiesValue, PlayerProfileData.Trophies, 0.7f, 0.6f, "");
            CountUp(bestValue, PlayerProfileData.BestTrophies, 0.7f, 0.7f, "");
            CountUpCards(0.8f);
            CountUp(winsValue, PlayerProfileData.Wins, 0.6f, 0.85f, "");
            CountUp(winRateValue, PlayerProfileData.WinRatePercent, 0.6f, 0.9f, "%");
            CountUp(threeStarValue, PlayerProfileData.ThreeStarWins, 0.6f, 0.95f, "");
            CountUp(levelText, dataLevel, 0.6f, 0.6f, "");
            CountUp(coinText, PlayerWallet.Coins, 0.6f, 0.3f, "");
            CountUp(gemText, PlayerWallet.Gems, 0.6f, 0.3f, "");

            // xp bar fills
            bool maxLevel = dataLevel >= PlayerProfileData.MAX_LEVEL;
            float target = maxLevel ? 1f : Mathf.Clamp01(dataXp / (float)Mathf.Max(1, dataXpNeeded));
            SetXpFraction(0f);
            xpTween = DOVirtual.Float(0f, target, 0.9f, SetXpFraction).SetDelay(0.65f).SetEase(Ease.OutCubic).SetUpdate(true);

            // deck cards pop in with a stagger
            int k = 0;
            foreach (var t in unitTiles) PopTile(t, 0.75f + 0.05f * k++);
            foreach (var t in powerTiles) PopTile(t, 0.75f + 0.05f * k++);

            // elixir count-up
            if (elixirValue != null)
                DOVirtual.Float(0f, dataAvgElixir, 0.8f, v => { if (elixirValue != null) elixirValue.text = v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture); })
                    .SetDelay(1.0f).SetEase(Ease.OutCubic).SetUpdate(true);

            // idle loops
            if (avatarGlow != null)
            {
                avatarGlow.DOKill();
                idleTweens.Add(avatarGlow.DOFade(0.18f, 1.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true));
            }
            idleTweens.Add(crownIcon.DOScale(1.06f, 1.4f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetDelay(1.4f).SetUpdate(true));
            if (trophyIcon != null)
                idleTweens.Add(trophyIcon.DOAnchorPosY(trophyIcon.anchoredPosition.y + 6f * u, 1.1f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true));
            if (elixirDrop != null)
                idleTweens.Add(elixirDrop.DOScale(1.12f, 0.9f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true));
        }

        private void Pop(RectTransform rt, float delay, float duration)
        {
            if (rt == null) return;
            rt.DOKill();
            rt.localScale = Vector3.zero;
            rt.DOScale(Vector3.one, duration).SetDelay(delay).SetEase(Ease.OutBack, 2.2f).SetUpdate(true);
        }

        private void PopTile(Tile t, float delay)
        {
            t.visual.DOKill();
            t.visual.localScale = Vector3.zero;
            t.visual.DOScale(Vector3.one, 0.4f).SetDelay(delay).SetEase(Ease.OutBack, 2.4f).SetUpdate(true);
        }

        private void CountUp(TextMeshProUGUI text, int target, float duration, float delay, string suffix)
        {
            if (text == null) return;
            text.text = "0" + suffix;
            DOVirtual.Float(0f, target, duration, v => { if (text != null) text.text = PlayerProfileData.Format(Mathf.RoundToInt(v)) + suffix; })
                .SetDelay(delay).SetEase(Ease.OutCubic).SetUpdate(true);
        }

        private void CountUpCards(float delay)
        {
            if (inventory == null || cardsValue == null) return;
            int found = inventory.GetFoundCards().Count;
            int total = inventory.TotalCardCount;
            cardsValue.text = "0/" + total;
            DOVirtual.Float(0f, found, 0.6f, v => { if (cardsValue != null) cardsValue.text = Mathf.RoundToInt(v) + "/" + total; })
                .SetDelay(delay).SetEase(Ease.OutCubic).SetUpdate(true);
        }

        // ═════════════════════════════ Edit profile popup ═════════════════════════════

        private void EnsurePopupLayer()
        {
            if (popupLayer != null) return;
            popupLayer = NewRect("[ProfilePopupLayer]", canvasRect);
            Stretch(popupLayer);
        }

        private void OpenEditor()
        {
            if (editorRoot != null || pageRoot == null) return;
            Sfx(clickSound);
            EnsurePopupLayer();
            popupLayer.SetAsLastSibling();

            editorRoot = NewRect("Editor", popupLayer);
            Stretch(editorRoot);
            var group = editorRoot.gameObject.AddComponent<CanvasGroup>();

            var dim = NewImage("Dim", editorRoot, null, new Color(0f, 0.02f, 0.10f, 0.80f), false);
            Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(() => CloseEditor(false));

            pendingAvatar = PlayerProfileData.AvatarIndex;
            int avatarCount = avatarSprites != null ? avatarSprites.Length : 0;
            int cols = 5;
            float panelW = contentW - 70f * u;
            float padX = 36f * u;
            float aGap = 14f * u;
            float cell = (panelW - padX * 2f - aGap * (cols - 1)) / cols;
            int rows = Mathf.Max(1, Mathf.CeilToInt(avatarCount / (float)cols));
            float gridTop = 372f * u;
            float gridH = rows * cell + (rows - 1) * aGap;
            float btnH = 112f * u;
            float panelH = gridTop + gridH + 38f * u + btnH + 40f * u;

            editorPanel = NewRect("Panel", editorRoot);
            editorPanel.anchorMin = editorPanel.anchorMax = new Vector2(0.5f, 0.5f);
            editorPanel.sizeDelta = new Vector2(panelW, panelH);
            editorPanel.anchoredPosition = new Vector2(0f, 30f * u);

            var shadow = NewRounded("Shadow", editorPanel, new Color(0f, 0f, 0.06f, 0.7f), 42f);
            Stretch(shadow.rectTransform, 0f, -14f * u, 0f, 14f * u);
            var body = Framed(editorPanel, 42f, 7f, GoldHi, GoldLo, NavyHi, NavyLo);
            body.raycastTarget = true;

            var title = NewText("Title", editorPanel, "EDIT PROFILE", 56f * u, TextWhite, TextAlignmentOptions.Center);
            PlaceTop(title.rectTransform, 0f, 34f * u, panelW, 80f * u);
            title.characterSpacing = 3f;

            // close X
            var closeBtn = NewRect("Close", editorPanel);
            closeBtn.anchorMin = closeBtn.anchorMax = new Vector2(1f, 1f);
            closeBtn.sizeDelta = new Vector2(76f, 76f) * u;
            closeBtn.anchoredPosition = new Vector2(-18f * u - 38f * u, -18f * u - 38f * u);
            var cBg = NewRounded("Bg", closeBtn, new Color(0.9f, 0.18f, 0.22f), 22f);
            Stretch(cBg.rectTransform);
            var cT = NewText("X", closeBtn, "X", 44f * u, Color.white, TextAlignmentOptions.Center);
            Stretch(cT.rectTransform);
            MakeButton(closeBtn, () => CloseEditor(false));

            // name
            var nLabel = NewText("NameLabel", editorPanel, "PLAYER NAME", 28f * u, Gold, TextAlignmentOptions.MidlineLeft);
            PlaceTopLeft(nLabel.rectTransform, padX, 140f * u, panelW * 0.6f, 36f * u);
            nLabel.characterSpacing = 4f;
            nameCounter = NewText("Counter", editorPanel, "", 28f * u, TextDim, TextAlignmentOptions.MidlineRight);
            PlaceTopLeft(nameCounter.rectTransform, panelW - padX - 200f * u, 140f * u, 200f * u, 36f * u);

            var inputRT = NewRect("Input", editorPanel);
            PlaceTopLeft(inputRT, padX, 184f * u, panelW - padX * 2f, 104f * u);
            var inBg = Framed(inputRT, 26f, 4f, new Color(0.62f, 0.82f, 1f), new Color(0.26f, 0.46f, 0.92f), new Color(0.02f, 0.06f, 0.20f), new Color(0.05f, 0.12f, 0.34f));
            inBg.raycastTarget = true;
            if (crownSprite != null)
            {
                var nIcon = NewIcon("Icon", inputRT, crownSprite, new Vector2(54f, 42f) * u);
                nIcon.anchorMin = nIcon.anchorMax = new Vector2(0f, 0.5f);
                nIcon.anchoredPosition = new Vector2(48f * u, 0f);
            }
            var area = NewRect("TextArea", inputRT);
            Stretch(area, crownSprite != null ? 90f * u : 30f * u, 8f * u, 30f * u, 8f * u);
            area.gameObject.AddComponent<RectMask2D>();
            var inText = NewText("Text", area, "", 50f * u, Color.white, TextAlignmentOptions.MidlineLeft);
            Stretch(inText.rectTransform);
            var inHint = NewText("Placeholder", area, "Enter your name", 46f * u, new Color(0.55f, 0.65f, 0.85f, 0.8f), TextAlignmentOptions.MidlineLeft);
            inHint.fontStyle = FontStyles.Italic;
            Stretch(inHint.rectTransform);

            // Configure the field while inactive: TMP_InputField builds its caret and text hooks in OnEnable,
            // so it must already know its text component / viewport when it first becomes active.
            inputRT.gameObject.SetActive(false);
            nameInput = inputRT.gameObject.AddComponent<TMP_InputField>();
            nameInput.targetGraphic = inBg;
            nameInput.textViewport = area;
            nameInput.textComponent = inText;
            nameInput.placeholder = inHint;
            nameInput.lineType = TMP_InputField.LineType.SingleLine;
            nameInput.characterLimit = PlayerProfileData.MAX_NAME_LENGTH;
            nameInput.caretColor = Color.white;
            nameInput.selectionColor = new Color(1f, 0.80f, 0.30f, 0.35f);
            nameInput.customCaretColor = true;
            nameInput.text = PlayerProfileData.PlayerName;
            nameInput.onValueChanged.AddListener(OnNameEdited);
            nameInput.onSubmit.AddListener(_ => SaveProfile());
            nameInput.contentType = TMP_InputField.ContentType.Standard;
            nameInput.richText = false;
            if (font != null) nameInput.fontAsset = font;
            inputRT.gameObject.SetActive(true);
            OnNameEdited(nameInput.text);

            // avatars
            var aLabel = NewText("AvatarLabel", editorPanel, "CHOOSE AVATAR", 28f * u, Gold, TextAlignmentOptions.MidlineLeft);
            PlaceTopLeft(aLabel.rectTransform, padX, 316f * u, panelW * 0.6f, 36f * u);
            aLabel.characterSpacing = 4f;

            avatarRings.Clear();
            for (int i = 0; i < avatarCount; i++)
            {
                int idx = i;
                int row = i / cols, col = i % cols;
                var c = NewRect("Avatar" + i, editorPanel);
                PlaceTopLeft(c, padX + col * (cell + aGap), gridTop + row * (cell + aGap), cell, cell);
                var ringImg = NewImage("Ring", c, circleSprite, Color.white, false);
                Stretch(ringImg.rectTransform);
                var plate = NewImage("Plate", c, circleSprite, new Color(0.05f, 0.12f, 0.38f), false);
                Stretch(plate.rectTransform, 4f * u, 4f * u, 4f * u, 4f * u);
                var m = NewRect("Mask", c);
                Stretch(m, 7f * u, 7f * u, 7f * u, 7f * u);
                var mi = m.gameObject.AddComponent<Image>();
                mi.sprite = circleSprite;
                var mk = m.gameObject.AddComponent<Mask>();
                mk.showMaskGraphic = false;
                var pic = NewImage("Pic", m, avatarSprites[i], Color.white, false);
                pic.rectTransform.anchorMin = pic.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                pic.rectTransform.sizeDelta = new Vector2(cell, cell);
                var af = pic.gameObject.AddComponent<AspectRatioFitter>();
                af.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                af.aspectRatio = avatarSprites[i] != null ? avatarSprites[i].rect.width / Mathf.Max(1f, avatarSprites[i].rect.height) : 1f;
                avatarRings.Add(ringImg);
                MakeButton(c, () => SelectAvatar(idx, c));
            }
            UpdateAvatarRings();

            // save
            var save = BuildGoldButton(editorPanel, "SAVE", new Vector2(panelW * 0.5f, btnH), SaveProfile);
            PlaceTop(save, 0f, gridTop + gridH + 38f * u, panelW * 0.5f, btnH);

            // pop in
            group.alpha = 0f;
            group.DOFade(1f, 0.2f).SetUpdate(true);
            editorPanel.localScale = Vector3.one * 0.82f;
            editorPanel.DOScale(1f, 0.38f).SetEase(Ease.OutBack, 1.6f).SetUpdate(true);
        }

        private void OnNameEdited(string value)
        {
            if (nameCounter == null) return;
            int len = (value ?? "").Length;
            nameCounter.text = len + "/" + PlayerProfileData.MAX_NAME_LENGTH;
            nameCounter.color = len < PlayerProfileData.MIN_NAME_LENGTH ? new Color(1f, 0.5f, 0.45f) : TextDim;
        }

        private void SelectAvatar(int idx, RectTransform cell)
        {
            pendingAvatar = idx;
            Sfx(clickSound);
            UpdateAvatarRings();
            cell.DOKill();
            cell.localScale = Vector3.one;
            cell.DOPunchScale(Vector3.one * 0.18f, 0.3f, 6, 0.5f).SetUpdate(true);
        }

        private void UpdateAvatarRings()
        {
            for (int i = 0; i < avatarRings.Count; i++)
            {
                bool sel = i == pendingAvatar;
                avatarRings[i].color = sel ? Gold : new Color(0.35f, 0.5f, 0.8f, 0.6f);
                avatarRings[i].rectTransform.localScale = sel ? Vector3.one * 1.06f : Vector3.one;
            }
        }

        private void SaveProfile()
        {
            if (nameInput == null) return;
            if (!PlayerProfileData.TrySetPlayerName(nameInput.text, out string error))
            {
                Sfx(errorSound);
                ShowToast(error, new Color(0.7f, 0.12f, 0.16f, 0.96f), true);
                if (editorPanel != null)
                {
                    editorPanel.DOKill();
                    editorPanel.localScale = Vector3.one;
                    editorPanel.DOPunchAnchorPos(new Vector2(18f * u, 0f), 0.35f, 14, 0.6f).SetUpdate(true);
                }
                return;
            }
            PlayerProfileData.AvatarIndex = pendingAvatar;
            Sfx(successSound != null ? successSound : clickSound);
            CloseEditor(false);
            Refresh();
            avatarHolder.DOKill();
            avatarHolder.localScale = Vector3.one;
            avatarHolder.DOPunchScale(Vector3.one * 0.14f, 0.45f, 7, 0.6f).SetUpdate(true);
            nameText.rectTransform.DOKill();
            nameText.rectTransform.DOPunchScale(Vector3.one * 0.08f, 0.4f, 6, 0.6f).SetUpdate(true);
            ShowToast("Profile saved!", new Color(0.12f, 0.50f, 0.26f, 0.96f));
        }

        private void CloseEditor(bool instant)
        {
            if (editorRoot == null) return;
            var root = editorRoot;
            editorRoot = null;
            nameInput = null;
            nameCounter = null;
            avatarRings.Clear();
            if (instant)
            {
                KillTree(root);
                Destroy(root.gameObject);
                return;
            }
            var cg = root.GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = false;
            if (editorPanel != null) editorPanel.DOScale(0.9f, 0.16f).SetEase(Ease.InQuad).SetUpdate(true);
            if (cg != null)
                cg.DOFade(0f, 0.16f).SetUpdate(true).OnComplete(() =>
                {
                    if (root != null) { KillTree(root); Destroy(root.gameObject); }
                });
            else Destroy(root.gameObject);
        }

        private void OpenDeckEditor()
        {
            Sfx(clickSound);
            var cards = CardsScreenController.Instance;
            if (cards == null)
            {
                ShowToast("Open the Cards tab to edit your deck", new Color(0.12f, 0.35f, 0.7f, 0.96f));
                return;
            }
            Close();
            cards.Open();
        }

        // ═════════════════════════════ Buttons / toast ═════════════════════════════

        private RectTransform BuildGoldButton(Transform parent, string label, Vector2 size, Action onClick)
        {
            var btn = NewRect("Button_" + label, parent);
            btn.sizeDelta = size;
            var lip = NewRounded("Lip", btn, new Color(0.55f, 0.28f, 0.02f), 34f);
            Stretch(lip.rectTransform, 0f, 0f, 0f, 12f * u);
            var rimGold = NewRounded("Rim", btn, new Color(0.35f, 0.17f, 0.01f), 34f);
            Stretch(rimGold.rectTransform, 0f, 8f * u, 0f, 0f);
            var face = NewRounded("Face", btn, Color.white, 30f);
            Stretch(face.rectTransform, 5f * u, 13f * u, 5f * u, 5f * u);
            face.gameObject.AddComponent<CardsScreenGradient>().Set(new Color(1f, 0.90f, 0.40f), new Color(0.98f, 0.60f, 0.08f));
            var gloss = NewRounded("Gloss", face.rectTransform, new Color(1f, 1f, 1f, 0.3f), 24f);
            gloss.rectTransform.anchorMin = new Vector2(0f, 0.52f);
            gloss.rectTransform.anchorMax = Vector2.one;
            gloss.rectTransform.offsetMin = new Vector2(10f * u, 0f);
            gloss.rectTransform.offsetMax = new Vector2(-10f * u, -8f * u);
            var text = NewText("Label", face.rectTransform, label, size.y * 0.42f, Color.white, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            text.characterSpacing = 3f;
            MakeButton(btn, onClick);
            return btn;
        }

        private void MakeButton(RectTransform rt, Action onClick, bool feedback = true)
        {
            var hit = rt.GetComponent<Image>();
            if (hit == null)
            {
                hit = rt.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
            }
            hit.raycastTarget = true;
            var b = rt.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            if (feedback) rt.gameObject.AddComponent<ShopPressFeedback>();
        }

        private void ShowToast(string message, Color bg, bool overPopup = false)
        {
            Transform parent = overPopup && popupLayer != null ? popupLayer : (Transform)pageRoot;
            if (parent == null) return;
            if (toast != null) { KillTree(toast); Destroy(toast.gameObject); toast = null; }

            toast = NewRect("Toast", parent);
            toast.anchorMin = toast.anchorMax = new Vector2(0.5f, 0f);
            toast.pivot = new Vector2(0.5f, 0f);
            toast.sizeDelta = new Vector2(Mathf.Min(contentW - 80f * u, 760f * u), 84f * u);
            Vector2 rest = new Vector2(0f, 230f * u);
            toast.anchoredPosition = rest - new Vector2(0f, 60f * u);
            var cg = toast.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            var bgImg = NewRounded("Bg", toast, bg, 36f);
            Stretch(bgImg.rectTransform);
            var rim = NewRounded("Rim", toast, new Color(1f, 1f, 1f, 0.4f), 36f);
            Stretch(rim.rectTransform);
            rim.fillCenter = false;
            var t = NewText("Text", toast, message, 36f * u, Color.white, TextAlignmentOptions.Center);
            Stretch(t.rectTransform, 24f * u, 0f, 24f * u, 0f);
            t.enableAutoSizing = true;
            t.fontSizeMin = 20f * u;
            t.fontSizeMax = 36f * u;

            var tt = toast;
            DOTween.Sequence().SetUpdate(true)
                .Append(tt.DOAnchorPos(rest, 0.28f).SetEase(Ease.OutBack))
                .Join(cg.DOFade(1f, 0.2f))
                .AppendInterval(1.4f)
                .Append(cg.DOFade(0f, 0.3f))
                .OnComplete(() => { if (tt != null) Destroy(tt.gameObject); });
        }

        private void Sfx(AudioClip clip)
        {
            if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
        }

        // ═════════════════════════════ Rarity colours ═════════════════════════════

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

        private Image homeAvatarImage;

        /// <summary>Shows the chosen profile avatar on the home screen top bar (Canvas/Panel/toppanel/avataricon).</summary>
        private void SyncHomeAvatar()
        {
            if (avatarSprites == null || avatarSprites.Length == 0) return;
            if (homeAvatarImage == null && canvasRect != null)
            {
                var t = FindDeep(canvasRect, "avataricon");
                if (t != null) homeAvatarImage = t.GetComponent<Image>();
            }
            if (homeAvatarImage == null) return;
            var sp = avatarSprites[Mathf.Clamp(PlayerProfileData.AvatarIndex, 0, avatarSprites.Length - 1)];
            if (sp == null) return;
            homeAvatarImage.sprite = sp;
            homeAvatarImage.preserveAspect = true;
        }

        private IEnumerator HookSceneNextFrame()
        {
            yield return null; // let MainMenuController / Shop / Cards wire their own listeners first
            SyncHomeAvatar();
            if (hooked) yield break;
            hooked = true;

            if (bottomNav == null && canvasRect != null)
            {
                var nav = FindDeep(canvasRect, "downpanel");
                if (nav != null) bottomNav = nav as RectTransform;
            }
            EnsureSiblingBelowNav();

            if (hookBottomNav && bottomNav != null)
            {
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
                    btn.onClick.AddListener(Close);
                }
            }

            if (hookTopBarAvatar && canvasRect != null)
            {
                var top = FindDeep(canvasRect, "toppanel");
                Transform av = top != null ? (FindDeep(top, "avataricon") ?? FindDeep(top, "Avatar")) : null;
                if (av != null)
                {
                    var btn = av.GetComponent<Button>();
                    if (btn == null)
                    {
                        var g = av.GetComponent<Graphic>();
                        if (g != null) g.raycastTarget = true;
                        btn = av.gameObject.AddComponent<Button>();
                        btn.transition = Selectable.Transition.None;
                    }
                    btn.onClick.AddListener(Open);
                }
                else Debug.LogWarning("[ProfileController] No 'avataricon' found in the top panel: call ProfileController.Instance.Open() from your own button.");
            }
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
                // Let the list scroll under the bottom nav (the nav is drawn on top); the content keeps 220u of bottom clearance.
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
            var m = new Material(f.material) { name = f.name + " (Profile Outline)" };
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

        /// <summary>Anchors to the parent's top-left corner: distance from left / top, size.</summary>
        private static void PlaceTopLeft(RectTransform rt, float left, float top, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(left, -top);
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
        private static readonly Color GoldHi = new Color(1f, 0.88f, 0.46f, 1f);
        private static readonly Color GoldLo = new Color(0.80f, 0.48f, 0.08f, 1f);
        private static readonly Color NavyHi = new Color(0.07f, 0.20f, 0.64f, 1f);
        private static readonly Color NavyLo = new Color(0.02f, 0.07f, 0.30f, 1f);
        private static readonly Color RoyalHi = new Color(0.26f, 0.50f, 1f, 1f);
        private static readonly Color RoyalLo = new Color(0.08f, 0.20f, 0.66f, 1f);

        /// <summary>
        /// Solid bevelled frame: a full rounded shape in the rim gradient with the fill inset on top of it,
        /// so the rim is exactly <paramref name="thick"/> units wide (sliced "fillCenter = false" rims end up as thick as the corner radius).
        /// Adds a soft top sheen. Returns the fill image.
        /// </summary>
        private Image Framed(RectTransform holder, float radius, float thick, Color rimTop, Color rimBottom, Color fillTop, Color fillBottom)
        {
            var rim = NewRounded("Frame", holder, Color.white, radius);
            Stretch(rim.rectTransform);
            rim.gameObject.AddComponent<CardsScreenGradient>().Set(rimTop, rimBottom);

            var fill = NewRounded("Fill", holder, Color.white, Mathf.Max(4f, radius - thick));
            Stretch(fill.rectTransform, thick * u, thick * u, thick * u, thick * u);
            fill.gameObject.AddComponent<CardsScreenGradient>().Set(fillTop, fillBottom);

            var sheen = NewRounded("Sheen", holder, new Color(0.55f, 0.75f, 1f, 0.06f), Mathf.Max(4f, radius - thick));
            sheen.rectTransform.anchorMin = new Vector2(0f, 0.55f);
            sheen.rectTransform.anchorMax = Vector2.one;
            sheen.rectTransform.offsetMin = new Vector2(thick * u, 0f);
            sheen.rectTransform.offsetMax = new Vector2(-thick * u, -thick * u);
            sheen.raycastTarget = false;
            return fill;
        }

        private Image NewRounded(string name, Transform parent, Color color, float cornerRef)
        {
            var img = NewImage(name, parent, roundedSprite, color, false);
            if (roundedSprite != null && roundedSprite.border != Vector4.zero)
            {
                img.type = Image.Type.Sliced;
                float border = Mathf.Max(roundedSprite.border.x, roundedSprite.border.y);
                float canvasPPU = rootCanvas != null ? rootCanvas.referencePixelsPerUnit : 100f;
                float spritePPU = roundedSprite.pixelsPerUnit;
                float desired = Mathf.Max(1f, cornerRef * u);
                img.pixelsPerUnitMultiplier = Mathf.Max(0.01f, border * canvasPPU / (spritePPU * desired));
            }
            return img;
        }

        private RectTransform NewIcon(string name, Transform parent, Sprite sprite, Vector2 box)
        {
            var holder = NewRect(name, parent);
            holder.sizeDelta = box;
            var img = NewImage("Image", holder, sprite, sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f), true);
            Stretch(img.rectTransform);
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
            t.richText = true;
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
