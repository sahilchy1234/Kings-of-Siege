
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;

public class BattleUIIntro : MonoBehaviour
{
    // =========================================================
    // 1. CARD REFERENCES
    // =========================================================

    [Header("POWER-UP CARDS")]
    [SerializeField] private RectTransform[] powerupButtons;

    [Header("TROOP CARDS")]
    [SerializeField] private RectTransform[] troopButtons;

    // =========================================================
    // 2. TROOP PANEL BACKGROUND
    // =========================================================

    [Header("TROOP PANEL")]
    [SerializeField] private RectTransform troopBackground;
    [SerializeField] private CanvasGroup troopBackgroundGroup;
    [SerializeField] private Image troopBackgroundGlow;

    [SerializeField] private float backgroundDuration = 0.65f;
    [SerializeField] private float backgroundStartOffset = 180f;
    [SerializeField] private float backgroundStartScale = 0.72f;
    [SerializeField] private float backgroundPunch = 0.09f;

    // =========================================================
    // 3. ENERGY BAR
    // =========================================================

    [Header("ENERGY BAR")]
    [Tooltip("Use either a Filled Image or a Slider. Prefer Filled Image.")]
    [SerializeField] private Image energyFillImage;

    [SerializeField] private Slider powerSlider;

    [SerializeField] private RectTransform energyBarTransform;
    [SerializeField] private Image sliderGlow;

    [Header("ENERGY SHINE")]
    [SerializeField] private RectTransform energyShine;
    [SerializeField] private Image energyShineImage;

    [Header("ENERGY PARTICLES")]
    [SerializeField] private ParticleSystem chargeParticles;

    // =========================================================
    // 3B. SLIDERS INTRO
    // =========================================================

    [Header("SLIDERS INTRO")]
    [SerializeField] private GameObject firstSlider;
    [SerializeField] private GameObject secondSlider;

    [SerializeField] private float sliderDuration = 0.6f;
    [SerializeField] private float sliderDelay = 0.1f;
    [SerializeField] private float sliderStagger = 0.12f;

    [SerializeField] private Vector2 firstSliderOffset = new Vector2(-220f, 0f);
    [SerializeField] private Vector2 secondSliderOffset = new Vector2(220f, 0f);

    [SerializeField] private float sliderStartScale = 0.8f;
    [SerializeField] private float sliderPunchStrength = 0.08f;
    [SerializeField] private float sliderPunchDuration = 0.28f;

    // =========================================================
    // 4. CARD ENTRANCE
    // =========================================================

    [Header("CARD ENTRANCE")]
    [SerializeField] private float cardDuration = 0.72f;
    [SerializeField] private float cardDelay = 0.09f;

    [SerializeField] private float startOffset = 260f;
    [SerializeField] private float startScale = 0.48f;
    [SerializeField] private float rotationAmount = 20f;

    [SerializeField] private float horizontalSpread = 35f;

    [Header("CARD ANTICIPATION")]
    [SerializeField] private float anticipationDuration = 0.10f;
    [SerializeField] private float anticipationScale = 0.88f;

    // =========================================================
    // 5. CARD IMPACT
    // =========================================================

    [Header("CARD IMPACT")]
    [SerializeField] private float punchStrength = 0.16f;
    [SerializeField] private float punchDuration = 0.32f;
    [SerializeField] private float landingRotation = 6f;

    [SerializeField] private int punchVibrato = 9;
    [SerializeField] private float punchElasticity = 0.75f;

    // =========================================================
    // 6. BACKGROUND AND ENERGY TIMING
    // =========================================================

    [Header("ANIMATION TIMING")]
    [SerializeField] private float troopBackgroundDelay = 0.22f;
    [SerializeField] private float troopCardsDelay = 0.28f;
    [SerializeField] private float energyStartDelay = 0.15f;

    [Header("ENERGY CHARGE")]
    [SerializeField] private float energyDuration = 1.5f;
    [SerializeField] private Ease energyEase = Ease.OutExpo;

    // =========================================================
    // 7. GLOW AND SHINE
    // =========================================================

    [Header("GLOW EFFECT")]
    [Range(0f, 1f)]
    [SerializeField] private float glowPeakAlpha = 0.95f;

    [SerializeField] private float glowDuration = 0.35f;

    [Header("SHINE EFFECT")]
    [SerializeField] private float shineDuration = 0.65f;
    [SerializeField] private float shineAlpha = 0.85f;

    // =========================================================
    // 8. IDLE ANIMATION
    // =========================================================

    [Header("IDLE CARD ANIMATION")]
    [SerializeField] private bool enableIdleMotion = true;

    [SerializeField] private float idleFloatAmount = 3f;
    [SerializeField] private float idleFloatDuration = 1.8f;

    [SerializeField] private float idleScaleAmount = 0.012f;

    // =========================================================
    // 9. GLOBAL UI IMPACT
    // =========================================================

    [Header("GLOBAL UI IMPACT")]
    [SerializeField] private RectTransform uiImpactRoot;

    [SerializeField] private float globalImpactStrength = 5f;
    [SerializeField] private float globalImpactDuration = 0.24f;

    // =========================================================
    // 10. SOUND
    // =========================================================

    [Header("SOUND")]
    [SerializeField] private AudioSource audioSource;

    [SerializeField] private AudioClip cardLandSound;
    [SerializeField] private AudioClip backgroundLandSound;
    [SerializeField] private AudioClip energyChargeSound;
    [SerializeField] private AudioClip energyCompleteSound;

    [Range(0f, 1f)]
    [SerializeField] private float cardSoundVolume = 0.35f;

    [Range(0f, 1f)]
    [SerializeField] private float energySoundVolume = 0.65f;

    // =========================================================
    // 11. OPTIONS
    // =========================================================

    [Header("OPTIONS")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool useUnscaledTime = true;

    // =========================================================
    // INTERNAL DATA
    // =========================================================

    private class CardState
    {
        public RectTransform rect;
        public CanvasGroup group;

        public Vector2 position;
        public Vector3 scale;
        public Quaternion rotation;

        public float originalAlpha;
    }

    private class SliderState
    {
        public RectTransform rect;
        public CanvasGroup group;

        public Vector2 position;
        public Vector3 scale;
        public Vector2 startOffset;

        public float originalAlpha;
    }

    private CardState[] powerupStates;
    private CardState[] troopStates;

    private SliderState firstSliderState;
    private SliderState secondSliderState;

    private Sequence masterSequence;

    private Vector2 backgroundPosition;
    private Vector3 backgroundScale;

    private float backgroundAlpha = 1f;

    private float targetFillAmount;
    private float targetSliderValue;

    private bool initialized;

    // Parent LayoutGroups are disabled during the intro so DOTween can
    // freely move cards, then re-enabled once the sequence completes.
    private UnityEngine.UI.LayoutGroup powerupLayoutGroup;
    private UnityEngine.UI.LayoutGroup troopLayoutGroup;

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        // Do NOT cache positions here — the layout system has not run yet.
        // Caching happens in Initialize(), called from PlayIntro() after a
        // small delay so anchoredPositions are properly computed.
    }

    private void Start()
    {
        Invoke("PlayIntro", 0.1f);
    }

    private void Initialize()
    {
        if (initialized)
            return;

        // Ensure UI layout positions are calculated before we snapshot them.
        Canvas.ForceUpdateCanvases();

        powerupStates = CacheCards(powerupButtons);
        troopStates   = CacheCards(troopButtons);

        // Remember any LayoutGroup that controls these card sets so we can
        // disable it during the intro (otherwise it overrides anchoredPosition
        // every frame and all cards collapse to the same spot).
        if (powerupButtons != null && powerupButtons.Length > 0 && powerupButtons[0] != null)
            powerupLayoutGroup = powerupButtons[0].parent?.GetComponent<UnityEngine.UI.LayoutGroup>();
        if (troopButtons != null && troopButtons.Length > 0 && troopButtons[0] != null)
            troopLayoutGroup = troopButtons[0].parent?.GetComponent<UnityEngine.UI.LayoutGroup>();

        CacheBackground();

        firstSliderState = CacheSlider(firstSlider, firstSliderOffset);
        secondSliderState = CacheSlider(secondSlider, secondSliderOffset);

        if (energyFillImage != null)
            targetFillAmount = energyFillImage.fillAmount;

        if (powerSlider != null)
            targetSliderValue = powerSlider.value;

        initialized = true;
    }

    // =========================================================
    // CACHE CARD STATES
    // =========================================================

    private CardState[] CacheCards(RectTransform[] buttons)
    {
        if (buttons == null)
            return new CardState[0];

        List<CardState> result = new List<CardState>();

        foreach (RectTransform rect in buttons)
        {
            if (rect == null)
                continue;

            CanvasGroup group =
                rect.GetComponent<CanvasGroup>();

            if (group == null)
                group = rect.gameObject.AddComponent<CanvasGroup>();

            // Power cards can already be shrunk by the intro when this runs, so use their authored resting scale.
            var powerCard = rect.GetComponent<PowerupButtonUI>();
            Vector3 restingScale = powerCard != null ? powerCard.RestingScale : rect.localScale;

            result.Add(new CardState
            {
                rect = rect,
                group = group,

                position = rect.anchoredPosition,
                scale = restingScale,
                rotation = rect.localRotation,

                originalAlpha = group.alpha
            });
        }

        return result.ToArray();
    }

    // =========================================================
    // CACHE BACKGROUND
    // =========================================================

    private void CacheBackground()
    {
        if (troopBackground == null)
            return;

        if (troopBackgroundGroup == null)
        {
            troopBackgroundGroup =
                troopBackground.GetComponent<CanvasGroup>();

            if (troopBackgroundGroup == null)
            {
                troopBackgroundGroup =
                    troopBackground.gameObject.AddComponent<CanvasGroup>();
            }
        }

        backgroundPosition = troopBackground.anchoredPosition;
        backgroundScale = troopBackground.localScale;

        backgroundAlpha = troopBackgroundGroup.alpha;
    }

    // =========================================================
    // MAIN INTRO
    // =========================================================

    public void PlayIntro()
    {
        Initialize();

        KillAnimations();

        ResetCards(powerupStates);
        ResetCards(troopStates);

        ResetBackground();
        ResetEnergy();
        ResetSlider(firstSliderState);
        ResetSlider(secondSliderState);

        // Disable layout groups so DOTween can freely move each card to its
        // cached position without the layout engine overriding every frame.
        if (powerupLayoutGroup != null) powerupLayoutGroup.enabled = false;
        if (troopLayoutGroup   != null) troopLayoutGroup.enabled   = false;

        masterSequence = DOTween.Sequence();
        masterSequence.SetUpdate(useUnscaledTime);

        // ---------------------------------------------
        // SLIDERS INTRO
        // ---------------------------------------------

        AnimateSlider(firstSliderState, sliderDelay);
        AnimateSlider(secondSliderState, sliderDelay + sliderStagger);

        // ---------------------------------------------
        // PHASE 1: POWER-UP CARDS FLY IN
        // ---------------------------------------------

        AnimateCards(
            powerupStates,
            true,
            0f
        );

        // ---------------------------------------------
        // PHASE 2: TROOP BACKGROUND SLAMS IN
        // ---------------------------------------------

        float troopStart =
            powerupStates.Length * cardDelay +
            troopBackgroundDelay;

        AnimateBackground(troopStart);

        // ---------------------------------------------
        // PHASE 3: TROOP CARDS ENTER
        // ---------------------------------------------

        float troopCardsStart =
            troopStart + troopCardsDelay;

        AnimateCards(
            troopStates,
            false,
            troopCardsStart
        );

        // ---------------------------------------------
        // PHASE 4: ENERGY CHARGING
        // ---------------------------------------------

        float energyStart =
            troopCardsStart +
            troopStates.Length * cardDelay +
            energyStartDelay;

        AnimateEnergy(energyStart);

        // ---------------------------------------------
        // PHASE 5: FINAL INTERACTION
        // ---------------------------------------------

        masterSequence.OnComplete(() =>
        {
            // Cards are now at their correct positions; safe to re-enable
            // the layout group (it won't move anything since positions match).
            if (powerupLayoutGroup != null) powerupLayoutGroup.enabled = true;
            if (troopLayoutGroup   != null) troopLayoutGroup.enabled   = true;

            EnableCards(powerupStates);
            EnableCards(troopStates);

            PlayCompleteSound();

            if (enableIdleMotion)
            {
                StartIdleMotion(powerupStates);
                StartIdleMotion(troopStates);
            }
        });
    }

    // =========================================================
    // KILL PREVIOUS ANIMATIONS
    // =========================================================

    private void KillAnimations()
    {
        masterSequence?.Kill();

        KillCardTweens(powerupStates);
        KillCardTweens(troopStates);

        KillSlider(firstSliderState);
        KillSlider(secondSliderState);

        if (troopBackground != null)
            troopBackground.DOKill();

        if (troopBackgroundGroup != null)
            troopBackgroundGroup.DOKill();

        if (troopBackgroundGlow != null)
            troopBackgroundGlow.DOKill();

        if (sliderGlow != null)
            sliderGlow.DOKill();

        if (energyBarTransform != null)
            energyBarTransform.DOKill();

        if (energyShine != null)
            energyShine.DOKill();

        if (energyShineImage != null)
            energyShineImage.DOKill();

        if (uiImpactRoot != null)
            uiImpactRoot.DOKill();
    }

    private void KillCardTweens(CardState[] states)
    {
        if (states == null)
            return;

        foreach (CardState card in states)
        {
            if (card.rect != null)
                card.rect.DOKill();

            if (card.group != null)
                card.group.DOKill();
        }
    }

    // =========================================================
    // RESET CARDS
    // =========================================================

    private void ResetCards(CardState[] states)
    {
        if (states == null)
            return;

        foreach (CardState card in states)
        {
            if (card.rect == null)
                continue;

            card.rect.anchoredPosition = card.position;
            card.rect.localScale = card.scale;
            card.rect.localRotation = card.rotation;

            card.group.alpha = 0f;
          
        }
    }

    // =========================================================
    // RESET BACKGROUND
    // =========================================================

    private void ResetBackground()
    {
        if (troopBackground == null)
            return;

        troopBackground.anchoredPosition =
            backgroundPosition +
            Vector2.down * backgroundStartOffset;

        troopBackground.localScale =
            backgroundScale * backgroundStartScale;

        troopBackgroundGroup.alpha = 0f;

        // Background must never block troop buttons.
     

        if (troopBackgroundGlow != null)
        {
            Color c = troopBackgroundGlow.color;
            c.a = 0f;
            troopBackgroundGlow.color = c;
        }
    }

    // =========================================================
    // RESET ENERGY
    // =========================================================

    private void ResetEnergy()
    {
        if (energyFillImage != null)
            energyFillImage.fillAmount = 0f;
        else if (powerSlider != null)
            powerSlider.value = powerSlider.minValue;

        if (sliderGlow != null)
        {
            Color c = sliderGlow.color;
            c.a = 0f;
            sliderGlow.color = c;
        }

        if (energyShineImage != null)
        {
            Color c = energyShineImage.color;
            c.a = 0f;
            energyShineImage.color = c;
        }

        if (energyShine != null)
        {
            energyShine.anchoredPosition =
                new Vector2(-GetEnergyWidth() * 0.6f, 0f);
        }

        if (chargeParticles != null)
        {
            chargeParticles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }
    }

    // =========================================================
    // SLIDERS INTRO
    // =========================================================

    private SliderState CacheSlider(GameObject sliderObj, Vector2 offset)
    {
        if (sliderObj == null)
            return null;

        RectTransform rect = sliderObj.GetComponent<RectTransform>();
        if (rect == null)
            return null;

        CanvasGroup group = sliderObj.GetComponent<CanvasGroup>();
        if (group == null)
            group = sliderObj.AddComponent<CanvasGroup>();

        return new SliderState
        {
            rect = rect,
            group = group,
            position = rect.anchoredPosition,
            scale = rect.localScale,
            startOffset = offset,
            originalAlpha = group.alpha
        };
    }

    private void KillSlider(SliderState state)
    {
        if (state == null)
            return;

        if (state.rect != null)
            state.rect.DOKill();

        if (state.group != null)
            state.group.DOKill();
    }

    private void ResetSlider(SliderState state)
    {
        if (state == null || state.rect == null)
            return;

        state.rect.anchoredPosition = state.position + state.startOffset;
        state.rect.localScale = state.scale * sliderStartScale;

        if (state.group != null)
            state.group.alpha = 0f;
    }

    private void AnimateSlider(SliderState state, float delay)
    {
        if (state == null || state.rect == null)
            return;

        Sequence seq = DOTween.Sequence();
        seq.SetUpdate(useUnscaledTime);

        seq.Append(
            state.rect.DOAnchorPos(
                state.position,
                sliderDuration
            ).SetEase(Ease.OutBack, 1.5f)
        );

        seq.Join(
            state.rect.DOScale(
                state.scale,
                sliderDuration
            ).SetEase(Ease.OutBack, 1.6f)
        );

        if (state.group != null)
        {
            seq.Join(
                state.group.DOFade(
                    state.originalAlpha,
                    sliderDuration * 0.6f
                )
            );
        }

        if (sliderPunchStrength > 0f && sliderPunchDuration > 0f)
        {
            seq.Append(
                state.rect.DOPunchScale(
                    new Vector3(sliderPunchStrength, sliderPunchStrength, 0f),
                    sliderPunchDuration,
                    8,
                    0.75f
                )
            );
        }

        masterSequence.Insert(delay, seq);
    }

    // =========================================================
    // TROOP BACKGROUND ANIMATION
    // =========================================================

    private void AnimateBackground(float delay)
    {
        if (troopBackground == null)
            return;

        Sequence bg = DOTween.Sequence();

        bg.SetUpdate(useUnscaledTime);

        // Main entrance.
        bg.Append(
            troopBackground.DOAnchorPos(
                backgroundPosition,
                backgroundDuration
            ).SetEase(Ease.OutBack, 1.7f)
        );

        bg.Join(
            troopBackground.DOScale(
                backgroundScale,
                backgroundDuration
            ).SetEase(Ease.OutBack, 1.8f)
        );

        bg.Join(
            troopBackgroundGroup.DOFade(
                backgroundAlpha,
                backgroundDuration * 0.65f
            )
        );

        // Impact.
        bg.Append(
            troopBackground.DOPunchScale(
                new Vector3(
                    backgroundPunch,
                    backgroundPunch,
                    0f
                ),
                0.3f,
                8,
                0.75f
            )
        );

        bg.InsertCallback(
            backgroundDuration,
            () =>
            {
                PlaySound(backgroundLandSound, 0.55f);
                GlobalImpact();
            }
        );

        masterSequence.Insert(delay, bg);

        // Background glow.
        if (troopBackgroundGlow != null)
        {
            Sequence glow = DOTween.Sequence();

            glow.Append(
                troopBackgroundGlow.DOFade(
                    0.85f,
                    0.25f
                )
            );

            glow.Append(
                troopBackgroundGlow.DOFade(
                    0.25f,
                    0.5f
                )
            );

            glow.Append(
                troopBackgroundGlow.DOFade(
                    0f,
                    0.4f
                )
            );

            glow.SetUpdate(useUnscaledTime);

            masterSequence.Insert(delay, glow);
        }
    }

    // =========================================================
    // CARD ANIMATION
    // =========================================================

    private void AnimateCards(
        CardState[] states,
        bool fromTop,
        float groupDelay)
    {
        if (states == null)
            return;

        for (int i = 0; i < states.Length; i++)
        {
            CardState card = states[i];

            if (card.rect == null)
                continue;

            float direction = fromTop ? 1f : -1f;

            // Alternate horizontal entrance for a more organic feel.
            float sideOffset =
                (i % 2 == 0 ? -1f : 1f) * horizontalSpread;

            Vector2 startPosition =
                card.position +
                Vector2.up * startOffset * direction +
                Vector2.right * sideOffset;

            float startRotation =
                (fromTop ? 1f : -1f) *
                rotationAmount *
                (i % 2 == 0 ? 1f : 0.75f);

            card.rect.anchoredPosition = startPosition;

            card.rect.localScale =
                card.scale * startScale;

            card.rect.localRotation =
                card.rotation *
                Quaternion.Euler(
                    0f,
                    0f,
                    startRotation
                );

            // Each card gets its own timeline.
            Sequence cardSequence = DOTween.Sequence();

            cardSequence.SetUpdate(useUnscaledTime);

            // -----------------------------------------
            // ANTICIPATION
            // -----------------------------------------

            cardSequence.Append(
                card.rect.DOScale(
                    card.scale * anticipationScale,
                    anticipationDuration
                ).SetEase(Ease.OutQuad)
            );

            // -----------------------------------------
            // FLY INTO POSITION
            // -----------------------------------------

            cardSequence.Append(
                card.rect.DOAnchorPos(
                    card.position,
                    cardDuration
                ).SetEase(Ease.OutBack, 1.7f)
            );

            cardSequence.Join(
                card.rect.DOScale(
                    card.scale,
                    cardDuration
                ).SetEase(Ease.OutBack, 1.9f)
            );

            cardSequence.Join(
                card.rect.DOLocalRotateQuaternion(
                    card.rotation,
                    cardDuration
                ).SetEase(Ease.OutCubic)
            );

            cardSequence.Join(
                card.group.DOFade(
                    card.originalAlpha,
                    cardDuration * 0.55f
                )
            );

            // -----------------------------------------
            // LANDING SOUND
            // -----------------------------------------

            cardSequence.AppendCallback(() =>
            {
                PlaySound(cardLandSound, cardSoundVolume);
            });

            // -----------------------------------------
            // IMPACT BOUNCE
            // -----------------------------------------

            cardSequence.Append(
                card.rect.DOPunchScale(
                    new Vector3(
                        punchStrength,
                        punchStrength,
                        0f
                    ),
                    punchDuration,
                    punchVibrato,
                    punchElasticity
                )
            );

            cardSequence.Join(
                card.rect.DOPunchRotation(
                    new Vector3(
                        0f,
                        0f,
                        fromTop
                            ? -landingRotation
                            : landingRotation
                    ),
                    punchDuration,
                    punchVibrato,
                    punchElasticity
                )
            );

            // -----------------------------------------
            // INSERT INTO MAIN TIMELINE
            // -----------------------------------------

            float individualDelay =
                groupDelay + i * cardDelay;

            masterSequence.Insert(
                individualDelay,
                cardSequence
            );
        }
    }

    // =========================================================
    // ENERGY CHARGE ANIMATION
    // =========================================================

    private void AnimateEnergy(float delay)
    {
        if (energyFillImage == null && powerSlider == null)
            return;

        // Prefer Filled Image when assigned.
        bool useImage = energyFillImage != null;

        float target = useImage
            ? targetFillAmount
            : Mathf.InverseLerp(
                powerSlider.minValue,
                powerSlider.maxValue,
                targetSliderValue
            );

        // ---------------------------------------------
        // ENERGY FILL
        // ---------------------------------------------

        masterSequence.Insert(
            delay,
            DOTween.To(
                () => 0f,
                value =>
                {
                    if (useImage)
                    {
                        energyFillImage.fillAmount =
                            Mathf.Lerp(0f, target, value);
                    }
                    else
                    {
                        powerSlider.value = Mathf.Lerp(
                            powerSlider.minValue,
                            targetSliderValue,
                            value
                        );
                    }
                },
                1f,
                energyDuration
            ).SetEase(energyEase)
        );

        // ---------------------------------------------
        // CHARGE SOUND AND PARTICLES
        // ---------------------------------------------

        masterSequence.InsertCallback(delay, () =>
        {
            PlaySound(
                energyChargeSound,
                energySoundVolume
            );

            if (chargeParticles != null)
                chargeParticles.Play();
        });

        // ---------------------------------------------
        // GLOW PULSE
        // ---------------------------------------------

        if (sliderGlow != null)
        {
            Sequence glow = DOTween.Sequence();

            glow.Append(
                sliderGlow.DOFade(
                    glowPeakAlpha,
                    glowDuration
                ).SetEase(Ease.OutQuad)
            );

            glow.Append(
                sliderGlow.DOFade(
                    0.3f,
                    glowDuration
                ).SetEase(Ease.InOutSine)
            );

            glow.Append(
                sliderGlow.DOFade(
                    0f,
                    glowDuration
                )
            );

            glow.SetUpdate(useUnscaledTime);

            masterSequence.Insert(delay, glow);
        }

        // ---------------------------------------------
        // SHINE SWEEP
        // ---------------------------------------------

        if (energyShine != null && energyShineImage != null)
        {
            float width = GetEnergyWidth();

            energyShine.anchoredPosition =
                new Vector2(-width * 0.6f, 0f);

            Color c = energyShineImage.color;
            c.a = 0f;
            energyShineImage.color = c;

            Sequence shine = DOTween.Sequence();

            shine.Append(
                energyShineImage.DOFade(
                    shineAlpha,
                    0.12f
                )
            );

            shine.Join(
                energyShine.DOAnchorPosX(
                    width * 0.6f,
                    shineDuration
                ).SetEase(Ease.InOutSine)
            );

            shine.Append(
                energyShineImage.DOFade(
                    0f,
                    0.15f
                )
            );

            shine.SetUpdate(useUnscaledTime);

            masterSequence.Insert(delay, shine);
        }

        // ---------------------------------------------
        // ENERGY BAR IMPACT
        // ---------------------------------------------

        if (energyBarTransform != null)
        {
            masterSequence.InsertCallback(
                delay + energyDuration,
                () =>
                {
                    energyBarTransform.DOPunchScale(
                        new Vector3(0.09f, 0.16f, 0f),
                        0.4f,
                        10,
                        0.75f
                    ).SetUpdate(useUnscaledTime);

                    GlobalImpact();
                    PlaySound(energyCompleteSound, 0.7f);
                }
            );
        }

        // ---------------------------------------------
        // STOP PARTICLES
        // ---------------------------------------------

        if (chargeParticles != null)
        {
            masterSequence.InsertCallback(
                delay + energyDuration,
                () =>
                {
                    chargeParticles.Stop(
                        true,
                        ParticleSystemStopBehavior.StopEmitting
                    );
                }
            );
        }
    }

    private float GetEnergyWidth()
    {
        if (energyBarTransform != null)
            return energyBarTransform.rect.width;

        if (energyShine != null &&
            energyShine.parent is RectTransform parent)
        {
            return parent.rect.width;
        }

        return 300f;
    }

    // =========================================================
    // GLOBAL IMPACT
    // =========================================================

    private void GlobalImpact()
    {
        if (uiImpactRoot == null)
            return;

        uiImpactRoot.DOPunchAnchorPos(
            new Vector2(0f, -globalImpactStrength),
            globalImpactDuration,
            8,
            0.7f
        ).SetUpdate(useUnscaledTime);
    }

    // =========================================================
    // IDLE CARD ANIMATION
    // =========================================================

    private void StartIdleMotion(CardState[] states)
    {
        if (states == null)
            return;

        foreach (CardState card in states)
        {
            if (card.rect == null)
                continue;

            // Very subtle movement to keep cards alive.
            Sequence idle = DOTween.Sequence();

            idle.Append(
                card.rect.DOAnchorPosY(
                    card.position.y + idleFloatAmount,
                    idleFloatDuration
                ).SetEase(Ease.InOutSine)
            );

            idle.Join(
                card.rect.DOScale(
                    card.scale * (1f + idleScaleAmount),
                    idleFloatDuration
                ).SetEase(Ease.InOutSine)
            );

            idle.SetLoops(-1, LoopType.Yoyo);
            idle.SetUpdate(useUnscaledTime);
        }
    }

    // =========================================================
    // ENABLE INTERACTION
    // =========================================================

    private void EnableCards(CardState[] states)
    {
        if (states == null)
            return;

        foreach (CardState card in states)
        {
            if (card.group == null)
                continue;

            card.group.interactable = true;
            card.group.blocksRaycasts = true;
        }
    }

    // =========================================================
    // SOUND
    // =========================================================

    private void PlaySound(AudioClip clip, float volume)
    {
        if (audioSource == null || clip == null)
            return;

        audioSource.PlayOneShot(clip, volume);
    }

    private void PlayCompleteSound()
    {
        // Optional: add a separate UI-ready sound here if needed.
    }

    // =========================================================
    // PUBLIC CONTROLS
    // =========================================================

    public void ReplayIntro()
    {
        PlayIntro();
    }

    public void StopIntro()
    {
        KillAnimations();
    }

    // =========================================================
    // CLEANUP
    // =========================================================

    private void OnDisable()
    {
        KillAnimations();
    }

    private void OnDestroy()
    {
        KillAnimations();
    }
}
