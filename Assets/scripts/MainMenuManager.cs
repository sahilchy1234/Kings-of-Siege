using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// Controls the Main Menu lobby scene:
/// - Provides two primary modes: "Play vs AI" and "Play Multiplayer (1v1)".
/// - Connects to Photon PUN2 cloud and handles random matchmaking.
/// - Automatically synchronizes the match scene when two players connect.
/// - Includes runtime UI auto-generation as a seamless fallback if canvas links are unassigned.
/// </summary>
public class MainMenuManager : MonoBehaviourPunCallbacks
{
    private const string BATTLE_SCENE_NAME = "BattleScene";
    private const string NICKNAME_PREF_KEY = "PUN_PlayerNickName";



    public int trophies;


    [Header("Menu UI Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject matchmakingPanel;

    [Header("Main Menu Controls")]
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private Button playVsAIButton;
    [SerializeField] private Button playMultiplayerButton;
    [SerializeField] private TMP_Text connectionStatusText;
    [SerializeField] private TMP_Text appIdWarningText;

    [Header("Matchmaking Panel Controls")]
    [SerializeField] private TMP_Text matchmakingStatusText;
    [SerializeField] private Button cancelMatchmakingButton;

    [Header("Juicy UI Integration")]
    [SerializeField] private JuicyMainMenuUI juicyUI;

    private const float MATCH_SEARCH_TIMEOUT = 15f;

    private bool isSearchingForMatch = false;
    private bool isStartingMatch = false;
    private bool opponentFound = false;
    private bool fellBackToAI = false;
    private bool pendingOfflineStart = false;
    private static int lastAiPortraitIndex = -1;

    [Header("AI Opponent Portraits")]
    [Tooltip("Same order as the MatchReadyScreen opponent pool in BattleScene.")]
    [SerializeField] private Sprite[] aiOpponentPortraits;
    private Coroutine matchSearchRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstanceInMainMenu()
    {
        if (SceneManager.GetActiveScene().name == "MainMenu")
        {
            if (FindFirstObjectByType<MainMenuManager>() == null)
            {
                GameObject go = new GameObject("[MainMenuManager]");
                go.AddComponent<PhotonView>();
                go.AddComponent<MainMenuManager>();
                Debug.Log("[MainMenuManager] Auto-spawned runtime MainMenuManager.");
            }
        }
    }

    private void Awake()
    {

        trophies = PlayerPrefs.GetInt("trophies",0);

        // Keep running in background when browser tab or Editor window loses focus
        Application.runInBackground = true;
        PhotonNetwork.KeepAliveInBackground = 60000;
        PhotonNetwork.SendRate = 20;
        PhotonNetwork.SerializationRate = 10;

        // Ensure PUN automatically syncs loaded scene from MasterClient to all clients
        PhotonNetwork.AutomaticallySyncScene = true;

        if (PhotonNetwork.PhotonServerSettings != null && PhotonNetwork.PhotonServerSettings.AppSettings != null)
        {
            if (string.IsNullOrEmpty(PhotonNetwork.PhotonServerSettings.AppSettings.Server))
            {
                PhotonNetwork.PhotonServerSettings.AppSettings.Server = "ns.exitgames.com";
            }
        }

        if (PhotonNetwork.NetworkingClient != null && PhotonNetwork.NetworkingClient.LoadBalancingPeer != null)
        {
            PhotonNetwork.NetworkingClient.LoadBalancingPeer.DisconnectTimeout = 60000;
            PhotonNetwork.NetworkingClient.LoadBalancingPeer.SentCountAllowance = 25;
        }

        // Auto-build UI if not pre-wired in inspector
        if (mainPanel == null)
        {
            BuildRuntimeMenuUI();
        }

        if (juicyUI == null)
        {
            juicyUI = FindFirstObjectByType<JuicyMainMenuUI>();
        }
    }

    private void Start()
    {
        // Load saved player nickname or generate a fun default
        string savedName = PlayerPrefs.GetString(NICKNAME_PREF_KEY, "Commander_" + Random.Range(100, 999));
        if (nicknameInput != null)
        {
            nicknameInput.text = savedName;
            nicknameInput.onValueChanged.AddListener(OnNicknameChanged);
        }
        PhotonNetwork.NickName = savedName;

        // Hook button listeners
        if (playVsAIButton != null) playVsAIButton.onClick.AddListener(OnPlayVsAIClicked);
        if (playMultiplayerButton != null) playMultiplayerButton.onClick.AddListener(OnPlayMultiplayerClicked);
        if (cancelMatchmakingButton != null) cancelMatchmakingButton.onClick.AddListener(OnCancelMatchmakingClicked);

        // Check if Photon Realtime AppId is set
        ValidatePhotonSettings();

        // Connect to Master in background for fast matchmaking response
        if (!PhotonNetwork.IsConnected)
        {
            UpdateStatus("Connecting to Photon Network...");
            PhotonNetwork.ConnectUsingSettings();
        }
        else
        {
            UpdateStatus("Connected to Photon Cloud (" + PhotonNetwork.CloudRegion + ")");
        }
    }

    private void ValidatePhotonSettings()
    {
        if (appIdWarningText == null) return;

        bool hasAppId = PhotonNetwork.PhotonServerSettings != null &&
                        PhotonNetwork.PhotonServerSettings.AppSettings != null &&
                        !string.IsNullOrWhiteSpace(PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime) &&
                        PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime != "YourAppIdHere";

        appIdWarningText.gameObject.SetActive(!hasAppId);
        if (!hasAppId)
        {
            appIdWarningText.text = "[!] Photon AppId is not configured yet! Enter your AppId in PhotonServerSettings (Window > Photon Unity Networking > Highlight Server Settings) to play online.";
        }
    }

    private void OnNicknameChanged(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            newName = "Commander_" + Random.Range(100, 999);
        }
        PlayerPrefs.SetString(NICKNAME_PREF_KEY, newName);
        PhotonNetwork.NickName = newName;
    }

    #region Menu Actions

    /// <summary>
    /// Launches the game in offline / Singleplayer mode against the autonomous AI.
    /// </summary>
    public void OnPlayVsAIClicked()
    {
        if (isStartingMatch) return;
        isStartingMatch = true;

        bool fromTimeout = fellBackToAI;
        isSearchingForMatch = false;
        StopSearchTimer();

        GameModeManager.CurrentMode = GameModeManager.GameMode.VsAI;
        GameModeManager.LocalTeamId = 0;

        // Photon refuses OFFLINE mode while connected, so disconnect first and switch in OnDisconnected.
        if (PhotonNetwork.IsConnected)
        {
            pendingOfflineStart = true;
            PhotonNetwork.Disconnect();
        }
        else
        {
            PhotonNetwork.OfflineMode = true;
        }

        // Show the same "opponent found" reveal as a real 1v1 match, with the AI as the opponent.
        if (juicyUI != null)
        {
            if (!fromTimeout)
            {
                juicyUI.TransitionToMatchmaking();
            }
            SetMatchmakingStatus("Opponent found!");
            // The same fake name and portrait are picked here and reused by the MatchReadyPanel in BattleScene.
            int portraitIndex = PickAiPortraitIndex();
            MatchReadyScreen.PendingAiOpponentAvatarIndex = portraitIndex;
            if (aiOpponentPortraits != null && portraitIndex < aiOpponentPortraits.Length)
            {
                juicyUI.SetConfirmedOpponentSprite(aiOpponentPortraits[portraitIndex]);
            }
            juicyUI.PlayMatchFoundSequence(MatchReadyScreen.PickAiOpponentName(), LoadBattleScene);
        }
        else
        {
            LoadBattleScene();
        }
    }

    /// <summary>Random AI portrait that never repeats the previous match's portrait.</summary>
    private int PickAiPortraitIndex()
    {
        int n = aiOpponentPortraits != null ? aiOpponentPortraits.Length : 0;
        if (n <= 1) return 0;
        int idx;
        do
        {
            idx = Random.Range(0, n);
        } while (idx == lastAiPortraitIndex);
        lastAiPortraitIndex = idx;
        return idx;
    }

    private void LoadBattleScene()
    {
        Debug.Log("[MainMenu] Starting Play vs AI mode. Loading " + BATTLE_SCENE_NAME);
        SceneManager.LoadScene(BATTLE_SCENE_NAME);
    }

    /// <summary>
    /// Starts matchmaking to find or host a 1v1 multiplayer battle via PUN2.
    /// </summary>
    public void OnPlayMultiplayerClicked()
    {
        GameModeManager.CurrentMode = GameModeManager.GameMode.Multiplayer;
        PhotonNetwork.OfflineMode = false;
        isSearchingForMatch = true;
        opponentFound = false;
        fellBackToAI = false;

        StopSearchTimer();
        matchSearchRoutine = StartCoroutine(MatchSearchTimeoutRoutine());

        if (juicyUI != null)
        {
            juicyUI.TransitionToMatchmaking();
        }
        else
        {
            if (mainPanel != null) mainPanel.SetActive(false);
            if (matchmakingPanel != null) matchmakingPanel.SetActive(true);
        }

        SetMatchmakingStatus("Connecting to lobby...");

        if (PhotonNetwork.IsConnectedAndReady)
        {
            JoinOrCreateMatchRoom();
        }
        else if (!PhotonNetwork.IsConnected)
        {
            SetMatchmakingStatus("Connecting to Photon Cloud Server...");
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public void OnCancelMatchmakingClicked()
    {
        isSearchingForMatch = false;
        isStartingMatch = false;
        StopSearchTimer();

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }

        if (juicyUI != null)
        {
            juicyUI.TransitionToMainPanel();
        }
        else
        {
            JuicyPopup popup = matchmakingPanel != null ? matchmakingPanel.GetComponentInChildren<JuicyPopup>(true) : null;
            if (popup != null)
            {
                popup.PlayPopOut(() =>
                {
                    if (matchmakingPanel != null) matchmakingPanel.SetActive(false);
                    RestoreMainMenuOrFallback();
                });
            }
            else
            {
                if (matchmakingPanel != null) matchmakingPanel.SetActive(false);
                RestoreMainMenuOrFallback();
            }
        }

        UpdateStatus(PhotonNetwork.IsConnected ? "Connected (" + PhotonNetwork.CloudRegion + ")" : "Ready");
    }

    private void RestoreMainMenuOrFallback()
    {
        MainMenuController menuController = MainMenuController.Instance ?? FindFirstObjectByType<MainMenuController>(FindObjectsInactive.Include);
        if (menuController != null)
        {
            if (mainPanel != null) mainPanel.SetActive(false);
            menuController.gameObject.SetActive(true);
            menuController.PlayEntranceAnimation();
        }
        else if (mainPanel != null)
        {
            mainPanel.SetActive(true);
        }
    }

    private void JoinOrCreateMatchRoom()
    {
        SetMatchmakingStatus("Searching for open 1v1 battle room...");
        // Try joining an existing open 2-player match
        PhotonNetwork.JoinRandomRoom();
    }

    #endregion

    #region Photon Callbacks

    public override void OnConnectedToMaster()
    {
        if (PhotonNetwork.NetworkingClient != null && PhotonNetwork.NetworkingClient.LoadBalancingPeer != null)
        {
            PhotonNetwork.NetworkingClient.LoadBalancingPeer.DisconnectTimeout = 60000;
            PhotonNetwork.NetworkingClient.LoadBalancingPeer.SentCountAllowance = 25;
        }

        UpdateStatus("Connected to Photon Cloud (" + PhotonNetwork.CloudRegion + ")");
        ValidatePhotonSettings();

        if (isSearchingForMatch)
        {
            JoinOrCreateMatchRoom();
        }
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        SetMatchmakingStatus("No open rooms found. Creating new room...");
        // Create 2-player room (1v1)
        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = 2,
            IsOpen = true,
            IsVisible = true,
            EmptyRoomTtl = 60000,
            PlayerTtl = 120000
        };

        string roomName = "BattleRoom_" + Random.Range(1000, 9999);
        PhotonNetwork.CreateRoom(roomName, roomOptions);
    }

    public override void OnJoinedRoom()
    {
        int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;
        Debug.Log($"[MainMenu] Successfully joined room: {PhotonNetwork.CurrentRoom.Name}. Players: {playerCount}/2");

        if (playerCount >= 2)
        {
            opponentFound = true;
            StopSearchTimer();
            SetMatchmakingStatus("Opponent found! Initializing battlefield...");
            if (PhotonNetwork.IsMasterClient)
            {
                StartCoroutine(StartMatchCountdownRoutine());
            }
        }
        else
        {
            SetMatchmakingStatus("Room created! Waiting for an opponent to join (1/2)...");
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"[MainMenu] Player joined: {newPlayer.NickName}. Total: {PhotonNetwork.CurrentRoom.PlayerCount}/2");
        opponentFound = true;
        StopSearchTimer();
        SetMatchmakingStatus($"Opponent joined ({newPlayer.NickName})! Starting match...");

        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(StartMatchCountdownRoutine());
        }
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        if (pendingOfflineStart)
        {
            pendingOfflineStart = false;
            PhotonNetwork.OfflineMode = true;
        }

        UpdateStatus("Disconnected: " + cause.ToString());
        if (isSearchingForMatch)
        {
            SetMatchmakingStatus("Connection lost: " + cause.ToString() + ". Reconnecting...");
            StartCoroutine(ReconnectRoutine());
        }
    }

    /// <summary>
    /// Shows a countdown while searching for a real opponent. If nobody joins within
    /// MATCH_SEARCH_TIMEOUT seconds, the player is sent into a match against the AI.
    /// </summary>
    private IEnumerator MatchSearchTimeoutRoutine()
    {
        float remaining = MATCH_SEARCH_TIMEOUT;
        while (remaining > 0f)
        {
            if (!isSearchingForMatch || opponentFound || isStartingMatch)
            {
                matchSearchRoutine = null;
                yield break;
            }

            SetMatchmakingStatus($"Searching for opponent... {Mathf.CeilToInt(remaining)}s");
            yield return null;
            remaining -= Time.unscaledDeltaTime;
        }

        matchSearchRoutine = null;
        if (!isSearchingForMatch || opponentFound || isStartingMatch) yield break;

        FallBackToAI();
    }

    private void StopSearchTimer()
    {
        if (matchSearchRoutine != null)
        {
            StopCoroutine(matchSearchRoutine);
            matchSearchRoutine = null;
        }
    }

    private void FallBackToAI()
    {
        if (fellBackToAI) return;
        fellBackToAI = true;
        isSearchingForMatch = false;

        Debug.Log("[MainMenu] No opponent found within " + MATCH_SEARCH_TIMEOUT + "s. Starting match vs AI.");
        SetMatchmakingStatus("No opponent found. Starting battle vs AI...");
        OnPlayVsAIClicked();
    }

    private IEnumerator ReconnectRoutine()
    {
        yield return new WaitForSeconds(2f);
        if (isSearchingForMatch && !PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    private IEnumerator StartMatchCountdownRoutine()
    {
        if (isStartingMatch) yield break;
        isStartingMatch = true;

        if (juicyUI != null)
        {
            string oppName = "Enemy Commander";
            if (PhotonNetwork.PlayerListOthers != null && PhotonNetwork.PlayerListOthers.Length > 0)
            {
                oppName = PhotonNetwork.PlayerListOthers[0].NickName;
            }
            bool animDone = false;
            juicyUI.PlayMatchFoundSequence(oppName, () => animDone = true);
            while (!animDone) yield return null;
        }
        else
        {
            SetMatchmakingStatus("MATCH FOUND! Entering battlefield...");
            yield return new WaitForSecondsRealtime(0.4f);
        }

        Debug.Log("[MainMenu] MasterClient loading battle level: " + BATTLE_SCENE_NAME);
        PhotonNetwork.LoadLevel(BATTLE_SCENE_NAME);
    }

    #endregion

    private void UpdateStatus(string message)
    {
        if (connectionStatusText != null)
        {
            connectionStatusText.text = message;
        }
    }

    private void SetMatchmakingStatus(string status)
    {
        if (matchmakingStatusText != null)
        {
            matchmakingStatusText.text = status;
        }
    }

    #region Procedural UI Fallback Builder

    private void BuildRuntimeMenuUI()
    {
        // Create Canvas if none in scene
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("MainMenuCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Ensure EventSystem
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // Main Panel
        mainPanel = new GameObject("MainPanel");
        mainPanel.transform.SetParent(canvas.transform, false);
        RectTransform mainRect = mainPanel.AddComponent<RectTransform>();
        mainRect.anchorMin = Vector2.zero;
        mainRect.anchorMax = Vector2.one;
        mainRect.offsetMin = Vector2.zero;
        mainRect.offsetMax = Vector2.zero;

        Image bgImage = mainPanel.AddComponent<Image>();
        bgImage.color = new Color(0.08f, 0.10f, 0.15f, 0.95f);

        // Header / Title
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(mainPanel.transform, false);
        TMP_Text title = titleObj.AddComponent<TextMeshProUGUI>();
        title.text = "SIEGE: REALMS AT WAR";
        title.fontSize = 54;
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Center;
        title.color = new Color(1f, 0.85f, 0.35f, 1f);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.82f);
        titleRect.anchorMax = new Vector2(0.5f, 0.82f);
        titleRect.sizeDelta = new Vector2(900, 100);

        // Subtitle
        GameObject subObj = new GameObject("SubtitleText");
        subObj.transform.SetParent(mainPanel.transform, false);
        TMP_Text sub = subObj.AddComponent<TextMeshProUGUI>();
        sub.text = "Real-Time 1v1 Castle Defense & Strategy";
        sub.fontSize = 24;
        sub.alignment = TextAlignmentOptions.Center;
        sub.color = new Color(0.7f, 0.8f, 0.95f, 0.85f);
        RectTransform subRect = subObj.GetComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0.5f, 0.74f);
        subRect.anchorMax = new Vector2(0.5f, 0.74f);
        subRect.sizeDelta = new Vector2(600, 50);

        // Nickname Input Box
        GameObject nickObj = new GameObject("NicknameInput");
        nickObj.transform.SetParent(mainPanel.transform, false);
        Image nickBg = nickObj.AddComponent<Image>();
        nickBg.color = new Color(0.15f, 0.18f, 0.25f, 1f);
        RectTransform nickRect = nickObj.GetComponent<RectTransform>();
        nickRect.anchorMin = new Vector2(0.5f, 0.62f);
        nickRect.anchorMax = new Vector2(0.5f, 0.62f);
        nickRect.sizeDelta = new Vector2(420, 60);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(nickObj.transform, false);
        TMP_Text inputText = textObj.AddComponent<TextMeshProUGUI>();
        inputText.fontSize = 24;
        inputText.alignment = TextAlignmentOptions.Center;
        inputText.color = Color.white;
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(15, 5);
        textRect.offsetMax = new Vector2(-15, -5);

        nicknameInput = nickObj.AddComponent<TMP_InputField>();
        nicknameInput.textComponent = inputText;

        // Button 1: Play vs AI
        playVsAIButton = CreateButton(mainPanel.transform, "PlayVsAIButton", "PLAY VS AI (SOLO)", new Vector2(0.5f, 0.48f), new Color(0.2f, 0.5f, 0.25f, 1f));

        // Button 2: Play Multiplayer
        playMultiplayerButton = CreateButton(mainPanel.transform, "PlayMultiplayerButton", "PLAY MULTIPLAYER (1v1)", new Vector2(0.5f, 0.36f), new Color(0.18f, 0.45f, 0.85f, 1f));

        // Connection status
        GameObject statusObj = new GameObject("ConnectionStatusText");
        statusObj.transform.SetParent(mainPanel.transform, false);
        connectionStatusText = statusObj.AddComponent<TextMeshProUGUI>();
        connectionStatusText.fontSize = 20;
        connectionStatusText.alignment = TextAlignmentOptions.Center;
        connectionStatusText.color = new Color(0.6f, 0.7f, 0.8f, 0.8f);
        RectTransform statusRect = statusObj.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.5f, 0.22f);
        statusRect.anchorMax = new Vector2(0.5f, 0.22f);
        statusRect.sizeDelta = new Vector2(600, 40);

        // AppId Warning
        GameObject warnObj = new GameObject("AppIdWarningText");
        warnObj.transform.SetParent(mainPanel.transform, false);
        appIdWarningText = warnObj.AddComponent<TextMeshProUGUI>();
        appIdWarningText.fontSize = 18;
        appIdWarningText.alignment = TextAlignmentOptions.Center;
        appIdWarningText.color = new Color(1f, 0.4f, 0.3f, 1f);
        RectTransform warnRect = warnObj.GetComponent<RectTransform>();
        warnRect.anchorMin = new Vector2(0.5f, 0.12f);
        warnRect.anchorMax = new Vector2(0.5f, 0.12f);
        warnRect.sizeDelta = new Vector2(900, 60);

        // Matchmaking Overlay Panel
        matchmakingPanel = new GameObject("MatchmakingPanel");
        matchmakingPanel.transform.SetParent(canvas.transform, false);
        RectTransform matchRect = matchmakingPanel.AddComponent<RectTransform>();
        matchRect.anchorMin = Vector2.zero;
        matchRect.anchorMax = Vector2.one;
        matchRect.offsetMin = Vector2.zero;
        matchRect.offsetMax = Vector2.zero;

        Image matchBg = matchmakingPanel.AddComponent<Image>();
        matchBg.color = new Color(0.05f, 0.07f, 0.12f, 0.98f);

        GameObject mStatusObj = new GameObject("MatchmakingStatusText");
        mStatusObj.transform.SetParent(matchmakingPanel.transform, false);
        matchmakingStatusText = mStatusObj.AddComponent<TextMeshProUGUI>();
        matchmakingStatusText.text = "Searching for 1v1 match...";
        matchmakingStatusText.fontSize = 32;
        matchmakingStatusText.fontStyle = FontStyles.Bold;
        matchmakingStatusText.alignment = TextAlignmentOptions.Center;
        matchmakingStatusText.color = Color.white;
        RectTransform mStatusRect = mStatusObj.GetComponent<RectTransform>();
        mStatusRect.anchorMin = new Vector2(0.5f, 0.58f);
        mStatusRect.anchorMax = new Vector2(0.5f, 0.58f);
        mStatusRect.sizeDelta = new Vector2(800, 100);

        cancelMatchmakingButton = CreateButton(matchmakingPanel.transform, "CancelButton", "CANCEL SEARCH", new Vector2(0.5f, 0.40f), new Color(0.7f, 0.2f, 0.2f, 1f));

        matchmakingPanel.SetActive(false);
    }

    private Button CreateButton(Transform parent, string name, string label, Vector2 anchor, Color bgColor)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        Image img = btnObj.AddComponent<Image>();
        img.color = bgColor;

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.normalColor = bgColor;
        colors.highlightedColor = bgColor * 1.2f;
        colors.pressedColor = bgColor * 0.8f;
        btn.colors = colors;

        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.sizeDelta = new Vector2(360, 65);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        TMP_Text txt = textObj.AddComponent<TextMeshProUGUI>();
        txt.text = label;
        txt.fontSize = 22;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;

        RectTransform tRect = textObj.GetComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero;
        tRect.anchorMax = Vector2.one;
        tRect.offsetMin = Vector2.zero;
        tRect.offsetMax = Vector2.zero;

        return btn;
    }

    #endregion
}

