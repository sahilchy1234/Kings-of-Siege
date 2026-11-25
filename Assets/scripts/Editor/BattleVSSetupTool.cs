using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Editor setup tool to rebuild and configure the BattelVS UI using the official Kings of Siege assets
/// calibrated specifically for the portrait canvas.
/// </summary>
public static class BattleVSSetupTool
{
    private const string ASSET_PATH = "Assets/UIPACK/battlevs/png/";
    private const string PREFAB_PATH = "Assets/UIPACK/Prefabs/Prefabs_DemoScene_Panels/BattelVS.prefab";
    private const string FONT_PATH = "Assets/UIPACK/ResourcesData/Fonts/TMP_TiltWarp_Outline.asset";

    [MenuItem("Tools/BattleVS/Rebuild Kings of Siege BattleVS UI")]
    public static void RebuildAllMenu()
    {
        RebuildAll(showDialog: true);
    }

    public static void RebuildAll(bool showDialog = false)
    {
        Debug.Log("<color=#00FFAA>[BattleVSSetupTool] Starting BattleVS UI Rebuild...</color>");

        // 1. Rebuild Prefab
        RebuildPrefab();

        // 2. Rebuild in Active Scene if BattleScene
        RebuildInScene();

        Debug.Log("<color=#00FFAA>[BattleVSSetupTool] Rebuild Complete! All Kings of Siege assets assigned and aligned to portrait layout.</color>");
        if (showDialog)
        {
            EditorUtility.DisplayDialog("BattleVS Rebuilt", 
                "The BattleVS countdown screen has been rebuilt according to the Kings of Siege specification:\n\n" +
                "• Coral Red (#FF4148) upper & Royal Blue (#1760FF) lower diagonal division\n" +
                "• White seam line with 5 battle silhouettes\n" +
                "• Framed Knight Avatars (red top, blue bottom) centered on screen\n" +
                "• Assembled nameplates with overlapping league shields & level medallions\n" +
                "• Live white bold names and live dark bold level numbers (#222222)\n" +
                "• 3D Countdown graphics (1, 2, 3, START!)\n\n" +
                "You can test the animations in Edit Mode using the buttons on the BattelVS Inspector!",
                "OK");
        }
    }

    public static void RebuildPrefab()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
        if (prefabRoot == null)
        {
            Debug.LogError("[BattleVSSetupTool] Could not find prefab at: " + PREFAB_PATH);
            return;
        }

        string fullPath = AssetDatabase.GetAssetPath(prefabRoot);
        GameObject instance = PrefabUtility.InstantiatePrefab(prefabRoot) as GameObject;
        if (instance == null) return;

        try
        {
            SetupHierarchy(instance.transform);
            PrefabUtility.SaveAsPrefabAsset(instance, fullPath);
            Debug.Log("[BattleVSSetupTool] Saved rebuilt BattelVS.prefab");
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    public static void RebuildInScene()
    {
        BattelVS bvs = Object.FindFirstObjectByType<BattelVS>(FindObjectsInactive.Include);
        if (bvs == null) return;

        SetupHierarchy(bvs.transform);
        bvs.InitializeReferences();
        bvs.ApplyResponsivePortraitLayout();
        EditorUtility.SetDirty(bvs.gameObject);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[BattleVSSetupTool] Rebuilt BattelVS in active scene: " + SceneManager.GetActiveScene().name);
    }

    private static void SetupHierarchy(Transform root)
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);

        // Load Sprites
        Sprite avRed = LoadSprite("avatar_red.png");
        Sprite avBlue = LoadSprite("avatar_blue.png");
        Sprite npRed = LoadSprite("nameplate_red_silver_blank.png");
        Sprite npBlue = LoadSprite("nameplate_blue_gold_blank.png");
        Sprite shSilver = LoadSprite("league_silver.png");
        Sprite shGold = LoadSprite("league_gold.png");
        Sprite medSilver = LoadSprite("level_medallion_silver_blank.png");
        Sprite medGold = LoadSprite("level_medallion_gold_blank.png");

        Sprite cd3 = LoadSprite("countdown_3.png");
        Sprite cd2 = LoadSprite("countdown_2.png");
        Sprite cd1 = LoadSprite("countdown_1.png");
        Sprite cdStart = LoadSprite("countdown_start.png");

        Sprite iconKnight = LoadSprite("battle_mounted_knight.png");
        Sprite iconSpearman = LoadSprite("battle_spearman.png");
        Sprite iconSwords = LoadSprite("battle_crossed_swords_shield.png");
        Sprite iconSoldier = LoadSprite("battle_sword_soldier.png");
        Sprite iconSoldierRev = LoadSprite("battle_sword_soldier_reverse.png");

        // 1. Root & PanelMask
        RectTransform rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;
        rootRt.sizeDelta = Vector2.zero;
        rootRt.anchoredPosition = Vector2.zero;
        rootRt.localScale = Vector3.one;

        Transform maskTr = root.Find("PanelMask");
        GameObject maskObj;
        if (maskTr == null)
        {
            maskObj = new GameObject("PanelMask", typeof(RectTransform));
            maskObj.transform.SetParent(root, false);
        }
        else
        {
            maskObj = maskTr.gameObject;
        }

        RectTransform maskRt = maskObj.GetComponent<RectTransform>() ?? maskObj.AddComponent<RectTransform>();
        maskRt.anchorMin = Vector2.zero;
        maskRt.anchorMax = Vector2.one;
        maskRt.offsetMin = Vector2.zero;
        maskRt.offsetMax = Vector2.zero;
        maskRt.sizeDelta = Vector2.zero;
        maskRt.anchoredPosition = Vector2.zero;
        maskRt.localScale = Vector3.one;

        RectMask2D mask = maskObj.GetComponent<RectMask2D>() ?? maskObj.AddComponent<RectMask2D>();
        mask.enabled = true;

        // Clean obsolete placeholder children
        string[] oldNames = { "Bg_Top (1)", "Icon_Role", "Icon", "DecoLeft", "DecoRight", "GlowBlue", "GlowRed", "Bg_Top", "Bg_Bottom", "Bg_Center", "Character", "Character (1)", "Text_Player1", "Text_Player2", "Text_VS" };
        foreach (string oldName in oldNames)
        {
            for (int i = maskObj.transform.childCount - 1; i >= 0; i--)
            {
                Transform ch = maskObj.transform.GetChild(i);
                if (ch.name.StartsWith(oldName) || ch.name == oldName)
                {
                    Object.DestroyImmediate(ch.gameObject);
                }
            }
        }

        // 2. Bg_Bottom_Blue (Royal Blue #1760FF)
        GameObject bgBlueObj = GetOrCreateChild(maskObj.transform, "Bg_Bottom_Blue");
        RectTransform bgBlueRt = bgBlueObj.GetComponent<RectTransform>() ?? bgBlueObj.AddComponent<RectTransform>();
        bgBlueRt.anchorMin = Vector2.zero;
        bgBlueRt.anchorMax = Vector2.one;
        bgBlueRt.offsetMin = Vector2.zero;
        bgBlueRt.offsetMax = Vector2.zero;
        bgBlueRt.sizeDelta = Vector2.zero;
        bgBlueRt.anchoredPosition = Vector2.zero;
        bgBlueRt.localScale = Vector3.one;
        Image bgBlueImg = bgBlueObj.GetComponent<Image>() ?? bgBlueObj.AddComponent<Image>();
        bgBlueImg.sprite = null;
        bgBlueImg.color = BattelVS.ColorRoyalBlue;
        bgBlueImg.raycastTarget = false;

        // 3. Bg_Top_Red (Coral Red #FF4148)
        // Diagonal: endpoints left 54% height, right 46% height -> angle = -8.13 deg
        GameObject bgRedObj = GetOrCreateChild(maskObj.transform, "Bg_Top_Red");
        RectTransform bgRedRt = bgRedObj.GetComponent<RectTransform>() ?? bgRedObj.AddComponent<RectTransform>();
        bgRedRt.pivot = new Vector2(0.5f, 0f);
        bgRedRt.anchorMin = new Vector2(0.5f, 0.5f);
        bgRedRt.anchorMax = new Vector2(0.5f, 0.5f);
        bgRedRt.anchoredPosition = Vector2.zero;
        bgRedRt.sizeDelta = new Vector2(900f, 700f);
        bgRedRt.localEulerAngles = new Vector3(0f, 0f, -8.13f);
        bgRedRt.localScale = Vector3.one;
        Image bgRedImg = bgRedObj.GetComponent<Image>() ?? bgRedObj.AddComponent<Image>();
        bgRedImg.sprite = null;
        bgRedImg.color = BattelVS.ColorCoralRed;
        bgRedImg.raycastTarget = false;

        // 4. SeamContainer (Diagonal seam with line and white battle icons)
        GameObject seamObj = GetOrCreateChild(maskObj.transform, "SeamContainer");
        RectTransform seamRt = seamObj.GetComponent<RectTransform>() ?? seamObj.AddComponent<RectTransform>();
        seamRt.anchorMin = new Vector2(0.5f, 0.5f);
        seamRt.anchorMax = new Vector2(0.5f, 0.5f);
        seamRt.anchoredPosition = Vector2.zero;
        seamRt.sizeDelta = new Vector2(500f, 60f);
        seamRt.localEulerAngles = new Vector3(0f, 0f, -8.13f);
        seamRt.localScale = Vector3.one;

        // Thin white line along seam
        GameObject lineObj = GetOrCreateChild(seamObj.transform, "SeamLine");
        RectTransform lineRt = lineObj.GetComponent<RectTransform>() ?? lineObj.AddComponent<RectTransform>();
        lineRt.anchorMin = new Vector2(0.5f, 0.5f);
        lineRt.anchorMax = new Vector2(0.5f, 0.5f);
        lineRt.anchoredPosition = Vector2.zero;
        lineRt.sizeDelta = new Vector2(550f, 3f);
        lineRt.localScale = Vector3.one;
        Image lineImg = lineObj.GetComponent<Image>() ?? lineObj.AddComponent<Image>();
        lineImg.sprite = null;
        lineImg.color = new Color(1f, 1f, 1f, 0.90f);
        lineImg.raycastTarget = false;

        // 5 Battle Silhouettes along seam (visible across portrait width ~338)
        Image icon1 = CreateSilhouette(seamObj.transform, "Icon_MountedKnight", iconKnight, new Vector2(-118f, 12f), new Vector2(40f, 30f));
        Image icon2 = CreateSilhouette(seamObj.transform, "Icon_Spearman", iconSpearman, new Vector2(-68f, 10f), new Vector2(34f, 26f));
        Image icon3 = CreateSilhouette(seamObj.transform, "Icon_CrossedSwords", iconSwords, new Vector2(68f, -10f), new Vector2(34f, 26f));
        Image icon4 = CreateSilhouette(seamObj.transform, "Icon_SwordSoldier", iconSoldier, new Vector2(104f, -12f), new Vector2(34f, 26f));
        Image icon5 = CreateSilhouette(seamObj.transform, "Icon_SwordReverse", iconSoldierRev, new Vector2(136f, -14f), new Vector2(34f, 26f));

        // 5. Player 2 / Opponent (Red Section) - Y = +145
        GameObject p2Obj = GetOrCreateChild(maskObj.transform, "Player2_Red_Container");
        RectTransform p2Rt = p2Obj.GetComponent<RectTransform>() ?? p2Obj.AddComponent<RectTransform>();
        p2Rt.anchorMin = new Vector2(0.5f, 0.5f);
        p2Rt.anchorMax = new Vector2(0.5f, 0.5f);
        p2Rt.anchoredPosition = BattelVS.RESTING_POS_P2_RED;
        p2Rt.sizeDelta = new Vector2(300f, 200f);
        p2Rt.localScale = Vector3.one;

        // Avatar Red
        GameObject avRedObj = GetOrCreateChild(p2Obj.transform, "Avatar_Red");
        RectTransform avRedRt = avRedObj.GetComponent<RectTransform>() ?? avRedObj.AddComponent<RectTransform>();
        avRedRt.anchoredPosition = new Vector2(0f, 38f);
        avRedRt.sizeDelta = new Vector2(105f, 96f);
        avRedRt.localScale = Vector3.one;
        Image avRedImg = avRedObj.GetComponent<Image>() ?? avRedObj.AddComponent<Image>();
        avRedImg.sprite = avRed;
        avRedImg.preserveAspect = true;
        avRedImg.raycastTarget = false;

        // Nameplate Red Assembly
        GameObject npRedAssembly = GetOrCreateChild(p2Obj.transform, "Nameplate_Red_Assembly");
        RectTransform npRedAssRt = npRedAssembly.GetComponent<RectTransform>() ?? npRedAssembly.AddComponent<RectTransform>();
        npRedAssRt.anchoredPosition = new Vector2(0f, -42f);
        npRedAssRt.sizeDelta = new Vector2(240f, 52f);
        npRedAssRt.localScale = Vector3.one;

        // Nameplate Red Bg
        GameObject npRedBgObj = GetOrCreateChild(npRedAssembly.transform, "Nameplate_Red_Bg");
        RectTransform npRedBgRt = npRedBgObj.GetComponent<RectTransform>() ?? npRedBgObj.AddComponent<RectTransform>();
        npRedBgRt.anchoredPosition = Vector2.zero;
        npRedBgRt.sizeDelta = new Vector2(205f, 46f);
        npRedBgRt.localScale = Vector3.one;
        Image npRedImg = npRedBgObj.GetComponent<Image>() ?? npRedBgObj.AddComponent<Image>();
        npRedImg.sprite = npRed;
        npRedImg.preserveAspect = true;
        npRedImg.raycastTarget = false;

        // League Shield Red (left overlap)
        GameObject shRedObj = GetOrCreateChild(npRedAssembly.transform, "League_Shield");
        RectTransform shRedRt = shRedObj.GetComponent<RectTransform>() ?? shRedObj.AddComponent<RectTransform>();
        shRedRt.anchoredPosition = new Vector2(-90f, 0f);
        shRedRt.sizeDelta = new Vector2(42f, 38f);
        shRedRt.localScale = Vector3.one;
        Image shRedImg = shRedObj.GetComponent<Image>() ?? shRedObj.AddComponent<Image>();
        shRedImg.sprite = shSilver;
        shRedImg.preserveAspect = true;
        shRedImg.raycastTarget = false;

        // Level Medallion Red (right overlap)
        GameObject medRedObj = GetOrCreateChild(npRedAssembly.transform, "Level_Medallion");
        RectTransform medRedRt = medRedObj.GetComponent<RectTransform>() ?? medRedObj.AddComponent<RectTransform>();
        medRedRt.anchoredPosition = new Vector2(90f, 0f);
        medRedRt.sizeDelta = new Vector2(38f, 34f);
        medRedRt.localScale = Vector3.one;
        Image medRedImg = medRedObj.GetComponent<Image>() ?? medRedObj.AddComponent<Image>();
        medRedImg.sprite = medSilver;
        medRedImg.preserveAspect = true;
        medRedImg.raycastTarget = false;

        // Live Dark Level Text in Medallion
        GameObject txtP2LvlObj = GetOrCreateChild(medRedObj.transform, "Text_Player2_Level");
        RectTransform txtP2LvlRt = txtP2LvlObj.GetComponent<RectTransform>() ?? txtP2LvlObj.AddComponent<RectTransform>();
        txtP2LvlRt.anchorMin = Vector2.zero;
        txtP2LvlRt.anchorMax = Vector2.one;
        txtP2LvlRt.sizeDelta = Vector2.zero;
        txtP2LvlRt.anchoredPosition = Vector2.zero;
        txtP2LvlRt.localScale = Vector3.one;
        TextMeshProUGUI txtP2Lvl = txtP2LvlObj.GetComponent<TextMeshProUGUI>() ?? txtP2LvlObj.AddComponent<TextMeshProUGUI>();
        if (font != null) txtP2Lvl.font = font;
        txtP2Lvl.text = "12";
        txtP2Lvl.fontSize = 14f;
        txtP2Lvl.fontStyle = FontStyles.Bold;
        txtP2Lvl.color = BattelVS.ColorDarkMedallion;
        txtP2Lvl.alignment = TextAlignmentOptions.Center;
        txtP2Lvl.raycastTarget = false;

        // Live White Name Text in Nameplate
        GameObject txtP2NameObj = GetOrCreateChild(npRedAssembly.transform, "Text_Player2_Name");
        RectTransform txtP2NameRt = txtP2NameObj.GetComponent<RectTransform>() ?? txtP2NameObj.AddComponent<RectTransform>();
        txtP2NameRt.anchoredPosition = new Vector2(0f, 1f);
        txtP2NameRt.sizeDelta = new Vector2(125f, 30f);
        txtP2NameRt.localScale = Vector3.one;
        TextMeshProUGUI txtP2Name = txtP2NameObj.GetComponent<TextMeshProUGUI>() ?? txtP2NameObj.AddComponent<TextMeshProUGUI>();
        if (font != null) txtP2Name.font = font;
        txtP2Name.text = "AI Commander";
        txtP2Name.fontSize = 15f;
        txtP2Name.fontStyle = FontStyles.Bold;
        txtP2Name.color = Color.white;
        txtP2Name.alignment = TextAlignmentOptions.Center;
        txtP2Name.raycastTarget = false;

        // 6. Player 1 / Local Player (Blue Section) - Y = -145
        GameObject p1Obj = GetOrCreateChild(maskObj.transform, "Player1_Blue_Container");
        RectTransform p1Rt = p1Obj.GetComponent<RectTransform>() ?? p1Obj.AddComponent<RectTransform>();
        p1Rt.anchorMin = new Vector2(0.5f, 0.5f);
        p1Rt.anchorMax = new Vector2(0.5f, 0.5f);
        p1Rt.anchoredPosition = BattelVS.RESTING_POS_P1_BLUE;
        p1Rt.sizeDelta = new Vector2(300f, 200f);
        p1Rt.localScale = Vector3.one;

        // Nameplate Blue Assembly
        GameObject npBlueAssembly = GetOrCreateChild(p1Obj.transform, "Nameplate_Blue_Assembly");
        RectTransform npBlueAssRt = npBlueAssembly.GetComponent<RectTransform>() ?? npBlueAssembly.AddComponent<RectTransform>();
        npBlueAssRt.anchoredPosition = new Vector2(0f, 42f);
        npBlueAssRt.sizeDelta = new Vector2(240f, 52f);
        npBlueAssRt.localScale = Vector3.one;

        // Nameplate Blue Bg
        GameObject npBlueBgObj = GetOrCreateChild(npBlueAssembly.transform, "Nameplate_Blue_Bg");
        RectTransform npBlueBgRt = npBlueBgObj.GetComponent<RectTransform>() ?? npBlueBgObj.AddComponent<RectTransform>();
        npBlueBgRt.anchoredPosition = Vector2.zero;
        npBlueBgRt.sizeDelta = new Vector2(205f, 46f);
        npBlueBgRt.localScale = Vector3.one;
        Image npBlueImg = npBlueBgObj.GetComponent<Image>() ?? npBlueBgObj.AddComponent<Image>();
        npBlueImg.sprite = npBlue;
        npBlueImg.preserveAspect = true;
        npBlueImg.raycastTarget = false;

        // League Shield Blue (left overlap)
        GameObject shBlueObj = GetOrCreateChild(npBlueAssembly.transform, "League_Shield");
        RectTransform shBlueRt = shBlueObj.GetComponent<RectTransform>() ?? shBlueObj.AddComponent<RectTransform>();
        shBlueRt.anchoredPosition = new Vector2(-90f, 0f);
        shBlueRt.sizeDelta = new Vector2(42f, 38f);
        shBlueRt.localScale = Vector3.one;
        Image shBlueImg = shBlueObj.GetComponent<Image>() ?? shBlueObj.AddComponent<Image>();
        shBlueImg.sprite = shGold;
        shBlueImg.preserveAspect = true;
        shBlueImg.raycastTarget = false;

        // Level Medallion Blue (right overlap)
        GameObject medBlueObj = GetOrCreateChild(npBlueAssembly.transform, "Level_Medallion");
        RectTransform medBlueRt = medBlueObj.GetComponent<RectTransform>() ?? medBlueObj.AddComponent<RectTransform>();
        medBlueRt.anchoredPosition = new Vector2(90f, 0f);
        medBlueRt.sizeDelta = new Vector2(38f, 34f);
        medBlueRt.localScale = Vector3.one;
        Image medBlueImg = medBlueObj.GetComponent<Image>() ?? medBlueObj.AddComponent<Image>();
        medBlueImg.sprite = medGold;
        medBlueImg.preserveAspect = true;
        medBlueImg.raycastTarget = false;

        // Live Dark Level Text in Medallion
        GameObject txtP1LvlObj = GetOrCreateChild(medBlueObj.transform, "Text_Player1_Level");
        RectTransform txtP1LvlRt = txtP1LvlObj.GetComponent<RectTransform>() ?? txtP1LvlObj.AddComponent<RectTransform>();
        txtP1LvlRt.anchorMin = Vector2.zero;
        txtP1LvlRt.anchorMax = Vector2.one;
        txtP1LvlRt.sizeDelta = Vector2.zero;
        txtP1LvlRt.anchoredPosition = Vector2.zero;
        txtP1LvlRt.localScale = Vector3.one;
        TextMeshProUGUI txtP1Lvl = txtP1LvlObj.GetComponent<TextMeshProUGUI>() ?? txtP1LvlObj.AddComponent<TextMeshProUGUI>();
        if (font != null) txtP1Lvl.font = font;
        txtP1Lvl.text = "15";
        txtP1Lvl.fontSize = 14f;
        txtP1Lvl.fontStyle = FontStyles.Bold;
        txtP1Lvl.color = BattelVS.ColorDarkMedallion;
        txtP1Lvl.alignment = TextAlignmentOptions.Center;
        txtP1Lvl.raycastTarget = false;

        // Live White Name Text in Nameplate
        GameObject txtP1NameObj = GetOrCreateChild(npBlueAssembly.transform, "Text_Player1_Name");
        RectTransform txtP1NameRt = txtP1NameObj.GetComponent<RectTransform>() ?? txtP1NameObj.AddComponent<RectTransform>();
        txtP1NameRt.anchoredPosition = new Vector2(0f, 1f);
        txtP1NameRt.sizeDelta = new Vector2(125f, 30f);
        txtP1NameRt.localScale = Vector3.one;
        TextMeshProUGUI txtP1Name = txtP1NameObj.GetComponent<TextMeshProUGUI>() ?? txtP1NameObj.AddComponent<TextMeshProUGUI>();
        if (font != null) txtP1Name.font = font;
        txtP1Name.text = "Player 1";
        txtP1Name.fontSize = 15f;
        txtP1Name.fontStyle = FontStyles.Bold;
        txtP1Name.color = Color.white;
        txtP1Name.alignment = TextAlignmentOptions.Center;
        txtP1Name.raycastTarget = false;

        // Avatar Blue
        GameObject avBlueObj = GetOrCreateChild(p1Obj.transform, "Avatar_Blue");
        RectTransform avBlueRt = avBlueObj.GetComponent<RectTransform>() ?? avBlueObj.AddComponent<RectTransform>();
        avBlueRt.anchoredPosition = new Vector2(0f, -38f);
        avBlueRt.sizeDelta = new Vector2(105f, 96f);
        avBlueRt.localScale = Vector3.one;
        Image avBlueImg = avBlueObj.GetComponent<Image>() ?? avBlueObj.AddComponent<Image>();
        avBlueImg.sprite = avBlue;
        avBlueImg.preserveAspect = true;
        avBlueImg.raycastTarget = false;

        // 7. Center Countdown
        GameObject cdObj = GetOrCreateChild(maskObj.transform, "CenterCountdown");
        RectTransform cdRt = cdObj.GetComponent<RectTransform>() ?? cdObj.AddComponent<RectTransform>();
        cdRt.anchorMin = new Vector2(0.5f, 0.5f);
        cdRt.anchorMax = new Vector2(0.5f, 0.5f);
        cdRt.anchoredPosition = BattelVS.RESTING_POS_COUNTDOWN;
        cdRt.sizeDelta = new Vector2(120f, 100f);
        cdRt.localScale = Vector3.one;

        GameObject cdImgObj = GetOrCreateChild(cdObj.transform, "Image_Countdown");
        RectTransform cdImgRt = cdImgObj.GetComponent<RectTransform>() ?? cdImgObj.AddComponent<RectTransform>();
        cdImgRt.anchorMin = new Vector2(0.5f, 0.5f);
        cdImgRt.anchorMax = new Vector2(0.5f, 0.5f);
        cdImgRt.anchoredPosition = Vector2.zero;
        cdImgRt.sizeDelta = new Vector2(115f, 95f);
        cdImgRt.localScale = Vector3.one;
        Image cdImg = cdImgObj.GetComponent<Image>() ?? cdImgObj.AddComponent<Image>();
        cdImg.sprite = cd3;
        cdImg.preserveAspect = true;
        cdImg.raycastTarget = false;

        GameObject txtVsObj = GetOrCreateChild(cdObj.transform, "Text_VS");
        RectTransform txtVsRt = txtVsObj.GetComponent<RectTransform>() ?? txtVsObj.AddComponent<RectTransform>();
        txtVsRt.anchorMin = Vector2.zero;
        txtVsRt.anchorMax = Vector2.one;
        txtVsRt.sizeDelta = Vector2.zero;
        txtVsRt.anchoredPosition = Vector2.zero;
        txtVsRt.localScale = Vector3.one;
        TextMeshProUGUI txtVs = txtVsObj.GetComponent<TextMeshProUGUI>() ?? txtVsObj.AddComponent<TextMeshProUGUI>();
        if (font != null) txtVs.font = font;
        txtVs.text = "VS";
        txtVs.fontSize = 44f;
        txtVs.fontStyle = FontStyles.Bold;
        txtVs.color = new Color(1f, 0.88f, 0.2f, 1f);
        txtVs.alignment = TextAlignmentOptions.Center;
        txtVs.raycastTarget = false;
        txtVsObj.SetActive(false); // Countdown image is primary

        // Bind serialized references to BattelVS component on root
        BattelVS bvs = root.GetComponent<BattelVS>() ?? root.gameObject.AddComponent<BattelVS>();
        SerializedObject so = new SerializedObject(bvs);

        so.FindProperty("panelMask").objectReferenceValue = maskRt;
        so.FindProperty("bgTopRed").objectReferenceValue = bgRedRt;
        so.FindProperty("bgBottomBlue").objectReferenceValue = bgBlueRt;
        so.FindProperty("seamContainer").objectReferenceValue = seamRt;
        so.FindProperty("seamLine").objectReferenceValue = lineImg;

        SerializedProperty silhouettesProp = so.FindProperty("battleSilhouettes");
        if (silhouettesProp != null)
        {
            silhouettesProp.arraySize = 5;
            silhouettesProp.GetArrayElementAtIndex(0).objectReferenceValue = icon1;
            silhouettesProp.GetArrayElementAtIndex(1).objectReferenceValue = icon2;
            silhouettesProp.GetArrayElementAtIndex(2).objectReferenceValue = icon3;
            silhouettesProp.GetArrayElementAtIndex(3).objectReferenceValue = icon4;
            silhouettesProp.GetArrayElementAtIndex(4).objectReferenceValue = icon5;
        }

        so.FindProperty("containerPlayer2").objectReferenceValue = p2Rt;
        so.FindProperty("avatarRed").objectReferenceValue = avRedImg;
        so.FindProperty("nameplateRed").objectReferenceValue = npRedImg;
        so.FindProperty("leagueShieldRed").objectReferenceValue = shRedImg;
        so.FindProperty("levelMedallionRed").objectReferenceValue = medRedImg;
        so.FindProperty("textPlayer2Name").objectReferenceValue = txtP2Name;
        so.FindProperty("textPlayer2Level").objectReferenceValue = txtP2Lvl;

        so.FindProperty("containerPlayer1").objectReferenceValue = p1Rt;
        so.FindProperty("avatarBlue").objectReferenceValue = avBlueImg;
        so.FindProperty("nameplateBlue").objectReferenceValue = npBlueImg;
        so.FindProperty("leagueShieldBlue").objectReferenceValue = shBlueImg;
        so.FindProperty("levelMedallionBlue").objectReferenceValue = medBlueImg;
        so.FindProperty("textPlayer1Name").objectReferenceValue = txtP1Name;
        so.FindProperty("textPlayer1Level").objectReferenceValue = txtP1Lvl;

        so.FindProperty("countdownContainer").objectReferenceValue = cdRt;
        so.FindProperty("countdownImage").objectReferenceValue = cdImg;
        so.FindProperty("textVS").objectReferenceValue = txtVs;

        so.FindProperty("spriteCountdown3").objectReferenceValue = cd3;
        so.FindProperty("spriteCountdown2").objectReferenceValue = cd2;
        so.FindProperty("spriteCountdown1").objectReferenceValue = cd1;
        so.FindProperty("spriteCountdownStart").objectReferenceValue = cdStart;

        so.FindProperty("spriteLeagueStarterWood").objectReferenceValue = LoadSprite("league_starter_wood.png");
        so.FindProperty("spriteLeagueSilver").objectReferenceValue = shSilver;
        so.FindProperty("spriteLeagueGold").objectReferenceValue = shGold;
        so.FindProperty("spriteLeagueLegend").objectReferenceValue = LoadSprite("league_legend.png");
        so.FindProperty("spriteLeagueChampion").objectReferenceValue = LoadSprite("league_champion.png");

        so.ApplyModifiedProperties();
    }

    private static Image CreateSilhouette(Transform parent, string name, Sprite sprite, Vector2 pos, Vector2 size)
    {
        GameObject obj = GetOrCreateChild(parent, name);
        RectTransform rt = obj.GetComponent<RectTransform>() ?? obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.localEulerAngles = Vector3.zero;
        rt.localScale = Vector3.one;

        Image img = obj.GetComponent<Image>() ?? obj.AddComponent<Image>();
        img.sprite = sprite;
        img.color = Color.white;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
    }

    private static GameObject GetOrCreateChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child == null)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        if (child.GetComponent<RectTransform>() == null)
        {
            child.gameObject.AddComponent<RectTransform>();
        }
        return child.gameObject;
    }

    private static Sprite LoadSprite(string fileName)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(ASSET_PATH + fileName);
    }
}
