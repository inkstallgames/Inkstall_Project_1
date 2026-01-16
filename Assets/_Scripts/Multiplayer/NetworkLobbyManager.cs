using Fusion;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class NetworkLobbyManager : NetworkBehaviour
{
    public static NetworkLobbyManager Instance { get; private set; }

    [Header("UI References")]
    public GameObject lobbyPanel;
    public Button startButton;
    public TextMeshProUGUI playerListText;
    public Toggle readyToggle;
    public TMP_Dropdown teamDropdown;
    public TMP_Dropdown gameModeDropdown;
    public TMP_InputField playerNameInput;

    [Header("Game Settings")]
    public int minPlayersToStart = 2;
    public int maxPlayers = 4;

    [Networked] private TickTimer startGameTimer { get; set; }
    private bool isHost => Runner != null && Runner.IsSharedModeMasterClient;
    private string playerName = "Player";

    private Dictionary<PlayerRef, PlayerLobbyData> lobbyPlayers = new Dictionary<PlayerRef, PlayerLobbyData>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override void Spawned()
    {
        base.Spawned();

        // Initialize UI
        if (isHost)
        {
            startButton.gameObject.SetActive(true);
            startButton.onClick.AddListener(OnStartGameClicked);
            gameModeDropdown.interactable = true;
        }
        else
        {
            startButton.gameObject.SetActive(false);
            gameModeDropdown.interactable = false;
        }

        // Set up UI callbacks
        readyToggle.onValueChanged.AddListener(OnReadyToggleChanged);
        teamDropdown.onValueChanged.AddListener(OnTeamSelected);
        playerNameInput.onEndEdit.AddListener(OnPlayerNameChanged);

        // Set initial player name
        playerName = PlayerPrefs.GetString("PlayerName", $"Player_{Random.Range(1000, 9999)}");
        playerNameInput.text = playerName;

        // Register with the game manager
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.OnGameStarted += OnGameStarted;
        }
    }

    public void AddPlayerToLobby(PlayerRef player, string defaultName = null)
    {
        if (!lobbyPlayers.ContainsKey(player))
        {
            var playerData = new PlayerLobbyData
            {
                PlayerRef = player,
                PlayerName = defaultName ?? $"Player_{player.PlayerId}",
                IsReady = false,
                TeamId = 0,
                IsHost = player == Runner.LocalPlayer && isHost
            };

            lobbyPlayers.Add(player, playerData);
            UpdateLobbyUI();
        }
    }

    public void RemovePlayerFromLobby(PlayerRef player)
    {
        if (lobbyPlayers.Remove(player))
        {
            UpdateLobbyUI();
        }
    }


    private void UpdateLobbyUI()
    {
        if (lobbyPanel == null) return;

        // Update player list text
        playerListText.text = "Players:\n";
        int readyCount = 0;

        foreach (var playerData in lobbyPlayers.Values)
        {
            string status = playerData.IsReady ? "<color=green>Ready</color>" : "<color=red>Not Ready</color>";
            string team = playerData.TeamId == 0 ? "Blue" : "Red";
            playerListText.text += $"{playerData.PlayerName} (Team {team}) - {status}\n";

            if (playerData.IsReady) readyCount++;
        }

        // Update start button state
        if (isHost)
        {
            bool canStart = readyCount >= minPlayersToStart && readyCount == lobbyPlayers.Count;
            startButton.interactable = canStart;
        }

        // Update team dropdown based on current player count
        UpdateTeamSelectionUI();
    }

    private void UpdateTeamSelectionUI()
    {
        if (lobbyPlayers.TryGetValue(Runner.LocalPlayer, out var localPlayer))
        {
            // Disable team selection if already in a team with players
            bool canChangeTeam = CanPlayerChangeTeam(localPlayer.TeamId);
            teamDropdown.interactable = canChangeTeam && !localPlayer.IsReady;
        }
    }

    private bool CanPlayerChangeTeam(int newTeamId)
    {
        // Check if the team is full
        int teamSize = lobbyPlayers.Values.Count(p => p.TeamId == newTeamId);
        int otherTeamSize = lobbyPlayers.Values.Count(p => p.TeamId != newTeamId);

        // Allow switching if teams are balanced or if the other team has more players
        return teamSize <= otherTeamSize + 1;
    }

    // UI Event Handlers
    private void OnReadyToggleChanged(bool isReady)
    {
        if (Runner != null && Runner.IsRunning)
        {
            RPC_SetPlayerReady(Runner.LocalPlayer, isReady);
        }
    }

    private void OnTeamSelected(int teamIndex)
    {
        if (Runner != null && Runner.IsRunning && lobbyPlayers.TryGetValue(Runner.LocalPlayer, out var playerData))
        {
            if (CanPlayerChangeTeam(teamIndex))
            {
                RPC_SetPlayerTeam(Runner.LocalPlayer, teamIndex);
            }
            else
            {
                // Revert to previous team if can't switch
                teamDropdown.SetValueWithoutNotify(playerData.TeamId);
            }
        }
    }

    private void OnPlayerNameChanged(string newName)
    {
        playerName = newName.Trim();
        PlayerPrefs.SetString("PlayerName", playerName);
        
        if (Runner != null && Runner.IsRunning)
        {
            RPC_SetPlayerName(Runner.LocalPlayer, playerName);
        }
    }

    private void OnStartGameClicked()
    {
        if (isHost && Runner.IsSharedModeMasterClient)
        {
            // Get selected game mode
            GameMode gameMode = gameModeDropdown.value == 0 ? GameMode.FreeForAll : GameMode.TeamDeathmatch;
            
            // Start a countdown before the game starts
            startGameTimer = TickTimer.CreateFromSeconds(Runner, 5f);
            RPC_StartGameCountdown(5);
            
            // Disable UI interactions during countdown
            startButton.interactable = false;
            readyToggle.interactable = false;
            teamDropdown.interactable = false;
            gameModeDropdown.interactable = false;
        }
    }

    private void OnGameStarted()
    {
        // Hide lobby UI when game starts
        if (lobbyPanel != null)
        {
            lobbyPanel.SetActive(false);
        }
    }

    // RPCs
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SetPlayerReady(PlayerRef player, bool isReady)
    {
        if (lobbyPlayers.TryGetValue(player, out var playerData))
        {
            playerData.IsReady = isReady;
            UpdateLobbyUI();
            RPC_UpdatePlayerReady(player, isReady);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetPlayerTeam(PlayerRef player, int teamId)
    {
        if (lobbyPlayers.TryGetValue(player, out var playerData))
        {
            playerData.TeamId = teamId;
            UpdateLobbyUI();
            RPC_UpdatePlayerTeam(player, teamId);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SetPlayerName(PlayerRef player, string playerName)
    {
        if (lobbyPlayers.TryGetValue(player, out var playerData))
        {
            playerData.PlayerName = playerName;
            UpdateLobbyUI();
            RPC_UpdatePlayerName(player, playerName);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdatePlayerReady(PlayerRef player, bool isReady) { }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdatePlayerTeam(PlayerRef player, int teamId) { }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdatePlayerName(PlayerRef player, string playerName) { }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_StartGameCountdown(int seconds)
    {
        // Show countdown in UI
        startButton.GetComponentInChildren<TextMeshProUGUI>().text = $"Starting in {seconds}...";
    }

    // Network callbacks
    public override void FixedUpdateNetwork()
    {
        if (startGameTimer.Expired(Runner))
        {
            startGameTimer = TickTimer.None;
            if (isHost)
            {
                // Start the game with selected settings
                GameMode gameMode = gameModeDropdown.value == 0 ? GameMode.FreeForAll : GameMode.TeamDeathmatch;
                NetworkGameManager.Instance.RPC_StartGame(gameMode, 300, 3); // 5min rounds, 3 rounds to win
            }
        }
        else if (startGameTimer.IsRunning)
        {
            // Update countdown
            int remaining = (int)startGameTimer.RemainingTime(Runner).Value;
            RPC_UpdateGameCountdown(remaining);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdateGameCountdown(int seconds)
    {
        if (startButton != null)
        {
            startButton.GetComponentInChildren<TextMeshProUGUI>().text = $"Starting in {seconds}...";
        }
    }

    private void OnDestroy()
    {
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.OnGameStarted -= OnGameStarted;
        }
    }
}

[System.Serializable]
public class PlayerLobbyData : INetworkStruct
{
    public PlayerRef PlayerRef { get; set; }
    public string PlayerName { get; set; }
    public bool IsReady { get; set; }
    public int TeamId { get; set; }
    public bool IsHost { get; set; }
}