using Fusion;
using UnityEngine;
using Fusion.Sockets;
using System.Collections.Generic;
using System.Linq;

public class NetworkPlayerSpawner : NetworkBehaviour, INetworkRunnerCallbacks
{
    [Header("Player Settings")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private LayerMask groundLayer;
    
    [Header("Spawning")]
    [SerializeField] private float spawnRadius = 2f;
    [SerializeField] private int maxSpawnAttempts = 10;
    
    private List<PlayerSpawnPoint> spawnPoints = new List<PlayerSpawnPoint>();
    private NetworkLobbyManager lobbyManager;
    private NetworkGameManager gameManager;

    private void Awake()
    {
        // Find all spawn points in the scene
        spawnPoints = FindObjectsOfType<PlayerSpawnPoint>().ToList();
        
        // Get references to managers
        lobbyManager = FindObjectOfType<NetworkLobbyManager>();
        gameManager = NetworkGameManager.Instance;
        
        if (spawnPoints.Count == 0)
        {
            Debug.LogWarning("No spawn points found in the scene. Using default spawn behavior.");
        }
    }

    public override void Spawned()
    {
        // Register for network callbacks
        if (Runner != null)
        {
            Runner.AddCallbacks(this);
        }
        
        // If this is the host and we have a lobby manager, register for game start
        if (Object.HasStateAuthority && lobbyManager != null)
        {
            // The game will start through the lobby manager
        }
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player {player.PlayerId} joined the game");
        
        // If we have a lobby, let it handle the player joining
        if (lobbyManager != null)
        {
            lobbyManager.AddPlayerToLobby(player, false);
            
            // If we're in the lobby, don't spawn the player yet
            if (gameManager == null || gameManager.CurrentGameState == GameState.Lobby)
            {
                return;
            }
        }
        
        // Spawn the player in the game
        SpawnPlayer(player);
    }
    
    public void SpawnPlayer(PlayerRef player)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("Player prefab is not assigned in the inspector!");
            return;
        }
        
        // Get the player's team ID (default to -1 if not in a team)
        int teamId = -1;
        var networkData = Runner.GetPlayerObject(player)?.GetComponent<PlayerNetworkData>();
        if (networkData != null)
        {
            teamId = networkData.TeamId;
        }
        
        // Find a suitable spawn point
        Vector3 spawnPosition = GetSpawnPosition(teamId);
        Quaternion spawnRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        
        Debug.Log($"Spawning player {player.PlayerId} (Team {teamId}) at position: {spawnPosition}");
        
        // Spawn the player
        var playerObject = Runner.Spawn(
            playerPrefab,
            spawnPosition,
            spawnRotation,
            player
        );
        
        // Initialize player data if it's the local player
        if (player == Runner.LocalPlayer && playerObject != null)
        {
            // The PlayerNetworkData component will handle the rest
        }
    }
    
    private Vector3 GetSpawnPosition(int teamId)
    {
        // Try to find a team-specific spawn point first
        var teamSpawnPoints = spawnPoints.Where(p => p.teamId == teamId).ToList();
        if (teamSpawnPoints.Count > 0)
        {
            // Find an unoccupied spawn point
            var availableSpawns = teamSpawnPoints.Where(p => !p.isOccupied).ToList();
            if (availableSpawns.Count == 0)
            {
                // If all team spawns are occupied, use any team spawn
                availableSpawns = teamSpawnPoints;
            }
            
            // Pick a random spawn point from available ones
            var spawnPoint = availableSpawns[Random.Range(0, availableSpawns.Count)];
            return spawnPoint.transform.position;
        }
        
        // If no team spawn points, try to find any spawn point
        if (spawnPoints.Count > 0)
        {
            var availableSpawns = spawnPoints.Where(p => !p.isOccupied).ToList();
            if (availableSpawns.Count == 0)
            {
                availableSpawns = spawnPoints;
            }
            
            var spawnPoint = availableSpawns[Random.Range(0, availableSpawns.Count)];
            return spawnPoint.transform.position;
        }
        
        // Fallback to random position in spawn area
        Debug.LogWarning("No spawn points found. Using random spawn position.");
        
        // Try to find a valid position on the ground
        for (int i = 0; i < maxSpawnAttempts; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            Vector3 randomPosition = new Vector3(randomCircle.x, 10f, randomCircle.y);
            
            if (Physics.Raycast(randomPosition, Vector3.down, out RaycastHit hit, 20f, groundLayer))
            {
                return hit.point + Vector3.up * 1f; // Slightly above ground
            }
        }
        
        // If all else fails, return a default position
        return Vector3.up * 2f;
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
