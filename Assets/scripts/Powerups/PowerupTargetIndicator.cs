using UnityEngine;
using DG.Tweening;

/// <summary>
/// World-space circular targeting indicator projected onto the battlefield ground.
/// Enhanced Features:
/// - Sleek dual-layer luminous vector ring (crisp ice-white core line + royal sky-blue halo glow).
/// - Dynamic Sonar Hologram Wave expanding periodically from center to perimeter.
/// - Inertial drag physics: holographic ring banks smoothly into drag motion and springs flat when still.
/// - Precision tactical epicenter reticle with rotating kinetic crosshairs and bright diamond pip.
/// - Intelligent Enemy Target Lock-on: detects enemy troops in AOE and tightens reticle with tactile pop punch.
/// - 4 sleek cardinal precision tick marks (N, S, E, W) framing the impact perimeter.
/// - Ultra-low opacity translucent ground fill (~0.025 alpha) with true URP alpha blending.
/// - High-contrast stylized color scheme (Ice White / Sky Blue for friendly targeting, Warning Crimson for invalid).
/// - Zero z-fighting, zero GC allocations per frame in Update.
/// </summary>
[DisallowMultipleComponent]
public class PowerupTargetIndicator : MonoBehaviour
{
    [Header("Indicator Configuration")]
    [SerializeField] private float heightAboveGround = 0.04f;
    [SerializeField] private bool enableBreathing = true;
    [SerializeField, Range(0.5f, 4f)] private float breathingSpeed = 2.4f;
    [SerializeField, Range(0f, 0.06f)] private float breathingAmplitude = 0.018f;

    [Header("Sonar Hologram Wave")]
    [SerializeField] private bool enableSonarWave = true;
    [SerializeField, Range(0.6f, 3f)] private float sonarDuration = 1.35f;

    [Header("Inertial Drag Physics")]
    [SerializeField] private bool enableInertialTilt = true;
    [SerializeField, Range(1f, 12f)] private float maxTiltAngle = 6.0f;
    [SerializeField, Range(5f, 30f)] private float tiltSmoothSpeed = 16f;

    [Header("Ground Fill Opacity")]
    [Tooltip("Enable or disable the subtle translucent ground fill tint.")]
    [SerializeField] private bool enableGroundFill = true;
    [Tooltip("Ultra-low ground fill opacity so ground details and troops remain crystal clear.")]
    [SerializeField, Range(0f, 0.15f)] private float groundFillOpacity = 0.025f;

    [Header("Target Detection")]
    [SerializeField] private bool enableTargetLockFeedback = true;
    [SerializeField] private LayerMask targetLayerMask = ~0;

    [Header("Color Palette")]
    [Tooltip("If true, all friendly targeting uses the iconic Ice-White & Sky-Blue reticle. If false, uses powerup theme color.")]
    [SerializeField] private bool useUnifiedPlayerPalette = true;

    [SerializeField] private Color friendlyCoreColor = new Color(0.85f, 0.96f, 1f, 0.96f);   // Luminous Ice White-Cyan
    [SerializeField] private Color friendlyHaloColor = new Color(0.14f, 0.68f, 1f, 0.50f);   // Vibrant Sky Blue
    [SerializeField] private Color lockOnBrightColor = new Color(0.40f, 0.90f, 1f, 0.98f);   // High-luminance lock-on cyan
    [SerializeField] private Color invalidCoreColor = new Color(1f, 0.88f, 0.88f, 0.98f);    // Warning White-Red
    [SerializeField] private Color invalidHaloColor = new Color(1f, 0.18f, 0.28f, 0.95f);    // Screaming Warning Crimson

    // Geometry child objects
    private GameObject fillDiscObj;
    private GameObject outerRingObj;
    private GameObject haloRingObj;
    private GameObject sonarRingObj;
    private GameObject cardinalTicksRoot;
    private GameObject centerReticleRoot;
    private GameObject centerMiniRingObj;
    private GameObject centerCrosshairRoot;

    // LineRenderers
    private LineRenderer outerCoreLine;
    private LineRenderer haloLine;
    private LineRenderer sonarLine;
    private LineRenderer centerMiniLine;

    // Cardinal tick objects (N, S, E, W)
    private readonly Transform[] cardinalTicks = new Transform[4];

    // Materials
    private Material fillDiscMat;
    private Material coreRingMat;
    private Material haloRingMat;
    private Material sonarRingMat;
    private Material cardinalTicksMat;
    private Material centerCrosshairMat;
    private Material centerPipMat;

    // Procedural assets
    private Mesh fillDiscMesh;

    // Preallocated coordinate arrays (zero GC per frame)
    private const int RingSegments = 80;
    private const int HaloSegments = 64;
    private const int SonarSegments = 64;
    private const int CenterRingSegments = 32;

    private readonly Vector3[] outerRingPoints = new Vector3[RingSegments];
    private readonly Vector3[] haloRingPoints = new Vector3[HaloSegments];
    private readonly Vector3[] sonarRingPoints = new Vector3[SonarSegments];
    private readonly Vector3[] centerRingPoints = new Vector3[CenterRingSegments];

    // Inertial physics state
    private Vector3 lastPosition;
    private Vector3 dragVelocity;

    // Sonar timer
    private float sonarTimer = 0f;

    // Target lock detection state
    private float detectionTimer = 0f;
    private bool hasTargetLock = false;
    private readonly Collider[] overlapColliders = new Collider[16];

    // State
    private float targetRadius = 5f;
    private float currentDisplayRadius = 5f;
    private Color activeColor = Color.cyan;
    private bool isValidDrop = true;
    private bool isVisible = false;
    private Vector3 targetPosition;

    private void Awake()
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName == "MainMenu")
        {
            Destroy(gameObject);
            return;
        }

        BuildVisualHierarchy();
        HideImmediate();
    }

    private void BuildVisualHierarchy()
    {
        // 1. Translucent ground fill disc (true URP transparent material)
        fillDiscMesh = GenerateUnitCircleMesh(RingSegments);
        fillDiscMat = CreateUnlitTransparentMaterial("IndicatorFillMat", new Color(0.14f, 0.68f, 1f, groundFillOpacity));
        fillDiscObj = new GameObject("FillDisc");
        fillDiscObj.transform.SetParent(transform, false);
        fillDiscObj.transform.localPosition = new Vector3(0f, heightAboveGround, 0f);

        MeshFilter mf = fillDiscObj.AddComponent<MeshFilter>();
        mf.sharedMesh = fillDiscMesh;
        MeshRenderer mr = fillDiscObj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = fillDiscMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        // 2. Halo Glow Ring (LineRenderer - sleek 0.04m width, concentric at 96% radius)
        haloRingMat = CreateUnlitTransparentMaterial("HaloRingMat", friendlyHaloColor);
        haloRingObj = new GameObject("HaloRing");
        haloRingObj.transform.SetParent(transform, false);
        haloRingObj.transform.localPosition = new Vector3(0f, heightAboveGround + 0.003f, 0f);

        haloLine = haloRingObj.AddComponent<LineRenderer>();
        haloLine.useWorldSpace = false;
        haloLine.loop = true;
        haloLine.positionCount = HaloSegments;
        haloLine.startWidth = 0.04f;
        haloLine.endWidth = 0.04f;
        haloLine.sharedMaterial = haloRingMat;
        haloLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        haloLine.receiveShadows = false;

        // 3. Sonar Hologram Wave Ring (expanding periodic ripple)
        sonarRingMat = CreateUnlitTransparentMaterial("SonarRingMat", friendlyHaloColor);
        sonarRingObj = new GameObject("SonarRing");
        sonarRingObj.transform.SetParent(transform, false);
        sonarRingObj.transform.localPosition = new Vector3(0f, heightAboveGround + 0.004f, 0f);

        sonarLine = sonarRingObj.AddComponent<LineRenderer>();
        sonarLine.useWorldSpace = false;
        sonarLine.loop = true;
        sonarLine.positionCount = SonarSegments;
        sonarLine.startWidth = 0.03f;
        sonarLine.endWidth = 0.03f;
        sonarLine.sharedMaterial = sonarRingMat;
        sonarLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sonarLine.receiveShadows = false;

        // 4. Primary Core Ring (LineRenderer - sleek 0.075m crisp white-cyan core line)
        coreRingMat = CreateUnlitTransparentMaterial("CoreRingMat", friendlyCoreColor);
        outerRingObj = new GameObject("CoreRing");
        outerRingObj.transform.SetParent(transform, false);
        outerRingObj.transform.localPosition = new Vector3(0f, heightAboveGround + 0.006f, 0f);

        outerCoreLine = outerRingObj.AddComponent<LineRenderer>();
        outerCoreLine.useWorldSpace = false;
        outerCoreLine.loop = true;
        outerCoreLine.positionCount = RingSegments;
        outerCoreLine.startWidth = 0.075f;
        outerCoreLine.endWidth = 0.075f;
        outerCoreLine.sharedMaterial = coreRingMat;
        outerCoreLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outerCoreLine.receiveShadows = false;
        outerCoreLine.numCapVertices = 4;
        outerCoreLine.numCornerVertices = 4;

        // 5. Cardinal tick marks (N, S, E, W) crossing the outer boundary
        cardinalTicksMat = CreateUnlitTransparentMaterial("CardinalTicksMat", friendlyCoreColor);
        cardinalTicksRoot = new GameObject("CardinalTicks");
        cardinalTicksRoot.transform.SetParent(transform, false);
        cardinalTicksRoot.transform.localPosition = Vector3.zero;

        cardinalTicks[0] = CreateFlatBar("Tick_N", cardinalTicksRoot.transform, cardinalTicksMat).transform;
        cardinalTicks[1] = CreateFlatBar("Tick_S", cardinalTicksRoot.transform, cardinalTicksMat).transform;
        cardinalTicks[2] = CreateFlatBar("Tick_E", cardinalTicksRoot.transform, cardinalTicksMat).transform;
        cardinalTicks[3] = CreateFlatBar("Tick_W", cardinalTicksRoot.transform, cardinalTicksMat).transform;

        // 6. Precision Center Reticle Root
        centerReticleRoot = new GameObject("CenterReticle");
        centerReticleRoot.transform.SetParent(transform, false);
        centerReticleRoot.transform.localPosition = new Vector3(0f, heightAboveGround + 0.007f, 0f);

        // 6a. Center mini concentric ring (radius 0.28m, width 0.035m)
        centerMiniRingObj = new GameObject("CenterMiniRing");
        centerMiniRingObj.transform.SetParent(centerReticleRoot.transform, false);
        centerMiniRingObj.transform.localPosition = Vector3.zero;

        centerMiniLine = centerMiniRingObj.AddComponent<LineRenderer>();
        centerMiniLine.useWorldSpace = false;
        centerMiniLine.loop = true;
        centerMiniLine.positionCount = CenterRingSegments;
        centerMiniLine.startWidth = 0.035f;
        centerMiniLine.endWidth = 0.035f;
        centerMiniLine.sharedMaterial = haloRingMat;
        centerMiniLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        centerMiniLine.receiveShadows = false;

        float centerMiniStep = (Mathf.PI * 2f) / CenterRingSegments;
        float centerMiniRadius = 0.28f;
        for (int i = 0; i < CenterRingSegments; i++)
        {
            float a = i * centerMiniStep;
            centerRingPoints[i] = new Vector3(Mathf.Cos(a) * centerMiniRadius, 0f, Mathf.Sin(a) * centerMiniRadius);
        }
        centerMiniLine.SetPositions(centerRingPoints);

        // 6b. Center bright white diamond pip
        centerPipMat = CreateUnlitTransparentMaterial("CenterPipMat", Color.white);
        GameObject pip = CreateFlatBar("CenterPipDiamond", centerReticleRoot.transform, centerPipMat);
        pip.transform.localPosition = new Vector3(0f, 0.002f, 0f);
        pip.transform.localRotation = Quaternion.Euler(90f, 45f, 0f); // 45 deg diamond
        pip.transform.localScale = new Vector3(0.09f, 0.09f, 1f);

        // 6c. Rotating kinetic crosshair arms root
        centerCrosshairRoot = new GameObject("RotatingCrosshairs");
        centerCrosshairRoot.transform.SetParent(centerReticleRoot.transform, false);
        centerCrosshairRoot.transform.localPosition = Vector3.zero;

        centerCrosshairMat = CreateUnlitTransparentMaterial("CenterCrosshairMat", friendlyHaloColor);
        CreateCrosshairArm("Arm_N", centerCrosshairRoot.transform, new Vector3(0f, 0.001f, 0.26f), new Vector3(0.035f, 0.18f, 1f), centerCrosshairMat);
        CreateCrosshairArm("Arm_S", centerCrosshairRoot.transform, new Vector3(0f, 0.001f, -0.26f), new Vector3(0.035f, 0.18f, 1f), centerCrosshairMat);
        CreateCrosshairArm("Arm_E", centerCrosshairRoot.transform, new Vector3(0.26f, 0.001f, 0f), new Vector3(0.18f, 0.035f, 1f), centerCrosshairMat);
        CreateCrosshairArm("Arm_W", centerCrosshairRoot.transform, new Vector3(-0.26f, 0.001f, 0f), new Vector3(0.18f, 0.035f, 1f), centerCrosshairMat);

        ApplyColors();
        UpdateGeometry(currentDisplayRadius);
    }

    private static GameObject CreateFlatBar(string barName, Transform parent, Material mat)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = barName;
        quad.transform.SetParent(parent, false);
        quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        Collider col = quad.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = quad.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }

        return quad;
    }

    private static void CreateCrosshairArm(string armName, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
    {
        GameObject arm = CreateFlatBar(armName, parent, mat);
        arm.transform.localPosition = localPos;
        arm.transform.localScale = localScale;
    }

    /// <summary>
    /// Creates a 100% compliant transparent unlit material for URP and legacy pipelines,
    /// explicitly configuring shader keywords and blend modes so alpha blending works flawlessly.
    /// </summary>
    private static Material CreateUnlitTransparentMaterial(string matName, Color initialColor)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Mobile/Particles/Alpha Blended");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");

        Material mat = new Material(shader) { name = matName };

        if (shader.name.StartsWith("Universal Render Pipeline"))
        {
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // 1 = Transparent
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);     // 0 = Alpha Blend
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);      // Double-sided

            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 30;
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", initialColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", initialColor);
        }
        else if (shader.name == "Standard")
        {
            mat.SetFloat("_Mode", 3f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
            mat.color = initialColor;
        }
        else
        {
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            mat.color = initialColor;
        }

        return mat;
    }

    private static Mesh GenerateUnitCircleMesh(int segments)
    {
        Mesh mesh = new Mesh { name = "IndicatorUnitCircleMesh" };
        Vector3[] vertices = new Vector3[segments + 1];
        int[] triangles = new int[segments * 3];
        Vector2[] uvs = new Vector2[segments + 1];

        vertices[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0.5f);

        float angleStep = (Mathf.PI * 2f) / segments;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * angleStep;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            vertices[i + 1] = new Vector3(cos, 0f, sin);
            uvs[i + 1] = new Vector2((cos + 1f) * 0.5f, (sin + 1f) * 0.5f);

            int triIndex = i * 3;
            triangles[triIndex] = 0;
            triangles[triIndex + 1] = i + 1;
            triangles[triIndex + 2] = (i + 1 < segments) ? (i + 2) : 1;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void Update()
    {
        if (!isVisible) return;

        float dt = Time.deltaTime;

        // 1. Smooth responsive tracking toward raycast hit point
        transform.position = Vector3.Lerp(transform.position, targetPosition, dt * 32f);

        // 2. Inertial Drag Physics Tilt
        if (enableInertialTilt)
        {
            dragVelocity = Vector3.Lerp(dragVelocity, (transform.position - lastPosition) / Mathf.Max(dt, 0.001f), dt * 18f);
            lastPosition = transform.position;

            float tiltZ = Mathf.Clamp(-dragVelocity.x * 0.40f, -maxTiltAngle, maxTiltAngle);
            float tiltX = Mathf.Clamp(dragVelocity.z * 0.40f, -maxTiltAngle, maxTiltAngle);

            Quaternion targetRot = Quaternion.Euler(tiltX, 0f, tiltZ);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, dt * tiltSmoothSpeed);
        }
        else
        {
            transform.localRotation = Quaternion.identity;
        }

        // 3. Kinetic rotation of center crosshairs
        if (centerCrosshairRoot != null)
        {
            centerCrosshairRoot.transform.Rotate(Vector3.up, 18f * dt, Space.Self);
        }

        // 4. Gentle natural breathing pulse
        float breath = 0f;
        if (enableBreathing)
        {
            breath = Mathf.Sin(Time.time * breathingSpeed) * breathingAmplitude;
        }

        currentDisplayRadius = Mathf.Lerp(currentDisplayRadius, targetRadius + breath, dt * 20f);
        UpdateGeometry(currentDisplayRadius);

        // 5. Dynamic Holographic Sonar Radar Wave
        if (enableSonarWave && sonarLine != null)
        {
            sonarTimer += dt;
            float progress = (sonarTimer % sonarDuration) / sonarDuration;
            float sonarRadius = Mathf.Lerp(0.35f, currentDisplayRadius, progress);
            float sonarAlpha = Mathf.Sin(progress * Mathf.PI) * (isValidDrop ? 0.42f : 0.50f);

            float sonarStep = (Mathf.PI * 2f) / SonarSegments;
            for (int i = 0; i < SonarSegments; i++)
            {
                float a = i * sonarStep;
                sonarRingPoints[i] = new Vector3(Mathf.Cos(a) * sonarRadius, 0f, Mathf.Sin(a) * sonarRadius);
            }
            sonarLine.SetPositions(sonarRingPoints);

            Color baseHalo = (isValidDrop ? (hasTargetLock ? lockOnBrightColor : friendlyHaloColor) : invalidHaloColor);
            SetMaterialColor(sonarRingMat, new Color(baseHalo.r, baseHalo.g, baseHalo.b, sonarAlpha));
        }

        // 6. Tactical Enemy Target Lock Detection
        if (enableTargetLockFeedback && isValidDrop)
        {
            detectionTimer += dt;
            if (detectionTimer >= 0.12f)
            {
                detectionTimer = 0f;
                int enemyCount = CheckEnemiesInRange(currentDisplayRadius);
                bool targetAcquired = enemyCount > 0;

                if (targetAcquired != hasTargetLock)
                {
                    hasTargetLock = targetAcquired;
                    OnTargetLockChanged(hasTargetLock);
                }
            }
        }
    }

    private void UpdateGeometry(float radius)
    {
        // 1. Scale ground fill disc
        if (fillDiscObj != null)
        {
            fillDiscObj.SetActive(enableGroundFill && groundFillOpacity > 0.001f);
            fillDiscObj.transform.localScale = new Vector3(radius, 1f, radius);
        }

        // 2. Primary core ring points (80 segments at radius)
        float outerStep = (Mathf.PI * 2f) / RingSegments;
        for (int i = 0; i < RingSegments; i++)
        {
            float a = i * outerStep;
            outerRingPoints[i] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
        }
        if (outerCoreLine != null) outerCoreLine.SetPositions(outerRingPoints);

        // 3. Halo glow ring points (64 segments at 96% radius)
        float haloRadius = radius * 0.96f;
        float haloStep = (Mathf.PI * 2f) / HaloSegments;
        for (int i = 0; i < HaloSegments; i++)
        {
            float a = i * haloStep;
            haloRingPoints[i] = new Vector3(Mathf.Cos(a) * haloRadius, 0f, Mathf.Sin(a) * haloRadius);
        }
        if (haloLine != null) haloLine.SetPositions(haloRingPoints);

        // 4. Cardinal ticks (crossing outer boundary at 0, 90, 180, 270 deg)
        float tickY = heightAboveGround + 0.008f;
        float tickSpan = hasTargetLock ? 0.24f : 0.20f;
        float tickWidth = hasTargetLock ? 0.055f : 0.045f;

        if (cardinalTicks[0] != null) // North
        {
            cardinalTicks[0].localPosition = new Vector3(0f, tickY, radius);
            cardinalTicks[0].localScale = new Vector3(tickWidth, tickSpan, 1f);
        }
        if (cardinalTicks[1] != null) // South
        {
            cardinalTicks[1].localPosition = new Vector3(0f, tickY, -radius);
            cardinalTicks[1].localScale = new Vector3(tickWidth, tickSpan, 1f);
        }
        if (cardinalTicks[2] != null) // East
        {
            cardinalTicks[2].localPosition = new Vector3(radius, tickY, 0f);
            cardinalTicks[2].localScale = new Vector3(tickSpan, tickWidth, 1f);
        }
        if (cardinalTicks[3] != null) // West
        {
            cardinalTicks[3].localPosition = new Vector3(-radius, tickY, 0f);
            cardinalTicks[3].localScale = new Vector3(tickSpan, tickWidth, 1f);
        }
    }

    private int CheckEnemiesInRange(float radius)
    {
        int count = 0;
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, radius, overlapColliders, targetLayerMask, QueryTriggerInteraction.Ignore);
        int localTeam = GameModeManager.LocalTeamId;

        for (int i = 0; i < hitCount; i++)
        {
            PeasantUnitController unit = overlapColliders[i].GetComponentInParent<PeasantUnitController>();
            if (unit != null && !unit.IsDead && unit.TeamId != localTeam)
            {
                count++;
            }
        }
        return count;
    }

    private void OnTargetLockChanged(bool locked)
    {
        ApplyColors();

        if (centerReticleRoot != null)
        {
            centerReticleRoot.transform.DOKill(true);
            if (locked)
            {
                // Tactile juicy pop punch when locking onto enemy units
                centerReticleRoot.transform.DOPunchScale(Vector3.one * 0.25f, 0.22f, 8, 0.6f);
            }
            else
            {
                centerReticleRoot.transform.localScale = Vector3.one;
            }
        }
    }

    public void Show(float radius, Color themeColor)
    {
        targetRadius = radius;
        currentDisplayRadius = radius;
        activeColor = themeColor;
        isValidDrop = true;
        isVisible = true;
        hasTargetLock = false;
        sonarTimer = 0f;
        lastPosition = transform.position;
        dragVelocity = Vector3.zero;

        gameObject.SetActive(true);
        ApplyColors();
        UpdateGeometry(currentDisplayRadius);

        // Juicy pop-in scale punch
        transform.DOKill();
        transform.localScale = Vector3.one * 0.15f;
        transform.DOScale(1f, 0.20f).SetEase(Ease.OutBack, 1.8f);
    }

    public void Hide()
    {
        if (!isVisible) return;
        isVisible = false;
        hasTargetLock = false;

        transform.DOKill();
        transform.DOScale(0.02f, 0.10f).SetEase(Ease.InBack).OnComplete(() =>
        {
            gameObject.SetActive(false);
            transform.localScale = Vector3.one;
            transform.localRotation = Quaternion.identity;
        });
    }

    private void HideImmediate()
    {
        isVisible = false;
        hasTargetLock = false;
        transform.DOKill();
        gameObject.SetActive(false);
        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;
    }

    public void UpdatePosition(Vector3 worldPoint, bool valid)
    {
        targetPosition = worldPoint;
        if (!isVisible)
        {
            transform.position = targetPosition;
            lastPosition = targetPosition;
        }

        if (isValidDrop != valid)
        {
            isValidDrop = valid;
            ApplyColors();

            if (!valid)
            {
                // Rejection punch when entering forbidden territory
                transform.DOKill(true);
                transform.DOPunchScale(new Vector3(0.06f, -0.05f, 0.06f), 0.16f, 8, 0.5f);
            }
        }
    }

    private void ApplyColors()
    {
        Color coreCol;
        Color haloCol;

        if (isValidDrop)
        {
            if (useUnifiedPlayerPalette)
            {
                coreCol = hasTargetLock ? Color.white : friendlyCoreColor;
                haloCol = hasTargetLock ? lockOnBrightColor : friendlyHaloColor;
            }
            else
            {
                haloCol = GetVibrantColor(activeColor);
                coreCol = Color.Lerp(Color.white, haloCol, hasTargetLock ? 0.10f : 0.25f);
            }
        }
        else
        {
            coreCol = invalidCoreColor;
            haloCol = invalidHaloColor;
        }

        Color fillCol = new Color(haloCol.r, haloCol.g, haloCol.b, isValidDrop ? (hasTargetLock ? groundFillOpacity * 1.35f : groundFillOpacity) : (groundFillOpacity * 1.5f));
        Color pipCol = isValidDrop ? Color.white : new Color(1f, 0.40f, 0.40f, 0.98f);

        SetMaterialColor(coreRingMat, coreCol);
        SetMaterialColor(cardinalTicksMat, coreCol);
        SetMaterialColor(haloRingMat, haloCol);
        SetMaterialColor(fillDiscMat, fillCol);
        SetMaterialColor(centerCrosshairMat, haloCol);
        SetMaterialColor(centerPipMat, pipCol);
        SetMaterialColor(sonarRingMat, haloCol);
    }

    private static Color GetVibrantColor(Color baseColor)
    {
        Color.RGBToHSV(baseColor, out float h, out float s, out float v);
        s = Mathf.Clamp(s, 0.70f, 0.95f);
        v = 1.0f;
        Color res = Color.HSVToRGB(h, s, v);
        res.a = 1f;
        return res;
    }

    private static void SetMaterialColor(Material mat, Color col)
    {
        if (mat == null) return;
        mat.color = col;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);
    }

    private void OnDestroy()
    {
        if (coreRingMat != null) Destroy(coreRingMat);
        if (haloRingMat != null) Destroy(haloRingMat);
        if (sonarRingMat != null) Destroy(sonarRingMat);
        if (cardinalTicksMat != null) Destroy(cardinalTicksMat);
        if (fillDiscMat != null) Destroy(fillDiscMat);
        if (centerCrosshairMat != null) Destroy(centerCrosshairMat);
        if (centerPipMat != null) Destroy(centerPipMat);
        if (fillDiscMesh != null) Destroy(fillDiscMesh);
    }
}
