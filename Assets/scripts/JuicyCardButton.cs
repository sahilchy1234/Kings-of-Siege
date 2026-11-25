using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

/// <summary>
/// Ultra-juicy, responsive mobile RTS card button behavior powered by DOTween.
/// Provides:
/// - Squash & stretch on touch/press (press down).
/// - Squash & stretch on touch/press (0.92x scale).
/// - Overshoot elastic spring bounce on release.
/// - Ready pop punch when energy/elixir reaches card cost.
/// - Gentle idle breathing pulse while card is affordable and ready to deploy.
/// - Juicy shake rejection with red flash when tapped without sufficient energy.
/// - Punchy tilt-wobble & red flash rejection when tapped without sufficient energy.
/// - Pop punch on unit spawn or spell deployment.
/// - Guaranteed 100% precision return to resting position & rotation after any shake/tilt.
/// - NEVER modifies anchoredPosition so Unity's LayoutGroups remain 100% intact.
/// - Guaranteed return to perfectly straight Quaternion.identity rotation after any wobble.
/// </summary>
[DisallowMultipleComponent]
public class JuicyCardButton : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [Header("UI Element References")]
    [SerializeField] private RectTransform cardRect;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button button;
    [SerializeField] private Graphic mainGraphic;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private RectTransform costBadgeRect;

    [Header("Juice Configuration")]
    [Tooltip("Squash scale multiplier when button is pressed down.")]
    [SerializeField, Range(0.8f, 0.98f)] private float pressScaleMultiplier = 0.92f;

    [Tooltip("Slight scale lift when highlighted/hovered.")]
    [SerializeField, Range(1.0f, 1.15f)] private float hoverScaleMultiplier = 1.05f;

    [Tooltip("Enable subtle breathing pulse while card is affordable and ready to deploy.")]
    [SerializeField] private bool enableReadyBreathing = true;

    [Tooltip("Breathing amplitude when ready.")]
    [SerializeField, Range(1.01f, 1.08f)] private float readyBreathingMultiplier = 1.035f;

    [Header("Energy Controller Link")]
    [SerializeField] private SegmentedEnergySliderController energySlider;

    // Resting Transforms (precisely cached to guarantee zero drift after shakes)
    // Resting Scale & Colors (anchoredPosition is NEVER modified to preserve LayoutGroups)
    private Vector3 initialScale = Vector3.one;
    private Quaternion initialRotation = Quaternion.identity;

    private Vector3 initialBadgeScale = Vector3.one;
    private Vector2 initialBadgePosition = Vector2.zero;
    private Quaternion initialBadgeRotation = Quaternion.identity;

    private Color originalGraphicColor = Color.white;
    private Tween breathingTween;
    private Sequence shakeSequence;
    private bool isCurrentlyAffordable = false;
    private bool isPointerDown = false;
    private bool isInitialized = false;

    private float lastRejectFeedbackTime = -1f;
    private const float REJECT_FEEDBACK_COOLDOWN = 0.15f;

    public bool IsAffordable => isCurrentlyAffordable;
    public RectTransform CardRect => cardRect;
    public Button ButtonComponent => button;

    private void Awake()
    {
        Initialize();
    }

    private void Start()
    {
        if (energySlider == null)
        {
            energySlider = SegmentedEnergySliderController.Instance;
        }
    }

    private void OnDisable()
    {
        KillActiveTweens();
        ApplyRestingTransform();
    }

    private void OnDestroy()
    {
        KillActiveTweens();
        ApplyRestingTransform();
    }

    public void Initialize()
    {
        if (isInitialized) return;
        isInitialized = true;

        if (cardRect == null) cardRect = GetComponent<RectTransform>();
        if (cardRect != null)
        {
            initialScale = cardRect.localScale;
            initialRotation = cardRect.localRotation;
            cardRect.localRotation = Quaternion.identity;
        }

        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (button == null) button = GetComponent<Button>();
        if (mainGraphic == null) mainGraphic = GetComponent<Graphic>();
        if (mainGraphic != null) originalGraphicColor = mainGraphic.color;

        AutoFindCostBadge();

        if (costBadgeRect != null)
        {
            initialBadgeScale = costBadgeRect.localScale;
            initialBadgePosition = costBadgeRect.anchoredPosition;
            initialBadgeRotation = costBadgeRect.localRotation;
            costBadgeRect.localRotation = Quaternion.identity;
        }
    }

    /// <summary>
    /// Pure visual restoration WITHOUT calling any DOTween kill methods.
    /// Eliminates any accumulated drift from shakes or interruptions.
    /// 100% safe to use inside OnComplete or OnKill callbacks without causing DOTween reentrancy exceptions!
    /// </summary>
    public void ResetToRestingTransform()
    {
        ApplyRestingTransform();
    }

    public void ApplyRestingTransform()
    {
        if (cardRect != null)
        {
            cardRect.localRotation = Quaternion.identity;
            cardRect.localScale = initialScale;
        }

        if (costBadgeRect != null)
        {
            costBadgeRect.anchoredPosition = initialBadgePosition;
            costBadgeRect.localRotation = Quaternion.identity;
            costBadgeRect.localScale = initialBadgeScale;
        }
    }

    /// <summary>
    /// Safely terminates all active tweens on this card without triggering reentrancy loops,
    /// and immediately restores resting transforms.
    /// </summary>
    public void ResetToRestingRotationAndScale()
    {
        KillActiveTweens();
        ApplyRestingTransform();
    }

    /// <summary>
    /// Safely kills any active tweens on this card without triggering recursive kill callbacks.
    /// </summary>
    public void KillActiveTweens()
    {
        if (shakeSequence != null)
        {
            Sequence seq = shakeSequence;
            shakeSequence = null;
            if (seq.IsActive()) seq.Kill();
        }

        StopBreathing();

        if (cardRect != null) DOTween.Kill(cardRect);
        if (costBadgeRect != null) DOTween.Kill(costBadgeRect);
        if (mainGraphic != null)
        {
            DOTween.Kill(mainGraphic);
            mainGraphic.color = originalGraphicColor;
        }
    }

    private void AutoFindCostBadge()
    {
        if (costText == null)
        {
            costText = GetComponentInChildren<TMP_Text>(true);
        }

        if (costBadgeRect == null)
        {
            Transform badge = transform.Find("counterbadge");
            if (badge == null) badge = transform.Find("badge");
            if (badge == null) badge = transform.Find("Badge");
            if (badge == null && costText != null && costText.transform.parent != transform)
            {
                badge = costText.transform.parent;
            }

            if (badge != null)
            {
                costBadgeRect = badge.GetComponent<RectTransform>();
            }
            else if (costText != null)
            {
                costBadgeRect = costText.GetComponent<RectTransform>();
            }
        }
    }

    #region Pointer Events & Squash/Stretch

    public void OnPointerDown(PointerEventData eventData)
    {
        Initialize();
        isPointerDown = true;
        StopBreathing();

        bool canInteract = button == null || button.interactable;

        if (!canInteract || !isCurrentlyAffordable)
        {
            PlayRejectFeedback();
            return;
        }

        // Juicy squash press
        if (cardRect != null)
        {
            cardRect.DOKill();
            DOTween.Kill(cardRect);
            cardRect.localRotation = Quaternion.identity;
            cardRect.DOScale(initialScale * pressScaleMultiplier, 0.08f)
                .SetId(this)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPointerDown = false;

        if (cardRect != null)
        {
            DOTween.Kill(cardRect);
            cardRect.localRotation = Quaternion.identity;
            cardRect.DOScale(initialScale, 0.18f)
                .SetId(this)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    ApplyRestingTransform();
                    if (isCurrentlyAffordable && enableReadyBreathing && !isPointerDown)
                    {
                        StartBreathing();
                    }
                })
                .OnKill(ApplyRestingTransform);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        bool canInteract = button == null || button.interactable;

        if (!canInteract || !isCurrentlyAffordable)
        {
            PlayRejectFeedback();
            return;
        }

        PlaySpawnPop();
    }

    #endregion

    #region Juicy Animations

    /// <summary>
    /// Triggered when the unit spawns or card action successfully fires.
    /// </summary>
    public void PlaySpawnPop()
    {
        KillActiveTweens();
        ApplyRestingTransform();

        if (cardRect != null)
        {
            cardRect.DOPunchScale(new Vector3(-0.1f, 0.1f, 0f), 0.22f, 5, 0.5f)
                .SetId(this)
                .SetUpdate(true)
                .OnComplete(ApplyRestingTransform)
                .OnKill(ApplyRestingTransform);
        }

        if (costBadgeRect != null)
        {
            costBadgeRect.DOPunchScale(Vector3.one * 0.3f, 0.22f, 5, 0.6f)
                .SetId(this)
                .SetUpdate(true)
                .OnComplete(ApplyRestingTransform)
                .OnKill(ApplyRestingTransform);
        }
    }

    /// <summary>
    /// Shake & red flash rejection feedback when clicked without enough energy.
    /// Strictly resets position & rotation on both complete and kill to guarantee zero drift!
    /// Uses DOPunchRotation so it naturally oscillates and returns to 0 tilt,
    /// with an explicit Quaternion.identity guarantee on completion and kill.
    /// </summary>
    public void PlayRejectFeedback()
    {
        if (Time.unscaledTime - lastRejectFeedbackTime < REJECT_FEEDBACK_COOLDOWN)
        {
            return;
        }
        lastRejectFeedbackTime = Time.unscaledTime;

        KillActiveTweens();
        ApplyRestingTransform();

        if (cardRect != null)
        {
            shakeSequence = DOTween.Sequence();
            shakeSequence.SetId(this);
            shakeSequence.SetUpdate(true);
            shakeSequence.Append(cardRect.DOPunchRotation(new Vector3(0f, 0f, 6f), 0.26f, 10, 0.5f));
            shakeSequence.Join(cardRect.DOPunchScale(new Vector3(0.06f, -0.06f, 0f), 0.26f, 8, 0.5f));
            shakeSequence.OnComplete(() =>
            {
                shakeSequence = null;
                ApplyRestingTransform();
            });
            shakeSequence.OnKill(() =>
            {
                shakeSequence = null;
                ApplyRestingTransform();
            });
        }

        if (costBadgeRect != null)
        {
            costBadgeRect.localRotation = Quaternion.identity;
            costBadgeRect.DOPunchRotation(new Vector3(0f, 0f, 8f), 0.24f, 8, 0.5f)
                .SetId(this)
                .SetUpdate(true)
                .OnComplete(ApplyRestingTransform)
                .OnKill(ApplyRestingTransform);
        }

        if (mainGraphic != null)
        {
            mainGraphic.DOColor(new Color(1f, 0.35f, 0.35f, 1f), 0.08f)
                .SetLoops(2, LoopType.Yoyo)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (mainGraphic != null) mainGraphic.color = originalGraphicColor;
                })
                .OnKill(() =>
                {
                    if (mainGraphic != null) mainGraphic.color = originalGraphicColor;
                });
        }

        // Alert slider controller to shake the mana counter and bar too!
        if (energySlider == null) energySlider = SegmentedEnergySliderController.Instance;
        if (energySlider != null)
        {
            energySlider.PlayInsufficientEnergyFeedback();
        }
    }

    /// <summary>
    /// Updates whether this button is currently affordable based on energy.
    /// Triggers celebration pop when transitioning into affordable.
    /// </summary>
    public void SetAffordableState(bool canAfford)
    {
        Initialize();

        if (canAfford == isCurrentlyAffordable) return;
        isCurrentlyAffordable = canAfford;

        if (canAfford)
        {
            // Transition to AFFORDABLE: Juicy POP celebration!
            if (cardRect != null && !isPointerDown)
            {
                DOTween.Kill(cardRect);
                ApplyRestingTransform();
                cardRect.DOPunchScale(Vector3.one * 0.12f, 0.3f, 4, 0.5f)
                    .SetId(this)
                    .SetUpdate(true)
                    .OnComplete(() =>
                    {
                        ApplyRestingTransform();
                        if (enableReadyBreathing && isCurrentlyAffordable && !isPointerDown)
                        {
                            StartBreathing();
                        }
                    })
                    .OnKill(ApplyRestingTransform);
            }

            if (costBadgeRect != null)
            {
                DOTween.Kill(costBadgeRect);
                costBadgeRect.DOPunchScale(Vector3.one * 0.25f, 0.28f, 4, 0.6f)
                    .SetId(this)
                    .SetUpdate(true)
                    .OnComplete(ApplyRestingTransform)
                    .OnKill(ApplyRestingTransform);
            }
        }
        else
        {
            // Transition to UNAFFORDABLE: Stop breathing & smoothly return to resting scale
            StopBreathing();
            if (cardRect != null && !isPointerDown)
            {
                DOTween.Kill(cardRect);
                cardRect.localRotation = Quaternion.identity;
                cardRect.DOScale(initialScale, 0.18f)
                    .SetId(this)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true)
                    .OnComplete(ApplyRestingTransform)
                    .OnKill(ApplyRestingTransform);
            }
        }
    }

    private void StartBreathing()
    {
        if (!enableReadyBreathing || cardRect == null) return;
        StopBreathing();

        cardRect.localRotation = Quaternion.identity;

        breathingTween = cardRect.DOScale(initialScale * readyBreathingMultiplier, 0.65f)
            .SetId(this)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    private void StopBreathing()
    {
        if (breathingTween != null)
        {
            Tween b = breathingTween;
            breathingTween = null;
            if (b.IsActive()) b.Kill();
        }

        if (cardRect != null && !isPointerDown)
        {
            cardRect.localRotation = Quaternion.identity;
            cardRect.localScale = initialScale;
        }
    }

    #endregion

    #region Editor Context Menu Preview Helpers

    [ContextMenu("Test Juice: Spawn Pop Punch")]
    public void TestSpawnPop()
    {
        Initialize();
        PlaySpawnPop();
    }

    [ContextMenu("Test Juice: Rejection Shake")]
    public void TestRejectShake()
    {
        Initialize();
        PlayRejectFeedback();
    }

    [ContextMenu("Test Juice: Toggle Ready State")]
    public void TestToggleReadyState()
    {
        Initialize();
        SetAffordableState(!isCurrentlyAffordable);
    }

    [ContextMenu("Reset to Original Position & Rotation")]
    public void ContextResetToResting()
    {
        Initialize();
        ResetToRestingRotationAndScale();
    }

    #endregion
}
