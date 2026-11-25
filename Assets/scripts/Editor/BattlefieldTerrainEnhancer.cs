using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Phase 2 – Terrain Enhancement: rich grass/dirt layers, elevation, props, lighting.
/// Menu: Tools > Battlefield > Enhance Terrain (Phase 2)
/// </summary>
public class BattlefieldTerrainEnhancer : Editor
{
    // ── Terrain world coordinates ──────────────────────────────────────────────
    // Terrain (1): position (-59, 0, -83.1), size (500, 600, 235.2)
    // Playable area: X 0..58, Z ~9..41  (world space)

    static readonly float ARENA_MINX  = 0f;
    static readonly float ARENA_MAXX  = 58f;
    static readonly float ARENA_MINZ  = 9f;
    static readonly float ARENA_MAXZ  = 41f;
    static readonly float ARENA_CX    = 29f;  // centre X
    static readonly float ARENA_CZ    = 25f;  // centre Z

    // ── Texture paths ─────────────────────────────────────────────────────────
    const string HP = "Assets/Handpainted_Grass_and_Ground_Textures/Textures/";
    const string MEK = "Assets/Stylized Medieval Kingdom URP/Textures/";
    const string MPFX = "Assets/Stylized Medieval Kingdom URP/Prefabs/";

    // ── Prefab paths ───────────────────────────────────────────────────────────
    const string PFX_FENCE1   = MPFX + "Fence_1_1 Variant.prefab";
    const string PFX_FENCE2   = MPFX + "Fence_3_1 Variant.prefab";
    const string PFX_FENCE_SP = MPFX + "Fence_Spikes_1 Variant.prefab";
    const string PFX_HAY1     = MPFX + "Hay_1 Variant.prefab";
    const string PFX_HAY2     = MPFX + "Hay_2 Variant.prefab";
    const string PFX_CART     = MPFX + "Cart_1 Variant.prefab";
    const string PFX_CART2    = MPFX + "Cart_2 Variant.prefab";
    const string PFX_BARREL   = MPFX + "Barrel_1 Variant.prefab";
    const string PFX_CRATE    = MPFX + "Crate_1_Apples Variant.prefab";
    const string PFX_TENT     = MPFX + "Tent_1 Variant.prefab";
    const string PFX_FLOWER1  = MPFX + "Flower_1_Multi Variant.prefab";
    const string PFX_FLOWER2  = MPFX + "Flower_2_Multi Variant.prefab";
    const string PFX_MUSH1    = MPFX + "Mushroom_Group_1 Variant.prefab";
    const string PFX_MUSH2    = MPFX + "Mushroom_Group_2 Variant.prefab";
    const string PFX_ROCK1    = MPFX + "Rock_Formation_1 Variant.prefab";
    const string PFX_ROCK2    = MPFX + "Rock_Formation_3 Variant.prefab";
    const string PFX_ROCK3    = MPFX + "Rodck_Formation_2 Variant.prefab";
    const string PFX_STONE1   = MPFX + "Stone_1 Variant.prefab";
    const string PFX_STONE2   = MPFX + "Stone_2 Variant.prefab";
    const string PFX_TRUNK1   = MPFX + "TreeTrunk_1 Variant.prefab";
    const string PFX_TRUNK3   = MPFX + "TreeTrunk_3 Variant.prefab";
    const string PFX_BUSH     = MPFX + "Bush_1 Variant.prefab";
    const string PFX_FIREPL   = MPFX + "Fireplace Variant.prefab";
    const string PFX_STANDAR  = MPFX + "Standar_1 Variant.prefab";
    const string PFX_TOWER1   = MPFX + "Buildings/Tower_1 Variant.prefab";
    const string PFX_TOWER2   = MPFX + "Buildings/Tower_2 Variant.prefab";
    const string PFX_BARN     = MPFX + "Barn_1 Variant.prefab";
    const string PFX_BLDG5    = MPFX + "Buildings/Building_5 Variant.prefab";
    const string PFX_BARRIER1 = MPFX + "barrier_1 Variant.prefab";
    const string PFX_BARRIER2 = MPFX + "barrier_2 Variant.prefab";
    const string PFX_SPIKES1  = MPFX + "Spikes_1 Variant.prefab";
    const string PFX_COLUMN1  = MPFX + "Column_1 Variant.prefab";
    const string PFX_TROUGH   = MPFX + "Trough_Hay Variant.prefab";
    const string PFX_PINETREE1 = MPFX + "PineTree_1 Variant.prefab";
    const string PFX_PINETREE2 = MPFX + "PineTree_2 Variant.prefab";
    const string PFX_PINETREE3 = MPFX + "PineTree_3 Variant.prefab";
    const string PFX_GRASS_V  = MPFX + "Grass Variant.prefab";

    static System.Random rng = new System.Random(42);

    static float Rnd(float min, float max) { return min + (float)(rng.NextDouble() * (max - min)); }
    static float RndSym(float range) { return Rnd(-range, range); }

    // ─────────────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Battlefield/Enhance Terrain (Phase 2)")]
    public static void EnhanceTerrain()
    {
        rng = new System.Random(42);

        Terrain terrain = GetTerrain();
        if (terrain == null) { Debug.LogError("[Enhancer] No terrain found!"); return; }
        TerrainData td = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position; // (-59, 0, -83.1)

        Debug.Log("[Enhancer] ── Step 1: Terrain Layers ──");
        ApplyTerrainLayers(terrain, td, terrainPos);

        Debug.Log("[Enhancer] ── Step 2: Heightmap ──");
        ApplyHeightmap(td, terrainPos);

        Debug.Log("[Enhancer] ── Step 3: Lighting ──");
        ApplyLighting();

        Debug.Log("[Enhancer] ── Step 4: Clear old props ──");
        ClearOldProps();

        Debug.Log("[Enhancer] ── Step 5: Scatter props ──");
        ScatterProps(terrain);

        Debug.Log("[Enhancer] ── Step 6: Performance settings ──");
        ApplyTerrainPerformance(terrain);

        Debug.Log("[Enhancer] ── Saving scene ──");
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

        Debug.Log("[Enhancer] ✓ Phase 2 complete! Open Scene view to inspect.");
    }

    // =========================================================================
    // STEP 1 – TERRAIN LAYERS
    // =========================================================================
    static void ApplyTerrainLayers(Terrain terrain, TerrainData td, Vector3 terrainPos)
    {
        // --- Layer definitions ---
        // Idx 0: Bright green grass (primary)
        // Idx 1: Warm dirt path
        // Idx 2: Dark forest grass (edges)
        // Idx 3: Dry/sandy dirt (secondary path blend)
        // Idx 4: Lighted dirt (campfire/clearing areas)
        // Idx 5: Dark grass (forest understory)

        TerrainLayer[] layers = new TerrainLayer[6];

        // 0 – Bright grass (existing or new)
        layers[0] = CreateOrLoadLayer(
            "Assets/TerrainLayers/TL_GrassLighted.terrainlayer",
            HP + "Grass/Grass_lighted/Grass_lighted_up.png",
            MEK + "Grass Normal.png",
            new Vector2(6f, 6f));

        // 1 – Warm dirt path
        layers[1] = CreateOrLoadLayer(
            "Assets/TerrainLayers/TL_DirtLighted.terrainlayer",
            HP + "Dirt/dirt_lighted/dirt_lighted_up.png",
            MEK + "Ground Normal.png",
            new Vector2(5f, 5f));

        // 2 – Dark forest grass
        layers[2] = CreateOrLoadLayer(
            "Assets/TerrainLayers/TL_GrassDark.terrainlayer",
            HP + "Grass/Grass_darked/Grass_darked_up.png",
            MEK + "Grass Normal.png",
            new Vector2(7f, 7f));

        // 3 – Sandy/desatured dirt
        layers[3] = CreateOrLoadLayer(
            "Assets/TerrainLayers/TL_DirtDesatured.terrainlayer",
            HP + "Dirt/dirt_desatured/dirt_desatured_up.png",
            MEK + "Ground Normal.png",
            new Vector2(4f, 4f));

        // 4 – Dirt with rocks (around stone areas)
        layers[4] = CreateOrLoadLayer(
            "Assets/TerrainLayers/TL_DirtRocks.terrainlayer",
            HP + "Dirt/dirt_lighted_rocks/dirt_lighted_rocks_up.png",
            MEK + "Ground Normal.png",
            new Vector2(4f, 4f));

        // 5 – Swamp/dark grass (deep forest)
        layers[5] = CreateOrLoadLayer(
            "Assets/TerrainLayers/TL_GrassSwampDark.terrainlayer",
            HP + "Grass/Grass_swamp_dark/Grass_swamp_dark_up.png",
            MEK + "Grass Normal.png",
            new Vector2(8f, 8f));

        // Remove null layers
        List<TerrainLayer> validLayers = new List<TerrainLayer>();
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] != null) validLayers.Add(layers[i]);
            else Debug.LogWarning("[Enhancer] Layer " + i + " is null – skipping.");
        }

        td.terrainLayers = validLayers.ToArray();
        int numLayers = validLayers.Count;
        Debug.Log("[Enhancer] Applied " + numLayers + " terrain layers.");

        // --- Paint alphamaps ---
        int mapW = td.alphamapWidth;   // 512
        int mapH = td.alphamapHeight;  // 512
        float[,,] maps = new float[mapW, mapH, numLayers];

        // Terrain world bounds
        float tW = td.size.x; // 500
        float tH = td.size.z; // 235.2

        // Playable area in terrain UV space
        float playMinU = (ARENA_MINX - terrainPos.x) / tW; // (0 - (-59)) / 500 = 0.118
        float playMaxU = (ARENA_MAXX - terrainPos.x) / tW; // (58 - (-59)) / 500 = 0.234
        float playMinV = (ARENA_MINZ - terrainPos.z) / tH; // (9 - (-83.1)) / 235.2 ≈ 0.391
        float playMaxV = (ARENA_MAXZ - terrainPos.z) / tH; // (41 - (-83.1)) / 235.2 ≈ 0.528

        float playCU = (ARENA_CX - terrainPos.x) / tW;
        float playCZ = (ARENA_CZ - terrainPos.z) / tH;

        for (int y = 0; y < mapH; y++)
        {
            for (int x = 0; x < mapW; x++)
            {
                float u = (float)x / mapW; // terrain X → alphamap x
                float v = (float)y / mapH; // terrain Z → alphamap y

                // Normalize to playable area [0..1]
                float pu = Mathf.InverseLerp(playMinU, playMaxU, u);
                float pv = Mathf.InverseLerp(playMinV, playMaxV, v);

                // Distance from centre of playable field
                float dx = (u - playCU) / (playMaxU - playMinU);
                float dz = (v - playCZ) / (playMaxV - playMinV);
                float dist = Mathf.Sqrt(dx * dx + dz * dz);

                bool inPlayArea = (pu >= 0f && pu <= 1f && pv >= 0f && pv <= 1f);

                // Weights
                float w0 = 0f; // bright grass
                float w1 = 0f; // warm dirt path
                float w2 = 0f; // dark forest grass
                float w3 = 0f; // sandy dirt
                float w4 = 0f; // rocky dirt
                float w5 = 0f; // swamp dark

                if (inPlayArea)
                {
                    // --- Winding dirt path (centre lane) ---
                    // Create a gentle S-curve using sine wave along the Z axis
                    float sine = Mathf.Sin(pv * Mathf.PI * 1.8f) * 0.12f;
                    float pathCentreU = 0.5f + sine;
                    float pathDistU   = Mathf.Abs(pu - pathCentreU);
                    float pathWidth   = 0.14f; // ~8 world units wide
                    float pathBlend   = Mathf.SmoothStep(pathWidth, pathWidth * 0.4f, pathDistU);

                    // Left side path
                    float leftPathCU = 0.18f + Mathf.Sin(pv * Mathf.PI * 1.4f + 1f) * 0.06f;
                    float leftBlend  = Mathf.SmoothStep(0.07f, 0.02f, Mathf.Abs(pu - leftPathCU)) * 0.7f;

                    // Right side path
                    float rightPathCU = 0.82f + Mathf.Sin(pv * Mathf.PI * 1.4f - 1f) * 0.06f;
                    float rightBlend  = Mathf.SmoothStep(0.07f, 0.02f, Mathf.Abs(pu - rightPathCU)) * 0.7f;

                    float totalPath = Mathf.Clamp01(pathBlend + leftBlend + rightBlend);

                    // Forest edges (left & right outer 15%)
                    float edgeDist = Mathf.Min(pu, 1f - pu); // 0 at edges, 0.5 centre
                    float forestBlend = Mathf.SmoothStep(0.22f, 0.08f, edgeDist);

                    // Deep forest (outer 8%)
                    float deepForest = Mathf.SmoothStep(0.12f, 0.03f, edgeDist);

                    // Rocky patches – near castle zones and mid-corners
                    float rockyBlend = 0f;
                    // 4 rocky corner clusters
                    float[] rockU = { 0.15f, 0.85f, 0.15f, 0.85f };
                    float[] rockV = { 0.2f, 0.2f, 0.8f, 0.8f };
                    for (int r = 0; r < 4; r++)
                    {
                        float rd = Mathf.Sqrt((pu - rockU[r]) * (pu - rockU[r]) + (pv - rockV[r]) * (pv - rockV[r]));
                        rockyBlend = Mathf.Max(rockyBlend, Mathf.SmoothStep(0.12f, 0.03f, rd));
                    }

                    // Base: bright grass everywhere
                    w0 = 1f;

                    // Overlay dirt on paths
                    // Main path: blend between sandy and lighted dirt
                    float mainPathT = pathBlend;
                    w1 = mainPathT * 0.75f;
                    w3 = mainPathT * 0.25f;

                    // Side paths: lighter dirt
                    w3 += (leftBlend + rightBlend) * 0.6f;
                    w1 += (leftBlend + rightBlend) * 0.3f;

                    // Dark grass on forest edges
                    w2 = forestBlend * (1f - totalPath);

                    // Deep forest floor
                    w5 = deepForest * 0.5f;

                    // Rocky soil patches
                    w4 = rockyBlend * (1f - totalPath) * 0.6f;

                    // Pull bright grass down where other layers are strong
                    w0 = Mathf.Max(0f, 1f - w1 - w2 - w3 - w4 - w5);
                }
                else
                {
                    // Outside playable area – deep forest floor
                    w2 = 0.4f;
                    w5 = 0.4f;
                    w0 = 0.2f;
                }

                // Write (clamp to available layers)
                float total = w0 + w1 + w2 + w3 + w4 + w5;
                if (total < 0.001f) { w0 = 1f; total = 1f; }
                if (numLayers > 0) maps[y, x, 0] = w0 / total;
                if (numLayers > 1) maps[y, x, 1] = w1 / total;
                if (numLayers > 2) maps[y, x, 2] = w2 / total;
                if (numLayers > 3) maps[y, x, 3] = w3 / total;
                if (numLayers > 4) maps[y, x, 4] = w4 / total;
                if (numLayers > 5) maps[y, x, 5] = w5 / total;
            }
        }

        td.SetAlphamaps(0, 0, maps);
        Debug.Log("[Enhancer] Alphamaps painted.");
    }

    // =========================================================================
    // STEP 2 – HEIGHTMAP: gentle undulations, raised forest edges
    // =========================================================================
    static void ApplyHeightmap(TerrainData td, Vector3 terrainPos)
    {
        int hW = td.heightmapResolution;
        int hH = td.heightmapResolution;
        float[,] heights = td.GetHeights(0, 0, hW, hH);

        float tW = td.size.x;
        float tH_z = td.size.z;
        float maxH = td.size.y; // 600

        float playMinU = (ARENA_MINX - terrainPos.x) / tW;
        float playMaxU = (ARENA_MAXX - terrainPos.x) / tW;
        float playMinV = (ARENA_MINZ - terrainPos.z) / tH_z;
        float playMaxV = (ARENA_MAXZ - terrainPos.z) / tH_z;

        for (int y = 0; y < hH; y++)
        {
            for (int x = 0; x < hW; x++)
            {
                float u = (float)x / (hW - 1);
                float v = (float)y / (hH - 1);

                float pu = Mathf.InverseLerp(playMinU, playMaxU, u);
                float pv = Mathf.InverseLerp(playMinV, playMaxV, v);
                bool inPlay = (pu >= 0f && pu <= 1f && pv >= 0f && pv <= 1f);

                float baseH = 0f;

                if (inPlay)
                {
                    // Gentle undulations across the field
                    float undulation = Mathf.Sin(pu * Mathf.PI * 3.5f) * Mathf.Cos(pv * Mathf.PI * 2.3f) * 0.5f
                                     + Mathf.Sin(pu * Mathf.PI * 7f + 1.2f) * Mathf.Sin(pv * Mathf.PI * 5f) * 0.2f;
                    undulation *= 0.6f; // world height: ~0.3-0.5m

                    // Raised forest edges (outer 20% left/right)
                    float edgeDist = Mathf.Min(pu, 1f - pu);
                    float forestRaise = Mathf.SmoothStep(0.2f, 0.0f, edgeDist) * 1.5f; // up to ~1.5m

                    // Small raised ground near castle deployment zones
                    float castleRaise = 0f;
                    if (pv < 0.15f || pv > 0.85f)
                        castleRaise = Mathf.SmoothStep(0.15f, 0f, Mathf.Min(pv, 1f - pv)) * 0.8f;

                    // Centre path depression (slight dip for paths)
                    float pathSine = Mathf.Sin(pv * Mathf.PI * 1.8f) * 0.12f;
                    float pathDistU = Mathf.Abs(pu - (0.5f + pathSine));
                    float pathDip = -Mathf.SmoothStep(0.12f, 0.02f, pathDistU) * 0.4f;

                    float elevation = (undulation + forestRaise + castleRaise + pathDip) / maxH;
                    baseH = Mathf.Max(0f, elevation);
                }
                else
                {
                    // Outside: gently raised terrain
                    float edge = Mathf.Min(Mathf.Abs(u - playMinU), Mathf.Abs(u - playMaxU),
                                          Mathf.Abs(v - playMinV), Mathf.Abs(v - playMaxV));
                    float raise = Mathf.SmoothStep(0f, 0.05f, Mathf.Min(edge, 0.05f)) * 2.5f;
                    baseH = raise / maxH;
                }

                heights[y, x] = Mathf.Clamp(baseH, 0f, 1f);
            }
        }

        td.SetHeights(0, 0, heights);
        Debug.Log("[Enhancer] Heightmap applied.");
    }

    // =========================================================================
    // STEP 3 – LIGHTING
    // =========================================================================
    static void ApplyLighting()
    {
        // Directional light – warm golden medieval sunlight
        Light[] lights = GameObject.FindObjectsOfType<Light>();
        foreach (Light lt in lights)
        {
            if (lt.type == LightType.Directional)
            {
                lt.color = new Color(1f, 0.93f, 0.78f);    // warm golden
                lt.intensity = 1.6f;
                lt.shadowStrength = 0.6f;
                lt.shadows = LightShadows.Soft;
                // Rotate for nice angled shadows
                lt.transform.rotation = Quaternion.Euler(52f, -30f, 0f);
                Debug.Log("[Enhancer] Directional light updated.");
                break;
            }
        }

        // Ambient – soft green-sky
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor     = new Color(0.55f, 0.72f, 0.90f); // sky blue
        RenderSettings.ambientEquatorColor = new Color(0.40f, 0.60f, 0.35f); // green horizon
        RenderSettings.ambientGroundColor  = new Color(0.20f, 0.25f, 0.15f); // dark soil
        RenderSettings.fogColor            = new Color(0.72f, 0.84f, 0.68f);
        RenderSettings.fog                 = false; // keep clear for RTS readability

        Debug.Log("[Enhancer] Lighting/ambient updated.");
    }

    // =========================================================================
    // STEP 4 – CLEAR OLD ENVIRONMENT CONTAINER
    // =========================================================================
    static void ClearOldProps()
    {
        string[] containers = new string[]
        {
            "Environment_Props", "BF_Props", "BattlefieldProps",
            "Env_Container", "PropContainer", "PhaseOneProps"
        };
        foreach (string cname in containers)
        {
            GameObject go = GameObject.Find(cname);
            if (go != null)
            {
                UnityEngine.Object.DestroyImmediate(go);
                Debug.Log("[Enhancer] Cleared: " + cname);
            }
        }
    }

    // =========================================================================
    // STEP 5 – SCATTER PROPS
    // =========================================================================
    static void ScatterProps(Terrain terrain)
    {
        GameObject root = new GameObject("BF_Environment");
        Undo.RegisterCreatedObjectUndo(root, "Create BF_Environment");

        // ── Fences along paths ────────────────────────────────────────────────
        PlaceFenceRun(root, terrain,
            new Vector3(ARENA_MINX + 3f, 0, ARENA_MINZ + 2f),
            new Vector3(ARENA_MINX + 3f, 0, ARENA_MAXZ - 2f),
            PFX_FENCE1, 2.2f, "FenceLeft");

        PlaceFenceRun(root, terrain,
            new Vector3(ARENA_MAXX - 3f, 0, ARENA_MINZ + 2f),
            new Vector3(ARENA_MAXX - 3f, 0, ARENA_MAXZ - 2f),
            PFX_FENCE2, 2.2f, "FenceRight");

        // Mid fences framing the centre path
        PlaceFenceRun(root, terrain,
            new Vector3(ARENA_CX - 6f, 0, ARENA_MINZ + 4f),
            new Vector3(ARENA_CX - 6f, 0, ARENA_MINZ + 10f),
            PFX_FENCE1, 2.2f, "FenceMidL");
        PlaceFenceRun(root, terrain,
            new Vector3(ARENA_CX + 6f, 0, ARENA_MINZ + 4f),
            new Vector3(ARENA_CX + 6f, 0, ARENA_MINZ + 10f),
            PFX_FENCE2, 2.2f, "FenceMidR");

        // ── Haystacks (player left camp) ─────────────────────────────────────
        SpawnPropCluster(root, terrain, PFX_HAY1,
            new Vector3(ARENA_MINX + 5f, 0, ARENA_MINZ + 5f), 4, 2.5f, 0.8f, 1.1f, "Hay");
        SpawnPropCluster(root, terrain, PFX_HAY2,
            new Vector3(ARENA_MINX + 5f, 0, ARENA_MAXZ - 5f), 3, 2f, 0.9f, 1.2f, "Hay");

        // Haystacks enemy side
        SpawnPropCluster(root, terrain, PFX_HAY1,
            new Vector3(ARENA_MAXX - 5f, 0, ARENA_MINZ + 5f), 3, 2f, 0.8f, 1.1f, "Hay");
        SpawnPropCluster(root, terrain, PFX_TROUGH,
            new Vector3(ARENA_MAXX - 5f, 0, ARENA_MAXZ - 5f), 2, 2f, 0.9f, 1.0f, "Trough");

        // ── Carts scattered across lanes ──────────────────────────────────────
        SpawnRandomProps(root, terrain, new string[] { PFX_CART, PFX_CART2 },
            new Rect(ARENA_MINX + 2f, ARENA_MINZ + 3f, 10f, 25f), 5, 0.9f, 1.2f, "CartLeft");
        SpawnRandomProps(root, terrain, new string[] { PFX_CART, PFX_CART2 },
            new Rect(ARENA_MAXX - 12f, ARENA_MINZ + 3f, 10f, 25f), 5, 0.9f, 1.2f, "CartRight");

        // ── Barrels & crates near paths ───────────────────────────────────────
        SpawnRandomProps(root, terrain, new string[] { PFX_BARREL, PFX_CRATE },
            new Rect(ARENA_CX - 10f, ARENA_MINZ + 2f, 20f, 6f), 8, 0.7f, 0.95f, "BarrelBot");
        SpawnRandomProps(root, terrain, new string[] { PFX_BARREL, PFX_CRATE },
            new Rect(ARENA_CX - 10f, ARENA_MAXZ - 8f, 20f, 6f), 8, 0.7f, 0.95f, "BarrelTop");

        // ── Tents (small camps) ───────────────────────────────────────────────
        SpawnPropCluster(root, terrain, PFX_TENT,
            new Vector3(ARENA_MINX + 7f, 0, ARENA_CZ - 2f), 2, 3f, 0.9f, 1.1f, "TentL");
        SpawnPropCluster(root, terrain, PFX_TENT,
            new Vector3(ARENA_MAXX - 7f, 0, ARENA_CZ + 2f), 2, 3f, 0.9f, 1.1f, "TentR");

        // ── Rock formations ───────────────────────────────────────────────────
        SpawnPropCluster(root, terrain, PFX_ROCK1,
            new Vector3(ARENA_MINX + 4f, 0, ARENA_CZ - 6f), 2, 2.5f, 0.8f, 1.3f, "RockFormL");
        SpawnPropCluster(root, terrain, PFX_ROCK2,
            new Vector3(ARENA_MAXX - 4f, 0, ARENA_CZ + 6f), 2, 2.5f, 0.8f, 1.3f, "RockFormR");
        SpawnPropCluster(root, terrain, PFX_ROCK3,
            new Vector3(ARENA_MINX + 4f, 0, ARENA_MAXZ - 6f), 2, 2.5f, 0.8f, 1.3f, "RockFormLT");
        SpawnPropCluster(root, terrain, PFX_ROCK3,
            new Vector3(ARENA_MAXX - 4f, 0, ARENA_MINZ + 6f), 2, 2.5f, 0.8f, 1.3f, "RockFormRB");

        // Scattered smaller stones across mid-field
        SpawnRandomProps(root, terrain, new string[] { PFX_STONE1, PFX_STONE2 },
            new Rect(ARENA_MINX + 6f, ARENA_MINZ + 6f, ARENA_MAXX - ARENA_MINX - 12f, ARENA_MAXZ - ARENA_MINZ - 12f),
            16, 0.5f, 1.4f, "Stone");

        // ── Tree stumps & logs ────────────────────────────────────────────────
        SpawnRandomProps(root, terrain, new string[] { PFX_TRUNK1, PFX_TRUNK3 },
            new Rect(ARENA_MINX + 2f, ARENA_MINZ + 2f, 8f, ARENA_MAXZ - ARENA_MINZ - 4f),
            6, 0.7f, 1.1f, "TrunkL");
        SpawnRandomProps(root, terrain, new string[] { PFX_TRUNK1, PFX_TRUNK3 },
            new Rect(ARENA_MAXX - 10f, ARENA_MINZ + 2f, 8f, ARENA_MAXZ - ARENA_MINZ - 4f),
            6, 0.7f, 1.1f, "TrunkR");

        // ── Flowers scattered across the green ───────────────────────────────
        SpawnRandomProps(root, terrain, new string[] { PFX_FLOWER1, PFX_FLOWER2 },
            new Rect(ARENA_MINX + 5f, ARENA_MINZ + 3f, ARENA_MAXX - ARENA_MINX - 10f, ARENA_MAXZ - ARENA_MINZ - 6f),
            22, 0.6f, 1.2f, "Flower");

        // ── Mushroom groups (forest edge) ─────────────────────────────────────
        SpawnRandomProps(root, terrain, new string[] { PFX_MUSH1, PFX_MUSH2 },
            new Rect(ARENA_MINX + 1f, ARENA_MINZ + 2f, 5f, ARENA_MAXZ - ARENA_MINZ - 4f),
            8, 0.6f, 1.0f, "MushL");
        SpawnRandomProps(root, terrain, new string[] { PFX_MUSH1, PFX_MUSH2 },
            new Rect(ARENA_MAXX - 6f, ARENA_MINZ + 2f, 5f, ARENA_MAXZ - ARENA_MINZ - 4f),
            8, 0.6f, 1.0f, "MushR");

        // ── Bushes along forest edges ─────────────────────────────────────────
        SpawnRandomProps(root, terrain, new string[] { PFX_BUSH },
            new Rect(ARENA_MINX + 1f, ARENA_MINZ + 1f, 7f, ARENA_MAXZ - ARENA_MINZ - 2f),
            12, 0.8f, 1.3f, "BushL");
        SpawnRandomProps(root, terrain, new string[] { PFX_BUSH },
            new Rect(ARENA_MAXX - 8f, ARENA_MINZ + 1f, 7f, ARENA_MAXZ - ARENA_MINZ - 2f),
            12, 0.8f, 1.3f, "BushR");

        // ── Campfires at resource camps ───────────────────────────────────────
        SpawnPropAt(root, terrain, PFX_FIREPL, new Vector3(ARENA_MINX + 5f, 0, ARENA_CZ), 0.8f, "FireL");
        SpawnPropAt(root, terrain, PFX_FIREPL, new Vector3(ARENA_MAXX - 5f, 0, ARENA_CZ), 0.8f, "FireR");
        SpawnPropAt(root, terrain, PFX_FIREPL, new Vector3(ARENA_CX, 0, ARENA_CZ), 0.7f, "FireC");

        // ── Watchtowers (corner placements) ──────────────────────────────────
        SpawnPropAt(root, terrain, PFX_TOWER1, new Vector3(ARENA_MINX + 3f, 0, ARENA_MINZ + 3f), 1.0f, "WatchTL");
        SpawnPropAt(root, terrain, PFX_TOWER2, new Vector3(ARENA_MAXX - 3f, 0, ARENA_MAXZ - 3f), 1.0f, "WatchTR");
        SpawnPropAt(root, terrain, PFX_TOWER1, new Vector3(ARENA_MINX + 3f, 0, ARENA_MAXZ - 3f), 1.0f, "WatchBL");
        SpawnPropAt(root, terrain, PFX_TOWER2, new Vector3(ARENA_MAXX - 3f, 0, ARENA_MINZ + 3f), 1.0f, "WatchBR");

        // ── Barn / Building (side camps) ─────────────────────────────────────
        SpawnPropAt(root, terrain, PFX_BARN,
            new Vector3(ARENA_MINX + 5.5f, 0, ARENA_MINZ + 7f), 1.0f, "BarnL");
        SpawnPropAt(root, terrain, PFX_BLDG5,
            new Vector3(ARENA_MAXX - 5.5f, 0, ARENA_MAXZ - 7f), 1.0f, "BldgR");

        // ── Barrier / barricade lines ─────────────────────────────────────────
        PlaceFenceRun(root, terrain,
            new Vector3(ARENA_CX - 8f, 0, ARENA_CZ - 1f),
            new Vector3(ARENA_CX - 5f, 0, ARENA_CZ - 1f),
            PFX_BARRIER1, 1.5f, "BarrierL");
        PlaceFenceRun(root, terrain,
            new Vector3(ARENA_CX + 5f, 0, ARENA_CZ + 1f),
            new Vector3(ARENA_CX + 8f, 0, ARENA_CZ + 1f),
            PFX_BARRIER2, 1.5f, "BarrierR");

        // ── Extra pine trees as scene dressing (inside playable border) ───────
        ScatterBorderTrees(root, terrain);

        // ── Road signs ────────────────────────────────────────────────────────
        SpawnPropAt(root, terrain,
            MPFX + "RoadSign_1 Variant.prefab",
            new Vector3(ARENA_CX - 1f, 0, ARENA_CZ - 7f), 0.9f, "SignBot");
        SpawnPropAt(root, terrain,
            MPFX + "RoadSign_2 Variant.prefab",
            new Vector3(ARENA_CX + 1f, 0, ARENA_CZ + 7f), 0.9f, "SignTop");

        // ── Windmill (landmark mid-right) ─────────────────────────────────────
        SpawnPropAt(root, terrain,
            MPFX + "Buildings/Windmill Variant.prefab",
            new Vector3(ARENA_MAXX - 5f, 0, ARENA_CZ), 1.0f, "Windmill");

        GameObjectUtility.SetStaticEditorFlags(root,
            StaticEditorFlags.ContributeGI |
            StaticEditorFlags.OccludeeStatic |
            StaticEditorFlags.BatchingStatic);

        Debug.Log("[Enhancer] Props placed under BF_Environment.");
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    static void ScatterBorderTrees(GameObject root, Terrain terrain)
    {
        string[] treePfx = new string[] { PFX_PINETREE1, PFX_PINETREE2, PFX_PINETREE3 };
        int count = 0;

        // Left forest strip
        for (int i = 0; i < 20; i++)
        {
            float wx = Rnd(ARENA_MINX - 2f, ARENA_MINX + 3.5f);
            float wz = Rnd(ARENA_MINZ, ARENA_MAXZ);
            string pfx = treePfx[rng.Next(treePfx.Length)];
            SpawnPropAt(root, terrain, pfx,
                new Vector3(wx, 0, wz), Rnd(0.8f, 1.3f), "TreeBorderL_" + i);
            count++;
        }
        // Right forest strip
        for (int i = 0; i < 20; i++)
        {
            float wx = Rnd(ARENA_MAXX - 3.5f, ARENA_MAXX + 2f);
            float wz = Rnd(ARENA_MINZ, ARENA_MAXZ);
            string pfx = treePfx[rng.Next(treePfx.Length)];
            SpawnPropAt(root, terrain, pfx,
                new Vector3(wx, 0, wz), Rnd(0.8f, 1.3f), "TreeBorderR_" + i);
            count++;
        }
        // Extra mid-trees around castle zones
        string[] castleTreePos = new string[] { "LB", "RB", "LT", "RT" };
        float[] cxArr = { ARENA_MINX + 5f, ARENA_MAXX - 5f, ARENA_MINX + 5f, ARENA_MAXX - 5f };
        float[] czArr = { ARENA_MINZ + 2f, ARENA_MINZ + 2f, ARENA_MAXZ - 2f, ARENA_MAXZ - 2f };
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                string pfx = treePfx[rng.Next(treePfx.Length)];
                SpawnPropAt(root, terrain, pfx,
                    new Vector3(cxArr[i] + RndSym(2f), 0, czArr[i] + RndSym(2f)),
                    Rnd(0.7f, 1.1f), "CastleTree_" + castleTreePos[i] + "_" + j);
            }
        }
        Debug.Log("[Enhancer] Planted " + count + " border trees.");
    }

    static void PlaceFenceRun(GameObject root, Terrain terrain,
        Vector3 start, Vector3 end, string prefabPath, float spacing, string groupName)
    {
        GameObject pfab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (pfab == null) { Debug.LogWarning("[Enhancer] Prefab not found: " + prefabPath); return; }

        Vector3 dir = (end - start).normalized;
        float length = Vector3.Distance(start, end);
        int count = Mathf.FloorToInt(length / spacing);
        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

        GameObject group = new GameObject(groupName);
        group.transform.parent = root.transform;

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = start + dir * (i * spacing) + new Vector3(RndSym(0.15f), 0, RndSym(0.15f));
            pos.y = GetTerrainHeight(terrain, pos);
            GameObject go = (GameObject)UnityEngine.Object.Instantiate(pfab, pos,
                Quaternion.Euler(0, rot.eulerAngles.y + RndSym(5f), 0));
            go.transform.parent = group.transform;
            go.name = groupName + "_" + i;
        }
    }

    static void SpawnPropCluster(GameObject root, Terrain terrain, string prefabPath,
        Vector3 centre, int count, float radius, float scaleMin, float scaleMax, string baseName)
    {
        GameObject pfab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (pfab == null) { Debug.LogWarning("[Enhancer] Prefab not found: " + prefabPath); return; }

        for (int i = 0; i < count; i++)
        {
            float angle = (float)rng.NextDouble() * 360f;
            float dist  = (float)rng.NextDouble() * radius;
            Vector3 pos = centre + new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * dist, 0,
                                               Mathf.Sin(angle * Mathf.Deg2Rad) * dist);
            pos.y = GetTerrainHeight(terrain, pos);
            float sc = Rnd(scaleMin, scaleMax);
            GameObject go = (GameObject)UnityEngine.Object.Instantiate(pfab, pos,
                Quaternion.Euler(0, Rnd(0f, 360f), 0));
            go.transform.localScale = Vector3.one * sc;
            go.transform.parent = root.transform;
            go.name = baseName + "_" + i;
        }
    }

    static void SpawnRandomProps(GameObject root, Terrain terrain, string[] prefabPaths,
        Rect area, int count, float scaleMin, float scaleMax, string baseName)
    {
        List<GameObject> pfabs = new List<GameObject>();
        foreach (string path in prefabPaths)
        {
            GameObject pfab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (pfab != null) pfabs.Add(pfab);
            else Debug.LogWarning("[Enhancer] Prefab not found: " + path);
        }
        if (pfabs.Count == 0) return;

        for (int i = 0; i < count; i++)
        {
            float wx = Rnd(area.x, area.x + area.width);
            float wz = Rnd(area.y, area.y + area.height);
            Vector3 pos = new Vector3(wx, 0, wz);
            pos.y = GetTerrainHeight(terrain, pos);
            float sc = Rnd(scaleMin, scaleMax);
            GameObject pfab = pfabs[rng.Next(pfabs.Count)];
            GameObject go = (GameObject)UnityEngine.Object.Instantiate(pfab, pos,
                Quaternion.Euler(0, Rnd(0f, 360f), 0));
            go.transform.localScale = Vector3.one * sc;
            go.transform.parent = root.transform;
            go.name = baseName + "_" + i;
        }
    }

    static void SpawnPropAt(GameObject root, Terrain terrain, string prefabPath,
        Vector3 pos, float scale, string name)
    {
        GameObject pfab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (pfab == null) { Debug.LogWarning("[Enhancer] Prefab not found: " + prefabPath); return; }
        pos.y = GetTerrainHeight(terrain, pos);
        GameObject go = (GameObject)UnityEngine.Object.Instantiate(pfab, pos,
            Quaternion.Euler(0, Rnd(0f, 360f), 0));
        go.transform.localScale = Vector3.one * scale;
        go.transform.parent = root.transform;
        go.name = name;
    }

    static float GetTerrainHeight(Terrain terrain, Vector3 worldPos)
    {
        return terrain.SampleHeight(worldPos);
    }

    // =========================================================================
    // STEP 6 – PERFORMANCE
    // =========================================================================
    static void ApplyTerrainPerformance(Terrain terrain)
    {
        terrain.detailObjectDensity     = 0.55f;
        terrain.detailObjectDistance    = 60f;
        terrain.treeBillboardDistance   = 80f;
        terrain.treeDistance            = 400f;
        terrain.treeCrossFadeLength     = 10f;
        terrain.heightmapPixelError     = 5f;
        terrain.basemapDistance         = 200f;
        terrain.shadowCastingMode       = UnityEngine.Rendering.ShadowCastingMode.Off;
        Debug.Log("[Enhancer] Terrain performance settings applied.");
    }

    // =========================================================================
    // UTILITY
    // =========================================================================
    static Terrain GetTerrain()
    {
        // Try to find "Terrain (1)" specifically
        Terrain[] terrains = GameObject.FindObjectsOfType<Terrain>();
        foreach (Terrain t in terrains)
            if (t.gameObject.name == "Terrain (1)") return t;
        if (terrains.Length > 0) return terrains[0];
        return null;
    }

    static TerrainLayer CreateOrLoadLayer(string layerAssetPath, string texturePath,
        string normalPath, Vector2 tileSize)
    {
        // Try to load existing
        TerrainLayer existing = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerAssetPath);
        if (existing != null) return existing;

        Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (diffuse == null)
        {
            Debug.LogWarning("[Enhancer] Texture not found, skipping layer: " + texturePath);
            return null;
        }

        Texture2D normal = null;
        if (!string.IsNullOrEmpty(normalPath))
            normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);

        TerrainLayer layer = new TerrainLayer();
        layer.diffuseTexture = diffuse;
        layer.normalMapTexture = normal;
        layer.tileSize = tileSize;
        layer.tileOffset = Vector2.zero;

        // Ensure folder exists
        string folder = System.IO.Path.GetDirectoryName(layerAssetPath).Replace("\\", "/");
        if (!AssetDatabase.IsValidFolder(folder))
        {
            System.IO.Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }

        AssetDatabase.CreateAsset(layer, layerAssetPath);
        AssetDatabase.SaveAssets();
        return layer;
    }
}

