using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Controls match flow, header timer countdown (3 minutes), star rating evaluation,
/// and presents the minimal end-game victory/defeat UI with interactive replay options.
/// </summary>
public class GameManager : MonoBehaviour
{
    private static GameManager instance;
    public static GameManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<GameManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("[GameManager]");
                    instance = go.AddComponent<GameManager>();
                }
            }
            return instance;
        }
    }

    public enum MatchResult { Ongoing, Victory, Defeat, Draw }

    [Header("Match Duration")]
    [Tooltip("Total round duration in seconds (default is 180s = 3 minutes).")]
    [SerializeField] private float matchDurationSeconds = 300f;

    [Header("Castles / Objectives")]
    [Tooltip("Player's main castle (Team 0). If left null, automatically finds by teamId 0.")]
    [SerializeField] private PeasantObjectiveTarget playerCastle;
    [Tooltip("Enemy's main castle (Team 1). If left null, automatically finds by teamId 1.")]
    [SerializeField] private PeasantObjectiveTarget enemyCastle;

    [Header("Header Timer UI (Optional Inspector Links)")]
    [SerializeField] private TMP_Text timerText;

    [Header("End Game Modal UI (Optional Inspector Links)")]
    [Tooltip("Reference to the warcomplete UI panel (or component).")]
    [SerializeField] private WarCompleteUIController warCompleteUI;
    [SerializeField] private GameObject resultModalPanel;
    [SerializeField] private TMP_Text resultTitleText;
    [SerializeField] private TMP_Text resultSubtitleText;
    [SerializeField] private Image[] starImages;
    [SerializeField] private Button restartButton;

    [Header("Visual Styling")]
    [SerializeField] private Color starEarnedColor = new Color(1f, 0.85f, 0.15f, 1f); // Vibrant Gold
    [SerializeField] private Color starEmptyColor = new Color(0.25f, 0.28f, 0.35f, 0.45f); // Dim translucent gray
    [SerializeField] private Sprite starSprite;
    [SerializeField] private Sprite panelFrameSprite;

    // Runtime state
    private float timeRemaining;
    private bool isMatchOver = false;
    private MatchResult matchResult = MatchResult.Ongoing;
    private int earnedStars = 0;
    private int _lastDisplayedSecond = -1;

    public float TimeRemaining => timeRemaining;
    public float MatchDuration => matchDurationSeconds;

    /// <summary>Changes the round length. Only applies before the match has started.</summary>
    public void SetMatchDuration(float seconds)
    {
        if (MatchReadyScreen.IsMatchStarted || isMatchOver) return;
        matchDurationSeconds = Mathf.Max(30f, seconds);
        timeRemaining = matchDurationSeconds;
        _lastDisplayedSecond = -1;
    }
    public bool IsMatchOver => isMatchOver;
    public MatchResult Result => matchResult;
    public int EarnedStars => earnedStars;

    public event Action<MatchResult, int> OnMatchFinished;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        timeRemaining = matchDurationSeconds;
    }

    private void Start()
    {
        FindObjectivesIfMissing();
        LoadDefaultSprites();
        EnsureHeaderTimerUI();
        EnsureResultModalUI();
    }

    private void Update()
    {
        if (isMatchOver) return;
        if (!MatchReadyScreen.IsMatchStarted) return;

        // 1. Tick match timer
        timeRemaining -= Time.deltaTime;
        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            UpdateTimerText();
            EvaluateTimeoutResult();
            return;
        }

        UpdateTimerText();

        // 2. Continuous castle health checks
        CheckCastleDestruction();
    }

    private void FindObjectivesIfMissing()
    {
        PeasantObjectiveTarget[] targets = FindObjectsByType<PeasantObjectiveTarget>(FindObjectsSortMode.None);
        foreach (var t in targets)
        {
            if (t == null) continue;
            bool isCastle = t.ObjectiveName == "Castle" || t.name.ToLower().Contains("castle");
            if (isCastle)
            {
                if (t.TeamId == 0 && playerCastle == null) playerCastle = t;
                else if (t.TeamId == 1 && enemyCastle == null) enemyCastle = t;
            }
        }
    }

    private void LoadDefaultSprites()
    {
        if (starSprite == null)
        {
            string starPath = RuntimeAssets.GUIDToAssetPath("fb8441bb5024d4255b5338dd608b2296");
            if (!string.IsNullOrEmpty(starPath))
            {
                starSprite = RuntimeAssets.LoadAssetAtPath<Sprite>(starPath);
            }
        }
        if (panelFrameSprite == null)
        {
            string framePath = RuntimeAssets.GUIDToAssetPath("1e842e770a1fa4230871d2429b6596a7");
            if (!string.IsNullOrEmpty(framePath))
            {
                panelFrameSprite = RuntimeAssets.LoadAssetAtPath<Sprite>(framePath);
            }
        }
    }

    private void UpdateTimerText()
    {
        if (timerText == null) return;

        int totalSeconds = Mathf.CeilToInt(timeRemaining);
        if (totalSeconds == _lastDisplayedSecond) return;
        _lastDisplayedSecond = totalSeconds;

        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        timerText.text = $"{minutes:00}:{seconds:00}";

        // Urgent color alert during last 30 seconds
        if (timeRemaining <= 30f)
        {
            float blink = Mathf.PingPong(Time.time * 3f, 1f);
            timerText.color = Color.Lerp(Color.white, new Color(1f, 0.25f, 0.25f), blink);
        }
        else
        {
            timerText.color = Color.white;
        }
    }

    private void CheckCastleDestruction()
    {
        int localTeam = GameModeManager.LocalTeamId;

        if (playerCastle != null && (playerCastle.IsDestroyed || playerCastle.CurrentHealth <= 0f))
        {
            EndMatch(localTeam == 0 ? MatchResult.Defeat : MatchResult.Victory);
            return;
        }

        if (enemyCastle != null && (enemyCastle.IsDestroyed || enemyCastle.CurrentHealth <= 0f))
        {
            EndMatch(localTeam == 0 ? MatchResult.Victory : MatchResult.Defeat);
            return;
        }
    }

    private void EvaluateTimeoutResult()
    {
        FindObjectivesIfMissing();

        float playerHp = playerCastle != null ? playerCastle.CurrentHealth : 0f;
        float enemyHp = enemyCastle != null ? enemyCastle.CurrentHealth : 0f;
        int localTeam = GameModeManager.LocalTeamId;

        if (playerHp > enemyHp)
        {
            EndMatch(localTeam == 0 ? MatchResult.Victory : MatchResult.Defeat);
        }
        else if (enemyHp > playerHp)
        {
            EndMatch(localTeam == 0 ? MatchResult.Defeat : MatchResult.Victory);
        }
        else
        {
            EndMatch(MatchResult.Draw);
        }
    }

    /// <summary>Stats for the profile reward formula: seconds played, enemy towers destroyed, enemy base destroyed.</summary>
    public void GetMatchStats(out int elapsedSeconds, out int towersDestroyed, out bool baseDestroyed)
    {
        elapsedSeconds = Mathf.Max(0, Mathf.RoundToInt(matchDurationSeconds - timeRemaining));
        int enemyTeam = 1 - GameModeManager.LocalTeamId;
        towersDestroyed = 0;
        foreach (var tower in FindObjectsByType<TowerCombatController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tower != null && tower.TeamId == enemyTeam && tower.IsDestroyed) towersDestroyed++;
        }
        baseDestroyed = enemyCastle != null && (enemyCastle.IsDestroyed || enemyCastle.CurrentHealth <= 0f);
    }

    public void EndMatch(MatchResult result)
    {
        if (isMatchOver) return;

        isMatchOver = true;
        matchResult = result;

        // Calculate stars
        earnedStars = CalculateStars(result);

        // Notify systems
        OnMatchFinished?.Invoke(result, earnedStars);

        // Display Modal
        StartCoroutine(ShowResultModalRoutine());
    }

    private int CalculateStars(MatchResult result)
    {
        if (result == MatchResult.Defeat || result == MatchResult.Draw)
        {
            return 0;
        }

        // Victory Stars Evaluation:
        // 1 Star: Won the match (destroyed enemy or higher HP at timer end)
        // 2 Stars: Won with player castle health >= 50%
        // 3 Stars: Won with player castle health >= 85% or speedrun within 1.5 minutes!
        int stars = 1;

        int localTeam = GameModeManager.LocalTeamId;
        PeasantObjectiveTarget myCastle = (localTeam == 0) ? playerCastle : enemyCastle;

        if (myCastle != null && myCastle.MaxHealth > 0f)
        {
            float hpRatio = myCastle.CurrentHealth / myCastle.MaxHealth;
            if (hpRatio >= 0.5f) stars = 2;
            if (hpRatio >= 0.85f || (matchDurationSeconds - timeRemaining) <= 90f) stars = 3;
        }
        else
        {
            stars = 2;
        }

        return stars;
    }

    private IEnumerator ShowResultModalRoutine()
    {
        yield return new WaitForSeconds(0.8f);

        // 1. If warcomplete UI is present, trigger its celebratory animated sequence!
        if (warCompleteUI == null) warCompleteUI = FindFirstObjectByType<WarCompleteUIController>(FindObjectsInactive.Include);
        if (warCompleteUI == null)
        {
            GameObject wc = GameObject.Find("warcomplete");
            if (wc != null)
            {
                warCompleteUI = wc.GetComponent<WarCompleteUIController>();
                if (warCompleteUI == null) warCompleteUI = wc.AddComponent<WarCompleteUIController>();
            }
        }

        if (warCompleteUI != null)
        {
            bool isVictory = matchResult == MatchResult.Victory;
            warCompleteUI.PlayWarCompleteAnimation(isVictory, earnedStars);
            yield break;
        }

        // 2. Fallback to basic result panel
        if (resultModalPanel != null)
        {
            resultModalPanel.SetActive(true);

            // Animate star fill
            if (resultTitleText != null)
            {
                if (matchResult == MatchResult.Victory)
                {
                    resultTitleText.text = "VICTORY!";
                    resultTitleText.color = new Color(1f, 0.88f, 0.2f);
                }
                else if (matchResult == MatchResult.Defeat)
                {
                    resultTitleText.text = "DEFEAT";
                    resultTitleText.color = new Color(1f, 0.3f, 0.3f);
                }
                else
                {
                    resultTitleText.text = "DRAW";
                    resultTitleText.color = new Color(0.8f, 0.8f, 0.8f);
                }
            }

            if (resultSubtitleText != null)
            {
                if (matchResult == MatchResult.Victory)
                {
                    resultSubtitleText.text = earnedStars switch
                    {
                        3 => "Flawless Conquest! 3-Star Mastery!",
                        2 => "Great Victory! Strong Defense!",
                        _ => "Victory Achieved!"
                    };
                }
                else if (matchResult == MatchResult.Defeat)
                {
                    resultSubtitleText.text = "Your castle has fallen. Prepare your strategy!";
                }
                else
                {
                    resultSubtitleText.text = "Time expired with balanced kingdoms.";
                }
            }

            // Populate stars with sequential punch animation
            if (starImages != null)
            {
                for (int i = 0; i < starImages.Length; i++)
                {
                    if (starImages[i] != null)
                    {
                        starImages[i].color = (i < earnedStars) ? starEarnedColor : starEmptyColor;
                        starImages[i].transform.localScale = Vector3.one * 0.2f;
                    }
                }

                for (int i = 0; i < starImages.Length; i++)
                {
                    if (starImages[i] == null) continue;
                    yield return new WaitForSeconds(0.18f);

                    // Quick pop animation
                    float elapsed = 0f;
                    Vector3 targetScale = Vector3.one;
                    while (elapsed < 0.2f)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        float t = elapsed / 0.2f;
                        starImages[i].transform.localScale = Vector3.Lerp(Vector3.one * 0.2f, targetScale * 1.25f, t);
                        yield return null;
                    }
                    starImages[i].transform.localScale = targetScale;
                }
            }
        }
    }

    public void RestartMatch()
    {
        Scene current = SceneManager.GetActiveScene();
        SceneManager.LoadScene(current.buildIndex);
    }

    #region Auto Hierarchy UI Creation

    private Canvas GetScreenOverlayCanvas()
    {
        Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var c in allCanvases)
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.name == "Canvas")
                return c;
        }
        foreach (var c in allCanvases)
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                return c;
        }
        return null;
    }

    private void EnsureHeaderTimerUI()
    {
        if (timerText != null) return;

        // Search in existing scene ScreenSpaceOverlay Canvas
        Canvas canvas = GetScreenOverlayCanvas();
        if (canvas == null) return;

        // Check if HeaderTimer already exists
        Transform existingHeader = canvas.transform.Find("HeaderTimer");
        if (existingHeader != null)
        {
            timerText = existingHeader.GetComponentInChildren<TMP_Text>();
            if (timerText != null) return;
        }

        // Create Header Banner Container
        GameObject headerObj = new GameObject("HeaderTimer");
        headerObj.transform.SetParent(canvas.transform, false);

        RectTransform rect = headerObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -12f);
        rect.sizeDelta = new Vector2(130f, 38f);

        Image bg = headerObj.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.12f, 0.2f, 0.88f);
        if (panelFrameSprite != null) bg.sprite = panelFrameSprite;

        // Create Timer Label Text
        GameObject labelObj = new GameObject("TimerText");
        labelObj.transform.SetParent(headerObj.transform, false);
        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;

        TMP_Text tmp = labelObj.AddComponent<TextMeshProUGUI>();
        tmp.text = "03:00";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 20f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        timerText = tmp;
    }

    private void EnsureResultModalUI()
    {
        if (resultModalPanel != null) return;

        Canvas canvas = GetScreenOverlayCanvas();
        if (canvas == null) return;

        Transform existingModal = canvas.transform.Find("GameResultModal");
        if (existingModal != null)
        {
            resultModalPanel = existingModal.gameObject;
            return;
        }

        // Modal Root (Full Screen dim overlay)
        GameObject modalObj = new GameObject("GameResultModal");
        modalObj.transform.SetParent(canvas.transform, false);

        RectTransform modalRect = modalObj.AddComponent<RectTransform>();
        modalRect.anchorMin = Vector2.zero;
        modalRect.anchorMax = Vector2.one;
        modalRect.sizeDelta = Vector2.zero;

        Image overlayImg = modalObj.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.65f); // Dim battlefield

        // Center Card Panel
        GameObject cardObj = new GameObject("CardPanel");
        cardObj.transform.SetParent(modalObj.transform, false);
        RectTransform cardRect = cardObj.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(340f, 260f);

        Image cardImg = cardObj.AddComponent<Image>();
        cardImg.color = new Color(0.12f, 0.16f, 0.24f, 0.96f);
        if (panelFrameSprite != null) cardImg.sprite = panelFrameSprite;

        // Title Text (VICTORY / DEFEAT)
        GameObject titleObj = new GameObject("ResultTitle");
        titleObj.transform.SetParent(cardObj.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -22f);
        titleRect.sizeDelta = new Vector2(0f, 40f);

        resultTitleText = titleObj.AddComponent<TextMeshProUGUI>();
        resultTitleText.text = "VICTORY!";
        resultTitleText.fontSize = 28f;
        resultTitleText.fontStyle = FontStyles.Bold;
        resultTitleText.alignment = TextAlignmentOptions.Center;
        resultTitleText.color = new Color(1f, 0.85f, 0.2f);

        // Subtitle Text
        GameObject subObj = new GameObject("ResultSubtitle");
        subObj.transform.SetParent(cardObj.transform, false);
        RectTransform subRect = subObj.AddComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0f, 1f);
        subRect.anchorMax = new Vector2(1f, 1f);
        subRect.pivot = new Vector2(0.5f, 1f);
        subRect.anchoredPosition = new Vector2(0f, -62f);
        subRect.sizeDelta = new Vector2(0f, 24f);

        resultSubtitleText = subObj.AddComponent<TextMeshProUGUI>();
        resultSubtitleText.text = "All enemy forces decimated!";
        resultSubtitleText.fontSize = 14f;
        resultSubtitleText.alignment = TextAlignmentOptions.Center;
        resultSubtitleText.color = new Color(0.85f, 0.9f, 0.95f, 0.85f);

        // Star Row Container
        GameObject starRow = new GameObject("StarRow");
        starRow.transform.SetParent(cardObj.transform, false);
        RectTransform starRowRect = starRow.AddComponent<RectTransform>();
        starRowRect.anchorMin = new Vector2(0.5f, 0.5f);
        starRowRect.anchorMax = new Vector2(0.5f, 0.5f);
        starRowRect.pivot = new Vector2(0.5f, 0.5f);
        starRowRect.anchoredPosition = new Vector2(0f, 0f);
        starRowRect.sizeDelta = new Vector2(180f, 50f);

        HorizontalLayoutGroup hlg = starRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 16f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        starImages = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            GameObject starItem = new GameObject($"Star_{i + 1}");
            starItem.transform.SetParent(starRow.transform, false);
            RectTransform sRect = starItem.AddComponent<RectTransform>();
            sRect.sizeDelta = new Vector2(44f, 44f);

            Image sImg = starItem.AddComponent<Image>();
            sImg.sprite = starSprite;
            sImg.color = starEmptyColor;
            starImages[i] = sImg;
        }

        // Play Again Button
        GameObject btnObj = new GameObject("RestartButton");
        btnObj.transform.SetParent(cardObj.transform, false);
        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0f);
        btnRect.anchorMax = new Vector2(0.5f, 0f);
        btnRect.pivot = new Vector2(0.5f, 0f);
        btnRect.anchoredPosition = new Vector2(0f, 20f);
        btnRect.sizeDelta = new Vector2(160f, 42f);

        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.18f, 0.55f, 1f, 1f); // Vibrant tactical blue
        if (panelFrameSprite != null) btnImg.sprite = panelFrameSprite;

        restartButton = btnObj.AddComponent<Button>();
        restartButton.onClick.AddListener(RestartMatch);

        GameObject btnLabel = new GameObject("Text");
        btnLabel.transform.SetParent(btnObj.transform, false);
        RectTransform bLabelRect = btnLabel.AddComponent<RectTransform>();
        bLabelRect.anchorMin = Vector2.zero;
        bLabelRect.anchorMax = Vector2.one;
        bLabelRect.sizeDelta = Vector2.zero;

        TMP_Text bTmp = btnLabel.AddComponent<TextMeshProUGUI>();
        bTmp.text = "PLAY AGAIN";
        bTmp.fontSize = 15f;
        bTmp.fontStyle = FontStyles.Bold;
        bTmp.alignment = TextAlignmentOptions.Center;
        bTmp.color = Color.white;

        resultModalPanel = modalObj;
        resultModalPanel.SetActive(false);
    }

    #endregion
}

