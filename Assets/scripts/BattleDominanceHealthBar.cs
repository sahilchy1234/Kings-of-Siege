using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Dynamic Battle Power & Health Bar for Player and AI.
/// Both player and enemy start with FULL health bars (1.0 = 100%).
/// When a side is weakened (damage taken to castle, troops lost, territory pushed back),
/// their health bar decreases accordingly.
/// Supports both dual independent health bars (Player & AI full at start)
/// and single tug-of-war bar mode.
/// Upgraded with delayed ghost catch-up bars, white hit flashes, and dynamic text counters.
/// </summary>
public class BattleDominanceHealthBar : MonoBehaviour
{
    public enum HealthBarMode
    {
        DualFullHealthBars, // Recommended: Both Player and AI bars start at 100% FULL and decrease when weakened
        SingleTugOfWarBar   // Single bar showing balance of power (starts at 50% split)
    }

    public enum DominanceMeasureMode
    {
        Comprehensive,      // Castle health + active army strength + frontline territory
        CastleHealthOnly,   // Castle / Objective HP percentage
        ArmyStrengthOnly,   // Living troops headcount & health
        TerritoryRatioOnly  // Territory control from BattleSceneSetup
    }

    [Header("Health Bar Mode")]
    [Tooltip("DualFullHealthBars: Both Player & AI bars start at 100% (full) and decrease as each side weakens.\nSingleTugOfWarBar: Single bar starts at 50% split.")]
    [SerializeField] private HealthBarMode barMode = HealthBarMode.DualFullHealthBars;

    [Header("UI Fill Image References")]
    [Tooltip("Player health bar image (Set Image Type to Filled in Inspector!). Starts at 100% full.")]
    [SerializeField] private Image playerFillImage;

    [Tooltip("AI / Enemy health bar image (Set Image Type to Filled in Inspector!). Starts at 100% full.")]
    [SerializeField] private Image enemyFillImage;

    [Header("Ghost Catch-Up Bars (Auto-Created if Null)")]
    [SerializeField] private Image playerGhostFill;
    [SerializeField] private Image enemyGhostFill;
    [SerializeField] private Color ghostColor = new Color(1f, 0.72f, 0.18f, 0.95f);
    [SerializeField] private float ghostDelay = 0.28f;
    [SerializeField] private float ghostCatchupDuration = 0.45f;

    [Header("Text Feedback")]
    [Tooltip("Whether to display percentage numbers in HUD text. Set to false to remove percentages from text.")]
    [SerializeField] private bool showPercentage = false;

    [Tooltip("Optional text to display percentages or scores (e.g. '100% vs 100%').")]
    [SerializeField] private TextMeshProUGUI scoreText;

    [Tooltip("Optional player health/score text (e.g. '100%' or '1000').")]
    [SerializeField] private TextMeshProUGUI playerScoreText;

    [Tooltip("Optional enemy health/score text (e.g. '100%' or '1000').")]
    [SerializeField] private TextMeshProUGUI enemyScoreText;

    [Header("Evaluation Settings")]
    [SerializeField] private DominanceMeasureMode measureMode = DominanceMeasureMode.Comprehensive;

    [Tooltip("How fast the fill smoothly animates toward the target value.")]
    [SerializeField, Min(0.5f)] private float smoothSpeed = 4f;

    [Header("Comprehensive Mode Weights")]
    [Tooltip("Weight given to castle/objective remaining health (0 to 1).")]
    [SerializeField, Range(0f, 1f)] private float castleWeight = 0.50f;

    [Tooltip("Weight given to living units army health & count (0 to 1).")]
    [SerializeField, Range(0f, 1f)] private float armyWeight = 0.30f;

    [Tooltip("Weight given to physical territorial vanguard push (0 to 1).")]
    [SerializeField, Range(0f, 1f)] private float territoryWeight = 0.20f;

    [Header("Colors (Optional Dynamic Tints)")]
    [SerializeField] private bool useDynamicGradient = false;
    [SerializeField] private Color fullStrengthColor = new Color(0.2f, 0.75f, 1f, 1f);
    [SerializeField] private Color criticalLowColor = new Color(1f, 0.25f, 0.25f, 1f);

    // Cached references
    private PeasantObjectiveTarget playerCastle;
    private PeasantObjectiveTarget enemyCastle;

    private RectTransform playerContainer;
    private RectTransform enemyContainer;
    private Color playerBaseColor = Color.white;
    private Color enemyBaseColor = Color.white;

    // Baseline army strength recorded when units spawn (starts at 0 so army is 100% full before units engage)
    private float baselinePlayerArmy = 0f;
    private float baselineEnemyArmy = 0f;

    // Smoothed fill values (0.0 to 1.0) - Initialized to 1.0 (FULL)
    private float targetPlayerHealth = 1.0f;
    private float targetEnemyHealth = 1.0f;
    private float displayedPlayerFill = 1.0f;
    private float displayedEnemyFill = 1.0f;

    private float lastKnownPlayerHealth = 1.0f;
    private float lastKnownEnemyHealth = 1.0f;

    // Startup grace period to guarantee both bars remain rock-solid 100% full while the scene initializes
    private float startupGraceTimer = 0.5f;

    // Tweens
    private Tween playerGhostTween;
    private Tween enemyGhostTween;
    private Tween playerFlashTween;
    private Tween enemyFlashTween;

    public float PlayerHealthRatio => displayedPlayerFill;
    public float EnemyHealthRatio => displayedEnemyFill;
    public bool ShowPercentage
    {
        get => showPercentage;
        set
        {
            showPercentage = value;
            UpdateUI();
        }
    }

    private void Awake()
    {
        AutoWireReferences();

        // Ensure both bars are 100% full right from the instant of awakening
        targetPlayerHealth = 1.0f;
        targetEnemyHealth = 1.0f;
        displayedPlayerFill = 1.0f;
        displayedEnemyFill = 1.0f;
        lastKnownPlayerHealth = 1.0f;
        lastKnownEnemyHealth = 1.0f;

        if (playerFillImage != null) playerFillImage.fillAmount = 1.0f;
        if (enemyFillImage != null) enemyFillImage.fillAmount = 1.0f;
    }

    private void Start()
    {
        AutoWireReferences();
        FindObjectives();
        ConfigureFillImage(playerFillImage);
        ConfigureFillImage(enemyFillImage);
        ConfigureGhostFills();

        // Ensure damage directly impacts the health bars
        if (measureMode == DominanceMeasureMode.TerritoryRatioOnly)
        {
            measureMode = DominanceMeasureMode.Comprehensive;
        }

        // Both bars start 100% FULL at the beginning of the game
        targetPlayerHealth = 1.0f;
        targetEnemyHealth = 1.0f;
        displayedPlayerFill = 1.0f;
        displayedEnemyFill = 1.0f;

        lastKnownPlayerHealth = targetPlayerHealth;
        lastKnownEnemyHealth = targetEnemyHealth;

        if (playerFillImage != null)
        {
            playerFillImage.fillAmount = 1.0f;
            playerBaseColor = playerFillImage.color;
        }
        if (enemyFillImage != null)
        {
            enemyFillImage.fillAmount = 1.0f;
            enemyBaseColor = enemyFillImage.color;
        }

        if (playerGhostFill != null) playerGhostFill.fillAmount = 1.0f;
        if (enemyGhostFill != null) enemyGhostFill.fillAmount = 1.0f;

        // Ensure scale is exactly 1 (no scaling effect on slider or name)
        if (playerContainer != null) playerContainer.localScale = Vector3.one;
        if (enemyContainer != null) enemyContainer.localScale = Vector3.one;
        if (playerScoreText != null) playerScoreText.transform.localScale = Vector3.one;
        if (enemyScoreText != null) enemyScoreText.transform.localScale = Vector3.one;

        CleanInitialTextLabels();
        UpdateUI();
    }

    public void AutoWireReferences()
    {
        // 1. Auto-wire Player Fill Image
        if (playerFillImage == null)
        {
            var pGo = GameObject.Find("Canvas/BattleDominanceBar/player/healthbar")
                   ?? GameObject.Find("Canvas/player/healthbar")
                   ?? GameObject.Find("player/healthbar");
            if (pGo != null) playerFillImage = pGo.GetComponent<Image>();
        }

        // 2. Auto-wire Enemy Fill Image
        if (enemyFillImage == null)
        {
            var eGo = GameObject.Find("Canvas/BattleDominanceBar/healthOpponent/healthbar")
                   ?? GameObject.Find("Canvas/healthOpponent/healthbar")
                   ?? GameObject.Find("healthOpponent/healthbar");
            if (eGo != null) enemyFillImage = eGo.GetComponent<Image>();
        }

        // 3. Containers for punch scale
        if (playerFillImage != null && playerContainer == null)
        {
            playerContainer = playerFillImage.transform.parent as RectTransform;
            playerBaseColor = playerFillImage.color;
        }
        if (enemyFillImage != null && enemyContainer == null)
        {
            enemyContainer = enemyFillImage.transform.parent as RectTransform;
            enemyBaseColor = enemyFillImage.color;
        }

        // 4. Auto-wire Text
        if (playerScoreText == null && playerContainer != null)
        {
            var txt = playerContainer.Find("HPPercent") ?? playerContainer.Find("opponentname") ?? playerContainer.Find("playername");
            if (txt != null) playerScoreText = txt.GetComponent<TextMeshProUGUI>();
        }
        if (enemyScoreText == null && enemyContainer != null)
        {
            var txt = enemyContainer.Find("HPPercent") ?? enemyContainer.Find("opponentname");
            if (txt != null) enemyScoreText = txt.GetComponent<TextMeshProUGUI>();
        }
    }

    private void ConfigureGhostFills()
    {
        if (playerGhostFill == null && playerFillImage != null)
        {
            Transform existingGhost = playerFillImage.transform.parent.Find("GhostFill");
            if (existingGhost != null)
            {
                playerGhostFill = existingGhost.GetComponent<Image>();
            }
            else
            {
                GameObject ghostGo = Instantiate(playerFillImage.gameObject, playerFillImage.transform.parent);
                ghostGo.name = "GhostFill";
                ghostGo.transform.SetSiblingIndex(playerFillImage.transform.GetSiblingIndex());
                playerGhostFill = ghostGo.GetComponent<Image>();
            }

            if (playerGhostFill != null)
            {
                playerGhostFill.color = ghostColor;
                if (playerFillImage != null)
                {
                    playerGhostFill.fillOrigin = playerFillImage.fillOrigin;
                    playerGhostFill.fillClockwise = playerFillImage.fillClockwise;
                }
                ConfigureFillImage(playerGhostFill);
            }
        }

        if (enemyGhostFill == null && enemyFillImage != null)
        {
            Transform existingGhost = enemyFillImage.transform.parent.Find("GhostFill");
            if (existingGhost != null)
            {
                enemyGhostFill = existingGhost.GetComponent<Image>();
            }
            else
            {
                GameObject ghostGo = Instantiate(enemyFillImage.gameObject, enemyFillImage.transform.parent);
                ghostGo.name = "GhostFill";
                ghostGo.transform.SetSiblingIndex(enemyFillImage.transform.GetSiblingIndex());
                enemyGhostFill = ghostGo.GetComponent<Image>();
            }

            if (enemyGhostFill != null)
            {
                enemyGhostFill.color = ghostColor;
                if (enemyFillImage != null)
                {
                    enemyGhostFill.fillOrigin = enemyFillImage.fillOrigin;
                    enemyGhostFill.fillClockwise = enemyFillImage.fillClockwise;
                }
                ConfigureFillImage(enemyGhostFill);
            }
        }
    }

    private void ConfigureFillImage(Image img)
    {
        if (img == null) return;

        // Ensure sprite is present so Image.Type.Filled works in Unity UI
        if (img.sprite == null)
        {
            img.sprite = JuicyUnitHealthBar.GetDefaultWhiteSprite();
        }

        // Ensure Image type is Filled so fillAmount property works
        if (img.type != Image.Type.Filled)
        {
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
        }
    }

    private void Update()
    {
        // Startup grace period: ensure both bars stay completely full during scene initialization
        if (startupGraceTimer > 0f)
        {
            startupGraceTimer -= Time.deltaTime;
            targetPlayerHealth = 1.0f;
            targetEnemyHealth = 1.0f;
            displayedPlayerFill = 1.0f;
            displayedEnemyFill = 1.0f;
            lastKnownPlayerHealth = 1.0f;
            lastKnownEnemyHealth = 1.0f;

            if (playerFillImage != null) playerFillImage.fillAmount = 1.0f;
            if (enemyFillImage != null) enemyFillImage.fillAmount = 1.0f;
            if (playerGhostFill != null) playerGhostFill.fillAmount = 1.0f;
            if (enemyGhostFill != null) enemyGhostFill.fillAmount = 1.0f;

            UpdateUI();
            return;
        }

        if (barMode == HealthBarMode.DualFullHealthBars)
        {
            EvaluateDualFullHealth();
        }
        else
        {
            EvaluateTugOfWar();
        }

        // Check for damage events (significant drop in target health)
        CheckHealthChanges();

        // Smoothly interpolate fill amounts
        displayedPlayerFill = Mathf.Lerp(displayedPlayerFill, targetPlayerHealth, Time.deltaTime * smoothSpeed);
        displayedEnemyFill = Mathf.Lerp(displayedEnemyFill, targetEnemyHealth, Time.deltaTime * smoothSpeed);

        UpdateUI();
    }

    private void CheckHealthChanges()
    {
        // Player damage event
        if (targetPlayerHealth < lastKnownPlayerHealth - 0.005f)
        {
            PlayPlayerDamageJuice(targetPlayerHealth);
        }
        else if (targetPlayerHealth > lastKnownPlayerHealth + 0.01f)
        {
            // Player heal event
            if (playerGhostFill != null) playerGhostFill.fillAmount = targetPlayerHealth;
        }
        lastKnownPlayerHealth = targetPlayerHealth;

        // Enemy damage event
        if (targetEnemyHealth < lastKnownEnemyHealth - 0.005f)
        {
            PlayEnemyDamageJuice(targetEnemyHealth);
        }
        else if (targetEnemyHealth > lastKnownEnemyHealth + 0.01f)
        {
            // Enemy heal event
            if (enemyGhostFill != null) enemyGhostFill.fillAmount = targetEnemyHealth;
        }
        lastKnownEnemyHealth = targetEnemyHealth;
    }

    private void PlayPlayerDamageJuice(float newTarget)
    {
        // 1. White Flash on Player Fill
        if (playerFillImage != null)
        {
            playerFlashTween?.Kill();
            playerFillImage.color = Color.white;
            playerFlashTween = playerFillImage.DOColor(playerBaseColor, 0.16f).SetEase(Ease.OutQuad);
        }

        // 2. Delayed Ghost Catch-Up
        playerGhostTween?.Kill();
        playerGhostTween = DOVirtual.DelayedCall(ghostDelay, () =>
        {
            if (playerGhostFill != null)
            {
                playerGhostFill.DOFillAmount(newTarget, ghostCatchupDuration).SetEase(Ease.OutCubic);
            }
        });
    }

    private void PlayEnemyDamageJuice(float newTarget)
    {
        // 1. White Flash on Enemy Fill
        if (enemyFillImage != null)
        {
            enemyFlashTween?.Kill();
            enemyFillImage.color = Color.white;
            enemyFlashTween = enemyFillImage.DOColor(enemyBaseColor, 0.16f).SetEase(Ease.OutQuad);
        }

        // 2. Delayed Ghost Catch-Up
        enemyGhostTween?.Kill();
        enemyGhostTween = DOVirtual.DelayedCall(ghostDelay, () =>
        {
            if (enemyGhostFill != null)
            {
                enemyGhostFill.DOFillAmount(newTarget, ghostCatchupDuration).SetEase(Ease.OutCubic);
            }
        });
    }

    private void FindObjectives()
    {
        PeasantObjectiveTarget[] objectives = FindObjectsByType<PeasantObjectiveTarget>(FindObjectsSortMode.None);
        foreach (var obj in objectives)
        {
            if (obj == null) continue;
            bool isCastle = obj.ObjectiveName == "Castle" || obj.name.ToLower().Contains("castle");
            if (isCastle)
            {
                if (obj.TeamId == 0 && playerCastle == null) playerCastle = obj;
                else if (obj.TeamId == 1 && enemyCastle == null) enemyCastle = obj;
            }
        }

        // Fallback: If no object explicitly named "castle" was found, pair the primary team objectives
        if (playerCastle == null || enemyCastle == null)
        {
            foreach (var obj in objectives)
            {
                if (obj == null) continue;
                if (obj.TeamId == 0 && playerCastle == null) playerCastle = obj;
                else if (obj.TeamId == 1 && enemyCastle == null) enemyCastle = obj;
            }
        }
    }

    /// <summary>
    /// Dual Full Health Bars: Both start at 100% full.
    /// As each team suffers damage (castle takes hits, units die, or territory lost),
    /// their respective bar decreases from 1.0 down toward 0.0.
    /// </summary>
    private void EvaluateDualFullHealth()
    {
        if (playerCastle == null || enemyCastle == null) FindObjectives();

        // 1. Castle health fractions (1.0 = full castle health)
        float pCastleHP = 1.0f;
        if (playerCastle != null)
        {
            float max = Mathf.Max(1f, playerCastle.MaxHealth);
            pCastleHP = playerCastle.IsDestroyed ? 0f : Mathf.Clamp01(playerCastle.CurrentHealth / max);
        }

        float eCastleHP = 1.0f;
        if (enemyCastle != null)
        {
            float max = Mathf.Max(1f, enemyCastle.MaxHealth);
            eCastleHP = enemyCastle.IsDestroyed ? 0f : Mathf.Clamp01(enemyCastle.CurrentHealth / max);
        }

        // 2. Army strength fractions (starts at 1.0 full until units spawn and die)
        CalculateArmyStrengths(out float currentPArmy, out float currentEArmy);
        if (currentPArmy > baselinePlayerArmy) baselinePlayerArmy = currentPArmy;
        if (currentEArmy > baselineEnemyArmy) baselineEnemyArmy = currentEArmy;

        float pArmyFraction = baselinePlayerArmy > 0.01f ? Mathf.Clamp01(currentPArmy / baselinePlayerArmy) : 1.0f;
        float eArmyFraction = baselineEnemyArmy > 0.01f ? Mathf.Clamp01(currentEArmy / baselineEnemyArmy) : 1.0f;

        // 3. Territory fraction (0.5 = neutral frontline, 1.0 = full territory intact)
        float pTerritory = 1.0f;
        float eTerritory = 1.0f;
        if (BattleSceneSetup.Instance != null)
        {
            float ratio = Mathf.Clamp01(BattleSceneSetup.Instance.DisplayedRatio);
            pTerritory = ratio >= 0.5f ? 1.0f : Mathf.Clamp01(ratio / 0.5f);
            eTerritory = ratio <= 0.5f ? 1.0f : Mathf.Clamp01((1f - ratio) / 0.5f);
        }

        switch (measureMode)
        {
            case DominanceMeasureMode.CastleHealthOnly:
                targetPlayerHealth = pCastleHP;
                targetEnemyHealth = eCastleHP;
                break;

            case DominanceMeasureMode.ArmyStrengthOnly:
                targetPlayerHealth = pArmyFraction;
                targetEnemyHealth = eArmyFraction;
                break;

            case DominanceMeasureMode.TerritoryRatioOnly:
                targetPlayerHealth = pTerritory;
                targetEnemyHealth = eTerritory;
                break;

            case DominanceMeasureMode.Comprehensive:
            default:
                float totalW = castleWeight + armyWeight + territoryWeight;
                if (totalW <= 0.001f) totalW = 1f;

                targetPlayerHealth = (pCastleHP * castleWeight + pArmyFraction * armyWeight + pTerritory * territoryWeight) / totalW;
                targetEnemyHealth = (eCastleHP * castleWeight + eArmyFraction * armyWeight + eTerritory * territoryWeight) / totalW;
                break;
        }

        // Lock dead castles to 0
        if (playerCastle != null && playerCastle.IsDestroyed) targetPlayerHealth = 0f;
        if (enemyCastle != null && enemyCastle.IsDestroyed) targetEnemyHealth = 0f;

        targetPlayerHealth = Mathf.Clamp01(targetPlayerHealth);
        targetEnemyHealth = Mathf.Clamp01(targetEnemyHealth);
    }

    /// <summary>
    /// Single Tug of War mode where bar is split between both sides.
    /// </summary>
    private void EvaluateTugOfWar()
    {
        if (playerCastle == null || enemyCastle == null) FindObjectives();

        float pCastle = 1.0f;
        if (playerCastle != null)
            pCastle = playerCastle.IsDestroyed ? 0f : Mathf.Clamp01(playerCastle.CurrentHealth / Mathf.Max(1f, playerCastle.MaxHealth));

        float eCastle = 1.0f;
        if (enemyCastle != null)
            eCastle = enemyCastle.IsDestroyed ? 0f : Mathf.Clamp01(enemyCastle.CurrentHealth / Mathf.Max(1f, enemyCastle.MaxHealth));

        float castleRatio = (pCastle + eCastle > 0.001f) ? (pCastle / (pCastle + eCastle)) : 0.5f;

        CalculateArmyStrengths(out float pArmy, out float eArmy);
        float armyRatio = (pArmy + eArmy > 0.001f) ? (pArmy / (pArmy + eArmy)) : 0.5f;

        float territoryRatio = BattleSceneSetup.Instance != null ? Mathf.Clamp01(BattleSceneSetup.Instance.DisplayedRatio) : 0.5f;

        float totalW = castleWeight + armyWeight + territoryWeight;
        if (totalW <= 0.001f) totalW = 1f;

        float blended = (castleRatio * castleWeight + armyRatio * armyWeight + territoryRatio * territoryWeight) / totalW;

        if (playerCastle != null && playerCastle.IsDestroyed) blended = 0f;
        else if (enemyCastle != null && enemyCastle.IsDestroyed) blended = 1f;

        targetPlayerHealth = Mathf.Clamp01(blended);
        targetEnemyHealth = 1f - targetPlayerHealth;
    }

    private void CalculateArmyStrengths(out float playerArmyStrength, out float enemyArmyStrength)
    {
        playerArmyStrength = 0f;
        enemyArmyStrength = 0f;

        IReadOnlyList<PeasantUnitController> units = null;
        if (BattleAIManager.Instance != null)
            units = BattleAIManager.Instance.GetAllLivingUnits();

        if (units != null)
        {
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u == null || u.IsDead) continue;

                float power = u.CurrentHealth + 30f;
                if (u.TeamId == 0) playerArmyStrength += power;
                else if (u.TeamId == 1) enemyArmyStrength += power;
            }
        }
        else
        {
            PeasantUnitController[] all = FindObjectsByType<PeasantUnitController>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                var u = all[i];
                if (u == null || u.IsDead) continue;

                float power = u.CurrentHealth + 30f;
                if (u.TeamId == 0) playerArmyStrength += power;
                else if (u.TeamId == 1) enemyArmyStrength += power;
            }
        }
    }

    private void CleanInitialTextLabels()
    {
        if (!showPercentage)
        {
            if (scoreText != null) scoreText.text = "";
            if (playerScoreText != null) playerScoreText.text = GetCleanLabel(playerScoreText, "PLAYER");
            if (enemyScoreText != null) enemyScoreText.text = GetCleanLabel(enemyScoreText, "AI");
        }
    }

    private string GetCleanLabel(TextMeshProUGUI tmp, string fallback)
    {
        if (tmp == null) return fallback;
        if (tmp.name == "HPPercent") return "";
        string text = tmp.text ?? "";
        string cleaned = System.Text.RegularExpressions.Regex.Replace(text, @"\s*\d+%\s*", "").Trim();
        return string.IsNullOrEmpty(cleaned) ? fallback : cleaned;
    }

    private void UpdateUI()
    {
        // 1. Update Player Fill Image
        if (playerFillImage != null)
        {
            playerFillImage.fillAmount = displayedPlayerFill;

            if (useDynamicGradient)
            {
                playerFillImage.color = Color.Lerp(criticalLowColor, fullStrengthColor, displayedPlayerFill);
            }
        }

        // 2. Update Enemy Fill Image
        if (enemyFillImage != null)
        {
            enemyFillImage.fillAmount = displayedEnemyFill;

            if (useDynamicGradient)
            {
                enemyFillImage.color = Color.Lerp(criticalLowColor, fullStrengthColor, displayedEnemyFill);
            }
        }

        // 3. Text feedback
        if (showPercentage)
        {
            int pPercent = Mathf.RoundToInt(displayedPlayerFill * 100f);
            int ePercent = Mathf.RoundToInt(displayedEnemyFill * 100f);

            if (scoreText != null)
            {
                scoreText.text = $"{pPercent}% - {ePercent}%";
            }

            if (playerScoreText != null)
            {
                if (playerScoreText.name == "HPPercent")
                    playerScoreText.text = $"{pPercent}%";
                else
                    playerScoreText.text = $"{GetCleanLabel(playerScoreText, "PLAYER")} {pPercent}%";
            }

            if (enemyScoreText != null)
            {
                if (enemyScoreText.name == "HPPercent")
                    enemyScoreText.text = $"{ePercent}%";
                else
                    enemyScoreText.text = $"{GetCleanLabel(enemyScoreText, "AI")} {ePercent}%";
            }
        }
        else
        {
            // Percentage removed from text as requested
            if (scoreText != null && scoreText.text.Contains("%"))
            {
                scoreText.text = "";
            }

            if (playerScoreText != null && playerScoreText.text.Contains("%"))
            {
                playerScoreText.text = GetCleanLabel(playerScoreText, "PLAYER");
            }

            if (enemyScoreText != null && enemyScoreText.text.Contains("%"))
            {
                enemyScoreText.text = GetCleanLabel(enemyScoreText, "AI");
            }
        }
    }

    /// <summary>
    /// Manually adjust or boost dominance score from external game events (e.g. powerup bonus, kills).
    /// </summary>
    public void AddDominanceOffset(float delta)
    {
        targetPlayerHealth = Mathf.Clamp01(targetPlayerHealth + delta);
        targetEnemyHealth = Mathf.Clamp01(targetEnemyHealth - delta);
    }

    private void OnDestroy()
    {
        playerGhostTween?.Kill();
        enemyGhostTween?.Kill();
        playerFlashTween?.Kill();
        enemyFlashTween?.Kill();
    }
}
