using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkStarter : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkRunner _runnerPrefab;
    private NetworkRunner _runner;

    public async void StartHost()
    {
        if (_runner != null) return;

        _runner = Instantiate(_runnerPrefab);
        _runner.AddCallbacks(this);
        _runner.ProvideInput = true;

        var scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
        var sceneManager = _runner.gameObject.AddComponent<NetworkSceneManagerDefault>();

        await _runner.StartGame(new StartGameArgs()
        {
            GameMode = Fusion.GameMode.Host,
            SessionName = NetworkLobbyManager.Instance.JoinCode, // Use the generated join code
            Scene = scene,
            SceneManager = sceneManager,
            PlayerCount = 10 // Max players
        });
    }

    public async void JoinSession(string sessionCode)
    {
        if (_runner != null) return;

        _runner = Instantiate(_runnerPrefab);
        _runner.AddCallbacks(this);
        _runner.ProvideInput = true;

        var scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
        var sceneManager = _runner.gameObject.AddComponent<NetworkSceneManagerDefault>();

        await _runner.StartGame(new StartGameArgs()
        {
            GameMode = Fusion.GameMode.Client,
            SessionName = sessionCode,
            Scene = scene,
            SceneManager = sceneManager
        });
    }

    // --- INetworkRunnerCallbacks Implementation ---

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player {player.PlayerId} joined.");
        if (NetworkLobbyManager.Instance != null)
        {
            NetworkLobbyManager.Instance.OnPlayerJoined(player);
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player {player.PlayerId} left.");
        if (NetworkLobbyManager.Instance != null)
        {
            NetworkLobbyManager.Instance.OnPlayerLeft(player);
        }
    }

    // --- Unused Callbacks ---
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player){ }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player){ }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data){ }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress){ }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }

    public void ShutdownRunner()
    {
        if (_runner != null && !_runner.IsShutdown)
        {
            _runner.Shutdown();
            _runner = null;
            // Reload the scene to reset to the main menu
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
