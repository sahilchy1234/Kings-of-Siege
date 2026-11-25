using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace Siege.Chests
{
    /// <summary>
    /// Represents an individual chest slot inside the 'Your Chests' selection popup.
    /// Manages real-time countdowns, state persistence, visual updates, and juicy DOTween feedback.
    /// Strictly preserves authored scene transform coordinates and layout.
    /// </summary>
    [DisallowMultipleComponent]
    public class ChestSlotUI : MonoBehaviour
    {
        [Header("Slot Identity")]
        [SerializeField] private int slotIndex = 0;
        [SerializeField] private ChestType chestType = ChestType.Silver;
        [SerializeField] private ChestSlotState currentState = ChestSlotState.Locked;

        [Header("UI Component Bindings")]
        [SerializeField] private Image chestImage;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Button actionButton;
        [SerializeField] private Image buttonImage;
        [SerializeField] private TextMeshProUGUI buttonText;
        [SerializeField] private Image timeIcon;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private GameObject backglowObject;
        [SerializeField] private Image backglowImage;

        [Header("Button Style Sprites")]
        [SerializeField] private Sprite readyButtonSprite;     // Green_Empty
        [SerializeField] private Sprite unlockButtonSprite;    // Cyan_Empty
        [SerializeField] private Sprite waitingButtonSprite;   // Grey_Empty

        private ChestConfig config;
        private DateTime unlockEndTime;
        private long queuedAtBinary = 0; // UTC time this chest joined the unlock queue (0 = not queued)
        private RectTransform rectTransform;
        private Vector3 authoredScale = Vector3.one;
        private Vector2 authoredPos = Vector2.zero;
        private Vector3 buttonAuthoredScale = Vector3.one;

        private Tween buttonPulseTween;
        private Tween glowPulseTween;
        private float lastTickTime = 0f;
        private bool isInitialized = false;

        public int SlotIndex => slotIndex;
        public ChestType Type => chestType;
        public ChestSlotState CurrentState => currentState;
        public ChestConfig Config => config;
        public long QueuedAtBinary => queuedAtBinary;
        public DateTime UnlockEndTime => unlockEndTime;

        /// <summary>
        /// True when this slot holds the single unlock slot: its timer is running, or it finished
        /// but has not been claimed yet. Only one slot may be occupied at a time.
        /// </summary>
        public bool IsOccupyingUnlockSlot => currentState == ChestSlotState.Unlocking || currentState == ChestSlotState.Ready;

        public float UnlockHours
        {
            get
            {
                if (config != null && config.unlockHours > 0) return config.unlockHours;
                switch (chestType)
                {
                    case ChestType.Silver: return 2f;
                    case ChestType.Gold: return 8f;
                    case ChestType.Master: return 24f;
                    default: return 4f;
                }
            }
        }

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            authoredScale = transform.localScale;
            if (rectTransform != null) authoredPos = rectTransform.anchoredPosition;

            if (actionButton != null && actionButton.transform != transform)
            {
                buttonAuthoredScale = actionButton.transform.localScale;
                actionButton.onClick.RemoveAllListeners();
                actionButton.onClick.AddListener(OnSlotClicked);
            }

            if (backglowObject != null && backglowImage == null)
            {
                backglowImage = backglowObject.GetComponent<Image>();
            }
        }

        private void Start()
        {
            if (!isInitialized)
            {
                Initialize(slotIndex, chestType, config, readyButtonSprite, unlockButtonSprite, waitingButtonSprite);
            }
        }

        private void Update()
        {
            if (currentState == ChestSlotState.Unlocking)
            {
                if (Time.unscaledTime - lastTickTime >= 1.0f)
                {
                    lastTickTime = Time.unscaledTime;
                    UpdateCountdown();
                }
            }
        }

        private void OnDestroy()
        {
            KillAllTweens();
        }

        public void Initialize(int index, ChestType type, ChestConfig cfg, Sprite greenBtn, Sprite cyanBtn, Sprite greyBtn)
        {
            isInitialized = true;
            slotIndex = index;
            chestType = type;
            config = cfg;
            readyButtonSprite = greenBtn;
            unlockButtonSprite = cyanBtn;
            waitingButtonSprite = greyBtn;

            if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
            authoredScale = transform.localScale;
            if (rectTransform != null && (authoredPos == Vector2.zero || authoredPos.x == 0 && authoredPos.y == 0))
            {
                authoredPos = rectTransform.anchoredPosition;
            }

            if (actionButton != null && actionButton.transform != transform)
            {
                buttonAuthoredScale = actionButton.transform.localScale;
            }

            LoadState();
            ApplyStateVisuals(false);
        }

        public void ResetToResting()
        {
            KillAllTweens();
            if (rectTransform != null)
            {
                rectTransform.localScale = authoredScale;
            }
            if (actionButton != null && actionButton.transform != transform)
            {
                actionButton.transform.localScale = buttonAuthoredScale;
            }
        }

        public void PlayEntrancePunch(float delay)
        {
            if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
            rectTransform.DOKill();
            rectTransform.localScale = authoredScale * 0.90f;
            rectTransform.DOScale(authoredScale, 0.30f)
                .SetDelay(delay)
                .SetEase(Ease.OutBack);
        }

        #region State Persistence

        private string PrefKey(string key) => $"Siege_ChestSlot_{slotIndex}_{key}";

        public void LoadState()
        {
            if (config == null || config.unlockHours <= 0)
            {
                if (ChestSystemController.Instance != null)
                {
                    config = ChestSystemController.Instance.GetConfig(chestType);
                }
                if (config == null || config.unlockHours <= 0)
                {
                    config = ChestConfig.CreateDefault(chestType);
                }
            }

            // Default slots layout:
            // Slot 0 (Silver) -> Ready (immediate gratification for testing)
            // Slot 1 (Gold) -> Waiting
            // Slot 2 (Master) -> Locked
            int defaultState = (int)(slotIndex == 0 ? ChestSlotState.Ready : (slotIndex == 1 ? ChestSlotState.Waiting : ChestSlotState.Locked));
            int savedState = PlayerPrefs.GetInt(PrefKey("State"), defaultState);
            currentState = (ChestSlotState)savedState;

            string endStr = PlayerPrefs.GetString(PrefKey("EndTime"), string.Empty);
            if (!string.IsNullOrEmpty(endStr) && long.TryParse(endStr, out long binaryTime))
            {
                unlockEndTime = DateTime.FromBinary(binaryTime);
                if (currentState == ChestSlotState.Unlocking && DateTime.UtcNow >= unlockEndTime)
                {
                    currentState = ChestSlotState.Ready;
                    SaveState();
                }
            }

            queuedAtBinary = long.TryParse(PlayerPrefs.GetString(PrefKey("QueuedAt"), "0"), out long q) ? q : 0;
        }

        public void SaveState()
        {
            PlayerPrefs.SetInt(PrefKey("State"), (int)currentState);
            PlayerPrefs.SetInt(PrefKey("Type"), (int)chestType);
            PlayerPrefs.SetString(PrefKey("EndTime"), unlockEndTime.ToBinary().ToString());
            PlayerPrefs.SetString(PrefKey("QueuedAt"), queuedAtBinary.ToString());
            PlayerPrefs.Save();
        }

        /// <summary>Puts this chest in the unlock queue. It starts automatically once the unlock slot frees up.</summary>
        public void QueueForUnlock()
        {
            queuedAtBinary = DateTime.UtcNow.ToBinary();
            SetState(ChestSlotState.Waiting);
        }

        public void SetState(ChestSlotState newState, DateTime? endTime = null)
        {
            currentState = newState;
            if (newState != ChestSlotState.Waiting)
            {
                queuedAtBinary = 0; // only queued chests keep a queue position
            }
            if (endTime.HasValue)
            {
                unlockEndTime = endTime.Value;
            }
            SaveState();
            ApplyStateVisuals(true);
        }

        #endregion

        #region Visual & Animation Updates

        public void ApplyStateVisuals(bool animateTransition)
        {
            KillAllTweens();

            if (titleText != null)
            {
                titleText.text = chestType.ToString();
            }

            if (chestImage != null && config != null && config.closedSprite != null)
            {
                chestImage.sprite = config.closedSprite;
            }

            switch (currentState)
            {
                case ChestSlotState.Ready:
                    SetupReadyVisuals(animateTransition);
                    break;

                case ChestSlotState.Waiting:
                    SetupWaitingVisuals();
                    break;

                case ChestSlotState.Unlocking:
                    SetupUnlockingVisuals();
                    break;

                case ChestSlotState.Locked:
                case ChestSlotState.Empty:
                default:
                    SetupLockedVisuals();
                    break;
            }
        }

        private void SetupReadyVisuals(bool animate)
        {
            if (buttonImage != null && readyButtonSprite != null)
                buttonImage.sprite = readyButtonSprite;

            if (buttonText != null)
                buttonText.text = "Ready";

            if (timerText != null)
                timerText.text = $"{UnlockHours}h";

            if (backglowObject != null)
                backglowObject.SetActive(true);

            // Subtle tactile punch on transition
            if (animate && rectTransform != null)
            {
                rectTransform.DOPunchScale(authoredScale * 0.08f, 0.25f, 4, 0.4f);
            }

            // Pulsing halo glow behind chest
            if (backglowImage != null)
            {
                backglowImage.DOKill();
                Color c = backglowImage.color;
                c.a = 0.45f;
                backglowImage.color = c;
                glowPulseTween = backglowImage.DOFade(0.85f, 0.95f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo);
            }

            // Button subtle breathing loop
            if (actionButton != null && actionButton.transform != transform)
            {
                actionButton.transform.DOKill();
                buttonPulseTween = actionButton.transform
                    .DOScale(buttonAuthoredScale * 1.04f, 0.95f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo);
            }
        }

        private void SetupWaitingVisuals()
        {
            if (buttonImage != null && waitingButtonSprite != null)
                buttonImage.sprite = waitingButtonSprite;

            if (buttonText != null)
                buttonText.text = "Waiting";

            if (timerText != null)
                timerText.text = $"{UnlockHours}h";

            if (backglowObject != null)
                backglowObject.SetActive(false);
        }

        private void SetupUnlockingVisuals()
        {
            if (buttonImage != null && waitingButtonSprite != null)
                buttonImage.sprite = waitingButtonSprite;

            if (backglowObject != null)
            {
                backglowObject.SetActive(true);
                if (backglowImage != null)
                {
                    backglowImage.DOKill();
                    Color c = new Color(0.25f, 0.75f, 1f, 0.25f);
                    backglowImage.color = c;
                    glowPulseTween = backglowImage.DOFade(0.55f, 1.2f)
                        .SetEase(Ease.InOutSine)
                        .SetLoops(-1, LoopType.Yoyo);
                }
            }

            UpdateCountdown();
        }

        private void SetupLockedVisuals()
        {
            if (buttonImage != null && unlockButtonSprite != null)
                buttonImage.sprite = unlockButtonSprite;

            if (buttonText != null)
                buttonText.text = "Unlock";

            if (timerText != null)
                timerText.text = $"{UnlockHours}h";

            if (backglowObject != null)
                backglowObject.SetActive(false);
        }

        private void UpdateCountdown()
        {
            TimeSpan remaining = unlockEndTime - DateTime.UtcNow;
            if (remaining.TotalSeconds <= 0)
            {
                currentState = ChestSlotState.Ready;
                SaveState();
                ApplyStateVisuals(true);
                return;
            }

            string formattedTime;
            if (remaining.TotalHours >= 1)
            {
                formattedTime = $"{(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
            }
            else
            {
                formattedTime = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}";
            }

            if (timerText != null)
            {
                timerText.text = formattedTime;
            }

            // Show the live countdown on the button as well as the time label
            if (buttonText != null)
            {
                buttonText.text = formattedTime;
            }
        }

        private void KillAllTweens()
        {
            if (buttonPulseTween != null && buttonPulseTween.IsActive()) buttonPulseTween.Kill();
            if (glowPulseTween != null && glowPulseTween.IsActive()) glowPulseTween.Kill();
            if (rectTransform != null) rectTransform.DOKill();
            if (actionButton != null && actionButton.transform != transform) actionButton.transform.DOKill();
        }

        #endregion

        #region User Interaction

        public void OnSlotClicked()
        {
            // Tactile crisp punch on click
            if (actionButton != null && actionButton.transform != transform)
            {
                actionButton.transform.DOKill();
                actionButton.transform.localScale = buttonAuthoredScale;
                actionButton.transform.DOScale(buttonAuthoredScale * 0.92f, 0.08f)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() =>
                    {
                        actionButton.transform.DOScale(buttonAuthoredScale, 0.15f).SetEase(Ease.OutBack);
                    });
            }

            switch (currentState)
            {
                case ChestSlotState.Ready:
                    ChestSystemController.Instance.StartChestOpening(this);
                    break;

                case ChestSlotState.Waiting:
                    // Tapping a queued chest cancels its place in the queue
                    ChestSystemController.Instance.CancelQueue(this);
                    break;

                case ChestSlotState.Locked:
                case ChestSlotState.Empty:
                    ChestSystemController.Instance.RequestUnlock(this);
                    break;

                case ChestSlotState.Unlocking:
                    // Instant Speed-Up / Instant Unlock for testing
                    CompleteUnlockImmediately();
                    break;
            }
        }

        public void StartUnlockingTimer()
        {
            double durationSeconds = UnlockHours * 3600;
            unlockEndTime = DateTime.UtcNow.AddSeconds(durationSeconds);
            SetState(ChestSlotState.Unlocking, unlockEndTime);

            if (ChestSystemController.Instance != null)
            {
                ChestSystemController.Instance.PlayCardFlipSound();
            }

            if (backglowImage != null)
            {
                backglowImage.DOKill();
                Color flare = new Color(0.3f, 0.85f, 1f, 0.9f);
                backglowImage.color = flare;
                backglowImage.DOFade(0.35f, 0.5f).SetEase(Ease.OutQuad);
            }

            if (rectTransform != null)
            {
                rectTransform.DOKill();
                rectTransform.anchoredPosition = authoredPos;
                Sequence hopSeq = DOTween.Sequence();
                hopSeq.Append(rectTransform.DOScale(new Vector3(authoredScale.x * 1.14f, authoredScale.y * 0.86f, authoredScale.z), 0.08f).SetEase(Ease.OutQuad));
                hopSeq.Append(rectTransform.DOAnchorPosY(authoredPos.y + 8f, 0.15f).SetEase(Ease.OutQuad));
                hopSeq.Join(rectTransform.DOScale(new Vector3(authoredScale.x * 0.92f, authoredScale.y * 1.12f, authoredScale.z), 0.15f).SetEase(Ease.OutQuad));
                hopSeq.Append(rectTransform.DOAnchorPosY(authoredPos.y, 0.20f).SetEase(Ease.OutBounce));
                hopSeq.Join(rectTransform.DOScale(authoredScale, 0.20f).SetEase(Ease.OutBounce));
            }
        }

        public void CompleteUnlockImmediately()
        {
            unlockEndTime = DateTime.UtcNow;
            SetState(ChestSlotState.Ready);

            if (ChestSystemController.Instance != null)
            {
                ChestSystemController.Instance.PlayCollectSound();
            }

            if (backglowImage != null)
            {
                backglowImage.DOKill();
                Color goldFlare = new Color(1f, 0.9f, 0.3f, 1f);
                backglowImage.color = goldFlare;
                backglowImage.DOFade(0.7f, 0.4f);
            }

            if (rectTransform != null)
            {
                rectTransform.DOKill();
                rectTransform.anchoredPosition = authoredPos;
                Sequence readySeq = DOTween.Sequence();
                readySeq.Append(rectTransform.DOScale(authoredScale * 1.20f, 0.18f).SetEase(Ease.OutBack, 1.4f));
                readySeq.Append(rectTransform.DOScale(authoredScale, 0.18f).SetEase(Ease.OutBounce));
            }
        }

        /// <summary>Remaining unlock time in the same format the slot shows (HH:MM:SS or MM:SS).</summary>
        public string GetCountdownText()
        {
            TimeSpan remaining = unlockEndTime - DateTime.UtcNow;
            if (remaining.TotalSeconds <= 0) return "00:00";
            if (remaining.TotalHours >= 1)
                return $"{(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
            return $"{remaining.Minutes:D2}:{remaining.Seconds:D2}";
        }

        public void ConsumeChest()
        {
            SetState(ChestSlotState.Locked);
        }

        #endregion
    }
}
