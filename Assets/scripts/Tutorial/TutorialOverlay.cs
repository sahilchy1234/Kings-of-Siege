using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Siege.Tutorial
{
    /// <summary>Something on screen the tutorial points at: a UI rect, several UI rects, or a world object.</summary>
    public class TutorialTarget
    {
        /// <summary>Screen-pixel rect of the target this frame, or null when it can't be seen.</summary>
        public Func<Rect?> GetScreenRect;
        /// <summary>World targets get a round spotlight; UI targets a rounded frame.</summary>
        public bool round;

        public static TutorialTarget UI(RectTransform rt, float padPx = 14f)
        {
            if (rt == null) return null;
            return new TutorialTarget { GetScreenRect = () => Pad(ScreenRectOf(rt), padPx) };
        }

        public static TutorialTarget UIUnion(IList<RectTransform> rts, float padPx = 14f)
        {
            if (rts == null || rts.Count == 0) return null;
            var list = new List<RectTransform>(rts);
            return new TutorialTarget
            {
                GetScreenRect = () =>
                {
                    Rect? acc = null;
                    foreach (var rt in list)
                    {
                        var r = ScreenRectOf(rt);
                        if (!r.HasValue) continue;
                        acc = acc.HasValue ? Rect.MinMaxRect(Mathf.Min(acc.Value.xMin, r.Value.xMin), Mathf.Min(acc.Value.yMin, r.Value.yMin),
                                                             Mathf.Max(acc.Value.xMax, r.Value.xMax), Mathf.Max(acc.Value.yMax, r.Value.yMax)) : r;
                    }
                    return Pad(acc, padPx);
                }
            };
        }

        /// <summary>World object seen through Camera.main, highlighted with a circle of the given world radius.</summary>
        public static TutorialTarget World(Transform t, float worldRadius, float minPx = 140f)
        {
            if (t == null) return null;
            return new TutorialTarget
            {
                round = true,
                GetScreenRect = () =>
                {
                    if (t == null) return null;
                    var cam = Camera.main;
                    if (cam == null) return null;
                    Vector3 c = cam.WorldToScreenPoint(t.position);
                    if (c.z <= 0f) return null;
                    Vector3 e = cam.WorldToScreenPoint(t.position + cam.transform.right * worldRadius);
                    float r = Mathf.Max(minPx * 0.5f, Vector2.Distance(c, e));
                    r = Mathf.Min(r, Mathf.Min(Screen.width, Screen.height) * 0.42f);
                    // Keep the spotlight on screen when the object sits just past an edge.
                    c.x = Mathf.Clamp(c.x, r * 0.7f, Screen.width - r * 0.7f);
                    c.y = Mathf.Clamp(c.y, r * 0.7f, Screen.height - r * 0.7f);
                    return new Rect(c.x - r, c.y - r, r * 2f, r * 2f);
                }
            };
        }

        /// <summary>World object highlighted by a circle that encloses its on-screen bounds (from renderers).
        /// scale grows or shrinks the circle; minPx is the smallest diameter at 1080p.</summary>
        public static TutorialTarget WorldBounds(Func<Bounds?> getBounds, float scale = 1f, float minPx = 180f)
        {
            if (getBounds == null) return null;
            return new TutorialTarget
            {
                round = true,
                GetScreenRect = () =>
                {
                    var bb = getBounds();
                    var cam = Camera.main;
                    if (!bb.HasValue || cam == null) return null;
                    var b = bb.Value;
                    float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
                    bool any = false;
                    for (int i = 0; i < 8; i++)
                    {
                        var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) != 0 ? 1 : -1, (i & 2) != 0 ? 1 : -1, (i & 4) != 0 ? 1 : -1));
                        var p = cam.WorldToScreenPoint(corner);
                        if (p.z <= 0f) continue;
                        any = true;
                        x0 = Mathf.Min(x0, p.x); y0 = Mathf.Min(y0, p.y);
                        x1 = Mathf.Max(x1, p.x); y1 = Mathf.Max(y1, p.y);
                    }
                    if (!any) return null;
                    float s = Mathf.Max(0.6f, Mathf.Min(Screen.width, Screen.height) / 1080f);
                    var c = new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);
                    float r = Mathf.Max(x1 - x0, y1 - y0) * 0.5f * scale;
                    r = Mathf.Max(r, minPx * 0.5f * s);
                    r = Mathf.Min(r, Mathf.Min(Screen.width, Screen.height) * 0.46f);
                    c.x = Mathf.Clamp(c.x, r * 0.7f, Screen.width - r * 0.7f);
                    c.y = Mathf.Clamp(c.y, r * 0.7f, Screen.height - r * 0.7f);
                    return new Rect(c.x - r, c.y - r, r * 2f, r * 2f);
                }
            };
        }

        public static Rect? ScreenRectOf(RectTransform rt)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return null;
            var canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = null;
            if (canvas != null)
            {
                canvas = canvas.rootCanvas;
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) cam = canvas.worldCamera;
            }
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
                x0 = Mathf.Min(x0, p.x); y0 = Mathf.Min(y0, p.y);
                x1 = Mathf.Max(x1, p.x); y1 = Mathf.Max(y1, p.y);
            }
            if (x1 - x0 < 1f || y1 - y0 < 1f) return null;
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        private static Rect? Pad(Rect? r, float pad)
        {
            if (!r.HasValue) return null;
            float s = Mathf.Max(0.6f, Mathf.Min(Screen.width, Screen.height) / 1080f);
            float p = pad * s;
            return Rect.MinMaxRect(r.Value.xMin - p, r.Value.yMin - p, r.Value.xMax + p, r.Value.yMax + p);
        }
    }

    public enum HandMode { None, Tap, Drag }

    /// <summary>One line of the tutorial: what the advisor says, what is highlighted, and what ends the step.</summary>
    public class TutorialStep
    {
        public string text;
        public TutorialTarget target;
        public HandMode hand = HandMode.None;
        /// <summary>Where the hand taps or starts its drag, when it should not be the middle of the highlight (e.g. one card of a row).</summary>
        public TutorialTarget handAt;
        /// <summary>Screen point the hand drags to (HandMode.Drag).</summary>
        public Func<Vector2?> dragTo;
        /// <summary>Info step: any tap continues. The highlighted thing can't be pressed.</summary>
        public bool tapToContinue = true;
        /// <summary>Action step: a tap inside the highlight ends the step (and reaches the real button).</summary>
        public bool tapTarget;
        /// <summary>Action step: ends when this turns true. The highlight stays pressable.</summary>
        public Func<bool> until;
        /// <summary>Darkness of the dimmed area, 0..1.</summary>
        public float dim = 0.72f;
        /// <summary>Normalized screen height for the dialog centre, or -1 to place it away from the target.</summary>
        public float dialogY = -1f;
        /// <summary>Called once when a tapTarget step is tapped.</summary>
        public Action onTargetTapped;
        /// <summary>Real buttons under the highlight. A click on any of them also ends a tapTarget step.</summary>
        public List<Button> watchButtons;
    }

    /// <summary>
    /// Runtime-built overlay: dimmed screen with a hole over the target, glowing gold frame with corner brackets and
    /// sparkles (or a ring for world targets), animated pointing hand, the King's dialog panel in the game's UI art,
    /// title banners, confetti, and a Skip button with confirmation.
    /// Uses unscaled time only, so it keeps working while the battle is paused (timeScale 0).
    /// </summary>
    public class TutorialOverlay : MonoBehaviour
    {
        private static readonly Color Gold = new Color(1f, 0.82f, 0.25f, 1f);
        private static readonly Color Cream = new Color(1f, 0.97f, 0.88f, 1f);
        private const float DialogH = 320f;
        private const float GlowInset = 36f;

        public bool SkipRequested { get; private set; }
        /// <summary>Logs each step's highlight rect and missed taps (editor and development builds).</summary>
        public static bool? LogOverride;
        private static bool LogSteps => LogOverride ?? Debug.isDebugBuild;
        public bool IsShowing => canvas != null && canvas.enabled;
        public event Action OnSkipConfirmed;
        private bool forceAdvance;

        /// <summary>Finishes the current step as if the player did what it asks (testing: Kings of Siege > Tutorial > Advance Step).</summary>
        public void ForceAdvance() { forceAdvance = true; }

        private KoSTutorialArt art;
        private Canvas canvas;
        private CanvasScaler scaler;
        private RectTransform root;
        private CanvasGroup rootGroup;
        private CanvasGroup dimGroup;
        private RectTransform dimTop, dimBottom, dimLeft, dimRight;
        private Image spotlight;
        private Image infoBlocker;

        // Highlight
        private RectTransform glow, frame, ring;
        private Image glowImg, frameImg, ringImg;
        private readonly RectTransform[] brackets = new RectTransform[4];
        private readonly RectTransform[] sparkles = new RectTransform[5];
        private RectTransform hand, handShadow, ripple;
        private CanvasGroup handGroup;
        private Image rippleImg;

        // Dialog
        private RectTransform dialog;
        private CanvasGroup dialogGroup;
        private RectTransform advisor;
        private Vector2 advisorBase;
        private RectTransform bodyRect;
        private TextMeshProUGUI body, nameText, chapterText;
        private RectTransform nextBtn, turnTag, chapterChip;
        private CanvasGroup nextGroup, turnGroup, chapterGroup;
        private string chapter = "";

        // Skip
        private RectTransform skipBtn;
        private RectTransform confirm, confirmBox;
        private CanvasGroup confirmGroup;
        private float confirmT;

        // Banner + confetti
        private RectTransform banner, bannerRibbon, bannerRays;
        private CanvasGroup bannerGroup;
        private Image bannerRaysImg;
        private TextMeshProUGUI bannerTitle, bannerSub;
        private RectTransform confettiLayer;
        private class Piece { public RectTransform rt; public Image img; public Vector2 v; public float spin, life, maxLife, seed; }
        private readonly List<Piece> confetti = new List<Piece>();

        private TutorialStep step;
        private Rect? holeScreen;
        private float typed;
        private bool typing;
        private float stepTime;
        private float dialogTargetY = -1f;
        private bool confirmOpen;
        private bool skipClickedThisFrame;
        private bool freshPop;
        private float popT = 1f, kingIn = 1f, kingPunch;
        private bool nextVisible;

        private static Sprite roundSprite, ringRoundSprite, frameSprite, circleRingSprite, goldRingSprite, spotSprite, softDotSprite,
            whiteSprite, glowRectSprite, glowCircleSprite, shadowSprite, bracketSprite, raysSprite, handFallback;

        // ───────────────────────────── Build ─────────────────────────────

        public void Build(KoSTutorialArt artAsset)
        {
            art = artAsset;
            MakeSprites();

            var cgo = new GameObject("TutorialCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            canvas.overrideSorting = true;
            scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.matchWidthOrHeight = 0.5f;
            UpdateReference();
            root = cgo.GetComponent<RectTransform>();
            rootGroup = cgo.GetComponent<CanvasGroup>();

            // Dim around the hole
            var dims = Box(root, "Dim", Vector2.zero, Vector2.one);
            dimGroup = dims.gameObject.AddComponent<CanvasGroup>();
            dimTop = DimPiece(dims, "Top");
            dimBottom = DimPiece(dims, "Bottom");
            dimLeft = DimPiece(dims, "Left");
            dimRight = DimPiece(dims, "Right");
            var spot = Box(dims, "Spotlight", Vector2.zero, Vector2.zero);
            spotlight = spot.gameObject.AddComponent<Image>();
            spotlight.sprite = spotSprite;
            spotlight.color = Color.black;
            spotlight.raycastTarget = true;

            // Full-screen catcher for info steps (keeps the highlighted button from being pressed)
            infoBlocker = Box(root, "InfoBlocker", Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            infoBlocker.color = new Color(0, 0, 0, 0);

            // Soft glow behind the frame
            glow = Box(root, "Glow", Vector2.zero, Vector2.zero);
            glowImg = glow.gameObject.AddComponent<Image>();
            glowImg.sprite = glowRectSprite;
            glowImg.type = Image.Type.Sliced;
            glowImg.raycastTarget = false;

            // Gold frame for UI targets, ring for world targets
            frame = Box(root, "Frame", Vector2.zero, Vector2.zero);
            frameImg = frame.gameObject.AddComponent<Image>();
            frameImg.sprite = frameSprite;
            frameImg.type = Image.Type.Sliced;
            frameImg.raycastTarget = false;

            ring = Box(root, "Ring", Vector2.zero, Vector2.zero);
            ringImg = ring.gameObject.AddComponent<Image>();
            ringImg.sprite = goldRingSprite;
            ringImg.raycastTarget = false;

            for (int i = 0; i < brackets.Length; i++)
            {
                brackets[i] = Box(root, "Bracket" + i, Vector2.zero, Vector2.zero);
                brackets[i].pivot = new Vector2(0f, 1f);
                var bi = brackets[i].gameObject.AddComponent<Image>();
                bi.sprite = bracketSprite;
                bi.raycastTarget = false;
            }
            for (int i = 0; i < sparkles.Length; i++)
            {
                sparkles[i] = Box(root, "Sparkle" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                sparkles[i].sizeDelta = new Vector2(64, 64);
                var si = sparkles[i].gameObject.AddComponent<Image>();
                bool hasArt = art != null && art.sparkle != null;
                si.sprite = hasArt ? art.sparkle : softDotSprite;
                si.color = hasArt ? Color.white : new Color(1f, 0.9f, 0.5f, 0.9f);
                si.preserveAspect = true;
                si.raycastTarget = false;
            }

            // Tap ripple + hand (with a drop shadow)
            ripple = Box(root, "Ripple", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            ripple.sizeDelta = new Vector2(170, 170);
            rippleImg = ripple.gameObject.AddComponent<Image>();
            rippleImg.sprite = circleRingSprite;
            rippleImg.color = new Color(1, 1, 1, 0);
            rippleImg.raycastTarget = false;

            hand = Box(root, "Hand", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            hand.sizeDelta = new Vector2(180, 180);
            hand.pivot = new Vector2(0.45f, 0.95f);
            handGroup = hand.gameObject.AddComponent<CanvasGroup>();
            handGroup.blocksRaycasts = false;
            var handSprite = art != null && art.hand != null ? art.hand : handFallback;
            handShadow = Box(hand, "Shadow", Vector2.zero, Vector2.one);
            var hs = handShadow.gameObject.AddComponent<Image>();
            hs.sprite = handSprite;
            hs.preserveAspect = true;
            hs.color = new Color(0f, 0f, 0f, 0.4f);
            hs.raycastTarget = false;
            var glove = Box(hand, "Glove", Vector2.zero, Vector2.one);
            var hi = glove.gameObject.AddComponent<Image>();
            hi.sprite = handSprite;
            hi.preserveAspect = true;
            hi.raycastTarget = false;

            BuildDialog();
            BuildBanner();
            confettiLayer = Box(root, "Confetti", Vector2.zero, Vector2.one);
            BuildSkip();
            BuildConfirm();

            HideHighlight();
            canvas.enabled = false;
        }

        private void BuildDialog()
        {
            dialog = Box(root, "Dialog", new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.3f));
            dialog.sizeDelta = new Vector2(1000, DialogH);
            dialogGroup = dialog.gameObject.AddComponent<CanvasGroup>();

            var shadow = Box(dialog, "Shadow", Vector2.zero, Vector2.one);
            shadow.offsetMin = new Vector2(-32, -32 - 16);
            shadow.offsetMax = new Vector2(32, 32 - 16);
            var shi = shadow.gameObject.AddComponent<Image>();
            shi.sprite = shadowSprite;
            shi.type = Image.Type.Sliced;
            shi.color = new Color(0f, 0f, 0f, 0.6f);
            shi.raycastTarget = false;

            var panel = Box(dialog, "Panel", Vector2.zero, Vector2.one);
            var pi = panel.gameObject.AddComponent<Image>();
            if (art != null && art.dialogPanel != null)
            {
                pi.sprite = art.dialogPanel;
                pi.type = Image.Type.Sliced;
                pi.pixelsPerUnitMultiplier = 0.75f;
                pi.color = Color.white;
            }
            else
            {
                pi.sprite = roundSprite;
                pi.type = Image.Type.Sliced;
                pi.color = Color.white;
                var trim = Box(panel, "Trim", Vector2.zero, Vector2.one);
                trim.offsetMin = new Vector2(8, 8);
                trim.offsetMax = new Vector2(-8, -8);
                var ti = trim.gameObject.AddComponent<Image>();
                ti.sprite = ringRoundSprite;
                ti.type = Image.Type.Sliced;
                ti.color = new Color(1f, 0.85f, 0.35f, 0.55f);
                ti.raycastTarget = false;
            }

            // Advisor portrait standing on the bottom-left, taller than the panel
            advisor = Box(dialog, "Advisor", Vector2.zero, Vector2.zero);
            advisor.pivot = new Vector2(0.5f, 0f);
            advisor.sizeDelta = new Vector2(300, 400);
            advisorBase = new Vector2(152, 6);
            advisor.anchoredPosition = advisorBase;
            var advisorImg = advisor.gameObject.AddComponent<Image>();
            advisorImg.preserveAspect = true;
            advisorImg.raycastTarget = false;
            if (art != null && art.advisor != null) advisorImg.sprite = art.advisor;
            else advisor.gameObject.SetActive(false);
            bool hasAdvisor = advisor.gameObject.activeSelf;

            // Name ribbon over the top edge
            var tag = Box(dialog, "NameTag", new Vector2(0, 1), new Vector2(0, 1));
            tag.pivot = new Vector2(0, 0.5f);
            var tagImg = tag.gameObject.AddComponent<Image>();
            tagImg.raycastTarget = false;
            RectTransform nameArea;
            if (art != null && art.nameRibbon != null)
            {
                tagImg.sprite = art.nameRibbon;
                tagImg.preserveAspect = true;
                tag.sizeDelta = new Vector2(330, 128);
                tag.anchoredPosition = new Vector2(hasAdvisor ? 262 : 30, 8);
                nameArea = Box(tag, "Area", new Vector2(0.16f, 0.32f), new Vector2(0.84f, 0.96f));
            }
            else
            {
                tagImg.sprite = roundSprite; tagImg.type = Image.Type.Sliced; tagImg.color = Gold;
                tag.sizeDelta = new Vector2(320, 72);
                tag.anchoredPosition = new Vector2(hasAdvisor ? 262 : 30, 0);
                nameArea = Box(tag, "Area", Vector2.zero, Vector2.one);
            }
            nameText = Text(nameArea, "Name", art != null && !string.IsNullOrEmpty(art.advisorName) ? art.advisorName.ToUpperInvariant() : "THE KING", 36, Color.white, TextAlignmentOptions.Center, true);
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 22;
            nameText.fontSizeMax = 36;

            // Chapter chip (info steps) and YOUR TURN ribbon (action steps), top-right
            chapterChip = Box(dialog, "Chapter", new Vector2(1, 1), new Vector2(1, 1));
            chapterChip.pivot = new Vector2(1, 0.5f);
            chapterChip.sizeDelta = new Vector2(300, 52);
            chapterChip.anchoredPosition = new Vector2(-44, 0);
            chapterGroup = chapterChip.gameObject.AddComponent<CanvasGroup>();
            chapterGroup.blocksRaycasts = false;
            var ci = chapterChip.gameObject.AddComponent<Image>();
            ci.sprite = roundSprite;
            ci.type = Image.Type.Sliced;
            ci.pixelsPerUnitMultiplier = 1.6f;
            ci.raycastTarget = false;
            chapterText = Text(chapterChip, "Text", "", 24, Gold, TextAlignmentOptions.Center, false);
            chapterText.characterSpacing = 6f;
            chapterText.enableAutoSizing = true;
            chapterText.fontSizeMin = 16;
            chapterText.fontSizeMax = 24;
            chapterText.rectTransform.offsetMin = new Vector2(18, 2);
            chapterText.rectTransform.offsetMax = new Vector2(-18, 0);

            turnTag = Box(dialog, "YourTurn", new Vector2(1, 1), new Vector2(1, 1));
            turnTag.pivot = new Vector2(0.5f, 0.5f);
            turnTag.anchoredPosition = new Vector2(-190, 14);
            turnGroup = turnTag.gameObject.AddComponent<CanvasGroup>();
            turnGroup.blocksRaycasts = false;
            var tti = turnTag.gameObject.AddComponent<Image>();
            tti.raycastTarget = false;
            RectTransform turnArea;
            if (art != null && art.actionRibbon != null)
            {
                tti.sprite = art.actionRibbon;
                tti.preserveAspect = true;
                turnTag.sizeDelta = new Vector2(320, 124);
                turnArea = Box(turnTag, "Area", new Vector2(0.16f, 0.32f), new Vector2(0.84f, 0.96f));
            }
            else
            {
                tti.sprite = roundSprite; tti.type = Image.Type.Sliced; tti.color = new Color(0.3f, 0.85f, 0.35f);
                turnTag.sizeDelta = new Vector2(300, 70);
                turnArea = Box(turnTag, "Area", Vector2.zero, Vector2.one);
            }
            var tt = Text(turnArea, "Text", "YOUR TURN!", 36, Color.white, TextAlignmentOptions.Center, true);
            tt.enableAutoSizing = true;
            tt.fontSizeMin = 22;
            tt.fontSizeMax = 36;

            bodyRect = Box(dialog, "Body", Vector2.zero, Vector2.one);
            bodyRect.offsetMin = new Vector2(hasAdvisor ? 304 : 48, 66);
            bodyRect.offsetMax = new Vector2(-46, -64);
            body = Text(bodyRect, "Text", "", 40, Cream, TextAlignmentOptions.TopLeft, false);
            body.enableAutoSizing = true;
            body.fontSizeMin = 26;
            body.fontSizeMax = 40;
            body.lineSpacing = -2;

            // NEXT button straddling the bottom edge (visual only: any tap continues)
            nextBtn = Box(dialog, "Next", new Vector2(1, 0), new Vector2(1, 0));
            nextBtn.pivot = new Vector2(0.5f, 0.5f);
            nextBtn.sizeDelta = new Vector2(210, 86);
            nextBtn.anchoredPosition = new Vector2(-150, 4);
            nextGroup = nextBtn.gameObject.AddComponent<CanvasGroup>();
            nextGroup.blocksRaycasts = false;
            var ni = nextBtn.gameObject.AddComponent<Image>();
            ni.raycastTarget = false;
            if (art != null && art.buttonYellow != null)
            {
                ni.sprite = art.buttonYellow;
                ni.type = Image.Type.Sliced;
                ni.pixelsPerUnitMultiplier = PillPpu(art.buttonYellow, 86f);
            }
            else { ni.sprite = roundSprite; ni.type = Image.Type.Sliced; ni.color = Gold; }
            var nt = Text(nextBtn, "Label", "NEXT", 38, Color.white, TextAlignmentOptions.Center, true);
            nt.rectTransform.offsetMin = new Vector2(10, 10);
            nt.rectTransform.offsetMax = new Vector2(-10, 0);
        }

        private void BuildBanner()
        {
            banner = Box(root, "Banner", Vector2.zero, Vector2.one);
            bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.blocksRaycasts = false;
            var c = new Vector2(0.5f, 0.6f);

            bannerRays = Box(banner, "Rays", c, c);
            bannerRays.sizeDelta = new Vector2(1400, 1400);
            bannerRaysImg = bannerRays.gameObject.AddComponent<Image>();
            bannerRaysImg.sprite = raysSprite;
            bannerRaysImg.color = new Color(1f, 0.85f, 0.4f, 0.45f);
            bannerRaysImg.raycastTarget = false;

            bannerRibbon = Box(banner, "Ribbon", c, c);
            var ri = bannerRibbon.gameObject.AddComponent<Image>();
            ri.raycastTarget = false;
            RectTransform area;
            if (art != null && art.nameRibbon != null)
            {
                ri.sprite = art.nameRibbon;
                ri.preserveAspect = true;
                bannerRibbon.sizeDelta = new Vector2(920, 356);
                area = Box(bannerRibbon, "Area", new Vector2(0.17f, 0.34f), new Vector2(0.83f, 0.95f));
            }
            else
            {
                ri.sprite = roundSprite; ri.type = Image.Type.Sliced; ri.color = Gold;
                bannerRibbon.sizeDelta = new Vector2(900, 180);
                area = Box(bannerRibbon, "Area", Vector2.zero, Vector2.one);
            }
            bannerTitle = Text(area, "Title", "", 72, Color.white, TextAlignmentOptions.Center, true);
            bannerTitle.enableAutoSizing = true;
            bannerTitle.fontSizeMin = 34;
            bannerTitle.fontSizeMax = 72;
            bannerTitle.textWrappingMode = TextWrappingModes.NoWrap;

            var sub = Box(banner, "Sub", c, c);
            sub.pivot = new Vector2(0.5f, 1f);
            sub.sizeDelta = new Vector2(1000, 120);
            sub.anchoredPosition = new Vector2(0, -195);
            bannerSub = Text(sub, "Text", "", 42, Cream, TextAlignmentOptions.Top, false);
            bannerSub.outlineWidth = 0.18f;
            bannerSub.outlineColor = new Color32(10, 15, 40, 255);

            banner.gameObject.SetActive(false);
        }

        private void BuildSkip()
        {
            skipBtn = Box(root, "Skip", new Vector2(1, 1), new Vector2(1, 1));
            skipBtn.pivot = new Vector2(1, 1);
            skipBtn.sizeDelta = new Vector2(190, 76);
            skipBtn.anchoredPosition = new Vector2(-28, -250);
            var img = skipBtn.gameObject.AddComponent<Image>();
            if (art != null && art.buttonNavy != null)
            {
                img.sprite = art.buttonNavy;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = PillPpu(art.buttonNavy, 76f);
            }
            else { img.sprite = roundSprite; img.type = Image.Type.Sliced; img.color = new Color(0.75f, 0.8f, 0.95f, 0.95f); }
            var t = Text(skipBtn, "Label", "SKIP", 32, Color.white, TextAlignmentOptions.Center, true);
            t.rectTransform.offsetMin = new Vector2(8, 8);
            var b = skipBtn.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(() => { skipClickedThisFrame = true; OpenConfirm(); });
        }

        private void BuildConfirm()
        {
            confirm = Box(root, "SkipConfirm", Vector2.zero, Vector2.one);
            confirmGroup = confirm.gameObject.AddComponent<CanvasGroup>();
            var bg = confirm.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0.02f, 0.08f, 0.8f);

            confirmBox = Box(confirm, "Box", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            confirmBox.sizeDelta = new Vector2(840, 470);
            var bi = confirmBox.gameObject.AddComponent<Image>();
            if (art != null && art.dialogPanel != null)
            {
                bi.sprite = art.dialogPanel;
                bi.type = Image.Type.Sliced;
                bi.pixelsPerUnitMultiplier = 0.7f;
            }
            else { bi.sprite = roundSprite; bi.type = Image.Type.Sliced; }

            RectTransform titleArea;
            if (art != null && art.nameRibbon != null)
            {
                var rib = Box(confirmBox, "Ribbon", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
                rib.sizeDelta = new Vector2(600, 232);
                rib.anchoredPosition = new Vector2(0, 6);
                var ri = rib.gameObject.AddComponent<Image>();
                ri.sprite = art.nameRibbon;
                ri.preserveAspect = true;
                ri.raycastTarget = false;
                titleArea = Box(rib, "Area", new Vector2(0.17f, 0.34f), new Vector2(0.83f, 0.95f));
            }
            else
            {
                titleArea = Box(confirmBox, "Area", new Vector2(0, 1), new Vector2(1, 1));
                titleArea.pivot = new Vector2(0.5f, 1);
                titleArea.sizeDelta = new Vector2(-60, 90);
                titleArea.anchoredPosition = new Vector2(0, -30);
            }
            var title = Text(titleArea, "Title", "SKIP TUTORIAL?", 50, Color.white, TextAlignmentOptions.Center, true);
            title.enableAutoSizing = true;
            title.fontSizeMin = 28;
            title.fontSizeMax = 50;

            var msg = Text(confirmBox, "Msg", "You can replay it any time from <b>Settings</b>.", 36, Cream, TextAlignmentOptions.Center, false);
            msg.rectTransform.anchorMin = new Vector2(0, 0.38f);
            msg.rectTransform.anchorMax = new Vector2(1, 0.76f);
            msg.rectTransform.offsetMin = new Vector2(60, 0);
            msg.rectTransform.offsetMax = new Vector2(-60, 0);

            MakeButton(confirmBox, "Keep", "KEEP LEARNING", art != null ? art.buttonGreen : null, new Color(0.2f, 0.72f, 0.3f), 0.06f, 0.49f, CloseConfirm);
            MakeButton(confirmBox, "Yes", "SKIP", art != null ? art.buttonRed : null, new Color(0.88f, 0.28f, 0.22f), 0.51f, 0.94f, () =>
            {
                CloseConfirm();
                SkipRequested = true;
                OnSkipConfirmed?.Invoke();
            });
            confirm.gameObject.SetActive(false);
        }

        private void MakeButton(RectTransform parent, string name, string label, Sprite sprite, Color fallback, float x0, float x1, Action onClick)
        {
            var r = Box(parent, name, new Vector2(x0, 0), new Vector2(x1, 0));
            r.pivot = new Vector2(0.5f, 0);
            r.sizeDelta = new Vector2(0, 112);
            r.anchoredPosition = new Vector2(0, 46);
            var img = r.gameObject.AddComponent<Image>();
            img.type = Image.Type.Sliced;
            if (sprite != null) { img.sprite = sprite; img.pixelsPerUnitMultiplier = PillPpu(sprite, 112f); }
            else { img.sprite = roundSprite; img.color = fallback; }
            var t = Text(r, "Label", label, 36, Color.white, TextAlignmentOptions.Center, true);
            t.enableAutoSizing = true;
            t.fontSizeMin = 22;
            t.fontSizeMax = 36;
            t.rectTransform.offsetMin = new Vector2(18, 14);
            t.rectTransform.offsetMax = new Vector2(-18, -4);
            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(() => { skipClickedThisFrame = true; TutorialSound("ui"); onClick(); });
        }

        private void OpenConfirm()
        {
            if (confirmOpen) return;
            confirmOpen = true;
            TutorialSound("ui");
            confirm.gameObject.SetActive(true);
            confirm.SetAsLastSibling();
            confirmGroup.alpha = 0f;
            confirmT = 0f;
            confirmBox.localScale = Vector3.one * 0.75f;
        }

        private void CloseConfirm()
        {
            confirmOpen = false;
            confirm.gameObject.SetActive(false);
        }

        // ───────────────────────────── Public API ─────────────────────────────

        public void Show()
        {
            if (canvas == null) return;
            SkipRequested = false;
            canvas.enabled = true;
            rootGroup.alpha = 1f;
        }

        public void Hide()
        {
            if (canvas == null) return;
            step = null;
            canvas.enabled = false;
            dialogGroup.alpha = 0f;
            banner.gameObject.SetActive(false);
            foreach (var p in confetti) p.rt.gameObject.SetActive(false);
            CloseConfirm();
        }

        public void ShowSkipButton(bool show)
        {
            if (skipBtn != null) skipBtn.gameObject.SetActive(show);
        }

        /// <summary>Name of the current part of the tutorial, shown on a chip at the top-right of the dialog.</summary>
        public void SetChapter(string name)
        {
            chapter = name ?? "";
            if (chapterText != null) chapterText.text = chapter.ToUpperInvariant();
        }

        /// <summary>Runs one step until it is finished (tap, target tap, condition) or Skip is confirmed.</summary>
        public IEnumerator Run(TutorialStep s)
        {
            if (s == null || SkipRequested) yield break;
            Show();
            step = s;
            forceAdvance = false;
            stepTime = 0f;
            typed = 0f;
            typing = true;
            body.text = s.text ?? "";
            body.maxVisibleCharacters = 0;
            nextVisible = false;
            nextGroup.alpha = 0f;

            // A dialog that is already up swaps its text with a small bounce; otherwise it pops in and the King walks in.
            freshPop = dialogGroup.alpha < 0.3f;
            popT = 0f;
            if (freshPop)
            {
                dialogGroup.alpha = 0f;
                dialog.localScale = Vector3.one * 0.6f;
                dialogTargetY = -1f;
                kingIn = 0f;
            }
            else kingPunch = 1f;

            bool action = s.tapTarget || s.until != null;
            infoBlocker.gameObject.SetActive(!action);
            handGroup.alpha = 0f;
            Refresh();
            TutorialSound("card_select");

            // Small grace period so the tap that ended the previous step doesn't skip this one.
            float grace = 0.25f;
            bool targetTapped = false;
            bool buttonHit = false;
            UnityEngine.Events.UnityAction onHit = () => buttonHit = true;
            if (s.tapTarget && s.watchButtons != null)
                foreach (var b in s.watchButtons) if (b != null) b.onClick.AddListener(onHit);
            if (LogSteps)
                Debug.Log("[KoSTutorial] Step \"" + Short(s.text) + "\" target=" + (holeScreen.HasValue ? holeScreen.Value.ToString() : "none") + " screen=" + Screen.width + "x" + Screen.height);
            while (!SkipRequested)
            {
                yield return null;
                grace -= Time.unscaledDeltaTime;
                if (confirmOpen) continue;
                if (forceAdvance)
                {
                    forceAdvance = false;
                    targetTapped = s.tapTarget;
                    break;
                }

                bool released = PointerReleased(out Vector2 pos);
                if (skipClickedThisFrame) { skipClickedThisFrame = false; released = false; }
                if (released && IsOverSkip(pos)) released = false;

                // Action steps react even while the text is still typing: the real button got the tap too.
                if (s.until != null && s.until()) break;
                if (s.tapTarget && (buttonHit || (released && grace <= 0f && holeScreen.HasValue && holeScreen.Value.Contains(pos))))
                {
                    targetTapped = true;
                    break;
                }
                if (s.tapTarget && released && LogSteps)
                    Debug.Log("[KoSTutorial] Tap at " + pos + " is outside the highlight " + (holeScreen.HasValue ? holeScreen.Value.ToString() : "none"));
                if (action) { if (released && typing) typed = 9999f; continue; }

                if (typing)
                {
                    if (released && grace <= 0f) typed = 9999f;
                    continue;
                }

                if (released && grace <= 0f) break;
            }

            if (s.watchButtons != null)
                foreach (var b in s.watchButtons) if (b != null) b.onClick.RemoveListener(onHit);

            if (targetTapped && !SkipRequested)
            {
                TutorialSound("card_play");
                s.onTargetTapped?.Invoke();
            }
            else if (!SkipRequested && !action) TutorialSound("ui");
            step = null;
        }

        /// <summary>Shows only the dim layer (no text) for short waits between steps.</summary>
        public void Blank(float dim = 0f)
        {
            Show();
            step = null;
            holeScreen = null;
            dimGroup.alpha = LinearDim(dim);
            dialogGroup.alpha = 0f;
            handGroup.alpha = 0f;
            HideHighlight();
            infoBlocker.gameObject.SetActive(true);
        }

        /// <summary>Big title ribbon that slams in over a dimmed screen, e.g. "TRAINING BATTLE". A tap shortens it.</summary>
        public IEnumerator Banner(string title, string subtitle = null, float hold = 1.6f)
        {
            if (canvas == null || SkipRequested) yield break;
            Show();
            step = null;
            holeScreen = null;
            HideHighlight();
            handGroup.alpha = 0f;
            dialogGroup.alpha = 0f;
            infoBlocker.gameObject.SetActive(true);
            bannerTitle.text = title ?? "";
            bannerSub.text = subtitle ?? "";
            banner.gameObject.SetActive(true);
            AnimateBanner(0f, hold);
            TutorialSound("banner_gong");

            float t = 0f, inDur = 0.45f, outDur = 0.35f;
            while (t < inDur + hold + outDur && !SkipRequested)
            {
                yield return null;
                if (confirmOpen) continue;
                float dt = Time.unscaledDeltaTime;
                t += dt;
                bool released = PointerReleased(out Vector2 pos);
                if (skipClickedThisFrame) { skipClickedThisFrame = false; released = false; }
                if (released && !IsOverSkip(pos) && t > 0.5f && t < inDur + hold) t = inDur + hold;
                dimGroup.alpha = Mathf.MoveTowards(dimGroup.alpha, LinearDim(0.72f), dt * 3f);
                AnimateBanner(t, hold);
            }
            banner.gameObject.SetActive(false);
        }

        /// <summary>Confetti burst from a screen point (default: upper middle of the screen).</summary>
        public void Burst(Vector2? screenPos = null, int count = 70)
        {
            if (canvas == null || !canvas.enabled) return;
            Vector2 size = root.rect.size;
            Vector2 origin = screenPos.HasValue
                ? new Vector2((screenPos.Value.x / Mathf.Max(1, Screen.width) - 0.5f) * size.x, (screenPos.Value.y / Mathf.Max(1, Screen.height) - 0.5f) * size.y)
                : new Vector2(0f, size.y * 0.12f);
            Color[] palette =
            {
                new Color(1f, 0.82f, 0.2f), new Color(1f, 0.35f, 0.3f), new Color(0.3f, 0.65f, 1f),
                new Color(0.35f, 0.9f, 0.4f), new Color(1f, 1f, 1f), new Color(0.85f, 0.45f, 1f)
            };
            for (int i = 0; i < count; i++)
            {
                Piece p = null;
                foreach (var q in confetti) if (!q.rt.gameObject.activeSelf) { p = q; break; }
                if (p == null)
                {
                    var r = Box(confettiLayer, "Bit", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                    var img = r.gameObject.AddComponent<Image>();
                    img.sprite = whiteSprite;
                    img.raycastTarget = false;
                    p = new Piece { rt = r, img = img };
                    confetti.Add(p);
                }
                p.rt.gameObject.SetActive(true);
                p.rt.sizeDelta = new Vector2(UnityEngine.Random.Range(16f, 28f), UnityEngine.Random.Range(9f, 15f));
                p.rt.anchoredPosition = origin + UnityEngine.Random.insideUnitCircle * 40f;
                p.img.color = palette[UnityEngine.Random.Range(0, palette.Length)];
                float ang = UnityEngine.Random.Range(15f, 165f) * Mathf.Deg2Rad;
                float spd = UnityEngine.Random.Range(900f, 1800f);
                p.v = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd;
                p.spin = UnityEngine.Random.Range(-720f, 720f);
                p.life = 0f;
                p.maxLife = UnityEngine.Random.Range(1.8f, 2.8f);
                p.seed = UnityEngine.Random.Range(0f, 10f);
            }
        }

        // ───────────────────────────── Per-frame ─────────────────────────────

        private void Update()
        {
            if (canvas == null || !canvas.enabled) return;
            UpdateReference();
            float dt = Time.unscaledDeltaTime;
            UpdateConfetti(dt);
            if (confirmOpen)
            {
                confirmGroup.alpha = Mathf.MoveTowards(confirmGroup.alpha, 1f, dt * 6f);
                confirmT += dt;
                confirmBox.localScale = Vector3.one * Mathf.LerpUnclamped(0.75f, 1f, EaseOutBack(Mathf.Clamp01(confirmT / 0.3f)));
            }
            if (step == null) return;

            stepTime += dt;
            bool action = step.tapTarget || step.until != null;

            if (typing)
            {
                typed += dt * 55f;
                body.ForceMeshUpdate();
                int total = body.textInfo.characterCount;
                body.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(typed));
                if (typed >= total)
                {
                    typing = false;
                    body.maxVisibleCharacters = 99999;
                    nextVisible = step.tapToContinue && !action;
                }
            }

            // Dialog pop-in or text-swap bounce
            popT = Mathf.Min(1f, popT + dt / (freshPop ? 0.38f : 0.25f));
            if (freshPop)
            {
                dialog.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, EaseOutBack(popT));
                dialogGroup.alpha = Mathf.Clamp01(popT * 3f);
            }
            else
            {
                dialog.localScale = Vector3.one * Mathf.LerpUnclamped(0.96f, 1f, EaseOutBack(popT));
                dialogGroup.alpha = Mathf.MoveTowards(dialogGroup.alpha, 1f, dt * 6f);
            }

            // NEXT button, YOUR TURN ribbon, chapter chip
            nextGroup.alpha = Mathf.MoveTowards(nextGroup.alpha, nextVisible ? 1f : 0f, dt * 7f);
            nextBtn.localScale = Vector3.one * (nextVisible ? (1f + 0.05f * Mathf.Sin(stepTime * 5f)) * Mathf.LerpUnclamped(0.6f, 1f, EaseOutBack(nextGroup.alpha)) : 1f);
            turnGroup.alpha = Mathf.MoveTowards(turnGroup.alpha, action ? 1f : 0f, dt * 7f);
            turnTag.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(stepTime * 6f));
            turnTag.localRotation = Quaternion.Euler(0, 0, 3f * Mathf.Sin(stepTime * 3f));
            chapterGroup.alpha = Mathf.MoveTowards(chapterGroup.alpha, !action && chapter.Length > 0 ? 1f : 0f, dt * 7f);

            AnimateAdvisor(dt);
            Refresh();
        }

        private void AnimateAdvisor(float dt)
        {
            if (advisor == null || !advisor.gameObject.activeSelf) return;
            kingIn = Mathf.MoveTowards(kingIn, 1f, dt / 0.5f);
            kingPunch = Mathf.MoveTowards(kingPunch, 0f, dt * 2.5f);
            float tt = Time.unscaledTime;
            float slide = 1f - EaseOutBack(kingIn);
            float breathe = Mathf.Sin(tt * 2.2f);
            float talk = typing ? Mathf.Sin(tt * 15f) : 0f;
            advisor.anchoredPosition = advisorBase + new Vector2(-340f * slide, Mathf.Abs(talk) * 7f);
            advisor.localRotation = Quaternion.Euler(0, 0, talk * 2.2f);
            float sq = kingPunch * Mathf.Sin((1f - kingPunch) * Mathf.PI * 3f) * 0.08f;
            advisor.localScale = new Vector3(1f + 0.01f * breathe - sq, 1f + 0.018f * breathe + sq, 1f);
        }

        private void AnimateBanner(float t, float hold)
        {
            float k = Mathf.Clamp01(t / 0.45f);
            float o = Mathf.Clamp01((t - 0.45f - hold) / 0.35f);
            bannerGroup.alpha = Mathf.Clamp01(k * 2.5f) * (1f - o);
            bannerRibbon.localScale = Vector3.one * Mathf.LerpUnclamped(2.4f, 1f, EaseOutBack(k)) * (1f + 0.1f * o);
            bannerRibbon.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-8f, 0f, EaseOutBack(k)));
            bannerRays.localRotation = Quaternion.Euler(0, 0, -t * 22f);
            bannerRays.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, 1f - Mathf.Pow(1f - k, 3f));
            float sa = Mathf.Clamp01((t - 0.3f) / 0.3f);
            bannerSub.alpha = sa;
            ((RectTransform)bannerSub.rectTransform.parent).anchoredPosition = new Vector2(0, -195f - 30f * (1f - sa));
        }

        private void UpdateConfetti(float dt)
        {
            for (int i = 0; i < confetti.Count; i++)
            {
                var p = confetti[i];
                if (!p.rt.gameObject.activeSelf) continue;
                p.life += dt;
                if (p.life >= p.maxLife) { p.rt.gameObject.SetActive(false); continue; }
                p.v.y -= 2600f * dt;
                p.v *= 1f - Mathf.Min(0.9f, 1.4f * dt);
                p.rt.anchoredPosition += p.v * dt;
                p.rt.localRotation = Quaternion.Euler(0, 0, p.seed * 36f + p.spin * p.life);
                p.rt.localScale = new Vector3(Mathf.Cos(p.life * 11f + p.seed), 1f, 1f);
                var c = p.img.color;
                c.a = 1f - Mathf.Clamp01((p.life - (p.maxLife - 0.5f)) / 0.5f);
                p.img.color = c;
            }
        }

        private void HideHighlight()
        {
            glow.gameObject.SetActive(false);
            frame.gameObject.SetActive(false);
            ring.gameObject.SetActive(false);
            spotlight.gameObject.SetActive(false);
            foreach (var b in brackets) b.gameObject.SetActive(false);
            foreach (var s in sparkles) s.gameObject.SetActive(false);
            rippleImg.color = new Color(1, 1, 1, 0);
            if (nextGroup != null) nextGroup.alpha = 0f;
            if (turnGroup != null) turnGroup.alpha = 0f;
            if (chapterGroup != null) chapterGroup.alpha = 0f;
            SetAnchors(dimTop, new Vector2(0, 0.5f), Vector2.one);
            SetAnchors(dimBottom, Vector2.zero, new Vector2(1, 0.5f));
            SetAnchors(dimLeft, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f));
            SetAnchors(dimRight, new Vector2(0.5f, 0.5f), new Vector2(1, 0.5f));
        }

        private void Refresh()
        {
            if (step == null) return;
            float sw = Mathf.Max(1, Screen.width), sh = Mathf.Max(1, Screen.height);
            holeScreen = step.target != null ? step.target.GetScreenRect() : null;

            dimGroup.alpha = Mathf.MoveTowards(dimGroup.alpha, LinearDim(step.dim), Time.unscaledDeltaTime * 3f);

            Vector2 h0, h1;
            if (holeScreen.HasValue)
            {
                var r = holeScreen.Value;
                h0 = new Vector2(Mathf.Clamp01(r.xMin / sw), Mathf.Clamp01(r.yMin / sh));
                h1 = new Vector2(Mathf.Clamp01(r.xMax / sw), Mathf.Clamp01(r.yMax / sh));
            }
            else
            {
                h0 = h1 = new Vector2(0.5f, 0.5f);
            }
            SetAnchors(dimTop, new Vector2(0, h1.y), new Vector2(1, 1));
            SetAnchors(dimBottom, new Vector2(0, 0), new Vector2(1, h0.y));
            SetAnchors(dimLeft, new Vector2(0, h0.y), new Vector2(h0.x, h1.y));
            SetAnchors(dimRight, new Vector2(h1.x, h0.y), new Vector2(1, h1.y));

            bool has = holeScreen.HasValue;
            bool round = has && step.target.round;
            if (round)
            {
                // Circles may hang past the screen edge; clamping them would squash them into ovals.
                var r = holeScreen.Value;
                h0 = new Vector2(r.xMin / sw, r.yMin / sh);
                h1 = new Vector2(r.xMax / sw, r.yMax / sh);
            }
            spotlight.gameObject.SetActive(round);
            if (round) SetAnchors(spotlight.rectTransform, h0, h1);

            Vector2 size = root.rect.size;
            Vector2 holeUnits = new Vector2((h1.x - h0.x) * size.x, (h1.y - h0.y) * size.y);
            Vector2 center = new Vector2(((h0.x + h1.x) * 0.5f - 0.5f) * size.x, ((h0.y + h1.y) * 0.5f - 0.5f) * size.y);
            float wave = Mathf.Sin(stepTime * 5f);
            float pulse = 1f + 0.025f * wave;

            // Frame / ring
            frame.gameObject.SetActive(has && !round);
            ring.gameObject.SetActive(round);
            if (has)
            {
                var fr = round ? ring : frame;
                SetAnchors(fr, h0, h1);
                fr.localScale = Vector3.one * pulse;
                (round ? ringImg : frameImg).color = new Color(1f, 1f, 1f, 0.85f + 0.15f * wave);
            }

            // Soft glow
            glow.gameObject.SetActive(has);
            if (has)
            {
                SetAnchors(glow, h0, h1);
                if (round)
                {
                    glowImg.sprite = glowCircleSprite;
                    glowImg.type = Image.Type.Simple;
                    glow.offsetMin = -holeUnits * 0.2835f;
                    glow.offsetMax = holeUnits * 0.2835f;
                }
                else
                {
                    glowImg.sprite = glowRectSprite;
                    glowImg.type = Image.Type.Sliced;
                    glow.offsetMin = new Vector2(-GlowInset, -GlowInset);
                    glow.offsetMax = new Vector2(GlowInset, GlowInset);
                }
                glow.localScale = Vector3.one * pulse;
                glowImg.color = new Color(1f, 0.78f, 0.3f, 0.6f + 0.3f * wave);
            }

            // Corner brackets that bounce outward (UI targets)
            bool br = has && !round;
            float minDim = Mathf.Min(holeUnits.x, holeUnits.y);
            if (br)
            {
                float bs = Mathf.Clamp(minDim * 0.45f, 34f, 62f);
                float off = 16f + 8f * Mathf.Abs(Mathf.Sin(stepTime * 3.2f));
                Vector2[] corner = { new Vector2(h0.x, h1.y), new Vector2(h1.x, h1.y), new Vector2(h1.x, h0.y), new Vector2(h0.x, h0.y) };
                Vector2[] dir = { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, -1) };
                float[] rot = { 0f, -90f, 180f, 90f };
                for (int i = 0; i < 4; i++)
                {
                    var b = brackets[i];
                    b.gameObject.SetActive(true);
                    b.anchorMin = b.anchorMax = corner[i];
                    b.sizeDelta = new Vector2(bs, bs);
                    b.anchoredPosition = dir[i] * off;
                    b.localRotation = Quaternion.Euler(0, 0, rot[i]);
                }
            }
            else foreach (var b in brackets) b.gameObject.SetActive(false);

            // Sparkles running around the edge
            for (int i = 0; i < sparkles.Length; i++)
            {
                var s = sparkles[i];
                s.gameObject.SetActive(has);
                if (!has) continue;
                float u = Mathf.Repeat(stepTime * 0.13f + i / (float)sparkles.Length, 1f);
                Vector2 half = holeUnits * 0.5f;
                Vector2 pos;
                if (round)
                {
                    float a = u * Mathf.PI * 2f;
                    pos = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Mathf.Min(half.x, half.y) * 0.94f;
                }
                else pos = center + PerimeterPoint(half, u);
                s.anchoredPosition = pos;
                float sz = Mathf.Clamp(minDim * 0.5f, 40f, 76f);
                s.sizeDelta = new Vector2(sz, sz);
                float tw = 0.6f + 0.5f * Mathf.Abs(Mathf.Sin(stepTime * 4.5f + i * 1.7f));
                s.localScale = Vector3.one * tw;
                s.localRotation = Quaternion.Euler(0, 0, stepTime * 90f + i * 40f);
            }

            // Dialog placement: away from the target
            float want = step.dialogY;
            if (want < 0f)
            {
                if (!has) want = 0.3f;
                else
                {
                    float cy = (h0.y + h1.y) * 0.5f;
                    want = cy < 0.5f ? Mathf.Clamp(h1.y + 0.17f, 0.6f, 0.7f) : Mathf.Clamp(h0.y - 0.17f, 0.2f, 0.4f);
                }
            }
            if (dialogTargetY <= 0f) dialogTargetY = want;
            dialogTargetY = Mathf.Lerp(dialogTargetY, want, Time.unscaledDeltaTime * 8f);
            dialog.anchorMin = dialog.anchorMax = new Vector2(0.5f, dialogTargetY);
            float cw = root.rect.width;
            dialog.sizeDelta = new Vector2(Mathf.Min(1040f, cw - 50f), DialogH);

            UpdateHand(h0, h1);
        }

        private static Vector2 PerimeterPoint(Vector2 half, float u)
        {
            float w = half.x * 2f, h = half.y * 2f;
            float d = u * 2f * (w + h);
            if (d < w) return new Vector2(-half.x + d, half.y);
            d -= w;
            if (d < h) return new Vector2(half.x, half.y - d);
            d -= h;
            if (d < w) return new Vector2(half.x - d, -half.y);
            d -= w;
            return new Vector2(-half.x, -half.y + d);
        }

        private void UpdateHand(Vector2 h0, Vector2 h1)
        {
            if (step.hand == HandMode.None || !holeScreen.HasValue || typing)
            {
                handGroup.alpha = Mathf.MoveTowards(handGroup.alpha, 0f, Time.unscaledDeltaTime * 6f);
                rippleImg.color = new Color(1, 1, 1, 0);
                return;
            }

            Vector2 size = root.rect.size;
            Vector2 center = new Vector2(((h0.x + h1.x) * 0.5f - 0.5f) * size.x, ((h0.y + h1.y) * 0.5f - 0.5f) * size.y);
            float pointY = (h0.y + h1.y) * 0.5f;
            if (step.handAt != null)
            {
                var hr = step.handAt.GetScreenRect();
                if (hr.HasValue)
                {
                    // Fingertip on the upper part of that card, so the hand doesn't cover what it points at.
                    float px = hr.Value.center.x / Mathf.Max(1, Screen.width);
                    pointY = (hr.Value.yMin + hr.Value.height * 0.62f) / Mathf.Max(1, Screen.height);
                    center = new Vector2((px - 0.5f) * size.x, (pointY - 0.5f) * size.y);
                }
            }
            float t = stepTime;
            float press;

            if (step.hand == HandMode.Tap)
            {
                bool fromAbove = pointY < 0.3f;
                float dir = fromAbove ? -1f : 1f;   // direction the finger points
                hand.localRotation = Quaternion.Euler(0, 0, fromAbove ? 192f : 12f);
                float p = Mathf.Repeat(t, 1.2f) / 1.2f;
                press = p < 0.45f ? Mathf.SmoothStep(0f, 1f, p / 0.45f) : p < 0.6f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (p - 0.6f) / 0.4f);
                float lift = Mathf.Lerp(70f, 0f, press);
                hand.anchoredPosition = center - new Vector2(0, dir * (lift + 10f));
                hand.localScale = Vector3.one * Mathf.Lerp(1.05f, 0.92f, press);
                handGroup.alpha = Mathf.MoveTowards(handGroup.alpha, 1f, Time.unscaledDeltaTime * 5f);

                float rp = p < 0.5f ? -1f : (p - 0.5f) / 0.5f;
                if (rp >= 0f)
                {
                    ripple.anchoredPosition = center;
                    ripple.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.5f, rp);
                    rippleImg.color = new Color(1f, 0.92f, 0.6f, (1f - rp) * 0.9f);
                }
                else rippleImg.color = new Color(1, 1, 1, 0);
            }
            else
            {
                hand.localRotation = Quaternion.Euler(0, 0, 12f);
                Vector2? endScreen = step.dragTo != null ? step.dragTo() : null;
                Vector2 end = endScreen.HasValue
                    ? new Vector2((endScreen.Value.x / Mathf.Max(1, Screen.width) - 0.5f) * size.x, (endScreen.Value.y / Mathf.Max(1, Screen.height) - 0.5f) * size.y)
                    : center + new Vector2(0, size.y * 0.3f);
                float p = Mathf.Repeat(t, 2.2f) / 2.2f;
                float a;
                Vector2 pos;
                if (p < 0.18f) { pos = center; a = p / 0.18f; press = p / 0.18f; }
                else if (p < 0.8f) { float k = Mathf.SmoothStep(0f, 1f, (p - 0.18f) / 0.62f); pos = Vector2.Lerp(center, end, k); a = 1f; press = 1f; }
                else { pos = end; a = 1f - (p - 0.8f) / 0.2f; press = 1f - (p - 0.8f) / 0.2f; }
                hand.localScale = Vector3.one * Mathf.Lerp(1.05f, 0.92f, press);
                hand.anchoredPosition = pos;
                handGroup.alpha = a;

                ripple.anchoredPosition = end;
                float rp = Mathf.Repeat(t * 0.9f, 1f);
                ripple.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.3f, rp);
                rippleImg.color = new Color(0.55f, 1f, 0.55f, (1f - rp) * 0.9f);
            }

            // Shadow falls down-right on screen and tightens as the finger presses.
            Vector3 sOff = Quaternion.Inverse(hand.localRotation) * new Vector3(16f, -20f, 0f) * Mathf.Lerp(1f, 0.35f, press);
            handShadow.offsetMin = handShadow.offsetMax = new Vector2(sOff.x, sOff.y);
        }

        // ───────────────────────────── Input ─────────────────────────────

        private static bool PointerReleased(out Vector2 pos)
        {
            pos = Vector2.zero;
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var tc = Input.GetTouch(i);
                    if (tc.phase == TouchPhase.Ended) { pos = tc.position; return true; }
                }
                return false;
            }
            if (Input.GetMouseButtonUp(0)) { pos = Input.mousePosition; return true; }
            return false;
#elif ENABLE_INPUT_SYSTEM
            var p = UnityEngine.InputSystem.Pointer.current;
            if (p != null && p.press.wasReleasedThisFrame) { pos = p.position.ReadValue(); return true; }
            return false;
#else
            return false;
#endif
        }

        private bool IsOverSkip(Vector2 screenPos)
        {
            if (skipBtn == null || !skipBtn.gameObject.activeInHierarchy) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(skipBtn, screenPos, null);
        }

        // ───────────────────────────── Helpers ─────────────────────────────

        /// <summary>The project renders in Linear space, where black at alpha a only darkens the picture to (1-a)^(1/2.2).
        /// This turns the wanted perceived darkness into the alpha that gives it.</summary>
        private static float LinearDim(float perceived)
        {
            if (QualitySettings.activeColorSpace != ColorSpace.Linear) return perceived;
            return 1f - Mathf.Pow(1f - Mathf.Clamp01(perceived), 2.2f);
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            x -= 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        /// <summary>pixelsPerUnitMultiplier that makes a sliced pill sprite fit the given height.</summary>
        private static float PillPpu(Sprite s, float height)
        {
            if (s == null || height <= 0f) return 1f;
            return Mathf.Max(0.1f, s.rect.height / height);
        }

        private static string Short(string t)
        {
            if (string.IsNullOrEmpty(t)) return "";
            t = System.Text.RegularExpressions.Regex.Replace(t, "<.*?>", "");
            return t.Length > 40 ? t.Substring(0, 40) + "..." : t;
        }

        private void UpdateReference()
        {
            if (scaler == null) return;
            var want = Screen.height >= Screen.width ? new Vector2(1080, 1920) : new Vector2(1920, 1080);
            if (scaler.referenceResolution != want) scaler.referenceResolution = want;
        }

        private static void SetAnchors(RectTransform r, Vector2 min, Vector2 max)
        {
            r.anchorMin = min;
            r.anchorMax = max;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        private RectTransform DimPiece(RectTransform parent, string name)
        {
            var r = Box(parent, name, Vector2.zero, Vector2.one);
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = whiteSprite;
            img.color = Color.black;
            return r;
        }

        private static RectTransform Box(RectTransform parent, string name, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var r = go.GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.anchorMin = aMin;
            r.anchorMax = aMax;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            return r;
        }

        private TextMeshProUGUI Text(RectTransform parent, string name, string text, float size, Color color, TextAlignmentOptions align, bool outline)
        {
            var r = Box(parent, name, Vector2.zero, Vector2.one);
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset f = null;
            if (art != null) f = outline && art.outlineFont != null ? art.outlineFont : art.font;
            if (f != null) t.font = f;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.richText = true;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        public static void TutorialSound(string id)
        {
            try { KoSAudio.Play2D(id, 0.8f); } catch { }
        }

        // ───────────────────────────── Generated sprites ─────────────────────────────

        private static void MakeSprites()
        {
            if (roundSprite != null) return;
            whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            roundSprite = RoundRect(96, 30, 5, new Color(0.13f, 0.2f, 0.42f), new Color(0.06f, 0.1f, 0.24f), new Color(0.98f, 0.8f, 0.3f), false);
            ringRoundSprite = RoundRect(96, 30, 7, Color.clear, Color.clear, Color.white, true);
            frameSprite = GoldFrame(96, 26, 11);
            circleRingSprite = Circle(256, 0.44f, 0.5f, true);
            goldRingSprite = GoldRing(256, 0.425f);
            spotSprite = Circle(256, 0.0f, 0.47f, false);
            softDotSprite = Circle(64, 0f, 0.5f, false);
            glowRectSprite = SoftRect(128, GlowInset, 26f, 13f, true);
            shadowSprite = SoftRect(128, 32f, 30f, 12f, false);
            glowCircleSprite = GlowCircle(256, 0.3f, 0.05f);
            bracketSprite = Bracket(64, 13, 46, 3);
            raysSprite = Rays(256, 14);
            handFallback = Resources.Load<Sprite>("Tutorial/kos_tutorial_hand");
            if (handFallback == null)
            {
                var tex = Resources.Load<Texture2D>("Tutorial/kos_tutorial_hand");
                if (tex != null) handFallback = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.45f, 0.95f));
                else handFallback = Circle(64, 0f, 0.5f, false);
            }
        }

        private static Texture2D NewTex(int w, int h) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        private static float RoundRectDist(float x, float y, float half, float inset, float radius)
        {
            float qx = Mathf.Abs(x - half) - (half - inset - radius);
            float qy = Mathf.Abs(y - half) - (half - inset - radius);
            return new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
        }

        /// <summary>9-sliceable rounded rectangle; ringOnly draws just the border.</summary>
        private static Sprite RoundRect(int size, int radius, int border, Color top, Color bottom, Color edge, bool ringOnly)
        {
            var tex = NewTex(size, size);
            var px = new Color[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float outside = RoundRectDist(x + 0.5f, y + 0.5f, half, 0f, radius);
                    float aOuter = Mathf.Clamp01(0.5f - outside);
                    float aInner = Mathf.Clamp01(0.5f - (outside + border));
                    Color fill = Color.Lerp(bottom, top, y / (float)size);
                    Color c;
                    if (ringOnly) { c = edge; c.a = aOuter - aInner; }
                    else { c = Color.Lerp(edge, fill, aInner); c.a = aOuter; }
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            float b = radius + border + 2;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        /// <summary>Gold bevelled frame with a dark outline, hollow inside (9-sliced).</summary>
        private static Sprite GoldFrame(int size, int radius, int border)
        {
            var tex = NewTex(size, size);
            var px = new Color[size * size];
            float half = size * 0.5f;
            Color dark = new Color(0.3f, 0.14f, 0.02f, 1f);
            Color goldLo = new Color(0.86f, 0.52f, 0.1f, 1f);
            Color goldHi = new Color(1f, 0.95f, 0.6f, 1f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float sd = RoundRectDist(x + 0.5f, y + 0.5f, half, 0f, radius);
                    float depth = -sd;
                    float a = Mathf.Clamp01(0.5f - sd) * Mathf.Clamp01(border - depth + 0.5f);
                    Color c;
                    if (depth < 2.5f || depth > border - 2f) c = dark;
                    else
                    {
                        float band = (depth - 2.5f) / Mathf.Max(1f, border - 4.5f);           // 0 outer .. 1 inner
                        float bevel = 1f - Mathf.Abs(band - 0.35f) * 1.4f;                     // highlight line
                        c = Color.Lerp(goldLo, goldHi, Mathf.Clamp01(y / (float)size * 0.7f + bevel * 0.45f));
                    }
                    c.a = a;
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            float b = radius + border + 2;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        /// <summary>Gold ring with dark edges, from inner (radius fraction) to the sprite's edge.</summary>
        private static Sprite GoldRing(int size, float inner)
        {
            var tex = NewTex(size, size);
            var px = new Color[size * size];
            float c0 = size * 0.5f;
            float aa = 1.5f / size, edge = 2.5f / size;
            Color dark = new Color(0.3f, 0.14f, 0.02f, 1f);
            Color goldLo = new Color(0.86f, 0.52f, 0.1f, 1f);
            Color goldHi = new Color(1f, 0.95f, 0.6f, 1f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x + 0.5f - c0, y + 0.5f - c0).magnitude / size;
                    float a = Mathf.Clamp01((d - inner) / aa) * Mathf.Clamp01((0.5f - d) / aa);
                    Color c = (d - inner < edge || 0.5f - d < edge) ? dark : Color.Lerp(goldLo, goldHi, y / (float)size);
                    c.a = a;
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>Soft blurred rounded rect. hollow: only the glow outside the shape (for highlight glows); else a filled shadow.</summary>
        private static Sprite SoftRect(int size, float inset, float radius, float sigma, bool hollow)
        {
            var tex = NewTex(size, size);
            var px = new Color[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = RoundRectDist(x + 0.5f, y + 0.5f, half, inset, radius);
                    float a = d > 0f ? Mathf.Exp(-(d * d) / (2f * sigma * sigma)) : (hollow ? Mathf.Clamp01(1f + d / 3f) : 1f);
                    px[y * size + x] = new Color(1, 1, 1, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            float b = Mathf.Min(half - 1f, inset + radius + 2f);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        /// <summary>Glow outside a circle of radius r0 (fractions of the sprite size), clear inside.</summary>
        private static Sprite GlowCircle(int size, float r0, float sigma)
        {
            var tex = NewTex(size, size);
            var px = new Color[size * size];
            float c0 = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x + 0.5f - c0, y + 0.5f - c0).magnitude / size;
                    float a = d < r0 ? Mathf.Clamp01(1f - (r0 - d) / 0.012f) : Mathf.Exp(-((d - r0) * (d - r0)) / (2f * sigma * sigma));
                    a *= Mathf.Clamp01((0.5f - d) / 0.03f);
                    px[y * size + x] = new Color(1, 1, 1, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>Gold L-shaped corner bracket (top-left corner) with a dark outline.</summary>
        private static Sprite Bracket(int size, int thick, int len, int outline)
        {
            var tex = NewTex(size, size);
            var px = new Color[size * size];
            Color dark = new Color(0.3f, 0.14f, 0.02f, 1f);
            Color goldLo = new Color(0.9f, 0.58f, 0.12f, 1f);
            Color goldHi = new Color(1f, 0.95f, 0.6f, 1f);
            int m = outline + 1;
            bool In(int x, int y, int grow)
            {
                int top = size - 1 - m;
                bool vertical = x >= m - grow && x < m + thick + grow && y <= top + grow && y > top - len - grow;
                bool horizontal = y <= top + grow && y > top - thick - grow && x >= m - grow && x < m + len + grow;
                return vertical || horizontal;
            }
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color c = new Color(0, 0, 0, 0);
                    if (In(x, y, 0)) c = Color.Lerp(goldLo, goldHi, y / (float)size);
                    else if (In(x, y, outline)) c = dark;
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0f, 1f));
        }

        /// <summary>Soft light rays fanning out from the centre (for banners).</summary>
        private static Sprite Rays(int size, int count)
        {
            var tex = NewTex(size, size);
            var px = new Color[size * size];
            float c0 = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 v = new Vector2(x + 0.5f - c0, y + 0.5f - c0);
                    float d = v.magnitude / size;
                    float ang = Mathf.Atan2(v.y, v.x);
                    float ray = Mathf.Pow(Mathf.Clamp01(Mathf.Cos(ang * count) * 0.5f + 0.5f), 3f);
                    float fall = Mathf.Pow(Mathf.Clamp01(1f - d / 0.5f), 1.3f);
                    float core = Mathf.Exp(-(d * d) / (2f * 0.08f * 0.08f)) * 0.6f;
                    px[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(ray * fall + core));
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>ring=true: ring between inner and outer (radius fractions). ring=false and inner=0: inverted spotlight (dark outside the circle).</summary>
        private static Sprite Circle(int size, float inner, float outer, bool ring)
        {
            var tex = NewTex(size, size);
            var px = new Color[size * size];
            float c = size * 0.5f;
            float aa = 1.5f / size;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x + 0.5f - c, y + 0.5f - c).magnitude / size;
                    float a;
                    if (ring) a = Mathf.Clamp01((d - inner) / aa) * Mathf.Clamp01((outer - d) / aa);
                    else if (inner <= 0f && outer < 0.5f) a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - outer + 0.04f) / 0.05f)); // spotlight
                    else a = Mathf.Clamp01((outer - d) / aa);
                    px[y * size + x] = new Color(1, 1, 1, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
