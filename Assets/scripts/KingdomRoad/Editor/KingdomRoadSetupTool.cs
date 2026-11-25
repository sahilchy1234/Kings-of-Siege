#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Siege.KingdomRoad;
using Siege.Profile;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Siege.KingdomRoad.EditorTools
{
    /// <summary>
    /// Menu: Kings of Siege > Kingdom Road > Build Kingdom Road Panel
    /// Builds the whole panel as prebuilt GameObjects under the main Canvas (laid out to match the
    /// Kingdom Road mockup) and wires the league banner ("leauge") on the home screen to open it.
    /// Re-running it rebuilds the panel from scratch.
    /// </summary>
    public static class KingdomRoadSetupTool
    {
        private const string ROOT_NAME = "[KingdomRoadScreen]";
        private const string ART_DIR = "Assets/scripts/KingdomRoad/Art";

        // Layout (canvas units, tuned for a 1080-wide portrait screen)
        private const float ROW_H = 150f;
        private const float BANNER_H = 150f;
        private const float MARKER_H = 80f;
        private const float THRESH_RIGHT = 170f;   // right edge of the trophy numbers
        private const float LINE_X = 228f;         // timeline x
        private const float ICON_X = 390f;         // reward icon centre
        private const float TEXT_X = 510f;         // reward title left edge

        private static readonly Color BgTop = new Color(0.106f, 0.224f, 0.475f, 1f);
        private static readonly Color BgBottom = new Color(0.047f, 0.110f, 0.290f, 1f);
        private static readonly Color Teal = new Color(0.33f, 0.88f, 0.95f, 1f);
        private static readonly Color Muted = new Color(0.66f, 0.74f, 0.88f, 1f);
        private static readonly Color LineCol = new Color(0.62f, 0.69f, 0.84f, 0.55f);
        private static readonly Color DotCol = new Color(0.72f, 0.78f, 0.90f, 1f);
        private static readonly Color Green = new Color(0.20f, 0.74f, 0.36f, 1f);
        private static readonly Color Gold = new Color(0.98f, 0.80f, 0.30f, 1f);

        private static TMP_FontAsset _font;
        private static Sprite _circle, _bgGradient, _band, _lock, _check;

        [MenuItem("Kings of Siege/Kingdom Road/Build Kingdom Road Panel")]
        public static void Build()
        {
            Canvas canvas = null;
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.gameObject.name == "Canvas") { canvas = c; break; }
            if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("Kingdom Road", "No Canvas found in the active scene. Open MainMenu first.", "OK");
                return;
            }

            var old = GameObject.Find(ROOT_NAME);
            if (old != null) Object.DestroyImmediate(old);

            // ── Art
            EnsureArt();
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UIPACK/ResourcesData/Fonts/TMP_LINESeedSans.asset");
            if (_font == null) _font = TMP_Settings.defaultFontAsset;
            _lock = Load("Assets/UIPACK/ResourcesData/Sprites/Components/IconMisc/Icon_Lock01.png");
            _check = Load("Assets/UIPACK/ResourcesData/Sprites/Components/IconMisc/Icon_Check.png");

            var icons = new Dictionary<string, Sprite>
            {
                { "coins", Load("Assets/UIPACK/Kings_of_Siege_Store_PNG_Elements/Products/Coin_Stack.png") },
                { "silver", Load("Assets/UIPACK/Kings_of_Siege_Store_PNG_Elements/Products/Silver_Chest.png") },
                { "gold", Load("Assets/UIPACK/Kings_of_Siege_Store_PNG_Elements/Products/Gold_Chest.png") },
                { "cards", Load("Assets/UIPACK/Kings_of_Siege_Home_UI_PNGs/Icons/cards.png") },
                { "firearrows", Load("Assets/UIPACK/Kings_of_Siege_10_Power_Cards_No_Text/02_Fire_Arrows.png") },
            };
            Sprite shWood = Load("Assets/UIPACK/battlevs/png/league_starter_wood.png");
            Sprite shSilver = Load("Assets/UIPACK/battlevs/png/league_silver.png");
            Sprite shGold = Load("Assets/UIPACK/battlevs/png/league_gold.png");
            Sprite shLegend = Load("Assets/UIPACK/battlevs/png/league_legend.png");
            Sprite shChamp = Load("Assets/UIPACK/battlevs/png/league_champion.png");
            // Wood, Silver, Gold, Platinum, Tiel, Epic, Super, Legend, Champions
            var shields = new[] { shWood, shSilver, shGold, shGold, shGold, shLegend, shLegend, shLegend, shChamp };
            // 28 UIPACK badges, slot order = PlayerProfileData.RankTier.badgeIndex (27 = Kings)
            var badges = new Sprite[PlayerProfileData.RANK_BADGE_COUNT];
            for (int i = 0; i < badges.Length; i++)
                badges[i] = Load("Assets/UIPACK/badges/" + PlayerProfileData.RankBadgeFileNames[i] + ".png");

            // ── Root (always active: holds the manager / Open entry point)
            var rootRt = NewRect(ROOT_NAME, canvas.transform);
            Stretch(rootRt, 0, 0, 0, 0);
            var manager = rootRt.gameObject.AddComponent<KingdomRoadManager>();
            manager.leagueShields = shields;
            manager.rankBadgeByDivision = badges;

            // ── Window: transparent full-screen holder (HUD and nav bar stay visible)
            var windowRt = NewRect("Window", rootRt);
            Stretch(windowRt, 0, 0, 0, 0);
            manager.window = windowRt.gameObject;
            manager.canvasGroup = windowRt.gameObject.AddComponent<CanvasGroup>();

            // ── Panel between the top HUD and the bottom nav
            var panel = NewImage("Panel", windowRt, Color.white, _bgGradient);
            panel.rectTransform.anchorMin = new Vector2(0f, 0.092f);
            panel.rectTransform.anchorMax = new Vector2(1f, 0.885f);
            panel.rectTransform.offsetMin = Vector2.zero;
            panel.rectTransform.offsetMax = Vector2.zero;
            manager.panel = panel.rectTransform;
            var p = panel.rectTransform;

            // Title + close
            var title = NewText("Title", p, "Kingdom Road", 64, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -44), new Vector2(700, 80));

            var closeRt = NewRect("Close", p);
            Place(closeRt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -42), new Vector2(84, 84));
            var closeHit = closeRt.gameObject.AddComponent<Image>();
            closeHit.color = new Color(0, 0, 0, 0);
            var closeBtn = closeRt.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = closeHit;
            for (int i = 0; i < 2; i++)
            {
                var bar = NewImage("Bar" + i, closeRt, Color.white, _circle);
                bar.type = Image.Type.Sliced;
                bar.raycastTarget = false;
                Place(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56, 10));
                bar.rectTransform.localRotation = Quaternion.Euler(0, 0, i == 0 ? 45f : -45f);
            }
            manager.closeButton = closeBtn;

            // ── Header
            manager.headerShield = NewImage("HeaderShield", p, Color.white, shWood);
            manager.headerShield.preserveAspect = true;
            Place(manager.headerShield.rectTransform, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(180, -275), new Vector2(230, 230));
            manager.headerRoman = NewText("Roman", manager.headerShield.rectTransform, "II", 50, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(manager.headerRoman.rectTransform, 0, 0, 0, 0);
            manager.headerRoman.outlineWidth = 0.25f;
            manager.headerRoman.outlineColor = new Color32(40, 20, 5, 255);

            manager.headerLeague = NewText("HeaderLeague", p, "Wood II", 54, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
            Place(manager.headerLeague.rectTransform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(312, -205), new Vector2(560, 64));
            manager.headerTrophies = NewText("HeaderTrophies", p, "0 trophies", 38, new Color(0.92f, 0.95f, 1f), FontStyles.Normal, TextAlignmentOptions.Left);
            Place(manager.headerTrophies.rectTransform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(312, -256), new Vector2(560, 48));

            var progBg = NewImage("ProgressBg", p, new Color(0.45f, 0.53f, 0.70f, 0.55f), _circle);
            progBg.type = Image.Type.Sliced;
            Place(progBg.rectTransform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(312, -304), new Vector2(440, 18));
            var fill = NewImage("ProgressFill", progBg.rectTransform, Teal, _circle);
            fill.type = Image.Type.Sliced;
            manager.progressFill = fill.rectTransform;
            manager.progressFill.anchorMin = Vector2.zero;
            manager.progressFill.anchorMax = new Vector2(0.6f, 1f);
            manager.progressFill.offsetMin = Vector2.zero;
            manager.progressFill.offsetMax = Vector2.zero;

            manager.headerNext = NewText("HeaderNext", p, "", 32, new Color(0.62f, 0.78f, 0.86f), FontStyles.Normal, TextAlignmentOptions.Left);
            Place(manager.headerNext.rectTransform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(312, -346), new Vector2(560, 44));

            // ── Timeline scroll view
            var scrollRt = NewRect("Scroll", p);
            Stretch(scrollRt, 0, 120, 0, 400);
            var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            var viewRt = NewRect("Viewport", scrollRt);
            Stretch(viewRt, 0, 0, 0, 0);
            viewRt.gameObject.AddComponent<RectMask2D>();
            var viewImg = viewRt.gameObject.AddComponent<Image>();
            viewImg.color = new Color(0, 0, 0, 0);
            var contentRt = NewRect("Content", viewRt);
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;
            var vlg = contentRt.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(0, 0, 20, 40);
            contentRt.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewRt;
            scroll.content = contentRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 30f;
            manager.scroll = scroll;
            manager.content = contentRt;

            // ── Rows, highest trophies first
            var rowList = new List<KingdomRoadRow>();
            var milestones = KingdomRoadData.Build();
            for (int n = KingdomRoadData.DIVISION_COUNT - 1; n >= 0; n--)
            {
                int b = Siege.League.LeagueSystem.DIVISION_SIZE * n;
                for (int i = milestones.Count - 1; i >= 0; i--)
                {
                    var m = milestones[i];
                    if (m.threshold <= b || m.threshold > b + 100) continue;
                    rowList.Add(BuildRewardRow(contentRt, m, icons, badges[n], RomanOf(n)));
                }
                rowList.Add(BuildBannerRow(contentRt, n, KingdomRoadData.DivisionStart(n), badges[n]));
            }
            manager.rows = rowList.ToArray();

            // ── "You" marker (moved into place at runtime)
            var markerRt = NewRect("PlayerMarker", contentRt);
            var mle = markerRt.gameObject.AddComponent<LayoutElement>();
            mle.preferredHeight = MARKER_H;
            mle.minHeight = MARKER_H;
            AddLine(markerRt);
            var mDot = NewImage("Dot", markerRt, Teal, _circle);
            Place(mDot.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(LINE_X, 0), new Vector2(22, 22));
            manager.playerMarkerText = NewText("MarkerText", markerRt, "You · 0", 38, Teal, FontStyles.Bold, TextAlignmentOptions.Left);
            Place(manager.playerMarkerText.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(LINE_X + 44, 0), new Vector2(500, 50));
            var mSep = NewImage("Separator", markerRt, new Color(1, 1, 1, 0.16f));
            mSep.rectTransform.anchorMin = new Vector2(0, 0);
            mSep.rectTransform.anchorMax = new Vector2(1, 0);
            mSep.rectTransform.offsetMin = new Vector2(LINE_X + 60, 0);
            mSep.rectTransform.offsetMax = new Vector2(-60, 2);
            manager.playerMarker = markerRt;
            manager.markerBand = null;

            // ── Footer hint
            var hintRt = NewRect("ScrollHint", p);
            Place(hintRt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(520, 50));
            var chevron = NewImage("Chevron", hintRt, Muted, Load("Assets/UIPACK/ResourcesData/Sprites/Components/IconMisc/Icon_ArrowNext.png"));
            chevron.preserveAspect = true;
            Place(chevron.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(110, 0), new Vector2(30, 30));
            chevron.rectTransform.localRotation = Quaternion.Euler(0, 0, -90f);
            var hint = NewText("Label", hintRt, "Scroll to explore", 32, Muted, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(hint.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(145, 0), new Vector2(360, 44));

            WireEntryButton(manager);

            windowRt.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[KingdomRoadSetupTool] Built " + manager.rows.Length + " rows under " + ROOT_NAME + ".");
        }

        // ───────────────────────────── Debug (Play mode) ─────────────────────────────

        private const string PREV_TROPHIES = "KoS_KR_Debug_PrevTrophies";

        [MenuItem("Kings of Siege/Kingdom Road/Debug/Set Trophies 165")]
        public static void DebugSetTrophies()
        {
            if (!EditorPrefs.HasKey(PREV_TROPHIES)) EditorPrefs.SetInt(PREV_TROPHIES, PlayerPrefs.GetInt("trophies", 0));
            PlayerPrefs.SetInt("trophies", 165);
            PlayerPrefs.Save();
            var m = Object.FindFirstObjectByType<KingdomRoadManager>();
            if (Application.isPlaying && m != null && m.window != null && m.window.activeSelf) m.Refresh();
            Debug.Log("[KingdomRoad] Trophies set to 165 (was " + EditorPrefs.GetInt(PREV_TROPHIES) + ").");
        }

        [MenuItem("Kings of Siege/Kingdom Road/Debug/Restore Trophies")]
        public static void DebugRestoreTrophies()
        {
            if (!EditorPrefs.HasKey(PREV_TROPHIES)) return;
            PlayerPrefs.SetInt("trophies", EditorPrefs.GetInt(PREV_TROPHIES));
            PlayerPrefs.Save();
            Debug.Log("[KingdomRoad] Trophies restored to " + EditorPrefs.GetInt(PREV_TROPHIES) + ".");
            EditorPrefs.DeleteKey(PREV_TROPHIES);
        }

        [MenuItem("Kings of Siege/Kingdom Road/Debug/Open Panel (Play mode)")]
        public static void DebugOpen()
        {
            var m = Object.FindFirstObjectByType<KingdomRoadManager>();
            if (!Application.isPlaying || m == null) { Debug.LogWarning("[KingdomRoad] Enter Play mode first."); return; }
            m.Open();
            Debug.Log("[KingdomRoad] Opened. window active = " + m.window.activeSelf);
        }

        [MenuItem("Kings of Siege/Kingdom Road/Debug/Claim Next Reachable (Play mode)")]
        public static void DebugClaim()
        {
            var m = Object.FindFirstObjectByType<KingdomRoadManager>();
            if (!Application.isPlaying || m == null) { Debug.LogWarning("[KingdomRoad] Enter Play mode first."); return; }
            int trophies = PlayerPrefs.GetInt("trophies", 0);
            for (int i = m.rows.Length - 1; i >= 0; i--)
            {
                var r = m.rows[i];
                if (r.isDivisionBanner || r.threshold > trophies || PlayerPrefs.GetInt(r.ClaimKey, 0) == 1) continue;
                int coins = Siege.Shop.PlayerWallet.Coins;
                r.OnTap();
                Debug.Log("[KingdomRoad] Claimed " + r.threshold + " (" + r.rewardTitle + "). claimed=" + PlayerPrefs.GetInt(r.ClaimKey, 0) +
                          " coins " + coins + " -> " + Siege.Shop.PlayerWallet.Coins + " subtitle='" + r.subtitleText.text + "'");
                return;
            }
            Debug.Log("[KingdomRoad] Nothing to claim.");
        }

        [MenuItem("Kings of Siege/Kingdom Road/Debug/Close Panel (Play mode)")]
        public static void DebugClose()
        {
            var m = Object.FindFirstObjectByType<KingdomRoadManager>();
            if (m != null) m.Close();
        }

        [MenuItem("Kings of Siege/Kingdom Road/Debug/Reset Claims")]
        public static void DebugResetClaims()
        {
            foreach (var ms in KingdomRoadData.Build()) PlayerPrefs.DeleteKey("KoS_KR_Claimed_" + ms.threshold);
            PlayerPrefs.Save();
            Debug.Log("[KingdomRoad] Claims reset.");
        }

        // ───────────────────────────── Rows ─────────────────────────────

        private static KingdomRoadRow BuildRewardRow(RectTransform content, Milestone m, Dictionary<string, Sprite> icons, Sprite shield, string roman)
        {
            var root = NewRect("Reward_" + m.threshold, content);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = ROW_H;
            le.minHeight = ROW_H;
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;
            btn.transition = Selectable.Transition.None;
            var row = root.gameObject.AddComponent<KingdomRoadRow>();
            row.isDivisionBanner = false;
            row.threshold = m.threshold;
            row.kind = m.kind;
            row.rewardTitle = m.title;
            row.amount = m.amount;
            row.button = btn;
            UnityEventTools.AddVoidPersistentListener(btn.onClick, row.OnTap);

            // "Next reward" highlight band (shown by the manager)
            row.background = NewImage("NextBand", root, new Color(0.22f, 0.45f, 0.82f, 0.55f), _band);
            row.background.raycastTarget = false;
            Stretch(row.background.rectTransform, 40, 0, 0, 0);
            row.background.gameObject.SetActive(false);

            // Claimable highlight (gold band)
            row.glow = NewImage("ClaimBand", root, new Color(Gold.r, Gold.g, Gold.b, 0.18f), _band);
            row.glow.raycastTarget = false;
            Stretch(row.glow.rectTransform, 40, 0, 0, 0);
            row.glow.gameObject.SetActive(false);

            AddLine(root);
            row.dot = NewImage("Dot", root, DotCol, _circle);
            Place(row.dot.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(LINE_X, 0), new Vector2(22, 22));
            row.thresholdText = NewText("Threshold", root, m.threshold.ToString(), 46, Color.white, FontStyles.Bold, TextAlignmentOptions.Right);
            Place(row.thresholdText.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(THRESH_RIGHT, 0), new Vector2(160, 60));

            Sprite iconSprite;
            float iconSize = 200f;   // store/chest art has transparent padding
            if (m.kind == RewardKind.Badge) iconSprite = shield;
            else if (m.kind == RewardKind.Coins) iconSprite = icons["coins"];
            else if (m.kind == RewardKind.SilverChest) iconSprite = icons["silver"];
            else if (m.kind == RewardKind.GoldChest) iconSprite = icons["gold"];
            else if (m.title.IndexOf("Fire Arrows", StringComparison.OrdinalIgnoreCase) >= 0) { iconSprite = icons["firearrows"]; iconSize = 130f; }
            else { iconSprite = icons["cards"]; iconSize = 190f; }   // cards.png has wide transparent padding

            row.icon = NewImage("Icon", root, Color.white, iconSprite);
            row.icon.preserveAspect = true;
            row.icon.raycastTarget = false;
            Place(row.icon.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(ICON_X, 0), new Vector2(iconSize, iconSize));
            if (m.kind == RewardKind.Badge)
            {
                var rn = NewText("Roman", row.icon.rectTransform, roman, 40, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
                Stretch(rn.rectTransform, 0, 0, 0, 0);
                rn.outlineWidth = 0.25f;
                rn.outlineColor = new Color32(40, 20, 5, 255);
            }

            row.titleText = NewText("Title", root, m.title, 46, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
            Place(row.titleText.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(TEXT_X, 0), new Vector2(470, 58));
            row.titleText.overflowMode = TextOverflowModes.Ellipsis;
            row.subtitleText = NewText("Subtitle", root, "", 36, Muted, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(row.subtitleText.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(TEXT_X, -26), new Vector2(480, 46));

            var lockImg = NewImage("Lock", root, new Color(0.80f, 0.85f, 0.95f, 1f), _lock);
            lockImg.preserveAspect = true;
            lockImg.raycastTarget = false;
            float titleW = Mathf.Min(row.titleText.GetPreferredValues(m.title).x, 470f);
            Place(lockImg.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(TEXT_X + titleW + 44f, 0), new Vector2(36, 42));
            row.lockIcon = lockImg.gameObject;

            // Green check circle in front of "Claimed"
            var checkBg = NewImage("Claimed", root, Green, _circle);
            checkBg.raycastTarget = false;
            Place(checkBg.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(TEXT_X, -26), new Vector2(36, 36));
            var checkMark = NewImage("Check", checkBg.rectTransform, Color.white, _check);
            checkMark.preserveAspect = true;
            checkMark.raycastTarget = false;
            Stretch(checkMark.rectTransform, 7, 7, 7, 7);
            checkBg.gameObject.SetActive(false);
            row.claimedIcon = checkBg.gameObject;

            var sep = NewImage("Separator", root, new Color(1, 1, 1, 0.12f));
            sep.raycastTarget = false;
            sep.rectTransform.anchorMin = new Vector2(0, 0);
            sep.rectTransform.anchorMax = new Vector2(1, 0);
            sep.rectTransform.offsetMin = new Vector2(LINE_X + 60, 0);
            sep.rectTransform.offsetMax = new Vector2(-60, 2);

            return row;
        }

        private static KingdomRoadRow BuildBannerRow(RectTransform content, int n, int threshold, Sprite shield)
        {
            var root = NewRect("Division_" + threshold, content);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = BANNER_H;
            le.minHeight = BANNER_H;
            var row = root.gameObject.AddComponent<KingdomRoadRow>();
            row.isDivisionBanner = true;
            row.threshold = threshold;

            AddLine(root);

            // Horizontal line from the timeline dot to the badge
            var hl = NewImage("Divider", root, new Color(0.80f, 0.85f, 0.95f, 0.8f));
            hl.raycastTarget = false;
            hl.rectTransform.anchorMin = new Vector2(0, 0.5f);
            hl.rectTransform.anchorMax = new Vector2(1, 0.5f);
            hl.rectTransform.offsetMin = new Vector2(LINE_X, -1.5f);
            hl.rectTransform.offsetMax = new Vector2(-150, 1.5f);

            row.dot = NewImage("Dot", root, DotCol, _circle);
            Place(row.dot.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(LINE_X, 0), new Vector2(22, 22));
            row.thresholdText = NewText("Threshold", root, threshold.ToString(), 46, Color.white, FontStyles.Bold, TextAlignmentOptions.Right);
            Place(row.thresholdText.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(THRESH_RIGHT, 0), new Vector2(160, 60));

            row.titleText = NewText("Title", root, KingdomRoadData.DivisionName(n), 36, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            row.titleText.rectTransform.anchorMin = new Vector2(0, 0.5f);
            row.titleText.rectTransform.anchorMax = new Vector2(1, 0.5f);
            row.titleText.rectTransform.offsetMin = new Vector2(LINE_X + 280, 10);
            row.titleText.rectTransform.offsetMax = new Vector2(-170, 56);
            row.subtitleText = NewText("Subtitle", root, "Division begins", 32, Muted, FontStyles.Normal, TextAlignmentOptions.Center);
            row.subtitleText.rectTransform.anchorMin = new Vector2(0, 0.5f);
            row.subtitleText.rectTransform.anchorMax = new Vector2(1, 0.5f);
            row.subtitleText.rectTransform.offsetMin = new Vector2(LINE_X + 280, -54);
            row.subtitleText.rectTransform.offsetMax = new Vector2(-170, -10);

            row.divisionBadge = NewImage("DivisionBadge", root, Color.white, shield);
            row.divisionBadge.preserveAspect = true;
            row.divisionBadge.raycastTarget = false;
            Place(row.divisionBadge.rectTransform, new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-95, 0), new Vector2(170, 170));
            var rn = NewText("Roman", row.divisionBadge.rectTransform, RomanOf(n), 36, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(rn.rectTransform, 0, 0, 0, 0);
            rn.outlineWidth = 0.25f;
            rn.outlineColor = new Color32(40, 20, 5, 255);

            return row;
        }

        private static string RomanOf(int n)
        {
            string[] r = { "I", "II", "III" };
            return r[n % 3];
        }

        private static void AddLine(RectTransform parent)
        {
            var line = NewImage("Line", parent, LineCol);
            line.raycastTarget = false;
            var rt = line.rectTransform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 1);
            rt.offsetMin = new Vector2(LINE_X - 1.5f, 0);
            rt.offsetMax = new Vector2(LINE_X + 1.5f, 0);
        }

        // ───────────────────────────── Entry point ─────────────────────────────

        private static void WireEntryButton(KingdomRoadManager manager)
        {
            Transform banner = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == "leauge" && t.gameObject.scene == SceneManager.GetActiveScene()) { banner = t; break; }
            }
            if (banner == null)
            {
                Debug.LogWarning("[KingdomRoadSetupTool] League banner 'leauge' not found; panel built but not wired.");
                return;
            }
            var btn = banner.GetComponent<Button>();
            if (btn == null) btn = banner.gameObject.AddComponent<Button>();
            btn.targetGraphic = banner.GetComponent<Image>();
            // Drop stale listeners left by earlier builds (their target was destroyed).
            for (int i = btn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(btn.onClick, i);
            UnityEventTools.AddVoidPersistentListener(btn.onClick, manager.Open);
            EditorUtility.SetDirty(btn);
        }

        // ───────────────────────────── Generated art ─────────────────────────────

        private static void EnsureArt()
        {
            if (!Directory.Exists(ART_DIR)) Directory.CreateDirectory(ART_DIR);

            _circle = Generate(ART_DIR + "/KR_Circle.png", 64, 64, (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32, 32));
                return new Color(1, 1, 1, Mathf.Clamp01(31.5f - d));
            }, new Vector4(31, 31, 31, 31));

            _bgGradient = Generate(ART_DIR + "/KR_PanelGradient.png", 8, 256, (x, y) =>
            {
                float t = y / 255f;
                return Color.Lerp(BgBottom, BgTop, Mathf.SmoothStep(0f, 1f, t));
            }, Vector4.zero);

            _band = Generate(ART_DIR + "/KR_Band.png", 256, 8, (x, y) =>
            {
                float t = x / 255f;
                float a = t < 0.25f ? Mathf.SmoothStep(0f, 1f, t / 0.25f) : Mathf.Lerp(1f, 0.25f, (t - 0.25f) / 0.75f);
                return new Color(1, 1, 1, a);
            }, Vector4.zero);
        }

        private static Sprite Generate(string path, int w, int h, Func<int, int, Color> pixel, Vector4 border)
        {
            if (!File.Exists(path))
            {
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        tex.SetPixel(x, y, pixel(x, y));
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp != null && (imp.textureType != TextureImporterType.Sprite || imp.spriteBorder != border))
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spriteBorder = border;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ───────────────────────────── Helpers ─────────────────────────────

        private static Sprite Load(string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null) Debug.LogWarning("[KingdomRoadSetupTool] Sprite not found: " + path);
            return s;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image NewImage(string name, Transform parent, Color c, Sprite s = null)
        {
            var img = NewRect(name, parent).gameObject.AddComponent<Image>();
            img.color = c;
            img.sprite = s;
            return img;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color c, FontStyles style, TextAlignmentOptions align)
        {
            var t = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (_font != null) t.font = _font;
            t.text = text;
            t.fontSize = size;
            t.color = c;
            t.fontStyle = style;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }

        private static void Stretch(RectTransform rt, float l, float b, float r, float t)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
#endif
