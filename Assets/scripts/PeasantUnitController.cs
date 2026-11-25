using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Peasant unit controller for a Unity RTS.
/// Supports NavMesh movement, idle/run, target selection, melee attacks,
/// damage, hurt and death animations, and simple team-based combat.
///
/// Animator parameters expected by the supplied controller:
///   running (Bool), attack (Trigger), hurt (Trigger), death (Trigger)
///
/// Recommended setup:
///   1. Add this script, a NavMeshAgent, and a Collider to the peasant root.
///   2. Assign the Animator and a valid target LayerMask.
///   3. Bake a NavMesh on the battlefield.
///   4. Put enemy units on a layer included in targetLayers.
///   5. Optional: add an Animation Event named "AnimationAttackHit" to the
///      attack clip at the frame where the weapon should hit.
///      If you do not add the event, the controller uses hitDelay instead.
/// </summary>
[DisallowMultipleComponent]
public class PeasantUnitController : MonoBehaviour
{
    public enum UnitState { Idle, Moving, Chasing, Attacking, Hurt, Dead }
    public enum UnitCombatType { Melee, Archer }

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform attackPoint;

    [Header("Identity")]
    [SerializeField] private int teamId = 0;
    [SerializeField] private string unitName = "Peasant";
    [SerializeField] private UnitCombatType unitType = UnitCombatType.Melee;
    [Tooltip("If true, this unit is a siege machine / mechanical engine (Ballista, Catapult, Ram, etc.) rather than a living human soldier. Spawns wood splinters/sparks instead of blood.")]
    [SerializeField] private bool isMachine = false;
    [Tooltip("Optional custom hit particle prefab spawned when this unit gets hit. If null, machines spawn wood/sparks and soldiers spawn blood.")]
    [SerializeField] private GameObject hitVfxPrefab;

    [Header("Health")]
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [SerializeField] private bool destroyAfterDeath = false;
    [SerializeField, Min(0f)] private float destroyDelay = 4f;
    [SerializeField] private Transform healthBarCube;
    [SerializeField] private JuicyUnitHealthBar juicyHealthBar;
    [SerializeField] private Vector3 healthBarScaleAxis = Vector3.right;
    [SerializeField] private bool hideHealthBarOnDeath = true;

    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 3.5f;
    [SerializeField, Min(0.1f)] private float acceleration = 12f;
    [SerializeField, Min(0.1f)] private float turnSpeed = 720f;
    [SerializeField, Min(0.05f)] private float stoppingDistance = 0.15f;
    [SerializeField] private bool updateRotation = true;
    [Tooltip("If true, the unit remains stationary: will not move or chase enemies.")]
    [SerializeField] private bool isStationary = false;
    [Header("Unit Scale")]
    [Tooltip("Target scale for deployed humanoid units (default: 0.77, 0.77, 0.77).")]
    [SerializeField] private Vector3 deployedScale = new Vector3(0.77f, 0.77f, 0.77f);
    [Tooltip("Target scale for deployed siege machines (default: 1, 1, 1).")]
    [SerializeField] private Vector3 machineScale = Vector3.one;
    public Vector3 DeployedScale => IsMachine ? machineScale : deployedScale;
    public Vector3 TargetScale => DeployedScale;
    [Tooltip("If true, the unit can rotate to face and track targets (even while stationary).")]
    [SerializeField] private bool canRotate = true;
    [Tooltip("If true, other peasants cannot target, attack, or damage this unit.")]
    [SerializeField] private bool cannotBeAttackedByPeasants = false;
    [Tooltip("If true, dynamically adjusts animator playback speed (or speed float parameter) to match the actual agent velocity so running looks natural without foot-sliding.")]
    [SerializeField] private bool syncAnimSpeedToMoveSpeed = true;
    [Tooltip("The reference movement speed (meters/sec) where the run animation plays at normal 1.0x speed.")]
    [SerializeField, Min(0.5f)] private float referenceRunSpeed = 3.5f;
    [Tooltip("Smooth transition time for movement animation speed blending. Prevents jerky snap transitions when accelerating or decelerating.")]
    [SerializeField, Range(0.05f, 0.4f)] private float animSpeedDampTime = 0.12f;
    [Tooltip("Multiplier for the running animation playback speed. Higher values make the running animation play faster.")]
    [SerializeField, Min(0.1f)] private float runAnimationSpeedMultiplier = 1f;

    [Header("Combat")]
    [Tooltip("Overall power multiplier. Scales attack damage (multiplied) and attack speed/cooldown (faster attacks). Default is 1.")]
    [SerializeField, Min(0.1f)] private float powerMultiplier = 1f;
    [SerializeField] public bool canAutoAcquireTargets = true;
    [SerializeField, Min(0.1f)] private float detectionRange = 8f;
    [SerializeField] private LayerMask targetLayers = ~0;
    [SerializeField, Min(0.1f)] private float attackRange = 1.6f;
    [SerializeField, Min(0f)] private float attackDamage = 15f;
    [SerializeField, Min(0.1f)] private float attackCooldown = 1.4f;
    [SerializeField, Min(0f)] private float hitDelay = 0.45f;
    [SerializeField, Min(0f)] private float attackRangePadding = 0.15f;
    [Tooltip("Minimum distance to fire at a target (e.g. catapults cannot hit anything closer than this). 0 = no minimum.")]
    [SerializeField, Min(0f)] private float minAttackRange = 0f;
    [SerializeField] private bool stopToAttack = true;
    [SerializeField] private bool faceTargetBeforeAttack = true;

    [Header("Ranged Archer Settings")]
    [Tooltip("Prefab of the arrow projectile fired when UnitType is Archer.")]
    [SerializeField] private GameObject arrowPrefab;
    [Tooltip("Spawn transform point where the arrow is created (defaults to attackPoint or head/weapon height).")]
    [SerializeField] private Transform arrowSpawnPoint;
    [Tooltip("Delay in seconds from the start of the attack animation until the arrow is released from the bowstring. Matches the visual bow release frame.")]
    [SerializeField, Min(0.05f)] private float archerHitDelay = 0.55f;
    [Tooltip("Time in seconds (e.g. 0.8s - 1.0s) an archer plants their feet, stops moving, and takes aim before starting their first attack draw. Makes archer combat realistic and deliberate.")]
    [SerializeField, Min(0f)] private float archerAimDelay = 0.85f;
    [Tooltip("LayerMask of obstacles (e.g. Castle walls, buildings, terrain) that block arrow trajectory.")]
    [SerializeField] private LayerMask lineOfSightObstacleLayers = ~0;
    [Tooltip("If true, archers check for obstacles between them and target before attacking, repositioning if blocked.")]
    [SerializeField] private bool checkLineOfSightBeforeAttack = true;
    [Tooltip("If true, archers will not attack until they lock onto their target (facing within targetLockAngleThreshold), and will only fire after lockOnFireDelay (0.7s) of holding lock.")]
    [SerializeField] private bool lockTargetBeforeAttack = true;
    [Tooltip("Delay in seconds after locking onto the target before firing.")]
    [SerializeField, Min(0f)] private float lockOnFireDelay = 0.7f;
    [Tooltip("Maximum angle difference in degrees between unit forward and target to consider target locked.")]
    [SerializeField, Range(1f, 45f)] private float targetLockAngleThreshold = 10f;
    [Tooltip("If true, requires re-locking and waiting lockOnFireDelay (0.7s) before every single arrow shot. If false, subsequent shots on the already-locked target occur at normal attack cooldown.")]
    [SerializeField] private bool relockBetweenShots = false;

    [Header("Spacing & Flocking (Personal Distance)")]
    [Tooltip("Apply gentle separation steering from friendly units to prevent clustering and clumping.")]
    [SerializeField] private bool applyFlockingSeparation = true;
    [Tooltip("Prevent standing units from being crowded or clipped into while idle.")]
    [SerializeField] private bool maintainPersonalSpaceWhenIdle = true;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Audio clip played while attacking (weapon swing / strike / grunt).")]
    [SerializeField] private AudioClip attackAudioClip;
    [Tooltip("Audio clip played specifically when an arrow is released/fired by an archer.")]
    [SerializeField] private AudioClip bowReleaseAudioClip;
    [Tooltip("Array of audio clips played at start and sometimes during the middle of battle (randomly picked).")]
    [SerializeField] private AudioClip[] screamAudioClips;
    [SerializeField, Range(0f, 1f)] private float attackAudioVolume = 0.85f;
    [SerializeField, Range(0f, 1f)] private float screamAudioVolume = 0.9f;
    [Tooltip("Random pitch variation (+/-) so each unit sounds unique and natural.")]
    [SerializeField, Range(0f, 0.3f)] private float pitchVariation = 0.12f;
    [Tooltip("Delay before the initial spawn scream plays.")]
    [SerializeField, Min(0f)] private float startScreamDelay = 0.25f;
    [Tooltip("Minimum time (seconds) between occasional screams in the middle of battle.")]
    [SerializeField, Min(1f)] private float minMidScreamInterval = 5f;
    [Tooltip("Maximum time (seconds) between occasional screams in the middle of battle.")]
    [SerializeField, Min(1f)] private float maxMidScreamInterval = 12f;

    public float PowerMultiplier
    {
        get => powerMultiplier;
        set => powerMultiplier = Mathf.Max(0.1f, value);
    }
    public float EffectiveAttackDamage => attackDamage * powerMultiplier;
    public float EffectiveAttackCooldown => attackCooldown / powerMultiplier;
    public float EffectiveHitDelay
    {
        get
        {
            float baseDelay = (unitType == UnitCombatType.Archer) ? archerHitDelay : hitDelay;
            return baseDelay / powerMultiplier;
        }
    }

    [Header("Animator Parameter Names")]
    [SerializeField] private string runningBool = "running";
    [Tooltip("Optional Animator float parameter name (e.g. 'speed') if your animator controller uses a float blend tree.")]
    [SerializeField] private string speedFloat = "speed";
    [SerializeField] private string attackTrigger = "attack";
    [SerializeField] private string hurtTrigger = "hurt";
    [SerializeField] private string deathTrigger = "death";

    [Header("Debug & Diagnostics")]
    [Tooltip("Show visual scene gizmos (detection sphere, attack range sphere, line of sight ray, destination line).")]
    [SerializeField] private bool showDebugGizmos = true;
    [Tooltip("Enable detailed debug logging in console for this unit.")]
    [SerializeField] private bool verboseDebugLogging = false;
    [SerializeField] private UnitState currentState = UnitState.Idle;

    /// <summary>
    /// Checks if this unit is the diagnostic target (requires verboseDebugLogging enabled).
    /// </summary>
    public bool IsTargetDebugUnit => verboseDebugLogging;

    private float nextDebugLogTime = 0f;
    private string lastDebugAction = "";
    private RaycastHit lastLosHit;
    private bool lastLosHadHit = false;
    private Vector3 lastLosStart;
    private Vector3 lastLosEnd;
    private bool lastLosResult = true;
    private Vector3 lastRepositionDest;
    private bool lastRepositionAttempted = false;

    private float currentHealth;
    private Vector3 initialHealthBarScale = Vector3.one;
    private Vector3 initialHealthBarLocalPos = Vector3.zero;
    private float nextAttackTime;
    private Transform currentTarget;
    private Coroutine hitRoutine;
    private bool attackHitApplied;
    private bool hasAnimator;
    private bool isDead;
    private bool isHurt;
    private bool isCommandMoving;
    private Vector3 commandDestination;
    private float nextTargetEvaluationTime;
    private float nextIdleCheckTime;
    private float nextMidBattleScreamTime;
    private float archerAimReadyTime;
    private float agentStuckTimer = 0f;
    private bool hasSpeedFloatParam;
    private float currentAnimSpeed = 0f;
    private bool isTargetLocked = false;
    private float targetLockAcquiredTime = -1f;
    private Transform lastLockedTarget;
    private Vector3 lastAttackTargetPos;
    private Transform targetWhenAttackStarted;

    public float AttackRange => attackRange;
    public float DetectionRange => detectionRange;
    public NavMeshAgent Agent => agent;
    public UnitCombatType UnitType => unitType;
    public bool IsArcher => unitType == UnitCombatType.Archer;

    private float stuckDuration = 0f;
    private Vector3 lastProgressPosition;
    private float nextProgressCheckTime = 0f;
    private bool isDetouring = false;
    private Vector3 activeDetourTarget = Vector3.zero;
    private float detourTimeout = 0f;
    private float nextDetourRecheckTime = 0f;

    /// <summary>
    /// Checks if this agent is physically blocked or stopped at an impassable obstacle/wall.
    /// </summary>
    public bool IsAgentStuck => stuckDuration >= 0.35f || 
        (agent != null && agent.enabled && agent.isOnNavMesh && agent.hasPath && !agent.pathPending && 
         agent.pathStatus == NavMeshPathStatus.PathPartial && agent.remainingDistance < 0.45f);

    /// <summary>
    /// Mounted / Cavalry: ~0.80m - 0.85m
    /// Foot soldiers: ~0.45m - 0.55m
    /// Machines: ~1.35m
    /// </summary>
    public float PhysicalRadius => _cachedPhysicalRadius > 0f ? _cachedPhysicalRadius : 0.50f;

    /// <summary>
    /// Center-to-center comfort distance between units of this type.
    /// </summary>
    public float DesiredSpacingDistance => _cachedDesiredSpacingDistance > 0f ? _cachedDesiredSpacingDistance : 1.45f;

    /// <summary>
    /// Checks if this unit is a Ballista siege machine.
    /// </summary>
    public bool IsBallista => _cachedIsBallista;

    /// <summary>
    /// Checks if this unit is a siege machine or mechanical unit.
    /// </summary>
    public bool IsMachine
    {
        get => _cachedIsMachine;
        set { isMachine = value; _cachedIsMachine = value; }
    }

    public GameObject HitVfxPrefab
    {
        get => hitVfxPrefab;
        set => hitVfxPrefab = value;
    }

    private static GameObject cachedDefaultBloodVfx;
    private static GameObject cachedDefaultMachineVfx;

    // Set once globally; avoids repeating a global physics layer call on every Awake.
    private static bool s_physicsLayerIgnoreSet;

    // Cached per-unit values computed once in Awake to avoid per-frame string/component searches.
    private bool _cachedIsMachine;
    private bool _cachedIsBallista;
    private float _cachedPhysicalRadius;
    private float _cachedDesiredSpacingDistance;

    // Reusable property block — avoids creating Material instances in ApplyTeamVisuals.
    private MaterialPropertyBlock _mpb;

    // Collider lookup cache for GetDistanceToTarget — avoids GetComponent every call.
    private Transform _distCacheKey;
    private Collider  _distCacheCol;

    /// <summary>
    /// Spawns the appropriate hit particle effect for this unit (Wood splinters/Sparks for machines, Blood for soldiers/players).
    /// </summary>
    public void SpawnHitEffect(Vector3 hitPoint, Vector3 hitDirection)
    {
        GameObject vfx = hitVfxPrefab;
        if (vfx == null)
        {
            if (IsMachine)
            {
                if (cachedDefaultMachineVfx == null)
                {
                    cachedDefaultMachineVfx = Resources.Load<GameObject>("Effects/ArrowHit_Wood");
                    if (cachedDefaultMachineVfx == null)
                        cachedDefaultMachineVfx = RuntimeAssets.LoadAssetAtPath<GameObject>("Assets/PolygonParticles/Prefabs/FX_Impact_Wood_01.prefab");
                    if (cachedDefaultMachineVfx == null)
                        cachedDefaultMachineVfx = RuntimeAssets.LoadAssetAtPath<GameObject>("Assets/PolygonParticles/Prefabs/FX_Impact_Metal_01.prefab");
                    if (cachedDefaultMachineVfx == null)
                        cachedDefaultMachineVfx = RuntimeAssets.LoadAssetAtPath<GameObject>("Assets/PolygonParticles/Prefabs/FX_Sparks_01.prefab");
                }
                vfx = cachedDefaultMachineVfx;
            }
            else
            {
                if (cachedDefaultBloodVfx == null)
                {
                    cachedDefaultBloodVfx = Resources.Load<GameObject>("Effects/ArrowHit_Blood");
                    if (cachedDefaultBloodVfx == null)
                        cachedDefaultBloodVfx = RuntimeAssets.LoadAssetAtPath<GameObject>("Assets/PolygonParticles/Prefabs/FX_BloodSplat_01.prefab");
                    if (cachedDefaultBloodVfx == null)
                        cachedDefaultBloodVfx = RuntimeAssets.LoadAssetAtPath<GameObject>("Assets/PolygonParticles/Prefabs/FX_BloodSplat_Small_01.prefab");
                }
                vfx = cachedDefaultBloodVfx;
            }
        }

        if (vfx != null)
        {
            Quaternion rot = hitDirection.sqrMagnitude > 0.001f ? Quaternion.LookRotation(hitDirection) : Quaternion.identity;
            GameObject instance = SimpleGameObjectPool.Get(vfx, hitPoint, rot);
            SimpleGameObjectPool.ReturnDelayed(instance, 2f);
        }
    }

    /// <summary>
    /// Determines whether this unit is a genuine foot archer eligible to garrison/station atop towers.
    /// Ballistas (even though their combat type is Archer), siege engines, mounted units, and melee units are strictly ineligible.
    /// </summary>
    public bool IsEligibleTowerArcher
    {
        get
        {
            if (this == null || gameObject == null || isDead) return false;
            if (IsBallista || IsMachine) return false;

            string goName = gameObject.name.ToLower();
            if (goName.Contains("mounted") || goName.Contains("cavalry") || goName.Contains("siege"))
                return false;

            ArcherUnit archerComponent = GetComponent<ArcherUnit>();
            if (archerComponent != null) return true;

            if (goName.Contains("archer")) return true;

            // Must have Archer combat type and not be a siege engine/ballista
            return unitType == UnitCombatType.Archer;
        }
    }

    /// <summary>
    /// Calculates the effective horizontal distance from this unit to a target.
    /// For buildings, castles, and defense towers with colliders, measures distance to the closest
    /// surface point of the collider (flattened to ground level Y) so melee units can reach and attack large structures.
    /// </summary>
    public float GetDistanceToTarget(Transform target)
    {
        if (target == null) return float.MaxValue;

        // Cache the collider lookup so we don't call GetComponent every frame per unit.
        Collider col;
        if (target != _distCacheKey)
        {
            _distCacheKey = target;
            col = target.GetComponentInChildren<Collider>();
            if (col == null) col = target.GetComponentInParent<Collider>();
            _distCacheCol = col;
        }
        else
        {
            col = _distCacheCol;
        }

        if (col != null && !(col is CharacterController))
        {
            Vector3 closestPoint = col.ClosestPoint(transform.position);
            closestPoint.y = transform.position.y;
            return Vector3.Distance(transform.position, closestPoint);
        }

        Vector3 targetFlat = target.position;
        targetFlat.y = transform.position.y;
        return Vector3.Distance(transform.position, targetFlat);
    }

    public bool IsStationary
    {
        get => isStationary;
        set => SetStationary(value);
    }

    public bool CanRotate
    {
        get => canRotate;
        set => canRotate = value;
    }

    public bool CannotBeAttackedByPeasants
    {
        get => cannotBeAttackedByPeasants;
        set => cannotBeAttackedByPeasants = value;
    }

    public float RunAnimationSpeedMultiplier
    {
        get => runAnimationSpeedMultiplier;
        set => runAnimationSpeedMultiplier = Mathf.Max(0.05f, value);
    }

    public float AttackRangeProperty
    {
        get => attackRange;
        set => attackRange = Mathf.Max(0.1f, value);
    }

    public float DetectionRangeProperty
    {
        get => detectionRange;
        set => detectionRange = Mathf.Max(0.1f, value);
    }

    public float AttackDamageProperty
    {
        get => attackDamage;
        set => attackDamage = Mathf.Max(0f, value);
    }

    public float AttackCooldownProperty
    {
        get => attackCooldown;
        set => attackCooldown = Mathf.Max(0.05f, value);
    }

    public float ArcherHitDelayProperty
    {
        get => archerHitDelay;
        set => archerHitDelay = Mathf.Max(0.02f, value);
    }

    public float ArcherAimDelayProperty
    {
        get => archerAimDelay;
        set => archerAimDelay = Mathf.Max(0f, value);
    }

    public GameObject ArrowPrefabProperty
    {
        get => arrowPrefab;
        set => arrowPrefab = value;
    }

    public Transform ArrowSpawnPointProperty
    {
        get => arrowSpawnPoint;
        set => arrowSpawnPoint = value;
    }

    public bool LockTargetBeforeAttack
    {
        get => lockTargetBeforeAttack;
        set => lockTargetBeforeAttack = value;
    }

    public bool RequireTargetLock
    {
        get => lockTargetBeforeAttack;
        set => lockTargetBeforeAttack = value;
    }

    public float LockOnFireDelay
    {
        get => lockOnFireDelay;
        set => lockOnFireDelay = Mathf.Max(0f, value);
    }

    public float TargetLockAngleThreshold
    {
        get => targetLockAngleThreshold;
        set => targetLockAngleThreshold = Mathf.Clamp(value, 1f, 45f);
    }

    public bool RelockBetweenShots
    {
        get => relockBetweenShots;
        set => relockBetweenShots = value;
    }

    public bool IsTargetLockedOn => isTargetLocked;

    public void SetStationary(bool stationary)
    {
        isStationary = stationary;
        if (agent != null)
        {
            if (isStationary)
            {
                // Disable agent completely so Unity does not snap unit down to the ground NavMesh surface
                if (agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                    agent.ResetPath();
                }
                agent.enabled = false;
            }
            else
            {
                agent.enabled = true;
                if (agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.speed = EffectiveMoveSpeed;
                }
            }
        }
        if (isStationary)
        {
            SetRunningAnimation(false);
            if (currentState == UnitState.Moving || currentState == UnitState.Chasing)
                currentState = UnitState.Idle;
        }
    }

    public void ConfigureArchery(
        float newAttackRange,
        float newDetectionRange,
        float newAttackCooldown,
        float newDamage,
        bool stationary,
        bool allowRotation,
        bool immuneToPeasants,
        GameObject newArrowPrefab = null,
        Transform newArrowSpawnPoint = null,
        float newHitDelay = 0.22f,
        float newAimDelay = 0.1f,
        bool newLockTargetBeforeAttack = true,
        float newLockOnFireDelay = 0.7f,
        float newTargetLockAngleThreshold = 10f,
        bool newRelockBetweenShots = false)
    {
        unitType = UnitCombatType.Archer;
        attackRange = Mathf.Max(1f, newAttackRange);
        detectionRange = Mathf.Max(attackRange, newDetectionRange);
        attackCooldown = Mathf.Max(0.05f, newAttackCooldown);
        attackDamage = Mathf.Max(0f, newDamage);
        archerHitDelay = Mathf.Max(0.02f, newHitDelay);
        archerAimDelay = Mathf.Max(0f, newAimDelay);
        lockTargetBeforeAttack = newLockTargetBeforeAttack;
        lockOnFireDelay = Mathf.Max(0f, newLockOnFireDelay);
        targetLockAngleThreshold = Mathf.Clamp(newTargetLockAngleThreshold, 1f, 45f);
        relockBetweenShots = newRelockBetweenShots;
        isStationary = stationary;
        canRotate = allowRotation;
        cannotBeAttackedByPeasants = immuneToPeasants;

        if (newArrowPrefab != null) arrowPrefab = newArrowPrefab;
        if (newArrowSpawnPoint != null) arrowSpawnPoint = newArrowSpawnPoint;

        SetStationary(stationary);
    }

    public void SetUnitType(UnitCombatType type, GameObject customArrowPrefab = null, Transform customSpawnPoint = null)
    {
        unitType = type;
        if (type == UnitCombatType.Archer)
        {
            if (attackRange <= 2f) attackRange = 7.5f;
            if (detectionRange <= 8f) detectionRange = 11f;
            if (attackCooldown <= 1.4f) attackCooldown = 1.35f;
            if (customArrowPrefab != null) arrowPrefab = customArrowPrefab;
            if (customSpawnPoint != null) arrowSpawnPoint = customSpawnPoint;
        }
    }

    public int TeamId
    {
        get => teamId;
        set
        {
            teamId = value;
            ApplyTeamVisuals();
        }
    }

    public void SetTeamId(int newTeamId)
    {
        teamId = newTeamId;
        ApplyTeamVisuals();
    }
    public string UnitName => unitName;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;
    public UnitState CurrentState => currentState;
    public Transform CurrentTarget => currentTarget;
    public string RunningBoolName => runningBool;
    public string AttackTriggerName => attackTrigger;
    public string HurtTriggerName => hurtTrigger;
    public string DeathTriggerName => deathTrigger;
    public bool IsRunning
    {
        get
        {
            if (isDead) return false;
            if (hasAnimator && animator != null && animator.GetBool(runningBool)) return true;
            if (currentState == UnitState.Moving || currentState == UnitState.Chasing) return true;
            if (agent != null && agent.enabled && agent.isOnNavMesh && agent.velocity.sqrMagnitude > 0.05f) return true;
            return false;
        }
    }

    public void SetHealthNetworked(float newHp)
    {
        currentHealth = Mathf.Max(0f, newHp);
        UpdateHealthBar();
    }

    public void DieNetworked()
    {
        Die();
    }

    public void PlayAttackAnimationNetworked()
    {
        if (hasAnimator && !isDead && animator != null)
        {
            animator.ResetTrigger(attackTrigger);

            float animSpeed = 1f;
            if (unitType == UnitCombatType.Archer)
            {
                animSpeed = Mathf.Clamp(1.5f / Mathf.Max(0.15f, EffectiveAttackCooldown), 1f, 3.5f);
            }
            else if (powerMultiplier > 1f)
            {
                animSpeed = Mathf.Clamp(powerMultiplier, 1f, 3f);
            }
            animator.speed = animSpeed;

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName("attack") || stateInfo.IsName("Attack") || stateInfo.IsTag("Attack"))
            {
                animator.Play("attack", 0, 0f);
            }
            else
            {
                animator.SetTrigger(attackTrigger);
            }
            PlayAttackSound();

            // For ranged archer proxies, launch visual arrow matching bow release frame
            if (unitType == UnitCombatType.Archer && arrowPrefab != null)
            {
                if (hitRoutine != null) StopCoroutine(hitRoutine);
                hitRoutine = StartCoroutine(SpawnNetworkProxyArrowRoutine());
            }
        }
    }

    private System.Collections.IEnumerator SpawnNetworkProxyArrowRoutine()
    {
        yield return new WaitForSeconds(EffectiveHitDelay);
        if (isDead) yield break;

        Vector3 spawnPos = arrowSpawnPoint != null ? arrowSpawnPoint.position :
                           (attackPoint != null ? attackPoint.position : transform.position + Vector3.up * 1.2f);
        Vector3 aimDir = transform.forward;
        Vector3 targetPos = spawnPos + aimDir * attackRange;

        GameObject arrowObj = Instantiate(arrowPrefab, spawnPos, arrowPrefab.transform.rotation);
        ArrowProjectile arrow = arrowObj.GetComponent<ArrowProjectile>();
        if (arrow == null) arrow = arrowObj.AddComponent<ArrowProjectile>();
        arrow.Launch(null, targetPos, 0f, this);

        float remainingDuration = Mathf.Max(0.05f, EffectiveAttackCooldown - EffectiveHitDelay);
        yield return new WaitForSeconds(remainingDuration);
        if (hasAnimator && animator != null)
        {
            animator.speed = 1f;
        }
    }

    public void PlayHurtAnimationNetworked()
    {
        if (hasAnimator && !isDead && animator != null)
        {
            animator.SetTrigger(hurtTrigger);
        }
    }

    // Network proxy follower state (used on Guest in PUN2 multiplayer)
    private Vector3 networkTargetPosition;
    private Quaternion networkTargetRotation;
    private bool networkTargetRunning;
    private bool hasReceivedNetworkState = false;

    public void UpdateNetworkProxyState(Vector3 pos, Quaternion rot, float hp, byte state, bool isRunning)
    {
        networkTargetPosition = pos;
        networkTargetRotation = rot;
        networkTargetRunning = isRunning;

        if (!hasReceivedNetworkState)
        {
            transform.position = pos;
            transform.rotation = rot;
            hasReceivedNetworkState = true;
        }

        if (Mathf.Abs(currentHealth - hp) > 0.05f)
        {
            SetHealthNetworked(hp);
        }

        if (hp <= 0f && !isDead)
        {
            DieNetworked();
            return;
        }

        if (isDead) return;

        if (state == (byte)UnitState.Attacking && currentState != UnitState.Attacking)
        {
            PlayAttackAnimationNetworked();
        }
        else if (state == (byte)UnitState.Hurt && currentState != UnitState.Hurt)
        {
            PlayHurtAnimationNetworked();
        }

        SetRunningAnimation(isRunning);
        currentState = (UnitState)state;
    }

    public Renderer[] unitRenderers;
    public Texture texture_opp;
    public Texture texture_self;
    private static Texture cachedSelfTexture = null;
    private static Texture cachedOppTexture = null;

    private void Reset()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        audioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        transform.localScale = DeployedScale;

        // If parented under an objective (such as an archery house/tower), ensure matching team ID
        PeasantObjectiveTarget parentObjective = GetComponentInParent<PeasantObjectiveTarget>();
        if (parentObjective != null && teamId != parentObjective.TeamId)
        {
            teamId = parentObjective.TeamId;
        }

        if (healthBarCube != null)
        {
            initialHealthBarScale = healthBarCube.localScale;
            initialHealthBarLocalPos = healthBarCube.localPosition;
        }

        EnsureJuicyHealthBar();
        ApplyTeamVisuals();

        if (BattleAIManager.Instance != null)
        {
            BattleAIManager.Instance.RegisterUnit(this);
        }
        nextTargetEvaluationTime = Time.time + Random.Range(0.05f, 0.35f);

        // Deploy thud (rate-limited by KoSAudio so mass spawns stay clean)
        if (MatchReadyScreen.IsMatchStarted) KoSAudio.Play("unit_deploy", transform.position);

        // Play scream at the start (spawn / battle charge) and schedule mid-battle screams
        if (HasScreamClips())
        {
            StartCoroutine(PlayStartScreamRoutine());
        }
        ScheduleNextMidBattleScream();
    }

    private void OnDestroy()
    {
        if (NetworkBattleBridge.Instance != null)
        {
            NetworkBattleBridge.Instance.UnregisterUnit(this);
        }

        if (BattleAIManager.Instance != null)
        {
            BattleAIManager.Instance.UnregisterUnit(this);
        }
    }

    private void EnsureJuicyHealthBar()
    {
        if (juicyHealthBar == null)
        {
            juicyHealthBar = GetComponentInChildren<JuicyUnitHealthBar>(true);
            if (juicyHealthBar == null)
            {
                Transform hbParent = healthBarCube != null ? healthBarCube.parent : transform.Find("healthbar");
                if (hbParent == null)
                {
                    Transform[] allT = GetComponentsInChildren<Transform>(true);
                    for (int i = 0; i < allT.Length; i++)
                    {
                        if (allT[i].name.Equals("healthbar", System.StringComparison.OrdinalIgnoreCase))
                        {
                            hbParent = allT[i];
                            break;
                        }
                    }
                }

                if (hbParent != null)
                {
                    juicyHealthBar = hbParent.GetComponent<JuicyUnitHealthBar>();
                    if (juicyHealthBar == null)
                        juicyHealthBar = hbParent.gameObject.AddComponent<JuicyUnitHealthBar>();
                }
            }
        }

        if (juicyHealthBar != null)
        {
            juicyHealthBar.Setup(transform, teamId, maxHealth, IsMachine);
        }
    }

    public void ApplyTeamVisuals()
    {
        if (juicyHealthBar != null)
        {
            juicyHealthBar.ApplyTeamTheming(teamId);
        }

        int localTeam = GameModeManager.LocalTeamId;
        bool isFriendly = (teamId == localTeam);

        // Health bar color based on local player's team: Blue for friendly, Red for enemy
        if (healthBarCube != null)
        {
            Renderer hbRenderer = healthBarCube.GetComponent<Renderer>();
            if (hbRenderer != null)
            {
                if (_mpb == null) _mpb = new MaterialPropertyBlock();
                hbRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor("_Color", isFriendly ? new Color(0.15f, 0.55f, 1f) : new Color(0.95f, 0.2f, 0.2f));
                hbRenderer.SetPropertyBlock(_mpb);
            }
        }

        // Auto-discover renderers if not wired in inspector
        if (unitRenderers == null || unitRenderers.Length == 0)
        {
            Renderer[] all = GetComponentsInChildren<Renderer>(true);
            List<Renderer> validList = new List<Renderer>();
            Renderer hbRenderer = healthBarCube != null ? healthBarCube.GetComponent<Renderer>() : null;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i] != hbRenderer && all[i].gameObject != (healthBarCube != null ? healthBarCube.gameObject : null))
                {
                    validList.Add(all[i]);
                }
            }
            unitRenderers = validList.ToArray();
        }

        // Cache textures for runtime-spawned prefabs
        if (texture_self != null && cachedSelfTexture == null) cachedSelfTexture = texture_self;
        if (texture_opp != null && cachedOppTexture == null) cachedOppTexture = texture_opp;
        if (texture_self == null) texture_self = cachedSelfTexture;
        if (texture_opp == null) texture_opp = cachedOppTexture;

        if (texture_self == null)
            texture_self = Resources.Load<Texture>("Units/TT_RTS_Units_blueB");
        if (texture_opp == null)
            texture_opp = Resources.Load<Texture>("Units/TT_RTS_Units_red");

        if (texture_self == null)
            texture_self = RuntimeAssets.LoadAssetAtPath<Texture>("Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/models/materials/color/Units/Textures/TT_RTS_Units_blueB.tga");
        if (texture_opp == null)
            texture_opp = RuntimeAssets.LoadAssetAtPath<Texture>("Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/models/materials/color/Units/Textures/TT_RTS_Units_red.tga");

        // Texture based on local player's team: self for friendly, opp for opponent.
        // Use MaterialPropertyBlock so we don't create per-instance Material objects,
        // which would break GPU instancing/batching and cause GC allocations on every spawn.
        //
        // Performance: URP shaders (Lit, SimpleLit, ...) sample _BaseMap and ignore _MainTex, so on
        // those renderers the block changed nothing visually but still made every unit
        // incompatible with the SRP Batcher (one slow draw path per unit renderer). Only apply the
        // block where the material really samples _MainTex (legacy/custom shaders without _BaseMap).
        Texture targetTexture = isFriendly ? texture_self : (texture_opp != null ? texture_opp : texture_self);
        if (targetTexture != null && unitRenderers != null)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            for (int i = 0; i < unitRenderers.Length; i++)
            {
                Renderer r = unitRenderers[i];
                if (r == null) continue;

                if (UsesMainTexSampling(r))
                {
                    r.GetPropertyBlock(_mpb);
                    _mpb.SetTexture("_MainTex", targetTexture);
                    r.SetPropertyBlock(_mpb);
                }
                else if (r.HasPropertyBlock())
                {
                    // Clear any block left from an earlier spawn (pooled units) so SRP Batcher applies.
                    r.SetPropertyBlock(null);
                }
            }
        }
    }

    // Cached per shader: true if the shader has _MainTex but no _BaseMap (so _MainTex is what it samples).
    private static readonly Dictionary<Shader, bool> s_mainTexShaderCache = new Dictionary<Shader, bool>();
    private static readonly List<Material> s_sharedMatsScratch = new List<Material>(4);

    private static bool UsesMainTexSampling(Renderer r)
    {
        r.GetSharedMaterials(s_sharedMatsScratch);
        for (int m = 0; m < s_sharedMatsScratch.Count; m++)
        {
            Material mat = s_sharedMatsScratch[m];
            if (mat == null || mat.shader == null) continue;
            if (!s_mainTexShaderCache.TryGetValue(mat.shader, out bool usesMainTex))
            {
                usesMainTex = mat.HasProperty("_MainTex") && !mat.HasProperty("_BaseMap");
                s_mainTexShaderCache[mat.shader] = usesMainTex;
            }
            if (usesMainTex) return true;
        }
        return false;
    }

    private void Awake()
    {
        transform.localScale = DeployedScale;

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        if (audioSource != null)
        {
            audioSource.spatialBlend = 1f; // 3D spatial sound
            audioSource.playOnAwake = false;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 35f;
        }

        hasAnimator = animator != null;
        currentHealth = maxHealth;
        UpdateHealthBar();

        // Compute cached unit-type values once so properties don't string-search every frame.
        string goNameLower = gameObject.name.ToLower();
        _cachedIsBallista = goNameLower.Contains("ballista") ||
            (!string.IsNullOrEmpty(unitName) && unitName.IndexOf("ballista", System.StringComparison.OrdinalIgnoreCase) >= 0);
        _cachedIsMachine = isMachine || _cachedIsBallista ||
            goNameLower.Contains("catapult") || goNameLower.Contains("ram") ||
            goNameLower.Contains("trebuchet") || goNameLower.Contains("cannon") ||
            goNameLower.Contains("siege") || goNameLower.Contains("wagon") ||
            goNameLower.Contains("cart") || goNameLower.Contains("machine") ||
            (!string.IsNullOrEmpty(unitName) && (
                unitName.IndexOf("catapult",  System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                unitName.IndexOf("ram",       System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                unitName.IndexOf("trebuchet", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                unitName.IndexOf("cannon",    System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                unitName.IndexOf("siege",     System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                unitName.IndexOf("wagon",     System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                unitName.IndexOf("cart",      System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                unitName.IndexOf("machine",   System.StringComparison.OrdinalIgnoreCase) >= 0));
        bool isMounted = goNameLower.Contains("mounted") || goNameLower.Contains("cavalry") || goNameLower.Contains("horse");
        _cachedPhysicalRadius         = (_cachedIsBallista || _cachedIsMachine) ? 1.35f : (isMounted ? 0.85f : 0.50f);
        _cachedDesiredSpacingDistance = (_cachedIsBallista || _cachedIsMachine) ? 3.4f  : (isMounted ? 2.2f  : 1.45f);

        _mpb = new MaterialPropertyBlock();

        // Global physics layer setting — only needs to run once across all units.
        if (!s_physicsLayerIgnoreSet)
        {
            s_physicsLayerIgnoreSet = true;
            Physics.IgnoreLayerCollision(6, 6, true);
        }

        if (agent != null)
        {
            if (isStationary)
            {
                // Disable agent so Unity does not warp or snap the unit to ground NavMesh
                agent.enabled = false;
            }
            else
            {
                agent.speed = moveSpeed;
                agent.acceleration = acceleration;
                agent.angularSpeed = turnSpeed;
                agent.stoppingDistance = Mathf.Max(stoppingDistance, 0.25f);
                agent.updateRotation = updateRotation;
                // Compact radius (0.38m) prevents huge avoidance bubbles from blocking movement in groups
                agent.radius = Mathf.Clamp(agent.radius, 0.32f, 0.40f);
                // HighQuality avoidance is too expensive for 30+ agents on mobile.
                // LowQuality still steers around obstacles; avoidance priority keeps formation order.
#if UNITY_ANDROID && !UNITY_EDITOR
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
#else
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
#endif
                agent.avoidancePriority = 50;
            }
        }

        if (hasAnimator)
        {
            // CullUpdateTransforms skips expensive transform writes when off-screen,
            // while keeping animation logic running so state is correct when it comes back.
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            animator.SetBool(runningBool, false);

            if (!string.IsNullOrEmpty(speedFloat))
            {
                foreach (AnimatorControllerParameter param in animator.parameters)
                {
                    if (param.type == AnimatorControllerParameterType.Float && param.name == speedFloat)
                    {
                        hasSpeedFloatParam = true;
                        break;
                    }
                }
            }
        }
    }

    private void UpdateHealthBar()
    {
        if (juicyHealthBar != null)
        {
            juicyHealthBar.SetHealth(currentHealth, maxHealth);
        }

        if (healthBarCube == null) return;

        float healthPercent = Mathf.Clamp01(currentHealth / maxHealth);
        Vector3 newScale = initialHealthBarScale;
        Vector3 newPos = initialHealthBarLocalPos;

        if (healthBarScaleAxis.x > 0f)
        {
            newScale.x = initialHealthBarScale.x * healthPercent;
            // Shift position to the left by half the lost width so the left edge stays fixed
            newPos.x = initialHealthBarLocalPos.x - (initialHealthBarScale.x * (1f - healthPercent) * 0.5f);
        }
        if (healthBarScaleAxis.y > 0f)
        {
            newScale.y = initialHealthBarScale.y * healthPercent;
            newPos.y = initialHealthBarLocalPos.y - (initialHealthBarScale.y * (1f - healthPercent) * 0.5f);
        }
        if (healthBarScaleAxis.z > 0f)
        {
            newScale.z = initialHealthBarScale.z * healthPercent;
            newPos.z = initialHealthBarLocalPos.z - (initialHealthBarScale.z * (1f - healthPercent) * 0.5f);
        }

        healthBarCube.localScale = newScale;
        healthBarCube.localPosition = newPos;
    }

    private void Update()
    {
        if (isDead) return;

        // Freeze combat logic during pre-match countdown
        if (MatchReadyScreen.Instance != null && !MatchReadyScreen.IsMatchStarted)
        {
            SetRunningAnimation(false);
            return;
        }

        // In PUN2 multiplayer, non-MasterClient (Guest) acts as a smooth proxy follower.
        // MasterClient is the sole authority for AI navigation, targeting, and combat logic.
        if (NetworkBattleBridge.Instance != null && NetworkBattleBridge.Instance.IsMultiplayer && !Photon.Pun.PhotonNetwork.IsMasterClient)
        {
            if (agent != null && agent.enabled)
            {
                agent.enabled = false;
            }

            if (hasReceivedNetworkState)
            {
                float dist = Vector3.Distance(transform.position, networkTargetPosition);
                if (dist > 4f)
                {
                    transform.position = networkTargetPosition;
                    transform.rotation = networkTargetRotation;
                }
                else
                {
                    transform.position = Vector3.Lerp(transform.position, networkTargetPosition, Time.deltaTime * 18f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, networkTargetRotation, Time.deltaTime * 18f);
                }

                // Continuously maintain running animation state on proxy at 60+ FPS
                SetRunningAnimation(networkTargetRunning);
            }
            return;
        }

        if (!isStationary && agent == null) return;

        if (isHurt)
        {
            SetRunningAnimation(false);
            return;
        }

        // Screaming occasionally in the middle of battle (random clip from array)
        if (HasScreamClips() && Time.time >= nextMidBattleScreamTime)
        {
            ScheduleNextMidBattleScream();
            if (currentState == UnitState.Chasing || currentState == UnitState.Attacking || currentState == UnitState.Moving)
            {
                PlayScreamSound();
            }
        }

        // Do not interrupt an active attack animation mid-shot with re-targeting or target clearing
        if (currentState != UnitState.Attacking)
        {
            if (currentTarget != null && !IsTargetValid(currentTarget))
                ClearTarget();

            // Combat priority and tactical coordination:
            // Use BattleAIManager to balance targets across all units so they don't dogpile on one foe.
            if (canAutoAcquireTargets && !isCommandMoving)
            {
                if (currentTarget == null)
                {
                    AcquireTarget();
                }
                else if (Time.time >= nextTargetEvaluationTime)
                {
                    nextTargetEvaluationTime = Time.time + Random.Range(0.35f, 0.55f);
                    if (BattleAIManager.Instance != null)
                    {
                        Transform optimalTarget = BattleAIManager.Instance.GetBestTargetForUnit(this);
                        if (optimalTarget != null && optimalTarget != currentTarget)
                        {
                            SetTarget(optimalTarget);
                        }
                    }
                    else if (currentTarget.GetComponentInParent<PeasantObjectiveTarget>() != null)
                    {
                        PeasantUnitController nearbyEnemy = FindNearestLivingEnemyUnit();
                        // In fallback mode, only pull off the castle if the enemy is close (within detectionRange)
                        if (nearbyEnemy != null && Vector3.Distance(transform.position, nearbyEnemy.transform.position) <= detectionRange)
                        {
                            ClearTarget();
                            AcquireTarget();
                        }
                    }
                }
            }
        }

        if (currentTarget != null)
        {
            isCommandMoving = false;
            HandleCombatTarget();
            if (isStationary && canRotate && currentTarget != null)
            {
                FaceTarget(currentTarget.position);
            }
        }
        else if (isCommandMoving)
        {
            HandleMoveCommand();
        }
        else
        {
            UpdateMovementAnimation();
            if (currentState != UnitState.Hurt && currentState != UnitState.Dead)
                currentState = IsAgentMoving() ? UnitState.Moving : UnitState.Idle;

            // Maintain personal distance while idle so standing units don't gather into a clump
            if (maintainPersonalSpaceWhenIdle && currentState == UnitState.Idle && Time.time >= nextIdleCheckTime)
            {
                nextIdleCheckTime = Time.time + 0.4f;
                MaintainIdleSeparation();
            }
        }

        // Active continuous friendly separation keeps units well-spaced whether marching, fighting, or standing
        ResolveUnitCrowding();
    }

    private void ResolveUnitCrowding()
    {
        // Only apply gentle separation when stopped/idle so it never interferes with smooth NavMeshAgent path movement while walking
        if (isStationary || isDead || !applyFlockingSeparation || BattleAIManager.Instance == null) return;
        if (IsAgentMoving()) return;

        Vector3 push = BattleAIManager.Instance.GetSeparationVector(this);
        if (push.sqrMagnitude > 0.005f)
        {
            Vector3 nudge = push * (Time.deltaTime * 1.5f);
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.Move(nudge);
            }
        }
    }

    /// <summary>Issue a movement order, suitable for right-click RTS input.</summary>
    public void MoveTo(Vector3 destination)
    {
        if (isStationary || isDead || agent == null || !agent.isOnNavMesh) return;

        ClearTarget();
        isCommandMoving = true;
        commandDestination = destination;
        agent.isStopped = false;
        agent.speed = EffectiveMoveSpeed;
        agent.SetDestination(destination);
        currentState = UnitState.Moving;
        SetRunningAnimation(true);
    }

    /// <summary>Assign a specific enemy target.</summary>
    public void SetTarget(Transform target)
    {
        if (isDead || target == null || target == transform) return;
        if (!IsTargetValid(target)) return;

        if (currentTarget != null && currentTarget != target && BattleAIManager.Instance != null)
        {
            BattleAIManager.Instance.NotifyTargetAbandoned(this, currentTarget);
        }

        if (currentTarget != target)
        {
            isTargetLocked = false;
            targetLockAcquiredTime = -1f;
            lastLockedTarget = null;
        }

        currentTarget = target;
        isCommandMoving = false;

        if (IsTargetDebugUnit)
        {
            // Debug.Log($"<color=cyan>[{name} DEBUG] Target ASSIGNED:</color> '{(target != null ? target.name : "null")}' at pos={target.position}, dist={Vector3.Distance(transform.position, target.position):F2}m");
        }

        if (BattleAIManager.Instance != null)
        {
            BattleAIManager.Instance.AssignTarget(this, target);
        }

        if (!isStationary && agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
    }

    public void StopOrders()
    {
        if (isDead) return;
        ClearTarget();
        if (hitRoutine != null)
        {
            StopCoroutine(hitRoutine);
            hitRoutine = null;
        }
        if (hasAnimator && animator != null)
        {
            animator.speed = 1f;
        }
        isCommandMoving = false;
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
        SetRunningAnimation(false);
        currentState = UnitState.Idle;
    }

    private void HandleMoveCommand()
    {
        if (!agent.isOnNavMesh)
        {
            SetRunningAnimation(false);
            return;
        }

        agent.isStopped = false;
        if (!agent.pathPending && agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, 0.1f))
        {
            isCommandMoving = false;
            agent.ResetPath();
            SetRunningAnimation(false);
            currentState = UnitState.Idle;
        }
        else
        {
            SetRunningAnimation(IsAgentMoving());
            currentState = UnitState.Moving;
        }
    }

    private void HandleCombatTarget()
    {
        if (currentTarget == null || !IsTargetValid(currentTarget))
        {
            ClearTarget();
            if (currentState == UnitState.Attacking && hitRoutine != null)
            {
                StopCoroutine(hitRoutine);
                hitRoutine = null;
            }
            if (hasAnimator && animator != null)
            {
                animator.speed = 1f;
            }
            currentState = UnitState.Idle;
            return;
        }

        // If the unit is currently in the middle of executing an attack, protect the attack action!
        // Do NOT let distance checks or line-of-sight fluctuations interrupt the attack or switch state to Chasing/Idle mid-shot.
        if (currentState == UnitState.Attacking)
        {
            if (canRotate && currentTarget != null)
            {
                FaceTarget(currentTarget.position);
            }
            return;
        }

        if (!isStationary && (agent == null || !agent.enabled || !agent.isOnNavMesh))
        {
            if (IsTargetDebugUnit && Time.time >= nextDebugLogTime)
            {
                nextDebugLogTime = Time.time + 0.5f;
                // Debug.LogWarning($"[{name} DEBUG] HandleCombatTarget Aborted! Agent state: enabled={(agent != null && agent.enabled)}, isOnNavMesh={(agent != null && agent.isOnNavMesh)}");
            }
            return;
        }

        // Check if targeting a tower or objective:
        TowerCombatController targetTower = currentTarget.GetComponentInParent<TowerCombatController>();
        if (targetTower != null)
        {
            // STRICT FRIENDLY FIRE GUARD: If target is on the same team, NEVER attack it!
            if (targetTower.TeamId == teamId)
            {
                // Only unoccupied wall towers (not autonomous base towers) can be garrisoned by friendly archers (NEVER ballistas or melee)
                if (!targetTower.AttacksAutonomously && !targetTower.IsOccupied && !targetTower.IsDestroyed && IsEligibleTowerArcher && !IsBallista)
                {
                    float distToTower = Vector3.Distance(transform.position, targetTower.transform.position);
                    if (distToTower <= 7.5f)
                    {
                        if (targetTower.TryOccupyTower(this))
                        {
                            return;
                        }
                    }
                }
                else
                {
                    // Friendly base tower, occupied tower, or non-archer unit: immediately clear target so we NEVER attack our own tower!
                    ClearTarget();
                    return;
                }
            }
        }

        PeasantObjectiveTarget currentObjective = currentTarget.GetComponentInParent<PeasantObjectiveTarget>();
        if (currentObjective != null && currentObjective.TeamId == teamId)
        {
            // Never attack friendly objectives/castles/towers
            ClearTarget();
            return;
        }

        // Use coordinated surround/flanking slot from BattleAIManager (maintains reasonable distance)
        Vector3 destination = currentTarget.position;
        if (BattleAIManager.Instance != null)
        {
            destination = BattleAIManager.Instance.GetCoordinatedAttackPosition(this, currentTarget);
        }

        float distanceToTarget = GetDistanceToTarget(currentTarget);
        float distanceToSlot = Vector3.Distance(transform.position, destination);

        // Hysteresis buffer prevents rapid toggling between moving and stopping when target takes small steps
        float engageDist = attackRange + attackRangePadding + 0.25f;
        float disengageDist = (unitType == UnitCombatType.Archer)
            ? (attackRange * 1.25f + 1.0f)
            : (attackRange + attackRangePadding + 0.85f);

        bool isCurrentlyStoppedOrInCombat = (currentState == UnitState.Attacking) ||
            (!isStationary && agent != null && agent.isOnNavMesh && agent.isStopped);

        bool inAttackRange = isCurrentlyStoppedOrInCombat
            ? (distanceToTarget <= disengageDist)
            : (distanceToTarget <= engageDist);

        bool reachedSlot = distanceToSlot <= Mathf.Max(agent.stoppingDistance + 0.35f, 0.65f);

        // Stuck resolution: if an agent is trying to reach its destination but cannot move (velocity ~ 0) for > 0.35s
        // because friendly units are in front, resolve it immediately!
        if (!isStationary && agent != null && agent.enabled && agent.isOnNavMesh && !agent.isStopped && agent.hasPath)
        {
            if (agent.velocity.sqrMagnitude < 0.05f && !agent.pathPending)
            {
                agentStuckTimer += Time.deltaTime;
                // If near the enemy target (within 2.8m for melee, or in archer range), engage immediately!
                if (agentStuckTimer > 0.35f && distanceToTarget <= (attackRange + attackRangePadding + 1.2f))
                {
                    inAttackRange = true;
                }
                else if (agentStuckTimer > 0.65f)
                {
                    // Blocked on the march: take a lateral step to bypass the obstacle/crowd
                    Vector3 lateral = Vector3.Cross((destination - transform.position).normalized, Vector3.up) * (Random.value > 0.5f ? 1.0f : -1.0f);
                    Vector3 alternatePos = transform.position + lateral;
                    if (NavMesh.SamplePosition(alternatePos, out NavMeshHit altHit, 1.5f, NavMesh.AllAreas))
                    {
                        agent.SetDestination(altHit.position);
                    }
                    agentStuckTimer = 0f;
                }
            }
            else
            {
                agentStuckTimer = 0f;
            }
        }
        else
        {
            agentStuckTimer = 0f;
        }

        // Check if ranged Archer has a clear line of sight (not obstructed by castle or obstacles)
        bool hasClearTrajectory = true;
        if (unitType == UnitCombatType.Archer && checkLineOfSightBeforeAttack)
        {
            hasClearTrajectory = HasClearLineOfSightToTarget(currentTarget);
        }

        // Periodic diagnostic tracking (for diagnostic target unit)
        if (IsTargetDebugUnit && Time.time >= nextDebugLogTime)
        {
            nextDebugLogTime = Time.time + 0.5f;
        }

        bool tooCloseToFire = minAttackRange > 0f && distanceToTarget < minAttackRange;
        if (!tooCloseToFire && hasClearTrajectory && (inAttackRange || (!isStationary && reachedSlot && distanceToTarget <= disengageDist)))
        {
            if (IsTargetDebugUnit && lastDebugAction != "ATTACK")
            {
                lastDebugAction = "ATTACK";
            }

            if (stopToAttack || isStationary)
            {
                if (agent != null && agent.isOnNavMesh && !agent.isStopped)
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                    agent.ResetPath();
                    agent.avoidancePriority = 30; // High priority: moving units steer around stopped combatants
                }
            }

            SetRunningAnimation(false);
            if (faceTargetBeforeAttack && canRotate) FaceTarget(currentTarget.position);

            // Archer aim and target tracking:
            // Archer faces target smoothly. If generally facing (within 35 degrees) and cooldown ready, shoot!
            if (unitType == UnitCombatType.Archer)
            {
                bool isFacing = IsTargetLocked(currentTarget);

                if (!isFacing)
                {
                    if (currentState != UnitState.Attacking)
                        currentState = UnitState.Idle;
                    return;
                }
            }

            if (Time.time >= nextAttackTime && !isHurt)
            {
                BeginAttack();
            }
            else if (currentState != UnitState.Attacking)
            {
                currentState = UnitState.Idle;
            }
        }
        else
        {
            if (isStationary)
            {
                if (agent != null && agent.isOnNavMesh && !agent.isStopped)
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                    agent.ResetPath();
                }
                SetRunningAnimation(false);
                currentState = UnitState.Idle;

                if (canRotate && currentTarget != null)
                {
                    FaceTarget(currentTarget.position);
                }

                // If target moved outside of attack range or trajectory blocked, clear target to acquire an attackable enemy
                if (currentState != UnitState.Attacking && (distanceToTarget > disengageDist || !hasClearTrajectory))
                {
                    ClearTarget();
                }
                return;
            }

            // Reset aim stance when moving / repositioning
            archerAimReadyTime = 0f;
            isTargetLocked = false;
            targetLockAcquiredTime = -1f;
            lastLockedTarget = null;

            // If trajectory is blocked by an obstacle (e.g. castle wall), calculate a repositioning destination
            Vector3 moveDest = destination;
            if (!hasClearTrajectory && unitType == UnitCombatType.Archer)
            {
                moveDest = FindRepositionDestinationAroundObstacle(currentTarget);
                lastRepositionAttempted = true;
                lastRepositionDest = moveDest;
            }
            else
            {
                lastRepositionAttempted = false;
            }

            if (IsTargetDebugUnit && lastDebugAction != "CHASE")
            {
                lastDebugAction = "CHASE";
            }

            if (agent.isStopped)
            {
                agent.isStopped = false;
            }
            agent.speed = EffectiveMoveSpeed;
            agent.avoidancePriority = 50; // Normal priority while marching

            // Only update destination if slot/target actually shifted to prevent constant repath stutter
            if (!agent.hasPath || Vector3.Distance(agent.destination, moveDest) > 0.45f)
            {
                agent.SetDestination(moveDest);
            }

            SetRunningAnimation(IsAgentMoving());
            currentState = UnitState.Chasing;
        }
    }

    /// <summary>
    /// Checks if a direct straight line trajectory from this archer to the target is clear of castle walls/obstacles.
    /// </summary>
    public bool HasClearLineOfSightToTarget(Transform target)
    {
        if (target == null) return false;

        // If target IS the castle/objective itself, trajectory is valid as we want to shoot the castle!
        PeasantObjectiveTarget objective = target.GetComponentInParent<PeasantObjectiveTarget>();
        if (objective != null) return true;

        Vector3 start = transform.position + Vector3.up * 1.35f;
        Vector3 end = target.position + Vector3.up * 1.1f;

        Vector3 dir = end - start;
        float dist = dir.magnitude;
        if (dist < 0.5f) return true;

        lastLosStart = start;
        lastLosEnd = end;

        if (Physics.Raycast(start, dir.normalized, out RaycastHit hit, dist - 0.3f, lineOfSightObstacleLayers, QueryTriggerInteraction.Ignore))
        {
            lastLosHadHit = true;
            lastLosHit = hit;

            // If hit is this unit or target (or children of target), not blocked
            if (hit.transform == target || hit.transform.IsChildOf(target))
            {
                lastLosResult = true;
                return true;
            }
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                lastLosResult = true;
                return true;
            }

            // Other living peasant units walking in front should not permanently block an archer from firing
            PeasantUnitController otherUnit = hit.collider.GetComponentInParent<PeasantUnitController>();
            if (otherUnit != null)
            {
                lastLosResult = true;
                return true;
            }

            // If hit is ground/terrain, ignore unless it's a major hill blocking line of sight
            if (hit.collider is TerrainCollider || hit.collider.gameObject.name.IndexOf("Terrain", System.StringComparison.OrdinalIgnoreCase) >= 0 || hit.collider.gameObject.layer == LayerMask.NameToLayer("Ground"))
            {
                lastLosResult = true;
                return true;
            }

            // Blocked by castle wall or real obstacle!
            lastLosResult = false;
            return false;
        }

        lastLosHadHit = false;
        lastLosResult = true;
        return true;
    }

    private Vector3 FindRepositionDestinationAroundObstacle(Transform target)
    {
        Vector3 targetPos = target.position;
        Vector3 myPos = transform.position;
        Vector3 dirFromTarget = (myPos - targetPos).normalized;
        if (dirFromTarget.sqrMagnitude < 0.01f) dirFromTarget = Vector3.forward;

        float desiredDist = Mathf.Clamp(attackRange * 0.75f, 4.5f, 8.5f);

        // Test flanking angles (+45°, -45°, +90°, -90°, +135°, -135°) to find a clear vantage point
        float[] testAngles = { 45f, -45f, 90f, -90f, 135f, -135f };
        for (int i = 0; i < testAngles.Length; i++)
        {
            Vector3 testDir = Quaternion.Euler(0f, testAngles[i], 0f) * dirFromTarget;
            Vector3 candidatePos = targetPos + testDir * desiredDist;

            if (NavMesh.SamplePosition(candidatePos, out NavMeshHit navHit, 3f, NavMesh.AllAreas))
            {
                // Verify this vantage position has line of sight to the target
                Vector3 testEye = navHit.position + Vector3.up * 1.2f;
                Vector3 targetCenter = targetPos + Vector3.up * 1f;
                Vector3 rayDir = targetCenter - testEye;
                float rayDist = rayDir.magnitude;

                if (!Physics.Raycast(testEye, rayDir.normalized, out RaycastHit hit, rayDist - 0.2f, lineOfSightObstacleLayers, QueryTriggerInteraction.Ignore))
                {
                    return navHit.position;
                }
                else if (hit.transform == target || hit.transform.IsChildOf(target))
                {
                    return navHit.position;
                }
            }
        }

        // Fallback: move closer or laterally
        Vector3 lateral = Vector3.Cross(dirFromTarget, Vector3.up).normalized * 3f;
        if (NavMesh.SamplePosition(myPos + lateral, out NavMeshHit lateralHit, 3f, NavMesh.AllAreas))
        {
            return lateralHit.position;
        }

        return targetPos + dirFromTarget * desiredDist;
    }

    private void BeginAttack()
    {
        if (isDead || currentTarget == null || !IsTargetValid(currentTarget)) return;

        // In multiplayer, attacks and arrow launches are authoritative on MasterClient
        if (NetworkBattleBridge.Instance != null && NetworkBattleBridge.Instance.IsMultiplayer && !Photon.Pun.PhotonNetwork.IsMasterClient)
            return;

        // Do not interrupt an attack that is already actively running
        if (currentState == UnitState.Attacking && hitRoutine != null)
            return;

        targetWhenAttackStarted = currentTarget;
        lastAttackTargetPos = currentTarget.position;
        nextAttackTime = Time.time + EffectiveAttackCooldown;
        attackHitApplied = false;
        currentState = UnitState.Attacking;
        SetRunningAnimation(false);

        // For melee units, play attack grunt/swing at the start.
        // For archers, bow release sound plays when the arrow actually fires in ApplyAttackHitInternal.
        if (unitType != UnitCombatType.Archer)
        {
            PlayAttackSound();
        }

        if (hasAnimator && animator != null)
        {
            animator.ResetTrigger(attackTrigger);

            // Scale attack animation playback speed to match EffectiveAttackCooldown so the full draw/swing completes
            // cleanly instead of restarting from the middle.
            float animSpeed = 1f;
            if (unitType == UnitCombatType.Archer)
            {
                // Archer attack clip is approx 1.5s long at 1x speed. Scale to match cooldown so full animation fits.
                animSpeed = Mathf.Clamp(1.5f / Mathf.Max(0.15f, EffectiveAttackCooldown), 1f, 3.5f);
            }
            else if (powerMultiplier > 1f)
            {
                animSpeed = Mathf.Clamp(powerMultiplier, 1f, 3f);
            }
            animator.speed = animSpeed;

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName("attack") || stateInfo.IsName("Attack") || stateInfo.IsTag("Attack"))
            {
                animator.Play("attack", 0, 0f);
            }
            else
            {
                animator.SetTrigger(attackTrigger);
            }
        }

        if (hitRoutine != null) StopCoroutine(hitRoutine);
        hitRoutine = StartCoroutine(ApplyHitAfterDelay());
    }

    private IEnumerator ApplyHitAfterDelay()
    {
        yield return new WaitForSeconds(EffectiveHitDelay);
        if (!attackHitApplied && !isDead && !isHurt)
            ApplyAttackHitInternal();

        // Smooth follow-through: allow the visual strike/release follow-through to finish naturally (0.18s - 0.28s)
        // rather than locking the unit in frozen state for the entire attack cooldown.
        float followThrough = (unitType == UnitCombatType.Archer) ? 0.2f : 0.28f;
        if (animator != null && animator.speed > 0.05f)
        {
            followThrough /= animator.speed;
        }
        followThrough = Mathf.Min(followThrough, Mathf.Max(0.05f, EffectiveAttackCooldown - EffectiveHitDelay));
        yield return new WaitForSeconds(followThrough);

        if (hasAnimator && animator != null)
        {
            animator.speed = 1f;
        }

        if (currentState == UnitState.Attacking && !isDead && !isHurt)
        {
            currentState = UnitState.Idle;
        }

        hitRoutine = null;
    }

    /// <summary>
    /// Optional Animation Event: add an event named AnimationAttackHit to the
    /// attack clip at the actual impact frame. If used, it overrides the
    /// fallback hit delay for that attack.
    /// </summary>
    public void AnimationAttackHit()
    {
        if (isDead || isHurt || attackHitApplied) return;
        ApplyAttackHitInternal();
    }

    private void ApplyAttackHitInternal()
    {
        attackHitApplied = true;

        Transform activeTarget = currentTarget != null ? currentTarget : targetWhenAttackStarted;
        bool hasLivingTarget = activeTarget != null && IsTargetValid(activeTarget);

        // Friendly fire safeguard: abort immediately if target is a friendly objective
        PeasantObjectiveTarget targetObj = activeTarget != null ? activeTarget.GetComponentInParent<PeasantObjectiveTarget>() : null;
        if (targetObj != null && targetObj.TeamId == teamId)
        {
            ClearTarget();
            return;
        }

        // Melee units check reach so they don't hit if target ran away or died
        if (unitType != UnitCombatType.Archer)
        {
            if (!hasLivingTarget) return;
            float distance = GetDistanceToTarget(activeTarget);
            float maxAllowedReach = attackRange + attackRangePadding + 1.25f;
            if (distance > maxAllowedReach) return;
            KoSAudio.Play(IsMachine ? "machine_hit" : "melee_hit", activeTarget.position);
        }

        float finalDamage = EffectiveAttackDamage;

        // If this unit is an Archer, always launch an arrow projectile to match the bow release animation
        if (unitType == UnitCombatType.Archer)
        {
            // Play bow release / twang audio
            if (bowReleaseAudioClip != null)
            {
                PlayClipWithPitch(bowReleaseAudioClip, attackAudioVolume, "bow_release");
            }
            else
            {
                PlayAttackSound();
            }

            if (arrowPrefab != null)
            {
                Vector3 spawnPos = arrowSpawnPoint != null ? arrowSpawnPoint.position :
                                   (attackPoint != null ? attackPoint.position : transform.position + Vector3.up * 1.2f);

                GameObject arrowObj = Instantiate(arrowPrefab, spawnPos, arrowPrefab.transform.rotation);
                ArrowProjectile arrow = arrowObj.GetComponent<ArrowProjectile>();
                if (arrow == null)
                {
                    arrow = arrowObj.AddComponent<ArrowProjectile>();
                }

                // If target died during bow draw, arrow still launches toward target's last known location
                Vector3 aimPos = activeTarget != null ? activeTarget.position : lastAttackTargetPos;
                arrow.Launch(hasLivingTarget ? activeTarget : null, aimPos, finalDamage, this);
                return;
            }
            // Direct damage fallback if arrowPrefab is unassigned
        }

        if (!hasLivingTarget) return;

        // Direct damage (Melee or Archer fallback without arrow prefab)
        PeasantUnitController victim = activeTarget.GetComponentInParent<PeasantUnitController>();
        if (victim != null)
        {
            victim.TakeDamage(finalDamage, this);
            Vector3 hitDir = (victim.transform.position - transform.position).normalized;
            Vector3 hitPos = victim.transform.position + Vector3.up * 1f - hitDir * 0.25f;
            victim.SpawnHitEffect(hitPos, hitDir);
            return;
        }

        PeasantObjectiveTarget objective = activeTarget.GetComponentInParent<PeasantObjectiveTarget>();
        if (objective != null)
        {
            if (objective.TeamId == teamId)
            {
                ClearTarget();
                return;
            }
            objective.TakeDamage(finalDamage, this);
            return;
        }

        IDamageable damageable = activeTarget.GetComponentInParent<IDamageable>();
        if (damageable != null)
            damageable.TakeDamage(finalDamage, this);
    }

    [Header("Powerup Modifiers")]
    private float damageTakenMultiplier = 1f;
    private bool isShieldImmune = false;
    private float slowFactor = 1f; // 1 = normal speed, 0.5 = 50% speed
    private Coroutine slowRoutine;

    public float DamageTakenMultiplier
    {
        get => damageTakenMultiplier;
        set => damageTakenMultiplier = Mathf.Max(0f, value);
    }

    public bool IsShieldImmune
    {
        get => isShieldImmune;
        set => isShieldImmune = value;
    }

    public float EffectiveMoveSpeed => moveSpeed * slowFactor;

    /// <summary>
    /// Applies a temporary slow effect (e.g. from steel traps).
    /// </summary>
    /// <param name="factor">Speed multiplier between 0.1 and 1.0 (e.g. 0.4 for 60% slow).</param>
    /// <param name="duration">Duration in seconds.</param>
    public void ApplySlow(float factor, float duration)
    {
        if (isDead) return;
        if (slowRoutine != null)
        {
            StopCoroutine(slowRoutine);
        }
        slowRoutine = StartCoroutine(SlowRoutine(factor, duration));
    }

    private System.Collections.IEnumerator SlowRoutine(float factor, float duration)
    {
        slowFactor = Mathf.Clamp(factor, 0.15f, 1f);
        if (agent != null && agent.isOnNavMesh)
        {
            agent.speed = EffectiveMoveSpeed;
        }

        yield return new WaitForSeconds(duration);

        slowFactor = 1f;
        if (agent != null && agent.isOnNavMesh)
        {
            agent.speed = moveSpeed;
        }
        slowRoutine = null;
    }

    /// <summary>
    /// Restores health up to maxHealth and updates the health bar.
    /// </summary>
    public void Heal(float amount)
    {
        if (isDead || amount <= 0f) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        UpdateHealthBar();
        if (juicyHealthBar != null)
        {
            juicyHealthBar.OnHeal(amount);
        }
    }

    public void TakeDamage(float amount, PeasantUnitController attacker = null)
    {
        if (isDead || amount <= 0f) return;

        // In multiplayer, MasterClient is the sole authority for damage calculation and deduction
        if (NetworkBattleBridge.Instance != null && NetworkBattleBridge.Instance.IsMultiplayer && !Photon.Pun.PhotonNetwork.IsMasterClient)
        {
            return;
        }

        // Shield dome immunity
        if (isShieldImmune) return;

        // Archery type criteria: cannot be attacked by other peasants
        if (cannotBeAttackedByPeasants && attacker != null)
        {
            return;
        }

        // Apply damage taken multiplier (e.g. Shield Dome reduction)
        amount *= damageTakenMultiplier;
        if (amount <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        UpdateHealthBar();
        if (juicyHealthBar != null)
        {
            juicyHealthBar.OnTakeDamage(amount);
        }

        if (currentHealth <= 0f)
        {
            Die();
            return;
        }

        if (attacker != null && currentTarget == null && attacker.TeamId != teamId)
            SetTarget(attacker.transform);

        if (hitRoutine != null)
        {
            StopCoroutine(hitRoutine);
            hitRoutine = null;
        }

        if (hasAnimator && animator != null)
        {
            animator.speed = 1f;
        }

        isHurt = true;
        currentState = UnitState.Hurt;
        SetRunningAnimation(false);

        if (agent != null && agent.isOnNavMesh)
            agent.isStopped = true;

        if (hasAnimator)
        {
            animator.ResetTrigger(attackTrigger);
            animator.SetTrigger(hurtTrigger);
        }

        StartCoroutine(RecoverFromHurt());
    }

    private IEnumerator RecoverFromHurt()
    {
        // Keep this in sync with the Hurt clip length; change if your clip differs.
        float wait = 0.65f;
        if (hasAnimator)
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.length > 0f) wait = Mathf.Max(wait, info.length * 0.8f);
        }

        yield return new WaitForSeconds(wait);
        if (isDead) yield break;

        isHurt = false;
        if (agent != null && agent.isOnNavMesh) agent.isStopped = false;
        currentState = currentTarget != null ? UnitState.Chasing : UnitState.Idle;
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;
        isHurt = false;
        currentState = UnitState.Dead;
        currentTarget = null;
        isCommandMoving = false;

        if (juicyHealthBar != null)
        {
            juicyHealthBar.PlayDestruction();
        }

        if (healthBarCube != null)
        {
            healthBarCube.gameObject.SetActive(false);
        }

        if (hitRoutine != null) StopCoroutine(hitRoutine);

        if (agent != null)
        {
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            agent.enabled = false;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
            col.enabled = false;

        if (hasAnimator && animator != null)
        {
            animator.speed = 1f;
            animator.ResetTrigger(attackTrigger);
            animator.ResetTrigger(hurtTrigger);
            animator.SetBool(runningBool, false);
            animator.SetTrigger(deathTrigger);
        }

        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        KoSAudio.Play(IsMachine ? "wood_smash" : "unit_death", transform.position);

        // Always guarantee despawn after death so bodies don't stay frozen on battlefield forever
        float despawnDelay = destroyAfterDeath ? Mathf.Max(2.5f, destroyDelay) : 3.5f;
        Destroy(gameObject, despawnDelay);

        if (BattleAIManager.Instance != null)
        {
            BattleAIManager.Instance.UnregisterUnit(this);
        }

        if (NetworkBattleBridge.Instance != null)
        {
            if (NetworkBattleBridge.Instance.IsMultiplayer && Photon.Pun.PhotonNetwork.IsMasterClient)
            {
                NetworkBattleBridge.Instance.BroadcastUnitDeath(this);
            }
            else
            {
                NetworkBattleBridge.Instance.UnregisterUnit(this);
            }
        }
    }

    private void AcquireTarget()
    {
        // 1) Coordinated targeting via BattleAIManager (prevents dogpiling, promotes surrounding)
        if (BattleAIManager.Instance != null)
        {
            Transform coordinatedTarget = BattleAIManager.Instance.GetBestTargetForUnit(this);
            if (coordinatedTarget != null)
            {
                float distToCoord = GetDistanceToTarget(coordinatedTarget);
                if (!isStationary || distToCoord <= attackRange + attackRangePadding + 0.5f)
                {
                    SetTarget(coordinatedTarget);
                    return;
                }
            }
        }

        // 2) Fallback: Find a living enemy unit anywhere in the scene
        PeasantUnitController enemyUnit = FindNearestLivingEnemyUnit();
        PeasantObjectiveTarget enemyObjective = FindNearestLivingEnemyObjective();

        float distToEnemy = enemyUnit != null ? Vector3.Distance(transform.position, enemyUnit.transform.position) : float.MaxValue;
        float distToObj = enemyObjective != null ? GetDistanceToTarget(enemyObjective.TargetPoint) : float.MaxValue;

        // Prioritize enemy unit if nearby or closer than objective
        if (enemyUnit != null && (distToEnemy <= Mathf.Max(detectionRange, 16f) || distToEnemy <= distToObj))
        {
            if (!isStationary || distToEnemy <= attackRange + attackRangePadding + 0.5f)
            {
                SetTarget(enemyUnit.transform);
                return;
            }
        }

        // 3) Fallback: attack enemy objectives/castles/towers (all unit types can destroy towers)
        if (enemyObjective != null)
        {
            if (!isStationary || distToObj <= attackRange + attackRangePadding + 0.5f)
            {
                SetTarget(enemyObjective.TargetPoint);
                return;
            }
        }

        // 4) If distant enemy exists and mobile, target it
        if (enemyUnit != null && !isStationary)
        {
            SetTarget(enemyUnit.transform);
        }
    }

    private PeasantUnitController FindNearestLivingEnemyUnit()
    {
        // Performance: use the BattleAIManager registry (maintained on enable/disable/death)
        // instead of a scene-wide FindObjectsByType + array allocation on every target search.
        IReadOnlyList<PeasantUnitController> units = BattleAIManager.Instance != null
            ? BattleAIManager.Instance.GetAllLivingUnits()
            : FindObjectsByType<PeasantUnitController>(FindObjectsSortMode.None);

        PeasantUnitController nearest = null;
        float nearestDistance = float.MaxValue;

        for (int ui = 0; ui < units.Count; ui++)
        {
            PeasantUnitController candidate = units[ui];
            if (candidate == null || candidate == this || candidate.IsDead ||
                candidate.TeamId == teamId)
                continue;

            // Cannot be attacked by other peasants
            if (candidate.CannotBeAttackedByPeasants)
                continue;

            if (!IsLayerAllowed(candidate.gameObject.layer))
                continue;

            float distance = (candidate.transform.position - transform.position).sqrMagnitude;
            if (isStationary && distance > (attackRange + attackRangePadding + 0.5f) * (attackRange + attackRangePadding + 0.5f))
                continue;

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = candidate;
            }
        }

        return nearest;
    }

    private PeasantObjectiveTarget FindNearestLivingEnemyObjective()
    {
        IReadOnlyList<PeasantObjectiveTarget> objectives = BattleAIManager.Instance != null
            ? BattleAIManager.Instance.GetAllActiveObjectives()
            : FindObjectsByType<PeasantObjectiveTarget>(FindObjectsSortMode.None);

        PeasantObjectiveTarget nearest = null;
        float nearestDistance = float.MaxValue;

        for (int oi = 0; oi < objectives.Count; oi++)
        {
            PeasantObjectiveTarget objective = objectives[oi];
            if (objective == null || !objective.isActiveAndEnabled ||
                objective.IsDestroyed || objective.TeamId == teamId)
                continue;

            if (!IsLayerAllowed(objective.gameObject.layer))
                continue;

            float distance = GetDistanceToTarget(objective.TargetPoint);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = objective;
            }
        }

        return nearest;
    }

    private bool IsLayerAllowed(int layer)
    {
        return (targetLayers.value & (1 << layer)) != 0;
    }

    private bool IsTargetValid(Transform target)
    {
        if (target == null || target == transform) return false;

        PeasantUnitController unit = target.GetComponentInParent<PeasantUnitController>();
        if (unit != null)
        {
            // If target cannot be attacked by peasants, it is invalid for this peasant to target
            if (unit.CannotBeAttackedByPeasants) return false;
            return !unit.IsDead && unit.TeamId != teamId;
        }

        PeasantObjectiveTarget objective = target.GetComponentInParent<PeasantObjectiveTarget>();
        if (objective != null)
        {
            // If it's an unoccupied wall tower (not an autonomous base tower), an eligible archer (NOT ballista) can target it to walk there and garrison it!
            TowerCombatController tcc = objective.GetComponent<TowerCombatController>();
            if (tcc != null && !tcc.AttacksAutonomously && !tcc.IsOccupied && !tcc.IsDestroyed && (tcc.TeamId < 0 || tcc.TeamId == teamId))
            {
                if (IsEligibleTowerArcher && !IsBallista) return true;
            }

            // All types of players/units can attack and destroy non-friendly towers and objectives!
            return !objective.IsDestroyed && objective.TeamId != teamId;
        }

        return target.gameObject.activeInHierarchy;
    }

    private void ClearTarget()
    {
        if (currentTarget != null && BattleAIManager.Instance != null)
        {
            BattleAIManager.Instance.NotifyTargetAbandoned(this, currentTarget);
        }
        currentTarget = null;
        isTargetLocked = false;
        targetLockAcquiredTime = -1f;
        lastLockedTarget = null;
    }

    /// <summary>
    /// Checks whether this unit has acquired, faces, and locked onto the target within targetLockAngleThreshold.
    /// </summary>
    public bool IsTargetLocked(Transform target)
    {
        if (target == null || !IsTargetValid(target)) return false;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return true;

        float angle = Vector3.Angle(transform.forward, direction.normalized);
        float threshold = Mathf.Max(targetLockAngleThreshold, 35f);
        return angle <= threshold;
    }

    private void MaintainIdleSeparation()
    {
        if (isStationary) return;
        if (agent == null || !agent.isOnNavMesh || BattleAIManager.Instance == null) return;
        Vector3 push = BattleAIManager.Instance.GetSeparationVector(this);
        if (push.sqrMagnitude > 0.05f)
        {
            Vector3 nudgeTarget = transform.position + push * 0.4f;
            if (NavMesh.SamplePosition(nudgeTarget, out NavMeshHit hit, 1.2f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }
    }

    private void FaceTarget(Vector3 targetPosition)
    {
        if (!canRotate) return;
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion desired = Quaternion.LookRotation(direction.normalized);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, turnSpeed * Time.deltaTime);
    }

    #region Audio Playback & Timing

    public bool HasScreamClips()
    {
        if (screamAudioClips == null || screamAudioClips.Length == 0) return false;
        for (int i = 0; i < screamAudioClips.Length; i++)
        {
            if (screamAudioClips[i] != null) return true;
        }
        return false;
    }

    private AudioClip GetRandomScreamClip()
    {
        if (screamAudioClips == null || screamAudioClips.Length == 0) return null;

        List<AudioClip> validClips = new List<AudioClip>();
        for (int i = 0; i < screamAudioClips.Length; i++)
        {
            if (screamAudioClips[i] != null)
                validClips.Add(screamAudioClips[i]);
        }

        if (validClips.Count == 0) return null;
        return validClips[Random.Range(0, validClips.Count)];
    }

    /// <summary>Plays the attack audio clip with natural pitch variation.</summary>
    public void PlayAttackSound()
    {
        if (isDead || attackAudioClip == null || audioSource == null) return;
        string evt = IsMachine ? "siege_fire" : (unitType == UnitCombatType.Archer ? "bow_release" : "unit_attack");
        PlayClipWithPitch(attackAudioClip, attackAudioVolume, evt);
    }

    /// <summary>Plays a random scream / battle cry audio clip from the array with natural pitch variation.</summary>
    public void PlayScreamSound()
    {
        if (isDead || audioSource == null || IsMachine) return;
        AudioClip clip = GetRandomScreamClip();
        if (clip == null) return;
        PlayClipWithPitch(clip, screamAudioVolume, "scream");
    }

    private void PlayClipWithPitch(AudioClip clip, float volume, string evt = null)
    {
        if (clip == null || audioSource == null) return;
        audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        // Routed through KoSAudio: shared voice pool, per-event voice limits and mixing.
        KoSAudio.PlayOneShot(audioSource, clip, volume, evt);
    }

    private IEnumerator PlayStartScreamRoutine()
    {
        if (IsMachine) yield break;
        yield return new WaitForSeconds(Random.Range(0.1f, Mathf.Max(0.2f, startScreamDelay)));
        if (!isDead && !IsMachine)
        {
            PlayScreamSound();
        }
    }

    private void ScheduleNextMidBattleScream()
    {
        nextMidBattleScreamTime = Time.time + Random.Range(minMidScreamInterval, maxMidScreamInterval);
    }

    #endregion

    private bool IsAgentMoving()
    {
        if (isStationary) return false;
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return false;
        if (agent.isStopped)
            return false;

        // An agent is only visually running if it has actual physical velocity across the ground!
        // This completely eliminates units running on the spot while stuck against walls or allies.
        bool hasPhysicalVelocity = agent.velocity.sqrMagnitude > 0.06f;

        // Allow a brief grace window while a new path is being computed
        if (!hasPhysicalVelocity && agent.pathPending)
            return true;

        return hasPhysicalVelocity;
    }

    private void UpdateMovementAnimation()
    {
        SetRunningAnimation(IsAgentMoving());
    }

    public void SetRunningAnimation(bool running)
    {
        if (!hasAnimator || isDead || animator == null) return;

        animator.SetBool(runningBool, running);

        // Network proxy follower (on Guest or when NavMeshAgent is disabled for network replication)
        bool isNetworkProxy = (NetworkBattleBridge.Instance != null && NetworkBattleBridge.Instance.IsMultiplayer && !Photon.Pun.PhotonNetwork.IsMasterClient) ||
                              (agent == null || !agent.enabled);

        if (isNetworkProxy)
        {
            animator.speed = running ? runAnimationSpeedMultiplier : 1f;
            if (hasSpeedFloatParam)
            {
                // Crucial: when running, speed must be 1.0f so the Animator run clip plays smoothly instead of freezing at 0.0x speed!
                animator.SetFloat(speedFloat, running ? (1f * runAnimationSpeedMultiplier) : 0f);
            }
            return;
        }

        if (running)
        {
            float animSpeed = runAnimationSpeedMultiplier;
            if (syncAnimSpeedToMoveSpeed && agent != null && agent.enabled)
            {
                // Smoothly scale playback speed with actual ground velocity to prevent foot-sliding
                float currentSpeed = Mathf.Max(agent.velocity.magnitude, EffectiveMoveSpeed * 0.45f);
                animSpeed = Mathf.Clamp(currentSpeed / referenceRunSpeed, 0.7f, 1.35f) * runAnimationSpeedMultiplier;
            }
            animator.speed = animSpeed;
            if (hasSpeedFloatParam)
            {
                animator.SetFloat(speedFloat, animSpeed);
            }
        }
        else
        {
            // Smoothly restore normal playback speed when stationary (idle/combat/hurt)
            if (currentState != UnitState.Attacking)
            {
                animator.speed = 1f;
            }

            if (hasSpeedFloatParam)
            {
                currentAnimSpeed = 0f;
                // Keep speed parameter at 1f so exit transitions out of run state don't freeze at 0 playback speed
                animator.SetFloat(speedFloat, 1f);
            }
        }
    }

    /// <summary>Use this if an animation event signals that Hurt has finished.</summary>
    public void AnimationHurtFinished()
    {
        if (isDead) return;
        isHurt = false;
        if (agent != null && agent.isOnNavMesh) agent.isStopped = false;
    }

    private void OnDrawGizmosSelected()
    {
        DrawDebugGizmos(true);
    }

    private void OnDrawGizmos()
    {
        // Automatically draw gizmos in scene view for ballista / debug unit without needing selection
        if (IsTargetDebugUnit && showDebugGizmos)
        {
            DrawDebugGizmos(false);
        }
    }

    private void DrawDebugGizmos(bool isSelected)
    {
        if (!showDebugGizmos) return;

        // Draw Detection Range (Yellow wire sphere)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Draw Attack Range (Red wire sphere)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Draw Line of Sight
        if (currentTarget != null)
        {
            Vector3 start = arrowSpawnPoint != null ? arrowSpawnPoint.position :
                            (attackPoint != null ? attackPoint.position : transform.position + Vector3.up * 1.2f);
            Vector3 end = currentTarget.position + Vector3.up * 1f;

            if (lastLosHadHit && !lastLosResult)
            {
                // Blocked: Red line to hit point, red sphere at hit point, faded red line to target
                Gizmos.color = Color.red;
                Gizmos.DrawLine(start, lastLosHit.point);
                Gizmos.DrawSphere(lastLosHit.point, 0.25f);
                Gizmos.color = new Color(1f, 0f, 0f, 0.35f);
                Gizmos.DrawLine(lastLosHit.point, end);
            }
            else
            {
                // Clear line of sight
                if (unitType == UnitCombatType.Archer && lockTargetBeforeAttack)
                {
                    bool isLocked = IsTargetLocked(currentTarget);
                    bool readyToFire = isLocked && targetLockAcquiredTime > 0f && Time.time >= targetLockAcquiredTime + lockOnFireDelay;
                    Gizmos.color = readyToFire ? Color.green : (isLocked ? Color.yellow : new Color(1f, 0.5f, 0f));
                }
                else
                {
                    Gizmos.color = Color.green;
                }
                Gizmos.DrawLine(start, end);
            }

            // Draw line to target base
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }

        // Draw NavMeshAgent Destination & Path
        if (agent != null && agent.isOnNavMesh)
        {
            if (agent.hasPath)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(agent.destination, Vector3.one * 0.4f);
                Gizmos.DrawLine(transform.position, agent.destination);

                Vector3[] corners = agent.path.corners;
                if (corners != null && corners.Length > 1)
                {
                    Gizmos.color = Color.cyan;
                    for (int i = 0; i < corners.Length - 1; i++)
                    {
                        Gizmos.DrawLine(corners[i], corners[i + 1]);
                    }
                }
            }

            // Draw reposition destination marker if active
            if (lastRepositionAttempted)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(lastRepositionDest, 0.5f);
                Gizmos.DrawLine(transform.position, lastRepositionDest);
            }
        }
    }
}

/// <summary>
/// Optional interface for non-Peasant enemies. A component on the target can
/// implement this to receive damage from the peasant.
/// </summary>
public interface IDamageable
{
    void TakeDamage(float amount, PeasantUnitController attacker);
}
