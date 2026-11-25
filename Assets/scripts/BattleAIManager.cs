using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Central Battle AI Coordinator that manages all PeasantUnitController units.
/// Solves gathering and crowding by:
/// 1. Enforcing reasonable combat distance (1.7m - 2.0m) and inter-attacker separation (2.0m+).
/// 2. Discrete non-overlapping surround slots around targets: each slot can only be occupied by ONE unit.
/// 3. Second-rank support ring (3.6m distance) for overflow units so surplus units maintain reserve distance
///    instead of dogpiling.
/// 4. Flocking / Boids personal-space separation forces so units don't gather when marching, fighting, or idling.
/// 5. Wide perimeter slots around castles/objectives to spread siege attacks across the wall.
/// </summary>
public class BattleAIManager : MonoBehaviour
{
    private static BattleAIManager instance;
    public static BattleAIManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<BattleAIManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("[BattleAIManager]");
                    instance = go.AddComponent<BattleAIManager>();
                }
            }
            return instance;
        }
    }

    [Header("Reasonable Combat Spacing")]
    [Tooltip("Distance maintained by melee units around an enemy peasant. Keeps a clear, reasonable combat distance.")]
    [SerializeField, Range(1.4f, 2.5f)] private float combatEngagementDistance = 1.75f;

    [Tooltip("Minimum distance maintained between any two attacking allies around the same target.")]
    [SerializeField, Range(1.5f, 3.5f)] private float minInterAttackerDistance = 2.2f;

    [Tooltip("Maximum simultaneous front-line melee attackers assigned to a single enemy peasant unit.")]
    [SerializeField, Range(1, 4)] private int maxAttackersPerUnit = 3;

    [Tooltip("Distance kept by second-rank / support reserve units if all front slots on a target are filled.")]
    [SerializeField, Range(2.0f, 6.0f)] private float supportRankDistance = 2.5f;

    [Header("Objective (Castle) Spacing & Siege Commitment")]
    [Tooltip("Maximum simultaneous attackers spread along an objective's perimeter.")]
    [SerializeField, Range(2, 16)] private int maxAttackersPerObjective = 8;

    [Tooltip("Minimum distance between attackers along the objective wall/perimeter.")]
    [SerializeField, Range(1.5f, 4.0f)] private float minObjectiveSlotSpacing = 2.4f;

    [Tooltip("Fallback radius for objectives if no collider is found.")]
    [SerializeField, Min(1f)] private float defaultObjectivePerimeterRadius = 4.0f;

    [Tooltip("Percentage (0 to 1) of siege units that remain committed to destroying the objective even if an enemy spawns.")]
    [SerializeField, Range(0.2f, 0.9f)] private float siegeCommitmentRatio = 0.65f;

    [Tooltip("Distance from siege unit to spawned enemy beyond which siege units ignore the enemy and stay on the castle.")]
    [SerializeField, Range(3f, 20f)] private float siegeRetargetMaxDistance = 6.0f;

    [Header("Flocking & Personal Space")]
    [Tooltip("Personal space radius between friendly units while marching or standing.")]
    [SerializeField, Range(0.8f, 3.0f)] private float personalSpaceRadius = 1.6f;

    [Tooltip("Strength of the separation push to prevent clustering.")]
    [SerializeField, Range(0.2f, 3.0f)] private float separationStrength = 1.2f;

    [Header("Tactical Prioritization")]
    [Tooltip("How strongly distance influences target selection (higher = prefers closest).")]
    [SerializeField, Range(0.1f, 3f)] private float distanceWeight = 1.0f;

    [Tooltip("Bonus score for targets attacking this unit or a nearby ally.")]
    [SerializeField, Range(0f, 50f)] private float threatRetaliationBonus = 18f;

    [Tooltip("Bonus score for enemies with low health (finishing blows).")]
    [SerializeField, Range(0f, 30f)] private float lowHealthBonus = 10f;

    [Tooltip("Hysteresis bonus to keep attacking the current target unless another is significantly better.")]
    [SerializeField, Range(0f, 30f)] private float targetStickinessBonus = 15f;

    [Tooltip("NavMesh sample radius for clamping slot positions.")]
    [SerializeField, Min(0.2f)] private float navMeshSampleDistance = 2.0f;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;

    // Registry of all living units and objectives
    private readonly List<PeasantUnitController> allLivingUnits = new List<PeasantUnitController>();
    private readonly List<PeasantObjectiveTarget> allActiveObjectives = new List<PeasantObjectiveTarget>();

    // Discrete slot allocation per target
    private class TargetEngagement
    {
        public Transform target;
        public bool isObjective;
        public float primaryRadius;
        public float supportRadius;
        public int primarySlotCount;
        public int supportSlotCount;

        // Slot occupancy: index -> occupying unit
        public PeasantUnitController[] primarySlots;
        public PeasantUnitController[] supportSlots;
        public PeasantUnitController[] archerSlots;          // Rank 1: Front firing line
        public PeasantUnitController[] archerSupportSlots;   // Rank 2: Secondary firing rank (staggered)
        public PeasantUnitController[] archerReserveSlots;   // Rank 3: Reserve firing rank (staggered)
        public float archerRadius;
        public int archerSlotCount;

        // World anchor rotation (so slots remain fixed relative to target or world)
        public float anchorAngleDeg;

        public TargetEngagement(Transform target, bool isObjective, float primaryRad, float supportRad, int primSlots, int suppSlots)
        {
            this.target = target;
            this.isObjective = isObjective;
            this.primaryRadius = primaryRad;
            this.supportRadius = supportRad;
            this.primarySlotCount = primSlots;
            this.supportSlotCount = suppSlots;
            this.primarySlots = new PeasantUnitController[primSlots];
            this.supportSlots = new PeasantUnitController[suppSlots];
            this.anchorAngleDeg = target.eulerAngles.y;

            this.archerRadius = isObjective ? (primaryRadius + 4.5f) : 7.0f;
            float archerPerimeter = 2f * Mathf.PI * this.archerRadius;
            this.archerSlotCount = Mathf.Clamp(Mathf.FloorToInt(archerPerimeter / 3.0f), 6, 16);
            this.archerSlots = new PeasantUnitController[this.archerSlotCount];
            this.archerSupportSlots = new PeasantUnitController[this.archerSlotCount];
            this.archerReserveSlots = new PeasantUnitController[this.archerSlotCount];
        }

        public int OccupiedPrimaryCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < primarySlots.Length; i++)
                    if (primarySlots[i] != null && !primarySlots[i].IsDead) count++;
                return count;
            }
        }

        public int OccupiedArcherCount
        {
            get
            {
                int count = 0;
                if (archerSlots != null)
                    for (int i = 0; i < archerSlots.Length; i++)
                        if (archerSlots[i] != null && !archerSlots[i].IsDead) count++;
                if (archerSupportSlots != null)
                    for (int i = 0; i < archerSupportSlots.Length; i++)
                        if (archerSupportSlots[i] != null && !archerSupportSlots[i].IsDead) count++;
                if (archerReserveSlots != null)
                    for (int i = 0; i < archerReserveSlots.Length; i++)
                        if (archerReserveSlots[i] != null && !archerReserveSlots[i].IsDead) count++;
                return count;
            }
        }

        public int TotalAttackerCount => OccupiedPrimaryCount + OccupiedSupportCount + OccupiedArcherCount;

        public int OccupiedSupportCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < supportSlots.Length; i++)
                    if (supportSlots[i] != null && !supportSlots[i].IsDead) count++;
                return count;
            }
        }

        public void CleanDead()
        {
            for (int i = 0; i < primarySlots.Length; i++)
                if (primarySlots[i] == null || primarySlots[i].IsDead) primarySlots[i] = null;
            for (int i = 0; i < supportSlots.Length; i++)
                if (supportSlots[i] == null || supportSlots[i].IsDead) supportSlots[i] = null;
            if (archerSlots != null)
                for (int i = 0; i < archerSlots.Length; i++)
                    if (archerSlots[i] == null || archerSlots[i].IsDead) archerSlots[i] = null;
            if (archerSupportSlots != null)
                for (int i = 0; i < archerSupportSlots.Length; i++)
                    if (archerSupportSlots[i] == null || archerSupportSlots[i].IsDead) archerSupportSlots[i] = null;
            if (archerReserveSlots != null)
                for (int i = 0; i < archerReserveSlots.Length; i++)
                    if (archerReserveSlots[i] == null || archerReserveSlots[i].IsDead) archerReserveSlots[i] = null;
        }

        public void Release(PeasantUnitController unit)
        {
            for (int i = 0; i < primarySlots.Length; i++)
                if (primarySlots[i] == unit) primarySlots[i] = null;
            for (int i = 0; i < supportSlots.Length; i++)
                if (supportSlots[i] == unit) supportSlots[i] = null;
            if (archerSlots != null)
                for (int i = 0; i < archerSlots.Length; i++)
                    if (archerSlots[i] == unit) archerSlots[i] = null;
            if (archerSupportSlots != null)
                for (int i = 0; i < archerSupportSlots.Length; i++)
                    if (archerSupportSlots[i] == unit) archerSupportSlots[i] = null;
            if (archerReserveSlots != null)
                for (int i = 0; i < archerReserveSlots.Length; i++)
                    if (archerReserveSlots[i] == unit) archerReserveSlots[i] = null;
        }
    }

    private readonly Dictionary<Transform, TargetEngagement> engagementMap =
        new Dictionary<Transform, TargetEngagement>();

    private readonly Dictionary<PeasantUnitController, Transform> unitToTarget =
        new Dictionary<PeasantUnitController, Transform>();

    public float PersonalSpaceRadius => personalSpaceRadius;
    public float SeparationStrength => separationStrength;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    #region Registration

    public void RegisterUnit(PeasantUnitController unit)
    {
        if (unit == null || unit.IsDead) return;
        if (!allLivingUnits.Contains(unit))
            allLivingUnits.Add(unit);
    }

    public void UnregisterUnit(PeasantUnitController unit)
    {
        if (unit == null) return;
        allLivingUnits.Remove(unit);
        ClearUnitAssignment(unit);

        // If this unit was a target, clean its engagement
        Transform t = unit.transform;
        if (engagementMap.TryGetValue(t, out TargetEngagement eng))
        {
            eng.CleanDead();
            for (int i = 0; i < eng.primarySlots.Length; i++)
            {
                if (eng.primarySlots[i] != null)
                    ClearUnitAssignment(eng.primarySlots[i]);
            }
            for (int i = 0; i < eng.supportSlots.Length; i++)
            {
                if (eng.supportSlots[i] != null)
                    ClearUnitAssignment(eng.supportSlots[i]);
            }
            for (int i = 0; i < eng.archerSlots.Length; i++)
            {
                if (eng.archerSlots[i] != null)
                    ClearUnitAssignment(eng.archerSlots[i]);
            }
            for (int i = 0; i < eng.archerSupportSlots.Length; i++)
            {
                if (eng.archerSupportSlots[i] != null)
                    ClearUnitAssignment(eng.archerSupportSlots[i]);
            }
            for (int i = 0; i < eng.archerReserveSlots.Length; i++)
            {
                if (eng.archerReserveSlots[i] != null)
                    ClearUnitAssignment(eng.archerReserveSlots[i]);
            }
            engagementMap.Remove(t);
        }
    }

    public void RegisterObjective(PeasantObjectiveTarget objective)
    {
        if (objective == null || objective.IsDestroyed) return;
        if (!allActiveObjectives.Contains(objective))
            allActiveObjectives.Add(objective);
    }

    public void UnregisterObjective(PeasantObjectiveTarget objective)
    {
        if (objective == null) return;
        allActiveObjectives.Remove(objective);
        Transform t = objective.TargetPoint;
        if (engagementMap.TryGetValue(t, out TargetEngagement eng))
        {
            for (int i = 0; i < eng.primarySlots.Length; i++)
            {
                if (eng.primarySlots[i] != null)
                    ClearUnitAssignment(eng.primarySlots[i]);
            }
            for (int i = 0; i < eng.archerSlots.Length; i++)
            {
                if (eng.archerSlots[i] != null)
                    ClearUnitAssignment(eng.archerSlots[i]);
            }
            for (int i = 0; i < eng.archerSupportSlots.Length; i++)
            {
                if (eng.archerSupportSlots[i] != null)
                    ClearUnitAssignment(eng.archerSupportSlots[i]);
            }
            for (int i = 0; i < eng.archerReserveSlots.Length; i++)
            {
                if (eng.archerReserveSlots[i] != null)
                    ClearUnitAssignment(eng.archerReserveSlots[i]);
            }
            engagementMap.Remove(t);
        }
    }

    public IReadOnlyList<PeasantUnitController> GetAllLivingUnits() => allLivingUnits;
    public IReadOnlyList<PeasantObjectiveTarget> GetAllActiveObjectives() => allActiveObjectives;

    #endregion

    #region Target Selection with Anti-Gathering Balancing

    public Transform GetBestTargetForUnit(PeasantUnitController unit)
    {
        if (unit == null || unit.IsDead) return null;

        // In multiplayer, targeting simulation is solely authoritative on MasterClient
        if (NetworkBattleBridge.Instance != null && NetworkBattleBridge.Instance.IsMultiplayer && !Photon.Pun.PhotonNetwork.IsMasterClient)
            return null;

        PruneNullAndDead();

        int myTeam = unit.TeamId;
        Transform currentAssigned = GetAssignedTarget(unit);

        // 1. Evaluate Living Enemy Units
        List<PeasantUnitController> enemyUnits = new List<PeasantUnitController>();
        foreach (var candidate in allLivingUnits)
        {
            if (candidate == null || candidate.IsDead || candidate.TeamId == myTeam) continue;
            if (candidate.CannotBeAttackedByPeasants) continue;
            enemyUnits.Add(candidate);
        }

        // Stationary units can only target enemies within their attack range
        if (unit.IsStationary)
        {
            enemyUnits.RemoveAll(e => Vector3.Distance(unit.transform.position, e.transform.position) > unit.AttackRange + 1f);
        }

        // 1. Check if unit is already engaged on an enemy objective (castle siege)
        PeasantObjectiveTarget currentObj = currentAssigned != null ? currentAssigned.GetComponentInParent<PeasantObjectiveTarget>() : null;
        bool isAttackingObjective = currentObj != null && currentObj.TeamId != myTeam && currentObj.TeamId >= 0 && !currentObj.IsDestroyed;

        if (isAttackingObjective && enemyUnits.Count > 0)
        {
            // Check if this unit should stay committed to attacking the objective
            if (ShouldStayCommittedToObjective(unit, currentAssigned, enemyUnits))
            {
                // Unit remains on the castle!
                return currentAssigned;
            }
        }

        // 2. Evaluate Objectives vs Enemy Units
        float closestEnemyDist = float.MaxValue;
        for (int i = 0; i < enemyUnits.Count; i++)
        {
            float d = Vector3.Distance(unit.transform.position, enemyUnits[i].transform.position);
            if (d < closestEnemyDist) closestEnemyDist = d;
        }

        // Find closest enemy objective
        float closestObjDist = float.MaxValue;
        for (int i = 0; i < allActiveObjectives.Count; i++)
        {
            var obj = allActiveObjectives[i];
            if (obj == null || obj.IsDestroyed || obj.TeamId == myTeam) continue;
            float d = unit.GetDistanceToTarget(obj.TargetPoint);
            if (d < closestObjDist) closestObjDist = d;
        }

        // Prioritize enemy unit if in local combat range or closer than any objective:
        bool prioritizeEnemyUnit = (enemyUnits.Count > 0) &&
            (closestEnemyDist <= Mathf.Max(unit.DetectionRange, 16f) || closestEnemyDist <= closestObjDist);

        if (prioritizeEnemyUnit)
        {
            Transform bestEnemy = PickBestEnemyUnit(unit, enemyUnits, currentAssigned);
            if (bestEnemy != null)
            {
                AssignTarget(unit, bestEnemy);
                return bestEnemy;
            }
        }

        // 2.5 Archers seek to claim or garrison vacant wall towers before marching on distant castles!
        // Strictly exclude Ballistas, siege engines, and melee units
        bool isArcher = unit.IsEligibleTowerArcher && !unit.IsBallista;
        if (isArcher && !unit.IsStationary)
        {
            Transform vacantTower = FindVacantWallTowerForArcher(unit);
            if (vacantTower != null)
            {
                AssignTarget(unit, vacantTower);
                return vacantTower;
            }
        }

        // 3. Evaluate Objectives (all units can destroy enemy towers & objectives)
        List<PeasantObjectiveTarget> enemyObjectives = new List<PeasantObjectiveTarget>();
        foreach (var obj in allActiveObjectives)
        {
            if (obj == null || obj.IsDestroyed || obj.TeamId == myTeam) continue;
            enemyObjectives.Add(obj);
        }

        if (unit.IsStationary && enemyObjectives.Count > 0)
        {
            enemyObjectives.RemoveAll(obj => unit.GetDistanceToTarget(obj.TargetPoint) > unit.AttackRange + 1f);
        }

        if (enemyObjectives.Count > 0)
        {
            Transform bestObjective = PickBestObjective(unit, enemyObjectives, currentAssigned);
            if (bestObjective != null)
            {
                AssignTarget(unit, bestObjective);
                return bestObjective;
            }
        }

        // 4. Fallback: if distant enemy units exist, target them
        if (enemyUnits.Count > 0)
        {
            Transform bestEnemy = PickBestEnemyUnit(unit, enemyUnits, currentAssigned);
            if (bestEnemy != null)
            {
                AssignTarget(unit, bestEnemy);
                return bestEnemy;
            }
        }

        return null;
    }

    private bool ShouldStayCommittedToObjective(
        PeasantUnitController unit,
        Transform currentObjective,
        List<PeasantUnitController> enemyUnits)
    {
        if (!engagementMap.TryGetValue(currentObjective, out TargetEngagement eng))
            return false;

        eng.CleanDead();
        int totalSiegeAttackers = eng.TotalAttackerCount;

        // If there are only 1 or 2 units attacking the castle, keep at least 1 attacking
        int committedCountTarget = Mathf.Clamp(Mathf.RoundToInt(totalSiegeAttackers * siegeCommitmentRatio), 1, totalSiegeAttackers);

        // Find distance to closest enemy unit
        Vector3 unitPos = unit.transform.position;
        float closestEnemyDist = float.MaxValue;
        PeasantUnitController closestEnemy = null;

        for (int i = 0; i < enemyUnits.Count; i++)
        {
            float d = Vector3.Distance(unitPos, enemyUnits[i].transform.position);
            if (d < closestEnemyDist)
            {
                closestEnemyDist = d;
                closestEnemy = enemyUnits[i];
            }
        }

        // If the closest enemy is far away (e.g. just spawned at base or > siegeRetargetMaxDistance), stay on the castle
        if (closestEnemyDist > siegeRetargetMaxDistance)
        {
            return true;
        }

        // If the closest enemy is already at capacity with other defenders, stay on the castle
        if (closestEnemy != null)
        {
            TargetEngagement enemyEng = GetOrCreateEngagement(closestEnemy.transform, false);
            enemyEng.CleanDead();
            if (enemyEng.OccupiedPrimaryCount >= enemyEng.primarySlotCount)
            {
                return true;
            }
        }

        // Count how many siege allies are closer to the enemy than this unit
        int alliesCloserToEnemy = 0;
        for (int i = 0; i < eng.primarySlots.Length; i++)
        {
            PeasantUnitController ally = eng.primarySlots[i];
            if (ally != null && ally != unit && !ally.IsDead)
            {
                float allyDist = Vector3.Distance(ally.transform.position, closestEnemy.transform.position);
                if (allyDist < closestEnemyDist)
                {
                    alliesCloserToEnemy++;
                }
            }
        }

        // If there are already enough closer allies available to respond to the enemy, this unit remains on the objective
        int defendersNeeded = Mathf.Min(maxAttackersPerUnit, enemyUnits.Count * 2);
        if (alliesCloserToEnemy < defendersNeeded)
        {
            // This is one of the closest units to the threat, peel off to defend/intercept
            return false;
        }

        // Otherwise remain on the castle
        return true;
    }

    private Transform FindVacantWallTowerForArcher(PeasantUnitController archer)
    {
        if (archer == null || archer.IsDead || archer.IsBallista || !archer.IsEligibleTowerArcher) return null;

        Transform bestTower = null;
        float closestDistSq = float.MaxValue;
        Vector3 myPos = archer.transform.position;

        for (int i = 0; i < allActiveObjectives.Count; i++)
        {
            var obj = allActiveObjectives[i];
            if (obj == null || obj.IsDestroyed) continue;

            TowerCombatController tcc = obj.GetComponent<TowerCombatController>();
            // ONLY wall towers (which do NOT attack autonomously and have TowerGarrisonZone) can be garrisoned!
            if (tcc != null && !tcc.AttacksAutonomously && !tcc.IsOccupied && !tcc.IsDestroyed)
            {
                // Can occupy if neutral (teamId < 0) or matching team
                if (tcc.TeamId < 0 || tcc.TeamId == archer.TeamId)
                {
                    Transform t = obj.TargetPoint;
                    int assignedCount = 0;
                    if (engagementMap.TryGetValue(t, out TargetEngagement eng))
                    {
                        eng.CleanDead();
                        assignedCount = eng.TotalAttackerCount;
                    }

                    if (assignedCount < 1)
                    {
                        float dSq = (t.position - myPos).sqrMagnitude;
                        if (dSq < closestDistSq)
                        {
                            closestDistSq = dSq;
                            bestTower = t;
                        }
                    }
                }
            }
        }

        return bestTower;
    }

    private Transform PickBestEnemyUnit(
        PeasantUnitController unit,
        List<PeasantUnitController> candidates,
        Transform currentAssigned)
    {
        Vector3 unitPos = unit.transform.position;
        PeasantUnitController bestCandidate = null;
        float highestScore = float.MinValue;

        // Check if ANY enemy has open primary slots
        bool anyHasOpenSlots = false;
        foreach (var cand in candidates)
        {
            TargetEngagement eng = GetOrCreateEngagement(cand.transform, false);
            eng.CleanDead();
            if (eng.OccupiedPrimaryCount < eng.primarySlotCount)
            {
                anyHasOpenSlots = true;
                break;
            }
        }

        foreach (var cand in candidates)
        {
            Transform candTransform = cand.transform;
            TargetEngagement eng = GetOrCreateEngagement(candTransform, false);
            eng.CleanDead();

            int primaryOccupied = eng.OccupiedPrimaryCount;
            bool isCurrentTarget = candTransform == currentAssigned;

            // If some enemies have open slots, strictly avoid full enemies
            if (anyHasOpenSlots && primaryOccupied >= eng.primarySlotCount && !isCurrentTarget)
            {
                continue;
            }

            float dist = Vector3.Distance(unitPos, candTransform.position);
            // Distance score: decays smoothly with distance
            float score = 100f / (1f + dist * distanceWeight * 0.15f);

            // Crowd Penalty: heavy penalty per attacker to distribute units across enemies
            score -= (primaryOccupied * 30f);

            // Retaliation / Protect Ally:
            if (cand.CurrentTarget == unit.transform)
            {
                score += threatRetaliationBonus;
            }
            else if (cand.CurrentTarget != null)
            {
                PeasantUnitController ally = cand.CurrentTarget.GetComponentInParent<PeasantUnitController>();
                if (ally != null && ally.TeamId == unit.TeamId && ally.CurrentHealth < ally.MaxHealth * 0.5f)
                {
                    score += threatRetaliationBonus * 0.6f;
                }
            }

            // Low health finish bonus
            if (cand.MaxHealth > 0f)
            {
                float missingHealthRatio = 1f - Mathf.Clamp01(cand.CurrentHealth / cand.MaxHealth);
                score += missingHealthRatio * lowHealthBonus;
            }

            // Target Stickiness
            if (isCurrentTarget)
            {
                score += targetStickinessBonus;
            }

            if (score > highestScore)
            {
                highestScore = score;
                bestCandidate = cand;
            }
        }

        return bestCandidate != null ? bestCandidate.transform : null;
    }

    private Transform PickBestObjective(
        PeasantUnitController unit,
        List<PeasantObjectiveTarget> objectives,
        Transform currentAssigned)
    {
        Vector3 unitPos = unit.transform.position;
        PeasantObjectiveTarget bestObj = null;
        float highestScore = float.MinValue;

        foreach (var obj in objectives)
        {
            Transform targetPoint = obj.TargetPoint;
            TargetEngagement eng = GetOrCreateEngagement(targetPoint, true);
            eng.CleanDead();

            float dist = unit != null ? unit.GetDistanceToTarget(targetPoint) : Vector3.Distance(unitPos, targetPoint.position);
            float score = 100f / (1f + dist * distanceWeight * 0.15f);

            score -= (eng.OccupiedPrimaryCount * 8f);

            if (targetPoint == currentAssigned)
            {
                score += targetStickinessBonus;
            }

            if (score > highestScore)
            {
                highestScore = score;
                bestObj = obj;
            }
        }

        return bestObj != null ? bestObj.TargetPoint : null;
    }

    #endregion

    #region Discrete Surround Slot Allocation

    /// <summary>
    /// Returns the unique, non-overlapping surround slot position for the attacker.
    /// Ensures units stand at a clean, reasonable combat distance and are well-separated from each other.
    /// </summary>
    public Vector3 GetCoordinatedAttackPosition(PeasantUnitController attacker, Transform target)
    {
        if (target == null || attacker == null)
            return attacker != null ? attacker.transform.position : Vector3.zero;

        // In multiplayer, proxy follower on Guest does not calculate surround slots
        if (NetworkBattleBridge.Instance != null && NetworkBattleBridge.Instance.IsMultiplayer && !Photon.Pun.PhotonNetwork.IsMasterClient)
            return attacker.transform.position;

        // Stationary units never move from their post
        if (attacker.IsStationary)
            return attacker.transform.position;

        bool isObjective = target.GetComponentInParent<PeasantObjectiveTarget>() != null;
        TargetEngagement eng = GetOrCreateEngagement(target, isObjective);
        eng.CleanDead();

        // If attacker is an Archer or Ballista, assign a distinct non-overlapping firing slot in multi-rank formations
        if (attacker.IsArcher)
        {
            float desiredRangedDist = Mathf.Clamp(attacker.AttackRange * 0.75f, 5.5f, 9.5f);
            float actualRadius = Mathf.Max(desiredRangedDist, eng.archerRadius);
            float rankOffset = attacker.IsBallista ? 3.8f : 2.5f;
            float minSpacing = attacker.DesiredSpacingDistance;
            float staggerAngle = 180f / Mathf.Max(1, eng.archerSlotCount);

            // 1. Check if attacker already has an assigned slot in Rank 1, 2, or 3
            int myRank1 = GetAssignedSlotIndex(eng.archerSlots, attacker);
            if (myRank1 >= 0)
            {
                if (attacker.IsAgentStuck) eng.archerSlots[myRank1] = null;
                else return CalculateSlotWorldPos(eng, myRank1, actualRadius, eng.archerSlotCount, 0f);
            }

            int myRank2 = GetAssignedSlotIndex(eng.archerSupportSlots, attacker);
            if (myRank2 >= 0)
            {
                if (attacker.IsAgentStuck) eng.archerSupportSlots[myRank2] = null;
                else return CalculateSlotWorldPos(eng, myRank2, actualRadius + rankOffset, eng.archerSlotCount, staggerAngle);
            }

            int myRank3 = GetAssignedSlotIndex(eng.archerReserveSlots, attacker);
            if (myRank3 >= 0)
            {
                if (attacker.IsAgentStuck) eng.archerReserveSlots[myRank3] = null;
                else return CalculateSlotWorldPos(eng, myRank3, actualRadius + (rankOffset * 2f), eng.archerSlotCount, 0f);
            }

            // 2. Try to assign an open, properly-spaced slot in Rank 1 (front firing line)
            int openRank1 = FindBestAvailableRangedSlot(eng.archerSlots, actualRadius, eng.archerSlotCount, eng, attacker.transform.position, minSpacing, 0f, attacker);
            if (openRank1 >= 0)
            {
                eng.Release(attacker);
                eng.archerSlots[openRank1] = attacker;
                return CalculateSlotWorldPos(eng, openRank1, actualRadius, eng.archerSlotCount, 0f);
            }

            // 3. Rank 1 is full or crowded on this road! Assign to Rank 2 (second firing line, staggered)
            float rank2Radius = actualRadius + rankOffset;
            int openRank2 = FindBestAvailableRangedSlot(eng.archerSupportSlots, rank2Radius, eng.archerSlotCount, eng, attacker.transform.position, minSpacing, staggerAngle, attacker);
            if (openRank2 >= 0)
            {
                eng.Release(attacker);
                eng.archerSupportSlots[openRank2] = attacker;
                return CalculateSlotWorldPos(eng, openRank2, rank2Radius, eng.archerSlotCount, staggerAngle);
            }

            // 4. Rank 2 is full! Assign to Rank 3 (third firing rank, reserve depth)
            float rank3Radius = actualRadius + (rankOffset * 2f);
            int openRank3 = FindBestAvailableRangedSlot(eng.archerReserveSlots, rank3Radius, eng.archerSlotCount, eng, attacker.transform.position, minSpacing, 0f, attacker);
            if (openRank3 >= 0)
            {
                eng.Release(attacker);
                eng.archerReserveSlots[openRank3] = attacker;
                return CalculateSlotWorldPos(eng, openRank3, rank3Radius, eng.archerSlotCount, 0f);
            }

            // 5. Fallback: Hold position along approach vector at reserve distance with spacing checks
            Vector3 diff = attacker.transform.position - target.position;
            diff.y = 0f;
            Vector3 dirFromTarget = diff.sqrMagnitude > 0.1f ? diff.normalized : Vector3.forward;
            Vector3 rangedPos = target.position + dirFromTarget * rank3Radius;
            if (NavMesh.SamplePosition(rangedPos, out NavMeshHit navHit, navMeshSampleDistance, NavMesh.AllAreas))
            {
                return navHit.position;
            }
            return rangedPos;
        }

        // 1. Check if attacker already has an assigned primary slot on this target
        int myPrimarySlot = GetAssignedSlotIndex(eng.primarySlots, attacker);
        if (myPrimarySlot >= 0)
        {
            if (attacker.IsAgentStuck)
            {
                eng.primarySlots[myPrimarySlot] = null;
            }
            else
            {
                return CalculateSlotWorldPos(eng, myPrimarySlot, eng.primaryRadius, eng.primarySlotCount);
            }
        }

        // 2. Try to assign an open primary slot closest to the attacker
        int openSlot = FindBestAvailableSlot(eng.primarySlots, eng.primaryRadius, eng.primarySlotCount, eng, attacker.transform.position);
        if (openSlot >= 0)
        {
            eng.Release(attacker);
            eng.primarySlots[openSlot] = attacker;
            return CalculateSlotWorldPos(eng, openSlot, eng.primaryRadius, eng.primarySlotCount);
        }

        // 3. Primary ring is full! Use second-rank support reserve ring (reasonable distance)
        int mySupportSlot = GetAssignedSlotIndex(eng.supportSlots, attacker);
        if (mySupportSlot >= 0)
        {
            if (attacker.IsAgentStuck)
            {
                eng.supportSlots[mySupportSlot] = null;
            }
            else
            {
                return CalculateSlotWorldPos(eng, mySupportSlot, eng.supportRadius, eng.supportSlotCount);
            }
        }

        int openSupportSlot = FindBestAvailableSlot(eng.supportSlots, eng.supportRadius, eng.supportSlotCount, eng, attacker.transform.position);
        if (openSupportSlot >= 0)
        {
            eng.Release(attacker);
            eng.supportSlots[openSupportSlot] = attacker;
            return CalculateSlotWorldPos(eng, openSupportSlot, eng.supportRadius, eng.supportSlotCount);
        }

        // Fallback: hold reasonable distance in attacker's current approach direction
        Vector3 dir = (attacker.transform.position - target.position).normalized;
        if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
        return target.position + dir * eng.supportRadius;
    }

    private int GetAssignedSlotIndex(PeasantUnitController[] slots, PeasantUnitController unit)
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] == unit) return i;
        return -1;
    }

    /// <summary>
    /// Calculates total walking distance along corners of a computed NavMesh path.
    /// </summary>
    public static float CalculatePathLength(NavMeshPath path)
    {
        if (path == null || path.corners == null || path.corners.Length < 2)
            return float.MaxValue;

        float length = 0f;
        for (int i = 0; i < path.corners.Length - 1; i++)
        {
            length += Vector3.Distance(path.corners[i], path.corners[i + 1]);
        }
        return length;
    }

    private int FindBestAvailableSlot(
        PeasantUnitController[] slots,
        float radius,
        int totalSlots,
        TargetEngagement eng,
        Vector3 attackerPos)
    {
        int bestSlot = -1;
        float shortestScore = float.MaxValue;
        NavMeshPath path = new NavMeshPath();

        for (int i = 0; i < totalSlots; i++)
        {
            if (slots[i] == null || slots[i].IsDead)
            {
                Vector3 slotPos = CalculateSlotWorldPos(eng, i, radius, totalSlots);

                // Check reachability via NavMesh so units never navigate straight into obstacles/walls
                bool hasPath = NavMesh.CalculatePath(attackerPos, slotPos, NavMesh.AllAreas, path);
                bool isComplete = hasPath && path.status == NavMeshPathStatus.PathComplete;

                float score;
                if (isComplete)
                {
                    // Prioritize reachable slots by actual walking path length around obstacles
                    score = CalculatePathLength(path);
                }
                else
                {
                    // Heavy penalty for slots on the other side of impassable fences/walls
                    score = Vector3.Distance(attackerPos, slotPos) + 5000f;
                }

                if (score < shortestScore)
                {
                    shortestScore = score;
                    bestSlot = i;
                }
            }
        }

        return bestSlot;
    }

    private Vector3 CalculateSlotWorldPos(TargetEngagement eng, int slotIndex, float radius, int totalSlots, float angleOffsetDeg = 0f)
    {
        Vector3 center = eng.target.position;
        float angleStep = 360f / Mathf.Max(1, totalSlots);
        float angleDeg = eng.anchorAngleDeg + (slotIndex * angleStep) + angleOffsetDeg;

        Quaternion rot = Quaternion.Euler(0f, angleDeg, 0f);
        Vector3 offset = rot * Vector3.forward * radius;
        Vector3 desiredPos = center + offset;

        if (NavMesh.SamplePosition(desiredPos, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return desiredPos;
    }

    private bool IsPositionTooCloseToAnyOccupiedSlot(TargetEngagement eng, Vector3 testPos, float minDistance, PeasantUnitController self)
    {
        float minSqr = minDistance * minDistance;

        // Check primary slots
        if (eng.primarySlots != null)
        {
            for (int i = 0; i < eng.primarySlots.Length; i++)
            {
                PeasantUnitController unit = eng.primarySlots[i];
                if (unit != null && unit != self && !unit.IsDead)
                {
                    Vector3 p = unit.transform.position;
                    p.y = testPos.y;
                    if ((testPos - p).sqrMagnitude < minSqr) return true;
                }
            }
        }

        // Check archer rank 1
        if (eng.archerSlots != null)
        {
            for (int i = 0; i < eng.archerSlots.Length; i++)
            {
                PeasantUnitController unit = eng.archerSlots[i];
                if (unit != null && unit != self && !unit.IsDead)
                {
                    Vector3 p = unit.transform.position;
                    p.y = testPos.y;
                    if ((testPos - p).sqrMagnitude < minSqr) return true;
                }
            }
        }

        // Check archer rank 2
        if (eng.archerSupportSlots != null)
        {
            for (int i = 0; i < eng.archerSupportSlots.Length; i++)
            {
                PeasantUnitController unit = eng.archerSupportSlots[i];
                if (unit != null && unit != self && !unit.IsDead)
                {
                    Vector3 p = unit.transform.position;
                    p.y = testPos.y;
                    if ((testPos - p).sqrMagnitude < minSqr) return true;
                }
            }
        }

        // Check archer rank 3
        if (eng.archerReserveSlots != null)
        {
            for (int i = 0; i < eng.archerReserveSlots.Length; i++)
            {
                PeasantUnitController unit = eng.archerReserveSlots[i];
                if (unit != null && unit != self && !unit.IsDead)
                {
                    Vector3 p = unit.transform.position;
                    p.y = testPos.y;
                    if ((testPos - p).sqrMagnitude < minSqr) return true;
                }
            }
        }

        return false;
    }

    private bool IsPositionTooCloseToLivingAllies(Vector3 testPos, float minDistance, PeasantUnitController self)
    {
        float minSqr = minDistance * minDistance;
        int myTeam = self != null ? self.TeamId : 0;

        for (int i = 0; i < allLivingUnits.Count; i++)
        {
            PeasantUnitController unit = allLivingUnits[i];
            if (unit != null && unit != self && !unit.IsDead && unit.TeamId == myTeam)
            {
                Vector3 p = unit.transform.position;
                p.y = testPos.y;
                if ((testPos - p).sqrMagnitude < minSqr) return true;
            }
        }

        return false;
    }

    private int FindBestAvailableRangedSlot(
        PeasantUnitController[] slots,
        float radius,
        int totalSlots,
        TargetEngagement eng,
        Vector3 attackerPos,
        float minSeparation,
        float angleOffsetDeg,
        PeasantUnitController attacker)
    {
        int bestSlot = -1;
        float shortestScore = float.MaxValue;
        NavMeshPath path = new NavMeshPath();

        for (int i = 0; i < totalSlots; i++)
        {
            if (slots[i] == null || slots[i].IsDead)
            {
                Vector3 rawSlotPos = CalculateSlotWorldPos(eng, i, radius, totalSlots, angleOffsetDeg);

                // Sample onto NavMesh
                if (!NavMesh.SamplePosition(rawSlotPos, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
                    continue;

                Vector3 validPos = hit.position;

                // Validate spatial separation against all occupied slots on this target
                if (IsPositionTooCloseToAnyOccupiedSlot(eng, validPos, minSeparation, attacker))
                    continue;

                // Validate spatial separation against all other living friendly units
                if (IsPositionTooCloseToLivingAllies(validPos, minSeparation, attacker))
                    continue;

                // Check reachability via NavMesh
                bool hasPath = NavMesh.CalculatePath(attackerPos, validPos, NavMesh.AllAreas, path);
                bool isComplete = hasPath && path.status == NavMeshPathStatus.PathComplete;

                float score;
                if (isComplete)
                {
                    score = CalculatePathLength(path);
                }
                else
                {
                    score = Vector3.Distance(attackerPos, validPos) + 5000f;
                }

                if (score < shortestScore)
                {
                    shortestScore = score;
                    bestSlot = i;
                }
            }
        }

        return bestSlot;
    }

    private TargetEngagement GetOrCreateEngagement(Transform target, bool isObjective)
    {
        if (engagementMap.TryGetValue(target, out TargetEngagement existing))
            return existing;

        float primaryRad;
        float supportRad;
        int primarySlots;
        int supportSlots;

        if (isObjective)
        {
            Collider col = target.GetComponentInChildren<Collider>();
            float baseRadius = defaultObjectivePerimeterRadius;
            if (col != null)
            {
                Vector3 ext = col.bounds.extents;
                baseRadius = Mathf.Max(ext.x, ext.z) + 1.2f;
            }
            primaryRad = baseRadius + 1.2f;
            supportRad = primaryRad + 2.5f;

            // Compute slot count based on perimeter and minimum spacing
            float perimeter = 2f * Mathf.PI * primaryRad;
            primarySlots = Mathf.Clamp(Mathf.FloorToInt(perimeter / minObjectiveSlotSpacing), 4, maxAttackersPerObjective);
            supportSlots = primarySlots + 4;
        }
        else
        {
            primaryRad = combatEngagementDistance;
            supportRad = supportRankDistance;

            // Ensure minInterAttackerDistance is respected around the circumference
            float circum = 2f * Mathf.PI * primaryRad;
            int maxPossibleSlots = Mathf.Max(2, Mathf.FloorToInt(circum / minInterAttackerDistance));
            primarySlots = Mathf.Clamp(maxAttackersPerUnit, 2, maxPossibleSlots);
            supportSlots = 6;
        }

        TargetEngagement newEng = new TargetEngagement(target, isObjective, primaryRad, supportRad, primarySlots, supportSlots);
        engagementMap[target] = newEng;
        return newEng;
    }

    #endregion

    #region Flocking / Personal Space Separation Vector

    /// <summary>
    /// Calculates a personal space separation offset away from nearby friendly units.
    /// Prevents gathering when marching, attacking, or standing idle.
    /// </summary>
    public Vector3 GetSeparationVector(PeasantUnitController unit)
    {
        if (unit == null || unit.IsDead || unit.IsStationary) return Vector3.zero;

        Vector3 separation = Vector3.zero;
        int neighborCount = 0;
        Vector3 myPos = unit.transform.position;
        int myTeam = unit.TeamId;
        float myRadius = unit.PhysicalRadius;

        for (int i = 0; i < allLivingUnits.Count; i++)
        {
            PeasantUnitController other = allLivingUnits[i];
            if (other == null || other == unit || other.IsDead || other.TeamId != myTeam)
                continue;

            Vector3 diff = myPos - other.transform.position;
            diff.y = 0f;
            float sqrDist = diff.sqrMagnitude;

            // Dynamically calculate required clearance based on both unit physical footprints
            float otherRadius = other.PhysicalRadius;
            float desiredClearance = myRadius + otherRadius + ((unit.IsBallista || other.IsBallista) ? 0.85f : 0.55f);
            float clearanceSqr = desiredClearance * desiredClearance;

            if (sqrDist < clearanceSqr && sqrDist > 0.0001f)
            {
                float dist = Mathf.Sqrt(sqrDist);
                float overlap = desiredClearance - dist;
                float normalizedPush = Mathf.Clamp01(overlap / desiredClearance);

                // Urgency multiplier if physically overlapping / clipping
                float physicalDist = myRadius + otherRadius;
                float urgency = (dist < physicalDist) ? 3.0f : 1.5f;

                separation += (diff / dist) * (normalizedPush * urgency);
                neighborCount++;
            }
        }

        if (neighborCount > 0)
        {
            separation /= neighborCount;
            separation = Vector3.ClampMagnitude(separation, 2.5f) * Mathf.Max(separationStrength, 2.2f);
        }

        return separation;
    }

    #endregion

    #region Internal Registry & Tracking

    public void AssignTarget(PeasantUnitController attacker, Transform target)
    {
        if (attacker == null || target == null) return;
        ClearUnitAssignment(attacker);
        unitToTarget[attacker] = target;
    }

    public void NotifyTargetAbandoned(PeasantUnitController attacker, Transform target)
    {
        if (attacker == null) return;
        ClearUnitAssignment(attacker);
    }

    private void ClearUnitAssignment(PeasantUnitController unit)
    {
        if (unitToTarget.TryGetValue(unit, out Transform oldTarget))
        {
            if (oldTarget != null && engagementMap.TryGetValue(oldTarget, out TargetEngagement eng))
            {
                eng.Release(unit);
            }
            unitToTarget.Remove(unit);
        }
    }

    public Transform GetAssignedTarget(PeasantUnitController unit)
    {
        if (unit == null) return null;
        if (unitToTarget.TryGetValue(unit, out Transform target))
        {
            if (target != null) return target;
        }
        return null;
    }

    private void PruneNullAndDead()
    {
        allLivingUnits.RemoveAll(u => u == null || u.IsDead);
        allActiveObjectives.RemoveAll(o => o == null || o.IsDestroyed);
    }

    #endregion

    #region Scene View Visualizer

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || !Application.isPlaying) return;

        foreach (var kvp in engagementMap)
        {
            Transform target = kvp.Key;
            TargetEngagement eng = kvp.Value;
            if (target == null) continue;

            Vector3 targetPos = target.position;

            // Draw primary combat ring (reasonable distance)
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.5f);
            Gizmos.DrawWireSphere(targetPos, eng.primaryRadius);

            // Draw support reserve ring
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.3f);
            Gizmos.DrawWireSphere(targetPos, eng.supportRadius);

            // Primary slots
            for (int i = 0; i < eng.primarySlotCount; i++)
            {
                Vector3 slotPos = CalculateSlotWorldPos(eng, i, eng.primaryRadius, eng.primarySlotCount);
                bool occupied = eng.primarySlots[i] != null && !eng.primarySlots[i].IsDead;

                Gizmos.color = occupied ? Color.red : Color.green;
                Gizmos.DrawWireSphere(slotPos, 0.3f);

                if (occupied)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(slotPos, eng.primarySlots[i].transform.position);
                }
            }

            // Support slots
            for (int i = 0; i < eng.supportSlotCount; i++)
            {
                Vector3 slotPos = CalculateSlotWorldPos(eng, i, eng.supportRadius, eng.supportSlotCount);
                bool occupied = eng.supportSlots[i] != null && !eng.supportSlots[i].IsDead;

                if (occupied)
                {
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawWireSphere(slotPos, 0.25f);
                    Gizmos.DrawLine(slotPos, eng.supportSlots[i].transform.position);
                }
            }

            // Archer Rank 1 slots
            if (eng.archerSlots != null)
            {
                for (int i = 0; i < eng.archerSlotCount; i++)
                {
                    if (eng.archerSlots[i] != null && !eng.archerSlots[i].IsDead)
                    {
                        Vector3 slotPos = CalculateSlotWorldPos(eng, i, eng.archerRadius, eng.archerSlotCount, 0f);
                        Gizmos.color = Color.cyan;
                        Gizmos.DrawWireSphere(slotPos, 0.35f);
                        Gizmos.DrawLine(slotPos, eng.archerSlots[i].transform.position);
                    }
                }
            }

            // Archer Rank 2 slots
            if (eng.archerSupportSlots != null)
            {
                float stagger = 180f / Mathf.Max(1, eng.archerSlotCount);
                for (int i = 0; i < eng.archerSlotCount; i++)
                {
                    if (eng.archerSupportSlots[i] != null && !eng.archerSupportSlots[i].IsDead)
                    {
                        Vector3 slotPos = CalculateSlotWorldPos(eng, i, eng.archerRadius + 3.8f, eng.archerSlotCount, stagger);
                        Gizmos.color = new Color(1f, 0.5f, 0f); // Orange
                        Gizmos.DrawWireSphere(slotPos, 0.35f);
                        Gizmos.DrawLine(slotPos, eng.archerSupportSlots[i].transform.position);
                    }
                }
            }

            // Archer Rank 3 slots
            if (eng.archerReserveSlots != null)
            {
                for (int i = 0; i < eng.archerSlotCount; i++)
                {
                    if (eng.archerReserveSlots[i] != null && !eng.archerReserveSlots[i].IsDead)
                    {
                        Vector3 slotPos = CalculateSlotWorldPos(eng, i, eng.archerRadius + 7.6f, eng.archerSlotCount, 0f);
                        Gizmos.color = Color.magenta;
                        Gizmos.DrawWireSphere(slotPos, 0.35f);
                        Gizmos.DrawLine(slotPos, eng.archerReserveSlots[i].transform.position);
                    }
                }
            }
        }
    }

    #endregion
}
