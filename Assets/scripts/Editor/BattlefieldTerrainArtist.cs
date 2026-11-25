using UnityEngine;
using UnityEditor;

/// <summary>
/// Master AAA Terrain Sculptor & Painter matching the reference image:
/// - 2048x2048 Alphamap & 1025x1025 Heightmap for razor-sharp quality
/// - Authentic twin-rut cart road (two distinct wheel tracks with grass in between)
/// - Organic meandering country road with natural bends and shoulder pebbles
/// - Multi-tonal lush stylized meadow grass (sunny clearings + rich turf)
/// - Organically framed valley hills with gentle countryside undulations
/// - Physical wheel rut depth indentations
/// </summary>
public static class BattlefieldTerrainArtist
{
    const string SM_PATH = "Assets/Stylized Medieval Kingdom URP/Textures/";
    const string HP_PATH = "Assets/Handpainted_Grass_and_Ground_Textures/Textures/";

    public static float SmoothStepEdge(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3.0f - 2.0f * t);
    }

    [MenuItem("Tools/Battlefield/Paint and Sculpt Terrain (Reference Match)")]
    public static void ApplyTerrainArt()
    {
        Terrain terrain = GetTerrain();
        if (terrain == null)
        {
            Debug.LogError("[TerrainArtist] Could not find 'Terrain (1)'!");
            return;
        }

        TerrainData td = terrain.terrainData;
        Vector3 tPos = terrain.transform.position;
        Vector3 tSize = td.size;

        Debug.Log("[TerrainArtist] Upgrading Resolutions (Alphamap 2048, Heightmap 1025)...");
        if (td.alphamapResolution != 2048) td.alphamapResolution = 2048;
        if (td.heightmapResolution != 1025) td.heightmapResolution = 1025;

        Debug.Log("[TerrainArtist] Configuring Master Terrain Layers...");
        SetupMasterLayers(td);

        Debug.Log("[TerrainArtist] Sculpting High-Fidelity Heightmap...");
        SculptHeightmap(td, tPos, tSize);

        Debug.Log("[TerrainArtist] Painting Razor-Sharp 2048 Alphamap...");
        PaintAlphamapChunked(td, tPos, tSize);

        Debug.Log("[TerrainArtist] Applying Quality Render Settings...");
        ApplyQualitySettings(terrain);

        // Mark dirty & save
        EditorUtility.SetDirty(td);
        EditorUtility.SetDirty(terrain);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

        Debug.Log("[TerrainArtist] ✓ Master AAA Terrain Sculpting and Painting Complete!");
    }

    static Terrain GetTerrain()
    {
        Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        foreach (Terrain t in terrains)
        {
            if (t.name == "Terrain (1)") return t;
        }
        return terrains.Length > 0 ? terrains[0] : null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // MASTER TERRAIN LAYERS
    // ─────────────────────────────────────────────────────────────────────────
    static void SetupMasterLayers(TerrainData td)
    {
        // Textures
        Texture2D grassBase = AssetDatabase.LoadAssetAtPath<Texture2D>(SM_PATH + "Grass Texture (Texture).png");
        Texture2D grassNorm = AssetDatabase.LoadAssetAtPath<Texture2D>(SM_PATH + "Grass Normal.png");
        Texture2D dirtBase  = AssetDatabase.LoadAssetAtPath<Texture2D>(SM_PATH + "Ground Color.png");
        Texture2D dirtNorm  = AssetDatabase.LoadAssetAtPath<Texture2D>(SM_PATH + "Ground Normal.png");
        Texture2D grassDark = AssetDatabase.LoadAssetAtPath<Texture2D>(HP_PATH + "Grass/Grass_darked/Grass_darked_up.png");
        Texture2D dirtClay  = AssetDatabase.LoadAssetAtPath<Texture2D>(HP_PATH + "Dirt/dirt_clay/dirt_clay_up.png");
        Texture2D dirtRocks = AssetDatabase.LoadAssetAtPath<Texture2D>(HP_PATH + "Dirt/dirt_lighted_rocks/dirt_lighted_rocks_up.png");
        Texture2D grassSwamp = AssetDatabase.LoadAssetAtPath<Texture2D>(HP_PATH + "Grass/Grass_swamp_dark/Grass_swamp_dark_up.png");
        Texture2D grassLight = AssetDatabase.LoadAssetAtPath<Texture2D>(HP_PATH + "Grass/Grass_lighted/Grass_lighted_up.png");

        TerrainLayer[] layers = new TerrainLayer[7];

        // 0: Primary Stylized Grass (rich vibrant green)
        layers[0] = GetOrCreateLayer("Assets/TerrainLayers/TL_Master_GrassBase.terrainlayer",
            grassBase, grassNorm, new Vector2(7f, 7f));

        // 1: Primary Cart Road Dirt (warm stylized medieval earth)
        layers[1] = GetOrCreateLayer("Assets/TerrainLayers/TL_Master_DirtRoad.terrainlayer",
            dirtBase, dirtNorm, new Vector2(4.5f, 4.5f));

        // 2: Dark Hillside Turf (forest border framing)
        layers[2] = GetOrCreateLayer("Assets/TerrainLayers/TL_Master_GrassDark.terrainlayer",
            grassDark, grassNorm, new Vector2(10f, 10f));

        // 3: Compacted Rut Clay (darker wheel groove bottom)
        layers[3] = GetOrCreateLayer("Assets/TerrainLayers/TL_Master_DirtClay.terrainlayer",
            dirtClay, dirtNorm, new Vector2(3.5f, 3.5f));

        // 4: Roadside Pebbles & Gravel
        layers[4] = GetOrCreateLayer("Assets/TerrainLayers/TL_Master_DirtRocks.terrainlayer",
            dirtRocks, dirtNorm, new Vector2(3.5f, 3.5f));

        // 5: Deep Wilderness Floor (outer perimeter)
        layers[5] = GetOrCreateLayer("Assets/TerrainLayers/TL_Master_GrassSwamp.terrainlayer",
            grassSwamp, grassNorm, new Vector2(12f, 12f));

        // 6: Sunny Meadow Grass (warm golden-green clearings)
        layers[6] = GetOrCreateLayer("Assets/TerrainLayers/TL_Master_GrassSunny.terrainlayer",
            grassLight, grassNorm, new Vector2(8f, 8f));

        td.terrainLayers = layers;
    }

    static TerrainLayer GetOrCreateLayer(string path, Texture2D diffuse, Texture2D normal, Vector2 tileSize)
    {
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer == null)
        {
            layer = new TerrainLayer();
            string folder = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(folder))
            {
                System.IO.Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }
            AssetDatabase.CreateAsset(layer, path);
        }

        layer.diffuseTexture = diffuse;
        layer.normalMapTexture = normal;
        layer.tileSize = tileSize;
        layer.tileOffset = Vector2.zero;
        EditorUtility.SetDirty(layer);
        return layer;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PATH CURVE (Full Length: X = 4.4 to X = 168.5)
    // ─────────────────────────────────────────────────────────────────────────
    public static float GetPathCenterZ(float wx)
    {
        float xStart = 4.4f;
        float xEnd = 168.5f;
        float zStart = 24.7f;
        float zEnd = 21.9f;

        float t = Mathf.Clamp01((wx - xStart) / (xEnd - xStart));
        float baseZ = Mathf.Lerp(zStart, zEnd, t);
        float envelope = Mathf.Sin(t * Mathf.PI);

        // Natural cart road meandering S-curves across the battlefield
        float wave = Mathf.Sin(t * Mathf.PI * 8.6f) * 2.3f
                   + Mathf.Sin(t * Mathf.PI * 4.3f + 0.6f) * 1.1f
                   + Mathf.Cos(t * Mathf.PI * 2.1f) * 0.45f;

        // Subtle organic road micro-wandering (so it's not a rigid sine wave)
        float microWander = (Mathf.PerlinNoise(wx * 0.08f, 23.4f) - 0.5f) * 0.55f;

        return baseZ + envelope * (wave + microWander);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HEIGHTMAP SCULPTING (1025x1025)
    // ─────────────────────────────────────────────────────────────────────────
    static void SculptHeightmap(TerrainData td, Vector3 tPos, Vector3 tSize)
    {
        int res = td.heightmapResolution; // 1025
        float[,] heights = td.GetHeights(0, 0, res, res);
        float maxHeight = tSize.y; // 600m

        for (int y = 0; y < res; y++)
        {
            float normZ = (float)y / (res - 1);
            float worldZ = tPos.z + normZ * tSize.z;

            for (int x = 0; x < res; x++)
            {
                float normX = (float)x / (res - 1);
                float worldX = tPos.x + normX * tSize.x;

                float heightMeters = CalculateWorldHeight(worldX, worldZ);
                heights[y, x] = Mathf.Clamp01(heightMeters / maxHeight);
            }
        }

        td.SetHeights(0, 0, heights);
    }

    public static float CalculateWorldHeight(float wx, float wz)
    {
        float xStart = 4.4f;
        float xEnd = 168.5f;

        float pathZ = GetPathCenterZ(wx);
        float distFromPathZ = Mathf.Abs(wz - pathZ);

        bool inArenaX = (wx >= (xStart - 10f) && wx <= (xEnd + 10f));
        bool inArenaZ = (wz >= 5f && wz <= 45f);

        // 1. Outside wilderness hills (2.5m - 4.0m)
        if (!inArenaX || !inArenaZ)
        {
            float distOutX = 0f;
            if (wx < (xStart - 10f)) distOutX = (xStart - 10f) - wx;
            else if (wx > (xEnd + 10f)) distOutX = wx - (xEnd + 10f);

            float distOutZ = 0f;
            if (wz < 5f) distOutZ = 5f - wz;
            else if (wz > 45f) distOutZ = wz - 45f;

            float distOut = Mathf.Sqrt(distOutX * distOutX + distOutZ * distOutZ);
            float wildRise = SmoothStepEdge(0f, 15f, distOut) * 3.4f;
            return 0.12f + wildRise;
        }

        // Inside battlefield corridor:
        // 2. Framing side hills that organically widen and narrow along the valley
        float valleyHalfWidth = 10.5f + Mathf.Sin(wx * 0.09f) * 1.8f + Mathf.Cos(wx * 0.19f) * 0.9f;
        float sideHill = SmoothStepEdge(valleyHalfWidth, valleyHalfWidth + 7.5f, distFromPathZ) * 2.2f;

        // 3. Castle courtyards: flat at 0.05m around CastleA (X=4.4) and CastleB (X=168.5)
        float castleDistA = Mathf.Sqrt(Mathf.Pow(wx - xStart, 2f) + Mathf.Pow(wz - 24.7f, 2f));
        float castleDistB = Mathf.Sqrt(Mathf.Pow(wx - xEnd, 2f) + Mathf.Pow(wz - 21.9f, 2f));
        float castleFactorA = SmoothStepEdge(3.5f, 9.0f, castleDistA);
        float castleFactorB = SmoothStepEdge(3.5f, 9.0f, castleDistB);
        float fieldZone = Mathf.Min(castleFactorA, castleFactorB);

        // 4. Gentle rolling countryside undulations (0.05m to 0.14m)
        float roll1 = Mathf.Sin(wx * 0.07f) * Mathf.Cos(wz * 0.11f) * 0.10f;
        float roll2 = Mathf.Sin(wx * 0.16f + 1.2f) * Mathf.Sin(wz * 0.20f) * 0.05f;
        float fieldUndulation = (roll1 + roll2) * fieldZone;

        // 5. Physical wheel rut depth indentations (-0.05m into the earth)
        float dRut1 = Mathf.Abs(wz - (pathZ + 0.58f));
        float dRut2 = Mathf.Abs(wz - (pathZ - 0.58f));
        float inRut1 = SmoothStepEdge(0.30f, 0.04f, dRut1);
        float inRut2 = SmoothStepEdge(0.30f, 0.04f, dRut2);
        float rutIndent = -(inRut1 + inRut2) * 0.05f * fieldZone;

        float finalH = 0.06f + sideHill + fieldUndulation + rutIndent;
        return Mathf.Max(0.01f, finalH);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ALPHAMAP PAINTING (2048x2048 in 4 Chunks)
    // ─────────────────────────────────────────────────────────────────────────
    static void PaintAlphamapChunked(TerrainData td, Vector3 tPos, Vector3 tSize)
    {
        int totalW = td.alphamapWidth;   // 2048
        int totalH = td.alphamapHeight;  // 2048
        int numLayers = td.alphamapLayers;
        int chunkSize = 512;

        for (int yStart = 0; yStart < totalH; yStart += chunkSize)
        {
            int currentH = Mathf.Min(chunkSize, totalH - yStart);
            float[,,] chunk = new float[currentH, totalW, numLayers];

            for (int cy = 0; cy < currentH; cy++)
            {
                int y = yStart + cy;
                float normZ = (float)y / (totalH - 1);
                float worldZ = tPos.z + normZ * tSize.z;

                for (int x = 0; x < totalW; x++)
                {
                    float normX = (float)x / (totalW - 1);
                    float worldX = tPos.x + normX * tSize.x;

                    CalculateAlphamapWeights(worldX, worldZ, numLayers, chunk, cy, x);
                }
            }

            td.SetAlphamaps(0, yStart, chunk);
        }
    }

    static void CalculateAlphamapWeights(float wx, float wz, int numLayers, float[,,] maps, int cy, int x)
    {
        float xStart = 4.4f;
        float xEnd = 168.5f;

        bool inArenaX = (wx >= (xStart - 8f) && wx <= (xEnd + 8f));
        bool inArenaZ = (wz >= 5f && wz <= 45f);

        if (!inArenaX || !inArenaZ)
        {
            // Deep forest perimeter outside battlefield
            if (numLayers > 5) maps[cy, x, 5] = 0.80f; // GrassSwamp
            if (numLayers > 2) maps[cy, x, 2] = 0.20f; // GrassDark
            return;
        }

        float pathZ = GetPathCenterZ(wx);
        float distFromPathZ = Mathf.Abs(wz - pathZ);

        // 1. Forest edge framing transition (organic hillsides)
        float valleyHalfWidth = 10.5f + Mathf.Sin(wx * 0.09f) * 1.8f + Mathf.Cos(wx * 0.19f) * 0.9f;
        float forestEdge = SmoothStepEdge(valleyHalfWidth - 1.5f, valleyHalfWidth + 5.5f, distFromPathZ);
        float deepForest = SmoothStepEdge(valleyHalfWidth + 4.0f, valleyHalfWidth + 9.5f, distFromPathZ);

        // 2. Twin Cart Ruts (Authentic Wagon Wheel Tracks)
        // Two distinct parallel wheel tracks separated by 1.16m
        float dRut1 = Mathf.Abs(wz - (pathZ + 0.58f));
        float dRut2 = Mathf.Abs(wz - (pathZ - 0.58f));

        // Subtle wheel groove edge micro-irregularity
        float edgeNoise = (Mathf.PerlinNoise(wx * 0.40f, wz * 0.40f) - 0.5f) * 0.07f;
        float rutRadius = 0.30f + edgeNoise;

        float rut1 = SmoothStepEdge(rutRadius, 0.04f, dRut1);
        float rut2 = SmoothStepEdge(rutRadius, 0.04f, dRut2);
        float totalRut = Mathf.Clamp01(rut1 + rut2);

        // Deep groove core (clay bottom of each wheel track)
        float minRutDist = Mathf.Min(dRut1, dRut2);
        float rutCore = SmoothStepEdge(0.16f, 0.02f, minRutDist);

        // Track shoulder pebbles (pebbly soil on the outer edges of the tracks)
        float rutShoulder = SmoothStepEdge(0.48f, 0.22f, minRutDist) * (1.0f - totalRut);

        // Castle gate dirt courtyards
        float gateDistA = Mathf.Sqrt(Mathf.Pow(wx - xStart, 2f) + Mathf.Pow(wz - 24.7f, 2f));
        float gateDistB = Mathf.Sqrt(Mathf.Pow(wx - xEnd, 2f) + Mathf.Pow(wz - 21.9f, 2f));
        float gateApronA = SmoothStepEdge(4.0f, 0.8f, gateDistA);
        float gateApronB = SmoothStepEdge(4.0f, 0.8f, gateDistB);
        float gateDirt = Mathf.Max(gateApronA, gateApronB) * 0.92f;

        float dirtTotal = Mathf.Max(totalRut, gateDirt);

        // 3. Painterly Sunny Meadow Grass Patches (large cloud-like warm clearings)
        float p1 = Mathf.PerlinNoise(wx * 0.025f + 12f, wz * 0.035f + 34f);
        float p2 = Mathf.PerlinNoise(wx * 0.060f + 56f, wz * 0.080f + 78f);
        float sunnyPool = Mathf.Clamp01(p1 * 0.75f + p2 * 0.25f);
        float sunnyHighlight = SmoothStepEdge(0.40f, 0.70f, sunnyPool);

        // Layer assignments:
        // Layer 0: TL_Master_GrassBase (rich vibrant stylized green meadow)
        // Layer 1: TL_Master_DirtRoad (warm stylized medieval road earth)
        // Layer 2: TL_Master_GrassDark (dark hillside forest turf)
        // Layer 3: TL_Master_DirtClay (deep compacted rut groove clay)
        // Layer 4: TL_Master_DirtRocks (roadside pebbles and loose gravel)
        // Layer 5: TL_Master_GrassSwamp (perimeter wilderness floor)
        // Layer 6: TL_Master_GrassSunny (warm sunny golden-green clearings)

        float w0 = 0f;
        float w1 = 0f;
        float w2 = 0f;
        float w3 = 0f;
        float w4 = 0f;
        float w5 = 0f;
        float w6 = 0f;

        float grassRemainder = Mathf.Clamp01(1.0f - dirtTotal);

        // Meadow grass blending (base vibrant grass + sunny golden-green pools)
        w0 = grassRemainder * (1.0f - sunnyHighlight * 0.55f);
        w6 = grassRemainder * (sunnyHighlight * 0.55f);

        // Between the wheel tracks: slightly lighter trampled grass
        float centerGrassFactor = SmoothStepEdge(0.50f, 0.05f, Mathf.Abs(distFromPathZ)) * (1.0f - totalRut);
        w6 += grassRemainder * centerGrassFactor * 0.35f;
        w0 -= grassRemainder * centerGrassFactor * 0.35f;
        w0 = Mathf.Max(0f, w0);

        // Forest hillside framing
        w2 = grassRemainder * forestEdge * 0.75f;
        w5 = grassRemainder * deepForest * 0.85f;
        w0 *= (1.0f - forestEdge);
        w6 *= (1.0f - forestEdge);

        // Dirt road layers
        // Main wheel rut earth: Layer 1
        w1 = dirtTotal * (0.75f - rutCore * 0.40f);
        // Deep clay wheel groove: Layer 3
        w3 = dirtTotal * (rutCore * 0.60f + 0.15f);
        // Shoulder pebbles and loose stone: Layer 4
        w4 = rutShoulder * 0.35f + gateDirt * 0.15f;

        // Normalize
        float sum = w0 + w1 + w2 + w3 + w4 + w5 + w6;
        if (sum < 0.0001f) { w0 = 1f; sum = 1f; }

        if (numLayers > 0) maps[cy, x, 0] = w0 / sum;
        if (numLayers > 1) maps[cy, x, 1] = w1 / sum;
        if (numLayers > 2) maps[cy, x, 2] = w2 / sum;
        if (numLayers > 3) maps[cy, x, 3] = w3 / sum;
        if (numLayers > 4) maps[cy, x, 4] = w4 / sum;
        if (numLayers > 5) maps[cy, x, 5] = w5 / sum;
        if (numLayers > 6) maps[cy, x, 6] = w6 / sum;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // QUALITY RENDER SETTINGS
    // ─────────────────────────────────────────────────────────────────────────
    static void ApplyQualitySettings(Terrain terrain)
    {
        terrain.drawInstanced = true;
        terrain.heightmapPixelError = 1.0f;
        terrain.basemapDistance = 1000f;
        terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;

        // Directional Light: Warm golden medieval sunlight
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (Light lt in lights)
        {
            if (lt.type == LightType.Directional)
            {
                lt.color = new Color(1.0f, 0.96f, 0.85f);
                lt.intensity = 2.4f;
                lt.shadowStrength = 0.60f;
                lt.shadows = LightShadows.Soft;
                lt.transform.rotation = Quaternion.Euler(54f, 320f, 0f);
                break;
            }
        }

        // Ambient: Vibrant sunny fantasy atmosphere
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor     = new Color(0.65f, 0.82f, 0.52f); // sunny warm meadow sky
        RenderSettings.ambientEquatorColor = new Color(0.48f, 0.68f, 0.38f); // lush horizon
        RenderSettings.ambientGroundColor  = new Color(0.30f, 0.42f, 0.22f); // warm soil
        RenderSettings.fog = false;
    }
}
