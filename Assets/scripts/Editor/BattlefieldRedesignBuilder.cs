using UnityEngine;
using UnityEditor;
using Unity.AI.Navigation;
using System.Collections.Generic;

public class BattlefieldRedesignBuilder
{
    private const float CenterZ = 25f;

    [MenuItem("Tools/Battlefield/Rebuild Wide Battlefield")]
    public static void RebuildBattlefield()
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Rebuild Wide Battlefield");
        int undoGroup = Undo.GetCurrentGroup();

        Debug.Log("<color=green>[Battlefield Builder] Starting Battlefield Redesign...</color>");

        // 1. Position Castles
        PositionCastles();

        // 2. Position Camera
        SetupCamera();

        // 3. Paint Terrain Dirt Paths
        PaintTerrainPaths();

        // 4. Plant Forest Boundaries & Trees
        PlantTreesAndFoliage();

        // 5. Build Environmental Props & Landmarks (Matching Reference)
        BuildEnvironmentProps();

        // 6. Update Spawn Points
        UpdateSpawnPoints();

        // 7. Deactivate / Clean Up Preset Blocking Fortresses
        HandlePresetPlayers();

        // 8. Build Reference-Matching Deployment Zones
        BuildDeploymentZones();

        // 9. Update Camera Swipe Limits
        UpdateCameraSwipeLimits();

        // 10. Rebake NavMesh
        BakeNavMesh();

        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log("<color=green>[Battlefield Builder] Wide Battlefield Redesign Complete!</color>");
    }

    private static void PositionCastles()
    {
        var cA = GameObject.Find("CastleA");
        if (cA != null)
        {
            Undo.RecordObject(cA.transform, "Position CastleA");
            cA.transform.position = new Vector3(0f, -0.37f, CenterZ);
            cA.transform.rotation = Quaternion.Euler(270f, 0f, 0f);
            cA.transform.localScale = new Vector3(1.64f, 1.64f, 1.64f);
        }

        var cB = GameObject.Find("CastleB");
        if (cB != null)
        {
            Undo.RecordObject(cB.transform, "Position CastleB");
            cB.transform.position = new Vector3(58f, -0.37f, CenterZ);
            cB.transform.rotation = Quaternion.Euler(270f, 180f, 0f);
            cB.transform.localScale = new Vector3(1.6f, 1.6f, 1.6f);
        }
    }

    private static void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;

        Undo.RecordObject(cam.transform, "Setup Camera");
        cam.transform.position = new Vector3(-15f, 57f, CenterZ);
        cam.transform.rotation = Quaternion.Euler(53.5f, 90f, 0f);
        Undo.RecordObject(cam, "Setup Camera FOV");
        cam.fieldOfView = 48.5f;
    }

    private static void PaintTerrainPaths()
    {
        var terrainGo = GameObject.Find("Terrain (1)");
        if (terrainGo == null) return;
        var terrain = terrainGo.GetComponent<Terrain>();
        if (terrain == null || terrain.terrainData == null) return;

        var td = terrain.terrainData;
        int mapW = td.alphamapWidth;
        int mapH = td.alphamapHeight;
        Vector3 tPos = terrain.transform.position;
        Vector3 tSize = td.size;

        float[,,] maps = td.GetAlphamaps(0, 0, mapW, mapH);
        int layerCount = td.terrainLayers.Length;
        if (layerCount < 2) return;

        for (int y = 0; y < mapH; y++)
        {
            float worldZ = tPos.z + ((float)y / mapH) * tSize.z;
            for (int x = 0; x < mapW; x++)
            {
                float worldX = tPos.x + ((float)x / mapW) * tSize.x;

                if (worldX >= -20f && worldX <= 80f && worldZ >= -5f && worldZ <= 55f)
                {
                    float dirtIntensity = CalculateDirtIntensity(worldX, worldZ);

                    maps[y, x, 0] = 1f - dirtIntensity;
                    maps[y, x, 1] = dirtIntensity;
                    for (int l = 2; l < layerCount; l++)
                    {
                        maps[y, x, l] = 0f;
                    }
                }
            }
        }

        td.SetAlphamaps(0, 0, maps);
        Debug.Log("[Battlefield Builder] Terrain alphamaps painted.");
    }

    private static float CalculateDirtIntensity(float wx, float wz)
    {
        float maxIntensity = 0f;

        // 1. Castle A Courtyard (X: 1 to 10.5, Z: 17 to 33)
        if (wx >= 1f && wx <= 10.5f)
        {
            float distFromCenter = Mathf.Abs(wz - CenterZ);
            float lateralFade = Mathf.Clamp01(1f - (distFromCenter - 5.5f) / 2.5f);
            float forwardFade = Mathf.Clamp01(1f - (Mathf.Abs(wx - 5.5f) - 3.5f) / 1.5f);
            maxIntensity = Mathf.Max(maxIntensity, lateralFade * forwardFade);
        }

        // 2. Castle B Courtyard (X: 47.5 to 57, Z: 17 to 33)
        if (wx >= 47.5f && wx <= 57f)
        {
            float distFromCenter = Mathf.Abs(wz - CenterZ);
            float lateralFade = Mathf.Clamp01(1f - (distFromCenter - 5.5f) / 2.5f);
            float forwardFade = Mathf.Clamp01(1f - (Mathf.Abs(wx - 52.5f) - 3.5f) / 1.5f);
            maxIntensity = Mathf.Max(maxIntensity, lateralFade * forwardFade);
        }

        // 3. Central Lane: Wide and dominant (X: 7 to 51, Z: ~25, width ~ 7.5m)
        if (wx >= 7f && wx <= 51f)
        {
            float centerOffset = Mathf.Sin(wx * 0.12f) * 0.35f;
            float pathCenterZ = CenterZ + centerOffset;
            float dist = Mathf.Abs(wz - pathCenterZ);
            float halfWidth = 3.8f;
            float edgeFade = 1.6f;
            float intensity = Mathf.Clamp01(1f - (dist - (halfWidth - edgeFade)) / edgeFade);
            maxIntensity = Mathf.Max(maxIntensity, intensity);
        }

        // 4. Left Lane: Branches out from X=12, curves out to Z ~ 35.5 at X=29, rejoins at X=48
        if (wx >= 11f && wx <= 49f)
        {
            float t = (wx - 29f) / 17f;
            float curveZ = CenterZ + (10.5f * (1f - t * t));

            float distToFenceCenter = Vector2.Distance(new Vector2(wx, wz), new Vector2(29f, 34.2f));
            if (distToFenceCenter > 3.2f)
            {
                float distToPath = Mathf.Abs(wz - curveZ);
                float halfWidth = 2.7f;
                float edgeFade = 1.4f;
                float intensity = Mathf.Clamp01(1f - (distToPath - (halfWidth - edgeFade)) / edgeFade) * 0.95f;
                maxIntensity = Mathf.Max(maxIntensity, intensity);
            }
        }

        // 5. Right Lane: Branches out from X=12, curves out to Z ~ 14.5 at X=28, passes watchtower, rejoins at X=48
        if (wx >= 11f && wx <= 49f)
        {
            float t = (wx - 28.5f) / 16.5f;
            float curveZ = CenterZ - (10.5f * (1f - t * t));

            float distToRuinCenter = Vector2.Distance(new Vector2(wx, wz), new Vector2(28f, 15.8f));
            if (distToRuinCenter > 3.2f)
            {
                float distToPath = Mathf.Abs(wz - curveZ);
                float halfWidth = 2.7f;
                float edgeFade = 1.4f;
                float intensity = Mathf.Clamp01(1f - (distToPath - (halfWidth - edgeFade)) / edgeFade) * 0.95f;
                maxIntensity = Mathf.Max(maxIntensity, intensity);
            }
        }

        // 6. Resource Camp Dirt Clearings
        float dLL = Vector2.Distance(new Vector2(wx, wz), new Vector2(3f, 38f));
        if (dLL < 4.5f) maxIntensity = Mathf.Max(maxIntensity, Mathf.Clamp01(1f - dLL / 4.5f) * 0.85f);

        float dLR = Vector2.Distance(new Vector2(wx, wz), new Vector2(3f, 12f));
        if (dLR < 4.5f) maxIntensity = Mathf.Max(maxIntensity, Mathf.Clamp01(1f - dLR / 4.5f) * 0.85f);

        float dUL = Vector2.Distance(new Vector2(wx, wz), new Vector2(55f, 38f));
        if (dUL < 4.5f) maxIntensity = Mathf.Max(maxIntensity, Mathf.Clamp01(1f - dUL / 4.5f) * 0.85f);

        float dUR = Vector2.Distance(new Vector2(wx, wz), new Vector2(55f, 12f));
        if (dUR < 4.5f) maxIntensity = Mathf.Max(maxIntensity, Mathf.Clamp01(1f - dUR / 4.5f) * 0.85f);

        return Mathf.Clamp01(maxIntensity);
    }

    private static void PlantTreesAndFoliage()
    {
        var terrainGo = GameObject.Find("Terrain (1)");
        if (terrainGo == null) return;
        var terrain = terrainGo.GetComponent<Terrain>();
        var td = terrain.terrainData;

        Vector3 tPos = terrain.transform.position;
        Vector3 tSize = td.size;

        List<TreeInstance> newTrees = new List<TreeInstance>();

        // 1. Left Border Forest: Z from 41 to 65, X from -15 to 75
        for (float x = -15f; x <= 75f; x += 2.6f)
        {
            for (float z = 41.5f; z <= 62f; z += 2.8f)
            {
                float jX = x + Random.Range(-1.1f, 1.1f);
                float jZ = z + Random.Range(-1.1f, 1.1f);

                int proto = Random.Range(0, Mathf.Min(4, td.treePrototypes.Length));
                float height = Random.Range(0.85f, 1.35f);
                float width = Random.Range(0.85f, 1.25f);

                AddTerrainTree(newTrees, jX, jZ, proto, width, height, tPos, tSize);
            }
        }

        // 2. Right Border Forest: Z from -8 to 8.5, X from -15 to 75
        for (float x = -15f; x <= 75f; x += 2.6f)
        {
            for (float z = -8f; z <= 8.5f; z += 2.8f)
            {
                float jX = x + Random.Range(-1.1f, 1.1f);
                float jZ = z + Random.Range(-1.1f, 1.1f);

                int proto = Random.Range(0, Mathf.Min(4, td.treePrototypes.Length));
                float height = Random.Range(0.85f, 1.35f);
                float width = Random.Range(0.85f, 1.25f);

                AddTerrainTree(newTrees, jX, jZ, proto, width, height, tPos, tSize);
            }
        }

        // 3. Top Backdrop Forest behind CastleB (X: 63 to 85, Z: 5 to 45)
        for (float x = 63f; x <= 82f; x += 3.0f)
        {
            for (float z = 9f; z <= 41f; z += 3.0f)
            {
                float jX = x + Random.Range(-1.2f, 1.2f);
                float jZ = z + Random.Range(-1.2f, 1.2f);

                int proto = Random.Range(0, Mathf.Min(4, td.treePrototypes.Length));
                float height = Random.Range(0.9f, 1.4f);
                float width = Random.Range(0.9f, 1.3f);

                AddTerrainTree(newTrees, jX, jZ, proto, width, height, tPos, tSize);
            }
        }

        // 4. Bottom Backdrop Forest behind CastleA (X: -25 to -4, Z: 5 to 45)
        for (float x = -24f; x <= -4f; x += 3.0f)
        {
            for (float z = 9f; z <= 41f; z += 3.0f)
            {
                if (z >= 17f && z <= 33f && x >= -18f) continue;

                float jX = x + Random.Range(-1.2f, 1.2f);
                float jZ = z + Random.Range(-1.2f, 1.2f);

                int proto = Random.Range(0, Mathf.Min(4, td.treePrototypes.Length));
                float height = Random.Range(0.9f, 1.4f);
                float width = Random.Range(0.9f, 1.3f);

                AddTerrainTree(newTrees, jX, jZ, proto, width, height, tPos, tSize);
            }
        }

        // 5. Lone Pine Tree inside the Left Enclosure at (29f, 34.2f)
        AddTerrainTree(newTrees, 29f, 34.2f, 0, 1.05f, 1.15f, tPos, tSize);

        td.treeInstances = newTrees.ToArray();
        Debug.Log($"[Battlefield Builder] Planted {newTrees.Count} terrain trees for outer forests.");
    }

    private static void AddTerrainTree(List<TreeInstance> list, float wx, float wz, int protoIndex, float width, float height, Vector3 tPos, Vector3 tSize)
    {
        float normX = (wx - tPos.x) / tSize.x;
        float normZ = (wz - tPos.z) / tSize.z;

        if (normX < 0f || normX > 1f || normZ < 0f || normZ > 1f) return;

        TreeInstance ti = new TreeInstance
        {
            position = new Vector3(normX, 0f, normZ),
            prototypeIndex = protoIndex,
            widthScale = width,
            heightScale = height,
            color = Color.white,
            lightmapColor = Color.white,
            rotation = Random.Range(0f, 360f) * Mathf.Deg2Rad
        };

        list.Add(ti);
    }

    private static void BuildEnvironmentProps()
    {
        var envGo = GameObject.Find("Environment");
        if (envGo == null) envGo = new GameObject("Environment");

        List<GameObject> toDestroy = new List<GameObject>();
        for (int i = 0; i < envGo.transform.childCount; i++)
        {
            var child = envGo.transform.GetChild(i).gameObject;
            if (child.name.Contains("Terrain")) continue;
            toDestroy.Add(child);
        }
        foreach (var go in toDestroy) Undo.DestroyObjectImmediate(go);

        // Load Prefabs
        GameObject fencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Stylized Medieval Kingdom URP/Prefabs/Fence_1_1 Variant.prefab") 
                              ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Stylized Medieval Kingdom URP/Prefabs/Fence_3_1 Variant.prefab");
        GameObject gatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Stylized Medieval Kingdom URP/Prefabs/Wall/Wall_Gate Variant.prefab");
        GameObject towerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Stylized Medieval Kingdom URP/Prefabs/Outpost/Platform_W_2 Variant.prefab");
        GameObject stairsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Stylized Medieval Kingdom URP/Prefabs/Outpost/Platform_W_Stairs Variant.prefab");
        GameObject rock1 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Stylized Medieval Kingdom URP/Prefabs/Plants/Rock_Formation_1 Variant.prefab");
        GameObject stone1 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Stylized Medieval Kingdom URP/Prefabs/Plants/Stone_1 Variant.prefab");
        GameObject hutPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Stylized Medieval Kingdom URP/Prefabs/Buildings/Building_Addon_1 Variant.prefab")
                            ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Stylized Medieval Kingdom URP/Prefabs/Buildings/Tent_1 Variant.prefab");
        GameObject crateApples = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Stylized Medieval Kingdom URP/Prefabs/Market/Crate_1_Apples Variant.prefab");
        GameObject barrel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Stylized Medieval Kingdom URP/Prefabs/Market/Barrel_1 Variant.prefab");
        GameObject cart = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Stylized Medieval Kingdom URP/Prefabs/Market/Cart_1 Variant.prefab");
        GameObject bannerFbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/models/extras/banners/TT_RTS_Banner_plain.FBX");
        GameObject fireplace = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Stylized Medieval Kingdom URP/Prefabs/Buildings/Fireplace Variant.prefab");
        RuntimeAnimatorController bannerAnim = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/models/extras/banners/animation/TT_RTS_Banner_plain.controller");

        Material blueMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/models/extras/banners/materials/colors/TT_Banner_blue_A.mat");
        Material redMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/models/extras/banners/materials/colors/TT_Banner_red.mat");

        // 1. LEFT ENCLOSURE: Lone Pine Tree with Upright Octagonal Wooden Fence (at 29f, 0f, 34.2f)
        GameObject leftRing = new GameObject("LeftEnclosure_TreeRing");
        leftRing.transform.SetParent(envGo.transform, false);
        leftRing.transform.position = new Vector3(29f, 0f, 34.2f);
        BuildPolygonFence(leftRing.transform, fencePrefab, 3.2f, 8);

        // 2. RIGHT ENCLOSURE: Ancient Ruined Stone Arch with Torches & Fence (at 28f, 0f, 15.8f)
        GameObject rightRing = new GameObject("RightEnclosure_RuinRing");
        rightRing.transform.SetParent(envGo.transform, false);
        rightRing.transform.position = new Vector3(28f, 0f, 15.8f);
        BuildPolygonFence(rightRing.transform, fencePrefab, 3.2f, 8);

        if (gatePrefab != null)
        {
            var arch = (GameObject)PrefabUtility.InstantiatePrefab(gatePrefab, rightRing.transform);
            arch.transform.localPosition = Vector3.zero;
            arch.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            arch.transform.localScale = new Vector3(0.50f, 0.50f, 0.50f);
        }
        if (fireplace != null)
        {
            var f1 = (GameObject)PrefabUtility.InstantiatePrefab(fireplace, rightRing.transform);
            f1.transform.localPosition = new Vector3(1.3f, 0f, -1.3f);
            f1.transform.localScale = Vector3.one * 0.7f;

            var f2 = (GameObject)PrefabUtility.InstantiatePrefab(fireplace, rightRing.transform);
            f2.transform.localPosition = new Vector3(-1.3f, 0f, 1.3f);
            f2.transform.localScale = Vector3.one * 0.7f;
        }

        // 3. WATCHTOWERS ON BOTH SIDES
        if (towerPrefab != null)
        {
            var twR = (GameObject)PrefabUtility.InstantiatePrefab(towerPrefab, envGo.transform);
            twR.name = "Watchtower_Right";
            twR.transform.position = new Vector3(38f, 0f, 10.5f);
            twR.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            twR.transform.localScale = Vector3.one * 1.05f;

            if (stairsPrefab != null)
            {
                var stR = (GameObject)PrefabUtility.InstantiatePrefab(stairsPrefab, twR.transform);
                stR.transform.localPosition = new Vector3(0f, 0f, -2.4f);
                stR.transform.localRotation = Quaternion.identity;
            }

            var twL = (GameObject)PrefabUtility.InstantiatePrefab(towerPrefab, envGo.transform);
            twL.name = "Watchtower_Left";
            twL.transform.position = new Vector3(38f, 0f, 39.5f);
            twL.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            twL.transform.localScale = Vector3.one * 1.05f;

            if (stairsPrefab != null)
            {
                var stL = (GameObject)PrefabUtility.InstantiatePrefab(stairsPrefab, twL.transform);
                stL.transform.localPosition = new Vector3(0f, 0f, 2.4f);
                stL.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
        }

        // 4. RESOURCE CAMPS (4 Corners flanking castles)
        CreateCamp(envGo.transform, "Camp_EnemyLeft", new Vector3(55f, 0f, 38f), hutPrefab, cart, crateApples, barrel, fencePrefab, 135f);
        CreateCamp(envGo.transform, "Camp_EnemyRight", new Vector3(55f, 0f, 12f), hutPrefab, cart, crateApples, barrel, fencePrefab, 45f);
        CreateCamp(envGo.transform, "Camp_PlayerLeft", new Vector3(3f, 0f, 38f), hutPrefab, cart, crateApples, barrel, fencePrefab, 225f);
        CreateCamp(envGo.transform, "Camp_PlayerRight", new Vector3(3f, 0f, 12f), hutPrefab, cart, crateApples, barrel, fencePrefab, 315f);

        // 5. CASTLE FLANKING BANNERS (Positioned in front of castle walls flanking courtyard)
        CreateBannerWithFlag(envGo.transform, "PlayerBanner_Left", new Vector3(8.5f, 0f, 19.5f), bannerFbx, blueMat, bannerAnim, 90f);
        CreateBannerWithFlag(envGo.transform, "PlayerBanner_Right", new Vector3(8.5f, 0f, 30.5f), bannerFbx, blueMat, bannerAnim, 90f);
        CreateBannerWithFlag(envGo.transform, "EnemyBanner_Left", new Vector3(49.5f, 0f, 19.5f), bannerFbx, redMat, bannerAnim, 270f);
        CreateBannerWithFlag(envGo.transform, "EnemyBanner_Right", new Vector3(49.5f, 0f, 30.5f), bannerFbx, redMat, bannerAnim, 270f);

        // 6. SCATTERED NATURAL ROCKS (strictly along outer grass borders)
        if (rock1 != null)
        {
            var rL = (GameObject)PrefabUtility.InstantiatePrefab(rock1, envGo.transform);
            rL.name = "Rock_OuterLeft";
            rL.transform.position = new Vector3(22f, -1.8f, 39.5f);
            rL.transform.localScale = Vector3.one * 0.85f;

            var rR = (GameObject)PrefabUtility.InstantiatePrefab(rock1, envGo.transform);
            rR.name = "Rock_OuterRight";
            rR.transform.position = new Vector3(34f, -1.8f, 10.5f);
            rR.transform.localScale = Vector3.one * 0.85f;
        }

        if (stone1 != null)
        {
            var s1 = (GameObject)PrefabUtility.InstantiatePrefab(stone1, envGo.transform);
            s1.name = "Stone_MidLeftGrass";
            s1.transform.position = new Vector3(20f, -0.2f, 29.2f);
            s1.transform.localScale = Vector3.one * 0.85f;

            var s2 = (GameObject)PrefabUtility.InstantiatePrefab(stone1, envGo.transform);
            s2.name = "Stone_MidRightGrass";
            s2.transform.position = new Vector3(37f, -0.2f, 20.8f);
            s2.transform.localScale = Vector3.one * 0.85f;
        }

        // Clean up legacy temple objects
        var t1 = GameObject.Find("Temple");
        if (t1 != null) t1.SetActive(false);
        var t2 = GameObject.Find("Temple (1)");
        if (t2 != null) t2.SetActive(false);

        Debug.Log("[Battlefield Builder] Environmental props and landmarks built.");
    }

    private static void BuildPolygonFence(Transform parent, GameObject fencePrefab, float radius, int segments)
    {
        if (fencePrefab == null) return;

        float angleStep = 360f / segments;
        Vector3[] verts = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float rad = (i * angleStep) * Mathf.Deg2Rad;
            verts[i] = new Vector3(Mathf.Sin(rad) * radius, 0f, Mathf.Cos(rad) * radius);
        }

        for (int i = 0; i < segments; i++)
        {
            Vector3 A = verts[i];
            Vector3 B = verts[(i + 1) % segments];
            Vector3 dir = B - A;
            float segLen = dir.magnitude;

            var fence = (GameObject)PrefabUtility.InstantiatePrefab(fencePrefab, parent);
            fence.transform.localPosition = A;

            Vector3 lookFwd = Vector3.Cross(Vector3.up, dir.normalized);
            fence.transform.localRotation = Quaternion.LookRotation(lookFwd, Vector3.up);

            float scaleX = segLen / 3.15f;
            fence.transform.localScale = new Vector3(scaleX, 0.75f, 0.75f);
        }
    }

    private static void CreateCamp(Transform parent, string name, Vector3 pos, GameObject hut, GameObject cart, GameObject crate, GameObject barrel, GameObject fence, float yRot)
    {
        GameObject camp = new GameObject(name);
        camp.transform.SetParent(parent, false);
        camp.transform.position = pos;
        camp.transform.rotation = Quaternion.Euler(0f, yRot, 0f);

        if (hut != null)
        {
            var h = (GameObject)PrefabUtility.InstantiatePrefab(hut, camp.transform);
            h.transform.localPosition = new Vector3(0f, 0f, 1.2f);
            h.transform.localScale = Vector3.one * 0.85f;
        }
        if (cart != null)
        {
            var c = (GameObject)PrefabUtility.InstantiatePrefab(cart, camp.transform);
            c.transform.localPosition = new Vector3(1.8f, 0f, -0.6f);
            c.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
            c.transform.localScale = Vector3.one * 0.8f;
        }
        if (crate != null)
        {
            var cr1 = (GameObject)PrefabUtility.InstantiatePrefab(crate, camp.transform);
            cr1.transform.localPosition = new Vector3(-1.4f, 0f, -0.8f);
            cr1.transform.localScale = Vector3.one * 0.9f;

            var cr2 = (GameObject)PrefabUtility.InstantiatePrefab(crate, camp.transform);
            cr2.transform.localPosition = new Vector3(-1.0f, 0f, -1.6f);
            cr2.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
            cr2.transform.localScale = Vector3.one * 0.9f;
        }
        if (barrel != null)
        {
            var b = (GameObject)PrefabUtility.InstantiatePrefab(barrel, camp.transform);
            b.transform.localPosition = new Vector3(-1.9f, 0f, -1.2f);
            b.transform.localScale = Vector3.one * 0.9f;
        }
        if (fence != null)
        {
            var f = (GameObject)PrefabUtility.InstantiatePrefab(fence, camp.transform);
            f.transform.localPosition = new Vector3(0f, 0f, -2.2f);
            f.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            f.transform.localScale = Vector3.one * 0.8f;
        }
    }

    private static void CreateBannerWithFlag(Transform parent, string name, Vector3 pos, GameObject bannerFbx, Material mat, RuntimeAnimatorController anim, float yRot)
    {
        if (bannerFbx == null) return;

        GameObject banner = (GameObject)PrefabUtility.InstantiatePrefab(bannerFbx, parent);
        banner.name = name;
        banner.transform.position = pos;
        banner.transform.rotation = Quaternion.Euler(0f, yRot, 0f);
        banner.transform.localScale = Vector3.one * 1.8f;

        // Keep Pole A and Flag A active, disable duplicates B, C, D
        for (int i = 0; i < banner.transform.childCount; i++)
        {
            var child = banner.transform.GetChild(i);
            if (child.name.EndsWith("_B") || child.name.EndsWith("_C") || child.name.EndsWith("_D"))
            {
                child.gameObject.SetActive(false);
            }
        }

        if (mat != null)
        {
            foreach (var r in banner.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = mat;
            }
        }

        if (anim != null)
        {
            var animator = banner.GetComponent<Animator>();
            if (animator == null) animator = banner.AddComponent<Animator>();
            animator.runtimeAnimatorController = anim;
        }
    }

    private static void UpdateSpawnPoints()
    {
        var spawnRoot = GameObject.Find("SpawnPoint(s)");
        if (spawnRoot == null) return;

        Undo.RecordObject(spawnRoot.transform, "Update SpawnPoint(s) Root");
        spawnRoot.transform.position = Vector3.zero;

        // Player (GreenPlayer / Team 0) Spawns inside Blue Deployment Zone
        var green = GameObject.Find("SpawnPoint(s)/GreenPlayer");
        if (green != null)
        {
            green.transform.position = Vector3.zero;
            Transform sp1 = green.transform.childCount > 0 ? green.transform.GetChild(0) : null;
            Transform sp2 = green.transform.childCount > 1 ? green.transform.GetChild(1) : null;

            if (sp1 != null) sp1.position = new Vector3(8f, 0f, 22f);
            if (sp2 != null) sp2.position = new Vector3(8f, 0f, 28f);
        }

        // Enemy (RedPlayer / Team 1) Spawns inside Red Deployment Zone
        var red = GameObject.Find("SpawnPoint(s)/RedPlayer");
        if (red != null)
        {
            red.transform.position = Vector3.zero;
            Transform sp1 = red.transform.childCount > 0 ? red.transform.GetChild(0) : null;
            Transform sp2 = red.transform.childCount > 1 ? red.transform.GetChild(1) : null;

            if (sp1 != null) sp1.position = new Vector3(50f, 0f, 22f);
            if (sp2 != null) sp2.position = new Vector3(50f, 0f, 28f);
        }

        Debug.Log("[Battlefield Builder] Spawn points updated.");
    }

    private static void HandlePresetPlayers()
    {
        var pp = GameObject.Find("preset players");
        if (pp != null)
        {
            Undo.RecordObject(pp, "Deactivate preset players");
            pp.SetActive(false);
        }
    }

    private static void BuildDeploymentZones()
    {
        var bfGo = GameObject.Find("BattleField");
        if (bfGo == null) return;

        Undo.RecordObject(bfGo.transform, "Update BattleField Transform");
        bfGo.transform.position = new Vector3(29f, 0f, CenterZ);
        bfGo.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

        var setup = bfGo.GetComponent<BattleSceneSetup>();
        if (setup != null)
        {
            Undo.RecordObject(setup, "Update BattleSceneSetup Settings");
            setup.arenaLength = 58f;
            setup.arenaWidth = 36f;
            setup.showZoneFills = false;
            setup.showBorders = false;
            setup.showFrontline = false;
            setup.BuildBattlefield();
        }

        // Clean existing custom deployment zones
        Transform oldBlue = bfGo.transform.Find("DeploymentZone_Blue");
        if (oldBlue != null) Object.DestroyImmediate(oldBlue.gameObject);
        Transform oldRed = bfGo.transform.Find("DeploymentZone_Red");
        if (oldRed != null) Object.DestroyImmediate(oldRed.gameObject);

        // Player Deployment Zone: directly in front of Blue Castle gate (local Z = -20f, X = 0)
        // World: center is at X = 9f, Z = 25f. (width = 14m across Z, depth = 7m along X)
        CreateReferenceDeploymentZone(bfGo.transform, "DeploymentZone_Blue", new Vector3(0f, 0.04f, -20f), 14f, 7.0f, new Color(0.18f, 0.55f, 1f, 0.95f), new Color(0.18f, 0.55f, 1f, 0.18f));

        // Enemy Deployment Zone: directly in front of Red Castle gate (local Z = 20f, X = 0)
        // World: center is at X = 49f, Z = 25f. (width = 14m across Z, depth = 7m along X)
        CreateReferenceDeploymentZone(bfGo.transform, "DeploymentZone_Red", new Vector3(0f, 0.04f, 20f), 14f, 7.0f, new Color(1f, 0.22f, 0.22f, 0.95f), new Color(1f, 0.22f, 0.22f, 0.18f));

        Debug.Log("[Battlefield Builder] Deployment zones built.");
    }

    private static void CreateReferenceDeploymentZone(Transform parent, string name, Vector3 localCenter, float width, float depth, Color borderColor, Color fillColor)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localCenter;
        root.transform.localRotation = Quaternion.identity;

        // Ground Fill using Sprites/Default for reliable alpha transparency
        GameObject fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fill.name = "ZoneFill";
        fill.transform.SetParent(root.transform, false);
        fill.transform.localPosition = Vector3.zero;
        fill.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        fill.transform.localScale = new Vector3(width, depth, 1f);
        Object.DestroyImmediate(fill.GetComponent<Collider>());

        var fillRend = fill.GetComponent<Renderer>();
        fillRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        fillRend.receiveShadows = false;
        fillRend.sharedMaterial = CreateSpriteMat(fillColor, null);

        // Outline Borders (Top, Bottom, Left, Right)
        float t = 0.20f;
        float halfW = width * 0.5f;
        float halfD = depth * 0.5f;
        Material borderMat = CreateSpriteMat(borderColor, null);

        CreateBar(root.transform, "Border_Top", new Vector3(0f, 0.01f, halfD), new Vector3(width + t, t, t), borderMat);
        CreateBar(root.transform, "Border_Bottom", new Vector3(0f, 0.01f, -halfD), new Vector3(width + t, t, t), borderMat);
        CreateBar(root.transform, "Border_Left", new Vector3(-halfW, 0.01f, 0f), new Vector3(t, t, depth), borderMat);
        CreateBar(root.transform, "Border_Right", new Vector3(halfW, 0.01f, 0f), new Vector3(t, t, depth), borderMat);

        // Crossed Swords Quad
        Texture2D swordsTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UIPACK/battlevs/arena_matchmaking_transparent_sprites/Icons/crossed_swords.png");
        if (swordsTex != null)
        {
            GameObject swords = GameObject.CreatePrimitive(PrimitiveType.Quad);
            swords.name = "CrossedSwords";
            swords.transform.SetParent(root.transform, false);
            swords.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            swords.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            swords.transform.localScale = new Vector3(3.2f, 3.2f, 1f);
            Object.DestroyImmediate(swords.GetComponent<Collider>());

            var swordsRend = swords.GetComponent<Renderer>();
            swordsRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            swordsRend.receiveShadows = false;
            swordsRend.sharedMaterial = CreateSpriteMat(new Color(1f, 1f, 1f, 0.90f), swordsTex);
        }
    }

    private static void CreateBar(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat)
    {
        GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = name;
        bar.transform.SetParent(parent, false);
        bar.transform.localPosition = localPos;
        bar.transform.localRotation = Quaternion.identity;
        bar.transform.localScale = localScale;
        Object.DestroyImmediate(bar.GetComponent<Collider>());

        var rend = bar.GetComponent<Renderer>();
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        rend.sharedMaterial = mat;
    }

    private static Material CreateSpriteMat(Color color, Texture2D tex)
    {
        Shader shader = Shader.Find("Sprites/Default");
        Material mat = new Material(shader) { name = "SpriteMat_" + color.ToString() };
        mat.color = color;
        if (tex != null) mat.mainTexture = tex;
        return mat;
    }

    private static void UpdateCameraSwipeLimits()
    {
        var cam = Camera.main;
        if (cam == null) return;

        var swipe = cam.GetComponent<CameraSwipeController>();
        if (swipe != null)
        {
            Undo.RecordObject(swipe, "Update Camera Swipe Limits");
            swipe.MinX = -15f;
            swipe.MaxX = -15f;
        }
    }

    private static void BakeNavMesh()
    {
        var terrainGo = GameObject.Find("Terrain (1)");
        if (terrainGo == null) return;

        var surface = terrainGo.GetComponent<NavMeshSurface>();
        if (surface != null)
        {
            Debug.Log("[Battlefield Builder] Baking NavMeshSurface on Terrain (1)...");
            surface.BuildNavMesh();
            Debug.Log("[Battlefield Builder] NavMesh baked successfully!");
        }
    }
}
