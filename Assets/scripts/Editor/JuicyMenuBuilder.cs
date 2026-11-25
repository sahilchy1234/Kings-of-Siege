#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Professional Editor tool that rebuilds and redesigns MainPanel and MatchmakingPanel
/// in the MainMenu scene with the stylized medieval UIPACK assets and wires up DOTween juice.
/// </summary>
public static class JuicyMenuBuilder
{
    private const string MAIN_MENU_SCENE_PATH = "Assets/Scenes/MainMenu.unity";

    // Sprite Asset Paths
    private const string POPUP_BG_PATH = "Assets/UIPACK/ResourcesData/Sprites/Components/Popup/popup02_Demo1.png";
    private const string RIBBON_YELLOW_PATH = "Assets/UIPACK/ResourcesData/Sprites/Components/Label/Label_Ribbon01_Yellow.png";
    private const string RIBBON_RED_PATH = "Assets/UIPACK/ResourcesData/Sprites/Components/Label/Label_Ribbon01_Red.png";
    private const string BTN_BLUE_PATH = "Assets/UIPACK/ResourcesData/Sprites/Components/Button/Button01_Demo_Blue.png";
    private const string BTN_GREEN_PATH = "Assets/UIPACK/ResourcesData/Sprites/Components/Button/Button01_Demo_Green.png";
    private const string BTN_RED_PATH = "Assets/UIPACK/ResourcesData/Sprites/Components/Button/Button01_Demo_Red.png";
    private const string FRAME_SQUARE_PATH = "Assets/UIPACK/ResourcesData/Sprites/Components/Frame/BasicFrame_SquareOutline02_Demo01.png";
    private const string FRAME_CIRCLE_PATH = "Assets/UIPACK/ResourcesData/Sprites/Components/Frame/BasicFrame_CircleOutline02_Demo.png";
    private const string GLOW_PATH = "Assets/UIPACK/ResourcesData/Sprites/Demo/Demo_Image/Glow01.png";
    private const string CROWN_PATH = "Assets/UIPACK/ResourcesData/Sprites/Demo/Demo_Image/Image_Crown.png";
    private const string DIM_BG_PATH = "Assets/UIPACK/ResourcesData/Sprites/Demo/Demo_Background/Panel_Dimed.png";
    private const string SWORDS_SHIELD_PATH = "Assets/UIPACK/battlevs/png/battle_crossed_swords_shield.png";
    private const string AVATAR_BLUE_PATH = "Assets/UIPACK/battlevs/png/avatar_blue.png";
    private const string AVATAR_RED_PATH = "Assets/UIPACK/battlevs/png/avatar_red.png";
    private const string DOT_PATH = "Assets/UIPACK/ResourcesData/Sprites/Components/UI_Etc/Alert_Dot_White.png";

    // Font Paths
    private const string FONT_TILTWARP_PATH = "Assets/UIPACK/ResourcesData/Fonts/TMP_TiltWarp.asset";
    private const string FONT_OUTLINE_PATH = "Assets/UIPACK/ResourcesData/Fonts/TMP_TiltWarp_Outline.asset";

    [MenuItem("Tools/SEIGE/Redesign Main & Matchmaking Panels (Juicy)", false, 20)]
    public static void RedesignPanelsInScene()
    {
        Debug.Log("<color=#00FFAA>[JuicyMenuBuilder] Starting Main Menu UI Redesign...</color>");

        // Ensure MainMenu scene is open
        if (EditorSceneManager.GetActiveScene().path != MAIN_MENU_SCENE_PATH)
        {
            if (File.Exists(MAIN_MENU_SCENE_PATH))
            {
                EditorSceneManager.OpenScene(MAIN_MENU_SCENE_PATH);
            }
            else
            {
                Debug.LogError("[JuicyMenuBuilder] Could not find MainMenu scene at " + MAIN_MENU_SCENE_PATH);
                return;
            }
        }

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[JuicyMenuBuilder] No Canvas found in MainMenu scene!");
            return;
        }

        // Load Sprites
        Sprite popupBg = AssetDatabase.LoadAssetAtPath<Sprite>(POPUP_BG_PATH);
        Sprite ribbonYellow = AssetDatabase.LoadAssetAtPath<Sprite>(RIBBON_YELLOW_PATH);
        Sprite ribbonRed = AssetDatabase.LoadAssetAtPath<Sprite>(RIBBON_RED_PATH);
        Sprite btnBlue = AssetDatabase.LoadAssetAtPath<Sprite>(BTN_BLUE_PATH);
        Sprite btnGreen = AssetDatabase.LoadAssetAtPath<Sprite>(BTN_GREEN_PATH);
        Sprite btnRed = AssetDatabase.LoadAssetAtPath<Sprite>(BTN_RED_PATH);
        Sprite frameSquare = AssetDatabase.LoadAssetAtPath<Sprite>(FRAME_SQUARE_PATH);
        Sprite frameCircle = AssetDatabase.LoadAssetAtPath<Sprite>(FRAME_CIRCLE_PATH);
        Sprite glow = AssetDatabase.LoadAssetAtPath<Sprite>(GLOW_PATH);
        Sprite crown = AssetDatabase.LoadAssetAtPath<Sprite>(CROWN_PATH);
        Sprite dimBg = AssetDatabase.LoadAssetAtPath<Sprite>(DIM_BG_PATH);
        Sprite swordsShield = AssetDatabase.LoadAssetAtPath<Sprite>(SWORDS_SHIELD_PATH);
        Sprite avatarBlue = AssetDatabase.LoadAssetAtPath<Sprite>(AVATAR_BLUE_PATH);
        Sprite avatarRed = AssetDatabase.LoadAssetAtPath<Sprite>(AVATAR_RED_PATH);
        Sprite dot = AssetDatabase.LoadAssetAtPath<Sprite>(DOT_PATH);

        // Load Fonts
        TMP_FontAsset fontTilt = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_TILTWARP_PATH);
        TMP_FontAsset fontOutline = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_OUTLINE_PATH);
        if (fontOutline == null) fontOutline = fontTilt;

        // Clean up old MainPanel and MatchmakingPanel
        Transform oldMain = canvas.transform.Find("MainPanel");
        if (oldMain != null) Undo.DestroyObjectImmediate(oldMain.gameObject);

        Transform oldMatch = canvas.transform.Find("MatchmakingPanel");
        if (oldMatch != null) Undo.DestroyObjectImmediate(oldMatch.gameObject);

        // ==========================================
        // 1. BUILD REDESIGNED MAIN PANEL
        // ==========================================
        GameObject mainPanel = new GameObject("MainPanel");
        Undo.RegisterCreatedObjectUndo(mainPanel, "Create MainPanel");
        mainPanel.transform.SetParent(canvas.transform, false);
        mainPanel.transform.SetAsLastSibling();

        RectTransform mainRect = mainPanel.AddComponent<RectTransform>();
        StretchFull(mainRect);

        CanvasGroup mainCg = mainPanel.AddComponent<CanvasGroup>();

        // Dimmer background
        Image mainDimImg = mainPanel.AddComponent<Image>();
        mainDimImg.sprite = dimBg;
        mainDimImg.type = Image.Type.Simple;
        mainDimImg.color = new Color(0.04f, 0.06f, 0.10f, 0.85f);

        // Aura Glow behind CenterCard
        GameObject mainGlowObj = new GameObject("CardAuraGlow");
        mainGlowObj.transform.SetParent(mainPanel.transform, false);
        Image mainGlowImg = mainGlowObj.AddComponent<Image>();
        mainGlowImg.sprite = glow;
        mainGlowImg.color = new Color(1f, 0.75f, 0.25f, 0.22f);
        RectTransform mainGlowRect = mainGlowObj.GetComponent<RectTransform>();
        mainGlowRect.anchorMin = new Vector2(0.5f, 0.5f);
        mainGlowRect.anchorMax = new Vector2(0.5f, 0.5f);
        mainGlowRect.sizeDelta = new Vector2(880, 960);

        // CenterCard
        GameObject centerCard = new GameObject("CenterCard");
        centerCard.transform.SetParent(mainPanel.transform, false);
        Image cardImg = centerCard.AddComponent<Image>();
        cardImg.sprite = popupBg;
        cardImg.type = Image.Type.Sliced;
        RectTransform cardRect = centerCard.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(680, 840);

        // Header Ribbon
        GameObject ribbonObj = new GameObject("HeaderRibbon");
        ribbonObj.transform.SetParent(centerCard.transform, false);
        Image ribbonImg = ribbonObj.AddComponent<Image>();
        ribbonImg.sprite = ribbonYellow;
        ribbonImg.type = Image.Type.Sliced;
        RectTransform ribbonRect = ribbonObj.GetComponent<RectTransform>();
        ribbonRect.anchorMin = new Vector2(0.5f, 0.5f);
        ribbonRect.anchorMax = new Vector2(0.5f, 0.5f);
        ribbonRect.anchoredPosition = new Vector2(0f, 385f);
        ribbonRect.sizeDelta = new Vector2(620, 110);

        // Crown Crest
        GameObject crownObj = new GameObject("CrownDeco");
        crownObj.transform.SetParent(ribbonObj.transform, false);
        Image crownImg = crownObj.AddComponent<Image>();
        crownImg.sprite = crown;
        crownImg.preserveAspect = true;
        RectTransform crownRect = crownObj.GetComponent<RectTransform>();
        crownRect.anchorMin = new Vector2(0.5f, 1f);
        crownRect.anchorMax = new Vector2(0.5f, 1f);
        crownRect.anchoredPosition = new Vector2(0f, 22f);
        crownRect.sizeDelta = new Vector2(80, 80);

        // Title Text
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(ribbonObj.transform, false);
        TextMeshProUGUI titleTxt = titleObj.AddComponent<TextMeshProUGUI>();
        titleTxt.text = "SIEGE: REALMS AT WAR";
        titleTxt.font = fontOutline;
        titleTxt.fontSize = 38;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.color = new Color(1f, 0.92f, 0.45f, 1f);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = new Vector2(20, 15);
        titleRect.offsetMax = new Vector2(-20, -10);

        // Subtitle Text
        GameObject subObj = new GameObject("SubtitleText");
        subObj.transform.SetParent(centerCard.transform, false);
        TextMeshProUGUI subTxt = subObj.AddComponent<TextMeshProUGUI>();
        subTxt.text = "REAL-TIME 1v1 CASTLE DEFENSE & STRATEGY";
        subTxt.font = fontTilt;
        subTxt.fontSize = 17;
        subTxt.fontStyle = FontStyles.Bold;
        subTxt.alignment = TextAlignmentOptions.Center;
        subTxt.color = new Color(0.80f, 0.88f, 0.98f, 0.9f);
        RectTransform subRect = subObj.GetComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0.5f, 0.5f);
        subRect.anchorMax = new Vector2(0.5f, 0.5f);
        subRect.anchoredPosition = new Vector2(0f, 310f);
        subRect.sizeDelta = new Vector2(620, 35);

        // Commander Profile Box
        GameObject profileCard = new GameObject("CommanderProfileBox");
        profileCard.transform.SetParent(centerCard.transform, false);
        Image profBg = profileCard.AddComponent<Image>();
        profBg.sprite = frameSquare;
        profBg.type = Image.Type.Sliced;
        profBg.color = new Color(0.10f, 0.14f, 0.22f, 0.95f);
        RectTransform profRect = profileCard.GetComponent<RectTransform>();
        profRect.anchorMin = new Vector2(0.5f, 0.5f);
        profRect.anchorMax = new Vector2(0.5f, 0.5f);
        profRect.anchoredPosition = new Vector2(0f, 205f);
        profRect.sizeDelta = new Vector2(540, 115);

        // Profile Avatar Frame & Icon
        GameObject avFrameObj = new GameObject("AvatarFrame");
        avFrameObj.transform.SetParent(profileCard.transform, false);
        Image avFrameImg = avFrameObj.AddComponent<Image>();
        avFrameImg.sprite = frameCircle;
        avFrameImg.preserveAspect = true;
        RectTransform avFrameRect = avFrameObj.GetComponent<RectTransform>();
        avFrameRect.anchorMin = new Vector2(0f, 0.5f);
        avFrameRect.anchorMax = new Vector2(0f, 0.5f);
        avFrameRect.anchoredPosition = new Vector2(55f, 0f);
        avFrameRect.sizeDelta = new Vector2(85, 85);

        GameObject avIconObj = new GameObject("AvatarIcon");
        avIconObj.transform.SetParent(avFrameObj.transform, false);
        Image avIconImg = avIconObj.AddComponent<Image>();
        avIconImg.sprite = avatarBlue;
        avIconImg.preserveAspect = true;
        RectTransform avIconRect = avIconObj.GetComponent<RectTransform>();
        avIconRect.anchorMin = Vector2.zero;
        avIconRect.anchorMax = Vector2.one;
        avIconRect.offsetMin = new Vector2(6, 6);
        avIconRect.offsetMax = new Vector2(-6, -6);

        // Nickname Label
        GameObject nickLblObj = new GameObject("NickLabel");
        nickLblObj.transform.SetParent(profileCard.transform, false);
        TextMeshProUGUI nickLbl = nickLblObj.AddComponent<TextMeshProUGUI>();
        nickLbl.text = "COMMANDER NICKNAME";
        nickLbl.font = fontTilt;
        nickLbl.fontSize = 15;
        nickLbl.fontStyle = FontStyles.Bold;
        nickLbl.color = new Color(1f, 0.82f, 0.35f, 1f);
        RectTransform nickLblRect = nickLblObj.GetComponent<RectTransform>();
        nickLblRect.anchorMin = new Vector2(0f, 1f);
        nickLblRect.anchorMax = new Vector2(1f, 1f);
        nickLblRect.anchoredPosition = new Vector2(58f, -22f);
        nickLblRect.sizeDelta = new Vector2(400, 24);

        // Nickname Input Box
        GameObject inputObj = new GameObject("NicknameInputField");
        inputObj.transform.SetParent(profileCard.transform, false);
        Image inputBg = inputObj.AddComponent<Image>();
        inputBg.sprite = frameSquare;
        inputBg.type = Image.Type.Sliced;
        inputBg.color = new Color(0.06f, 0.08f, 0.14f, 1f);
        RectTransform inputRect = inputObj.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0f, 0f);
        inputRect.anchorMax = new Vector2(1f, 0f);
        inputRect.anchoredPosition = new Vector2(58f, 32f);
        inputRect.sizeDelta = new Vector2(400, 46);

        GameObject inputTextObj = new GameObject("Text");
        inputTextObj.transform.SetParent(inputObj.transform, false);
        TextMeshProUGUI inputText = inputTextObj.AddComponent<TextMeshProUGUI>();
        inputText.text = "Commander";
        inputText.font = fontTilt;
        inputText.fontSize = 22;
        inputText.fontStyle = FontStyles.Bold;
        inputText.alignment = TextAlignmentOptions.MidlineLeft;
        inputText.color = Color.white;
        RectTransform inputTRect = inputTextObj.GetComponent<RectTransform>();
        inputTRect.anchorMin = Vector2.zero;
        inputTRect.anchorMax = Vector2.one;
        inputTRect.offsetMin = new Vector2(14, 0);
        inputTRect.offsetMax = new Vector2(-14, 0);

        TMP_InputField inputField = inputObj.AddComponent<TMP_InputField>();
        inputField.textComponent = inputText;

        // Button 1: Play Multiplayer (1v1)
        GameObject multiBtnObj = CreateJuicyButton(centerCard.transform, "Btn_PlayMultiplayer", btnBlue,
            new Vector2(0f, 50f), new Vector2(540, 95), swordsShield, "MULTIPLAYER (1v1)", "REAL-TIME RANKED ARENA",
            new Color(0.35f, 0.85f, 1f, 1f), fontOutline, fontTilt);
        Button multiBtn = multiBtnObj.GetComponent<Button>();

        // Button 2: Play vs AI (Solo)
        GameObject aiBtnObj = CreateJuicyButton(centerCard.transform, "Btn_PlayVsAI", btnGreen,
            new Vector2(0f, -65f), new Vector2(540, 85), avatarRed, "PRACTICE VS AI", "OFFLINE SOLO COMBAT TRAINING",
            new Color(0.45f, 1f, 0.55f, 1f), fontOutline, fontTilt);
        Button aiBtn = aiBtnObj.GetComponent<Button>();

        // Connection Status Bar
        GameObject connPill = new GameObject("ConnectionStatusBar");
        connPill.transform.SetParent(centerCard.transform, false);
        Image connPillBg = connPill.AddComponent<Image>();
        connPillBg.sprite = frameSquare;
        connPillBg.type = Image.Type.Sliced;
        connPillBg.color = new Color(0.08f, 0.11f, 0.18f, 0.95f);
        RectTransform connPillRect = connPill.GetComponent<RectTransform>();
        connPillRect.anchorMin = new Vector2(0.5f, 0.5f);
        connPillRect.anchorMax = new Vector2(0.5f, 0.5f);
        connPillRect.anchoredPosition = new Vector2(0f, -170f);
        connPillRect.sizeDelta = new Vector2(540, 44);

        // Status Dot
        GameObject dotObj = new GameObject("StatusDot");
        dotObj.transform.SetParent(connPill.transform, false);
        Image dotImg = dotObj.AddComponent<Image>();
        dotImg.sprite = dot;
        dotImg.color = new Color(0.1f, 1f, 0.45f, 1f); // Online Green
        RectTransform dotRect = dotObj.GetComponent<RectTransform>();
        dotRect.anchorMin = new Vector2(0f, 0.5f);
        dotRect.anchorMax = new Vector2(0f, 0.5f);
        dotRect.anchoredPosition = new Vector2(25f, 0f);
        dotRect.sizeDelta = new Vector2(16, 16);

        // Connection Status Text
        GameObject connTextObj = new GameObject("ConnectionStatusText");
        connTextObj.transform.SetParent(connPill.transform, false);
        TextMeshProUGUI connTxt = connTextObj.AddComponent<TextMeshProUGUI>();
        connTxt.text = "Connecting to Photon Cloud Network...";
        connTxt.font = fontTilt;
        connTxt.fontSize = 16;
        connTxt.alignment = TextAlignmentOptions.MidlineLeft;
        connTxt.color = new Color(0.80f, 0.88f, 0.98f, 0.9f);
        RectTransform connTRect = connTextObj.GetComponent<RectTransform>();
        connTRect.anchorMin = Vector2.zero;
        connTRect.anchorMax = Vector2.one;
        connTRect.offsetMin = new Vector2(44, 0);
        connTRect.offsetMax = new Vector2(-15, 0);

        // AppId Warning Text
        GameObject warnObj = new GameObject("AppIdWarningText");
        warnObj.transform.SetParent(centerCard.transform, false);
        TextMeshProUGUI warnTxt = warnObj.AddComponent<TextMeshProUGUI>();
        warnTxt.text = "";
        warnTxt.font = fontTilt;
        warnTxt.fontSize = 15;
        warnTxt.alignment = TextAlignmentOptions.Center;
        warnTxt.color = new Color(1f, 0.45f, 0.35f, 1f);
        RectTransform warnRect = warnObj.GetComponent<RectTransform>();
        warnRect.anchorMin = new Vector2(0.5f, 0.5f);
        warnRect.anchorMax = new Vector2(0.5f, 0.5f);
        warnRect.anchoredPosition = new Vector2(0f, -235f);
        warnRect.sizeDelta = new Vector2(620, 50);

        // ==========================================
        // 2. BUILD REDESIGNED MATCHMAKING PANEL
        // ==========================================
        GameObject matchPanel = new GameObject("MatchmakingPanel");
        Undo.RegisterCreatedObjectUndo(matchPanel, "Create MatchmakingPanel");
        matchPanel.transform.SetParent(canvas.transform, false);
        matchPanel.transform.SetAsLastSibling();

        RectTransform matchRect = matchPanel.AddComponent<RectTransform>();
        StretchFull(matchRect);

        CanvasGroup matchCg = matchPanel.AddComponent<CanvasGroup>();

        // Match Dimmer
        Image matchDim = matchPanel.AddComponent<Image>();
        matchDim.sprite = dimBg;
        matchDim.color = new Color(0.02f, 0.04f, 0.08f, 0.92f);

        // Match Card
        GameObject matchCard = new GameObject("MatchCard");
        matchCard.transform.SetParent(matchPanel.transform, false);
        Image mCardImg = matchCard.AddComponent<Image>();
        mCardImg.sprite = popupBg;
        mCardImg.type = Image.Type.Sliced;
        RectTransform mCardRect = matchCard.GetComponent<RectTransform>();
        mCardRect.anchorMin = new Vector2(0.5f, 0.5f);
        mCardRect.anchorMax = new Vector2(0.5f, 0.5f);
        mCardRect.sizeDelta = new Vector2(700, 860);

        // Header Ribbon
        GameObject mRibbonObj = new GameObject("MatchHeaderRibbon");
        mRibbonObj.transform.SetParent(matchCard.transform, false);
        Image mRibbonImg = mRibbonObj.AddComponent<Image>();
        mRibbonImg.sprite = ribbonRed;
        mRibbonImg.type = Image.Type.Sliced;
        RectTransform mRibbonRect = mRibbonObj.GetComponent<RectTransform>();
        mRibbonRect.anchorMin = new Vector2(0.5f, 0.5f);
        mRibbonRect.anchorMax = new Vector2(0.5f, 0.5f);
        mRibbonRect.anchoredPosition = new Vector2(0f, 395f);
        mRibbonRect.sizeDelta = new Vector2(640, 110);

        GameObject mTitleObj = new GameObject("MTitle");
        mTitleObj.transform.SetParent(mRibbonObj.transform, false);
        TextMeshProUGUI mTitleTxt = mTitleObj.AddComponent<TextMeshProUGUI>();
        mTitleTxt.text = "ARENA MATCHMAKING";
        mTitleTxt.font = fontOutline;
        mTitleTxt.fontSize = 36;
        mTitleTxt.fontStyle = FontStyles.Bold;
        mTitleTxt.alignment = TextAlignmentOptions.Center;
        mTitleTxt.color = new Color(1f, 0.88f, 0.45f, 1f);
        RectTransform mTitleRect = mTitleObj.GetComponent<RectTransform>();
        mTitleRect.anchorMin = Vector2.zero;
        mTitleRect.anchorMax = Vector2.one;
        mTitleRect.offsetMin = new Vector2(20, 12);
        mTitleRect.offsetMax = new Vector2(-20, -10);

        // VS Showdown Container
        GameObject showdownObj = new GameObject("ShowdownContainer");
        showdownObj.transform.SetParent(matchCard.transform, false);
        RectTransform showdownRect = showdownObj.AddComponent<RectTransform>();
        showdownRect.anchorMin = new Vector2(0.5f, 0.5f);
        showdownRect.anchorMax = new Vector2(0.5f, 0.5f);
        showdownRect.anchoredPosition = new Vector2(0f, 150f);
        showdownRect.sizeDelta = new Vector2(640, 280);

        // Rotating Aura Glow behind Swords
        GameObject auraObj = new GameObject("AuraGlow");
        auraObj.transform.SetParent(showdownObj.transform, false);
        Image auraImg = auraObj.AddComponent<Image>();
        auraImg.sprite = glow;
        auraImg.color = new Color(1f, 0.65f, 0.15f, 0.32f);
        RectTransform auraRect = auraObj.GetComponent<RectTransform>();
        auraRect.anchorMin = new Vector2(0.5f, 0.5f);
        auraRect.anchorMax = new Vector2(0.5f, 0.5f);
        auraRect.sizeDelta = new Vector2(400, 400);

        // Center Swords & Shield
        GameObject swordsObj = new GameObject("SwordsCenterpiece");
        swordsObj.transform.SetParent(showdownObj.transform, false);
        Image swordsImg = swordsObj.AddComponent<Image>();
        swordsImg.sprite = swordsShield;
        swordsImg.preserveAspect = true;
        RectTransform swordsRect = swordsObj.GetComponent<RectTransform>();
        swordsRect.anchorMin = new Vector2(0.5f, 0.5f);
        swordsRect.anchorMax = new Vector2(0.5f, 0.5f);
        swordsRect.sizeDelta = new Vector2(230, 180);

        // VS Badge
        GameObject vsBadgeObj = new GameObject("VSBadge");
        vsBadgeObj.transform.SetParent(showdownObj.transform, false);
        TextMeshProUGUI vsTxt = vsBadgeObj.AddComponent<TextMeshProUGUI>();
        vsTxt.text = "VS";
        vsTxt.font = fontOutline;
        vsTxt.fontSize = 42;
        vsTxt.fontStyle = FontStyles.Bold;
        vsTxt.alignment = TextAlignmentOptions.Center;
        vsTxt.color = new Color(1f, 0.90f, 0.35f, 1f);
        RectTransform vsBadgeRect = vsBadgeObj.GetComponent<RectTransform>();
        vsBadgeRect.anchorMin = new Vector2(0.5f, 0.5f);
        vsBadgeRect.anchorMax = new Vector2(0.5f, 0.5f);
        vsBadgeRect.anchoredPosition = new Vector2(0f, -65f);
        vsBadgeRect.sizeDelta = new Vector2(100, 60);

        // Left: Player Profile
        GameObject pCard = CreateFighterCard(showdownObj.transform, "PlayerCard", new Vector2(-195f, 0f),
            frameCircle, avatarBlue, "Commander", "YOU", new Color(0.2f, 0.6f, 1f, 1f), fontTilt, fontOutline);
        RectTransform pCardRect = pCard.GetComponent<RectTransform>();
        TextMeshProUGUI pNameText = pCard.transform.Find("NameText").GetComponent<TextMeshProUGUI>();

        // Right: Opponent Profile
        GameObject oCard = CreateFighterCard(showdownObj.transform, "OpponentCard", new Vector2(195f, 0f),
            frameCircle, avatarRed, "Searching...", "RIVAL", new Color(1f, 0.3f, 0.3f, 1f), fontTilt, fontOutline);
        RectTransform oCardRect = oCard.GetComponent<RectTransform>();
        Image oAvatarImg = oCard.transform.Find("AvatarFrame/AvatarIcon").GetComponent<Image>();
        oAvatarImg.color = new Color(1f, 0.4f, 0.4f, 0.65f); // Mystery silhouette
        TextMeshProUGUI oNameText = oCard.transform.Find("NameText").GetComponent<TextMeshProUGUI>();

        // Status & Timer Box
        GameObject statusBox = new GameObject("StatusBox");
        statusBox.transform.SetParent(matchCard.transform, false);
        Image statusBoxBg = statusBox.AddComponent<Image>();
        statusBoxBg.sprite = frameSquare;
        statusBoxBg.type = Image.Type.Sliced;
        statusBoxBg.color = new Color(0.10f, 0.13f, 0.20f, 0.95f);
        RectTransform statusBoxRect = statusBox.GetComponent<RectTransform>();
        statusBoxRect.anchorMin = new Vector2(0.5f, 0.5f);
        statusBoxRect.anchorMax = new Vector2(0.5f, 0.5f);
        statusBoxRect.anchoredPosition = new Vector2(0f, -90f);
        statusBoxRect.sizeDelta = new Vector2(580, 115);

        GameObject mStatusObj = new GameObject("MatchStatusText");
        mStatusObj.transform.SetParent(statusBox.transform, false);
        TextMeshProUGUI mStatusTxt = mStatusObj.AddComponent<TextMeshProUGUI>();
        mStatusTxt.text = "Searching for open 1v1 battle room...";
        mStatusTxt.font = fontTilt;
        mStatusTxt.fontSize = 22;
        mStatusTxt.fontStyle = FontStyles.Bold;
        mStatusTxt.alignment = TextAlignmentOptions.Center;
        mStatusTxt.color = Color.white;
        RectTransform mStatusRect = mStatusObj.GetComponent<RectTransform>();
        mStatusRect.anchorMin = new Vector2(0f, 0.45f);
        mStatusRect.anchorMax = new Vector2(1f, 1f);
        mStatusRect.offsetMin = new Vector2(15, 0);
        mStatusRect.offsetMax = new Vector2(-15, -10);

        GameObject timerObj = new GameObject("MatchTimerText");
        timerObj.transform.SetParent(statusBox.transform, false);
        TextMeshProUGUI timerTxt = timerObj.AddComponent<TextMeshProUGUI>();
        timerTxt.text = "ELAPSED: 00:00";
        timerTxt.font = fontTilt;
        timerTxt.fontSize = 18;
        timerTxt.fontStyle = FontStyles.Bold;
        timerTxt.alignment = TextAlignmentOptions.Center;
        timerTxt.color = new Color(1f, 0.85f, 0.35f, 1f); // Gold
        RectTransform timerRect = timerObj.GetComponent<RectTransform>();
        timerRect.anchorMin = new Vector2(0f, 0f);
        timerRect.anchorMax = new Vector2(1f, 0.45f);
        timerRect.offsetMin = new Vector2(15, 10);
        timerRect.offsetMax = new Vector2(-15, 0);

        // Cancel Button
        GameObject cancelBtnObj = new GameObject("Btn_CancelMatch");
        cancelBtnObj.transform.SetParent(matchCard.transform, false);
        Image cancelBtnImg = cancelBtnObj.AddComponent<Image>();
        cancelBtnImg.sprite = btnRed;
        cancelBtnImg.type = Image.Type.Sliced;
        Button cancelBtn = cancelBtnObj.AddComponent<Button>();
        RectTransform cancelBtnRect = cancelBtnObj.GetComponent<RectTransform>();
        cancelBtnRect.anchorMin = new Vector2(0.5f, 0.5f);
        cancelBtnRect.anchorMax = new Vector2(0.5f, 0.5f);
        cancelBtnRect.anchoredPosition = new Vector2(0f, -245f);
        cancelBtnRect.sizeDelta = new Vector2(460, 80);

        GameObject cancelTxtObj = new GameObject("BtnText");
        cancelTxtObj.transform.SetParent(cancelBtnObj.transform, false);
        TextMeshProUGUI cancelTxt = cancelTxtObj.AddComponent<TextMeshProUGUI>();
        cancelTxt.text = "CANCEL SEARCH";
        cancelTxt.font = fontOutline;
        cancelTxt.fontSize = 26;
        cancelTxt.fontStyle = FontStyles.Bold;
        cancelTxt.alignment = TextAlignmentOptions.Center;
        cancelTxt.color = Color.white;
        RectTransform cancelTRect = cancelTxtObj.GetComponent<RectTransform>();
        cancelTRect.anchorMin = Vector2.zero;
        cancelTRect.anchorMax = Vector2.one;
        cancelTRect.offsetMin = Vector2.zero;
        cancelTRect.offsetMax = Vector2.zero;

        // Hide matchmaking panel by default
        matchPanel.SetActive(false);

        // ==========================================
        // 3. ATTACH & CONFIGURE JUICYMAINMENUUI
        // ==========================================
        JuicyMainMenuUI juicy = canvas.GetComponent<JuicyMainMenuUI>();
        if (juicy == null) juicy = Undo.AddComponent<JuicyMainMenuUI>(canvas.gameObject);

        SerializedObject juicySo = new SerializedObject(juicy);
        juicySo.FindProperty("mainPanel").objectReferenceValue = mainPanel;
        juicySo.FindProperty("mainCardRect").objectReferenceValue = cardRect;
        juicySo.FindProperty("mainCanvasGroup").objectReferenceValue = mainCg;
        juicySo.FindProperty("matchmakingPanel").objectReferenceValue = matchPanel;
        juicySo.FindProperty("matchmakingCardRect").objectReferenceValue = mCardRect;
        juicySo.FindProperty("matchmakingCanvasGroup").objectReferenceValue = matchCg;
        juicySo.FindProperty("playMultiplayerButton").objectReferenceValue = multiBtn;
        juicySo.FindProperty("playVsAIButton").objectReferenceValue = aiBtn;
        juicySo.FindProperty("cancelMatchmakingButton").objectReferenceValue = cancelBtn;
        juicySo.FindProperty("auraGlowRect").objectReferenceValue = auraRect;
        juicySo.FindProperty("swordsCenterpieceRect").objectReferenceValue = swordsRect;
        juicySo.FindProperty("vsBadgeRect").objectReferenceValue = vsBadgeRect;
        juicySo.FindProperty("playerAvatarRect").objectReferenceValue = pCardRect;
        juicySo.FindProperty("opponentAvatarRect").objectReferenceValue = oCardRect;
        juicySo.FindProperty("opponentAvatarImage").objectReferenceValue = oAvatarImg;
        juicySo.FindProperty("opponentNameText").objectReferenceValue = oNameText;
        juicySo.FindProperty("playerNicknameText").objectReferenceValue = pNameText;
        juicySo.FindProperty("matchmakingStatusText").objectReferenceValue = mStatusTxt;
        juicySo.FindProperty("matchmakingTimerText").objectReferenceValue = timerTxt;
        juicySo.FindProperty("connectionDot").objectReferenceValue = dotImg;
        juicySo.FindProperty("connectionStatusText").objectReferenceValue = connTxt;
        juicySo.FindProperty("nicknameInputField").objectReferenceValue = inputField;
        juicySo.ApplyModifiedProperties();

        // ==========================================
        // 4. WIRE UP MAINMENUMANAGER REFERENCES
        // ==========================================
        MainMenuManager manager = Object.FindFirstObjectByType<MainMenuManager>();
        if (manager != null)
        {
            SerializedObject mgrSo = new SerializedObject(manager);
            mgrSo.FindProperty("mainPanel").objectReferenceValue = mainPanel;
            mgrSo.FindProperty("matchmakingPanel").objectReferenceValue = matchPanel;
            mgrSo.FindProperty("nicknameInput").objectReferenceValue = inputField;
            mgrSo.FindProperty("playVsAIButton").objectReferenceValue = aiBtn;
            mgrSo.FindProperty("playMultiplayerButton").objectReferenceValue = multiBtn;
            mgrSo.FindProperty("connectionStatusText").objectReferenceValue = connTxt;
            mgrSo.FindProperty("appIdWarningText").objectReferenceValue = warnTxt;
            mgrSo.FindProperty("matchmakingStatusText").objectReferenceValue = mStatusTxt;
            mgrSo.FindProperty("cancelMatchmakingButton").objectReferenceValue = cancelBtn;
            mgrSo.FindProperty("juicyUI").objectReferenceValue = juicy;
            mgrSo.ApplyModifiedProperties();

            EditorUtility.SetDirty(manager);
        }

        EditorUtility.SetDirty(juicy);
        EditorUtility.SetDirty(mainPanel);
        EditorUtility.SetDirty(matchPanel);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("<color=#00FFAA><b>[JuicyMenuBuilder]</b> SUCCESS! MainPanel and MatchmakingPanel redesigned with rich medieval assets & DOTween juice!</color>");
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static GameObject CreateJuicyButton(Transform parent, string name, Sprite btnSprite,
        Vector2 anchoredPos, Vector2 size, Sprite iconSprite, string mainTitle, string subTitle,
        Color subColor, TMP_FontAsset titleFont, TMP_FontAsset subFont)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        Image img = btnObj.AddComponent<Image>();
        img.sprite = btnSprite;
        img.type = Image.Type.Sliced;
        Button btn = btnObj.AddComponent<Button>();

        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        // Left Icon
        if (iconSprite != null)
        {
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(btnObj.transform, false);
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = iconSprite;
            iconImg.preserveAspect = true;
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(50f, 0f);
            iconRect.sizeDelta = new Vector2(65, 65);
        }

        // Text Container
        GameObject textContainer = new GameObject("TextContainer");
        textContainer.transform.SetParent(btnObj.transform, false);
        RectTransform tcRect = textContainer.AddComponent<RectTransform>();
        tcRect.anchorMin = new Vector2(0f, 0f);
        tcRect.anchorMax = new Vector2(1f, 1f);
        tcRect.offsetMin = new Vector2(95, 8);
        tcRect.offsetMax = new Vector2(-20, -8);

        // Main Text
        GameObject mainTxtObj = new GameObject("MainText");
        mainTxtObj.transform.SetParent(textContainer.transform, false);
        TextMeshProUGUI mTxt = mainTxtObj.AddComponent<TextMeshProUGUI>();
        mTxt.text = mainTitle;
        mTxt.font = titleFont;
        mTxt.fontSize = 28;
        mTxt.fontStyle = FontStyles.Bold;
        mTxt.alignment = TextAlignmentOptions.MidlineLeft;
        mTxt.color = Color.white;
        RectTransform mtRect = mainTxtObj.GetComponent<RectTransform>();
        mtRect.anchorMin = new Vector2(0f, 0.42f);
        mtRect.anchorMax = new Vector2(1f, 1f);
        mtRect.offsetMin = Vector2.zero;
        mtRect.offsetMax = Vector2.zero;

        // Sub Text
        GameObject subTxtObj = new GameObject("SubText");
        subTxtObj.transform.SetParent(textContainer.transform, false);
        TextMeshProUGUI sTxt = subTxtObj.AddComponent<TextMeshProUGUI>();
        sTxt.text = subTitle;
        sTxt.font = subFont;
        sTxt.fontSize = 14;
        sTxt.fontStyle = FontStyles.Bold;
        sTxt.alignment = TextAlignmentOptions.MidlineLeft;
        sTxt.color = subColor;
        RectTransform stRect = subTxtObj.GetComponent<RectTransform>();
        stRect.anchorMin = new Vector2(0f, 0f);
        stRect.anchorMax = new Vector2(1f, 0.42f);
        stRect.offsetMin = Vector2.zero;
        stRect.offsetMax = Vector2.zero;

        return btnObj;
    }

    private static GameObject CreateFighterCard(Transform parent, string name, Vector2 anchoredPos,
        Sprite frameSprite, Sprite avatarSprite, string initialName, string roleBadgeText,
        Color roleColor, TMP_FontAsset fontNormal, TMP_FontAsset fontOutline)
    {
        GameObject cardObj = new GameObject(name);
        cardObj.transform.SetParent(parent, false);
        RectTransform cardRect = cardObj.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = anchoredPos;
        cardRect.sizeDelta = new Vector2(160, 240);

        // Avatar Frame
        GameObject frameObj = new GameObject("AvatarFrame");
        frameObj.transform.SetParent(cardObj.transform, false);
        Image frameImg = frameObj.AddComponent<Image>();
        frameImg.sprite = frameSprite;
        frameImg.preserveAspect = true;
        RectTransform frameRect = frameObj.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0.5f, 1f);
        frameRect.anchorMax = new Vector2(0.5f, 1f);
        frameRect.anchoredPosition = new Vector2(0f, -65f);
        frameRect.sizeDelta = new Vector2(120, 120);

        // Avatar Icon
        GameObject iconObj = new GameObject("AvatarIcon");
        iconObj.transform.SetParent(frameObj.transform, false);
        Image iconImg = iconObj.AddComponent<Image>();
        iconImg.sprite = avatarSprite;
        iconImg.preserveAspect = true;
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(8, 8);
        iconRect.offsetMax = new Vector2(-8, -8);

        // Role Badge Pill
        GameObject badgeObj = new GameObject("RoleBadge");
        badgeObj.transform.SetParent(cardObj.transform, false);
        Image badgeBg = badgeObj.AddComponent<Image>();
        badgeBg.color = roleColor;
        RectTransform badgeRect = badgeObj.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0.5f, 1f);
        badgeRect.anchorMax = new Vector2(0.5f, 1f);
        badgeRect.anchoredPosition = new Vector2(0f, -135f);
        badgeRect.sizeDelta = new Vector2(75, 24);

        GameObject badgeTxtObj = new GameObject("Text");
        badgeTxtObj.transform.SetParent(badgeObj.transform, false);
        TextMeshProUGUI bTxt = badgeTxtObj.AddComponent<TextMeshProUGUI>();
        bTxt.text = roleBadgeText;
        bTxt.font = fontOutline;
        bTxt.fontSize = 13;
        bTxt.fontStyle = FontStyles.Bold;
        bTxt.alignment = TextAlignmentOptions.Center;
        bTxt.color = Color.white;
        RectTransform bTRect = badgeTxtObj.GetComponent<RectTransform>();
        bTRect.anchorMin = Vector2.zero;
        bTRect.anchorMax = Vector2.one;
        bTRect.offsetMin = Vector2.zero;
        bTRect.offsetMax = Vector2.zero;

        // Fighter Name
        GameObject nameObj = new GameObject("NameText");
        nameObj.transform.SetParent(cardObj.transform, false);
        TextMeshProUGUI nameTxt = nameObj.AddComponent<TextMeshProUGUI>();
        nameTxt.text = initialName;
        nameTxt.font = fontNormal;
        nameTxt.fontSize = 18;
        nameTxt.fontStyle = FontStyles.Bold;
        nameTxt.alignment = TextAlignmentOptions.Center;
        nameTxt.color = Color.white;
        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0f);
        nameRect.anchorMax = new Vector2(1f, 0f);
        nameRect.anchoredPosition = new Vector2(0f, 25f);
        nameRect.sizeDelta = new Vector2(160, 32);

        return cardObj;
    }
}
#endif

