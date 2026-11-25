using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using UnityEditor.SceneManagement;

/// <summary>
/// Editor setup tool to construct and wire the concrete Acquisition UI GameObjects directly in the Scene:
/// 1. Screen-Space HUD Toast in Canvas (Canvas/TowerAcquisitionToast)
/// 2. 3D World-Space Floating Billboard Banner on Wall Towers (walltowerour/TowerAcquisitionBanner & walltowerenemy/TowerAcquisitionBanner)
/// 3. Saves a reusable prefab: Assets/prefabs/TowerAcquisitionBanner.prefab
/// 
/// Runs automatically on script reload and via menu items so designers can modify layouts,
/// colors, fonts, and sprites directly in the Unity Inspector and Scene view!
/// </summary>
public static class TowerAcquisitionSetupTool
{
    [MenuItem("Tools/Battlefield/Setup Tower Acquisition UI in Scene")]
    public static void ManualSetup()
    {
        SetupAcquisitionUIInScene(silent: false);
    }

    [MenuItem("Tools/Battlefield/Show Acquisition UI in Scene (Edit Mode)")]
    public static void ShowUIInEditMode()
    {
        var toast = Object.FindFirstObjectByType<TowerAcquisitionUI>(FindObjectsInactive.Include);
        if (toast != null) toast.ShowInSceneEditMode();

        var banners = Object.FindObjectsByType<TowerWorldBanner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var b in banners) b.ShowInSceneEditMode();

        SceneView.RepaintAll();
        Debug.Log("<color=green>[TowerAcquisitionSetupTool] Acquisition UI elements now VISIBLE in Scene View for editing!</color>");
    }

    [MenuItem("Tools/Battlefield/Hide Acquisition UI (Ready for Play)")]
    public static void HideUIForPlay()
    {
        var toast = Object.FindFirstObjectByType<TowerAcquisitionUI>(FindObjectsInactive.Include);
        if (toast != null) toast.HideToastImmediate();

        var banners = Object.FindObjectsByType<TowerWorldBanner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var b in banners) b.HideImmediate();

        SceneView.RepaintAll();
        Debug.Log("<color=yellow>[TowerAcquisitionSetupTool] Acquisition UI elements HIDDEN and ready for gameplay captures.</color>");
    }

    public static void SetupAcquisitionUIInScene(bool silent = false)
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        if (!activeScene.isLoaded) return;

        bool sceneModified = false;

        // 1. Setup Screen Toast in Canvas
        sceneModified |= SetupScreenToastInCanvas(silent);

        // 2. Setup World Banners on Wall Towers
        sceneModified |= SetupWorldBannersOnWallTowers(silent);

        if (sceneModified && !Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            if (!silent)
            {
                Debug.Log("<color=green>[TowerAcquisitionSetupTool] ✓ Tower Acquisition UI successfully set up in scene and saved to disk!</color>");
            }
        }
    }

    #region 1. Screen-Space HUD Toast in Canvas

    private static bool SetupScreenToastInCanvas(bool silent)
    {
        Canvas screenCanvas = FindScreenCanvasInScene();
        if (screenCanvas == null)
        {
            if (!silent) Debug.LogWarning("[TowerAcquisitionSetupTool] Could not find a screen Canvas in scene.");
            return false;
        }

        bool modified = false;
        Transform toastTr = screenCanvas.transform.Find("TowerAcquisitionToast");
        GameObject toastGo;

        if (toastTr == null)
        {
            toastGo = new GameObject("TowerAcquisitionToast", typeof(RectTransform), typeof(CanvasGroup), typeof(TowerAcquisitionUI));
            Undo.RegisterCreatedObjectUndo(toastGo, "Create TowerAcquisitionToast");
            toastGo.transform.SetParent(screenCanvas.transform, false);
            toastTr = toastGo.transform;
            modified = true;
        }
        else
        {
            toastGo = toastTr.gameObject;
        }

        // RectTransform configuration
        RectTransform toastRect = toastGo.GetComponent<RectTransform>();
        toastRect.anchorMin = new Vector2(0.5f, 1f);
        toastRect.anchorMax = new Vector2(0.5f, 1f);
        toastRect.pivot = new Vector2(0.5f, 1f);
        toastRect.sizeDelta = new Vector2(480f, 72f);
        toastRect.anchoredPosition = new Vector2(0f, -38f); // Resting visible position in scene view

        CanvasGroup cg = toastGo.GetComponent<CanvasGroup>();
        if (cg == null) cg = toastGo.AddComponent<CanvasGroup>();
        cg.alpha = 1f; // Visible for easy editing in Edit Mode
        cg.blocksRaycasts = false;

        TowerAcquisitionUI ui = toastGo.GetComponent<TowerAcquisitionUI>();
        if (ui == null) ui = toastGo.AddComponent<TowerAcquisitionUI>();

        // Load Sprites and Fonts
        Sprite blueRibbon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/battlevs/arena_matchmaking_transparent_sprites/Banners/blue_name_ribbon.png");
        if (blueRibbon == null)
            blueRibbon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Label/Label_Ribbon01_Blue.png");

        Sprite redRibbon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/battlevs/arena_matchmaking_transparent_sprites/Banners/red_name_ribbon.png");
        if (redRibbon == null)
            redRibbon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Label/Label_Ribbon01_Red.png");

        Sprite yellowRibbon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Label/Label_Ribbon01_Yellow.png");

        Sprite crownSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/battlevs/arena_matchmaking_transparent_sprites/Icons/crown.png");
        if (crownSprite == null)
            crownSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Icon_ItemIcons/128/ItemIcon_Badge_Crown.png");

        Sprite shieldSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/IconMisc/Icon_Shield01.png");
        Sprite swordsSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Icon_ItemIcons/128/ItemIcon_Weapon_SwordShield.png");
        Sprite starSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/IconMisc/Icon_Star01_l.png");
        Sprite auraGlowSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Demo/Demo_Image/Glow01.png");

        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        GameObject levelUpVfx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonParticles/Prefabs/FX_LevelUp_01.prefab");
        GameObject fireworksVfx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonParticles/Prefabs/FX_Fireworks_Group_01.prefab");
        AudioClip fanfare = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audioMohamad/nhlk-revolution-108386.mp3");

        // 1. Toast Background (Ribbon Image)
        Transform bgTr = toastTr.Find("ToastBg");
        GameObject bgGo = (bgTr != null) ? bgTr.gameObject : new GameObject("ToastBg", typeof(RectTransform), typeof(Image));
        if (bgTr == null)
        {
            bgGo.transform.SetParent(toastTr, false);
            modified = true;
        }
        RectTransform bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        Image bgImg = bgGo.GetComponent<Image>();
        bgImg.type = Image.Type.Sliced;
        if (blueRibbon != null)
        {
            bgImg.sprite = blueRibbon;
            bgImg.color = Color.white;
        }
        else
        {
            bgImg.color = new Color(0.04f, 0.52f, 0.89f, 1f);
        }

        // 2. Left Crest Icon
        Transform leftIconTr = toastTr.Find("LeftIcon");
        GameObject leftIconGo = (leftIconTr != null) ? leftIconTr.gameObject : new GameObject("LeftIcon", typeof(RectTransform), typeof(Image));
        if (leftIconTr == null)
        {
            leftIconGo.transform.SetParent(toastTr, false);
            modified = true;
        }
        RectTransform lRect = leftIconGo.GetComponent<RectTransform>();
        lRect.anchorMin = new Vector2(0f, 0.5f);
        lRect.anchorMax = new Vector2(0f, 0.5f);
        lRect.pivot = new Vector2(0.5f, 0.5f);
        lRect.sizeDelta = new Vector2(46f, 46f);
        lRect.anchoredPosition = new Vector2(42f, 0f);
        Image lImg = leftIconGo.GetComponent<Image>();
        if (crownSprite != null) lImg.sprite = crownSprite;
        lImg.color = Color.white;

        // 3. Right Crest Icon
        Transform rightIconTr = toastTr.Find("RightIcon");
        GameObject rightIconGo = (rightIconTr != null) ? rightIconTr.gameObject : new GameObject("RightIcon", typeof(RectTransform), typeof(Image));
        if (rightIconTr == null)
        {
            rightIconGo.transform.SetParent(toastTr, false);
            modified = true;
        }
        RectTransform rRect = rightIconGo.GetComponent<RectTransform>();
        rRect.anchorMin = new Vector2(1f, 0.5f);
        rRect.anchorMax = new Vector2(1f, 0.5f);
        rRect.pivot = new Vector2(0.5f, 0.5f);
        rRect.sizeDelta = new Vector2(46f, 46f);
        rRect.anchoredPosition = new Vector2(-42f, 0f);
        Image rImg = rightIconGo.GetComponent<Image>();
        if (crownSprite != null) rImg.sprite = crownSprite;
        rImg.color = Color.white;

        // 4. Toast Title
        Transform titleTr = toastTr.Find("ToastTitle");
        GameObject titleGo = (titleTr != null) ? titleTr.gameObject : new GameObject("ToastTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        if (titleTr == null)
        {
            titleGo.transform.SetParent(toastTr, false);
            modified = true;
        }
        RectTransform titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.5f);
        titleRect.anchorMax = new Vector2(1f, 0.5f);
        titleRect.sizeDelta = new Vector2(-130f, 34f);
        titleRect.anchoredPosition = new Vector2(0f, 10f);
        TextMeshProUGUI titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null) titleTmp.font = fontAsset;
        titleTmp.text = "★ TOWER ACQUIRED! ★";
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.fontSize = 24f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = Color.white;
        titleTmp.outlineWidth = 0.35f;
        titleTmp.outlineColor = Color.black;

        // 5. Toast Subtitle
        Transform subTr = toastTr.Find("ToastSubtitle");
        GameObject subGo = (subTr != null) ? subTr.gameObject : new GameObject("ToastSubtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        if (subTr == null)
        {
            subGo.transform.SetParent(toastTr, false);
            modified = true;
        }
        RectTransform subRect = subGo.GetComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0f, 0.5f);
        subRect.anchorMax = new Vector2(1f, 0.5f);
        subRect.sizeDelta = new Vector2(-140f, 22f);
        subRect.anchoredPosition = new Vector2(0f, -16f);
        TextMeshProUGUI subTmp = subGo.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null) subTmp.font = fontAsset;
        subTmp.text = "DEFENSIVE OUTPOST ACTIVE";
        subTmp.alignment = TextAlignmentOptions.Center;
        subTmp.fontSize = 13f;
        subTmp.fontStyle = FontStyles.Bold;
        subTmp.color = new Color(0.95f, 0.77f, 0.25f, 1f); // Royal Gold
        subTmp.outlineWidth = 0.25f;
        subTmp.outlineColor = Color.black;

        // 6. Shine Sweep Slice
        Transform shineTr = toastTr.Find("ShineSlice");
        GameObject shineGo = (shineTr != null) ? shineTr.gameObject : new GameObject("ShineSlice", typeof(RectTransform), typeof(Image));
        if (shineTr == null)
        {
            shineGo.transform.SetParent(toastTr, false);
            modified = true;
        }
        RectTransform sRect = shineGo.GetComponent<RectTransform>();
        sRect.sizeDelta = new Vector2(55f, 72f);
        sRect.anchoredPosition = new Vector2(-260f, 0f);
        Image sImg = shineGo.GetComponent<Image>();
        sImg.color = new Color(1f, 1f, 1f, 0.35f);
        sImg.raycastTarget = false;

        // Ensure toast renders on top
        toastTr.SetAsLastSibling();

        // Wire serialized fields on TowerAcquisitionUI
        SerializedObject so = new SerializedObject(ui);
        so.FindProperty("toastRect").objectReferenceValue = toastRect;
        so.FindProperty("toastCanvasGroup").objectReferenceValue = cg;
        so.FindProperty("bgImage").objectReferenceValue = bgImg;
        so.FindProperty("leftIcon").objectReferenceValue = lImg;
        so.FindProperty("rightIcon").objectReferenceValue = rImg;
        so.FindProperty("titleText").objectReferenceValue = titleTmp;
        so.FindProperty("subtitleText").objectReferenceValue = subTmp;
        so.FindProperty("shineSlice").objectReferenceValue = sRect;

        so.FindProperty("blueRibbonSprite").objectReferenceValue = blueRibbon;
        so.FindProperty("redRibbonSprite").objectReferenceValue = redRibbon;
        so.FindProperty("yellowRibbonSprite").objectReferenceValue = yellowRibbon;
        so.FindProperty("crownIconSprite").objectReferenceValue = crownSprite;
        so.FindProperty("shieldIconSprite").objectReferenceValue = shieldSprite;
        so.FindProperty("swordsIconSprite").objectReferenceValue = swordsSprite;
        so.FindProperty("starIconSprite").objectReferenceValue = starSprite;
        so.FindProperty("auraGlowSprite").objectReferenceValue = auraGlowSprite;
        so.FindProperty("fontAsset").objectReferenceValue = fontAsset;

        so.FindProperty("levelUpVfxPrefab").objectReferenceValue = levelUpVfx;
        so.FindProperty("fireworksVfxPrefab").objectReferenceValue = fireworksVfx;
        so.FindProperty("fanfareSound").objectReferenceValue = fanfare;

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(toastGo);

        if (!silent)
        {
            Debug.Log("<color=cyan>[TowerAcquisitionSetupTool] ✓ Configured Canvas/TowerAcquisitionToast in scene.</color>");
        }

        return modified;
    }

    #endregion

    #region 2. 3D World-Space Floating Billboard Banner on Wall Towers

    private static bool SetupWorldBannersOnWallTowers(bool silent)
    {
        var allTowers = Object.FindObjectsByType<TowerCombatController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (allTowers == null || allTowers.Length == 0) return false;

        bool modified = false;

        // Load common assets
        Sprite blueRibbon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/battlevs/arena_matchmaking_transparent_sprites/Banners/blue_name_ribbon.png");
        if (blueRibbon == null)
            blueRibbon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Label/Label_Ribbon01_Blue.png");

        Sprite redRibbon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/battlevs/arena_matchmaking_transparent_sprites/Banners/red_name_ribbon.png");
        if (redRibbon == null)
            redRibbon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Label/Label_Ribbon01_Red.png");

        Sprite yellowRibbon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Label/Label_Ribbon01_Yellow.png");

        Sprite crownSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/battlevs/arena_matchmaking_transparent_sprites/Icons/crown.png");
        if (crownSprite == null)
            crownSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Icon_ItemIcons/128/ItemIcon_Badge_Crown.png");

        Sprite shieldSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/IconMisc/Icon_Shield01.png");
        Sprite swordsSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/Icon_ItemIcons/128/ItemIcon_Weapon_SwordShield.png");
        Sprite starSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Components/IconMisc/Icon_Star01_l.png");
        Sprite auraGlowSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/ResourcesData/Sprites/Demo/Demo_Image/Glow01.png");

        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

        foreach (var tower in allTowers)
        {
            string towerName = tower.gameObject.name.ToLower();
            // Focus on capturable wall towers (or all towers if capturable)
            if (towerName.Contains("wall") || tower.gameObject.name.StartsWith("walltower"))
            {
                modified |= SetupSingleTowerBanner(tower, blueRibbon, redRibbon, yellowRibbon, crownSprite, shieldSprite, swordsSprite, starSprite, auraGlowSprite, fontAsset, silent);
            }
        }

        return modified;
    }

    private static bool SetupSingleTowerBanner(
        TowerCombatController tower,
        Sprite blueRibbon, Sprite redRibbon, Sprite yellowRibbon,
        Sprite crownSprite, Sprite shieldSprite, Sprite swordsSprite,
        Sprite starSprite, Sprite auraGlowSprite, TMP_FontAsset fontAsset,
        bool silent)
    {
        bool modified = false;
        Transform bannerTr = tower.transform.Find("TowerAcquisitionBanner");
        GameObject bannerGo;

        if (bannerTr == null)
        {
            bannerGo = new GameObject("TowerAcquisitionBanner", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(CanvasScaler), typeof(TowerWorldBanner));
            Undo.RegisterCreatedObjectUndo(bannerGo, $"Create TowerAcquisitionBanner on {tower.name}");
            bannerGo.transform.SetParent(tower.transform, false);
            bannerTr = bannerGo.transform;
            modified = true;
        }
        else
        {
            bannerGo = bannerTr.gameObject;
        }

        // Configure World Space Canvas
        Canvas canvas = bannerGo.GetComponent<Canvas>();
        if (canvas == null) canvas = bannerGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = bannerGo.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = bannerGo.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        CanvasGroup cg = bannerGo.GetComponent<CanvasGroup>();
        if (cg == null) cg = bannerGo.AddComponent<CanvasGroup>();
        cg.alpha = 1f; // Visible for easy editing in scene view!

        TowerWorldBanner banner = bannerGo.GetComponent<TowerWorldBanner>();
        if (banner == null) banner = bannerGo.AddComponent<TowerWorldBanner>();

        // Height offset: 11.2m above tower base (just above walkway and battlements)
        float heightOffset = tower.PlatformHeightOffset + 3.8f;
        if (heightOffset < 11.2f) heightOffset = 11.2f;

        bannerTr.localPosition = new Vector3(0f, heightOffset, 0f);
        bannerTr.localRotation = Quaternion.identity;

        // Container
        Transform containerTr = bannerTr.Find("BannerContainer");
        GameObject containerGo = (containerTr != null) ? containerTr.gameObject : new GameObject("BannerContainer", typeof(RectTransform));
        if (containerTr == null)
        {
            containerGo.transform.SetParent(bannerTr, false);
            containerTr = containerGo.transform;
            modified = true;
        }
        RectTransform containerRect = containerGo.GetComponent<RectTransform>();
        containerRect.sizeDelta = new Vector2(300f, 140f);
        containerRect.anchoredPosition = Vector2.zero;

        // Calculate normalized world scale
        Vector3 parentLossy = tower.transform.lossyScale;
        float px = Mathf.Abs(parentLossy.x) > 0.001f ? parentLossy.x : 1f;
        float py = Mathf.Abs(parentLossy.y) > 0.001f ? parentLossy.y : 1f;
        float pz = Mathf.Abs(parentLossy.z) > 0.001f ? parentLossy.z : 1f;
        float baseScale = 4.2f / 300f; // ~0.014f
        containerRect.localScale = new Vector3(baseScale / px, baseScale / py, baseScale / pz);

        // 1. Rotating Radial Aura Glow
        Transform auraTr = containerTr.Find("AuraGlow");
        GameObject auraGo = (auraTr != null) ? auraTr.gameObject : new GameObject("AuraGlow", typeof(RectTransform), typeof(Image));
        if (auraTr == null)
        {
            auraGo.transform.SetParent(containerTr, false);
            modified = true;
        }
        RectTransform auraRect = auraGo.GetComponent<RectTransform>();
        auraRect.sizeDelta = new Vector2(175f, 175f);
        auraRect.anchoredPosition = new Vector2(0f, 22f);
        Image auraImg = auraGo.GetComponent<Image>();
        if (auraGlowSprite != null) auraImg.sprite = auraGlowSprite;
        auraImg.color = new Color(0.2f, 0.75f, 1f, 0.75f);

        // 2. Ribbon Background
        Transform ribbonTr = containerTr.Find("Ribbon");
        GameObject ribbonGo = (ribbonTr != null) ? ribbonTr.gameObject : new GameObject("Ribbon", typeof(RectTransform), typeof(Image));
        if (ribbonTr == null)
        {
            ribbonGo.transform.SetParent(containerTr, false);
            modified = true;
        }
        RectTransform ribbonRect = ribbonGo.GetComponent<RectTransform>();
        ribbonRect.sizeDelta = new Vector2(300f, 75f);
        ribbonRect.anchoredPosition = new Vector2(0f, -5f);
        Image ribbonImg = ribbonGo.GetComponent<Image>();
        ribbonImg.type = Image.Type.Sliced;
        if (blueRibbon != null)
        {
            ribbonImg.sprite = blueRibbon;
            ribbonImg.color = Color.white;
        }
        else
        {
            ribbonImg.color = new Color(0.04f, 0.52f, 0.89f, 1f);
        }

        // 3. Center Crest Icon (Crown / Shield)
        Transform emblemTr = containerTr.Find("EmblemIcon");
        GameObject emblemGo = (emblemTr != null) ? emblemTr.gameObject : new GameObject("EmblemIcon", typeof(RectTransform), typeof(Image));
        if (emblemTr == null)
        {
            emblemGo.transform.SetParent(containerTr, false);
            modified = true;
        }
        RectTransform emblemRect = emblemGo.GetComponent<RectTransform>();
        emblemRect.sizeDelta = new Vector2(62f, 62f);
        emblemRect.anchoredPosition = new Vector2(0f, 32f);
        Image emblemImg = emblemGo.GetComponent<Image>();
        if (crownSprite != null) emblemImg.sprite = crownSprite;
        emblemImg.color = Color.white;

        // 4. Flanking Stars
        Transform starLTr = containerTr.Find("StarLeft");
        GameObject starLGo = (starLTr != null) ? starLTr.gameObject : new GameObject("StarLeft", typeof(RectTransform), typeof(Image));
        if (starLTr == null)
        {
            starLGo.transform.SetParent(containerTr, false);
            modified = true;
        }
        RectTransform starLRect = starLGo.GetComponent<RectTransform>();
        starLRect.sizeDelta = new Vector2(24f, 24f);
        starLRect.anchoredPosition = new Vector2(-105f, 0f);
        Image starLImg = starLGo.GetComponent<Image>();
        if (starSprite != null) starLImg.sprite = starSprite;
        starLImg.color = new Color(0.95f, 0.77f, 0.25f, 1f);

        Transform starRTr = containerTr.Find("StarRight");
        GameObject starRGo = (starRTr != null) ? starRTr.gameObject : new GameObject("StarRight", typeof(RectTransform), typeof(Image));
        if (starRTr == null)
        {
            starRGo.transform.SetParent(containerTr, false);
            modified = true;
        }
        RectTransform starRRect = starRGo.GetComponent<RectTransform>();
        starRRect.sizeDelta = new Vector2(24f, 24f);
        starRRect.anchoredPosition = new Vector2(105f, 0f);
        Image starRImg = starRGo.GetComponent<Image>();
        if (starSprite != null) starRImg.sprite = starSprite;
        starRImg.color = new Color(0.95f, 0.77f, 0.25f, 1f);

        // 5. Title Text
        Transform titleTr = containerTr.Find("TitleText");
        GameObject titleGo = (titleTr != null) ? titleTr.gameObject : new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        if (titleTr == null)
        {
            titleGo.transform.SetParent(containerTr, false);
            modified = true;
        }
        RectTransform titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.sizeDelta = new Vector2(280f, 38f);
        titleRect.anchoredPosition = new Vector2(0f, 2f);
        TextMeshProUGUI titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null) titleTmp.font = fontAsset;
        titleTmp.text = "TOWER ACQUIRED!";
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.fontSize = 24f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = Color.white;
        titleTmp.outlineWidth = 0.32f;
        titleTmp.outlineColor = new Color(0.05f, 0.05f, 0.08f, 0.95f);

        // 6. Subtitle Text
        Transform subTr = containerTr.Find("SubtitleText");
        GameObject subGo = (subTr != null) ? subTr.gameObject : new GameObject("SubtitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        if (subTr == null)
        {
            subGo.transform.SetParent(containerTr, false);
            modified = true;
        }
        RectTransform subRect = subGo.GetComponent<RectTransform>();
        subRect.sizeDelta = new Vector2(270f, 22f);
        subRect.anchoredPosition = new Vector2(0f, -22f);
        TextMeshProUGUI subTmp = subGo.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null) subTmp.font = fontAsset;
        subTmp.text = "★ DEFENSIVE OUTPOST ACTIVE ★";
        subTmp.alignment = TextAlignmentOptions.Center;
        subTmp.fontSize = 12f;
        subTmp.fontStyle = FontStyles.Bold;
        subTmp.color = new Color(0.95f, 0.77f, 0.25f, 1f); // Royal Gold
        subTmp.outlineWidth = 0.2f;
        subTmp.outlineColor = Color.black;

        // Wire serialized fields on TowerWorldBanner
        SerializedObject bSo = new SerializedObject(banner);
        bSo.FindProperty("worldCanvas").objectReferenceValue = canvas;
        bSo.FindProperty("canvasGroup").objectReferenceValue = cg;
        bSo.FindProperty("bannerContainer").objectReferenceValue = containerRect;
        bSo.FindProperty("auraRect").objectReferenceValue = auraRect;
        bSo.FindProperty("auraImage").objectReferenceValue = auraImg;
        bSo.FindProperty("ribbonImage").objectReferenceValue = ribbonImg;
        bSo.FindProperty("emblemImage").objectReferenceValue = emblemImg;
        bSo.FindProperty("starLeft").objectReferenceValue = starLImg;
        bSo.FindProperty("starRight").objectReferenceValue = starRImg;
        bSo.FindProperty("titleText").objectReferenceValue = titleTmp;
        bSo.FindProperty("subtitleText").objectReferenceValue = subTmp;

        bSo.FindProperty("heightOffset").floatValue = heightOffset;
        bSo.FindProperty("playerRibbonSprite").objectReferenceValue = blueRibbon;
        bSo.FindProperty("enemyRibbonSprite").objectReferenceValue = redRibbon;
        bSo.FindProperty("neutralRibbonSprite").objectReferenceValue = yellowRibbon;
        bSo.FindProperty("crownIconSprite").objectReferenceValue = crownSprite;
        bSo.FindProperty("shieldIconSprite").objectReferenceValue = shieldSprite;
        bSo.FindProperty("swordsIconSprite").objectReferenceValue = swordsSprite;
        bSo.FindProperty("starSprite").objectReferenceValue = starSprite;
        bSo.FindProperty("auraGlowSprite").objectReferenceValue = auraGlowSprite;

        bSo.ApplyModifiedProperties();
        EditorUtility.SetDirty(bannerGo);

        // Link on TowerCombatController
        SerializedObject tSo = new SerializedObject(tower);
        tSo.FindProperty("worldBanner").objectReferenceValue = banner;
        tSo.ApplyModifiedProperties();
        EditorUtility.SetDirty(tower);

        if (!silent)
        {
            Debug.Log($"<color=cyan>[TowerAcquisitionSetupTool] ✓ Configured TowerAcquisitionBanner on {tower.name}.</color>");
        }

        // Save reusable prefab as well
        string prefabPath = "Assets/prefabs/TowerAcquisitionBanner.prefab";
        try
        {
            PrefabUtility.SaveAsPrefabAssetAndConnect(bannerGo, prefabPath, InteractionMode.AutomatedAction);
        }
        catch (System.Exception ex)
        {
            if (!silent) Debug.LogWarning($"[TowerAcquisitionSetupTool] Could not save prefab to {prefabPath}: {ex.Message}");
        }

        return true;
    }

    #endregion

    private static Canvas FindScreenCanvasInScene()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in canvases)
        {
            if (c.renderMode != RenderMode.WorldSpace && c.gameObject.name == "Canvas")
                return c;
        }
        foreach (var c in canvases)
        {
            if (c.renderMode != RenderMode.WorldSpace)
                return c;
        }
        return null;
    }
}
