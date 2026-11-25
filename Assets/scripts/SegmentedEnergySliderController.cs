using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using DG.Tweening;

/// <summary>
/// Ultra-juicy, high-impact segmented energy/mana bar controller powered by DOTween.
/// Features:
/// - Smooth filling with whole-segment gain punch (hexagon pop & counter flash).
/// - Instant fill drain with fighting-game style ghost lag bar.
/// - Full-capacity energetic breathing pulse so player never caps elixir.
/// - Punchy button affordability notifications (cards pop when cost is reached).
/// - Juicy rejection shake & red flash feedback when tapping unaffordable actions.
/// </summary>
[DisallowMultipleComponent]
public class SegmentedEnergySliderController : MonoBehaviour
{
    private static SegmentedEnergySliderController instance;
    public static SegmentedEnergySliderController Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<SegmentedEnergySliderController>();
            }
            return instance;
        }
    }

    [System.Serializable]
    public class EnergyButtonConfig
    {
        [Tooltip("Descriptive label for this action")]
        public string label = "Action";

        [Tooltip("The UI Button component")]
        public Button button;

        [Tooltip("How many segment points this button costs to click (e.g. 1, 2, 3...)")]
        [Min(0.1f)] public float energyCost = 1f;

        [Tooltip("Optional text label on or inside the button showing its cost")]
        public TMP_Text costText;

        [Tooltip("Automatically set button.interactable = false when there is not enough energy")]
        public bool disableWhenInsufficient = true;

        [Tooltip("Optional CanvasGroup to dim button visuals when disabled (e.g. alpha = 0.5)")]
        public CanvasGroup canvasGroup;

        [Tooltip("Alpha opacity when button cannot be used")]
        [Range(0.1f, 1f)] public float disabledAlpha = 0.55f;

        [Tooltip("Event triggered when this button is clicked and has enough energy")]
        public UnityEvent onEnergySpent;

        // Runtime juicy tracking
        [System.NonSerialized] public bool wasAffordable = false;
        // When false, a click on this button never spends energy (powerups spend only when a cast actually executes).
        [System.NonSerialized] public bool spendOnClick = true;
        [System.NonSerialized] public JuicyCardButton juicyCard;
        [System.NonSerialized] public PowerupButtonUI powerupButton;
    }

    [Header("Segments & Energy")]
    [Tooltip("Total parts/segments the bar is divided into (default: 8 parts).")]
    [SerializeField, Min(1)] private int totalSegments = 8;

    [Tooltip("Initial energy amount on start (0 to totalSegments).")]
    [SerializeField] private float startingEnergy = 0f;

    [Tooltip("Fill speed in segments per second. (e.g., 1.0 means 1 full segment every 1 second).")]
    [SerializeField, Min(0.01f)] private float fillRate = 0.8f;

    [Tooltip("Pause or resume energy regeneration.")]
    [SerializeField] private bool isFilling = true;

    [Header("Image Fill & UI References")]
    [Tooltip("The Image component with Image Type = Filled that represents the slider bar.")]
    [SerializeField] private Image fillImage;

    [Tooltip("Secondary ghost fill image placed behind fillImage for drain-lag animation.")]
    [SerializeField] private Image ghostFillImage;

    [Tooltip("TMP Text that displays the current segment count/number (e.g., '0', '1', '2' ... '8').")]
    [SerializeField] private TMP_Text segmentNumberText;

    [Tooltip("Format for segmentNumberText. '{0}' = current segment number, '{1}' = total max segments.")]
    [SerializeField] private string textFormat = "{0}";

    [Header("Golden Hexagon Badge (Juice Reference)")]
    [Tooltip("Transform of the golden hexagon icon containing the number text.")]
    [SerializeField] private RectTransform goldenBadgeRect;
    [SerializeField] private Image goldenBadgeImage;

    [Header("Tick / Divider Markers (Optional)")]
    [Tooltip("Optional container/parent holding visual divider lines for the segments.")]
    [SerializeField] private RectTransform dividersContainer;

    [Tooltip("Optional divider sprite / prefab for the line separator.")]
    [SerializeField] private GameObject dividerPrefab;

    [Header("Buttons & Weightages")]
    [Tooltip("List of buttons that cost energy from the bar.")]
    [SerializeField] private List<EnergyButtonConfig> buttonConfigs = new List<EnergyButtonConfig>();

    [Header("Events")]
    [Tooltip("Fires on energy update: (currentEnergy, totalSegments, fillAmount0to1).")]
    public UnityEvent<float, float, float> onEnergyChanged;

    [Tooltip("Fires when the bar is completely filled to max capacity.")]
    public UnityEvent onEnergyFull;

    private float currentEnergy = 0f;
    private int lastWholeSegment = -1;
    private bool isFullEnergyBreathing = false;
    private Tween fullBreathingTween;
    private Tween ghostDrainTween;

    // Resting transforms & colors (precisely cached to guarantee zero drift after shakes)
    private Vector3 origGoldenScale = Vector3.one;
    private Vector2 origGoldenPosition = Vector2.zero;
    private Quaternion origGoldenRotation = Quaternion.identity;
    private Color origGoldenColor = Color.white;

    private Vector3 origTextScale = Vector3.one;
    private Vector3 origTextPosition = Vector3.zero;
    private Quaternion origTextRotation = Quaternion.identity;
    private Color origTextColor = Color.white;

    private Vector3 origFillScale = Vector3.one;
    private Vector2 origFillPosition = Vector2.zero;
    private Quaternion origFillRotation = Quaternion.identity;
    private Color origFillColor = Color.white;
    private bool isInitialized = false;
    private float lastInsufficientFeedbackTime = -1f;
    private const float INSUFFICIENT_FEEDBACK_COOLDOWN = 0.15f;

    public float CurrentEnergy => currentEnergy;
    public int TotalSegments => totalSegments;
    public float NormalizedFill => totalSegments > 0 ? Mathf.Clamp01(currentEnergy / totalSegments) : 0f;
    public float FillRate { get => fillRate; set => fillRate = Mathf.Max(0.01f, value); }
    public bool IsFilling { get => isFilling; set => isFilling = value; }
    public IReadOnlyList<EnergyButtonConfig> ButtonConfigs => buttonConfigs;
    public RectTransform GoldenBadgeRect => goldenBadgeRect;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }

        currentEnergy = Mathf.Clamp(startingEnergy, 0f, totalSegments);
        lastWholeSegment = Mathf.FloorToInt(currentEnergy);

        EnsureComponents();
        CacheRestingVisuals();
        SetupButtons();
        CreateDividerMarks();
    }

    private void Start()
    {
        UpdateUI(true);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < buttonConfigs.Count; i++)
        {
            var cfg = buttonConfigs[i];
            if (cfg != null && cfg.button != null)
            {
                cfg.button.onClick.RemoveAllListeners();
            }
        }

        if (fullBreathingTween != null)
        {
            Tween fb = fullBreathingTween;
            fullBreathingTween = null;
            if (fb.IsActive()) fb.Kill();
        }

        if (ghostDrainTween != null)
        {
            Tween gd = ghostDrainTween;
            ghostDrainTween = null;
            if (gd.IsActive()) gd.Kill();
        }

        ResetGoldenResting();
        ResetFillResting();
        if (ghostFillImage != null) ghostFillImage.DOKill();
        if (ghostFillImage != null) DOTween.Kill(ghostFillImage);
    }

    private void CacheRestingVisuals()
    {
        if (isInitialized) return;
        isInitialized = true;

        if (goldenBadgeRect != null)
        {
            origGoldenScale = goldenBadgeRect.localScale;
            origGoldenPosition = goldenBadgeRect.anchoredPosition;
            Vector2 pos = goldenBadgeRect.anchoredPosition;
            if (pos != Vector2.zero)
            {
                origGoldenPosition = pos;
            }
            else if (origGoldenPosition == Vector2.zero)
            {
                origGoldenPosition = new Vector2(-167.9f, 49.2f);
            }
            origGoldenRotation = goldenBadgeRect.localRotation;
        }
        else if (origGoldenPosition == Vector2.zero)
        {
            origGoldenPosition = new Vector2(-167.9f, 49.2f);
        }

        if (goldenBadgeImage != null)
        {
            origGoldenColor = goldenBadgeImage.color;
        }

        if (segmentNumberText != null)
        {
            origTextScale = segmentNumberText.transform.localScale;
            origTextPosition = segmentNumberText.transform.localPosition;
            origTextRotation = segmentNumberText.transform.localRotation;
            origTextColor = segmentNumberText.color;
        }

        if (fillImage != null)
        {
            origFillColor = fillImage.color;
            origFillScale = fillImage.rectTransform.localScale;
            origFillPosition = fillImage.rectTransform.anchoredPosition;
            origFillRotation = fillImage.rectTransform.localRotation;
        }
    }

    /// <summary>
    /// Pure visual restoration WITHOUT calling DOKill(). Safe inside OnComplete and OnKill callbacks.
    /// </summary>
    public void ApplyGoldenRestingVisuals()
    {
        if (goldenBadgeRect != null)
        {
            if (origGoldenPosition != Vector2.zero)
            {
                goldenBadgeRect.anchoredPosition = origGoldenPosition;
            }
            goldenBadgeRect.localRotation = Quaternion.identity;
            goldenBadgeRect.localScale = origGoldenScale;
        }

        if (segmentNumberText != null)
        {
            segmentNumberText.transform.localPosition = origTextPosition;
            segmentNumberText.transform.localRotation = Quaternion.identity;
            segmentNumberText.transform.localScale = origTextScale;
            segmentNumberText.color = origTextColor;
        }
    }

    public void ResetGoldenResting()
    {
        if (goldenBadgeRect != null) DOTween.Kill(goldenBadgeRect);
        if (segmentNumberText != null) DOTween.Kill(segmentNumberText.transform);
        ApplyGoldenRestingVisuals();
    }

    /// <summary>
    /// Pure visual restoration WITHOUT calling DOKill(). Safe inside OnComplete and OnKill callbacks.
    /// </summary>
    public void ApplyFillRestingVisuals()
    {
        if (fillImage != null)
        {
            fillImage.rectTransform.anchoredPosition = origFillPosition;
            fillImage.rectTransform.localRotation = origFillRotation;
            fillImage.rectTransform.localScale = origFillScale;
            fillImage.color = origFillColor;
        }
    }

    public void ResetFillResting()
    {
        if (fillImage != null) DOTween.Kill(fillImage.rectTransform);
        ApplyFillRestingVisuals();
    }

    private void Update()
    {
        if (MatchReadyScreen.Instance != null && !MatchReadyScreen.IsMatchStarted) return;

        if (isFilling && currentEnergy < totalSegments)
        {
            float prevEnergy = currentEnergy;
            currentEnergy += fillRate * Time.deltaTime;

            if (currentEnergy >= totalSegments)
            {
                currentEnergy = totalSegments;
                onEnergyFull?.Invoke();
                SetFullEnergyBreathing(true);
            }
            else if (isFullEnergyBreathing)
            {
                SetFullEnergyBreathing(false);
            }

            int currentWhole = Mathf.FloorToInt(currentEnergy);
            if (currentWhole > lastWholeSegment)
            {
                lastWholeSegment = currentWhole;
                PlaySegmentGainedJuice(currentWhole);
            }
            else if (currentWhole < lastWholeSegment)
            {
                lastWholeSegment = currentWhole;
            }

            UpdateUI(false);
            onEnergyChanged?.Invoke(currentEnergy, totalSegments, NormalizedFill);
        }
        else if (currentEnergy >= totalSegments && !isFullEnergyBreathing)
        {
            SetFullEnergyBreathing(true);
        }
    }

    /// <summary>
    /// Ensures Image, Text, Ghost drain bar, and Golden Hexagon references are linked.
    /// </summary>
    public void EnsureComponents()
    {
        if (fillImage == null)
        {
            fillImage = GetComponent<Image>();
            if (fillImage == null) fillImage = GetComponentInChildren<Image>();
        }

        if (fillImage != null)
        {
            if (fillImage.type != Image.Type.Filled)
            {
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            }

            EnsureGhostBar();
        }

        if (goldenBadgeRect == null)
        {
            GameObject goldenObj = GameObject.Find("golden");
            if (goldenObj == null && transform.parent != null)
            {
                Transform t = transform.parent.Find("golden");
                if (t != null) goldenObj = t.gameObject;
            }

            if (goldenObj != null)
            {
                goldenBadgeRect = goldenObj.GetComponent<RectTransform>();
                goldenBadgeImage = goldenObj.GetComponent<Image>();
            }
        }

        if (segmentNumberText == null)
        {
            if (goldenBadgeRect != null)
            {
                segmentNumberText = goldenBadgeRect.GetComponentInChildren<TMP_Text>(true);
            }
            if (segmentNumberText == null)
            {
                segmentNumberText = GetComponentInChildren<TMP_Text>(true);
            }
        }
    }

    private void EnsureGhostBar()
    {
        if (ghostFillImage != null || fillImage == null) return;

        Transform ghostT = fillImage.transform.Find("GhostFill");
        if (ghostT == null && fillImage.transform.parent != null)
        {
            ghostT = fillImage.transform.parent.Find("GhostFill");
        }

        if (ghostT != null)
        {
            ghostFillImage = ghostT.GetComponent<Image>();
            return;
        }

        // Dynamically create ghost drain bar placed right behind fillImage
        GameObject ghostObj = new GameObject("GhostFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        ghostObj.transform.SetParent(fillImage.transform.parent != null ? fillImage.transform.parent : fillImage.transform, false);
        ghostObj.transform.SetSiblingIndex(fillImage.transform.GetSiblingIndex());

        RectTransform ghostRt = ghostObj.GetComponent<RectTransform>();
        RectTransform fillRt = fillImage.rectTransform;
        ghostRt.anchorMin = fillRt.anchorMin;
        ghostRt.anchorMax = fillRt.anchorMax;
        ghostRt.pivot = fillRt.pivot;
        ghostRt.anchoredPosition = fillRt.anchoredPosition;
        ghostRt.sizeDelta = fillRt.sizeDelta;
        ghostRt.localScale = fillRt.localScale;

        ghostFillImage = ghostObj.GetComponent<Image>();
        ghostFillImage.sprite = fillImage.sprite;
        ghostFillImage.type = Image.Type.Filled;
        ghostFillImage.fillMethod = fillImage.fillMethod;
        ghostFillImage.fillOrigin = fillImage.fillOrigin;
        ghostFillImage.color = new Color(1f, 0.88f, 0.45f, 0.7f); // warm glowing drain tint
        ghostFillImage.fillAmount = fillImage.fillAmount;
        ghostFillImage.raycastTarget = false;
    }

    private void SetupButtons()
    {
        for (int i = 0; i < buttonConfigs.Count; i++)
        {
            int index = i;
            var cfg = buttonConfigs[index];
            if (cfg == null || cfg.button == null) continue;

            // Link or auto-add JuicyCardButton or PowerupButtonUI for unified reactive animations
            cfg.powerupButton = cfg.button.GetComponent<PowerupButtonUI>();
            cfg.juicyCard = cfg.button.GetComponent<JuicyCardButton>();
            if (cfg.juicyCard == null && cfg.powerupButton == null)
            {
                cfg.juicyCard = cfg.button.gameObject.AddComponent<JuicyCardButton>();
                cfg.juicyCard.Initialize();
            }

            if (cfg.costText != null)
            {
                cfg.costText.text = cfg.energyCost.ToString("0");
            }

            // Powerups spend energy only when their cast executes (PowerupManager), never on a tap.
            cfg.spendOnClick = cfg.powerupButton == null;
            if (cfg.spendOnClick)
            {
                cfg.button.onClick.AddListener(() => OnButtonClicked(index));
            }
        }

        UpdateButtonInteractability();
    }

    /// <summary>
    /// Consumes the button's cost from the fill amount and fires its actions if affordable.
    /// </summary>
    public bool OnButtonClicked(int buttonIndex)
    {
        if (buttonIndex < 0 || buttonIndex >= buttonConfigs.Count) return false;
        var cfg = buttonConfigs[buttonIndex];
        if (cfg == null || cfg.button == null) return false;
        if (!cfg.spendOnClick) return false;

        if (!cfg.button.interactable || currentEnergy < cfg.energyCost)
        {
            PlayInsufficientEnergyFeedback();
            return false;
        }

        return TrySpendEnergy(cfg.energyCost, () =>
        {
            cfg.onEnergySpent?.Invoke();
        });
    }

    /// <summary>
    /// Spends the specified amount of energy (in segment units).
    /// Triggers juicy drain punch and ghost drain bar lag animation.
    /// </summary>
    public bool TrySpendEnergy(float cost, Action onSuccess = null)
    {
        if (currentEnergy >= cost)
        {
            float prevEnergy = currentEnergy;
            currentEnergy -= cost;
            if (currentEnergy < 0f) currentEnergy = 0f;

            lastWholeSegment = Mathf.FloorToInt(currentEnergy);
            SetFullEnergyBreathing(false);

            PlayEnergySpentJuice(prevEnergy, currentEnergy);

            UpdateUI(false);
            onEnergyChanged?.Invoke(currentEnergy, totalSegments, NormalizedFill);
            onSuccess?.Invoke();
            return true;
        }

        PlayInsufficientEnergyFeedback();
        return false;
    }

    /// <summary>
    /// True when the bar currently holds at least the given cost.
    /// </summary>
    public bool CanAfford(float cost)
    {
        return currentEnergy >= cost;
    }

    /// <summary>
    /// Directly deducts a given weight from the fill amount.
    /// </summary>
    public void DeductEnergy(float amount)
    {
        TrySpendEnergy(amount, null);
    }

    /// <summary>
    /// Adds energy directly to the bar.
    /// </summary>
    public void AddEnergy(float amount)
    {
        currentEnergy = Mathf.Clamp(currentEnergy + amount, 0f, totalSegments);
        int newWhole = Mathf.FloorToInt(currentEnergy);
        if (newWhole > lastWholeSegment)
        {
            lastWholeSegment = newWhole;
            PlaySegmentGainedJuice(newWhole);
        }
        UpdateUI(false);
        onEnergyChanged?.Invoke(currentEnergy, totalSegments, NormalizedFill);
    }

    /// <summary>
    /// Updates the Image fillAmount, segment number text, and button interactability.
    /// </summary>
    private void UpdateUI(bool isInstant)
    {
        // 1. Update Fill Amount
        float targetFill = NormalizedFill;
        if (fillImage != null)
        {
            fillImage.fillAmount = targetFill;
        }

        // When regenerating, keep ghost fill locked to main fill
        if (ghostFillImage != null && (ghostDrainTween == null || !ghostDrainTween.IsActive()))
        {
            ghostFillImage.fillAmount = Mathf.Max(ghostFillImage.fillAmount, targetFill);
        }

        // 2. Update Number Text
        if (segmentNumberText != null)
        {
            int wholePart = Mathf.FloorToInt(currentEnergy);
            segmentNumberText.text = string.Format(textFormat, wholePart, totalSegments);
        }

        // 3. Update Button Interactability & Trigger Juicy Pop on Affordable Transition
        UpdateButtonInteractability();
    }

    /// <summary>
    /// Dynamically updates button states with juicy pop transitions when becoming affordable.
    /// </summary>
    private void UpdateButtonInteractability()
    {
        for (int i = 0; i < buttonConfigs.Count; i++)
        {
            var cfg = buttonConfigs[i];
            if (cfg == null || cfg.button == null) continue;

            if (cfg.disableWhenInsufficient)
            {
                bool canAfford = currentEnergy >= cfg.energyCost;

                if (cfg.button.interactable != canAfford)
                {
                    cfg.button.interactable = canAfford;
                }

                // Check for state transition
                if (canAfford != cfg.wasAffordable)
                {
                    cfg.wasAffordable = canAfford;
                    OnButtonAffordabilityChanged(cfg, canAfford);
                }
            }
        }
    }

    private void OnButtonAffordabilityChanged(EnergyButtonConfig cfg, bool canAfford)
    {
        if (canAfford)
        {
            // BECAME AFFORDABLE: Notify custom card scripts or play fallback bounce
            if (cfg.juicyCard != null)
            {
                cfg.juicyCard.SetAffordableState(true);
            }
            else if (cfg.powerupButton != null)
            {
                cfg.powerupButton.SetAffordableState(true);
            }
            else
            {
                if (cfg.button != null)
                {
                    cfg.button.transform.DOKill();
                    cfg.button.transform.DOPunchScale(Vector3.one * 0.13f, 0.32f, 4, 0.5f)
                        .SetUpdate(true);
                }

                if (cfg.costText != null)
                {
                    cfg.costText.transform.DOKill();
                    cfg.costText.transform.DOPunchScale(Vector3.one * 0.3f, 0.28f, 4, 0.6f)
                        .SetUpdate(true);
                }
            }

            if (cfg.canvasGroup != null)
            {
                cfg.canvasGroup.DOKill();
                cfg.canvasGroup.DOFade(1.0f, 0.16f).SetUpdate(true);
            }
        }
        else
        {
            // BECAME UNAFFORDABLE: Smooth fade out
            if (cfg.canvasGroup != null)
            {
                cfg.canvasGroup.DOKill();
                cfg.canvasGroup.DOFade(cfg.disabledAlpha, 0.2f).SetUpdate(true);
            }

            if (cfg.juicyCard != null) cfg.juicyCard.SetAffordableState(false);
            if (cfg.powerupButton != null) cfg.powerupButton.SetAffordableState(false);
        }
    }

    #region DOTween Juicy Animations

    /// <summary>
    /// Fired when an entire energy point/segment is reached (e.g. 0 -> 1, 1 -> 2).
    /// Hexagon badge bounces with elastic pop, number text punches and flashes bright gold.
    /// </summary>
    public void PlaySegmentGainedJuice(int segmentNumber)
    {
        CacheRestingVisuals();
        ResetGoldenResting();
        ResetFillResting();

        // 1. Hexagon badge punch
        if (goldenBadgeRect != null)
        {
            goldenBadgeRect.DOPunchScale(origGoldenScale * 0.28f, 0.32f, 5, 0.6f)
                .SetUpdate(true)
                .OnComplete(ApplyGoldenRestingVisuals)
                .OnKill(ApplyGoldenRestingVisuals);
        }

        // 2. Number text punch & flash
        if (segmentNumberText != null)
        {
            segmentNumberText.transform.DOPunchScale(origTextScale * 0.45f, 0.28f, 6, 0.7f)
                .SetUpdate(true);

            segmentNumberText.DOKill(true);
            segmentNumberText.color = new Color(1f, 1f, 0.5f, 1f); // flash bright
            segmentNumberText.DOColor(origTextColor, 0.25f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .OnComplete(() => segmentNumberText.color = origTextColor);
        }

        // 3. Slider fill bar squash punch
        if (fillImage != null)
        {
            fillImage.rectTransform.DOPunchScale(new Vector3(0.012f, 0.08f, 0f), 0.2f, 4, 0.5f)
                .SetUpdate(true)
                .OnComplete(ApplyFillRestingVisuals)
                .OnKill(ApplyFillRestingVisuals);
        }
    }

    /// <summary>
    /// Fired when energy is spent. Main fill drops immediately,
    /// while the ghost bar smoothly catches up after a brief hang.
    /// </summary>
    private void PlayEnergySpentJuice(float prevEnergy, float newEnergy)
    {
        CacheRestingVisuals();
        ResetGoldenResting();
        ResetFillResting();

        float targetFill = totalSegments > 0 ? Mathf.Clamp01(newEnergy / totalSegments) : 0f;

        // 1. Ghost Drain Bar Lag Animation
        if (ghostFillImage != null)
        {
            ghostFillImage.DOKill();
            ghostFillImage.fillAmount = totalSegments > 0 ? Mathf.Clamp01(prevEnergy / totalSegments) : targetFill;

            ghostDrainTween = ghostFillImage.DOFillAmount(targetFill, 0.42f)
                .SetDelay(0.12f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }

        // 2. Hexagon badge spend punch (inward squash & rebound)
        if (goldenBadgeRect != null)
        {
            goldenBadgeRect.DOPunchScale(origGoldenScale * -0.2f, 0.22f, 4, 0.5f)
                .SetUpdate(true)
                .OnComplete(ApplyGoldenRestingVisuals)
                .OnKill(ApplyGoldenRestingVisuals);
        }

        // 3. Number text spend punch
        if (segmentNumberText != null)
        {
            segmentNumberText.transform.DOPunchScale(origTextScale * -0.25f, 0.2f, 4, 0.5f)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (segmentNumberText != null)
                    {
                        segmentNumberText.transform.localPosition = origTextPosition;
                        segmentNumberText.transform.localRotation = origTextRotation;
                        segmentNumberText.transform.localScale = origTextScale;
                    }
                });
        }

        // 4. Fill image subtle spend punch
        if (fillImage != null)
        {
            fillImage.rectTransform.DOPunchScale(new Vector3(-0.02f, 0.06f, 0f), 0.2f)
                .SetUpdate(true)
                .OnComplete(ApplyFillRestingVisuals)
                .OnKill(ApplyFillRestingVisuals);
        }
    }

    /// <summary>
    /// Shakes the hexagon badge and flashes red when tapping an action with insufficient energy.
    /// Strictly resets position & rotation on both complete and kill to guarantee zero drift!
    /// </summary>
    public void PlayInsufficientEnergyFeedback()
    {
        if (Time.unscaledTime - lastInsufficientFeedbackTime < INSUFFICIENT_FEEDBACK_COOLDOWN)
        {
            return;
        }
        lastInsufficientFeedbackTime = Time.unscaledTime;

        CacheRestingVisuals();
        ResetGoldenResting();
        ResetFillResting();

        if (goldenBadgeRect != null)
        {
            goldenBadgeRect.DOKill();
            goldenBadgeRect.localRotation = Quaternion.identity;
            goldenBadgeRect.localScale = origGoldenScale;
            goldenBadgeRect.DOPunchRotation(new Vector3(0f, 0f, 8f), 0.28f, 12, 0.5f)
                .SetUpdate(true)
                .OnComplete(ApplyGoldenRestingVisuals)
                .OnKill(ApplyGoldenRestingVisuals);
        }

        if (goldenBadgeImage != null)
        {
            goldenBadgeImage.DOKill(true);
            goldenBadgeImage.DOColor(new Color(1f, 0.35f, 0.35f, 1f), 0.08f)
                .SetLoops(2, LoopType.Yoyo)
                .SetUpdate(true)
                .OnComplete(() => goldenBadgeImage.color = origGoldenColor);
        }

        if (segmentNumberText != null)
        {
            segmentNumberText.DOKill(true);
            segmentNumberText.DOColor(new Color(1f, 0.3f, 0.3f, 1f), 0.08f)
                .SetLoops(2, LoopType.Yoyo)
                .SetUpdate(true)
                .OnComplete(() => segmentNumberText.color = origTextColor);
        }

        if (fillImage != null)
        {
            fillImage.DOKill(true);
            fillImage.DOColor(new Color(1f, 0.55f, 0.55f, 1f), 0.08f)
                .SetLoops(2, LoopType.Yoyo)
                .SetUpdate(true)
                .OnComplete(() => fillImage.color = origFillColor);
        }
    }

    /// <summary>
    /// Toggles the energetic full-capacity breathing pulse when energy hits max (e.g. 8/8).
    /// </summary>
    public void SetFullEnergyBreathing(bool enabled)
    {
        CacheRestingVisuals();

        if (enabled == isFullEnergyBreathing) return;
        isFullEnergyBreathing = enabled;

        if (enabled)
        {
            if (goldenBadgeRect != null)
            {
                goldenBadgeRect.DOKill();
                goldenBadgeRect.anchoredPosition = origGoldenPosition;
                goldenBadgeRect.localRotation = origGoldenRotation;
                DOTween.Kill(goldenBadgeRect);
                ApplyGoldenRestingVisuals();
                fullBreathingTween = goldenBadgeRect.DOScale(origGoldenScale * 1.14f, 0.55f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true);
            }
        }
        else
        {
            if (fullBreathingTween != null && fullBreathingTween.IsActive())
            if (fullBreathingTween != null)
            {
                fullBreathingTween.Kill();
                Tween fb = fullBreathingTween;
                fullBreathingTween = null;
                if (fb.IsActive()) fb.Kill();
            }

            ResetGoldenResting();
            ApplyGoldenRestingVisuals();
        }
    }

    #endregion

    /// <summary>
    /// Generates vertical divider lines across the bar dividing it into totalSegments parts.
    /// </summary>
    public void CreateDividerMarks()
    {
        if (dividersContainer == null || totalSegments <= 1) return;

        for (int i = dividersContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(dividersContainer.GetChild(i).gameObject);
        }

        int dividerCount = totalSegments - 1;
        float segmentStep = 1f / totalSegments;

        for (int i = 1; i <= dividerCount; i++)
        {
            GameObject markObj;
            if (dividerPrefab != null)
            {
                markObj = Instantiate(dividerPrefab, dividersContainer);
            }
            else
            {
                markObj = new GameObject($"Divider_{i}");
                markObj.transform.SetParent(dividersContainer, false);
                Image img = markObj.AddComponent<Image>();
                img.color = new Color(0.12f, 0.12f, 0.12f, 0.75f);
                img.raycastTarget = false;
            }

            RectTransform rt = markObj.GetComponent<RectTransform>();
            if (rt != null)
            {
                float normalizedX = i * segmentStep;
                rt.anchorMin = new Vector2(normalizedX, 0f);
                rt.anchorMax = new Vector2(normalizedX, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(2f, 0f);
                rt.anchoredPosition = Vector2.zero;
            }
        }
    }

    /// <summary>
    /// Adds or updates a button configuration at runtime with its energy cost and canvasGroup.
    /// </summary>
    public EnergyButtonConfig RegisterButton(Button button, float cost, UnityAction onSpendAction = null, TMP_Text costLabel = null, CanvasGroup cg = null, string label = null, bool spendOnClick = true)
    {
        if (button == null) return null;

        EnergyButtonConfig existing = buttonConfigs.Find(c => c != null && c.button == button);
        if (existing != null)
        {
            existing.energyCost = cost;
            existing.spendOnClick = spendOnClick;
            if (costLabel != null) existing.costText = costLabel;
            if (cg != null) existing.canvasGroup = cg;
            if (!string.IsNullOrEmpty(label)) existing.label = label;
            existing.juicyCard = button.GetComponent<JuicyCardButton>();
            existing.powerupButton = button.GetComponent<PowerupButtonUI>();

            if (onSpendAction != null)
            {
                existing.onEnergySpent.RemoveAllListeners();
                existing.onEnergySpent.AddListener(onSpendAction);
            }
            if (existing.costText != null)
            {
                existing.costText.text = cost.ToString("0");
            }
            UpdateButtonInteractability();
            return existing;
        }

        PowerupButtonUI pBtn = button.GetComponent<PowerupButtonUI>();
        JuicyCardButton jCard = button.GetComponent<JuicyCardButton>();
        if (jCard == null && pBtn == null)
        {
            jCard = button.gameObject.AddComponent<JuicyCardButton>();
            jCard.Initialize();
        }

        var config = new EnergyButtonConfig
        {
            label = !string.IsNullOrEmpty(label) ? label : button.gameObject.name,
            button = button,
            energyCost = cost,
            costText = costLabel,
            canvasGroup = cg != null ? cg : button.GetComponent<CanvasGroup>(),
            disableWhenInsufficient = true,
            juicyCard = jCard,
            powerupButton = pBtn,
            spendOnClick = spendOnClick
        };

        if (onSpendAction != null)
        {
            config.onEnergySpent.AddListener(onSpendAction);
        }

        buttonConfigs.Add(config);
        int idx = buttonConfigs.Count - 1;
        if (spendOnClick)
        {
            button.onClick.AddListener(() => OnButtonClicked(idx));
        }

        if (costLabel != null)
        {
            costLabel.text = cost.ToString("0");
        }

        UpdateButtonInteractability();
        return config;
    }

    /// <summary>
    /// Updates an existing registered button's energy cost and optionally its label.
    /// </summary>
    public void UpdateButtonCost(Button button, float newCost, string newLabel = null)
    {
        if (button == null) return;
        EnergyButtonConfig cfg = buttonConfigs.Find(c => c != null && c.button == button);
        if (cfg != null)
        {
            cfg.energyCost = newCost;
            if (!string.IsNullOrEmpty(newLabel)) cfg.label = newLabel;
            if (cfg.costText != null) cfg.costText.text = newCost.ToString("0");
            UpdateButtonInteractability();
        }
    }

    /// <summary>
    /// Removes a button from the buttonConfigs list.
    /// </summary>
    public void UnregisterButton(Button button)
    {
        if (button == null) return;
        buttonConfigs.RemoveAll(c => c == null || c.button == button);
    }

    #region Editor Context Menu Preview Helpers

    [ContextMenu("Test Juice: Gain 1 Segment Punch")]
    public void TestGainSegmentPunch()
    {
        EnsureComponents();
        CacheRestingVisuals();
        PlaySegmentGainedJuice(Mathf.FloorToInt(currentEnergy) + 1);
    }

    [ContextMenu("Test Juice: Spend Energy Drain")]
    public void TestSpendEnergyDrain()
    {
        EnsureComponents();
        CacheRestingVisuals();
        float prev = currentEnergy;
        float next = Mathf.Max(0f, currentEnergy - 2f);
        PlayEnergySpentJuice(prev, next);
    }

    [ContextMenu("Test Juice: Insufficient Energy Rejection Shake")]
    public void TestInsufficientEnergyShake()
    {
        EnsureComponents();
        CacheRestingVisuals();
        PlayInsufficientEnergyFeedback();
    }

    [ContextMenu("Test Juice: Toggle Full Energy Breathing")]
    public void TestToggleFullEnergyBreathing()
    {
        EnsureComponents();
        CacheRestingVisuals();
        SetFullEnergyBreathing(!isFullEnergyBreathing);
    }

    #endregion
}
