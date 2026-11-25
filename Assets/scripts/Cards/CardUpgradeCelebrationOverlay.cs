using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Siege.Cards;

/// <summary>
/// AAA Clash Royale-style celebratory Level-Up animation overlay.
/// Built with authentic project UI pack assets:
/// - Screen_Dim vignette
/// - Chest_Gold_Glow radiant rays
/// - Authentic card frames & LevelBanner
/// - Authentic golden crown & blue_name_ribbon
/// - 3D Green_Empty continue button
/// - Sparkle star particles
/// </summary>
[DisallowMultipleComponent]
public class CardUpgradeCelebrationOverlay : MonoBehaviour
{
    [Header("Root & Canvas")]
    [SerializeField] private RectTransform overlayRoot;
    [SerializeField] private CanvasGroup overlayCanvasGroup;
    [SerializeField] private Image backdropDimmer;

    [Header("Rays & Halos")]
    [SerializeField] private RectTransform raysContainer;
    [SerializeField] private Image raysImage;
    [SerializeField] private Image radialGlowImage;

    [Header("Card Hero Presentation")]
    [SerializeField] private RectTransform cardHeroContainer;
    [SerializeField] private Image cardHeroFrame;
    [SerializeField] private Image cardHeroBase;
    [SerializeField] private Image cardHeroArt;
    [SerializeField] private Image cardHeroFlash;
    [SerializeField] private Image costDroplet;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private Image cardLevelBanner;
    [SerializeField] private TMP_Text cardLevelText;

    [Header("Celebration Badges")]
    [SerializeField] private RectTransform crownRect;
    [SerializeField] private Image crownImage;
    [SerializeField] private RectTransform bannerRect;
    [SerializeField] private Image bannerImage;
    [SerializeField] private TMP_Text bannerTitleText;

    [Header("Level Transition & Stat Showcase")]
    [SerializeField] private RectTransform levelTransitionRow;
    [SerializeField] private TMP_Text levelTransitionText;
    [SerializeField] private RectTransform statShowcaseCard;
    [SerializeField] private TMP_Text statHpLabel;
    [SerializeField] private TMP_Text statHpValue;
    [SerializeField] private TMP_Text statDmgLabel;
    [SerializeField] private TMP_Text statDmgValue;

    [Header("Confetti / Particles Container")]
    [SerializeField] private RectTransform particlesContainer;
    [SerializeField] private Sprite sparkleParticleSprite;

    [Header("Action Button")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Image continueButtonImage;
    [SerializeField] private TMP_Text continueButtonLabel;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip whooshSound;
    [SerializeField] private AudioClip impactSound;
    [SerializeField] private AudioClip chimeSound;

    [Header("Rarity Frame Sprites")]
    [SerializeField] private Sprite frameCommon;
    [SerializeField] private Sprite frameRare;
    [SerializeField] private Sprite frameEpic;
    [SerializeField] private Sprite frameLegendary;

    // Runtime state
    private Tween raysRotateTween;
    private Tween raysPulseTween;
    private Tween glowPulseTween;
    private readonly List<GameObject> activeParticles = new List<GameObject>();
    private Action onCelebrationComplete;
    private bool isPlaying = false;

    public static CardUpgradeCelebrationOverlay Create(Transform parent)
    {
        var overlayGO = new GameObject("UpgradeCelebrationOverlay", typeof(RectTransform), typeof(CanvasGroup), typeof(CardUpgradeCelebrationOverlay));
        overlayGO.transform.SetParent(parent, false);
        overlayGO.transform.SetAsLastSibling();

        var rt = overlayGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var comp = overlayGO.GetComponent<CardUpgradeCelebrationOverlay>();
        comp.overlayRoot = rt;
        comp.overlayCanvasGroup = overlayGO.GetComponent<CanvasGroup>();

        // Audio
        comp.audioSource = overlayGO.AddComponent<AudioSource>();
        comp.audioSource.playOnAwake = false;

        // 1. Fullscreen Dimmer Backdrop
        var dimmerGO = new GameObject("BackdropDimmer", typeof(RectTransform), typeof(Image));
        dimmerGO.transform.SetParent(overlayGO.transform, false);
        var dRT = dimmerGO.GetComponent<RectTransform>();
        dRT.anchorMin = Vector2.zero; dRT.anchorMax = Vector2.one;
        dRT.offsetMin = Vector2.zero; dRT.offsetMax = Vector2.zero;
        comp.backdropDimmer = dimmerGO.GetComponent<Image>();
        comp.backdropDimmer.color = new Color(0.01f, 0.04f, 0.12f, 0.96f);

        // 2. Radiant Rays Container
        var raysGO = new GameObject("RaysContainer", typeof(RectTransform), typeof(Image));
        raysGO.transform.SetParent(overlayGO.transform, false);
        comp.raysContainer = raysGO.GetComponent<RectTransform>();
        comp.raysContainer.sizeDelta = new Vector2(960f, 960f);
        comp.raysContainer.anchoredPosition = new Vector2(0f, 75f);
        comp.raysImage = raysGO.GetComponent<Image>();
        comp.raysImage.color = new Color(1f, 0.82f, 0.28f, 0.85f);
        comp.raysImage.raycastTarget = false;

        // 3. Radial Soft Glow
        var glowGO = new GameObject("RadialGlow", typeof(RectTransform), typeof(Image));
        glowGO.transform.SetParent(overlayGO.transform, false);
        var gRT = glowGO.GetComponent<RectTransform>();
        gRT.sizeDelta = new Vector2(720f, 720f);
        gRT.anchoredPosition = new Vector2(0f, 75f);
        comp.radialGlowImage = glowGO.GetComponent<Image>();
        comp.radialGlowImage.color = new Color(1f, 0.72f, 0.18f, 0.50f);
        comp.radialGlowImage.raycastTarget = false;

        // 4. Particles Container
        var partGO = new GameObject("ParticlesContainer", typeof(RectTransform));
        partGO.transform.SetParent(overlayGO.transform, false);
        comp.particlesContainer = partGO.GetComponent<RectTransform>();
        comp.particlesContainer.anchoredPosition = new Vector2(0f, 75f);

        // 5. Card Hero Container (Grand Size: 360x480)
        var heroGO = new GameObject("CardHero", typeof(RectTransform), typeof(Image));
        heroGO.transform.SetParent(overlayGO.transform, false);
        comp.cardHeroContainer = heroGO.GetComponent<RectTransform>();
        comp.cardHeroContainer.sizeDelta = new Vector2(360f, 480f);
        comp.cardHeroContainer.anchoredPosition = new Vector2(0f, 75f);
        comp.cardHeroFrame = heroGO.GetComponent<Image>();
        comp.cardHeroFrame.type = Image.Type.Sliced;

        // Mask & Base
        var maskGO = new GameObject("Mask", typeof(RectTransform), typeof(Image), typeof(Mask));
        maskGO.transform.SetParent(heroGO.transform, false);
        var mRT = maskGO.GetComponent<RectTransform>();
        mRT.anchorMin = new Vector2(0.04f, 0.14f); mRT.anchorMax = new Vector2(0.96f, 0.96f);
        mRT.offsetMin = Vector2.zero; mRT.offsetMax = Vector2.zero;
        comp.cardHeroBase = maskGO.GetComponent<Image>();
        comp.cardHeroBase.type = Image.Type.Sliced;
        var mask = maskGO.GetComponent<Mask>();
        mask.showMaskGraphic = true;

        // Art
        var artGO = new GameObject("CardArt", typeof(RectTransform), typeof(Image));
        artGO.transform.SetParent(maskGO.transform, false);
        var aRT = artGO.GetComponent<RectTransform>();
        aRT.anchorMin = Vector2.zero; aRT.anchorMax = Vector2.one;
        aRT.offsetMin = Vector2.zero; aRT.offsetMax = Vector2.zero;
        comp.cardHeroArt = artGO.GetComponent<Image>();
        comp.cardHeroArt.preserveAspect = true;

        // Flash Overlay
        var flashGO = new GameObject("FlashOverlay", typeof(RectTransform), typeof(Image));
        flashGO.transform.SetParent(maskGO.transform, false);
        var fRT = flashGO.GetComponent<RectTransform>();
        fRT.anchorMin = Vector2.zero; fRT.anchorMax = Vector2.one;
        fRT.offsetMin = Vector2.zero; fRT.offsetMax = Vector2.zero;
        comp.cardHeroFlash = flashGO.GetComponent<Image>();
        comp.cardHeroFlash.color = new Color(1f, 1f, 1f, 0f);
        comp.cardHeroFlash.raycastTarget = false;

        // Elixir Cost badge in top-left
        var costGO = new GameObject("CostBadge", typeof(RectTransform), typeof(Image));
        costGO.transform.SetParent(heroGO.transform, false);
        var cRT = costGO.GetComponent<RectTransform>();
        cRT.anchorMin = new Vector2(0f, 1f); cRT.anchorMax = new Vector2(0f, 1f);
        cRT.anchoredPosition = new Vector2(32f, -32f);
        cRT.sizeDelta = new Vector2(60f, 60f);
        comp.costDroplet = costGO.GetComponent<Image>();
        comp.costDroplet.preserveAspect = true;

        var costTxtGO = new GameObject("CostText", typeof(RectTransform), typeof(TextMeshProUGUI));
        costTxtGO.transform.SetParent(costGO.transform, false);
        var ctRT = costTxtGO.GetComponent<RectTransform>();
        ctRT.anchorMin = Vector2.zero; ctRT.anchorMax = Vector2.one;
        ctRT.offsetMin = Vector2.zero; ctRT.offsetMax = Vector2.zero;
        comp.costText = costTxtGO.GetComponent<TextMeshProUGUI>();
        comp.costText.alignment = TextAlignmentOptions.Center;
        comp.costText.fontStyle = FontStyles.Bold;
        comp.costText.fontSize = 28;

        // Card Level Ribbon at bottom of card
        var cardLvlGO = new GameObject("CardLevelBanner", typeof(RectTransform), typeof(Image));
        cardLvlGO.transform.SetParent(heroGO.transform, false);
        var clRT = cardLvlGO.GetComponent<RectTransform>();
        clRT.anchorMin = new Vector2(0.04f, 0.02f); clRT.anchorMax = new Vector2(0.96f, 0.14f);
        clRT.offsetMin = Vector2.zero; clRT.offsetMax = Vector2.zero;
        comp.cardLevelBanner = cardLvlGO.GetComponent<Image>();
        comp.cardLevelBanner.type = Image.Type.Sliced;
        comp.cardLevelBanner.color = new Color(0.12f, 0.18f, 0.28f, 0.95f);

        var cardLvlTxtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        cardLvlTxtGO.transform.SetParent(cardLvlGO.transform, false);
        var cltRT = cardLvlTxtGO.GetComponent<RectTransform>();
        cltRT.anchorMin = Vector2.zero; cltRT.anchorMax = Vector2.one;
        cltRT.offsetMin = Vector2.zero; cltRT.offsetMax = Vector2.zero;
        comp.cardLevelText = cardLvlTxtGO.GetComponent<TextMeshProUGUI>();
        comp.cardLevelText.alignment = TextAlignmentOptions.Center;
        comp.cardLevelText.fontStyle = FontStyles.Bold;
        comp.cardLevelText.fontSize = 22;
        comp.cardLevelText.color = Color.white;

        // 6. Crown (Perched on top of card)
        var crownGO = new GameObject("Crown", typeof(RectTransform), typeof(Image));
        crownGO.transform.SetParent(overlayGO.transform, false);
        comp.crownRect = crownGO.GetComponent<RectTransform>();
        comp.crownRect.sizeDelta = new Vector2(110f, 110f);
        comp.crownRect.anchoredPosition = new Vector2(0f, 345f);
        comp.crownImage = crownGO.GetComponent<Image>();
        comp.crownImage.preserveAspect = true;

        // 7. Grand Ribbon Announcement Banner
        var bannerGO = new GameObject("BannerAnnouncement", typeof(RectTransform), typeof(Image));
        bannerGO.transform.SetParent(overlayGO.transform, false);
        comp.bannerRect = bannerGO.GetComponent<RectTransform>();
        comp.bannerRect.sizeDelta = new Vector2(520f, 96f);
        comp.bannerRect.anchoredPosition = new Vector2(0f, 440f);
        comp.bannerImage = bannerGO.GetComponent<Image>();
        comp.bannerImage.preserveAspect = true;

        var titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(bannerGO.transform, false);
        var tRT = titleGO.GetComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = Vector2.zero; tRT.offsetMax = Vector2.zero;
        comp.bannerTitleText = titleGO.GetComponent<TextMeshProUGUI>();
        comp.bannerTitleText.text = "LEVEL UP!";
        comp.bannerTitleText.fontSize = 38;
        comp.bannerTitleText.fontStyle = FontStyles.Bold;
        comp.bannerTitleText.alignment = TextAlignmentOptions.Center;
        comp.bannerTitleText.color = new Color(1f, 0.90f, 0.20f);

        // 8. Level Transition Row
        var lvlRowGO = new GameObject("LevelTransitionRow", typeof(RectTransform));
        lvlRowGO.transform.SetParent(overlayGO.transform, false);
        comp.levelTransitionRow = lvlRowGO.GetComponent<RectTransform>();
        comp.levelTransitionRow.sizeDelta = new Vector2(480f, 60f);
        comp.levelTransitionRow.anchoredPosition = new Vector2(0f, -205f);

        var lvlTransGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        lvlTransGO.transform.SetParent(lvlRowGO.transform, false);
        var ltRT = lvlTransGO.GetComponent<RectTransform>();
        ltRT.anchorMin = Vector2.zero; ltRT.anchorMax = Vector2.one;
        ltRT.offsetMin = Vector2.zero; ltRT.offsetMax = Vector2.zero;
        comp.levelTransitionText = lvlTransGO.GetComponent<TextMeshProUGUI>();
        comp.levelTransitionText.fontSize = 32;
        comp.levelTransitionText.fontStyle = FontStyles.Bold;
        comp.levelTransitionText.alignment = TextAlignmentOptions.Center;
        comp.levelTransitionText.color = Color.white;

        // 9. Stat Boost Comparison Card (Clash Royale Style: HP & Damage)
        var statCardGO = new GameObject("StatShowcaseCard", typeof(RectTransform), typeof(Image));
        statCardGO.transform.SetParent(overlayGO.transform, false);
        comp.statShowcaseCard = statCardGO.GetComponent<RectTransform>();
        comp.statShowcaseCard.sizeDelta = new Vector2(500f, 92f);
        comp.statShowcaseCard.anchoredPosition = new Vector2(0f, -288f);
        var scImg = statCardGO.GetComponent<Image>();
        scImg.type = Image.Type.Sliced;
        scImg.color = new Color(0.05f, 0.14f, 0.28f, 0.96f);

        // Left Stat (Hitpoints)
        var hpGO = new GameObject("StatHP", typeof(RectTransform));
        hpGO.transform.SetParent(statCardGO.transform, false);
        var hpRT = hpGO.GetComponent<RectTransform>();
        hpRT.anchorMin = new Vector2(0.04f, 0.08f); hpRT.anchorMax = new Vector2(0.48f, 0.92f);
        hpRT.offsetMin = Vector2.zero; hpRT.offsetMax = Vector2.zero;

        var hpLblGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        hpLblGO.transform.SetParent(hpGO.transform, false);
        var hlRT = hpLblGO.GetComponent<RectTransform>();
        hlRT.anchorMin = new Vector2(0f, 0.50f); hlRT.anchorMax = new Vector2(1f, 1f);
        hlRT.offsetMin = Vector2.zero; hlRT.offsetMax = Vector2.zero;
        comp.statHpLabel = hpLblGO.GetComponent<TextMeshProUGUI>();
        comp.statHpLabel.text = "<color=#6EDAFF>HITPOINTS</color>";
        comp.statHpLabel.fontSize = 18;
        comp.statHpLabel.fontStyle = FontStyles.Bold;
        comp.statHpLabel.alignment = TextAlignmentOptions.Center;

        var hpValGO = new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI));
        hpValGO.transform.SetParent(hpGO.transform, false);
        var hvRT = hpValGO.GetComponent<RectTransform>();
        hvRT.anchorMin = new Vector2(0f, 0f); hvRT.anchorMax = new Vector2(1f, 0.50f);
        hvRT.offsetMin = Vector2.zero; hvRT.offsetMax = Vector2.zero;
        comp.statHpValue = hpValGO.GetComponent<TextMeshProUGUI>();
        comp.statHpValue.text = "<color=#2ECC71><b>+10%</b> ▲</color>";
        comp.statHpValue.fontSize = 24;
        comp.statHpValue.fontStyle = FontStyles.Bold;
        comp.statHpValue.alignment = TextAlignmentOptions.Center;

        // Divider Line
        var divGO = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        divGO.transform.SetParent(statCardGO.transform, false);
        var divRT = divGO.GetComponent<RectTransform>();
        divRT.anchorMin = new Vector2(0.50f, 0.15f); divRT.anchorMax = new Vector2(0.50f, 0.85f);
        divRT.sizeDelta = new Vector2(2f, 0f);
        var divImg = divGO.GetComponent<Image>();
        divImg.color = new Color(0.20f, 0.35f, 0.55f, 0.6f);

        // Right Stat (Damage)
        var dmgGO = new GameObject("StatDamage", typeof(RectTransform));
        dmgGO.transform.SetParent(statCardGO.transform, false);
        var dmgRT = dmgGO.GetComponent<RectTransform>();
        dmgRT.anchorMin = new Vector2(0.52f, 0.08f); dmgRT.anchorMax = new Vector2(0.96f, 0.92f);
        dmgRT.offsetMin = Vector2.zero; dmgRT.offsetMax = Vector2.zero;

        var dmgLblGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        dmgLblGO.transform.SetParent(dmgGO.transform, false);
        var dlRT = dmgLblGO.GetComponent<RectTransform>();
        dlRT.anchorMin = new Vector2(0f, 0.50f); dlRT.anchorMax = new Vector2(1f, 1f);
        dlRT.offsetMin = Vector2.zero; dlRT.offsetMax = Vector2.zero;
        comp.statDmgLabel = dmgLblGO.GetComponent<TextMeshProUGUI>();
        comp.statDmgLabel.text = "<color=#FF7675>DAMAGE</color>";
        comp.statDmgLabel.fontSize = 18;
        comp.statDmgLabel.fontStyle = FontStyles.Bold;
        comp.statDmgLabel.alignment = TextAlignmentOptions.Center;

        var dmgValGO = new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI));
        dmgValGO.transform.SetParent(dmgGO.transform, false);
        var dvRT = dmgValGO.GetComponent<RectTransform>();
        dvRT.anchorMin = new Vector2(0f, 0f); dvRT.anchorMax = new Vector2(1f, 0.50f);
        dvRT.offsetMin = Vector2.zero; dvRT.offsetMax = Vector2.zero;
        comp.statDmgValue = dmgValGO.GetComponent<TextMeshProUGUI>();
        comp.statDmgValue.text = "<color=#2ECC71><b>+10%</b> ▲</color>";
        comp.statDmgValue.fontSize = 24;
        comp.statDmgValue.fontStyle = FontStyles.Bold;
        comp.statDmgValue.alignment = TextAlignmentOptions.Center;

        // 10. 3D Continue Button (Vibrant Green with Bevel)
        var btnGO = new GameObject("ContinueButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(overlayGO.transform, false);
        var bRT = btnGO.GetComponent<RectTransform>();
        bRT.sizeDelta = new Vector2(340f, 84f);
        bRT.anchoredPosition = new Vector2(0f, -395f);
        comp.continueButtonImage = btnGO.GetComponent<Image>();
        comp.continueButtonImage.type = Image.Type.Sliced;
        comp.continueButton = btnGO.GetComponent<Button>();
        comp.continueButton.targetGraphic = comp.continueButtonImage;
        comp.continueButton.onClick.AddListener(comp.Dismiss);

        var btnLblGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        btnLblGO.transform.SetParent(btnGO.transform, false);
        var blRT = btnLblGO.GetComponent<RectTransform>();
        blRT.anchorMin = Vector2.zero; blRT.anchorMax = Vector2.one;
        blRT.offsetMin = Vector2.zero; blRT.offsetMax = Vector2.zero;
        comp.continueButtonLabel = btnLblGO.GetComponent<TextMeshProUGUI>();
        comp.continueButtonLabel.text = "CONTINUE";
        comp.continueButtonLabel.fontSize = 28;
        comp.continueButtonLabel.fontStyle = FontStyles.Bold;
        comp.continueButtonLabel.alignment = TextAlignmentOptions.Center;
        comp.continueButtonLabel.color = Color.white;

        // Load authentic project assets
        var screenDim = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/Chest_UI_Pack/Overlays/Screen_Dim.png");
        if (screenDim != null) comp.backdropDimmer.sprite = screenDim;

        var chestGlow = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/Chest_UI_Pack/Overlays/Chest_Gold_Glow.png");
        if (chestGlow != null) comp.raysImage.sprite = chestGlow;
        else comp.raysImage.sprite = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Demo/Demo_Image/Image_Light.png");

        comp.radialGlowImage.sprite = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Demo/Demo_Image/Glow01.png");

        var ribbonSpr = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/battlevs/arena_matchmaking_transparent_sprites/Banners/blue_name_ribbon.png");
        if (ribbonSpr != null) comp.bannerImage.sprite = ribbonSpr;

        var crownSpr = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/battlevs/arena_matchmaking_transparent_sprites/Icons/crown.png");
        if (crownSpr == null) crownSpr = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Demo/Demo_Image/Image_Crown.png");
        comp.crownImage.sprite = crownSpr;

        var greenBtnSpr = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/Chest_UI_Pack/Buttons/Green_Empty.png");
        if (greenBtnSpr != null) comp.continueButtonImage.sprite = greenBtnSpr;

        var lvlBannerSpr = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_LevelBanner_Sliced.png");
        if (lvlBannerSpr != null) comp.cardLevelBanner.sprite = lvlBannerSpr;

        comp.sparkleParticleSprite = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/Chest_UI_Pack/Effects/Sparkle.png");
        comp.costDroplet.sprite = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Icon_ElixirDroplet.png");
        comp.cardHeroFrame.sprite = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_Frame_Rare.png");
        comp.cardHeroBase.sprite = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_Base_Sliced.png");

        comp.frameCommon = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_Frame_Common.png");
        comp.frameRare = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_Frame_Rare.png");
        comp.frameEpic = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_Frame_Epic.png");
        comp.frameLegendary = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Card_Frame_Legendary.png");

        comp.whooshSound = RuntimeAssets.LoadAssetAtPath<AudioClip>("Assets/Resources/Sounds/arrowrelease.mp3");
        comp.impactSound = RuntimeAssets.LoadAssetAtPath<AudioClip>("Assets/Resources/Sounds/dragon-studio-hammer-smash-effect-382731.mp3");
        comp.chimeSound  = RuntimeAssets.LoadAssetAtPath<AudioClip>("Assets/Resources/Sounds/arrowhit.mp3");

        overlayGO.SetActive(false);
        return comp;
    }

    private void Awake()
    {
        if (overlayRoot == null) overlayRoot = GetComponent<RectTransform>();
        if (overlayCanvasGroup == null) overlayCanvasGroup = GetComponent<CanvasGroup>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(Dismiss);
        }

        // Fullscreen tap dismiss
        var clickTrigger = gameObject.GetComponent<Button>();
        if (clickTrigger == null) clickTrigger = gameObject.AddComponent<Button>();
        clickTrigger.transition = Selectable.Transition.None;
        clickTrigger.onClick.RemoveAllListeners();
        clickTrigger.onClick.AddListener(Dismiss);

        if (overlayCanvasGroup != null) overlayCanvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Launches the full AAA Clash Royale celebration animation sequence.
    /// </summary>
    public void PlayUpgradeSequence(CardDefinition def, int oldLevel, int newLevel, Action onComplete = null)
    {
        if (def == null)
        {
            onComplete?.Invoke();
            return;
        }

        onCelebrationComplete = onComplete;
        isPlaying = true;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        // Configure visuals for this card
        ConfigureCardVisuals(def, oldLevel, newLevel);

        // Kill active tweens
        KillActiveTweens();

        // 1. Fade in Dimmer & Overlay
        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.alpha = 0f;
            overlayCanvasGroup.DOFade(1f, 0.22f).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        // 2. Play Whoosh SFX
        PlaySfx(whooshSound, 0.85f);

        // 3. Sunburst Rays: Pop in and start rotating
        if (raysContainer != null)
        {
            raysContainer.localScale = Vector3.zero;
            raysContainer.DOScale(Vector3.one, 0.40f).SetEase(Ease.OutBack).SetUpdate(true);

            raysRotateTween = raysContainer.DORotate(new Vector3(0, 0, -360f), 14f, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart)
                .SetUpdate(true);

            raysPulseTween = raysContainer.DOScale(Vector3.one * 1.08f, 1.2f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        // 4. Radial Glow: Gentle breathing pulse
        if (radialGlowImage != null)
        {
            radialGlowImage.transform.localScale = Vector3.one * 0.85f;
            glowPulseTween = radialGlowImage.transform.DOScale(Vector3.one * 1.25f, 0.9f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        // 5. Card Hero: Grand entrance with elastic overshoot
        if (cardHeroContainer != null)
        {
            cardHeroContainer.localScale = Vector3.zero;
            cardHeroContainer.localRotation = Quaternion.Euler(0, 0, -6f);

            var seq = DOTween.Sequence().SetUpdate(true);
            seq.Append(cardHeroContainer.DOScale(Vector3.one * 1.15f, 0.38f).SetEase(Ease.OutBack));
            seq.Join(cardHeroContainer.DORotate(Vector3.zero, 0.38f).SetEase(Ease.OutBack));
            seq.Append(cardHeroContainer.DOScale(Vector3.one, 0.22f).SetEase(Ease.InOutQuad));

            seq.OnComplete(() =>
            {
                PlaySfx(impactSound, 0.95f);
                PlayCardEnergyFlash();
                SpawnStarConfettiBurst();
                PlayLevelTransitionAnim(oldLevel, newLevel);
            });
        }
        else
        {
            PlayLevelTransitionAnim(oldLevel, newLevel);
        }

        // 6. Crown Drop onto card
        if (crownRect != null)
        {
            crownRect.localScale = Vector3.zero;
            crownRect.anchoredPosition = new Vector2(0f, 420f);
            crownRect.DOScale(Vector3.one, 0.45f).SetDelay(0.32f).SetEase(Ease.OutBounce).SetUpdate(true);
            crownRect.DOAnchorPosY(345f, 0.45f).SetDelay(0.32f).SetEase(Ease.OutBounce).SetUpdate(true);
        }

        // 7. Grand Ribbon Banner
        if (bannerRect != null)
        {
            bannerRect.localScale = Vector3.zero;
            bannerRect.DOScale(Vector3.one, 0.40f).SetDelay(0.20f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        // 8. Stat Showcase Card: Slide in from bottom
        if (statShowcaseCard != null)
        {
            statShowcaseCard.localScale = Vector3.zero;
            statShowcaseCard.DOScale(Vector3.one, 0.38f).SetDelay(0.55f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        // 9. Continue Button: Pop in with tactile bounce
        if (continueButton != null)
        {
            continueButton.transform.localScale = Vector3.zero;
            continueButton.transform.DOScale(Vector3.one, 0.42f).SetDelay(0.70f).SetEase(Ease.OutBack).SetUpdate(true);
        }
    }

    private void ConfigureCardVisuals(CardDefinition def, int oldLevel, int newLevel)
    {
        if (cardHeroArt != null) cardHeroArt.sprite = def.cardSprite;

        // Frame by rarity
        if (cardHeroFrame != null)
        {
            switch (def.rarity)
            {
                case CardRarity.Legendary: if (frameLegendary != null) cardHeroFrame.sprite = frameLegendary; break;
                case CardRarity.Epic:      if (frameEpic != null) cardHeroFrame.sprite = frameEpic; break;
                case CardRarity.Rare:      if (frameRare != null) cardHeroFrame.sprite = frameRare; break;
                case CardRarity.Common:
                default:                   if (frameCommon != null) cardHeroFrame.sprite = frameCommon; break;
            }
        }

        // Elixir Cost
        if (costText != null) costText.text = def.elixirCost.ToString();

        // Level text on card ribbon
        if (cardLevelText != null)
            cardLevelText.text = $"Level {newLevel}";

        // Level Transition Text
        if (levelTransitionText != null)
        {
            levelTransitionText.text = $"<size=28><color=#95A5A6>Level {oldLevel}</color></size>    <size=34><color=#F1C40F> to </color></size>    <size=36><b><color=#FFFFFF>Level {oldLevel}</color></b></size>";
        }

        // Stat Card Values
        bool isMax = def.IsMaxLevel(newLevel);
        if (statHpValue != null)
            statHpValue.text = isMax ? "<color=#FFD700>MAX</color>" : "<color=#2ECC71><b>+10%</b> ▲</color>";

        if (statDmgValue != null)
            statDmgValue.text = isMax ? "<color=#FFD700>MAX</color>" : "<color=#2ECC71><b>+10%</b> ▲</color>";

        if (cardHeroFlash != null)
        {
            cardHeroFlash.color = new Color(1f, 1f, 1f, 0f);
        }
    }

    private void PlayCardEnergyFlash()
    {
        if (cardHeroFlash != null)
        {
            cardHeroFlash.DOKill();
            cardHeroFlash.color = new Color(1f, 1f, 1f, 0.90f);
            cardHeroFlash.DOFade(0f, 0.40f).SetEase(Ease.OutQuad).SetUpdate(true);
        }
    }

    private void PlayLevelTransitionAnim(int oldLevel, int newLevel)
    {
        if (levelTransitionText == null) return;

        levelTransitionText.transform.DOKill();

        var seq = DOTween.Sequence().SetUpdate(true);
        seq.Append(levelTransitionText.transform.DOScale(Vector3.one * 1.30f, 0.20f).SetEase(Ease.OutQuad));
        seq.AppendCallback(() =>
        {
            PlaySfx(chimeSound, 1f);
            levelTransitionText.text = $"<size=28><color=#95A5A6>Level {oldLevel}</color></size>    <size=34><color=#F1C40F> to </color></size>    <size=38><b><color=#2ECC71>Level {newLevel}</color></b></size>";
        });
        seq.Append(levelTransitionText.transform.DOPunchScale(Vector3.one * 0.40f, 0.35f, 6, 0.6f));
        seq.Append(levelTransitionText.transform.DOScale(Vector3.one, 0.18f).SetEase(Ease.InOutQuad));

        // Punch stat card
        if (statShowcaseCard != null)
        {
            statShowcaseCard.DOPunchScale(Vector3.one * 0.12f, 0.35f, 5, 0.5f).SetUpdate(true);
        }
    }

    private void SpawnStarConfettiBurst()
    {
        if (particlesContainer == null) return;

        ClearActiveParticles();

        int count = 20;
        for (int i = 0; i < count; i++)
        {
            float angle = (i / (float)count) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.15f, 0.15f);
            float dist = UnityEngine.Random.Range(220f, 480f);
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 targetPos = dir * dist;

            var pGO = new GameObject($"Sparkle_{i}", typeof(RectTransform), typeof(Image));
            pGO.transform.SetParent(particlesContainer, false);
            activeParticles.Add(pGO);

            var pRT = pGO.GetComponent<RectTransform>();
            pRT.sizeDelta = new Vector2(36f, 36f);
            pRT.anchoredPosition = Vector2.zero;
            pRT.localScale = Vector3.zero;

            var pImg = pGO.GetComponent<Image>();
            if (sparkleParticleSprite != null) pImg.sprite = sparkleParticleSprite;
            pImg.color = (i % 2 == 0) ? new Color(1f, 0.90f, 0.35f, 1f) : new Color(0.35f, 0.95f, 0.65f, 1f);
            pImg.raycastTarget = false;

            float flyDuration = UnityEngine.Random.Range(0.65f, 0.90f);
            pRT.DOAnchorPos(targetPos, flyDuration).SetEase(Ease.OutCubic).SetUpdate(true);
            pRT.DOScale(Vector3.one * UnityEngine.Random.Range(0.9f, 1.5f), flyDuration * 0.4f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    pRT.DOScale(Vector3.zero, flyDuration * 0.6f).SetEase(Ease.InQuad).SetUpdate(true);
                });
            pRT.DORotate(new Vector3(0, 0, UnityEngine.Random.Range(-360f, 360f)), flyDuration, RotateMode.FastBeyond360).SetUpdate(true);
            pImg.DOFade(0f, flyDuration).SetEase(Ease.InCubic).SetUpdate(true);
        }
    }

    public void Dismiss()
    {
        if (!isPlaying) return;
        isPlaying = false;

        KillActiveTweens();

        // Crisp exit
        if (cardHeroContainer != null)
        {
            cardHeroContainer.DOScale(Vector3.zero, 0.20f).SetEase(Ease.InBack).SetUpdate(true);
        }

        if (raysContainer != null)
        {
            raysContainer.DOScale(Vector3.zero, 0.18f).SetEase(Ease.InQuad).SetUpdate(true);
        }

        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.DOFade(0f, 0.20f).SetEase(Ease.InQuad).SetUpdate(true)
                .OnComplete(() =>
                {
                    ClearActiveParticles();
                    gameObject.SetActive(false);
                    onCelebrationComplete?.Invoke();
                });
        }
        else
        {
            ClearActiveParticles();
            gameObject.SetActive(false);
            onCelebrationComplete?.Invoke();
        }
    }

    private void PlaySfx(AudioClip clip, float volume = 1f)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }

    private void KillActiveTweens()
    {
        raysRotateTween?.Kill();
        raysPulseTween?.Kill();
        glowPulseTween?.Kill();
        if (raysContainer != null) raysContainer.DOKill();
        if (cardHeroContainer != null) cardHeroContainer.DOKill();
        if (crownRect != null) crownRect.DOKill();
        if (bannerRect != null) bannerRect.DOKill();
        if (levelTransitionRow != null) levelTransitionRow.DOKill();
        if (statShowcaseCard != null) statShowcaseCard.DOKill();
        if (continueButton != null) continueButton.transform.DOKill();
        if (cardHeroFlash != null) cardHeroFlash.DOKill();
        if (levelTransitionText != null) levelTransitionText.transform.DOKill();
    }

    private void ClearActiveParticles()
    {
        for (int i = 0; i < activeParticles.Count; i++)
        {
            if (activeParticles[i] != null)
                Destroy(activeParticles[i]);
        }
        activeParticles.Clear();
    }

    private void OnDestroy()
    {
        KillActiveTweens();
        ClearActiveParticles();
    }
}
