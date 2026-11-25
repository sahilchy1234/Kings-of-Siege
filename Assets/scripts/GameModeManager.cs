using UnityEngine;

/// <summary>
/// Persists current gameplay mode (Singleplayer vs AI or PUN2 Multiplayer)
/// across scene transitions and tracks local player's assigned team.
/// </summary>
public static class GameModeManager
{
    public enum GameMode
    {
        VsAI,
        Multiplayer
    }

    private static GameMode currentMode = GameMode.VsAI;
    private static int localTeamId = 0; // 0 = Player/Blue team, 1 = Enemy/Red team

    public static GameMode CurrentMode
    {
        get => currentMode;
        set => currentMode = value;
    }

    /// <summary>
    /// Local player's assigned team ID in the active match.
    /// In VsAI mode: always 0 (Player).
    /// In Multiplayer: 0 if MasterClient (Host), 1 if joined guest.
    /// </summary>
    public static int LocalTeamId
    {
        get => localTeamId;
        set => localTeamId = value;
    }

    public static bool IsMultiplayer => currentMode == GameMode.Multiplayer;
    public static bool IsVsAI => currentMode == GameMode.VsAI;

    public static void ResetToDefault()
    {
        currentMode = GameMode.VsAI;
        localTeamId = 0;
    }
}

