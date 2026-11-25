using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Ultra-Juicy, AAA World-Space Health Bar for Player Soldiers, Siege Machines, and Fortresses.
/// Features:
/// - Guaranteed Decreasing Fill: Dual-layer horizontal animation (Unity UI Image.fillAmount + RectTransform.anchorMax)
///   powered by auto-generated crisp white sprite texture so the bar visibly drops on every hit.
/// - Snappy main fill drop (0.08s) with delayed "Ghost" amber damage catch-up glide.
/// - Impact punch scale (heavy mechanical bounce on machines, snappy bounce on soldiers).
/// - White hit flash on fill.
/// - Floating combat text damage numbers ("-25", "-50") with arc and fade.
/// - Specialized Machine & Soldier scaling: Machines (Catapults, Rams, Ballistas) receive heavier, beefier bars with durability numbers.
/// - Critical low-health (<25%) rhythmic heartbeat pulse.
/// - Billboard alignment: Always flat & upright facing the main camera, free of bone animation distortion.
/// - Juicy collapse animation on unit destruction.
/// </summary>
public class JuicyUnitHealthBar : MonoBehaviour
{
    public enum BarType
    {
        Soldier,    // Human units (Swordsman, Archer, Spearman, Peasant, King, Knight)
        Machine,    // Siege machines (Catapult, Ram, Ballista, Cart)
        Objective   // Castles / Fortress structures
    }

    [Header("Bar Configuration")]
    [SerializeField] private BarType barType = BarType.Soldier;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.15f, 0f);
    [SerializeField] private Vector2 barSize = new Vector2(110f, 14f);
    [SerializeField] private float targetWorldWidth = 1.25f;
    [SerializeField] private bool showHpText = false;

    [Header("Theming Colors")]
    [SerializeField] private Color friendlyColor = new Color(0.04f, 0.52f, 0.89f, 1f);   // Royal Blue (#0984E3)
    [SerializeField] private Color enemyColor = new Color(0.88f, 0.22f, 0.22f, 1f);      // Fiery Crimson (#E03838)
    [SerializeField] private Color neutralColor = new Color(0.95f, 0.77f, 0.25f, 1f);    // Amber Gold
    [SerializeField] private Color ghostCatchupColor = new Color(1f, 0.65f, 0.2f, 0.95f); // Amber Orange
    [SerializeField] private Color backgroundColor = new Color(0.10f, 0.12f, 0.15f, 0.92f);
    [SerializeField] private Color borderColor = new Color(0.03f, 0.04f, 0.05f, 0.96f);

    [Header("Juice & Animation Settings")]
    [SerializeField] private float ghostDelay = 0.25f;
    [SerializeField] private float ghostCatchupDuration = 0.45f;
    [SerializeField] private float punchScaleMagnitude = 0.18f;
    [SerializeField] private bool enableDamageNumbers = true;
    [SerializeField] private TMP_FontAsset damageNumberFont;

    [Header("UI References (Auto-Generated if Null)")]
    [SerializeField] private Transform uiRoot;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Image borderImage;
    [SerializeField] private Image bgImage;
    [SerializeField] private Image ghostFillImage;
    [SerializeField] private Image mainFillImage;
    [SerializeField] private RectTransform ghostFillRect;
    [SerializeField] private RectTransform mainFillRect;
    [SerializeField] private TextMeshProUGUI hpText;

    private Transform targetUnitTransform;
    private Transform cachedCameraTransform;
    private int teamId = 0;
    private PeasantUnitController _cachedPUC;
    private PeasantObjectiveTarget _cachedPOT;
    private float maxHp = 100f;
    private float currentHp = 100f;
    private bool isDestroyed = false;
    private bool isCriticalLow = false;
    private int lastDamageFrame = -1;

    private Tween ghostTween;
    private Tween punchTween;
    private Tween flashTween;
    private Tween heartbeatTween;

    private Vector3 baseLocalScale = Vector3.one;

    private static Sprite s_DefaultWhiteSprite;

    public BarType Type => barType;
    public bool IsMachine => barType == BarType.Machine;

    public TMP_FontAsset DamageNumberFont
    {
        get => damageNumberFont;
        set => damageNumberFont = value;
    }

    private static TMP_FontAsset s_SharedDamageFont;
    private static int s_DamageSprayCounter = 0;

    public static TMP_FontAsset GetTiltWarpOutlineFont()
    {
        if (s_SharedDamageFont != null) return s_SharedDamageFont;

        // 1. Try Resources.Load
        s_SharedDamageFont = Resources.Load<TMP_FontAsset>("Fonts/TMP_TiltWarp_Outline");
        if (s_SharedDamageFont == null)
            s_SharedDamageFont = Resources.Load<TMP_FontAsset>("TMP_TiltWarp_Outline");

        // 2. Fallback to AssetDatabase in Editor
        if (s_SharedDamageFont == null)
        {
            s_SharedDamageFont = RuntimeAssets.LoadAssetAtPath<TMP_FontAsset>("Assets/UIPACK/ResourcesData/Fonts/TMP_TiltWarp_Outline.asset");
        }

        return s_SharedDamageFont;
    }

    public static Sprite GetDefaultWhiteSprite()
    {
        if (s_DefaultWhiteSprite == null)
        {
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Color[] colors = new Color[] { Color.white, Color.white, Color.white, Color.white };
            tex.SetPixels(colors);
            tex.Apply();
            tex.name = "JuicyHealthBar_WhiteTex";
            s_DefaultWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
            s_DefaultWhiteSprite.name = "JuicyHealthBar_WhiteSprite";
        }
        return s_DefaultWhiteSprite;
    }

    public void Setup(Transform unitRoot, int owningTeam, float maxHealth, bool isMachineUnit)
    {
        this.targetUnitTransform = unitRoot;
        _cachedPUC = unitRoot != null ? unitRoot.GetComponent<PeasantUnitController>() : null;
        _cachedPOT = null;
        this.teamId = owningTeam;
        this.maxHp = Mathf.Max(1f, maxHealth);
        this.currentHp = this.maxHp;
        this.isDestroyed = false;

        if (isMachineUnit)
        {
            ConfigureAsMachine();
        }
        else
        {
            ConfigureAsSoldier();
        }

        EnsureUIComponents();
        ApplyTeamTheming();
        UpdateFill(1f, instant: true);
        ApplyNormalizedScale();
    }

    public void SetupObjective(Transform objectiveRoot, int owningTeam, float maxHealth)
    {
        this.targetUnitTransform = objectiveRoot;
        _cachedPUC = null;
        _cachedPOT = objectiveRoot != null ? objectiveRoot.GetComponent<PeasantObjectiveTarget>() : null;
        this.teamId = owningTeam;
        this.maxHp = Mathf.Max(1f, maxHealth);
        this.currentHp = this.maxHp;
        this.isDestroyed = false;

        ConfigureAsObjective();
        EnsureUIComponents();
        ApplyTeamTheming();
        UpdateFill(1f, instant: true);
        ApplyNormalizedScale();
    }

    public void ConfigureAsSoldier()
    {
        barType = BarType.Soldier;
        worldOffset = new Vector3(0f, 2.15f, 0f);
        barSize = new Vector2(110f, 14f);
        targetWorldWidth = 1.25f;
        punchScaleMagnitude = 0.18f;
        showHpText = false;
    }

    public void ConfigureAsMachine()
    {
        barType = BarType.Machine;
        worldOffset = new Vector3(0f, 3.2f, 0f);
        barSize = new Vector2(170f, 22f);
        targetWorldWidth = 2.4f;
        punchScaleMagnitude = 0.24f; // Meaty heavy mechanical punch
        showHpText = true;           // Machines display durability numbers
    }

    public void ConfigureAsObjective()
    {
        barType = BarType.Objective;
        worldOffset = new Vector3(0f, 8.5f, 0f);
        barSize = new Vector2(260f, 30f);
        targetWorldWidth = 5.0f;
        punchScaleMagnitude = 0.22f;
        showHpText = true;
    }

    public void SetWorldOffset(Vector3 offset) => worldOffset = offset;

    public void ApplyNormalizedScale()
    {
        if (uiRoot == null) return;

        float ratio = targetWorldWidth / Mathf.Max(10f, barSize.x);
        Transform p = uiRoot.parent;
        if (p != null)
        {
            Vector3 lossy = p.lossyScale;
            float px = Mathf.Abs(lossy.x) > 0.001f ? Mathf.Abs(lossy.x) : 1f;
            float py = Mathf.Abs(lossy.y) > 0.001f ? Mathf.Abs(lossy.y) : 1f;
            baseLocalScale = new Vector3(ratio / px, ratio / py, 1f);
        }
        else
        {
            baseLocalScale = new Vector3(ratio, ratio, 1f);
        }

        uiRoot.localScale = baseLocalScale;
    }

    private void Awake()
    {
        // If this health bar is attached to a stationed archer under a tower, self-destruct immediately.
        // Tower health is already handled exclusively by TowerHealthBar.
        TowerCombatController parentTower = GetComponentInParent<TowerCombatController>();
        if (parentTower != null)
        {
            Destroy(gameObject);
            return;
        }

        FindTargetUnit();
        HideForeign3DCubes();
        EnsureUIComponents();
        ApplyNormalizedScale();

        if (Camera.main != null)
        {
            cachedCameraTransform = Camera.main.transform;
        }
    }

    private void Start()
    {
        TowerCombatController parentTower = GetComponentInParent<TowerCombatController>();
        if (parentTower != null)
        {
            Destroy(gameObject);
            return;
        }

        FindTargetUnit();
        HideForeign3DCubes();
        EnsureUIComponents();
        ApplyNormalizedScale();

        if (cachedCameraTransform == null && Camera.main != null)
        {
            cachedCameraTransform = Camera.main.transform;
        }

        ApplyTeamTheming();
    }

    private void FindTargetUnit()
    {
        if (targetUnitTransform != null) return;

        PeasantUnitController puc = GetComponentInParent<PeasantUnitController>();
        if (puc != null)
        {
            targetUnitTransform = puc.transform;
            teamId = puc.TeamId;
            if (puc.IsMachine) ConfigureAsMachine();
            else ConfigureAsSoldier();
            return;
        }

        PeasantObjectiveTarget pot = GetComponentInParent<PeasantObjectiveTarget>();
        if (pot != null)
        {
            targetUnitTransform = pot.transform;
            teamId = pot.TeamId;
            ConfigureAsObjective();
            return;
        }

        if (transform.parent != null)
        {
            targetUnitTransform = transform.root;
        }
    }

    /// <summary>
    /// Hides raw legacy 3D cubes (heathBarmain) if present so they don't clip through the new UI bar.
    /// </summary>
    public void HideForeign3DCubes()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.name == "UICanvas") continue;

            if (child.name.StartsWith("heathBar", System.StringComparison.OrdinalIgnoreCase) ||
                child.name.StartsWith("healthbar", System.StringComparison.OrdinalIgnoreCase))
            {
                var mr = child.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = false;
                var col = child.GetComponent<Collider>();
                if (col != null) col.enabled = false;
            }
        }

        var rootMr = GetComponent<MeshRenderer>();
        if (rootMr != null) rootMr.enabled = false;
        var rootCol = GetComponent<Collider>();
        if (rootCol != null) rootCol.enabled = false;
    }

    private void LateUpdate()
    {
        if (isDestroyed || uiRoot == null) return;

        // Dynamic team sync in case unit team changed dynamically after initialization
        if (targetUnitTransform != null)
        {
            if (_cachedPUC != null && _cachedPUC.TeamId != teamId)
            {
                ApplyTeamTheming(_cachedPUC.TeamId);
            }
            else if (_cachedPOT != null && _cachedPOT.TeamId != teamId)
            {
                ApplyTeamTheming(_cachedPOT.TeamId);
            }
        }

        // Follow owner unit position with world offset (ignoring bone rotation)
        if (targetUnitTransform != null)
        {
            uiRoot.position = targetUnitTransform.position + worldOffset;
        }

        // Billboard: Always face Main Camera flat and upright
        if (cachedCameraTransform == null && Camera.main != null)
        {
            cachedCameraTransform = Camera.main.transform;
        }

        if (cachedCameraTransform != null)
        {
            uiRoot.rotation = cachedCameraTransform.rotation;
        }
    }

    public void ApplyTeamTheming(int? newTeamId = null)
    {
        if (newTeamId.HasValue) teamId = newTeamId.Value;

        Color targetColor = GetCurrentTeamColor();
        if (mainFillImage != null)
        {
            mainFillImage.color = targetColor;
        }
    }

    public void SetHealth(float newHp, float maxHealth)
    {
        if (isDestroyed) return;

        this.maxHp = Mathf.Max(1f, maxHealth);
        float prevHp = currentHp;
        currentHp = Mathf.Clamp(newHp, 0f, this.maxHp);
        float pct = Mathf.Clamp01(currentHp / this.maxHp);

        bool tookDamage = currentHp < prevHp;
        float damageAmount = prevHp - currentHp;

        UpdateFill(pct, instant: false);

        if (tookDamage && gameObject.activeInHierarchy)
        {
            lastDamageFrame = Time.frameCount;
            PlayDamageImpactFeedback();
            if (enableDamageNumbers && damageAmount >= 1f)
            {
                SpawnFloatingDamageNumber(damageAmount);
            }
        }

        // Critical low health heartbeat check (< 25%)
        UpdateLowHealthHeartbeat(pct);

        if (currentHp <= 0f)
        {
            PlayDestruction();
        }
    }

    public void OnTakeDamage(float damageAmount)
    {
        if (isDestroyed || damageAmount <= 0f) return;
        if (lastDamageFrame == Time.frameCount) return; // Prevent duplicate triggers in same frame
        lastDamageFrame = Time.frameCount;

        PlayDamageImpactFeedback();
        if (enableDamageNumbers)
        {
            SpawnFloatingDamageNumber(damageAmount);
        }
    }

    public void OnHeal(float healAmount)
    {
        if (isDestroyed || healAmount <= 0f) return;
        if (mainFillImage != null)
        {
            flashTween?.Kill();
            mainFillImage.color = new Color(0.3f, 1f, 0.4f, 1f); // Vibrant green flash
            Color originalColor = GetCurrentTeamColor();
            flashTween = mainFillImage.DOColor(originalColor, 0.25f).SetEase(Ease.OutQuad);
        }
    }

    private Color GetCurrentTeamColor()
    {
        if (teamId < 0) return neutralColor;
        int localTeam = GameModeManager.LocalTeamId;
        return (teamId == localTeam) ? friendlyColor : enemyColor;
    }

    private void UpdateFill(float targetPercent, bool instant)
    {
        targetPercent = Mathf.Clamp01(targetPercent);

        if (hpText != null && showHpText)
        {
            string suffix = (barType == BarType.Machine) ? " HP" : "";
            hpText.text = $"{Mathf.CeilToInt(currentHp)} / {Mathf.CeilToInt(maxHp)}{suffix}";
        }

        // 1. Snappy Main Fill Drop (Image fillAmount AND physical RectTransform anchorMax)
        if (mainFillImage != null)
        {
            mainFillImage.DOKill();
            if (mainFillRect != null) mainFillRect.DOKill();

            if (instant)
            {
                mainFillImage.fillAmount = targetPercent;
                if (mainFillRect != null) mainFillRect.anchorMax = new Vector2(targetPercent, 1f);
            }
            else
            {
                mainFillImage.DOFillAmount(targetPercent, 0.08f).SetEase(Ease.OutQuad);
                if (mainFillRect != null) mainFillRect.DOAnchorMax(new Vector2(targetPercent, 1f), 0.08f).SetEase(Ease.OutQuad);
            }
        }

        // 2. Delayed Ghost Amber Catch-Up Glide
        if (ghostFillImage != null)
        {
            ghostTween?.Kill();
            ghostFillImage.DOKill();
            if (ghostFillRect != null) ghostFillRect.DOKill();

            if (instant)
            {
                ghostFillImage.fillAmount = targetPercent;
                if (ghostFillRect != null) ghostFillRect.anchorMax = new Vector2(targetPercent, 1f);
            }
            else
            {
                ghostTween = DOVirtual.DelayedCall(ghostDelay, () =>
                {
                    if (ghostFillImage != null && !isDestroyed)
                    {
                        ghostFillImage.DOFillAmount(targetPercent, ghostCatchupDuration).SetEase(Ease.OutCubic);
                        if (ghostFillRect != null)
                            ghostFillRect.DOAnchorMax(new Vector2(targetPercent, 1f), ghostCatchupDuration).SetEase(Ease.OutCubic);
                    }
                });
            }
        }
    }

    private void PlayDamageImpactFeedback()
    {
        Transform animTarget = uiRoot != null ? uiRoot : transform;

        // 1. Juicy Punch Scale
        punchTween?.Kill();
        ApplyNormalizedScale();
        float punchDuration = (barType == BarType.Machine) ? 0.26f : 0.20f;
        int vibrato = (barType == BarType.Machine) ? 7 : 10;
        punchTween = animTarget.DOPunchScale(baseLocalScale * punchScaleMagnitude, punchDuration, vibrato, 1f);

        // 2. White Flash on Main Fill
        if (mainFillImage != null)
        {
            flashTween?.Kill();
            Color originalColor = GetCurrentTeamColor();
            mainFillImage.color = Color.white;
            flashTween = mainFillImage.DOColor(originalColor, 0.15f).SetEase(Ease.OutQuad);
        }
    }

    private void UpdateLowHealthHeartbeat(float healthPct)
    {
        bool shouldPulse = healthPct > 0.001f && healthPct <= 0.25f;
        Transform animTarget = uiRoot != null ? uiRoot : transform;

        if (shouldPulse && !isCriticalLow)
        {
            isCriticalLow = true;
            heartbeatTween?.Kill();
            heartbeatTween = animTarget.DOScale(baseLocalScale * 1.15f, 0.32f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }
        else if (!shouldPulse && isCriticalLow)
        {
            isCriticalLow = false;
            heartbeatTween?.Kill();
            ApplyNormalizedScale();
        }
    }

    private void SpawnFloatingDamageNumber(float damage)
    {
        if (damage <= 0.01f) return;

        // 1. Resolve Camera billboard orientation and directions
        Transform camTr = cachedCameraTransform;
        if (camTr == null && Camera.main != null)
        {
            cachedCameraTransform = Camera.main.transform;
            camTr = cachedCameraTransform;
        }

        Vector3 camRight = camTr != null ? camTr.right : Vector3.right;
        Vector3 camUp = camTr != null ? camTr.up : Vector3.up;
        Quaternion baseCamRot = camTr != null ? camTr.rotation : Quaternion.identity;

        // 2. Anti-Clustering Dynamic Lateral Spray (Fountain arcs so rapid hits don't overlap)
        s_DamageSprayCounter++;
        float[] sprayAngles = { 0f, -0.32f, 0.32f, -0.52f, 0.52f, -0.18f, 0.18f };
        float spray = sprayAngles[s_DamageSprayCounter % sprayAngles.Length] + Random.Range(-0.06f, 0.06f);

        // 3. World Spawn Point (Anchored slightly above the health bar / unit head)
        Vector3 rootPos = (uiRoot != null ? uiRoot.position : transform.position);
        Vector3 spawnPos = rootPos + (camUp * 0.45f) + (camRight * (spray * 0.35f)) + Random.insideUnitSphere * 0.04f;

        GameObject textGo = new GameObject("JuicyDamageNumber", typeof(RectTransform), typeof(TextMeshPro));
        textGo.transform.position = spawnPos;

        // Dynamic comic tilt on burst (opposing drift angle for snappy physical momentum)
        float zAngle = -spray * 24f + Random.Range(-5f, 5f);
        textGo.transform.rotation = baseCamRot * Quaternion.Euler(0f, 0f, zAngle);

        // 4. TextMeshPro & Typography Configuration using TMP_TiltWarp_Outline
        TextMeshPro tmp = textGo.GetComponent<TextMeshPro>();

        // Apply requested TMP_TiltWarp_Outline font asset
        TMP_FontAsset font = damageNumberFont != null ? damageNumberFont : GetTiltWarpOutlineFont();
        if (font != null)
        {
            tmp.font = font;
        }

        int intDamage = Mathf.Max(1, Mathf.RoundToInt(damage));
        bool isMachine = (barType == BarType.Machine);
        bool isObjective = (barType == BarType.Objective);
        bool isCrit = damage >= 35f || (maxHp > 0 && damage >= maxHp * 0.25f);

        // Critical hit styling with exclamation mark
        tmp.text = isCrit ? $"-{intDamage}!" : $"-{intDamage}";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;

        // Responsive font sizing based on hit severity
        float baseFontSize;
        if (isMachine)
            baseFontSize = isCrit ? 6.2f : 4.8f;
        else if (isObjective)
            baseFontSize = isCrit ? 6.8f : 5.4f;
        else
            baseFontSize = isCrit ? 4.8f : 3.8f;

        tmp.fontSize = baseFontSize;

        // 5. Rich Vertex Gradient & High-Contrast Outline
        tmp.enableVertexGradient = true;
        if (isMachine || isCrit)
        {
            // Intense fiery amber / molten lava gradient for crits & siege machines
            tmp.colorGradient = new VertexGradient(
                new Color(1f, 0.98f, 0.50f, 1f), // Top Left: Solar Flare Yellow
                new Color(1f, 0.80f, 0.20f, 1f), // Top Right: Electric Gold
                new Color(1f, 0.32f, 0.05f, 1f), // Bottom Left: Fiery Amber-Crimson
                new Color(0.85f, 0.10f, 0.05f, 1f) // Bottom Right: Molten Ruby
            );
        }
        else
        {
            // Crisp radiant golden yellow gradient for soldier combat hits
            tmp.colorGradient = new VertexGradient(
                new Color(1f, 1f, 0.75f, 1f),     // Top Left: Highlight Cream Yellow
                new Color(1f, 0.92f, 0.35f, 1f),  // Top Right: Pure Golden Yellow
                new Color(1f, 0.62f, 0.08f, 1f),  // Bottom Left: Rich Amber Orange
                new Color(0.95f, 0.45f, 0.02f, 1f) // Bottom Right: Warm Tangerine
            );
        }

        tmp.outlineWidth = 0.30f;
        tmp.outlineColor = new Color(0.04f, 0.04f, 0.05f, 1f);

        // Sorting Order: Render high above characters, ground, and billboard canvas
        MeshRenderer mr = textGo.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sortingOrder = 200;
        }

        // 6. Camera-Relative Parabolic Trajectory
        float jumpApexY = isCrit ? 1.05f : (isMachine ? 0.95f : 0.80f);
        float lateralTravel = spray * 0.70f;
        Vector3 apexPos = spawnPos + (camUp * jumpApexY) + (camRight * lateralTravel);
        Vector3 endPos = apexPos + (camUp * 0.35f) + (camRight * (lateralTravel * 0.35f));

        // 7. Orchestrated DOTween Kinetic Sequence
        textGo.transform.localScale = Vector3.zero;

        float popScale = isCrit ? 1.55f : (isMachine ? 1.40f : 1.28f);
        float popInDuration = isCrit ? 0.11f : 0.09f;

        Sequence seq = DOTween.Sequence();
        seq.SetLink(textGo, LinkBehaviour.KillOnDestroy);

        // A. Explosive scale pop-in with juicy overshoot, settling to natural size
        seq.Append(textGo.transform.DOScale(Vector3.one * popScale, popInDuration).SetEase(Ease.OutBack, 3.8f));
        seq.Append(textGo.transform.DOScale(Vector3.one, 0.08f).SetEase(Ease.InOutQuad));

        // B. Fast burst to apex, followed by buoyant upward glide
        seq.Insert(0f, textGo.transform.DOMove(apexPos, 0.24f).SetEase(Ease.OutCubic));
        seq.Insert(0.24f, textGo.transform.DOMove(endPos, 0.52f).SetEase(Ease.OutSine));

        // C. Smooth rotational recovery from comic tilt towards upright billboard
        seq.Insert(0.06f, textGo.transform.DORotateQuaternion(baseCamRot, 0.32f).SetEase(Ease.OutSine));

        // D. Heavy impact punch scale for crits and siege machines
        if (isCrit || isMachine)
        {
            seq.Insert(0.16f, textGo.transform.DOPunchScale(Vector3.one * 0.22f, 0.18f, 5, 0.8f));
        }

        // E. Smooth fade-out and gentle contraction at the end
        seq.Insert(0.44f, tmp.DOFade(0f, 0.30f).SetEase(Ease.InQuad));
        seq.Insert(0.46f, textGo.transform.DOScale(Vector3.one * 0.55f, 0.28f).SetEase(Ease.InBack));

        // F. Clean destruction
        seq.OnComplete(() =>
        {
            if (textGo != null)
            {
                Destroy(textGo);
            }
        });
    }

    public void PlayDestruction()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        ghostTween?.Kill();
        punchTween?.Kill();
        flashTween?.Kill();
        heartbeatTween?.Kill();

        Transform animTarget = uiRoot != null ? uiRoot : transform;
        animTarget.DOScale(Vector3.zero, 0.25f)
            .SetEase(Ease.InBack)
            .OnComplete(() =>
            {
                if (uiRoot != null) uiRoot.gameObject.SetActive(false);
                if (this != null && gameObject != null)
                    gameObject.SetActive(false);
            });
    }

    public void EnsureUIComponents()
    {
        HideForeign3DCubes();

        Sprite whiteSprite = GetDefaultWhiteSprite();

        // 1. World-Space Canvas Container
        Transform uiCanvasTr = null;
        if (targetUnitTransform != null)
        {
            uiCanvasTr = targetUnitTransform.Find("UICanvas");
        }
        if (uiCanvasTr == null)
        {
            uiCanvasTr = transform.Find("UICanvas");
        }

        if (uiCanvasTr == null)
        {
            GameObject uiCanvasGo = new GameObject("UICanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Transform parentTarget = targetUnitTransform != null ? targetUnitTransform : transform;
            uiCanvasGo.transform.SetParent(parentTarget, false);
            uiCanvasTr = uiCanvasGo.transform;
        }
        else if (targetUnitTransform != null && uiCanvasTr.parent != targetUnitTransform)
        {
            // Reparent to unit root to avoid non-uniform scale distortion from old healthbar cube
            uiCanvasTr.SetParent(targetUnitTransform, false);
        }

        uiRoot = uiCanvasTr;

        canvas = uiRoot.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            if (cachedCameraTransform != null) canvas.worldCamera = cachedCameraTransform.GetComponent<Camera>();
        }

        CanvasScaler scaler = uiRoot.GetComponent<CanvasScaler>();
        if (scaler != null) scaler.dynamicPixelsPerUnit = 10f;

        RectTransform rootRect = uiRoot.GetComponent<RectTransform>();
        if (rootRect != null)
        {
            rootRect.sizeDelta = barSize;
            rootRect.pivot = new Vector2(0.5f, 0.5f);
        }

        ApplyNormalizedScale();

        // 2. Outer Border
        Transform borderTr = uiRoot.Find("Border");
        if (borderTr == null)
        {
            GameObject borderGo = new GameObject("Border", typeof(RectTransform), typeof(Image));
            borderGo.transform.SetParent(uiRoot, false);
            borderTr = borderGo.transform;
        }
        RectTransform borderRect = borderTr.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        float borderOffset = (barType == BarType.Machine) ? 4f : (barType == BarType.Objective ? 5f : 2.5f);
        borderRect.offsetMin = new Vector2(-borderOffset, -borderOffset);
        borderRect.offsetMax = new Vector2(borderOffset, borderOffset);
        borderImage = borderTr.GetComponent<Image>();
        borderImage.sprite = whiteSprite;
        borderImage.color = borderColor;

        // 3. Dark Background
        Transform bgTr = uiRoot.Find("Background");
        if (bgTr == null)
        {
            GameObject bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(uiRoot, false);
            bgTr = bgGo.transform;
        }
        RectTransform bgRect = bgTr.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        bgImage = bgTr.GetComponent<Image>();
        bgImage.sprite = whiteSprite;
        bgImage.color = backgroundColor;

        // 4. Ghost Catch-Up Fill
        Transform ghostTr = uiRoot.Find("GhostFill");
        if (ghostTr == null)
        {
            GameObject ghostGo = new GameObject("GhostFill", typeof(RectTransform), typeof(Image));
            ghostGo.transform.SetParent(uiRoot, false);
            ghostTr = ghostGo.transform;
        }
        ghostFillRect = ghostTr.GetComponent<RectTransform>();
        ghostFillRect.anchorMin = Vector2.zero;
        ghostFillRect.anchorMax = Vector2.one;
        ghostFillRect.offsetMin = Vector2.zero;
        ghostFillRect.offsetMax = Vector2.zero;
        ghostFillImage = ghostTr.GetComponent<Image>();
        ghostFillImage.sprite = whiteSprite;
        ghostFillImage.type = Image.Type.Filled;
        ghostFillImage.fillMethod = Image.FillMethod.Horizontal;
        ghostFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        ghostFillImage.color = ghostCatchupColor;

        // 5. Main Health Fill
        Transform fillTr = uiRoot.Find("MainFill");
        if (fillTr == null)
        {
            GameObject fillGo = new GameObject("MainFill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(uiRoot, false);
            fillTr = fillGo.transform;
        }
        mainFillRect = fillTr.GetComponent<RectTransform>();
        mainFillRect.anchorMin = Vector2.zero;
        mainFillRect.anchorMax = Vector2.one;
        mainFillRect.offsetMin = Vector2.zero;
        mainFillRect.offsetMax = Vector2.zero;
        mainFillImage = fillTr.GetComponent<Image>();
        mainFillImage.sprite = whiteSprite;
        mainFillImage.type = Image.Type.Filled;
        mainFillImage.fillMethod = Image.FillMethod.Horizontal;
        mainFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        mainFillImage.color = GetCurrentTeamColor();

        // 6. Optional HP Text (machines & objectives)
        Transform textTr = uiRoot.Find("HPText");
        if (textTr == null)
        {
            GameObject textGo = new GameObject("HPText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(uiRoot, false);
            textTr = textGo.transform;
        }
        RectTransform textRect = textTr.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        hpText = textTr.GetComponent<TextMeshProUGUI>();
        hpText.alignment = TextAlignmentOptions.Center;
        hpText.fontSize = (barType == BarType.Machine) ? 14f : (barType == BarType.Objective ? 18f : 11f);
        hpText.fontStyle = FontStyles.Bold;
        hpText.color = Color.white;
        hpText.outlineWidth = 0.28f;
        hpText.outlineColor = Color.black;
        TMP_FontAsset textFont = damageNumberFont != null ? damageNumberFont : GetTiltWarpOutlineFont();
        if (textFont != null) hpText.font = textFont;
        hpText.gameObject.SetActive(showHpText);
    }

    private void OnDisable()
    {
        KillAllTweens();
    }

    private void OnDestroy()
    {
        KillAllTweens();
    }

    private void KillAllTweens()
    {
        ghostTween?.Kill();
        punchTween?.Kill();
        flashTween?.Kill();
        heartbeatTween?.Kill();

        if (mainFillImage != null) mainFillImage.DOKill();
        if (ghostFillImage != null) ghostFillImage.DOKill();
        if (mainFillRect != null) mainFillRect.DOKill();
        if (ghostFillRect != null) ghostFillRect.DOKill();
        if (uiRoot != null) uiRoot.DOKill();
        transform.DOKill();
    }
}
