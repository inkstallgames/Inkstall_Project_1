using Fusion;
using Fusion.Sockets;
using System;
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

    [Networked, Capacity(8)]
    public NetworkDictionary<PlayerRef, bool> PlayersReadyToLoad { get; }

    [Networked]
    public int PlayersLoadedCount { get; set; }

    [Networked]
    public NetworkBool IsGameReady { get; set; }

    private readonly List<string> mapOptions = new List<string> { "Rust" };
    private readonly List<string> timeOptions = new List<string> { "3:00", "5:00", "10:00" };
    private readonly List<Color> playerColors = new List<Color>
    {
        Color.blue,
        Color.yellow,
        Color.magenta,
        Color.cyan,
        new Color(1f, 0.5f, 0f),  // Orange
        Color.white,
        new Color(0.5f, 0f, 1f),  // Purple
        new Color(0f, 1f, 0.5f)   // Teal
    };
    private readonly int[] timeInSeconds = { 180, 300, 600 };

    private LobbyUIManager uiManager;

    public override void Spawned()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

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

        if (Instance == this)
        {
            Instance = null;
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

        // --- Auto-assign team based on which team has fewer members ---
        int teamACount = 0;
        int teamBCount = 0;
        foreach (var kvp in LobbyPlayers)
        {
            if (kvp.Value.TeamID == 0) teamACount++;
            else if (kvp.Value.TeamID == 1) teamBCount++;
        }
        int assignedTeam = (teamBCount < teamACount) ? 1 : 0; // tie or A fewer → Team A
        Debug.Log($"[NetworkLobbyManager] Auto-assigning player {player.PlayerId} to Team {assignedTeam} (A:{teamACount} B:{teamBCount})");

        var playerData = new PlayerLobbyData
        {
            PlayerName = initialName,
            IsHost = isHost,
            IsReady = isHost, // Host is always ready
            TeamID = assignedTeam
        };
        
        LobbyPlayers.Add(player, playerData);
        
        // Set the networked PlayerColor property using the retrieve-modify-set pattern
        // Use modulo to cycle through available colors if we have more players than colors
        int colorIndex = (LobbyPlayers.Count - 1) % playerColors.Count;
        Color assignedColor = playerColors[colorIndex];
        Debug.Log($"[NetworkLobbyManager] Assigning color at index {colorIndex}: {assignedColor} (R:{assignedColor.r}, G:{assignedColor.g}, B:{assignedColor.b})");
        
        var data = LobbyPlayers[player];
        data.PlayerColor = assignedColor;
        LobbyPlayers.Set(player, data);
        
        // Verify the color was set
        var verifyData = LobbyPlayers[player];
        Debug.Log($"[NetworkLobbyManager] Added player {player.PlayerId} to lobby. IsHost: {isHost}, Team: {assignedTeam}, Assigned Color: {assignedColor}, Stored Color: {verifyData.PlayerColor}");
        
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

    /// <summary>
    /// Called by the Switch Team button in the lobby UI.
    /// Flips the local player to the opposite team and notifies the server.
    /// </summary>
    public void SwitchTeam()
    {
        if (!LobbyPlayers.ContainsKey(Runner.LocalPlayer)) return;
        int currentTeam = LobbyPlayers[Runner.LocalPlayer].TeamID;
        int newTeam = (currentTeam == 0) ? 1 : 0;
        Debug.Log($"[NetworkLobbyManager] Local player switching from Team {currentTeam} to Team {newTeam}");
        RPC_SetPlayerTeam(newTeam);
    }

    public void OnMapSelectionChanged(int index) => RPC_SetGameSetting(nameof(SelectedMapIndex), index);
    public void OnModeSelectionChanged(int index) => RPC_SetGameSetting(nameof(SelectedModeIndex), index);
    public void OnTimeSelectionChanged(int index) => RPC_SetGameSetting(nameof(SelectedTimeIndex), index);

    public void ToggleReadyStatus()
    {
        if (LobbyPlayers.ContainsKey(Runner.LocalPlayer))
        {
            bool newReadyState = !LobbyPlayers[Runner.LocalPlayer].IsReady;
            RPC_SetPlayerReady(newReadyState);

            // Immediately update the local UI for better responsiveness
            if (uiManager != null)
            {
                uiManager.SetReadyButtonState(newReadyState);
            }
        }
    }

        public void StartGame()
    {
        Debug.Log($"[NetworkLobbyManager] StartGame called. IsServer: {Runner?.IsServer}");

        if (!Runner.IsServer)
        {
            Debug.LogError("[NetworkLobbyManager] Only the server can start the game!");
            return;
        }

        // Hero selection is disabled — spawn players directly with playerPrefab
        Debug.Log("[NetworkLobbyManager] Skipping hero selection. Loading map immediately...");
        
        // Validate map selection
        if (SelectedMapIndex < 0 || SelectedMapIndex >= mapOptions.Count)
        {
            Debug.LogError($"[NetworkLobbyManager] Invalid map index: {SelectedMapIndex}. Defaulting to index 0.");
            SelectedMapIndex = 0;
        }

        string sceneName = mapOptions[SelectedMapIndex];
        Debug.Log($"[NetworkLobbyManager] Loading map: {sceneName}");

        // Notify all clients the game is starting (shows loading screen etc.)
        RPC_NotifyGameStarting();

        // Reset loading state for the new game
        PlayersLoadedCount = 0;
        IsGameReady = false;

        // Notify NetworkGameManager to start the game (loads the scene)
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.StartGame(
                (GameMode)SelectedModeIndex,
                new int[] { 180, 300, 600 }[SelectedTimeIndex],
                sceneName
            );
        }
        else
        {
            Debug.LogError("[NetworkLobbyManager] NetworkGameManager.Instance is null! Cannot start game.");
        }
    }

    private void OnGameStarted()
    {
        uiManager.ShowLobby(false);
    }

    public override void Render()
    {
        base.Render();

        // Re-acquire UI manager if needed
        if (uiManager == null)
        {
            uiManager = LobbyUIManager.Instance;
        }

        // Update lobby UI
        UpdateLobbyUI();
        UpdateGameSettingsUI();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdateLobbyUI()
    {
        if (uiManager != null && Runner != null && Runner.IsRunning)
        {
            try 
            {
                // Create a dictionary with all players in the lobby
                var players = new Dictionary<int, PlayerLobbyData>();
                foreach (var kvp in LobbyPlayers)
                {
                    if (kvp.Key != default(PlayerRef))
                    {
                        var playerData = kvp.Value;
                        players[kvp.Key.PlayerId] = playerData;
                        Debug.Log($"[RPC_UpdateLobbyUI] Adding player {kvp.Key.PlayerId}: {playerData.PlayerName} (Host: {playerData.IsHost}, Ready: {playerData.IsReady})");
                    }
                }
                
                // Update the player list in the UI
                if (uiManager != null) // Double check uiManager is still valid
                {
                    uiManager.UpdatePlayerList(players);
                    
                    // Update UI based on local player
                    if (LobbyPlayers.ContainsKey(Runner.LocalPlayer))
                    {
                        var localPlayerData = LobbyPlayers[Runner.LocalPlayer];
                        
                        // Only show ready button for non-host players
                        if (uiManager.readyButton != null)
                        {
                            uiManager.readyButton.gameObject.SetActive(!localPlayerData.IsHost);
                            if (!localPlayerData.IsHost)
                            {
                                uiManager.SetReadyButtonState(localPlayerData.IsReady);
                            }
                        }
                        
                        // Show start button only for host
                        if (uiManager.startGameButton != null)
                        {
                            bool isHost = Runner.IsServer;
                            uiManager.startGameButton.gameObject.SetActive(isHost);
                            
                            // Update start button state for host
                            if (isHost)
                            {
                                // Check if testing mode is enabled
                                var networkStarter = NetworkStarter.Instance;
                                bool isTestingMode = networkStarter != null && networkStarter.IsHostOnlyTestingEnabled;
                                
                                // In testing mode, allow starting with 1 player. Otherwise require minPlayersToStart
                                int requiredPlayers = isTestingMode ? 1 : minPlayersToStart;
                                bool allReady = LobbyPlayers.Count >= requiredPlayers && 
                                             LobbyPlayers.All(p => p.Value.IsReady);
                                
                                Debug.Log($"[NetworkLobbyManager] Start button check - Testing Mode: {isTestingMode}, Players: {LobbyPlayers.Count}, Required: {requiredPlayers}, All Ready: {allReady}");
                                uiManager.startGameButton.interactable = allReady;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[RPC_UpdateLobbyUI] Error updating UI: {e.Message}\n{e.StackTrace}");
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
        }
    }

    private void UpdateGameSettingsUI()
    {
        if (uiManager == null) return;

        // Update button interactability based on host status
        bool isHost = Runner.IsServer;
        
        if (uiManager.mapButton != null)
        {
            uiManager.mapButton.interactable = isHost;
        }
        
        if (uiManager.modeDropdown != null)
        {
            uiManager.modeDropdown.SetValueWithoutNotify(SelectedModeIndex);
            uiManager.modeDropdown.interactable = isHost;
        }
        
        if (uiManager.timeDropdown != null)
        {
            uiManager.timeDropdown.SetValueWithoutNotify(SelectedTimeIndex);
            uiManager.timeDropdown.interactable = isHost;
        }
    }

    // RPCs
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetPlayerReady(bool isReady, RpcInfo info = default)
    {
        var playerRef = info.Source;
        if (playerRef != default && LobbyPlayers.ContainsKey(playerRef))
        {
            var data = LobbyPlayers[playerRef];
            data.IsReady = isReady;
            LobbyPlayers.Set(playerRef, data);
            Debug.Log($"[RPC_SetPlayerReady] Player {playerRef.PlayerId} set ready state to {isReady}");
            RPC_UpdateLobbyUI();
        }
        else
        {
            Debug.LogError($"[RPC_SetPlayerReady] Received ready state from invalid player: {playerRef.PlayerId}");
        }
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
        PlayerRef source = info.Source;
        // When the host calls this on themselves, info.Source may be default
        if (source == default) source = Runner.LocalPlayer;

        if (LobbyPlayers.ContainsKey(source))
        {
            var data = LobbyPlayers[source];
            data.TeamID = teamId;
            LobbyPlayers.Set(source, data);
            Debug.Log($"[NetworkLobbyManager] Player {source.PlayerId} team set to {teamId}");
            // Broadcast updated player list to all clients
            RPC_UpdateLobbyUI();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_NotifyGameStarting()
    {
        Debug.Log($"[NetworkLobbyManager] Game is starting! Loading map: {mapOptions[SelectedMapIndex]}");
        
        // Show loading screen or transition effect
        if (uiManager != null)
        {
            uiManager.ShowLoadingScreen();
        }
        
        // Disable player input during scene transition
        var localPlayer = Runner.LocalPlayer;
        if (LobbyPlayers.ContainsKey(localPlayer))
        {
            var playerData = LobbyPlayers[localPlayer];
            Debug.Log($"[NetworkLobbyManager] Player {localPlayer.PlayerId} ({playerData.PlayerName}) received game start notification");
        }
        
        // Client confirms they're ready to load the scene
        if (!Runner.IsServer)
        {
            StartCoroutine(ConfirmReadyToLoadAfterDelay());
        }
    }
    
    private System.Collections.IEnumerator ConfirmReadyToLoadAfterDelay()
    {
        // Small delay to ensure UI is ready
        yield return new WaitForSeconds(0.5f);
        
        Debug.Log($"[NetworkLobbyManager] Client {Runner.LocalPlayer.PlayerId} confirming ready to load");
        RPC_ConfirmReadyToLoad(Runner.LocalPlayer);
    }
    
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ConfirmReadyToLoad(PlayerRef player, RpcInfo info = default)
    {
        if (Runner.IsServer)
        {
            Debug.Log($"[NetworkLobbyManager] Player {player.PlayerId} confirmed ready to load");
            PlayersReadyToLoad.Set(player, true);
            
            // Check if all players are ready
            CheckIfAllPlayersReadyToLoad();
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
                UpdateLobbyUI();
            }
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_PlayerHasLoadedScene(PlayerRef player, RpcInfo info = default)
    {
        if (!Runner.IsServer) return;

        PlayersLoadedCount++;
        Debug.Log($"[NetworkLobbyManager] Player {player.PlayerId} has loaded the scene. {PlayersLoadedCount}/{LobbyPlayers.Count} players loaded.");

        if (PlayersLoadedCount >= LobbyPlayers.Count)
        {
            Debug.Log("[NetworkLobbyManager] All players have loaded the scene. Game is ready!");
            IsGameReady = true;
        }
    }


    private void CheckIfAllPlayersReadyToLoad()
    {
        if (!Runner.IsServer) return;
        
        // Check if all players are ready to load
        bool allPlayersReady = LobbyPlayers.Count == PlayersReadyToLoad.Count;
        
        if (allPlayersReady)
        {
            Debug.Log("[NetworkLobbyManager] All players are ready to load. Proceeding with scene load...");
            
            // Load the scene
            string sceneName = mapOptions[SelectedMapIndex];
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            Debug.Log($"[NetworkLobbyManager] Loading scene: {sceneName} (Current scene: {currentScene})");
            
            // Initialize the game with the selected scene
            if (NetworkGameManager.Instance != null)
            {
                Debug.Log($"[NetworkLobbyManager] Initializing game with scene: {sceneName}");
                GameMode gameMode = (GameMode)SelectedModeIndex;

                // Assign teams based on the selected game mode
                if (gameMode == GameMode.FreeForAll)
                {
                    foreach (var kvp in LobbyPlayers)
                    {
                        var player = kvp.Key;
                        var playerData = kvp.Value;
                        playerData.TeamID = -1; // -1 for FreeForAll
                        LobbyPlayers.Set(player, playerData);
                        Debug.Log($"[NetworkLobbyManager] Set TeamID for Player {player.PlayerId} to -1 for FreeForAll.");
                    }
                }
                else if (gameMode == GameMode.TeamDeathmatch)
                {
                    int teamCounter = 0;
                    foreach (var kvp in LobbyPlayers)
                    {
                        var player = kvp.Key;
                        var playerData = kvp.Value;
                        playerData.TeamID = teamCounter % 2; // Alternate between Team 0 and Team 1
                        LobbyPlayers.Set(player, playerData);
                        Debug.Log($"[NetworkLobbyManager] Set TeamID for Player {player.PlayerId} to {playerData.TeamID} for TeamDeathmatch.");
                        teamCounter++;
                    }
                }

                NetworkGameManager.Instance.StartGame(gameMode, timeInSeconds[SelectedTimeIndex], sceneName);
            }
            else
            {
                Debug.LogError("[NetworkLobbyManager] NetworkGameManager instance not found!");
            }
        }
        else
        {
            Debug.Log($"[NetworkLobbyManager] Not all players are ready to load. {PlayersReadyToLoad.Count}/{LobbyPlayers.Count} players ready. Waiting...");
        }
    }
}

public struct PlayerLobbyData : INetworkStruct
{
    public NetworkString<_16> PlayerName;
    public bool IsReady;
    public bool IsHost;
    public int TeamID;
    [Networked] public Color PlayerColor { get; set; }
}