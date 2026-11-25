using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Drives match pacing so battles feel tense and fair (Clash Royale style):
///  - 3 minute rounds instead of 5.
///  - Player starts with the same elixir as the AI and regenerates at the same level-scaled rate.
///  - DOUBLE ELIXIR for both sides in the final 60 seconds, announced with a banner.
///  - Comeback RALLY: when the player's castle is clearly behind, player regen gets a +25% boost.
///  - "FINAL 10 SECONDS" banner for a last-second push.
/// Created automatically in any scene that has a GameManager and a SmartAIPlayer (single player battles only).
///
/// Banners use the same look as the tutorial banner: a UIPACK ribbon (Resources/BattleBanners), rotating light rays,
/// a round emblem, a spark burst and the TiltWarp font from Resources/Tutorial/KoSTutorialArt. Everything falls back
/// to generated shapes if those assets are missing.
/// </summary>
public class BattlePacingDirector : MonoBehaviour
{
    [SerializeField] private float matchDurationSeconds = 180f;
    [SerializeField] private float doubleElixirSeconds = 60f;
    [SerializeField] private float doubleElixirMultiplier = 2f;
    [SerializeField] private float finalCountdownSeconds = 10f;
    [Tooltip("Rally triggers when the player's castle HP ratio is this far below the enemy's.")]
    [SerializeField] private float rallyHpGap = 0.25f;
    [SerializeField] private float rallyMultiplier = 1.25f;

    private GameManager game;
    private SmartAIPlayer ai;
    private SegmentedEnergySliderController bar;
    private PeasantObjectiveTarget playerCastle, enemyCastle;

    private float playerBaseFill = -1f;
    private bool started, doubleAnnounced, finalAnnounced, rallyActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded += (s, m) => TryCreate();
        TryCreate();
    }

    private static void TryCreate()
    {
        if (GameModeManager.IsMultiplayer) return;
        if (FindFirstObjectByType<BattlePacingDirector>() != null) return;
        if (FindFirstObjectByType<GameManager>() == null || FindFirstObjectByType<SmartAIPlayer>() == null) return;
        new GameObject("[BattlePacingDirector]").AddComponent<BattlePacingDirector>();
    }

    private void Start()
    {
        game = FindFirstObjectByType<GameManager>();
        ai = FindFirstObjectByType<SmartAIPlayer>();
        bar = SegmentedEnergySliderController.Instance;
        if (game != null) game.SetMatchDuration(matchDurationSeconds);

        foreach (var t in FindObjectsByType<PeasantObjectiveTarget>(FindObjectsSortMode.None))
        {
            if (t == null) continue;
            bool isCastle = t.ObjectiveName == "Castle" || t.name.ToLower().Contains("castle");
            if (!isCastle) continue;
            if (t.TeamId == 0 && playerCastle == null) playerCastle = t;
            else if (t.TeamId == 1 && enemyCastle == null) enemyCastle = t;
        }
    }

    private void Update()
    {
        if (game == null || bar == null || !MatchReadyScreen.IsMatchStarted || game.IsMatchOver) return;
        if (playerBaseFill < 0f) playerBaseFill = bar.FillRate;

        if (!started)
        {
            started = true;
            // Same opening hand as the AI, so the first push is a real decision instead of a wait.
            float aiStart = ai != null ? ai.StartingEnergy : 4f;
            if (bar.CurrentEnergy < aiStart) bar.AddEnergy(aiStart - bar.CurrentEnergy);
        }

        float remaining = game.TimeRemaining;
        bool doubleElixir = remaining <= doubleElixirSeconds;
        float phase = doubleElixir ? doubleElixirMultiplier : 1f;

        // Comeback rally with hysteresis so it doesn't flicker.
        float pHp = Ratio(playerCastle), eHp = Ratio(enemyCastle);
        if (!rallyActive && eHp - pHp >= rallyHpGap) { rallyActive = true; ShowBanner(BannerKind.Rally); }
        else if (rallyActive && eHp - pHp < rallyHpGap * 0.5f) rallyActive = false;

        float levelMult = ai != null ? ai.LevelEnergyMultiplier : 1f;
        bar.FillRate = playerBaseFill * levelMult * phase * (rallyActive ? rallyMultiplier : 1f);
        if (ai != null) ai.ExternalEnergyMultiplier = phase;

        if (doubleElixir && !doubleAnnounced) { doubleAnnounced = true; ShowBanner(BannerKind.DoubleElixir); }
        if (remaining <= finalCountdownSeconds && !finalAnnounced) { finalAnnounced = true; ShowBanner(BannerKind.FinalSeconds); }

        // Clock ticks for the last 5 seconds.
        int sec = Mathf.CeilToInt(remaining);
        if (sec <= 5 && sec >= 1 && sec != lastTickSecond) { lastTickSecond = sec; KoSAudio.Play2D("countdown_tick", sec <= 3 ? 1f : 0.75f); }
    }

    private int lastTickSecond = -1;

    private static float Ratio(PeasantObjectiveTarget t) =>
        t == null || t.MaxHealth <= 0f ? 1f : Mathf.Clamp01(t.CurrentHealth / t.MaxHealth);

    // ───────────────────────────── Banner ─────────────────────────────

    public enum BannerKind { DoubleElixir, FinalSeconds, Rally }

    private struct BannerStyle
    {
        public string title, subtitle, ribbon;
        public Color accent, glow, subtitleColor;
    }

    private static BannerStyle StyleFor(BannerKind kind)
    {
        switch (kind)
        {
            case BannerKind.FinalSeconds:
                return new BannerStyle
                {
                    title = "FINAL 10 SECONDS!", subtitle = "Last push! Everything on the castle!", ribbon = "Label_Ribbon01_Red",
                    accent = new Color(1f, 0.28f, 0.2f), glow = new Color(1f, 0.55f, 0.2f), subtitleColor = new Color(1f, 0.9f, 0.75f)
                };
            case BannerKind.Rally:
                return new BannerStyle
                {
                    title = "RALLY!", subtitle = "+25% elixir while you're behind", ribbon = "Label_Ribbon01_Sky",
                    accent = new Color(0.3f, 0.8f, 1f), glow = new Color(0.55f, 0.9f, 1f), subtitleColor = new Color(0.85f, 0.96f, 1f)
                };
            default:
                return new BannerStyle
                {
                    title = "DOUBLE ELIXIR!", subtitle = "Elixir flows twice as fast", ribbon = "Label_Ribbon01_Magenta",
                    accent = new Color(0.85f, 0.3f, 1f), glow = new Color(1f, 0.55f, 1f), subtitleColor = new Color(1f, 0.88f, 1f)
                };
        }
    }

    private const float BannerY = 0.6f;          // vertical anchor of the banner on screen
    private const float RibbonW = 960f, RibbonH = 236f;

    private RectTransform bannerRoot, band, rays, glow, ribbon, emblem, subtitle, sparkLayer;
    private CanvasGroup bannerGroup, subtitleGroup, emblemGroup;
    private Image flashImg, vignetteImg, bandImg, raysImg, glowImg, ribbonImg, emblemFill, emblemHalo, dropA, dropB;
    private TextMeshProUGUI titleText, subtitleText, emblemText, emblemBadge;
    private RectTransform titleRect;
    private Coroutine bannerRoutine;
    private BannerKind currentKind;

    private class Spark { public RectTransform rt; public Image img; public Vector2 v; public float spin, life, maxLife; }
    private readonly List<Spark> sparks = new List<Spark>();

    private static Sprite whiteSprite, raysSprite, glowSprite, discSprite, goldRingSprite, bandSprite, vignetteSprite, starSprite;
    private static TMP_FontAsset bannerFont, bannerOutlineFont;
    private static Sprite elixirSprite;
    private static readonly Dictionary<string, Sprite> ribbonCache = new Dictionary<string, Sprite>();

    private void ShowBanner(BannerKind kind)
    {
        if (bannerRoot == null) BuildBanner();
        if (kind == BannerKind.FinalSeconds) { KoSAudio.Play2D("final_gong"); KoSAudio.SetMusicIntensity(1.06f); }
        else KoSAudio.Play2D("banner_gong");

        if (bannerRoutine != null) StopCoroutine(bannerRoutine);
        ClearSparks();
        bannerRoutine = StartCoroutine(PlayBanner(kind));
    }

    private IEnumerator PlayBanner(BannerKind kind)
    {
        currentKind = kind;
        var st = StyleFor(kind);
        ApplyStyle(kind, st);

        bannerRoot.gameObject.SetActive(true);
        const float hold = 2.0f, outDur = 0.38f;
        float total = hold + outDur;
        bool sparksFired = false, punched = false;
        float t = 0f;

        while (t < total)
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            t += dt;

            // Intro (0 - 0.4s): ribbon slams in from big + tilted, rays grow, screen flashes.
            float kIn = Mathf.Clamp01(t / 0.42f);
            float kOut = Mathf.Clamp01((t - hold) / outDur);
            float outE = kOut * kOut;

            bannerGroup.alpha = Mathf.Clamp01(t / 0.12f) * (1f - outE);
            flashImg.color = new Color(st.glow.r, st.glow.g, st.glow.b, 0.32f * Mathf.Clamp01(1f - t / 0.35f));

            float s = Mathf.LerpUnclamped(2.3f, 1f, EaseOutBack(kIn, 1.9f));
            ribbon.localScale = Vector3.one * s * (1f + 0.18f * outE);
            ribbon.localRotation = Quaternion.Euler(0, 0, Mathf.LerpUnclamped(-7f, 0f, EaseOutBack(kIn, 1.4f)));

            // Title punch when the ribbon lands.
            if (!punched && t >= 0.42f) punched = true;
            float pk = punched ? Mathf.Clamp01((t - 0.42f) / 0.35f) : 0f;
            float punch = punched ? Mathf.Sin(pk * Mathf.PI * 2.5f) * (1f - pk) * 0.09f : 0f;
            titleRect.localScale = Vector3.one * (1f + punch);

            // Dark band behind everything opens vertically.
            band.localScale = new Vector3(1f, EaseOutCubic(Mathf.Clamp01(t / 0.3f)) * (1f - outE), 1f);

            rays.localRotation = Quaternion.Euler(0, 0, -t * 24f);
            rays.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, EaseOutCubic(kIn)) * (1f + 0.04f * Mathf.Sin(t * 5f));
            glow.localScale = Vector3.one * (Mathf.Lerp(0.5f, 1f, EaseOutCubic(kIn)) + 0.06f * Mathf.Sin(t * 7f));

            // Emblem drops on top of the ribbon a beat later.
            float kE = Mathf.Clamp01((t - 0.18f) / 0.4f);
            emblemGroup.alpha = Mathf.Clamp01(kE * 3f);
            emblem.anchoredPosition = new Vector2(0, Mathf.Lerp(360f, 196f, EaseOutBack(kE, 2.2f)));
            float bob = punched ? Mathf.Sin((t - 0.42f) * 4.5f) * 5f : 0f;
            emblem.anchoredPosition += new Vector2(0, bob);
            emblem.localScale = Vector3.one * Mathf.LerpUnclamped(0.2f, 1f, EaseOutBack(kE, 2.4f)) * (1f - 0.3f * outE);
            emblemHalo.rectTransform.localRotation = Quaternion.Euler(0, 0, t * 60f);
            emblemHalo.color = WithA(st.glow, 0.55f + 0.25f * Mathf.Sin(t * 8f));

            if (kind == BannerKind.FinalSeconds && game != null)
                emblemText.text = Mathf.Max(1, Mathf.CeilToInt(game.TimeRemaining)).ToString();
            if (kind == BannerKind.DoubleElixir)
            {
                // Two drops that pulse out of phase, like elixir pouring in.
                dropA.rectTransform.localScale = Vector3.one * (1f + 0.07f * Mathf.Sin(t * 9f));
                dropB.rectTransform.localScale = Vector3.one * (1f + 0.07f * Mathf.Sin(t * 9f + 1.6f));
            }

            // Subtitle slides up under the ribbon.
            float kS = Mathf.Clamp01((t - 0.38f) / 0.3f);
            subtitleGroup.alpha = kS * (1f - outE);
            subtitle.anchoredPosition = new Vector2(0, Mathf.Lerp(-118f, -150f, EaseOutCubic(kS)));

            // Final seconds: red edges pulse like a heartbeat.
            if (kind == BannerKind.FinalSeconds)
            {
                float beat = Mathf.Pow(Mathf.Abs(Mathf.Sin(t * 4.2f)), 3f);
                vignetteImg.color = WithA(st.accent, (0.25f + 0.45f * beat) * Mathf.Clamp01(t / 0.2f) * (1f - outE));
            }
            else vignetteImg.color = Color.clear;

            if (!sparksFired && t >= 0.3f) { sparksFired = true; FireSparks(st); }
            UpdateSparks(dt);

            yield return null;
        }

        ClearSparks();
        bannerGroup.alpha = 0f;
        vignetteImg.color = Color.clear;
        bannerRoot.gameObject.SetActive(false);
        bannerRoutine = null;
    }

    private void ApplyStyle(BannerKind kind, BannerStyle st)
    {
        titleText.text = st.title;
        subtitleText.text = st.subtitle;
        subtitleText.color = st.subtitleColor;

        var rib = LoadRibbon(st.ribbon);
        if (rib != null)
        {
            ribbonImg.sprite = rib;
            ribbonImg.type = Image.Type.Sliced;
            ribbonImg.pixelsPerUnitMultiplier = rib.rect.height / RibbonH;
            ribbonImg.color = Color.white;
        }
        else
        {
            ribbonImg.sprite = whiteSprite;
            ribbonImg.type = Image.Type.Simple;
            ribbonImg.color = Color.Lerp(st.accent, Color.black, 0.35f);
        }

        // Title: white face with a soft tint toward the bottom, dark outline from the outline font / outline.
        Color bottom = Color.Lerp(st.glow, Color.white, 0.55f);
        titleText.enableVertexGradient = true;
        titleText.colorGradient = new VertexGradient(Color.white, Color.white, bottom, bottom);

        raysImg.color = WithA(st.glow, 0.5f);
        glowImg.color = WithA(st.glow, 0.38f);
        emblemFill.color = Color.Lerp(st.accent, Color.black, 0.55f);

        bool dbl = kind == BannerKind.DoubleElixir, rally = kind == BannerKind.Rally;
        dropA.gameObject.SetActive(dbl || rally);
        dropB.gameObject.SetActive(dbl);
        dropA.rectTransform.anchoredPosition = dbl ? new Vector2(-30f, 8f) : new Vector2(0f, 10f);
        dropB.rectTransform.anchoredPosition = new Vector2(30f, -2f);
        emblemText.gameObject.SetActive(kind == BannerKind.FinalSeconds);
        emblemText.text = "10";
        emblemBadge.gameObject.SetActive(dbl || rally);
        emblemBadge.text = dbl ? "x2" : "+25%";
        emblemBadge.fontSize = dbl ? 76 : 50;
    }

    // ───────────────────────────── Sparks ─────────────────────────────

    private void FireSparks(BannerStyle st)
    {
        for (int i = 0; i < 26; i++)
        {
            var go = new GameObject("Spark", typeof(RectTransform));
            go.transform.SetParent(sparkLayer, false);
            var rt = (RectTransform)go.transform;
            float sz = Random.Range(22f, 46f);
            rt.sizeDelta = new Vector2(sz, sz);
            float ang = Random.Range(0f, Mathf.PI * 2f);
            Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.75f);
            rt.anchoredPosition = dir * Random.Range(60f, 200f);
            var img = go.AddComponent<Image>();
            img.sprite = starSprite;
            img.raycastTarget = false;
            img.color = Random.value < 0.5f ? Color.Lerp(st.glow, Color.white, 0.4f) : new Color(1f, 0.88f, 0.4f);
            sparks.Add(new Spark
            {
                rt = rt, img = img, v = dir * Random.Range(520f, 1050f),
                spin = Random.Range(-360f, 360f), maxLife = Random.Range(0.7f, 1.15f)
            });
        }
    }

    private void UpdateSparks(float dt)
    {
        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            var p = sparks[i];
            p.life += dt;
            if (p.life >= p.maxLife || p.rt == null) { if (p.rt != null) Destroy(p.rt.gameObject); sparks.RemoveAt(i); continue; }
            p.v *= Mathf.Pow(0.06f, dt);   // strong drag: burst then hang
            p.v.y -= 260f * dt;
            p.rt.anchoredPosition += p.v * dt;
            p.rt.localRotation = Quaternion.Euler(0, 0, p.rt.localEulerAngles.z + p.spin * dt);
            float k = p.life / p.maxLife;
            p.rt.localScale = Vector3.one * (k < 0.15f ? k / 0.15f : 1f - Mathf.Pow((k - 0.15f) / 0.85f, 2f));
            var c = p.img.color; c.a = 1f - k * k; p.img.color = c;
        }
    }

    private void ClearSparks()
    {
        foreach (var p in sparks) if (p.rt != null) Destroy(p.rt.gameObject);
        sparks.Clear();
    }

    // ───────────────────────────── Build ─────────────────────────────

    private void BuildBanner()
    {
        MakeSprites();
        LoadArt();

        var canvasGo = new GameObject("PacingBannerCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        bannerRoot = Box((RectTransform)canvasGo.transform, "Banner", Vector2.zero, Vector2.one);
        bannerGroup = bannerRoot.gameObject.AddComponent<CanvasGroup>();
        bannerGroup.blocksRaycasts = false;
        bannerGroup.interactable = false;

        // Full screen layers.
        vignetteImg = AddImage(Box(bannerRoot, "EdgePulse", Vector2.zero, Vector2.one), vignetteSprite, Color.clear);
        vignetteImg.type = Image.Type.Sliced;
        flashImg = AddImage(Box(bannerRoot, "Flash", Vector2.zero, Vector2.one), whiteSprite, Color.clear);

        var c = new Vector2(0.5f, BannerY);

        band = Box(bannerRoot, "Band", new Vector2(0f, BannerY), new Vector2(1f, BannerY));
        band.sizeDelta = new Vector2(0f, 560f);
        band.anchoredPosition = new Vector2(0f, 40f);
        bandImg = AddImage(band, bandSprite, new Color(0.03f, 0.02f, 0.08f, 0.62f));

        rays = Centered(bannerRoot, "Rays", c, new Vector2(1500, 1500));
        raysImg = AddImage(rays, raysSprite, Color.clear);

        glow = Centered(bannerRoot, "Glow", c, new Vector2(900, 520));
        glowImg = AddImage(glow, glowSprite, Color.clear);

        sparkLayer = Centered(bannerRoot, "Sparks", c, Vector2.zero);

        // Ribbon + title.
        ribbon = Centered(bannerRoot, "Ribbon", c, new Vector2(RibbonW, RibbonH));
        var shadow = Centered(ribbon, "Shadow", new Vector2(0.5f, 0.5f), new Vector2(RibbonW * 0.92f, RibbonH * 0.8f));
        shadow.anchoredPosition = new Vector2(0, -22f);
        AddImage(shadow, glowSprite, new Color(0f, 0f, 0f, 0.55f));
        ribbonImg = AddImage(Box(ribbon, "Art", Vector2.zero, Vector2.one), whiteSprite, Color.white);

        // The face of the UIPACK ribbon is the upper ~70%; the folds sit below it.
        var area = Box(ribbon, "TitleArea", new Vector2(0.12f, 0.3f), new Vector2(0.88f, 0.96f));
        titleRect = area;
        titleText = MakeText(area, "Title", 96f, true);
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 40f;
        titleText.fontSizeMax = 96f;
        titleText.characterSpacing = 2f;
        titleText.margin = new Vector4(0, 4, 0, 0);

        // Emblem above the ribbon.
        emblem = Centered(bannerRoot, "Emblem", c, new Vector2(230, 230));
        emblemGroup = emblem.gameObject.AddComponent<CanvasGroup>();
        emblemGroup.blocksRaycasts = false;
        emblemHalo = AddImage(Centered(emblem, "Halo", new Vector2(0.5f, 0.5f), new Vector2(420, 420)), raysSprite, Color.clear);
        var disc = Centered(emblem, "Disc", new Vector2(0.5f, 0.5f), new Vector2(214, 214));
        AddImage(Centered(disc, "Drop", new Vector2(0.5f, 0.5f), new Vector2(236, 236)), glowSprite, new Color(0, 0, 0, 0.5f))
            .rectTransform.anchoredPosition = new Vector2(0, -10);
        emblemFill = AddImage(Box(disc, "Fill", Vector2.zero, Vector2.one), discSprite, Color.black);
        AddImage(Centered(disc, "Shine", new Vector2(0.5f, 0.72f), new Vector2(140, 60)), glowSprite, new Color(1, 1, 1, 0.18f));
        dropA = AddImage(Centered(disc, "DropA", new Vector2(0.5f, 0.5f), new Vector2(128, 128)), elixirSprite, Color.white);
        dropB = AddImage(Centered(disc, "DropB", new Vector2(0.5f, 0.5f), new Vector2(128, 128)), elixirSprite, Color.white);
        dropA.preserveAspect = dropB.preserveAspect = true;
        dropA.transform.SetAsLastSibling();
        AddImage(Box(disc, "GoldRing", Vector2.zero, Vector2.one), goldRingSprite, Color.white);

        var numRect = Box(disc, "Number", new Vector2(0.08f, 0.1f), new Vector2(0.92f, 0.92f));
        emblemText = MakeText(numRect, "Text", 120f, true);
        emblemText.color = Color.white;
        emblemText.enableVertexGradient = true;
        emblemText.colorGradient = new VertexGradient(Color.white, Color.white, new Color(1f, 0.85f, 0.4f), new Color(1f, 0.85f, 0.4f));

        var badgeRect = Centered(disc, "Badge", new Vector2(0.84f, 0.12f), new Vector2(170, 100));
        emblemBadge = MakeText(badgeRect, "Text", 64f, true);
        emblemBadge.color = Color.white;
        emblemBadge.enableVertexGradient = true;
        emblemBadge.colorGradient = new VertexGradient(new Color(1f, 0.97f, 0.7f), new Color(1f, 0.97f, 0.7f), new Color(1f, 0.72f, 0.15f), new Color(1f, 0.72f, 0.15f));

        // Subtitle.
        subtitle = Centered(bannerRoot, "Subtitle", c, new Vector2(1000, 90));
        subtitleGroup = subtitle.gameObject.AddComponent<CanvasGroup>();
        subtitleText = MakeText(subtitle, "Text", 50f, true);

        bannerRoot.gameObject.SetActive(false);
    }

    private static void LoadArt()
    {
        if (bannerFont == null)
        {
            var art = Resources.Load<Siege.Tutorial.KoSTutorialArt>("Tutorial/KoSTutorialArt");
            if (art != null) { bannerFont = art.font; bannerOutlineFont = art.outlineFont != null ? art.outlineFont : art.font; }
        }
        if (elixirSprite == null)
        {
            elixirSprite = Resources.Load<Sprite>("BattleBanners/Icon_ElixirDroplet");
            if (elixirSprite == null) elixirSprite = discSprite;
        }
    }

    private static Sprite LoadRibbon(string name)
    {
        if (ribbonCache.TryGetValue(name, out var s)) return s;
        s = Resources.Load<Sprite>("BattleBanners/" + name);
        ribbonCache[name] = s;
        return s;
    }

    private TextMeshProUGUI MakeText(RectTransform parent, string name, float size, bool heavy)
    {
        var r = Box(parent, name, Vector2.zero, Vector2.one);
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        var f = heavy ? bannerOutlineFont : bannerFont;
        if (f != null) t.font = f;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        t.color = Color.white;
        if (heavy && f == null)
        {
            // No KoS font available: fake the chunky outline on the default font.
            t.fontStyle = FontStyles.Bold;
            t.outlineWidth = 0.2f;
            t.outlineColor = new Color32(20, 10, 30, 255);
        }
        return t;
    }

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

    private static Color WithA(Color c, float a) { c.a = a; return c; }
    private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
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
        raysSprite = Rays(256, 14);
        glowSprite = SoftDisc(128);
        discSprite = Disc(256, 0.5f);
        goldRingSprite = GoldRing(256, 0.41f);
        bandSprite = Band(8, 128);
        vignetteSprite = Vignette(128);
        starSprite = Star(64);
    }

    private static Texture2D NewTex(int w, int h) =>
        new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

    private static Sprite Finish(Texture2D tex, Color[] px)
    {
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
    }

    /// <summary>Soft light rays fanning out from the centre (same look as the tutorial banner).</summary>
    private static Sprite Rays(int size, int count)
    {
        var tex = NewTex(size, size);
        var px = new Color[size * size];
        float c0 = size * 0.5f;
        for (int y = 0; y < size; y++)
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
        return Finish(tex, px);
    }

    /// <summary>Gaussian blob, used for glows and drop shadows.</summary>
    private static Sprite SoftDisc(int size)
    {
        var tex = NewTex(size, size);
        var px = new Color[size * size];
        float c0 = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - c0, y + 0.5f - c0).magnitude / c0;
                px[y * size + x] = new Color(1, 1, 1, Mathf.Exp(-d * d * 3.2f) * Mathf.Clamp01((1f - d) * 6f));
            }
        return Finish(tex, px);
    }

    /// <summary>Filled circle with a subtle top-lit gradient (tinted by Image.color).</summary>
    private static Sprite Disc(int size, float radius)
    {
        var tex = NewTex(size, size);
        var px = new Color[size * size];
        float c0 = size * 0.5f, aa = 1.5f / size;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - c0, y + 0.5f - c0).magnitude / size;
                float a = Mathf.Clamp01((radius - d) / aa);
                float shade = Mathf.Lerp(0.75f, 1.35f, y / (float)size);
                px[y * size + x] = new Color(Mathf.Min(1f, shade), Mathf.Min(1f, shade), Mathf.Min(1f, shade), a);
            }
        return Finish(tex, px);
    }

    /// <summary>Bevelled gold ring with dark edges, from inner (radius fraction) to the sprite edge.</summary>
    private static Sprite GoldRing(int size, float inner)
    {
        var tex = NewTex(size, size);
        var px = new Color[size * size];
        float c0 = size * 0.5f;
        float aa = 1.5f / size, edge = 3.5f / size;
        Color dark = new Color(0.3f, 0.14f, 0.02f, 1f);
        Color goldLo = new Color(0.86f, 0.52f, 0.1f, 1f);
        Color goldHi = new Color(1f, 0.95f, 0.6f, 1f);
        float mid = (inner + 0.5f) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - c0, y + 0.5f - c0).magnitude / size;
                float a = Mathf.Clamp01((d - inner) / aa) * Mathf.Clamp01((0.5f - d) / aa);
                Color c;
                if (d - inner < edge || 0.5f - d < edge) c = dark;
                else
                {
                    float bevel = 1f - Mathf.Abs(d - mid) / (0.5f - inner) * 2f;
                    c = Color.Lerp(goldLo, goldHi, Mathf.Clamp01(y / (float)size * 0.75f + bevel * 0.35f));
                }
                c.a = a;
                px[y * size + x] = c;
            }
        return Finish(tex, px);
    }

    /// <summary>Horizontal strip that fades out at the top and bottom.</summary>
    private static Sprite Band(int w, int h)
    {
        var tex = NewTex(w, h);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float v = Mathf.Abs((y + 0.5f) / h * 2f - 1f);
            float a = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.35f, 1f, v));
            for (int x = 0; x < w; x++) px[y * w + x] = new Color(1, 1, 1, a);
        }
        return Finish(tex, px);
    }

    /// <summary>Screen edge glow, clear in the middle; 9-sliced so it stays thin on any aspect ratio.</summary>
    private static Sprite Vignette(int size)
    {
        var tex = NewTex(size, size);
        var px = new Color[size * size];
        float edge = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Min(x + 0.5f, size - x - 0.5f) / edge;
                float dy = Mathf.Min(y + 0.5f, size - y - 0.5f) / edge;
                float d = Mathf.Min(dx, dy);
                px[y * size + x] = new Color(1, 1, 1, Mathf.Pow(1f - Mathf.Clamp01(d), 2.2f));
            }
        tex.SetPixels(px);
        tex.Apply();
        float b = size * 0.5f - 1f;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
    }

    /// <summary>Four-point sparkle star with a soft core.</summary>
    private static Sprite Star(int size)
    {
        var tex = NewTex(size, size);
        var px = new Color[size * size];
        float c0 = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = Mathf.Abs(x + 0.5f - c0) / c0, v = Mathf.Abs(y + 0.5f - c0) / c0;
                float star = Mathf.Clamp01(1f - (Mathf.Sqrt(u) + Mathf.Sqrt(v)) * 0.95f) * 2.2f;
                float core = Mathf.Exp(-(u * u + v * v) * 10f);
                px[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(star + core));
            }
        return Finish(tex, px);
    }
}

// bust
