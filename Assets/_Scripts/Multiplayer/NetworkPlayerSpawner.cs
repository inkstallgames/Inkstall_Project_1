using Fusion;
using UnityEngine;
using Fusion.Sockets;

public class NetworkPlayerSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Vector3 spawnArea = new Vector3(5f, 0f, 5f);

    private void Start()
    {
        // Register this component to receive network callbacks
        var runner = GetComponent<NetworkRunner>();
        if (runner != null)
        {
            runner.AddCallbacks(this);
        }
        else
        {
            Debug.LogError("NetworkRunner not found on this GameObject!");
        }
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player {player.PlayerId} joined the game");
        
        if (player == runner.LocalPlayer)
        {
            if (playerPrefab == null)
            {
                Debug.LogError("Player prefab is not assigned in the inspector!");
                return;
            }

            // Calculate a random spawn position within the spawn area
            Vector3 spawnPosition = new Vector3(
                Random.Range(-spawnArea.x, spawnArea.x),
                spawnArea.y,
                Random.Range(-spawnArea.z, spawnArea.z)
            );

            Debug.Log($"Spawning player at position: {spawnPosition}");
            
            // Spawn the player
            runner.Spawn(
                playerPrefab,
                spawnPosition,
                Quaternion.identity,
                player
            );
        }
    }

    // Empty required callbacks
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {}
    public void OnInput(NetworkRunner runner, NetworkInput input)
{
    PlayerInputData data = new PlayerInputData();
    data.movement = new Vector2(
        Input.GetAxis("Horizontal"),
        Input.GetAxis("Vertical")
    );
    input.Set(data);
}
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) {}
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) {}
    public void OnConnectedToServer(NetworkRunner runner) {}
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) {}
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) {}
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) {}
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) {}
    public void OnSessionListUpdated(NetworkRunner runner, System.Collections.Generic.List<SessionInfo> sessionList) {}
    public void OnCustomAuthenticationResponse(NetworkRunner runner, System.Collections.Generic.Dictionary<string, object> data) {}
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) {}
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) {}
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) {}
    public void OnSceneLoadDone(NetworkRunner runner) {}
    public void OnSceneLoadStart(NetworkRunner runner) {}
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {}
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {}
}
