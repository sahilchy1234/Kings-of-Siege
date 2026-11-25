using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Protective energy dome that shields friendly units within its radius.
/// Provides damage immunity / heavy damage reduction, continuous health regeneration,
/// and deflects or dissolves incoming enemy projectiles.
/// </summary>
public class ShieldDomeEffect : MonoBehaviour
{
    [Header("Dome Properties")]
    [SerializeField] private float radius = 6.0f;
    [SerializeField] private float duration = 12.0f;
    [SerializeField] private float healPerSecond = 12f;
    [SerializeField, Range(0f, 1f)] private float damageReduction = 0.85f; // 85% damage reduction (or immunity)
    [SerializeField] private bool grantCompleteImmunity = true;

    [Header("Repel Enemies")]
    [Tooltip("If true, enemies cannot walk through the dome wall and get pushed backward away from the perimeter.")]
    [SerializeField] private bool repelEnemies = true;
    [SerializeField] private float repelForce = 6.5f;

    [Header("Visuals & Audio")]
    [SerializeField] private Color domeColor = new Color(0f, 0.85f, 1f, 0.35f);
    [SerializeField] private AudioClip activateClip;

    private static readonly List<ShieldDomeEffect> activeDomes = new List<ShieldDomeEffect>();
    public static IReadOnlyList<ShieldDomeEffect> ActiveDomes => activeDomes;

    private AudioSource audioSource;
    private int playerTeamId = 0;
    public int TeamId => playerTeamId;
    public float Radius => radius;
    private readonly HashSet<PeasantUnitController> shieldedUnits = new HashSet<PeasantUnitController>();
    private readonly HashSet<PeasantObjectiveTarget> shieldedObjectives = new HashSet<PeasantObjectiveTarget>();
    private readonly HashSet<PeasantUnitController> _currentInsideUnits = new HashSet<PeasantUnitController>();
    private readonly HashSet<PeasantObjectiveTarget> _currentInsideObjs = new HashSet<PeasantObjectiveTarget>();
    private PeasantObjectiveTarget[] _cachedObjectives;

    private GameObject domeMeshObj;
    private Material domeMaterial;
    private LineRenderer perimeterRing;
    private float lifetimeTimer;

    public void Initialize(Vector3 center, float domeRadius, int playerTeam = 0,
        AudioClip customActivateAudio = null, float customDuration = 0f, float customHealPerSec = 0f)
    {
        transform.position = center;
        this.radius = domeRadius;
        this.playerTeamId = playerTeam;
        if (customActivateAudio != null) activateClip = customActivateAudio;
        if (customDuration > 0f) this.duration = customDuration;
        if (customHealPerSec > 0f) this.healPerSecond = customHealPerSec;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0.5f;

        BuildDomeVisuals();

        if (!activeDomes.Contains(this))
            activeDomes.Add(this);

        if (audioSource != null)
        {
            KoSAudio.PlayOneShot(audioSource, activateClip, 0.85f, "shield_up");
        }

        lifetimeTimer = duration;
        StartCoroutine(DomeLifetimeRoutine());
    }

    /// <summary>
    /// Checks whether a given position and team is currently protected by an active shield dome.
    /// </summary>
    public static bool IsProtectedByAnyDome(Vector3 worldPos, int teamId)
    {
        for (int i = 0; i < activeDomes.Count; i++)
        {
            ShieldDomeEffect dome = activeDomes[i];
            if (dome == null) continue;
            if (dome.TeamId == teamId)
            {
                float d = Vector3.Distance(dome.transform.position, worldPos);
                if (d <= dome.Radius + 0.35f)
                    return true;
            }
        }
        return false;
    }

    private void BuildDomeVisuals()
    {
        // 1. Procedural Hemisphere Mesh
        domeMeshObj = new GameObject("EnergyDomeMesh");
        domeMeshObj.transform.SetParent(transform, false);
        domeMeshObj.transform.localPosition = Vector3.zero;

        MeshFilter mf = domeMeshObj.AddComponent<MeshFilter>();
        MeshRenderer mr = domeMeshObj.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        mf.sharedMesh = GenerateHemisphereMesh(radius, 24, 16);

        domeMaterial = CreateDomeMaterial();
        mr.material = domeMaterial;

        // 2. Glowing perimeter ring on ground
        perimeterRing = gameObject.AddComponent<LineRenderer>();
        perimeterRing.useWorldSpace = false;
        perimeterRing.loop = true;
        int ringSegs = 64;
        perimeterRing.positionCount = ringSegs;
        perimeterRing.startWidth = 0.16f;
        perimeterRing.endWidth = 0.16f;
        perimeterRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        perimeterRing.receiveShadows = false;

        Material ringMat = new Material(domeMaterial) { name = "DomeRingMat" };
        ringMat.color = new Color(domeColor.r, domeColor.g, domeColor.b, 0.9f);
        perimeterRing.material = ringMat;

        Vector3[] ringPts = new Vector3[ringSegs];
        float step = (Mathf.PI * 2f) / ringSegs;
        for (int i = 0; i < ringSegs; i++)
        {
            float a = i * step;
            ringPts[i] = new Vector3(Mathf.Cos(a) * radius, 0.05f, Mathf.Sin(a) * radius);
        }
        perimeterRing.SetPositions(ringPts);

        // Juicy DOTween elastic pop in animation
        domeMeshObj.transform.localScale = Vector3.zero;
        domeMeshObj.transform.DOScale(Vector3.one, 0.38f).SetEase(Ease.OutBack, 2.2f);
    }

    private Material CreateDomeMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader) { name = "ShieldDomeEnergyMat" };
        mat.SetFloat("_Surface", 1);
        mat.SetFloat("_Blend", 0);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 30;

        mat.color = domeColor;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", domeColor);
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", domeColor * 1.5f);

        return mat;
    }

    private Mesh GenerateHemisphereMesh(float r, int longitudeSegments, int latitudeSegments)
    {
        Mesh mesh = new Mesh { name = "ShieldDomeHemisphere" };

        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        // Top pole
        verts.Add(new Vector3(0f, r, 0f));
        uvs.Add(new Vector2(0.5f, 1f));

        for (int lat = 1; lat <= latitudeSegments; lat++)
        {
            float phi = (Mathf.PI * 0.5f) * (1f - (float)lat / latitudeSegments);
            float y = Mathf.Sin(phi) * r;
            float cosPhi = Mathf.Cos(phi) * r;

            for (int lon = 0; lon <= longitudeSegments; lon++)
            {
                float theta = (Mathf.PI * 2f) * ((float)lon / longitudeSegments);
                float x = Mathf.Cos(theta) * cosPhi;
                float z = Mathf.Sin(theta) * cosPhi;

                verts.Add(new Vector3(x, Mathf.Max(0f, y), z));
                uvs.Add(new Vector2((float)lon / longitudeSegments, (float)lat / latitudeSegments));
            }
        }

        // Top cap triangles
        for (int lon = 0; lon < longitudeSegments; lon++)
        {
            tris.Add(0);
            tris.Add(lon + 1);
            tris.Add(lon + 2);
        }

        // Body quads
        int stride = longitudeSegments + 1;
        for (int lat = 0; lat < latitudeSegments - 1; lat++)
        {
            for (int lon = 0; lon < longitudeSegments; lon++)
            {
                int curr = 1 + lat * stride + lon;
                int next = curr + stride;

                tris.Add(curr);
                tris.Add(next);
                tris.Add(curr + 1);

                tris.Add(curr + 1);
                tris.Add(next);
                tris.Add(next + 1);
            }
        }

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();

        return mesh;
    }

    private void Update()
    {
        // Shimmering dome rotation and gentle pulse
        if (domeMeshObj != null)
        {
            domeMeshObj.transform.Rotate(Vector3.up, 12f * Time.deltaTime, Space.Self);
        }

        ProtectAndHealAllies();
        ProtectObjectives();
        RepelEnemyUnits();
        InterceptProjectiles();
    }

    private void ProtectAndHealAllies()
    {
        IReadOnlyList<PeasantUnitController> units = null;
        if (BattleAIManager.Instance != null)
            units = BattleAIManager.Instance.GetAllLivingUnits();

        HashSet<PeasantUnitController> currentInside = _currentInsideUnits;
        currentInside.Clear();

        if (units != null)
        {
            for (int i = 0; i < units.Count; i++)
            {
                PeasantUnitController unit = units[i];
                if (unit == null || unit.IsDead) continue;
                if (unit.TeamId != playerTeamId) continue; // Only shield player/allies

                float dist = Vector3.Distance(transform.position, unit.transform.position);
                if (dist <= radius)
                {
                    currentInside.Add(unit);

                    // Apply protection
                    if (grantCompleteImmunity)
                    {
                        unit.IsShieldImmune = true;
                    }
                    else
                    {
                        unit.DamageTakenMultiplier = 1f - damageReduction;
                    }

                    // Continuous healing
                    if (healPerSecond > 0f)
                    {
                        unit.Heal(healPerSecond * Time.deltaTime);
                    }
                }
            }
        }
        else
        {
            // Fallback search
            PeasantUnitController[] all = FindObjectsByType<PeasantUnitController>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                PeasantUnitController unit = all[i];
                if (unit == null || unit.IsDead || unit.TeamId != playerTeamId) continue;

                float dist = Vector3.Distance(transform.position, unit.transform.position);
                if (dist <= radius)
                {
                    currentInside.Add(unit);
                    if (grantCompleteImmunity) unit.IsShieldImmune = true;
                    else unit.DamageTakenMultiplier = 1f - damageReduction;
                    if (healPerSecond > 0f) unit.Heal(healPerSecond * Time.deltaTime);
                }
            }
        }

        // Remove buffs from units that have left the dome
        foreach (var unit in shieldedUnits)
        {
            if (unit != null && !currentInside.Contains(unit))
            {
                unit.IsShieldImmune = false;
                unit.DamageTakenMultiplier = 1f;
            }
        }

        shieldedUnits.Clear();
        foreach (var u in currentInside)
        {
            shieldedUnits.Add(u);
        }
    }

    private void ProtectObjectives()
    {
        if (_cachedObjectives == null)
            _cachedObjectives = FindObjectsByType<PeasantObjectiveTarget>(FindObjectsSortMode.None);
        PeasantObjectiveTarget[] objectives = _cachedObjectives;
        HashSet<PeasantObjectiveTarget> currentObjsInside = _currentInsideObjs;
        currentObjsInside.Clear();

        for (int i = 0; i < objectives.Length; i++)
        {
            PeasantObjectiveTarget obj = objectives[i];
            if (obj == null || obj.IsDestroyed || obj.TeamId != playerTeamId) continue;

            float dist = Vector3.Distance(transform.position, obj.TargetPoint.position);
            if (dist <= radius + 1.5f)
            {
                currentObjsInside.Add(obj);
                obj.IsShieldImmune = true;
            }
        }

        foreach (var obj in shieldedObjectives)
        {
            if (obj != null && !currentObjsInside.Contains(obj))
            {
                obj.IsShieldImmune = false;
            }
        }

        shieldedObjectives.Clear();
        foreach (var o in currentObjsInside)
        {
            shieldedObjectives.Add(o);
        }
    }

    private void RepelEnemyUnits()
    {
        if (!repelEnemies) return;

        IReadOnlyList<PeasantUnitController> units = null;
        if (BattleAIManager.Instance != null)
            units = BattleAIManager.Instance.GetAllLivingUnits();

        if (units == null) return;

        Vector3 domeCenter = transform.position;
        float dt = Time.deltaTime;

        for (int i = 0; i < units.Count; i++)
        {
            PeasantUnitController enemy = units[i];
            if (enemy == null || enemy.IsDead || enemy.TeamId == playerTeamId || enemy.IsStationary) continue;

            Vector3 enemyPos = enemy.transform.position;
            Vector3 diff = enemyPos - domeCenter;
            diff.y = 0f;
            float dist = diff.magnitude;

            // If enemy is inside or pushing into the dome boundary
            if (dist < radius)
            {
                Vector3 pushDir = dist > 0.05f ? (diff / dist) : transform.forward;
                float penetration = (radius - dist) + 0.5f;
                Vector3 nudge = pushDir * (repelForce * penetration * dt);

                if (enemy.Agent != null && enemy.Agent.enabled && enemy.Agent.isOnNavMesh)
                {
                    enemy.Agent.Move(nudge);
                }
                else
                {
                    enemy.transform.position += nudge;
                }
            }
        }
    }

    private static readonly Collider[] _arrowOverlapBuffer = new Collider[32];

    private void InterceptProjectiles()
    {
        // OverlapSphere avoids a full scene scan every frame
        int count = Physics.OverlapSphereNonAlloc(transform.position, radius, _arrowOverlapBuffer, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Collider col = _arrowOverlapBuffer[i];
            if (col == null) continue;
            ArrowProjectile arrow = col.GetComponent<ArrowProjectile>();
            if (arrow == null) continue;

            if (domeMeshObj != null)
                domeMeshObj.transform.DOPunchScale(Vector3.one * 0.04f, 0.12f, 6, 0.5f);
            CreateDeflectionSpark(arrow.transform.position);
            Destroy(arrow.gameObject);
        }
    }

    private void CreateDeflectionSpark(Vector3 pos)
    {
        GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        spark.transform.position = pos;
        spark.transform.localScale = Vector3.one * 0.3f;
        Collider c = spark.GetComponent<Collider>();
        if (c != null) Destroy(c);
        Renderer r = spark.GetComponent<Renderer>();
        if (r != null) r.material.color = Color.white;
        Destroy(spark, 0.15f);
    }

    private IEnumerator DomeLifetimeRoutine()
    {
        yield return new WaitForSeconds(Mathf.Max(0.5f, duration - 1.0f));

        // Fade out
        float elapsed = 0f;
        Color startCol = domeColor;
        while (elapsed < 1.0f)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(startCol.a, 0f, elapsed / 1.0f);
            if (domeMaterial != null)
            {
                Color c = new Color(startCol.r, startCol.g, startCol.b, alpha);
                domeMaterial.color = c;
            }
            yield return null;
        }

        CleanupAndDestroy();
    }

    private void CleanupAndDestroy()
    {
        activeDomes.Remove(this);

        // Restore all shielded units
        foreach (var unit in shieldedUnits)
        {
            if (unit != null)
            {
                unit.IsShieldImmune = false;
                unit.DamageTakenMultiplier = 1f;
            }
        }
        shieldedUnits.Clear();

        foreach (var obj in shieldedObjectives)
        {
            if (obj != null)
            {
                obj.IsShieldImmune = false;
            }
        }
        shieldedObjectives.Clear();

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        CleanupAndDestroy();
    }
}

