using UnityEngine;

/// <summary>
/// Battlefield Deployment & Territory Visuals.
/// Creates a clean, tactical deployment arena matching the reference design:
/// - Player zone with bright blue boundary borders and subtle tinted ground.
/// - Enemy zone with crimson red boundary borders and subtle tinted ground.
/// - Clear dividing frontline between player & enemy territories.
/// - Clean ground-plane alignment (XZ) matching world space with no strange vertical skew.
/// - Supports dynamic territory pushing at runtime.
/// </summary>
public class BattleSceneSetup : MonoBehaviour
{
    public static BattleSceneSetup Instance { get; private set; }
    public enum TerritoryCalculationMode
    {
        FurthestTroopAdvance, // The frontline directly tracks how far the armies have physically advanced into each other's territory!
        UnitPresence,         // Dynamic frontline push based on unit positions & headcount
        UnitCountRatio,       // Headcount ratio
        FixedSplit            // Fixed percentage split (e.g., 50/50)
    }

    [Header("Territory Calculation")]
    [Tooltip("Dynamically push territory boundary based on battle progress.")]
    public bool dynamicTerritoryAtRuntime = true;
    public TerritoryCalculationMode calculationMode = TerritoryCalculationMode.FurthestTroopAdvance;

    [Tooltip("How far behind the vanguard troop line the border sits (creates breathing room).")]
    [Range(0f, 4f)] public float vanguardBuffer = 1.0f;

    [Tooltip("Minimum territory ratio preserved for either side (e.g. 0.08 = player never drops below 8% unless defeated).")]
    [Range(0.02f, 0.2f)] public float minTerritoryRatio = 0.06f;

    [Range(0.1f, 0.9f)]
    [Tooltip("Default split ratio (0.5 = 50% Player, 50% Enemy).")]
    public float playerTerritoryRatio = 0.5f;

    [Tooltip("Smooth transition speed for frontline boundary movement.")]
    [Min(0.1f)] public float smoothDampSpeed = 3f;

    [Header("Battlefield Size")]
    [Tooltip("Width of the battlefield (across X axis).")]
    [Min(1f)] public float arenaWidth = 14f;
    [Tooltip("Total length of the battlefield from player base to enemy base (along Z axis).")]
    [Min(1f)] public float arenaLength = 26f;
    [Tooltip("Border line thickness.")]
    [Min(0.02f)] public float borderThickness = 0.18f;
    [Tooltip("Height above ground to prevent z-fighting with the floor mesh.")]
    public float groundYOffset = 0.05f;

    [Header("Colors & Opacity (Reference Match)")]
    [Tooltip("Player border outline color (Tactical Blue).")]
    public Color playerBorderColor = new Color(0.15f, 0.45f, 1f, 0.95f);
    [Tooltip("Player zone fill/tint base color.")]
    public Color playerFillColor = new Color(0.18f, 0.50f, 1f, 1f);
    [Tooltip("Opacity / transparency of player filled surface (0 = fully invisible, 1 = solid).")]
    [Range(0f, 1f)] public float playerFillOpacity = 0.22f;

    [Tooltip("Enemy border outline color (Tactical Red).")]
    public Color enemyBorderColor = new Color(1f, 0.22f, 0.22f, 0.95f);
    [Tooltip("Enemy zone fill/tint base color.")]
    public Color enemyFillColor = new Color(1f, 0.22f, 0.22f, 1f);
    [Tooltip("Opacity / transparency of enemy filled surface (0 = fully invisible, 1 = solid).")]
    [Range(0f, 1f)] public float enemyFillOpacity = 0.22f;

    [Tooltip("Dividing frontline border color (highlights contested boundary).")]
    public Color frontlineColor = new Color(0.2f, 0.55f, 1f, 1f);

    [Header("Display Options")]
    [Tooltip("If true, the battlefield borders & fill only appear when aiming/targeting or pointing at the ground.")]
    public bool showOnlyWhileAiming = true;
    public bool showZoneFills = true;
    public bool showBorders = true;
    public bool showFrontline = true;
    public bool updateInEditorMode = true;

    // Runtime state
    private float currentRatioTarget = 0.5f;
    private float displayedRatio = 0.5f;

    public float CurrentRatioTarget => currentRatioTarget;
    public float DisplayedRatio => displayedRatio;

    private GameObject visualRoot;
    private Transform playerFillTransform;
    private Transform enemyFillTransform;

    // Player borders: Bottom, Left, Right
    private Transform playerBottomBorder;
    private Transform playerLeftBorder;
    private Transform playerRightBorder;

    // Enemy borders: Top, Left, Right
    private Transform enemyTopBorder;
    private Transform enemyLeftBorder;
    private Transform enemyRightBorder;

    // Frontline dividing border
    private Transform frontlineBorder;

    private Material playerFillMaterial;
    private Material enemyFillMaterial;
    private int lastLocalTeamId = -1;
    private float _territoryCalcTimer;
    private const float TerritoryCalcInterval = 0.25f; // 4 Hz instead of 60 Hz

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        lastLocalTeamId = GameModeManager.LocalTeamId;
        displayedRatio = playerTerritoryRatio;
        currentRatioTarget = playerTerritoryRatio;
        BuildBattlefield();
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            if (lastLocalTeamId != GameModeManager.LocalTeamId)
            {
                lastLocalTeamId = GameModeManager.LocalTeamId;
                BuildBattlefield();
            }

            if (dynamicTerritoryAtRuntime)
            {
                _territoryCalcTimer -= Time.deltaTime;
                if (_territoryCalcTimer <= 0f)
                {
                    _territoryCalcTimer = TerritoryCalcInterval;
                    CalculateTerritoryDominance();
                }
            }
            else
            {
                currentRatioTarget = playerTerritoryRatio;
            }

            displayedRatio = Mathf.Lerp(displayedRatio, currentRatioTarget, Time.deltaTime * smoothDampSpeed);
            UpdateLayout(displayedRatio);
            UpdateVisibility();
        }
        else if (updateInEditorMode)
        {
            displayedRatio = playerTerritoryRatio;
            BuildBattlefield();
        }
    }

    private void OnValidate()
    {
        arenaWidth = Mathf.Max(1f, arenaWidth);
        arenaLength = Mathf.Max(1f, arenaLength);
        borderThickness = Mathf.Max(0.02f, borderThickness);
        playerTerritoryRatio = Mathf.Clamp(playerTerritoryRatio, 0.1f, 0.9f);
        playerFillOpacity = Mathf.Clamp01(playerFillOpacity);
        enemyFillOpacity = Mathf.Clamp01(enemyFillOpacity);

        ApplyMaterialProperties();
    }

    private void ApplyMaterialProperties()
    {
        if (playerFillMaterial != null)
        {
            Color p = playerFillColor;
            p.a = playerFillOpacity;
            if (playerFillMaterial.HasProperty("_BaseColor")) playerFillMaterial.SetColor("_BaseColor", p);
            if (playerFillMaterial.HasProperty("_Color")) playerFillMaterial.SetColor("_Color", p);
        }

        if (enemyFillMaterial != null)
        {
            Color e = enemyFillColor;
            e.a = enemyFillOpacity;
            if (enemyFillMaterial.HasProperty("_BaseColor")) enemyFillMaterial.SetColor("_BaseColor", e);
            if (enemyFillMaterial.HasProperty("_Color")) enemyFillMaterial.SetColor("_Color", e);
        }
    }

    private void CalculateTerritoryDominance()
    {
        if (calculationMode == TerritoryCalculationMode.FixedSplit)
        {
            currentRatioTarget = playerTerritoryRatio;
            return;
        }

        // Use BattleAIManager's cached list to avoid a full scene scan.
        System.Collections.Generic.IReadOnlyList<PeasantUnitController> allUnits =
            BattleAIManager.Instance != null
                ? BattleAIManager.Instance.GetAllLivingUnits()
                : (System.Collections.Generic.IReadOnlyList<PeasantUnitController>)
                    FindObjectsByType<PeasantUnitController>(FindObjectsSortMode.None);

        int playerCount = 0;
        int enemyCount = 0;
        float playerProgress = 0f;
        float enemyProgress = 0f;

        // Track furthest physical vanguard advance (0 = player base, 1 = enemy base)
        float maxPlayerAdvance = 0f;
        float maxEnemyAdvance = 0f; // how far enemy advanced towards player base (0 = at enemy base, 1 = at player base)

        Vector3 center = transform.position;
        Vector3 forwardDir = transform.forward; // Points from player towards enemy

        foreach (var unit in allUnits)
        {
            if (unit == null || unit.IsDead) continue;

            // Normalized position along the length (-0.5 at player base, +0.5 at enemy base)
            Vector3 diff = unit.transform.position - center;
            float zNorm = Vector3.Dot(diff, forwardDir) / arenaLength;
            float playerBaseToUnit = Mathf.Clamp01(zNorm + 0.5f); // 0 at player base, 1 at enemy base

            if (unit.TeamId == 0)
            {
                playerCount++;
                playerProgress += playerBaseToUnit;
                if (playerBaseToUnit > maxPlayerAdvance)
                {
                    maxPlayerAdvance = playerBaseToUnit;
                }
            }
            else if (unit.TeamId == 1)
            {
                enemyCount++;
                float enemyBaseToUnit = Mathf.Clamp01(1f - playerBaseToUnit); // 0 at enemy base, 1 at player base
                enemyProgress += enemyBaseToUnit;
                if (enemyBaseToUnit > maxEnemyAdvance)
                {
                    maxEnemyAdvance = enemyBaseToUnit;
                }
            }
        }

        if (calculationMode == TerritoryCalculationMode.FurthestTroopAdvance)
        {
            float minRatio = minTerritoryRatio;
            float maxRatio = 1f - minTerritoryRatio;
            float bufRatio = vanguardBuffer / arenaLength;

            if (playerCount == 0 && enemyCount == 0)
            {
                // Default back towards initial ratio when battlefield is empty
                currentRatioTarget = playerTerritoryRatio;
            }
            else if (playerCount > 0 && enemyCount == 0)
            {
                // Only player troops advancing: claim ground up to the foremost player troop!
                float target = maxPlayerAdvance + bufRatio;
                currentRatioTarget = Mathf.Clamp(target, playerTerritoryRatio, maxRatio);
            }
            else if (playerCount == 0 && enemyCount > 0)
            {
                // Only enemies advancing: enemy territory pushes into player's side!
                // Enemy's border reaches (1 - maxEnemyAdvance - bufRatio)
                float target = 1f - (maxEnemyAdvance + bufRatio);
                currentRatioTarget = Mathf.Clamp(target, minRatio, playerTerritoryRatio);
            }
            else
            {
                // Both armies on the field:
                // Player's vanguard reaches maxPlayerAdvance; Enemy's vanguard reaches (1 - maxEnemyAdvance)
                float playerFront = maxPlayerAdvance;
                float enemyFront = 1f - maxEnemyAdvance;

                float clashPoint;
                if (playerFront >= enemyFront)
                {
                    // Armies have met / engaged in battle:
                    // Frontline is dynamically located at their physical battle clash point,
                    // weighted by the local troop density around the clash line!
                    float avgPlayerPos = playerProgress / playerCount;
                    float avgEnemyPos = 1f - (enemyProgress / enemyCount);
                    clashPoint = Mathf.Lerp((playerFront + enemyFront) * 0.5f, (avgPlayerPos + avgEnemyPos) * 0.5f, 0.4f);
                }
                else
                {
                    // Armies are advancing towards each other, border naturally sits halfway between the two vanguards!
                    clashPoint = (playerFront + enemyFront) * 0.5f;
                }

                currentRatioTarget = Mathf.Clamp(clashPoint, minRatio, maxRatio);
            }
        }
        else if (calculationMode == TerritoryCalculationMode.UnitCountRatio)
        {
            if (playerCount == 0 && enemyCount == 0)
                currentRatioTarget = 0.5f;
            else
                currentRatioTarget = Mathf.Clamp((float)playerCount / (playerCount + enemyCount), 0.2f, 0.8f);
        }
        else if (calculationMode == TerritoryCalculationMode.UnitPresence)
        {
            if (playerCount == 0 && enemyCount == 0)
            {
                currentRatioTarget = 0.5f;
            }
            else
            {
                float baseRatio = 0.5f;
                float bodyDiff = (playerCount - enemyCount) * 0.035f;
                float pushDiff = ((playerProgress - enemyProgress) / Mathf.Max(1, playerCount + enemyCount)) * 0.25f;

                currentRatioTarget = Mathf.Clamp(baseRatio + bodyDiff + pushDiff, 0.15f, 0.85f);
            }
        }
    }

    public void BuildBattlefield()
    {
        ClearVisuals();

        visualRoot = new GameObject("Battlefield Visuals");
        visualRoot.transform.SetParent(transform, false);
        visualRoot.transform.localPosition = Vector3.zero;
        visualRoot.transform.localRotation = Quaternion.identity;

        // Check local player perspective
        bool isTeam1 = (GameModeManager.LocalTeamId == 1);

        Color pFill = isTeam1 ? enemyFillColor : playerFillColor;
        pFill.a = isTeam1 ? enemyFillOpacity : playerFillOpacity;

        Color eFill = isTeam1 ? playerFillColor : enemyFillColor;
        eFill.a = isTeam1 ? playerFillOpacity : enemyFillOpacity;

        Color pBorder = isTeam1 ? enemyBorderColor : playerBorderColor;
        Color eBorder = isTeam1 ? playerBorderColor : enemyBorderColor;

        playerFillMaterial = CreateTransparentMaterial(pFill, "PlayerFillMat");
        enemyFillMaterial = CreateTransparentMaterial(eFill, "EnemyFillMat");
        Material playerBorderMat = CreateTransparentMaterial(pBorder, "PlayerBorderMat", true);
        Material enemyBorderMat = CreateTransparentMaterial(eBorder, "EnemyBorderMat", true);
        Material frontlineMat = CreateTransparentMaterial(frontlineColor, "FrontlineMat", true);

        // 1. Zone Fills (subtle ground tint)
        if (showZoneFills)
        {
            playerFillTransform = CreateFlatQuad(visualRoot.transform, "Player Zone Fill", playerFillMaterial);
            enemyFillTransform = CreateFlatQuad(visualRoot.transform, "Enemy Zone Fill", enemyFillMaterial);
        }

        // 2. Borders
        if (showBorders)
        {
            // Player Box Outlines (Bottom, Left, Right)
            playerBottomBorder = CreateBorderBar(visualRoot.transform, "Player Border Bottom", playerBorderMat);
            playerLeftBorder = CreateBorderBar(visualRoot.transform, "Player Border Left", playerBorderMat);
            playerRightBorder = CreateBorderBar(visualRoot.transform, "Player Border Right", playerBorderMat);

            // Enemy Box Outlines (Top, Left, Right)
            enemyTopBorder = CreateBorderBar(visualRoot.transform, "Enemy Border Top", enemyBorderMat);
            enemyLeftBorder = CreateBorderBar(visualRoot.transform, "Enemy Border Left", enemyBorderMat);
            enemyRightBorder = CreateBorderBar(visualRoot.transform, "Enemy Border Right", enemyBorderMat);
        }

        // 3. Frontline Border
        if (showFrontline)
        {
            frontlineBorder = CreateBorderBar(visualRoot.transform, "Frontline Boundary", frontlineMat);
        }

        UpdateLayout(displayedRatio);
        UpdateVisibility();
    }

    /// <summary>
    /// Explicitly control visibility of the battlefield visuals from code.
    /// </summary>
    public void SetVisualsVisible(bool visible)
    {
        externalVisibilityOverride = visible;
        if (visualRoot != null)
        {
            visualRoot.SetActive(visible);
        }
    }

    private bool? externalVisibilityOverride = null;

    private void UpdateVisibility()
    {
        if (visualRoot == null) return;

        if (showOnlyWhileAiming)
        {
            bool isAiming = false;
            if (PowerupManager.Instance != null && PowerupManager.Instance.IsAiming)
            {
                isAiming = true;
            }
            else if (externalVisibilityOverride.HasValue)
            {
                isAiming = externalVisibilityOverride.Value;
            }

            if (visualRoot.activeSelf != isAiming)
            {
                visualRoot.SetActive(isAiming);
            }
        }
        else
        {
            if (!visualRoot.activeSelf)
            {
                visualRoot.SetActive(true);
            }
        }
    }

    private void UpdateLayout(float ratio)
    {
        if (visualRoot == null) return;

        float w = arenaWidth;
        float totalL = arenaLength;
        float t = borderThickness;
        float y = groundYOffset;

        float playerL = totalL * ratio;
        float enemyL = totalL * (1f - ratio);

        // Z coordinates (Player is from -totalL/2 to frontlineZ; Enemy is from frontlineZ to +totalL/2)
        float zMin = -totalL / 2f;
        float zMax = totalL / 2f;
        float frontlineZ = zMin + playerL;

        float playerCenterZ = zMin + (playerL / 2f);
        float enemyCenterZ = frontlineZ + (enemyL / 2f);

        // 1. Ground Fills
        if (playerFillTransform != null)
        {
            playerFillTransform.localPosition = new Vector3(0f, y, playerCenterZ);
            playerFillTransform.localScale = new Vector3(w, playerL, 1f);
        }

        if (enemyFillTransform != null)
        {
            enemyFillTransform.localPosition = new Vector3(0f, y, enemyCenterZ);
            enemyFillTransform.localScale = new Vector3(w, enemyL, 1f);
        }

        // 2. Player Borders
        if (playerBottomBorder != null)
        {
            // Bottom horizontal bar
            playerBottomBorder.localPosition = new Vector3(0f, y + 0.01f, zMin);
            playerBottomBorder.localScale = new Vector3(w + t, t, t);
        }
        if (playerLeftBorder != null)
        {
            // Left vertical side bar
            playerLeftBorder.localPosition = new Vector3(-w / 2f, y + 0.01f, playerCenterZ);
            playerLeftBorder.localScale = new Vector3(t, t, playerL);
        }
        if (playerRightBorder != null)
        {
            // Right vertical side bar
            playerRightBorder.localPosition = new Vector3(w / 2f, y + 0.01f, playerCenterZ);
            playerRightBorder.localScale = new Vector3(t, t, playerL);
        }

        // 3. Enemy Borders
        if (enemyTopBorder != null)
        {
            // Top horizontal bar
            enemyTopBorder.localPosition = new Vector3(0f, y + 0.01f, zMax);
            enemyTopBorder.localScale = new Vector3(w + t, t, t);
        }
        if (enemyLeftBorder != null)
        {
            // Left vertical side bar
            enemyLeftBorder.localPosition = new Vector3(-w / 2f, y + 0.01f, enemyCenterZ);
            enemyLeftBorder.localScale = new Vector3(t, t, enemyL);
        }
        if (enemyRightBorder != null)
        {
            // Right vertical side bar
            enemyRightBorder.localPosition = new Vector3(w / 2f, y + 0.01f, enemyCenterZ);
            enemyRightBorder.localScale = new Vector3(t, t, enemyL);
        }

        // 4. Frontline dividing line
        if (frontlineBorder != null)
        {
            frontlineBorder.localPosition = new Vector3(0f, y + 0.015f, frontlineZ);
            frontlineBorder.localScale = new Vector3(w + t, t * 1.35f, t * 1.35f);
        }
    }

    private Transform CreateFlatQuad(Transform parent, string name, Material mat)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        quad.transform.SetParent(parent, false);
        quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Lie flat on XZ ground

        RemoveCollider(quad);

        Renderer r = quad.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        return quad.transform;
    }

    private Transform CreateBorderBar(Transform parent, string name, Material mat)
    {
        GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = name;
        bar.transform.SetParent(parent, false);
        bar.transform.localRotation = Quaternion.identity;

        RemoveCollider(bar);

        Renderer r = bar.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        return bar.transform;
    }

    private Material CreateTransparentMaterial(Color color, string matName, bool isEmissive = false)
    {
        // Try URP Unlit shader first (matches the Inspector screenshot)
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");

        Material mat = new Material(shader) { name = matName };

        // Configure URP Surface Options: Transparent + Alpha Blending
        if (shader.name.StartsWith("Universal Render Pipeline"))
        {
            // _Surface: 0 = Opaque, 1 = Transparent
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            // _Blend: 0 = Alpha, 1 = Premultiply, 2 = Additive, 3 = Multiply
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            // _ZWrite: 0 = Off (Standard for transparent objects)
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            // _Cull: 0 = Both, 1 = Back, 2 = Front
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f); // Render both sides for quad visibility

            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

            // Set color on URP property (_BaseColor) and standard property (_Color)
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        }
        else if (shader.name == "Standard")
        {
            mat.SetFloat("_Mode", 3f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
            mat.color = color;
        }
        else
        {
            mat.color = color;
        }

        if (isEmissive)
        {
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", color * 1.25f);
        }

        return mat;
    }

    private void RemoveCollider(GameObject obj)
    {
        Collider col = obj.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }
    }

    private void ClearVisuals()
    {
        if (visualRoot != null)
        {
            if (Application.isPlaying) Destroy(visualRoot);
            else DestroyImmediate(visualRoot);
            visualRoot = null;
        }
    }

    #region Territory Bounds & Queries

    /// <summary>
    /// Checks whether a world position is inside the arena's boundaries and belongs to the given team's territory.
    /// Team 0 = Player (Blue border area).
    /// Team 1 = Enemy (Red border area).
    /// </summary>
    public bool IsInTeamTerritory(Vector3 worldPos, int teamId)
    {
        if (teamId == 0) return IsInPlayerTerritory(worldPos);
        return IsInEnemyTerritory(worldPos);
    }

    /// <summary>
    /// Checks whether a world position is within the Player's Blue Border territory.
    /// </summary>
    public bool IsInPlayerTerritory(Vector3 worldPos)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        float halfW = arenaWidth * 0.5f;
        float zMin = -arenaLength * 0.5f;
        float frontlineZ = zMin + (arenaLength * displayedRatio);

        // Allow a slight tolerance margin so drops right against borders are friendly
        float margin = 0.4f;

        if (localPos.x < (-halfW - margin) || localPos.x > (halfW + margin))
            return false;

        return (localPos.z >= (zMin - margin) && localPos.z <= (frontlineZ + margin));
    }

    /// <summary>
    /// Checks whether a world position is within the Enemy's Red Border territory.
    /// </summary>
    public bool IsInEnemyTerritory(Vector3 worldPos)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        float halfW = arenaWidth * 0.5f;
        float zMin = -arenaLength * 0.5f;
        float zMax = arenaLength * 0.5f;
        float frontlineZ = zMin + (arenaLength * displayedRatio);

        float margin = 0.4f;

        if (localPos.x < (-halfW - margin) || localPos.x > (halfW + margin))
            return false;

        return (localPos.z >= (frontlineZ - margin) && localPos.z <= (zMax + margin));
    }

    /// <summary>
    /// Clamps or projects a target world position into the Enemy's Red Border territory.
    /// Useful for Enemy AI targeting.
    /// </summary>
    public Vector3 ClampToEnemyTerritory(Vector3 worldPos, float innerPadding = 0.5f)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        float halfW = Mathf.Max(0.5f, (arenaWidth * 0.5f) - innerPadding);
        float zMin = -arenaLength * 0.5f;
        float zMax = (arenaLength * 0.5f) - innerPadding;
        float frontlineZ = zMin + (arenaLength * displayedRatio) + innerPadding;

        localPos.x = Mathf.Clamp(localPos.x, -halfW, halfW);
        localPos.z = Mathf.Clamp(localPos.z, frontlineZ, zMax);

        return transform.TransformPoint(localPos);
    }

    /// <summary>
    /// Clamps or projects a target world position into the Player's Blue Border territory.
    /// </summary>
    public Vector3 ClampToPlayerTerritory(Vector3 worldPos, float innerPadding = 0.5f)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        float halfW = Mathf.Max(0.5f, (arenaWidth * 0.5f) - innerPadding);
        float zMin = (-arenaLength * 0.5f) + innerPadding;
        float frontlineZ = (-arenaLength * 0.5f) + (arenaLength * displayedRatio) - innerPadding;

        localPos.x = Mathf.Clamp(localPos.x, -halfW, halfW);
        localPos.z = Mathf.Clamp(localPos.z, zMin, Mathf.Max(zMin, frontlineZ));

        return transform.TransformPoint(localPos);
    }

    #endregion

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        ClearVisuals();
    }
}
