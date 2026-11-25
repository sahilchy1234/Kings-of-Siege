using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// Authoritative network battle controller for PUN2 multiplayer:
/// - In Multiplayer:
///   1. Turns off all enemy AI (SmartAIPlayer & EnemyAISpawner).
///   2. Assigns Player 1 to Team 0 (Blue Base) and Player 2 to Team 1 (Red Base).
///   3. Replicates unit spawns with exact Vector3 coordinates so both players spawn at identical locations.
///   4. High-frequency (8 Hz) state synchronization keeping peasant positions, rotations, animations,
///      and health perfectly in lockstep between Host and Guest.
///   5. Replicates Castle/Objective health so both players see matching health bars.
///   6. Broadcasts powerups and resolves Victory/Defeat.
/// </summary>
[RequireComponent(typeof(PhotonView))]
public class NetworkBattleBridge : MonoBehaviourPunCallbacks, IPunObservable
{
    public static NetworkBattleBridge Instance { get; private set; }

    [Header("Network Status")]
    [SerializeField] private bool isMultiplayerActive = false;
    [SerializeField] private int localAssignedTeam = 0;

    [Header("Scene References")]
    [SerializeField] private PeasantButtonSpawner playerSpawner;
    [SerializeField] private EnemyAISpawner aiSpawner;
    [SerializeField] private SmartAIPlayer aiCommander;
    [SerializeField] private CameraSwipeController cameraController;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] team0SpawnPoints;
    [SerializeField] private Transform[] team1SpawnPoints;

    [Header("Castles / Objectives Authority")]
    [SerializeField] private PeasantObjectiveTarget playerCastle;
    [SerializeField] private PeasantObjectiveTarget enemyCastle;
    private PeasantObjectiveTarget[] allSceneObjectives;

    private PhotonView pv;
    private int unitIdCounter = 100;
    private float syncTimer = 0f;
    private const float SYNC_INTERVAL = 0.12f; // ~8 Hz heartbeat

    // Active units dictionary: NetworkUnitID -> PeasantUnitController
    private readonly Dictionary<int, PeasantUnitController> activeUnits = new Dictionary<int, PeasantUnitController>();

    public bool IsMultiplayer => isMultiplayerActive;
    public int LocalTeamId => localAssignedTeam;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstanceInBattleScene()
    {
        if (SceneManager.GetActiveScene().name == "BattleScene")
        {
            if (Instance == null && FindFirstObjectByType<NetworkBattleBridge>() == null)
            {
                GameObject go = new GameObject("[NetworkBattleBridge]");
                go.AddComponent<PhotonView>();
                go.AddComponent<NetworkBattleBridge>();
                Debug.Log("[NetworkBattleBridge] Auto-spawned runtime bridge in BattleScene.");
            }
        }
    }

    private void Awake()
    {
        Instance = this;
        Application.runInBackground = true;
        PhotonNetwork.KeepAliveInBackground = 60000;

        pv = GetComponent<PhotonView>();
        if (pv == null) pv = gameObject.AddComponent<PhotonView>();

        if (pv != null)
        {
            if (pv.ObservedComponents == null)
            {
                pv.ObservedComponents = new List<Component>();
            }
            if (!pv.ObservedComponents.Contains(this))
            {
                pv.ObservedComponents.Add(this);
                pv.Synchronization = ViewSynchronization.UnreliableOnChange;
            }
        }

        // Cache scene components
        if (playerSpawner == null) playerSpawner = FindFirstObjectByType<PeasantButtonSpawner>();
        if (aiSpawner == null) aiSpawner = FindFirstObjectByType<EnemyAISpawner>();
        if (aiCommander == null) aiCommander = FindFirstObjectByType<SmartAIPlayer>();
        if (cameraController == null) cameraController = FindFirstObjectByType<CameraSwipeController>();

        FindSpawnPointsIfMissing();
        FindCastlesIfMissing();
    }

    private void Start()
    {
        InitializeBattleMode();
    }

    private void FindSpawnPointsIfMissing()
    {
        // Cache Team 0 Spawn Points
        if (team0SpawnPoints == null || team0SpawnPoints.Length == 0)
        {
            if (playerSpawner != null)
            {
                team0SpawnPoints = playerSpawner.GetSpawnPoints();
            }
        }

        // Cache Team 1 Spawn Points
        if (team1SpawnPoints == null || team1SpawnPoints.Length == 0)
        {
            if (aiSpawner != null)
            {
                team1SpawnPoints = aiSpawner.GetSpawnPoints();
            }

            if (team1SpawnPoints == null || team1SpawnPoints.Length == 0)
            {
                // Fallback: find any transform named "spawnpoint" located on the enemy side (X > 50)
                Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
                List<Transform> t1List = new List<Transform>();
                foreach (var t in allTransforms)
                {
                    if (t.name.ToLower().Contains("spawnpoint") && t.position.x > 50f)
                    {
                        t1List.Add(t);
                    }
                }
                if (t1List.Count > 0) team1SpawnPoints = t1List.ToArray();
            }
        }
    }

    private void FindObjectivesIfMissing()
    {
        if (allSceneObjectives != null && allSceneObjectives.Length > 0) return;

        allSceneObjectives = FindObjectsByType<PeasantObjectiveTarget>(FindObjectsSortMode.None);
        if (allSceneObjectives != null && allSceneObjectives.Length > 0)
        {
            System.Array.Sort(allSceneObjectives, (a, b) =>
            {
                int c = a.TeamId.CompareTo(b.TeamId);
                if (c != 0) return c;
                c = a.ObjectiveName.CompareTo(b.ObjectiveName);
                if (c != 0) return c;
                return a.transform.position.x.CompareTo(b.transform.position.x);
            });
        }
    }

    private void FindCastlesIfMissing()
    {
        FindObjectivesIfMissing();
        if (playerCastle != null && enemyCastle != null) return;

        PeasantObjectiveTarget[] targets = allSceneObjectives != null ? allSceneObjectives : FindObjectsByType<PeasantObjectiveTarget>(FindObjectsSortMode.None);
        foreach (var t in targets)
        {
            if (t.ObjectiveName == "Castle" || t.name.ToLower().Contains("castle"))
            {
                if (t.TeamId == 0 && playerCastle == null) playerCastle = t;
                else if (t.TeamId == 1 && enemyCastle == null) enemyCastle = t;
            }
        }
    }

    /// <summary>
    /// Discovers all pre-existing units placed in the scene (such as TT_Archers on towers),
    /// deterministically assigns matching NetIDs on all clients, and registers them for full network sync.
    /// </summary>
    private void RegisterPreExistingSceneUnits()
    {
        PeasantUnitController[] sceneUnits = FindObjectsByType<PeasantUnitController>(FindObjectsSortMode.None);
        if (sceneUnits == null || sceneUnits.Length == 0) return;

        // Sort deterministically so Host and Guest assign the exact same NetID to each scene unit
        System.Array.Sort(sceneUnits, (a, b) =>
        {
            int c = a.TeamId.CompareTo(b.TeamId);
            if (c != 0) return c;
            c = a.transform.position.x.CompareTo(b.transform.position.x);
            if (c != 0) return c;
            c = a.transform.position.z.CompareTo(b.transform.position.z);
            if (c != 0) return c;
            return a.name.CompareTo(b.name);
        });

        int sceneNetId = 1;
        for (int i = 0; i < sceneUnits.Length; i++)
        {
            PeasantUnitController unit = sceneUnits[i];
            if (unit == null) continue;

            // Ensure unit matches parent objective team if parented under a tower/building
            PeasantObjectiveTarget parentObj = unit.GetComponentInParent<PeasantObjectiveTarget>();
            if (parentObj != null && unit.TeamId != parentObj.TeamId)
            {
                unit.TeamId = parentObj.TeamId;
            }

            int netId = sceneNetId++;
            RegisterUnit(netId, unit);
            unit.ApplyTeamVisuals();

            // On Guest in multiplayer, disable NavMeshAgent so proxy smoothly tracks MasterClient authority
            if (isMultiplayerActive && !PhotonNetwork.IsMasterClient)
            {
                NavMeshAgent nav = unit.GetComponent<NavMeshAgent>();
                if (nav != null) nav.enabled = false;
            }

            Debug.Log($"<color=#00FFAA>[NetworkBattleBridge] Registered Scene Unit '{unit.gameObject.name}' (Team {unit.TeamId}) as NetID {netId} at {unit.transform.position}</color>");
        }
    }

    /// <summary>
    /// Builds a camera pose that sits 'distance' units behind the castle owned by 'team',
    /// looking across the arena toward the opposing castle, at the castle's own height/Z.
    /// Returns false if either castle cannot be found.
    /// </summary>
    private bool TryGetCastleCameraView(int team, float distance, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        PeasantObjectiveTarget[] targets = FindObjectsByType<PeasantObjectiveTarget>(FindObjectsSortMode.None);
        Transform own = null;
        Transform enemy = null;
        foreach (var t in targets)
        {
            if (t == null || t.ObjectiveName != "Castle") continue;
            if (t.TeamId == team && own == null) own = t.transform;
            else if (t.TeamId != team && enemy == null) enemy = t.transform;
        }
        Debug.Log($"[NetworkBattleBridge] Guest camera castle lookup: team {team} castle={(own != null ? own.position.ToString() : "NOT FOUND")}, opposing castle={(enemy != null ? enemy.position.ToString() : "NOT FOUND")}");
        if (own == null || enemy == null) return false;

        // Mirror the Host's camera (-15, 57, 25; yaw 90) through the midpoint between the two castles,
        // so the Guest's framing and angle match the Host's exactly, just facing the other way.
        // Host pose matches the Host branch in InitializeBattleMode.
        const float hostX = -15f;
        const float hostY = 57f;
        const float hostZ = 25f;
        float midX = (own.position.x + enemy.position.x) * 0.5f;
        float mirroredX = 2f * midX - hostX;
        position = new Vector3(mirroredX, hostY, hostZ);
        rotation = Quaternion.Euler(53.5f, 270f, 0f);
        return true;
    }

    // Player 1 (blue) starting camera, in the camera's LOCAL space (as shown in the Inspector).
    // X was -17; moved 10 units further toward the blue side so more of the blue base shows at start.
    private static readonly Vector3 PlayerOneLocalPos = new Vector3(-27f, 45.52734f, 2.3f);
    private static readonly Vector3 PlayerOneLocalEuler = new Vector3(55f, 90f, 0f);

    private void ApplyPlayerOneCamera()
    {
        Transform camTr = cameraController != null ? cameraController.transform
                        : (Camera.main != null ? Camera.main.transform : null);
        if (camTr == null) return;

        Vector3 pos = PlayerOneLocalPos;
        Quaternion rot = Quaternion.Euler(PlayerOneLocalEuler);
        if (camTr.parent != null)
        {
            pos = camTr.parent.TransformPoint(pos);
            rot = camTr.parent.rotation * rot;
        }

        if (cameraController != null) cameraController.ConfigureForTeam(0, pos, rot);
        else camTr.SetPositionAndRotation(pos, rot);
    }

    private System.Collections.IEnumerator LogCameraPoseLater(Transform cam)
    {
        yield return new WaitForSeconds(1f);
        if (cam == null) yield break;
        Debug.Log($"[NetworkBattleBridge] Camera after spawn (team {localAssignedTeam}): local pos {cam.localPosition}, local rot {cam.localEulerAngles}, world pos {cam.position}");
    }

    private void InitializeBattleMode()
    {
        bool online = PhotonNetwork.IsConnected && !PhotonNetwork.OfflineMode && PhotonNetwork.InRoom;
        isMultiplayerActive = (GameModeManager.IsMultiplayer || online) && online;

        // Register any pre-placed units (e.g. TT_Archers stationed on towers)
        RegisterPreExistingSceneUnits();

        if (!isMultiplayerActive)
        {
            // === SOLO / VS AI MODE ===
            Debug.Log("[NetworkBattleBridge] Match running in SOLO / VS AI Mode.");
            localAssignedTeam = 0;
            GameModeManager.LocalTeamId = 0;

            ApplyPlayerOneCamera();

            if (aiSpawner != null) aiSpawner.enabled = true;
            if (aiCommander != null) aiCommander.enabled = true;

            if (MatchReadyScreen.Instance != null)
            {
                MatchReadyScreen.Instance.UpdateHUDPlayerNames();
            }
            return;
        }

        // === MULTIPLAYER MODE (PUN2) ===
        GameModeManager.CurrentMode = GameModeManager.GameMode.Multiplayer;
        localAssignedTeam = PhotonNetwork.IsMasterClient ? 0 : 1;
        GameModeManager.LocalTeamId = localAssignedTeam;

        Debug.Log($"<color=#00FFAA>[NetworkBattleBridge] Online 1v1 Battle Initialized! MasterClient={PhotonNetwork.IsMasterClient}, LocalTeam={localAssignedTeam}</color>");

        // 1. Disable Autonomous AI so real human players command the armies!
        EnemyAISpawner[] allAiSpawners = FindObjectsByType<EnemyAISpawner>(FindObjectsSortMode.None);
        foreach (var s in allAiSpawners)
        {
            s.enabled = false;
            s.StopAllCoroutines();
        }
        SmartAIPlayer[] allAiCommanders = FindObjectsByType<SmartAIPlayer>(FindObjectsSortMode.None);
        foreach (var c in allAiCommanders)
        {
            c.enabled = false;
            c.StopAllCoroutines();
        }

        // 2. Configure Camera View for Player Perspective
        if (localAssignedTeam == 1)
        {
            // Team 1 (Guest): the pose the user set in the Inspector. Main Camera is a CHILD of
            // "GameObject (2)/GameObject", so the Inspector shows LOCAL values. Convert them to
            // world space before handing them to the swipe controller (which works in world space).
            Vector3 team1CamPos = new Vector3(165.3f, 61.02135f, 1.8f);
            Quaternion team1CamRot = Quaternion.Euler(55f, 271.413f, 0f);
            Transform camTr = cameraController != null ? cameraController.transform
                            : (Camera.main != null ? Camera.main.transform : null);
            if (camTr != null && camTr.parent != null)
            {
                team1CamPos = camTr.parent.TransformPoint(team1CamPos);
                team1CamRot = camTr.parent.rotation * team1CamRot;
            }

            if (cameraController != null)
            {
                cameraController.ConfigureForTeam(1, team1CamPos, team1CamRot);
            }
            else
            {
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    mainCam.transform.position = team1CamPos;
                    mainCam.transform.rotation = team1CamRot;
                }
            }
        }
        else
        {
            // Team 0 (Host / Solo): Position camera behind Blue Base (X = -15.0), standard angle (Yaw = 90°)
            ApplyPlayerOneCamera();
        }

        // Both players use the same orthographic setup (values from the Inspector).
        Camera playerCam = cameraController != null ? cameraController.GetComponent<Camera>() : Camera.main;
        if (playerCam != null)
        {
            playerCam.orthographic = true;
            playerCam.orthographicSize = 24.6f;
            playerCam.nearClipPlane = 0.3f;
            playerCam.farClipPlane = 200f;
            StartCoroutine(LogCameraPoseLater(playerCam.transform));
        }

        // 3. Refresh Visuals on Castles, Units, and Battlefield for Local Perspective
        if (MatchReadyScreen.Instance != null)
        {
            MatchReadyScreen.Instance.UpdateHUDPlayerNames();
        }

        if (BattleSceneSetup.Instance != null)
        {
            BattleSceneSetup.Instance.BuildBattlefield();
        }

        PeasantObjectiveTarget[] castles = FindObjectsByType<PeasantObjectiveTarget>(FindObjectsSortMode.None);
        foreach (var c in castles)
        {
            c.ApplyTeamHealthBarColor();
        }

        TeamMaterialSwapper[] swappers = FindObjectsByType<TeamMaterialSwapper>(FindObjectsSortMode.None);
        foreach (var s in swappers)
        {
            s.ApplyTeamMaterial();
        }

        PeasantUnitController[] units = FindObjectsByType<PeasantUnitController>(FindObjectsSortMode.None);
        foreach (var u in units)
        {
            u.ApplyTeamVisuals();
        }
    }

    private void Update()
    {
        if (!isMultiplayerActive) return;

        // MasterClient broadcasts position, health, and state synchronization at 8 Hz
        if (PhotonNetwork.IsMasterClient)
        {
            syncTimer += Time.deltaTime;
            if (syncTimer >= SYNC_INTERVAL)
            {
                syncTimer = 0f;
                BroadcastUnitSync();
            }
        }
    }

    #region Unit Registry

    public void RegisterUnit(int netId, PeasantUnitController controller)
    {
        if (controller == null) return;
        activeUnits[netId] = controller;
    }

    public int GetUnitNetId(PeasantUnitController controller)
    {
        if (controller == null) return -1;
        foreach (var kvp in activeUnits)
        {
            if (kvp.Value == controller)
                return kvp.Key;
        }
        return -1;
    }

    public void UnregisterUnit(PeasantUnitController controller)
    {
        if (controller == null) return;
        int toRemove = -1;
        foreach (var kvp in activeUnits)
        {
            if (kvp.Value == controller)
            {
                toRemove = kvp.Key;
                break;
            }
        }
        if (toRemove != -1)
        {
            activeUnits.Remove(toRemove);
        }
    }

    /// <summary>
    /// Explicitly broadcasts authoritative unit death from MasterClient to Guest proxy.
    /// Ensures death animation, health bar hiding, and despawn happen immediately on all screens.
    /// </summary>
    public void BroadcastUnitDeath(PeasantUnitController controller)
    {
        if (!isMultiplayerActive || pv == null) return;
        if (controller == null) return;

        int netId = -1;
        foreach (var kvp in activeUnits)
        {
            if (kvp.Value == controller)
            {
                netId = kvp.Key;
                break;
            }
        }

        if (netId != -1)
        {
            activeUnits.Remove(netId);
            pv.RPC(nameof(RPC_UnitDied), RpcTarget.Others, netId);
        }
    }

    [PunRPC]
    private void RPC_UnitDied(int netId)
    {
        if (activeUnits.TryGetValue(netId, out PeasantUnitController ctrl))
        {
            activeUnits.Remove(netId);
            if (ctrl != null && !ctrl.IsDead)
            {
                ctrl.DieNetworked();
            }
        }
    }

    #endregion

    #region Unit Spawning Synchronization

    /// <summary>
    /// Invoked when a player clicks a spawn button in the UI.
    /// Routes the request through the authoritative MasterClient so units spawn with identical coordinates on both devices.
    /// </summary>
    public void RequestSpawnUnit(int unitIndex)
    {
        if (!MatchReadyScreen.IsMatchStarted) return;

        if (!isMultiplayerActive)
        {
            if (playerSpawner != null) playerSpawner.SpawnPeasantDirect(localAssignedTeam, unitIndex);
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            // MasterClient calculates exact world coordinates and broadcasts spawn
            SpawnAuthoritativeUnit(localAssignedTeam, unitIndex);
        }
        else
        {
            // Guest Client asks MasterClient to spawn a unit for Team 1
            pv.RPC(nameof(RPC_RequestSpawn), RpcTarget.MasterClient, localAssignedTeam, unitIndex);
        }
    }

    [PunRPC]
    private void RPC_RequestSpawn(int requestingTeamId, int unitIndex)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        SpawnAuthoritativeUnit(requestingTeamId, unitIndex);
    }

    private void SpawnAuthoritativeUnit(int teamId, int unitIndex)
    {
        FindSpawnPointsIfMissing();

        Transform[] points = (teamId == 0) ? team0SpawnPoints : team1SpawnPoints;
        if (points == null || points.Length == 0)
        {
            Debug.LogError($"[NetworkBattleBridge] No spawn points found for Team {teamId}!");
            return;
        }

        // Pick one of the team's spawn points
        Transform selectedPoint = points[Random.Range(0, points.Length)];
        Vector3 spawnPos = selectedPoint.position;

        // Add small random circle dispersion
        Vector2 offset = Random.insideUnitCircle * 1.5f;
        spawnPos += new Vector3(offset.x, 0f, offset.y);

        // Snap onto NavMesh
        if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 3.5f, NavMesh.AllAreas))
        {
            spawnPos = hit.position;
        }

        Quaternion spawnRot = selectedPoint.rotation;
        int netId = ++unitIdCounter;

        // Broadcast to ALL players with the exact Vector3 position and Quaternion rotation!
        pv.RPC(nameof(RPC_SpawnPeasantAt), RpcTarget.All, teamId, unitIndex, spawnPos, spawnRot, netId);
    }

    [PunRPC]
    private void RPC_SpawnPeasantAt(int teamId, int unitIndex, Vector3 spawnPos, Quaternion spawnRot, int netId)
    {
        Debug.Log($"[NetworkBattleBridge] Spawning Team {teamId} Unit {unitIndex} at exact pos {spawnPos} (NetID: {netId})");
        if (playerSpawner != null)
        {
            playerSpawner.SpawnAtExactLocation(teamId, unitIndex, spawnPos, spawnRot, netId);
        }
    }

    #endregion

    #region High-Frequency Position & Combat Synchronization (8 Hz)

    private void BroadcastUnitSync()
    {
        if (!MatchReadyScreen.IsMatchStarted) return;
        if (activeUnits.Count == 0) return;

        // Clean out any destroyed units
        List<int> toRemove = null;
        foreach (var kvp in activeUnits)
        {
            if (kvp.Value == null)
            {
                if (toRemove == null) toRemove = new List<int>();
                toRemove.Add(kvp.Key);
            }
        }
        if (toRemove != null)
        {
            foreach (int id in toRemove) activeUnits.Remove(id);
        }

        int count = activeUnits.Count;
        if (count == 0) return;

        int[] ids = new int[count];
        Vector3[] positions = new Vector3[count];
        float[] yaws = new float[count];
        float[] healths = new float[count];
        byte[] states = new byte[count];
        bool[] runnings = new bool[count];

        int i = 0;
        foreach (var kvp in activeUnits)
        {
            ids[i] = kvp.Key;
            PeasantUnitController ctrl = kvp.Value;
            positions[i] = ctrl.transform.position;
            yaws[i] = ctrl.transform.eulerAngles.y;
            healths[i] = ctrl.CurrentHealth;
            states[i] = (byte)ctrl.CurrentState;
            runnings[i] = ctrl.IsRunning;
            i++;
        }

        // Send to Guest Client
        pv.RPC(nameof(RPC_ApplyUnitSync), RpcTarget.Others, ids, positions, yaws, healths, states, runnings);
    }

    [PunRPC]
    private void RPC_ApplyUnitSync(int[] ids, Vector3[] positions, float[] yaws, float[] healths, byte[] states, bool[] runnings)
    {
        HashSet<int> receivedIds = new HashSet<int>(ids);

        for (int i = 0; i < ids.Length; i++)
        {
            int id = ids[i];
            if (activeUnits.TryGetValue(id, out PeasantUnitController ctrl) && ctrl != null)
            {
                if (ctrl.IsDead) continue;

                Quaternion rot = Quaternion.Euler(0f, yaws[i], 0f);
                ctrl.UpdateNetworkProxyState(positions[i], rot, healths[i], states[i], runnings[i]);
            }
        }

        // Prune any units on the Guest that MasterClient is no longer broadcasting (dead or despawned)
        List<int> missing = null;
        foreach (var kvp in activeUnits)
        {
            int id = kvp.Key;
            PeasantUnitController ctrl = kvp.Value;
            if (ctrl == null)
            {
                if (missing == null) missing = new List<int>();
                missing.Add(id);
                continue;
            }

            if (!receivedIds.Contains(id))
            {
                if (!ctrl.IsDead)
                {
                    ctrl.DieNetworked();
                }
                if (missing == null) missing = new List<int>();
                missing.Add(id);
            }
        }

        if (missing != null)
        {
            foreach (int mId in missing)
            {
                activeUnits.Remove(mId);
            }
        }
    }

    #endregion

    #region Powerup Casting Synchronization

    public void RequestCastPowerup(PowerupType powerupType, Vector3 targetPosition)
    {
        if (!isMultiplayerActive)
        {
            if (PowerupManager.Instance != null)
            {
                PowerupManager.Instance.ExecutePowerup(powerupType, targetPosition, localAssignedTeam);
            }
            return;
        }

        pv.RPC(nameof(RPC_CastPowerup), RpcTarget.All, localAssignedTeam, (int)powerupType, targetPosition);
    }

    [PunRPC]
    private void RPC_CastPowerup(int castingTeam, int powerupTypeInt, Vector3 targetPosition)
    {
        PowerupType type = (PowerupType)powerupTypeInt;
        Debug.Log($"[NetworkBattleBridge] RPC_CastPowerup: Team {castingTeam} invoked {type} at {targetPosition}");

        if (PowerupManager.Instance != null)
        {
            PowerupManager.Instance.ExecutePowerup(type, targetPosition, castingTeam);
        }
    }

    /// <summary>
    /// MasterClient broadcasts a tower arrow launch to all other players in multiplayer.
    /// </summary>
    public void BroadcastTowerFired(int towerIndex, Vector3 targetPos, int targetNetId)
    {
        if (!isMultiplayerActive) return;
        PhotonView pv = photonView != null ? photonView : GetComponent<PhotonView>();
        if (pv == null) return;

        pv.RPC(nameof(RPC_TowerFired), RpcTarget.Others, towerIndex, targetPos, targetNetId);
    }

    [PunRPC]
    private void RPC_TowerFired(int towerIndex, Vector3 targetPos, int targetNetId)
    {
        TowerCombatController.ReceiveNetworkTowerFire(towerIndex, targetPos, targetNetId);
    }

    /// <summary>
    /// MasterClient broadcasts a tower capture event to all other players in multiplayer.
    /// </summary>
    public void BroadcastTowerCaptured(int towerIndex, int newTeam)
    {
        if (!isMultiplayerActive) return;
        PhotonView pv = photonView != null ? photonView : GetComponent<PhotonView>();
        if (pv == null) return;

        pv.RPC(nameof(RPC_TowerCaptured), RpcTarget.Others, towerIndex, newTeam);
    }

    [PunRPC]
    private void RPC_TowerCaptured(int towerIndex, int newTeam)
    {
        TowerCombatController.ReceiveNetworkTowerCaptured(towerIndex, newTeam);
    }

    #endregion

    #region Castle & Objective Health Replication (IPunObservable)

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (!isMultiplayerActive) return;

        FindCastlesIfMissing();
        if (allSceneObjectives == null || allSceneObjectives.Length == 0) return;

        if (stream.IsWriting)
        {
            // MasterClient authoritatively replicates HP of all objectives (Castles & Archery Houses)
            for (int i = 0; i < allSceneObjectives.Length; i++)
            {
                stream.SendNext(allSceneObjectives[i] != null ? allSceneObjectives[i].CurrentHealth : 0f);
            }
        }
        else
        {
            // Guest Client receives HP for all objectives
            for (int i = 0; i < allSceneObjectives.Length; i++)
            {
                float hp = (float)stream.ReceiveNext();
                if (allSceneObjectives[i] != null && Mathf.Abs(allSceneObjectives[i].CurrentHealth - hp) > 0.05f)
                {
                    allSceneObjectives[i].SetHealthNetworked(hp);
                }
            }
        }
    }

    #endregion

    #region Match End & Room Cleanup

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"<color=#00FFAA>[NetworkBattleBridge] Opponent ({newPlayer.NickName}) joined the room!</color>");
        if (MatchReadyScreen.Instance != null)
        {
            MatchReadyScreen.Instance.UpdateHUDPlayerNames();
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.LogWarning($"[NetworkBattleBridge] Opponent ({otherPlayer.NickName}) left the match!");
        if (GameManager.Instance != null && !GameManager.Instance.IsMatchOver)
        {
            GameManager.Instance.EndMatch(GameManager.MatchResult.Victory);
        }
    }

    public void LeaveMatchAndReturnToMenu()
    {
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
        GameModeManager.ResetToDefault();
        SceneManager.LoadScene("MainMenu");
    }

    #endregion
}
