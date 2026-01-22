using Fusion;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class NetworkLobbyManager : NetworkBehaviour
{
    public static NetworkLobbyManager Instance { get; private set; }

    [Header("Game Settings")]
    public int minPlayersToStart = 2;
    [Networked, Capacity(8)] public NetworkDictionary<PlayerRef, PlayerLobbyData> LobbyPlayers { get; } 

    [Networked] public string JoinCode { get; set; }
    
    // Generate a random join code
    public string GenerateJoinCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(Enumerable.Repeat(chars, 6)
            .Select(s => s[UnityEngine.Random.Range(0, s.Length)]).ToArray());
    }
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
        Debug.Log($"[NetworkLobbyManager] Spawned called. Object: {gameObject.name}, IsServer: {Runner.IsServer}, LocalPlayer: {Runner.LocalPlayer}");
        
        uiManager = LobbyUIManager.Instance;
        Debug.Log($"[NetworkLobbyManager] UI Manager found: {uiManager != null}");
        
        if (uiManager == null)
        {
            Debug.LogError("[NetworkLobbyManager] LobbyUIManager.Instance is null! Make sure it exists in the scene.");
        }

        // Only the host generates a join code
        if (Runner.IsServer)
        {
            Debug.Log("[NetworkLobbyManager] This is the server (host)");
            
            // Immediately show the lobby UI for host
            if (uiManager != null)
            {
                uiManager.ShowLobby(true);
            }
            
            // Get the join code from NetworkStarter instead of generating a new one
            var networkStarter = FindObjectOfType<NetworkStarter>();
            if (networkStarter != null && !string.IsNullOrEmpty(networkStarter.CurrentJoinCode))
            {
                JoinCode = networkStarter.CurrentJoinCode;
                Debug.Log($"[NetworkLobbyManager] Using join code from NetworkStarter: {JoinCode}");
            }
            else if (string.IsNullOrEmpty(JoinCode))
            {
                // Fallback to generating a new code if not provided by NetworkStarter
                JoinCode = GenerateJoinCode();
                Debug.Log($"[NetworkLobbyManager] Generated new Join Code: {JoinCode}");
            }
            
            // Update UI with join code immediately
            if (uiManager != null)
            {
                uiManager.SetJoinCode(JoinCode);
            }
            
            // Add self to lobby
            Debug.Log("[NetworkLobbyManager] Adding host player to lobby");
            AddPlayerToLobby(Runner.LocalPlayer, true);
        }
        else
        {
            Debug.Log("[NetworkLobbyManager] This is a client");
        }

        // Initialize UI only after room is fully created
        if (uiManager != null)
        {
            // Hide lobby panel initially
            uiManager.ShowLobby(false);
            
            // Set up UI callbacks
            var modeOptions = System.Enum.GetNames(typeof(GameMode)).ToList();
            bool isHost = Runner.IsServer;
            
            // Initialize UI with host/client specific settings
            uiManager.InitializeLobbyUI(mapOptions, modeOptions, timeOptions, isHost);
            
            // If this is the host, 
            if (isHost && LobbyPlayers.ContainsKey(Runner.LocalPlayer))
            {
                var hostData = LobbyPlayers[Runner.LocalPlayer];
                hostData.IsReady = true;
                LobbyPlayers.Set(Runner.LocalPlayer, hostData);
                
                // Update the UI for all clients
                RPC_UpdateLobbyUI();
                
                // Show lobby panel for host after setup
                uiManager.ShowLobby(true);
            }
            
            // Update UI with join code if we have one
            if (!string.IsNullOrEmpty(JoinCode))
            {
                Debug.Log($"[NetworkLobbyManager] Setting join code in UI: {JoinCode}");
                uiManager.SetJoinCode(JoinCode);
            }
            else
            {
                Debug.Log("[NetworkLobbyManager] No join code to display");
            }
        }
        else
        {
            Debug.LogError("[NetworkLobbyManager] UI Manager is null!");
        }

        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.OnGameStarted += OnGameStarted;
        }

        // If we are a client (not the host/server), we need to tell the host our name
        if (Runner.IsClient && !Runner.IsServer)
        {
            string playerName = PlayerPrefs.GetString("PlayerName", "Unknown Player");
            Debug.Log($"[NetworkLobbyManager] Sending player name to host: {playerName}");
            RPC_SetLobbyPlayerName(playerName);
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

    public void AddPlayerToLobby(PlayerRef player, bool isHost = false)
    {
        if (LobbyPlayers.ContainsKey(player)) return;
        
        string initialName = $"Player {LobbyPlayers.Count + 1}";
        
        // If adding self (Host), use saved name
        if (player == Runner.LocalPlayer)
        {
             initialName = PlayerPrefs.GetString("PlayerName", initialName);
        }

        var playerData = new PlayerLobbyData
        {
            PlayerName = initialName,
            IsReady = isHost, // Host is automatically ready
            IsHost = isHost
        };
        
        LobbyPlayers.Add(player, playerData);
        Debug.Log($"[NetworkLobbyManager] Added player {player.PlayerId} to lobby. IsHost: {isHost}");
        
        // If this is the host, update their ready status in the UI immediately
        if (isHost && uiManager != null)
        {
            uiManager.SetReadyButtonState(true);
            
            // Enable start button for host
            var startButton = uiManager.GetComponentInChildren<Button>(true);
            if (startButton != null)
            {
                startButton.interactable = true;
            }
        }
        
        // The UI will be updated when the client sends its name via RPC_SetLobbyPlayerName
    }

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

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdateLobbyUI()
    {
        if (uiManager != null && Runner != null)
        {
            // Create a dictionary with all players in the lobby
            var players = new Dictionary<int, PlayerLobbyData>();
            foreach (var kvp in LobbyPlayers)
            {
                players[kvp.Key.PlayerId] = kvp.Value;
                Debug.Log($"[RPC_UpdateLobbyUI] Adding player {kvp.Key.PlayerId}: {kvp.Value.PlayerName} (Host: {kvp.Value.IsHost}, Ready: {kvp.Value.IsReady})");
            }
            
            // Update the player list in the UI
            uiManager.UpdatePlayerList(players);
            
            // Update ready button state for local player
            if (LobbyPlayers.ContainsKey(Runner.LocalPlayer))
            {
                var localPlayerData = LobbyPlayers[Runner.LocalPlayer];
                uiManager.SetReadyButtonState(localPlayerData.IsReady);
            }

            // Update start button state for host
            if (Runner.IsServer)
            {
                bool allReady = LobbyPlayers.Count >= minPlayersToStart && 
                              LobbyPlayers.All(p => p.Value.IsReady);
                uiManager.SetStartButtonState(allReady);
            }
        }
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
    public void RPC_SetLobbyPlayerName(string name, RpcInfo info = default)
    {
        Debug.Log($"[NetworkLobbyManager] Received name update from {info.Source}: {name}");
        if (LobbyPlayers.ContainsKey(info.Source))
        {
            var data = LobbyPlayers[info.Source];
            data.PlayerName = name;
            LobbyPlayers.Set(info.Source, data);

            // After updating the name, force a UI update for all clients
            RPC_UpdateLobbyUI();
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
            Debug.Log($"[NetworkLobbyManager] Player {player.PlayerId} joined the lobby");
            AddPlayerToLobby(player, false);
            
            // Force update the UI for all clients
            RPC_UpdateLobbyUI();
            
            // Log the current player count for debugging
            Debug.Log($"[NetworkLobbyManager] Total players in lobby: {LobbyPlayers.Count}");
            foreach (var kvp in LobbyPlayers)
            {
                Debug.Log($"- Player {kvp.Key.PlayerId}: {kvp.Value.PlayerName} (Host: {kvp.Value.IsHost}, Ready: {kvp.Value.IsReady})");
            }
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
}

public struct PlayerLobbyData : INetworkStruct
{
    public NetworkString<_16> PlayerName;
    public bool IsReady;
    public bool IsHost;
    public int TeamID;
}