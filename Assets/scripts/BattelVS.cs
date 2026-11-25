using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Kings of Siege — Battle VS & Countdown Screen (BattelVS).
/// Seamlessly animates the manually crafted layout using punchy DOTween tweens:
/// - Entrance Slam: Red and Blue combatants slam from top/bottom with heavy bounce,
///   seam line slashes open, silhouettes pop, ground shakes, countdown number 3 lands with squash & stretch.
/// - Idle Tension Breathing: Subtle organic floating breathing on red and blue combatants.
/// - Countdown Pump: 3 -> 2 -> 1 -> START! with dynamic sprite swap and clean 3D pop scaling (isolated to the number only).
/// - Opponent Joined: Punch feedback on opponent container when connecting in multiplayer.
/// - Dismiss Blast: START! zooms into camera, red/blue slice away up and down into 3D battlefield, seam slashes shut.
/// - Works seamlessly both in Play Mode and directly in Edit Mode without running the game.
/// </summary>
[DisallowMultipleComponent]
public class BattelVS : MonoBehaviour
{
    // Calibrated coordinates matching manual layout
    public static readonly Vector2 RESTING_POS_P2_RED = new Vector2(-9.7f, 198f);
    public static readonly Vector2 RESTING_POS_P1_BLUE = new Vector2(-9.7f, -150f);
    public static readonly Vector2 RESTING_POS_COUNTDOWN = Vector2.zero;
    public static readonly Vector2 RESTING_POS_SEAM = new Vector2(-5f, -7.1f);

    // Official Palette from README
    public static readonly Color ColorCoralRed = new Color(1f, 0.255f, 0.282f, 1f); // #FF4148
    public static readonly Color ColorRoyalBlue = new Color(0.090f, 0.376f, 1f, 1f);   // #1760FF
    public static readonly Color ColorDarkMedallion = new Color(0.133f, 0.133f, 0.133f, 1f); // #222222

    [Header("Panel Root & Mask")]
    [SerializeField] private RectTransform panelMask;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Diagonal Background & Seam")]
    [SerializeField] private RectTransform bgTopRed;
    [SerializeField] private RectTransform bgBottomBlue;
    [SerializeField] private RectTransform middleWhite;
    [SerializeField] private RectTransform seamContainer;
    [SerializeField] private Image seamLine;
    [SerializeField] private RectTransform decoratons;
    [SerializeField] private Image[] battleSilhouettes;

    [Header("Player 2 / Opponent (Red Section)")]
    [SerializeField] private RectTransform containerPlayer2;
    [SerializeField] private Image avatarRed;
    [SerializeField] private Image nameplateRed;
    [SerializeField] private Image leagueShieldRed;
    [SerializeField] private Image levelMedallionRed;
    [SerializeField] private TMP_Text textPlayer2Name;
    [SerializeField] private TMP_Text textPlayer2Level;

    [Header("Player 1 / Local Player (Blue Section)")]
    [SerializeField] private RectTransform containerPlayer1;
    [SerializeField] private Image avatarBlue;
    [SerializeField] private Image nameplateBlue;
    [SerializeField] private Image leagueShieldBlue;
    [SerializeField] private Image levelMedallionBlue;
    [SerializeField] private TMP_Text textPlayer1Name;
    [SerializeField] private TMP_Text textPlayer1Level;

    [Header("Center Countdown (middleText)")]
    [SerializeField] private RectTransform countdownContainer;
    [SerializeField] private Image countdownImage;
    [SerializeField] private TMP_Text textVS;

    [Header("Countdown Sprites")]
    [SerializeField] private Sprite spriteCountdown3;
    [SerializeField] private Sprite spriteCountdown2;
    [SerializeField] private Sprite spriteCountdown1;
    [SerializeField] private Sprite spriteCountdownStart;

    [Header("League Sprites Library")]
    [SerializeField] private Sprite spriteLeagueStarterWood;
    [SerializeField] private Sprite spriteLeagueSilver;
    [SerializeField] private Sprite spriteLeagueGold;
    [SerializeField] private Sprite spriteLeagueLegend;
    [SerializeField] private Sprite spriteLeagueChampion;

    // Resting transforms (faithfully preserved from manual setup)
    private Vector2 origPosContainerP1 = new Vector2(-9.7f, -150f);
    private Vector3 origScaleContainerP1 = new Vector3(1.84f, 1.56f, 1.56f);
    private Vector3 origRotContainerP1 = Vector3.zero;

    private Vector2 origPosContainerP2 = new Vector2(-9.7f, 198f);
    private Vector3 origScaleContainerP2 = new Vector3(1.84f, 1.56f, 1.56f);
    private Vector3 origRotContainerP2 = Vector3.zero;

    private Vector2 origPosCountdown = Vector2.zero;
    private Vector3 origScaleCountdown = new Vector3(1.32f, 1.32f, 1.32f);
    private Vector3 origRotCountdown = Vector3.zero;

    private Vector2 origPosMiddleWhite = new Vector2(-5f, -7.1f);
    private Vector3 origScaleMiddleWhite = new Vector3(1f, 0.47f, 1f);
    private Vector3 origRotMiddleWhite = new Vector3(0f, 0f, 13.44f);

    private Vector2 origPosDecoratons = Vector2.zero;
    private Vector3 origScaleDecoratons = Vector3.one;
    private Vector3 origRotDecoratons = Vector3.zero;

    private Vector2[] origSilPositions;
    private Vector3[] origSilScales;
    private Vector3[] origSilRotations;

    private Sequence activeSeq;
    private Tween idleFloatAvatar1;
    private Tween idleFloatAvatar2;
    public bool IsInitialized => isInitialized;
    private bool isInitialized = false;

    private void Awake()
    {
        InitializeReferences();
        ApplyResponsivePortraitLayout();
    }

    private void Start()
    {
        RecordRestingTransforms();
    }

    /// <summary>
    /// Finds and connects all references matching the scene hierarchy (both user-custom names and default names).
    /// </summary>
    public void InitializeReferences()
    {
        if (panelMask == null)
        {
            Transform pm = transform.Find("PanelMask");
            panelMask = pm != null ? pm.GetComponent<RectTransform>() : GetComponent<RectTransform>();
        }

        Transform searchRoot = panelMask != null ? panelMask : transform;

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // Backgrounds
        if (bgTopRed == null) bgTopRed = FindChildRect(searchRoot, "Bg_Top_Red") ?? FindChildRect(searchRoot, "Bg_Top");
        if (bgBottomBlue == null) bgBottomBlue = FindChildRect(searchRoot, "Bg_Bottom_Blue") ?? FindChildRect(searchRoot, "Bg_Bottom");

        // White seam divider
        if (middleWhite == null) middleWhite = FindChildRect(searchRoot, "middleWhite") ?? FindChildRect(searchRoot, "MiddleWhite") ?? FindChildRect(searchRoot, "SeamLine");
        if (seamContainer == null) seamContainer = middleWhite != null ? middleWhite : FindChildRect(searchRoot, "SeamContainer");
        if (seamLine == null && seamContainer != null) seamLine = seamContainer.GetComponent<Image>() ?? seamContainer.GetComponentInChildren<Image>();

        // Silhouettes / Decoratons
        if (decoratons == null) decoratons = FindChildRect(searchRoot, "decoratons") ?? FindChildRect(searchRoot, "decorators") ?? FindChildRect(searchRoot, "Decorations");

        if (battleSilhouettes == null || battleSilhouettes.Length == 0)
        {
            Transform silRoot = decoratons != null ? decoratons : (seamContainer != null ? seamContainer : null);
            if (silRoot != null)
            {
                var list = new List<Image>();
                foreach (Transform c in silRoot)
                {
                    Image img = c.GetComponent<Image>();
                    if (img != null) list.Add(img);
                }
                if (list.Count > 0) battleSilhouettes = list.ToArray();
            }
        }

        // Player 2 / Red Opponent
        if (containerPlayer2 == null)
        {
            containerPlayer2 = FindChildRect(searchRoot, "redaavatar") 
                            ?? FindChildRect(searchRoot, "redavatar")
                            ?? FindChildRect(searchRoot, "redaavatar")
                            ?? FindChildRect(searchRoot, "Player2_Red_Container") 
                            ?? FindChildRect(searchRoot, "Character (1)");
        }
        if (containerPlayer2 != null)
        {
            if (avatarRed == null) avatarRed = containerPlayer2.GetComponent<Image>() ?? FindChildComponent<Image>(containerPlayer2, "Avatar_Red");

            Transform np = containerPlayer2.Find("namePlate") ?? containerPlayer2.Find("Nameplate_Red_Assembly") ?? containerPlayer2.Find("Nameplate_Red");
            if (np != null)
            {
                if (nameplateRed == null) nameplateRed = np.GetComponent<Image>() ?? FindChildComponent<Image>(np, "Nameplate_Red_Bg");
                if (leagueShieldRed == null) leagueShieldRed = FindChildComponent<Image>(np, "badge") ?? FindChildComponent<Image>(np, "League_Shield");
                if (levelMedallionRed == null) levelMedallionRed = FindChildComponent<Image>(np, "Level_Medallion");
                if (textPlayer2Name == null) textPlayer2Name = np.GetComponentInChildren<TMP_Text>();
                if (textPlayer2Level == null) textPlayer2Level = FindChildComponent<TMP_Text>(np, "Text_Player2_Level");
            }
            if (textPlayer2Name == null) textPlayer2Name = containerPlayer2.GetComponentInChildren<TMP_Text>();
        }

        // Player 1 / Blue Local Player
        if (containerPlayer1 == null)
        {
            containerPlayer1 = FindChildRect(searchRoot, "blueavatar") 
                            ?? FindChildRect(searchRoot, "Player1_Blue_Container") 
                            ?? FindChildRect(searchRoot, "Character");
        }
        if (containerPlayer1 != null)
        {
            if (avatarBlue == null) avatarBlue = containerPlayer1.GetComponent<Image>() ?? FindChildComponent<Image>(containerPlayer1, "Avatar_Blue");

            Transform np = containerPlayer1.Find("namePlate") ?? containerPlayer1.Find("Nameplate_Blue_Assembly") ?? containerPlayer1.Find("Nameplate_Blue");
            if (np != null)
            {
                if (nameplateBlue == null) nameplateBlue = np.GetComponent<Image>() ?? FindChildComponent<Image>(np, "Nameplate_Blue_Bg");
                if (leagueShieldBlue == null) leagueShieldBlue = FindChildComponent<Image>(np, "badge") ?? FindChildComponent<Image>(np, "League_Shield");
                if (levelMedallionBlue == null) levelMedallionBlue = FindChildComponent<Image>(np, "Level_Medallion");
                if (textPlayer1Name == null) textPlayer1Name = np.GetComponentInChildren<TMP_Text>();
                if (textPlayer1Level == null) textPlayer1Level = FindChildComponent<TMP_Text>(np, "Text_Player1_Level");
            }
            if (textPlayer1Name == null) textPlayer1Name = containerPlayer1.GetComponentInChildren<TMP_Text>();
        }

        // Center Countdown
        if (countdownContainer == null)
        {
            countdownContainer = FindChildRect(searchRoot, "middleText") 
                              ?? FindChildRect(searchRoot, "CenterCountdown") 
                              ?? FindChildRect(searchRoot, "CountdownContainer");
        }
        if (countdownContainer != null)
        {
            if (countdownImage == null) countdownImage = countdownContainer.GetComponent<Image>() ?? countdownContainer.GetComponentInChildren<Image>();
            if (textVS == null) textVS = countdownContainer.GetComponentInChildren<TMP_Text>() ?? FindChildComponent<TMP_Text>(searchRoot, "Text_VS");
        }

        LoadDefaultSpritesIfMissing();
        RecordRestingTransforms();
        isInitialized = true;
    }

    public void LoadDefaultSpritesIfMissing()
    {
        if (spriteCountdown3 == null)
            spriteCountdown3 = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/battlevs/png/countdown_3.png");
        if (spriteCountdown2 == null)
            spriteCountdown2 = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/battlevs/png/countdown_2.png");
        if (spriteCountdown1 == null)
            spriteCountdown1 = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/battlevs/png/countdown_1.png");
        if (spriteCountdownStart == null)
            spriteCountdownStart = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/battlevs/png/countdown_start.png");

        if (spriteLeagueStarterWood == null)
            spriteLeagueStarterWood = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/battlevs/png/league_starter_wood.png");
        if (spriteLeagueSilver == null)
            spriteLeagueSilver = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/battlevs/png/league_silver.png");
        if (spriteLeagueGold == null)
            spriteLeagueGold = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/battlevs/png/league_gold.png");
        if (spriteLeagueLegend == null)
            spriteLeagueLegend = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/battlevs/png/league_legend.png");
        if (spriteLeagueChampion == null)
            spriteLeagueChampion = RuntimeAssets.LoadAssetAtPath<Sprite>("Assets/UIPACK/battlevs/png/league_champion.png");
    }

    /// <summary>
    /// Records the user's manual placement as the resting reference.
    /// Never clobbers manual scene settings.
    /// </summary>
    public void RecordRestingTransforms()
    {
        if (containerPlayer1 != null)
        {
            origPosContainerP1 = containerPlayer1.anchoredPosition;
            if (containerPlayer1.localScale.sqrMagnitude > 0.01f)
                origScaleContainerP1 = containerPlayer1.localScale;
            origRotContainerP1 = containerPlayer1.localEulerAngles;
        }

        if (containerPlayer2 != null)
        {
            origPosContainerP2 = containerPlayer2.anchoredPosition;
            if (containerPlayer2.localScale.sqrMagnitude > 0.01f)
                origScaleContainerP2 = containerPlayer2.localScale;
            origRotContainerP2 = containerPlayer2.localEulerAngles;
        }

        if (countdownContainer != null)
        {
            origPosCountdown = countdownContainer.anchoredPosition;
            if (countdownContainer.localScale.sqrMagnitude > 0.01f)
                origScaleCountdown = countdownContainer.localScale;
            origRotCountdown = countdownContainer.localEulerAngles;
        }

        Transform seam = middleWhite != null ? middleWhite : seamContainer;
        if (seam != null)
        {
            RectTransform srt = seam.GetComponent<RectTransform>();
            if (srt != null)
            {
                origPosMiddleWhite = srt.anchoredPosition;
                if (srt.localScale.sqrMagnitude > 0.01f)
                    origScaleMiddleWhite = srt.localScale;
                origRotMiddleWhite = srt.localEulerAngles;
            }
        }

        if (decoratons != null)
        {
            origPosDecoratons = decoratons.anchoredPosition;
            if (decoratons.localScale.sqrMagnitude > 0.01f)
                origScaleDecoratons = decoratons.localScale;
            origRotDecoratons = decoratons.localEulerAngles;
        }

        if (battleSilhouettes != null && battleSilhouettes.Length > 0)
        {
            origSilPositions = new Vector2[battleSilhouettes.Length];
            origSilScales = new Vector3[battleSilhouettes.Length];
            origSilRotations = new Vector3[battleSilhouettes.Length];

            for (int i = 0; i < battleSilhouettes.Length; i++)
            {
                if (battleSilhouettes[i] != null)
                {
                    origSilPositions[i] = battleSilhouettes[i].rectTransform.anchoredPosition;
                    origSilScales[i] = battleSilhouettes[i].rectTransform.localScale;
                    origSilRotations[i] = battleSilhouettes[i].rectTransform.localEulerAngles;
                }
            }
        }
    }

    /// <summary>
    /// Enforces canvas full-screen stretch on root and PanelMask without altering manually placed children.
    /// </summary>
    public void ApplyResponsivePortraitLayout()
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        if (panelMask != null)
        {
            panelMask.anchorMin = Vector2.zero;
            panelMask.anchorMax = Vector2.one;
            panelMask.offsetMin = Vector2.zero;
            panelMask.offsetMax = Vector2.zero;
            panelMask.anchoredPosition = Vector2.zero;
            panelMask.sizeDelta = Vector2.zero;
            panelMask.localScale = Vector3.one;

            RectMask2D mask = panelMask.GetComponent<RectMask2D>();
            if (mask != null) mask.enabled = true;
        }

        RecordRestingTransforms();
    }

    #region Ultra-Juicy DOTween Animations

    /// <summary>
    /// Entrance slam animation:
    /// - White seam slashes open.
    /// - Silhouettes pop in along the seam.
    /// - Red (opponent) and Blue (local player) slam into resting positions with high-impact combat bounce.
    /// - Ground shockwave shakes the screen.
    /// - Countdown graphic drops with squash & stretch.
    /// - Transitions into idle breathing.
    /// </summary>
    public void PlayEntranceAnimation(Action onComplete = null)
    {
        InitializeReferences();
        KillAllTweens();

        if (canvasGroup != null) canvasGroup.alpha = 1f;
        gameObject.SetActive(true);

        // Prep entrance initial state (outside screen bounds)
        if (containerPlayer2 != null)
        {
            containerPlayer2.anchoredPosition = origPosContainerP2 + new Vector2(0f, 520f);
            containerPlayer2.localScale = origScaleContainerP2;
            containerPlayer2.localEulerAngles = origRotContainerP2;
        }

        if (containerPlayer1 != null)
        {
            containerPlayer1.anchoredPosition = origPosContainerP1 - new Vector2(0f, 520f);
            containerPlayer1.localScale = origScaleContainerP1;
            containerPlayer1.localEulerAngles = origRotContainerP1;
        }

        Transform seam = middleWhite != null ? middleWhite : seamContainer;
        if (seam != null)
        {
            seam.localScale = new Vector3(0.001f, origScaleMiddleWhite.y, origScaleMiddleWhite.z);
        }

        if (decoratons != null)
        {
            decoratons.localScale = Vector3.zero;
        }

        if (countdownContainer != null)
        {
            countdownContainer.localScale = Vector3.zero;
        }

        if (countdownImage != null && spriteCountdown3 != null)
        {
            countdownImage.sprite = spriteCountdown3;
            countdownImage.gameObject.SetActive(true);
        }
        if (textVS != null) textVS.gameObject.SetActive(false);

        activeSeq = DOTween.Sequence().SetUpdate(true);

        // 1. Slashes open the white diagonal seam (0.28s)
        if (seam != null)
        {
            activeSeq.Append(seam.DOScaleX(origScaleMiddleWhite.x, 0.28f).SetEase(Ease.OutBack, 1.4f));
        }

        // 2. Silhouettes pop in (0.24s)
        if (decoratons != null)
        {
            activeSeq.Join(decoratons.DOScale(origScaleDecoratons, 0.24f).SetEase(Ease.OutBack, 1.6f));
        }

        // 3. Combatants slam into arena with combat bounce (0.40s)
        if (containerPlayer2 != null)
        {
            activeSeq.Join(containerPlayer2.DOAnchorPosY(origPosContainerP2.y, 0.40f).SetEase(Ease.OutBack, 1.35f));
        }
        if (containerPlayer1 != null)
        {
            activeSeq.Join(containerPlayer1.DOAnchorPosY(origPosContainerP1.y, 0.40f).SetEase(Ease.OutBack, 1.35f));
        }

        // 4. Ground slam impact screen shake
        activeSeq.AppendCallback(() =>
        {
            transform.DOShakePosition(0.22f, new Vector3(8f, 10f, 0f), 18, 90f, false, true).SetUpdate(true);
        });

        // 5. Center countdown graphic drops with squash & stretch (0.34s)
        if (countdownContainer != null)
        {
            activeSeq.Append(countdownContainer.DOScale(origScaleCountdown * 1.55f, 0.18f).SetEase(Ease.OutQuad));
            activeSeq.Append(countdownContainer.DOScale(origScaleCountdown, 0.16f).SetEase(Ease.OutBounce));
        }

        // 6. Complete & Start idle breathing
        activeSeq.OnComplete(() =>
        {
            StartIdleBreathing();
            onComplete?.Invoke();
        });
    }

    /// <summary>
    /// Organic idle breathing sway on combatants while waiting.
    /// </summary>
    public void StartIdleBreathing()
    {
        if (idleFloatAvatar1 != null && idleFloatAvatar1.IsActive()) idleFloatAvatar1.Kill();
        if (idleFloatAvatar2 != null && idleFloatAvatar2.IsActive()) idleFloatAvatar2.Kill();

        if (containerPlayer1 != null)
        {
            idleFloatAvatar1 = containerPlayer1
                .DOAnchorPosY(origPosContainerP1.y - 4f, 1.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        if (containerPlayer2 != null)
        {
            idleFloatAvatar2 = containerPlayer2
                .DOAnchorPosY(origPosContainerP2.y + 4f, 1.4f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }
    }

    /// <summary>
    /// Plays a clean pop/pump animation strictly on the countdown number/graphic (3 -> 2 -> 1 -> START!).
    /// The player and opponent icons remain steady without any bump or pulse distortion.
    /// </summary>
    public void PlayCountdownStep(string stepText, Color? customColor = null)
    {
        if (countdownContainer == null) return;

        Sprite targetSprite = null;
        string cleanText = stepText != null ? stepText.Trim().ToUpperInvariant() : "";

        if (cleanText == "3") targetSprite = spriteCountdown3;
        else if (cleanText == "2") targetSprite = spriteCountdown2;
        else if (cleanText == "1") targetSprite = spriteCountdown1;
        else if (cleanText.Contains("START") || cleanText.Contains("BATTLE")) targetSprite = spriteCountdownStart;

        if (countdownImage != null)
        {
            if (targetSprite != null)
            {
                countdownImage.sprite = targetSprite;
                countdownImage.gameObject.SetActive(true);
                if (textVS != null) textVS.gameObject.SetActive(false);
            }
            else
            {
                countdownImage.gameObject.SetActive(false);
                if (textVS != null)
                {
                    textVS.gameObject.SetActive(true);
                    textVS.text = stepText;
                    if (customColor.HasValue) textVS.color = customColor.Value;
                }
            }
        }

        bool isStart = cleanText.Contains("START") || cleanText.Contains("BATTLE");

        // Clean number pump animation: scales up and snaps cleanly to resting scale
        countdownContainer.DOKill();
        countdownContainer.localEulerAngles = origRotCountdown;
        float popMultiplier = isStart ? 1.75f : 1.5f;
        countdownContainer.localScale = origScaleCountdown * popMultiplier;
        countdownContainer.DOScale(origScaleCountdown, isStart ? 0.45f : 0.35f)
            .SetEase(Ease.OutBack, isStart ? 2.5f : 1.8f)
            .SetUpdate(true);

        // Keep player/opponent icons steady at their resting scale (no pulse / bumping)
        if (containerPlayer1 != null) containerPlayer1.localScale = origScaleContainerP1;
        if (containerPlayer2 != null) containerPlayer2.localScale = origScaleContainerP2;
    }

    /// <summary>
    /// Reacts with impact when opponent connects in online multiplayer.
    /// </summary>
    public void PunchOpponentJoined(string opponentName)
    {
        SetPlayer2(opponentName, "CONNECTED");

        if (containerPlayer2 != null)
        {
            containerPlayer2.DOKill();
            containerPlayer2.localScale = origScaleContainerP2;
            containerPlayer2.DOPunchScale(origScaleContainerP2 * 0.25f, 0.45f, 8, 0.6f).SetUpdate(true);
            containerPlayer2.DOPunchRotation(new Vector3(0f, 0f, 8f), 0.40f, 7, 0.6f).SetUpdate(true);
        }

        transform.DOShakePosition(0.18f, new Vector3(5f, 6f, 0f), 14, 90f, false, true).SetUpdate(true);
    }

    /// <summary>
    /// Breakaway blast into the 3D battlefield:
    /// - START! zooms into camera.
    /// - Red section blasts up, Blue section blasts down.
    /// - Seam line slashes shut.
    /// - Smoothly fades out and starts the match.
    /// </summary>
    public void PlayDismissAnimation(Action onComplete = null)
    {
        KillAllTweens();

        Sequence dismissSeq = DOTween.Sequence().SetUpdate(true);

        // 1. Zoom START! graphic into camera
        if (countdownContainer != null)
        {
            dismissSeq.Append(countdownContainer.DOScale(origScaleCountdown * 3.2f, 0.28f).SetEase(Ease.InQuad));
        }

        // 2. Breakaway slices
        if (containerPlayer2 != null)
        {
            dismissSeq.Join(containerPlayer2.DOAnchorPosY(origPosContainerP2.y + 650f, 0.35f).SetEase(Ease.InBack, 1.35f));
        }
        if (containerPlayer1 != null)
        {
            dismissSeq.Join(containerPlayer1.DOAnchorPosY(origPosContainerP1.y - 650f, 0.35f).SetEase(Ease.InBack, 1.35f));
        }

        Transform seam = middleWhite != null ? middleWhite : seamContainer;
        if (seam != null)
        {
            dismissSeq.Join(seam.DOScaleX(0.001f, 0.26f).SetEase(Ease.InQuad));
        }
        if (decoratons != null)
        {
            dismissSeq.Join(decoratons.DOScale(Vector3.zero, 0.25f).SetEase(Ease.InBack));
        }

        if (canvasGroup != null)
        {
            dismissSeq.Join(canvasGroup.DOFade(0f, 0.30f).SetEase(Ease.InQuad));
        }

        dismissSeq.OnComplete(() =>
        {
            gameObject.SetActive(false);
            onComplete?.Invoke();
        });
    }

    /// <summary>
    /// Pure DOTween full sequence: 3 -> 2 -> 1 -> START! -> Dismiss.
    /// Operates identically in Play Mode and directly in Edit Mode without playing!
    /// </summary>
    public void PlayCountdownSequence(Action onComplete = null)
    {
        KillAllTweens();

        if (canvasGroup != null) canvasGroup.alpha = 1f;
        gameObject.SetActive(true);

        activeSeq = DOTween.Sequence().SetUpdate(true);
        activeSeq.AppendCallback(() => PlayCountdownStep("3"));
        activeSeq.AppendInterval(1.0f);
        activeSeq.AppendCallback(() => PlayCountdownStep("2"));
        activeSeq.AppendInterval(1.0f);
        activeSeq.AppendCallback(() => PlayCountdownStep("1"));
        activeSeq.AppendInterval(1.0f);
        activeSeq.AppendCallback(() => PlayCountdownStep("START!"));
        activeSeq.AppendInterval(0.65f);
        activeSeq.AppendCallback(() => PlayDismissAnimation(onComplete));
    }

    public void ResetToRestingState()
    {
        KillAllTweens();

        if (canvasGroup != null) canvasGroup.alpha = 1f;
        transform.localScale = Vector3.one;

        if (containerPlayer1 != null)
        {
            containerPlayer1.anchoredPosition = origPosContainerP1;
            containerPlayer1.localScale = origScaleContainerP1;
            containerPlayer1.localEulerAngles = origRotContainerP1;
        }

        if (containerPlayer2 != null)
        {
            containerPlayer2.anchoredPosition = origPosContainerP2;
            containerPlayer2.localScale = origScaleContainerP2;
            containerPlayer2.localEulerAngles = origRotContainerP2;
        }

        if (countdownContainer != null)
        {
            countdownContainer.anchoredPosition = origPosCountdown;
            countdownContainer.localScale = origScaleCountdown;
            countdownContainer.localEulerAngles = origRotCountdown;
        }

        Transform seam = middleWhite != null ? middleWhite : seamContainer;
        if (seam != null)
        {
            RectTransform srt = seam.GetComponent<RectTransform>();
            if (srt != null)
            {
                srt.anchoredPosition = origPosMiddleWhite;
                srt.localScale = origScaleMiddleWhite;
                srt.localEulerAngles = origRotMiddleWhite;
            }
        }

        if (decoratons != null)
        {
            decoratons.anchoredPosition = origPosDecoratons;
            decoratons.localScale = origScaleDecoratons;
            decoratons.localEulerAngles = origRotDecoratons;
        }

        if (battleSilhouettes != null && origSilScales != null)
        {
            for (int i = 0; i < battleSilhouettes.Length && i < origSilScales.Length; i++)
            {
                if (battleSilhouettes[i] != null)
                {
                    RectTransform srt = battleSilhouettes[i].rectTransform;
                    srt.anchoredPosition = origSilPositions[i];
                    srt.localScale = origSilScales[i];
                    srt.localEulerAngles = origSilRotations[i];
                }
            }
        }

        if (countdownImage != null && spriteCountdown3 != null)
        {
            countdownImage.sprite = spriteCountdown3;
            countdownImage.gameObject.SetActive(true);
        }
        if (textVS != null) textVS.gameObject.SetActive(false);
    }

    public void SetPlayer1(string nickname, string subtitle = "COMMANDER")
    {
        if (textPlayer1Name != null && !string.IsNullOrEmpty(nickname))
        {
            textPlayer1Name.text = nickname;
        }
        if (textPlayer1Level != null && !string.IsNullOrEmpty(subtitle))
        {
            if (subtitle.Contains("Lv.")) textPlayer1Level.text = subtitle.Replace("Lv.", "").Trim();
            else if (int.TryParse(subtitle, out int parsed)) textPlayer1Level.text = parsed.ToString();
            else textPlayer1Level.text = "15";
        }
    }

    public void SetPlayer2(string nickname, string subtitle = "WAITING...")
    {
        if (textPlayer2Name != null && !string.IsNullOrEmpty(nickname))
        {
            textPlayer2Name.text = nickname;
        }
        if (textPlayer2Level != null && !string.IsNullOrEmpty(subtitle))
        {
            if (subtitle.Contains("Lv.")) textPlayer2Level.text = subtitle.Replace("Lv.", "").Trim();
            else if (int.TryParse(subtitle, out int parsed)) textPlayer2Level.text = parsed.ToString();
            else if (subtitle.Contains("WAITING")) textPlayer2Level.text = "?";
            else textPlayer2Level.text = "12";
        }
    }

    public void SetPlayer1Avatar(Sprite avatar)
    {
        if (avatarBlue != null && avatar != null) avatarBlue.sprite = avatar;
    }

    public void SetPlayer2Avatar(Sprite avatar)
    {
        if (avatarRed != null && avatar != null) avatarRed.sprite = avatar;
    }

    public void SetPlayer1League(Sprite leagueSprite)
    {
        if (leagueShieldBlue != null && leagueSprite != null)
            leagueShieldBlue.sprite = leagueSprite;
    }

    public void SetPlayer2League(Sprite leagueSprite)
    {
        if (leagueShieldRed != null && leagueSprite != null)
            leagueShieldRed.sprite = leagueSprite;
    }

    private void KillAllTweens()
    {
        if (activeSeq != null && activeSeq.IsActive()) activeSeq.Kill();
        if (idleFloatAvatar1 != null && idleFloatAvatar1.IsActive()) idleFloatAvatar1.Kill();
        if (idleFloatAvatar2 != null && idleFloatAvatar2.IsActive()) idleFloatAvatar2.Kill();

        transform.DOKill();
        if (containerPlayer1 != null) containerPlayer1.DOKill();
        if (containerPlayer2 != null) containerPlayer2.DOKill();
        if (countdownContainer != null) countdownContainer.DOKill();
        if (middleWhite != null) middleWhite.DOKill();
        if (seamContainer != null) seamContainer.DOKill();
        if (decoratons != null) decoratons.DOKill();
        if (canvasGroup != null) canvasGroup.DOKill();

        if (battleSilhouettes != null)
        {
            foreach (var s in battleSilhouettes)
            {
                if (s != null) s.rectTransform.DOKill();
            }
        }
    }

    private void OnDestroy()
    {
        KillAllTweens();
    }

    #endregion

    #region Editor Test Methods (Works in Play & Edit Mode)

    public void EditorTestEntrance()
    {
        PlayEntranceAnimation();
    }

    public void EditorTestOpponentJoined()
    {
        PunchOpponentJoined("Tabaja");
    }

    public void EditorTestCountdown()
    {
        PlayCountdownSequence();
    }

    public void EditorTestDismiss()
    {
        PlayDismissAnimation();
    }

    public void EditorResetLayout()
    {
        ResetToRestingState();
    }

    #endregion

    #region Helpers

    private RectTransform FindChildRect(Transform parent, string childName)
    {
        if (parent == null) return null;
        Transform t = parent.Find(childName);
        return t != null ? t.GetComponent<RectTransform>() : null;
    }

    private T FindChildComponent<T>(Transform parent, string childName) where T : Component
    {
        if (parent == null) return null;
        Transform t = parent.Find(childName);
        return t != null ? t.GetComponent<T>() : null;
    }

    #endregion
}
