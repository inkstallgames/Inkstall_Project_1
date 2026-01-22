using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

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
            Debug.Log($"[NetworkStarter] Prewarming {prefabs.Length} network prefabs");
            
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
                Debug.LogError("Failed to initialize NetworkRunner!");
                return;
            }

            if (_runner.IsRunning)
            {
                Debug.LogWarning("NetworkRunner is already running!");
                return;
            }

            // Generate and store the join code
            CurrentJoinCode = GenerateJoinCode();
            Debug.Log($"[NetworkStarter] Generated join code: {CurrentJoinCode}");
            
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
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10)); // 10 second timeout
            var completedTask = await Task.WhenAny(startTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                Debug.LogError("Host start timed out!");
                await ShutdownRunner();
                return;
            }

            var result = await startTask;

            if (result.Ok)
            {
                Debug.Log("[NetworkStarter] Host started successfully");
                
                if (_runner.IsServer && _lobbyManagerPrefab != null)
                {
                    Debug.Log("[NetworkStarter] Spawning LobbyManager...");
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
                Debug.LogError(error);
                // Show error to user and notify room creation failed
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    onRoomReady?.Invoke(false);
                    Debug.LogError(error);
                });
            }
        }
        catch (Exception e)
        {
            string error = $"Error starting host: {e.Message}";
            Debug.LogError(error);
            // Show error to user and notify room creation failed
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                onRoomReady?.Invoke(false);
                Debug.LogError(error);
            });
        }
    }

    public async void JoinSession(string sessionCode, Action<bool> onComplete = null)
    {
        if (_isShuttingDown || string.IsNullOrEmpty(sessionCode)) return;
        
        InitializeRunner();
        
        if (_runner == null)
        {
            Debug.LogError("Failed to initialize NetworkRunner!");
            return;
        }

        if (_runner.IsRunning)
        {
            Debug.LogWarning("NetworkRunner is already running!");
            return;
        }

        try
        {
            var startGameArgs = new StartGameArgs()
            {
                GameMode = Fusion.GameMode.Client,
                SessionName = sessionCode.Trim()
            };

            Debug.Log($"Joining session: {sessionCode}");
            
            var result = await _runner.StartGame(startGameArgs);

            if (result.Ok)
            {
                Debug.Log($"Successfully joined session: {sessionCode}");
                onComplete?.Invoke(true);
            }
            else
            {
                string error = $"Failed to Join Session: {result.ShutdownReason}";
                Debug.LogError(error);
                onComplete?.Invoke(false);
                
                // Show error to user (you might want to show this in the UI)
                // For now, we'll just log it
                Debug.LogError(error);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error joining session: {e}");
        }
    }

    public async Task ShutdownRunner()
    {
        if (_isShuttingDown || _runner == null || !_runner.IsRunning) return;
        _isShuttingDown = true;

        try
        {
            Debug.Log("[NetworkStarter] Shutting down NetworkRunner...");
            await _runner.Shutdown();
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkStarter] Error during shutdown: {e}");
        }
        finally
        {
            _isShuttingDown = false;
        }
    }

    // --- INetworkRunnerCallbacks Implementation ---
    
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[Network] Player {player.PlayerId} joined");
        NetworkLobbyManager.Instance?.OnPlayerJoined(player);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[Network] Player {player.PlayerId} left");
        NetworkLobbyManager.Instance?.OnPlayerLeft(player);
    }
    
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"[Network] Shutdown: {shutdownReason}");

        // Use the dispatcher to ensure UI updates happen on the main thread
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
                Debug.LogWarning("MainMenu not found, reloading scene as a fallback.");
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        });

        _runner = null;
    }
    
    public void OnConnectedToServer(NetworkRunner runner) => Debug.Log("[Network] Connected to server");
    public void OnDisconnectedFromServer(NetworkRunner runner) => Debug.Log("[Network] Disconnected from server");
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) {}
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) => 
        Debug.LogError($"[Network] Connect failed: {reason}");
    
    // Unused callbacks with empty implementations
    public void OnInput(NetworkRunner runner, NetworkInput input) {}
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) {}
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) {}
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) {}
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) {}
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) {}
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data) {}
    public void OnSceneLoadDone(NetworkRunner runner) {}
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
