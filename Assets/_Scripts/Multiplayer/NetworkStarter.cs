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
        
        // Ensure we have a NetworkRunner in the scene
        InitializeRunner();
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

    public async void StartHost()
    {
        if (_isShuttingDown) return;
        
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
            // Generate the join code and use it as the session name
            var joinCode = GenerateJoinCode();
            
            var startGameArgs = new StartGameArgs()
            {
                GameMode = Fusion.GameMode.Host,
                SessionName = joinCode, // Use the generated code as the session name
                PlayerCount = _maxPlayers,
                Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
                SceneManager = _sceneManager
            };

            Debug.Log($"Starting Host with Join Code: {joinCode}");
            
            var result = await _runner.StartGame(startGameArgs);

            if (result.Ok)
            {
                Debug.Log("Host started successfully.");
                // Spawn the lobby manager for the host
                if (_runner.IsServer && _lobbyManagerPrefab != null)
                {
                    _runner.Spawn(_lobbyManagerPrefab);
                }

                // Update the UI with the join code
                if (LobbyUIManager.Instance != null)
                {
                    LobbyUIManager.Instance.SetJoinCode(joinCode);
                }
            }
            else
            {
                Debug.LogError($"Failed to Start Host: {result.ShutdownReason}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error starting host: {e}");
        }
    }

    public async void JoinSession(string sessionCode)
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

            if (!result.Ok)
            {
                Debug.LogError($"Failed to Join Session: {result.ShutdownReason}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error joining session: {e}");
        }
    }

    public void ShutdownRunner()
    {
        if (_isShuttingDown || _runner == null) return;
        
        _isShuttingDown = true;
        
        if (_runner.IsRunning)
        {
            _runner.Shutdown(false, ShutdownReason.Ok);
        }
        
        _isShuttingDown = false;
        _runner = null;
        
        // Reload the current scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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
