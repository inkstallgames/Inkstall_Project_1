using Fusion;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public enum GameMode
{
    FreeForAll,
    TeamDeathmatch
}

public enum GameState
{
    Lobby,
    Starting,
    InProgress,
    RoundOver,
    GameOver
}

public class NetworkGameManager : NetworkBehaviour
{
    public static NetworkGameManager Instance { get; private set; }

    [Header("Game Settings")]
    [SerializeField] private bool _gameSettingsHeader;
    [Networked] public GameMode CurrentGameMode { get; set; }
    [Networked] public int RoundTime { get; private set; } = 300; // 5 minutes default
    [Networked] public int RoundsToWin { get; private set; } = 3;
    [Networked] public GameState CurrentGameState { get; private set; }
    [Networked] public float GameStartTime { get; private set; }
    [Networked] public float RoundStartTime { get; private set; }
    [Networked] public int CurrentRound { get; private set; } = 1;
    [Networked] public int BlueTeamScore { get; private set; }
    [Networked] public int RedTeamScore { get; private set; }
    [Networked] public int WinningTeam { get; private set; } = -1;

    [Header("References")]
    public Transform[] blueTeamSpawns;
    public Transform[] redTeamSpawns;
    public Transform[] freeForAllSpawns;

    private Dictionary<PlayerRef, PlayerNetworkData> players = new Dictionary<PlayerRef, PlayerNetworkData>();
    private NetworkLobbyManager lobbyManager;
    private NetworkPlayerSpawner playerSpawner;

    // Events
    public event System.Action OnGameStarted;
    public event System.Action OnRoundStarted;
    public event System.Action OnRoundEnded;
    public event System.Action OnGameEnded;

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

        // Find references
        lobbyManager = FindObjectOfType<NetworkLobbyManager>();
        playerSpawner = FindObjectOfType<NetworkPlayerSpawner>();
    }

    public override void Spawned()
    {
        base.Spawned();
        
        if (Object.HasStateAuthority)
        {
            // Initialize game state
            CurrentGameState = GameState.Lobby;
            BlueTeamScore = 0;
            RedTeamScore = 0;
            CurrentRound = 1;
        }
    }

    public void RegisterPlayer(PlayerRef player, PlayerNetworkData playerData)
    {
        if (!players.ContainsKey(player))
        {
            players[player] = playerData;
            Debug.Log($"Player {player.PlayerId} registered with team {playerData.TeamId}");
            
            // If game is in progress, spawn the player
            if (CurrentGameState == GameState.InProgress || CurrentGameState == GameState.RoundOver)
            {
                playerSpawner?.SpawnPlayer(player);
            }
        }
    }

    public void UnregisterPlayer(PlayerRef player)
    {
        if (players.Remove(player))
        {
            Debug.Log($"Player {player.PlayerId} unregistered");
            
            // Check if the game should end due to player disconnect
            if (Object.HasStateAuthority && CurrentGameState == GameState.InProgress)
            {
                CheckGameEndConditions();
            }
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_StartGame(GameMode gameMode, int roundTime, int roundsToWin)
    {
        if (CurrentGameState != GameState.Lobby) return;
        
        CurrentGameMode = gameMode;
        RoundTime = roundTime;
        RoundsToWin = roundsToWin;
        CurrentGameState = GameState.Starting;
        
        // Assign teams if in team mode
        if (gameMode == GameMode.TeamDeathmatch)
        {
            AssignTeams();
        }
        
        // Start the game after a short delay
        StartCoroutine(StartGameAfterDelay(3f));
    }
    
    private System.Collections.IEnumerator StartGameAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (Object.HasStateAuthority)
        {
            StartNewRound();
        }
    }
    
    private void StartNewRound()
    {
        CurrentGameState = GameState.InProgress;
        RoundStartTime = Time.time;
        
        // Reset player states and respawn them
        foreach (var player in players.Keys.ToList())
        {
            var playerObj = Runner.GetPlayerObject(player);
            if (playerObj != null)
            {
                var playerData = playerObj.GetComponent<PlayerNetworkData>();
                if (playerData != null)
                {
                    playerData.Health = 100;
                    playerData.Kills = 0;
                    playerData.Deaths = 0;
                }
                playerSpawner?.SpawnPlayer(player);
            }
        }
        
        RPC_OnRoundStarted();
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnRoundStarted()
    {
        Debug.Log($"Round {CurrentRound} started!");
        OnRoundStarted?.Invoke();
    }
    
    private void AssignTeams()
    {
        // Simple team assignment - alternate between teams
        int teamIndex = 0;
        foreach (var player in players.Keys.ToList())
        {
            var playerObj = Runner.GetPlayerObject(player);
            if (playerObj != null)
            {
                var playerData = playerObj.GetComponent<PlayerNetworkData>();
                if (playerData != null)
                {
                    playerData.TeamId = teamIndex % 2; // 0 or 1 for team assignment
                    teamIndex++;
                }
            }
        }
    }
    
    public void OnPlayerKilled(PlayerRef victim, PlayerRef killer)
    {
        if (!Object.HasStateAuthority) return;
        
        // Update scores based on game mode
        if (CurrentGameMode == GameMode.TeamDeathmatch)
        {
            var killerData = Runner.GetPlayerObject(killer)?.GetComponent<PlayerNetworkData>();
            var victimData = Runner.GetPlayerObject(victim)?.GetComponent<PlayerNetworkData>();
            
            if (killerData != null && victimData != null && killerData.TeamId != victimData.TeamId)
            {
                // Award point to killer's team
                if (killerData.TeamId == 0) BlueTeamScore++;
                else RedTeamScore++;
                
                // Check for round/game end
                CheckGameEndConditions();
            }
        }
    }
    
    private void CheckGameEndConditions()
    {
        if (CurrentGameMode == GameMode.TeamDeathmatch)
        {
            // Check if a team has won the round
            if (BlueTeamScore >= 25 || RedTeamScore >= 25)
            {
                int winningTeam = BlueTeamScore > RedTeamScore ? 0 : 1;
                EndRound(winningTeam);
            }
        }
        // Add other game mode conditions here
    }
    
    private void EndRound(int winningTeam)
    {
        if (!Object.HasStateAuthority) return;
        
        CurrentGameState = GameState.RoundOver;
        WinningTeam = winningTeam;
        
        // Update round wins
        if (winningTeam == 0) BlueTeamScore++;
        else RedTeamScore++;
        
        // Check for game win
        if (BlueTeamScore >= RoundsToWin || RedTeamScore >= RoundsToWin)
        {
            EndGame(winningTeam);
            return;
        }
        
        // Start next round after delay
        StartCoroutine(StartNextRoundAfterDelay(5f));
    }
    
    private System.Collections.IEnumerator StartNextRoundAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (Object.HasStateAuthority)
        {
            CurrentRound++;
            StartNewRound();
        }
    }
    
    private void EndGame(int winningTeam)
    {
        CurrentGameState = GameState.GameOver;
        WinningTeam = winningTeam;
        
        RPC_OnGameEnded(winningTeam);
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnGameEnded(int winningTeam)
    {
        Debug.Log($"Game Over! Team {winningTeam + 1} wins!");
        OnGameEnded?.Invoke();
    }

    public Transform GetSpawnPoint(int teamId)
    {
        Transform[] spawns = freeForAllSpawns;
        if (CurrentGameMode == GameMode.TeamDeathmatch)
        {
            spawns = teamId == 0 ? blueTeamSpawns : redTeamSpawns;
        }

        if (spawns != null && spawns.Length > 0)
        {
            return spawns[Random.Range(0, spawns.Length)];
        }

        Debug.LogWarning($"No spawn points found for team {teamId}. Using default spawn.");
        return transform; // Fallback
    }
}

