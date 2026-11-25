using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Ultra-juicy, AAA mobile popup animation system powered by DOTween.
/// Delivers:
/// - Dynamic squash & stretch entrance with springy elastic overshoot.
/// - Layered micro-pop choreography on child elements (header, swords, VS badge, fighter cards, buttons).
/// - Living idle loop (gentle card floating, rotating radial aura glow, breathing swords centerpiece).
/// - Punchy anticipatory suck-in dismiss sequence.
/// - Full Edit Mode & Play Mode live preview support in Inspector.
/// - Automatically caches and preserves the exact inspector scale and position.
/// </summary>
[DisallowMultipleComponent]
public class JuicyPopup : MonoBehaviour
{
    [Header("Popup Target & CanvasGroup")]
    [SerializeField] private RectTransform cardRect;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image backgroundDimmer;

    [Header("Choreographed Child Elements (Auto-Found if Unassigned)")]
    [SerializeField] private RectTransform headerRibbon;
    [SerializeField] private RectTransform swordsCenterpiece;
    [SerializeField] private RectTransform vsBadge;
    [SerializeField] private RectTransform playerCard;
    [SerializeField] private RectTransform opponentCard;
    [SerializeField] private RectTransform statusBox;
    [SerializeField] private RectTransform actionButton;
    [SerializeField] private RectTransform auraGlow;

    [Header("Entrance Pop Tuning")]
    [SerializeField, Range(0.2f, 1.2f)] private float popDuration = 0.52f;
    [SerializeField, Range(1.02f, 1.25f)] private float popOvershoot = 1.09f;
    [SerializeField, Range(20f, 150f)] private float startDropDistance = 75f;
    [SerializeField] private Ease popEase = Ease.OutBack;

    [Header("Living Idle Floating & Breathing")]
    [SerializeField] private bool enableIdleFloating = true;
    [SerializeField, Range(2f, 20f)] private float floatDistance = 6f;
    [SerializeField, Range(1f, 4f)] private float floatDuration = 2.2f;

    // Bedrock Resting Transforms (precisely cached to prevent layout drift)
    [SerializeField, HideInInspector] private bool hasRecordedResting = false;
    [SerializeField, HideInInspector] private Vector3 cardRestingScale = Vector3.one;
    [SerializeField, HideInInspector] private Vector2 cardRestingPos = Vector2.zero;
    [SerializeField, HideInInspector] private Quaternion cardRestingRot = Quaternion.identity;

    [SerializeField, HideInInspector] private Vector2 headerRestingPos;
    [SerializeField, HideInInspector] private Vector3 swordsRestingScale = Vector3.one;
    [SerializeField, HideInInspector] private Vector3 vsRestingScale = Vector3.one;
    [SerializeField, HideInInspector] private Vector2 playerRestingPos;
    [SerializeField, HideInInspector] private Vector2 opponentRestingPos;
    [SerializeField, HideInInspector] private Vector3 statusRestingScale = Vector3.one;
    [SerializeField, HideInInspector] private Vector3 buttonRestingScale = Vector3.one;

    // Runtime Tweens
    private Sequence entranceSequence;
    private Sequence dismissSequence;
    private Tween floatTween;
    private Tween auraRotateTween;
    private Tween swordsBreatheTween;
    private Tween vsPulseTween;

    private bool isPoppedIn = false;
    private bool isDismissing = false;
    public bool IsPoppedIn => isPoppedIn;
    public RectTransform CardRect => cardRect != null ? cardRect : (cardRect = GetComponent<RectTransform>());

    private void Awake()
    {
        InitializeComponents();
        RecordRestingTransforms();
    }

    private void OnEnable()
    {
        InitializeComponents();
        if (!hasRecordedResting)
        {
            RecordRestingTransforms();
        }
    }

    public void InitializeComponents()
    {
        if (cardRect == null) cardRect = GetComponent<RectTransform>();
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // Auto-find dimmer in parent if not assigned
        if (backgroundDimmer == null && transform.parent != null)
        {
            backgroundDimmer = transform.parent.GetComponent<Image>();
        }

        // Auto-find child elements by name if not assigned
        if (headerRibbon == null) headerRibbon = FindChildRect("MatchHeaderRibbon") ?? FindChildRect("HeaderRibbon");
        if (swordsCenterpiece == null) swordsCenterpiece = FindChildRect("SwordsCenterpiece");
        if (vsBadge == null) vsBadge = FindChildRect("VSBadge");
        if (playerCard == null) playerCard = FindChildRect("PlayerCard");
        if (opponentCard == null) opponentCard = FindChildRect("OpponentCard");
        if (statusBox == null) statusBox = FindChildRect("StatusBox");
        if (actionButton == null) actionButton = FindChildRect("Btn_CancelMatch") ?? FindChildRect("CancelButton");
        if (auraGlow == null) auraGlow = FindChildRect("AuraGlow") ?? FindChildRect("CardAuraGlow");

        HookCancelButton();
    }

    private void HookCancelButton()
    {
        if (actionButton == null) return;
        Button cancelBtn = actionButton.GetComponent<Button>();
        if (cancelBtn != null)
        {
            cancelBtn.onClick.RemoveListener(OnCancelButtonClicked);
            cancelBtn.onClick.AddListener(OnCancelButtonClicked);
        }
    }

    /// <summary>
    /// Closes this popup and reloads the main menu instead of the legacy main panel.
    /// </summary>
    public void OnCancelButtonClicked()
    {
        if (isDismissing) return;

        MainMenuManager mmManager = FindFirstObjectByType<MainMenuManager>();
        if (mmManager != null)
        {
            mmManager.OnCancelMatchmakingClicked();
            return;
        }

        PlayPopOut(() =>
        {
            if (transform.parent != null && backgroundDimmer != null && transform.parent.gameObject != gameObject)
            {
                transform.parent.gameObject.SetActive(false);
            }

            MainMenuController menuController = MainMenuController.Instance ?? FindFirstObjectByType<MainMenuController>(FindObjectsInactive.Include);
            if (menuController != null)
            {
                menuController.gameObject.SetActive(true);
                menuController.PlayEntranceAnimation();
            }
        });
    }

    private RectTransform FindChildRect(string childName)
    {
        Transform t = transform.Find(childName);
        if (t != null) return t.GetComponent<RectTransform>();

        // Deep search
        RectTransform[] children = GetComponentsInChildren<RectTransform>(true);
        foreach (var c in children)
        {
            if (c.name.Equals(childName, StringComparison.OrdinalIgnoreCase))
                return c;
        }
        return null;
    }

    /// <summary>
    /// Records the exact resting transforms set by the designer in the Inspector.
    /// </summary>
    [ContextMenu("Record Resting Transforms")]
    public void RecordRestingTransforms()
    {
        if (cardRect == null) cardRect = GetComponent<RectTransform>();
        if (cardRect == null) return;

        cardRestingScale = cardRect.localScale;
        cardRestingPos = cardRect.anchoredPosition;
        cardRestingRot = cardRect.localRotation;

        if (headerRibbon != null) headerRestingPos = headerRibbon.anchoredPosition;
        if (swordsCenterpiece != null) swordsRestingScale = swordsCenterpiece.localScale;
        if (vsBadge != null) vsRestingScale = vsBadge.localScale;
        if (playerCard != null) playerRestingPos = playerCard.anchoredPosition;
        if (opponentCard != null) opponentRestingPos = opponentCard.anchoredPosition;
        if (statusBox != null) statusRestingScale = statusBox.localScale;
        if (actionButton != null) buttonRestingScale = actionButton.localScale;

        hasRecordedResting = true;
    }

    /// <summary>
    /// Plays the full AAA pop-in entrance animation with squash & stretch and staggered child elements.
    /// </summary>
    [ContextMenu("Play Pop In Animation")]
    public void PlayPopIn(Action onComplete = null)
    {
        KillAllTweens();
        InitializeComponents();
        if (!hasRecordedResting) RecordRestingTransforms();

        isPoppedIn = true;
        isDismissing = false;
        gameObject.SetActive(true);

        // Reset to initial pre-pop state
        cardRect.localScale = cardRestingScale * 0.22f;
        cardRect.anchoredPosition = cardRestingPos + Vector2.down * startDropDistance;
        cardRect.localRotation = cardRestingRot;

        if (canvasGroup != null) canvasGroup.alpha = 0f;

        // Dimmer background fade
        if (backgroundDimmer != null)
        {
            backgroundDimmer.DOKill();
            Color dc = backgroundDimmer.color;
            dc.a = 0f;
            backgroundDimmer.color = dc;
            backgroundDimmer.DOFade(0.92f, 0.24f).SetUpdate(true);
        }

        // Pre-position child elements for staggered entrance
        if (headerRibbon != null)
        {
            headerRibbon.DOKill();
            headerRibbon.anchoredPosition = headerRestingPos + Vector2.up * 45f;
        }
        if (swordsCenterpiece != null)
        {
            swordsCenterpiece.DOKill();
            swordsCenterpiece.localScale = swordsRestingScale * 0.4f;
        }
        if (vsBadge != null)
        {
            vsBadge.DOKill();
            vsBadge.localScale = Vector3.zero;
        }
        if (playerCard != null)
        {
            playerCard.DOKill();
            playerCard.anchoredPosition = playerRestingPos + Vector2.left * 70f;
        }
        if (opponentCard != null)
        {
            opponentCard.DOKill();
            opponentCard.anchoredPosition = opponentRestingPos + Vector2.right * 70f;
        }
        if (statusBox != null)
        {
            statusBox.DOKill();
            statusBox.localScale = statusRestingScale * 0.8f;
        }
        if (actionButton != null)
        {
            actionButton.DOKill();
            actionButton.localScale = buttonRestingScale * 0.65f;
        }

        // Build Entrance Sequence
        entranceSequence = DOTween.Sequence().SetUpdate(true);

        // 1. Main Card Pop: Fast initial burst overshoot, then spring settlement
        float tUp = popDuration * 0.45f;
        float tSettle = popDuration * 0.55f;

        // Scale explosion
        entranceSequence.Append(cardRect.DOScale(cardRestingScale * popOvershoot, tUp).SetEase(Ease.OutQuad));
        entranceSequence.Append(cardRect.DOScale(cardRestingScale, tSettle).SetEase(Ease.OutElastic, 1.35f, 0.65f));

        // Position rise from bottom
        entranceSequence.Join(cardRect.DOAnchorPos(cardRestingPos, popDuration * 0.75f).SetEase(Ease.OutCubic));

        // Fade in
        if (canvasGroup != null)
        {
            entranceSequence.Join(canvasGroup.DOFade(1f, popDuration * 0.4f).SetEase(Ease.Linear));
        }

        // 2. Staggered inner elements
        if (headerRibbon != null)
        {
            entranceSequence.Insert(0.10f, headerRibbon.DOAnchorPos(headerRestingPos, 0.38f).SetEase(Ease.OutBack, 1.5f));
        }
        if (swordsCenterpiece != null)
        {
            entranceSequence.Insert(0.14f, swordsCenterpiece.DOScale(swordsRestingScale, 0.45f).SetEase(Ease.OutBack, 1.8f));
        }
        if (vsBadge != null)
        {
            entranceSequence.Insert(0.18f, vsBadge.DOScale(vsRestingScale, 0.40f).SetEase(Ease.OutElastic, 1.4f));
        }
        if (playerCard != null)
        {
            entranceSequence.Insert(0.20f, playerCard.DOAnchorPos(playerRestingPos, 0.38f).SetEase(Ease.OutBack, 1.3f));
        }
        if (opponentCard != null)
        {
            entranceSequence.Insert(0.22f, opponentCard.DOAnchorPos(opponentRestingPos, 0.38f).SetEase(Ease.OutBack, 1.3f));
        }
        if (statusBox != null)
        {
            entranceSequence.Insert(0.26f, statusBox.DOScale(statusRestingScale, 0.35f).SetEase(Ease.OutBack, 1.3f));
        }
        if (actionButton != null)
        {
            entranceSequence.Insert(0.30f, actionButton.DOScale(buttonRestingScale, 0.42f).SetEase(Ease.OutElastic, 1.4f));
        }

        // 3. Climax punch on centerpiece when settling
        if (swordsCenterpiece != null)
        {
            entranceSequence.InsertCallback(popDuration * 0.65f, () =>
            {
                if (swordsCenterpiece != null)
                {
                    swordsCenterpiece.DOPunchScale(swordsRestingScale * 0.18f, 0.32f, 4, 0.5f).SetUpdate(true);
                }
            });
        }

        // 4. Start Living Idle on complete
        entranceSequence.OnComplete(() =>
        {
            StartIdleLoop();
            onComplete?.Invoke();
        });
    }

    /// <summary>
    /// Plays the juicy pop-out dismiss sequence (quick anticipatory swell, then fast suck-in shrink).
    /// </summary>
    [ContextMenu("Play Pop Out Animation")]
    public void PlayPopOut(Action onComplete = null)
    {
        if (isDismissing) return;

        KillAllTweens();
        InitializeComponents();
        if (!hasRecordedResting) RecordRestingTransforms();

        isPoppedIn = false;
        isDismissing = true;

        dismissSequence = DOTween.Sequence().SetUpdate(true);

        // 1. Quick anticipatory swell (1.05x)
        dismissSequence.Append(cardRect.DOScale(new Vector3(cardRestingScale.x * 1.05f, cardRestingScale.y * 1.07f, cardRestingScale.z), 0.07f).SetEase(Ease.OutQuad));

        // 2. Fast suck-in shrink to 0.2x
        dismissSequence.Append(cardRect.DOScale(cardRestingScale * 0.22f, 0.20f).SetEase(Ease.InBack, 1.4f));
        dismissSequence.Join(cardRect.DOAnchorPosY(cardRestingPos.y - 60f, 0.18f).SetEase(Ease.InQuad));

        if (canvasGroup != null)
        {
            dismissSequence.Join(canvasGroup.DOFade(0f, 0.17f).SetEase(Ease.Linear));
        }

        if (backgroundDimmer != null)
        {
            dismissSequence.Join(backgroundDimmer.DOFade(0f, 0.20f));
        }

        dismissSequence.OnComplete(() =>
        {
            ResetToResting();
            gameObject.SetActive(false);
            onComplete?.Invoke();
        });
    }

    #region Living Idle Animations

    private void StartIdleLoop()
    {
        if (!enableIdleFloating || cardRect == null) return;

        // 1. Subtle, living floating bob on the main card
        floatTween = cardRect.DOAnchorPosY(cardRestingPos.y + floatDistance, floatDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);

        // 2. Rotating radial aura glow behind the centerpiece
        if (auraGlow != null)
        {
            auraGlow.DOKill();
            auraRotateTween = auraGlow.DORotate(new Vector3(0f, 0f, -360f), 12f, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart)
                .SetUpdate(true);
        }

        // 3. Swords centerpiece gentle breathing loop
        if (swordsCenterpiece != null)
        {
            swordsCenterpiece.DOKill();
            swordsBreatheTween = swordsCenterpiece.DOScale(swordsRestingScale * 1.07f, 1.35f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        // 4. VS emblem heartbeat pulse
        if (vsBadge != null)
        {
            vsBadge.DOKill();
            vsPulseTween = vsBadge.DOScale(vsRestingScale * 1.14f, 0.85f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }
    }

    #endregion

    /// <summary>
    /// Resets all transforms to their exact 100% precision resting values.
    /// </summary>
    [ContextMenu("Reset To Resting State")]
    public void ResetToResting()
    {
        KillAllTweens();
        isDismissing = false;
        if (!hasRecordedResting) return;

        if (cardRect != null)
        {
            cardRect.localScale = cardRestingScale;
            cardRect.anchoredPosition = cardRestingPos;
            cardRect.localRotation = cardRestingRot;
        }

        if (canvasGroup != null) canvasGroup.alpha = 1f;
        if (backgroundDimmer != null)
        {
            Color c = backgroundDimmer.color;
            c.a = 0.92f;
            backgroundDimmer.color = c;
        }

        if (headerRibbon != null) headerRibbon.anchoredPosition = headerRestingPos;
        if (swordsCenterpiece != null) swordsCenterpiece.localScale = swordsRestingScale;
        if (vsBadge != null) vsBadge.localScale = vsRestingScale;
        if (playerCard != null) playerCard.anchoredPosition = playerRestingPos;
        if (opponentCard != null) opponentCard.anchoredPosition = opponentRestingPos;
        if (statusBox != null) statusBox.localScale = statusRestingScale;
        if (actionButton != null) actionButton.localScale = buttonRestingScale;
        if (auraGlow != null) auraGlow.localRotation = Quaternion.identity;
    }

    private void KillAllTweens()
    {
        entranceSequence?.Kill();
        dismissSequence?.Kill();
        floatTween?.Kill();
        auraRotateTween?.Kill();
        swordsBreatheTween?.Kill();
        vsPulseTween?.Kill();

        if (cardRect != null) cardRect.DOKill();
        if (canvasGroup != null) canvasGroup.DOKill();
        if (headerRibbon != null) headerRibbon.DOKill();
        if (swordsCenterpiece != null) swordsCenterpiece.DOKill();
        if (vsBadge != null) vsBadge.DOKill();
        if (playerCard != null) playerCard.DOKill();
        if (opponentCard != null) opponentCard.DOKill();
        if (statusBox != null) statusBox.DOKill();
        if (actionButton != null) actionButton.DOKill();
        if (auraGlow != null) auraGlow.DOKill();
        if (backgroundDimmer != null) backgroundDimmer.DOKill();
    }

    private void OnDisable()
    {
        KillAllTweens();
        ResetToResting();
    }

    private void OnDestroy()
    {
        KillAllTweens();
    }
}

