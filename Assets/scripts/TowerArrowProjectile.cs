using UnityEngine;
using DG.Tweening;

/// <summary>
/// High-polish ballistic arrow projectile fired by Defense Towers.
/// Features true parabolic trajectory curving, tip tangent rotation,
/// dynamic trail visuals, spatial audio, particle hit sparks,
/// target DOTween squash/punch impact reactions, and realistic arrow sticking with elastic wobble.
/// </summary>
public class TowerArrowProjectile : MonoBehaviour
{
    [Header("Flight & Ballistics")]
    [SerializeField, Min(10f)] private float speed = 26f;
    [SerializeField, Min(1f)] private float arcHeight = 2.8f;
    [SerializeField, Min(0.1f)] private float maxFlightTime = 4f;

    [Header("Impact Polish")]
    [SerializeField] private AudioClip hitSound;
    [SerializeField, Range(0f, 1f)] private float hitVolume = 0.85f;
    [Tooltip("Blood VFX prefab spawned when tower arrow strikes a player or character unit.")]
    [SerializeField] private GameObject bloodHitVfxPrefab;
    [Tooltip("Machine Hit VFX prefab spawned when tower arrow strikes a siege machine (wood/sparks effect).")]
    [SerializeField] private GameObject machineHitVfxPrefab;
    [Tooltip("Dust VFX prefab spawned when tower arrow strikes a building, wall, or ground.")]
    [SerializeField] private GameObject dustHitVfxPrefab;
    [SerializeField] private GameObject hitVfxPrefab;
    [SerializeField] private bool stickOnImpact = true;
    [SerializeField] private float stickDuration = 3.5f;

    private static GameObject cachedDefaultBloodVfx;
    private static GameObject cachedDefaultMachineVfx;
    private static GameObject cachedDefaultDustVfx;

    private void Awake()
    {
        EnsureVfxAssigned();
    }

    private void Reset()
    {
        EnsureVfxAssigned();
    }

    private void EnsureVfxAssigned()
    {
        if (bloodHitVfxPrefab == null)
        {
            if (cachedDefaultBloodVfx == null)
            {
                cachedDefaultBloodVfx = Resources.Load<GameObject>("Effects/ArrowHit_Blood");
            }
            if (cachedDefaultBloodVfx == null)
            {
                cachedDefaultBloodVfx = RuntimeAssets.LoadAssetAtPath<GameObject>("Assets/PolygonParticles/Prefabs/FX_BloodSplat_01.prefab");
                if (cachedDefaultBloodVfx == null)
                    cachedDefaultBloodVfx = RuntimeAssets.LoadAssetAtPath<GameObject>("Assets/PolygonParticles/Prefabs/FX_BloodSplat_Small_01.prefab");
            }
            bloodHitVfxPrefab = cachedDefaultBloodVfx;
        }

        if (machineHitVfxPrefab == null)
        {
            if (cachedDefaultMachineVfx == null)
            {
                cachedDefaultMachineVfx = Resources.Load<GameObject>("Effects/ArrowHit_Wood");
            }
            if (cachedDefaultMachineVfx == null)
            {
                cachedDefaultMachineVfx = RuntimeAssets.LoadAssetAtPath<GameObject>("Assets/PolygonParticles/Prefabs/FX_Impact_Wood_01.prefab");
                if (cachedDefaultMachineVfx == null)
                    cachedDefaultMachineVfx = RuntimeAssets.LoadAssetAtPath<GameObject>("Assets/PolygonParticles/Prefabs/FX_Impact_Metal_01.prefab");
                if (cachedDefaultMachineVfx == null)
                    cachedDefaultMachineVfx = RuntimeAssets.LoadAssetAtPath<GameObject>("Assets/PolygonParticles/Prefabs/FX_Sparks_01.prefab");
            }
            machineHitVfxPrefab = cachedDefaultMachineVfx;
        }

        if (dustHitVfxPrefab == null)
        {
            if (cachedDefaultDustVfx == null)
            {
                cachedDefaultDustVfx = Resources.Load<GameObject>("Effects/ArrowHit_Dust");
            }
            if (cachedDefaultDustVfx == null)
            {
                cachedDefaultDustVfx = RuntimeAssets.LoadAssetAtPath<GameObject>("Assets/PolygonParticles/Prefabs/FX_Dust_Small_01.prefab");
            }
            dustHitVfxPrefab = cachedDefaultDustVfx;
        }
    }

    private Transform targetTransform;
    private Vector3 targetPos;
    private Vector3 startPos;
    private float damage;
    private int shooterTeamId;
    private bool isNetworkProxy;
    private float flightTime;
    private float totalFlightDuration;
    private bool isInitialized = false;
    private bool hasHit = false;
    private TrailRenderer trail;

    public void Launch(Transform target, Vector3 targetCoordinate, float damageAmount, int teamId, bool proxyOnly = false, GameObject customVfx = null, AudioClip customHitSound = null, GameObject customBloodVfx = null, GameObject customDustVfx = null, GameObject customMachineVfx = null)
    {
        this.targetTransform = target;
        this.targetPos = target != null ? target.position + Vector3.up * 1.0f : targetCoordinate;
        this.startPos = transform.position;
        this.damage = damageAmount;
        this.shooterTeamId = teamId;
        this.isNetworkProxy = proxyOnly;

        if (customVfx != null) this.hitVfxPrefab = customVfx;
        if (customHitSound != null) this.hitSound = customHitSound;
        if (customBloodVfx != null) this.bloodHitVfxPrefab = customBloodVfx;
        if (customDustVfx != null) this.dustHitVfxPrefab = customDustVfx;
        if (customMachineVfx != null) this.machineHitVfxPrefab = customMachineVfx;

        EnsureVfxAssigned();

        float distance = Vector3.Distance(startPos, targetPos);
        totalFlightDuration = Mathf.Max(0.08f, distance / speed);
        // Dynamic arc height scaling based on distance
        arcHeight = Mathf.Clamp(distance * 0.12f, 1.8f, 4.5f);

        flightTime = 0f;
        isInitialized = true;

        SetupTrail();

        // Initial orientation
        Vector3 initialDir = targetPos - startPos;
        if (initialDir.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.FromToRotation(Vector3.down, initialDir.normalized);
        }

        Destroy(gameObject, maxFlightTime + stickDuration + 1f);
    }

    private void SetupTrail()
    {
        trail = GetComponent<TrailRenderer>();
        if (trail == null) trail = gameObject.AddComponent<TrailRenderer>();

        trail.time = 0.25f;
        trail.startWidth = 0.12f;
        trail.endWidth = 0.01f;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Clean white/gold trail gradient
        Gradient grad = new Gradient();
        Color headCol = (shooterTeamId == 0) ? new Color(0.2f, 0.7f, 1f, 0.8f) : new Color(1f, 0.4f, 0.2f, 0.8f);
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(headCol, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        trail.colorGradient = grad;

        // Performance: shared trail material (colour comes from the gradient), instead of a new
        // Material + Shader.Find for every tower arrow.
        if (s_sharedTrailMaterial == null)
            s_sharedTrailMaterial = new Material(Shader.Find("Sprites/Default")) { name = "TowerArrowTrail (shared)" };
        trail.sharedMaterial = s_sharedTrailMaterial;
    }

    private static Material s_sharedTrailMaterial;

    private void Update()
    {
        if (!isInitialized || hasHit) return;

        flightTime += Time.deltaTime;
        float progress = Mathf.Clamp01(flightTime / totalFlightDuration);

        // Update target position if living target moves
        if (targetTransform != null)
        {
            targetPos = targetTransform.position + Vector3.up * 1.0f;
        }

        // Parabolic arc position: 4 * h * t * (1 - t)
        float arcOffset = 4f * arcHeight * progress * (1f - progress);
        Vector3 currentPos = Vector3.Lerp(startPos, targetPos, progress);
        currentPos.y += arcOffset;

        Vector3 prevPos = transform.position;
        Vector3 delta = currentPos - prevPos;

        if (delta.sqrMagnitude > 0.00001f)
        {
            // Point arrowhead (-Y) along velocity
            transform.rotation = Quaternion.FromToRotation(Vector3.down, delta.normalized);

            // Raycast forward to prevent passing through solid target or ground
            if (Physics.Raycast(prevPos, delta.normalized, out RaycastHit hit, delta.magnitude + 0.1f, ~0, QueryTriggerInteraction.Ignore))
            {
                // Check if hit is valid target
                PeasantUnitController hitUnit = hit.collider.GetComponentInParent<PeasantUnitController>();
                PeasantObjectiveTarget hitObj = hit.collider.GetComponentInParent<PeasantObjectiveTarget>();

                if (hitUnit != null && !hitUnit.IsDead && hitUnit.TeamId != shooterTeamId)
                {
                    transform.position = hit.point;
                    ExecuteImpact(hitUnit.transform, hit.point, hitUnit, null);
                    return;
                }
                else if (hitObj != null && !hitObj.IsDestroyed && hitObj.TeamId != shooterTeamId)
                {
                    transform.position = hit.point;
                    ExecuteImpact(hitObj.transform, hit.point, null, hitObj);
                    return;
                }
            }
        }

        transform.position = currentPos;

        // Destination reached
        if (progress >= 1f || Vector3.Distance(transform.position, targetPos) < 0.4f)
        {
            ExecuteImpact(targetTransform, targetPos, targetTransform?.GetComponentInParent<PeasantUnitController>(), targetTransform?.GetComponentInParent<PeasantObjectiveTarget>());
        }
    }

    private void ExecuteImpact(Transform victimTransform, Vector3 impactPoint, PeasantUnitController victimUnit, PeasantObjectiveTarget victimObj)
    {
        if (hasHit) return;
        hasHit = true;

        if (trail != null)
        {
            trail.autodestruct = false;
        }

        // 1. Audio Feedback
        if (hitSound != null)
        {
            KoSAudio.PlayClipAtPoint(hitSound, impactPoint, hitVolume, "arrow_hit");
        }

        bool isMachine = victimUnit != null && victimUnit.IsMachine;
        bool isBlood = victimUnit != null && !victimUnit.IsMachine;

        // 2. Particle Hit VFX (Blood for players/units, Wood/Sparks for machines, Dust for buildings/environment)
        GameObject vfxToSpawn = null;
        if (victimUnit != null && victimUnit.HitVfxPrefab != null)
        {
            vfxToSpawn = victimUnit.HitVfxPrefab;
        }
        else if (isMachine)
        {
            vfxToSpawn = machineHitVfxPrefab;
        }
        else if (isBlood)
        {
            vfxToSpawn = bloodHitVfxPrefab;
        }
        else
        {
            vfxToSpawn = dustHitVfxPrefab;
        }

        if (vfxToSpawn == null) vfxToSpawn = hitVfxPrefab;

        if (vfxToSpawn != null)
        {
            GameObject fx = Instantiate(vfxToSpawn, impactPoint, (isBlood || isMachine) ? transform.rotation : Quaternion.identity);
            Destroy(fx, 2.5f);
        }

        // 3. Authoritative Combat Damage
        if (!isNetworkProxy)
        {
            if (victimUnit != null && !victimUnit.IsDead && victimUnit.TeamId != shooterTeamId)
            {
                if (!victimUnit.IsShieldImmune && !ShieldDomeEffect.IsProtectedByAnyDome(victimUnit.transform.position, victimUnit.TeamId))
                {
                    victimUnit.TakeDamage(damage, null);
                }

                // SQUASH EFFECT REMOVED: Arrow hits do not punch-scale/squash the victim unit
            }
            else if (victimObj != null && !victimObj.IsDestroyed && victimObj.TeamId != shooterTeamId)
            {
                if (!victimObj.IsShieldImmune && !ShieldDomeEffect.IsProtectedByAnyDome(victimObj.TargetPoint.position, victimObj.TeamId))
                {
                    victimObj.TakeDamage(damage, null);
                }
            }
        }

        // 4. Arrow Sticking & DOTween Elastic Wobble
        if (stickOnImpact)
        {
            if (victimTransform != null)
            {
                transform.SetParent(victimTransform);
            }

            // Elastic wobble vibration as the arrow lodges in wood/stone/flesh
            transform.DOShakeRotation(0.28f, 22f, 16, 90f);

            // Smooth shrink and despawn after stick duration
            DOVirtual.DelayedCall(stickDuration, () =>
            {
                if (this != null && gameObject != null)
                {
                    transform.DOScale(Vector3.zero, 0.35f)
                        .SetEase(Ease.InBack)
                        .OnComplete(() => Destroy(gameObject));
                }
            });
        }
        else
        {
            Destroy(gameObject, 0.2f);
        }
    }
}
