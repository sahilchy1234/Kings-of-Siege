using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Spawns a peasant when a UI Button is clicked.
/// Add this to a scene GameObject, assign the prefab and spawn point,
/// then connect the Button's On Click() to SpawnPeasant().
/// </summary>
public class PeasantButtonSpawner : MonoBehaviour
{
    [Header("Prefab and Spawn Points")]
    [SerializeField] private GameObject[] peasantPrefabs;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private bool randomizeSpawnPoint = true;

    [Header("Team")]
    [SerializeField] private int teamId = 0;

    [Header("Spawn Limits")]
    [Tooltip("Set to 0 for unlimited living peasants.")]
    [SerializeField, Min(0f)] private float spawnRadius = 2f;
    [SerializeField, Min(0f)] private float spawnCooldown = 0.5f;

    [Header("Optional UI")]
    [SerializeField] private Button spawnButton;
    [SerializeField] private TMP_Text statusText;

    [Header("NavMesh")]
    [SerializeField, Min(0.1f)] private float navMeshSampleDistance = 3f;

    private float nextSpawnTime;
    private int currentSpawnIndex = 0;

    private void Awake()
    {
    
    }

    private void OnDestroy()
    {

    }

    public void SetTeamId(int newTeamId)
    {
        teamId = newTeamId;
    }

    public void SetSpawnPoints(Transform[] newPoints)
    {
        if (newPoints != null && newPoints.Length > 0)
        {
            spawnPoints = newPoints;
        }
    }

    public Transform[] GetSpawnPoints() => spawnPoints;

    /// <summary>Connect this public method to the UI Button's On Click() event.</summary>
    public void SpawnPeasant(int index)
    {
        if (!MatchReadyScreen.IsMatchStarted)
        {
            SetStatus("Waiting for all players to be ready...");
            return;
        }

        // In multiplayer, delegate spawn request through NetworkBattleBridge RPC
        if (NetworkBattleBridge.Instance != null && NetworkBattleBridge.Instance.IsMultiplayer)
        {
            NetworkBattleBridge.Instance.RequestSpawnUnit(index);
            return;
        }

        SpawnPeasantDirect(teamId, index);
    }

    public void SpawnPeasantDirect(int targetTeam, int index)
    {
        EnsurePrefabsLoaded();
        Transform targetSpawnPoint = GetSpawnPoint();
        if (targetSpawnPoint == null) return;

        Vector3 spawnPos = targetSpawnPoint.position;
        if (spawnRadius > 0f)
        {
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            spawnPos += new Vector3(offset.x, 0f, offset.y);
        }

        if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
        {
            spawnPos = hit.position;
        }

        SpawnAtExactLocation(targetTeam, index, spawnPos, targetSpawnPoint.rotation, 0);
    }

    /// <summary>
    /// Spawns a unit at the exact world position and rotation provided across the network.
    /// Eliminates random seed and NavMesh sampling discrepancies between machines.
    /// </summary>
    public void SpawnAtExactLocation(int targetTeam, int index, Vector3 exactPos, Quaternion exactRot, int netId)
    {
        EnsurePrefabsLoaded();

        if (peasantPrefabs == null || peasantPrefabs.Length == 0) return;
        if (index < 0 || index >= peasantPrefabs.Length) index = 0;
        if (peasantPrefabs[index] == null) return;

        GameObject spawned = Instantiate(peasantPrefabs[index], exactPos, exactRot);

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
            controller.SetTeamId(targetTeam);

            if (NetworkBattleBridge.Instance != null && netId > 0)
            {
                NetworkBattleBridge.Instance.RegisterUnit(netId, controller);
            }
        }
        else
        {
            Debug.LogWarning("[PeasantButtonSpawner] The prefab has no PeasantUnitController.", spawned);
        }

        nextSpawnTime = Time.time + spawnCooldown;
        SetStatus("Unit spawned for Team " + targetTeam + "!");
    }

    private void EnsurePrefabsLoaded()
    {
        if (peasantPrefabs == null || peasantPrefabs.Length == 0)
        {
            GameObject fallback = Resources.Load<GameObject>("Powerups/TT_Halberdier");
            if (fallback != null)
            {
                peasantPrefabs = new GameObject[] { fallback };
            }
            if (peasantPrefabs == null || peasantPrefabs.Length == 0)
            {
                string[] guids = RuntimeAssets.FindAssets("t:Prefab TT_Halberdier");
                if (guids == null || guids.Length == 0) guids = RuntimeAssets.FindAssets("t:Prefab Peasant");
                if (guids != null && guids.Length > 0)
                {
                    peasantPrefabs = new GameObject[] { RuntimeAssets.LoadAssetAtPath<GameObject>(RuntimeAssets.GUIDToAssetPath(guids[0])) };
                }
            }
        }
    }

    public void SetPeasantPrefabs(GameObject[] prefabs)
    {
        peasantPrefabs = prefabs;
    }

    public GameObject[] PeasantPrefabs => peasantPrefabs;

    private Transform GetSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return null;

        if (randomizeSpawnPoint)
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

    private int CountAlivePeasants()
    {
        PeasantUnitController[] units = FindObjectsByType<PeasantUnitController>(FindObjectsSortMode.None);
        int count = 0;

        foreach (PeasantUnitController unit in units)
        {
            if (unit != null && !unit.IsDead && unit.TeamId == teamId)
                count++;
        }

        return count;
    }

    private void SetStatus(string message)
    {
        Debug.Log("[PeasantButtonSpawner] " + message, this);
        if (statusText != null)
            statusText.text = message;
    }
}
