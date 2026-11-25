using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using DG.Tweening;

/// <summary>
/// Ultra-juicy, AAA mobile main menu controller and animation system powered by DOTween.
/// Calibrated for the Kings of Siege / Clash Royale mobile portrait layout:
/// - Coordinated entrance choreography (sliding headers, popping league banners, bouncing king, pulsing battle button).
/// - Living idle animations (King breathing bob, battle button pulse, league badge float, side quest attention wiggle).
/// - Punchy tactile squash & stretch feedback for all interactive buttons.
/// - Tab switching animations for the bottom navigation bar.
/// - Full Edit Mode & Play Mode live preview capability.
/// </summary>
[DisallowMultipleComponent]
public class MainMenuController : MonoBehaviour
{
    private static MainMenuController instance;
    public static MainMenuController Instance => instance;

    public enum BattleAction
    {
        MultiplayerMatchmaking,
        PlayVsAI,
        CustomEventOnly
    }

    [Header("UI Element References")]
    [Tooltip("Root canvas group for panel fade effects (auto-added if missing).")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("Background image / scenery container.")]
    [SerializeField] private RectTransform backgroundRect;

    [Tooltip("Top status bar holding player avatar, gold, gems, and settings.")]
    [SerializeField] private RectTransform topPanelRect;

    [Tooltip("League banner holding tier name, shield badge, and trophy count.")]
    [SerializeField] private RectTransform leagueRect;

    [Tooltip("The shield or badge inside the league banner.")]
    [SerializeField] private RectTransform leagueBadgeRect;

    [Tooltip("The centerpiece King character sprite/model.")]
    [SerializeField] private RectTransform kingRect;

    [Tooltip("The primary call-to-action BATTLE button.")]
    [SerializeField] private RectTransform battleButtonRect;
    [SerializeField] private Button battleButton;

    [Tooltip("Right side buttons container (Missions, Rewards).")]
    [SerializeField] private RectTransform sidePanelRect;
    [SerializeField] private RectTransform[] sideButtons;

    [Tooltip("Bottom navigation bar (Shop, Cards, Home, Rankings).")]
    [SerializeField] private RectTransform downPanelRect;
    [SerializeField] private RectTransform[] downPanelTabs;

    [Header("Top Panel Controls")]
    [SerializeField] private Button avatarButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private TMP_Text coinCounterText;
    [SerializeField] private TMP_Text gemsCounterText;

    [Header("Entrance Animation Settings")]
    [SerializeField] private bool playEntranceOnStart = true;
    [SerializeField, Range(0.2f, 1.5f)] private float entranceDuration = 0.65f;
    [SerializeField] private Ease entranceEase = Ease.OutBack;
    [SerializeField, Range(0.01f, 0.2f)] private float staggerDelay = 0.06f;

    [Header("Idle Living Animation Settings")]
    [SerializeField] private bool enableIdleAnimations = true;
    [Tooltip("Vertical bob distance for the King character (pixels).")]
    [SerializeField, Range(2f, 25f)] private float kingBobDistance = 10f;
    [Tooltip("Cycle duration of the King floating breathing loop.")]
    [SerializeField, Range(1f, 4f)] private float kingBobDuration = 2.4f;
    [Tooltip("Subtle breathing squash/stretch multiplier for the King.")]
    [SerializeField, Range(1.01f, 1.08f)] private float kingBreathScaleMultiplier = 1.03f;
    [Tooltip("Continuous pulse scale multiplier for the BATTLE button.")]
    [SerializeField, Range(1.02f, 1.12f)] private float battlePulseMultiplier = 1.05f;
    [Tooltip("Battle button pulse cycle duration.")]
    [SerializeField, Range(0.6f, 2.0f)] private float battlePulseDuration = 1.1f;

    [Header("Battle Action Settings")]
    [SerializeField] private BattleAction battleAction = BattleAction.MultiplayerMatchmaking;
    [SerializeField] private UnityEvent onBattleClicked;

    [Header("Audio (Optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioClip battleClickSound;
    [SerializeField] private AudioClip kingTapSound;

    // --- Cached Resting State for 100% Precision Layout Preservation ---
    [System.Serializable]
    public struct ElementRestingState
    {
        public Vector2 anchoredPosition;
        public Vector3 localScale;
        public Quaternion localRotation;
    }

    [SerializeField, HideInInspector] private bool hasRecordedRestingState = false;
    [SerializeField, HideInInspector] private ElementRestingState topPanelResting;
    [SerializeField, HideInInspector] private ElementRestingState leagueResting;
    [SerializeField, HideInInspector] private ElementRestingState leagueBadgeResting;
    [SerializeField, HideInInspector] private ElementRestingState kingResting;
    [SerializeField, HideInInspector] private ElementRestingState battleButtonResting;
    [SerializeField, HideInInspector] private ElementRestingState sidePanelResting;
    [SerializeField, HideInInspector] private ElementRestingState downPanelResting;
    [SerializeField, HideInInspector] private ElementRestingState backgroundResting;
    [SerializeField, HideInInspector] private List<ElementRestingState> sideButtonsResting = new List<ElementRestingState>();
    [SerializeField, HideInInspector] private List<ElementRestingState> downTabsResting = new List<ElementRestingState>();

    // Runtime Tween Handles
    private Sequence entranceSequence;
    private Sequence exitSequence;
    private Tween kingBobTween;
    private Tween kingBreathTween;
    private Tween battlePulseTween;
    private Tween badgeFloatTween;
    private Coroutine sideAttentionCoroutine;
    private int currentSelectedTab = 2; // Default to 'Home' (3rd tab)
    private bool isExiting = false;
    private bool isBattlePressed = false;
    private readonly List<CanvasGroup> managedExitCanvasGroups = new List<CanvasGroup>();

    private void Awake()
    {
        if (instance == null) instance = this;

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        RecordRestingTransformsIfNeeded();
        HookButtonListeners();


        Invoke("StartANIM",0.2f);

    }

    public void StartANIM(){
                                    PlayEntranceAnimation();

    }



    private void Start()
    {
        if (playEntranceOnStart)
        {
            PlayEntranceAnimation();
        }
        else if (enableIdleAnimations)
        {
            StartIdleAnimations();
        }
    }

    private void OnDisable()
    {
        KillAllTweens();
        ResetToRestingState();
    }

    private void OnDestroy()
    {
        KillAllTweens();
    }

    #region Layout Caching & Auto-Wiring

    /// <summary>
    /// Records the exact current scene positions, rotations, and scales as bedrock resting coordinates.
    /// This guarantees zero layout drift regardless of multiple animations or interrupted tweens.
    /// </summary>
    public void RecordRestingTransforms()
    {
        if (topPanelRect != null) topPanelResting = RecordState(topPanelRect);
        if (leagueRect != null) leagueResting = RecordState(leagueRect);
        if (leagueBadgeRect != null) leagueBadgeResting = RecordState(leagueBadgeRect);
        if (kingRect != null) kingResting = RecordState(kingRect);
        if (battleButtonRect != null) battleButtonResting = RecordState(battleButtonRect);
        if (sidePanelRect != null) sidePanelResting = RecordState(sidePanelRect);
        if (downPanelRect != null) downPanelResting = RecordState(downPanelRect);
        if (backgroundRect != null) backgroundResting = RecordState(backgroundRect);

        sideButtonsResting.Clear();
        if (sideButtons != null)
        {
            foreach (var b in sideButtons)
            {
                if (b != null) sideButtonsResting.Add(RecordState(b));
            }
        }

        downTabsResting.Clear();
        if (downPanelTabs != null)
        {
            foreach (var t in downPanelTabs)
            {
                if (t != null) downTabsResting.Add(RecordState(t));
            }
        }

        hasRecordedRestingState = true;
    }

    private void RecordRestingTransformsIfNeeded()
    {
        if (!hasRecordedRestingState || kingResting.localScale == Vector3.zero)
        {
            RecordRestingTransforms();
        }
    }

    private ElementRestingState RecordState(RectTransform rt)
    {
        return new ElementRestingState
        {
            anchoredPosition = rt.anchoredPosition,
            localScale = rt.localScale,
            localRotation = rt.localRotation
        };
    }

    private void ApplyState(RectTransform rt, ElementRestingState state)
    {
        if (rt == null) return;
        rt.DOKill();
        rt.anchoredPosition = state.anchoredPosition;
        rt.localScale = state.localScale;
        rt.localRotation = state.localRotation;
    }

    /// <summary>
    /// Restores all UI elements back to their precisely recorded resting transforms.
    /// </summary>
    public void ResetToRestingState()
    {
        KillAllTweens();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        if (hasRecordedRestingState)
        {
            ApplyState(topPanelRect, topPanelResting);
            ApplyState(leagueRect, leagueResting);
            ApplyState(leagueBadgeRect, leagueBadgeResting);
            ApplyState(kingRect, kingResting);
            ApplyState(battleButtonRect, battleButtonResting);
            ApplyState(sidePanelRect, sidePanelResting);
            ApplyState(downPanelRect, downPanelResting);
            ApplyState(backgroundRect, backgroundResting);

            if (sideButtons != null && sideButtonsResting != null)
            {
                for (int i = 0; i < sideButtons.Length && i < sideButtonsResting.Count; i++)
                {
                    ApplyState(sideButtons[i], sideButtonsResting[i]);
                }
            }

            if (downPanelTabs != null && downTabsResting != null)
            {
                for (int i = 0; i < downPanelTabs.Length && i < downTabsResting.Count; i++)
                {
                    ApplyState(downPanelTabs[i], downTabsResting[i]);
                }
            }
        }

        RestoreManagedCanvasGroups();
        isExiting = false;
        isBattlePressed = false;
    }

    private void RestoreManagedCanvasGroups()
    {
        for (int i = 0; i < managedExitCanvasGroups.Count; i++)
        {
            CanvasGroup cg = managedExitCanvasGroups[i];
            if (cg == null) continue;
            cg.DOKill();
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
    }

    /// <summary>
    /// Intelligently discovers and auto-links all UI elements in the hierarchy by name or component.
    /// </summary>
    public void AutoWireHierarchy()
    {
        Transform root = transform;

        // Auto-find CanvasGroup
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // Auto-find Background
        if (backgroundRect == null)
        {
            Transform bg = FindDeepChild(root, "background") ?? FindDeepChild(root, "Background") ?? FindDeepChild(root, "bg");
            if (bg != null) backgroundRect = bg.GetComponent<RectTransform>();
        }

        // Auto-find Top Panel
        if (topPanelRect == null)
        {
            Transform tp = FindDeepChild(root, "toppanel") ?? FindDeepChild(root, "TopPanel") ?? FindDeepChild(root, "topPanel");
            if (tp != null) topPanelRect = tp.GetComponent<RectTransform>();
        }

        // Auto-find League Banner
        if (leagueRect == null)
        {
            Transform lg = FindDeepChild(root, "leauge") ?? FindDeepChild(root, "league") ?? FindDeepChild(root, "League");
            if (lg != null) leagueRect = lg.GetComponent<RectTransform>();
        }

        if (leagueRect != null && leagueBadgeRect == null)
        {
            Transform bd = FindDeepChild(leagueRect, "badge") ?? FindDeepChild(leagueRect, "Badge");
            if (bd != null) leagueBadgeRect = bd.GetComponent<RectTransform>();
        }

        // Auto-find King Character
        if (kingRect == null)
        {
            Transform kg = FindDeepChild(root, "king") ?? FindDeepChild(root, "King") ?? FindDeepChild(root, "character");
            if (kg != null) kingRect = kg.GetComponent<RectTransform>();
        }

        // Auto-find Battle Button
        if (battleButtonRect == null)
        {
            Transform bb = FindDeepChild(root, "battleButton") ?? FindDeepChild(root, "BattleButton") ?? FindDeepChild(root, "battle");
            if (bb != null) battleButtonRect = bb.GetComponent<RectTransform>();
        }

        if (battleButtonRect != null && battleButton == null)
        {
            battleButton = battleButtonRect.GetComponent<Button>();
            if (battleButton == null)
            {
                battleButton = battleButtonRect.gameObject.AddComponent<Button>();
            }
        }

        // Auto-find Side Panel
        if (sidePanelRect == null)
        {
            Transform sp = FindDeepChild(root, "sidepanel") ?? FindDeepChild(root, "SidePanel") ?? FindDeepChild(root, "side_panel");
            if (sp != null) sidePanelRect = sp.GetComponent<RectTransform>();
        }

        if (sidePanelRect != null && (sideButtons == null || sideButtons.Length == 0))
        {
            List<RectTransform> sbs = new List<RectTransform>();
            for (int i = 0; i < sidePanelRect.childCount; i++)
            {
                RectTransform child = sidePanelRect.GetChild(i) as RectTransform;
                if (child != null && child.gameObject.activeSelf)
                {
                    sbs.Add(child);
                }
            }
            sideButtons = sbs.ToArray();
        }

        // Auto-find Down Panel
        if (downPanelRect == null)
        {
            Transform dp = FindDeepChild(root, "downpanel") ?? FindDeepChild(root, "DownPanel") ?? FindDeepChild(root, "bottombar") ?? FindDeepChild(root, "navigation");
            if (dp != null) downPanelRect = dp.GetComponent<RectTransform>();
        }

        if (downPanelRect != null && (downPanelTabs == null || downPanelTabs.Length == 0))
        {
            List<RectTransform> tabs = new List<RectTransform>();
            for (int i = 0; i < downPanelRect.childCount; i++)
            {
                RectTransform child = downPanelRect.GetChild(i) as RectTransform;
                if (child != null)
                {
                    tabs.Add(child);
                }
            }
            downPanelTabs = tabs.ToArray();
        }

        // Top Panel details
        if (topPanelRect != null)
        {
            if (avatarButton == null)
            {
                Transform av = FindDeepChild(topPanelRect, "avataricon") ?? FindDeepChild(topPanelRect, "Avatar");
                if (av != null)
                {
                    avatarButton = av.GetComponent<Button>();
                    if (avatarButton == null) avatarButton = av.gameObject.AddComponent<Button>();
                }
            }

            if (settingsButton == null)
            {
                Transform st = FindDeepChild(topPanelRect, "settings") ?? FindDeepChild(topPanelRect, "Settings");
                if (st != null)
                {
                    settingsButton = st.GetComponent<Button>();
                    if (settingsButton == null) settingsButton = st.gameObject.AddComponent<Button>();
                }
            }

            if (coinCounterText == null)
            {
                Transform cc = FindDeepChild(topPanelRect, "coinCounter") ?? FindDeepChild(topPanelRect, "coins");
                if (cc != null) coinCounterText = cc.GetComponentInChildren<TMP_Text>();
            }

            if (gemsCounterText == null)
            {
                Transform gc = FindDeepChild(topPanelRect, "gemsCounter") ?? FindDeepChild(topPanelRect, "gems");
                if (gc != null) gemsCounterText = gc.GetComponentInChildren<TMP_Text>();
            }
        }

        RecordRestingTransforms();
        Debug.Log("<color=#00FFAA>[MainMenuController] Hierarchy auto-wired and resting coordinates recorded successfully!</color>");
    }

    private Transform FindDeepChild(Transform parent, string name)
    {
        if (parent == null) return null;
        if (string.Equals(parent.name, name, StringComparison.OrdinalIgnoreCase)) return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase)) return child;
            Transform result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }

    #endregion

    #region Button Listeners & Interaction Hookup

    private void HookButtonListeners()
    {
        if (battleButton != null)
        {
            battleButton.onClick.RemoveListener(OnBattleButtonClicked);
            battleButton.onClick.AddListener(OnBattleButtonClicked);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(OnSettingsButtonClicked);
            settingsButton.onClick.AddListener(OnSettingsButtonClicked);
        }

        if (avatarButton != null)
        {
            avatarButton.onClick.RemoveListener(OnAvatarButtonClicked);
            avatarButton.onClick.AddListener(OnAvatarButtonClicked);
        }

        // Add King click handler if King has graphic
        if (kingRect != null)
        {
            Button kingBtn = kingRect.GetComponent<Button>();
            if (kingBtn == null)
            {
                // Ensure King has RaycastTarget graphic to receive clicks
                Graphic g = kingRect.GetComponent<Graphic>();
                if (g != null)
                {
                    g.raycastTarget = true;
                    kingBtn = kingRect.gameObject.AddComponent<Button>();
                    kingBtn.transition = Selectable.Transition.None;
                }
            }
            if (kingBtn != null)
            {
                kingBtn.onClick.RemoveListener(OnKingCharacterClicked);
                kingBtn.onClick.AddListener(OnKingCharacterClicked);
            }
        }

        // Hook side panel buttons
        if (sideButtons != null)
        {
            for (int i = 0; i < sideButtons.Length; i++)
            {
                int index = i;
                RectTransform sb = sideButtons[i];
                if (sb == null) continue;

                Button btn = sb.GetComponent<Button>();
                if (btn == null)
                {
                    Graphic g = sb.GetComponent<Graphic>();
                    if (g != null) g.raycastTarget = true;
                    btn = sb.gameObject.AddComponent<Button>();
                    btn.transition = Selectable.Transition.None;
                }

                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnSideButtonClicked(index, sb));
            }
        }

        // Hook bottom bar tabs
        if (downPanelTabs != null)
        {
            for (int i = 0; i < downPanelTabs.Length; i++)
            {
                int tabIdx = i;
                RectTransform tab = downPanelTabs[i];
                if (tab == null) continue;

                Button btn = tab.GetComponent<Button>();
                if (btn == null)
                {
                    Graphic g = tab.GetComponent<Graphic>();
                    if (g != null) g.raycastTarget = true;
                    btn = tab.gameObject.AddComponent<Button>();
                    btn.transition = Selectable.Transition.None;
                }

                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnBottomTabClicked(tabIdx, tab));
            }
        }
    }

    #endregion

    #region Entrance & Exit Choreography

    /// <summary>
    /// Plays the full choreographed mobile main menu entrance animation.
    /// </summary>
    public void PlayEntranceAnimation()
    {
        RecordRestingTransformsIfNeeded();
        KillAllTweens();
        RestoreManagedCanvasGroups();
        isExiting = false;
        isBattlePressed = false;

        float screenH = Screen.height > 0 ? Screen.height : 1920f;
        float screenW = Screen.width > 0 ? Screen.width : 1080f;

        // 1. Initial State Preparation
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        // Background initial state
        if (backgroundRect != null)
        {
            backgroundRect.localScale = backgroundResting.localScale * 1.06f;
        }

        // Top Panel: Off-screen top
        if (topPanelRect != null)
        {
            topPanelRect.anchoredPosition = new Vector2(topPanelResting.anchoredPosition.x, topPanelResting.anchoredPosition.y + 400f);
            topPanelRect.localScale = topPanelResting.localScale;
        }

        // Down Panel: Off-screen bottom
        if (downPanelRect != null)
        {
            downPanelRect.anchoredPosition = new Vector2(downPanelResting.anchoredPosition.x, downPanelResting.anchoredPosition.y - 400f);
            downPanelRect.localScale = downPanelResting.localScale;
        }

        // League: Scaled down & lifted
        if (leagueRect != null)
        {
            leagueRect.anchoredPosition = new Vector2(leagueResting.anchoredPosition.x, leagueResting.anchoredPosition.y + 250f);
            leagueRect.localScale = Vector3.zero;
        }

        // King: Scaled to 0 (pop up)
        if (kingRect != null)
        {
            kingRect.anchoredPosition = kingResting.anchoredPosition;
            kingRect.localScale = Vector3.zero;
        }

        // Battle Button: Scaled down and dropped
        if (battleButtonRect != null)
        {
            battleButtonRect.anchoredPosition = new Vector2(battleButtonResting.anchoredPosition.x, battleButtonResting.anchoredPosition.y - 180f);
            battleButtonRect.localScale = Vector3.zero;
        }

        // Side Buttons: Off-screen right
        if (sideButtons != null)
        {
            for (int i = 0; i < sideButtons.Length; i++)
            {
                if (sideButtons[i] == null) continue;
                sideButtons[i].anchoredPosition = new Vector2(sideButtonsResting[i].anchoredPosition.x + 350f, sideButtonsResting[i].anchoredPosition.y);
                sideButtons[i].localScale = sideButtonsResting[i].localScale;
            }
        }

        // 2. Build Choreographed DOTween Sequence
        entranceSequence = DOTween.Sequence().SetUpdate(true);

        // Fade in canvas
        if (canvasGroup != null)
        {
            entranceSequence.Join(canvasGroup.DOFade(1f, 0.35f).SetEase(Ease.Linear));
        }

        // Background gentle settle zoom
        if (backgroundRect != null)
        {
            entranceSequence.Join(backgroundRect.DOScale(backgroundResting.localScale, 0.8f).SetEase(Ease.OutCubic));
        }

        // Top Panel drop
        if (topPanelRect != null)
        {
            entranceSequence.Insert(0.04f, topPanelRect.DOAnchorPos(topPanelResting.anchoredPosition, entranceDuration)
                .SetEase(entranceEase, 1.25f));
        }

        // Bottom Navigation slide up
        if (downPanelRect != null)
        {
            entranceSequence.Insert(0.08f, downPanelRect.DOAnchorPos(downPanelResting.anchoredPosition, entranceDuration)
                .SetEase(entranceEase, 1.15f));
        }

        // League banner slam & bounce
        if (leagueRect != null)
        {
            entranceSequence.Insert(0.12f, leagueRect.DOAnchorPos(leagueResting.anchoredPosition, entranceDuration * 0.9f).SetEase(entranceEase, 1.3f));
            entranceSequence.Insert(0.12f, leagueRect.DOScale(leagueResting.localScale, entranceDuration * 0.9f).SetEase(entranceEase, 1.5f));
        }

        // King Character elastic pop-up
        if (kingRect != null)
        {
            entranceSequence.Insert(0.20f, kingRect.DOScale(kingResting.localScale, entranceDuration * 1.1f).SetEase(entranceEase, 1.4f));
        }

        // Battle Button juicy punch pop
        if (battleButtonRect != null)
        {
            entranceSequence.Insert(0.28f, battleButtonRect.DOAnchorPos(battleButtonResting.anchoredPosition, entranceDuration * 0.85f).SetEase(entranceEase, 1.5f));
            entranceSequence.Insert(0.28f, battleButtonRect.DOScale(battleButtonResting.localScale, entranceDuration * 0.85f).SetEase(entranceEase, 1.6f));
        }

        // Side buttons staggered slide-in from right
        if (sideButtons != null)
        {
            for (int i = 0; i < sideButtons.Length; i++)
            {
                if (sideButtons[i] == null) continue;
                float delay = 0.35f + (i * staggerDelay);
                entranceSequence.Insert(delay, sideButtons[i].DOAnchorPos(sideButtonsResting[i].anchoredPosition, entranceDuration * 0.8f).SetEase(entranceEase, 1.35f));
            }
        }

        // Subtle currency counter pop
        if (coinCounterText != null)
        {
            entranceSequence.Insert(0.45f, coinCounterText.transform.DOPunchScale(Vector3.one * 0.25f, 0.35f, 5, 0.5f));
        }
        if (gemsCounterText != null)
        {
            entranceSequence.Insert(0.50f, gemsCounterText.transform.DOPunchScale(Vector3.one * 0.25f, 0.35f, 5, 0.5f));
        }

        // 3. Completion Callback: Enable Interaction & Start Idle
        entranceSequence.OnComplete(() =>
        {
            if (canvasGroup != null)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            if (enableIdleAnimations)
            {
                StartIdleAnimations();
            }
        });
    }

    /// <summary>
    /// Plays a sleek exit animation that hides all main menu UI elements while keeping only the background visible.
    /// </summary>
    public void PlayExitAnimation(Action onComplete = null)
    {
        if (isExiting) return;
        isExiting = true;

        StopIdleAnimations();
        if (canvasGroup != null)
        {
            canvasGroup.DOKill();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (backgroundRect == null)
        {
            Transform bg = FindDeepChild(transform, "background") ?? FindDeepChild(transform, "Background") ?? FindDeepChild(transform, "bg");
            if (bg != null) backgroundRect = bg.GetComponent<RectTransform>();
        }

        exitSequence = DOTween.Sequence().SetUpdate(true);

        // Slide & fade top panel up
        if (topPanelRect != null)
        {
            exitSequence.Join(topPanelRect.DOAnchorPosY(topPanelResting.anchoredPosition.y + 400f, 0.36f).SetEase(Ease.InBack, 1.2f));
            AddExitFade(topPanelRect, 0.08f, 0.24f);
        }

        // Slide & fade bottom bar down
        if (downPanelRect != null)
        {
            exitSequence.Join(downPanelRect.DOAnchorPosY(downPanelResting.anchoredPosition.y - 400f, 0.36f).SetEase(Ease.InBack, 1.2f));
            AddExitFade(downPanelRect, 0.08f, 0.24f);
        }

        // Staggered slide right & fade for side buttons
        if (sideButtons != null && sideButtons.Length > 0)
        {
            for (int i = 0; i < sideButtons.Length; i++)
            {
                if (sideButtons[i] == null) continue;
                float delay = i * 0.04f;
                Vector2 restPos = i < sideButtonsResting.Count ? sideButtonsResting[i].anchoredPosition : sideButtons[i].anchoredPosition;
                exitSequence.Insert(delay, sideButtons[i].DOAnchorPosX(restPos.x + 350f, 0.32f).SetEase(Ease.InBack, 1.15f));
                AddExitFade(sideButtons[i], delay + 0.06f, 0.22f);
            }
        }
        if (sidePanelRect != null)
        {
            AddExitFade(sidePanelRect, 0.12f, 0.24f);
        }

        // Shrink, drift & fade League banner
        if (leagueRect != null)
        {
            exitSequence.Join(leagueRect.DOScale(Vector3.zero, 0.30f).SetEase(Ease.InBack, 1.25f));
            exitSequence.Join(leagueRect.DOAnchorPosY(leagueResting.anchoredPosition.y + 80f, 0.30f).SetEase(Ease.InCubic));
            AddExitFade(leagueRect, 0.06f, 0.22f);
        }

        // Shrink & fade King character
        if (kingRect != null)
        {
            exitSequence.Insert(0.04f, kingRect.DOScale(Vector3.zero, 0.34f).SetEase(Ease.InBack, 1.3f));
            AddExitFade(kingRect, 0.10f, 0.24f);
        }

        // Shrink & fade Battle button
        if (battleButtonRect != null)
        {
            exitSequence.Join(battleButtonRect.DOScale(Vector3.zero, 0.28f).SetEase(Ease.InBack, 1.4f));
            AddExitFade(battleButtonRect, 0.06f, 0.20f);
        }

        // Ensure any other non-background UI children on the root also hide cleanly
        for (int i = 0; i < transform.childCount; i++)
        {
            RectTransform child = transform.GetChild(i) as RectTransform;
            if (child == null || !child.gameObject.activeSelf) continue;
            if (IsBackgroundOrAncestor(child)) continue;
            if (child == topPanelRect || child == downPanelRect || child == leagueRect ||
                child == kingRect || child == battleButtonRect || child == sidePanelRect) continue;

            AddExitFade(child, 0.05f, 0.26f);
        }

        exitSequence.OnComplete(() =>
        {
            onComplete?.Invoke();
        });
    }

    private void AddExitFade(RectTransform target, float delay, float duration)
    {
        if (target == null) return;
        CanvasGroup cg = target.GetComponent<CanvasGroup>();
        if (cg == null) cg = target.gameObject.AddComponent<CanvasGroup>();
        if (!managedExitCanvasGroups.Contains(cg))
        {
            managedExitCanvasGroups.Add(cg);
        }

        cg.DOKill();
        cg.interactable = false;
        cg.blocksRaycasts = false;
        exitSequence.Insert(delay, cg.DOFade(0f, duration).SetEase(Ease.OutQuad));
    }

    private bool IsBackgroundOrAncestor(Transform candidate)
    {
        if (backgroundRect == null || candidate == null) return false;
        return candidate == backgroundRect || backgroundRect.IsChildOf(candidate);
    }

    #endregion

    #region Idle Living Animations

    /// <summary>
    /// Starts the organic breathing loops for the King, Battle button, League badge, and Quests.
    /// </summary>
    public void StartIdleAnimations()
    {
        StopIdleAnimations();

        // 1. King Floating Bob & Organic Breathing
        if (kingRect != null)
        {
            Vector2 basePos = kingResting.anchoredPosition;
            kingBobTween = kingRect.DOAnchorPosY(basePos.y + kingBobDistance, kingBobDuration * 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);

            Vector3 baseScale = kingResting.localScale;
            Vector3 breathScale = new Vector3(baseScale.x * (2f - kingBreathScaleMultiplier), baseScale.y * kingBreathScaleMultiplier, baseScale.z);
            kingBreathTween = kingRect.DOScale(breathScale, kingBobDuration * 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        // 2. Battle Button Subtle Pulsing Call-To-Action
        if (battleButtonRect != null)
        {
            Vector3 baseScale = battleButtonResting.localScale;
            battlePulseTween = battleButtonRect.DOScale(baseScale * battlePulseMultiplier, battlePulseDuration * 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        // 3. League Badge Subtle Hover
        if (leagueBadgeRect != null)
        {
            Vector2 badgePos = leagueBadgeResting.anchoredPosition;
            badgeFloatTween = leagueBadgeRect.DOAnchorPosY(badgePos.y + 4f, 1.6f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        // 4. Side Buttons Attention Wiggle Loop
        if (gameObject.activeInHierarchy)
        {
            sideAttentionCoroutine = StartCoroutine(SideButtonsAttentionRoutine());
        }
    }

    public void StopIdleAnimations()
    {
        if (kingBobTween != null && kingBobTween.IsActive()) kingBobTween.Kill();
        if (kingBreathTween != null && kingBreathTween.IsActive()) kingBreathTween.Kill();
        if (battlePulseTween != null && battlePulseTween.IsActive()) battlePulseTween.Kill();
        if (badgeFloatTween != null && badgeFloatTween.IsActive()) badgeFloatTween.Kill();

        if (sideAttentionCoroutine != null)
        {
            StopCoroutine(sideAttentionCoroutine);
            sideAttentionCoroutine = null;
        }

        // Restore resting scales & positions for idle components
        if (hasRecordedRestingState)
        {
            if (kingRect != null)
            {
                kingRect.anchoredPosition = kingResting.anchoredPosition;
                kingRect.localScale = kingResting.localScale;
            }
            if (battleButtonRect != null)
            {
                battleButtonRect.localScale = battleButtonResting.localScale;
            }
            if (leagueBadgeRect != null)
            {
                leagueBadgeRect.anchoredPosition = leagueBadgeResting.anchoredPosition;
            }
        }
    }

    private IEnumerator SideButtonsAttentionRoutine()
    {
        while (enableIdleAnimations && !isExiting)
        {
            yield return new WaitForSecondsRealtime(4.5f);

            if (sideButtons != null && sideButtons.Length > 0 && !isExiting)
            {
                int randomIdx = UnityEngine.Random.Range(0, sideButtons.Length);
                RectTransform targetBtn = sideButtons[randomIdx];
                if (targetBtn != null)
                {
                    targetBtn.DOPunchScale(Vector3.one * 0.12f, 0.45f, 6, 0.6f).SetUpdate(true);
                }
            }
        }
    }

    private void KillAllTweens()
    {
        StopIdleAnimations();

        if (entranceSequence != null && entranceSequence.IsActive()) entranceSequence.Kill();
        if (exitSequence != null && exitSequence.IsActive()) exitSequence.Kill();

        if (topPanelRect != null) topPanelRect.DOKill();
        if (leagueRect != null) leagueRect.DOKill();
        if (leagueBadgeRect != null) leagueBadgeRect.DOKill();
        if (kingRect != null) kingRect.DOKill();
        if (battleButtonRect != null) battleButtonRect.DOKill();
        if (sidePanelRect != null) sidePanelRect.DOKill();
        if (downPanelRect != null) downPanelRect.DOKill();
        if (backgroundRect != null) backgroundRect.DOKill();
        if (canvasGroup != null) canvasGroup.DOKill();

        if (sideButtons != null)
        {
            foreach (var b in sideButtons) if (b != null) b.DOKill();
        }

        if (downPanelTabs != null)
        {
            foreach (var t in downPanelTabs) if (t != null) t.DOKill();
        }

        for (int i = 0; i < managedExitCanvasGroups.Count; i++)
        {
            if (managedExitCanvasGroups[i] != null) managedExitCanvasGroups[i].DOKill();
        }
    }

    #endregion

    #region Juicy Interactive Button Handlers

    /// <summary>
    /// Primary BATTLE button tapped.
    /// </summary>
    public void OnBattleButtonClicked()
    {
        if (isExiting || isBattlePressed) return;
        isBattlePressed = true;

        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        PlaySound(battleClickSound != null ? battleClickSound : buttonClickSound);

        // Pause pulse & play punchy squash and stretch
        if (battlePulseTween != null && battlePulseTween.IsActive()) battlePulseTween.Pause();

        if (battleButtonRect != null)
        {
            battleButtonRect.DOKill();
            battleButtonRect.localScale = battleButtonResting.localScale;
            // Juicy squash on press, then bounce
            Sequence punchSeq = DOTween.Sequence().SetUpdate(true);
            punchSeq.Append(battleButtonRect.DOScale(new Vector3(battleButtonResting.localScale.x * 1.15f, battleButtonResting.localScale.y * 0.85f, battleButtonResting.localScale.z), 0.08f).SetEase(Ease.OutQuad));
            punchSeq.Append(battleButtonRect.DOScale(battleButtonResting.localScale, 0.25f).SetEase(Ease.OutElastic, 1.4f));
            punchSeq.OnComplete(() =>
            {
                TriggerBattleLaunch();
            });
        }
        else
        {
            TriggerBattleLaunch();
        }
    }

    private void TriggerBattleLaunch()
    {
        onBattleClicked?.Invoke();

        MainMenuManager mmManager = FindFirstObjectByType<MainMenuManager>();

        switch (battleAction)
        {
            case BattleAction.MultiplayerMatchmaking:
                PlayExitAnimation(() =>
                {
                    if (mmManager != null)
                    {
                        mmManager.OnPlayMultiplayerClicked();
                    }
                    else
                    {
                        Debug.Log("<color=#FFCC00>[MainMenuController] Battle Clicked: Multiplayer Matchmaking triggered.</color>");
                    }
                });
                break;

            case BattleAction.PlayVsAI:
                PlayExitAnimation(() =>
                {
                    if (mmManager != null)
                    {
                        mmManager.OnPlayVsAIClicked();
                    }
                    else
                    {
                        Debug.Log("<color=#FFCC00>[MainMenuController] Battle Clicked: Play vs AI triggered.</color>");
                    }
                });
                break;

            case BattleAction.CustomEventOnly:
                PlayExitAnimation();
                break;
        }
    }

    /// <summary>
    /// Interactive King character tap: King does a joyful royal jump!
    /// </summary>
    public void OnKingCharacterClicked()
    {
        if (kingRect == null || isExiting) return;

        PlaySound(kingTapSound != null ? kingTapSound : buttonClickSound);

        // Temporarily pause bobbing & breathing
        if (kingBobTween != null && kingBobTween.IsActive()) kingBobTween.Pause();
        if (kingBreathTween != null && kingBreathTween.IsActive()) kingBreathTween.Pause();

        kingRect.DOKill();
        Vector2 startPos = kingResting.anchoredPosition;
        Vector3 startScale = kingResting.localScale;

        Sequence jumpSeq = DOTween.Sequence().SetUpdate(true);
        // Squash prep
        jumpSeq.Append(kingRect.DOScale(new Vector3(startScale.x * 1.12f, startScale.y * 0.88f, startScale.z), 0.08f));
        // Hop up & stretch
        jumpSeq.Append(kingRect.DOAnchorPosY(startPos.y + 35f, 0.16f).SetEase(Ease.OutQuad));
        jumpSeq.Join(kingRect.DOScale(new Vector3(startScale.x * 0.92f, startScale.y * 1.1f, startScale.z), 0.16f).SetEase(Ease.OutQuad));
        // Land & squash
        jumpSeq.Append(kingRect.DOAnchorPosY(startPos.y, 0.16f).SetEase(Ease.InQuad));
        jumpSeq.Join(kingRect.DOScale(new Vector3(startScale.x * 1.08f, startScale.y * 0.94f, startScale.z), 0.16f).SetEase(Ease.InQuad));
        // Settle back to normal
        jumpSeq.Append(kingRect.DOScale(startScale, 0.15f).SetEase(Ease.OutBack, 1.3f));
        jumpSeq.OnComplete(() =>
        {
            if (enableIdleAnimations && !isExiting)
            {
                StartIdleAnimations();
            }
        });
    }

    /// <summary>
    /// Bottom Navigation Bar tab switching.
    /// </summary>
    public void OnBottomTabClicked(int tabIndex, RectTransform tabRect)
    {
        PlaySound(buttonClickSound);
        currentSelectedTab = tabIndex;

        // Cards Collection Page integration (Tab 1 is the cards collection icon)
        if (tabIndex == 1)
        {
            if (CardsPageController.Instance != null)
                CardsPageController.Instance.OpenPage();
        }
        else
        {
            if (CardsPageController.Instance != null)
                CardsPageController.Instance.ClosePage();
        }

        if (downPanelTabs == null) return;

        for (int i = 0; i < downPanelTabs.Length; i++)
        {
            RectTransform tab = downPanelTabs[i];
            if (tab == null) continue;

            Vector3 baseScale = i < downTabsResting.Count ? downTabsResting[i].localScale : Vector3.one;
            tab.DOKill();

            if (i == tabIndex)
            {
                // Selected tab pops up with highlight scale
                Sequence selectSeq = DOTween.Sequence().SetUpdate(true);
                selectSeq.Append(tab.DOScale(baseScale * 1.16f, 0.12f).SetEase(Ease.OutQuad));
                selectSeq.Append(tab.DOScale(baseScale * 1.08f, 0.18f).SetEase(Ease.OutBack, 1.5f));
            }
            else
            {
                // Non-selected tabs gently return to resting scale
                tab.DOScale(baseScale, 0.18f).SetEase(Ease.OutQuad).SetUpdate(true);
            }
        }
    }

    /// <summary>
    /// Settings gear button clicked.
    /// </summary>
    public void OnSettingsButtonClicked()
    {
        PlaySound(buttonClickSound);
        if (settingsButton != null)
        {
            Transform t = settingsButton.transform;
            t.DOKill();
            t.DOPunchScale(Vector3.one * 0.2f, 0.3f, 8, 0.5f).SetUpdate(true);
            t.DORotate(new Vector3(0, 0, -90f), 0.35f, RotateMode.LocalAxisAdd).SetEase(Ease.OutBack).SetUpdate(true);
        }
        Debug.Log("[MainMenuController] Settings button clicked.");
    }

    /// <summary>
    /// Player Avatar clicked.
    /// </summary>
    public void OnAvatarButtonClicked()
    {
        PlaySound(buttonClickSound);
        if (avatarButton != null)
        {
            avatarButton.transform.DOKill();
            avatarButton.transform.DOPunchScale(Vector3.one * 0.18f, 0.3f, 6, 0.5f).SetUpdate(true);
        }
        Debug.Log("[MainMenuController] Player profile / avatar clicked.");
    }

    /// <summary>
    /// Side buttons (Missions, Rewards) clicked.
    /// </summary>
    public void OnSideButtonClicked(int index, RectTransform buttonRect)
    {
        PlaySound(buttonClickSound);
        if (buttonRect != null)
        {
            Vector3 baseScale = index < sideButtonsResting.Count ? sideButtonsResting[index].localScale : Vector3.one;
            buttonRect.DOKill();
            buttonRect.localScale = baseScale;
            buttonRect.DOPunchScale(baseScale * 0.18f, 0.3f, 7, 0.6f).SetUpdate(true);
        }
        Debug.Log($"[MainMenuController] Side button [{index}] clicked.");
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null) return;
        if (audioSource != null)
        {
            KoSAudio.PlayOneShot(audioSource, clip, 0.9f, "ui");
        }
        else
        {
            KoSAudio.PlayClipAtPoint(clip, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
        }
    }

    #endregion

    #region Edit Mode & Inspector Preview Helpers

    /// <summary>
    /// Live Preview Entrance in Edit Mode.
    /// </summary>
    public void EditorPreviewEntrance()
    {
        RecordRestingTransformsIfNeeded();
        PlayEntranceAnimation();
    }

    /// <summary>
    /// Live Preview Exit in Edit Mode.
    /// </summary>
    public void EditorPreviewExit()
    {
        RecordRestingTransformsIfNeeded();
        PlayExitAnimation();
    }

    /// <summary>
    /// Live Preview Battle Click in Edit Mode.
    /// </summary>
    public void EditorPreviewBattleClick()
    {
        OnBattleButtonClicked();
    }

    /// <summary>
    /// Live Preview King Tap Jump in Edit Mode.
    /// </summary>
    public void EditorPreviewKingJump()
    {
        OnKingCharacterClicked();
    }

    #endregion
}

