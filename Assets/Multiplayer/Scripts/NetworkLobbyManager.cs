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
    [Tooltip("Max players per team in TDM lobby (5 per panel).")]
    public int maxPlayersPerTeam = 5;
    [Networked, Capacity(10)] public NetworkDictionary<PlayerRef, PlayerLobbyData> LobbyPlayers { get; } 

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

    [Networked, Capacity(10)]
    public NetworkDictionary<PlayerRef, bool> PlayersReadyToLoad { get; }

    [Networked]
    public int PlayersLoadedCount { get; set; }

    [Networked]
    public NetworkBool IsGameReady { get; set; }

    [Networked]
    public TickTimer GameStartTimer { get; set; }

    private readonly List<string> mapOptions = new List<string> { "Rust" };
    private readonly List<string> timeOptions = new List<string> { "5:00", "10:00", "15:00", "20:00" };
    private readonly List<Color> playerColors = new List<Color>
    {
        Color.blue,
        Color.yellow,
        Color.magenta,
        Color.cyan,
        new Color(1f, 0.5f, 0f),  // Orange
        Color.white,
        new Color(0.5f, 0f, 1f),  // Purple
        new Color(0f, 1f, 0.5f),  // Teal
        new Color(1f, 0f, 0.5f),  // Pink
        new Color(0.5f, 1f, 0f)   // Lime
    };
    private readonly int[] timeInSeconds = { 300, 600, 900, 1200 };

    private LobbyUIManager uiManager;
    
    // Networked version counter — incremented on every lobby data change.
    // Clients detect changes by comparing against their local _lastLobbyVersion.
    // This is reliable because it syncs atomically with LobbyPlayers via Fusion state.
    [Networked] public int LobbyVersion { get; set; }
    
    // Dirty flag to prevent rebuilding the player list every frame (which kills button clicks)
    private bool _playerListDirty = true;
    private int _lastLobbyVersion = -1;
    private int _lastKnownPlayerCount = -1;

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
        // Debug.Log($"[NetworkLobbyManager] Spawned called. Object: {gameObject.name}, IsServer: {Runner.IsServer}, LocalPlayer: {Runner.LocalPlayer}");
        
        uiManager = LobbyUIManager.Instance;
        // Debug.Log($"[NetworkLobbyManager] UI Manager found: {uiManager != null}");
        
        if (uiManager == null)
        {
            // Debug.LogError("[NetworkLobbyManager] LobbyUIManager.Instance is null! Make sure it exists in the scene.");
        }

        // Only the host generates a join code
        if (Runner.IsServer)
        {
            // Debug.Log("[NetworkLobbyManager] This is the server (host)");
            
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
                // Debug.Log($"[NetworkLobbyManager] Using join code from NetworkStarter: {JoinCode}");
            }
            else if (string.IsNullOrEmpty(JoinCode))
            {
                // Fallback to generating a new code if not provided by NetworkStarter
                JoinCode = GenerateJoinCode();
                // Debug.Log($"[NetworkLobbyManager] Generated new Join Code: {JoinCode}");
            }
            
            // Update UI with join code immediately
            if (uiManager != null)
            {
                uiManager.SetJoinCode(JoinCode);
            }
            
            // Add all active players to the lobby (handles returning to lobby after a game)
            // Debug.Log("[NetworkLobbyManager] Adding connected players to lobby");
            foreach (var player in Runner.ActivePlayers)
            {
                bool isHostPlayer = (player == Runner.LocalPlayer);
                AddPlayerToLobby(player, isHostPlayer);
            }
        }
        else
        {
            // Debug.Log("[NetworkLobbyManager] This is a client");
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
                // Debug.Log($"[NetworkLobbyManager] Setting join code in UI: {JoinCode}");
                uiManager.SetJoinCode(JoinCode);
            }
            else
            {
                // Debug.Log("[NetworkLobbyManager] No join code to display");
            }
        }
        else
        {
            // Debug.LogError("[NetworkLobbyManager] UI Manager is null!");
        }

        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.OnGameStarted += OnGameStarted;
        }

        // If we are a client (not the host/server), we need to tell the host our name
        if (Runner.IsClient && !Runner.IsServer)
        {
            string playerName = PlayerPrefs.GetString("PlayerName", "Unknown Player");
            // Debug.Log($"[NetworkLobbyManager] Sending player name to host: {playerName}");
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
        int assignedTeam;
        if ((GameMode)SelectedModeIndex == GameMode.TeamDeathmatch)
        {
            if (teamACount >= maxPlayersPerTeam)
                assignedTeam = 1;
            else if (teamBCount >= maxPlayersPerTeam)
                assignedTeam = 0;
            else
                assignedTeam = teamACount <= teamBCount ? 0 : 1;
        }
        else
        {
            assignedTeam = (teamBCount < teamACount) ? 1 : 0;
        }

        var playerData = new PlayerLobbyData
        {
            PlayerName = initialName,
            IsHost = isHost,
            IsReady = isHost, // Host is always ready
            TeamID = assignedTeam
        };
        
        LobbyPlayers.Add(player, playerData);
        LobbyVersion++;
        
        // Set the networked PlayerColor property using the retrieve-modify-set pattern
        // Use modulo to cycle through available colors if we have more players than colors
        int colorIndex = (LobbyPlayers.Count - 1) % playerColors.Count;
        Color assignedColor = playerColors[colorIndex];
        // Debug.Log($"[NetworkLobbyManager] Assigning color at index {colorIndex}: {assignedColor} (R:{assignedColor.r}, G:{assignedColor.g}, B:{assignedColor.b})");
        
        var data = LobbyPlayers[player];
        data.PlayerColor = assignedColor;
        LobbyPlayers.Set(player, data);
        
        // Verify the color was set
        var verifyData = LobbyPlayers[player];
        // Debug.Log($"[NetworkLobbyManager] Added player {player.PlayerId} to lobby. IsHost: {isHost}, Team: {assignedTeam}, Assigned Color: {assignedColor}, Stored Color: {verifyData.PlayerColor}");
        
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
        if (Runner == null || !Runner.IsRunning) return;
        if (!LobbyPlayers.ContainsKey(Runner.LocalPlayer)) return;
        if ((GameMode)SelectedModeIndex != GameMode.TeamDeathmatch) return;

        int currentTeam = LobbyPlayers[Runner.LocalPlayer].TeamID;
        int newTeam = (currentTeam == 0) ? 1 : 0;
        TryRequestTeamChange(newTeam);
    }

    /// <summary>
    /// Requests a team change for the local player. Fails if the target team is full (5/5).
    /// </summary>
    public void TryRequestTeamChange(int teamId)
    {
        if (Runner == null || !Runner.IsRunning) return;
        if (!LobbyPlayers.ContainsKey(Runner.LocalPlayer)) return;

        if (!CanSwitchToTeam(Runner.LocalPlayer, teamId, out _))
            return;

        RPC_SetPlayerTeam(teamId);
    }

    private int CountPlayersOnTeam(int teamId, PlayerRef excludePlayer = default)
    {
        int count = 0;
        foreach (var kvp in LobbyPlayers)
        {
            if (excludePlayer != default && kvp.Key == excludePlayer)
                continue;
            if (kvp.Value.TeamID == teamId)
                count++;
        }
        return count;
    }

    private bool CanSwitchToTeam(PlayerRef player, int teamId, out string denyReason)
    {
        denyReason = null;

        if ((GameMode)SelectedModeIndex != GameMode.TeamDeathmatch)
            return true;

        if (!LobbyPlayers.ContainsKey(player))
        {
            denyReason = "Unable to switch teams.";
            return false;
        }

        if (LobbyPlayers[player].TeamID == teamId)
            return true;

        if (CountPlayersOnTeam(teamId, player) >= maxPlayersPerTeam)
        {
            string teamName = teamId == 0 ? "Hero's" : "Aliens";
            denyReason = $"{teamName} team is full ({maxPlayersPerTeam}/{maxPlayersPerTeam}).";
            return false;
        }

        return true;
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
        // Debug.Log($"[NetworkLobbyManager] StartGame called. IsServer: {Runner?.IsServer}");

        if (!Runner.IsServer)
        {
            // Debug.LogError("[NetworkLobbyManager] Only the server can start the game!");
            return;
        }

        // Hero selection is disabled — spawn players directly with playerPrefab
        // Debug.Log("[NetworkLobbyManager] Skipping hero selection. Loading map immediately...");
        
        // Validate map selection
        if (SelectedMapIndex < 0 || SelectedMapIndex >= mapOptions.Count)
        {
            // Debug.LogError($"[NetworkLobbyManager] Invalid map index: {SelectedMapIndex}. Defaulting to index 0.");
            SelectedMapIndex = 0;
        }

        string sceneName = mapOptions[SelectedMapIndex];
        // Debug.Log($"[NetworkLobbyManager] Loading map: {sceneName}");

        // Notify all clients the game is starting (shows loading screen etc.)
        RPC_NotifyGameStarting();

        // Reset loading state for the new game
        PlayersLoadedCount = 0;
        IsGameReady = false;
        GameStartTimer = default;

        // Notify NetworkGameManager to start the game (loads the scene)
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.StartGame(
                (GameMode)SelectedModeIndex,
                timeInSeconds[SelectedTimeIndex],
                sceneName
            );
        }
        else
        {
            // Debug.LogError("[NetworkLobbyManager] NetworkGameManager.Instance is null! Cannot start game.");
        }
    }

    private void OnGameStarted()
    {
        uiManager.ShowLobby(false);
    }

    public override void Render()
    {
        base.Render();

        // Re-acquire UI manager if needed (happens after scene transitions)
        if (uiManager == null)
        {
            uiManager = LobbyUIManager.Instance;
            
            // If we just re-acquired a new UI manager (e.g. returning from game to lobby),
            // force a full rebuild since the old scene's UI was destroyed
            if (uiManager != null)
            {
                _playerListDirty = true;
                
                // Re-initialize the lobby UI for the new scene
                var modeOptions = System.Enum.GetNames(typeof(GameMode)).ToList();
                bool isHost = Runner.IsServer;
                uiManager.InitializeLobbyUI(
                    new List<string> { "Rust" }, 
                    modeOptions, 
                    new List<string> { "5:00", "10:00", "15:00", "20:00" }, 
                    isHost
                );
                uiManager.ShowLobby(true);
                
                // Restore join code
                if (!string.IsNullOrEmpty(JoinCode))
                {
                    uiManager.SetJoinCode(JoinCode);
                }
            }
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
                // Mark the player list for rebuild on the next Render frame
                _playerListDirty = true;
                
                // Update UI based on local player (button states are cheap, do immediately)
                if (uiManager != null && LobbyPlayers.ContainsKey(Runner.LocalPlayer))
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
                            
                            uiManager.startGameButton.interactable = allReady;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // Debug.LogError($"[RPC_UpdateLobbyUI] Error updating UI: {e.Message}\n{e.StackTrace}");
            }
        }
    }

    private void UpdateLobbyUI()
    {
        if (uiManager == null) return;

        // Detect lobby data changes via the networked version counter
        // This is reliable because LobbyVersion syncs atomically with LobbyPlayers
        if (LobbyVersion != _lastLobbyVersion)
        {
            _lastLobbyVersion = LobbyVersion;
            _playerListDirty = true;
        }

        // Also detect player count changes as a fallback
        if (LobbyPlayers.Count != _lastKnownPlayerCount)
        {
            _lastKnownPlayerCount = LobbyPlayers.Count;
            _playerListDirty = true;
        }

        // Only rebuild the player list when data has actually changed
        // Rebuilding every frame destroys and recreates buttons, preventing click events
        if (_playerListDirty)
        {
            _playerListDirty = false;
            var playerDict = LobbyPlayers.ToDictionary(kvp => kvp.Key.PlayerId, kvp => kvp.Value);
            uiManager.UpdatePlayerList(playerDict);
        }

        // Update Join Code continuously
        if (!string.IsNullOrEmpty(JoinCode))
        {
            uiManager.SetJoinCode(JoinCode);
        }

        // Update button visibility and state based on host status (cheap, OK every frame)
        bool isHost = Runner.IsServer;
        
        if (uiManager.startGameButton != null)
        {
            uiManager.startGameButton.gameObject.SetActive(isHost);
            if (isHost)
            {
                var networkStarter = NetworkStarter.Instance;
                bool isTestingMode = networkStarter != null && networkStarter.IsHostOnlyTestingEnabled;
                int requiredPlayers = isTestingMode ? 1 : minPlayersToStart;
                bool allReady = LobbyPlayers.Count >= requiredPlayers && LobbyPlayers.All(p => p.Value.IsReady);
                uiManager.startGameButton.interactable = allReady;
            }
        }

        if (uiManager.readyButton != null)
        {
            uiManager.readyButton.gameObject.SetActive(!isHost);
            if (LobbyPlayers.ContainsKey(Runner.LocalPlayer))
            {
                var localPlayerData = LobbyPlayers[Runner.LocalPlayer];
                uiManager.SetReadyButtonState(localPlayerData.IsReady);
            }
        }

        // Hide team switch button for FreeForAll mode
        if (uiManager.switchTeamButton != null)
        {
            uiManager.switchTeamButton.gameObject.SetActive((GameMode)SelectedModeIndex == GameMode.TeamDeathmatch);
        }
    }

    private void UpdateGameSettingsUI()
    {
        if (uiManager == null) return;

        bool isHost = Runner.IsServer;
        
        if (uiManager.mapButton != null)
        {
            uiManager.mapButton.interactable = isHost;
        }
        
        if (uiManager.modeDropdown != null)
        {
            uiManager.modeDropdown.interactable = isHost;
            // Only populate "TDM" and "FFA" (2 options) to match LobbyUIManager's formatting
            int expectedCount = 2;
            if (uiManager.modeDropdown.options.Count != expectedCount)
            {
                uiManager.modeDropdown.ClearOptions();
                uiManager.modeDropdown.AddOptions(new List<string> { "TDM", "FFA" });
            }
            uiManager.modeDropdown.SetValueWithoutNotify(SelectedModeIndex);
        }
        
        if (uiManager.timeDropdown != null)
        {
            uiManager.timeDropdown.interactable = isHost;
            if (uiManager.timeDropdown.options.Count != timeOptions.Count)
            {
                uiManager.timeDropdown.ClearOptions();
                uiManager.timeDropdown.AddOptions(timeOptions);
            }
            uiManager.timeDropdown.SetValueWithoutNotify(SelectedTimeIndex);
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
            LobbyVersion++;
            // Debug.Log($"[RPC_SetPlayerReady] Player {playerRef.PlayerId} set ready state to {isReady}");
            RPC_UpdateLobbyUI();
        }
        else
        {
            // Debug.LogError($"[RPC_SetPlayerReady] Received ready state from invalid player: {playerRef.PlayerId}");
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
        // Debug.Log($"[NetworkLobbyManager] Received name update from {info.Source}: {name}");
        if (LobbyPlayers.ContainsKey(info.Source))
        {
            var data = LobbyPlayers[info.Source];
            data.PlayerName = name;
            LobbyPlayers.Set(info.Source, data);
            NetworkGameManager.Instance?.SetPlayerDisplayName(info.Source, name);
            LobbyVersion++;

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

        if (!LobbyPlayers.ContainsKey(source))
            return;

        var data = LobbyPlayers[source];
        if (data.TeamID == teamId)
            return;

        if (!CanSwitchToTeam(source, teamId, out _))
            return;

        data.TeamID = teamId;
        LobbyPlayers.Set(source, data);
        LobbyVersion++;
        RPC_UpdateLobbyUI();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_NotifyGameStarting()
    {
        // Debug.Log($"[NetworkLobbyManager] Game is starting! Loading map: {mapOptions[SelectedMapIndex]}");
        
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
            // Debug.Log($"[NetworkLobbyManager] Player {localPlayer.PlayerId} ({playerData.PlayerName}) received game start notification");
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
        
        // Debug.Log($"[NetworkLobbyManager] Client {Runner.LocalPlayer.PlayerId} confirming ready to load");
        RPC_ConfirmReadyToLoad(Runner.LocalPlayer);
    }
    
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ConfirmReadyToLoad(PlayerRef player, RpcInfo info = default)
    {
        if (Runner.IsServer)
        {
            // Debug.Log($"[NetworkLobbyManager] Player {player.PlayerId} confirmed ready to load");
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
            // Debug.Log($"[NetworkLobbyManager] Player {player.PlayerId} joined the lobby");
            AddPlayerToLobby(player, false);
            
            // Force update the UI for all clients
            RPC_UpdateLobbyUI();
            
            // Log the current player count for debugging
            // Debug.Log($"[NetworkLobbyManager] Total players in lobby: {LobbyPlayers.Count}");
            foreach (var kvp in LobbyPlayers)
            {
                // Debug.Log($"- Player {kvp.Key.PlayerId}: {kvp.Value.PlayerName} (Host: {kvp.Value.IsHost}, Ready: {kvp.Value.IsReady})");
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
                LobbyVersion++;
                UpdateLobbyUI();
            }
        }
    }

    /// <summary>
    /// Restores lobby data for a player who reconnected mid-game.
    /// Called by NetworkGameManager.RestoreReconnectedPlayer.
    /// </summary>
    public void RestoreReconnectedPlayerLobbyData(PlayerRef player, string playerName, int teamId)
    {
        if (!Runner.IsServer) return;

        // Remove stale entry if one exists
        if (LobbyPlayers.ContainsKey(player))
            LobbyPlayers.Remove(player);

        var data = new PlayerLobbyData
        {
            PlayerName = playerName,
            IsHost = false,
            IsReady = true, // Reconnected player is auto-ready since the game is in progress
            TeamID = teamId
        };

        LobbyPlayers.Add(player, data);

        // Assign a color
        int colorIndex = (LobbyPlayers.Count - 1) % 10; // match the playerColors list size
        var colors = new List<Color>
        {
            Color.blue, Color.yellow, Color.magenta, Color.cyan,
            new Color(1f, 0.5f, 0f), Color.white,
            new Color(0.5f, 0f, 1f), new Color(0f, 1f, 0.5f),
            new Color(1f, 0f, 0.5f), new Color(0.5f, 1f, 0f)
        };
        var coloredData = LobbyPlayers[player];
        coloredData.PlayerColor = colors[colorIndex];
        LobbyPlayers.Set(player, coloredData);

        LobbyVersion++;
        Debug.Log($"[NetworkLobbyManager] Restored lobby data for reconnected player '{playerName}' (Team {teamId})");
    }

    /// <summary>
    /// Kick a player from the lobby. Only the host can call this.
    /// </summary>
    /// <param name="playerId">The PlayerId (int) of the player to kick.</param>
    public void KickPlayer(int playerId)
    {
        if (!Runner.IsServer)
        {
            Debug.LogWarning("[NetworkLobbyManager] Only the host can kick players.");
            return;
        }

        // Find the matching PlayerRef from the lobby dictionary
        PlayerRef targetPlayer = default;
        foreach (var kvp in LobbyPlayers)
        {
            if (kvp.Key.PlayerId == playerId)
            {
                targetPlayer = kvp.Key;
                break;
            }
        }

        if (targetPlayer == default)
        {
            Debug.LogWarning($"[NetworkLobbyManager] Could not find player with ID {playerId} to kick.");
            return;
        }

        // Don't allow kicking the host
        if (LobbyPlayers.ContainsKey(targetPlayer) && LobbyPlayers[targetPlayer].IsHost)
        {
            Debug.LogWarning("[NetworkLobbyManager] Cannot kick the host!");
            return;
        }

        string kickedName = LobbyPlayers.ContainsKey(targetPlayer) 
            ? LobbyPlayers[targetPlayer].PlayerName.ToString() 
            : $"Player {playerId}";
        
        Debug.Log($"[NetworkLobbyManager] Host is kicking player: {kickedName} (ID: {playerId})");

        // Notify all clients that this player was kicked (so the kicked client can show a message)
        RPC_NotifyKicked(targetPlayer);

        // Remove from lobby data
        if (LobbyPlayers.ContainsKey(targetPlayer))
        {
            LobbyPlayers.Remove(targetPlayer);
            LobbyVersion++;
        }

        // Update UI for remaining players
        RPC_UpdateLobbyUI();

        // Disconnect the player from the server
        Runner.Disconnect(targetPlayer);
    }

    /// <summary>
    /// RPC sent to all clients when a player is kicked.
    /// The kicked client uses this to show a "You were kicked" message.
    /// Other clients just see a notification.
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyKicked(PlayerRef kickedPlayer)
    {
        // If the local player is the one being kicked, show a specific message
        if (Runner.LocalPlayer == kickedPlayer)
        {
            Debug.Log("[NetworkLobbyManager] You have been kicked from the lobby.");
            
            // Set a flag so the disconnect handler shows the right message
            NetworkStarter.Instance?.SetKickedFlag(true);
        }
        else
        {
            // Other players see a notification
            if (uiManager != null)
            {
                string kickedName = LobbyPlayers.ContainsKey(kickedPlayer) 
                    ? LobbyPlayers[kickedPlayer].PlayerName.ToString() 
                    : $"Player {kickedPlayer.PlayerId}";
                uiManager.ShowMessage($"{kickedName} was kicked from the lobby.");
            }
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_PlayerHasLoadedScene(PlayerRef player, RpcInfo info = default)
    {
        if (!Runner.IsServer) return;

        PlayersLoadedCount++;
        // Debug.Log($"[NetworkLobbyManager] Player {player.PlayerId} has loaded the scene. {PlayersLoadedCount}/{LobbyPlayers.Count} players loaded.");

        if (PlayersLoadedCount >= LobbyPlayers.Count && !IsGameReady)
        {
            // Debug.Log("[NetworkLobbyManager] All players have loaded the scene. Starting 5-second countdown.");
            IsGameReady = true;
            GameStartTimer = TickTimer.CreateFromSeconds(Runner, 5.0f);
        }
    }


    private void CheckIfAllPlayersReadyToLoad()
    {
        if (!Runner.IsServer) return;
        
        // Check if all players are ready to load
        bool allPlayersReady = LobbyPlayers.Count == PlayersReadyToLoad.Count;
        
        if (allPlayersReady)
        {
            // Debug.Log("[NetworkLobbyManager] All players are ready to load. Proceeding with scene load...");
            
            // Load the scene
            string sceneName = mapOptions[SelectedMapIndex];
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            // Debug.Log($"[NetworkLobbyManager] Loading scene: {sceneName} (Current scene: {currentScene})");
            
            // Initialize the game with the selected scene
            if (NetworkGameManager.Instance != null)
            {
                // Debug.Log($"[NetworkLobbyManager] Initializing game with scene: {sceneName}");
                GameMode gameMode = (GameMode)SelectedModeIndex;

                if (gameMode == GameMode.TeamDeathmatch)
                {
                    // Assign teams for TeamDeathmatch
                    int teamCounter = 0;
                    foreach (var kvp in LobbyPlayers)
                    {
                        var player = kvp.Key;
                        var playerData = kvp.Value;
                        playerData.TeamID = teamCounter % 2; // Alternate between Team 0 and Team 1
                        LobbyPlayers.Set(player, playerData);
                        teamCounter++;
                    }
                }
                else if (gameMode == GameMode.FreeForAll)
                {
                    // For FreeForAll, team ID is always -1
                    foreach (var kvp in LobbyPlayers)
                    {
                        var player = kvp.Key;
                        var playerData = kvp.Value;
                        playerData.TeamID = -1;
                        LobbyPlayers.Set(player, playerData);
                    }
                }

                NetworkGameManager.Instance.StartGame(gameMode, timeInSeconds[SelectedTimeIndex], sceneName);
            }
            else
            {
                // Debug.LogError("[NetworkLobbyManager] NetworkGameManager instance not found!");
            }
        }
        else
        {
            // Debug.Log($"[NetworkLobbyManager] Not all players are ready to load. {PlayersReadyToLoad.Count}/{LobbyPlayers.Count} players ready. Waiting...");
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