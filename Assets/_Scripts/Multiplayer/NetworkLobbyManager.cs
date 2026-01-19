using Fusion;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class NetworkLobbyManager : NetworkBehaviour
{
    public static NetworkLobbyManager Instance { get; private set; }

    [Header("Game Settings")]
    public int minPlayersToStart = 2;
    [Networked] public NetworkDictionary<PlayerRef, PlayerLobbyData> LobbyPlayers { get; } 

    [Networked] public string JoinCode { get; private set; }
    [Networked] public int SelectedMapIndex { get; set; }
    [Networked] public int SelectedModeIndex { get; set; }
    [Networked] public int SelectedTimeIndex { get; set; }

    private readonly List<string> mapOptions = new List<string> { "Map 1", "Map 2", "Map 3" };
    private readonly List<string> timeOptions = new List<string> { "3:00", "5:00", "10:00" };
    private readonly int[] timeInSeconds = { 180, 300, 600 };

    private LobbyUIManager uiManager;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void Spawned()
    {
        base.Spawned();
        uiManager = LobbyUIManager.Instance;

        if (Runner.IsServer)
        {
            // Host generates a join code
            JoinCode = GenerateJoinCode();
            
            // Add self to lobby
            AddPlayerToLobby(Runner.LocalPlayer, true);
        }

        // All clients initialize UI
        var modeOptions = System.Enum.GetNames(typeof(GameMode)).ToList();
        uiManager.InitializeLobbyUI(mapOptions, modeOptions, timeOptions);
        uiManager.SetJoinCode(JoinCode);
        uiManager.ShowLobby(true);


        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.OnGameStarted += OnGameStarted;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.OnGameStarted -= OnGameStarted;
        }
    }

    public void AddPlayerToLobby(PlayerRef player, bool isHost)
    {
        string playerName = $"Player_{player.PlayerId}";
        var data = new PlayerLobbyData { PlayerName = playerName, IsHost = isHost, IsReady = false };
        LobbyPlayers.Add(player, data);
    }

    // UI Callbacks
    public void OnMapSelectionChanged(int index) => RPC_SetGameSetting(nameof(SelectedMapIndex), index);
    public void OnModeSelectionChanged(int index) => RPC_SetGameSetting(nameof(SelectedModeIndex), index);
    public void OnTimeSelectionChanged(int index) => RPC_SetGameSetting(nameof(SelectedTimeIndex), index);

    public void ToggleReadyStatus()
    {
        RPC_SetPlayerReady(!LobbyPlayers[Runner.LocalPlayer].IsReady);
    }

    public void StartGame()
    {
        if (Runner.IsServer)
        {
            // Start the game with selected settings
            GameMode gameMode = (GameMode)SelectedModeIndex;
            int gameTime = timeInSeconds[SelectedTimeIndex];
            string sceneName = mapOptions[SelectedMapIndex];

            // TODO: Replace with actual scene loading logic
            Debug.Log($"Starting game on {sceneName} with mode {gameMode} for {gameTime}s");
            NetworkGameManager.Instance.StartGame(gameMode, gameTime, sceneName);
        }
    }

    private void OnGameStarted()
    {
        uiManager.ShowLobby(false);
    }

    public override void Render()
    {
        base.Render();
        UpdateLobbyUI();
        UpdateGameSettingsUI();
    }

    private void UpdateLobbyUI()
    {
        if (uiManager == null) return;

        // Update player list
        var playerDict = LobbyPlayers.ToDictionary(kvp => kvp.Key.PlayerId, kvp => kvp.Value);
        uiManager.UpdatePlayerList(playerDict);

        // Update ready button state for local player
        if (LobbyPlayers.ContainsKey(Runner.LocalPlayer))
        {
            var localPlayerData = LobbyPlayers[Runner.LocalPlayer];
            uiManager.SetReadyButtonState(localPlayerData.IsReady);
        }

        // Update start button for host
        if (Runner.IsServer)
        {
            bool allReady = LobbyPlayers.Count >= minPlayersToStart && LobbyPlayers.All(p => p.Value.IsReady);
            uiManager.SetStartButtonState(allReady);
        }
    }

    private void UpdateGameSettingsUI()
    {
        if (uiManager == null) return;

        uiManager.mapDropdown.SetValueWithoutNotify(SelectedMapIndex);
        uiManager.modeDropdown.SetValueWithoutNotify(SelectedModeIndex);
        uiManager.timeDropdown.SetValueWithoutNotify(SelectedTimeIndex);

        bool isHost = Runner.IsServer;
        uiManager.mapDropdown.interactable = isHost;
        uiManager.modeDropdown.interactable = isHost;
        uiManager.timeDropdown.interactable = isHost;
    }

    // RPCs
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SetPlayerReady(bool isReady)
    {
        var data = LobbyPlayers[Runner.LocalPlayer];
        data.IsReady = isReady;
        LobbyPlayers.Set(Runner.LocalPlayer, data);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SetGameSetting(string settingName, int value)
    {
        switch (settingName)
        {
            case nameof(SelectedMapIndex): SelectedMapIndex = value; break;
            case nameof(SelectedModeIndex): SelectedModeIndex = value; break;
            case nameof(SelectedTimeIndex): SelectedTimeIndex = value; break;
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetPlayerTeam(int teamId, RpcInfo info = default)
    {
        if (LobbyPlayers.ContainsKey(info.Source))
        {
            var data = LobbyPlayers[info.Source];
            data.TeamID = teamId;
            LobbyPlayers.Set(info.Source, data);
        }
    }

    // Player Connection Handling
    public void OnPlayerJoined(PlayerRef player)
    {
        if (Runner.IsServer)
        {
            AddPlayerToLobby(player, false);
        }
    }

    public void OnPlayerLeft(PlayerRef player)
    {
        if (Runner.IsServer)
        {
            if (LobbyPlayers.ContainsKey(player))
            {
                LobbyPlayers.Remove(player);
            }
        }
    }

    private string GenerateJoinCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, 6)
          .Select(s => s[Random.Range(0, s.Length)]).ToArray());
    }
}

public struct PlayerLobbyData : INetworkStruct
{
    public NetworkString<_16> PlayerName;
    public bool IsReady;
    public bool IsHost;
    public int TeamID;
}