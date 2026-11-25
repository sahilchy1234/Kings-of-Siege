using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Siege.Profile;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Siege.Rankings
{
    /// <summary>Sprites for the Rankings page. Kings of Siege > Rankings > Build Rankings Page fills these in.</summary>
    [Serializable]
    public class RankingsArt
    {
        [Header("Page")]
        public Sprite background;     // Store/Backgrounds/Shop_Blurred_Blue
        public Sprite ribbon;         // Label_Ribbon01_Yellow
        public Sprite backButton;     // Home/Panels/shortcut_button
        public Sprite backIcon;       // IconMisc/Icon_Arrow_Back
        public Sprite seasonPanel;    // battlevs wide_status_panel (sliced)
        public Sprite timerIcon;      // IconMisc/Icon_Timer
        public Sprite kingsBadge;     // badges/Kings

        [Header("Tabs")]
        public Sprite tabOn;          // Button01_Demo_Yellow (sliced)
        public Sprite tabOff;         // Button01_Demo_Blue (sliced)
        public Sprite tabGlobalIcon;  // IconMisc/Icon_Trophy_l
        public Sprite tabLocalIcon;   // IconMisc/Icon_Flag
        public Sprite tabClansIcon;   // IconMisc/Icon_Shield01

        [Header("Podium")]
        public Sprite trophy;         // battlevs trophy
        public Sprite crown;          // battlevs crown
        public Sprite laurel;         // battlevs laurel_wreath
        public Sprite glow;           // battlevs gold_glow_orb
        public Sprite medalGold;      // level_medallion_gold_blank
        public Sprite medalSilver;    // level_medallion_silver_blank

        [Header("Rows")]
        public Sprite rowPanel;       // battlevs wide_status_panel (sliced)
        public Sprite selfPanel;      // Frame/ListFrame01_White1 (sliced, tinted)
        public Sprite trophyPill;     // battlevs inner_status_slot
        public Sprite clanShield;     // IconMisc/Icon_Shield01 (tinted per clan)

        [Header("Lists")]
        public Sprite[] avatars;      // same order as the Profile page (index = PlayerProfileData.AvatarIndex)
        public Sprite[] rankBadges;   // PlayerProfileData.RankBadgeFileNames order (28)
        public Sprite[] flags;        // Icon_Flag_*
    }

    /// <summary>
    /// Kings of Siege Rankings page: Global, Local and Clans leaderboards with a top-3 podium,
    /// a scrolling list and the player's own rank pinned at the bottom.
    /// The UI is generated at runtime (reference width 941, like the Shop). Data comes only through
    /// IRankingsDataProvider (MockRankingsDataProvider by default).
    /// Opens from the bottom nav Rankings tab (downpanel child rankingsTabIndex).
    /// </summary>
    public class RankingsController : MonoBehaviour
    {
        public static RankingsController Instance { get; private set; }

        [Header("Wiring (set by Kings of Siege > Rankings > Build Rankings Page)")]
        public RankingsArt art = new RankingsArt();
        [Tooltip("TMP_TiltWarp (plain).")]
        public TMP_FontAsset titleFont;
        [Tooltip("TMP_TiltWarp_Outline (white text with a dark outline).")]
        public TMP_FontAsset outlineFont;
        [Tooltip("Index of the Rankings tab in the bottom nav 'downpanel' (Shop, Cards, Home, Rankings).")]
        public int rankingsTabIndex = 3;
        public bool hookBottomNav = true;
        [Range(10, 200)] public int listSize = 50;

        // ── Layout (reference units, multiplied by u) ──
        private const float REFERENCE_WIDTH = 941f;
        private const float SIDE = 24f;
        private const float TOP_PAD = 14f;
        private const float RIBBON_W = 480f;
        private const float RIBBON_H = 186f;
        private const float SEASON_TOP = 196f;
        private const float SEASON_H = 80f;
        private const float TABS_TOP = 290f;
        private const float TABS_H = 96f;
        private const float SCROLL_TOP = 398f;
        private const float SELF_H = 128f;
        private const float SELF_BOTTOM = 14f;
        private const float PODIUM_H = 545f;
        private const float ROW_H = 116f;
        private const float ROW_SPACING = 10f;

        // ── Colours ──
        private static readonly Color Brown = new Color(0.36f, 0.17f, 0.02f, 1f);
        private static readonly Color Gold = new Color(1f, 0.84f, 0.28f, 1f);
        private static readonly Color White = new Color(1f, 1f, 1f, 1f);
        private static readonly Color SoftBlue = new Color(0.72f, 0.84f, 1f, 1f);
        private static readonly Color Bronze = new Color(0.93f, 0.6f, 0.38f, 1f);
        private static readonly Color SelfTint = new Color(1f, 0.82f, 0.3f, 1f);
        private static readonly Color LocalRowTint = new Color(0.62f, 1f, 0.62f, 1f);
        private static readonly Color[] ClanColors =
        {
            new Color(0.45f, 0.65f, 1f), new Color(1f, 0.45f, 0.45f), new Color(0.5f, 0.95f, 0.55f), new Color(1f, 0.8f, 0.3f),
            new Color(0.8f, 0.55f, 1f), new Color(0.35f, 0.95f, 0.95f), new Color(1f, 0.6f, 0.25f), new Color(0.95f, 0.95f, 0.95f),
        };

        private IRankingsDataProvider provider;
        private RectTransform pageRect, canvasRect, bottomNav, contentRoot, headerRibbon, seasonStrip, tabsRow;
        private RectTransform scrollContent, selfRoot;
        private ScrollRect scroll;
        private CanvasGroup pageGroup, seasonGroup, tabsGroup;
        private TextMeshProUGUI seasonText, loadingText;
        private readonly Image[] tabBgs = new Image[3];
        private readonly TextMeshProUGUI[] tabLabels = new TextMeshProUGUI[3];
        private readonly List<Tween> loops = new List<Tween>();
        private RankingBoard board = RankingBoard.Global;
        private LeaderboardResult current;
        private int requestId;
        private bool built, isOpen;
        private float u = 1f;
        private float nextSeasonTick;

        public IRankingsDataProvider Provider
        {
            get
            {
                if (provider == null) provider = new MockRankingsDataProvider();
                return provider;
            }
            set
            {
                provider = value;
                if (isOpen) Refresh(true);
            }
        }

        public RankingBoard CurrentBoard => board;

        // ═════════════════════════════ Lifecycle ═════════════════════════════

        private void Awake()
        {
            Instance = this;
            // Build only at runtime: AddComponent in the editor also runs Awake.
            if (Application.isPlaying) EnsureBuilt();
        }

        private void OnDestroy()
        {
            KillLoops();
            if (Instance == this) Instance = null;
        }

        private IEnumerator Start()
        {
            yield return null;   // let MainMenuController finish wiring its own tab listeners
            if (!hookBottomNav || canvasRect == null) yield break;

            var nav = FindDeep(canvasRect, "downpanel");
            if (nav == null) yield break;
            bottomNav = nav as RectTransform;

            // Keep the page under the nav bar, like the Shop does
            if (transform.parent == bottomNav.parent && transform.GetSiblingIndex() > bottomNav.GetSiblingIndex())
                transform.SetSiblingIndex(bottomNav.GetSiblingIndex());

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

        private void Update()
        {
            if (!isOpen || current == null || Time.unscaledTime < nextSeasonTick) return;
            nextSeasonTick = Time.unscaledTime + 1f;
            UpdateSeasonText();
        }

        private void OnNavTabClicked(int index)
        {
            if (index == rankingsTabIndex) Open();
            else Close();
        }

        // ═════════════════════════════ Public API ═════════════════════════════

        public void Open()
        {
            if (!Application.isPlaying) return;
            EnsureBuilt();
            bool wasOpen = isOpen;
            isOpen = true;
            ApplyNavInset();

            pageGroup.DOKill();
            pageGroup.blocksRaycasts = true;
            pageGroup.interactable = true;

            if (!wasOpen)
            {
                pageGroup.alpha = 0f;
                pageGroup.DOFade(1f, 0.2f).SetUpdate(true);

                headerRibbon.DOKill();
                headerRibbon.localScale = Vector3.one * 0.4f;
                headerRibbon.DOScale(1f, 0.5f).SetEase(Ease.OutBack, 1.8f).SetUpdate(true);

                FadeSlide(seasonGroup, seasonStrip, 0.12f);
                FadeSlide(tabsGroup, tabsRow, 0.2f);
            }

            UpdateTabs(false);
            Refresh(true);
        }

        public void Close()
        {
            if (!built || !isOpen) return;
            isOpen = false;
            requestId++;   // drop answers still in flight
            KillLoops();
            pageGroup.blocksRaycasts = false;
            pageGroup.interactable = false;
            pageGroup.DOKill();
            pageGroup.DOFade(0f, 0.15f).SetUpdate(true);
        }

        public void SetBoard(RankingBoard b)
        {
            if (board == b && current != null) return;
            board = b;
            UpdateTabs(true);
            Refresh(true);
        }

        // ═════════════════════════════ Data ═════════════════════════════

        private void Refresh(bool animate)
        {
            int id = ++requestId;
            KillLoops();
            ClearChildren(scrollContent);
            ClearChildren(selfRoot);
            current = null;
            loadingText.gameObject.SetActive(true);

            Provider.GetLeaderboard(board, listSize, result =>
            {
                if (id != requestId || !isOpen) return;
                current = result;
                Show(result, animate);
            });
        }

        private void Show(LeaderboardResult result, bool animate)
        {
            loadingText.gameObject.SetActive(false);
            UpdateSeasonText();

            if (result == null || result.entries == null || result.entries.Count == 0)
            {
                loadingText.gameObject.SetActive(true);
                loadingText.text = "Nobody is ranked yet.";
                BuildSelf(result != null ? result.self : null, animate);
                return;
            }

            BuildPodium(result.entries, animate);
            for (int i = 3; i < result.entries.Count; i++) BuildRow(result.entries[i], i - 3, animate);
            BuildSelf(result.self, animate);

            Canvas.ForceUpdateCanvases();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
        }

        private void UpdateSeasonText()
        {
            if (seasonText == null || current == null) return;
            var left = current.seasonEndsUtc - DateTime.UtcNow;
            if (left.TotalSeconds < 0) left = TimeSpan.Zero;
            string time = left.TotalDays >= 1
                ? $"{(int)left.TotalDays}d {left.Hours}h"
                : $"{left.Hours}h {left.Minutes:00}m";
            string where = current.board == RankingBoard.Local && !string.IsNullOrEmpty(current.regionName)
                ? current.regionName.ToUpperInvariant() + "  -  "
                : "";
            seasonText.text = $"<color=#FFD54A>{where}SEASON {current.seasonNumber}</color>\n<size=80%>Ends in {time}</size>";
        }

        // ═════════════════════════════ Build: page shell ═════════════════════════════

        private void EnsureBuilt()
        {
            if (built) return;
            built = true;

            pageRect = (RectTransform)transform;
            var rootCanvas = GetComponentInParent<Canvas>();
            canvasRect = rootCanvas != null ? rootCanvas.rootCanvas.transform as RectTransform : pageRect.parent as RectTransform;
            pageRect.anchorMin = Vector2.zero;
            pageRect.anchorMax = Vector2.one;
            pageRect.offsetMin = Vector2.zero;
            pageRect.offsetMax = Vector2.zero;

            float width = canvasRect != null && canvasRect.rect.width > 1f ? canvasRect.rect.width : REFERENCE_WIDTH;
            u = Mathf.Max(0.4f, width / REFERENCE_WIDTH);

            // Never use ?? with GetComponent: in the editor a missing component is a fake-null object.
            pageGroup = GetComponent<CanvasGroup>();
            if (pageGroup == null) pageGroup = gameObject.AddComponent<CanvasGroup>();
            pageGroup.alpha = 0f;
            pageGroup.blocksRaycasts = false;
            pageGroup.interactable = false;

            // Full-screen backdrop
            var bg = GetComponent<Image>();
            if (bg == null) bg = gameObject.AddComponent<Image>();
            bg.sprite = art.background;
            bg.color = art.background != null ? White : new Color(0.05f, 0.15f, 0.45f, 1f);
            bg.raycastTarget = true;
            var shade = Box(pageRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.02f, 0.05f, 0.18f, 0.25f), "Shade");
            shade.GetComponent<Image>().raycastTarget = false;

            // Everything above the nav bar
            contentRoot = Box(pageRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, null, "Content");

            BuildHeader();
            BuildSeasonStrip();
            BuildTabs();
            BuildScroll();

            selfRoot = Box(contentRoot, new Vector2(0, 0), new Vector2(1, 0), new Vector2(P(SIDE), P(SELF_BOTTOM)), new Vector2(P(-SIDE), P(SELF_BOTTOM + SELF_H)), null, "Self");
        }

        private void BuildHeader()
        {
            // Back button
            var back = Box(contentRoot, new Vector2(0, 1), new Vector2(0, 1), new Vector2(P(SIDE), P(-TOP_PAD - 104)), new Vector2(P(SIDE + 104), P(-TOP_PAD - 8)), null, "Back");
            var backImg = back.gameObject.AddComponent<Image>();
            backImg.sprite = art.backButton;
            backImg.preserveAspect = true;
            backImg.color = art.backButton != null ? White : new Color(0.1f, 0.2f, 0.5f, 1f);
            var backIcon = Box(back, new Vector2(0.22f, 0.24f), new Vector2(0.78f, 0.8f), Vector2.zero, Vector2.zero, null, "Icon");
            if (art.backIcon != null)
            {
                var ic = backIcon.gameObject.AddComponent<Image>();
                ic.sprite = art.backIcon;
                ic.preserveAspect = true;
                ic.raycastTarget = false;
            }
            else Fill(backIcon, "<", 48, White, TextAlignmentOptions.Center, true);
            MakeButton(back, Close);

            // Gold ribbon title
            headerRibbon = Box(contentRoot, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero, null, "Ribbon");
            headerRibbon.pivot = new Vector2(0.5f, 1f);
            headerRibbon.sizeDelta = new Vector2(P(RIBBON_W), P(RIBBON_H));
            headerRibbon.anchoredPosition = new Vector2(0f, P(-TOP_PAD + 4));
            var rib = headerRibbon.gameObject.AddComponent<Image>();
            rib.sprite = art.ribbon;
            rib.color = art.ribbon != null ? White : Gold;
            rib.raycastTarget = false;
            var ribText = Box(headerRibbon, new Vector2(0.14f, 0.4f), new Vector2(0.86f, 0.96f), Vector2.zero, Vector2.zero, null, "Title");
            Fill(ribText, "RANKINGS", 56, Brown, TextAlignmentOptions.Center, false, 30);

            // Trophy on the right, balancing the back button
            if (art.trophy != null)
            {
                var tr = Box(contentRoot, new Vector2(1, 1), new Vector2(1, 1), new Vector2(P(-SIDE - 100), P(-TOP_PAD - 100)), new Vector2(P(-SIDE), P(-TOP_PAD - 14)), null, "Trophy");
                var ti = tr.gameObject.AddComponent<Image>();
                ti.sprite = art.trophy;
                ti.preserveAspect = true;
                ti.raycastTarget = false;
                tr.DOScale(1.08f, 1.1f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(tr.gameObject);
            }
        }

        private void BuildSeasonStrip()
        {
            seasonStrip = Box(contentRoot, new Vector2(0, 1), new Vector2(1, 1), new Vector2(P(SIDE), P(-SEASON_TOP - SEASON_H)), new Vector2(P(-SIDE), P(-SEASON_TOP)), null, "Season");
            seasonGroup = seasonStrip.gameObject.AddComponent<CanvasGroup>();
            var img = seasonStrip.gameObject.AddComponent<Image>();
            SetPanel(img, art.seasonPanel, SEASON_H, new Color(0.08f, 0.14f, 0.4f, 0.95f));

            if (art.timerIcon != null)
            {
                var ic = Box(seasonStrip, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(P(26), P(-27)), new Vector2(P(72), P(27)), null, "Timer");
                var im = ic.gameObject.AddComponent<Image>();
                im.sprite = art.timerIcon;
                im.preserveAspect = true;
                im.raycastTarget = false;
            }

            var txt = Box(seasonStrip, Vector2.zero, Vector2.one, new Vector2(P(86), P(6)), new Vector2(P(-250), P(-6)), null, "Text");
            seasonText = Fill(txt, "", 28, White, TextAlignmentOptions.Left, true);
            seasonText.lineSpacing = -18f;

            var kings = Box(seasonStrip, new Vector2(1, 0), new Vector2(1, 1), new Vector2(P(-240), P(6)), new Vector2(P(-22), P(-6)), null, "Kings");
            if (art.kingsBadge != null)
            {
                var kb = Box(kings, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, P(-30)), new Vector2(P(58), P(30)), null, "Badge");
                var ki = kb.gameObject.AddComponent<Image>();
                ki.sprite = art.kingsBadge;
                ki.preserveAspect = true;
                ki.raycastTarget = false;
            }
            var kt = Box(kings, Vector2.zero, Vector2.one, new Vector2(P(64), 0), Vector2.zero, null, "Text");
            var kText = Fill(kt, "Top 100\n<size=80%><color=#FFD54A>KINGS LEAGUE</color></size>", 24, White, TextAlignmentOptions.Left, true);
            kText.lineSpacing = -18f;
        }

        private void BuildTabs()
        {
            tabsRow = Box(contentRoot, new Vector2(0, 1), new Vector2(1, 1), new Vector2(P(SIDE), P(-TABS_TOP - TABS_H)), new Vector2(P(-SIDE), P(-TABS_TOP)), null, "Tabs");
            tabsGroup = tabsRow.gameObject.AddComponent<CanvasGroup>();

            string[] labels = { "Global", "Local", "Clans" };
            Sprite[] icons = { art.tabGlobalIcon, art.tabLocalIcon, art.tabClansIcon };
            float gap = P(10);
            for (int i = 0; i < 3; i++)
            {
                float x0 = i / 3f, x1 = (i + 1) / 3f;
                var tab = Box(tabsRow, new Vector2(x0, 0), new Vector2(x1, 1), new Vector2(i == 0 ? 0 : gap * 0.5f, 0), new Vector2(i == 2 ? 0 : -gap * 0.5f, 0), null, "Tab_" + labels[i]);
                tabBgs[i] = tab.gameObject.AddComponent<Image>();

                var content = Box(tab, Vector2.zero, Vector2.one, new Vector2(P(10), P(14)), new Vector2(P(-10), P(-6)), null, "Inner");
                if (icons[i] != null)
                {
                    var ic = Box(content, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(P(8), P(-24)), new Vector2(P(56), P(24)), null, "Icon");
                    var im = ic.gameObject.AddComponent<Image>();
                    im.sprite = icons[i];
                    im.preserveAspect = true;
                    im.raycastTarget = false;
                }
                var lb = Box(content, Vector2.zero, Vector2.one, new Vector2(P(icons[i] != null ? 60 : 0), 0), Vector2.zero, null, "Label");
                tabLabels[i] = Fill(lb, labels[i], 32, White, TextAlignmentOptions.Center, true, 18);

                var b = (RankingBoard)i;
                MakeButton(tab, () => SetBoard(b));
            }
        }

        private void BuildScroll()
        {
            var view = Box(contentRoot, Vector2.zero, Vector2.one, new Vector2(0, P(SELF_BOTTOM + SELF_H + 8)), new Vector2(0, P(-SCROLL_TOP)), null, "Scroll");
            view.gameObject.AddComponent<RectMask2D>();
            var hit = view.gameObject.AddComponent<Image>();   // catches drags between rows
            hit.color = new Color(0f, 0f, 0f, 0f);

            scrollContent = Box(view, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero, null, "List");
            scrollContent.pivot = new Vector2(0.5f, 1f);
            var vlg = scrollContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(Mathf.RoundToInt(P(SIDE)), Mathf.RoundToInt(P(SIDE)), 0, Mathf.RoundToInt(P(30)));
            vlg.spacing = P(ROW_SPACING);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var fitter = scrollContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = scrollContent;
            scroll.viewport = view;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;

            loadingText = Fill(Box(view, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, null, "Loading"), "Loading rankings...", 32, SoftBlue, TextAlignmentOptions.Center, true);
        }

        // ═════════════════════════════ Build: podium ═════════════════════════════

        private void BuildPodium(List<RankedEntry> entries, bool animate)
        {
            var podium = Box(scrollContent, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero, null, "Podium");
            var le = podium.gameObject.AddComponent<LayoutElement>();
            le.minHeight = P(PODIUM_H);
            le.preferredHeight = P(PODIUM_H);

            // Draw 2nd and 3rd first so 1st (and its glow) sits on top
            int[] order = { 2, 3, 1 };
            float[] delays = { 0.1f, 0.2f, 0.34f };
            for (int k = 0; k < order.Length; k++)
            {
                int place = order[k];
                if (place - 1 < entries.Count) BuildPodiumSlot(podium, entries[place - 1], place, animate, delays[k]);
            }
        }

        private void BuildPodiumSlot(RectTransform podium, RankedEntry e, int place, bool animate, float delay)
        {
            bool first = place == 1;
            float cx = first ? 0.5f : (place == 2 ? 0.18f : 0.82f);
            float avatarSize = first ? 220f : 168f;
            float avatarY = first ? 228f : 280f;               // centre, from the podium top
            float medalSize = first ? 96f : 80f;
            float medalY = avatarY + avatarSize * 0.5f - 6f;
            float nameY = medalY + medalSize * 0.5f + (first ? 44f : 30f);
            float subY = nameY + 36f;
            float pillY = subY + 44f;

            var slot = Box(podium, new Vector2(cx, 1), new Vector2(cx, 1), new Vector2(P(-150), P(-PODIUM_H)), new Vector2(P(150), 0), null, "Place" + place);

            if (first)
            {
                // Slowly turning light rays + a soft pulsing glow behind the champion
                var rays = Place(slot, 0, avatarY, 10, 10, "Rays");
                for (int r = 0; r < 12; r++)
                {
                    var ray = Box(rays, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, null, "Ray");
                    ray.pivot = new Vector2(0.5f, 0f);
                    ray.sizeDelta = new Vector2(P(r % 2 == 0 ? 46 : 30), P(r % 2 == 0 ? 225 : 185));
                    ray.anchoredPosition = Vector2.zero;
                    ray.localRotation = Quaternion.Euler(0, 0, r * 30f);
                    var ri = ray.gameObject.AddComponent<Image>();
                    ri.sprite = SoftGlow();
                    ri.color = new Color(1f, 0.9f, 0.55f, r % 2 == 0 ? 0.3f : 0.2f);
                    ri.raycastTarget = false;
                }
                loops.Add(rays.DOLocalRotate(new Vector3(0, 0, -360f), 24f, RotateMode.FastBeyond360).SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart).SetUpdate(true));

                var glow = Place(slot, 0, avatarY, 430, 430, "Glow");
                var gi = glow.gameObject.AddComponent<Image>();
                gi.sprite = SoftGlow();
                gi.color = new Color(1f, 0.82f, 0.35f, 0.75f);
                gi.raycastTarget = false;
                loops.Add(glow.DOScale(1.08f, 1.4f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true));
                loops.Add(gi.DOFade(0.45f, 1.4f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true));
            }

            if (first && art.laurel != null)
            {
                var laurel = Place(slot, 0, medalY - 4, 200, 175, "Laurel");
                var li = laurel.gameObject.AddComponent<Image>();
                li.sprite = art.laurel;
                li.preserveAspect = true;
                li.raycastTarget = false;
            }

            // Avatar (or clan shield)
            var av = Place(slot, 0, avatarY, avatarSize, avatarSize, "Avatar");
            var avImg = av.gameObject.AddComponent<Image>();
            SetEntryPortrait(avImg, e);
            avImg.raycastTarget = false;

            // Medal with the place number
            var medal = Place(slot, 0, medalY, medalSize, medalSize, "Medal");
            var mi = medal.gameObject.AddComponent<Image>();
            mi.sprite = place == 2 ? art.medalSilver : art.medalGold;
            mi.color = place == 3 ? Bronze : White;
            mi.preserveAspect = true;
            mi.raycastTarget = false;
            if (mi.sprite == null) mi.color = place == 1 ? Gold : (place == 2 ? SoftBlue : Bronze);
            Fill(Box(medal, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, P(-2)), null, "N"), place.ToString(), first ? 50 : 42, Brown, TextAlignmentOptions.Center, false);

            // Crown on the champion
            RectTransform crown = null;
            if (first && art.crown != null)
            {
                crown = Place(slot, 0, avatarY - avatarSize * 0.5f - 22f, 130, 108, "Crown");
                var ci = crown.gameObject.AddComponent<Image>();
                ci.sprite = art.crown;
                ci.preserveAspect = true;
                ci.raycastTarget = false;
                crown.localRotation = Quaternion.Euler(0, 0, 6f);
            }

            // Name, subtitle and trophies
            var name = Place(slot, 0, nameY, 290, 48, "Name");
            Fill(name, e.name + (e.isLocal ? "  (You)" : ""), first ? 36 : 31, e.isLocal ? Gold : White, TextAlignmentOptions.Center, true, 18);
            var sub = Place(slot, 0, subY, 290, 30, "Sub");
            Fill(sub, e.subtitle ?? "", 22, SoftBlue, TextAlignmentOptions.Center, true, 14);

            var pill = Place(slot, 0, pillY, 236, 58, "Trophies");
            var pi = pill.gameObject.AddComponent<Image>();
            pi.sprite = art.trophyPill;
            pi.color = art.trophyPill != null ? White : new Color(0.04f, 0.08f, 0.25f, 0.9f);
            pi.raycastTarget = false;
            if (art.trophy != null)
            {
                var ti = Box(pill, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(P(12), P(-22)), new Vector2(P(62), P(22)), null, "Icon").gameObject.AddComponent<Image>();
                ti.sprite = art.trophy;
                ti.preserveAspect = true;
                ti.raycastTarget = false;
            }
            var trophyText = Fill(Box(pill, Vector2.zero, Vector2.one, new Vector2(P(60), 0), new Vector2(P(-14), 0), null, "Count"), Fmt(e.trophies), 30, Gold, TextAlignmentOptions.Center, true);

            if (!animate) return;

            slot.localScale = Vector3.one * 0.2f;
            slot.DOScale(1f, 0.55f).SetEase(Ease.OutBack, 1.6f).SetDelay(delay).SetUpdate(true);

            int target = e.trophies;
            trophyText.text = "0";
            DOVirtual.Float(0f, target, 0.9f, v => { if (trophyText != null) trophyText.text = Fmt(Mathf.RoundToInt(v)); })
                .SetDelay(delay + 0.2f).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(trophyText.gameObject);

            if (crown != null)
            {
                Vector2 end = crown.anchoredPosition;
                crown.anchoredPosition = end + new Vector2(0, P(220));
                crown.DOAnchorPos(end, 0.6f).SetEase(Ease.OutBounce).SetDelay(delay + 0.35f).SetUpdate(true);
                loops.Add(crown.DOLocalRotate(new Vector3(0, 0, -6f), 1.6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetDelay(delay + 1f).SetUpdate(true));
            }
        }

        // ═════════════════════════════ Build: rows ═════════════════════════════

        private void BuildRow(RankedEntry e, int index, bool animate)
        {
            var holder = Box(scrollContent, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero, null, "Row_" + e.rank);
            var le = holder.gameObject.AddComponent<LayoutElement>();
            le.minHeight = P(ROW_H);
            le.preferredHeight = P(ROW_H);

            var visual = Box(holder, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, null, "Visual");
            BuildEntryContents(visual, e, false);
            MakeButton(visual, null);

            if (!animate || index > 9) return;
            var cg = visual.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            float d = 0.35f + index * 0.05f;
            cg.DOFade(1f, 0.3f).SetDelay(d).SetUpdate(true);
            visual.anchoredPosition = new Vector2(P(160), 0);
            visual.DOAnchorPosX(0f, 0.45f).SetEase(Ease.OutCubic).SetDelay(d).SetUpdate(true);
        }

        private void BuildSelf(RankedEntry self, bool animate)
        {
            var visual = Box(selfRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, null, "Visual");
            if (self != null)
            {
                BuildEntryContents(visual, self, true);
            }
            else
            {
                var img = visual.gameObject.AddComponent<Image>();
                SetPanel(img, art.rowPanel, SELF_H, new Color(0.08f, 0.14f, 0.4f, 0.95f));
                string msg = board == RankingBoard.Clans ? "Join a clan to see your clan's rank" : "Win battles to get ranked!";
                Fill(Box(visual, Vector2.zero, Vector2.one, new Vector2(P(30), 0), new Vector2(P(-30), 0), null, "Text"), msg, 30, SoftBlue, TextAlignmentOptions.Center, true, 18);
            }

            if (!animate) return;
            visual.anchoredPosition = new Vector2(0, P(-SELF_H - 60));
            visual.DOAnchorPosY(0f, 0.55f).SetEase(Ease.OutBack, 1.2f).SetDelay(0.3f).SetUpdate(true);
        }

        /// <summary>Fills a row: rank, portrait, flag, name, subtitle, league badge, trophies.</summary>
        private void BuildEntryContents(RectTransform row, RankedEntry e, bool pinned)
        {
            var bg = row.gameObject.AddComponent<Image>();
            if (pinned)
            {
                SetPanel(bg, art.selfPanel != null ? art.selfPanel : art.rowPanel, SELF_H, SelfTint);
                if (art.selfPanel != null) bg.color = SelfTint;
            }
            else
            {
                SetPanel(bg, art.rowPanel, ROW_H, new Color(0.08f, 0.14f, 0.4f, 0.95f));
                if (e.isLocal && art.rowPanel != null) bg.color = LocalRowTint;
            }

            Color nameColor = pinned ? Brown : (e.isLocal ? Gold : White);
            Color subColor = pinned ? new Color(0.45f, 0.25f, 0.05f, 1f) : SoftBlue;
            bool outline = !pinned;

            // Rank number
            float rankW = pinned ? 150f : 100f;
            var rank = Box(row, new Vector2(0, 0), new Vector2(0, 1), new Vector2(P(14), 0), new Vector2(P(14 + rankW), 0), null, "Rank");
            string rankText = pinned ? "#" + Fmt(e.rank) : e.rank.ToString();
            Color rankColor = pinned ? Brown : (e.rank <= 10 ? Gold : White);
            Fill(rank, rankText, pinned ? 34 : 40, rankColor, TextAlignmentOptions.Center, outline, 18);

            // Portrait
            float portrait = pinned ? 96f : 90f;
            float px = 14f + rankW + 4f;
            var av = Box(row, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(P(px), P(-portrait * 0.5f)), new Vector2(P(px + portrait), P(portrait * 0.5f)), null, "Avatar");
            var avImg = av.gameObject.AddComponent<Image>();
            SetEntryPortrait(avImg, e);
            avImg.raycastTarget = false;

            if (!e.isClan && e.level > 0 && art.medalSilver != null)
            {
                var lv = Box(av, new Vector2(0, 0), new Vector2(0, 0), new Vector2(P(-10), P(-8)), new Vector2(P(32), P(34)), null, "Level");
                var li = lv.gameObject.AddComponent<Image>();
                li.sprite = art.medalSilver;
                li.preserveAspect = true;
                li.raycastTarget = false;
                Fill(Box(lv, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, null, "N"), e.level.ToString(), 18, Brown, TextAlignmentOptions.Center, false, 10);
            }

            // Name line (flag + name) and subtitle
            float textX = px + portrait + 16f;
            float rightReserve = e.isClan ? 230f : 312f;
            Sprite flag = e.flagIndex >= 0 && art.flags != null && e.flagIndex < art.flags.Length ? art.flags[e.flagIndex] : null;
            float nameX = textX;
            if (flag != null)
            {
                var fl = Box(row, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(P(textX), P(6)), new Vector2(P(textX + 36), P(42)), null, "Flag");
                var fi = fl.gameObject.AddComponent<Image>();
                fi.sprite = flag;
                fi.preserveAspect = true;
                fi.raycastTarget = false;
                nameX += 44f;
            }
            var name = Box(row, new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(P(nameX), 0), new Vector2(P(-rightReserve), P(-8)), null, "Name");
            string label = e.name + (pinned && !e.isClan ? "  (You)" : "");
            Fill(name, label, 32, nameColor, TextAlignmentOptions.BottomLeft, outline, 18);

            var sub = Box(row, new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(P(textX), P(10)), new Vector2(P(-rightReserve), P(-2)), null, "Sub");
            string subText = e.subtitle ?? "";
            if (!e.isClan)
            {
                var tier = PlayerProfileData.GetRank(e.trophies, IsTop100(e));
                subText = $"{subText}  -  {tier.label}";
            }
            Fill(sub, subText, 22, subColor, TextAlignmentOptions.TopLeft, outline, 14);

            // League badge (players)
            if (!e.isClan && art.rankBadges != null && art.rankBadges.Length > 0)
            {
                int bi = Mathf.Clamp(PlayerProfileData.GetRank(e.trophies, IsTop100(e)).badgeIndex, 0, art.rankBadges.Length - 1);
                var badge = art.rankBadges[bi];
                if (badge != null)
                {
                    var b = Box(row, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(P(-302), P(-38)), new Vector2(P(-226), P(38)), null, "Badge");
                    var bimg = b.gameObject.AddComponent<Image>();
                    bimg.sprite = badge;
                    bimg.preserveAspect = true;
                    bimg.raycastTarget = false;
                }
            }

            // Trophy pill
            var pill = Box(row, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(P(-218), P(-29)), new Vector2(P(-16), P(29)), null, "Trophies");
            var pimg = pill.gameObject.AddComponent<Image>();
            pimg.sprite = art.trophyPill;
            pimg.color = art.trophyPill != null ? White : new Color(0.04f, 0.08f, 0.25f, 0.9f);
            pimg.raycastTarget = false;
            if (art.trophy != null)
            {
                var ti = Box(pill, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(P(10), P(-20)), new Vector2(P(56), P(20)), null, "Icon").gameObject.AddComponent<Image>();
                ti.sprite = art.trophy;
                ti.preserveAspect = true;
                ti.raycastTarget = false;
            }
            Fill(Box(pill, Vector2.zero, Vector2.one, new Vector2(P(56), 0), new Vector2(P(-12), 0), null, "Count"), Fmt(e.trophies), 28, Gold, TextAlignmentOptions.Center, true, 16);
        }

        private bool IsTop100(RankedEntry e)
        {
            return current != null && current.board == RankingBoard.Global && e.rank > 0 && e.rank <= Siege.League.LeagueSystem.KINGS_TOP_N;
        }

        private void SetEntryPortrait(Image img, RankedEntry e)
        {
            img.preserveAspect = true;
            if (e.isClan)
            {
                img.sprite = art.clanShield;
                img.color = ClanColors[Mathf.Abs(e.clanBadge) % ClanColors.Length];
                return;
            }
            if (art.avatars != null && art.avatars.Length > 0)
            {
                img.sprite = art.avatars[Mathf.Abs(e.avatarIndex) % art.avatars.Length];
                img.color = White;
            }
            if (img.sprite == null) img.color = SoftBlue;
        }

        // ═════════════════════════════ Tabs ═════════════════════════════

        private void UpdateTabs(bool punch)
        {
            for (int i = 0; i < 3; i++)
            {
                if (tabBgs[i] == null) continue;
                bool on = (int)board == i;
                SetPanel(tabBgs[i], on ? art.tabOn : art.tabOff, TABS_H, on ? Gold : new Color(0.2f, 0.4f, 0.9f, 1f));
                if (tabLabels[i] != null)
                {
                    ApplyFont(tabLabels[i], !on);
                    tabLabels[i].color = on ? Brown : White;
                }
                if (punch && on)
                {
                    var rt = tabBgs[i].rectTransform;
                    rt.DOKill();
                    rt.localScale = Vector3.one;
                    rt.DOPunchScale(new Vector3(0.08f, 0.08f, 0f), 0.3f, 6, 0.5f).SetUpdate(true);
                }
            }
        }

        // ═════════════════════════════ Helpers ═════════════════════════════

        private void ApplyNavInset()
        {
            if (contentRoot == null) return;
            contentRoot.offsetMin = new Vector2(0f, ComputeNavInset());
        }

        /// <summary>Height of the visible nav bar: the top of its tab buttons, measured from the bottom of the page.</summary>
        private float ComputeNavInset()
        {
            if (bottomNav == null || !bottomNav.gameObject.activeInHierarchy) return 0f;
            var corners = new Vector3[4];
            float top = float.MinValue;
            for (int i = 0; i < bottomNav.childCount; i++)
            {
                var c = bottomNav.GetChild(i) as RectTransform;
                if (c == null || !c.gameObject.activeInHierarchy) continue;
                c.GetWorldCorners(corners);
                top = Mathf.Max(top, pageRect.InverseTransformPoint(corners[1]).y);
            }
            if (top == float.MinValue) return 0f;
            return Mathf.Clamp(top - pageRect.rect.yMin, 0f, pageRect.rect.height * 0.4f);
        }

        private void FadeSlide(CanvasGroup g, RectTransform r, float delay)
        {
            if (g == null || r == null) return;
            g.DOKill();
            g.alpha = 0f;
            g.DOFade(1f, 0.3f).SetDelay(delay).SetUpdate(true);
            Vector2 end = r.anchoredPosition;
            r.DOKill(true);
            end = r.anchoredPosition;
            r.anchoredPosition = end + new Vector2(0, P(30));
            r.DOAnchorPos(end, 0.4f).SetEase(Ease.OutCubic).SetDelay(delay).SetUpdate(true);
        }

        private void KillLoops()
        {
            foreach (var t in loops) if (t != null && t.IsActive()) t.Kill();
            loops.Clear();
        }

        private static Sprite softGlow;

        /// <summary>A soft white radial gradient, generated once (the art pack's glow orb has hard edges when scaled up).</summary>
        private static Sprite SoftGlow()
        {
            if (softGlow != null) return softGlow;
            const int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "KoS_SoftGlow" };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float dx = (x + 0.5f) / S * 2f - 1f, dy = (y + 0.5f) / S * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    a = a * a * (3f - 2f * a);
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            softGlow = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            return softGlow;
        }

        /// <summary>Pixel value scaled to the live canvas.</summary>
        private float P(float v) => v * u;

        private static string Fmt(int n) => n.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

        private RectTransform Box(Transform parent, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, Color? bg, string name = "Box")
        {
            var go = new GameObject(name, typeof(RectTransform));
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            r.anchorMin = aMin;
            r.anchorMax = aMax;
            r.offsetMin = oMin;
            r.offsetMax = oMax;
            if (bg.HasValue)
            {
                var img = go.AddComponent<Image>();
                img.color = bg.Value;
            }
            return r;
        }

        /// <summary>A rect of size (w, h) whose centre is x right of the parent's centre and y below its top.</summary>
        private RectTransform Place(RectTransform parent, float x, float yFromTop, float w, float h, string name)
        {
            var r = Box(parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero, null, name);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(P(w), P(h));
            r.anchoredPosition = new Vector2(P(x), P(-yFromTop));
            return r;
        }

        /// <summary>Sliced panel whose borders are scaled so they fit the given height.</summary>
        private void SetPanel(Image img, Sprite sprite, float heightRef, Color fallback)
        {
            img.sprite = sprite;
            if (sprite == null)
            {
                img.type = Image.Type.Simple;
                img.color = fallback;
                return;
            }
            img.color = White;
            Vector4 b = sprite.border;
            if (b.sqrMagnitude > 0f)
            {
                img.type = Image.Type.Sliced;
                img.fillCenter = true;
                float vertical = b.y + b.w;
                float h = P(heightRef);
                img.pixelsPerUnitMultiplier = vertical > 0f && h > 0f ? Mathf.Max(0.1f, vertical / (h * 0.96f)) : 1f;
            }
            else
            {
                img.type = Image.Type.Simple;
            }
        }

        private TextMeshProUGUI Fill(RectTransform parent, string text, float size, Color color, TextAlignmentOptions align, bool outline, float minSize = 0f)
        {
            var child = Box(parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, null, "Text");
            var t = child.gameObject.AddComponent<TextMeshProUGUI>();
            ApplyFont(t, outline);
            t.text = text;
            t.fontSize = P(size);
            if (minSize > 0f)
            {
                t.enableAutoSizing = true;
                t.fontSizeMax = P(size);
                t.fontSizeMin = P(minSize);
            }
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            return t;
        }

        private void ApplyFont(TextMeshProUGUI t, bool outline)
        {
            var f = outline ? (outlineFont != null ? outlineFont : titleFont) : (titleFont != null ? titleFont : outlineFont);
            if (f != null) t.font = f;
        }

        private static void MakeButton(RectTransform target, Action onClick)
        {
            var btn = target.gameObject.GetComponent<Button>();
            if (btn == null) btn = target.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                target.DOKill(true);
                target.localScale = Vector3.one;
                target.DOPunchScale(new Vector3(-0.05f, -0.05f, 0f), 0.22f, 6, 0.6f).SetUpdate(true);
                onClick?.Invoke();
            });
        }

        private static void ClearChildren(Transform t)
        {
            if (t == null) return;
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var go = t.GetChild(i).gameObject;
                if (go.name == "Loading") continue;
                go.SetActive(false);   // drop out of the layout this frame
                Destroy(go);
            }
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent == null) return null;
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var r = FindDeep(parent.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
