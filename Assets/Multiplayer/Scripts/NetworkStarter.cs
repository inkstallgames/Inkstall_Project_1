using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
using System.Text;
// Removed System.Diagnostics to avoid ambiguity with UnityEngine.Debug

public class NetworkStarter : MonoBehaviour, INetworkRunnerCallbacks
{
    // ========== TESTING MODE ==========
    // Set this to TRUE to enable host-only testing (no client needed)
    // Set this to FALSE to revert to normal multiplayer mode
    [Header("TESTING MODE - Set to false for normal multiplayer")]
    [SerializeField] private bool enableHostOnlyTesting = false;
    // ==================================
    
    // Public property to check if testing mode is enabled
    public bool IsHostOnlyTestingEnabled => enableHostOnlyTesting;

    private static NetworkStarter _instance;
    public static NetworkStarter Instance => _instance;

    [Header("Settings")]
    [SerializeField] private NetworkRunner _runnerPrefab;
    [SerializeField] private int _maxPlayers = 10;
    [SerializeField] private NetworkObject _lobbyManagerPrefab;
    
    [Header("Network Quality")]
    [SerializeField] private float maxAcceptablePing = 150f; // ms
    [SerializeField] private bool enableHostQualityCheck = true;
    
    [Header("FPS OPTIMIZATION - Fast-Paced Settings")]
    [SerializeField] private bool enableFPSOptimizations = true;
    [SerializeField] private int targetFPS = 60;
    [SerializeField] private bool enableVSync = false;
    [SerializeField] private int optimizedUpdateFrequency = 30;
    
    // Store the current join code so it can be accessed by other scripts
    public string CurrentJoinCode { get; private set; }

    private NetworkRunner _runner;
    private NetworkSceneManagerDefault _sceneManager;
    private bool _isShuttingDown = false;
    private bool _wasKicked = false;
    private Coroutine _pingLoggerCoroutine;

    // ===== RECONNECTION SUPPORT =====
    /// <summary>
    /// A persistent connection token (GUID) stored in PlayerPrefs.
    /// Used to identify returning players after a disconnect.
    /// </summary>
    private byte[] _connectionToken;

    /// <summary>
    /// Maps PlayerRef → connection token for all players in the current session.
    /// Only populated on the server.
    /// </summary>
    private Dictionary<PlayerRef, byte[]> _playerTokens = new Dictionary<PlayerRef, byte[]>();

    /// <summary>
    /// Public accessor so NetworkGameManager can pull all tokens when the game starts.
    /// </summary>
    public Dictionary<PlayerRef, byte[]> PlayerTokens => _playerTokens;
    // ===== END RECONNECTION SUPPORT =====

    private async void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Generate or load persistent connection token
        _connectionToken = GetOrCreateConnectionToken();
        
        // Apply FPS optimizations for fast-paced gameplay
        ApplyFPSOptimizations();
        
        // Initialize FPS manager for consistent performance
        InitializeFPSManager();
        
        // Initialize runner and prewarm network resources
        InitializeRunner();
        await PrewarmNetworkResources();
    }
    
    /// <summary>
    /// Apply FPS-specific optimizations for fast-paced gameplay
    /// </summary>
    private void ApplyFPSOptimizations()
    {
        if (!enableFPSOptimizations) return;
        
        // Set target frame rate for consistent FPS
        Application.targetFrameRate = targetFPS;
        
        // Configure VSync for optimal performance
        QualitySettings.vSyncCount = enableVSync ? 1 : 0;
        
        // Optimize physics for FPS gameplay
        Physics.defaultSolverIterations = 4; // Reduced from default 6
        Physics.defaultSolverVelocityIterations = 1; // Reduced from default 1
        
        // Set time step to match target FPS
        Time.fixedDeltaTime = 1f / targetFPS;
        
        UnityEngine.Debug.Log($"[NetworkStarter] FPS Optimizations Applied: {targetFPS} FPS, VSync: {enableVSync}");
    }
    
    private void InitializeFPSManager()
    {
        // Add FPS manager if not already present
        if (GetComponent<NetworkFPSManager>() == null)
        {
            gameObject.AddComponent<NetworkFPSManager>();
            // UnityEngine.Debug.Log("[NetworkStarter] NetworkFPSManager initialized for consistent 60 FPS");
        }
    }
    
    private async Task PrewarmNetworkResources()
    {
        if (_runner != null)
        {
            // Preload network prefabs
            var prefabs = Resources.LoadAll<NetworkObject>("");
            // UnityEngine.Debug.Log($"[NetworkStarter] Prewarming {prefabs.Length} network prefabs");
            
            // Log all available prefabs for debugging
            foreach (var prefab in prefabs)
            {
                // UnityEngine.Debug.Log($"[NetworkStarter] Found network prefab: {prefab.name}");
            }
            
            // Check if LobbyManager prefab is available
            if (_lobbyManagerPrefab != null)
            {
                // UnityEngine.Debug.Log($"[NetworkStarter] LobbyManager prefab assigned: {_lobbyManagerPrefab.name}");
            }
            else
            {
                // UnityEngine.Debug.LogWarning("[NetworkStarter] LobbyManager prefab is NOT assigned!");
            }
            
            // Warm up the network transport layer
            await Task.Delay(100); // Small delay to allow Unity to initialize
        }
    }

    private void InitializeRunner()
    {
        if (_runner == null)
        {
            _runner = FindObjectOfType<NetworkRunner>();
            if (_runner == null && _runnerPrefab != null)
            {
                _runner = Instantiate(_runnerPrefab);
                DontDestroyOnLoad(_runner.gameObject);
            }

            if (_runner != null)
            {
                _runner.ProvideInput = true;
                _runner.AddCallbacks(this);
                
                // Ensure we have a scene manager
                _sceneManager = _runner.GetComponent<NetworkSceneManagerDefault>();
                if (_sceneManager == null)
                {
                    _sceneManager = _runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
                }
            }
        }
    }

    private string GenerateJoinCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(Enumerable.Repeat(chars, 6)
            .Select(s => s[UnityEngine.Random.Range(0, s.Length)]).ToArray());
    }

    public async void StartHost(Action<bool> onRoomReady = null)
    {
        if (_isShuttingDown) return;
        
        try
        {
            // Check host quality before starting
            if (enableHostQualityCheck && !await CheckHostQuality())
            {
                UnityEngine.Debug.LogError("[NetworkStarter] Host quality check failed! Poor connection detected.");
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    onRoomReady?.Invoke(false);
                });
                return;
            }
            
            InitializeRunner();
            
            if (_runner == null)
            {
                // UnityEngine.Debug.LogError("Failed to initialize NetworkRunner!");
                return;
            }

            if (_runner.IsRunning)
            {
                // UnityEngine.Debug.LogWarning("NetworkRunner is already running!");
                return;
            }

            // Generate and store the join code
            CurrentJoinCode = GenerateJoinCode();
            // UnityEngine.Debug.Log($"[NetworkStarter] Generated join code: {CurrentJoinCode}");
            
            // TESTING MODE: Host mode works for solo testing in Fusion
            Fusion.GameMode gameMode = Fusion.GameMode.Host;
            
            if (enableHostOnlyTesting)
            {
                // UnityEngine.Debug.Log($"[NetworkStarter] *** TESTING MODE ENABLED *** Starting as Host (can play solo without client)");
            }
            else
            {
                // UnityEngine.Debug.Log($"[NetworkStarter] Attempting to connect to Photon Cloud...");
            }
            
            // Optimized network settings for better client performance
            var startGameArgs = new StartGameArgs()
            {
                GameMode = gameMode,
                SessionName = CurrentJoinCode,
                PlayerCount = _maxPlayers,          // ← enforce 10-player cap
                Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
                SceneManager = _sceneManager,
                // Connection token for player identification on reconnect
                ConnectionToken = _connectionToken,
                // Ensure proper prefab loading for clients
                ObjectProvider = _runnerPrefab?.GetComponent<INetworkObjectProvider>()
                // NOTE: Network optimization settings (TickRate, ClientPrediction, etc.)
                // are configured in the NetworkRunner prefab in Unity Inspector
            };
            
            // Apply any Photon settings from the NetworkRunner prefab
            // These are configured in the Unity Editor on the NetworkRunner prefab

            // Start with timeout
            var startTask = _runner.StartGame(startGameArgs);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(120)); // 120 second timeout for cloud connection
            var completedTask = await Task.WhenAny(startTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                // UnityEngine.Debug.LogError("Host start timed out!");
                await ShutdownRunner();
                return;
            }

            var result = await startTask;

            if (result.Ok)
            {
                // UnityEngine.Debug.Log("[NetworkStarter] Host started successfully");
                LogConnectionInfo();
                StartPingLogging();
                
                if (_runner.IsServer && _lobbyManagerPrefab != null)
                {
                    // UnityEngine.Debug.Log("[NetworkStarter] Spawning LobbyManager...");
                    _runner.Spawn(_lobbyManagerPrefab);
                }

                // Notify that the room is ready
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    onRoomReady?.Invoke(true);
                    if (LobbyUIManager.Instance != null)
                    {
                        LobbyUIManager.Instance.SetJoinCode(CurrentJoinCode);
                    }
                });
            }
            else
            {
                string error = $"Failed to Start Host: {result.ShutdownReason}";
                // UnityEngine.Debug.LogError(error);
                // Show error to user and notify room creation failed
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    onRoomReady?.Invoke(false);
                    // UnityEngine.Debug.LogError(error);
                });
            }
        }
        catch (Exception e)
        {
            string error = $"Error starting host: {e.Message}";
            // UnityEngine.Debug.LogError(error);
            // Show error to user and notify room creation failed
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                onRoomReady?.Invoke(false);
                // UnityEngine.Debug.LogError(error);
            });
        }
    }

    public async void JoinSession(string sessionCode, Action<bool, string> onComplete = null)
    {
        if (_isShuttingDown || string.IsNullOrEmpty(sessionCode))
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                onComplete?.Invoke(false, "Invalid session code.");
            });
            return;
        }

        InitializeRunner();

        if (_runner == null)
        {
            // UnityEngine.Debug.LogError("Failed to initialize NetworkRunner!");
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                onComplete?.Invoke(false, "Network initialization failed.");
            });
            return;
        }

        if (_runner.IsRunning)
        {
            // UnityEngine.Debug.LogWarning("NetworkRunner is already running!");
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                onComplete?.Invoke(false, "Network is already active.");
            });
            return;
        }

        try
        {
            string normalizedCode = sessionCode.Trim().ToUpper();
            
            // Validate join code format
            if (normalizedCode.Length != 6)
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    onComplete?.Invoke(false, "Invalid join code format. Code must be 6 characters.");
                });
                return;
            }
            
            var startGameArgs = new StartGameArgs()
            {
                GameMode = Fusion.GameMode.Client,
                SessionName = normalizedCode,
                PlayerCount = _maxPlayers,          // ← match host's 10-player cap
                Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
                SceneManager = _sceneManager,
                // Connection token for player identification on reconnect
                ConnectionToken = _connectionToken,
                // Ensure proper prefab loading for clients
                ObjectProvider = _runnerPrefab?.GetComponent<INetworkObjectProvider>()
                // NOTE: Network optimization settings are configured in NetworkRunner prefab
            };

            // UnityEngine.Debug.Log($"[NetworkStarter] Attempting to join session: {normalizedCode}");

            var startTask = _runner.StartGame(startGameArgs);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(120));

            var completedTask = await Task.WhenAny(startTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                // UnityEngine.Debug.LogError("[NetworkStarter] Join attempt timed out!");
                await ShutdownRunner();
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    onComplete?.Invoke(false, "Server request timed out.");
                });
                return;
            }

            var result = await startTask;

            if (result.Ok)
            {
                // UnityEngine.Debug.Log($"[NetworkStarter] Successfully joined session: {normalizedCode}");
                LogConnectionInfo();
                StartPingLogging();
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    onComplete?.Invoke(true, null);
                });
            }
            else
            {
                string error = GetFriendlyErrorMessage(result.ShutdownReason);
                // UnityEngine.Debug.LogError($"[NetworkStarter] Failed to Join: {result.ShutdownReason}");
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    onComplete?.Invoke(false, error);
                });
            }
        }
        catch (Exception e)
        {
            // UnityEngine.Debug.LogError($"[NetworkStarter] Error joining session: {e.Message}\n{e.StackTrace}");
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                onComplete?.Invoke(false, "An unexpected error occurred.");
            });
        }
    }

    private string GetFriendlyErrorMessage(ShutdownReason reason)
    {
        switch (reason)
        {
            case ShutdownReason.GameNotFound:
                return "Room not Found";
            case ShutdownReason.GameClosed:
                return "Game Already Started";
            case ShutdownReason.ConnectionTimeout:
                return "Connection Timed Out";
            case ShutdownReason.ConnectionRefused:
                return "Game Already Started";
            case ShutdownReason.GameIsFull:
                return "Room is full";
            case ShutdownReason.OperationTimeout:
                return "Connection Timed Out";
            case ShutdownReason.InvalidAuthentication:
                return "Auth Failed";
            case ShutdownReason.IncompatibleConfiguration:
                return "Version Mismatch";
            default:
                return $"Error: {reason}";
        }
    }

    public async Task ShutdownRunner()
    {
        if (_isShuttingDown || _runner == null || !_runner.IsRunning) return;
        _isShuttingDown = true;

        try
        {
            // UnityEngine.Debug.Log("[NetworkStarter] Shutting down NetworkRunner...");
            StopPingLogging();
            await _runner.Shutdown();
        }
        catch (Exception e)
        {
            // UnityEngine.Debug.LogError($"[NetworkStarter] Error during shutdown: {e}");
        }
        finally
        {
            _isShuttingDown = false;
        }
    }

    private void LogConnectionInfo()
    {
        if (_runner == null) return;

        // UnityEngine.Debug.Log("========== NETWORK CONNECTION INFO ==========");
        // UnityEngine.Debug.Log($"[NetworkStarter] Region: India (in)");
        // UnityEngine.Debug.Log($"[NetworkStarter] Session Name: {_runner.SessionInfo.Name}");
        // UnityEngine.Debug.Log($"[NetworkStarter] Is Server: {_runner.IsServer}");
        // UnityEngine.Debug.Log($"[NetworkStarter] Is Client: {_runner.IsClient}");
        // UnityEngine.Debug.Log($"[NetworkStarter] Game Mode: {_runner.GameMode}");
        // UnityEngine.Debug.Log("=============================================");
    }

    private void StartPingLogging()
    {
        if (_pingLoggerCoroutine != null)
        {
            StopCoroutine(_pingLoggerCoroutine);
        }
        _pingLoggerCoroutine = StartCoroutine(PingLoggerCoroutine());
        // UnityEngine.Debug.Log("[NetworkStarter] Ping logging started");
    }

    private void StopPingLogging()
    {
        if (_pingLoggerCoroutine != null)
        {
            StopCoroutine(_pingLoggerCoroutine);
            _pingLoggerCoroutine = null;
            // UnityEngine.Debug.Log("[NetworkStarter] Ping logging stopped");
        }
    }

    private System.Collections.IEnumerator PingLoggerCoroutine()
    {
        // Optimized ping logging frequency for FPS games
        float pingInterval = enableFPSOptimizations ? 10f : 5f; // Reduce frequency for FPS
        
        while (_runner != null && _runner.IsRunning)
        {
            if (_runner.IsConnectedToServer || _runner.IsServer)
            {
                int ping = Mathf.RoundToInt((float)(_runner.GetPlayerRtt(_runner.LocalPlayer) * 1000));
                // UnityEngine.Debug.Log($"[PING] Current Ping: {ping}ms | Players: {_runner.ActivePlayers.Count()}/{_maxPlayers}");
                
                // Log ping warnings for FPS games
                if (ping > 150)
                {
                    UnityEngine.Debug.LogWarning($"[PING] High ping detected: {ping}ms - FPS gameplay may be affected");
                }
            }
            
            yield return new WaitForSeconds(pingInterval);
        }
    }

    // --- INetworkRunnerCallbacks Implementation ---
    
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        UnityEngine.Debug.Log($"[NetworkStarter] Player {player.PlayerId} joined. IsServer: {runner.IsServer}");
        var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEngine.Debug.Log($"[NetworkStarter] Current scene: {currentScene.name}");

        // ===== RECONNECTION: Register token and check for returning player =====
        if (runner.IsServer)
        {
            byte[] token = runner.GetPlayerConnectionToken(player);
            string tokenId = (token != null && token.Length > 0) ? NetworkGameManager.TokenToString(token) : "NULL";
            string shortToken = tokenId.Length > 8 ? tokenId.Substring(0, 8) : tokenId;
            UnityEngine.Debug.Log($"[RECONNECT] Player {player.PlayerId} token received: {shortToken}... (length: {(token != null ? token.Length : 0)})");

            if (token != null && token.Length > 0)
            {
                _playerTokens[player] = token;
                UnityEngine.Debug.Log($"[RECONNECT] Stored token {shortToken}... for Player {player.PlayerId} in _playerTokens (count: {_playerTokens.Count})");

                // Register the token with the game manager
                if (NetworkGameManager.Instance != null)
                {
                    NetworkGameManager.Instance.RegisterPlayerToken(player, token);

                    bool gameInProgress = NetworkGameManager.Instance.IsGameInProgress();
                    bool isDisconnected = NetworkGameManager.Instance.IsDisconnectedPlayer(token);
                    bool isKnownPlayer = NetworkGameManager.Instance.IsKnownMatchPlayer(token);
                    UnityEngine.Debug.Log($"[RECONNECT] Token {shortToken}... verification: GameInProgress={gameInProgress}, IsKnownMatchPlayer={isKnownPlayer}, IsDisconnectedPlayer={isDisconnected}");

                    // Check if this is a reconnecting player
                    if (gameInProgress && isDisconnected)
                    {
                        UnityEngine.Debug.Log($"[RECONNECT] ✅ Player {player.PlayerId} is RECONNECTING with token {shortToken}... — restoring state.");
                        NetworkGameManager.Instance.RestoreReconnectedPlayer(player, token);
                        return; // Skip normal join flow — reconnected player is handled
                    }
                    else if (gameInProgress && !isDisconnected)
                    {
                        UnityEngine.Debug.Log($"[RECONNECT] Player {player.PlayerId} joined mid-game but is NOT a disconnected player (token {shortToken}...). Normal join flow.");
                    }
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[RECONNECT] Player {player.PlayerId} has NO connection token! Reconnection will not work for this player.");
            }
        }
        // ===== END RECONNECTION =====

        // Use a coroutine to wait for the lobby manager to be ready, especially on clients
        StartCoroutine(NotifyLobbyManagerOfPlayerJoin(player));

        // Respawn the player if necessary (e.g., joining a game in progress)
        RespawnPlayerIfNecessary(runner, player);
    }

    private void SpawnPlayer(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;

        var playerObject = runner.GetPlayerObject(player);
        if (playerObject != null)
        {
            // Debug.Log($"[NetworkStarter] Player {player.PlayerId} already has a player object, skipping spawn.");
            return;
        }

        // Debug.Log("[NetworkStarter] Attempting to find NetworkPlayerSpawner in the scene.");
        var playerSpawner = FindObjectOfType<NetworkPlayerSpawner>();

        if (playerSpawner != null)
        {
            // Debug.Log($"[NetworkStarter] Found NetworkPlayerSpawner. Spawning player {player.PlayerId}.");
            playerSpawner.Init(runner);
            playerSpawner.SpawnPlayer(player);
        }
        else
        {
            // Debug.LogError("[NetworkStarter] NetworkPlayerSpawner not found in the scene! Player will not be spawned.");
        }
    }

    private System.Collections.IEnumerator NotifyLobbyManagerOfPlayerJoin(PlayerRef player)
    {
        // Wait for the NetworkLobbyManager to be spawned on the client
        float waitTime = 0;
        while (NetworkLobbyManager.Instance == null && waitTime < 5.0f) // 5 second timeout
        {
            yield return null; // Wait for the next frame
            waitTime += Time.deltaTime;
        }

        var lobbyManager = NetworkLobbyManager.Instance;
        if (lobbyManager != null)
        {
            // UnityEngine.Debug.Log("[NetworkStarter] Notifying NetworkLobbyManager about new player");
            lobbyManager.OnPlayerJoined(player);
        }
        else
        {
            // UnityEngine.Debug.LogError("[NetworkStarter] NetworkLobbyManager.Instance is null after waiting!");
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        UnityEngine.Debug.Log($"[NetworkStarter] Player {player.PlayerId} left. IsServer: {runner.IsServer}");
        
        if (runner.IsServer)
        {
            // ===== RECONNECTION: Save player state if game is in progress =====
            bool gameInProgress = NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsGameInProgress();
            
            if (gameInProgress)
            {
                // Get the player's connection token before we lose it
                byte[] token = null;
                _playerTokens.TryGetValue(player, out token);
                string tokenId = (token != null && token.Length > 0) ? NetworkGameManager.TokenToString(token) : "NULL";
                string shortToken = tokenId.Length > 8 ? tokenId.Substring(0, 8) : tokenId;

                UnityEngine.Debug.Log($"[RECONNECT] Player {player.PlayerId} left mid-game. Token: {shortToken}... — character will be KEPT ALIVE for reconnection.");

                if (token != null && token.Length > 0 && NetworkGameManager.Instance != null)
                {
                    // SaveDisconnectedPlayerData stores the NetworkObject reference and removes input authority.
                    // The character stays in the world — it will NOT be despawned.
                    NetworkGameManager.Instance.SaveDisconnectedPlayerData(player, token);
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"[RECONNECT] Cannot save state for Player {player.PlayerId} — token is null or GameManager unavailable.");
                }
                
                // Do NOT despawn the character — it stays in the world for reconnection.
                // Do NOT call runner.Disconnect — Fusion already handles the disconnect.
            }
            else
            {
                UnityEngine.Debug.Log($"[RECONNECT] Player {player.PlayerId} left but game is NOT in progress — despawning character.");
                
                // Game is not in progress (lobby or post-game), so despawn normally
                var playerObject = runner.GetPlayerObject(player);
                if (playerObject != null)
                {
                    runner.Despawn(playerObject);
                }
                else
                {
                    // Fallback: Search for the player's character manually
                    var allNetworkObjects = FindObjectsOfType<NetworkObject>();
                    foreach (var netObj in allNetworkObjects)
                    {
                        if (netObj.InputAuthority == player)
                        {
                            runner.Despawn(netObj);
                            break;
                        }
                    }
                }
                
                // Actively disconnect the player from the server
                runner.Disconnect(player);
            }
            // ===== END RECONNECTION =====

            // Clean up token mapping
            _playerTokens.Remove(player);
        }
        
        NetworkLobbyManager.Instance?.OnPlayerLeft(player);
    }
    
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        // UnityEngine.Debug.Log($"[Network] Shutdown: {shutdownReason}");

        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            
            // If we're not in the Lobby scene, we need to load it first
            if (currentScene.name != "MultiplayerLobby")
            {
                // UnityEngine.Debug.Log($"[NetworkStarter] Client disconnected from {currentScene.name}, loading MultiplayerLobby scene");
                SceneManager.LoadScene("MultiplayerLobby");
                
                // After loading lobby, show the error message
                StartCoroutine(ShowErrorAfterSceneLoad(shutdownReason));
            }
            else
            {
                // We're already in the Lobby scene, just show the error
                ShowShutdownError(shutdownReason);
            }
        });

        _runner = null;
    }
    
    private System.Collections.IEnumerator ShowErrorAfterSceneLoad(ShutdownReason shutdownReason)
    {
        // Wait for the scene to load
        yield return new WaitForSeconds(0.5f);
        
        ShowShutdownError(shutdownReason);
    }
    
    private void ShowShutdownError(ShutdownReason shutdownReason)
    {
        var mainMenu = FindObjectOfType<MainMenu>();
        if (mainMenu == null)
        {
            // UnityEngine.Debug.LogWarning("MainMenu not found after scene load.");
            return;
        }

        switch (shutdownReason)
        {
            case ShutdownReason.ConnectionTimeout:
            case ShutdownReason.OperationTimeout:
                mainMenu.ShowErrorAndReturnToMenu("Server request timed out.");
                break;
            case ShutdownReason.GameNotFound:
            case ShutdownReason.GameClosed:
            case ShutdownReason.GameIsFull:
            case ShutdownReason.ConnectionRefused:
            case ShutdownReason.InvalidAuthentication:
            case ShutdownReason.IncompatibleConfiguration:
                // These are handled by the JoinSession callback, no extra action needed here.
                break;
            default:
                mainMenu.ShowMainMenuPanel();
                break;
        }
    }
    
    public void OnConnectedToServer(NetworkRunner runner) { }
    
    public void OnDisconnectedFromServer(NetworkRunner runner)
    {
        // NOTE: This parameterless overload is NOT called by Fusion 2.
        // The actual callback is OnDisconnectedFromServer(NetworkRunner, NetDisconnectReason) below.
    }
    
    private System.Collections.IEnumerator ShowDisconnectErrorAfterSceneLoad()
    {
        // Wait for the scene to load
        yield return new WaitForSeconds(0.5f);
        
        ShowDisconnectError();
    }
    
    private void ShowDisconnectError()
    {
        var mainMenu = FindObjectOfType<MainMenu>();
        if (mainMenu == null)
        {
            // UnityEngine.Debug.LogWarning("MainMenu not found after scene load.");
            return;
        }
        
        if (_wasKicked)
        {
            _wasKicked = false;
            mainMenu.ShowErrorAndReturnToMenu("You were kicked from the lobby.");
        }
        else
        {
            mainMenu.ShowErrorAndReturnToMenu("Server request timed out.");
        }
    }
    
    /// <summary>
    /// Called by NetworkLobbyManager when the local player is being kicked.
    /// Sets a flag so the disconnect handler shows the correct message.
    /// </summary>
    public void SetKickedFlag(bool kicked)
    {
        _wasKicked = kicked;
    }
    
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        string tokenId = (token != null && token.Length > 0) ? NetworkGameManager.TokenToString(token) : "NULL";
        string shortToken = tokenId.Length > 8 ? tokenId.Substring(0, 8) : tokenId;
        UnityEngine.Debug.Log($"[RECONNECT] OnConnectRequest received. Token: {shortToken}... (length: {(token != null ? token.Length : 0)})");

        // ===== RECONNECTION: Gate mid-game connections =====
        if (runner.IsServer && NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsGameInProgress())
        {
            bool isKnown = token != null && token.Length > 0 && NetworkGameManager.Instance.IsKnownMatchPlayer(token);
            bool isDisconnected = token != null && token.Length > 0 && NetworkGameManager.Instance.IsDisconnectedPlayer(token);
            UnityEngine.Debug.Log($"[RECONNECT] Mid-game connection check — Token: {shortToken}..., IsKnownMatchPlayer: {isKnown}, IsDisconnectedPlayer: {isDisconnected}");

            // Only allow known match players to rejoin during a game
            if (isKnown)
            {
                UnityEngine.Debug.Log($"[RECONNECT] ✅ Connection ACCEPTED — token {shortToken}... is a known match player.");
                request.Accept();
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[RECONNECT] ❌ Connection REJECTED — token {shortToken}... is NOT a known match participant. Game in progress.");
                request.Refuse();
            }
        }
        else
        {
            // Not in a game — accept all connections normally
            UnityEngine.Debug.Log($"[RECONNECT] No active game — accepting connection for token {shortToken}...");
            request.Accept();
        }
    }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        // UnityEngine.Debug.LogError($"[Network] Connect failed: {reason}");
        // UnityEngine.Debug.LogError($"[Network] Remote Address: {remoteAddress}");
        // UnityEngine.Debug.LogError($"[Network] Check firewall/antivirus settings if this persists");
    }
    
    public void OnInput(NetworkRunner runner, NetworkInput input) {}
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) {}
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) {}
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) {}
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) {}
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) {}
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data) {}
    public void OnSceneLoadDone(NetworkRunner runner)
    {
        // This is called on all clients when a new scene is loaded
        // UnityEngine.Debug.Log($"[NetworkStarter] OnSceneLoadDone called. IsServer: {runner.IsServer}");

        var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        // UnityEngine.Debug.Log($"[NetworkStarter] Scene loaded: {currentScene.name}");

        // If returning to the lobby scene, spawn the LobbyManager so players can ready up again
        if (runner.IsServer && currentScene.name == "MultiplayerLobby")
        {
            if (NetworkLobbyManager.Instance == null && _lobbyManagerPrefab != null)
            {
                runner.Spawn(_lobbyManagerPrefab);
            }
        }

        // Client-side check: if we're not in the active players list, we were kicked/timed out
        if (!runner.IsServer && currentScene.name != "MultiplayerLobby")
        {
            bool isInActivePlayersList = false;
            foreach (var player in runner.ActivePlayers)
            {
                if (player == runner.LocalPlayer)
                {
                    isInActivePlayersList = true;
                    break;
                }
            }
            
            if (!isInActivePlayersList)
            {
                // UnityEngine.Debug.LogWarning($"[NetworkStarter] Client is not in active players list after scene load. Returning to lobby.");
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    SceneManager.LoadScene("MultiplayerLobby");
                    StartCoroutine(ShowDisconnectErrorAfterSceneLoad());
                });
                return;
            }
        }

        // Hide the main loading screen that persists between scenes
        if (LobbyUIManager.Instance != null)
        {
            LobbyUIManager.Instance.ShowLoadingScreen(false);
        }

        // If we are in the game scene, tell the scene's specific UI manager to show the waiting screen.
        if (currentScene.name == "Rust")
        {
            if (NetworkUIManager.Instance != null)
            {
                // UnityEngine.Debug.Log("[NetworkStarter] In Rust scene, showing waiting for players screen via NetworkUIManager.");
                NetworkUIManager.Instance.ShowWaitingForPlayersScreen(true);
            }
            else
            {
                // UnityEngine.Debug.LogError("[NetworkStarter] NetworkUIManager.Instance is null in the Rust scene!");
            }
        }

        // Notify the lobby manager that this player has loaded the scene
        if (NetworkLobbyManager.Instance != null)
        {
            // UnityEngine.Debug.Log($"[NetworkStarter] Player {runner.LocalPlayer.PlayerId} has loaded the scene, notifying lobby manager.");
            NetworkLobbyManager.Instance.RPC_PlayerHasLoadedScene(runner.LocalPlayer);
        }
        else
        {
            // UnityEngine.Debug.LogError("[NetworkStarter] NetworkLobbyManager.Instance is null, cannot notify that scene is loaded.");
        }

        // Respawn all players if necessary. This handles late-joiners.
        foreach (var player in runner.ActivePlayers)
        {
            RespawnPlayerIfNecessary(runner, player);
        }
    }
    
    private System.Collections.IEnumerator InitializeGameAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (NetworkGameManager.Instance != null)
        {
            // UnityEngine.Debug.Log("[NetworkStarter] Calling NetworkGameManager.InitializeGame after scene load");
            NetworkGameManager.Instance.InitializeGame();
        }
        else
        {
            // UnityEngine.Debug.LogError("[NetworkStarter] NetworkGameManager.Instance is null after scene load");
        }
    }
    
    public void OnSceneLoadStart(NetworkRunner runner) {}
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {}
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {}
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) {}
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) {}

    private void RespawnPlayerIfNecessary(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (currentScene.name != "Lobby")
            {
                // UnityEngine.Debug.Log($"[NetworkStarter] Checking if player {player.PlayerId} needs to be spawned in scene {currentScene.name}");
                SpawnPlayer(runner, player);
            }
        }
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        UnityEngine.Debug.Log($"[NetworkStarter] Disconnected from server. Reason: {reason}");
        
        // If we're a client and got disconnected, handle it
        if (!runner.IsServer)
        {
            // Shut down the network runner so it doesn't stay alive in the background
            ShutdownRunner();
            
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                
                // If we're not in the Lobby scene, we need to load it first
                if (currentScene.name != "MultiplayerLobby")
                {
                    UnityEngine.Debug.Log($"[NetworkStarter] Client disconnected from {currentScene.name}, loading MultiplayerLobby scene");
                    SceneManager.LoadScene("MultiplayerLobby");
                    
                    // After loading lobby, show the error message
                    StartCoroutine(ShowDisconnectErrorAfterSceneLoad());
                }
                else
                {
                    // We're already in the Lobby scene, just show the error
                    ShowDisconnectError();
                }
            });
        }
    }
    
    /// <summary>
    /// Check if host has acceptable internet quality for multiplayer
    /// </summary>
    private async Task<bool> CheckHostQuality()
    {
        // Test ping to a reliable server (Google DNS as fallback)
        var pingTask = TestConnectionQuality();
        var timeoutTask = Task.Delay(5000); // 5 second timeout
        
        var completedTask = await Task.WhenAny(pingTask, timeoutTask);
        
        if (completedTask == timeoutTask)
        {
            // Host quality check timed out
            return false;
        }
        
        float ping = await pingTask;
        
        if (ping > maxAcceptablePing)
        {
            // Host ping too high
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Test connection quality by pinging a reliable server
    /// </summary>
    private async Task<float> TestConnectionQuality()
    {
        try
        {
            // Use Unity's ping system or fallback to estimated value
            // For now, we'll use a simple estimation based on system performance
            await Task.Delay(100); // Simulate ping test
            
            // In a real implementation, you'd use:
            // 1. Unity's NetworkDiscovery
            // 2. Ping to Photon servers
            // 3. Custom ping implementation
            
            // Return estimated ping based on platform and connection type
            return Application.internetReachability == NetworkReachability.NotReachable ? 999f : 50f;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[NetworkStarter] Ping test failed: {e.Message}");
            return 999f; // Return very high ping on failure
        }
    }
    
    /// <summary>
    /// Get network quality rating for UI display
    /// </summary>
    public string GetNetworkQualityRating(float ping)
    {
        if (ping < 50) return "Excellent";
        if (ping < 100) return "Good";
        if (ping < 150) return "Fair";
        if (ping < 200) return "Poor";
        return "Very Poor";
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
            ShutdownRunner();
        }
    }

    // ===== RECONNECTION: Token Utilities =====

    /// <summary>
    /// Returns or creates a persistent connection token (GUID) stored in PlayerPrefs.
    /// This token uniquely identifies this device/player across sessions.
    /// </summary>
    private byte[] GetOrCreateConnectionToken()
    {
        string tokenKey = "Fusion_ConnectionToken";
        string savedToken = PlayerPrefs.GetString(tokenKey, "");

        if (string.IsNullOrEmpty(savedToken))
        {
            savedToken = Guid.NewGuid().ToString();
            PlayerPrefs.SetString(tokenKey, savedToken);
            PlayerPrefs.Save();
            UnityEngine.Debug.Log($"[RECONNECT] 🆕 Generated NEW connection token: {savedToken.Substring(0, 8)}...");
        }
        else
        {
            UnityEngine.Debug.Log($"[RECONNECT] 📋 Loaded existing connection token: {savedToken.Substring(0, 8)}...");
        }

        // Convert GUID string to bytes
        byte[] tokenBytes = Encoding.UTF8.GetBytes(savedToken);
        string hexToken = NetworkGameManager.TokenToString(tokenBytes);
        UnityEngine.Debug.Log($"[RECONNECT] Token hex ID: {hexToken.Substring(0, Mathf.Min(16, hexToken.Length))}... ({tokenBytes.Length} bytes)");
        return tokenBytes;
    }

    // ===== END RECONNECTION =====
}