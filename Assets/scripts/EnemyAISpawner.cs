using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Handles AI/Enemy unit spawning automatically or in waves.
/// Each spawned enemy uses the PeasantUnitController component.
/// </summary>
public class EnemyAISpawner : MonoBehaviour
{
    public enum SpawnerMode
    {
        Continuous, // Spawns units steadily on a cooldown timer
        WaveBased   // Spawns batches in waves with a delay between waves
    }

    [Header("Enemy Prefabs and Spawning")]
    [Tooltip("Enemy variant prefabs containing PeasantUnitController.")]
    [SerializeField] private GameObject[] enemyPrefabs;
    [Tooltip("If true, picks randomly among the enemyPrefabs; otherwise cycles through them.")]
    [SerializeField] private bool randomizePrefabVariant = true;
    [Tooltip("One or more spawn points where enemies can appear.")]
    [SerializeField] private Transform[] spawnPoints;
    [Tooltip("If true, picks randomly from spawnPoints; otherwise cycles sequentially.")]
    [SerializeField] private bool randomizeSpawnPoints = true;

    [Header("AI Team Settings")]
    [Tooltip("Team ID for enemy units (defaults to 1, while player is 0).")]
    [SerializeField] private int enemyTeamId = 1;
    [Tooltip("Optional power multiplier applied to spawned enemy units.")]
    [SerializeField, Min(0.1f)] private float enemyPowerMultiplier = 1f;

    [Header("Spawner Mode")]
    [SerializeField] private SpawnerMode mode = SpawnerMode.Continuous;
    [SerializeField] private bool autoStartSpawning = true;
    [SerializeField, Min(0f)] private float startDelay = 2f;

    [Header("Continuous Spawning Settings")]
    [SerializeField, Min(0.2f)] private float spawnInterval = 3f;

    [Header("Wave Spawning Settings")]
    [SerializeField, Min(1)] private int unitsPerWave = 3;
    [SerializeField, Min(0.1f)] private float delayBetweenUnitsInWave = 0.5f;
    [SerializeField, Min(1f)] private float delayBetweenWaves = 8f;
    [SerializeField, Min(1)] private int maxWaves = 10; // set 0 or high for infinite

    [Header("Spawn Limits & Positioning")]
    [Tooltip("Max concurrent living enemies for this team. Set to 0 for unlimited.")]
    [SerializeField, Min(0)] private int maxLivingEnemies = 15;
    [SerializeField, Min(0f)] private float spawnRadius = 1.5f;
    [SerializeField, Min(0.1f)] private float navMeshSampleDistance = 3f;

    private int currentSpawnIndex = 0;
    private int currentPrefabIndex = 0;
    private int currentWave = 0;
    private bool isSpawningActive = false;
    private Coroutine spawnLoopRoutine;

    public bool IsSpawningActive => isSpawningActive;
    public int CurrentWave => currentWave;
    public Transform[] GetSpawnPoints() => spawnPoints;
    public GameObject[] GetEnemyPrefabs() => enemyPrefabs;
    public int MaxLivingEnemies => maxLivingEnemies;

    /// <summary>Scales the power of every enemy spawned from now on (used for level-based difficulty).</summary>
    public void SetPowerMultiplier(float multiplier)
    {
        enemyPowerMultiplier = Mathf.Max(0.1f, multiplier);
    }

    private void Start()
    {
        if (GameModeManager.IsMultiplayer)
        {
            enabled = false;
            return;
        }

        // SmartAIPlayer owns spawning when present; it sets AiControlled in Awake (before any Start).
        if (autoStartSpawning && !AiControlled)
        {
            StartSpawning();
        }
    }

    /// <summary>True while SmartAIPlayer decides spawns itself. Stops this spawner's timer loop from starting.</summary>
    public bool AiControlled { get; set; }

    public void StartSpawning()
    {
        if (isSpawningActive) return;

        isSpawningActive = true;
        if (spawnLoopRoutine != null) StopCoroutine(spawnLoopRoutine);
        spawnLoopRoutine = StartCoroutine(SpawningRoutine());
    }

    public void StopSpawning()
    {
        isSpawningActive = false;
        if (spawnLoopRoutine != null)
        {
            StopCoroutine(spawnLoopRoutine);
            spawnLoopRoutine = null;
        }
    }

    private IEnumerator SpawningRoutine()
    {
        while (!MatchReadyScreen.IsMatchStarted)
            yield return null;

        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        if (mode == SpawnerMode.Continuous)
        {
            while (isSpawningActive)
            {
                if (CanSpawn())
                {
                    SpawnSingleEnemy();
                }
                yield return new WaitForSeconds(spawnInterval);
            }
        }
        else if (mode == SpawnerMode.WaveBased)
        {
            while (isSpawningActive && (maxWaves <= 0 || currentWave < maxWaves))
            {
                currentWave++;
                yield return StartCoroutine(SpawnWaveRoutine());
                yield return new WaitForSeconds(delayBetweenWaves);
            }
        }

        spawnLoopRoutine = null;
    }

    private IEnumerator SpawnWaveRoutine()
    {
        for (int i = 0; i < unitsPerWave; i++)
        {
            if (!isSpawningActive) yield break;

            if (CanSpawn())
            {
                SpawnSingleEnemy();
            }

            if (delayBetweenUnitsInWave > 0f)
                yield return new WaitForSeconds(delayBetweenUnitsInWave);
        }
    }

    public GameObject SpawnSingleEnemy()
    {
        GameObject prefabToSpawn = GetEnemyPrefab();
        if (prefabToSpawn == null)
        {
            Debug.LogError("[EnemyAISpawner] No valid enemy prefab assigned in the Inspector.", this);
            return null;
        }

        Transform spawnPoint = GetSpawnPoint();
        if (spawnPoint == null)
        {
            Debug.LogError("[EnemyAISpawner] No valid spawn points assigned in the Inspector.", this);
            return null;
        }

        return SpawnPrefabAt(prefabToSpawn, spawnPoint);
    }

    /// <summary>Spawns a specific pool prefab at a specific spawn point (used by SmartAIPlayer to pick units and lanes).</summary>
    public GameObject SpawnPrefabAt(GameObject prefabToSpawn, Transform spawnPoint)
    {
        if (prefabToSpawn == null || spawnPoint == null) return null;

        Vector3 spawnPos = spawnPoint.position;
        if (spawnRadius > 0f)
        {
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            spawnPos += new Vector3(randomCircle.x, 0f, randomCircle.y);
        }

        if (!NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
        {
            Debug.LogWarning("[EnemyAISpawner] Could not find valid NavMesh position near spawn point.", this);
            return null;
        }

        GameObject spawned = Instantiate(prefabToSpawn, hit.position, spawnPoint.rotation);

        PeasantUnitController controller = spawned.GetComponentInChildren<PeasantUnitController>();
        bool isMachine = (controller != null && controller.IsMachine) ||
                         spawned.name.IndexOf("ballista", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         spawned.name.IndexOf("catapult", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         spawned.name.IndexOf("ram", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         spawned.name.IndexOf("wagon", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         spawned.name.IndexOf("cart", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         spawned.name.IndexOf("siege", System.StringComparison.OrdinalIgnoreCase) >= 0;

        spawned.transform.localScale = (controller != null) ? controller.DeployedScale : (isMachine ? Vector3.one : new Vector3(0.77f, 0.77f, 0.77f));
        if (controller != null)
        {
            controller.SetTeamId(enemyTeamId);
            controller.PowerMultiplier = enemyPowerMultiplier;
        }
        else
        {
            Debug.LogWarning("[EnemyAISpawner] Spawned enemy prefab does not contain PeasantUnitController.", spawned);
        }

        return spawned;
    }

    private GameObject GetEnemyPrefab()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0) return null;

        if (randomizePrefabVariant)
        {
            int randomIndex = Random.Range(0, enemyPrefabs.Length);
            for (int i = 0; i < enemyPrefabs.Length; i++)
            {
                int index = (randomIndex + i) % enemyPrefabs.Length;
                if (enemyPrefabs[index] != null) return enemyPrefabs[index];
            }
        }
        else
        {
            for (int i = 0; i < enemyPrefabs.Length; i++)
            {
                int index = (currentPrefabIndex + i) % enemyPrefabs.Length;
                if (enemyPrefabs[index] != null)
                {
                    currentPrefabIndex = (index + 1) % enemyPrefabs.Length;
                    return enemyPrefabs[index];
                }
            }
        }

        return null;
    }

    private bool CanSpawn()
    {
        if (maxLivingEnemies <= 0) return true;
        return CountLivingEnemies() < maxLivingEnemies;
    }

    public int CountLivingEnemies()
    {
        // Performance: registry instead of scene-wide FindObjectsByType on every spawn check.
        System.Collections.Generic.IReadOnlyList<PeasantUnitController> allUnits = BattleAIManager.Instance != null
            ? BattleAIManager.Instance.GetAllLivingUnits()
            : FindObjectsByType<PeasantUnitController>(FindObjectsSortMode.None);
        int count = 0;

        for (int i = 0; i < allUnits.Count; i++)
        {
            PeasantUnitController unit = allUnits[i];
            if (unit != null && !unit.IsDead && unit.TeamId == enemyTeamId)
            {
                count++;
            }
        }

        return count;
    }

    private Transform GetSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return null;

        if (randomizeSpawnPoints)
        {
            int randomIndex = Random.Range(0, spawnPoints.Length);
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                int index = (randomIndex + i) % spawnPoints.Length;
                if (spawnPoints[index] != null) return spawnPoints[index];
            }
        }
        else
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                int index = (currentSpawnIndex + i) % spawnPoints.Length;
                if (spawnPoints[index] != null)
                {
                    currentSpawnIndex = (index + 1) % spawnPoints.Length;
                    return spawnPoints[index];
                }
            }
        }

        return null;
    }

    private void OnDisable()
    {
        StopSpawning();
    }
}
