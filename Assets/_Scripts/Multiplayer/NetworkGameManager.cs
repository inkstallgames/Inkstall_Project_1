using Fusion;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public enum GameMode
{
    FreeForAll,      // Classic deathmatch
    TeamDeathmatch,  // Team-based deathmatch
    BattleRoyale,    // Last player/team standing
    GunGame,         // Progress through weapons on each kill
    OneInTheChamber  // One bullet, one kill
}

public enum GameState
{
    Lobby,          // Players joining/selecting teams
    Starting,       // Countdown before game starts
    InProgress,     // Game is active
    RoundOver,      // Round has ended, show scores
    GameOver        // Match complete, show final results
}

public class NetworkGameManager : NetworkBehaviour
{
    public static NetworkGameManager Instance { get; private set; }

    [Header("Game Settings")]
    [SerializeField] private bool _gameSettingsHeader;
    [Networked] public GameMode CurrentGameMode { get; set; } = GameMode.FreeForAll;
    [Networked] public int RoundTime { get; private set; } = 300; // 5 minutes default
    [Networked] public int RoundsToWin { get; private set; } = 3;
    [Networked] public GameState CurrentGameState { get; private set; }
    [Networked] public float GameStartTime { get; private set; }
    [Networked] public float RoundStartTime { get; private set; }
    [Networked] public float RoundEndTime { get; private set; }
    [Networked] public int CurrentRound { get; private set; } = 1;
    [Networked] public int BlueTeamScore { get; private set; }
    [Networked] public int RedTeamScore { get; private set; }
    [Networked] public int WinningTeam { get; private set; } = -1;
    [Networked] public int PlayersAlive { get; private set; }
    [Networked] public int MaxPlayers { get; private set; } = 10; // Adjust based on your game's needs
    [Networked] public float RespawnTime { get; private set; } = 5f; // Time before players respawn
    
    [Networked, Capacity(20)] public NetworkLinkedList<PlayerRef> AllPlayers => default;
    [Networked, Capacity(10)] public NetworkLinkedList<PlayerRef> AlivePlayers => default;
    [Networked, Capacity(10)] public NetworkDictionary<PlayerRef, int> PlayerKills => default;
    [Networked, Capacity(10)] public NetworkDictionary<PlayerRef, int> PlayerDeaths => default;

    [Header("References")]
    public GameObject playerPrefab;
    public GameObject[] weaponPrefabs; // Array of available weapons
    
    [Header("Game Rules")]
    public int killsToWin = 20; // For FreeForAll and TeamDeathmatch
    public int maxRounds = 5;   // For round-based modes
    public float warmupTime = 10f;
    public float roundEndTime = 5f;

    private Dictionary<PlayerRef, PlayerNetworkData> players = new Dictionary<PlayerRef, PlayerNetworkData>();
    private NetworkPlayerSpawner playerSpawner;
    private Dictionary<PlayerRef, float> respawnTimers = new Dictionary<PlayerRef, float>();
    private List<NetworkObject> activeProjectiles = new List<NetworkObject>();

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

        RefreshPlayerSpawner();
    }

    private void RefreshPlayerSpawner()
    {
        // This method can still be used as a fallback or for re-syncing.
        playerSpawner = FindObjectOfType<NetworkPlayerSpawner>();
        if (playerSpawner != null)
        { 
            Debug.Log("[NetworkGameManager] NetworkPlayerSpawner found and assigned");
            if (Runner != null)
            {
                playerSpawner.Init(Runner);
                Debug.Log("[NetworkGameManager] NetworkPlayerSpawner initialized with Runner");
            }
        }
        else
        {
            Debug.LogWarning("[NetworkGameManager] NetworkPlayerSpawner not found in current scene");
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_NotifyGameStarting()
    {
        Debug.Log("[NetworkGameManager] Game is starting, preparing to load scene...");
        
        // Show loading screen on all clients
        if (LobbyUIManager.Instance != null)
        {
            Debug.Log("[NetworkGameManager] Showing loading screen for all clients");
            LobbyUIManager.Instance.ShowLoadingScreen(true);
        }
        else
        {
            Debug.LogWarning("[NetworkGameManager] LobbyUIManager.Instance is null, cannot show loading screen");
        }
    }

    public void StartGame(GameMode mode = GameMode.FreeForAll, int time = 300, string sceneName = null)
    {
        if (!Object.HasStateAuthority) 
        {
            Debug.LogError("[NetworkGameManager] Only the server can start the game!");
            return;
        }

        Debug.Log($"[NetworkGameManager] Starting game with mode: {mode}, time: {time}, scene: {sceneName}");
        
        CurrentGameMode = mode;
        RoundTime = time;
        CurrentGameState = GameState.Starting;
        GameStartTime = Time.time;
        CurrentRound = 1;
        BlueTeamScore = 0;
        RedTeamScore = 0;
        WinningTeam = -1;
        
        // Clear previous game data
        PlayerKills.Clear();
        PlayerDeaths.Clear();
        AlivePlayers.Clear();
        respawnTimers.Clear();

        // Initialize player stats
        foreach (var player in Runner.ActivePlayers)
        {
            PlayerKills.Set(player, 0);
            PlayerDeaths.Set(player, 0);
        }

        // If no scene name provided, use current scene
        if (string.IsNullOrEmpty(sceneName))
        {
            sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        }

        // Notify all clients that the game is starting
        RPC_NotifyGameStarting();
        
        // Load the selected map scene for all players
        try 
        {
            Debug.Log($"[NetworkGameManager] Loading scene: {sceneName}");
            Runner.LoadScene(sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
            // Note: InitializeGame will be called from NetworkStarter.OnSceneLoadDone
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[NetworkGameManager] Error loading scene {sceneName}: {e.Message}");
            Debug.LogException(e);
            // Fall back to current scene if loading fails
            Runner.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
    }
    
    
    private System.Collections.IEnumerator InitializeGameAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (Runner.IsServer)
        {
            Debug.Log("[NetworkGameManager] Scene loaded, initializing game...");
            InitializeGame();
        }
    }

    public void InitializeGame()
    {
        if (!Runner.IsServer) return;

        Debug.Log("[NetworkGameManager] Initializing game state...");
        
        // Refresh spawner reference in case scene changed
        RefreshPlayerSpawner();
        
        // Spawn all players immediately
        SpawnAllPlayers();
        
        CurrentGameState = GameState.InProgress;
        RoundStartTime = Time.time;
    }

    private void SpawnAllPlayers()
    {
        if (!Runner.IsServer) return;

        Debug.Log("[NetworkGameManager] Spawner is ready, spawning all active players.");

        foreach (var player in Runner.ActivePlayers)
        {
            if (playerSpawner != null)
            {
                Debug.Log($"[NetworkGameManager] Spawning player {player.PlayerId}");
                playerSpawner.SpawnPlayer(player);
            }
            else
            {
                Debug.LogError("[NetworkGameManager] NetworkPlayerSpawner is null, cannot spawn players!");
            }
            AlivePlayers.Add(player);
        }

        PlayersAlive = AlivePlayers.Count;
        Debug.Log($"[NetworkGameManager] Game started with {PlayersAlive} players");
        OnGameStarted?.Invoke();
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

        // Return to lobby after a short delay
        StartCoroutine(ReturnToLobbyAfterDelay(10f));
    }

    private System.Collections.IEnumerator ReturnToLobbyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (Object.HasStateAuthority)
        {
            // Assuming the lobby is at build index 0
            Runner.LoadScene(SceneRef.FromIndex(0), UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnGameEnded(int winningTeam)
    {
        Debug.Log($"Game Over! Team {winningTeam + 1} wins!");
        OnGameEnded?.Invoke();
    }

    public Transform GetSpawnPoint(int teamId)
    {
        if (playerSpawner == null)
        {
            Debug.LogError("[NetworkGameManager] PlayerSpawner is null, cannot get spawn point!");
            return null;
        }

        // Find all spawn points in the scene
        var spawnPoints = FindObjectsOfType<PlayerSpawnPoint>();
        
        if (spawnPoints.Length == 0)
        {
            Debug.LogWarning("[NetworkGameManager] No spawn points found in scene!");
            return null;
        }

        // Filter spawn points by team ID
        var teamSpawnPoints = System.Array.FindAll(spawnPoints, sp => sp.teamId == teamId);
        
        // If no team-specific spawn points, use any available spawn point
        if (teamSpawnPoints.Length == 0)
        {
            Debug.LogWarning($"[NetworkGameManager] No spawn points found for team {teamId}, using any available spawn point");
            teamSpawnPoints = spawnPoints;
        }

        // Find unoccupied spawn points
        var availableSpawns = System.Array.FindAll(teamSpawnPoints, sp => !sp.isOccupied);
        
        // If all are occupied, use any spawn point
        if (availableSpawns.Length == 0)
        {
            availableSpawns = teamSpawnPoints;
        }

        // Select a random spawn point
        var selectedSpawn = availableSpawns[UnityEngine.Random.Range(0, availableSpawns.Length)];
        selectedSpawn.isOccupied = true;

        return selectedSpawn.transform;
    }

}

