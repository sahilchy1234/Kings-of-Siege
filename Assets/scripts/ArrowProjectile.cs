using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;   
/// <summary>
/// High-polish ballistic projectile launched by ranged Archer and Ballista units.
/// Features true parabolic trajectory curving, tip tangent rotation,
/// dynamic team-tinted speed trail, spatial hit sound with organic pitch variation,
/// particle impact VFX, target DOTween squash/punch impact reactions,
/// and realistic arrow sticking with elastic rotation wobble.
/// </summary>
[DisallowMultipleComponent]
public class ArrowProjectile : MonoBehaviour
{
    public enum ModelForwardAxis
    {
        NegativeY, // Tip points down (-Y) in prefab view, like Arrow_B
        PositiveY, // Tip points up (+Y)
        PositiveZ, // Tip points forward (+Z standard Unity)
        NegativeZ, // Tip points backward (-Z)
        PositiveX, // Tip points right (+X)
        NegativeX  // Tip points left (-X)
    }

    [Header("Flight Properties")]
    [SerializeField, Min(1f)] private float speed = 24f;
    [SerializeField] private bool useArc = true;
    [Tooltip("Arc height in meters. If set to 0 or left at default, arc height automatically scales with distance (1.2m - 3.8m).")]
    [SerializeField, Min(0f)] private float arcHeight = 0f;
    [SerializeField, Min(0.5f)] private float lifeTime = 5f;

    [Header("Model Alignment")]
    [Tooltip("Which axis of your 3D arrow model points toward the arrowhead tip. In prefab Arrow_B, the tip points down along the green Y-axis, which is Negative Y.")]
    [SerializeField] private ModelForwardAxis modelForwardAxis = ModelForwardAxis.NegativeY;

    [Header("Visuals & Impact")]
    [Tooltip("If true, the arrow sticks realistically into the target or ground/obstacle upon impact instead of instantly vanishing.")]
    [SerializeField] private bool stickOnHit = true;
    [Tooltip("How long (in seconds) the stuck arrow remains visible before despawning.")]
    [SerializeField, Min(1f)] private float stuckLifeTime = 3.5f;

    [Header("Impact VFX (Blood for Players, Wood for Machines, Dust for Buildings)")]
    [Tooltip("VFX prefab spawned when arrow strikes a player or character unit (blood effect).")]
    [SerializeField] private GameObject bloodHitVfxPrefab;
    [Tooltip("VFX prefab spawned when arrow strikes a siege machine (wood splinters / sparks effect).")]
    [SerializeField] private GameObject machineHitVfxPrefab;
    [Tooltip("VFX prefab spawned when arrow strikes a building, wall, stone, or ground (dust effect).")]
    [SerializeField] private GameObject dustHitVfxPrefab;
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private GameObject redTeamHitVfxPrefab;
    [SerializeField] private AudioClip hitSound;
    [SerializeField, Range(0f, 1f)] private float hitSoundVolume = 0.85f;
    [SerializeField] private LayerMask obstacleLayers = ~0;

    [Header("Trail Visuals")]
    [SerializeField] private bool enableTrail = true;
    [SerializeField] private float trailTime = 0.22f;
    [SerializeField] private float trailStartWidth = 0.11f;
    [SerializeField] private float trailEndWidth = 0.01f;

    [Header("Heavy Boulder / Siege Impact (Catapult)")]
    [Tooltip("If true, treats this projectile as a heavy stone/boulder that causes ground impact craters, screenshake, AOE blast damage, and ragdoll/knockback!")]
    [SerializeField] private bool isHeavyBoulder = false;
    [SerializeField, Min(0f)] private float blastRadius = 3.5f;
    [SerializeField, Min(0f)] private float blastKnockback = 6.0f;
    [SerializeField] private bool triggerScreenShake = true;
    [SerializeField, Range(0f, 1f)] private float screenShakeIntensity = 0.45f;
    [SerializeField, Range(0.1f, 1f)] private float screenShakeDuration = 0.30f;
    [Tooltip("Secondary explosion VFX (e.g. ground dust shockwave, rock debris).")]
    [SerializeField] private GameObject secondaryImpactVfx;
    [Tooltip("Tertiary impact VFX (e.g. ground crack / dust puff).")]
    [SerializeField] private GameObject groundDustVfx;
    [Tooltip("Secondary impact audio clip (e.g. heavy bass crunch or rock shatter).")]
    [SerializeField] private AudioClip secondaryImpactSound;
    [Tooltip("Tumble rotation during flight (realistic boulder roll/tumble).")]
    [SerializeField] private bool tumbleInFlight = false;
    [SerializeField, Range(0.5f, 5f)] private float tumbleSpeed = 2.5f;

    private Transform targetTransform;
    private Vector3 targetPos;
    private Vector3 startPos;
    private float damage;
    private PeasantUnitController shooter;
    private float flightTime;
    private float totalFlightDuration;
    private float calculatedArcHeight;
    private bool initialized;
    private bool hasHit;
    private TrailRenderer trail;
    private PeasantUnitController _cachedTargetPUC;
    private Vector3 tumbleAxis = Vector3.up;

    private static AudioClip cachedDefaultHitSound;
    private static GameObject cachedDefaultBloodVfx;
    private static GameObject cachedDefaultMachineVfx;
    private static GameObject cachedDefaultDustVfx;
    private static GameObject cachedDefaultBlueVfx;
    private static GameObject cachedDefaultRedVfx;

    private void Awake()
    {
        EnsureAudioAndVfxAssigned();
    }

    private void Reset()
    {
        EnsureAudioAndVfxAssigned();
    }

    /// <summary>
    /// Launches the arrow towards target transform or fallback coordinate.
    /// Supports optional custom VFX and hit audio overrides.
    /// </summary>
    public void Launch(Transform target, Vector3 fallbackTargetPos, float damageAmount, PeasantUnitController attacker, GameObject customHitVfx = null, AudioClip customHitClip = null, GameObject customBloodVfx = null, GameObject customDustVfx = null, GameObject customMachineVfx = null)
    {
        this.targetTransform = target;
        _cachedTargetPUC = target != null ? target.GetComponentInParent<PeasantUnitController>() : null;
        this.targetPos = target != null ? target.position + Vector3.up * 1f : fallbackTargetPos;
        this.startPos = transform.position;
        this.damage = damageAmount;
        this.shooter = attacker;

        if (customHitVfx != null) this.hitEffectPrefab = customHitVfx;
        if (customHitClip != null) this.hitSound = customHitClip;
        if (customBloodVfx != null) this.bloodHitVfxPrefab = customBloodVfx;
        if (customDustVfx != null) this.dustHitVfxPrefab = customDustVfx;
        if (customMachineVfx != null) this.machineHitVfxPrefab = customMachineVfx;

        EnsureAudioAndVfxAssigned();

        float distance = Vector3.Distance(startPos, targetPos);
        totalFlightDuration = Mathf.Max(0.06f, distance / speed);
        flightTime = 0f;

        // Auto-scale ballistic arc height if useArc is true
        if (useArc)
        {
            if (arcHeight <= 0.05f)
            {
                // Parabolic arc scales naturally with distance: 1.2m close to 3.8m far
                calculatedArcHeight = Mathf.Clamp(distance * 0.13f, 1.2f, 3.8f);
            }
            else
            {
                calculatedArcHeight = arcHeight;
            }
        }
        else
        {
            calculatedArcHeight = 0f;
        }

        if (enableTrail)
        {
            SetupTrail();
        }

        initialized = true;

        // Initial launch orientation aligned with launch tangent: dy/dt at t=0 has extra 4*h
        Vector3 initialDir = targetPos - startPos;
        if (calculatedArcHeight > 0.01f)
        {
            initialDir.y += 4f * calculatedArcHeight;
        }

        if (initialDir.sqrMagnitude > 0.0001f)
        {
            transform.rotation = GetModelAlignedRotation(initialDir.normalized);
        }

        if (tumbleInFlight)
        {
            tumbleAxis = new Vector3(Random.Range(0.4f, 1f), Random.Range(-0.3f, 0.3f), Random.Range(0.4f, 1f)).normalized;
        }

        Destroy(gameObject, lifeTime + stuckLifeTime + 1f);
    }

    private void SetupTrail()
    {
        trail = GetComponent<TrailRenderer>();
        if (trail == null)
        {
            trail = gameObject.AddComponent<TrailRenderer>();
        }

        trail.time = trailTime;
        trail.startWidth = trailStartWidth;
        trail.endWidth = trailEndWidth;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.emitting = true;

        // Team tinted trail gradient
        int team = shooter != null ? shooter.TeamId : 0;
        Color headColor = (team == 0)
            ? new Color(0.25f, 0.75f, 1f, 0.85f)   // Team Blue cyan glow
            : new Color(1f, 0.35f, 0.15f, 0.85f);  // Team Red fiery glow
        Color tailColor = Color.white;

        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(headColor, 0.0f),
                new GradientColorKey(tailColor, 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.85f, 0.0f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        trail.colorGradient = grad;

        // Performance: one shared trail material for every arrow instead of Shader.Find +
        // new Material per arrow (which leaked a material each shot and prevented batching).
        // Per-arrow colour comes from colorGradient (vertex colours), so sharing is identical visually.
        if (s_sharedTrailMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Mobile/Particles/Additive");
            if (shader != null) s_sharedTrailMaterial = new Material(shader) { name = "ArrowTrail (shared)" };
        }
        if (s_sharedTrailMaterial != null)
        {
            trail.sharedMaterial = s_sharedTrailMaterial;
        }
    }

    private static Material s_sharedTrailMaterial;

    private void EnsureAudioAndVfxAssigned()
    {
        if (hitSound == null)
        {
            if (cachedDefaultHitSound == null)
            {
                cachedDefaultHitSound = Resources.Load<AudioClip>("Sounds/arrowhit");
            }
            hitSound = cachedDefaultHitSound;
        }

        // Blood VFX for players / character units
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
                if (cachedDefaultBloodVfx == null)
                    cachedDefaultBloodVfx = RuntimeAssets.LoadAssetAtPath<GameObject>("Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Liquids/CFXR2 Blood Shape Splash.prefab");
            }
            bloodHitVfxPrefab = cachedDefaultBloodVfx;
        }

        // Machine Hit VFX for siege engines
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

        // Dust VFX for buildings / obstacles / ground
        if (dustHitVfxPrefab == null)
        {
            if (cachedDefaultDustVfx == null)
            {
                cachedDefaultDustVfx = Resources.Load<GameObject>("Effects/ArrowHit_Dust");
            }
            if (cachedDefaultDustVfx == null)
            {
                cachedDefaultDustVfx = RuntimeAssets.LoadAssetAtPath<GameObject>("Assets/PolygonParticles/Prefabs/FX_Dust_Small_01.prefab");
                if (cachedDefaultDustVfx == null)
                    cachedDefaultDustVfx = RuntimeAssets.LoadAssetAtPath<GameObject>("Assets/PolygonParticles/Prefabs/FX_Dust_Big_01.prefab");
                if (cachedDefaultDustVfx == null)
                    cachedDefaultDustVfx = RuntimeAssets.LoadAssetAtPath<GameObject>("Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Misc/CFXR3 Hit Misc F Smoke.prefab");
            }
            dustHitVfxPrefab = cachedDefaultDustVfx;
        }

        if (hitEffectPrefab == null)
        {
            int team = shooter != null ? shooter.TeamId : 0;
            if (team == 1)
            {
                if (cachedDefaultRedVfx == null)
                {
                    cachedDefaultRedVfx = Resources.Load<GameObject>("Effects/ArrowHit_Red");
                }
                if (cachedDefaultRedVfx != null)
                {
                    hitEffectPrefab = cachedDefaultRedVfx;
                }
            }
            else
            {
                if (cachedDefaultBlueVfx == null)
                {
                    cachedDefaultBlueVfx = Resources.Load<GameObject>("Effects/ArrowHit_Blue");
                }
                if (cachedDefaultBlueVfx != null)
                {
                    hitEffectPrefab = cachedDefaultBlueVfx;
                }
            }

            if (hitEffectPrefab == null)
            {
                hitEffectPrefab = Resources.Load<GameObject>("Powerups/CFXR2 WW Explosion");
            }
        }
    }

    private void Update()
    {
        if (!initialized || hasHit) return;

        flightTime += Time.deltaTime;
        float progress = Mathf.Clamp01(flightTime / totalFlightDuration);

        // Update target position towards spine/chest if target is still alive and moving
        if (targetTransform != null)
        {
            PeasantUnitController targetUnit = _cachedTargetPUC;
            if (targetUnit != null && targetUnit.IsDead)
            {
                // Target died during flight; freeze last target coordinate
                targetTransform = null;
                _cachedTargetPUC = null;
            }
            else
            {
                Transform spine = FindBestAttachmentBone(targetTransform);
                if (spine != null && spine != targetTransform)
                {
                    targetPos = spine.position;
                }
                else
                {
                    targetPos = targetTransform.position + Vector3.up * 1f;
                }
            }
        }

        Vector3 currentPos = Vector3.Lerp(startPos, targetPos, progress);

        if (calculatedArcHeight > 0.01f)
        {
            // Parabolic vertical arc: 4 * h * p * (1 - p)
            float arcOffset = 4f * calculatedArcHeight * progress * (1f - progress);
            currentPos.y += arcOffset;
        }

        Vector3 prevPos = transform.position;
        Vector3 moveDelta = currentPos - prevPos;
        float moveDist = moveDelta.magnitude;

        if (moveDist > 0.0001f)
        {
            if (tumbleInFlight)
            {
                transform.Rotate(tumbleAxis * (360f * Time.deltaTime * tumbleSpeed), Space.World);
            }
            else
            {
                transform.rotation = GetModelAlignedRotation(moveDelta.normalized);
            }

            // Raycast sweep to check if the arrow hits an enemy, objective, or obstacle on the way
            if (Physics.Raycast(prevPos, moveDelta.normalized, out RaycastHit hit, moveDist, obstacleLayers, QueryTriggerInteraction.Ignore))
            {
                // 1. Ignore shooter and its children
                if (shooter != null && (hit.transform == shooter.transform || hit.transform.IsChildOf(shooter.transform)))
                {
                    // Pass through shooter
                }
                // 2. Check if hit is a PeasantUnitController
                else if (hit.collider != null && hit.collider.GetComponentInParent<PeasantUnitController>() != null)
                {
                    PeasantUnitController hitPeasant = hit.collider.GetComponentInParent<PeasantUnitController>();
                    // Friendly teammate or dead unit: pass straight through!
                    if (!hitPeasant.IsDead && (shooter == null || hitPeasant.TeamId != shooter.TeamId))
                    {
                        // Struck an enemy unit!
                        transform.position = hit.point;
                        OnHitTargetWithVictim(hitPeasant, hit);
                        return;
                    }
                }
                // 3. Check if hit is an Objective Target (Castle / Tower)
                else if (hit.collider != null && hit.collider.GetComponentInParent<PeasantObjectiveTarget>() != null)
                {
                    PeasantObjectiveTarget hitObjective = hit.collider.GetComponentInParent<PeasantObjectiveTarget>();
                    if (!hitObjective.IsDestroyed && (shooter == null || hitObjective.TeamId != shooter.TeamId))
                    {
                        transform.position = hit.point;
                        OnHitObjective(hitObjective, hit);
                        return;
                    }
                }
                // 4. Ground/Terrain: do not intercept arrows in mid-flight before reaching target
                else if (progress < 0.88f && (hit.collider is TerrainCollider || hit.collider.gameObject.name.IndexOf("Terrain", System.StringComparison.OrdinalIgnoreCase) >= 0 || hit.collider.gameObject.layer == LayerMask.NameToLayer("Ground")))
                {
                    // Pass over ground bumps mid-flight
                }
                // 5. Check if hit is the target or child of target
                else if (targetTransform != null && (hit.transform == targetTransform || hit.transform.IsChildOf(targetTransform)))
                {
                    transform.position = hit.point;
                    OnHitTarget(hit);
                    return;
                }
                // 6. Real obstacle (e.g. wall, rock, building)
                else if (progress >= 0.2f)
                {
                    transform.position = hit.point;
                    OnHitObstacle(hit);
                    return;
                }
            }
        }

        transform.position = currentPos;

        // Check if reached destination
        if (progress >= 1f || Vector3.Distance(transform.position, targetPos) < 0.35f)
        {
            OnHitTarget(default);
        }
    }

    private void OnHitTargetWithVictim(PeasantUnitController victim, RaycastHit hit)
    {
        if (hasHit) return;
        hasHit = true;

        if (trail != null)
        {
            trail.emitting = false;
        }

        if (victim != null && !victim.IsDead)
        {
            if (!victim.IsShieldImmune && !ShieldDomeEffect.IsProtectedByAnyDome(victim.transform.position, victim.TeamId))
            {
                if (damage > 0f)
                {
                    victim.TakeDamage(damage, shooter);
                }
            }

            // Squash effect removed: unit maintains natural scale without squash/punch distortion
        }

        Vector3 impactPos = hit.point != Vector3.zero ? hit.point : transform.position;
        if (isHeavyBoulder)
        {
            ApplyBoulderAoeDamage(impactPos, victim != null ? victim.gameObject.GetInstanceID() : 0);
        }

        bool isMachine = victim != null && victim.IsMachine;
        bool isBlood = victim != null && !victim.IsMachine;
        GameObject customUnitVfx = victim != null ? victim.HitVfxPrefab : null;

        PlayHitEffects(impactPos, isBlood: isBlood, isMachine: isMachine, customOverrideVfx: customUnitVfx);
        StickArrow(victim != null ? victim.transform : hit.transform, impactPos);
    }

    private void OnHitObjective(PeasantObjectiveTarget objective, RaycastHit hit)
    {
        if (hasHit) return;
        hasHit = true;

        if (trail != null)
        {
            trail.emitting = false;
        }

        if (objective != null && !objective.IsDestroyed)
        {
            if (!objective.IsShieldImmune && !ShieldDomeEffect.IsProtectedByAnyDome(objective.TargetPoint.position, objective.TeamId))
            {
                if (damage > 0f)
                {
                    objective.TakeDamage(damage, shooter);
                }
            }
        }

        Vector3 impactPos = hit.point != Vector3.zero ? hit.point : transform.position;
        if (isHeavyBoulder)
        {
            ApplyBoulderAoeDamage(impactPos, objective != null ? objective.gameObject.GetInstanceID() : 0);
        }
        PlayHitEffects(impactPos, isBlood: false);
        StickArrow(objective != null ? objective.transform : hit.transform, impactPos);
    }

    private void OnHitObstacle(RaycastHit hit)
    {
        if (hasHit) return;
        hasHit = true;

        if (trail != null)
        {
            trail.emitting = false;
        }

        // If the obstacle is an attackable castle / objective, damage it
        if (hit.collider != null)
        {
            PeasantObjectiveTarget objective = hit.collider.GetComponentInParent<PeasantObjectiveTarget>();
            if (objective != null && !objective.IsDestroyed && (shooter == null || objective.TeamId != shooter.TeamId))
            {
                if (!objective.IsShieldImmune && !ShieldDomeEffect.IsProtectedByAnyDome(objective.TargetPoint.position, objective.TeamId))
                {
                    if (damage > 0f)
                    {
                        objective.TakeDamage(damage, shooter);
                    }
                }
            }
        }

        Vector3 impactPos = hit.point != Vector3.zero ? hit.point : transform.position;
        if (isHeavyBoulder)
        {
            ApplyBoulderAoeDamage(impactPos, 0);
        }
        PlayHitEffects(impactPos, isBlood: false);
        StickArrow(hit.transform, impactPos);
    }

    private void OnHitTarget(RaycastHit hit)
    {
        if (hasHit) return;
        hasHit = true;

        if (trail != null)
        {
            trail.emitting = false;
        }

        Transform hitTransform = hit.transform != null ? hit.transform : targetTransform;
        Vector3 hitPoint = hit.point != Vector3.zero ? hit.point : transform.position;
        bool damageApplied = false;
        bool isBloodHit = false;
        bool isMachineHit = false;
        GameObject customUnitVfx = null;
        int directHitId = 0;

        if (targetTransform != null)
        {
            PeasantUnitController victim = targetTransform.GetComponentInParent<PeasantUnitController>();
            if (victim != null && !victim.IsDead)
            {
                directHitId = victim.gameObject.GetInstanceID();
                if (!victim.IsShieldImmune && !ShieldDomeEffect.IsProtectedByAnyDome(victim.transform.position, victim.TeamId))
                {
                    if (damage > 0f)
                    {
                        victim.TakeDamage(damage, shooter);
                    }
                    damageApplied = true;
                }

                if (victim.IsMachine)
                {
                    isMachineHit = true;
                    isBloodHit = false;
                }
                else
                {
                    isBloodHit = true;
                    isMachineHit = false;
                }
                customUnitVfx = victim.HitVfxPrefab;
            }
            else
            {
                PeasantObjectiveTarget objective = targetTransform.GetComponentInParent<PeasantObjectiveTarget>();
                if (objective != null && !objective.IsDestroyed)
                {
                    directHitId = objective.gameObject.GetInstanceID();
                    // Strict friendly fire check: never damage friendly objectives
                    if (shooter != null && objective.TeamId == shooter.TeamId)
                    {
                        Destroy(gameObject);
                        return;
                    }

                    if (!objective.IsShieldImmune && !ShieldDomeEffect.IsProtectedByAnyDome(objective.TargetPoint.position, objective.TeamId))
                    {
                        if (damage > 0f)
                        {
                            objective.TakeDamage(damage, shooter);
                        }
                        damageApplied = true;
                    }

                    isBloodHit = false;
                    isMachineHit = false;
                }
                else
                {
                    IDamageable damageable = targetTransform.GetComponentInParent<IDamageable>();
                    if (damageable != null)
                    {
                        if (damage > 0f)
                        {
                            damageable.TakeDamage(damage, shooter);
                        }
                        damageApplied = true;
                    }
                    isBloodHit = false;
                    isMachineHit = false;
                }
            }
        }

        // Proximity fallback: if original target died or was null, damage closest enemy in impact area
        if (!damageApplied && shooter != null && damage > 0f)
        {
            Collider[] nearby = Physics.OverlapSphere(transform.position, 1.4f);
            for (int i = 0; i < nearby.Length; i++)
            {
                PeasantUnitController nearbyEnemy = nearby[i].GetComponentInParent<PeasantUnitController>();
                if (nearbyEnemy != null && !nearbyEnemy.IsDead && nearbyEnemy.TeamId != shooter.TeamId)
                {
                    if (!nearbyEnemy.IsShieldImmune && !ShieldDomeEffect.IsProtectedByAnyDome(nearbyEnemy.transform.position, nearbyEnemy.TeamId))
                    {
                        nearbyEnemy.TakeDamage(damage, shooter);
                        directHitId = nearbyEnemy.gameObject.GetInstanceID();
                        hitTransform = nearbyEnemy.transform;
                        if (nearbyEnemy.IsMachine)
                        {
                            isMachineHit = true;
                            isBloodHit = false;
                        }
                        else
                        {
                            isBloodHit = true;
                            isMachineHit = false;
                        }
                        customUnitVfx = nearbyEnemy.HitVfxPrefab;
                        break;
                    }
                }
            }
        }

        if (isHeavyBoulder)
        {
            ApplyBoulderAoeDamage(hitPoint, directHitId);
        }

        PlayHitEffects(hitPoint, isBlood: isBloodHit, isMachine: isMachineHit, customOverrideVfx: customUnitVfx);
        StickArrow(hitTransform, hitPoint);
    }

    private void PlayHitEffects(Vector3 point, bool isBlood, bool isMachine = false, GameObject customOverrideVfx = null)
    {
        DetachAndStopChildParticles();

        if (hitSound != null)
        {
            float pitch = Random.Range(0.94f, 1.06f);
            PlayClipAtPointWithPitch(hitSound, point, hitSoundVolume, pitch, "arrow_hit");
        }

        GameObject vfxToSpawn = customOverrideVfx;
        if (vfxToSpawn == null)
        {
            if (isMachine)
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
        }

        if (vfxToSpawn == null)
        {
            vfxToSpawn = hitEffectPrefab;
            if (shooter != null && shooter.TeamId == 1 && redTeamHitVfxPrefab != null)
            {
                vfxToSpawn = redTeamHitVfxPrefab;
            }
        }

        if (vfxToSpawn != null)
        {
            GameObject fx = SimpleGameObjectPool.Get(vfxToSpawn, point, (isBlood || isMachine) ? transform.rotation : Quaternion.identity);
            SimpleGameObjectPool.ReturnDelayed(fx, 2.5f);
        }

        if (isHeavyBoulder)
        {
            // Camera tremor / screen shake for satisfying heavy impact feedback
            if (triggerScreenShake && CameraSwipeController.Instance != null)
            {
                CameraSwipeController.Instance.TriggerShake(screenShakeIntensity, screenShakeDuration);
            }

            // Layered secondary crunch / rock shatter audio
            if (secondaryImpactSound != null)
            {
                float p = Random.Range(0.82f, 0.98f);
                PlayClipAtPointWithPitch(secondaryImpactSound, point, 1.0f, p, "siege_impact");
            }

            // Secondary rock shard explosion VFX
            if (secondaryImpactVfx != null)
            {
                GameObject sec = SimpleGameObjectPool.Get(secondaryImpactVfx, point, Quaternion.identity);
                SimpleGameObjectPool.ReturnDelayed(sec, 3f);
            }

            // Ground dust / impact shockwave VFX
            if (groundDustVfx != null)
            {
                GameObject gd = SimpleGameObjectPool.Get(groundDustVfx, point, Quaternion.identity);
                SimpleGameObjectPool.ReturnDelayed(gd, 3f);
            }
        }
    }

    private void ApplyBoulderAoeDamage(Vector3 impactPoint, int directHitInstanceId)
    {
        if (!isHeavyBoulder || blastRadius <= 0f) return;

        Collider[] hits = Physics.OverlapSphere(impactPoint, blastRadius);
        HashSet<int> processedUnits = new HashSet<int>();
        if (directHitInstanceId != 0)
        {
            processedUnits.Add(directHitInstanceId);
        }

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i];
            if (col == null) continue;

            PeasantUnitController victim = col.GetComponentInParent<PeasantUnitController>();
            if (victim != null)
            {
                int id = victim.gameObject.GetInstanceID();
                if (processedUnits.Contains(id)) continue;
                processedUnits.Add(id);

                if (!victim.IsDead && (shooter == null || victim.TeamId != shooter.TeamId))
                {
                    if (!victim.IsShieldImmune && !ShieldDomeEffect.IsProtectedByAnyDome(victim.transform.position, victim.TeamId))
                    {
                        float dist = Vector3.Distance(impactPoint, victim.transform.position);
                        float factor = Mathf.Clamp01(1f - (dist / blastRadius) * 0.45f);
                        victim.TakeDamage(damage * factor, shooter);

                        // Radial impulse push
                        Vector3 pushDir = (victim.transform.position - impactPoint);
                        pushDir.y = 0f;
                        if (pushDir.sqrMagnitude > 0.001f)
                        {
                            pushDir.Normalize();
                            UnityEngine.AI.NavMeshAgent a = victim.GetComponent<UnityEngine.AI.NavMeshAgent>();
                            if (a != null && a.isOnNavMesh)
                            {
                                a.Move(pushDir * (blastKnockback * factor * 0.22f));
                            }
                        }
                    }
                }
                continue;
            }

            PeasantObjectiveTarget obj = col.GetComponentInParent<PeasantObjectiveTarget>();
            if (obj != null && !obj.IsDestroyed && (shooter == null || obj.TeamId != shooter.TeamId))
            {
                int id = obj.gameObject.GetInstanceID();
                if (processedUnits.Contains(id)) continue;
                processedUnits.Add(id);

                if (!obj.IsShieldImmune && !ShieldDomeEffect.IsProtectedByAnyDome(obj.TargetPoint.position, obj.TeamId))
                {
                    obj.TakeDamage(damage, shooter);
                }
            }
        }
    }

    private void DetachAndStopChildParticles()
    {
        ParticleSystem[] pSystems = GetComponentsInChildren<ParticleSystem>();
        for (int i = 0; i < pSystems.Length; i++)
        {
            if (pSystems[i] == null) continue;
            pSystems[i].transform.SetParent(null, true);
            pSystems[i].Stop();
            Destroy(pSystems[i].gameObject, 2.5f);
        }
    }

    private void PlayClipAtPointWithPitch(AudioClip clip, Vector3 position, float volume, float pitch, string evt = null)
    {
        if (clip == null) return;
        // Pooled + voice-limited through KoSAudio (no temp GameObject per arrow).
        KoSAudio.PlayClipAtPoint(clip, position, volume, evt, pitch);
    }

    private void StickArrow(Transform hitSurface, Vector3 impactPoint)
    {
        CancelInvoke();

        if (isHeavyBoulder)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;

            if (impactPoint != Vector3.zero) transform.position = impactPoint;

            Vector3 curScale = transform.localScale;
            transform.DOKill(false);
            Sequence slam = DOTween.Sequence();
            // 1. High-impact pancake squash into ground
            slam.Append(transform.DOScale(new Vector3(curScale.x * 1.5f, curScale.y * 0.35f, curScale.z * 1.5f), 0.05f).SetEase(Ease.OutQuad));
            // 2. Shatter / disintegrate into rubble burst
            slam.Append(transform.DOScale(Vector3.zero, 0.18f).SetEase(Ease.InQuad));
            slam.OnComplete(() =>
            {
                if (this != null && gameObject != null) Destroy(gameObject);
            });
            return;
        }

        if (stickOnHit)
        {
            // Disable colliders and physics so the arrow stays static
            Collider[] colliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.linearVelocity = Vector3.zero;
            }

            // Embed slightly forward into the struck object
            if (impactPoint != Vector3.zero)
            {
                transform.position = impactPoint;
            }

            // Find best bone to attach to: specifically "Bip001 Spine" (or spine/chest/pelvis bones)
            // so the stuck arrow animates realistically with the character's body movements
            Transform attachBone = FindBestAttachmentBone(hitSurface);
            if (attachBone != null)
            {
                transform.SetParent(attachBone, true);
            }
            else if (hitSurface != null)
            {
                transform.SetParent(hitSurface, true);
            }

            // Elastic rotational wobble as the arrow embeds!
            transform.DOKill(false);
            transform.DOShakeRotation(0.26f, 18f, 14, 90f);

            // Smooth shrink and despawn after stuckLifeTime
            DOVirtual.DelayedCall(stuckLifeTime, () =>
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
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Searches the hit character's hierarchy for Bip001 Spine (or nearest spine/chest bone)
    /// to ensure the stuck arrow moves with the character's animated skeleton.
    /// </summary>
    private Transform FindBestAttachmentBone(Transform hitRoot)
    {
        if (hitRoot == null) return null;

        // 1. Direct search by priority names (e.g. "Bip001 Spine")
        string[] priorityBones = { "Bip001 Spine", "Bip001 Pelvis", "Spine", "spine", "Chest", "chest" };
        Transform[] allChildren = hitRoot.GetComponentsInChildren<Transform>(true);

        for (int p = 0; p < priorityBones.Length; p++)
        {
            string targetName = priorityBones[p];
            for (int i = 0; i < allChildren.Length; i++)
            {
                if (allChildren[i] != null && allChildren[i].name.Equals(targetName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return allChildren[i];
                }
            }
        }

        // 2. Partial match containing "Spine"
        for (int i = 0; i < allChildren.Length; i++)
        {
            if (allChildren[i] != null && allChildren[i].name.IndexOf("Spine", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return allChildren[i];
            }
        }

        return hitRoot;
    }

    /// <summary>
    /// Computes rotation aligning the model's forward axis with the flight direction.
    /// In Arrow_B, the arrowhead tip points in the -Y direction (downwards).
    /// </summary>
    private Quaternion GetModelAlignedRotation(Vector3 flightDirection)
    {
        if (flightDirection.sqrMagnitude < 0.0001f)
            return transform.rotation;

        Vector3 modelTipVector;
        switch (modelForwardAxis)
        {
            case ModelForwardAxis.NegativeY:
                modelTipVector = Vector3.down;
                break;
            case ModelForwardAxis.PositiveY:
                modelTipVector = Vector3.up;
                break;
            case ModelForwardAxis.PositiveZ:
                modelTipVector = Vector3.forward;
                break;
            case ModelForwardAxis.NegativeZ:
                modelTipVector = Vector3.back;
                break;
            case ModelForwardAxis.PositiveX:
                modelTipVector = Vector3.right;
                break;
            case ModelForwardAxis.NegativeX:
                modelTipVector = Vector3.left;
                break;
            default:
                modelTipVector = Vector3.down;
                break;
        }

        // Rotate so that the model's tip vector points directly along flightDirection
        return Quaternion.FromToRotation(modelTipVector, flightDirection);
    }

    private void OnDestroy()
    {
        transform.DOKill();
    }
}
