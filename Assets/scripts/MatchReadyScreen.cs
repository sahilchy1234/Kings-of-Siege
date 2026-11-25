using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using Siege.Profile;

/// <summary>
/// Pre-match Waiting & Synchronization Screen in BattleScene:
/// - Controls the portrait animated BattelVS screen using DOTween.
/// - Automatically waits for opponent in 1v1 Online Multiplayer.
/// - Immediately starts the match countdown as soon as both players are connected.
/// - Synchronizes real player nicknames to both BattelVS and the top HUD ('playername' and 'opponentname').
/// - Cleans up legacy UI panels so BattelVS is the singular, clean portrait ready screen.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PhotonView))]
public class MatchReadyScreen : MonoBehaviourPunCallbacks
{
    private static MatchReadyScreen instance;
    public static MatchReadyScreen Instance => instance;

    /// <summary>True once both players are ready/connected and the countdown finishes.</summary>
    public static bool IsMatchStarted { get; private set; } = false;
    public static bool IsWaitingForReady => !IsMatchStarted;

    [Header("New Portrait Battle VS Panel (DOTween)")]
    [SerializeField] private BattelVS battelVS;

    [Header("UI Hierarchy Root")]
    [SerializeField] private GameObject readyRootPanel;
    [SerializeField] private CanvasGroup readyCanvasGroup;

    [Header("Scene HUD Real Names (Top Score Header)")]
    [Tooltip("The 'playername' TextMeshProUGUI in BattleScene Canvas (left side).")]
    [SerializeField] private TMP_Text hudPlayerName;
    [Tooltip("The 'opponentname' TextMeshProUGUI in BattleScene Canvas (right side).")]
    [SerializeField] private TMP_Text hudOpponentName;

    [Header("Avatar Pools")]
    [Tooltip("Same order as the profile page avatars (PlayerProfileData.AvatarIndex). Shown on the player side.")]
    [SerializeField] private Sprite[] playerAvatarPool;
    [Tooltip("Avatars a random fake opponent can use in VS AI matches.")]
    [SerializeField] private Sprite[] opponentAvatarPool;

    /// <summary>Fake opponent name chosen by the main menu for the current VS AI match.</summary>
    public static string PendingAiOpponentName { get; private set; }

    /// <summary>Opponent name currently shown in the battle HUD (used by the match result panel).</summary>
    public string OpponentDisplayName =>
        hudOpponentName != null && !string.IsNullOrEmpty(hudOpponentName.text) ? hudOpponentName.text : (PendingAiOpponentName ?? "Opponent");

    /// <summary>The local player's profile avatar from the avatar pool, or null if the pool is empty.</summary>
    public Sprite LocalAvatarSprite =>
        playerAvatarPool != null && playerAvatarPool.Length > 0
            ? playerAvatarPool[Mathf.Clamp(PlayerProfileData.AvatarIndex, 0, playerAvatarPool.Length - 1)]
            : null;

    /// <summary>The opponent portrait shown on the VS screen for this match (null if none was shown).</summary>
    public Sprite OpponentAvatarSprite { get; private set; }

    private static readonly string[] FakeOpponentNames =
    {
        "Iron_Wolf", "Ash_Crusher", "Vel_Ravager", "StoneHelm", "Nightbane", "Ember_Lord",
        "GraveKnight", "Rook_Prime", "Dusk_Raider", "Thorn_King", "Vanguard_Max", "Silent_Pike"
    };

    /// <summary>Picks a random fake opponent name for a VS AI match. Call once per match before the battle loads.</summary>
    public static string PickAiOpponentName()
    {
        PendingAiOpponentName = FakeOpponentNames[UnityEngine.Random.Range(0, FakeOpponentNames.Length)];
        return PendingAiOpponentName;
    }

    /// <summary>Opponent portrait index chosen by the main menu for the current VS AI match (-1 = not chosen).</summary>
    public static int PendingAiOpponentAvatarIndex = -1;

    private static int lastOpponentAvatarIndex = -1;

    /// <summary>Random opponent portrait that never repeats the previous match's portrait.</summary>
    private static int PickOpponentAvatarIndex(int count)
    {
        if (count <= 1) return 0;
        int idx;
        do
        {
            idx = UnityEngine.Random.Range(0, count);
        } while (idx == lastOpponentAvatarIndex);
        lastOpponentAvatarIndex = idx;
        return idx;
    }

    private PhotonView pv;
    private readonly HashSet<int> readyActorNumbers = new HashSet<int>();
    private bool countdownInProgress = false;
    private bool isMultiplayerMode = false;
    private float dotAnimTimer = 0f;
    private int dotCount = 0;
    private float autoStartCheckTimer = 0f;

    // Palette
    private static readonly Color ColorGold = new Color(1f, 0.85f, 0.2f, 1f);
    private static readonly Color ColorGreenReady = new Color(0.15f, 0.85f, 0.35f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstanceInBattleScene()
    {
        if (SceneManager.GetActiveScene().name == "BattleScene")
        {
            if (FindFirstObjectByType<MatchReadyScreen>() == null)
            {
                GameObject go = new GameObject("[MatchReadyScreen]");
                go.AddComponent<PhotonView>();
                go.AddComponent<MatchReadyScreen>();
                Debug.Log("[MatchReadyScreen] Auto-spawned runtime Ready Screen in BattleScene.");
            }
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        IsMatchStarted = false;
        CameraSwipeController.SuppressSwipe = true;

        pv = GetComponent<PhotonView>();
        if (pv == null) pv = gameObject.AddComponent<PhotonView>();

        // Ensure background execution and keep-alive
        Application.runInBackground = true;
        PhotonNetwork.KeepAliveInBackground = 60000;

        if (PhotonNetwork.NetworkingClient != null && PhotonNetwork.NetworkingClient.LoadBalancingPeer != null)
        {
            PhotonNetwork.NetworkingClient.LoadBalancingPeer.DisconnectTimeout = 60000;
            PhotonNetwork.NetworkingClient.LoadBalancingPeer.SentCountAllowance = 25;
        }

        bool online = PhotonNetwork.IsConnected && !PhotonNetwork.OfflineMode && PhotonNetwork.InRoom;
        isMultiplayerMode = (GameModeManager.IsMultiplayer || online) && online;

        // Auto-find HUD elements
        FindHudNameReferences();

        // Bind root panel and BattelVS
        EnsureUIReferencesBound();
    }

    private void Start()
    {
        IsMatchStarted = false;
        CameraSwipeController.SuppressSwipe = true;

        // Clean legacy elements (Header, VSContainer, BottomControls) so BattelVS is isolated
        CleanOldMatchReadyElements();

        // Dynamically populate real player names in HUD and BattelVS
        UpdateHUDPlayerNames();
        RefreshPlayerDisplay();
        ApplyLocalAvatar();

        // Trigger entrance animation with DOTween
        if (battelVS != null)
        {
            battelVS.PlayEntranceAnimation();
        }

        if (!isMultiplayerMode)
        {
            // Solo / VS AI: start match countdown immediately
            ConfigureSingleplayerMode();
            StartCoroutine(CountdownRoutine());
        }
        else
        {
            // Multiplayer: check if 2 players are already in the room
            CheckAutoStartMultiplayer();
        }
    }

    private void Update()
    {
        if (IsMatchStarted) return;

        // Active KeepAlive ping to prevent WebGL browser socket disconnects (Error 1006 / AppOutOfFocus)
        if (PhotonNetwork.IsConnected && !PhotonNetwork.OfflineMode)
        {
            if (PhotonNetwork.NetworkingClient != null && PhotonNetwork.NetworkingClient.LoadBalancingPeer != null)
            {
                PhotonNetwork.NetworkingClient.LoadBalancingPeer.SendAcksOnly();
            }
            PhotonNetwork.SendAllOutgoingCommands();
        }

        if (!isMultiplayerMode || countdownInProgress) return;

        // Dot animation while waiting for opponent
        dotAnimTimer += Time.unscaledDeltaTime;
        if (dotAnimTimer >= 0.45f)
        {
            dotAnimTimer = 0f;
            dotCount = (dotCount + 1) % 4;
            AnimateWaitingDots();
        }

        // Periodic auto-start check in multiplayer
        autoStartCheckTimer += Time.unscaledDeltaTime;
        if (autoStartCheckTimer >= 0.5f)
        {
            autoStartCheckTimer = 0f;
            RefreshPlayerDisplay();
            CheckAutoStartMultiplayer();
        }
    }

    private void AnimateWaitingDots()
    {
        int playerCount = (PhotonNetwork.CurrentRoom != null) ? PhotonNetwork.CurrentRoom.PlayerCount : 1;
        if (playerCount < 2 && battelVS != null)
        {
            string dots = new string('.', dotCount);
            battelVS.SetPlayer2($"Waiting for Opponent{dots}", "SEARCHING...");
        }
    }

    /// <summary>
    /// If both players are present in the multiplayer room, MasterClient initiates the battle countdown immediately.
    /// </summary>
    private void CheckAutoStartMultiplayer()
    {
        if (countdownInProgress || !isMultiplayerMode) return;

        int playerCount = (PhotonNetwork.CurrentRoom != null) ? PhotonNetwork.CurrentRoom.PlayerCount : 0;
        if (playerCount >= 2)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                Debug.Log("<color=#00FFAA>[MatchReadyScreen] Both players connected! Triggering battle countdown immediately!</color>");
                if (pv != null)
                {
                    pv.RPC(nameof(RPC_StartBattleCountdown), RpcTarget.All);
                }
                else
                {
                    StartCoroutine(CountdownRoutine());
                }
            }
        }
    }

    #region Real Player Names & HUD Synchronization

    public void FindHudNameReferences()
    {
        if (hudPlayerName == null)
        {
            GameObject pGo = GameObject.Find("playername");
            if (pGo != null) hudPlayerName = pGo.GetComponent<TMP_Text>();
        }

        if (hudOpponentName == null)
        {
            GameObject oGo = GameObject.Find("opponentname");
            if (oGo != null) hudOpponentName = oGo.GetComponent<TMP_Text>();
        }
    }

    public void UpdateHUDPlayerNames()
    {
        FindHudNameReferences();

        // 1. Local Player Nickname
        string localNick = GetLocalPlayerNickname();
        if (hudPlayerName != null)
        {
            hudPlayerName.text = localNick;
        }

        // 2. Opponent Nickname
        string opponentNick = PendingAiOpponentName ?? "AI Commander";
        try
        {
            if (PhotonNetwork.NetworkingClient != null && PhotonNetwork.IsConnected && !PhotonNetwork.OfflineMode && PhotonNetwork.InRoom)
            {
                Player[] others = PhotonNetwork.PlayerListOthers;
                if (others != null && others.Length > 0 && others[0] != null && !string.IsNullOrEmpty(others[0].NickName))
                {
                    opponentNick = others[0].NickName;
                }
                else
                {
                    opponentNick = "Waiting for Opponent...";
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[MatchReadyScreen] Safe Photon opponent check fallback: {ex.Message}");
        }

        if (hudOpponentName != null)
        {
            hudOpponentName.text = opponentNick;
        }

        Debug.Log($"[MatchReadyScreen] Updated HUD Names: Player='{localNick}', Opponent='{opponentNick}'");
    }

    public static string GetLocalPlayerNickname()
    {
        try
        {
            if (PhotonNetwork.NetworkingClient != null && PhotonNetwork.IsConnected && !string.IsNullOrEmpty(PhotonNetwork.NickName))
                return PhotonNetwork.NickName;
        }
        catch {}

        string saved = PlayerPrefs.GetString("PUN_PlayerNickName", "");
        if (!string.IsNullOrEmpty(saved))
            return saved;

        saved = PlayerPrefs.GetString("PlayerName", "");
        if (!string.IsNullOrEmpty(saved))
            return saved;

        return "Player 1";
    }

    #endregion

    #region Mode Configuration & Player Display

    private void ConfigureSingleplayerMode()
    {
        string localName = GetLocalPlayerNickname();
        string oppName = PendingAiOpponentName ?? PickAiOpponentName();
        if (battelVS != null)
        {
            battelVS.SetPlayer1(localName, "COMMANDER");
            battelVS.SetPlayer2(oppName, "AUTONOMOUS");

            ApplyLocalAvatar();
            if (opponentAvatarPool != null && opponentAvatarPool.Length > 0)
            {
                // Use the portrait index chosen in the main menu so both panels show the same face.
                int len = opponentAvatarPool.Length;
                int avIdx = PendingAiOpponentAvatarIndex >= 0 ? PendingAiOpponentAvatarIndex : PickOpponentAvatarIndex(len);
                OpponentAvatarSprite = opponentAvatarPool[Mathf.Clamp(avIdx, 0, len - 1)];
                battelVS.SetPlayer2Avatar(OpponentAvatarSprite);
            }
        }
        UpdateHUDPlayerNames();
    }

    /// <summary>Shows the real player's chosen profile avatar on the player (blue) side.</summary>
    private void ApplyLocalAvatar()
    {
        if (battelVS == null || playerAvatarPool == null || playerAvatarPool.Length == 0) return;
        int idx = Mathf.Clamp(PlayerProfileData.AvatarIndex, 0, playerAvatarPool.Length - 1);
        battelVS.SetPlayer1Avatar(playerAvatarPool[idx]);
    }

    private void RefreshPlayerDisplay()
    {
        UpdateHUDPlayerNames();

        if (!isMultiplayerMode || PhotonNetwork.NetworkingClient == null || !PhotonNetwork.IsConnected) return;

        int playerCount = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.PlayerCount : 1;
        Player[] players = PhotonNetwork.PlayerList;

        // Player 1 (Host / Blue)
        Player p1 = players.Length > 0 ? players[0] : null;
        string p1Name = p1 != null ? p1.NickName : GetLocalPlayerNickname();

        // Player 2 (Guest / Red)
        if (playerCount >= 2 && players.Length > 1)
        {
            Player p2 = players[1];
            string p2Name = p2 != null ? p2.NickName : "Opponent";
            if (battelVS != null)
            {
                battelVS.SetPlayer1(p1Name, "CONNECTED");
                battelVS.SetPlayer2(p2Name, "CONNECTED");
            }
        }
        else
        {
            if (battelVS != null)
            {
                battelVS.SetPlayer1(p1Name, "READY");
                battelVS.SetPlayer2("Waiting for Opponent...", "SEARCHING...");
            }
        }
    }

    #endregion

    #region Network Events & RPCs

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"<color=#00FFAA>[MatchReadyScreen] Opponent connected: {newPlayer.NickName} (Total: {PhotonNetwork.CurrentRoom.PlayerCount}/2)</color>");
        UpdateHUDPlayerNames();

        if (battelVS != null)
        {
            battelVS.PunchOpponentJoined(newPlayer.NickName);
        }

        CheckAutoStartMultiplayer();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (IsMatchStarted || !isMultiplayerMode) return;

        Debug.LogWarning($"[MatchReadyScreen] Opponent {otherPlayer.NickName} left before match started.");
        readyActorNumbers.Remove(otherPlayer.ActorNumber);

        if (battelVS != null)
        {
            battelVS.SetPlayer2("Opponent Disconnected", "WAITING...");
        }

        UpdateHUDPlayerNames();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"[MatchReadyScreen] Disconnected from Photon: {cause}");
        // VS AI runs offline (PhotonNetwork.OfflineMode), so this callback must not replace the AI opponent's name.
        if (!isMultiplayerMode) return;
        if (battelVS != null)
        {
            battelVS.SetPlayer2("Disconnected", $"{cause}");
        }
    }

    [PunRPC]
    private void RPC_StartBattleCountdown()
    {
        if (countdownInProgress) return;
        StartCoroutine(CountdownRoutine());
    }

    private IEnumerator CountdownRoutine()
    {
        countdownInProgress = true;

        // Re-apply the AI opponent's name so the panel always shows the name chosen in the main menu.
        if (!isMultiplayerMode && battelVS != null && PendingAiOpponentName != null)
        {
            battelVS.SetPlayer2(PendingAiOpponentName, "AUTONOMOUS");
        }

        // 3... 2... 1... countdown (only the number pumps, icons stay steady)
        string[] steps = { "3", "2", "1" };
        for (int i = 0; i < steps.Length; i++)
        {
            if (battelVS != null)
            {
                battelVS.PlayCountdownStep(steps[i], ColorGold);
            }
            yield return new WaitForSecondsRealtime(1.0f);
        }

        // BATTLE START!
        if (battelVS != null)
        {
            battelVS.PlayCountdownStep("START!", ColorGreenReady);
        }
        yield return new WaitForSecondsRealtime(0.5f);

        // Smooth DOTween dismiss animation
        bool dismissed = false;
        if (battelVS != null)
        {
            battelVS.PlayDismissAnimation(() => { dismissed = true; });
        }
        else
        {
            dismissed = true;
        }

        float timeout = 0.5f;
        while (!dismissed && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        // Officially Start Match!
        IsMatchStarted = true;
        CameraSwipeController.SuppressSwipe = false;

        if (readyRootPanel != null)
        {
            readyRootPanel.SetActive(false);
        }

        UpdateHUDPlayerNames();
        Debug.Log("<color=#00FFAA>======================================\n BATTLE OFFICIALLY STARTED!\n======================================</color>");
    }

    public void OnExitButtonClicked()
    {
        IsMatchStarted = false;
        CameraSwipeController.SuppressSwipe = false;

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }

        SceneManager.LoadScene("MainMenu");
    }

    #endregion

    #region Hierarchy Binding & UI Cleanup

    public void EnsureUIReferencesBound()
    {
        if (readyRootPanel == null)
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                Transform found = canvas.transform.Find("MatchReadyPanel");
                if (found != null)
                {
                    readyRootPanel = found.gameObject;
                    readyCanvasGroup = found.GetComponent<CanvasGroup>();
                }
            }
        }

        if (readyRootPanel != null)
        {
            Transform bvs = readyRootPanel.transform.Find("BatteVS");
            if (bvs == null) bvs = readyRootPanel.transform.Find("BattelVS");
            if (bvs != null)
            {
                battelVS = bvs.GetComponent<BattelVS>();
                if (battelVS == null) battelVS = bvs.gameObject.AddComponent<BattelVS>();
                battelVS.InitializeReferences();
            }
        }

        FindHudNameReferences();
    }

    /// <summary>
    /// Removes legacy Header, VSContainer, BottomControls, CountdownText so BattelVS is isolated.
    /// </summary>
    public void CleanOldMatchReadyElements()
    {
        EnsureUIReferencesBound();
        if (readyRootPanel == null) return;

        string[] oldNames = { "Header", "VSContainer", "BottomControls", "CountdownText" };
        foreach (var n in oldNames)
        {
            Transform child = readyRootPanel.transform.Find(n);
            if (child != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.Undo.DestroyObjectImmediate(child.gameObject);
                else
                    Destroy(child.gameObject);
#else
                Destroy(child.gameObject);
#endif
            }
        }

        // Remove dark vignette background on MatchReadyPanel if present so BattelVS visuals dominate
        Image bgImg = readyRootPanel.GetComponent<Image>();
        if (bgImg != null)
        {
            bgImg.color = Color.clear;
            bgImg.raycastTarget = false;
        }

        if (battelVS != null)
        {
            battelVS.InitializeReferences();
            battelVS.ApplyResponsivePortraitLayout();
        }
    }

    #endregion
}
