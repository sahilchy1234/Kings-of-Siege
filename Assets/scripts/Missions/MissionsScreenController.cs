using System;
using System.Collections.Generic;
using DG.Tweening;
using Siege.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Siege.Missions
{
    /// <summary>
    /// Kings of Siege "Missions" page: Daily / Weekly / Achievements tabs, Season Pass bar, Claim All.
    /// Built at runtime in the same style as the Profile and Cards pages (royal blue, gold frames, white text).
    /// Mission data comes from MissionsManager; progress comes from the PlayerPrefs totals it records.
    /// Open it with Instance.Open() (or Open(tabIndex): 0 Daily, 1 Weekly, 2 Achievements) from any button.
    /// </summary>
    public class MissionsScreenController : MonoBehaviour
    {
        public static MissionsScreenController Instance { get; private set; }

        [Header("Sprites (optional: plain shapes are used when empty)")]
        public Sprite backgroundSprite;
        public Sprite roundedSprite;
        public Sprite circleSprite;
        public Sprite coinSprite;
        public Sprite gemSprite;
        public Sprite headerIcon;
        public Sprite kingSprite;
        public Sprite seasonBadgeSprite;
        public Sprite levelShieldSprite;
        public Sprite ribbonSprite;
        public Sprite clockSprite;
        [Tooltip("Tab icons: Daily, Weekly, Achievements.")]
        public Sprite[] tabIconSprites = new Sprite[3];
        public Sprite silverChestSprite;
        public Sprite goldChestSprite;
        [Tooltip("Icons by MissionStat order: Matches, Wins, Draws, ThreeStarWins, TrophiesGained, TowersDestroyed, BasesDestroyed.")]
        public Sprite[] statIcons = new Sprite[7];
        public TMP_FontAsset font;

        [Header("Audio (optional)")]
        public AudioSource audioSource;
        public AudioClip claimSound;
        public AudioClip tabSound;

        // Layout reference (same as the other pages). All sizes below are in these units and scaled by u.
        private const float REF_W = 941f;
        private const float ROW_REF_W = 901f;

        private static readonly Color BgBlue = new Color(0.04f, 0.11f, 0.34f, 1f);
        private static readonly Color Navy = new Color(0.04f, 0.10f, 0.32f, 1f);
        private static readonly Color TabActive = new Color(0.16f, 0.52f, 0.98f, 1f);
        private static readonly Color TabInactive = new Color(0.05f, 0.13f, 0.34f, 0.95f);
        private static readonly Color BandBlue = new Color(0.10f, 0.42f, 0.92f, 1f);
        private static readonly Color TextWhite = new Color(1f, 0.98f, 0.94f, 1f);
        private static readonly Color TextDim = new Color(0.62f, 0.72f, 0.88f, 1f);
        private static readonly Color Gold = new Color(1f, 0.80f, 0.25f, 1f);
        private static readonly Color GoldDark = new Color(0.62f, 0.40f, 0.08f, 1f);
        private static readonly Color ClaimGreen = new Color(0.30f, 0.92f, 0.32f, 1f);
        private static readonly Color BarBlue = new Color(0.20f, 0.72f, 1f, 1f);
        private static readonly Color Grey = new Color(0.30f, 0.34f, 0.44f, 1f);

        private static readonly string[] TabNames = { "Daily", "Weekly", "Achievements" };
        private static readonly MissionType[] TabTypes = { MissionType.Daily, MissionType.Weekly, MissionType.Achievement };
        private static readonly Color[] ConfettiColors =
        {
            new Color(1f, 0.84f, 0.20f), Color.white, new Color(0.45f, 0.80f, 1f), new Color(0.60f, 1f, 0.50f)
        };

        // Runtime. Marked [SerializeField] so a page baked by BuildInEditor survives domain reload:
        // Open() then reuses it (the editor layout persists into Play mode) instead of rebuilding.
        private RectTransform canvasRect;
        [SerializeField] private RectTransform pageRoot, column, track, fxLayer;
        [SerializeField] private CanvasGroup pageGroup;
        private float u = 1f, contentW = 0f;
        [SerializeField] private float builtWidth = -1f;
        private bool isOpen;
        private int currentTab;
        private float timer;

        [SerializeField] private Image[] tabBgs = new Image[3];
        [SerializeField] private TextMeshProUGUI[] tabLabels = new TextMeshProUGUI[3];
        [SerializeField] private CanvasGroup[] pageGroups = new CanvasGroup[3];
        [SerializeField] private RectTransform[] pageContents = new RectTransform[3];
        [SerializeField] private TextMeshProUGUI[] resetTexts = new TextMeshProUGUI[3];
        private readonly string[] periodKeys = new string[3];

        [SerializeField] private TextMeshProUGUI coinText, gemText, claimAllText;
        [SerializeField] private Image claimAllBg;
        [SerializeField] private Button claimAllButton, backButton;

        /// <summary>One prebuilt mission row. Built once (in the editor) and only updated at runtime, never rebuilt,
        /// so manual tweaks to the baked hierarchy survive entering Play mode.</summary>
        [System.Serializable]
        private class RowView
        {
            public MissionData mission;
            public Image frame, inner, ring, fill, claimLip, claimBg;
            public TextMeshProUGUI barText, claimLabel;
            public Button claimButton;
        }
        [SerializeField] private List<RowView> rowViews = new List<RowView>();

        // ═════════════════════════════ Lifecycle ═════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            PlayerWallet.EnsureSeeded();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            PlayerWallet.OnCoinsChanged += HandleCoinsChanged;
            PlayerWallet.OnGemsChanged += HandleGemsChanged;
            MissionsManager.OnProgressChanged += HandleProgressChanged;
        }

        private void OnDisable()
        {
            PlayerWallet.OnCoinsChanged -= HandleCoinsChanged;
            PlayerWallet.OnGemsChanged -= HandleGemsChanged;
            MissionsManager.OnProgressChanged -= HandleProgressChanged;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!isOpen || pageRoot == null) return;
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f) return;
            timer = 0.25f;

            DateTime now = DateTime.Now;
            for (int i = 0; i < 3; i++)
            {
                MissionType type = TabTypes[i];
                if (type == MissionType.Achievement) continue;
                // A new day or week started: rebuild so the baselines and claim states switch over.
                if (MissionsManager.PeriodKey(type, now) != periodKeys[i]) { Refresh(); return; }
                if (resetTexts[i] != null) resetTexts[i].text = ResetLabel(type, now);
            }
        }

        // ═════════════════════════════ Public API ═════════════════════════════

        public void Open(int tab = 0)
        {
            ResolveCanvas();
            if (canvasRect == null) return;
            Canvas.ForceUpdateCanvases();

            // Recompute the layout scale so a page baked in the editor still sizes correctly.
            float w = canvasRect.rect.width;
            float h = canvasRect.rect.height;
            contentW = Mathf.Min(w, h * 0.62f);
            u = contentW / REF_W;
            // Only build when there is no prebuilt page at all. A baked page is reused as-is and never rebuilt,
            // so edits made in the editor are kept.
            if (pageRoot == null) Build();

            // Button click listeners are added in code, so they do not survive serialization of a baked page.
            // Re-bind them every time the page opens.
            RewireButtons();

            pageRoot.gameObject.SetActive(true);
            bool wasOpen = isOpen;
            isOpen = true;
            pageGroup.DOKill();
            pageGroup.interactable = true;
            pageGroup.blocksRaycasts = true;

            Refresh();
            SwitchTab(Mathf.Clamp(tab, 0, 2), false);

            if (!wasOpen)
            {
                pageGroup.alpha = 0f;
                pageGroup.DOFade(1f, 0.22f).SetUpdate(true);
                pageRoot.localScale = Vector3.one * 0.97f;
                pageRoot.DOScale(1f, 0.28f).SetEase(Ease.OutBack).SetUpdate(true);
                Sfx(tabSound);
            }
            else pageGroup.alpha = 1f;
        }

        /// <summary>Re-binds every button's onClick from the serialized references. Needed because a baked page
        /// keeps its Button components but not the code-added listeners.</summary>
        private void RewireButtons()
        {
            Rebind(backButton, Close);
            Rebind(claimAllButton, OnClaimAll);
            for (int i = 0; i < 3; i++)
            {
                if (tabBgs[i] == null) continue;
                var tb = tabBgs[i].GetComponent<Button>();
                int idx = i;
                Rebind(tb, () => OnTabClicked(idx));
            }
            foreach (var rv in rowViews)
            {
                if (rv == null || rv.claimButton == null) continue;
                var captured = rv;
                Rebind(rv.claimButton, () => OnClaim(captured.mission, captured.claimBg.rectTransform));
            }
        }

        private static void Rebind(Button b, UnityEngine.Events.UnityAction action)
        {
            if (b == null) return;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(action);
        }

        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;
            if (pageRoot == null) return;
            pageGroup.DOKill();
            pageGroup.interactable = false;
            pageGroup.blocksRaycasts = false;
            pageGroup.DOFade(0f, 0.18f).SetEase(Ease.InQuad).SetUpdate(true)
                .OnComplete(() => { if (!isOpen && pageRoot != null) pageRoot.gameObject.SetActive(false); });
        }

#if UNITY_EDITOR
        /// <summary>Builds the full page in Edit mode so it can be inspected and tweaked without entering Play mode.
        /// Called by Kings of Siege > Missions > Build Missions Page after the sprites are wired.</summary>
        public void BuildInEditor()
        {
            ResolveCanvas();
            if (canvasRect == null) { Debug.LogWarning("[MissionsScreenController] BuildInEditor: no Canvas found."); return; }
            UnityEngine.Canvas.ForceUpdateCanvases();
            if (canvasRect.rect.width < 1f)
            {
                var scaler = canvasRect.GetComponent<UnityEngine.UI.CanvasScaler>();
                canvasRect.sizeDelta = scaler != null ? scaler.referenceResolution : new Vector2(1080f, 1920f);
                UnityEngine.Canvas.ForceUpdateCanvases();
            }

            Build();

            // Build() already created the rows; just set values and the active tab (no animation in Edit mode).
            Refresh();
            currentTab = 0;
            SwitchTab(0, false);

            pageRoot.gameObject.SetActive(true);
            pageGroup.alpha = 1f;
            pageGroup.interactable = true;
            pageGroup.blocksRaycasts = true;
            UnityEditor.EditorUtility.SetDirty(gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            Debug.Log("[MissionsScreenController] BuildInEditor: page built with " + pageRoot.childCount + " children.");
        }
#endif

        // ═════════════════════════════ Build ═════════════════════════════

        private void ResolveCanvas()
        {
            var c = GetComponentInParent<Canvas>();
            if (c != null) c = c.rootCanvas;
            canvasRect = c != null ? c.transform as RectTransform : transform.parent as RectTransform;
        }

        private static void SafeDestroy(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        private void Build()
        {
            if (pageRoot != null) SafeDestroy(pageRoot.gameObject);
            // Remove any page baked earlier (e.g. by the editor tool) so Play mode does not duplicate it.
            if (canvasRect != null)
            {
                var stale = canvasRect.Find("MissionsPage");
                if (stale != null) SafeDestroy(stale.gameObject);
            }

            float W = canvasRect.rect.width;
            float H = canvasRect.rect.height;
            contentW = Mathf.Min(W, H * 0.62f);
            u = contentW / REF_W;
            builtWidth = W;
            if (font == null) font = TMP_Settings.defaultFontAsset;

            // Parent to the canvas rect: this object may be a plain Transform, which would give the page zero size.
            pageRoot = NewRect(canvasRect, "MissionsPage");
            Stretch(pageRoot);
            pageGroup = pageRoot.gameObject.AddComponent<CanvasGroup>();
            var solid = pageRoot.gameObject.AddComponent<Image>();
            solid.color = BgBlue;
            solid.raycastTarget = true;

            if (backgroundSprite != null)
            {
                var bgImg = NewImage(pageRoot, "Background", backgroundSprite, Color.white);
                bgImg.rectTransform.anchorMin = bgImg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                bgImg.rectTransform.sizeDelta = new Vector2(W, H);
                var fit = bgImg.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = backgroundSprite.rect.width / Mathf.Max(1f, backgroundSprite.rect.height);
                var shade = NewImage(pageRoot, "Shade", null, new Color(0.02f, 0.06f, 0.20f, 0.55f));
                Stretch(shade.rectTransform);
            }

            column = NewRect(pageRoot, "Column");
            column.anchorMin = new Vector2(0.5f, 0f);
            column.anchorMax = new Vector2(0.5f, 1f);
            column.pivot = new Vector2(0.5f, 0.5f);
            column.sizeDelta = new Vector2(contentW, 0f);
            column.anchoredPosition = Vector2.zero;

            BuildHeader();
            BuildHero();
            BuildTabs();
            BuildPages();
            BuildRows();

            // Confetti layer sits on top of everything.
            fxLayer = NewRect(pageRoot, "Fx");
            Stretch(fxLayer);

            pageRoot.gameObject.SetActive(false);
        }

        private void BuildHeader()
        {
            var band = NewImage(column, "HeaderBand", null, new Color(0.03f, 0.08f, 0.26f, 1f));
            Place(band.rectTransform, 0, 0, 0, REF_W, 112);
            var line = NewImage(column, "HeaderLine", null, Gold);
            Place(line.rectTransform, 0, 0, 108, REF_W, 5);

            var back = Box(column, "BackButton", new Color(0.12f, 0.40f, 0.92f, 1f));
            Place(back.rectTransform, 0, 20, 16, 96, 80);
            var arrow = Label(back.transform, "Arrow", "<", 56f, Color.white, TextAlignmentOptions.Center);
            Stretch(arrow.rectTransform);
            backButton = AddButton(back, Close);

            float titleX = 128f;
            if (headerIcon != null)
            {
                var hi = NewImage(column, "HeaderIcon", headerIcon, Color.white);
                hi.preserveAspect = true;
                Place(hi.rectTransform, 0, 124, 8, 84, 84);
                titleX = 214f;
            }
            var title = Label(column, "Title", "MISSIONS", 50f, TextWhite, TextAlignmentOptions.MidlineLeft);
            Place(title.rectTransform, 0, titleX, 18, 300, 80);

            var coinPill = Box(column, "CoinPill", new Color(0.01f, 0.04f, 0.14f, 0.85f));
            Place(coinPill.rectTransform, 1, 16, 16, 200, 84);
            coinText = PillContents(coinPill, coinSprite);

            var gemPill = Box(column, "GemPill", new Color(0.01f, 0.04f, 0.14f, 0.85f));
            Place(gemPill.rectTransform, 1, 228, 16, 170, 84);
            gemText = PillContents(gemPill, gemSprite);
        }

        private TextMeshProUGUI PillContents(Image pill, Sprite icon)
        {
            if (icon != null)
            {
                var ic = NewImage(pill.transform, "Icon", icon, Color.white);
                Place(ic.rectTransform, 0, 8, 10, 64, 64);
            }
            var value = Label(pill.transform, "Value", "0", 34f, TextWhite, TextAlignmentOptions.MidlineRight);
            Place(value.rectTransform, 0, 74, 14, 116, 56);
            return value;
        }

        private void BuildHero()
        {
            if (kingSprite != null)
            {
                var k = NewImage(column, "King", kingSprite, Color.white);
                k.preserveAspect = true;
                Place(k.rectTransform, 0, 6, 120, 290, 290);
            }

            var bubbleRim = Box(column, "BubbleRim", Gold);
            Place(bubbleRim.rectTransform, 0, 290, 160, 370, 112);
            var bubble = Box(bubbleRim.transform, "Bubble", new Color(0.05f, 0.14f, 0.44f, 1f));
            InsetAll(bubble.rectTransform, 3f);
            var bubbleText = Label(bubble.transform, "Text", "Complete missions to earn rewards!", 26f, TextWhite, TextAlignmentOptions.Center);
            InsetAll(bubbleText.rectTransform, 14f);

            if (goldChestSprite != null)
            {
                var chest = NewImage(column, "HeroChest", goldChestSprite, Color.white);
                chest.preserveAspect = true;
                Place(chest.rectTransform, 1, 8, 190, 190, 170);
            }

            claimAllBg = Box(column, "ClaimAll", Grey);
            Place(claimAllBg.rectTransform, 1, 16, 124, 250, 66);
            claimAllButton = AddButton(claimAllBg, OnClaimAll);
            claimAllText = Label(claimAllBg.transform, "Label", "CLAIM ALL", 30f, TextDim, TextAlignmentOptions.Center);
            Stretch(claimAllText.rectTransform);
        }

        private void BuildTabs()
        {
            float tabW = 291f, gap = 14f;
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var bg = Box(column, TabNames[i] + "Tab", TabInactive);
                Place(bg.rectTransform, 0, 20 + i * (tabW + gap), 440, tabW, 84);
                tabBgs[i] = bg;

                bool hasIcon = tabIconSprites != null && i < tabIconSprites.Length && tabIconSprites[i] != null;
                if (hasIcon)
                {
                    var ic = NewImage(bg.transform, "Icon", tabIconSprites[i], Color.white);
                    ic.preserveAspect = true;
                    Place(ic.rectTransform, 0, 18, 12, 60, 60);
                }
                tabLabels[i] = Label(bg.transform, "Label", TabNames[i], i == 2 ? 30f : 38f, TextWhite, TextAlignmentOptions.Center);
                Stretch(tabLabels[i].rectTransform);
                if (hasIcon) tabLabels[i].rectTransform.offsetMin = new Vector2(70f * u, 0f);
                AddButton(bg, () => OnTabClicked(idx));
            }

            var band = NewImage(column, "TabBand", null, BandBlue);
            Place(band.rectTransform, 0, 0, 532, REF_W, 8);
        }

        private void BuildPages()
        {
            var viewport = NewRect(column, "Viewport");
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = new Vector2(0f, -548f * u);
            viewport.gameObject.AddComponent<RectMask2D>();

            track = NewRect(viewport, "Track");
            track.anchorMin = new Vector2(0f, 0f);
            track.anchorMax = new Vector2(0f, 1f);
            track.pivot = new Vector2(0f, 0.5f);
            track.sizeDelta = new Vector2(contentW * 3f, 0f);
            track.anchoredPosition = Vector2.zero;

            for (int i = 0; i < 3; i++)
            {
                var page = NewRect(track, "Page_" + TabNames[i]);
                page.anchorMin = new Vector2(0f, 0f);
                page.anchorMax = new Vector2(0f, 1f);
                page.pivot = new Vector2(0f, 0.5f);
                page.sizeDelta = new Vector2(contentW, 0f);
                page.anchoredPosition = new Vector2(contentW * i, 0f);
                pageGroups[i] = page.gameObject.AddComponent<CanvasGroup>();
                var hit = page.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);

                var scroll = page.gameObject.AddComponent<ScrollRect>();
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Elastic;
                scroll.elasticity = 0.12f;
                scroll.decelerationRate = 0.12f;
                scroll.viewport = page;

                var content = NewRect(page, "Content");
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
                content.sizeDelta = Vector2.zero;
                var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
                vl.childAlignment = TextAnchor.UpperCenter;
                vl.spacing = 14f * u;
                vl.padding = new RectOffset(Mathf.RoundToInt(20f * u), Mathf.RoundToInt(20f * u), Mathf.RoundToInt(16f * u), Mathf.RoundToInt(40f * u));
                vl.childControlWidth = true;
                vl.childControlHeight = true;
                vl.childForceExpandWidth = true;
                vl.childForceExpandHeight = false;
                var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                scroll.content = content;

                pageContents[i] = content;
            }
        }

        // ═════════════════════════════ Refresh ═════════════════════════════

        /// <summary>Runtime refresh: only updates values on the prebuilt rows — it never rebuilds the hierarchy.</summary>
        private void Refresh()
        {
            if (pageRoot == null) return;
            DateTime now = DateTime.Now;

            coinText.text = PlayerWallet.Coins.ToString("N0");
            gemText.text = PlayerWallet.Gems.ToString("N0");

            for (int i = 0; i < 3; i++)
            {
                periodKeys[i] = MissionsManager.PeriodKey(TabTypes[i], now);
                if (resetTexts[i] != null) resetTexts[i].text = ResetLabel(TabTypes[i], now);
            }
            foreach (var rv in rowViews) UpdateRow(rv, now);
            UpdateClaimAll(now);
        }

        private void UpdateClaimAll(DateTime now)
        {
            int ready = MissionsManager.CountReady(TabTypes[currentTab], now);
            claimAllButton.interactable = ready > 0;
            claimAllBg.color = ready > 0 ? Gold : Grey;
            claimAllText.text = ready > 0 ? "CLAIM ALL (" + ready + ")" : "CLAIM ALL";
            claimAllText.color = ready > 0 ? new Color(0.25f, 0.14f, 0.02f) : TextDim;
        }

        /// <summary>Builds every tab's reset ribbon and one prebuilt row per mission. Called once, at build/bake time.</summary>
        private void BuildRows()
        {
            rowViews.Clear();
            DateTime now = DateTime.Now;
            for (int i = 0; i < 3; i++)
            {
                var content = pageContents[i];
                MissionType type = TabTypes[i];
                resetTexts[i] = null;

                if (type != MissionType.Achievement)
                {
                    var head = Blank(content, "ResetRow", 76f);
                    if (ribbonSprite != null)
                    {
                        var rib = NewImage(head, "Ribbon", ribbonSprite, Color.white);
                        rib.rectTransform.anchorMin = rib.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                        rib.rectTransform.sizeDelta = new Vector2(640f * u, 76f * u);
                    }
                    if (clockSprite != null)
                    {
                        var clk = NewImage(head, "Clock", clockSprite, Color.white);
                        clk.preserveAspect = true;
                        clk.rectTransform.anchorMin = clk.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                        clk.rectTransform.sizeDelta = new Vector2(40f * u, 40f * u);
                        clk.rectTransform.anchoredPosition = new Vector2(-215f * u, 0f);
                    }
                    resetTexts[i] = Label(head, "Reset", ResetLabel(type, now), 30f, TextWhite, TextAlignmentOptions.Center);
                    Stretch(resetTexts[i].rectTransform);
                }

                var list = MissionsManager.GetMissions(type);
                if (list.Count == 0)
                {
                    var empty = Blank(content, "Empty", 120f);
                    var t = Label(empty, "Text", "No missions here yet.", 30f, TextDim, TextAlignmentOptions.Center);
                    Stretch(t.rectTransform);
                    continue;
                }
                foreach (var m in list) rowViews.Add(BuildRow(content, m));
            }
        }

        /// <summary>Builds one mission row in a neutral state and returns references for UpdateRow to drive.</summary>
        private RowView BuildRow(RectTransform content, MissionData m)
        {
            var rv = new RowView { mission = m };
            var row = Blank(content, "Mission_" + m.Id, 176f);

            rv.frame = Box(row, "Frame", GoldDark);
            Stretch(rv.frame.rectTransform);
            rv.inner = Box(row, "Inner", Navy);
            InsetAll(rv.inner.rectTransform, 4f);
            var sheen = Box(row, "Sheen", new Color(1f, 1f, 1f, 0.06f));
            sheen.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            sheen.rectTransform.anchorMax = Vector2.one;
            sheen.rectTransform.offsetMin = new Vector2(10f * u, 0f);
            sheen.rectTransform.offsetMax = new Vector2(-10f * u, -8f * u);

            // Difficulty ring + icon
            rv.ring = Disc(row, "Ring", new Color(m.RingColor.r, m.RingColor.g, m.RingColor.b, 0.35f));
            Place(rv.ring.rectTransform, 0, 12, 22, 136, 136);
            Transform face;
            if (levelShieldSprite != null)
            {
                var shield = NewImage(row, "Shield", levelShieldSprite, Color.white);
                shield.preserveAspect = true;
                Place(shield.rectTransform, 0, 20, 26, 120, 124);
                var holder = NewRect(shield.transform, "IconHolder");
                holder.anchorMin = new Vector2(0.2f, 0.22f);
                holder.anchorMax = new Vector2(0.8f, 0.78f);
                holder.offsetMin = holder.offsetMax = Vector2.zero;
                face = holder;
            }
            else
            {
                var plain = Disc(rv.ring.transform, "Face", new Color(0.03f, 0.09f, 0.30f));
                InsetAll(plain.rectTransform, 10f);
                face = plain.transform;
            }
            Sprite statIcon = m.icon != null ? m.icon
                : (statIcons != null && (int)m.stat < statIcons.Length ? statIcons[(int)m.stat] : null);
            if (statIcon != null)
            {
                var icon = NewImage(face.transform, "Icon", statIcon, Color.white);
                InsetAll(icon.rectTransform, levelShieldSprite != null ? 6f : 14f);
                icon.preserveAspect = true;
            }
            else
            {
                var badge = Label(face.transform, "Badge", StatShort(m.stat), 34f, TextWhite, TextAlignmentOptions.Center);
                Stretch(badge.rectTransform);
            }

            // Goal + progress bar
            var goal = Label(row, "Goal", m.goal, 30f, TextWhite, TextAlignmentOptions.MidlineLeft);
            goal.enableWordWrapping = true;
            Place(goal.rectTransform, 0, 156, 20, 520, 62);

            var barTrack = Box(row, "BarTrack", new Color(0.01f, 0.04f, 0.16f, 0.95f));
            Place(barTrack.rectTransform, 0, 156, 98, 520, 40);
            rv.fill = Box(barTrack.transform, "Fill", BarBlue);
            Stretch(rv.fill.rectTransform);
            rv.fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            rv.barText = Label(barTrack.transform, "BarText", "0 / " + m.target, 24f, TextWhite, TextAlignmentOptions.Center);
            Stretch(rv.barText.rectTransform);

            // Rewards (right column)
            if (coinSprite != null && m.coinReward > 0)
            {
                var ci = NewImage(row, "CoinIcon", coinSprite, Color.white);
                Place(ci.rectTransform, 1, 20, 22, 40, 40);
            }
            var coins = Label(row, "Coins", m.coinReward > 0 ? "+" + m.coinReward.ToString("N0") : "", 32f, Gold, TextAlignmentOptions.MidlineRight);
            Place(coins.rectTransform, 1, 68, 20, 130, 44);

            string sub = "";
            if (m.gemReward > 0) sub = "+" + m.gemReward + " gems";
            if (!string.IsNullOrEmpty(m.chestName)) sub = sub.Length > 0 ? sub + ", " + m.chestName : m.chestName;
            Sprite chest = null;
            if (!string.IsNullOrEmpty(m.chestName))
                chest = m.chestName.ToLower().Contains("gold") ? goldChestSprite : silverChestSprite;
            float subX = 20f, subW = 190f;
            if (chest != null)
            {
                var chestImg = NewImage(row, "ChestIcon", chest, Color.white);
                chestImg.preserveAspect = true;
                Place(chestImg.rectTransform, 1, 20, 58, 40, 40);
                subX = 66f; subW = 144f;
            }
            var subText = Label(row, "Sub", sub, 22f, TextDim, TextAlignmentOptions.MidlineRight);
            Place(subText.rectTransform, 1, subX, 66, subW, 28);

            // Claim button
            rv.claimLip = Box(row, "ClaimLip", new Color(0.16f, 0.19f, 0.28f, 1f));
            Place(rv.claimLip.rectTransform, 1, 20, 106, 170, 56);
            rv.claimBg = Box(row, "ClaimButton", Grey);
            Place(rv.claimBg.rectTransform, 1, 20, 100, 170, 56);
            var captured = m;
            rv.claimButton = AddButton(rv.claimBg, () => OnClaim(captured, rv.claimBg.rectTransform));
            rv.claimButton.interactable = false;
            rv.claimLabel = Label(rv.claimBg.transform, "Label", "CLAIM", 30f, TextDim, TextAlignmentOptions.Center);
            Stretch(rv.claimLabel.rectTransform);
            return rv;
        }

        /// <summary>Updates one prebuilt row to the mission's current progress / claim state. No objects created.</summary>
        private void UpdateRow(RowView rv, DateTime now)
        {
            if (rv == null || rv.mission == null || rv.fill == null) return;
            var m = rv.mission;
            int progress = MissionsManager.Progress(m, now);
            bool claimed = MissionsManager.IsClaimed(m, now);
            bool ready = !claimed && progress >= m.target;
            float r = m.target > 0 ? Mathf.Clamp01(progress / (float)m.target) : 0f;

            rv.fill.rectTransform.anchorMax = new Vector2(claimed ? 1f : r, 1f);
            rv.fill.color = claimed ? new Color(0.22f, 0.55f, 0.30f) : ready ? ClaimGreen : BarBlue;
            rv.barText.text = claimed ? "CLAIMED" : progress + " / " + m.target;

            rv.frame.color = ready ? Gold : GoldDark;
            rv.inner.color = ready ? new Color(0.09f, 0.24f, 0.66f, 0.98f) : Navy;
            rv.claimBg.color = ready ? Gold : Grey;
            rv.claimLip.color = ready ? GoldDark : new Color(0.16f, 0.19f, 0.28f, 1f);
            rv.claimButton.interactable = ready;
            rv.claimLabel.text = claimed ? "DONE" : "CLAIM";
            rv.claimLabel.color = ready ? new Color(0.25f, 0.14f, 0.02f) : TextDim;

            var bt = rv.claimBg.rectTransform;
            bt.DOKill();
            bt.localScale = Vector3.one;
            if (ready && Application.isPlaying)
                bt.DOScale(1.06f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);
        }

        // ═════════════════════════════ Tabs ═════════════════════════════

        private void OnTabClicked(int idx)
        {
            if (idx == currentTab)
            {
                tabBgs[idx].rectTransform.DOKill(true);
                tabBgs[idx].rectTransform.DOPunchScale(Vector3.one * 0.06f, 0.25f, 6, 0.6f).SetUpdate(true);
                return;
            }
            SwitchTab(idx, true);
        }

        private void SwitchTab(int idx, bool animate)
        {
            bool changed = idx != currentTab;
            currentTab = idx;
            float targetX = -contentW * idx;

            track.DOKill();
            if (animate && changed) track.DOAnchorPosX(targetX, 0.42f).SetEase(Ease.OutQuint).SetUpdate(true);
            else track.anchoredPosition = new Vector2(targetX, 0f);

            for (int i = 0; i < 3; i++)
            {
                bool on = i == idx;
                pageGroups[i].blocksRaycasts = on;
                tabBgs[i].DOKill();
                tabLabels[i].DOKill();
                pageGroups[i].DOKill();
                if (animate)
                {
                    tabBgs[i].DOColor(on ? TabActive : TabInactive, 0.2f).SetUpdate(true);
                    tabLabels[i].DOColor(on ? TextWhite : TextDim, 0.2f).SetUpdate(true);
                    tabBgs[i].rectTransform.DOScale(on ? 1f : 0.94f, 0.3f).SetEase(on ? Ease.OutBack : Ease.OutQuad).SetUpdate(true);
                    pageGroups[i].DOFade(on ? 1f : 0.35f, 0.3f).SetUpdate(true);
                }
                else
                {
                    tabBgs[i].color = on ? TabActive : TabInactive;
                    tabLabels[i].color = on ? TextWhite : TextDim;
                    tabBgs[i].rectTransform.localScale = Vector3.one * (on ? 1f : 0.94f);
                    pageGroups[i].alpha = on ? 1f : 0.35f;
                }
            }
            if (changed && animate) Sfx(tabSound);
            if (claimAllBg != null) UpdateClaimAll(DateTime.Now);
        }

        // ═════════════════════════════ Claiming ═════════════════════════════

        private void OnClaim(MissionData m, RectTransform button)
        {
            if (!MissionsManager.Claim(m, DateTime.Now)) return;
            Sfx(claimSound);
            SpawnConfetti(LocalPoint(button), 44);
            button.DOPunchScale(Vector3.one * 0.12f, 0.3f, 6, 0.6f).SetUpdate(true);
            DOVirtual.DelayedCall(0.35f, () => Refresh()).SetUpdate(true);
        }

        private void OnClaimAll()
        {
            DateTime now = DateTime.Now;
            int n = 0;
            foreach (var m in MissionsManager.GetMissions(TabTypes[currentTab]))
                if (MissionsManager.Claim(m, now)) n++;
            if (n == 0) return;

            Sfx(claimSound);
            SpawnConfetti(LocalPoint(claimAllBg.rectTransform), 30 + 10 * n);
            DOVirtual.DelayedCall(0.35f, () => Refresh()).SetUpdate(true);
        }

        private Vector2 LocalPoint(RectTransform rt)
        {
            return fxLayer.InverseTransformPoint(rt.position);
        }

        private void SpawnConfetti(Vector2 origin, int count)
        {
            if (fxLayer == null) return;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Confetti", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(fxLayer, false);
                var img = go.GetComponent<Image>();
                img.color = ConfettiColors[UnityEngine.Random.Range(0, ConfettiColors.Length)];
                img.raycastTarget = false;

                float s = UnityEngine.Random.Range(9f, 17f) * u;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(s, s * UnityEngine.Random.Range(0.45f, 0.8f));
                rt.anchoredPosition = origin;

                Vector2 apex = origin + new Vector2(UnityEngine.Random.Range(-260f, 260f) * u, UnityEngine.Random.Range(140f, 380f) * u);
                Vector2 end = new Vector2(apex.x + UnityEngine.Random.Range(-60f, 60f) * u, apex.y - UnityEngine.Random.Range(460f, 640f) * u);
                float fall = UnityEngine.Random.Range(0.9f, 1.3f);

                var seq = DOTween.Sequence();
                seq.Append(rt.DOAnchorPos(apex, UnityEngine.Random.Range(0.35f, 0.55f)).SetEase(Ease.OutCubic));
                seq.Append(rt.DOAnchorPos(end, fall).SetEase(Ease.InCubic));
                seq.Insert(0f, rt.DORotate(new Vector3(0f, 0f, UnityEngine.Random.Range(540f, 900f)), 0.45f + fall, RotateMode.FastBeyond360).SetEase(Ease.Linear));
                seq.Insert(0.4f, img.DOFade(0f, fall));
                seq.SetUpdate(true).OnComplete(() => { if (go != null) Destroy(go); });
            }
        }

        // ═════════════════════════════ Events ═════════════════════════════

        private void HandleCoinsChanged(int oldValue, int newValue)
        {
            if (coinText != null) coinText.text = newValue.ToString("N0");
        }

        private void HandleGemsChanged(int oldValue, int newValue)
        {
            if (gemText != null) gemText.text = newValue.ToString("N0");
        }

        private void HandleProgressChanged()
        {
            if (isOpen) Refresh();
        }

        // ═════════════════════════════ Helpers ═════════════════════════════

        private static string ResetLabel(MissionType type, DateTime now)
        {
            TimeSpan left = MissionsManager.NextReset(type, now) - now;
            if (left < TimeSpan.Zero) left = TimeSpan.Zero;
            string time = (left.Days > 0 ? left.Days + "d " : "") + $"{left.Hours:00}:{left.Minutes:00}:{left.Seconds:00}";
            return (type == MissionType.Daily ? "Daily" : "Weekly") + " resets in <color=#FFD84A>" + time + "</color>";
        }

        private static string StatShort(MissionStat stat)
        {
            switch (stat)
            {
                case MissionStat.Matches: return "B";
                case MissionStat.Wins: return "W";
                case MissionStat.Draws: return "D";
                case MissionStat.ThreeStarWins: return "3";
                case MissionStat.TrophiesGained: return "T";
                case MissionStat.TowersDestroyed: return "TW";
                case MissionStat.BasesDestroyed: return "C";
                default: return "?";
            }
        }

        private void Sfx(AudioClip clip)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
        }

        /// <summary>Positions a top-anchored child. ax = 0 anchors left, 1 anchors right (x is then the distance from the right edge). Values are in reference units.</summary>
        private void Place(RectTransform rt, float ax, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(ax, 1f);
            rt.pivot = new Vector2(ax, 1f);
            rt.anchoredPosition = new Vector2((ax == 0f ? x : -x) * u, -y * u);
            rt.sizeDelta = new Vector2(w * u, h * u);
        }

        private RectTransform Blank(Transform parent, string name, float h)
        {
            var rt = NewRect(parent, name);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = h * u;
            le.minHeight = h * u;
            le.flexibleWidth = 1f;
            return rt;
        }

        private static RectTransform NewRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void InsetAll(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset * u, inset * u);
            rt.offsetMax = new Vector2(-inset * u, -inset * u);
        }

        private Image Box(Transform parent, string name, Color c)
        {
            var img = NewImage(parent, name, roundedSprite, c);
            img.type = roundedSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            return img;
        }

        private Image Disc(Transform parent, string name, Color c)
        {
            var img = NewImage(parent, name, circleSprite, c);
            return img;
        }

        private Image NewImage(Transform parent, string name, Sprite sprite, Color c)
        {
            var rt = NewRect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        private TextMeshProUGUI Label(Transform parent, string name, string text, float size, Color c, TextAlignmentOptions align)
        {
            var rt = NewRect(parent, name);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size * u;
            t.color = c;
            t.alignment = align;
            t.enableWordWrapping = true;
            t.raycastTarget = false;
            return t;
        }

        private Button AddButton(Image img, Action onClick)
        {
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.targetGraphic = img;
            b.onClick.AddListener(() => onClick?.Invoke());
            return b;
        }
    }
}
