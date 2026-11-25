using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Helper script that configures a PeasantUnitController as a specialized stationary Archery unit.
/// Meets all specified criteria:
/// 1. Stationary: The controller will not move (NavMeshAgent stopped, speed 0, immune to separation push).
/// 2. Rotation: Can rotate freely to track, aim, and shoot targets.
/// 3. Great Range: High attack and detection range (e.g. 22m attack / 25m detection).
/// 4. Great Attack Speed: Fast rate of fire with low cooldown (e.g. 0.55s cooldown, 0.22s release).
/// 5. Peasant Immunity: Cannot be targeted, attacked, or damaged by other peasants.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PeasantUnitController))]
[AddComponentMenu("Combat/Archery Peasant Helper")]
public class ArcheryPeasantHelper : MonoBehaviour
{
    [Header("Movement & Rotation (Stationary Criteria)")]
    [Tooltip("If true, the unit remains stationary: will not move or chase enemies.")]
    [SerializeField] private bool isStationary = true;

    [Tooltip("If true, the unit can rotate freely to track and face targets.")]
    [SerializeField] private bool canRotate = true;

    [Tooltip("Speed in degrees/second at which the archer rotates to face targets.")]
    [SerializeField, Min(60f)] private float turnSpeed = 720f;

    [Header("Range & Attack Speed (Archery Criteria)")]
    [Tooltip("Great attack range for long-distance archery coverage.")]
    [SerializeField, Min(5f)] private float archerAttackRange = 22f;

    [Tooltip("Detection range used to acquire enemies within shooting reach.")]
    [SerializeField, Min(5f)] private float archerDetectionRange = 25f;

    [Tooltip("Base damage dealt per arrow hit.")]
    [SerializeField, Min(1f)] private float archerDamage = 18f;

    [Tooltip("Attack cooldown in seconds (great attack speed: 0.55s = ~1.8 arrows/second).")]
    [SerializeField, Range(0.1f, 2f)] private float archerAttackCooldown = 0.55f;

    [Tooltip("Delay from attack trigger until arrow release (fast release for rapid firing).")]
    [SerializeField, Range(0.05f, 0.6f)] private float archerHitDelay = 0.22f;

    [Tooltip("Aim stance delay before firing (minimal delay for quick response).")]
    [SerializeField, Range(0f, 0.6f)] private float archerAimDelay = 0.1f;

    [Header("Peasant Immunity (Combat Protection)")]
    [Tooltip("If true, this archer cannot be targeted, attacked, or damaged by other peasants.")]
    [SerializeField] private bool cannotBeAttackedByPeasants = true;

    [Header("Projectile References")]
    [Tooltip("Prefab of the arrow projectile fired by this archery unit.")]
    [SerializeField] private GameObject arrowPrefab;

    [Tooltip("Optional custom spawn point transform where arrows spawn (defaults to attackPoint or head height).")]
    [SerializeField] private Transform arrowSpawnPoint;

    [Header("Visual Gizmos")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color attackRangeColor = new Color(0f, 0.85f, 1f, 0.35f);
    [SerializeField] private Color detectionRangeColor = new Color(1f, 0.85f, 0f, 0.2f);

    private PeasantUnitController controller;
    private NavMeshAgent agent;

    public bool IsStationary => isStationary;
    public bool CanRotate => canRotate;
    public bool CannotBeAttackedByPeasants => cannotBeAttackedByPeasants;
    public float AttackRange => archerAttackRange;
    public float AttackCooldown => archerAttackCooldown;

    private void Awake()
    {
        CacheReferences();
        ApplyArcheryConfiguration();
    }

    private void Start()
    {
        ApplyArcheryConfiguration();
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

        ApplyArcheryConfiguration();
    }

    private void OnValidate()
    {
        if (archerDetectionRange < archerAttackRange)
            archerDetectionRange = archerAttackRange;

        if (Application.isPlaying)
        {
            ApplyArcheryConfiguration();
        }
    }

    private void Update()
    {
        if (isStationary && agent != null && agent.isOnNavMesh)
        {
            if (!agent.isStopped)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                agent.ResetPath();
            }
        }
    }

    private void CacheReferences()
    {
        if (controller == null) controller = GetComponent<PeasantUnitController>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (arrowPrefab == null) arrowPrefab = Resources.Load<GameObject>("Powerups/Arrow_B");
    }

    /// <summary>
    /// Applies all archery settings to the attached PeasantUnitController and NavMeshAgent.
    /// Can be called manually from scripts or via the Inspector context menu.
    /// </summary>
    [ContextMenu("Apply Archery Configuration")]
    public void ApplyArcheryConfiguration()
    {
        CacheReferences();

        if (controller != null)
        {
            controller.ConfigureArchery(
                newAttackRange: archerAttackRange,
                newDetectionRange: archerDetectionRange,
                newAttackCooldown: archerAttackCooldown,
                newDamage: archerDamage,
                stationary: isStationary,
                allowRotation: canRotate,
                immuneToPeasants: cannotBeAttackedByPeasants,
                newArrowPrefab: arrowPrefab,
                newArrowSpawnPoint: arrowSpawnPoint,
                newHitDelay: archerHitDelay,
                newAimDelay: archerAimDelay
            );
        }

        if (agent != null)
        {
            if (isStationary)
            {
                // Disable agent so Unity does not snap or warp the unit to ground NavMesh
                agent.enabled = false;
            }
            else
            {
                agent.enabled = true;
                if (agent.isOnNavMesh)
                {
                    agent.angularSpeed = turnSpeed;
                }
            }
        }
    }

    /// <summary>Toggles stationary mode at runtime.</summary>
    public void SetStationary(bool stationary)
    {
        isStationary = stationary;
        if (controller != null)
        {
            controller.SetStationary(stationary);
        }
    }

    /// <summary>Toggles peasant attack immunity at runtime.</summary>
    public void SetImmuneToPeasants(bool immune)
    {
        cannotBeAttackedByPeasants = immune;
        if (controller != null)
        {
            controller.CannotBeAttackedByPeasants = immune;
        }
    }

    /// <summary>Updates the archer's range and cooldown stats dynamically.</summary>
    public void SetArcheryStats(float range, float cooldown, float damage)
    {
        archerAttackRange = Mathf.Max(1f, range);
        archerDetectionRange = Mathf.Max(archerAttackRange, archerDetectionRange);
        archerAttackCooldown = Mathf.Max(0.05f, cooldown);
        archerDamage = Mathf.Max(0f, damage);

        ApplyArcheryConfiguration();
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        Vector3 pos = transform.position;

        // Draw Great Attack Range
        Gizmos.color = attackRangeColor;
        Gizmos.DrawWireSphere(pos, archerAttackRange);

        // Draw Detection Range
        Gizmos.color = detectionRangeColor;
        Gizmos.DrawWireSphere(pos, archerDetectionRange);

        // Draw forward facing direction ray
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(pos + Vector3.up * 1f, transform.forward * 3f);

        // Draw stationary base circle
        if (isStationary)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(pos, 0.6f);
        }
    }
}
