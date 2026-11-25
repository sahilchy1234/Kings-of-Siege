using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

/// <summary>
/// Ultra-juicy, AAA mobile UI animation and transition controller powered by DOTween.
/// Orchestrates:
/// - Smooth modal pop entrance and exit with elastic bounce and canvas group fading.
/// - Fluid transitions between MainPanel and MatchmakingPanel (no abrupt SetActive pops).
/// - Tactile squash & stretch button feedback with overshoot spring bounce.
/// - Living idle animations: rotating radial aura glow, breathing centerpiece swords & shield,
///   pulsing VS emblem, and breathing multiplayer CTA button.
/// - Dynamic matchmaking animated dots ("Searching for match.", "..", "...") and live elapsed timer.
/// - Vertical Slot Reel / Arena Wheel: Continuous masked conveyor-belt reel where Avatar A slides upward
///   while Avatar B enters from below with vertical motion-stretch (scaleY: 1.15).
/// - Dramatic Match Found Deceleration: The reel slows down (tick... tick... SLAM!) onto the confirmed
///   opponent with an elastic spring bounce!
/// </summary>
[DisallowMultipleComponent]
public class JuicyMainMenuUI : MonoBehaviour
{
    private static JuicyMainMenuUI instance;
    public static JuicyMainMenuUI Instance => instance;

    [Header("Panel Containers")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private RectTransform mainCardRect;
    [SerializeField] private CanvasGroup mainCanvasGroup;

    [SerializeField] private GameObject matchmakingPanel;
    [SerializeField] private RectTransform matchmakingCardRect;
    [SerializeField] private CanvasGroup matchmakingCanvasGroup;

    [Header("Interactive Buttons for Tactile Juice")]
    [SerializeField] private Button playMultiplayerButton;
    [SerializeField] private Button playVsAIButton;
    [SerializeField] private Button cancelMatchmakingButton;

    [Header("Matchmaking Visual Elements")]
    [SerializeField] private RectTransform auraGlowRect;
    [SerializeField] private RectTransform swordsCenterpieceRect;
    [SerializeField] private RectTransform vsBadgeRect;
    [SerializeField] private RectTransform playerAvatarRect;
    [SerializeField] private RectTransform opponentCardRect;
    [SerializeField] private RectTransform opponentAvatarRect;
    [SerializeField] private Image opponentAvatarImage;
    [SerializeField] private RectTransform opponentEnergyGlowRect;
    [SerializeField] private TMP_Text opponentNameText;
    [SerializeField] private TMP_Text opponentRoleText;
    [SerializeField] private TMP_Text playerNicknameText;
    [SerializeField] private TMP_Text matchmakingStatusText;
    private TMP_Text matchTipsLabel;
    private GameObject matchTipsRoot;
    private CanvasGroup matchTipsGroup;
    private RectTransform matchTipsProgressFill;
    private const float MatchTipHoldSeconds = 2.5f;
    [SerializeField] private TMP_Text matchmakingTimerText;

    [Header("Vertical Slot Reel System (Arcade / Casino)")]
    [SerializeField] private RectTransform reelViewport;
    [SerializeField] private RectTransform reelAvatarA;
    [SerializeField] private RectTransform reelAvatarB;
    [SerializeField] private Image reelAvatarImageA;
    [SerializeField] private Image reelAvatarImageB;
    [SerializeField, Range(80f, 160f)] private float slotTravelDistance = 115f;
    [SerializeField, Range(0.06f, 0.25f)] private float reelStepDuration = 0.11f;
    [SerializeField, Range(1.05f, 1.30f)] private float motionStretchY = 1.15f;

    [Header("Candidate Pool Tuning")]
    [Tooltip("Pool of candidate avatar portraits cycled through the slot reel.")]
    [SerializeField] private Sprite[] candidateAvatars;
    [Tooltip("Thematic candidate rival titles displayed during search.")]
    [SerializeField] private string[] candidateNames;
    [Tooltip("Fallback sprite when idling or reset.")]
    [SerializeField] private Sprite defaultOpponentSprite;
    [Tooltip("Confirmed opponent avatar revealed on Match Found.")]
    [SerializeField] private Sprite confirmedOpponentSprite;
    [Tooltip("Whether to cycle candidate rival titles alongside avatars.")]
    [SerializeField] private bool cycleCandidateNames = true;

    [Header("Status & Profile Elements")]
    [SerializeField] private Image connectionDot;
    [SerializeField] private TMP_Text connectionStatusText;
    [SerializeField] private TMP_InputField nicknameInputField;

    // Tween handles
    private Tween auraRotateTween;
    private Tween swordsBreatheTween;
    private Tween vsPulseTween;
    private Tween multiBtnPulseTween;
    private Tween dotPulseTween;
    private Tween opponentGlowRotateTween;
    private Coroutine matchmakingTimerCoroutine;
    private Coroutine dotAnimCoroutine;
    private Coroutine avatarShuffleCoroutine;
    private Coroutine decelerationCoroutine;

    private float matchmakingStartTime;
    private bool isMatchFound = false;
    private int currentAvatarIndex = 0;
    private bool isUsingAvatarA = true;

    // Resting transforms for zero layout drift
    private Vector2 nameTextRestingPos = new Vector2(0f, 75.70f);
    private Vector3 glowOriginalScale = Vector3.one;

    private static readonly string[] DefaultCandidateTitles = new string[]
    {
        "Red Commander",
        "Shadow Assassin",
        "Mounted Knight",
        "Royal Spearman",
        "Iron Vanguard",
        "Highland Monarch",
        "Elven Ranger",
        "Grand Paladin",
        "Imperial Guardian",
        "Crimson Sorceress",
        "Shadow Trickster",
        "Arena Champion"
    };

    private void Awake()
    {
        instance = this;
        EnsureCanvasGroups();
        EnsureSlotReelReferences();
        CacheRestingTransforms();

        if (candidateNames == null || candidateNames.Length == 0)
        {
            candidateNames = DefaultCandidateTitles;
        }
    }

    private void EnsureSlotReelReferences()
    {
        if (opponentCardRect == null)
        {
            var matchPanelTransform = matchmakingPanel != null ? matchmakingPanel.transform : transform.Find("MatchmakingPanel");
            if (matchPanelTransform != null)
            {
                var oppCard = matchPanelTransform.Find("MatchCard/ShowdownContainer/OpponentCard");
                if (oppCard != null) opponentCardRect = oppCard.GetComponent<RectTransform>();
            }
        }

        if (opponentCardRect != null)
        {
            var oppFrame = opponentCardRect.Find("AvatarFrame");
            if (oppFrame != null)
            {
                if (reelViewport == null)
                {
                    var vp = oppFrame.Find("ReelViewport");
                    if (vp != null) reelViewport = vp.GetComponent<RectTransform>();
                }
                if (opponentEnergyGlowRect == null)
                {
                    var glow = oppFrame.Find("AvatarIcon (1)");
                    if (glow != null) opponentEnergyGlowRect = glow.GetComponent<RectTransform>();
                }
            }
        }

        if (reelViewport != null)
        {
            if (reelAvatarA == null)
            {
                var a = reelViewport.Find("ReelAvatarA");
                if (a != null) reelAvatarA = a.GetComponent<RectTransform>();
            }
            if (reelAvatarB == null)
            {
                var b = reelViewport.Find("ReelAvatarB");
                if (b != null) reelAvatarB = b.GetComponent<RectTransform>();
            }

            if (reelAvatarA != null && reelAvatarImageA == null)
            {
                reelAvatarImageA = reelAvatarA.GetComponent<Image>();
            }
            if (reelAvatarB != null && reelAvatarImageB == null)
            {
                reelAvatarImageB = reelAvatarB.GetComponent<Image>();
            }
        }
    }

    private void CacheRestingTransforms()
    {
        if (opponentNameText != null)
        {
            nameTextRestingPos = opponentNameText.rectTransform.anchoredPosition;
            if (Mathf.Abs(nameTextRestingPos.y) < 1f)
            {
                nameTextRestingPos = new Vector2(0f, 75.70f);
                opponentNameText.rectTransform.anchoredPosition = nameTextRestingPos;
            }
        }

        if (opponentEnergyGlowRect != null)
        {
            glowOriginalScale = opponentEnergyGlowRect.localScale;
        }
    }

    private void Start()
    {
        SetupTactileButtons();
        StartIdleAnimations();

        if (mainPanel != null && mainPanel.activeSelf)
        {
            PlayCardEntrance(mainCardRect, mainCanvasGroup);
        }
    }

    private void EnsureCanvasGroups()
    {
        if (mainPanel != null && mainCanvasGroup == null)
        {
            mainCanvasGroup = mainPanel.GetComponent<CanvasGroup>();
            if (mainCanvasGroup == null) mainCanvasGroup = mainPanel.AddComponent<CanvasGroup>();
        }
        if (matchmakingPanel != null && matchmakingCanvasGroup == null)
        {
            matchmakingCanvasGroup = matchmakingPanel.GetComponent<CanvasGroup>();
            if (matchmakingCanvasGroup == null) matchmakingCanvasGroup = matchmakingPanel.AddComponent<CanvasGroup>();
        }
    }

    private void SetupTactileButtons()
    {
        AttachTactileJuice(playMultiplayerButton, 0.93f);
        AttachTactileJuice(playVsAIButton, 0.93f);
        AttachTactileJuice(cancelMatchmakingButton, 0.92f);
    }

    public static void AttachTactileJuice(Button btn, float pressScale = 0.92f)
    {
        if (btn == null) return;
        JuicyButtonHandler handler = btn.gameObject.GetComponent<JuicyButtonHandler>();
        if (handler == null)
        {
            handler = btn.gameObject.AddComponent<JuicyButtonHandler>();
            handler.Initialize(pressScale);
        }
    }

    #region Living Idle Animations

    private void StartIdleAnimations()
    {
        if (auraGlowRect != null)
        {
            auraRotateTween = auraGlowRect.DORotate(new Vector3(0f, 0f, -360f), 14f, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart)
                .SetUpdate(true);
        }

        if (swordsCenterpieceRect != null)
        {
            swordsBreatheTween = swordsCenterpieceRect.DOScale(swordsCenterpieceRect.localScale * 1.08f, 1.25f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        if (vsBadgeRect != null)
        {
            vsPulseTween = vsBadgeRect.DOScale(vsBadgeRect.localScale * 1.15f, 0.85f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        if (playMultiplayerButton != null)
        {
            RectTransform btnRect = playMultiplayerButton.GetComponent<RectTransform>();
            if (btnRect != null)
            {
                multiBtnPulseTween = btnRect.DOScale(btnRect.localScale * 1.035f, 1.1f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true);
            }
        }

        if (connectionDot != null)
        {
            dotPulseTween = connectionDot.transform.DOScale(Vector3.one * 1.28f, 0.8f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }
    }

    #endregion

    #region Panel Transitions

    /// <summary>Shows the tip plaque over the background after the matchmaking card has faded out, holds it, then continues to the battle.</summary>
    private void ShowMatchTipsThen(Action done)
    {
        EnsureMatchTipsLabel();
        if (matchTipsRoot == null)
        {
            done?.Invoke();
            return;
        }

        matchTipsRoot.transform.SetAsLastSibling();
        matchTipsRoot.SetActive(true);

        RectTransform rootRect = (RectTransform)matchTipsRoot.transform;
        rootRect.DOKill();
        matchTipsGroup.DOKill();
        rootRect.localScale = Vector3.one * 0.85f;
        matchTipsGroup.alpha = 0f;

        Sequence intro = DOTween.Sequence().SetUpdate(true);
        intro.Append(matchTipsGroup.DOFade(1f, 0.3f));
        intro.Join(rootRect.DOScale(1f, 0.45f).SetEase(Ease.OutBack));

        if (matchTipsProgressFill != null)
        {
            matchTipsProgressFill.DOKill();
            matchTipsProgressFill.anchorMax = new Vector2(0f, 1f);
            matchTipsProgressFill.DOAnchorMax(new Vector2(1f, 1f), MatchTipHoldSeconds)
                .SetEase(Ease.InOutSine).SetUpdate(true);
        }

        StartCoroutine(HoldMatchTipsThen(done));
    }

    private IEnumerator HoldMatchTipsThen(Action done)
    {
        yield return new WaitForSecondsRealtime(MatchTipHoldSeconds);
        done?.Invoke();
    }

    /// <summary>
    /// Builds the battle-tip plaque: navy gradient panel with a bevelled gold frame, a gold "BATTLE TIP" ribbon
    /// on the top edge, the rotating tip text, and a gold loading bar. Sprites are generated at runtime.
    /// </summary>
    private void EnsureMatchTipsLabel()
    {
        if (matchTipsRoot != null || matchmakingPanel == null) return;
        Transform canvasRoot = matchmakingPanel.transform.parent;
        if (canvasRoot == null) return;

        TMP_FontAsset titleFont = matchmakingStatusText != null ? matchmakingStatusText.font : null;

        Sprite plaqueSprite = BuildRoundedSprite(128, 26, 7,
            new Color(0.13f, 0.19f, 0.38f), new Color(0.04f, 0.06f, 0.15f),
            new Color(1f, 0.88f, 0.45f), new Color(0.70f, 0.45f, 0.10f),
            new Color(0.02f, 0.03f, 0.08f));
        Sprite ribbonSprite = BuildRoundedSprite(96, 28, 4,
            new Color(1f, 0.87f, 0.42f), new Color(0.86f, 0.56f, 0.14f),
            new Color(0.48f, 0.27f, 0.06f), new Color(0.30f, 0.16f, 0.03f),
            new Color(1f, 0.96f, 0.75f));
        Sprite barSprite = BuildRoundedSprite(40, 14, 0,
            Color.white, Color.white, Color.white, Color.white, Color.white);

        // Plaque
        matchTipsRoot = new GameObject("MatchLoadingTips", typeof(RectTransform));
        matchTipsRoot.transform.SetParent(canvasRoot, false);
        RectTransform rootRect = (RectTransform)matchTipsRoot.transform;
        rootRect.anchorMin = new Vector2(0.06f, 0.09f);
        rootRect.anchorMax = new Vector2(0.94f, 0.25f);
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        matchTipsGroup = matchTipsRoot.AddComponent<CanvasGroup>();
        matchTipsGroup.blocksRaycasts = false;
        matchTipsGroup.interactable = false;

        Image plate = matchTipsRoot.AddComponent<Image>();
        plate.sprite = plaqueSprite;
        plate.type = Image.Type.Sliced;
        plate.raycastTarget = false;

        // Ribbon header straddling the top edge
        GameObject ribbonObj = new GameObject("TipRibbon", typeof(RectTransform));
        ribbonObj.transform.SetParent(matchTipsRoot.transform, false);
        RectTransform ribbonRect = (RectTransform)ribbonObj.transform;
        ribbonRect.anchorMin = new Vector2(0.5f, 1f);
        ribbonRect.anchorMax = new Vector2(0.5f, 1f);
        ribbonRect.pivot = new Vector2(0.5f, 0.5f);
        ribbonRect.sizeDelta = new Vector2(340f, 64f);
        ribbonRect.anchoredPosition = new Vector2(0f, 4f);
        Image ribbon = ribbonObj.AddComponent<Image>();
        ribbon.sprite = ribbonSprite;
        ribbon.type = Image.Type.Sliced;
        ribbon.raycastTarget = false;

        TextMeshProUGUI header = CreateTipText("TipHeader", ribbonObj.transform, titleFont, "BATTLE TIP", 28f,
            new Color(0.24f, 0.12f, 0.02f, 1f));
        header.characterSpacing = 8f;
        Stretch((RectTransform)header.transform, new Vector2(0f, 0f), new Vector2(1f, 1f));

        // Rotating tip body
        matchTipsLabel = CreateTipText("TipBody", matchTipsRoot.transform, titleFont, "", 40f,
            new Color(1f, 0.96f, 0.86f, 0f));
        matchTipsLabel.enableWordWrapping = true;
        matchTipsLabel.lineSpacing = -6f;
        matchTipsLabel.outlineWidth = 0.14f;
        matchTipsLabel.outlineColor = new Color32(8, 12, 28, 255);
        Stretch((RectTransform)matchTipsLabel.transform, new Vector2(0.06f, 0.30f), new Vector2(0.94f, 0.80f));
        matchTipsLabel.gameObject.AddComponent<MatchLoadingTipRotator>();

        // Gold loading bar
        GameObject barBgObj = new GameObject("TipProgressBg", typeof(RectTransform));
        barBgObj.transform.SetParent(matchTipsRoot.transform, false);
        Stretch((RectTransform)barBgObj.transform, new Vector2(0.14f, 0.11f), new Vector2(0.86f, 0.19f));
        Image barBg = barBgObj.AddComponent<Image>();
        barBg.sprite = barSprite;
        barBg.type = Image.Type.Sliced;
        barBg.color = new Color(0f, 0f, 0f, 0.55f);
        barBg.raycastTarget = false;

        GameObject fillObj = new GameObject("TipProgressFill", typeof(RectTransform));
        fillObj.transform.SetParent(barBgObj.transform, false);
        matchTipsProgressFill = (RectTransform)fillObj.transform;
        Stretch(matchTipsProgressFill, new Vector2(0f, 0f), new Vector2(0f, 1f));
        Image fill = fillObj.AddComponent<Image>();
        fill.sprite = barSprite;
        fill.type = Image.Type.Sliced;
        fill.color = new Color(1f, 0.80f, 0.30f, 1f);
        fill.raycastTarget = false;

        matchTipsRoot.SetActive(false);
    }

    private static TextMeshProUGUI CreateTipText(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI t = obj.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = color;
        t.raycastTarget = false;
        return t;
    }

    private static void Stretch(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>Anti-aliased rounded-rect sprite with a vertical-gradient fill, a bevelled border and a thin inner line, 9-sliced.</summary>
    private static Sprite BuildRoundedSprite(int size, int radius, int border,
        Color fillTop, Color fillBottom, Color borderTop, Color borderBottom, Color innerLine)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        float half = size * 0.5f;
        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            float v = (y + 0.5f) / size;
            for (int x = 0; x < size; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                float dist = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                float depth = -dist;
                float alpha = Mathf.Clamp01(depth + 0.5f);

                Color c;
                if (depth < border)
                {
                    c = Color.Lerp(borderBottom, borderTop, v);
                    // Bevel highlight along the outer edge
                    c = Color.Lerp(c, Color.white, Mathf.Clamp01(1f - depth / Mathf.Max(1f, border)) * 0.18f);
                }
                else if (border > 0 && depth < border + 2f)
                {
                    c = innerLine;
                }
                else
                {
                    c = Color.Lerp(fillBottom, fillTop, v);
                }
                c.a *= alpha;
                px[y * size + x] = c;
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        float slice = radius + border + 2f;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(slice, slice, slice, slice));
    }

    public void TransitionToMatchmaking()
    {
        isMatchFound = false;

        EnsureMatchTipsLabel();

        if (playerNicknameText != null && nicknameInputField != null)
        {
            playerNicknameText.text = nicknameInputField.text;
        }

        if (opponentNameText != null)
        {
            opponentNameText.text = "Searching...";
            opponentNameText.color = Color.white;
            opponentNameText.rectTransform.anchoredPosition = nameTextRestingPos;
        }

        // Start continuous vertical slot reel loop
        StartAvatarShuffleLoop();

        EnsureCanvasGroups();

        if (mainCardRect != null && mainCanvasGroup != null)
        {
            mainCardRect.DOKill();
            mainCanvasGroup.DOKill();

            Sequence outSeq = DOTween.Sequence().SetUpdate(true);
            outSeq.Append(mainCardRect.DOScale(Vector3.one * 0.88f, 0.22f).SetEase(Ease.InBack));
            outSeq.Join(mainCanvasGroup.DOFade(0f, 0.20f));
            outSeq.OnComplete(() =>
            {
                if (mainPanel != null) mainPanel.SetActive(false);
            });
        }
        else if (mainPanel != null)
        {
            mainPanel.SetActive(false);
        }

        if (matchmakingPanel != null)
        {
            matchmakingPanel.SetActive(true);

            JuicyPopup matchPopup = matchmakingCardRect != null ? matchmakingCardRect.GetComponent<JuicyPopup>() : null;
            if (matchPopup != null)
            {
                matchPopup.PlayPopIn();
            }
            else if (matchmakingCardRect != null && matchmakingCanvasGroup != null)
            {
                matchmakingCardRect.DOKill();
                matchmakingCanvasGroup.DOKill();

                matchmakingCardRect.localScale = Vector3.one * 0.72f;
                matchmakingCanvasGroup.alpha = 0f;

                Sequence inSeq = DOTween.Sequence().SetUpdate(true);
                inSeq.Append(matchmakingCardRect.DOScale(Vector3.one, 0.45f).SetEase(Ease.OutBack, 1.25f));
                inSeq.Join(matchmakingCanvasGroup.DOFade(1f, 0.28f));
            }
        }

        matchmakingStartTime = Time.unscaledTime;
        if (matchmakingTimerCoroutine != null) StopCoroutine(matchmakingTimerCoroutine);
        matchmakingTimerCoroutine = StartCoroutine(MatchmakingTimerRoutine());

        if (dotAnimCoroutine != null) StopCoroutine(dotAnimCoroutine);
        dotAnimCoroutine = StartCoroutine(DotAnimationRoutine());
    }

    public void TransitionToMainPanel()
    {
        StopAvatarShuffleLoop();

        if (decelerationCoroutine != null)
        {
            StopCoroutine(decelerationCoroutine);
            decelerationCoroutine = null;
        }

        if (matchmakingTimerCoroutine != null)
        {
            StopCoroutine(matchmakingTimerCoroutine);
            matchmakingTimerCoroutine = null;
        }
        if (dotAnimCoroutine != null)
        {
            StopCoroutine(dotAnimCoroutine);
            dotAnimCoroutine = null;
        }

        // Reset slot reel avatars
        ResetReelVisuals();

        if (opponentNameText != null)
        {
            opponentNameText.text = "Searching...";
            opponentNameText.color = Color.white;
            opponentNameText.rectTransform.anchoredPosition = nameTextRestingPos;
        }

        EnsureCanvasGroups();

        JuicyPopup matchPopup = matchmakingCardRect != null ? matchmakingCardRect.GetComponent<JuicyPopup>() : null;
        if (matchPopup != null)
        {
            matchPopup.PlayPopOut(() =>
            {
                if (matchmakingPanel != null) matchmakingPanel.SetActive(false);
                RestoreMainMenuOrPanel();
            });
            return;
        }

        if (matchmakingCardRect != null && matchmakingCanvasGroup != null)
        {
            matchmakingCardRect.DOKill();
            matchmakingCanvasGroup.DOKill();

            Sequence outSeq = DOTween.Sequence().SetUpdate(true);
            outSeq.Append(matchmakingCardRect.DOScale(Vector3.one * 0.86f, 0.20f).SetEase(Ease.InBack));
            outSeq.Join(matchmakingCanvasGroup.DOFade(0f, 0.18f));
            outSeq.OnComplete(() =>
            {
                if (matchmakingPanel != null) matchmakingPanel.SetActive(false);
                RestoreMainMenuOrPanel();
            });
        }
        else if (matchmakingPanel != null)
        {
            matchmakingPanel.SetActive(false);
            RestoreMainMenuOrPanel();
        }
    }

    private void RestoreMainMenuOrPanel()
    {
        MainMenuController menuController = MainMenuController.Instance ?? FindFirstObjectByType<MainMenuController>(FindObjectsInactive.Include);
        if (menuController != null)
        {
            if (mainPanel != null) mainPanel.SetActive(false);
            menuController.gameObject.SetActive(true);
            menuController.PlayEntranceAnimation();
            return;
        }

        if (mainPanel != null)
        {
            mainPanel.SetActive(true);
            PlayCardEntrance(mainCardRect, mainCanvasGroup);
        }
    }

    private void PlayCardEntrance(RectTransform card, CanvasGroup cg)
    {
        if (card == null) return;
        card.DOKill();
        card.localScale = Vector3.one * 0.75f;

        if (cg != null)
        {
            cg.DOKill();
            cg.alpha = 0f;
            cg.DOFade(1f, 0.28f).SetUpdate(true);
        }

        card.DOScale(Vector3.one, 0.45f).SetEase(Ease.OutBack, 1.2f).SetUpdate(true);
    }

    #endregion

    #region Vertical Slot Reel System (Arcade / Casino)

    /// <summary>
    /// Starts the continuous vertical slot reel conveyor-belt search loop.
    /// Avatar A slides upward while Avatar B enters from below at the same time.
    /// </summary>
    public void StartAvatarShuffleLoop()
    {
        StopAvatarShuffleLoop();
        EnsureSlotReelReferences();
        CacheRestingTransforms();

        // Slow background glow rotation
        if (opponentEnergyGlowRect != null)
        {
            opponentGlowRotateTween?.Kill();
            opponentGlowRotateTween = opponentEnergyGlowRect
                .DORotate(new Vector3(0f, 0f, -360f), 8f, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart)
                .SetUpdate(true);
        }

        avatarShuffleCoroutine = StartCoroutine(ContinuousSlotReelRoutine());
    }

    public void StopAvatarShuffleLoop()
    {
        if (avatarShuffleCoroutine != null)
        {
            StopCoroutine(avatarShuffleCoroutine);
            avatarShuffleCoroutine = null;
        }

        opponentGlowRotateTween?.Kill();
        opponentGlowRotateTween = null;
    }

    private void ResetReelVisuals()
    {
        if (reelAvatarA != null)
        {
            reelAvatarA.DOKill();
            reelAvatarA.anchoredPosition = Vector2.zero;
            reelAvatarA.localScale = Vector3.one;
        }
        if (reelAvatarB != null)
        {
            reelAvatarB.DOKill();
            reelAvatarB.anchoredPosition = new Vector2(0f, -slotTravelDistance);
            reelAvatarB.localScale = Vector3.one;
        }

        Sprite fallback = defaultOpponentSprite != null ? defaultOpponentSprite :
            (candidateAvatars != null && candidateAvatars.Length > 0 ? candidateAvatars[0] : null);

        if (reelAvatarImageA != null && fallback != null)
        {
            reelAvatarImageA.DOKill();
            reelAvatarImageA.sprite = fallback;
            reelAvatarImageA.color = Color.white;
        }
        if (reelAvatarImageB != null && fallback != null)
        {
            reelAvatarImageB.DOKill();
            reelAvatarImageB.sprite = fallback;
            reelAvatarImageB.color = Color.white;
        }

        if (opponentEnergyGlowRect != null)
        {
            opponentEnergyGlowRect.DOKill();
            opponentEnergyGlowRect.localScale = glowOriginalScale;
        }

        if (opponentNameText != null)
        {
            opponentNameText.rectTransform.DOKill();
            opponentNameText.DOKill();
            opponentNameText.rectTransform.anchoredPosition = nameTextRestingPos;
            opponentNameText.color = Color.white;
        }
    }

    private IEnumerator ContinuousSlotReelRoutine()
    {
        if (candidateAvatars == null || candidateAvatars.Length == 0) yield break;

        isUsingAvatarA = true;
        currentAvatarIndex = UnityEngine.Random.Range(0, candidateAvatars.Length);

        // Initial setup for Avatar A at center
        if (reelAvatarImageA != null && candidateAvatars[currentAvatarIndex] != null)
        {
            reelAvatarImageA.sprite = candidateAvatars[currentAvatarIndex];
            reelAvatarImageA.color = Color.white;
        }
        if (reelAvatarA != null)
        {
            reelAvatarA.anchoredPosition = Vector2.zero;
            reelAvatarA.localScale = Vector3.one;
        }
        if (reelAvatarB != null)
        {
            reelAvatarB.anchoredPosition = new Vector2(0f, -slotTravelDistance);
            reelAvatarB.localScale = Vector3.one;
        }

        while (!isMatchFound)
        {
            // Pick next candidate avatar & title
            currentAvatarIndex = (currentAvatarIndex + 1) % candidateAvatars.Length;
            Sprite nextSprite = candidateAvatars[currentAvatarIndex];
            string nextTitle = (candidateNames != null && candidateNames.Length > 0)
                ? candidateNames[currentAvatarIndex % candidateNames.Length]
                : null;

            // Execute synchronous conveyor step
            yield return StartCoroutine(ExecuteSlotReelStep(reelStepDuration, nextSprite, nextTitle, false));

            // Ultra-brief micro-gap between reel ticks for rapid arcade cadence
            yield return new WaitForSecondsRealtime(0.03f);
        }
    }

    /// <summary>
    /// Executes a single synchronous conveyor-belt step where the active avatar slides upward
    /// while the incoming avatar enters from below with vertical motion-stretch.
    /// </summary>
    private IEnumerator ExecuteSlotReelStep(float stepDuration, Sprite nextSprite, string nextTitle, bool isFinalSlam = false)
    {
        RectTransform activeAvatar = isUsingAvatarA ? reelAvatarA : reelAvatarB;
        RectTransform incomingAvatar = isUsingAvatarA ? reelAvatarB : reelAvatarA;
        Image incomingImg = isUsingAvatarA ? reelAvatarImageB : reelAvatarImageA;
        Image activeImg = isUsingAvatarA ? reelAvatarImageA : reelAvatarImageB;

        if (activeAvatar == null || incomingAvatar == null || incomingImg == null) yield break;

        activeAvatar.DOKill();
        incomingAvatar.DOKill();

        // 1. Setup incoming avatar below slot viewport
        if (nextSprite != null)
        {
            incomingImg.sprite = nextSprite;
        }
        incomingImg.color = Color.white; // True natural color, NO RED TINT!
        incomingAvatar.anchoredPosition = new Vector2(0f, -slotTravelDistance);

        // 2. Vertical motion-stretch during the rush
        float stretch = isFinalSlam ? 1.08f : motionStretchY;
        activeAvatar.localScale = new Vector3(1f, stretch, 1f);
        incomingAvatar.localScale = new Vector3(1f, stretch, 1f);

        // 3. Kinetic candidate title roll
        if (cycleCandidateNames && !string.IsNullOrEmpty(nextTitle))
        {
            TriggerNameTickerRoll(nextTitle, stepDuration);
        }

        // Starburst rotation kick
        TriggerEnergyShockwavePulse();

        // 4. Synchronous conveyor rush!
        Sequence stepSeq = DOTween.Sequence().SetUpdate(true);

        if (isFinalSlam)
        {
            // SLAM! Heavy entry with dramatic elastic spring bounce
            stepSeq.Join(activeAvatar.DOAnchorPosY(slotTravelDistance, stepDuration * 0.70f).SetEase(Ease.InQuad));
            stepSeq.Join(incomingAvatar.DOAnchorPosY(0f, stepDuration).SetEase(Ease.OutBounce));
            stepSeq.Join(incomingAvatar.DOScale(Vector3.one, stepDuration).SetEase(Ease.OutBounce));
        }
        else
        {
            // Smooth rapid conveyor rush
            stepSeq.Join(activeAvatar.DOAnchorPosY(slotTravelDistance, stepDuration).SetEase(Ease.InQuad));
            stepSeq.Join(incomingAvatar.DOAnchorPosY(0f, stepDuration).SetEase(Ease.OutQuad));
            stepSeq.Join(incomingAvatar.DOScale(Vector3.one, stepDuration * 0.45f).SetDelay(stepDuration * 0.55f));
        }

        bool isStepDone = false;
        stepSeq.OnComplete(() => isStepDone = true);
        stepSeq.OnKill(() => isStepDone = true);

        float timeout = stepDuration + 0.08f;
        float elapsed = 0f;
        while (!isStepDone && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // 5. Reset outgoing avatar below for the next incoming cycle
        activeAvatar.anchoredPosition = new Vector2(0f, -slotTravelDistance);
        activeAvatar.localScale = Vector3.one;
        incomingAvatar.anchoredPosition = Vector2.zero;
        incomingAvatar.localScale = Vector3.one;

        // Toggle active role
        isUsingAvatarA = !isUsingAvatarA;
    }

    private void TriggerEnergyShockwavePulse()
    {
        if (opponentEnergyGlowRect == null) return;

        opponentEnergyGlowRect.DOKill();
        opponentEnergyGlowRect.DORotate(opponentEnergyGlowRect.localEulerAngles + new Vector3(0, 0, -50f), 0.12f, RotateMode.FastBeyond360).SetEase(Ease.OutQuad).SetUpdate(true);
        opponentEnergyGlowRect.DOPunchScale(Vector3.one * 0.18f, 0.14f, 2, 0.5f).SetUpdate(true);
    }

    private void TriggerNameTickerRoll(string nextTitle, float duration)
    {
        if (opponentNameText == null) return;

        opponentNameText.rectTransform.DOKill();
        opponentNameText.DOKill();

        Sequence textSeq = DOTween.Sequence().SetUpdate(true);
        // Upward flick and fade
        textSeq.Append(opponentNameText.rectTransform.DOAnchorPosY(nameTextRestingPos.y + 6f, duration * 0.45f).SetEase(Ease.InQuad));
        textSeq.Join(opponentNameText.DOFade(0.3f, duration * 0.45f));
        textSeq.AppendCallback(() =>
        {
            opponentNameText.text = nextTitle;
            opponentNameText.rectTransform.anchoredPosition = new Vector2(nameTextRestingPos.x, nameTextRestingPos.y - 6f);
            opponentNameText.color = Color.white;
        });
        // Snap up into resting position
        textSeq.Append(opponentNameText.rectTransform.DOAnchorPosY(nameTextRestingPos.y, duration * 0.55f).SetEase(Ease.OutBack, 1.8f));
        textSeq.Join(opponentNameText.DOFade(1f, duration * 0.55f));

        if (opponentRoleText != null)
        {
            opponentRoleText.transform.DOKill();
            opponentRoleText.transform.DOPunchScale(Vector3.one * 0.08f, 0.12f, 2, 0.5f).SetUpdate(true);
        }
    }

    #endregion

    #region Matchmaking Status & Timer

    private IEnumerator MatchmakingTimerRoutine()
    {
        while (!isMatchFound)
        {
            float elapsed = Time.unscaledTime - matchmakingStartTime;
            int minutes = (int)(elapsed / 60f);
            int seconds = (int)(elapsed % 60f);

            if (matchmakingTimerText != null)
            {
                matchmakingTimerText.text = $"ELAPSED: {minutes:D2}:{seconds:D2}";
            }
            yield return new WaitForSecondsRealtime(1.0f);
        }
    }

    private IEnumerator DotAnimationRoutine()
    {
        int dotCount = 0;
        string[] dots = new string[] { "", ".", "..", "..." };

        while (!isMatchFound)
        {
            if (matchmakingStatusText != null)
            {
                string baseText = matchmakingStatusText.text;
                if (!baseText.Contains("FOUND") && !baseText.Contains("ACQUIRED") && !baseText.Contains("LOCKING"))
                {
                    int dotIdx = baseText.IndexOf('.');
                    string clean = dotIdx >= 0 ? baseText.Substring(0, dotIdx) : baseText;
                    matchmakingStatusText.text = clean + dots[dotCount];
                }
            }
            dotCount = (dotCount + 1) % dots.Length;
            yield return new WaitForSecondsRealtime(0.45f);
        }
    }

    #endregion

    #region Epic Dramatic Match Found Deceleration (tick... tick... SLAM!)

    /// <summary>
    /// Executes the dramatic slot reel deceleration sequence (tick... tick... SLAM!)
    /// onto the confirmed opponent with an elastic spring bounce before battle!
    /// </summary>
    /// <summary>Sets the portrait the match-found reveal lands on, so it matches the BattleScene ready panel.</summary>
    public void SetConfirmedOpponentSprite(Sprite sprite)
    {
        if (sprite != null) confirmedOpponentSprite = sprite;
    }

    public void PlayMatchFoundSequence(string opponentName, Action onComplete)
    {
        isMatchFound = true;
        StopAvatarShuffleLoop();

        if (matchmakingTimerCoroutine != null) StopCoroutine(matchmakingTimerCoroutine);
        if (dotAnimCoroutine != null) StopCoroutine(dotAnimCoroutine);

        if (decelerationCoroutine != null) StopCoroutine(decelerationCoroutine);
        decelerationCoroutine = StartCoroutine(MatchFoundDecelerationRoutine(opponentName, onComplete));
    }

    private IEnumerator MatchFoundDecelerationRoutine(string opponentName, Action onComplete)
    {
        Sprite finalSprite = confirmedOpponentSprite != null ? confirmedOpponentSprite :
            (candidateAvatars != null && candidateAvatars.Length > 0 ? candidateAvatars[0] : null);

        string finalName = string.IsNullOrWhiteSpace(opponentName) ? "Enemy Commander" : opponentName;

        // 1. Tick 1 (Fast tick - 0.12s)
        currentAvatarIndex = (currentAvatarIndex + 1) % candidateAvatars.Length;
        yield return StartCoroutine(ExecuteSlotReelStep(0.12f, candidateAvatars[currentAvatarIndex], "Targeting...", false));

        // 2. Tick 2 (Decelerating tick - 0.22s)
        if (matchmakingStatusText != null)
        {
            matchmakingStatusText.text = "<color=#FFCC00>TARGET ACQUIRED...</color>";
        }
        currentAvatarIndex = (currentAvatarIndex + 1) % candidateAvatars.Length;
        yield return StartCoroutine(ExecuteSlotReelStep(0.22f, candidateAvatars[currentAvatarIndex], "Locking On...", false));

        // 3. Tick 3 (Suspenseful crawl - 0.35s)
        currentAvatarIndex = (currentAvatarIndex + 1) % candidateAvatars.Length;
        yield return StartCoroutine(ExecuteSlotReelStep(0.35f, candidateAvatars[currentAvatarIndex], "Challenger Approaching...", false));

        // 4. Tick 4: THE SLAM! (0.45s onto confirmed opponent with elastic spring bounce!)
        if (matchmakingStatusText != null)
        {
            matchmakingStatusText.text = "<color=#00FFAA>MATCH FOUND!</color> PREPARING BATTLE...";
        }

        yield return StartCoroutine(ExecuteSlotReelStep(0.45f, finalSprite, finalName, true));

        // The avatar that just slammed into center is:
        RectTransform confirmedAvatarRT = isUsingAvatarA ? reelAvatarB : reelAvatarA;

        // 5. Climax Showdown Impact Choreography:
        Sequence showdown = DOTween.Sequence().SetUpdate(true);

        // Swords centerpiece punch & flash
        if (swordsCenterpieceRect != null)
        {
            showdown.Append(swordsCenterpieceRect.DOPunchScale(Vector3.one * 0.38f, 0.45f, 8, 1f));
        }

        // VS emblem punch
        if (vsBadgeRect != null)
        {
            showdown.Join(vsBadgeRect.DOPunchScale(Vector3.one * 0.42f, 0.45f, 6, 1f));
        }

        // Confirmed avatar lock-in pop punch
        if (confirmedAvatarRT != null)
        {
            showdown.Join(confirmedAvatarRT.DOPunchScale(Vector3.one * 0.32f, 0.42f, 6, 0.8f));
        }

        // Starburst energy flare explosion
        if (opponentEnergyGlowRect != null)
        {
            showdown.Join(opponentEnergyGlowRect.DOPunchScale(Vector3.one * 0.50f, 0.45f, 6, 0.8f));
        }

        // Name text golden glow punch
        if (opponentNameText != null)
        {
            opponentNameText.color = new Color(1f, 0.90f, 0.30f, 1f); // Glorious gold
            showdown.Join(opponentNameText.rectTransform.DOPunchScale(Vector3.one * 0.32f, 0.42f, 5, 0.7f));
        }

        // 6. Brief dramatic pause for player satisfaction
        showdown.AppendInterval(0.65f);

        // 7. Fade out and launch battle!
        if (matchmakingCanvasGroup != null)
        {
            showdown.Append(matchmakingCanvasGroup.DOFade(0f, 0.35f));
        }

        showdown.OnComplete(() =>
        {
            ShowMatchTipsThen(onComplete);
        });
    }

    #endregion

    private void OnDestroy()
    {
        StopAvatarShuffleLoop();
        auraRotateTween?.Kill();
        swordsBreatheTween?.Kill();
        vsPulseTween?.Kill();
        multiBtnPulseTween?.Kill();
        dotPulseTween?.Kill();
        DOTween.Kill(this);
    }
}

/// <summary>
/// Helper component for ultra-tactile button feedback (squash, stretch, and spring bounce).
/// </summary>
public class JuicyButtonHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    private RectTransform rectTransform;
    private Vector3 originalScale = Vector3.one;
    private float pressScaleMultiplier = 0.92f;
    private bool isPointerOver = false;

    public void Initialize(float pressScale)
    {
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            originalScale = rectTransform.localScale;
        }
        pressScaleMultiplier = pressScale;
    }

    private void Awake()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null) originalScale = rectTransform.localScale;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (rectTransform == null) return;
        rectTransform.DOKill();
        // Squash on press
        Vector3 target = new Vector3(originalScale.x * 1.05f, originalScale.y * pressScaleMultiplier, originalScale.z);
        rectTransform.DOScale(target, 0.08f).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (rectTransform == null) return;
        rectTransform.DOKill();
        // Overshoot spring bounce on release
        Vector3 target = isPointerOver ? originalScale * 1.04f : originalScale;
        rectTransform.DOScale(target, 0.38f).SetEase(Ease.OutElastic, 1.4f).SetUpdate(true);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerOver = true;
        if (rectTransform == null) return;
        rectTransform.DOKill();
        rectTransform.DOScale(originalScale * 1.04f, 0.15f).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerOver = false;
        if (rectTransform == null) return;
        rectTransform.DOKill();
        rectTransform.DOScale(originalScale, 0.18f).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    private void OnDisable()
    {
        if (rectTransform != null)
        {
            rectTransform.DOKill();
            rectTransform.localScale = originalScale;
        }
    }
}
