using UnityEngine;
using UnityEditor;
using UnityEngine.AI;
using UnityEditor.SceneManagement;

public static class TowerSceneSetup
{
    [MenuItem("Tools/Battlefield/Setup Defense Towers (All)")]
    public static void SetupTowersInScene()
    {
        Debug.Log("<color=cyan>[TowerSceneSetup] Starting Complete Defense Tower Setup (Preset & Wall Towers)...</color>");

        GameObject presetPlayers = GameObject.Find("preset players");
        if (presetPlayers == null)
        {
            Debug.LogError("[TowerSceneSetup] Could not find 'preset players' in active scene!");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(presetPlayers, "Setup Defense Towers");

        // 1. Clean up obsolete disabled ArcheryOUR and disabled TT_Archers
        var obsoleteList = new System.Collections.Generic.List<GameObject>();
        for (int i = 0; i < presetPlayers.transform.childCount; i++)
        {
            Transform child = presetPlayers.transform.GetChild(i);
            if (child.name.StartsWith("ArcheryOUR") || (child.name == "TT_Archer" && !child.gameObject.activeSelf))
            {
                obsoleteList.Add(child.gameObject);
            }
        }

        foreach (var obj in obsoleteList)
        {
            Undo.DestroyObjectImmediate(obj);
        }

        // 2. Discover the 4 tower GameObjects
        TowerCombatController[] existingControllers = presetPlayers.GetComponentsInChildren<TowerCombatController>(true);
        var ourTowers = new System.Collections.Generic.List<GameObject>();
        var enemyTowers = new System.Collections.Generic.List<GameObject>();

        for (int i = 0; i < presetPlayers.transform.childCount; i++)
        {
            Transform child = presetPlayers.transform.GetChild(i);
            if (child.name.StartsWith("towerour"))
            {
                ourTowers.Add(child.gameObject);
            }
            else if (child.name.StartsWith("towerenemy"))
            {
                enemyTowers.Add(child.gameObject);
            }
        }

        // Sort by X coordinate for consistent ordering
        ourTowers.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        enemyTowers.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

        Debug.Log($"[TowerSceneSetup] Found {ourTowers.Count} our towers and {enemyTowers.Count} enemy towers.");

        // Load required assets
        AudioClip shootSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audioMohamad/arrowrelease.mp3");
        AudioClip hitSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audioMohamad/arrowhit.mp3");
        AudioClip smashSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audioMohamad/dragon-studio-hammer-smash-effect-382731.mp3");

        GameObject hitVfx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR Hit D 3D (Yellow).prefab");
        if (hitVfx == null)
            hitVfx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR Hit A (Red).prefab");

        GameObject explodeVfx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Explosions/CFXR2 WW Explosion.prefab");
        if (explodeVfx == null)
            explodeVfx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Explosions/CFXR Explosion 1.prefab");

        GameObject archerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/prefabs/TT_Archer.prefab");
        GameObject arrowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Powerups/Arrow_B.prefab");

        Material ourMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/materials/TT_RTS_Buildings.mat");
        Material enemyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/materials/TT_RTS_buildings_red.mat");
        Material ourArcherMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/models/materials/color/Units/TT_RTS_Units_blue.mat");
        Material enemyArcherMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/models/materials/color/Units/TT_RTS_Units_red.mat");

        // Setup Our Towers (Team 0, Indices 0 and 1)
        for (int i = 0; i < ourTowers.Count; i++)
        {
            SetupSingleTower(ourTowers[i], index: i, team: 0, displayName: "towerour",
                shootSound, hitSound, smashSound, hitVfx, explodeVfx, archerPrefab, arrowPrefab, ourMat, ourArcherMat, enemyArcherMat);
        }

        // Setup Enemy Towers (Team 1, Indices 2 and 3)
        for (int i = 0; i < enemyTowers.Count; i++)
        {
            SetupSingleTower(enemyTowers[i], index: 2 + i, team: 1, displayName: "towerenemy",
                shootSound, hitSound, smashSound, hitVfx, explodeVfx, archerPrefab, arrowPrefab, enemyMat, ourArcherMat, enemyArcherMat);
        }

        EditorSceneManager.MarkSceneDirty(presetPlayers.scene);
        Debug.Log("<color=green>[TowerSceneSetup] All 4 Preset Defense Towers Successfully Configured!</color>");

        // 3. Setup Wall Towers
        SetupWallTowersInScene();
    }

    [MenuItem("Tools/Battlefield/Test Tower Acquisition (Team 0 - Player)")]
    public static void TestTowerAcquisitionPlayer()
    {
        TowerCombatController[] towers = Object.FindObjectsByType<TowerCombatController>(FindObjectsSortMode.None);
        if (towers.Length > 0)
        {
            towers[0].TriggerAcquisitionFeedback(0);
        }
        else
        {
            Debug.LogWarning("[TowerSceneSetup] No TowerCombatController found in scene to test!");
        }
    }

    [MenuItem("Tools/Battlefield/Test Tower Acquisition (Team 1 - Enemy)")]
    public static void TestTowerAcquisitionEnemy()
    {
        TowerCombatController[] towers = Object.FindObjectsByType<TowerCombatController>(FindObjectsSortMode.None);
        if (towers.Length > 0)
        {
            towers[0].TriggerAcquisitionFeedback(1);
        }
        else
        {
            Debug.LogWarning("[TowerSceneSetup] No TowerCombatController found in scene to test!");
        }
    }

    [MenuItem("Tools/Battlefield/Setup Wall Towers Only")]
    public static void SetupWallTowersInScene()
    {
        Debug.Log("<color=cyan>[TowerSceneSetup] Discovering and Setting Up Battlefield Wall Towers...</color>");

        var allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var wallTowers = new System.Collections.Generic.List<GameObject>();

        foreach (var go in allObjects)
        {
            string lowerName = go.name.ToLower();
            if (lowerName == "walltower" || lowerName == "walltowerour" || lowerName == "walltowerenemy")
            {
                if (!wallTowers.Contains(go))
                {
                    wallTowers.Add(go);
                }
            }
        }

        if (wallTowers.Count < 2)
        {
            Debug.LogError($"[TowerSceneSetup] Expected at least 2 wall towers in scene, but found {wallTowers.Count}!");
            return;
        }

        // Sort by X coordinate: smaller X = Our defensive wall (Team 0), larger X = Enemy defensive wall (Team 1)
        wallTowers.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

        GameObject ourWallTower = wallTowers[0];
        GameObject enemyWallTower = wallTowers[1];

        ourWallTower.name = "walltowerour";
        enemyWallTower.name = "walltowerenemy";

        GameObject presetPlayers = GameObject.Find("preset players");
        if (presetPlayers != null)
        {
            if (ourWallTower.transform.parent != presetPlayers.transform)
                Undo.SetTransformParent(ourWallTower.transform, presetPlayers.transform, "Parent walltowerour");
            if (enemyWallTower.transform.parent != presetPlayers.transform)
                Undo.SetTransformParent(enemyWallTower.transform, presetPlayers.transform, "Parent walltowerenemy");
        }

        Undo.RegisterFullObjectHierarchyUndo(ourWallTower, "Setup Wall Tower Our");
        Undo.RegisterFullObjectHierarchyUndo(enemyWallTower, "Setup Wall Tower Enemy");

        // Load required assets
        AudioClip shootSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audioMohamad/arrowrelease.mp3");
        AudioClip hitSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audioMohamad/arrowhit.mp3");
        AudioClip smashSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audioMohamad/dragon-studio-hammer-smash-effect-382731.mp3");
        AudioClip captureSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audioMohamad/nhlk-revolution-108386.mp3");

        GameObject hitVfx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR Hit D 3D (Yellow).prefab");
        if (hitVfx == null)
            hitVfx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR Hit A (Red).prefab");

        GameObject explodeVfx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Explosions/CFXR2 WW Explosion.prefab");
        if (explodeVfx == null)
            explodeVfx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Explosions/CFXR Explosion 1.prefab");

        GameObject captureVfx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonParticles/Prefabs/FX_LevelUp_01.prefab");
        if (captureVfx == null)
            captureVfx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonParticles/Prefabs/FX_Fireworks_Group_01.prefab");

        GameObject archerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefabs/characters/TT_Archer.prefab");
        if (archerPrefab == null)
            archerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/prefabs/TT_Archer.prefab");
        GameObject arrowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Powerups/Arrow_B.prefab");
        RuntimeAnimatorController archerAnimCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/AnimationController/archer.controller");

        Material ourArcherMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/models/materials/color/Units/TT_RTS_Units_blue.mat");
        Material enemyArcherMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/models/materials/color/Units/TT_RTS_Units_red.mat");

        // Setup Wall Towers (Both start NEUTRAL: Team -1, empty platform, first to reach/occupy captures it!)
        SetupSingleWallTower(ourWallTower, index: 4, team: -1, displayName: "Neutral Wall Tower",
            shootSound, hitSound, smashSound, hitVfx, explodeVfx, captureSound, captureVfx, archerPrefab, arrowPrefab, archerAnimCtrl, ourArcherMat, enemyArcherMat);

        SetupSingleWallTower(enemyWallTower, index: 5, team: -1, displayName: "Neutral Wall Tower",
            shootSound, hitSound, smashSound, hitVfx, explodeVfx, captureSound, captureVfx, archerPrefab, arrowPrefab, archerAnimCtrl, ourArcherMat, enemyArcherMat);

        EditorSceneManager.MarkSceneDirty(ourWallTower.scene);
        Debug.Log("<color=green>[TowerSceneSetup] Both Wall Towers Successfully Configured with Elevated Archer Base, Combat, and Garrisoning!</color>");

        // Setup Acquisition UI in Scene (Toast in Canvas and Banners on Wall Towers)
        TowerAcquisitionSetupTool.SetupAcquisitionUIInScene(silent: false);
    }

    private static void SetupSingleWallTower(
        GameObject towerGo, int index, int team, string displayName,
        AudioClip shootClip, AudioClip hitClip, AudioClip smashClip,
        GameObject hitVfx, GameObject explodeVfx, AudioClip captureSoundClip, GameObject captureVfxPrefab,
        GameObject archerPrefab, GameObject arrowPrefab, RuntimeAnimatorController archerAnimCtrl, Material ourArcherMat, Material enemyArcherMat)
    {
        Material archerMat = (team == 0) ? ourArcherMat : enemyArcherMat;
        Undo.RecordObject(towerGo, "Setup Single Wall Tower");

        towerGo.layer = 0; // Default layer

        // 1. Remove non-convex MeshCollider so it doesn't block internal raycasts or arrows
        MeshCollider mc = towerGo.GetComponent<MeshCollider>();
        if (mc != null)
        {
            Undo.DestroyObjectImmediate(mc);
        }

        // 2. Add/Configure CapsuleCollider (Y-axis aligned)
        CapsuleCollider col = towerGo.GetComponent<CapsuleCollider>();
        if (col == null) col = Undo.AddComponent<CapsuleCollider>(towerGo);
        col.direction = 1; // Y-axis
        col.center = new Vector3(0f, 8.5f, 0f);
        col.radius = 4.0f;
        col.height = 16.5f;

        // 3. NavMeshObstacle with carving
        NavMeshObstacle obs = towerGo.GetComponent<NavMeshObstacle>();
        if (obs == null) obs = Undo.AddComponent<NavMeshObstacle>(towerGo);
        obs.shape = NavMeshObstacleShape.Capsule;
        obs.center = new Vector3(0f, 8.5f, 0f);
        obs.radius = 4.0f;
        obs.height = 16.5f;
        obs.carving = true;

        // 4. PeasantObjectiveTarget
        PeasantObjectiveTarget pot = towerGo.GetComponent<PeasantObjectiveTarget>();
        if (pot == null) pot = Undo.AddComponent<PeasantObjectiveTarget>(towerGo);

        SerializedObject potSo = new SerializedObject(pot);
        potSo.FindProperty("teamId").intValue = team;
        potSo.FindProperty("objectiveName").stringValue = displayName;
        potSo.FindProperty("maxHealth").floatValue = 750f;
        potSo.FindProperty("autoColorByTeam").boolValue = false;
        potSo.FindProperty("targetPoint").objectReferenceValue = towerGo.transform;
        potSo.FindProperty("destroyGameObjectOnDeath").boolValue = false;
        potSo.ApplyModifiedProperties();

        // 5. TowerCombatController
        TowerCombatController tcc = towerGo.GetComponent<TowerCombatController>();
        if (tcc == null) tcc = Undo.AddComponent<TowerCombatController>(towerGo);

        float heightOffset = 7.05f; // Walkway floor platform Y: local Y ~15.7 * 0.449 = 7.05f (world Y ~ 7.65f)

        SerializedObject tccSo = new SerializedObject(tcc);
        tccSo.FindProperty("towerIndex").intValue = index;
        tccSo.FindProperty("teamId").intValue = team;
        tccSo.FindProperty("towerDisplayName").stringValue = displayName;
        tccSo.FindProperty("maxHealth").floatValue = 750f;
        tccSo.FindProperty("attackRange").floatValue = 34f;
        tccSo.FindProperty("damagePerShot").floatValue = 32f;
        tccSo.FindProperty("attackCooldown").floatValue = 1.25f;
        tccSo.FindProperty("bowReleaseDelay").floatValue = 0.22f;
        tccSo.FindProperty("platformHeightOffset").floatValue = heightOffset;

        tccSo.FindProperty("shootSound").objectReferenceValue = shootClip;
        tccSo.FindProperty("hitSound").objectReferenceValue = hitClip;
        tccSo.FindProperty("destructionSound").objectReferenceValue = smashClip;

        tccSo.FindProperty("captureSound").objectReferenceValue = captureSoundClip;
        tccSo.FindProperty("captureVfxPrefab").objectReferenceValue = captureVfxPrefab;

        tccSo.FindProperty("hitVfxPrefab").objectReferenceValue = hitVfx;
        tccSo.FindProperty("destructionVfxPrefab").objectReferenceValue = explodeVfx;
        tccSo.FindProperty("archerPrefab").objectReferenceValue = archerPrefab;
        tccSo.FindProperty("arrowPrefab").objectReferenceValue = arrowPrefab;
        tccSo.FindProperty("team0ArcherMaterial").objectReferenceValue = ourArcherMat;
        tccSo.FindProperty("team1ArcherMaterial").objectReferenceValue = enemyArcherMat;
        tccSo.FindProperty("archerAnimatorController").objectReferenceValue = archerAnimCtrl;
        tccSo.FindProperty("enableArcherAnimations").boolValue = true; // Wall towers have archer animations enabled!
        tccSo.FindProperty("attacksAutonomously").boolValue = false; // Wall towers require an archer to occupy!
        tccSo.FindProperty("allowCaptureOnDestroy").boolValue = true; // Only wall towers can be captured and occupied!
        tccSo.FindProperty("flag").boolValue = false; // Never use destruction prefab for capturable wall towers
        tccSo.FindProperty("AfterObject").objectReferenceValue = null;

        tccSo.ApplyModifiedProperties();

        // 6. Wall towers start NEUTRAL with NO archer atop platform!
        // Whichever team reaches the tower first occupies and captures it!
        Transform archerTr = towerGo.transform.Find("StationedArcher");
        if (archerTr != null)
        {
            Undo.DestroyObjectImmediate(archerTr.gameObject);
            archerTr = null;
        }

        tccSo.Update();
        tccSo.FindProperty("stationedArcherTransform").objectReferenceValue = null;
        tccSo.FindProperty("archerAnimator").objectReferenceValue = null;
        tccSo.ApplyModifiedProperties();

        // 7. World-Space TowerHealthBar
        Transform hbTr = towerGo.transform.Find("TowerHealthBar");
        TowerHealthBar thb = null;
        if (hbTr == null)
        {
            GameObject hbGo = new GameObject("TowerHealthBar");
            hbGo.transform.SetParent(towerGo.transform, false);
            thb = hbGo.AddComponent<TowerHealthBar>();
        }
        else
        {
            thb = hbTr.GetComponent<TowerHealthBar>();
            if (thb == null) thb = hbTr.gameObject.AddComponent<TowerHealthBar>();
        }

        thb.EnsureUIComponents();
        thb.Setup(towerGo.transform, team, 750f);
        thb.SetWorldOffset(new Vector3(0f, 8.5f, 0f));

        tccSo.Update();
        tccSo.FindProperty("healthBar").objectReferenceValue = thb;
        tccSo.ApplyModifiedProperties();

        // 8. TowerGarrisonZone for dynamic occupancy (ONLY wall towers can be occupied!)
        TowerGarrisonZone tgz = towerGo.GetComponent<TowerGarrisonZone>();
        if (tgz == null) tgz = Undo.AddComponent<TowerGarrisonZone>(towerGo);

        SerializedObject tgzSo = new SerializedObject(tgz);
        tgzSo.FindProperty("garrisonRadius").floatValue = 8.0f;
        tgzSo.FindProperty("attractRadius").floatValue = 35.0f;
        tgzSo.FindProperty("autoOccupyNearbyArchers").boolValue = true;
        tgzSo.FindProperty("scanInterval").floatValue = 0.25f;
        tgzSo.ApplyModifiedProperties();

        // 9. Arrow Launch Point
        Transform launchPointTr = towerGo.transform.Find("ArrowLaunchPoint");
        if (launchPointTr == null)
        {
            GameObject lpGo = new GameObject("ArrowLaunchPoint");
            lpGo.transform.SetParent(towerGo.transform, false);
            lpGo.transform.position = new Vector3(towerGo.transform.position.x, towerGo.transform.position.y + heightOffset + 1.2f, towerGo.transform.position.z);
            launchPointTr = lpGo.transform;
            Undo.RegisterCreatedObjectUndo(lpGo, "Create ArrowLaunchPoint");
        }

        if (launchPointTr.GetComponent<ArrowLaunchPointGizmo>() == null)
        {
            Undo.AddComponent<ArrowLaunchPointGizmo>(launchPointTr.gameObject);
        }

        tccSo.Update();
        tccSo.FindProperty("arrowLaunchPoint").objectReferenceValue = launchPointTr;
        tccSo.ApplyModifiedProperties();

        Debug.Log($"[TowerSceneSetup] Wall Tower '{towerGo.name}' (Idx {index}, Team {team}) configured successfully.");
    }

    private static void SetupSingleTower(
        GameObject towerGo, int index, int team, string displayName,
        AudioClip shootClip, AudioClip hitClip, AudioClip smashClip,
        GameObject hitVfx, GameObject explodeVfx,
        GameObject archerPrefab, GameObject arrowPrefab, Material teamMat, Material ourArcherMat, Material enemyArcherMat)
    {
        Undo.RecordObject(towerGo, "Setup Single Tower");

        // Ensure proper layer
        towerGo.layer = 0; // Default layer

        // 1. Material
        Renderer r = towerGo.GetComponent<Renderer>();
        if (r != null && teamMat != null)
        {
            r.sharedMaterial = teamMat;
        }

        // 2. Collider (Capsule aligned with FBX +Z axis = world Y)
        CapsuleCollider col = towerGo.GetComponent<CapsuleCollider>();
        if (col == null) col = Undo.AddComponent<CapsuleCollider>(towerGo);
        col.direction = 2; // Z-axis
        col.radius = 2.0f;
        col.height = 7.5f;
        col.center = new Vector3(0f, 0f, 3.5f);

        // 3. NavMeshObstacle with carving
        NavMeshObstacle obs = towerGo.GetComponent<NavMeshObstacle>();
        if (obs == null) obs = Undo.AddComponent<NavMeshObstacle>(towerGo);
        obs.shape = NavMeshObstacleShape.Capsule;
        obs.radius = 2.0f;
        obs.height = 7.5f;
        obs.center = new Vector3(0f, 0f, 3.5f);
        obs.carving = true;

        // 4. PeasantObjectiveTarget
        PeasantObjectiveTarget pot = towerGo.GetComponent<PeasantObjectiveTarget>();
        if (pot == null) pot = Undo.AddComponent<PeasantObjectiveTarget>(towerGo);

        SerializedObject potSo = new SerializedObject(pot);
        potSo.FindProperty("teamId").intValue = team;
        potSo.FindProperty("objectiveName").stringValue = displayName;
        potSo.FindProperty("maxHealth").floatValue = 650f;
        potSo.FindProperty("autoColorByTeam").boolValue = false;
        potSo.FindProperty("targetPoint").objectReferenceValue = towerGo.transform;
        potSo.FindProperty("destroyGameObjectOnDeath").boolValue = false;
        potSo.ApplyModifiedProperties();

        // 5. TowerCombatController
        TowerCombatController tcc = towerGo.GetComponent<TowerCombatController>();
        if (tcc == null) tcc = Undo.AddComponent<TowerCombatController>(towerGo);

        SerializedObject tccSo = new SerializedObject(tcc);
        tccSo.FindProperty("towerIndex").intValue = index;
        tccSo.FindProperty("teamId").intValue = team;
        tccSo.FindProperty("towerDisplayName").stringValue = displayName;
        tccSo.FindProperty("maxHealth").floatValue = 650f;
        tccSo.FindProperty("attackRange").floatValue = 32f; // Generous range to cover road defense
        tccSo.FindProperty("damagePerShot").floatValue = 28f;
        tccSo.FindProperty("attackCooldown").floatValue = 1.35f;
        tccSo.FindProperty("bowReleaseDelay").floatValue = 0.22f;

        // Platform height offset based on world position
        float standingPlatformWorldY = 10.24f;
        if (towerGo.transform.position.y < 1.8f) standingPlatformWorldY = 10.11f;
        float heightOffset = standingPlatformWorldY - towerGo.transform.position.y;
        tccSo.FindProperty("platformHeightOffset").floatValue = heightOffset;

        tccSo.FindProperty("shootSound").objectReferenceValue = shootClip;
        tccSo.FindProperty("hitSound").objectReferenceValue = hitClip;
        tccSo.FindProperty("destructionSound").objectReferenceValue = smashClip;

        tccSo.FindProperty("hitVfxPrefab").objectReferenceValue = hitVfx;
        tccSo.FindProperty("destructionVfxPrefab").objectReferenceValue = explodeVfx;
        tccSo.FindProperty("archerPrefab").objectReferenceValue = null; // Autonomous base towers don't use an archer prefab!
        tccSo.FindProperty("arrowPrefab").objectReferenceValue = arrowPrefab;
        tccSo.FindProperty("team0ArcherMaterial").objectReferenceValue = ourArcherMat;
        tccSo.FindProperty("team1ArcherMaterial").objectReferenceValue = enemyArcherMat;
        tccSo.FindProperty("attacksAutonomously").boolValue = true; // Preset base defense towers attack by themselves!
        tccSo.FindProperty("enableArcherAnimations").boolValue = false;
        tccSo.FindProperty("archerAnimatorController").objectReferenceValue = null;
        tccSo.FindProperty("allowCaptureOnDestroy").boolValue = false; // Preset base towers are permanent structures - NOT capturable!

        tccSo.ApplyModifiedProperties();

        // 6. Base defense towers attack by themselves - NO StationedArcher! Remove any stationed archer.
        Transform archerTr = towerGo.transform.Find("StationedArcher");
        if (archerTr != null)
        {
            Undo.DestroyObjectImmediate(archerTr.gameObject);
            archerTr = null;
        }

        tccSo.Update();
        tccSo.FindProperty("stationedArcherTransform").objectReferenceValue = null;
        tccSo.FindProperty("archerAnimator").objectReferenceValue = null;
        tccSo.ApplyModifiedProperties();

        // 7. World-Space TowerHealthBar
        Transform hbTr = towerGo.transform.Find("TowerHealthBar");
        if (hbTr == null)
        {
            GameObject hbGo = new GameObject("TowerHealthBar");
            hbGo.transform.SetParent(towerGo.transform, false);
            TowerHealthBar thb = hbGo.AddComponent<TowerHealthBar>();
            thb.EnsureUIComponents();
            thb.Setup(towerGo.transform, team, 650f);

            tccSo.Update();
            tccSo.FindProperty("healthBar").objectReferenceValue = thb;
            tccSo.ApplyModifiedProperties();
        }
        else
        {
            TowerHealthBar thb = hbTr.GetComponent<TowerHealthBar>();
            if (thb == null) thb = hbTr.gameObject.AddComponent<TowerHealthBar>();
            thb.EnsureUIComponents();
            thb.Setup(towerGo.transform, team, 650f);

            tccSo.Update();
            tccSo.FindProperty("healthBar").objectReferenceValue = thb;
            tccSo.ApplyModifiedProperties();
        }

        // 8. Preset base defense towers can NEVER be garrisoned or occupied - only wall towers!
        TowerGarrisonZone tgz = towerGo.GetComponent<TowerGarrisonZone>();
        if (tgz != null)
        {
            Undo.DestroyObjectImmediate(tgz);
        }

        // 9. Arrow Launch Point (Configurable launch point for autonomous base towers)
        Transform launchPointTr = towerGo.transform.Find("ArrowLaunchPoint");
        if (launchPointTr == null)
        {
            GameObject lpGo = new GameObject("ArrowLaunchPoint");
            lpGo.transform.SetParent(towerGo.transform, false);
            lpGo.transform.position = new Vector3(towerGo.transform.position.x, standingPlatformWorldY + 0.15f, towerGo.transform.position.z);
            launchPointTr = lpGo.transform;
            Undo.RegisterCreatedObjectUndo(lpGo, "Create ArrowLaunchPoint");
        }

        if (launchPointTr.GetComponent<ArrowLaunchPointGizmo>() == null)
        {
            Undo.AddComponent<ArrowLaunchPointGizmo>(launchPointTr.gameObject);
        }

        tccSo.Update();
        tccSo.FindProperty("arrowLaunchPoint").objectReferenceValue = launchPointTr;
        tccSo.ApplyModifiedProperties();

        Debug.Log($"[TowerSceneSetup] Tower '{towerGo.name}' (Idx {index}, Team {team}) configured successfully.");
    }
}
