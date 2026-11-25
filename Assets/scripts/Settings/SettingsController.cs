using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Siege.Profile;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Siege.Settings
{
    /// <summary>Sprites for the Settings panel. Kings of Siege > Settings > Build Settings Panel fills these in.</summary>
    [Serializable]
    public class SettingsArt
    {
        [Header("Window")]
        public Sprite panel;          // battlevs wide_status_panel (navy, gold bevel, sliced)
        public Sprite ribbon;         // Label_Ribbon01_Yellow
        public Sprite gear;           // Home Icons/settings
        public Sprite buttonBlue;     // Button01_Demo_Blue (sliced)
        public Sprite buttonRed;      // Button01_Demo_Red (sliced)

        [Header("Icons")]
        public Sprite iconMusic;      // Icon_Music
        public Sprite iconSound;      // Icon_Sound
        public Sprite iconVibration;  // Icon_Bell
        public Sprite iconGraphics;   // Icon_Picture
        public Sprite iconFps;        // Icon_Timer
        public Sprite iconName;       // Icon_Body
        public Sprite iconId;         // Icon_Ticket
        public Sprite iconLanguage;   // Icon_Flag
        public Sprite iconPencil;     // Icon_Pencil
        public Sprite iconClose;      // Icon_Close01
        public Sprite iconCredits;    // Icon_Book
        public Sprite iconReset;      // Icon_Arrow_Back
        public Sprite iconCheck;      // Icon_Check
        public Sprite flagEnglish;    // Icon_Flag_Eng
    }

    /// <summary>
    /// Kings of Siege Settings popup: audio (music / sound effects with mute), game (vibration,
    /// graphics quality, frame rate), account (name, player id, language), credits and reset.
    /// Built at runtime in its own sorting canvas, opened by the gear button in the top bar
    /// (Canvas/Panel/toppanel/settings). Values are stored through KoSSettings / KoSAudio.
    /// </summary>
    public class SettingsController : MonoBehaviour
    {
        public static SettingsController Instance { get; private set; }

        [Header("Wiring (set by Kings of Siege > Settings > Build Settings Panel)")]
        public SettingsArt art = new SettingsArt();
        [Tooltip("TMP_TiltWarp (plain).")]
        public TMP_FontAsset titleFont;
        [Tooltip("TMP_TiltWarp_Outline (white text with a dark outline).")]
        public TMP_FontAsset outlineFont;
        [Tooltip("Hook the gear button named settingsButtonName under the top bar.")]
        public bool hookSettingsButton = true;
        public string settingsButtonName = "settings";
        public int sortingOrder = 60;

        // ── Layout (reference units, multiplied by u) ──
        private const float REFERENCE_WIDTH = 941f;
        private const float WIN_W = 860f;
        private const float WIN_H = 1530f;
        private const float SIDE = 44f;
        private const float ROW_H = 116f;
        private const float CARD_PAD = 8f;
        private const float HEADER_H = 50f;

        // ── Colours ──
        private static readonly Color White = Color.white;
        private static readonly Color Brown = new Color(0.33f, 0.15f, 0.02f, 1f);
        private static readonly Color Gold = new Color(1f, 0.84f, 0.3f, 1f);
        private static readonly Color SoftBlue = new Color(0.7f, 0.82f, 1f, 1f);
        private static readonly Color DimBlue = new Color(0.5f, 0.6f, 0.85f, 1f);

        private RectTransform root, window, ribbon, closeBtn, gearRect, toast, creditsRoot, creditsWindow;
        private Canvas ownCanvas;
        private CanvasGroup rootGroup, windowGroup, toastGroup, creditsGroup;
        private Image backdrop, creditsBackdrop;
        private TextMeshProUGUI toastText;
        private readonly List<RectTransform> sections = new List<RectTransform>();
        private float u = 1f, fit = 1f;
        private bool built, isOpen, creditsOpen, suppress;
        private float lastSfxPreview;

        // controls
        private VolumeRow musicRow, sfxRow;
        private Switch vibrationSwitch;
        private Segmented graphicsSeg, fpsSeg;
        private TMP_InputField nameField;
        private TextMeshProUGUI idText;

        // ═════════════════════════════ Lifecycle ═════════════════════════════

        private void Awake()
        {
            Instance = this;
            if (Application.isPlaying) EnsureBuilt();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            DOTween.Kill(this);
        }

        private IEnumerator Start()
        {
            yield return null;   // let MainMenuController finish its own wiring first
            if (!hookSettingsButton) yield break;
            // Our own (possibly disabled) canvas reports itself as root, so walk up to the real one.
            Transform top = TopCanvas();
            Transform bar = FindDeep(top, "toppanel");
            Transform gear = FindDeep(bar != null ? bar : top, settingsButtonName);
            if (gear == null) { Debug.LogWarning("[Settings] Gear button '" + settingsButtonName + "' not found."); yield break; }
            var btn = gear.GetComponent<Button>();
            if (btn == null) btn = gear.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(Open);
        }

        private void Update()
        {
            if (!isOpen) return;
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (creditsOpen) CloseCredits();
                else Close();
            }
#endif
        }

        // ═════════════════════════════ Public API ═════════════════════════════

        public bool IsOpen => isOpen;

        public void Open()
        {
            if (!Application.isPlaying || isOpen) return;
            EnsureBuilt();
            isOpen = true;
            RefreshAll();
            ComputeFit();

            ownCanvas.enabled = true;
            rootGroup.DOKill();
            rootGroup.alpha = 1f;
            rootGroup.blocksRaycasts = true;
            rootGroup.interactable = true;

            backdrop.DOKill();
            backdrop.color = new Color(0f, 0.02f, 0.08f, 0f);
            backdrop.DOFade(0.74f, 0.22f).SetUpdate(true);

            window.DOKill();
            windowGroup.DOKill();
            window.localScale = Vector3.one * fit * 0.72f;
            window.DOScale(fit, 0.42f).SetEase(Ease.OutBack, 1.6f).SetUpdate(true);
            windowGroup.alpha = 0f;
            windowGroup.DOFade(1f, 0.16f).SetUpdate(true);

            ribbon.DOKill();
            ribbon.localScale = Vector3.one * 0.3f;
            ribbon.DOScale(1f, 0.5f).SetEase(Ease.OutBack, 2.2f).SetDelay(0.1f).SetUpdate(true);

            closeBtn.DOKill();
            closeBtn.localScale = Vector3.zero;
            closeBtn.localRotation = Quaternion.Euler(0, 0, -120f);
            closeBtn.DOScale(1f, 0.4f).SetEase(Ease.OutBack, 2f).SetDelay(0.22f).SetUpdate(true);
            closeBtn.DOLocalRotate(Vector3.zero, 0.4f).SetEase(Ease.OutCubic).SetDelay(0.22f).SetUpdate(true);

            for (int i = 0; i < sections.Count; i++)
            {
                var s = sections[i];
                var g = s.GetComponent<CanvasGroup>();
                s.DOKill(true);
                g.DOKill();
                Vector2 end = s.anchoredPosition;
                s.anchoredPosition = end + new Vector2(0f, P(-36f));
                g.alpha = 0f;
                float d = 0.12f + i * 0.055f;
                s.DOAnchorPos(end, 0.42f).SetEase(Ease.OutCubic).SetDelay(d).SetUpdate(true);
                g.DOFade(1f, 0.3f).SetDelay(d).SetUpdate(true);
            }

            toastGroup.alpha = 0f;
            creditsRoot.gameObject.SetActive(false);
            creditsOpen = false;
            KoSSettings.Haptic(12);
        }

        public void Close()
        {
            if (!built || !isOpen) return;
            isOpen = false;
            if (nameField != null && nameField.isFocused) nameField.DeactivateInputField();
            Click();
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;
            backdrop.DOKill();
            backdrop.DOFade(0f, 0.2f).SetUpdate(true);
            window.DOKill();
            window.DOScale(fit * 0.85f, 0.2f).SetEase(Ease.InBack, 1.5f).SetUpdate(true);
            windowGroup.DOKill();
            windowGroup.DOFade(0f, 0.2f).SetUpdate(true).OnComplete(() =>
            {
                if (!isOpen) ownCanvas.enabled = false;
            });
            PlayerPrefs.Save();
        }

        // ═════════════════════════════ Build ═════════════════════════════

        private void EnsureBuilt()
        {
            if (built) return;
            built = true;

            root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            var canvasRect = TopCanvas() as RectTransform;
            float width = canvasRect != null && canvasRect.rect.width > 1f ? canvasRect.rect.width : REFERENCE_WIDTH;
            u = Mathf.Max(0.4f, width / REFERENCE_WIDTH);

            // Own canvas: drawn above every page, and costs nothing while closed.
            ownCanvas = GetComponent<Canvas>();
            if (ownCanvas == null) ownCanvas = gameObject.AddComponent<Canvas>();
            ownCanvas.overrideSorting = true;
            ownCanvas.sortingOrder = sortingOrder;
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            ownCanvas.enabled = false;

            rootGroup = GetComponent<CanvasGroup>();
            if (rootGroup == null) rootGroup = gameObject.AddComponent<CanvasGroup>();
            rootGroup.alpha = 0f;
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;

            // Backdrop: tap outside the window to close
            var bd = Box(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, "Backdrop");
            backdrop = bd.gameObject.AddComponent<Image>();
            backdrop.color = new Color(0f, 0.02f, 0.08f, 0f);
            var bdBtn = bd.gameObject.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Close);

            BuildWindow();
            BuildCredits();
        }

        private void BuildWindow()
        {
            window = Box(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "Window");
            window.sizeDelta = new Vector2(P(WIN_W), P(WIN_H));
            window.anchoredPosition = new Vector2(0f, P(-30f));
            windowGroup = window.gameObject.AddComponent<CanvasGroup>();

            // Soft drop shadow
            var sh = Box(window, Vector2.zero, Vector2.one, new Vector2(P(-34), P(-62)), new Vector2(P(34), P(10)), "Shadow");
            var shImg = sh.gameObject.AddComponent<Image>();
            shImg.sprite = Gfx.Shadow();
            shImg.type = Image.Type.Sliced;
            shImg.pixelsPerUnitMultiplier = 1f / u;
            shImg.color = new Color(0f, 0f, 0.05f, 0.75f);
            shImg.raycastTarget = false;

            // Frame + body (swallows taps so they don't reach the backdrop)
            var frame = Box(window, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, "Frame");
            var fImg = frame.gameObject.AddComponent<Image>();
            SetPanel(fImg, art.panel, 150f, new Color(0.1f, 0.18f, 0.48f, 1f));

            // Inner light: a soft top sheen and a darker floor give the flat panel depth
            var sheen = Box(frame, Vector2.zero, Vector2.one, new Vector2(P(16), P(16)), new Vector2(P(-16), P(-16)), "Sheen");
            var shn = sheen.gameObject.AddComponent<Image>();
            shn.sprite = Gfx.VerticalFade();
            shn.color = new Color(0.55f, 0.75f, 1f, 0.16f);
            shn.raycastTarget = false;

            BuildRibbon();
            BuildCloseButton();

            float y = 132f;
            y = BuildAudio(y);
            y = BuildGame(y + 18f);
            y = BuildAccount(y + 18f);
            BuildFooter(y + 26f);
            BuildToast();
        }

        private void BuildRibbon()
        {
            ribbon = Box(window, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero, "Ribbon");
            ribbon.sizeDelta = new Vector2(P(600), P(220));
            ribbon.anchoredPosition = new Vector2(0f, P(26));
            var rib = ribbon.gameObject.AddComponent<Image>();
            rib.sprite = art.ribbon;
            rib.color = art.ribbon != null ? White : Gold;
            rib.raycastTarget = true;   // taps on the ribbon must not reach the backdrop

            // Gear + title, centred together on the ribbon's banner part
            var title = Box(ribbon, new Vector2(0.14f, 0.42f), new Vector2(0.86f, 0.95f), Vector2.zero, Vector2.zero, "Title");
            var t = Fill(title, "SETTINGS", 58, Brown, TextAlignmentOptions.Center, false, 30);
            t.margin = new Vector4(P(70), 0, 0, 0);
            if (art.gear != null)
            {
                gearRect = Box(title, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "Gear");
                gearRect.sizeDelta = new Vector2(P(74), P(74));
                gearRect.anchoredPosition = new Vector2(P(-138), P(2));
                var gi = gearRect.gameObject.AddComponent<Image>();
                gi.sprite = art.gear;
                gi.preserveAspect = true;
                gi.raycastTarget = false;
                gearRect.DOLocalRotate(new Vector3(0, 0, -360f), 9f, RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear).SetLoops(-1).SetUpdate(true).SetLink(gearRect.gameObject);
            }
        }

        private void BuildCloseButton()
        {
            closeBtn = Box(window, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, "Close");
            closeBtn.sizeDelta = new Vector2(P(104), P(104));
            closeBtn.anchoredPosition = new Vector2(P(-14), P(-14));
            var img = closeBtn.gameObject.AddComponent<Image>();
            img.sprite = Gfx.Circle(new Color(1f, 0.45f, 0.36f), new Color(0.75f, 0.1f, 0.08f), new Color(0.3f, 0.03f, 0.02f), 4f, 0.55f);
            var ring = Box(closeBtn, Vector2.zero, Vector2.one, new Vector2(P(-6), P(-6)), new Vector2(P(6), P(6)), "Ring");
            var ri = ring.gameObject.AddComponent<Image>();
            ri.sprite = Gfx.Ring(Gfx.GoldTop, Gfx.GoldBottom, 0.1f);
            ri.raycastTarget = false;
            ring.SetAsFirstSibling();
            var ic = Box(closeBtn, new Vector2(0.26f, 0.26f), new Vector2(0.74f, 0.74f), Vector2.zero, Vector2.zero, "X");
            if (art.iconClose != null)
            {
                var ii = ic.gameObject.AddComponent<Image>();
                ii.sprite = art.iconClose;
                ii.preserveAspect = true;
                ii.raycastTarget = false;
            }
            else Fill(ic, "X", 48, White, TextAlignmentOptions.Center, true);
            MakeButton(closeBtn, Close, false);
        }

        // ───────────────────────────── sections ─────────────────────────────

        private RectTransform Section(float top, string title, int rows, out RectTransform card)
        {
            float cardH = rows * ROW_H + CARD_PAD * 2f;
            var sec = Box(window, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(P(SIDE), P(-top - HEADER_H - cardH)), new Vector2(P(-SIDE), P(-top)), title);
            sec.gameObject.AddComponent<CanvasGroup>();
            sections.Add(sec);

            // Header: gold caps with fading rules either side
            var head = Box(sec, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, P(-HEADER_H)), Vector2.zero, "Header");
            var ht = Fill(head, title, 30, Gold, TextAlignmentOptions.Center, true);
            ht.characterSpacing = 6f;
            for (int side = 0; side < 2; side++)
            {
                var rule = Box(head, new Vector2(side == 0 ? 0f : 0.5f, 0.5f), new Vector2(side == 0 ? 0.5f : 1f, 0.5f),
                    new Vector2(side == 0 ? P(10) : P(115), P(-2)), new Vector2(side == 0 ? P(-115) : P(-10), P(2)), "Rule");
                var ri = rule.gameObject.AddComponent<Image>();
                ri.sprite = Gfx.HorizontalFade(side == 0);
                ri.color = new Color(1f, 0.82f, 0.35f, 0.75f);
                ri.raycastTarget = false;
                var dot = Box(head, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "Dot");
                dot.sizeDelta = new Vector2(P(12), P(12));
                dot.anchoredPosition = new Vector2(side == 0 ? P(-104) : P(104), 0f);
                dot.localRotation = Quaternion.Euler(0, 0, 45f);
                var di = dot.gameObject.AddComponent<Image>();
                di.color = Gold;
                di.raycastTarget = false;
            }

            card = Box(sec, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0, P(cardH)), "Card");
            var ci = card.gameObject.AddComponent<Image>();
            ci.sprite = Gfx.Card();
            ci.type = Image.Type.Sliced;
            ci.pixelsPerUnitMultiplier = 1f / u;
            ci.raycastTarget = false;
            return sec;
        }

        private RectTransform Row(RectTransform card, int index, int count, Sprite icon, string label, Action onIconTap, out Image badge)
        {
            var row = Box(card, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0, P(-CARD_PAD - (index + 1) * ROW_H)), new Vector2(0, P(-CARD_PAD - index * ROW_H)), "Row_" + label);

            if (index < count - 1)
            {
                var div = Box(row, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(P(24), P(-1)), new Vector2(P(-24), P(1)), "Divider");
                var dImg = div.gameObject.AddComponent<Image>();
                dImg.sprite = Gfx.HorizontalFade(null);
                dImg.color = new Color(0.6f, 0.75f, 1f, 0.22f);
                dImg.raycastTarget = false;
            }

            // Gold badge with the icon
            var b = Box(row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero, "Badge");
            b.sizeDelta = new Vector2(P(80), P(80));
            b.anchoredPosition = new Vector2(P(24 + 40), 0f);
            badge = b.gameObject.AddComponent<Image>();
            badge.sprite = Gfx.Circle(Gfx.GoldTop, Gfx.GoldBottom, Gfx.DarkBrown, 3.5f, 0.55f);
            badge.raycastTarget = onIconTap != null;
            var inner = Box(b, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "Inner");
            inner.sizeDelta = new Vector2(P(62), P(62));
            var innerImg = inner.gameObject.AddComponent<Image>();
            innerImg.sprite = Gfx.Circle(new Color(0.2f, 0.36f, 0.82f), new Color(0.08f, 0.15f, 0.45f), new Color(0.04f, 0.07f, 0.2f, 0.9f), 2f, 0.25f);
            innerImg.raycastTarget = false;
            var ic = Box(inner, new Vector2(0.17f, 0.17f), new Vector2(0.83f, 0.83f), Vector2.zero, Vector2.zero, "Icon");
            if (icon != null)
            {
                var ii = ic.gameObject.AddComponent<Image>();
                ii.sprite = icon;
                ii.preserveAspect = true;
                ii.raycastTarget = false;
            }
            if (onIconTap != null) MakeButton(b, onIconTap, true);

            var lb = Box(row, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(P(124), 0), new Vector2(P(330), 0), "Label");
            Fill(lb, label, 34, White, TextAlignmentOptions.Left, true, 22);
            return row;
        }

        private float BuildAudio(float top)
        {
            Section(top, "AUDIO", 2, out var card);
            musicRow = BuildVolumeRow(card, 0, art.iconMusic, "Music", true);
            sfxRow = BuildVolumeRow(card, 1, art.iconSound, "Sounds", false);
            return top + HEADER_H + 2 * ROW_H + CARD_PAD * 2f;
        }

        private float BuildGame(float top)
        {
            Section(top, "GAME", 3, out var card);

            var r0 = Row(card, 0, 3, art.iconVibration, "Vibration", null, out _);
            vibrationSwitch = BuildSwitch(r0, on =>
            {
                KoSSettings.Vibration = on;
                if (on) { KoSSettings.Haptic(40); Sfx("reward_pop"); }
                else Click();
            });
            var tap = r0.gameObject.AddComponent<Image>();
            tap.color = new Color(0, 0, 0, 0);
            var rb = r0.gameObject.AddComponent<Button>();
            rb.transition = Selectable.Transition.None;
            rb.onClick.AddListener(() => vibrationSwitch.Toggle());

            var r1 = Row(card, 1, 3, art.iconGraphics, "Graphics", null, out _);
            graphicsSeg = BuildSegmented(r1, KoSSettings.GraphicsNames, 444f, i =>
            {
                KoSSettings.Graphics = i;
                ShowToast(i == 0 ? "Low graphics: longest battery life" : i == 1 ? "Balanced look and speed" : "Best looking battles");
            });

            var r2 = Row(card, 2, 3, art.iconFps, "Frame Rate", null, out _);
            fpsSeg = BuildSegmented(r2, new[] { "30 FPS", "60 FPS" }, 320f, i =>
            {
                KoSSettings.TargetFps = KoSSettings.FpsOptions[i];
                ShowToast(i == 0 ? "30 FPS saves battery" : "60 FPS: smoothest play");
            });

            return top + HEADER_H + 3 * ROW_H + CARD_PAD * 2f;
        }

        private float BuildAccount(float top)
        {
            Section(top, "ACCOUNT", 3, out var card);

            // Player name: inline field + pencil
            var r0 = Row(card, 0, 3, art.iconName, "Name", null, out _);
            BuildNameField(r0);

            // Player id + copy
            var r1 = Row(card, 1, 3, art.iconId, "Player ID", null, out _);
            var copy = Box(r1, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero, "Copy");
            copy.sizeDelta = new Vector2(P(130), P(66));
            copy.anchoredPosition = new Vector2(P(-24 - 65), 0f);
            var cImg = copy.gameObject.AddComponent<Image>();
            SetPanel(cImg, art.buttonBlue, 66f, new Color(0.2f, 0.45f, 0.95f));
            Fill(Box(copy, Vector2.zero, Vector2.one, new Vector2(0, P(4)), Vector2.zero, "T"), "COPY", 28, White, TextAlignmentOptions.Center, true, 16);
            MakeButton(copy, () =>
            {
                GUIUtility.systemCopyBuffer = PlayerProfileData.PlayerTag;
                Sfx("reward_pop");
                KoSSettings.Haptic(15);
                ShowToast("Player ID copied");
            }, false);
            var idBox = Box(r1, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(P(330), 0), new Vector2(P(-24 - 130 - 18), 0), "Id");
            idText = Fill(idBox, "", 32, Gold, TextAlignmentOptions.Right, true, 18);

            // Language
            var r2 = Row(card, 2, 3, art.iconLanguage, "Language", null, out _);
            var chip = Box(r2, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero, "Chip");
            chip.sizeDelta = new Vector2(P(300), P(68));
            chip.anchoredPosition = new Vector2(P(-24 - 150), 0f);
            var chImg = chip.gameObject.AddComponent<Image>();
            chImg.sprite = Gfx.Field();
            chImg.type = Image.Type.Sliced;
            chImg.pixelsPerUnitMultiplier = 1f / u;
            if (art.flagEnglish != null)
            {
                var fl = Box(chip, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero, "Flag");
                fl.sizeDelta = new Vector2(P(50), P(50));
                fl.anchoredPosition = new Vector2(P(16 + 25), 0f);
                var fi = fl.gameObject.AddComponent<Image>();
                fi.sprite = art.flagEnglish;
                fi.preserveAspect = true;
                fi.raycastTarget = false;
            }
            Fill(Box(chip, Vector2.zero, Vector2.one, new Vector2(P(76), 0), new Vector2(P(-56), 0), "T"), "English", 30, White, TextAlignmentOptions.Left, true, 18);
            if (art.iconCheck != null)
            {
                var ck = Box(chip, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero, "Check");
                ck.sizeDelta = new Vector2(P(38), P(38));
                ck.anchoredPosition = new Vector2(P(-14 - 19), 0f);
                var ci = ck.gameObject.AddComponent<Image>();
                ci.sprite = art.iconCheck;
                ci.preserveAspect = true;
                ci.raycastTarget = false;
            }
            MakeButton(chip, () => { Click(); ShowToast("More languages are coming soon"); }, false);

            return top + HEADER_H + 3 * ROW_H + CARD_PAD * 2f;
        }

        private void BuildFooter(float top)
        {
            var foot = Box(window, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(P(SIDE), P(-top - 160)), new Vector2(P(-SIDE), P(-top)), "Footer");
            foot.gameObject.AddComponent<CanvasGroup>();
            sections.Add(foot);

            BigButton(foot, 0f, 0.333f, art.buttonBlue, new Color(0.2f, 0.45f, 0.95f), null, "CREDITS", OpenCredits);
            BigButton(foot, 0.333f, 0.667f, art.buttonBlue, new Color(0.2f, 0.45f, 0.95f), null, "TUTORIAL", ReplayTutorial);
            BigButton(foot, 0.667f, 1f, art.buttonRed, new Color(0.9f, 0.25f, 0.2f), null, "DEFAULTS", ResetDefaults);

            var ver = Box(foot, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0, P(44)), "Version");
            Fill(ver, "Kings of Siege  -  v" + Application.version, 24, DimBlue, TextAlignmentOptions.Center, true);
        }

        /// <summary>Closes Settings and starts the onboarding tutorial again (Siege.Tutorial.KoSTutorial).</summary>
        private void ReplayTutorial()
        {
            Close();
            Siege.Tutorial.KoSTutorial.Replay();
        }

        private void BigButton(RectTransform parent, float x0, float x1, Sprite sprite, Color fallback, Sprite icon, string label, Action onClick)
        {
            var b = Box(parent, new Vector2(x0, 1f), new Vector2(x1, 1f), new Vector2(P(x0 > 0f ? 10 : 0), P(-104)), new Vector2(P(x1 < 1f ? -10 : 0), 0), label);
            var img = b.gameObject.AddComponent<Image>();
            SetPanel(img, sprite, 104f, fallback);
            var inner = Box(b, Vector2.zero, Vector2.one, new Vector2(0, P(6)), Vector2.zero, "Inner");
            if (icon != null)
            {
                var ic = Box(inner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "Icon");
                ic.sizeDelta = new Vector2(P(48), P(48));
                ic.anchoredPosition = new Vector2(P(-92), 0f);
                var ii = ic.gameObject.AddComponent<Image>();
                ii.sprite = icon;
                ii.preserveAspect = true;
                ii.raycastTarget = false;
            }
            var t = Fill(inner, label, 34, White, TextAlignmentOptions.Center, true, 20);
            t.margin = new Vector4(icon != null ? P(56) : 0, 0, 0, 0);
            MakeButton(b, onClick, false);
        }

        private void BuildToast()
        {
            toast = Box(window, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero, "Toast");
            toast.sizeDelta = new Vector2(P(640), P(70));
            toast.anchoredPosition = new Vector2(0f, P(250));
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            toastGroup.blocksRaycasts = false;
            var img = toast.gameObject.AddComponent<Image>();
            img.sprite = Gfx.Pill(new Color(0.08f, 0.1f, 0.22f, 0.96f), new Color(0.03f, 0.04f, 0.12f, 0.96f), new Color(1f, 0.8f, 0.3f, 0.9f), 2.5f, 0f);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = Gfx.PillPpu(P(70));
            img.raycastTarget = false;
            toastText = Fill(Box(toast, Vector2.zero, Vector2.one, new Vector2(P(24), 0), new Vector2(P(-24), 0), "T"), "", 28, White, TextAlignmentOptions.Center, true, 16);
        }

        // ───────────────────────────── controls ─────────────────────────────

        private class VolumeRow
        {
            public Slider slider;
            public TextMeshProUGUI percent;
            public Image badge, fill, slash;
            public bool music;
        }

        private VolumeRow BuildVolumeRow(RectTransform card, int index, Sprite icon, string label, bool music)
        {
            var vr = new VolumeRow { music = music };
            var row = Row(card, index, 2, icon, label, () => ToggleMute(vr), out vr.badge);

            // Red slash over the badge while muted
            var sl = Box(vr.badge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "Muted");
            sl.sizeDelta = new Vector2(P(86), P(11));
            sl.localRotation = Quaternion.Euler(0, 0, 45f);
            vr.slash = sl.gameObject.AddComponent<Image>();
            vr.slash.sprite = Gfx.Pill(new Color(1f, 0.35f, 0.3f), new Color(0.8f, 0.1f, 0.08f), new Color(0.3f, 0.02f, 0.02f), 2f, 0f);
            vr.slash.type = Image.Type.Sliced;
            vr.slash.pixelsPerUnitMultiplier = Gfx.PillPpu(P(11));
            vr.slash.raycastTarget = false;

            vr.percent = Fill(Box(row, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(P(-24 - 92), 0), new Vector2(P(-24), 0), "Pct"), "100%", 30, Gold, TextAlignmentOptions.Right, true, 18);

            // Slider: dark inset track, glossy gold fill, round gold knob
            var area = Box(row, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(P(262), P(-34)), new Vector2(P(-24 - 92 - 26), P(34)), "Slider");
            var s = area.gameObject.AddComponent<Slider>();
            s.transition = Selectable.Transition.None;
            s.direction = Slider.Direction.LeftToRight;
            s.minValue = 0f;
            s.maxValue = 1f;

            const float trackH = 30f, knob = 64f;
            var track = Box(area, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0, P(-trackH / 2)), new Vector2(0, P(trackH / 2)), "Track");
            var tImg = track.gameObject.AddComponent<Image>();
            tImg.sprite = Gfx.Pill(new Color(0.02f, 0.05f, 0.16f), new Color(0.07f, 0.12f, 0.3f), new Color(0f, 0f, 0.04f, 0.95f), 2.5f, 0f);
            tImg.type = Image.Type.Sliced;
            tImg.pixelsPerUnitMultiplier = Gfx.PillPpu(P(trackH));

            var fillArea = Box(track, Vector2.zero, Vector2.one, new Vector2(P(3), P(3)), new Vector2(P(-3), P(-3)), "FillArea");
            var fill = Box(fillArea, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero, "Fill");
            vr.fill = fill.gameObject.AddComponent<Image>();
            vr.fill.sprite = Gfx.Pill(Gfx.GoldTop, new Color(0.9f, 0.52f, 0.06f), new Color(0.45f, 0.22f, 0f, 0.8f), 1.5f, 0.55f);
            vr.fill.type = Image.Type.Sliced;
            vr.fill.pixelsPerUnitMultiplier = Gfx.PillPpu(P(trackH - 6));
            vr.fill.raycastTarget = false;

            // The Slider stretches the knob to the full height of this area, so the area is exactly knob-tall.
            var handleArea = Box(area, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(P(knob * 0.32f), P(-knob / 2f)), new Vector2(P(-knob * 0.32f), P(knob / 2f)), "HandleArea");
            var handle = Box(handleArea, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero, "Knob");
            handle.sizeDelta = new Vector2(P(knob), 0f);
            var hImg = handle.gameObject.AddComponent<Image>();
            hImg.sprite = Gfx.Circle(new Color(1f, 0.97f, 0.82f), new Color(0.95f, 0.7f, 0.2f), Gfx.DarkBrown, 3.5f, 0.6f);
            var hShadow = Box(handle, Vector2.zero, Vector2.one, new Vector2(P(-6), P(-12)), new Vector2(P(6), P(2)), "Shadow");
            var hs = hShadow.gameObject.AddComponent<Image>();
            hs.sprite = Gfx.SoftDot();
            hs.color = new Color(0, 0, 0, 0.45f);
            hs.raycastTarget = false;
            hShadow.SetAsFirstSibling();
            // Gem in the knob centre
            var gem = Box(handle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "Gem");
            gem.sizeDelta = new Vector2(P(22), P(22));
            gem.localRotation = Quaternion.Euler(0, 0, 45f);
            var gi = gem.gameObject.AddComponent<Image>();
            gi.sprite = Gfx.RoundedSmall(new Color(0.55f, 0.8f, 1f), new Color(0.15f, 0.35f, 0.85f), new Color(0.05f, 0.1f, 0.3f), 2f);
            gi.raycastTarget = false;

            s.fillRect = fill;
            s.handleRect = handle;
            s.targetGraphic = hImg;
            vr.slider = s;

            s.onValueChanged.AddListener(v => OnVolume(vr, v));

            // Knob grows while dragging
            var trig = area.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            AddTrigger(trig, UnityEngine.EventSystems.EventTriggerType.PointerDown, () => { handle.DOKill(); handle.DOScale(1.18f, 0.12f).SetUpdate(true); });
            AddTrigger(trig, UnityEngine.EventSystems.EventTriggerType.PointerUp, () => { handle.DOKill(); handle.DOScale(1f, 0.25f).SetEase(Ease.OutBack, 2f).SetUpdate(true); });
            return vr;
        }

        private static void AddTrigger(UnityEngine.EventSystems.EventTrigger t, UnityEngine.EventSystems.EventTriggerType type, Action a)
        {
            var e = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = type };
            e.callback.AddListener(_ => a());
            t.triggers.Add(e);
        }

        private void OnVolume(VolumeRow vr, float v)
        {
            if (suppress) return;
            if (vr.music) KoSSettings.MusicVolume = v;
            else
            {
                KoSSettings.SfxVolume = v;
                if (Time.unscaledTime - lastSfxPreview > 0.14f && v > 0.01f)
                {
                    lastSfxPreview = Time.unscaledTime;
                    KoSAudio.Play2D("card_select", 0.9f);
                }
            }
            PaintVolume(vr);
        }

        private void ToggleMute(VolumeRow vr)
        {
            if (vr.music) KoSSettings.ToggleMusicMute();
            else KoSSettings.ToggleSfxMute();
            KoSSettings.Haptic(15);
            float v = vr.music ? KoSSettings.MusicVolume : KoSSettings.SfxVolume;
            suppress = true;
            vr.slider.DOKill();
            DOTween.To(() => vr.slider.value, x => { vr.slider.value = x; PaintVolume(vr); }, v, 0.25f).SetEase(Ease.OutCubic).SetUpdate(true).SetTarget(vr.slider)
                .OnComplete(() => { suppress = false; PaintVolume(vr); })
                .OnKill(() => suppress = false);
            if (v > 0.01f) Sfx("reward_pop");
            ShowToast((vr.music ? "Music " : "Sounds ") + (v > 0.01f ? "on" : "muted"));
        }

        private void PaintVolume(VolumeRow vr)
        {
            float v = vr.slider.value;
            bool muted = v <= 0.001f;
            vr.percent.text = Mathf.RoundToInt(v * 100f) + "%";
            vr.percent.color = muted ? DimBlue : Gold;
            vr.slash.enabled = muted;
            vr.badge.color = muted ? new Color(0.55f, 0.55f, 0.6f) : White;
            vr.fill.enabled = v > 0.02f;
        }

        private class Switch
        {
            public RectTransform knob;
            public Image onBg;
            public TextMeshProUGUI onText, offText;
            public bool state;
            public float travel;
            public Action<bool> changed;

            public void Set(bool on, bool animate)
            {
                state = on;
                float x = on ? travel : -travel;
                knob.DOKill();
                onBg.DOKill();
                if (animate)
                {
                    knob.DOAnchorPosX(x, 0.26f).SetEase(Ease.OutBack, 1.6f).SetUpdate(true);
                    knob.localScale = new Vector3(1.15f, 0.9f, 1f);
                    knob.DOScale(1f, 0.3f).SetEase(Ease.OutBack, 2f).SetUpdate(true);
                    onBg.DOFade(on ? 1f : 0f, 0.2f).SetUpdate(true);
                }
                else
                {
                    knob.anchoredPosition = new Vector2(x, 0f);
                    var c = onBg.color; c.a = on ? 1f : 0f; onBg.color = c;
                }
                onText.enabled = on;
                offText.enabled = !on;
            }

            public void Toggle()
            {
                Set(!state, true);
                changed?.Invoke(state);
            }
        }

        private Switch BuildSwitch(RectTransform row, Action<bool> changed)
        {
            const float w = 156f, h = 70f;
            var sw = new Switch { changed = changed, travel = P((w - h) / 2f) };
            var r = Box(row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero, "Switch");
            r.sizeDelta = new Vector2(P(w), P(h));
            r.anchoredPosition = new Vector2(P(-24 - w / 2f), 0f);
            float ppu = Gfx.PillPpu(P(h));

            var off = r.gameObject.AddComponent<Image>();
            off.sprite = Gfx.Pill(new Color(0.17f, 0.21f, 0.34f), new Color(0.3f, 0.36f, 0.52f), new Color(0.04f, 0.05f, 0.12f), 3f, 0f);
            off.type = Image.Type.Sliced;
            off.pixelsPerUnitMultiplier = ppu;

            var onR = Box(r, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, "On");
            sw.onBg = onR.gameObject.AddComponent<Image>();
            sw.onBg.sprite = Gfx.Pill(new Color(0.55f, 0.92f, 0.36f), new Color(0.16f, 0.58f, 0.15f), new Color(0.06f, 0.25f, 0.04f), 3f, 0.35f);
            sw.onBg.type = Image.Type.Sliced;
            sw.onBg.pixelsPerUnitMultiplier = ppu;
            sw.onBg.raycastTarget = false;

            sw.onText = Fill(Box(r, Vector2.zero, new Vector2(0.62f, 1f), new Vector2(P(12), P(2)), Vector2.zero, "OnT"), "ON", 26, White, TextAlignmentOptions.Center, true);
            sw.offText = Fill(Box(r, new Vector2(0.38f, 0f), Vector2.one, new Vector2(0, P(2)), new Vector2(P(-12), 0), "OffT"), "OFF", 26, SoftBlue, TextAlignmentOptions.Center, true);

            sw.knob = Box(r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "Knob");
            sw.knob.sizeDelta = new Vector2(P(h - 6), P(h - 6));
            var kImg = sw.knob.gameObject.AddComponent<Image>();
            kImg.sprite = Gfx.Circle(new Color(1f, 1f, 0.97f), new Color(0.8f, 0.84f, 0.92f), new Color(0.1f, 0.12f, 0.2f), 3f, 0.4f);
            kImg.raycastTarget = false;

            var btn = r.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(sw.Toggle);
            sw.Set(true, false);
            return sw;
        }

        private class Segmented
        {
            public RectTransform pill;
            public TextMeshProUGUI[] labels;
            public float segW, width;
            public int index = -1;

            public void Select(int i, bool animate)
            {
                index = i;
                float x = -width / 2f + segW * (i + 0.5f);
                pill.DOKill();
                if (animate)
                {
                    pill.DOAnchorPosX(x, 0.28f).SetEase(Ease.OutBack, 1.3f).SetUpdate(true);
                    pill.localScale = new Vector3(0.92f, 1.06f, 1f);
                    pill.DOScale(1f, 0.3f).SetEase(Ease.OutBack, 2f).SetUpdate(true);
                }
                else pill.anchoredPosition = new Vector2(x, 0f);
                for (int k = 0; k < labels.Length; k++)
                {
                    labels[k].color = k == i ? Brown : SoftBlue;
                    labels[k].fontStyle = FontStyles.Normal;
                }
            }
        }

        private Segmented BuildSegmented(RectTransform row, string[] options, float width, Action<int> picked)
        {
            const float h = 70f;
            var seg = new Segmented { width = P(width), segW = P(width) / options.Length, labels = new TextMeshProUGUI[options.Length] };
            var r = Box(row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero, "Segmented");
            r.sizeDelta = new Vector2(P(width), P(h));
            r.anchoredPosition = new Vector2(P(-24 - width / 2f), 0f);
            var bg = r.gameObject.AddComponent<Image>();
            bg.sprite = Gfx.Field();
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = 1f / u;

            seg.pill = Box(r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "Pill");
            seg.pill.sizeDelta = new Vector2(seg.segW - P(8), P(h - 10));
            var pImg = seg.pill.gameObject.AddComponent<Image>();
            pImg.sprite = Gfx.RoundedSmall(new Color(1f, 0.93f, 0.55f), new Color(0.93f, 0.6f, 0.1f), Gfx.DarkBrown, 2.5f, 0.45f);
            pImg.type = Image.Type.Sliced;
            pImg.pixelsPerUnitMultiplier = 1f / u;
            pImg.raycastTarget = false;

            for (int i = 0; i < options.Length; i++)
            {
                int idx = i;
                var o = Box(r, new Vector2((float)i / options.Length, 0f), new Vector2((float)(i + 1) / options.Length, 1f), Vector2.zero, Vector2.zero, options[i]);
                var hit = o.gameObject.AddComponent<Image>();
                hit.color = new Color(0, 0, 0, 0);
                seg.labels[i] = Fill(Box(o, Vector2.zero, Vector2.one, new Vector2(P(4), P(2)), new Vector2(P(-4), 0), "T"), options[i], 28, SoftBlue, TextAlignmentOptions.Center, false, 16);
                var b = o.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() =>
                {
                    if (seg.index == idx) return;
                    seg.Select(idx, true);
                    Sfx("card_select");
                    KoSSettings.Haptic(12);
                    picked?.Invoke(idx);
                });
            }
            seg.Select(0, false);
            return seg;
        }

        private void BuildNameField(RectTransform row)
        {
            var edit = Box(row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero, "Edit");
            edit.sizeDelta = new Vector2(P(70), P(70));
            edit.anchoredPosition = new Vector2(P(-24 - 35), 0f);
            var eImg = edit.gameObject.AddComponent<Image>();
            eImg.sprite = Gfx.RoundedSmall(Gfx.GoldTop, Gfx.GoldBottom, Gfx.DarkBrown, 3f, 0.5f);
            eImg.type = Image.Type.Sliced;
            eImg.pixelsPerUnitMultiplier = 1f / u;
            var pen = Box(edit, new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f), Vector2.zero, Vector2.zero, "Pencil");
            if (art.iconPencil != null)
            {
                var pi = pen.gameObject.AddComponent<Image>();
                pi.sprite = art.iconPencil;
                pi.preserveAspect = true;
                pi.raycastTarget = false;
            }

            var field = Box(row, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(P(262), P(-34)), new Vector2(P(-24 - 70 - 14), P(34)), "Field");
            var fImg = field.gameObject.AddComponent<Image>();
            fImg.sprite = Gfx.Field();
            fImg.type = Image.Type.Sliced;
            fImg.pixelsPerUnitMultiplier = 1f / u;

            var viewport = Box(field, Vector2.zero, Vector2.one, new Vector2(P(20), P(4)), new Vector2(P(-16), P(-4)), "Viewport");
            viewport.gameObject.AddComponent<RectMask2D>();

            var textR = Box(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, "Text");
            var text = textR.gameObject.AddComponent<TextMeshProUGUI>();
            ApplyFont(text, true);
            text.fontSize = P(32);
            text.color = White;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;

            var phR = Box(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, "Placeholder");
            var ph = phR.gameObject.AddComponent<TextMeshProUGUI>();
            ApplyFont(ph, true);
            ph.fontSize = P(30);
            ph.color = new Color(0.6f, 0.7f, 0.9f, 0.6f);
            ph.alignment = TextAlignmentOptions.MidlineLeft;
            ph.text = "Your name";
            ph.raycastTarget = false;

            // Configure the input field while inactive so its OnEnable sees the text component (caret setup).
            field.gameObject.SetActive(false);
            nameField = field.gameObject.AddComponent<TMP_InputField>();
            nameField.transition = Selectable.Transition.None;
            nameField.targetGraphic = fImg;
            nameField.textViewport = viewport;
            nameField.textComponent = text;
            nameField.placeholder = ph;
            nameField.characterLimit = PlayerProfileData.MAX_NAME_LENGTH;
            nameField.lineType = TMP_InputField.LineType.SingleLine;
            nameField.contentType = TMP_InputField.ContentType.Standard;
            nameField.caretColor = Gold;
            nameField.customCaretColor = true;
            nameField.caretWidth = Mathf.Max(2, Mathf.RoundToInt(P(3)));
            nameField.selectionColor = new Color(1f, 0.8f, 0.3f, 0.35f);
            nameField.onFocusSelectAll = true;
            nameField.onSelect.AddListener(_ =>
            {
                field.DOKill(true);
                field.DOPunchScale(new Vector3(0.03f, 0.03f, 0f), 0.2f, 6, 0.5f).SetUpdate(true);
                fImg.color = new Color(1f, 0.92f, 0.7f);
            });
            nameField.onEndEdit.AddListener(CommitName);
            nameField.onDeselect.AddListener(_ => fImg.color = White);
            field.gameObject.SetActive(true);

            MakeButton(edit, () =>
            {
                Click();
                nameField.Select();
                nameField.ActivateInputField();
            }, false);
        }

        private void CommitName(string value)
        {
            if (nameField == null) return;
            string current = PlayerProfileData.PlayerName;
            if ((value ?? "").Trim() == current) { nameField.SetTextWithoutNotify(current); return; }
            if (PlayerProfileData.TrySetPlayerName(value, out string error))
            {
                nameField.SetTextWithoutNotify(PlayerProfileData.PlayerName);
                Sfx("reward_pop");
                KoSSettings.Haptic(20);
                ShowToast("Name saved");
            }
            else
            {
                nameField.SetTextWithoutNotify(current);
                Sfx("ui_error");
                var r = (RectTransform)nameField.transform;
                r.DOKill(true);
                r.DOShakeAnchorPos(0.35f, new Vector2(P(12), 0f), 20, 0f).SetUpdate(true);
                ShowToast(string.IsNullOrEmpty(error) ? "That name can't be used" : error);
            }
        }

        // ───────────────────────────── credits ─────────────────────────────

        private const string CreditsText =
            "<size=120%><color=#FFD54A>KINGS OF SIEGE</color></size>\n" +
            "A medieval strategy battler\n\n" +
            "<color=#FFD54A>MUSIC</color>\n" +
            "Town Theme RPG and Battle Theme A\nby cynicmusic (CC0)\n\n" +
            "<color=#FFD54A>SOUND EFFECTS</color>\n" +
            "Kenney: Impact Sounds, RPG Audio,\nInterface Sounds, Casino Audio,\nMusic Jingles (CC0)\n" +
            "rubberduck: 100 CC0 SFX 1 and 2 (CC0)\n\n" +
            "<color=#FFD54A>BUILT WITH</color>\n" +
            "Unity, DOTween, TextMesh Pro,\nPhoton PUN\n\n" +
            "<color=#FFD54A>THANK YOU</color>\n" +
            "for playing and testing!";

        private void BuildCredits()
        {
            creditsRoot = Box(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, "Credits");
            creditsGroup = creditsRoot.gameObject.AddComponent<CanvasGroup>();
            creditsBackdrop = creditsRoot.gameObject.AddComponent<Image>();
            creditsBackdrop.color = new Color(0f, 0.02f, 0.08f, 0.62f);
            var bb = creditsRoot.gameObject.AddComponent<Button>();
            bb.transition = Selectable.Transition.None;
            bb.onClick.AddListener(CloseCredits);

            creditsWindow = Box(creditsRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "Window");
            creditsWindow.sizeDelta = new Vector2(P(760), P(1080));
            var sh = Box(creditsWindow, Vector2.zero, Vector2.one, new Vector2(P(-30), P(-56)), new Vector2(P(30), P(10)), "Shadow");
            var shImg = sh.gameObject.AddComponent<Image>();
            shImg.sprite = Gfx.Shadow();
            shImg.type = Image.Type.Sliced;
            shImg.pixelsPerUnitMultiplier = 1f / u;
            shImg.color = new Color(0f, 0f, 0.05f, 0.75f);
            shImg.raycastTarget = false;
            var frame = Box(creditsWindow, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, "Frame");
            var fImg = frame.gameObject.AddComponent<Image>();
            SetPanel(fImg, art.panel, 150f, new Color(0.1f, 0.18f, 0.48f, 1f));

            var rib = Box(creditsWindow, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero, "Ribbon");
            rib.sizeDelta = new Vector2(P(500), P(184));
            rib.anchoredPosition = new Vector2(0f, P(22));
            var ri = rib.gameObject.AddComponent<Image>();
            ri.sprite = art.ribbon;
            ri.color = art.ribbon != null ? White : Gold;
            ri.raycastTarget = false;
            Fill(Box(rib, new Vector2(0.14f, 0.42f), new Vector2(0.86f, 0.95f), Vector2.zero, Vector2.zero, "Title"), "CREDITS", 50, Brown, TextAlignmentOptions.Center, false, 26);

            var view = Box(creditsWindow, Vector2.zero, Vector2.one, new Vector2(P(40), P(130)), new Vector2(P(-40), P(-120)), "View");
            var vImg = view.gameObject.AddComponent<Image>();
            vImg.sprite = Gfx.Card();
            vImg.type = Image.Type.Sliced;
            vImg.pixelsPerUnitMultiplier = 1f / u;
            view.gameObject.AddComponent<RectMask2D>();
            var content = Box(view, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, "Content");
            content.pivot = new Vector2(0.5f, 1f);
            var t = content.gameObject.AddComponent<TextMeshProUGUI>();
            ApplyFont(t, true);
            t.text = CreditsText;
            t.fontSize = P(30);
            t.color = White;
            t.alignment = TextAlignmentOptions.Top;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.margin = new Vector4(P(24), P(28), P(24), P(28));
            t.raycastTarget = false;
            var csf = content.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sr = view.gameObject.AddComponent<ScrollRect>();
            sr.content = content;
            sr.horizontal = false;
            sr.viewport = view;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 30f;

            var ok = Box(creditsWindow, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero, "OK");
            ok.sizeDelta = new Vector2(P(300), P(96));
            ok.anchoredPosition = new Vector2(0f, P(70));
            var okImg = ok.gameObject.AddComponent<Image>();
            SetPanel(okImg, art.buttonBlue, 96f, new Color(0.2f, 0.45f, 0.95f));
            Fill(Box(ok, Vector2.zero, Vector2.one, new Vector2(0, P(6)), Vector2.zero, "T"), "OK", 38, White, TextAlignmentOptions.Center, true);
            MakeButton(ok, CloseCredits, false);

            creditsRoot.gameObject.SetActive(false);
        }

        private void OpenCredits()
        {
            if (creditsOpen) return;
            creditsOpen = true;
            creditsRoot.gameObject.SetActive(true);
            creditsRoot.SetAsLastSibling();
            creditsGroup.DOKill();
            creditsGroup.alpha = 0f;
            creditsGroup.blocksRaycasts = true;
            creditsGroup.DOFade(1f, 0.18f).SetUpdate(true);
            creditsWindow.DOKill();
            creditsWindow.localScale = Vector3.one * fit * 0.7f;
            creditsWindow.DOScale(fit, 0.4f).SetEase(Ease.OutBack, 1.6f).SetUpdate(true);
            var sr = creditsWindow.GetComponentInChildren<ScrollRect>();
            if (sr != null) { Canvas.ForceUpdateCanvases(); sr.verticalNormalizedPosition = 1f; }
        }

        private void CloseCredits()
        {
            if (!creditsOpen) return;
            creditsOpen = false;
            Click();
            creditsGroup.blocksRaycasts = false;
            creditsGroup.DOKill();
            creditsGroup.DOFade(0f, 0.16f).SetUpdate(true).OnComplete(() => { if (!creditsOpen) creditsRoot.gameObject.SetActive(false); });
            creditsWindow.DOKill();
            creditsWindow.DOScale(fit * 0.85f, 0.16f).SetEase(Ease.InBack).SetUpdate(true);
        }

        // ───────────────────────────── state ─────────────────────────────

        private void RefreshAll()
        {
            suppress = true;
            musicRow.slider.value = KoSSettings.MusicVolume;
            sfxRow.slider.value = KoSSettings.SfxVolume;
            suppress = false;
            PaintVolume(musicRow);
            PaintVolume(sfxRow);
            vibrationSwitch.Set(KoSSettings.Vibration, false);
            graphicsSeg.Select(KoSSettings.Graphics, false);
            fpsSeg.Select(KoSSettings.TargetFps <= 30 ? 0 : 1, false);
            nameField.SetTextWithoutNotify(PlayerProfileData.PlayerName);
            idText.text = PlayerProfileData.PlayerTag;
        }

        private void ResetDefaults()
        {
            KoSSettings.MusicVolume = 1f;
            KoSSettings.SfxVolume = 1f;
            KoSSettings.Vibration = true;
            KoSSettings.Graphics = 2;
            KoSSettings.TargetFps = 60;
            PlayerPrefs.Save();

            suppress = true;
            musicRow.slider.DOKill();
            sfxRow.slider.DOKill();
            DOTween.To(() => musicRow.slider.value, x => { musicRow.slider.value = x; PaintVolume(musicRow); }, 1f, 0.35f).SetUpdate(true).SetTarget(musicRow.slider);
            DOTween.To(() => sfxRow.slider.value, x => { sfxRow.slider.value = x; PaintVolume(sfxRow); }, 1f, 0.35f).SetUpdate(true).SetTarget(sfxRow.slider)
                .OnComplete(() => suppress = false).OnKill(() => suppress = false);
            if (!vibrationSwitch.state) vibrationSwitch.Set(true, true);
            graphicsSeg.Select(2, true);
            fpsSeg.Select(1, true);
            Sfx("reward_pop");
            KoSSettings.Haptic(30);
            ShowToast("Settings restored to default");
        }

        private void ShowToast(string msg)
        {
            if (toast == null) return;
            toastText.text = msg;
            toast.DOKill(true);
            toastGroup.DOKill();
            toastGroup.alpha = 0f;
            toast.localScale = Vector3.one * 0.85f;
            toast.DOScale(1f, 0.25f).SetEase(Ease.OutBack, 2f).SetUpdate(true);
            DOTween.Sequence().SetTarget(toastGroup).SetUpdate(true)
                .Append(toastGroup.DOFade(1f, 0.15f))
                .AppendInterval(1.5f)
                .Append(toastGroup.DOFade(0f, 0.3f));
        }

        private void ComputeFit()
        {
            var parent = root.rect;
            float needH = P(WIN_H + 160f);
            float needW = P(WIN_W + 30f);
            fit = Mathf.Min(1f, parent.height > 1f ? parent.height / needH : 1f, parent.width > 1f ? parent.width / needW : 1f);
        }

        // ═════════════════════════════ Helpers ═════════════════════════════

        private static void Sfx(string id) => KoSAudio.Play2D(id, 0.85f);
        private static void Click() => KoSAudio.Play2D("card_select", 0.8f);

        private float P(float v) => v * u;

        private static RectTransform Box(Transform parent, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            r.anchorMin = aMin;
            r.anchorMax = aMax;
            r.offsetMin = oMin;
            r.offsetMax = oMax;
            return r;
        }

        /// <summary>Sliced panel whose borders are scaled so they fit the given height.</summary>
        private void SetPanel(Image img, Sprite sprite, float heightRef, Color fallback)
        {
            img.sprite = sprite;
            if (sprite == null)
            {
                img.sprite = Gfx.RoundedSmall(fallback, fallback * 0.7f, new Color(0.1f, 0.05f, 0f), 3f, 0.3f);
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 1f / u;
                img.color = White;
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
            else img.type = Image.Type.Simple;
        }

        private TextMeshProUGUI Fill(RectTransform parent, string text, float size, Color color, TextAlignmentOptions align, bool outline, float minSize = 0f)
        {
            var child = Box(parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, "Text");
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

        private static void MakeButton(RectTransform target, Action onClick, bool bigPunch)
        {
            var btn = target.gameObject.GetComponent<Button>();
            if (btn == null) btn = target.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            if (target.GetComponent<Graphic>() == null)
            {
                var hit = target.gameObject.AddComponent<Image>();
                hit.color = new Color(0, 0, 0, 0);
            }
            btn.onClick.AddListener(() =>
            {
                target.DOKill(true);
                target.localScale = Vector3.one;
                float p = bigPunch ? -0.14f : -0.07f;
                target.DOPunchScale(new Vector3(p, p, 0f), 0.24f, 6, 0.6f).SetUpdate(true);
                onClick?.Invoke();
            });
        }

        private Transform TopCanvas()
        {
            Transform best = transform.parent;
            for (var t = transform.parent; t != null; t = t.parent)
                if (t.GetComponent<Canvas>() != null) best = t;
            return best;
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

        // ═════════════════════════════ Procedural sprites ═════════════════════════════

        /// <summary>
        /// Anti-aliased rounded shapes with vertical gradients, outlines and a glossy top highlight,
        /// generated once and cached. Used where the art pack has nothing that fits.
        /// </summary>
        private static class Gfx
        {
            public static readonly Color GoldTop = new Color(1f, 0.92f, 0.5f);
            public static readonly Color GoldBottom = new Color(0.86f, 0.53f, 0.08f);
            public static readonly Color DarkBrown = new Color(0.27f, 0.13f, 0.02f);

            private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

            /// <summary>Pixels-per-unit multiplier that makes a pill sprite's round ends exactly fit the given height.</summary>
            public static float PillPpu(float height) => height > 0f ? 64f / height : 1f;

            public static Sprite Circle(Color top, Color bottom, Color border, float borderPx, float gloss)
                => Make("C", 128, 128, 64f, top, bottom, border, borderPx * 1.6f, gloss, 0f, false);

            public static Sprite Pill(Color top, Color bottom, Color border, float borderPx, float gloss)
                => Make("P", 128, 64, 32f, top, bottom, border, borderPx, gloss, 0f, true);

            public static Sprite RoundedSmall(Color top, Color bottom, Color border, float borderPx, float gloss = 0.3f)
                => Make("R", 64, 64, 16f, top, bottom, border, borderPx, gloss, 0f, true);

            public static Sprite Card()
                => Make("Card", 96, 96, 26f, new Color(0.06f, 0.12f, 0.34f, 0.82f), new Color(0.03f, 0.06f, 0.2f, 0.9f), new Color(0.45f, 0.6f, 1f, 0.35f), 2f, 0.06f, 0f, true);

            public static Sprite Field()
                => Make("Field", 64, 64, 18f, new Color(0.02f, 0.04f, 0.14f, 0.95f), new Color(0.06f, 0.1f, 0.26f, 0.95f), new Color(0.45f, 0.6f, 1f, 0.45f), 2f, 0f, 0f, true);

            public static Sprite Shadow()
                => Make("Shadow", 128, 128, 48f, Color.white, Color.white, Color.white, 0f, 0f, 30f, true);

            public static Sprite SoftDot()
                => Make("Dot", 64, 64, 32f, Color.white, Color.white, Color.white, 0f, 0f, 14f, false);

            /// <summary>A ring (transparent centre) for the close button rim.</summary>
            public static Sprite Ring(Color top, Color bottom, float thickness)
            {
                string key = "Ring" + top + bottom + thickness;
                if (cache.TryGetValue(key, out var s) && s != null) return s;
                const int S = 128;
                var tex = NewTex(S, S, key);
                var px = new Color32[S * S];
                float r = S / 2f, inner = r * (1f - thickness);
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float dx = x + 0.5f - r, dy = y + 0.5f - r;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(r - 1f - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                        Color c = Color.Lerp(bottom, top, (float)y / S);
                        float edge = Mathf.Min(Mathf.Abs(d - (r - 1.5f)), Mathf.Abs(d - inner - 1f));
                        if (edge < 1.6f) c = Color.Lerp(c, DarkBrown, 1f - edge / 1.6f);
                        c.a = a;
                        px[y * S + x] = c;
                    }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                s = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
                cache[key] = s;
                return s;
            }

            /// <summary>White at the top fading to clear at the bottom (soft sheen overlay).</summary>
            public static Sprite VerticalFade()
            {
                const string key = "VFade";
                if (cache.TryGetValue(key, out var s) && s != null) return s;
                var tex = NewTex(4, 128, key);
                var px = new Color32[4 * 128];
                for (int y = 0; y < 128; y++)
                {
                    float t = y / 127f;
                    float a = Mathf.Pow(Mathf.Clamp01((t - 0.55f) / 0.45f), 1.6f);
                    for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color(1f, 1f, 1f, a);
                }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                s = Sprite.Create(tex, new Rect(0, 0, 4, 128), new Vector2(0.5f, 0.5f), 100f);
                cache[key] = s;
                return s;
            }

            /// <summary>Horizontal rule: fades out to the left (true), right (false), or both ends (null).</summary>
            public static Sprite HorizontalFade(bool? fadeLeft)
            {
                string key = "HFade" + (fadeLeft.HasValue ? fadeLeft.Value.ToString() : "both");
                if (cache.TryGetValue(key, out var s) && s != null) return s;
                var tex = NewTex(128, 4, key);
                var px = new Color32[128 * 4];
                for (int x = 0; x < 128; x++)
                {
                    float t = x / 127f;
                    float a = fadeLeft.HasValue ? (fadeLeft.Value ? t : 1f - t) : 1f - Mathf.Abs(t * 2f - 1f);
                    a = Mathf.SmoothStep(0f, 1f, a);
                    for (int y = 0; y < 4; y++) px[y * 128 + x] = new Color(1f, 1f, 1f, a);
                }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                s = Sprite.Create(tex, new Rect(0, 0, 128, 4), new Vector2(0.5f, 0.5f), 100f);
                cache[key] = s;
                return s;
            }

            private static Texture2D NewTex(int w, int h, string name)
                => new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "KoS_" + name };

            /// <summary>
            /// Rounded rectangle (sliced by its radius). blur > 0 makes a soft shadow instead of a hard shape.
            /// gloss adds a white highlight over the upper part.
            /// </summary>
            private static Sprite Make(string kind, int w, int h, float radius, Color top, Color bottom, Color border, float borderPx, float gloss, float blur, bool sliced)
            {
                string key = kind + w + "x" + h + radius + top + bottom + border + borderPx + gloss + blur;
                if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

                var tex = NewTex(w, h, kind);
                var px = new Color32[w * h];
                float hx = w / 2f, hy = h / 2f;
                float pad = blur > 0f ? blur : 0.5f;
                float rx = hx - pad, ry = hy - pad;
                float rad = Mathf.Min(radius, Mathf.Min(rx, ry));
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float px0 = Mathf.Abs(x + 0.5f - hx) - (rx - rad);
                        float py0 = Mathf.Abs(y + 0.5f - hy) - (ry - rad);
                        float outside = new Vector2(Mathf.Max(px0, 0f), Mathf.Max(py0, 0f)).magnitude + Mathf.Min(Mathf.Max(px0, py0), 0f) - rad;
                        Color c;
                        if (blur > 0f)
                        {
                            float a = Mathf.Clamp01(0.5f - outside / blur);
                            a = a * a * (3f - 2f * a);
                            c = new Color(top.r, top.g, top.b, a);
                        }
                        else
                        {
                            float t = (y + 0.5f) / h;
                            c = Color.Lerp(bottom, top, t);
                            // Gloss: a soft white band over the upper ~40%
                            if (gloss > 0f)
                            {
                                float g = Mathf.Clamp01((t - 0.52f) / 0.3f);
                                float inset = Mathf.Clamp01((-outside - borderPx) / Mathf.Max(2f, rad * 0.35f));
                                c = Color.Lerp(c, new Color(1f, 1f, 1f, c.a), gloss * 0.55f * g * inset);
                            }
                            // Lower inner shade for a bevel
                            float shade = Mathf.Clamp01(1f - (-outside - borderPx) / Mathf.Max(2f, rad * 0.5f)) * (1f - t);
                            c = Color.Lerp(c, new Color(c.r * 0.7f, c.g * 0.7f, c.b * 0.7f, c.a), shade * 0.5f);
                            if (borderPx > 0f)
                            {
                                float bt = Mathf.Clamp01(outside + borderPx + 0.5f);
                                c = Color.Lerp(c, border, bt * border.a + (1f - border.a) * bt * 0f);
                                if (border.a < 1f && bt > 0f) c.a = Mathf.Lerp(c.a, Mathf.Max(c.a, border.a), bt);
                            }
                            c.a *= Mathf.Clamp01(0.5f - outside);
                        }
                        px[y * w + x] = c;
                    }
                }
                tex.SetPixels32(px);
                tex.Apply(false, true);

                float b = sliced ? Mathf.Ceil(rad + pad + 1f) : 0f;
                var border4 = sliced ? new Vector4(Mathf.Min(b, hx - 1), Mathf.Min(b, hy - 1), Mathf.Min(b, hx - 1), Mathf.Min(b, hy - 1)) : Vector4.zero;
                var s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border4);
                cache[key] = s;
                return s;
            }
        }
    }
}
