using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace Siege.Chests
{
    /// <summary>
    /// AAA-Quality, ultra-juicy Chest System Manager powered by DOTween.
    /// Orchestrates:
    /// - Modal popups with springy elastic squash & stretch.
    /// - 3 Chest slots with live timers, state persistence, and floating idle breathing loops.
    /// - 12-frame animated chest opening sequence at 12 fps with anticipation squash and panel punch.
    /// - Cinematic 3D card flip reveals for Army & Power cards and rapid rolling coin countup.
    /// - Seamless economy integration with top bar currency and player inventory persistence.
    /// </summary>
    [DisallowMultipleComponent]
    public class ChestSystemController : MonoBehaviour
    {
        private static ChestSystemController instance;
        public static ChestSystemController Instance => instance;

        [Header("Root Modal Elements")]
        [SerializeField] private GameObject modalRoot;
        [SerializeField] private Image backgroundDimmer;
        [SerializeField] private CanvasGroup rootCanvasGroup;

        [Header("Panels")]
        [SerializeField] private RectTransform selectionPanel; // backpanel
        [SerializeField] private RectTransform openingPanel;   // chestPanel
        [SerializeField] private RectTransform rewardPanel;    // rewardPanel

        [Header("Chest Slots (Selection Screen)")]
        [SerializeField] private ChestSlotUI[] chestSlots;
        [SerializeField] private Button selectionCloseButton;

        [Header("Opening Screen UI Elements")]
        [SerializeField] private TextMeshProUGUI openingTopText;
        [SerializeField] private Image openingChestImage;
        [SerializeField] private Image openingGlow;
        [SerializeField] private TextMeshProUGUI tapToRevealText;
        [SerializeField] private Button openingCloseButton;
        [SerializeField] private Button tapToRevealClickZone;

        [Header("Reward Screen UI Elements")]
        [SerializeField] private TextMeshProUGUI rewardTopText;
        [SerializeField] private Image rewardChestImage;
        [SerializeField] private Image rewardGlow;
        [SerializeField] private RectTransform goldCoinsContainer;
        [SerializeField] private TextMeshProUGUI goldCoinsText;
        [SerializeField] private Image goldCoinsImage;
        [SerializeField] private RectTransform armyRewardCard;
        [SerializeField] private Image armyCardImage;
        [SerializeField] private TextMeshProUGUI armyCardCount;
        [SerializeField] private TextMeshProUGUI armyCardName;
        [SerializeField] private RectTransform powerRewardCard;
        [SerializeField] private Image powerCardImage;
        [SerializeField] private TextMeshProUGUI powerCardCount;
        [SerializeField] private TextMeshProUGUI powerCardName;
        [SerializeField] private Button collectButton;
        [SerializeField] private Button rewardCloseButton;

        [Header("Main Menu HUD Integration")]
        [SerializeField] private TMP_Text hudCoinCounterText;
        [SerializeField] private Button sidePanelChestShortcut;
        private TMP_Text sidePanelTimerText;
        private float sidePanelNextRefresh;

        [Header("Art Assets & Configurations")]
        [SerializeField] private ChestConfig silverConfig;
        [SerializeField] private ChestConfig goldConfig;
        [SerializeField] private ChestConfig masterConfig;
        [SerializeField] private Sprite cardBackSprite;
        [SerializeField] private Sprite sparkleSprite;
        [SerializeField] private Sprite coinSprite;
        [SerializeField] private Sprite coinStackSprite;
        [SerializeField] private Sprite innerGlowSprite;
        [SerializeField] private Sprite[] armyUnitSprites;
        [SerializeField] private Sprite[] powerCardSprites;

        [Header("Button Style Sprites")]
        [SerializeField] private Sprite readyButtonSprite;   // Green_Empty
        [SerializeField] private Sprite unlockButtonSprite;  // Cyan_Empty
        [SerializeField] private Sprite waitingButtonSprite; // Grey_Empty

        [Header("Audio (Optional)")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip openChestSound;
        [SerializeField] private AudioClip cardFlipSound;
        [SerializeField] private AudioClip coinTickSound;
        [SerializeField] private AudioClip collectSound;

        // Cached Resting Transforms (to guarantee zero layout drift)
        private Vector3 selectionPanelRestingScale = Vector3.one;
        private Vector3 openingPanelRestingScale = Vector3.one;
        private Vector3 rewardPanelRestingScale = Vector3.one;
        private Vector3 openingChestRestingScale = Vector3.one;
        private Vector2 openingChestRestingPos = Vector2.zero;
        private Vector3 tapToRevealRestingScale = Vector3.one;
        private Vector3 rewardChestRestingScale = Vector3.one;
        private Vector2 rewardChestRestingPos = Vector2.zero;
        private Vector3 collectButtonRestingScale = Vector3.one;
        private Vector3 goldCoinsRestingScale = Vector3.one;
        private Vector2 goldCoinsRestingPos = Vector2.zero;
        private Vector3 armyCardRestingScale = Vector3.one;
        private Vector2 armyCardRestingPos = Vector2.zero;
        private Vector3 powerCardRestingScale = Vector3.one;
        private Vector2 powerCardRestingPos = Vector2.zero;

        // Runtime State
        private ChestSlotUI currentActiveSlot;
        private ChestConfig currentOpeningConfig;
        private ChestReward currentReward;
        private bool isOpeningSequencePlaying = false;
        private bool isModalOpen = false;
        private GameObject openingInteriorLoot;
        private GameObject rewardInteriorLoot;

        // Tweens
        private Tween glowRotateTween;
        private Tween chestFloatTween;
        private Tween tapTextPulseTween;
        private Tween collectPulseTween;
        private Coroutine activeOpeningCoroutine;

        private void Awake()
        {
            if (instance == null) instance = this;

            if (modalRoot == null) modalRoot = gameObject;
            if (backgroundDimmer == null) backgroundDimmer = GetComponent<Image>();
            if (rootCanvasGroup == null)
            {
                rootCanvasGroup = GetComponent<CanvasGroup>();
                if (rootCanvasGroup == null) rootCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // Remove any old Animators on test chest objects to avoid fighting DOTween and sprite anims
            var animators = GetComponentsInChildren<Animator>(true);
            foreach (var anim in animators)
            {
                if (anim != null && anim.gameObject.name.IndexOf("chest1", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Destroy(anim);
                }
            }

            CacheRestingTransforms();
            EnsureInteriorTreasureObjects();
            HookListeners();
            InitializeDefaultConfigsIfNeeded();
        }

        private void Start()
        {
            InitializeSlots();

            // Fix up saved states (older builds could leave several chests unlocking), then restore the queue
            EnforceSingleUnlockSlot();
            PromoteQueuedChest();

            // Link HUD Coin text if not assigned
            if (hudCoinCounterText == null)
            {
                var cc = GameObject.Find("Canvas/Panel/toppanel/coinCounter");
                if (cc != null) hudCoinCounterText = cc.GetComponentInChildren<TMP_Text>();
            }

            // Sync initial currency
            UpdateHudCoinDisplay(GetSavedCoins(), false);

            // Side panel chest button shows the countdown on its "timer" label
            GameObject sideChestBtn = GameObject.Find("Canvas/Panel/sidepanel/button_chest");
            if (sideChestBtn != null)
            {
                Transform timerTrans = sideChestBtn.transform.Find("timer");
                if (timerTrans != null) sidePanelTimerText = timerTrans.GetComponent<TMP_Text>();
            }
        }

        private void LateUpdate()
        {
            if (sidePanelTimerText == null) return;
            if (Time.unscaledTime < sidePanelNextRefresh) return;
            sidePanelNextRefresh = Time.unscaledTime + 0.25f;

            // Mirror the unlocking chest's countdown on the main menu side panel
            ChestSlotUI active = GetOccupyingSlot();
            string text = string.Empty;
            if (active != null)
            {
                text = active.CurrentState == ChestSlotState.Ready ? "Ready" : active.GetCountdownText();
            }

            if (sidePanelTimerText.text != text) sidePanelTimerText.text = text;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            // Debug quick-test keys: 1, 2, 3 to instant-ready Silver, Gold, Master chests
            if (Input.GetKeyDown(KeyCode.Alpha1) && chestSlots != null && chestSlots.Length > 0)
                chestSlots[0].CompleteUnlockImmediately();
            if (Input.GetKeyDown(KeyCode.Alpha2) && chestSlots != null && chestSlots.Length > 1)
                chestSlots[1].CompleteUnlockImmediately();
            if (Input.GetKeyDown(KeyCode.Alpha3) && chestSlots != null && chestSlots.Length > 2)
                chestSlots[2].CompleteUnlockImmediately();
        }
#endif

        private void OnDestroy()
        {
            KillAllActiveTweens();
        }

        private void CacheRestingTransforms()
        {
            if (selectionPanel != null)
            {
                selectionPanelRestingScale = selectionPanel.localScale;
                if (selectionPanelRestingScale.x < 1f) selectionPanelRestingScale = new Vector3(6.1296763f, 6.1296763f, 6.1296763f);
            }
            if (openingPanel != null)
            {
                openingPanelRestingScale = openingPanel.localScale;
                if (openingPanelRestingScale.x < 1f) openingPanelRestingScale = new Vector3(6.1296763f, 6.1296763f, 6.1296763f);
            }
            if (rewardPanel != null)
            {
                rewardPanelRestingScale = rewardPanel.localScale;
                if (rewardPanelRestingScale.x < 1f) rewardPanelRestingScale = new Vector3(6.1296763f, 6.1296763f, 6.1296763f);
            }

            if (openingChestImage != null)
            {
                openingChestRestingScale = openingChestImage.rectTransform.localScale;
                openingChestRestingPos = openingChestImage.rectTransform.anchoredPosition;
            }

            if (tapToRevealText != null) tapToRevealRestingScale = tapToRevealText.transform.localScale;
            if (rewardChestImage != null)
            {
                rewardChestRestingScale = rewardChestImage.rectTransform.localScale;
                rewardChestRestingPos = rewardChestImage.rectTransform.anchoredPosition;
            }
            if (collectButton != null) collectButtonRestingScale = collectButton.transform.localScale;
            if (goldCoinsContainer != null)
            {
                goldCoinsRestingScale = goldCoinsContainer.localScale;
                goldCoinsRestingPos = goldCoinsContainer.anchoredPosition;
            }
            if (armyRewardCard != null)
            {
                armyCardRestingScale = armyRewardCard.localScale;
                armyCardRestingPos = armyRewardCard.anchoredPosition;
            }
            if (powerRewardCard != null)
            {
                powerCardRestingScale = powerRewardCard.localScale;
                powerCardRestingPos = powerRewardCard.anchoredPosition;
            }
        }

        private void EnsureInteriorTreasureObjects()
        {
            if (openingChestImage != null && openingInteriorLoot == null)
            {
                Transform existing = openingChestImage.transform.Find("InteriorLoot");
                if (existing != null)
                {
                    openingInteriorLoot = existing.gameObject;
                }
                else
                {
                    openingInteriorLoot = CreateInteriorLootVisual(openingChestImage.rectTransform);
                }
                if (openingInteriorLoot != null) openingInteriorLoot.transform.localScale = Vector3.zero;
            }

            if (rewardChestImage != null && rewardInteriorLoot == null)
            {
                Transform existing = rewardChestImage.transform.Find("InteriorLoot");
                if (existing != null)
                {
                    rewardInteriorLoot = existing.gameObject;
                }
                else
                {
                    rewardInteriorLoot = CreateInteriorLootVisual(rewardChestImage.rectTransform);
                }
                if (rewardInteriorLoot != null) rewardInteriorLoot.transform.localScale = Vector3.zero;
            }
        }

        private GameObject CreateInteriorLootVisual(RectTransform parentChest)
        {
            GameObject lootRoot = new GameObject("InteriorLoot", typeof(RectTransform));
            lootRoot.transform.SetParent(parentChest, false);

            RectTransform rootRt = lootRoot.GetComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0.5f, 0.5f);
            rootRt.anchorMax = new Vector2(0.5f, 0.5f);
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.anchoredPosition = new Vector2(0f, -4f);
            rootRt.sizeDelta = new Vector2(65f, 55f);
            rootRt.localScale = Vector3.zero;

            // 1. Inner Golden Light Beam
            if (innerGlowSprite != null)
            {
                GameObject glowObj = new GameObject("InnerGlow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                glowObj.transform.SetParent(lootRoot.transform, false);
                RectTransform glowRt = glowObj.GetComponent<RectTransform>();
                glowRt.anchoredPosition = new Vector2(0f, 10f);
                glowRt.sizeDelta = new Vector2(75f, 65f);
                Image glowImg = glowObj.GetComponent<Image>();
                glowImg.sprite = innerGlowSprite;
                glowImg.color = new Color(1f, 0.95f, 0.4f, 0.75f);
                glowImg.raycastTarget = false;
            }

            // 2. Overflowing Golden Coins Stack
            if (coinStackSprite != null)
            {
                GameObject coinsObj = new GameObject("CoinsStack", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                coinsObj.transform.SetParent(lootRoot.transform, false);
                RectTransform coinsRt = coinsObj.GetComponent<RectTransform>();
                coinsRt.anchoredPosition = new Vector2(0f, -2f);
                coinsRt.sizeDelta = new Vector2(58f, 50f);
                Image coinsImg = coinsObj.GetComponent<Image>();
                coinsImg.sprite = coinStackSprite;
                coinsImg.raycastTarget = false;
            }

            return lootRoot;
        }

        private void HookListeners()
        {
            if (selectionCloseButton != null) selectionCloseButton.onClick.AddListener(CloseChestsMenu);
            if (openingCloseButton != null) openingCloseButton.onClick.AddListener(CloseToSelection);
            if (rewardCloseButton != null) rewardCloseButton.onClick.AddListener(OnCollectClicked);

            if (tapToRevealClickZone != null) tapToRevealClickZone.onClick.AddListener(OnTapToRevealTriggered);
            if (tapToRevealText != null)
            {
                Button textBtn = tapToRevealText.GetComponent<Button>();
                if (textBtn != null) textBtn.onClick.AddListener(OnTapToRevealTriggered);
            }

            if (collectButton != null) collectButton.onClick.AddListener(OnCollectClicked);
            if (sidePanelChestShortcut != null) sidePanelChestShortcut.onClick.AddListener(OpenChestsMenu);
        }

        private void InitializeDefaultConfigsIfNeeded()
        {
            if (silverConfig == null || silverConfig.unlockHours <= 0)
                silverConfig = ChestConfig.CreateDefault(ChestType.Silver);
            silverConfig.unlockHours = 2f;
            silverConfig.minCoins = 20;
            silverConfig.maxCoins = 200;
            silverConfig.armyCopies = 2;
            silverConfig.powerCopies = 2;

            if (goldConfig == null || goldConfig.unlockHours <= 0)
                goldConfig = ChestConfig.CreateDefault(ChestType.Gold);
            goldConfig.unlockHours = 8f;
            goldConfig.minCoins = 100;
            goldConfig.maxCoins = 500;
            goldConfig.armyCopies = 4;
            goldConfig.powerCopies = 4;

            if (masterConfig == null || masterConfig.unlockHours <= 0)
                masterConfig = ChestConfig.CreateDefault(ChestType.Master);
            masterConfig.unlockHours = 24f;
            masterConfig.minCoins = 500;
            masterConfig.maxCoins = 1000;
            masterConfig.armyCopies = 10;
            masterConfig.powerCopies = 10;
        }

        private void InitializeSlots()
        {
            if (chestSlots == null || chestSlots.Length == 0)
            {
                chestSlots = selectionPanel.GetComponentsInChildren<ChestSlotUI>(true);
            }

            for (int i = 0; i < chestSlots.Length; i++)
            {
                ChestType type = (ChestType)Mathf.Clamp(i, 0, 2);
                ChestConfig cfg = GetConfig(type);
                chestSlots[i].Initialize(i, type, cfg, readyButtonSprite, unlockButtonSprite, waitingButtonSprite);
            }
        }

        public ChestConfig GetConfig(ChestType type)
        {
            switch (type)
            {
                case ChestType.Silver: return silverConfig;
                case ChestType.Gold: return goldConfig;
                case ChestType.Master: return masterConfig;
                default: return silverConfig;
            }
        }

        #region Screen Flow & Transitions

        /// <summary>
        /// Opens the Chest Selection modal dialog with punchy AAA entrance juice.
        /// </summary>
        public void OpenChestsMenu()
        {
            isModalOpen = true;
            modalRoot.SetActive(true);

            // Hide other panels
            if (openingPanel != null) openingPanel.gameObject.SetActive(false);
            if (rewardPanel != null) rewardPanel.gameObject.SetActive(false);

            if (selectionPanel != null)
            {
                selectionPanel.gameObject.SetActive(true);
                selectionPanel.DOKill();
                selectionPanel.localScale = selectionPanelRestingScale * 0.70f;

                if (rootCanvasGroup != null)
                {
                    rootCanvasGroup.DOKill();
                    rootCanvasGroup.alpha = 0f;
                    rootCanvasGroup.DOFade(1f, 0.22f);
                }

                // Smooth popup bounce
                selectionPanel.DOScale(selectionPanelRestingScale, 0.38f)
                    .SetEase(Ease.OutBack, 1.15f);
            }
        }

        /// <summary>
        /// Smoothly closes the chest modal back to the main menu lobby.
        /// </summary>
        public void CloseChestsMenu()
        {
            isModalOpen = false;
            if (selectionPanel != null)
            {
                selectionPanel.DOKill();
                selectionPanel.DOScale(selectionPanelRestingScale * 0.7f, 0.2f).SetEase(Ease.InBack);
            }

            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.DOKill();
                rootCanvasGroup.DOFade(0f, 0.2f).OnComplete(() =>
                {
                    modalRoot.SetActive(false);
                    if (selectionPanel != null) selectionPanel.localScale = selectionPanelRestingScale;
                    if (rootCanvasGroup != null) rootCanvasGroup.alpha = 1f;
                });
            }
            else
            {
                modalRoot.SetActive(false);
            }
        }

        public void CloseToSelection()
        {
            KillAllActiveTweens();
            if (activeOpeningCoroutine != null) StopCoroutine(activeOpeningCoroutine);

            if (openingPanel != null) openingPanel.gameObject.SetActive(false);
            if (rewardPanel != null) rewardPanel.gameObject.SetActive(false);

            OpenChestsMenu();
        }

        #endregion

        #region Chest Opening Sequence (Tap to Reveal)

        #region Unlock Queue

        /// <summary>
        /// Only one chest may unlock at a time. A chest occupies the unlock slot from the moment its
        /// timer starts until it is claimed. Returns the slot currently holding it, or null.
        /// </summary>
        private ChestSlotUI GetOccupyingSlot(ChestSlotUI except = null)
        {
            if (chestSlots == null) return null;
            foreach (var s in chestSlots)
            {
                if (s != null && s != except && s.IsOccupyingUnlockSlot) return s;
            }
            return null;
        }

        /// <summary>
        /// Guarantees at most one occupying chest. Keeps a Ready chest first (it is claimable),
        /// otherwise the chest that finishes soonest. Any others drop back into the queue.
        /// </summary>
        public void EnforceSingleUnlockSlot()
        {
            if (chestSlots == null) return;

            ChestSlotUI keeper = null;
            foreach (var s in chestSlots)
            {
                if (s == null || !s.IsOccupyingUnlockSlot) continue;
                if (keeper == null
                    || (s.CurrentState == ChestSlotState.Ready && keeper.CurrentState != ChestSlotState.Ready)
                    || (s.CurrentState == keeper.CurrentState && s.UnlockEndTime < keeper.UnlockEndTime))
                {
                    keeper = s;
                }
            }

            if (keeper == null) return;

            foreach (var s in chestSlots)
            {
                if (s != null && s != keeper && s.IsOccupyingUnlockSlot)
                {
                    s.QueueForUnlock();
                }
            }
        }

        /// <summary>Called when the player taps Unlock on a locked chest.</summary>
        public void RequestUnlock(ChestSlotUI slot)
        {
            if (slot == null) return;

            if (GetOccupyingSlot(slot) != null)
            {
                slot.QueueForUnlock();   // another chest holds the unlock slot; queue this one
            }
            else
            {
                slot.StartUnlockingTimer();
            }
        }

        /// <summary>Called when the player taps a queued chest to remove it from the queue.</summary>
        public void CancelQueue(ChestSlotUI slot)
        {
            if (slot == null) return;
            slot.ConsumeChest(); // sets it back to Locked and clears its queue position
        }

        /// <summary>
        /// If the unlock slot is free, starts the earliest-queued chest's timer.
        /// </summary>
        public void PromoteQueuedChest()
        {
            if (chestSlots == null || GetOccupyingSlot() != null) return;

            ChestSlotUI next = null;
            foreach (var s in chestSlots)
            {
                if (s == null || s.CurrentState != ChestSlotState.Waiting) continue;
                if (next == null || s.QueuedAtBinary < next.QueuedAtBinary) next = s;
            }

            if (next != null)
            {
                next.StartUnlockingTimer();
            }
        }

        #endregion

        /// <summary>
        /// Transitions from Selection screen to the Tap-to-Reveal screen.
        /// </summary>
        public void StartChestOpening(ChestSlotUI slot)
        {
            currentActiveSlot = slot;
            currentOpeningConfig = slot.Config ?? GetConfig(slot.Type);
            isOpeningSequencePlaying = false;

            // Generate rewards upfront based on exact specification
            currentReward = GenerateChestRewards(currentOpeningConfig);

            // Pop out selection panel
            if (selectionPanel != null)
            {
                selectionPanel.DOScale(selectionPanelRestingScale * 0.7f, 0.18f)
                    .SetEase(Ease.InBack)
                    .OnComplete(() =>
                    {
                        selectionPanel.gameObject.SetActive(false);
                        SetupOpeningScreen();
                    });
            }
            else
            {
                SetupOpeningScreen();
            }
        }

        private void SetupOpeningScreen()
        {
            if (openingPanel == null) return;

            openingPanel.gameObject.SetActive(true);
            openingPanel.DOKill();
            openingPanel.localScale = openingPanelRestingScale * 0.70f;
            openingPanel.DOScale(openingPanelRestingScale, 0.35f).SetEase(Ease.OutBack, 1.15f);

            if (openingTopText != null)
            {
                openingTopText.text = currentOpeningConfig.displayName;
            }

            EnsureInteriorTreasureObjects();
            if (openingInteriorLoot != null)
            {
                openingInteriorLoot.transform.DOKill();
                openingInteriorLoot.transform.localScale = Vector3.zero;
            }

            // Reset chest image to Frame 01 (closed)
            if (openingChestImage != null)
            {
                openingChestImage.rectTransform.DOKill();
                openingChestImage.rectTransform.anchoredPosition = openingChestRestingPos;
                openingChestImage.rectTransform.localScale = openingChestRestingScale;

                if (currentOpeningConfig.animationFrames != null && currentOpeningConfig.animationFrames.Length > 0)
                {
                    openingChestImage.sprite = currentOpeningConfig.animationFrames[0];
                }
                else if (currentOpeningConfig.closedSprite != null)
                {
                    openingChestImage.sprite = currentOpeningConfig.closedSprite;
                }

                // Subtle living idle floating bob (only 3.5 pixels, gentle)
                chestFloatTween = openingChestImage.rectTransform
                    .DOAnchorPosY(openingChestRestingPos.y + 3.5f, 1.5f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo);
            }

            // Continuous rotating radial energy aura
            if (openingGlow != null)
            {
                openingGlow.gameObject.SetActive(true);
                openingGlow.transform.DOKill();
                glowRotateTween = openingGlow.transform
                    .DORotate(new Vector3(0, 0, 360), 9f, RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear)
                    .SetLoops(-1);
            }

            // Calm, elegant 'Tap to reveal' button/text (strictly NO exaggerated scale bouncing)
            if (tapToRevealText != null)
            {
                tapToRevealText.gameObject.SetActive(true);
                tapToRevealText.transform.DOKill();
                tapToRevealText.transform.localScale = tapToRevealRestingScale;

                tapToRevealText.DOKill();
                Color c = tapToRevealText.color;
                c.a = 1f;
                tapToRevealText.color = c;

                // Gentle breathing alpha pulse: clean, clear, non-distracting
                tapTextPulseTween = tapToRevealText
                    .DOFade(0.40f, 1.0f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo);
            }
        }

        public void OnTapToRevealTriggered()
        {
            if (isOpeningSequencePlaying) return;
            isOpeningSequencePlaying = true;

            // Immediately dismiss tap to reveal text smoothly
            if (tapTextPulseTween != null && tapTextPulseTween.IsActive()) tapTextPulseTween.Kill();
            if (tapToRevealText != null)
            {
                tapToRevealText.DOFade(0f, 0.12f).OnComplete(() =>
                {
                    tapToRevealText.gameObject.SetActive(false);
                    tapToRevealText.transform.localScale = tapToRevealRestingScale;
                });
            }

            PlaySfx(openChestSound);
            if (activeOpeningCoroutine != null) StopCoroutine(activeOpeningCoroutine);
            activeOpeningCoroutine = StartCoroutine(OpeningAnimationRoutine());
        }

        private IEnumerator OpeningAnimationRoutine()
        {
            // Kill idle animations
            if (chestFloatTween != null && chestFloatTween.IsActive()) chestFloatTween.Kill();
            if (tapTextPulseTween != null && tapTextPulseTween.IsActive()) tapTextPulseTween.Kill();
            if (tapToRevealText != null) tapToRevealText.gameObject.SetActive(false);

            // Phase 1: Heavy Anticipation Squash & Tension Rumble
            openingChestImage.rectTransform.DOKill();
            openingChestImage.rectTransform.anchoredPosition = openingChestRestingPos;

            // Squash down into pedestal
            openingChestImage.rectTransform
                .DOScale(new Vector3(openingChestRestingScale.x * 1.15f, openingChestRestingScale.y * 0.85f, openingChestRestingScale.z), 0.12f)
                .SetEase(Ease.OutQuad);

            // Rapid tension rumble vibration
            openingChestImage.rectTransform.DOPunchRotation(new Vector3(0, 0, 6f), 0.36f, 24, 0.45f);
            openingChestImage.rectTransform.DOPunchPosition(new Vector3(4f, 0, 0), 0.36f, 26, 0.55f);

            // Aura glow flares and accelerates rotation
            if (openingGlow != null)
            {
                openingGlow.transform.DOScale(Vector3.one * 1.35f, 0.36f).SetEase(Ease.OutQuad);
                if (glowRotateTween != null && glowRotateTween.IsActive()) glowRotateTween.timeScale = 2.5f;
            }

            yield return new WaitForSecondsRealtime(0.36f);

            // Phase 2: The Spring Leap & Lock Burst (Frames 1 - 5)
            // Chest springs upward with vertical stretch
            openingChestImage.rectTransform.DOAnchorPosY(openingChestRestingPos.y + 14f, 0.22f).SetEase(Ease.OutQuad);
            openingChestImage.rectTransform.DOScale(new Vector3(openingChestRestingScale.x * 0.92f, openingChestRestingScale.y * 1.14f, openingChestRestingScale.z), 0.22f).SetEase(Ease.OutQuad);

            var frames = currentOpeningConfig.animationFrames;
            if (frames != null && frames.Length >= 12)
            {
                // Play Frames 1-5 (Seam cracks open)
                for (int i = 0; i < 5; i++)
                {
                    if (frames[i] != null) openingChestImage.sprite = frames[i];
                    yield return new WaitForSecondsRealtime(0.048f);
                }

                // Phase 3: The Lid Throw & Radiance Surge (Frames 6 - 9)
                // Minor sparkle leak from the opening seam
                SpawnSparkleBurst(openingPanel, openingChestRestingPos + new Vector2(0f, 15f), 5, new Color(1f, 0.95f, 0.5f, 1f));

                if (openingGlow != null)
                {
                    openingGlow.transform.DOScale(Vector3.one * 1.55f, 0.20f).SetEase(Ease.OutQuad);
                }

                for (int i = 5; i < 9; i++)
                {
                    if (frames[i] != null) openingChestImage.sprite = frames[i];
                    // On frame 7 (as chest opens), pop up the interior overflowing treasure layer!
                    if (i == 6 && openingInteriorLoot != null)
                    {
                        openingInteriorLoot.SetActive(true);
                        openingInteriorLoot.transform.DOKill();
                        openingInteriorLoot.transform.localScale = Vector3.zero;
                        openingInteriorLoot.transform.DOScale(Vector3.one, 0.22f).SetEase(Ease.OutBack, 1.4f);
                    }
                    yield return new WaitForSecondsRealtime(0.048f);
                }

                // Phase 4: Climax Slam & Radiant Explosion (Frames 10 - 12)
                // Chest slams back down to resting position with heavy impact
                openingChestImage.rectTransform.DOAnchorPosY(openingChestRestingPos.y, 0.16f).SetEase(Ease.OutBounce);

                // Landing impact squash & stretch overshoot
                Sequence landingSeq = DOTween.Sequence();
                landingSeq.Append(openingChestImage.rectTransform.DOScale(new Vector3(openingChestRestingScale.x * 1.18f, openingChestRestingScale.y * 0.86f, openingChestRestingScale.z), 0.10f).SetEase(Ease.OutQuad));
                landingSeq.Append(openingChestImage.rectTransform.DOScale(openingChestRestingScale, 0.18f).SetEase(Ease.OutBounce));

                // Screen / panel shockwave punch on impact
                if (openingPanel != null)
                {
                    openingPanel.DOPunchPosition(new Vector3(0, -9f, 0), 0.28f, 16, 0.5f);
                }

                // Punch the interior loot on slam!
                if (openingInteriorLoot != null)
                {
                    openingInteriorLoot.transform.DOPunchScale(Vector3.one * 0.25f, 0.35f, 5, 0.5f);
                }

                // Full radiant sparkle burst (12 sparkling golden stars explode from chest)
                SpawnSparkleBurst(openingPanel, openingChestRestingPos + new Vector2(0f, 10f), 12, new Color(1f, 0.92f, 0.4f, 1f));

                // Erupt a fountain of 7 golden coins shooting upwards out of the chest mouth!
                SpawnFlyingCoinFountain(openingPanel, openingChestRestingPos + new Vector2(0f, 8f), 7);
                PlayCoinTickSound();

                for (int i = 9; i < 12; i++)
                {
                    if (frames[i] != null) openingChestImage.sprite = frames[i];
                    yield return new WaitForSecondsRealtime(0.048f);
                }
            }
            else
            {
                // Fallback if no frames
                openingChestImage.rectTransform.DOAnchorPosY(openingChestRestingPos.y, 0.16f).SetEase(Ease.OutBounce);
                openingChestImage.rectTransform.DOScale(openingChestRestingScale, 0.20f).SetEase(Ease.OutBounce);
                SpawnSparkleBurst(openingPanel, openingChestRestingPos, 10);
            }

            // Restore glow rotation timescale
            if (glowRotateTween != null && glowRotateTween.IsActive()) glowRotateTween.timeScale = 1.0f;

            // Hold open chest in full triumphant glory for player satisfaction
            yield return new WaitForSecondsRealtime(0.38f);

            // Phase 5: Transition to Reward Screen
            TransitionToRewardScreen();
        }

        private void SpawnSparkleBurst(RectTransform parent, Vector2 spawnPos, int count, Color? tint = null)
        {
            if (sparkleSprite == null || parent == null) return;

            Color sparkleColor = tint ?? new Color(1f, 0.93f, 0.45f, 1f);

            for (int i = 0; i < count; i++)
            {
                GameObject sparkleObj = new GameObject($"SparkleBurst_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                sparkleObj.transform.SetParent(parent, false);

                RectTransform rt = sparkleObj.GetComponent<RectTransform>();
                rt.anchoredPosition = spawnPos;
                rt.localScale = Vector3.zero;
                rt.sizeDelta = new Vector2(26f, 26f);

                Image img = sparkleObj.GetComponent<Image>();
                img.sprite = sparkleSprite;
                img.color = sparkleColor;
                img.raycastTarget = false;

                // Radial explosion vector with random angle and distance
                float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float distance = UnityEngine.Random.Range(35f, 95f);
                Vector2 targetPos = spawnPos + new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance);
                float duration = UnityEngine.Random.Range(0.40f, 0.65f);
                float targetScale = UnityEngine.Random.Range(0.7f, 1.25f);
                float spinAngle = UnityEngine.Random.Range(-360f, 360f);

                Sequence seq = DOTween.Sequence();
                seq.Append(rt.DOAnchorPos(targetPos, duration).SetEase(Ease.OutQuad));
                seq.Join(rt.DOScale(Vector3.one * targetScale, duration * 0.35f).SetEase(Ease.OutQuad));
                seq.Join(rt.DORotate(new Vector3(0, 0, spinAngle), duration, RotateMode.FastBeyond360).SetEase(Ease.OutQuad));
                seq.Insert(duration * 0.35f, img.DOFade(0f, duration * 0.65f).SetEase(Ease.InQuad));
                seq.OnComplete(() => Destroy(sparkleObj));
            }
        }

        private void SpawnFlyingCoinFountain(RectTransform parent, Vector2 spawnPos, int count)
        {
            if (coinSprite == null || parent == null) return;

            for (int i = 0; i < count; i++)
            {
                GameObject coinObj = new GameObject($"FlyingCoin_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                coinObj.transform.SetParent(parent, false);

                RectTransform rt = coinObj.GetComponent<RectTransform>();
                rt.anchoredPosition = spawnPos;
                rt.sizeDelta = new Vector2(26f, 26f);
                rt.localScale = Vector3.zero;

                Image img = coinObj.GetComponent<Image>();
                img.sprite = coinSprite;
                img.raycastTarget = false;

                // Parabolic fountain arc: shoots up and spreads outward
                float spreadX = UnityEngine.Random.Range(-55f, 55f);
                float jumpHeight = UnityEngine.Random.Range(45f, 85f);
                Vector2 targetPos = new Vector2(spawnPos.x + spreadX, spawnPos.y + UnityEngine.Random.Range(-15f, 25f));
                float duration = UnityEngine.Random.Range(0.60f, 0.85f);

                Sequence seq = DOTween.Sequence();
                seq.Append(rt.DOScale(Vector3.one * UnityEngine.Random.Range(0.75f, 1.1f), 0.12f).SetEase(Ease.OutQuad));
                seq.Join(rt.DOJumpAnchorPos(targetPos, jumpHeight, 1, duration).SetEase(Ease.OutQuad));
                // 3D-like flip rotation while flying
                seq.Join(rt.DORotate(new Vector3(0f, 360f * UnityEngine.Random.Range(1, 3), UnityEngine.Random.Range(-45f, 45f)), duration, RotateMode.FastBeyond360));
                // Fade out near the end of arc
                seq.Insert(duration * 0.60f, img.DOFade(0f, duration * 0.40f));
                seq.OnComplete(() => Destroy(coinObj));
            }
        }

        private void SpawnFlyingCoinsStream(RectTransform parent, Vector2 spawnPos, Vector2 targetPos, int count)
        {
            if (coinSprite == null || parent == null) return;

            for (int i = 0; i < count; i++)
            {
                int coinIndex = i;
                GameObject coinObj = new GameObject($"CoinStream_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                coinObj.transform.SetParent(parent, false);

                RectTransform rt = coinObj.GetComponent<RectTransform>();
                rt.anchoredPosition = spawnPos;
                rt.sizeDelta = new Vector2(24f, 24f);
                rt.localScale = Vector3.zero;

                Image img = coinObj.GetComponent<Image>();
                img.sprite = coinSprite;
                img.raycastTarget = false;

                float delay = coinIndex * 0.04f;
                float duration = 0.36f;
                float jumpHeight = 16f + UnityEngine.Random.Range(-4f, 8f);

                Sequence seq = DOTween.Sequence();
                seq.SetDelay(delay);
                seq.Append(rt.DOScale(Vector3.one * 0.85f, 0.08f).SetEase(Ease.OutQuad));
                seq.Join(rt.DOJumpAnchorPos(targetPos, jumpHeight, 1, duration).SetEase(Ease.OutQuad));
                seq.Join(rt.DORotate(new Vector3(0, 360f * UnityEngine.Random.Range(1, 2), 0), duration, RotateMode.FastBeyond360));
                seq.Insert(delay + duration * 0.75f, rt.DOScale(Vector3.zero, duration * 0.25f).SetEase(Ease.InQuad));
                seq.OnComplete(() => Destroy(coinObj));
            }
        }

        #endregion

        #region Reward Reveal & Collection Screen

        private void TransitionToRewardScreen()
        {
            // Seamless handoff: no window shrinking or popping!
            // openingPanel and rewardPanel share identical anchoredPosition {2, -2} and scale 6.1296763
            if (openingPanel != null)
            {
                openingPanel.gameObject.SetActive(false);
            }
            DisplayRewards();
        }

        private void DisplayRewards()
        {
            if (rewardPanel == null) return;

            rewardPanel.gameObject.SetActive(true);
            rewardPanel.DOKill();
            rewardPanel.localScale = rewardPanelRestingScale;
            rewardPanel.anchoredPosition = new Vector2(2f, -2f);

            if (rewardTopText != null)
            {
                rewardTopText.text = "Your Rewards";
                rewardTopText.alpha = 0f;
                rewardTopText.DOFade(1f, 0.35f);
            }

            // Set open chest sprite (Frame 12 or openSprite)
            if (rewardChestImage != null)
            {
                var frames = currentOpeningConfig.animationFrames;
                if (frames != null && frames.Length >= 12)
                    rewardChestImage.sprite = frames[11];
                else if (currentOpeningConfig.openSprite != null)
                    rewardChestImage.sprite = currentOpeningConfig.openSprite;

                // Show overflowing treasure stack inside reward chest mouth
                EnsureInteriorTreasureObjects();
                if (rewardInteriorLoot != null)
                {
                    rewardInteriorLoot.SetActive(true);
                    rewardInteriorLoot.transform.DOKill();
                    rewardInteriorLoot.transform.localScale = Vector3.one;
                }

                // Start at exact opening chest position and scale, then smoothly glide up!
                rewardChestImage.rectTransform.DOKill();
                rewardChestImage.rectTransform.anchoredPosition = openingChestRestingPos;
                rewardChestImage.rectTransform.localScale = openingChestRestingScale;

                float glideTime = 0.38f;
                rewardChestImage.rectTransform.DOAnchorPos(rewardChestRestingPos, glideTime)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() =>
                    {
                        // Guarantee exact snap after tween
                        rewardChestImage.rectTransform.anchoredPosition = rewardChestRestingPos;
                        rewardChestImage.rectTransform.localScale = rewardChestRestingScale;
                    });
                rewardChestImage.rectTransform.DOScale(rewardChestRestingScale, glideTime).SetEase(Ease.OutQuad);
            }

            // Rotating golden halo aura behind chest
            if (rewardGlow != null)
            {
                rewardGlow.gameObject.SetActive(true);
                rewardGlow.transform.DOKill();
                rewardGlow.transform.DORotate(new Vector3(0, 0, 360), 8f, RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear)
                    .SetLoops(-1);
            }

            // --- STAGE ALL REWARD ITEMS AT CHEST MOUTH ---
            // Everything starts invisible, stacked behind the open chest
            Vector2 spawnOrigin = rewardChestRestingPos; // (-1.1, 27.5)

            if (goldCoinsContainer != null)
            {
                goldCoinsContainer.DOKill();
                goldCoinsContainer.localScale = Vector3.zero;
                goldCoinsContainer.anchoredPosition = spawnOrigin;
            }
            if (armyRewardCard != null)
            {
                armyRewardCard.DOKill();
                armyRewardCard.localScale = Vector3.zero;
                armyRewardCard.anchoredPosition = spawnOrigin;
            }
            if (powerRewardCard != null)
            {
                powerRewardCard.DOKill();
                powerRewardCard.localScale = Vector3.zero;
                powerRewardCard.anchoredPosition = spawnOrigin;
            }
            if (collectButton != null)
            {
                collectButton.interactable = false;
                collectButton.transform.localScale = Vector3.zero;
            }

            // Pre-set reward data before animation starts
            if (goldCoinsText != null) goldCoinsText.text = "+0 Coin";

            if (armyCardImage != null && cardBackSprite != null)
                armyCardImage.sprite = cardBackSprite;
            if (armyCardCount != null) armyCardCount.text = $"{currentReward.armyCopies}x";
            if (armyCardName != null) armyCardName.text = currentReward.armyCardName;

            if (powerCardImage != null && cardBackSprite != null)
                powerCardImage.sprite = cardBackSprite;
            if (powerCardCount != null) powerCardCount.text = $"{currentReward.powerCopies}x";
            if (powerCardName != null) powerCardName.text = currentReward.powerCardName;

            StartCoroutine(ChoreographedRewardRevealRoutine());
        }

        /// <summary>
        /// Choreographed cinematic reward reveal: items erupt from the chest mouth
        /// with parabolic arcs, 3D card flips, and satisfying impact landings.
        /// All items snap to exact authored resting positions after animation.
        /// </summary>
        private IEnumerator ChoreographedRewardRevealRoutine()
        {
            // Wait for chest to glide into reward position
            yield return new WaitForSecondsRealtime(0.38f);

            // Chest arrival punch
            if (rewardChestImage != null)
            {
                rewardChestImage.rectTransform.DOPunchScale(rewardChestRestingScale * 0.14f, 0.28f, 5, 0.5f);
            }

            // Spawn origin = chest mouth position (slightly above chest center)
            Vector2 spawnOrigin = rewardChestRestingPos; // (-1.1, 27.5)
            float arcApex = 42f; // Y apex above the chest for parabolic arcs

            yield return new WaitForSecondsRealtime(0.12f);

            // ============================================================
            // PHASE 1: BOTH CARDS ERUPT SIMULTANEOUSLY (left + right arc)
            // ============================================================
            float cardFlightTime = 0.50f;

            // --- ARMY CARD (arcs to left slot) ---
            if (armyRewardCard != null)
            {
                PlaySfx(cardFlipSound);

                // Hide copy badge initially
                if (armyCardCount != null) armyCardCount.rectTransform.localScale = Vector3.zero;

                armyRewardCard.anchoredPosition = spawnOrigin;
                armyRewardCard.localScale = new Vector3(0.01f, armyCardRestingScale.y * 0.3f, armyCardRestingScale.z);

                // X: sweep left with deceleration
                armyRewardCard.DOAnchorPosX(armyCardRestingPos.x, cardFlightTime).SetEase(Ease.OutCubic);

                // Y: arc up then down (parabolic)
                Sequence armyYArc = DOTween.Sequence();
                armyYArc.Append(armyRewardCard.DOAnchorPosY(arcApex, cardFlightTime * 0.40f).SetEase(Ease.OutSine));
                armyYArc.Append(armyRewardCard.DOAnchorPosY(armyCardRestingPos.y, cardFlightTime * 0.60f).SetEase(Ease.InQuad));

                // Scale Y: grow to full height during flight
                armyRewardCard.DOScaleY(armyCardRestingScale.y, cardFlightTime * 0.65f).SetEase(Ease.OutQuad);

                // 3D Card Flip: start thin, expand, squeeze to 0 at apex (card-back visible), flip sprite, expand to full
                Sequence armyFlip = DOTween.Sequence();
                armyFlip.Append(armyRewardCard.DOScaleX(armyCardRestingScale.x * 0.9f, cardFlightTime * 0.28f).SetEase(Ease.OutQuad));
                armyFlip.Append(armyRewardCard.DOScaleX(0.001f, cardFlightTime * 0.12f).SetEase(Ease.InQuad)
                    .OnComplete(() =>
                    {
                        // Flip the card face!
                        if (armyCardImage != null && currentReward.armyCardSprite != null)
                            armyCardImage.sprite = currentReward.armyCardSprite;
                    }));
                armyFlip.Append(armyRewardCard.DOScaleX(armyCardRestingScale.x * 1.15f, cardFlightTime * 0.35f).SetEase(Ease.OutQuad));
                armyFlip.Append(armyRewardCard.DOScaleX(armyCardRestingScale.x, cardFlightTime * 0.25f).SetEase(Ease.OutBounce));

                // Trail sparkles during flight
                SpawnSparkleBurst(rewardPanel, spawnOrigin, 3, new Color(0.4f, 0.85f, 1f, 0.8f));
            }

            // --- POWER CARD (arcs to right slot, slightly delayed for stagger) ---
            if (powerRewardCard != null)
            {
                // Hide copy badge initially
                if (powerCardCount != null) powerCardCount.rectTransform.localScale = Vector3.zero;

                powerRewardCard.anchoredPosition = spawnOrigin;
                powerRewardCard.localScale = new Vector3(0.01f, powerCardRestingScale.y * 0.3f, powerCardRestingScale.z);

                float stagger = 0.08f; // Slight delay so they don't look identical

                // X: sweep right with deceleration
                powerRewardCard.DOAnchorPosX(powerCardRestingPos.x, cardFlightTime)
                    .SetDelay(stagger).SetEase(Ease.OutCubic);

                // Y: arc up then down
                Sequence powerYArc = DOTween.Sequence();
                powerYArc.SetDelay(stagger);
                powerYArc.Append(powerRewardCard.DOAnchorPosY(arcApex, cardFlightTime * 0.40f).SetEase(Ease.OutSine));
                powerYArc.Append(powerRewardCard.DOAnchorPosY(powerCardRestingPos.y, cardFlightTime * 0.60f).SetEase(Ease.InQuad));

                // Scale Y: grow
                powerRewardCard.DOScaleY(powerCardRestingScale.y, cardFlightTime * 0.65f)
                    .SetDelay(stagger).SetEase(Ease.OutQuad);

                // 3D Card Flip
                Sequence powerFlip = DOTween.Sequence();
                powerFlip.SetDelay(stagger);
                powerFlip.Append(powerRewardCard.DOScaleX(powerCardRestingScale.x * 0.9f, cardFlightTime * 0.28f).SetEase(Ease.OutQuad));
                powerFlip.Append(powerRewardCard.DOScaleX(0.001f, cardFlightTime * 0.12f).SetEase(Ease.InQuad)
                    .OnComplete(() =>
                    {
                        PlaySfx(cardFlipSound);
                        if (powerCardImage != null && currentReward.powerCardSprite != null)
                            powerCardImage.sprite = currentReward.powerCardSprite;
                    }));
                powerFlip.Append(powerRewardCard.DOScaleX(powerCardRestingScale.x * 1.15f, cardFlightTime * 0.35f).SetEase(Ease.OutQuad));
                powerFlip.Append(powerRewardCard.DOScaleX(powerCardRestingScale.x, cardFlightTime * 0.25f).SetEase(Ease.OutBounce));

                SpawnSparkleBurst(rewardPanel, spawnOrigin, 3, new Color(1f, 0.85f, 0.4f, 0.8f));
            }

            // Wait for the cards to land
            yield return new WaitForSecondsRealtime(cardFlightTime + 0.08f);

            // --- CARD LANDING IMPACTS (simultaneous) ---
            if (armyRewardCard != null)
            {
                // Snap to exact position first
                armyRewardCard.anchoredPosition = armyCardRestingPos;
                armyRewardCard.localScale = armyCardRestingScale;
                // Squash & stretch impact
                Sequence armyLand = DOTween.Sequence();
                armyLand.Append(armyRewardCard.DOScale(new Vector3(armyCardRestingScale.x * 1.18f, armyCardRestingScale.y * 0.82f, armyCardRestingScale.z), 0.06f));
                armyLand.Append(armyRewardCard.DOScale(new Vector3(armyCardRestingScale.x * 0.92f, armyCardRestingScale.y * 1.08f, armyCardRestingScale.z), 0.08f));
                armyLand.Append(armyRewardCard.DOScale(armyCardRestingScale, 0.10f).SetEase(Ease.OutBounce));
                SpawnSparkleBurst(rewardPanel, armyCardRestingPos, 7, new Color(0.35f, 0.85f, 1f, 1f));

                // Pop copy count badge
                if (armyCardCount != null)
                {
                    armyCardCount.rectTransform.localScale = Vector3.zero;
                    armyCardCount.rectTransform.DOScale(Vector3.one, 0.28f)
                        .SetDelay(0.06f)
                        .SetEase(Ease.OutBack, 2.0f);
                }
            }

            if (powerRewardCard != null)
            {
                powerRewardCard.anchoredPosition = powerCardRestingPos;
                powerRewardCard.localScale = powerCardRestingScale;
                Sequence powerLand = DOTween.Sequence();
                powerLand.Append(powerRewardCard.DOScale(new Vector3(powerCardRestingScale.x * 1.18f, powerCardRestingScale.y * 0.82f, powerCardRestingScale.z), 0.06f));
                powerLand.Append(powerRewardCard.DOScale(new Vector3(powerCardRestingScale.x * 0.92f, powerCardRestingScale.y * 1.08f, powerCardRestingScale.z), 0.08f));
                powerLand.Append(powerRewardCard.DOScale(powerCardRestingScale, 0.10f).SetEase(Ease.OutBounce));
                SpawnSparkleBurst(rewardPanel, powerCardRestingPos, 7, new Color(1f, 0.75f, 0.25f, 1f));

                if (powerCardCount != null)
                {
                    powerCardCount.rectTransform.localScale = Vector3.zero;
                    powerCardCount.rectTransform.DOScale(Vector3.one, 0.28f)
                        .SetDelay(0.06f)
                        .SetEase(Ease.OutBack, 2.0f);
                }
            }

            yield return new WaitForSecondsRealtime(0.20f);

            // ============================================================
            // PHASE 2: COINS RAIN DOWN FROM CHEST (center, with fountain)
            // ============================================================
            if (goldCoinsContainer != null)
            {
                PlaySfx(coinTickSound);

                // Shower of coins from chest to coin plate
                SpawnFlyingCoinsStream(rewardPanel, spawnOrigin, goldCoinsRestingPos, 10);

                // The coin container itself drops down from chest
                goldCoinsContainer.anchoredPosition = spawnOrigin;
                goldCoinsContainer.localScale = goldCoinsRestingScale * 0.3f;

                goldCoinsContainer.DOAnchorPos(goldCoinsRestingPos, 0.38f)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() =>
                    {
                        goldCoinsContainer.anchoredPosition = goldCoinsRestingPos;
                    });
                goldCoinsContainer.DOScale(goldCoinsRestingScale, 0.38f)
                    .SetEase(Ease.OutBack, 1.3f)
                    .OnComplete(() =>
                    {
                        goldCoinsContainer.localScale = goldCoinsRestingScale;
                    });

                yield return new WaitForSecondsRealtime(0.38f);

                // Impact bounce
                Sequence coinLand = DOTween.Sequence();
                coinLand.Append(goldCoinsContainer.DOScale(new Vector3(goldCoinsRestingScale.x * 1.22f, goldCoinsRestingScale.y * 0.80f, goldCoinsRestingScale.z), 0.07f));
                coinLand.Append(goldCoinsContainer.DOScale(goldCoinsRestingScale, 0.14f).SetEase(Ease.OutBounce));
                coinLand.OnComplete(() => goldCoinsContainer.localScale = goldCoinsRestingScale);

                SpawnSparkleBurst(rewardPanel, goldCoinsRestingPos, 8, new Color(1f, 0.92f, 0.35f, 1f));

                // Rolling coin counter with ticks
                int startCoins = 0;
                int targetCoins = currentReward.coins;
                float lastTick = 0f;
                DOTween.To(() => startCoins, x =>
                {
                    startCoins = x;
                    if (goldCoinsText != null) goldCoinsText.text = $"+{startCoins} Coin";
                    if (Time.unscaledTime - lastTick > 0.06f)
                    {
                        lastTick = Time.unscaledTime;
                        PlayCoinTickSound();
                    }
                }, targetCoins, 0.50f).SetEase(Ease.OutQuad);

                yield return new WaitForSecondsRealtime(0.30f);
            }

            // ============================================================
            // PHASE 3: COLLECT BUTTON DROP
            // ============================================================
            if (collectButton != null)
            {
                collectButton.interactable = true;
                collectButton.transform.DOScale(collectButtonRestingScale, 0.38f)
                    .SetEase(Ease.OutBack, 1.5f)
                    .OnComplete(() =>
                    {
                        collectButton.transform.localScale = collectButtonRestingScale;
                        collectPulseTween = collectButton.transform
                            .DOScale(collectButtonRestingScale * 1.04f, 0.90f)
                            .SetEase(Ease.InOutSine)
                            .SetLoops(-1, LoopType.Yoyo);
                    });
            }
        }

        public void OnCollectClicked()
        {
            if (collectButton != null)
            {
                collectButton.interactable = false;
                collectButton.transform.DOKill();
                collectButton.transform.DOScale(collectButtonRestingScale * 0.90f, 0.1f)
                    .OnComplete(() => collectButton.transform.DOScale(collectButtonRestingScale, 0.15f));
            }

            PlaySfx(collectSound);
            KoSAudio.Play2D("reward_jingle");

            // Flying rewards animation up towards top HUD
            if (goldCoinsContainer != null)
            {
                goldCoinsContainer.DOAnchorPosY(goldCoinsRestingPos.y + 90f, 0.28f).SetEase(Ease.InQuad);
                goldCoinsContainer.DOScale(Vector3.zero, 0.28f).SetEase(Ease.InQuad);
            }
            if (armyRewardCard != null)
            {
                armyRewardCard.DOAnchorPosY(armyCardRestingPos.y + 90f, 0.28f).SetEase(Ease.InQuad);
                armyRewardCard.DOScale(Vector3.zero, 0.28f).SetEase(Ease.InQuad);
            }
            if (powerRewardCard != null)
            {
                powerRewardCard.DOAnchorPosY(powerCardRestingPos.y + 90f, 0.28f).SetEase(Ease.InQuad);
                powerRewardCard.DOScale(Vector3.zero, 0.28f).SetEase(Ease.InQuad);
            }

            // Grant rewards authoritatively to player profile
            CommitRewardsToPlayerProfile(currentReward);

            // Consume the opened chest slot, then let the next queued chest start its timer
            if (currentActiveSlot != null)
            {
                currentActiveSlot.ConsumeChest();
            }
            PromoteQueuedChest();

            // Smooth exit and dismiss
            if (rewardPanel != null)
            {
                rewardPanel.DOScale(rewardPanelRestingScale * 0.7f, 0.25f)
                    .SetDelay(0.12f)
                    .SetEase(Ease.InBack)
                    .OnComplete(() =>
                    {
                        rewardPanel.gameObject.SetActive(false);
                        rewardPanel.localScale = rewardPanelRestingScale;
                        if (openingInteriorLoot != null) openingInteriorLoot.transform.localScale = Vector3.zero;
                        if (rewardInteriorLoot != null) rewardInteriorLoot.transform.localScale = Vector3.zero;
                        CloseChestsMenu();
                    });
            }
            else
            {
                CloseChestsMenu();
            }
        }

        private void CommitRewardsToPlayerProfile(ChestReward reward)
        {
            if (reward == null) return;

            // 1. Add coins
            int prevCoins = GetSavedCoins();
            int newCoins = prevCoins + reward.coins;
            PlayerPrefs.SetInt("PlayerCoins", newCoins);

            // Animate top HUD currency counter
            AnimateCoinIncrement(prevCoins, newCoins);

            // 2. Add Army unit copies
            if (!string.IsNullOrEmpty(reward.armyCardName))
            {
                string key = $"CardCopies_{reward.armyCardName}";
                int existing = PlayerPrefs.GetInt(key, 0);
                PlayerPrefs.SetInt(key, existing + reward.armyCopies);

                // Authoritatively credit copies to CardInventoryManager for card level upgrades
                if (Siege.Cards.CardInventoryManager.Instance != null)
                {
                    Siege.Cards.CardInventoryManager.Instance.AddCardCopiesByReward(reward.armyCardName, reward.armyCardSprite, reward.armyCopies);
                }
            }

            // 3. Add Power copies
            if (!string.IsNullOrEmpty(reward.powerCardName))
            {
                string key = $"PowerCopies_{reward.powerCardName}";
                int existing = PlayerPrefs.GetInt(key, 0);
                PlayerPrefs.SetInt(key, existing + reward.powerCopies);

                // Authoritatively credit copies to CardInventoryManager for card level upgrades
                if (Siege.Cards.CardInventoryManager.Instance != null)
                {
                    Siege.Cards.CardInventoryManager.Instance.AddCardCopiesByReward(reward.powerCardName, reward.powerCardSprite, reward.powerCopies);
                }
            }

            PlayerPrefs.Save();
            Debug.Log($"<color=#FFCC00>[ChestSystem] Collected: {reward.coins} Coins, {reward.armyCopies}x {reward.armyCardName}, {reward.powerCopies}x {reward.powerCardName}</color>");
        }

        private int GetSavedCoins()
        {
            return PlayerPrefs.GetInt("PlayerCoins", 12500);
        }

        private void UpdateHudCoinDisplay(int coins, bool punch)
        {
            if (hudCoinCounterText != null)
            {
                hudCoinCounterText.text = coins.ToString("N0");
                if (punch)
                {
                    hudCoinCounterText.rectTransform.DOKill();
                    hudCoinCounterText.rectTransform.DOPunchScale(Vector3.one * 0.25f, 0.4f, 6, 0.6f);
                }
            }
        }

        private void AnimateCoinIncrement(int from, int to)
        {
            if (hudCoinCounterText == null) return;

            int current = from;
            DOTween.To(() => current, x =>
            {
                current = x;
                hudCoinCounterText.text = current.ToString("N0");
            }, to, 0.9f)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                hudCoinCounterText.text = to.ToString("N0");
                hudCoinCounterText.rectTransform.DOPunchScale(Vector3.one * 0.3f, 0.45f, 6, 0.6f);
            });
        }

        #endregion

        #region Reward Generation

        /// <summary>
        /// Generates chest rewards strictly matching the specifications:
        /// Silver: 2 hours | 20-200 Coins | 2 Army Copies | 2 Power Copies
        /// Gold: 8 hours | 100-500 Coins | 4 Army Copies | 4 Power Copies
        /// Master: 24 hours | 500-1000 Coins | 10 Army Copies | 10 Power Copies
        /// </summary>
        public ChestReward GenerateChestRewards(ChestConfig config)
        {
            var reward = new ChestReward
            {
                chestType = config.type,
                coins = UnityEngine.Random.Range(config.minCoins, config.maxCoins + 1),
                armyCopies = config.armyCopies,
                powerCopies = config.powerCopies
            };

            // Pick random Army Unit
            if (armyUnitSprites != null && armyUnitSprites.Length > 0)
            {
                int rIndex = UnityEngine.Random.Range(0, armyUnitSprites.Length);
                reward.armyCardSprite = armyUnitSprites[rIndex];
                reward.armyCardName = CleanCardName(armyUnitSprites[rIndex].name);
            }
            else
            {
                reward.armyCardName = "Swordsman";
            }

            // Pick random Power Card
            if (powerCardSprites != null && powerCardSprites.Length > 0)
            {
                int rIndex = UnityEngine.Random.Range(0, powerCardSprites.Length);
                reward.powerCardSprite = powerCardSprites[rIndex];
                reward.powerCardName = CleanCardName(powerCardSprites[rIndex].name);
            }
            else
            {
                reward.powerCardName = "Power Cards";
            }

            return reward;
        }

        private string CleanCardName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return "Card";
            // Strip leading numbers like "01_Swordsman" -> "Swordsman"
            string clean = rawName;
            if (clean.Length > 3 && char.IsDigit(clean[0]) && char.IsDigit(clean[1]) && clean[2] == '_')
            {
                clean = clean.Substring(3);
            }
            clean = clean.Replace('_', ' ');
            return clean;
        }

        #endregion

        #region Audio & Helpers

        public void PlayOpenChestSound() => PlaySfx(openChestSound);
        public void PlayCardFlipSound() => PlaySfx(cardFlipSound);
        public void PlayCoinTickSound() => PlaySfx(coinTickSound);
        public void PlayCollectSound() => PlaySfx(collectSound);

        private void PlaySfx(AudioClip clip)
        {
            if (clip != null && audioSource != null)
            {
                KoSAudio.PlayOneShot(audioSource, clip, 1f, "chest");
            }
        }

        private void KillAllActiveTweens()
        {
            if (glowRotateTween != null && glowRotateTween.IsActive()) glowRotateTween.Kill();
            if (chestFloatTween != null && chestFloatTween.IsActive()) chestFloatTween.Kill();
            if (tapTextPulseTween != null && tapTextPulseTween.IsActive()) tapTextPulseTween.Kill();
            if (collectPulseTween != null && collectPulseTween.IsActive()) collectPulseTween.Kill();
        }

        #endregion
    }
}
