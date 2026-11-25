using System.Collections;
using System.Collections.Generic;
using Siege.Cards;
using Siege.Profile;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Autonomous Enemy AI Commander (Opponent) that plays against the player (Team 1 vs Team 0).
///
/// Decision loop (every decision tick):
///  1. Scales itself to the PLAYER'S LEVEL (PlayerProfileData): faster reactions, more elixir,
///     stronger units, a stronger deck (higher elixir cost cap) and more aggressive play.
///  2. Reads the battlefield from BattleAIManager's living-unit list (no per-frame scene scans):
///     castle threats, player clusters and the player's composition (archers / cavalry / siege / melee).
///  3. Defends with spells when the castle is threatened, otherwise casts area spells on player clusters.
///  4. Deploys units from its pool with ELIXIR management: it only spends what it has, and it scores
///     every affordable unit by counter-value against the player's current composition and by value per elixir.
///  5. Chooses a LANE: defends the spawn point nearest its castle when threatened, otherwise pushes the
///     spawn point where the player has the fewest defenders.
///
/// The AI owns spawning: EnemyAISpawner's own timer loop is stopped when the AI starts.
/// </summary>
public class SmartAIPlayer : MonoBehaviour
{
    private static SmartAIPlayer instance;
    public static SmartAIPlayer Instance => instance;

    [Header("AI Control Toggle")]
    [Tooltip("Enable or disable autonomous opponent AI at any time.")]
    [SerializeField] private bool aiActive = true;

    [Header("Team Configuration")]
    [Tooltip("Team ID for this AI opponent (Defaults to 1, while Player is 0).")]
    [SerializeField] private int teamId = 1;

    [Header("Decision Timing (at player level 1; faster at higher levels)")]
    [Tooltip("Evaluation interval between tactical assessments at player level 1 (seconds).")]
    [SerializeField, Range(0.2f, 2.0f)] private float decisionInterval = 1.0f;
    [Tooltip("Evaluation interval at the maximum player level (seconds). Lower = faster reactions.")]
    [SerializeField, Range(0.1f, 1.0f)] private float maxLevelDecisionInterval = 0.35f;

    [Header("Energy & Economy Management (elixir)")]
    [Tooltip("Enemy AI simulated max energy (elixir).")]
    [SerializeField] private float maxEnergy = 8f;
    [Tooltip("Enemy AI energy fill rate at player level 1 (segments per second).")]
    [SerializeField] private float energyFillRate = 0.8f;
    [Tooltip("Energy fill multiplier at the maximum player level.")]
    [SerializeField, Min(1f)] private float maxLevelEnergyFillMultiplier = 1.35f;
    [Tooltip("Current enemy energy.")]
    [SerializeField] private float currentEnergy = 4f;
    [Tooltip("Aggressiveness at player level 1 (0 = passive, 1 = hyper aggressive).")]
    [SerializeField, Range(0.1f, 1f)] private float aggressiveness = 0.6f;

    [Header("Level Scaling (player level -> AI strength)")]
    [Tooltip("Turn level scaling on or off.")]
    [SerializeField] private bool useLevelScaling = true;
    [Tooltip("Enemy unit power multiplier at player level 1.")]
    [SerializeField, Min(0.1f)] private float minUnitPowerMultiplier = 0.85f;
    [Tooltip("Enemy unit power multiplier at the maximum player level.")]
    [SerializeField, Min(0.1f)] private float maxUnitPowerMultiplier = 1.6f;
    [Tooltip("Highest unit elixir cost the AI will deploy at player level 1.")]
    [SerializeField, Range(1, 8)] private int minDeployCostCap = 3;
    [Tooltip("Highest unit elixir cost the AI will deploy at the maximum player level.")]
    [SerializeField, Range(1, 8)] private int maxDeployCostCap = 8;
    [Tooltip("Seconds between unit deployments at player level 1.")]
    [SerializeField, Min(0.5f)] private float deployCooldown = 3.5f;
    [Tooltip("Seconds between unit deployments at the maximum player level.")]
    [SerializeField, Min(0.5f)] private float maxLevelDeployCooldown = 2.0f;

    [Header("Opponent Powerup Arsenal")]
    [Tooltip("Allow the opponent AI to unleash powerup spells against the player.")]
    [SerializeField] private bool allowPowerupCasts = true;
    [Tooltip("Minimum number of clustered player units required to justify area damage spells.")]
    [SerializeField, Range(1, 6)] private int minUnitsForAoe = 2;
    [Tooltip("Cooldown between enemy AI powerup casts (seconds).")]
    [SerializeField, Range(1.5f, 10f)] private float powerupCastCooldown = 3.5f;

    [Tooltip("List of powerups in the opponent AI's spell arsenal.")]
    [SerializeField] private List<PowerupType> aiArsenal = new List<PowerupType>
    {
        PowerupType.BigFireball,
        PowerupType.ArrowShower,
        PowerupType.WoodRolling,
        PowerupType.ShieldDome,
        PowerupType.StoneShower,
        PowerupType.FireArrows,
        PowerupType.SteelTrap,
        PowerupType.Fence
    };

    [Header("References (Optional Inspector Links)")]
    [SerializeField] private EnemyAISpawner enemySpawner;
    [SerializeField] private PeasantObjectiveTarget myCastle;     // Enemy Castle (Team 1)
    [SerializeField] private PeasantObjectiveTarget opponentCastle; // Player Castle (Team 0)

    // ───────────────────────── Runtime state ─────────────────────────

    /// <summary>One deployable unit from the spawner pool, with values read once from its prefab.</summary>
    private class UnitProfile
    {
        public GameObject prefab;
        public int cost;          // elixir
        public float power;       // hp * dps, used for value-per-elixir
        public bool isArcher;
        public bool isMachine;    // ballista, catapult, ram
        public bool isMounted;    // cavalry / mounted units
    }

    private readonly List<UnitProfile> unitPool = new List<UnitProfile>();
    private readonly List<PeasantUnitController> myUnits = new List<PeasantUnitController>();
    private readonly List<PeasantUnitController> playerUnits = new List<PeasantUnitController>();

    private float nextAllowedPowerupTime;
    private float nextDeployTime;
    private float nextLevelRefreshTime;
    private int playerLevel = 1;
    private float levelT;     // 0 at level 1 .. 1 at max level
    private bool poolBuilt;
    private readonly Dictionary<PowerupType, float> aiPowerupCooldowns = new Dictionary<PowerupType, float>();
    private PowerupType[] _cooldownKeysBuffer;

    public bool IsAIActive
    {
        get => aiActive;
        set => aiActive = value;
    }

    public float CurrentEnergy => currentEnergy;
    /// <summary>Base fill rate and level multiplier, read by BattlePacingDirector so the player regenerates at the same pace.</summary>
    public float BaseEnergyFillRate => energyFillRate;
    public float StartingEnergy => currentEnergy;
    public float LevelEnergyMultiplier => useLevelScaling ? Mathf.Lerp(1f, maxLevelEnergyFillMultiplier, levelT) : 1f;
    /// <summary>Set by BattlePacingDirector for double elixir (2x) phases.</summary>
    public float ExternalEnergyMultiplier { get; set; } = 1f;
    public int PlayerLevel => playerLevel;
    public int PoolSize => unitPool.Count;

    private void Awake()
    {
        if (instance == null) instance = this;

        // Claim spawning before any Start runs, so the spawner's auto-start cannot race with the AI.
        if (enemySpawner == null) enemySpawner = FindFirstObjectByType<EnemyAISpawner>();
        if (enemySpawner != null) enemySpawner.AiControlled = true;
    }

    private void Start()
    {
        if (GameModeManager.IsMultiplayer)
        {
            aiActive = false;
            enabled = false;
            return;
        }

        FindSceneDependencies();
        RefreshLevelScaling(true);
        StartCoroutine(AIDecisionLoopRoutine());
    }

    private void Update()
    {
        if (!aiActive) return;
        if (!MatchReadyScreen.IsMatchStarted) return;

        // Elixir regeneration (faster at higher player levels)
        if (currentEnergy < maxEnergy)
        {
            float fill = energyFillRate * LevelEnergyMultiplier * ExternalEnergyMultiplier;
            currentEnergy = Mathf.Min(maxEnergy, currentEnergy + (fill * Time.deltaTime));
        }

        // Tick internal powerup cooldowns
        if (_cooldownKeysBuffer == null || _cooldownKeysBuffer.Length != aiPowerupCooldowns.Count)
            _cooldownKeysBuffer = new PowerupType[aiPowerupCooldowns.Count];
        aiPowerupCooldowns.Keys.CopyTo(_cooldownKeysBuffer, 0);
        for (int i = 0; i < _cooldownKeysBuffer.Length; i++)
        {
            PowerupType t = _cooldownKeysBuffer[i];
            if (aiPowerupCooldowns[t] > 0f)
                aiPowerupCooldowns[t] = Mathf.Max(0f, aiPowerupCooldowns[t] - Time.deltaTime);
        }
    }

    private void FindSceneDependencies()
    {
        if (enemySpawner == null)
            enemySpawner = FindFirstObjectByType<EnemyAISpawner>();

        // The AI owns spawning now: stop the spawner's own timer loop.
        if (enemySpawner != null)
        {
            enemySpawner.StopSpawning();
            if (!poolBuilt) BuildUnitPool();
        }

        if (myCastle == null || opponentCastle == null)
        {
            PeasantObjectiveTarget[] objectives = FindObjectsByType<PeasantObjectiveTarget>(FindObjectsSortMode.None);
            foreach (var obj in objectives)
            {
                if (obj == null) continue;
                bool isCastle = obj.ObjectiveName == "Castle" || obj.name.ToLower().Contains("castle");
                if (isCastle)
                {
                    if (obj.TeamId == teamId && myCastle == null) myCastle = obj;
                    else if (obj.TeamId != teamId && opponentCastle == null) opponentCastle = obj;
                }
            }
        }
    }

    // ───────────────────────── Unit pool ─────────────────────────

    /// <summary>
    /// Reads every spawner prefab once: stats, role and an elixir cost estimated from hp * dps.
    /// Prefabs without PeasantUnitController or NavMeshAgent are skipped, so only units that can actually fight are used.
    /// </summary>
    private void BuildUnitPool()
    {
        poolBuilt = true;
        unitPool.Clear();
        GameObject[] prefabs = enemySpawner.GetEnemyPrefabs();
        if (prefabs == null) return;

        foreach (GameObject prefab in prefabs)
        {
            if (prefab == null) continue;
            PeasantUnitController pu = prefab.GetComponentInChildren<PeasantUnitController>(true);
            NavMeshAgent agent = prefab.GetComponentInChildren<NavMeshAgent>(true);
            if (pu == null || agent == null)
            {
                Debug.LogWarning($"[Enemy AI] Skipping {prefab.name}: missing PeasantUnitController or NavMeshAgent.", prefab);
                continue;
            }

            string lower = prefab.name.ToLowerInvariant();
            bool machine = pu.IsMachine || pu.IsBallista || lower.Contains("ballista") || lower.Contains("catapult") || lower.Contains("ram");
            bool mounted = !machine && (lower.Contains("mounted") || lower.Contains("cavalry") || lower.Contains("horse"));

            float dps = pu.AttackDamageProperty / Mathf.Max(0.05f, pu.AttackCooldownProperty);
            float power = Mathf.Max(1f, pu.MaxHealth) * Mathf.Max(0.1f, dps);

            // Cost 1..8: log2 scale of power around a ~1200 baseline (a basic spearman = 3), siege machines cost extra.
            float raw = 3f + 2f * Mathf.Log(power / 1200f, 2f);
            if (machine) raw += 1f;
            int cost = Mathf.Clamp(Mathf.RoundToInt(raw), 1, 8);

            unitPool.Add(new UnitProfile
            {
                prefab = prefab,
                cost = cost,
                power = power,
                isArcher = pu.IsArcher,
                isMachine = machine,
                isMounted = mounted
            });
        }

        Debug.Log($"[Enemy AI] Unit pool built: {unitPool.Count} units.");
    }

    // ───────────────────────── Level scaling ─────────────────────────

    private void RefreshLevelScaling(bool force)
    {
        if (!useLevelScaling) { playerLevel = 1; levelT = 0f; return; }
        if (!force && Time.time < nextLevelRefreshTime) return;
        nextLevelRefreshTime = Time.time + 5f;

        int level = 1;
        PlayerProfileData.ComputeLevel(CardInventoryManager.Instance, out level, out _, out _);
        playerLevel = Mathf.Clamp(level, 1, PlayerProfileData.MAX_LEVEL);
        levelT = (playerLevel - 1) / (float)(PlayerProfileData.MAX_LEVEL - 1);

        if (enemySpawner != null)
        {
            enemySpawner.SetPowerMultiplier(Mathf.Lerp(minUnitPowerMultiplier, maxUnitPowerMultiplier, levelT));
        }
    }

    private float EffectiveDecisionInterval => useLevelScaling ? Mathf.Lerp(decisionInterval, maxLevelDecisionInterval, levelT) : decisionInterval;
    private float EffectiveDeployCooldown => useLevelScaling ? Mathf.Lerp(deployCooldown, maxLevelDeployCooldown, levelT) : deployCooldown;
    private int DeployCostCap => useLevelScaling ? Mathf.RoundToInt(Mathf.Lerp(minDeployCostCap, maxDeployCostCap, levelT)) : maxDeployCostCap;

    // ───────────────────────── Decision loop ─────────────────────────

    private IEnumerator AIDecisionLoopRoutine()
    {
        while (!MatchReadyScreen.IsMatchStarted)
            yield return null;

        // Initial delay before enemy AI begins attacks
        yield return new WaitForSeconds(2.0f);

        while (true)
        {
            if (aiActive && (GameManager.Instance == null || !GameManager.Instance.IsMatchOver))
            {
                RefreshLevelScaling(false);
                MakeTacticalDecision();
            }

            yield return new WaitForSeconds(EffectiveDecisionInterval);
        }
    }

    private void MakeTacticalDecision()
    {
        // 1. Read the battlefield
        CategorizeLivingUnits(myUnits, playerUnits);
        bool isCastleUnderThreat = CheckIfCastleUnderThreat(playerUnits);

        // 2. Defensive spells when player troops reach the enemy castle
        if (isCastleUnderThreat && allowPowerupCasts && Time.time >= nextAllowedPowerupTime)
        {
            if (TryCastDefensivePowerup())
            {
                nextAllowedPowerupTime = Time.time + powerupCastCooldown;
                return;
            }
        }

        // 3. Offensive spells on player clusters
        if (allowPowerupCasts && Time.time >= nextAllowedPowerupTime && currentEnergy >= 2f)
        {
            if (TryCastTacticalPowerup(playerUnits))
            {
                nextAllowedPowerupTime = Time.time + powerupCastCooldown;
                return;
            }
        }

        // 4. Deploy units with elixir management, counter-picks and lane choice
        TryDeployUnit(playerUnits, isCastleUnderThreat);
    }

    private void TryDeployUnit(List<PeasantUnitController> enemyTroops, bool castleThreatened)
    {
        if (enemySpawner == null || unitPool.Count == 0) return;
        if (Time.time < nextDeployTime) return;

        // Respect the spawner's own living-unit cap
        int maxLiving = enemySpawner.MaxLivingEnemies;
        if (maxLiving > 0 && myUnits.Count >= maxLiving) return;

        // Player composition (counts over living player units)
        int playerArchers = 0, playerMounted = 0, playerMelee = 0, playerSiege = 0;
        for (int i = 0; i < enemyTroops.Count; i++)
        {
            var u = enemyTroops[i];
            if (u.IsArcher) playerArchers++;
            else if (u.IsMachine || u.IsBallista) playerSiege++;
            else if (u.PhysicalRadius >= 0.85f) playerMounted++;
            else playerMelee++;
        }
        float total = Mathf.Max(1f, enemyTroops.Count);
        float archerShare = playerArchers / total;
        float mountedShare = playerMounted / total;
        float siegeShare = playerSiege / total;
        float meleeShare = playerMelee / total;

        int costCap = DeployCostCap;
        UnitProfile best = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < unitPool.Count; i++)
        {
            UnitProfile p = unitPool[i];
            if (p.cost > costCap || p.cost > currentEnergy) continue;

            // Counter-picks
            float score = 1f;
            if (p.isMounted) score += archerShare * 2.5f + siegeShare * 1.5f;      // cavalry hunts archers and siege
            if (p.isArcher) score += meleeShare * 1.8f + mountedShare * 0.8f;      // archers shred melee pushes
            if (p.isMachine) score += (castleThreatened ? -2f : 0.6f) + siegeShare * -0.5f; // siege only for pushes
            if (!p.isMachine && !p.isArcher && !p.isMounted) score += mountedShare * 1.0f;   // spearmen/infantry hold against cavalry

            // Value per elixir
            score += Mathf.Clamp01(p.power / (p.cost * 120f)) * 1.2f;

            // Defence first when threatened: prefer sturdier, more expensive units
            if (castleThreatened) score += p.cost * 0.15f;

            score += Random.value * 0.25f; // small variety so the AI does not look scripted

            if (score > bestScore)
            {
                bestScore = score;
                best = p;
            }
        }

        if (best == null) return;

        Transform lane = PickLane(enemyTroops, castleThreatened);
        if (lane == null) return;

        GameObject spawned = enemySpawner.SpawnPrefabAt(best.prefab, lane);
        if (spawned == null) return;

        currentEnergy = Mathf.Max(0f, currentEnergy - best.cost);
        nextDeployTime = Time.time + EffectiveDeployCooldown;
    }

    /// <summary>
    /// Defends the spawn point closest to the enemy castle when threatened, otherwise pushes
    /// the spawn point where the player has the fewest defenders nearby.
    /// </summary>
    private Transform PickLane(List<PeasantUnitController> enemyTroops, bool castleThreatened)
    {
        Transform[] points = enemySpawner.GetSpawnPoints();
        if (points == null || points.Length == 0) return null;

        Transform best = null;
        float bestValue = float.MaxValue;
        Vector3 castlePos = myCastle != null ? myCastle.transform.position : Vector3.zero;

        for (int i = 0; i < points.Length; i++)
        {
            Transform sp = points[i];
            if (sp == null) continue;

            float value;
            if (castleThreatened && myCastle != null)
            {
                // Lowest value = closest to our castle
                value = Vector3.Distance(sp.position, castlePos);
            }
            else
            {
                // Lowest value = fewest player defenders within reach of this spawn point
                int nearby = 0;
                float r2 = 15f * 15f;
                for (int j = 0; j < enemyTroops.Count; j++)
                {
                    if ((enemyTroops[j].transform.position - sp.position).sqrMagnitude <= r2) nearby++;
                }
                value = nearby;
            }

            if (value < bestValue)
            {
                bestValue = value;
                best = sp;
            }
        }
        return best;
    }

    private void CategorizeLivingUnits(List<PeasantUnitController> mine, List<PeasantUnitController> enemies)
    {
        mine.Clear();
        enemies.Clear();

        if (BattleAIManager.Instance != null)
        {
            var units = BattleAIManager.Instance.GetAllLivingUnits();
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u == null || u.IsDead) continue;
                if (u.TeamId == teamId) mine.Add(u);
                else enemies.Add(u);
            }
        }
        else
        {
            PeasantUnitController[] all = FindObjectsByType<PeasantUnitController>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                var u = all[i];
                if (u == null || u.IsDead) continue;
                if (u.TeamId == teamId) mine.Add(u);
                else enemies.Add(u);
            }
        }
    }

    private bool CheckIfCastleUnderThreat(List<PeasantUnitController> enemies)
    {
        if (myCastle == null) return false;
        Vector3 castlePos = myCastle.transform.position;

        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] == null || enemies[i].IsDead) continue;
            float dist = Vector3.Distance(enemies[i].transform.position, castlePos);
            if (dist < 22f)
            {
                return true;
            }
        }
        return false;
    }

    private bool TryCastDefensivePowerup()
    {
        if (PowerupManager.Instance == null || myCastle == null) return false;

        Vector3 defPos = myCastle.transform.position + Vector3.left * 4f; // in front of enemy castle towards the arena center

        var defenseChoices = new List<PowerupType> { PowerupType.Fence, PowerupType.ShieldDome, PowerupType.WoodRolling, PowerupType.SteelTrap };
        defenseChoices.Shuffle();

        for (int i = 0; i < defenseChoices.Count; i++)
        {
            PowerupType type = defenseChoices[i];
            if (IsOnCooldown(type)) continue;

            PowerupData config = PowerupManager.Instance.GetConfig(type);
            float cost = config != null ? config.energyCost : 2f;
            if (cost > currentEnergy) continue;

            CastPowerupAt(type, defPos, cost);
            return true;
        }
        return false;
    }

    private bool TryCastTacticalPowerup(List<PeasantUnitController> enemies)
    {
        if (PowerupManager.Instance == null || enemies.Count == 0) return false;

        // Find best cluster of player troops
        if (!FindBestCluster(enemies, 6.5f, out Vector3 clusterCenter, out int clusterCount))
        {
            return false;
        }

        float aggr = useLevelScaling ? Mathf.Lerp(aggressiveness, Mathf.Min(1f, aggressiveness + 0.3f), levelT) : aggressiveness;
        if (clusterCount < minUnitsForAoe && Random.value > aggr)
        {
            return false;
        }

        // Select attack spell
        var attackChoices = new List<PowerupType>
        {
            PowerupType.BigFireball,
            PowerupType.ArrowShower,
            PowerupType.StoneShower,
            PowerupType.FireArrows,
            PowerupType.WoodRolling,
            PowerupType.ArcherTower
        };
        attackChoices.Shuffle();

        for (int i = 0; i < attackChoices.Count; i++)
        {
            PowerupType type = attackChoices[i];
            if (IsOnCooldown(type)) continue;

            PowerupData config = PowerupManager.Instance.GetConfig(type);
            float cost = config != null ? config.energyCost : 3f;
            if (cost > currentEnergy) continue;

            // Wood rolling rolls from enemy territory to the player side
            Vector3 castPos = (type == PowerupType.WoodRolling) ? (clusterCenter + Vector3.right * 6f) : clusterCenter;

            CastPowerupAt(type, castPos, cost);
            return true;
        }

        return false;
    }

    private void CastPowerupAt(PowerupType type, Vector3 worldPos, float cost)
    {
        if (PowerupManager.Instance == null) return;

        // Ensure the enemy AI casts STRICTLY inside the red border territory (Team 1)
        if (BattleSceneSetup.Instance != null)
        {
            worldPos = BattleSceneSetup.Instance.ClampToEnemyTerritory(worldPos);
        }

        // Execute powerup with castingTeam = teamId (Team 1 Enemy)
        bool success = PowerupManager.Instance.ExecutePowerup(type, worldPos, teamId);
        if (success)
        {
            currentEnergy = Mathf.Max(0f, currentEnergy - cost);
            PowerupData config = PowerupManager.Instance.GetConfig(type);
            float cd = config != null ? config.cooldown : 8f;
            aiPowerupCooldowns[type] = cd;
            Debug.Log($"<color=red>[Enemy AI Opponent] Cast powerup <b>{type}</b> inside Enemy Red Territory at {worldPos}!</color>");
        }
    }

    private bool IsOnCooldown(PowerupType type)
    {
        return aiPowerupCooldowns.TryGetValue(type, out float rem) && rem > 0.05f;
    }

    private bool FindBestCluster(List<PeasantUnitController> units, float searchRadius, out Vector3 bestCenter, out int maxCount)
    {
        bestCenter = Vector3.zero;
        maxCount = 0;
        float radiusSq = searchRadius * searchRadius;

        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] == null || units[i].IsDead) continue;
            Vector3 center = units[i].transform.position;
            int count = 1;

            for (int j = 0; j < units.Count; j++)
            {
                if (i == j || units[j] == null || units[j].IsDead) continue;
                if ((units[j].transform.position - center).sqrMagnitude <= radiusSq)
                {
                    count++;
                }
            }

            if (count > maxCount)
            {
                maxCount = count;
                bestCenter = center;
            }
        }
        return maxCount > 0;
    }
}

public static class ListExtensions
{
    private static readonly System.Random rng = new System.Random();
    public static void Shuffle<T>(this IList<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }
}
