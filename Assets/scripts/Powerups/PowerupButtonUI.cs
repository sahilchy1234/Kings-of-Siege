using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

/// <summary>
/// Attached to each power-up UI button card.
/// Ultra-juicy DOTween interactions:
/// - Squash & stretch on touch/drag (0.92x press, 1.12x lift).
/// - 3D card-flip animation on spell cycle.
/// - Cooldown completion celebration punch.
/// - Reactive pop punch when becoming affordable.
/// - Shake rejection feedback with red flash on insufficient energy or cooldown.
/// </summary>
[DisallowMultipleComponent]
public class PowerupButtonUI : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerDownHandler, IPointerUpHandler
{
    [Header("Powerup Binding")]
    [SerializeField] private PowerupType powerupType;

    [Header("UI Visual Elements")]
    [SerializeField] private Image buttonBgImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Image cooldownOverlay;
    [SerializeField] private TMP_Text cooldownText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform cardRect;

    [Header("Hotkey & Animation")]
    [SerializeField] private KeyCode hotKey = KeyCode.None;
    [SerializeField, Range(1f, 1.3f)] private float dragScale = 1.10f;
    [SerializeField] private bool enableReadyBreathing = true;

    [Header("Energy Slider Link")]
    [Tooltip("Optional direct reference to SegmentedEnergySliderController. If unassigned, automatically finds it in scene.")]
    [SerializeField] private SegmentedEnergySliderController energySliderController;
    [SerializeField] private Button buttonComponent;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private RectTransform costBadgeRect;

    private bool isDragging = false;
    private bool isPointerDownOnCard = false;
    private bool wasOnCooldown = false;
    private bool isCurrentlyAffordable = false;
    private Vector2 pointerDownScreenPos;

    // Resting Transforms (precisely cached to guarantee zero drift after shakes)
    // Resting Scale & Rotation (anchoredPosition is NEVER modified so LayoutGroups maintain positions perfectly)
    private Vector3 initialScale = Vector3.one;
    private Quaternion initialRotation = Quaternion.identity;

    private Vector3 initialBadgeScale = Vector3.one;
    private Vector2 initialBadgePosition = Vector2.zero;
    private Quaternion initialBadgeRotation = Quaternion.identity;

    private float totalCooldown = 10f;
    private int slotIndex = -1;
    private PowerupUIBar parentBar;
    private Tween breathingTween;
    private Sequence shakeSequence;

    public PowerupType BoundPowerupType => powerupType;
    public int SlotIndex => slotIndex;
    public Button ButtonComponent => buttonComponent;
    public RectTransform CardRect => cardRect;
    /// <summary>The authored resting scale, captured in Awake before any intro animation can shrink the card.</summary>
    public Vector3 RestingScale => initialScale;

    public void SetSlotIndex(int index, PowerupUIBar bar)
    {
        this.slotIndex = index;
        this.parentBar = bar;
    }

    private void Awake()
    {
        if (cardRect == null) cardRect = GetComponent<RectTransform>();
        if (cardRect != null)
        {
            initialScale = cardRect.localScale;
            initialRotation = cardRect.localRotation;
            cardRect.localRotation = Quaternion.identity;
        }

        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (buttonComponent == null) buttonComponent = GetComponent<Button>();

        AutoFindChildReferences();

        if (costBadgeRect != null)
        {
            initialBadgeScale = costBadgeRect.localScale;
            initialBadgePosition = costBadgeRect.anchoredPosition;
            initialBadgeRotation = costBadgeRect.localRotation;
            costBadgeRect.localRotation = Quaternion.identity;
        }
    }

    private void Start()
    {
        RegisterToEnergySlider();
        UpdateConfigDisplay();
    }

    private void OnDisable()
    {
        StopBreathing();
        ResetToRestingTransform();
        ResetToRestingRotationAndScale();
    }

    private void OnDestroy()
    {
        StopBreathing();
        if (shakeSequence != null && shakeSequence.IsActive()) shakeSequence.Kill();
        if (cardRect != null) cardRect.DOKill();
        if (buttonBgImage != null) buttonBgImage.DOKill();
        if (costBadgeRect != null) costBadgeRect.DOKill();
        DOTween.Kill(this);
        if (cardRect != null) DOTween.Kill(cardRect);
        if (buttonBgImage != null) DOTween.Kill(buttonBgImage);
        if (costBadgeRect != null) DOTween.Kill(costBadgeRect);

        if (energySliderController != null && buttonComponent != null)
        {
            energySliderController.UnregisterButton(buttonComponent);
        }
    }

    /// <summary>
    /// Instantly restores the exact resting anchoredPosition, localRotation, and localScale.
    /// Eliminates any accumulated drift from shakes or interruptions.
    /// Restores resting rotation and scale WITHOUT calling DOTween kill methods.
    /// Safe inside OnComplete/OnKill callbacks to prevent IndexOutOfRangeException.
    /// </summary>
    private void ApplyRestingTransform()
    {
        if (shakeSequence != null && shakeSequence.IsActive())
        {
            shakeSequence.Kill();
            shakeSequence = null;
        }

        if (cardRect != null)
        {
            cardRect.DOKill();
            cardRect.localRotation = Quaternion.identity;
            cardRect.localScale = initialScale;
        }

        if (costBadgeRect != null)
        {
            costBadgeRect.DOKill();
            costBadgeRect.anchoredPosition = initialBadgePosition;
            costBadgeRect.localRotation = initialBadgeRotation;
            costBadgeRect.localRotation = Quaternion.identity;
            costBadgeRect.localScale = initialBadgeScale;
        }
    }

    /// <summary>
    /// Instantly restores the exact resting localRotation (Quaternion.identity) and localScale.
    /// Does NOT touch anchoredPosition, allowing LayoutGroups to manage positions perfectly.
    /// </summary>
    public void ResetToRestingRotationAndScale()
    {
        DOTween.Kill(this);
        if (cardRect != null) DOTween.Kill(cardRect);
        if (costBadgeRect != null) DOTween.Kill(costBadgeRect);
        ApplyRestingTransform();
    }

    /// <summary>
    /// Backward-compatibility alias for ResetToRestingRotationAndScale.
    /// </summary>
    public void ResetToRestingTransform()
    {
        ResetToRestingRotationAndScale();
    }

    public void BindPowerup(PowerupType type, KeyCode shortcutKey = KeyCode.None)
    {
        this.powerupType = type;
        this.hotKey = shortcutKey;
        RegisterToEnergySlider();
        UpdateConfigDisplay();
    }

    private void AutoFindChildReferences()
    {
        if (buttonBgImage == null)
        {
            buttonBgImage = GetComponent<Image>();
        }

        if (iconImage == null)
        {
            Transform t = transform.Find("Icon");
            if (t != null) iconImage = t.GetComponent<Image>();
        }

        if (titleText == null)
        {
            Transform t = transform.Find("Title");
            if (t != null) titleText = t.GetComponent<TMP_Text>();
        }

        if (cooldownOverlay == null)
        {
            Transform t = transform.Find("CooldownOverlay");
            if (t != null) cooldownOverlay = t.GetComponent<Image>();
        }

        if (cooldownText == null)
        {
            Transform t = transform.Find("CooldownText");
            if (t != null) cooldownText = t.GetComponent<TMP_Text>();
        }

        if (costBadgeRect == null)
        {
            Transform badge = transform.Find("counterbadge");
            if (badge == null) badge = transform.Find("badge");
            if (badge != null)
            {
                costBadgeRect = badge.GetComponent<RectTransform>();
                costText = badge.GetComponentInChildren<TMP_Text>(true);
            }
        }

        if (costText == null)
        {
            Transform t = transform.Find("CostText");
            if (t == null) t = transform.Find("Cost");
            if (t != null) costText = t.GetComponent<TMP_Text>();
        }
    }

    /// <summary>
    /// Registers this button and its energy cost directly into SegmentedEnergySliderController's buttonConfigs.
    /// </summary>
    public void RegisterToEnergySlider()
    {
        if (energySliderController == null)
        {
            energySliderController = SegmentedEnergySliderController.Instance;
        }

        if (energySliderController == null) return;

        if (buttonComponent == null)
        {
            buttonComponent = GetComponent<Button>();
            if (buttonComponent == null)
            {
                buttonComponent = gameObject.AddComponent<Button>();
                if (buttonBgImage != null)
                {
                    buttonComponent.targetGraphic = buttonBgImage;
                }
            }
        }

        float cost = 2f;
        if (PowerupManager.Instance != null)
        {
            PowerupData config = PowerupManager.Instance.GetConfig(powerupType);
            if (config != null)
            {
                cost = config.energyCost;
            }
        }

        energySliderController.RegisterButton(
            buttonComponent,
            cost,
            null,
            costText,
            canvasGroup,
            $"Powerup_{powerupType}",
            spendOnClick: false
        );
    }

    public void UpdateConfigDisplay()
    {
        if (PowerupManager.Instance == null) return;
        PowerupData config = PowerupManager.Instance.GetConfig(powerupType);
        if (config == null) return;

        totalCooldown = config.cooldown;

        if (titleText != null)
        {
            titleText.text = config.displayName;
        }

        Sprite cardSprite = config.buttonImage != null ? config.buttonImage : PowerupManager.GetDefaultCardSprite(powerupType);

        if (cardSprite != null)
        {
            if (buttonBgImage != null)
            {
                buttonBgImage.sprite = cardSprite;
                buttonBgImage.color = Color.white;
            }

            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(false);
            }
            if (titleText != null)
            {
                titleText.gameObject.SetActive(false);
            }
        }
        else
        {
            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(true);
                if (config.icon != null)
                {
                    iconImage.sprite = config.icon;
                    iconImage.color = Color.white;
                }
            }
            if (titleText != null)
            {
                titleText.gameObject.SetActive(true);
            }
        }

        if (cooldownOverlay != null)
        {
            cooldownOverlay.type = Image.Type.Filled;
            cooldownOverlay.fillMethod = Image.FillMethod.Radial360;
            cooldownOverlay.fillOrigin = (int)Image.Origin360.Top;
            cooldownOverlay.fillClockwise = false;
            cooldownOverlay.gameObject.SetActive(false);
        }

        Transform badge = costBadgeRect != null ? costBadgeRect : transform.Find("counterbadge");
        if (badge != null)
        {
            badge.gameObject.SetActive(true);
        }
        if (costText != null)
        {
            costText.gameObject.SetActive(true);
            costText.text = config.energyCost.ToString("0");
        }

        if (energySliderController == null)
        {
            energySliderController = SegmentedEnergySliderController.Instance;
        }
        if (energySliderController != null && buttonComponent != null)
        {
            energySliderController.UpdateButtonCost(buttonComponent, config.energyCost, $"Powerup_{powerupType}");
        }
    }

    private void Update()
    {
        UpdateCooldownVisuals();
        CheckHotkeyTrigger();

        // Continuous smooth ground tracking every frame while aiming
        if (isDragging && PowerupManager.Instance != null && PowerupManager.Instance.IsAiming)
        {
            Vector2 screenPos = Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
            PowerupManager.Instance.UpdateAiming(screenPos);
        }
    }

    private void UpdateCooldownVisuals()
    {
        if (PowerupManager.Instance == null) return;

        float remaining = PowerupManager.Instance.GetRemainingCooldown(powerupType);
        if (remaining > 0.05f)
        {
            wasOnCooldown = true;

            if (cooldownOverlay != null)
            {
                if (!cooldownOverlay.gameObject.activeSelf)
                    cooldownOverlay.gameObject.SetActive(true);

                cooldownOverlay.fillAmount = Mathf.Clamp01(remaining / Mathf.Max(0.1f, totalCooldown));
            }

            if (cooldownText != null)
            {
                if (!cooldownText.gameObject.activeSelf)
                    cooldownText.gameObject.SetActive(true);

                cooldownText.text = remaining > 9.9f ? $"{remaining:F0}s" : $"{remaining:F1}s";
            }
        }
        else
        {
            if (wasOnCooldown)
            {
                wasOnCooldown = false;
                PlayCooldownFinishedPop();
            }

            if (cooldownOverlay != null && cooldownOverlay.gameObject.activeSelf)
                cooldownOverlay.gameObject.SetActive(false);

            if (cooldownText != null && cooldownText.gameObject.activeSelf)
                cooldownText.gameObject.SetActive(false);
        }
    }

    private void CheckHotkeyTrigger()
    {
        if (hotKey != KeyCode.None && Input.GetKeyDown(hotKey))
        {
            if (buttonComponent != null && !buttonComponent.interactable)
            {
                PlayShakeReject();
                return;
            }

            if (!PowerupManager.Instance.IsOnCooldown(powerupType))
            {
                Vector2 screenPos = Input.mousePosition;
                PowerupManager.Instance.StartAiming(powerupType, screenPos);
            }
        }
    }

    #region Drag & Drop Event Handlers (Juicy DOTween)

    public void OnPointerDown(PointerEventData eventData)
    {
        StopBreathing();

        if (buttonComponent != null && !buttonComponent.interactable)
        {
            PlayShakeReject();
            return;
        }

        if (PowerupManager.Instance == null) return;
        if (PowerupManager.Instance.IsOnCooldown(powerupType))
        {
            PlayShakeReject();
            return;
        }

        pointerDownScreenPos = eventData.position;
        isPointerDownOnCard = true;
        isDragging = true;

        // Juicy squash press on pointer down
        if (cardRect != null)
        {
            cardRect.DOKill();
            cardRect.DOScale(initialScale * 0.92f, 0.08f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }

        // Start targeting indicator immediately
        PowerupManager.Instance.StartAiming(powerupType, eventData.position);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (buttonComponent != null && !buttonComponent.interactable) return;
        if (PowerupManager.Instance == null) return;
        if (PowerupManager.Instance.IsOnCooldown(powerupType)) return;

        isDragging = true;

        // Smooth juicy card lift when dragging starts
        if (cardRect != null)
        {
            cardRect.DOKill();
            cardRect.DOScale(initialScale * dragScale, 0.18f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
        }

        if (canvasGroup != null)
        {
            canvasGroup.DOKill();
            canvasGroup.DOFade(0.85f, 0.15f)
                .SetUpdate(true);
        }

        if (!PowerupManager.Instance.IsAiming)
        {
            PowerupManager.Instance.StartAiming(powerupType, eventData.position);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        if (PowerupManager.Instance != null)
        {
            PowerupManager.Instance.UpdateAiming(eventData.position);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        FinishDrop(eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        FinishDrop(eventData.position);
    }

    private void FinishDrop(Vector2 releasePos)
    {
        if (!isDragging && !isPointerDownOnCard) return;

        isDragging = false;
        isPointerDownOnCard = false;
        ResetDragVisuals();

        if (PowerupManager.Instance != null && PowerupManager.Instance.IsAiming)
        {
            float dragDistance = Vector2.Distance(pointerDownScreenPos, releasePos);
            if (dragDistance > 20f)
            {
                // Released out on the battlefield: execute powerup!
                PowerupManager.Instance.FinishAiming(releasePos);
            }
            else
            {
                // Released right on the button: cycle to next powerup in category!
                PowerupManager.Instance.CancelAiming();

                if (parentBar != null && slotIndex >= 0)
                {
                    parentBar.CycleCategorySlot(slotIndex);
                }
            }
        }
    }

    private void ResetDragVisuals()
    {
        if (cardRect != null)
        {
            cardRect.DOKill();
            DOTween.Kill(cardRect);
            cardRect.localRotation = Quaternion.identity;
            cardRect.DOScale(initialScale, 0.20f)
                .SetId(this)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    ApplyRestingTransform();
                    if (isCurrentlyAffordable && enableReadyBreathing && !isDragging)
                    {
                        StartBreathing();
                    }
                })
                .OnKill(ApplyRestingTransform);
        }

        if (canvasGroup != null)
        {
            canvasGroup.DOKill();
            canvasGroup.DOFade(1.0f, 0.15f).SetUpdate(true);
        }
    }

    #endregion

    #region Juicy DOTween Animations

    /// <summary>
    /// Shake & red flash rejection feedback when clicked without enough energy or while on cooldown.
    /// Strictly resets position & rotation on both complete and kill to guarantee zero drift!
    /// Uses DOPunchRotation and DOPunchScale (NEVER modifies anchoredPosition so LayoutGroup stays intact).
    /// Strictly resets localRotation and localScale to original resting state on both complete and kill.
    /// </summary>
    public void PlayShakeReject()
    {
        ResetToRestingRotationAndScale();

        if (cardRect != null)
        {
            shakeSequence = DOTween.Sequence();
            shakeSequence.SetId(this);
            shakeSequence.SetUpdate(true);
            shakeSequence.Append(cardRect.DOPunchRotation(new Vector3(0f, 0f, 7f), 0.28f, 12, 0.5f));
            shakeSequence.Join(cardRect.DOPunchScale(new Vector3(0.06f, 0.06f, 0f), 0.28f, 8, 0.5f));
            shakeSequence.OnComplete(ApplyRestingTransform);
            shakeSequence.OnKill(ApplyRestingTransform);
        }

        if (buttonBgImage != null)
        {
            DOTween.Kill(buttonBgImage);
            buttonBgImage.DOColor(new Color(1f, 0.4f, 0.4f, 1f), 0.08f)
                .SetLoops(2, LoopType.Yoyo)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (buttonBgImage != null) buttonBgImage.color = Color.white;
                })
                .OnKill(() =>
                {
                    if (buttonBgImage != null) buttonBgImage.color = Color.white;
                });
        }

        if (energySliderController == null) energySliderController = SegmentedEnergySliderController.Instance;
        if (energySliderController != null)
        {
            energySliderController.PlayInsufficientEnergyFeedback();
        }
    }

    /// <summary>
    /// 3D card flip animation when cycling between spells in the same category.
    /// Rotates 90 degrees around Y, swaps visual content at halfway mark,
    /// then pops back open with a juicy elastic bounce!
    /// </summary>
    public void PlayCardCycleAnimation(Action onHalfwaySwap)
    {
        if (cardRect == null)
        {
            onHalfwaySwap?.Invoke();
            return;
        }

        StopBreathing();
        ResetToRestingTransform();
        ResetToRestingRotationAndScale();

        Sequence seq = DOTween.Sequence();
        seq.SetId(this);
        seq.SetUpdate(true);

        // 1. Flip halfway shut
        seq.Append(cardRect.DOScale(new Vector3(0.05f, initialScale.y * 1.05f, initialScale.z), 0.12f).SetEase(Ease.InQuad));
        seq.Join(cardRect.DORotate(new Vector3(0f, 90f, 0f), 0.12f).SetEase(Ease.InQuad));

        // 2. Midpoint swap
        seq.AppendCallback(() =>
        {
            onHalfwaySwap?.Invoke();
        });

        // 3. Flip back open with spring bounce
        seq.Append(cardRect.DOScale(initialScale, 0.20f).SetEase(Ease.OutBack));
        seq.Join(cardRect.DORotate(Vector3.zero, 0.20f).SetEase(Ease.OutBack));

        // 4. Juicy landing slam pop
        seq.Append(cardRect.DOPunchScale(Vector3.one * 0.15f, 0.25f, 4, 0.5f));

        seq.OnComplete(() =>
        {
            ResetToRestingTransform();
            ApplyRestingTransform();
            if (isCurrentlyAffordable && enableReadyBreathing)
            {
                StartBreathing();
            }
        });
        seq.OnKill(ResetToRestingTransform);
        seq.OnKill(ApplyRestingTransform);
    }

    /// <summary>
    /// Celebration pop punch when cooldown reaches 0 and card becomes available again.
    /// </summary>
    public void PlayCooldownFinishedPop()
    {
        if (cardRect != null)
        {
            ResetToRestingRotationAndScale();
            cardRect.DOPunchScale(Vector3.one * 0.20f, 0.35f, 5, 0.6f)
                .SetId(this)
                .SetUpdate(true)
                .OnComplete(ApplyRestingTransform)
                .OnKill(ApplyRestingTransform);
        }
    }

    /// <summary>
    /// Updates affordable state from SegmentedEnergySliderController.
    /// Plays an energetic pop when card transitions into affordable.
    /// </summary>
    public void SetAffordableState(bool canAfford)
    {
        if (canAfford == isCurrentlyAffordable) return;
        isCurrentlyAffordable = canAfford;

        if (canAfford)
        {
            if (cardRect != null && !isDragging)
            {
                ApplyRestingTransform();
                cardRect.DOPunchScale(Vector3.one * 0.14f, 0.35f, 4, 0.5f)
                    .SetId(this)
                    .SetUpdate(true)
                    .OnComplete(() =>
                    {
                        ApplyRestingTransform();
                        if (enableReadyBreathing && isCurrentlyAffordable && !isDragging)
                        {
                            StartBreathing();
                        }
                    })
                    .OnKill(ApplyRestingTransform);
            }

            if (costBadgeRect != null)
            {
                DOTween.Kill(costBadgeRect);
                costBadgeRect.DOPunchScale(Vector3.one * 0.3f, 0.3f, 4, 0.6f)
                    .SetId(this)
                    .SetUpdate(true)
                    .OnComplete(ApplyRestingTransform)
                    .OnKill(ApplyRestingTransform);
            }
        }
        else
        {
            StopBreathing();
            if (cardRect != null && !isDragging)
            {
                DOTween.Kill(cardRect);
                cardRect.localRotation = Quaternion.identity;
                cardRect.DOScale(initialScale, 0.20f)
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

        breathingTween = cardRect.DOScale(initialScale * 1.03f, 0.65f)
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

        if (cardRect != null && !isDragging)
        {
            cardRect.localRotation = Quaternion.identity;
            cardRect.localScale = initialScale;
        }
    }

    #endregion

    #region Editor Context Menu Preview Helpers

    [ContextMenu("Test Juice: 3D Card Flip")]
    public void Test3DCardFlip()
    {
        PlayCardCycleAnimation(null);
    }

    [ContextMenu("Test Juice: Rejection Shake")]
    public void TestRejectShake()
    {
        PlayShakeReject();
    }

    [ContextMenu("Test Juice: Cooldown Finish Pop")]
    public void TestCooldownPop()
    {
        PlayCooldownFinishedPop();
    }

    [ContextMenu("Test Juice: Toggle Ready State")]
    public void TestToggleReadyState()
    {
        SetAffordableState(!isCurrentlyAffordable);
    }

    [ContextMenu("Reset to Original Position & Rotation")]
    public void ContextResetToResting()
    {
        ResetToRestingTransform();
    }

    #endregion
}
