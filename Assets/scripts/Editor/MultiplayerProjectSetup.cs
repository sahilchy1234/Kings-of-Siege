using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Photon.Pun;
using Object = UnityEngine.Object;

/// <summary>
/// Editor setup wizard:
/// 1. Ensures PhotonServerSettings asset exists.
/// 2. Creates the MainMenu scene (with Play vs AI and Play Multiplayer options).
/// 3. Registers MainMenu (Index 0) and BattleScene (Index 1) in EditorBuildSettings.
/// 4. Adds NetworkBattleBridge with PhotonView to BattleScene.
/// </summary>
public static class MultiplayerProjectSetup
{
    private const string MAIN_MENU_SCENE_PATH = "Assets/Scenes/MainMenu.unity";
    private const string BATTLE_SCENE_PATH = "Assets/Scenes/BattleScene.unity";

    [MenuItem("Tools/Setup PUN 2 Multiplayer & Main Menu", priority = 0)]
    public static void SetupProject()
    {
        bool proceed = EditorUtility.DisplayDialog(
            "Setup PUN 2 Multiplayer & Main Menu",
            "This will create a Main Menu scene with 'Play vs AI' and 'Play Multiplayer (1v1)' options, " +
            "configure NetworkBattleBridge in BattleScene, and register both scenes in Build Settings.\n\n" +
            "Would you like to proceed?",
            "Yes, Setup Now", "Cancel");

        if (!proceed) return;

        RunSetupSilent();

        EditorUtility.DisplayDialog(
            "Setup Complete!",
            "🎉 PUN 2 Multiplayer & Main Menu have been successfully configured!\n\n" +
            "• MainMenu Scene: Assets/Scenes/MainMenu.unity (Index 0)\n" +
            "• Battle Scene: Assets/Scenes/BattleScene.unity (Index 1)\n" +
            "• Network Bridge: [NetworkBattleBridge] added to BattleScene\n\n" +
            "IMPORTANT: If you haven't already, paste your Photon Realtime AppId in PhotonServerSettings (now selected in Inspector).",
            "Awesome!");
    }

    public static void RunSetupSilent()
    {
        EditorSceneManager.SaveOpenScenes();

        // 1. Initialize Photon Server Settings
        EnsurePhotonSettings();

        // 2. Setup Main Menu Scene
        CreateMainMenuScene();

        // 3. Setup Battle Scene
        ConfigureBattleScene();

        // 4. Update Build Settings
        UpdateBuildSettings();

        // 5. Re-open Main Menu scene for testing
        EditorSceneManager.OpenScene(MAIN_MENU_SCENE_PATH);

        // Highlight PhotonServerSettings so user can enter AppId
        if (PhotonNetwork.PhotonServerSettings != null)
        {
            Selection.activeObject = PhotonNetwork.PhotonServerSettings;
        }

        Debug.Log("<color=#00FFAA>[MultiplayerProjectSetup] PUN 2 Multiplayer Setup Complete!</color>");
    }

    private static void EnsurePhotonSettings()
    {
        try
        {
            if (PhotonNetwork.PhotonServerSettings == null)
            {
                PhotonNetwork.LoadOrCreateSettings();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[MultiplayerProjectSetup] PhotonServerSettings check: " + ex.Message);
        }
    }

    private static void UpdateBuildSettings()
    {
        EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene(MAIN_MENU_SCENE_PATH, true),
            new EditorBuildSettingsScene(BATTLE_SCENE_PATH, true)
        };

        EditorBuildSettings.scenes = scenes;
        AssetDatabase.SaveAssets();
        Debug.Log("<color=#00FFAA>[MultiplayerProjectSetup] Registered scenes in Build Settings (MainMenu = 0, BattleScene = 1).</color>");
    }

    private static void ConfigureBattleScene()
    {
        if (!File.Exists(BATTLE_SCENE_PATH))
        {
            Debug.LogError($"[MultiplayerProjectSetup] Battle scene not found at {BATTLE_SCENE_PATH}");
            return;
        }

        var scene = EditorSceneManager.OpenScene(BATTLE_SCENE_PATH, OpenSceneMode.Single);

        Type bridgeType = Type.GetType("NetworkBattleBridge, Assembly-CSharp");
        if (bridgeType == null)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                bridgeType = asm.GetType("NetworkBattleBridge");
                if (bridgeType != null) break;
            }
        }

        GameObject bridgeObj = GameObject.Find("[NetworkBattleBridge]");
        Component bridge = null;

        if (bridgeObj == null)
        {
            bridgeObj = new GameObject("[NetworkBattleBridge]");
        }

        if (bridgeType != null)
        {
            bridge = bridgeObj.GetComponent(bridgeType);
            if (bridge == null)
            {
                bridge = bridgeObj.AddComponent(bridgeType);
            }
        }

        // Ensure PhotonView
        PhotonView pv = bridgeObj.GetComponent<PhotonView>();
        if (pv == null)
        {
            pv = bridgeObj.AddComponent<PhotonView>();
        }
        pv.ViewID = 999;

        // Auto-assign references if bridge component exists
        if (bridge != null)
        {
            SerializedObject so = new SerializedObject(bridge);
            AssignProperty(so, "playerSpawner", UnityEngine.Object.FindFirstObjectByType<PeasantButtonSpawner>());
            AssignProperty(so, "aiSpawner", UnityEngine.Object.FindFirstObjectByType<EnemyAISpawner>());
            AssignProperty(so, "aiCommander", UnityEngine.Object.FindFirstObjectByType<SmartAIPlayer>());
            AssignProperty(so, "cameraController", UnityEngine.Object.FindFirstObjectByType<CameraSwipeController>());
            so.ApplyModifiedProperties();
        }

        EditorUtility.SetDirty(bridgeObj);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("<color=#00FFAA>[MultiplayerProjectSetup] BattleScene successfully configured with NetworkBattleBridge.</color>");
    }

    private static void CreateMainMenuScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 1. Camera
        GameObject camObj = new GameObject("Main Camera");
        Camera cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.08f, 0.12f, 1f);
        cam.orthographic = true;
        camObj.tag = "MainCamera";
        camObj.transform.position = new Vector3(0, 0, -10);

        // 2. Event System
        GameObject eventSystemObj = new GameObject("EventSystem");
        eventSystemObj.AddComponent<EventSystem>();
        eventSystemObj.AddComponent<StandaloneInputModule>();

        // 3. Canvas
        GameObject canvasObj = new GameObject("Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<GraphicRaycaster>();

        // 4. Main Menu Manager Object
        GameObject managerObj = new GameObject("[MainMenuManager]");
        Type menuManagerType = Type.GetType("MainMenuManager, Assembly-CSharp");
        if (menuManagerType == null)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                menuManagerType = asm.GetType("MainMenuManager");
                if (menuManagerType != null) break;
            }
        }

        Component menuManager = null;
        if (menuManagerType != null)
        {
            menuManager = managerObj.AddComponent(menuManagerType);
        }
        managerObj.AddComponent<PhotonView>();

        SerializedObject so = (menuManager != null) ? new SerializedObject(menuManager) : null;

        // --- MAIN PANEL ---
        GameObject mainPanel = new GameObject("MainPanel");
        mainPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform mainRect = mainPanel.AddComponent<RectTransform>();
        StretchFull(mainRect);
        Image bgImg = mainPanel.AddComponent<Image>();
        bgImg.color = new Color(0.07f, 0.09f, 0.14f, 1f);
        AssignProperty(so, "mainPanel", mainPanel);

        // Medieval Decorative Card / Frame
        GameObject cardObj = new GameObject("CenterCard");
        cardObj.transform.SetParent(mainPanel.transform, false);
        Image cardImg = cardObj.AddComponent<Image>();
        cardImg.color = new Color(0.11f, 0.14f, 0.20f, 0.95f);
        RectTransform cardRect = cardObj.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(720, 800);

        // Title
        GameObject titleObj = CreateText(cardObj.transform, "TitleText", "SIEGE: REALMS AT WAR", 48, FontStyles.Bold,
            new Color(1f, 0.82f, 0.32f, 1f), new Vector2(0.5f, 0.88f), new Vector2(660, 80));

        // Subtitle
        CreateText(cardObj.transform, "SubtitleText", "Tactical 1v1 Castle Defense", 22, FontStyles.Italic,
            new Color(0.65f, 0.75f, 0.88f, 0.85f), new Vector2(0.5f, 0.79f), new Vector2(600, 40));

        // Nickname label & input
        CreateText(cardObj.transform, "NickLabel", "COMMANDER NICKNAME:", 18, FontStyles.Bold,
            new Color(0.8f, 0.85f, 0.9f, 0.9f), new Vector2(0.5f, 0.68f), new Vector2(480, 30));

        GameObject inputObj = new GameObject("NicknameInputField");
        inputObj.transform.SetParent(cardObj.transform, false);
        Image inputBg = inputObj.AddComponent<Image>();
        inputBg.color = new Color(0.06f, 0.08f, 0.12f, 1f);
        RectTransform inputRect = inputObj.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0.5f, 0.61f);
        inputRect.anchorMax = new Vector2(0.5f, 0.61f);
        inputRect.sizeDelta = new Vector2(460, 55);

        GameObject inputTextObj = CreateText(inputObj.transform, "Text", "Commander", 22, FontStyles.Normal,
            Color.white, new Vector2(0.5f, 0.5f), new Vector2(430, 45));
        TMP_InputField inputField = inputObj.AddComponent<TMP_InputField>();
        inputField.textComponent = inputTextObj.GetComponent<TextMeshProUGUI>();
        AssignProperty(so, "nicknameInput", inputField);

        // Button 1: Play vs AI
        Button vsAiBtn = CreateStyledButton(cardObj.transform, "Btn_PlayVsAI", "PLAY VS AI (SOLO)",
            new Vector2(0.5f, 0.46f), new Vector2(460, 65), new Color(0.20f, 0.55f, 0.28f, 1f));
        AssignProperty(so, "playVsAIButton", vsAiBtn);

        // Button 2: Play Multiplayer
        Button multiBtn = CreateStyledButton(cardObj.transform, "Btn_PlayMultiplayer", "PLAY MULTIPLAYER (1v1)",
            new Vector2(0.5f, 0.34f), new Vector2(460, 65), new Color(0.18f, 0.48f, 0.88f, 1f));
        AssignProperty(so, "playMultiplayerButton", multiBtn);

        // Connection status text
        GameObject connText = CreateText(cardObj.transform, "ConnectionStatusText", "Connecting to Photon Cloud...", 18, FontStyles.Normal,
            new Color(0.6f, 0.7f, 0.82f, 0.8f), new Vector2(0.5f, 0.22f), new Vector2(600, 35));
        AssignProperty(so, "connectionStatusText", connText.GetComponent<TextMeshProUGUI>());

        // AppId Warning text
        GameObject warnText = CreateText(cardObj.transform, "AppIdWarningText", "", 16, FontStyles.Normal,
            new Color(1f, 0.4f, 0.35f, 1f), new Vector2(0.5f, 0.12f), new Vector2(660, 50));
        AssignProperty(so, "appIdWarningText", warnText.GetComponent<TextMeshProUGUI>());

        // --- MATCHMAKING OVERLAY PANEL ---
        GameObject matchPanel = new GameObject("MatchmakingPanel");
        matchPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform matchRect = matchPanel.AddComponent<RectTransform>();
        StretchFull(matchRect);
        Image matchBg = matchPanel.AddComponent<Image>();
        matchBg.color = new Color(0.04f, 0.06f, 0.10f, 0.98f);
        AssignProperty(so, "matchmakingPanel", matchPanel);

        // Center card inside matchmaking panel
        GameObject mCardObj = new GameObject("MatchCard");
        mCardObj.transform.SetParent(matchPanel.transform, false);
        Image mCardImg = mCardObj.AddComponent<Image>();
        mCardImg.color = new Color(0.09f, 0.12f, 0.18f, 0.95f);
        RectTransform mCardRect = mCardObj.GetComponent<RectTransform>();
        mCardRect.anchorMin = new Vector2(0.5f, 0.5f);
        mCardRect.anchorMax = new Vector2(0.5f, 0.5f);
        mCardRect.sizeDelta = new Vector2(650, 450);

        CreateText(mCardObj.transform, "MTitle", "1v1 MULTIPLAYER MATCHMAKING", 28, FontStyles.Bold,
            new Color(1f, 0.82f, 0.32f, 1f), new Vector2(0.5f, 0.80f), new Vector2(580, 50));

        GameObject mStatusText = CreateText(mCardObj.transform, "MatchStatusText", "Searching for open battle...", 24, FontStyles.Normal,
            Color.white, new Vector2(0.5f, 0.55f), new Vector2(580, 80));
        AssignProperty(so, "matchmakingStatusText", mStatusText.GetComponent<TextMeshProUGUI>());

        Button cancelBtn = CreateStyledButton(mCardObj.transform, "Btn_CancelMatch", "CANCEL SEARCH",
            new Vector2(0.5f, 0.28f), new Vector2(380, 55), new Color(0.72f, 0.22f, 0.22f, 1f));
        AssignProperty(so, "cancelMatchmakingButton", cancelBtn);

        matchPanel.SetActive(false);

        if (so != null)
        {
            so.ApplyModifiedProperties();
        }

        // Save scene
        EditorSceneManager.SaveScene(scene, MAIN_MENU_SCENE_PATH);
        Debug.Log("<color=#00FFAA>[MultiplayerProjectSetup] Created and saved MainMenu scene at " + MAIN_MENU_SCENE_PATH + "</color>");
    }

    private static void AssignProperty(SerializedObject so, string propertyName, UnityEngine.Object obj)
    {
        if (so == null || obj == null) return;
        var prop = so.FindProperty(propertyName);
        if (prop != null)
        {
            prop.objectReferenceValue = obj;
        }
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static GameObject CreateText(Transform parent, string name, string content, float size, FontStyles style, Color color, Vector2 anchor, Vector2 sizeDelta)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;

        RectTransform rect = textObj.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.sizeDelta = sizeDelta;

        return textObj;
    }

    private static Button CreateStyledButton(Transform parent, string name, string label, Vector2 anchor, Vector2 sizeDelta, Color baseColor)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        Image img = btnObj.AddComponent<Image>();
        img.color = baseColor;

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = baseColor * 1.25f;
        colors.pressedColor = baseColor * 0.75f;
        btn.colors = colors;

        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.sizeDelta = sizeDelta;

        CreateText(btnObj.transform, "BtnText", label, 20, FontStyles.Bold, Color.white, new Vector2(0.5f, 0.5f), sizeDelta);

        return btn;
    }
}
