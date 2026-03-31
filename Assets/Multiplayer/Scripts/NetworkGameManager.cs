using Fusion;

using UnityEngine;

using System.Collections.Generic;

using System.Linq;



public enum GameMode

{

    TeamDeathmatch   // Team-based deathmatch

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

    [Networked] public GameMode CurrentGameMode { get; set; } = GameMode.TeamDeathmatch;

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

            // Debug.Log("[NetworkGameManager] NetworkPlayerSpawner found and assigned");

            if (Runner != null)

            {

                playerSpawner.Init(Runner);

                // Debug.Log("[NetworkGameManager] NetworkPlayerSpawner initialized with Runner");

            }

        }

        else

        {

            // Debug.LogWarning("[NetworkGameManager] NetworkPlayerSpawner not found in current scene");

        }

    }



    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]

    public void RPC_NotifyGameStarting()

    {

        // Debug.Log("[NetworkGameManager] Game is starting, preparing to load scene...");

        

        // Show loading screen on all clients

        if (LobbyUIManager.Instance != null)

        {

            // Debug.Log("[NetworkGameManager] Showing loading screen for all clients");

            LobbyUIManager.Instance.ShowLoadingScreen(true);

        }

        else

        {

            // Debug.LogWarning("[NetworkGameManager] LobbyUIManager.Instance is null, cannot show loading screen");

        }

    }



    public void StartGame(GameMode mode = GameMode.TeamDeathmatch, int time = 300, string sceneName = null)

    {

        if (!Object.HasStateAuthority) 

        {

            // Debug.LogError("[NetworkGameManager] Only the server can start the game!");

            return;

        }



        // Debug.Log($"[NetworkGameManager] Starting game with mode: {mode}, time: {time}, scene: {sceneName}");

        

        CurrentGameMode = mode;

        RoundTime = time;

        CurrentGameState = GameState.Starting;

        GameStartTime = Runner.SimulationTime;

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

        

        // Load the scene asynchronously to prevent the main thread from blocking

        StartCoroutine(LoadSceneAsync(sceneName));

    }

    

    

    private System.Collections.IEnumerator LoadSceneAsync(string sceneName)

    {

        // Debug.Log($"[NetworkGameManager] Starting asynchronous scene load for: {sceneName}");



        // Runner.LoadScene is already async in its operation with the server,

        // but we run it in a coroutine to ensure the local client's frame doesn't freeze

        // if scene activation is slow.

        Runner.LoadScene(sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);



        yield return null; // Yield for one frame to let the load process begin.



        // Debug.Log($"[NetworkGameManager] Asynchronous scene load for {sceneName} has been initiated.");



        // After the scene load is initiated, wait for a moment and then initialize the game logic.

        // This ensures that the game state is correctly set to InProgress after the scene is ready.

        StartCoroutine(InitializeGameAfterDelay(1.0f));

    }



    private System.Collections.IEnumerator InitializeGameAfterDelay(float delay)

    {

        yield return new WaitForSeconds(delay);

        if (Runner.IsServer)

        {

            // Debug.Log("[NetworkGameManager] Scene loaded, initializing game...");

            InitializeGame();

        }

    }



    public void InitializeGame()

    {

        if (!Runner.IsServer) return;



        // Debug.Log("[NetworkGameManager] Initializing game state...");

        

        // Refresh spawner reference in case scene changed

        RefreshPlayerSpawner();

        

        // Spawn all players immediately

        SpawnAllPlayers();

        

        // Stay in Starting state — InProgress will be set after the game start countdown ends

        CurrentGameState = GameState.Starting;

        // Debug.Log("[NetworkGameManager] Game state set to Starting. Waiting for countdown to finish before InProgress.");

    }



    /// <summary>

    /// Called after the game start countdown timer expires.

    /// Transitions the game to InProgress and starts the round timer.

    /// </summary>

    public void StartRoundAfterCountdown()

    {

        if (!Object.HasStateAuthority) return;



        // Debug.Log("[NetworkGameManager] Countdown finished. Transitioning to InProgress.");

        CurrentGameState = GameState.InProgress;

        RoundStartTime = Runner.SimulationTime;

        OnRoundStarted?.Invoke();

    }



    private void SpawnAllPlayers()

    {

        if (!Runner.IsServer) return;



        // Debug.Log("[NetworkGameManager] Spawner is ready, spawning all active players.");



        foreach (var player in Runner.ActivePlayers)

        {

            if (playerSpawner != null)

            {

                // Debug.Log($"[NetworkGameManager] Spawning player {player.PlayerId}");

                playerSpawner.SpawnPlayer(player);

            }

            else

            {

                // Debug.LogError("[NetworkGameManager] NetworkPlayerSpawner is null, cannot spawn players!");

            }

            AlivePlayers.Add(player);

        }



        PlayersAlive = AlivePlayers.Count;

        // Debug.Log($"[NetworkGameManager] Game started with {PlayersAlive} players");

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



    public override void FixedUpdateNetwork()

    {

        // Only the server checks timer expiry

        if (!Object.HasStateAuthority) return;

        if (CurrentGameState != GameState.InProgress) return;



        float elapsed = Runner.SimulationTime - RoundStartTime;

        if (elapsed >= RoundTime)

        {

            EndGameByTimer();

        }

    }



    private void EndGameByTimer()

    {

        if (CurrentGameState != GameState.InProgress) return; // guard double-call



        int winningTeam;

        if (BlueTeamScore > RedTeamScore)      winningTeam = 0;

        else if (RedTeamScore > BlueTeamScore) winningTeam = 1;

        else                                   winningTeam = -1; // draw



        EndGame(winningTeam);

    }



    public void RegisterPlayer(PlayerRef player, PlayerNetworkData playerData)

    {

        if (!players.ContainsKey(player))

        {

            players[player] = playerData;

            // Debug.Log($"Player {player.PlayerId} registered with team {playerData.TeamId}");

            

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

            // Debug.Log($"Player {player.PlayerId} unregistered");

            

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

        RoundStartTime = Runner.SimulationTime;

        

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

        // Debug.Log($"Round {CurrentRound} started!");

        OnRoundStarted?.Invoke();

    }

    

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]

    public void RPC_NotifyPlayerDied(PlayerRef victim, float respawnDuration)

    {

        // If this client IS the victim, show the UI

        if (Runner.LocalPlayer == victim && NetworkUIManager.Instance != null)

        {

            NetworkUIManager.Instance.ShowRespawnScreen(respawnDuration);

        }

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

        

        // Update individual player stats

        if (PlayerKills.ContainsKey(killer))

        {

            PlayerKills.Set(killer, PlayerKills[killer] + 1);

        }

        

        if (PlayerDeaths.ContainsKey(victim))

        {

            PlayerDeaths.Set(victim, PlayerDeaths[victim] + 1);

        }

        

        // Remove from alive players

        AlivePlayers.Remove(victim);

        PlayersAlive = AlivePlayers.Count;

        

        var killerData = Runner.GetPlayerObject(killer)?.GetComponent<PlayerNetworkData>();

        var victimData = Runner.GetPlayerObject(victim)?.GetComponent<PlayerNetworkData>();



        string killerName = killerData != null ? killerData.PlayerName : $"Player {killer.PlayerId}";

        string victimName = victimData != null ? victimData.PlayerName : $"Player {victim.PlayerId}";



        Debug.Log($"[NetworkGameManager] *** KILL LOG *** {victimName} was eliminated by {killerName}!");

        

        // Restore ability charge and ammo for the killer

        var killerObject = Runner.GetPlayerObject(killer);

        if (killerObject != null)

        {

            var abilityController = killerObject.GetComponent<PlayerAbilityController>();

            if (abilityController != null)

            {

                abilityController.GrantAbilityCharge();

                Debug.Log($"[NetworkGameManager] Ability charge restored for {killerName} after kill");

            }

            

            // Reset pistol ammo to full on kill

            var pistolBehaviour = killerObject.GetComponent<NetworkPistolBehaviour>();

            if (pistolBehaviour != null)

            {

                pistolBehaviour.ResetAmmoOnKill();

                Debug.Log($"[NetworkGameManager] Pistol ammo reset for {killerName} after kill");

            }

            

            // Reset laser energy to full on kill

            var laserBehaviour = killerObject.GetComponent<NetworkLaserBehaviour>();

            if (laserBehaviour != null)

            {

                laserBehaviour.ResetEnergyOnKill();

                Debug.Log($"[NetworkGameManager] Laser energy reset for {killerName} after kill");

            }

        }

        

        // Update scores - always award points if players have valid, distinct teams 

        // regardless of whether the mode was explicitly set to TeamDeathmatch in the Lobby

        if (killerData != null && victimData != null && 

            killerData.TeamId >= 0 && victimData.TeamId >= 0 && 

            killerData.TeamId != victimData.TeamId)

        {

            // Award point to killer's team

            if (killerData.TeamId == 0) BlueTeamScore++;

            else RedTeamScore++;

            

            string winningTeamName = killerData.TeamId == 0 ? "Blue" : "Red";

            Debug.Log($"[NetworkGameManager] Point awarded to {winningTeamName} Team! New Score - Blue: {BlueTeamScore}, Red: {RedTeamScore}");

        }



        if (CurrentGameMode == GameMode.TeamDeathmatch)

        {

            // Check for round/game end

            CheckGameEndConditions();

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

        if (CurrentGameState == GameState.GameOver) return; // prevent duplicate calls

        CurrentGameState = GameState.GameOver;

        WinningTeam = winningTeam;



        string winnerName = winningTeam == 0 ? "Hero's Won"

                          : winningTeam == 1 ? "Alien's won"

                          : "It's a Draw!";



        RPC_OnGameEnded(winningTeam, winnerName);



        // Return to lobby after a delay

        StartCoroutine(ReturnToLobbyAfterDelay(10f));

    }



    private System.Collections.IEnumerator ReturnToLobbyAfterDelay(float delay)

    {

        yield return new WaitForSeconds(delay);

        if (Object.HasStateAuthority)

        {

            Runner.LoadScene(SceneRef.FromIndex(0), UnityEngine.SceneManagement.LoadSceneMode.Single);

        }

    }



    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]

    private void RPC_OnGameEnded(int winningTeam, string winnerText)

    {

        OnGameEnded?.Invoke();

        NetworkUIManager.Instance?.ShowGameOverScreen(winnerText, winningTeam);

    }



    public Transform GetSpawnPoint(int teamId)

    {

        if (playerSpawner == null)

        {

            // Debug.LogError("[NetworkGameManager] PlayerSpawner is null, cannot get spawn point!");

            return null;

        }



        // Find all spawn points in the scene

        var spawnPoints = FindObjectsOfType<PlayerSpawnPoint>();

        

        if (spawnPoints.Length == 0)

        {

            // Debug.LogWarning("[NetworkGameManager] No spawn points found in scene!");

            return null;

        }



        // Filter spawn points by team ID

        var teamSpawnPoints = System.Array.FindAll(spawnPoints, sp => sp.teamId == teamId);

        

        // If no team-specific spawn points, use any available spawn point

        if (teamSpawnPoints.Length == 0)

        {

            // Debug.LogWarning($"[NetworkGameManager] No spawn points found for team {teamId}, using any available spawn point");

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



    public void ScheduleRespawn(PlayerRef playerRef, int teamId, string playerName, float delay)

    {

        if (!Object.HasStateAuthority) return;

        StartCoroutine(RespawnRoutine(playerRef, teamId, playerName, delay));

    }



    private System.Collections.IEnumerator RespawnRoutine(PlayerRef playerRef, int teamId, string playerName, float delay)

    {

        yield return new WaitForSeconds(delay);

        RespawnPlayer(playerRef, teamId, playerName);

    }



    public void ScheduleDeathSequence(PlayerRef playerRef, int teamId, string playerName, float respawnDelay)

    {

        if (!Object.HasStateAuthority) return;

        StartCoroutine(DeathSequenceRoutine(playerRef, teamId, playerName, respawnDelay));

    }



    private System.Collections.IEnumerator DeathSequenceRoutine(PlayerRef playerRef, int teamId, string playerName, float respawnDelay)

    {

        // 1. Wait a moment so the client has time to receive RPC_UpdateHealth(0) and update UI

        yield return new WaitForSeconds(0.5f);



        // 2. Notify the dead player to show the respawn screen

        // Subtract the 0.5s delay so the total time dead is roughly `respawnDelay`

        float remainingRespawnTime = Mathf.Max(0.1f, respawnDelay - 0.5f);

        RPC_NotifyPlayerDied(playerRef, remainingRespawnTime);



        // 3. Despawn the player

        if (Runner != null)

        {

            var playerObject = Runner.GetPlayerObject(playerRef);

            if (playerObject != null)

            {

                Runner.Despawn(playerObject);

            }

        }



        // 4. Schedule the actual respawn

        ScheduleRespawn(playerRef, teamId, playerName, remainingRespawnTime);

    }



    public void RespawnPlayer(PlayerRef playerRef, int teamId, string playerName)

    {

        if (!Object.HasStateAuthority)

        {

            // Debug.LogError("[NetworkGameManager] Only the server can respawn players!");

            return;

        }



        if (playerSpawner == null)

        {

            // Debug.LogError("[NetworkGameManager] PlayerSpawner is null, cannot respawn player!");

            RefreshPlayerSpawner();

            if (playerSpawner == null) return;

        }



        // Debug.Log($"[NetworkGameManager] Respawning player {playerRef.PlayerId} with team {teamId} and name {playerName}");



        // Spawn the player using the spawner

        playerSpawner.SpawnPlayer(playerRef);



        // Set the player's data after spawning

        var playerObject = Runner.GetPlayerObject(playerRef);

        if (playerObject != null)

        {

            var playerData = playerObject.GetComponent<PlayerNetworkData>();

            if (playerData != null)

            {

                playerData.TeamId = teamId;

                playerData.PlayerName = playerName;

                playerData.Health = 100;

                playerData.UpdateVisuals();

                // Restore ability charge on respawn

                playerObject.GetComponent<PlayerAbilityController>()?.GrantAbilityCharge();

                // Debug.Log($"[NetworkGameManager] Successfully respawned player {playerName} with full health");

            }

            

            // Reset bombs to full capacity

            var bombBehaviour = playerObject.GetComponent<NetworkBombBehaviour>();

            if (bombBehaviour != null)

            {

                bombBehaviour.CurrentBombs = bombBehaviour.MaxBombs;

                // Debug.Log($"[NetworkGameManager] Reset bombs to {bombBehaviour.MaxBombs} for player {playerName}");

            }

        }

        else

        {

            // Debug.LogError($"[NetworkGameManager] Failed to get player object after spawning for player {playerRef.PlayerId}");

        }

    }



}



