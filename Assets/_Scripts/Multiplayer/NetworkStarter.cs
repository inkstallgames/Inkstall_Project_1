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

    private async void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Initialize runner and prewarm network resources
        InitializeRunner();
        await PrewarmNetworkResources();
    }
    
    private async Task PrewarmNetworkResources()
    {
        if (_runner != null)
        {
            // Preload network prefabs
            var prefabs = Resources.LoadAll<NetworkObject>("");
            UnityEngine.Debug.Log($"[NetworkStarter] Prewarming {prefabs.Length} network prefabs");
            
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
            InitializeRunner();
            
            if (_runner == null)
            {
                UnityEngine.Debug.LogError("Failed to initialize NetworkRunner!");
                return;
            }

            if (_runner.IsRunning)
            {
                UnityEngine.Debug.LogWarning("NetworkRunner is already running!");
                return;
            }

            // Generate and store the join code
            CurrentJoinCode = GenerateJoinCode();
            UnityEngine.Debug.Log($"[NetworkStarter] Generated join code: {CurrentJoinCode}");
            UnityEngine.Debug.Log($"[NetworkStarter] Attempting to connect to Photon Cloud...");
            
            // Basic network settings - using only standard Fusion properties
            var startGameArgs = new StartGameArgs()
            {
                GameMode = Fusion.GameMode.Host,
                SessionName = CurrentJoinCode,
                PlayerCount = _maxPlayers,
                Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
                SceneManager = _sceneManager,
                // Standard Fusion properties
                ObjectProvider = _runnerPrefab?.GetComponent<INetworkObjectProvider>()
            };
            
            // Apply any Photon settings from the NetworkRunner prefab
            // These are configured in the Unity Editor on the NetworkRunner prefab

            // Start with timeout
            var startTask = _runner.StartGame(startGameArgs);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30)); // 30 second timeout for cloud connection
            var completedTask = await Task.WhenAny(startTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                UnityEngine.Debug.LogError("Host start timed out!");
                await ShutdownRunner();
                return;
            }

            var result = await startTask;

            if (result.Ok)
            {
                UnityEngine.Debug.Log("[NetworkStarter] Host started successfully");
                
                if (_runner.IsServer && _lobbyManagerPrefab != null)
                {
                    UnityEngine.Debug.Log("[NetworkStarter] Spawning LobbyManager...");
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
                UnityEngine.Debug.LogError(error);
                // Show error to user and notify room creation failed
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    onRoomReady?.Invoke(false);
                    UnityEngine.Debug.LogError(error);
                });
            }
        }
        catch (Exception e)
        {
            string error = $"Error starting host: {e.Message}";
            UnityEngine.Debug.LogError(error);
            // Show error to user and notify room creation failed
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                onRoomReady?.Invoke(false);
                UnityEngine.Debug.LogError(error);
            });
        }
    }

    public async void JoinSession(string sessionCode, Action<bool> onComplete = null)
    {
        if (_isShuttingDown || string.IsNullOrEmpty(sessionCode))
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                onComplete?.Invoke(false);
            });
            return;
        }
        
        InitializeRunner();
        
        if (_runner == null)
        {
            UnityEngine.Debug.LogError("Failed to initialize NetworkRunner!");
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                onComplete?.Invoke(false);
            });
            return;
        }

        if (_runner.IsRunning)
        {
            UnityEngine.Debug.LogWarning("NetworkRunner is already running!");
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                onComplete?.Invoke(false);
            });
            return;
        }

        try
        {
            // Normalize the session code to match host format (uppercase, trimmed)
            string normalizedCode = sessionCode.Trim().ToUpper();
            
            var startGameArgs = new StartGameArgs()
            {
                GameMode = Fusion.GameMode.Client,
                SessionName = normalizedCode,
                Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
                SceneManager = _sceneManager
            };

            UnityEngine.Debug.Log($"[NetworkStarter] Attempting to join session: {normalizedCode}");
            
            // Add timeout for join attempt
            var startTask = _runner.StartGame(startGameArgs);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30)); // 30 second timeout for joining
            var completedTask = await Task.WhenAny(startTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                UnityEngine.Debug.LogError("[NetworkStarter] Join attempt timed out!");
                await ShutdownRunner();
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    onComplete?.Invoke(false);
                });
                return;
            }

            var result = await startTask;

            if (result.Ok)
            {
                UnityEngine.Debug.Log($"[NetworkStarter] Successfully joined session: {normalizedCode}");
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    onComplete?.Invoke(true);
                });
            }
            else
            {
                string error = $"Failed to Join Session '{normalizedCode}': {result.ShutdownReason}";
                UnityEngine.Debug.LogError(error);
                
                // Ensure callback is on main thread
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    onComplete?.Invoke(false);
                });
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[NetworkStarter] Error joining session: {e.Message}\n{e.StackTrace}");
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                onComplete?.Invoke(false);
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

        if (runner.IsServer)
        {
            UnityEngine.Debug.Log("[NetworkStarter] Server is processing player join...");
            // Only spawn players in the game scene
            if (currentScene.name != "Lobby")
            {
                UnityEngine.Debug.Log("[NetworkStarter] Not in Lobby, attempting to spawn player...");
                SpawnPlayer(runner, player);
            }
            else
            {
                UnityEngine.Debug.Log("[NetworkStarter] In Lobby, player spawning is handled by lobby manager");
            }
        }
        else
        {
            UnityEngine.Debug.Log("[NetworkStarter] This is a client, not spawning player");
        }

        var lobbyManager = NetworkLobbyManager.Instance;
        if (lobbyManager != null)
        {
            UnityEngine.Debug.Log("[NetworkStarter] Notifying NetworkLobbyManager about new player");
            lobbyManager.OnPlayerJoined(player);
        }
        else
        {
            UnityEngine.Debug.LogError("[NetworkStarter] NetworkLobbyManager.Instance is null!");
        }
    }

    private void SpawnPlayer(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            var playerObject = runner.GetPlayerObject(player);
            if (playerObject != null) return; // Player already spawned

            if (NetworkLobbyManager.Instance != null && NetworkLobbyManager.Instance.LobbyPlayers.ContainsKey(player))
            {
                var lobbyData = NetworkLobbyManager.Instance.LobbyPlayers[player];
                int heroId = lobbyData.SelectedHeroId;
                NetworkObject heroPrefab = HeroManager.Instance.GetHeroPrefab(heroId);

                if (heroPrefab != null)
                {
                    Transform spawnPoint = NetworkGameManager.Instance.GetSpawnPoint(lobbyData.TeamID);
                    NetworkObject networkPlayerObject = runner.Spawn(heroPrefab, spawnPoint.position, spawnPoint.rotation, player);
                    runner.SetPlayerObject(player, networkPlayerObject);
                }
                else
                {
                    UnityEngine.Debug.LogError($"Hero prefab with ID {heroId} not found!");
                }
            }
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        UnityEngine.Debug.Log($"[Network] Player {player.PlayerId} left");
        NetworkLobbyManager.Instance?.OnPlayerLeft(player);
    }
    
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        UnityEngine.Debug.Log($"[Network] Shutdown: {shutdownReason}");

        // If the shutdown was caused by a failed join attempt, don't reset the whole UI.
        // The join failure logic in MainMenu will handle the UI updates.
        if (shutdownReason == ShutdownReason.GameNotFound || shutdownReason == ShutdownReason.InvalidAuthentication)
        {
            _runner = null;
            return;
        }

        // For all other shutdown reasons (e.g., host leaving), reset to the main menu.
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            var mainMenu = FindObjectOfType<MainMenu>();
            if (mainMenu != null)
            {
                mainMenu.ShowMainMenuPanel();
            }
            else
            {
                // Fallback to reloading the scene if MainMenu is not found
                UnityEngine.Debug.LogWarning("MainMenu not found, reloading scene as a fallback.");
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        });

        _runner = null;
    }
    
    public void OnConnectedToServer(NetworkRunner runner) => UnityEngine.Debug.Log("[Network] Connected to server");
    public void OnDisconnectedFromServer(NetworkRunner runner) => UnityEngine.Debug.Log("[Network] Disconnected from server");
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
        if (runner.IsServer)
        {
            // Spawn all players who are already in the lobby
            foreach (var player in runner.ActivePlayers)
            {
                SpawnPlayer(runner, player);
            }
        }
    }
    public void OnSceneLoadStart(NetworkRunner runner) {}
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {}
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {}
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) {}
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) {}
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
 