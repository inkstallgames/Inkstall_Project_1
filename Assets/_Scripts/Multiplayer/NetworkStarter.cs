using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
// Removed System.Diagnostics to avoid ambiguity with UnityEngine.Debug

public class NetworkStarter : MonoBehaviour, INetworkRunnerCallbacks
{
    private static NetworkStarter _instance;
    public static NetworkStarter Instance => _instance;

    [Header("Settings")]
    [SerializeField] private NetworkRunner _runnerPrefab;
    [SerializeField] private int _maxPlayers = 10;
    [SerializeField] private NetworkObject _lobbyManagerPrefab;
    
    // Store the current join code so it can be accessed by other scripts
    public string CurrentJoinCode { get; private set; }

    private NetworkRunner _runner;
    private NetworkSceneManagerDefault _sceneManager;
    private bool _isShuttingDown = false;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Initialize runner only - don't block with async operations
        InitializeRunner();
    }
    
    private async Task PrewarmNetworkResources()
    {
        if (_runner != null)
        {
            // Minimal warm up - just let Fusion initialize
            await Task.Delay(50); // Reduced delay
            UnityEngine.Debug.Log("[NetworkStarter] Network resources ready");
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

    public async void StartHost(Action<bool, string> onRoomReady = null)
    {
        if (_isShuttingDown) return;
        
        // Quick validation first
        if (_runner == null)
        {
            UnityEngine.Debug.LogError("Failed to initialize NetworkRunner!");
            onRoomReady?.Invoke(false, null);
            return;
        }

        if (_runner.IsRunning)
        {
            UnityEngine.Debug.LogWarning("NetworkRunner is already running!");
            onRoomReady?.Invoke(false, null);
            return;
        }

        try
        {
            // Generate join code quickly
            CurrentJoinCode = GenerateJoinCode();
            UnityEngine.Debug.Log($"[NetworkStarter] Generated join code: {CurrentJoinCode}");
            
            // IMMEDIATELY show the join code in UI (before network connection)
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                if (LobbyUIManager.Instance != null)
                {
                    LobbyUIManager.Instance.SetJoinCode(CurrentJoinCode);
                }
            });
            
            // Optimized network settings
            var startGameArgs = new StartGameArgs()
            {
                GameMode = Fusion.GameMode.Host,
                SessionName = CurrentJoinCode,
                PlayerCount = _maxPlayers,
                Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
                SceneManager = _sceneManager,
                ObjectProvider = _runnerPrefab?.GetComponent<INetworkObjectProvider>()
            };

            // Start with fast timeout
            var startTask = _runner.StartGame(startGameArgs);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10)); // Further reduced to 10 seconds
            var completedTask = await Task.WhenAny(startTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                UnityEngine.Debug.LogError("Host start timed out after 10 seconds!");
                await ShutdownRunner();
                onRoomReady?.Invoke(false, null);
                return;
            }

            var result = await startTask;

            if (result.Ok)
            {
                UnityEngine.Debug.Log("[NetworkStarter] Host started successfully");
                
                // Quick lobby spawn
                if (_runner.IsServer && _lobbyManagerPrefab != null)
                {
                    _runner.Spawn(_lobbyManagerPrefab);
                }

                // Room is ready - notify success with join code
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    onRoomReady?.Invoke(true, CurrentJoinCode);
                });
            }
            else
            {
                string error = $"Failed to Start Host: {result.ShutdownReason}";
                UnityEngine.Debug.LogError(error);
                UnityMainThreadDispatcher.Instance().Enqueue(() => onRoomReady?.Invoke(false, null));
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Error starting host: {e.Message}");
            UnityMainThreadDispatcher.Instance().Enqueue(() => onRoomReady?.Invoke(false, null));
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
            UnityEngine.Debug.LogError("Failed to initialize NetworkRunner!");
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                onComplete?.Invoke(false, "Network initialization failed.");
            });
            return;
        }

        if (_runner.IsRunning)
        {
            UnityEngine.Debug.LogWarning("NetworkRunner is already running!");
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
                Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
                SceneManager = _sceneManager
            };

            UnityEngine.Debug.Log($"[NetworkStarter] Attempting to join session: {normalizedCode}");

            var startTask = _runner.StartGame(startGameArgs);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(15)); // Increased timeout for better reliability

            var completedTask = await Task.WhenAny(startTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                UnityEngine.Debug.LogError("[NetworkStarter] Join attempt timed out after 15 seconds!");
                await ShutdownRunner();
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    onComplete?.Invoke(false, "Room not found or connection failed. Please check the join code and try again.");
                });
                return;
            }

            var result = await startTask;

            if (result.Ok)
            {
                UnityEngine.Debug.Log($"[NetworkStarter] Successfully joined session: {normalizedCode}");
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    onComplete?.Invoke(true, null);
                });
            }
            else
            {
                string error = $"Failed to Join: {result.ShutdownReason}";
                UnityEngine.Debug.LogError(error);
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    onComplete?.Invoke(false, error);
                });
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[NetworkStarter] Error joining session: {e.Message}\n{e.StackTrace}");
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                onComplete?.Invoke(false, "An unexpected error occurred.");
            });
        }
    }

    public async Task ShutdownRunner()
    {
        if (_isShuttingDown || _runner == null || !_runner.IsRunning) return;
        _isShuttingDown = true;

        try
        {
            UnityEngine.Debug.Log("[NetworkStarter] Shutting down NetworkRunner...");
            await _runner.Shutdown();
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[NetworkStarter] Error during shutdown: {e}");
        }
        finally
        {
            _isShuttingDown = false;
        }
    }

    // --- INetworkRunnerCallbacks Implementation ---
    
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        UnityEngine.Debug.Log($"[NetworkStarter] Player {player.PlayerId} joined. IsServer: {runner.IsServer}");
        var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEngine.Debug.Log($"[NetworkStarter] Current scene: {currentScene.name}");

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
            Debug.Log($"[NetworkStarter] Player {player.PlayerId} already has a player object, skipping spawn.");
            return; // Player already spawned
        }

        if (NetworkLobbyManager.Instance == null || !NetworkLobbyManager.Instance.LobbyPlayers.ContainsKey(player))
        {
            Debug.LogError($"[NetworkStarter] Lobby data not found for player {player.PlayerId}. Cannot spawn.");
            return;
        }

        var lobbyData = NetworkLobbyManager.Instance.LobbyPlayers[player];
        int heroId = lobbyData.SelectedHeroId;
        NetworkObject heroPrefab = HeroManager.Instance.GetHeroPrefab(heroId);

        if (heroPrefab == null)
        {
            UnityEngine.Debug.LogError($"[NetworkStarter] Hero prefab with ID {heroId} not found for player {player.PlayerId}!");
            return;
        }

        Debug.Log("[NetworkStarter] Attempting to find NetworkPlayerSpawner in the scene.");
        var playerSpawner = FindObjectOfType<NetworkPlayerSpawner>();

        if (playerSpawner != null)
        {
            Debug.Log("[NetworkStarter] SUCCESS: Found NetworkPlayerSpawner!");
            Debug.Log($"[NetworkStarter] Initializing NetworkPlayerSpawner with the current runner.");
            playerSpawner.Init(runner);
            Debug.Log($"[NetworkStarter] Spawning player {player.PlayerId} with hero {heroId} using NetworkPlayerSpawner");
            playerSpawner.SpawnPlayer(player);
        }
        else
        {
            Debug.LogError("[NetworkStarter] FAILED: NetworkPlayerSpawner not found in the current scene! Player will not be spawned. Please ensure the component is added to a GameObject in the 'MallMap' scene.");
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
            UnityEngine.Debug.Log("[NetworkStarter] Notifying NetworkLobbyManager about new player");
            lobbyManager.OnPlayerJoined(player);
        }
        else
        {
            UnityEngine.Debug.LogError("[NetworkStarter] NetworkLobbyManager.Instance is null after waiting!");
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        UnityEngine.Debug.Log($"[NetworkStarter] Player {player.PlayerId} left. IsServer: {runner.IsServer}");
        
        // Despawn the player's character if they have one
        if (runner.IsServer)
        {
            UnityEngine.Debug.Log($"[NetworkStarter] Attempting to find and despawn character for player {player.PlayerId}");
            
            var playerObject = runner.GetPlayerObject(player);
            if (playerObject != null)
            {
                UnityEngine.Debug.Log($"[NetworkStarter] Found player object via GetPlayerObject: {playerObject.name}");
                UnityEngine.Debug.Log($"[NetworkStarter] Despawning player {player.PlayerId}'s character");
                runner.Despawn(playerObject);
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[NetworkStarter] GetPlayerObject returned null for player {player.PlayerId}");
                
                // Fallback: Search for the player's character manually
                var allNetworkObjects = FindObjectsOfType<NetworkObject>();
                UnityEngine.Debug.Log($"[NetworkStarter] Searching through {allNetworkObjects.Length} NetworkObjects");
                
                foreach (var netObj in allNetworkObjects)
                {
                    if (netObj.InputAuthority == player)
                    {
                        UnityEngine.Debug.Log($"[NetworkStarter] Found character via InputAuthority: {netObj.name}");
                        UnityEngine.Debug.Log($"[NetworkStarter] Despawning {netObj.name} for player {player.PlayerId}");
                        runner.Despawn(netObj);
                        break;
                    }
                }
            }
            
            // Actively disconnect the player from the server to ensure they receive the disconnect signal
            UnityEngine.Debug.Log($"[NetworkStarter] Disconnecting player {player.PlayerId} from server");
            runner.Disconnect(player);
        }
        
        NetworkLobbyManager.Instance?.OnPlayerLeft(player);
    }
    
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        UnityEngine.Debug.Log($"[Network] Shutdown: {shutdownReason}");

        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            
            // If we're not in the Lobby scene, we need to load it first
            if (currentScene.name != "Lobby")
            {
                UnityEngine.Debug.Log($"[NetworkStarter] Client disconnected from {currentScene.name}, loading Lobby scene");
                SceneManager.LoadScene("Lobby");
                
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
            UnityEngine.Debug.LogWarning("MainMenu not found after scene load.");
            return;
        }

        switch (shutdownReason)
        {
            case ShutdownReason.ConnectionTimeout:
                mainMenu.ShowErrorAndReturnToMenu("Connection timed out. Room may not exist or network is slow.");
                break;
            case ShutdownReason.ConnectionRefused:
                mainMenu.ShowErrorAndReturnToMenu("Connection refused. Room may be full or not accepting players.");
                break;
            case ShutdownReason.OperationTimeout:
                mainMenu.ShowErrorAndReturnToMenu("Operation timed out. Please try again.");
                break;
            case ShutdownReason.GameNotFound:
                mainMenu.ShowErrorAndReturnToMenu("Room not found. Please check the join code and try again.");
                break;
            case ShutdownReason.InvalidAuthentication:
                mainMenu.ShowErrorAndReturnToMenu("Authentication failed. Please restart the game.");
                break;
            case ShutdownReason.IncompatibleConfiguration:
                mainMenu.ShowErrorAndReturnToMenu("Incompatible game version. Please ensure all players have the same version.");
                break;
            default:
                mainMenu.ShowErrorAndReturnToMenu($"Connection lost: {shutdownReason}. Please try again.");
                break;
        }
    }
    
    public void OnConnectedToServer(NetworkRunner runner) => UnityEngine.Debug.Log("[Network] Connected to server");
    
    public void OnDisconnectedFromServer(NetworkRunner runner)
    {
        UnityEngine.Debug.Log("[NetworkStarter] Disconnected from server");
        
        // If we're a client and got disconnected, handle it
        if (!runner.IsServer)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                
                // If we're not in the Lobby scene, we need to load it first
                if (currentScene.name != "Lobby")
                {
                    UnityEngine.Debug.Log($"[NetworkStarter] Client disconnected from {currentScene.name}, loading Lobby scene");
                    SceneManager.LoadScene("Lobby");
                    
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
            UnityEngine.Debug.LogWarning("MainMenu not found after scene load.");
            return;
        }
        
        mainMenu.ShowErrorAndReturnToMenu("Server request timed out.");
    }
    
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) {}
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        UnityEngine.Debug.LogError($"[Network] Connect failed: {reason}");
        UnityEngine.Debug.LogError($"[Network] Remote Address: {remoteAddress}");
        UnityEngine.Debug.LogError($"[Network] Check firewall/antivirus settings if this persists");
    }
    
    // Unused callbacks with empty implementations
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
        UnityEngine.Debug.Log($"[NetworkStarter] OnSceneLoadDone called. IsServer: {runner.IsServer}");

        var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEngine.Debug.Log($"[NetworkStarter] Scene loaded: {currentScene.name}");

        // Client-side check: if we're not in the active players list, we were kicked/timed out
        if (!runner.IsServer && currentScene.name != "Lobby")
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
                UnityEngine.Debug.LogWarning($"[NetworkStarter] Client is not in active players list after scene load. Returning to lobby.");
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    SceneManager.LoadScene("Lobby");
                    StartCoroutine(ShowDisconnectErrorAfterSceneLoad());
                });
                return;
            }
        }

        // Hide loading screen for all clients
        if (LobbyUIManager.Instance != null)
        {
            UnityEngine.Debug.Log("[NetworkStarter] Hiding loading screen after scene load");
            LobbyUIManager.Instance.ShowLoadingScreen(false);
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
            UnityEngine.Debug.Log("[NetworkStarter] Calling NetworkGameManager.InitializeGame after scene load");
            NetworkGameManager.Instance.InitializeGame();
        }
        else
        {
            UnityEngine.Debug.LogError("[NetworkStarter] NetworkGameManager.Instance is null after scene load");
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
                UnityEngine.Debug.Log($"[NetworkStarter] Checking if player {player.PlayerId} needs to be spawned in scene {currentScene.name}");
                SpawnPlayer(runner, player);
            }
        }
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) {}
    
    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
            ShutdownRunner();
        }
    }
}
 