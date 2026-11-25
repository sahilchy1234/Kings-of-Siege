using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Helper component that configures the PeasantUnitController on Awake/Reset/Start
/// as an Archer unit. Supports stationary defense mode, rotation to track targets,
/// great range and attack speed, and immunity to other peasants.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PeasantUnitController))]
public class ArcherUnit : MonoBehaviour
{
    [Header("Archer Presets")]
    [Tooltip("Attack range for archery shots (e.g. 7.5m - 25m).")]
    [SerializeField, Min(3f)] private float archerAttackRange = 22f;

    [Tooltip("Detection range used to acquire enemies within shooting reach.")]
    [SerializeField, Min(5f)] private float archerDetectionRange = 25f;

    [Tooltip("Base damage dealt per arrow hit.")]
    [SerializeField, Min(1f)] private float archerDamage = 18f;

    [Tooltip("Attack cooldown in seconds (e.g. 0.55s = great attack speed).")]
    [SerializeField, Min(0.1f)] private float archerCooldown = 0.55f;

    [Tooltip("Delay from attack trigger until arrow is released.")]
    [SerializeField, Range(0.05f, 0.6f)] private float archerHitDelay = 0.22f;

    [Tooltip("Aim stance delay before firing.")]
    [SerializeField, Range(0f, 0.6f)] private float archerAimDelay = 0.1f;

    [Header("Stationary & Rotation Criteria")]
    [Tooltip("If true, the unit remains stationary: will not move or chase enemies.")]
    [SerializeField] private bool isStationary = true;

    [Tooltip("If true, the unit can rotate freely to track and face targets.")]
    [SerializeField] private bool canRotate = true;

    [Tooltip("If true, other peasants cannot target, attack, or damage this archer.")]
    [SerializeField] private bool cannotBeAttackedByPeasants = true;

    [Header("Target Locking Criteria")]
    [Tooltip("If true, the archer will not attack until it locks onto its target (faces the target within angle threshold), and will wait lockOnFireDelay (0.7s) after locking before firing.")]
    [SerializeField] private bool lockTargetBeforeAttack = true;

    [Tooltip("Delay in seconds after locking onto the target before firing (default: 0.7s).")]
    [SerializeField, Min(0f)] private float lockOnFireDelay = 0.7f;

    [Tooltip("Maximum angle difference in degrees to consider target locked (default: 10 degrees).")]
    [SerializeField, Range(1f, 45f)] private float targetLockAngleThreshold = 10f;

    [Tooltip("If true, requires re-locking and waiting lockOnFireDelay before every single shot. If false, subsequent shots on the same locked target occur at normal attack cooldown.")]
    [SerializeField] private bool relockBetweenShots = false;

    [Header("Projectile References")]
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private Transform arrowSpawnPoint;

    private PeasantUnitController controller;
    private NavMeshAgent agent;

    public bool IsStationary => isStationary;
    public bool CanRotate => canRotate;
    public bool CannotBeAttackedByPeasants => cannotBeAttackedByPeasants;
    public bool LockTargetBeforeAttack => lockTargetBeforeAttack;
    public float LockOnFireDelay => lockOnFireDelay;
    public float TargetLockAngleThreshold => targetLockAngleThreshold;
    public bool RelockBetweenShots => relockBetweenShots;

    private void Awake()
    {
        CacheReferences();
        ApplyArcherConfiguration();
    }

    private void Start()
    {
        ApplyArcherConfiguration();
    }

    private void Reset()
    {
        CacheReferences();

        if (arrowPrefab == null)
        {
            string[] guids = RuntimeAssets.FindAssets("Arrow_B t:Prefab");
            if (guids != null && guids.Length > 0)
            {
                string path = RuntimeAssets.GUIDToAssetPath(guids[0]);
                arrowPrefab = RuntimeAssets.LoadAssetAtPath<GameObject>(path);
            }
        }

        ApplyArcherConfiguration();
    }

    private void OnValidate()
    {
        if (archerDetectionRange < archerAttackRange)
            archerDetectionRange = archerAttackRange;

        if (Application.isPlaying)
        {
            ApplyArcherConfiguration();
        }
    }

    private void CacheReferences()
    {
        if (controller == null) controller = GetComponent<PeasantUnitController>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (arrowPrefab == null) arrowPrefab = Resources.Load<GameObject>("Powerups/Arrow_B");
    }

    /// <summary>
    /// Configures the PeasantUnitController to behave as an Archer with the specified criteria.
    /// </summary>
    [ContextMenu("Apply Archer Configuration")]
    public void ApplyArcherConfiguration()
    {
        CacheReferences();

        if (controller != null)
        {
            controller.ConfigureArchery(
                newAttackRange: archerAttackRange,
                newDetectionRange: archerDetectionRange,
                newAttackCooldown: archerCooldown,
                newDamage: archerDamage,
                stationary: isStationary,
                allowRotation: canRotate,
                immuneToPeasants: cannotBeAttackedByPeasants,
                newArrowPrefab: arrowPrefab,
                newArrowSpawnPoint: arrowSpawnPoint,
                newHitDelay: archerHitDelay,
                newAimDelay: archerAimDelay,
                newLockTargetBeforeAttack: lockTargetBeforeAttack,
                newLockOnFireDelay: lockOnFireDelay,
                newTargetLockAngleThreshold: targetLockAngleThreshold,
                newRelockBetweenShots: relockBetweenShots
            );
        }

        if (agent != null)
        {
            if (isStationary)
            {
                agent.enabled = false;
            }
            else
            {
                agent.enabled = true;
            }
        }
    }
}
