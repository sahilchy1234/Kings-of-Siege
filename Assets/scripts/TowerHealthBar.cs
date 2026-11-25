using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Ultra-Juicy, AAA World-Space Health Bar for Defensive Wall Towers.
/// Features:
/// - Guaranteed Decreasing Fill: Dual-layer horizontal animation (Unity UI Image.fillAmount + RectTransform.anchorMax)
///   powered by auto-generated crisp white sprite texture so the bar visibly drops on every hit.
/// - Snappy main fill drop (0.08s) with delayed "Ghost" amber damage catch-up glide.
/// - Impact punch scale (heavy structural bounce).
/// - White hit flash on fill.
/// - Floating combat text damage numbers ("-35", "-60") with arc and fade.
/// - Critical low-health (<25%) rhythmic heartbeat pulse.
/// - Dynamic team theming (Player Royal Blue, AI Fiery Crimson, Neutral Contestable Amber).
/// - Billboard camera alignment: Always flat & upright facing the main camera.
/// - Dramatic death collapse on destruction.
/// </summary>
[ExecuteAlways]
public class TowerHealthBar : MonoBehaviour
{
    public enum BillboardMode
    {
        LookAtCamera,       // Points directly at the camera lens (zero perspective skew from any angle)
        CameraRotation,     // Matches camera plane parallel rotation
        CylindricalYOnly    // Yaw-only rotation around Y axis
    }

    [Header("Visual Dimensions & Offsets")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 10f, 0f);
    [SerializeField] private Vector2 barSize = new Vector2(240f, 32f);
    [SerializeField] private BillboardMode billboardMode = BillboardMode.LookAtCamera;

    [Header("Colors & Theming")]
    [SerializeField] private Color friendlyColor = new Color(0.04f, 0.52f, 0.89f, 1f); // Vibrant Royal Blue (#0984E3)
    [SerializeField] private Color enemyColor = new Color(0.84f, 0.19f, 0.19f, 1f);    // Fiery Crimson (#D63031)
    [SerializeField] private Color neutralColor = new Color(0.95f, 0.77f, 0.25f, 1f);  // Contestable Neutral Amber Gold (#F1C40F)
    [SerializeField] private Color ghostCatchupColor = new Color(1f, 0.62f, 0.26f, 0.95f); // Amber Orange
    [SerializeField] private Color backgroundColor = new Color(0.12f, 0.15f, 0.18f, 0.9f); // Dark Charcoal
    [SerializeField] private Color borderColor = new Color(0.05f, 0.05f, 0.07f, 0.95f);

    [Header("Animation Durations")]
    [SerializeField] private float ghostDelay = 0.25f;
    [SerializeField] private float ghostCatchupDuration = 0.45f;
    [SerializeField] private float punchScaleMagnitude = 0.18f;

    [Header("Floating Damage Numbers (Juice & Typography)")]
    [SerializeField] private bool enableDamageNumbers = true;
    [SerializeField] private TMP_FontAsset damageNumberFont;
    [SerializeField] private Color towerHitTopColor = new Color(1f, 0.96f, 0.92f, 1f);     // White-hot peach
    [SerializeField] private Color towerHitMidColor = new Color(1f, 0.65f, 0.35f, 1f);     // Solar tangerine
    [SerializeField] private Color towerHitBottomColor = new Color(1f, 0.23f, 0.19f, 1f);  // Searing crimson-coral
    [SerializeField] private Color towerHitDarkColor = new Color(0.64f, 0.03f, 0.03f, 1f);   // Deep molten ruby
    [SerializeField] private Color towerCritTopColor = new Color(1f, 1f, 1f, 1f);           // Diamond white-hot
    [SerializeField] private Color towerCritBottomColor = new Color(0.75f, 0.05f, 0.12f, 1f); // Intense molten magma

    [Header("UI Elements (Auto-Generated if Null)")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private Image borderImage;
    [SerializeField] private Image bgImage;
    [SerializeField] private Image ghostFillImage;
    [SerializeField] private Image mainFillImage;
    [SerializeField] private RectTransform ghostFillRect;
    [SerializeField] private RectTransform mainFillRect;
    [SerializeField] private TextMeshProUGUI hpText;

    private int teamId = 0;
    private float maxHp = 650f;
    private float currentHp = 650f;
    private Tween ghostTween;
    private Tween punchTween;
    private Tween flashTween;
    private Tween heartbeatTween;
    private Transform cachedCameraTransform;
    private Transform towerTransform;
    private bool isDestroyed = false;
    private bool isCriticalLow = false;
    private int lastDamageFrame = -1;

    private static Sprite s_DefaultWhiteSprite;

    public static Sprite GetDefaultWhiteSprite()
    {
        if (s_DefaultWhiteSprite == null)
        {
            s_DefaultWhiteSprite = JuicyUnitHealthBar.GetDefaultWhiteSprite();
        }
        return s_DefaultWhiteSprite;
    }

    public TMP_FontAsset DamageNumberFont
    {
        get => damageNumberFont;
        set => damageNumberFont = value;
    }

    private static TMP_FontAsset s_SharedTowerDamageFont;
    private static int s_TowerDamageSprayCounter = 0;

    public static TMP_FontAsset GetTiltWarpOutlineFont()
    {
        if (s_SharedTowerDamageFont != null) return s_SharedTowerDamageFont;

        // 1. Try Resources.Load
        s_SharedTowerDamageFont = Resources.Load<TMP_FontAsset>("Fonts/TMP_TiltWarp_Outline");
        if (s_SharedTowerDamageFont == null)
            s_SharedTowerDamageFont = Resources.Load<TMP_FontAsset>("TMP_TiltWarp_Outline");

        // 2. Fallback to AssetDatabase in Editor
        if (s_SharedTowerDamageFont == null)
        {
            s_SharedTowerDamageFont = RuntimeAssets.LoadAssetAtPath<TMP_FontAsset>("Assets/UIPACK/ResourcesData/Fonts/TMP_TiltWarp_Outline.asset");
        }

        return s_SharedTowerDamageFont;
    }

    public void SetWorldOffset(Vector3 offset) => worldOffset = offset;

    public void Setup(Transform ownerTower, int owningTeam, float maxHealth)
    {
        this.towerTransform = ownerTower;
        this.teamId = owningTeam;
        this.maxHp = Mathf.Max(1f, maxHealth);
        this.currentHp = this.maxHp;
        this.isDestroyed = false;

        EnsureUIComponents();
        ApplyTeamTheming();
        UpdateFill(1f, instant: true);
        ApplyNormalizedScale();
        UpdatePositionAndRotation();
    }

    /// <summary>
    /// Resets the health bar under new team ownership after capture.
    /// </summary>
    public void ResetHealthBar(int newTeam, float newMaxHp)
    {
        this.isDestroyed = false;
        this.isCriticalLow = false;
        this.teamId = newTeam;
        this.maxHp = Mathf.Max(1f, newMaxHp);
        this.currentHp = this.maxHp;

        gameObject.SetActive(true);
        EnsureUIComponents();
        ApplyTeamTheming();
        UpdateFill(1f, instant: true);
        ApplyNormalizedScale();
        UpdatePositionAndRotation();
    }

    private const float TARGET_WORLD_WIDTH = 3.4f;
    private const float BASE_UI_WIDTH = 240f;
    private const float TARGET_WORLD_SCALE = TARGET_WORLD_WIDTH / BASE_UI_WIDTH; // ~0.01416f

    public Vector3 GetTargetLocalScale()
    {
        Vector3 parentLossy = (transform.parent != null) ? transform.parent.lossyScale : Vector3.one;
        float pX = Mathf.Abs(parentLossy.x) > 0.001f ? Mathf.Abs(parentLossy.x) : 1f;
        float pY = Mathf.Abs(parentLossy.y) > 0.001f ? Mathf.Abs(parentLossy.y) : 1f;
        float pZ = Mathf.Abs(parentLossy.z) > 0.001f ? Mathf.Abs(parentLossy.z) : 1f;
        return new Vector3(TARGET_WORLD_SCALE / pX, TARGET_WORLD_SCALE / pY, TARGET_WORLD_SCALE / pZ);
    }

    public void ApplyNormalizedScale()
    {
        transform.localScale = GetTargetLocalScale();
    }

    private void Awake()
    {
        FindOwnerTower();
        EnsureUIComponents();
        ApplyNormalizedScale();
        CleanForeignChildren();
        UpdatePositionAndRotation();
    }

    private void Start()
    {
        FindOwnerTower();
        EnsureUIComponents();
        ApplyNormalizedScale();
        CleanForeignChildren();
        ApplyTeamTheming();
        UpdatePositionAndRotation();
    }

    private void FindOwnerTower()
    {
        if (towerTransform != null) return;
        TowerCombatController tcc = GetComponentInParent<TowerCombatController>();
        if (tcc != null)
        {
            towerTransform = tcc.transform;
            teamId = tcc.TeamId;
            maxHp = tcc.MaxHealth;
            currentHp = maxHp;
            return;
        }

        PeasantObjectiveTarget pot = GetComponentInParent<PeasantObjectiveTarget>();
        if (pot != null)
        {
            towerTransform = pot.transform;
            teamId = pot.TeamId;
            maxHp = pot.MaxHealth;
            currentHp = pot.CurrentHealth;
            return;
        }

        if (transform.parent != null)
        {
            towerTransform = transform.parent;
        }
    }

    public Transform GetActiveCameraTransform()
    {
        // 1. Cached reference if still valid and active
        if (cachedCameraTransform != null && cachedCameraTransform.gameObject.activeInHierarchy)
        {
            return cachedCameraTransform;
        }

        // 2. Main Camera
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            cachedCameraTransform = mainCam.transform;
            return cachedCameraTransform;
        }

        // 3. Current rendering camera fallback
        Camera currentCam = Camera.current;
        if (currentCam != null)
        {
            cachedCameraTransform = currentCam.transform;
            return cachedCameraTransform;
        }

#if UNITY_EDITOR
        // 4. In Editor scene view
        if (!Application.isPlaying)
        {
            if (UnityEditor.SceneView.lastActiveSceneView != null && UnityEditor.SceneView.lastActiveSceneView.camera != null)
            {
                return UnityEditor.SceneView.lastActiveSceneView.camera.transform;
            }
        }
#endif

        // 5. Fallback search for any active Camera
        Camera findCam = Object.FindFirstObjectByType<Camera>();
        if (findCam != null)
        {
            cachedCameraTransform = findCam.transform;
            return cachedCameraTransform;
        }

        return null;
    }

    private void LateUpdate()
    {
        UpdatePositionAndRotation();
    }

    /// <summary>
    /// Updates world position anchored to tower and orients the health bar to look directly at the active camera.
    /// </summary>
    public void UpdatePositionAndRotation()
    {
        if (isDestroyed) return;

        if (towerTransform == null)
        {
            FindOwnerTower();
        }

        // Follow tower position with world offset
        if (towerTransform != null)
        {
            transform.position = towerTransform.position + worldOffset;
        }

        // Billboard: Face Camera directly
        Transform camTr = GetActiveCameraTransform();
        if (camTr != null)
        {
            OrientTowardsCamera(camTr);
        }
    }

    public void OrientTowardsCamera(Transform camTr)
    {
        if (camTr == null) return;

        switch (billboardMode)
        {
            case BillboardMode.LookAtCamera:
            {
                // Vector from camera to health bar
                Vector3 dirFromCam = transform.position - camTr.position;
                if (dirFromCam.sqrMagnitude > 0.001f)
                {
                    // For World-Space Canvas facing -Z, pointing +Z along dirFromCam makes the canvas face directly at the camera lens
                    transform.rotation = Quaternion.LookRotation(dirFromCam.normalized, camTr.up);
                }
                break;
            }
            case BillboardMode.CameraRotation:
            {
                transform.rotation = camTr.rotation;
                break;
            }
            case BillboardMode.CylindricalYOnly:
            {
                Vector3 dirFromCam = transform.position - camTr.position;
                dirFromCam.y = 0f;
                if (dirFromCam.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(dirFromCam.normalized, Vector3.up);
                }
                break;
            }
        }

        if (canvas != null && canvas.worldCamera == null)
        {
            canvas.worldCamera = camTr.GetComponent<Camera>();
        }
    }

    public void ApplyTeamTheming()
    {
        Color targetColor = GetCurrentTeamColor();
        if (mainFillImage != null)
        {
            mainFillImage.color = targetColor;
        }
    }

    private Color GetCurrentTeamColor()
    {
        if (teamId < 0) return neutralColor;
        int localTeam = GameModeManager.LocalTeamId;
        return (teamId == localTeam) ? friendlyColor : enemyColor;
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
        if (lastDamageFrame == Time.frameCount) return;
        lastDamageFrame = Time.frameCount;

        PlayDamageImpactFeedback();
        if (enableDamageNumbers)
        {
            SpawnFloatingDamageNumber(damageAmount);
        }
    }

    private void UpdateFill(float targetPercent, bool instant)
    {
        targetPercent = Mathf.Clamp01(targetPercent);

        if (hpText != null)
        {
            string suffix = (teamId < 0) ? " (Neutral)" : "";
            hpText.text = $"{Mathf.CeilToInt(currentHp)} / {Mathf.CeilToInt(maxHp)}{suffix}";
        }

        // 1. Snappy Main Fill Drop (Image.fillAmount + RectTransform anchorMax)
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
        // 1. Juicy Punch Scale
        punchTween?.Kill();
        ApplyNormalizedScale();
        Vector3 baseLocalScale = GetTargetLocalScale();
        punchTween = transform.DOPunchScale(baseLocalScale * punchScaleMagnitude, 0.22f, 10, 1f);

        // 2. White Flash on Fill
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

        if (shouldPulse && !isCriticalLow)
        {
            isCriticalLow = true;
            heartbeatTween?.Kill();
            Vector3 baseScale = GetTargetLocalScale();
            heartbeatTween = transform.DOScale(baseScale * 1.15f, 0.32f)
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

        // 2. Anti-Clustering Dynamic Lateral Spray for High-Volume Tower Hits
        s_TowerDamageSprayCounter++;
        float[] sprayAngles = { 0f, -0.38f, 0.38f, -0.62f, 0.62f, -0.22f, 0.22f };
        float spray = sprayAngles[s_TowerDamageSprayCounter % sprayAngles.Length] + Random.Range(-0.08f, 0.08f);

        // 3. World Spawn Point (Anchored slightly above the tower health bar)
        Vector3 spawnPos = transform.position + (camUp * 0.65f) + (camRight * (spray * 0.45f)) + Random.insideUnitSphere * 0.06f;

        GameObject textGo = new GameObject("JuicyTowerDamageNumber", typeof(RectTransform), typeof(TextMeshPro));
        textGo.transform.position = spawnPos;

        // Dynamic comic tilt on burst (opposing drift angle for snappy physical momentum)
        float zAngle = -spray * 22f + Random.Range(-6f, 6f);
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
        bool isHeavySiege = damage >= 45f || (maxHp > 0 && damage >= maxHp * 0.12f);

        // Formatting text: Heavy siege strikes get exclamation punch
        tmp.text = isHeavySiege ? $"-{intDamage}!" : $"-{intDamage}";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;

        // Tower damage numbers are monumental and heavier than soldier hits
        float baseFontSize = isHeavySiege ? 6.8f : 5.4f;
        tmp.fontSize = baseFontSize;

        // 5. Unique Tower Color Design: White-Hot Coral to Fiery Volcanic Magma
        tmp.enableVertexGradient = true;
        if (isHeavySiege)
        {
            // Colossal siege impact (catapults, rams, heavy crits):
            // Pure diamond white-hot peak bursting into fiery scarlet and dark volcanic magma
            tmp.colorGradient = new VertexGradient(
                towerCritTopColor,
                new Color(1f, 0.70f, 0.60f, 1f),
                new Color(1f, 0.15f, 0.15f, 1f),
                towerCritBottomColor
            );
        }
        else
        {
            // Standard structural hit on tower:
            // White-hot solar peach top fading into vibrant electric coral-scarlet and molten ruby bottom
            tmp.colorGradient = new VertexGradient(
                towerHitTopColor,
                towerHitMidColor,
                towerHitBottomColor,
                towerHitDarkColor
            );
        }

        tmp.outlineWidth = 0.32f;
        tmp.outlineColor = new Color(0.03f, 0.03f, 0.04f, 1f);

        // Sorting Order: Render high above stone tower, arrows, and world UI
        MeshRenderer mr = textGo.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sortingOrder = 220;
        }

        // 6. Camera-Relative Parabolic Trajectory
        float jumpApexY = isHeavySiege ? 1.35f : 1.10f;
        float lateralTravel = spray * 0.90f;
        Vector3 apexPos = spawnPos + (camUp * jumpApexY) + (camRight * lateralTravel);
        Vector3 endPos = apexPos + (camUp * 0.45f) + (camRight * (lateralTravel * 0.35f));

        // 7. Orchestrated DOTween Kinetic Sequence
        textGo.transform.localScale = Vector3.zero;

        float popScale = isHeavySiege ? 1.60f : 1.35f;
        float popInDuration = isHeavySiege ? 0.12f : 0.09f;

        Sequence seq = DOTween.Sequence();
        seq.SetLink(textGo, LinkBehaviour.KillOnDestroy);

        // A. Explosive scale pop-in with heavy structural overshoot
        seq.Append(textGo.transform.DOScale(Vector3.one * popScale, popInDuration).SetEase(Ease.OutBack, 4.0f));
        seq.Append(textGo.transform.DOScale(Vector3.one, 0.08f).SetEase(Ease.InOutQuad));

        // B. Fast burst to apex, followed by buoyant upward float
        seq.Insert(0f, textGo.transform.DOMove(apexPos, 0.25f).SetEase(Ease.OutCubic));
        seq.Insert(0.25f, textGo.transform.DOMove(endPos, 0.55f).SetEase(Ease.OutSine));

        // C. Comic tilt straightening towards camera billboard
        seq.Insert(0.06f, textGo.transform.DORotateQuaternion(baseCamRot, 0.34f).SetEase(Ease.OutSine));

        // D. Heavy structural hit impact punch at apex
        if (isHeavySiege)
        {
            seq.Insert(0.18f, textGo.transform.DOPunchScale(Vector3.one * 0.26f, 0.20f, 6, 0.9f));
        }

        // E. Fade out and shrink
        seq.Insert(0.48f, tmp.DOFade(0f, 0.32f).SetEase(Ease.InQuad));
        seq.Insert(0.50f, textGo.transform.DOScale(Vector3.one * 0.55f, 0.30f).SetEase(Ease.InBack));

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

        KillAllTweens();

        transform.DOScale(Vector3.zero, 0.35f)
            .SetEase(Ease.InBack)
            .OnComplete(() =>
            {
                if (this != null && gameObject != null)
                    gameObject.SetActive(false);
            });
    }

    public void CleanForeignChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name == "[PowerupsBar]" || child.name == "GameResultModal")
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(child.gameObject);
                else
                    Destroy(child.gameObject);
#else
                Destroy(child.gameObject);
#endif
            }
        }
    }

    public void EnsureUIComponents()
    {
        CleanForeignChildren();

        Sprite whiteSprite = GetDefaultWhiteSprite();

        // Setup World-Space Canvas
        canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        if (cachedCameraTransform != null) canvas.worldCamera = cachedCameraTransform.GetComponent<Camera>();

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        RectTransform rootRect = GetComponent<RectTransform>();
        rootRect.sizeDelta = barSize;
        ApplyNormalizedScale(); // Crisp world scaling normalized across all parent scales

        // Outer Border
        Transform borderTr = transform.Find("Border");
        if (borderTr == null)
        {
            GameObject borderGo = new GameObject("Border", typeof(RectTransform), typeof(Image));
            borderGo.transform.SetParent(transform, false);
            borderTr = borderGo.transform;
        }
        RectTransform borderRect = borderTr.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = new Vector2(-4f, -4f);
        borderRect.offsetMax = new Vector2(4f, 4f);
        borderImage = borderTr.GetComponent<Image>();
        borderImage.sprite = whiteSprite;
        borderImage.color = borderColor;

        // Background
        Transform bgTr = transform.Find("Background");
        if (bgTr == null)
        {
            GameObject bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(transform, false);
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

        // Ghost Catch-up Fill
        Transform ghostTr = transform.Find("GhostFill");
        if (ghostTr == null)
        {
            GameObject ghostGo = new GameObject("GhostFill", typeof(RectTransform), typeof(Image));
            ghostGo.transform.SetParent(transform, false);
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

        // Main Health Fill
        Transform fillTr = transform.Find("MainFill");
        if (fillTr == null)
        {
            GameObject fillGo = new GameObject("MainFill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(transform, false);
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

        // Text Label
        Transform textTr = transform.Find("HPText");
        if (textTr == null)
        {
            GameObject textGo = new GameObject("HPText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(transform, false);
            textTr = textGo.transform;
        }
        RectTransform textRect = textTr.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        hpText = textTr.GetComponent<TextMeshProUGUI>();
        hpText.alignment = TextAlignmentOptions.Center;
        hpText.fontSize = 20f;
        hpText.fontStyle = FontStyles.Bold;
        hpText.color = Color.white;
        hpText.outlineWidth = 0.25f;
        hpText.outlineColor = Color.black;

        // Try load high quality font
        TMP_FontAsset font = damageNumberFont != null ? damageNumberFont : GetTiltWarpOutlineFont();
        if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts/TMP_LINESeedSans");
        if (font == null) font = Resources.Load<TMP_FontAsset>("LiberationSans SDF");
        if (font != null) hpText.font = font;
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
        transform.DOKill();
    }
}
