using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Shows a short flourish on the battlefield whenever the territory border actually moves.
///
/// The border here is the dynamic frontline owned by <see cref="BattleSceneSetup"/>: its
/// DisplayedRatio slides toward wherever the two armies have met, so the local player's
/// deploy zone grows when their troops push forward and shrinks when they are pushed back.
/// Because BattleSceneSetup hides its zone visuals unless the player is aiming a powerup,
/// that change is normally invisible — this component is the feedback for it.
///
/// What appears, only while the border is on the move:
///  - a glowing strip of ground over exactly the land claimed in this push, brightest at the new edge,
///  - a bright bar + soft glow sitting on the new border line,
///  - chevrons sliding across the claimed ground in the direction the border is travelling,
///  - a small ribbon label that floats over the border saying GROUND GAINED / GROUND LOST and the amount.
///
/// It is deliberately quiet: it needs a real shift (a few percent of the battlefield) to trigger,
/// fades out as soon as the border settles, is capped at a few seconds, and then goes on cooldown.
/// Matches the banner look in BattlePacingDirector / TutorialOverlay (UIPACK ribbon, TiltWarp font)
/// and falls back to generated shapes when that art is missing.
///
/// Creates itself in any scene that has a BattleSceneSetup, so there is nothing to wire up.
/// </summary>
[DisallowMultipleComponent]
public class TerritoryShiftIndicator : MonoBehaviour
{
    public static TerritoryShiftIndicator Instance { get; private set; }

    [Header("When it shows")]
    [Tooltip("The border must travel this much of the battlefield length in one push before anything appears.")]
    [Range(0.005f, 0.25f)] public float minShiftFraction = 0.06f;
    [Tooltip("Border speed (fraction of the battlefield per second) that counts as moving.")]
    public float moveSpeedThreshold = 0.012f;
    [Tooltip("At or below this speed the border counts as settled.")]
    public float stallSpeedThreshold = 0.0045f;
    [Tooltip("Seconds the border may sit still before the indicator fades out.")]
    public float stallBeforeHide = 0.55f;
    [Tooltip("Hard cap on a single showing, so it can never sit on screen.")]
    public float maxVisibleSeconds = 4f;
    [Tooltip("Quiet time after a showing before another one may start.")]
    public float cooldownSeconds = 2.5f;
    [Tooltip("Ignored window right after the match starts, when the first deploys swing the border hard.")]
    public float startupGrace = 2.5f;
    public float fadeOutSeconds = 0.45f;

    [Header("What it shows")]
    public bool showGroundSweep = true;
    public bool showFloatingLabel = true;
    public bool playSound = true;
    [Tooltip("How far back from the new border the chevrons travel, as a fraction of the battlefield length.")]
    [Range(0.01f, 0.2f)] public float chevronTravel = 0.05f;
    [Tooltip("Height above the battlefield's own ground visuals.")]
    public float extraGroundLift = 0.02f;

    // ───────────────────────────── Bootstrap ─────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded += (s, m) => TryCreate();
        TryCreate();
    }

    private static void TryCreate()
    {
        if (FindFirstObjectByType<TerritoryShiftIndicator>() != null) return;
        if (FindFirstObjectByType<BattleSceneSetup>() == null) return;
        new GameObject("[TerritoryShiftIndicator]").AddComponent<TerritoryShiftIndicator>();
    }

    // ───────────────────────────── State ─────────────────────────────

    private enum Phase { Idle, Active, FadingOut }

    private BattleSceneSetup arena;
    private GameManager game;
    private Camera cam;

    private Phase phase = Phase.Idle;
    private float lastRatio = -1f;
    private float velocity;            // smoothed d(ratio)/dt
    private float candidateAnchor;     // ratio where the current run of movement began
    private int candidateDir;          // +1 / -1 in ratio space, 0 = no run
    private float anchorRatio;         // ratio where the showing started
    private float liveRatio;           // ratio right now
    private float visibleTime, stallTime, cooldown, graceTime, fadeTime;
    private bool gaining;              // true: local player is claiming ground
    private float animTime;            // keeps the chevrons / pulses running

    // World visuals (parented to the arena so local Z is player -> enemy, like UpdateLayout).
    private Transform worldRoot, stripQuad, glowQuad, edgeBar;
    private readonly List<Transform> chevrons = new List<Transform>();
    private readonly List<Material> chevronMats = new List<Material>();
    private Material stripMat, glowMat, edgeMat;

    // Screen visuals.
    private Canvas canvas;
    private RectTransform labelRoot, pill, arrowRect;
    private CanvasGroup labelGroup;
    private Image pillImg, arrowImg;
    private TextMeshProUGUI titleText, deltaText;

    private static Sprite stripSprite, glowSprite, chevronSprite, whiteSprite;
    private static TMP_FontAsset uiFont, uiOutlineFont;
    private static readonly Dictionary<string, Sprite> ribbonCache = new Dictionary<string, Sprite>();
    private static bool artLoaded;

    private const float PillW = 520f, PillH = 168f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (worldRoot != null) Destroy(worldRoot.gameObject);
        if (canvas != null) Destroy(canvas.gameObject);
        DestroyMat(ref stripMat);
        DestroyMat(ref glowMat);
        DestroyMat(ref edgeMat);
        for (int i = 0; i < chevronMats.Count; i++)
        {
            var m = chevronMats[i];
            if (m != null) Destroy(m);
        }
        chevronMats.Clear();
    }

    private static void DestroyMat(ref Material m)
    {
        if (m != null) Destroy(m);
        m = null;
    }

    // ───────────────────────────── Drive ─────────────────────────────

    private void Update()
    {
        if (arena == null)
        {
            arena = BattleSceneSetup.Instance != null ? BattleSceneSetup.Instance : FindFirstObjectByType<BattleSceneSetup>();
            if (arena == null) return;
            lastRatio = arena.DisplayedRatio;
            candidateAnchor = lastRatio;
            Build();   // built once, up front, so the first border push costs no hitch
        }

        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        animTime += dt;

        // Only during live play. Any other time the indicator must not be on screen.
        if (game == null) game = FindFirstObjectByType<GameManager>();
        bool matchRunning = MatchReadyScreen.IsMatchStarted && (game == null || !game.IsMatchOver);

        if (!matchRunning)
        {
            graceTime = 0f;
            if (phase != Phase.Idle) BeginFadeOut();
            lastRatio = arena.DisplayedRatio;
            candidateAnchor = lastRatio;
            candidateDir = 0;
        }
        else if (graceTime < startupGrace)
        {
            graceTime += dt;
            lastRatio = arena.DisplayedRatio;
            candidateAnchor = lastRatio;
            candidateDir = 0;
        }

        liveRatio = arena.DisplayedRatio;
        float raw = dt > 0f ? (liveRatio - lastRatio) / dt : 0f;
        lastRatio = liveRatio;
        velocity = Mathf.Lerp(velocity, raw, 1f - Mathf.Exp(-dt * 9f));

        if (cooldown > 0f) cooldown -= dt;

        switch (phase)
        {
            case Phase.Idle: TickIdle(dt, matchRunning && graceTime >= startupGrace); break;
            case Phase.Active: TickActive(dt); break;
            case Phase.FadingOut: TickFadeOut(dt); break;
        }
    }

    private void TickIdle(float dt, bool armed)
    {
        if (!armed) return;

        int dir = velocity > stallSpeedThreshold ? 1 : (velocity < -stallSpeedThreshold ? -1 : 0);

        // A new run of movement, or a stall, re-anchors where this push is measured from.
        if (dir == 0 || dir != candidateDir)
        {
            candidateDir = dir;
            candidateAnchor = liveRatio;
            if (dir == 0) return;
        }

        if (cooldown > 0f) return;

        // A brisk push shows at the plain threshold; a slow creep has to cover more ground first.
        float shift = Mathf.Abs(liveRatio - candidateAnchor);
        float needed = Mathf.Abs(velocity) >= moveSpeedThreshold ? minShiftFraction : minShiftFraction * 1.8f;
        if (shift < needed) return;

        Begin(candidateAnchor, dir);
    }

    private void Begin(float fromRatio, int dirInRatio)
    {
        // Ratio is always team 0's share, so a rising ratio is a gain for team 0 and a loss for team 1.
        gaining = GameModeManager.LocalTeamId == 1 ? dirInRatio < 0 : dirInRatio > 0;

        anchorRatio = fromRatio;
        phase = Phase.Active;
        visibleTime = 0f;
        stallTime = 0f;
        fadeTime = 0f;
        animTime = 0f;

        Build();
        ApplyStyle();
        if (worldRoot != null) worldRoot.gameObject.SetActive(showGroundSweep);
        if (labelRoot != null) labelRoot.gameObject.SetActive(showFloatingLabel);

        if (playSound)
        {
            if (gaining) KoSAudio.Play2D("reward_pop", 0.7f);
            else KoSAudio.Play2D("rubble", 0.55f);
        }
    }

    private void TickActive(float dt)
    {
        visibleTime += dt;

        int dir = velocity > stallSpeedThreshold ? 1 : (velocity < -stallSpeedThreshold ? -1 : 0);
        bool stillGaining = GameModeManager.LocalTeamId == 1 ? dir < 0 : dir > 0;

        if (dir == 0)
        {
            stallTime += dt;
        }
        else if (stillGaining != gaining)
        {
            // The border turned around: this push is over, and the reversal starts its own run.
            candidateDir = dir;
            candidateAnchor = liveRatio;
            BeginFadeOut();
            return;
        }
        else
        {
            stallTime = 0f;
        }

        if (stallTime >= stallBeforeHide || visibleTime >= maxVisibleSeconds)
        {
            BeginFadeOut();
            return;
        }

        Layout(1f);
    }

    private void BeginFadeOut()
    {
        if (phase == Phase.Idle) return;
        phase = Phase.FadingOut;
        fadeTime = 0f;
        cooldown = cooldownSeconds;
        candidateAnchor = liveRatio;
    }

    private void TickFadeOut(float dt)
    {
        fadeTime += dt;
        float k = fadeOutSeconds > 0f ? Mathf.Clamp01(fadeTime / fadeOutSeconds) : 1f;
        if (k >= 1f)
        {
            phase = Phase.Idle;
            Hide();
            return;
        }
        Layout(1f - k * k);
    }

    private void OnDisable()
    {
        phase = Phase.Idle;
        Hide();
    }

    private void Hide()
    {
        if (worldRoot != null) worldRoot.gameObject.SetActive(false);
        if (labelRoot != null) labelRoot.gameObject.SetActive(false);
    }

    // ───────────────────────────── Layout ─────────────────────────────

    /// <summary>Places every piece for the current border position. <paramref name="alpha"/> drives the fade out.</summary>
    private void Layout(float alpha)
    {
        if (arena == null) return;

        float len = arena.arenaLength;
        float w = arena.arenaWidth;
        float zMin = -len * 0.5f;
        float y = arena.groundYOffset + extraGroundLift;

        float anchorZ = zMin + len * anchorRatio;
        float liveZ = zMin + len * liveRatio;

        float span = liveZ - anchorZ;                  // signed, in local Z
        float spanAbs = Mathf.Max(0.02f, Mathf.Abs(span));
        float toLive = span >= 0f ? 1f : -1f;          // local Z direction the border is travelling
        float centerZ = (anchorZ + liveZ) * 0.5f;

        float intro = Mathf.Clamp01(visibleTime / 0.28f);
        float ease = 1f - Mathf.Pow(1f - intro, 3f);
        float a = alpha * ease;

        // Everything on the ground is sized against the battlefield, which can be any length.
        float unit = Mathf.Max(4f, len);

        if (showGroundSweep && worldRoot != null)
        {
            // Claimed strip: the texture is brightest at V = 1, so flip Y to keep that at the live edge.
            // It covers at most a fixed distance back from the border rather than the whole claim, so
            // the wash looks the same whatever the size of the push and always fades out to nothing at
            // its far end. Stretching the gradient over the full claim instead made a short push render
            // as a solid slab, and clipping the gradient left a hard edge where it was cut off.
            if (stripQuad != null)
            {
                float fall = Mathf.Max(2f, unit * 0.12f);
                float showLen = Mathf.Min(spanAbs, fall);
                stripQuad.localPosition = new Vector3(0f, y, liveZ - toLive * showLen * 0.5f);
                stripQuad.localScale = new Vector3(w, showLen * toLive, 1f);
                SetMatAlpha(stripMat, 0.7f * a);
            }

            // Soft glow hugging the new border line.
            if (glowQuad != null)
            {
                float pulse = 0.78f + 0.22f * Mathf.Sin(animTime * 7.5f);
                glowQuad.localPosition = new Vector3(0f, y + 0.01f, liveZ);
                glowQuad.localScale = new Vector3(w * 1.04f, Mathf.Max(1.8f, unit * 0.025f), 1f);
                SetMatAlpha(glowMat, 0.42f * a * pulse);
            }

            // Bright bar on the new border line, heavier than the arena's own frontline bar.
            if (edgeBar != null)
            {
                float t = Mathf.Max(arena.borderThickness * 1.9f, unit * 0.007f);
                float grow = Mathf.LerpUnclamped(0.2f, 1f, EaseOutBack(intro, 2.1f));
                edgeBar.localPosition = new Vector3(0f, y + 0.02f, liveZ);
                edgeBar.localScale = new Vector3((w + t) * Mathf.Clamp01(grow), t, t);
                SetMatAlpha(edgeMat, a);
            }

            // One centred column of chevrons chasing the border, evenly spaced so they read as a flow
            // rather than a blanket. Side-by-side lanes just merge into a bright smear.
            float chevL = Mathf.Max(1f, unit * 0.018f);
            float chevW = Mathf.Max(1f, w * 0.26f);
            float travel = Mathf.Max(Mathf.Min(unit * chevronTravel, spanAbs), chevL * 3f);
            int n = Mathf.Max(1, chevrons.Count);
            for (int i = 0; i < chevrons.Count; i++)
            {
                var c = chevrons[i];
                if (c == null) continue;
                float p = Mathf.Repeat(animTime / 1.1f + i / (float)n, 1f);
                float z = liveZ - toLive * travel * (1f - p);
                float fade = Mathf.Min(Mathf.Clamp01(p / 0.18f), Mathf.Clamp01((1f - p) / 0.3f));
                c.localPosition = new Vector3(0f, y + 0.03f, z);
                c.localScale = new Vector3(chevW, chevL * toLive, 1f);
                float ca = 0.5f * a * fade;
                var mr = c.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = ca > 0.01f;
                if (i < chevronMats.Count) SetMatAlpha(chevronMats[i], ca);
            }
        }

        if (showFloatingLabel && labelRoot != null) LayoutLabel(a, liveZ);
    }

    /// <summary>Floats the ribbon label over the middle of the border line.</summary>
    private void LayoutLabel(float a, float liveZ)
    {
        if (cam == null || !cam.isActiveAndEnabled)
            cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();

        if (cam == null || canvas == null)
        {
            labelGroup.alpha = 0f;
            return;
        }

        Vector3 world = arena.transform.TransformPoint(new Vector3(0f, arena.groundYOffset, liveZ));
        Vector3 vp = cam.WorldToViewportPoint(world);
        if (vp.z <= 0.2f)
        {
            labelGroup.alpha = 0f;
            return;
        }

        var canvasRect = (RectTransform)canvas.transform;
        Vector2 size = canvasRect.rect.size;

        // Keep the label clear of the border line itself and inside the safe part of the screen.
        float x = Mathf.Clamp(vp.x, 0.26f, 0.74f);
        float yv = Mathf.Clamp(vp.y + 0.075f, 0.22f, 0.80f);

        float intro = Mathf.Clamp01(visibleTime / 0.3f);
        float pop = Mathf.LerpUnclamped(0.55f, 1f, EaseOutBack(intro, 2.4f));
        float bob = Mathf.Sin(animTime * 3.4f) * 7f;

        pill.anchoredPosition = new Vector2(0f, bob);
        pill.localScale = Vector3.one * pop * (1f + 0.04f * Mathf.Sin(animTime * 8f));
        labelRoot.anchoredPosition = new Vector2((x - 0.5f) * size.x, (yv - 0.5f) * size.y);
        labelGroup.alpha = a;

        // The arrow keeps nudging in the direction of the push.
        float slide = Mathf.Repeat(animTime * 1.6f, 1f);
        arrowRect.anchoredPosition = new Vector2(-PillW * 0.34f, (gaining ? 1f : -1f) * (6f + 10f * Mathf.Sin(slide * Mathf.PI * 2f)));

        // Amount of the battlefield claimed (or lost) in this push.
        int pct = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(liveRatio - anchorRatio) * 100f));
        deltaText.text = (gaining ? "+" : "-") + pct + "%";
    }

    private static void SetMatAlpha(Material m, float a)
    {
        if (m == null) return;
        Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
        c.a = Mathf.Clamp01(a);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    // ───────────────────────────── Style ─────────────────────────────

    private void ApplyStyle()
    {
        // The local player's own territory is always drawn blue by BattleSceneSetup, the enemy's red.
        Color accent = gaining ? arena.playerBorderColor : arena.enemyBorderColor;
        Color bright = Color.Lerp(accent, Color.white, 0.35f);

        SetBase(stripMat, accent, 0.7f);
        SetBase(glowMat, bright, 0.5f);
        SetBase(edgeMat, bright, 1f);
        // Light enough to read against the claimed ground under them, not so light they blow it out.
        Color chevColor = Color.Lerp(accent, Color.white, 0.45f);
        for (int i = 0; i < chevronMats.Count; i++) SetBase(chevronMats[i], chevColor, 0.5f);

        if (titleText != null)
        {
            titleText.text = gaining ? "GROUND GAINED" : "GROUND LOST";
            Color bottom = Color.Lerp(bright, Color.white, 0.5f);
            titleText.enableVertexGradient = true;
            titleText.colorGradient = new VertexGradient(Color.white, Color.white, bottom, bottom);
        }

        if (deltaText != null)
        {
            deltaText.enableVertexGradient = true;
            Color lo = gaining ? new Color(0.72f, 0.95f, 1f) : new Color(1f, 0.78f, 0.7f);
            deltaText.colorGradient = new VertexGradient(Color.white, Color.white, lo, lo);
        }

        if (arrowImg != null)
        {
            arrowImg.color = Color.Lerp(bright, Color.white, 0.4f);
            arrowRect.localRotation = Quaternion.Euler(0f, 0f, gaining ? 0f : 180f);
        }

        if (pillImg != null)
        {
            var rib = LoadRibbon(gaining ? "Label_Ribbon01_Sky" : "Label_Ribbon01_Red");
            if (rib != null)
            {
                pillImg.sprite = rib;
                pillImg.type = Image.Type.Sliced;
                pillImg.pixelsPerUnitMultiplier = Mathf.Max(0.01f, rib.rect.height / PillH);
                pillImg.color = Color.white;
            }
            else
            {
                pillImg.sprite = whiteSprite;
                pillImg.type = Image.Type.Simple;
                pillImg.color = Color.Lerp(accent, Color.black, 0.55f);
            }
        }
    }

    private static void SetBase(Material m, Color c, float a)
    {
        if (m == null) return;
        c.a = a;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * 1.3f);
    }

    // ───────────────────────────── Build ─────────────────────────────

    private void Build()
    {
        // Everything has to be present, not just the root. An editor domain reload (a script change
        // during play) keeps the GameObjects but empties these lists, which would otherwise leave the
        // chevrons orphaned and frozen for the rest of the session.
        bool intact = worldRoot != null && stripQuad != null && glowQuad != null && edgeBar != null
                      && canvas != null && labelRoot != null
                      && chevrons.Count > 0 && chevronMats.Count == chevrons.Count;
        if (intact) return;

        if (worldRoot != null) Destroy(worldRoot.gameObject);
        if (canvas != null) Destroy(canvas.gameObject);
        worldRoot = null;
        canvas = null;
        chevrons.Clear();
        chevronMats.Clear();

        MakeSprites();
        LoadArt();

        worldRoot = new GameObject("Territory Shift Visuals").transform;
        worldRoot.SetParent(arena.transform, false);
        worldRoot.localPosition = Vector3.zero;
        worldRoot.localRotation = Quaternion.identity;

        stripMat = GroundMaterial(stripSprite != null ? stripSprite.texture : null, "TerritoryStripMat", 3002);
        glowMat = GroundMaterial(glowSprite != null ? glowSprite.texture : null, "TerritoryGlowMat", 3003);
        edgeMat = GroundMaterial(null, "TerritoryEdgeMat", 3004, true);

        stripQuad = MakeQuad("Claimed Ground", stripMat);
        glowQuad = MakeQuad("Border Glow", glowMat);
        edgeBar = MakeBar("Border Edge", edgeMat);

        // One material each, so they can fade independently as they travel.
        Texture chevTex = chevronSprite != null ? chevronSprite.texture : null;
        for (int i = 0; i < 3; i++)
        {
            var m = GroundMaterial(chevTex, "TerritoryChevronMat" + i, 3005);
            chevronMats.Add(m);
            chevrons.Add(MakeQuad("Push Chevron " + i, m));
        }

        BuildLabel();

        worldRoot.gameObject.SetActive(false);
    }

    private Transform MakeQuad(string name, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        go.transform.SetParent(worldRoot, false);
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // lie flat on XZ; local +Y maps to +Z
        Strip(go, mat);
        return go.transform;
    }

    private Transform MakeBar(string name, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(worldRoot, false);
        go.transform.localRotation = Quaternion.identity;
        Strip(go, mat);
        return go.transform;
    }

    private static void Strip(GameObject go, Material mat)
    {
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    private void BuildLabel()
    {
        var canvasGo = new GameObject("TerritoryShiftCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 430;                      // under the pacing banners (500)
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        labelRoot = Centered((RectTransform)canvasGo.transform, "Label", new Vector2(0.5f, 0.5f), new Vector2(PillW, PillH));
        labelGroup = labelRoot.gameObject.AddComponent<CanvasGroup>();
        labelGroup.blocksRaycasts = false;
        labelGroup.interactable = false;

        pill = Centered(labelRoot, "Pill", new Vector2(0.5f, 0.5f), new Vector2(PillW, PillH));

        var shadow = Centered(pill, "Shadow", new Vector2(0.5f, 0.5f), new Vector2(PillW * 0.86f, PillH * 0.7f));
        shadow.anchoredPosition = new Vector2(0f, -14f);
        AddImage(shadow, glowSprite, new Color(0f, 0f, 0f, 0.42f));

        pillImg = AddImage(Box(pill, "Art", Vector2.zero, Vector2.one), whiteSprite, Color.white);

        arrowRect = Centered(pill, "Arrow", new Vector2(0.5f, 0.5f), new Vector2(84f, 84f));
        arrowImg = AddImage(arrowRect, chevronSprite, Color.white);
        arrowImg.preserveAspect = true;

        // The UIPACK ribbon's readable face is its upper portion; the folds sit below.
        var textArea = Box(pill, "Text", new Vector2(0.2f, 0.26f), new Vector2(0.95f, 0.95f));
        titleText = MakeText(Box(textArea, "Title", new Vector2(0f, 0.46f), new Vector2(1f, 1f)), 42f);
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 22f;
        titleText.fontSizeMax = 42f;
        titleText.characterSpacing = 2f;

        deltaText = MakeText(Box(textArea, "Delta", new Vector2(0f, 0f), new Vector2(1f, 0.5f)), 64f);
        deltaText.enableAutoSizing = true;
        deltaText.fontSizeMin = 30f;
        deltaText.fontSizeMax = 64f;

        labelRoot.gameObject.SetActive(false);
    }

    private TextMeshProUGUI MakeText(RectTransform parent, float size)
    {
        var t = parent.gameObject.AddComponent<TextMeshProUGUI>();
        var f = uiOutlineFont != null ? uiOutlineFont : uiFont;
        if (f != null) t.font = f;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        t.color = Color.white;
        if (f == null)
        {
            t.fontStyle = FontStyles.Bold;
            t.outlineWidth = 0.2f;
            t.outlineColor = new Color32(18, 12, 30, 255);
        }
        return t;
    }

    private static void LoadArt()
    {
        if (artLoaded) return;
        artLoaded = true;
        var art = Resources.Load<Siege.Tutorial.KoSTutorialArt>("Tutorial/KoSTutorialArt");
        if (art != null)
        {
            uiFont = art.font;
            uiOutlineFont = art.outlineFont != null ? art.outlineFont : art.font;
        }
    }

    private static Sprite LoadRibbon(string name)
    {
        if (ribbonCache.TryGetValue(name, out var s)) return s;
        s = Resources.Load<Sprite>("BattleBanners/" + name);
        ribbonCache[name] = s;
        return s;
    }

    /// <summary>Transparent, unlit, always-on-top-of-the-ground material, same setup as BattleSceneSetup's.</summary>
    private static Material GroundMaterial(Texture tex, string matName, int queue, bool emissive = false)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");

        var mat = new Material(shader) { name = matName };

        if (shader.name.StartsWith("Universal Render Pipeline"))
        {
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);   // both sides, so a negative scale still draws
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }
        else if (shader.name == "Standard")
        {
            mat.SetFloat("_Mode", 3f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_ALPHABLEND_ON");
        }

        if (tex != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        }

        if (emissive)
        {
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.white);
        }

        mat.renderQueue = queue;
        return mat;
    }

    // ───────────────────────────── UI helpers ─────────────────────────────

    private static RectTransform Box(RectTransform parent, string name, Vector2 aMin, Vector2 aMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = aMin; r.anchorMax = aMax;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        return r;
    }

    private static RectTransform Centered(RectTransform parent, string name, Vector2 anchor, Vector2 size)
    {
        var r = Box(parent, name, anchor, anchor);
        r.sizeDelta = size;
        r.anchoredPosition = Vector2.zero;
        return r;
    }

    private static Image AddImage(RectTransform r, Sprite sprite, Color color)
    {
        var img = r.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static float EaseOutBack(float x, float c1)
    {
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    // ───────────────────────────── Generated sprites ─────────────────────────────

    private static void MakeSprites()
    {
        if (whiteSprite != null) return;
        whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        stripSprite = ClaimStrip(64);
        glowSprite = SoftBand(64);
        chevronSprite = Chevron(96);
    }

    private static Texture2D NewTex(int w, int h) =>
        new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

    private static Sprite Finish(Texture2D tex, Color[] px)
    {
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
    }

    /// <summary>Ground tint that fades away from the live border edge (V = 1) and feathers at the sides.</summary>
    private static Sprite ClaimStrip(int size)
    {
        var tex = NewTex(size, size);
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            float v = (y + 0.5f) / size;
            float along = Mathf.Pow(v, 2.1f);                                  // brightest at the new border
            float lip = Mathf.Exp(-Mathf.Pow((1f - v) * size / 3.5f, 2f));      // crisp lip right on the edge
            for (int x = 0; x < size; x++)
            {
                float u = Mathf.Abs((x + 0.5f) / size * 2f - 1f);
                // Wide side feather, so the claimed ground reads as a glow rather than a painted rectangle.
                float sides = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.55f, 1f, u));
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((along * 0.85f + lip) * sides));
            }
        }
        return Finish(tex, px);
    }

    /// <summary>Glow line: a soft horizontal band centred on V = 0.5, feathered at the sides.</summary>
    private static Sprite SoftBand(int size)
    {
        var tex = NewTex(size, size);
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            float v = (y + 0.5f) / size * 2f - 1f;
            float band = Mathf.Exp(-v * v * 7f);
            for (int x = 0; x < size; x++)
            {
                float u = Mathf.Abs((x + 0.5f) / size * 2f - 1f);
                float sides = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.78f, 1f, u));
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(band * sides));
            }
        }
        return Finish(tex, px);
    }

    /// <summary>Chevron pointing toward V = 1, with soft shoulders.</summary>
    private static Sprite Chevron(int size)
    {
        var tex = NewTex(size, size);
        var px = new Color[size * size];
        const float slope = 0.78f, thick = 0.2f, feather = 0.11f;
        for (int y = 0; y < size; y++)
        {
            float v = (y + 0.5f) / size;
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float au = Mathf.Abs(u);
                float spine = 1f - au * slope;                       // the V, apex at u = 0, v = 1
                float d = Mathf.Abs(v - spine);
                float a = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(thick, thick + feather, d));
                a *= Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.84f, 1f, au));   // trim the tails
                a *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.12f, v));    // trim the bottom
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }
        }
        return Finish(tex, px);
    }
}

