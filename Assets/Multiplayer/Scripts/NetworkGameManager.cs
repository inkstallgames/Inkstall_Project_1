using Fusion;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;



public enum GameMode
{
    TeamDeathmatch,   // Team-based deathmatch
    FreeForAll,       // 10-player Free for All deathmatch
    CaptureTheBase    // Capture the base
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
    /// <summary>Display names for the current match (survives despawn/respawn).</summary>
    private readonly Dictionary<PlayerRef, string> _matchPlayerNames = new Dictionary<PlayerRef, string>();
    private NetworkPlayerSpawner playerSpawner;
    private Dictionary<PlayerRef, float> respawnTimers = new Dictionary<PlayerRef, float>();
    private List<NetworkObject> activeProjectiles = new List<NetworkObject>();

    // ===== RECONNECTION SUPPORT =====
    /// <summary>
    /// Stores the state of a player who disconnected mid-game so they can be restored on rejoin.
    /// </summary>
    public struct DisconnectedPlayerData
    {
        public string PlayerName;
        public int TeamId;
        public int Kills;
        public int Deaths;
        public float DisconnectTime;
        /// <summary>
        /// The player's character object that remains alive in the world.
        /// Null if the player was dead (mid-respawn) when they disconnected.
        /// </summary>
        public NetworkObject PlayerObject;
        /// <summary>
        /// The old PlayerRef that was assigned before disconnect.
        /// Needed to transfer networked dictionary entries (kills/deaths) to the new PlayerRef.
        /// </summary>
        public PlayerRef OldPlayerRef;
    }

    /// <summary>
    /// Maps a connection token (hex string) to the saved data of players who disconnected mid-game.
    /// </summary>
    private Dictionary<string, DisconnectedPlayerData> _disconnectedPlayers = new Dictionary<string, DisconnectedPlayerData>();

    /// <summary>
    /// Maps a connection token (hex string) to the PlayerRef that was assigned to that token.
    /// Populated when a player first joins during the game so we can identify them on reconnect.
    /// </summary>
    private Dictionary<string, PlayerRef> _tokenToPlayerRef = new Dictionary<string, PlayerRef>();

    /// <summary>
    /// Set of connection tokens for all players who were part of this match.
    /// Used to distinguish returning players from brand new ones mid-game.
    /// </summary>
    private HashSet<string> _matchPlayerTokens = new HashSet<string>();
    // ===== END RECONNECTION SUPPORT =====



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
        _disconnectedPlayers.Clear();
        _tokenToPlayerRef.Clear();
        _matchPlayerTokens.Clear();
        _matchPlayerNames.Clear();



        // Initialize player stats

        foreach (var player in Runner.ActivePlayers)

        {

            PlayerKills.Set(player, 0);

            PlayerDeaths.Set(player, 0);

            CachePlayerNameFromLobby(player);

        }



        // If no scene name provided, use current scene

        if (string.IsNullOrEmpty(sceneName))

        {

            sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        }



        // Notify all clients that the game is starting

        RPC_NotifyGameStarting();

        

        // Keep the session open so disconnected players can rejoin.
        // The OnConnectRequest callback in NetworkStarter will gate new vs returning players.
        if (Runner.SessionInfo != null)
        {
            Runner.SessionInfo.IsOpen = true;
            Debug.Log("[NetworkGameManager] Session kept open — disconnected players can rejoin during the match.");
        }

        // Register all current players' connection tokens as match participants
        RegisterAllPlayerTokens();

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

        if (CurrentGameMode == GameMode.TeamDeathmatch)
        {
            int winningTeam;
            if (BlueTeamScore > RedTeamScore)      winningTeam = 0;
            else if (RedTeamScore > BlueTeamScore) winningTeam = 1;
            else                                   winningTeam = -1; // draw

            EndGame(winningTeam);
        }
        else if (CurrentGameMode == GameMode.FreeForAll)
        {
            PlayerRef winnerRef = PlayerRef.None;
            int maxKills = -1;
            int minDeaths = int.MaxValue;
            bool isDraw = false;

            foreach (var kvp in PlayerKills)
            {
                int kills = kvp.Value;
                int deaths = PlayerDeaths.ContainsKey(kvp.Key) ? PlayerDeaths.Get(kvp.Key) : 0;

                if (kills > maxKills)
                {
                    maxKills = kills;
                    minDeaths = deaths;
                    winnerRef = kvp.Key;
                    isDraw = false;
                }
                else if (kills == maxKills && maxKills != -1)
                {
                    if (deaths < minDeaths)
                    {
                        minDeaths = deaths;
                        winnerRef = kvp.Key;
                        isDraw = false;
                    }
                    else if (deaths == minDeaths)
                    {
                        isDraw = true;
                    }
                }
            }

            if (isDraw || winnerRef == PlayerRef.None)
            {
                EndGame(-1, "It's a Draw!");
            }
            else
            {
                string winnerName = GetPlayerNameOrFallback(winnerRef);
                EndGame(-1, $"{winnerName} Wins!");
            }
        }
    }

    public string GetPlayerNameOrFallback(PlayerRef player)
    {
        if (player == PlayerRef.None)
            return "Unknown";

        // Live character (always current after respawn)
        if (Runner != null)
        {
            var playerObj = Runner.GetPlayerObject(player);
            if (playerObj != null)
            {
                var liveData = playerObj.GetComponent<PlayerNetworkData>();
                if (liveData != null && !string.IsNullOrEmpty(liveData.PlayerName))
                    return liveData.PlayerName;
            }
        }

        if (_matchPlayerNames.TryGetValue(player, out string cachedName) && !string.IsNullOrEmpty(cachedName))
            return cachedName;

        if (players.TryGetValue(player, out var data) && data != null && !string.IsNullOrEmpty(data.PlayerName))
            return data.PlayerName;

        if (NetworkLobbyManager.Instance != null && NetworkLobbyManager.Instance.LobbyPlayers.ContainsKey(player))
        {
            string lobbyName = NetworkLobbyManager.Instance.LobbyPlayers[player].PlayerName.ToString();
            if (!string.IsNullOrEmpty(lobbyName))
                return lobbyName;
        }

        foreach (var kvp in _disconnectedPlayers)
        {
            if (kvp.Value.OldPlayerRef == player && !string.IsNullOrEmpty(kvp.Value.PlayerName))
                return kvp.Value.PlayerName;
        }

        if (Runner != null && player == Runner.LocalPlayer)
        {
            string prefsName = PlayerPrefs.GetString("PlayerName", "");
            if (!string.IsNullOrEmpty(prefsName))
                return prefsName;
        }

        return $"Player {player.PlayerId}";
    }

    public void SetPlayerDisplayName(PlayerRef player, string name)
    {
        if (player == PlayerRef.None || string.IsNullOrEmpty(name))
            return;
        _matchPlayerNames[player] = name;
    }

    private void CachePlayerNameFromLobby(PlayerRef player)
    {
        if (NetworkLobbyManager.Instance != null && NetworkLobbyManager.Instance.LobbyPlayers.ContainsKey(player))
        {
            string lobbyName = NetworkLobbyManager.Instance.LobbyPlayers[player].PlayerName.ToString();
            if (!string.IsNullOrEmpty(lobbyName))
            {
                SetPlayerDisplayName(player, lobbyName);
                return;
            }
        }

        if (Runner != null && player == Runner.LocalPlayer)
        {
            string prefsName = PlayerPrefs.GetString("PlayerName", "");
            if (!string.IsNullOrEmpty(prefsName))
                SetPlayerDisplayName(player, prefsName);
        }
    }

    public void RegisterPlayer(PlayerRef player, PlayerNetworkData playerData)
    {
        if (playerData == null)
            return;

        players[player] = playerData;

        if (!string.IsNullOrEmpty(playerData.PlayerName))
            SetPlayerDisplayName(player, playerData.PlayerName);
        else
            CachePlayerNameFromLobby(player);
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

    public void OnPlayerKilled(NetworkObject victimObj, PlayerRef killer, string weaponName = "Unknown")
    {
        if (!Object.HasStateAuthority) 
        {
            return;
        }

        PlayerRef victim = victimObj != null ? victimObj.InputAuthority : default;
        var victimData = victimObj != null ? victimObj.GetComponent<PlayerNetworkData>() : null;

        // Update individual player stats
        if (killer != PlayerRef.None && PlayerKills.ContainsKey(killer))
        {
            PlayerKills.Set(killer, PlayerKills[killer] + 1);
        }

        if (victim != PlayerRef.None && PlayerDeaths.ContainsKey(victim))
        {
            PlayerDeaths.Set(victim, PlayerDeaths[victim] + 1);
        }

        // Remove from alive players
        if (victim != PlayerRef.None)
        {
            AlivePlayers.Remove(victim);
            PlayersAlive = AlivePlayers.Count;
        }

        var killerObject = Runner.GetPlayerObject(killer);
        var killerData = killerObject?.GetComponent<PlayerNetworkData>();

        string killerName = killerData != null ? killerData.PlayerName : $"Player {killer.PlayerId}";
        string victimName = victimData != null ? victimData.PlayerName : $"Unknown";


        // Restore ability charge and ammo for the killer
        if (killerObject != null)
        {
            var abilityController = killerObject.GetComponent<PlayerAbilityController>();
            if (abilityController != null)
            {
                abilityController.GrantAbilityCharge();
            }

            // Reset pistol ammo to full on kill
            var pistolBehaviour = killerObject.GetComponent<NetworkPistolBehaviour>();
            if (pistolBehaviour != null)
            {
                pistolBehaviour.ResetAmmoOnKill();
            }

            // Reset laser energy to full on kill
            var laserBehaviour = killerObject.GetComponent<NetworkLaserBehaviour>();
            if (laserBehaviour != null)
            {
                laserBehaviour.ResetEnergyOnKill();
            }
        }

        // Update scores - always award points if players have valid, distinct teams (only in TeamDeathmatch)
        if (CurrentGameMode == GameMode.TeamDeathmatch &&
            killerData != null && victimData != null && 
            killerData.TeamId >= 0 && victimData.TeamId >= 0 && 
            killerData.TeamId != victimData.TeamId)
        {
            // Award point to killer's team
            if (killerData.TeamId == 0) BlueTeamScore++;
            else RedTeamScore++;

            string winningTeamName = killerData.TeamId == 0 ? "Blue" : "Red";
        }

        // Send kill notification to the player who made the kill
        string notificationVictimName = victimName != "Unknown" ? victimName : $"Player {victim.PlayerId}";
        
        int killerTeam = killerData != null ? killerData.TeamId : -1;
        int victimTeam = victimData != null ? victimData.TeamId : -1;

        // Update global kill feed and personal notifications for all players via RPC
        RPC_UpdateKillFeed(killer, killerName, killerTeam, notificationVictimName, victimTeam, weaponName);

        if (CurrentGameMode == GameMode.TeamDeathmatch)
        {
            // Check for round/game end
            CheckGameEndConditions();
        }
    }

    /// <summary>
    /// RPC to send kill feed update to all clients
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdateKillFeed(PlayerRef killerPlayer, string killerName, int killerTeam, string victimName, int victimTeam, string weaponName)
    {
        if (NetworkUIManager.Instance != null)
        {
            // Show big personal notification only if this client is the killer
            if (killerPlayer == Runner.LocalPlayer)
            {
                NetworkUIManager.Instance.OnPlayerKilled(victimName);
            }
            
            // Add entry to the global kill feed for everyone
            NetworkUIManager.Instance.AddKillFeedEntry(killerName, killerTeam, victimName, victimTeam, weaponName);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_DebugSimulateKill(PlayerRef killer)
    {
        Debug.Log($"[NetworkGameManager] Simulated kill by {killer}");
        // Find a random victim to kill
        PlayerRef victim = default;
        foreach (var p in AlivePlayers)
        {
            if (p != killer)
            {
                victim = p;
                break;
            }
        }
        
        var victimObj = Runner.GetPlayerObject(victim);
        OnPlayerKilled(victimObj, killer, "TestWeapon");
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
        else if (CurrentGameMode == GameMode.FreeForAll)
        {
            // Check if any player has reached killsToWin
            PlayerRef bestPlayer = PlayerRef.None;
            int maxKills = -1;
            int minDeaths = int.MaxValue;
            bool isDraw = false;

            foreach (var kvp in PlayerKills)
            {
                if (kvp.Value >= killsToWin)
                {
                    int kills = kvp.Value;
                    int deaths = PlayerDeaths.ContainsKey(kvp.Key) ? PlayerDeaths.Get(kvp.Key) : 0;

                    if (kills > maxKills)
                    {
                        maxKills = kills;
                        minDeaths = deaths;
                        bestPlayer = kvp.Key;
                        isDraw = false;
                    }
                    else if (kills == maxKills)
                    {
                        if (deaths < minDeaths)
                        {
                            minDeaths = deaths;
                            bestPlayer = kvp.Key;
                            isDraw = false;
                        }
                        else if (deaths == minDeaths)
                        {
                            isDraw = true;
                        }
                    }
                }
            }

            if (maxKills >= killsToWin)
            {
                if (isDraw)
                {
                    EndGame(-1, "It's a Draw!");
                }
                else
                {
                    string winnerName = GetPlayerNameOrFallback(bestPlayer);
                    EndGame(-1, $"{winnerName} Wins!");
                }
            }
        }
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

    

    private void EndGame(int winningTeam, string winnerText = null)
    {
        if (CurrentGameState == GameState.GameOver) return; // prevent duplicate calls

        CurrentGameState = GameState.GameOver;
        WinningTeam = winningTeam;

        // Clear reconnection data — no need to rejoin a finished game
        // First, despawn any abandoned characters from disconnected players
        foreach (var kvp in _disconnectedPlayers)
        {
            var data = kvp.Value;
            if (data.PlayerObject != null && data.PlayerObject.IsValid && Runner != null)
            {
                Debug.Log($"[NetworkGameManager] EndGame — despawning abandoned character for '{data.PlayerName}'");
                Runner.Despawn(data.PlayerObject);
            }
        }
        _disconnectedPlayers.Clear();
        _matchPlayerTokens.Clear();

        if (string.IsNullOrEmpty(winnerText))
        {
            winnerText = winningTeam == 0 ? "Hero's Won"
                       : winningTeam == 1 ? "Alien's won"
                       : "It's a Draw!";
        }

        RPC_OnGameEnded(winningTeam, winnerText);

        // The NetworkUIManager handles the Game Over → Leaderboard → Exit sequence on each client.
    }



    private System.Collections.IEnumerator ReturnToLobbyAfterDelay(float delay)

    {

        yield return new WaitForSeconds(delay);

        if (Object.HasStateAuthority)

        {

            // Tell every client (and ourselves) to properly shut down

            RPC_ShutdownAndReturnToLobby();

        }

    }



    /// <summary>

    /// Sent to ALL clients when the game over delay finishes.

    /// Each client shuts down its NetworkRunner (same path as ExitToLobbyBtn),

    /// and the OnShutdown callback in NetworkStarter loads the Lobby scene.

    /// </summary>

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShutdownAndReturnToLobby()
    {
        // Unlock cursor for the lobby scene
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        // Return to lobby but keep the network runner alive
        if (Object.HasStateAuthority)
        {
            // Reopen the session so new players can join for the next match
            if (Runner.SessionInfo != null)
            {
                Runner.SessionInfo.IsOpen = true;
                Debug.Log("[NetworkGameManager] Session reopened — players can join again.");
            }
            
            Runner.LoadScene("MultiplayerLobby", UnityEngine.SceneManagement.LoadSceneMode.Single);
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



    public void ScheduleDeathSequence(NetworkObject deadObject, PlayerRef playerRef, int teamId, string playerName, float respawnDelay)

    {

        if (!Object.HasStateAuthority) return;

        StartCoroutine(DeathSequenceRoutine(deadObject, playerRef, teamId, playerName, respawnDelay));

    }



    private System.Collections.IEnumerator DeathSequenceRoutine(NetworkObject deadObject, PlayerRef playerRef, int teamId, string playerName, float respawnDelay)

    {

        // 1. Wait a moment so the client has time to receive RPC_UpdateHealth(0) and update UI

        yield return new WaitForSeconds(0.5f);



        // 2. Notify the dead player to show the respawn screen

        // Subtract the 0.5s delay so the total time dead is roughly `respawnDelay`

        float remainingRespawnTime = Mathf.Max(0.1f, respawnDelay - 0.5f);

        if (playerRef != PlayerRef.None)
        {
            RPC_NotifyPlayerDied(playerRef, remainingRespawnTime);
        }



        // 3. Despawn the player

        if (Runner != null && deadObject != null && deadObject.IsValid)

        {

            Runner.Despawn(deadObject);

        }



        // 4. Schedule the actual respawn
        if (playerRef != PlayerRef.None)
        {
            ScheduleRespawn(playerRef, teamId, playerName, remainingRespawnTime);
        }
        else
        {
            // Player is disconnected - they still respawn!
            StartCoroutine(RespawnDisconnectedPlayerRoutine(playerName, teamId, remainingRespawnTime));
        }
    }

    private System.Collections.IEnumerator RespawnDisconnectedPlayerRoutine(string playerName, int teamId, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (!Object.HasStateAuthority) yield break;

        // Find the disconnected player's data
        string tokenKey = null;
        foreach (var kvp in _disconnectedPlayers)
        {
            if (kvp.Value.PlayerName == playerName)
            {
                tokenKey = kvp.Key;
                break;
            }
        }

        if (tokenKey == null) yield break;

        var data = _disconnectedPlayers[tokenKey];

        // Spawn a new idle character
        var spawnPoint = GetSpawnPoint(teamId);
        var spawnPosition = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        var spawnRotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        GameObject prefabToSpawn = playerPrefab; // from GameManager
        if (teamId == 1 && weaponPrefabs != null && playerSpawner != null && playerSpawner.teamBPlayerPrefab != null) 
        {
            prefabToSpawn = playerSpawner.teamBPlayerPrefab;
        }
        else if (teamId == 1 && playerSpawner != null && playerSpawner.teamBPlayerPrefab != null)
        {
            prefabToSpawn = playerSpawner.teamBPlayerPrefab;
        }

        var playerObject = Runner.Spawn(prefabToSpawn, spawnPosition, spawnRotation, default); // No input authority

        if (playerObject != null)
        {
            var pnd = playerObject.GetComponent<PlayerNetworkData>();
            if (pnd != null)
            {
                pnd.TeamId = teamId;
                pnd.PlayerName = playerName;
                pnd.Health = 100;
                pnd.Kills = data.Kills;
                pnd.Deaths = data.Deaths;
                pnd.UpdateVisuals();
            }

            // Update the stored reference so when they reconnect, they get this new object
            data.PlayerObject = playerObject;
            _disconnectedPlayers[tokenKey] = data;
            
            Debug.Log($"[NetworkGameManager] Respawned idle character for disconnected player '{playerName}'");
        }
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

                if (CurrentGameMode != GameMode.FreeForAll)
                {
                    playerData.TeamId = teamId;
                }

                playerData.PlayerName = playerName;

                SetPlayerDisplayName(playerRef, playerName);

                RegisterPlayer(playerRef, playerData);

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

    // ===== RECONNECTION SUPPORT METHODS =====

    /// <summary>
    /// Converts a byte[] connection token to a hex string for dictionary lookup.
    /// </summary>
    public static string TokenToString(byte[] token)
    {
        if (token == null || token.Length == 0) return string.Empty;
        var sb = new StringBuilder(token.Length * 2);
        foreach (var b in token)
            sb.AppendFormat("{0:X2}", b);
        return sb.ToString();
    }

    /// <summary>
    /// Registers a player's connection token when they join during an active match.
    /// Called by NetworkStarter.OnPlayerJoined.
    /// </summary>
    public void RegisterPlayerToken(PlayerRef player, byte[] token)
    {
        if (token == null || token.Length == 0) return;
        string tokenStr = TokenToString(token);
        _tokenToPlayerRef[tokenStr] = player;
        _matchPlayerTokens.Add(tokenStr);
    }

    /// <summary>
    /// Registers all currently connected players' tokens as match participants.
    /// Called when the game starts. Pulls tokens from NetworkStarter since
    /// OnPlayerJoined fires during lobby (before NetworkGameManager exists).
    /// </summary>
    private void RegisterAllPlayerTokens()
    {
        var networkStarter = NetworkStarter.Instance;
        if (networkStarter == null)
        {
            Debug.LogWarning("[NetworkGameManager] NetworkStarter.Instance is null — cannot register player tokens!");
            return;
        }

        var allTokens = networkStarter.PlayerTokens;
        // reconnect debug silenced for performance

        foreach (var kvp in allTokens)
        {
            PlayerRef player = kvp.Key;
            byte[] token = kvp.Value;

            if (token == null || token.Length == 0) continue;

            string tokenStr = TokenToString(token);
            _tokenToPlayerRef[tokenStr] = player;
            _matchPlayerTokens.Add(tokenStr);

            string shortToken = tokenStr.Length > 8 ? tokenStr.Substring(0, 8) : tokenStr;
            // reconnect debug silenced for performance
        }

        // reconnect debug silenced for performance
    }

    /// <summary>
    /// Returns true if the game is currently in progress (Starting, InProgress, or RoundOver).
    /// </summary>
    public bool IsGameInProgress()
    {
        return CurrentGameState == GameState.Starting 
            || CurrentGameState == GameState.InProgress 
            || CurrentGameState == GameState.RoundOver;
    }

    /// <summary>
    /// Checks if a connection token belongs to a player who was in this match.
    /// Used by NetworkStarter.OnConnectRequest to allow/reject mid-game joins.
    /// </summary>
    public bool IsKnownMatchPlayer(byte[] token)
    {
        if (token == null || token.Length == 0) return false;
        return _matchPlayerTokens.Contains(TokenToString(token));
    }

    /// <summary>
    /// Checks if a connection token belongs to a player who is currently disconnected.
    /// </summary>
    public bool IsDisconnectedPlayer(byte[] token)
    {
        if (token == null || token.Length == 0) return false;
        return _disconnectedPlayers.ContainsKey(TokenToString(token));
    }

    /// <summary>
    /// Saves a disconnected player's character and stats so they can be restored on rejoin.
    /// The character is kept alive in the world — NOT despawned.
    /// Called by NetworkStarter.OnPlayerLeft when a game is in progress.
    /// </summary>
    public void SaveDisconnectedPlayerData(PlayerRef player, byte[] token)
    {
        if (token == null || token.Length == 0)
        {
            Debug.LogWarning($"[NetworkGameManager] Cannot save disconnected data — no token for player {player.PlayerId}");
            return;
        }

        string tokenStr = TokenToString(token);
        var data = new DisconnectedPlayerData
        {
            DisconnectTime = Runner.SimulationTime,
            OldPlayerRef = player
        };

        // Pull stats from the networked dictionaries
        if (PlayerKills.ContainsKey(player))
            data.Kills = PlayerKills.Get(player);
        if (PlayerDeaths.ContainsKey(player))
            data.Deaths = PlayerDeaths.Get(player);

        // Store the player's character object (it stays alive in the world)
        var playerObj = Runner.GetPlayerObject(player);
        data.PlayerObject = playerObj;

        if (playerObj != null)
        {
            var pnd = playerObj.GetComponent<PlayerNetworkData>();
            if (pnd != null)
            {
                data.TeamId = pnd.TeamId;
                data.PlayerName = pnd.PlayerName;
            }

            // Remove input authority so the character stops receiving input
            // The character will just stand idle in the world
            playerObj.RemoveInputAuthority();
            Debug.Log($"[NetworkGameManager] Removed input authority from '{data.PlayerName}' — character stays in world.");
        }
        else
        {
            // Player object is null — they were dead/respawning when they disconnected
            Debug.Log($"[NetworkGameManager] Player {player.PlayerId} has no character object (dead/respawning). Will respawn on reconnect.");
        }

        // Fallback: try lobby data for team/name
        if (string.IsNullOrEmpty(data.PlayerName) && NetworkLobbyManager.Instance != null
            && NetworkLobbyManager.Instance.LobbyPlayers.ContainsKey(player))
        {
            var lobbyData = NetworkLobbyManager.Instance.LobbyPlayers[player];
            data.TeamId = lobbyData.TeamID;
            data.PlayerName = lobbyData.PlayerName.ToString();
        }

        _disconnectedPlayers[tokenStr] = data;
        Debug.Log($"[NetworkGameManager] Saved disconnected data for '{data.PlayerName}' (Team {data.TeamId}, K:{data.Kills}/D:{data.Deaths}, HasObject: {data.PlayerObject != null})");
    }

    /// <summary>
    /// Restores a reconnected player by reassigning input authority on their existing character.
    /// If the character was destroyed (player was dead when they disconnected), respawns them.
    /// Called by NetworkStarter.OnPlayerJoined when the token matches a disconnected player.
    /// </summary>
    public void RestoreReconnectedPlayer(PlayerRef newPlayerRef, byte[] token)
    {
        string tokenStr = TokenToString(token);
        if (!_disconnectedPlayers.ContainsKey(tokenStr))
        {
            Debug.LogWarning($"[NetworkGameManager] No disconnected data found for token.");
            return;
        }

        var savedData = _disconnectedPlayers[tokenStr];
        _disconnectedPlayers.Remove(tokenStr);

        // Update the token→player mapping with the new PlayerRef
        _tokenToPlayerRef[tokenStr] = newPlayerRef;

        Debug.Log($"[NetworkGameManager] Restoring reconnected player '{savedData.PlayerName}' (Team {savedData.TeamId}, K:{savedData.Kills}/D:{savedData.Deaths}, HasObject: {savedData.PlayerObject != null})");

        // Transfer stats from old PlayerRef to new PlayerRef in networked dictionaries
        PlayerRef oldRef = savedData.OldPlayerRef;
        if (PlayerKills.ContainsKey(oldRef))
            PlayerKills.Remove(oldRef);
        if (PlayerDeaths.ContainsKey(oldRef))
            PlayerDeaths.Remove(oldRef);
        PlayerKills.Set(newPlayerRef, savedData.Kills);
        PlayerDeaths.Set(newPlayerRef, savedData.Deaths);

        // Update alive players list
        AlivePlayers.Remove(oldRef);
        if (AllPlayers.Contains(oldRef))
            AllPlayers.Remove(oldRef);
        if (!AllPlayers.Contains(newPlayerRef))
            AllPlayers.Add(newPlayerRef);

        // Update lobby data with the saved team and name
        if (NetworkLobbyManager.Instance != null)
        {
            NetworkLobbyManager.Instance.RestoreReconnectedPlayerLobbyData(
                newPlayerRef, savedData.PlayerName, savedData.TeamId);
        }

        // Check if the character object still exists in the world
        if (savedData.PlayerObject != null && savedData.PlayerObject.IsValid)
        {
            // Character is still alive — reassign input authority to the reconnecting player
            var existingObject = savedData.PlayerObject;
            existingObject.AssignInputAuthority(newPlayerRef);
            Runner.SetPlayerObject(newPlayerRef, existingObject);

            // Re-add to alive players
            if (!AlivePlayers.Contains(newPlayerRef))
                AlivePlayers.Add(newPlayerRef);
            PlayersAlive = AlivePlayers.Count;

            // Update the PlayerNetworkData with the new stats
            var pnd = existingObject.GetComponent<PlayerNetworkData>();
            if (pnd != null)
            {
                pnd.Kills = savedData.Kills;
                pnd.Deaths = savedData.Deaths;
            }

            Debug.Log($"[NetworkGameManager] ✅ Player '{savedData.PlayerName}' reconnected — SAME character reassigned (input authority transferred to Player {newPlayerRef.PlayerId}).");
        }
        else
        {
            // Character was destroyed (player was dead/respawning when they disconnected)
            // Need to spawn a fresh one
            Debug.Log($"[NetworkGameManager] Player '{savedData.PlayerName}' had no character — spawning fresh.");

            RefreshPlayerSpawner();
            if (playerSpawner != null)
            {
                playerSpawner.SpawnPlayer(newPlayerRef);

                var playerObject = Runner.GetPlayerObject(newPlayerRef);
                if (playerObject != null)
                {
                    var pnd = playerObject.GetComponent<PlayerNetworkData>();
                    if (pnd != null)
                    {
                        pnd.TeamId = savedData.TeamId;
                        pnd.PlayerName = savedData.PlayerName;
                        pnd.Health = 100;
                        pnd.Kills = savedData.Kills;
                        pnd.Deaths = savedData.Deaths;
                        pnd.UpdateVisuals();
                    }

                    // Reset abilities and ammo
                    playerObject.GetComponent<PlayerAbilityController>()?.GrantAbilityCharge();
                    var bombBehaviour = playerObject.GetComponent<NetworkBombBehaviour>();
                    if (bombBehaviour != null)
                        bombBehaviour.CurrentBombs = bombBehaviour.MaxBombs;
                }
            }

            // Re-add to alive players
            if (!AlivePlayers.Contains(newPlayerRef))
                AlivePlayers.Add(newPlayerRef);
            PlayersAlive = AlivePlayers.Count;
        }

        Debug.Log($"[NetworkGameManager] Player '{savedData.PlayerName}' has been fully restored.");
    }

    /// <summary>
    /// Gets the connection token for a player by looking up the stored mapping.
    /// Returns null if no token was registered.
    /// </summary>
    public byte[] GetTokenForPlayer(PlayerRef player)
    {
        foreach (var kvp in _tokenToPlayerRef)
        {
            if (kvp.Value == player)
            {
                // Convert hex string back to byte array
                string hex = kvp.Key;
                byte[] token = new byte[hex.Length / 2];
                for (int i = 0; i < token.Length; i++)
                    token[i] = System.Convert.ToByte(hex.Substring(i * 2, 2), 16);
                return token;
            }
        }
        return null;
    }

    // ===== END RECONNECTION SUPPORT =====
}
