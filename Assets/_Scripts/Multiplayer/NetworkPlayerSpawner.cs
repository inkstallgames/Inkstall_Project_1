using Fusion;

using UnityEngine;

using Fusion.Sockets;

using System.Collections.Generic;

using System.Linq;



public class NetworkPlayerSpawner : NetworkBehaviour
{

    [SerializeField] private GameObject playerPrefab;

    [SerializeField] private LayerMask groundLayer;



    [Header("Spawning")]

    [SerializeField] private float spawnRadius = 2f;

    [SerializeField] private int maxSpawnAttempts = 10;



    private List<PlayerSpawnPoint> spawnPoints = new List<PlayerSpawnPoint>();
    [Networked, Capacity(16)] private NetworkLinkedList<int> occupiedSpawnIndices { get; }

    private NetworkLobbyManager lobbyManager;

    private NetworkGameManager gameManager;

    private NetworkRunner _runner;



    public override void Spawned()
    {
        base.Spawned();
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.SetPlayerSpawner(this);
            Debug.Log("[NetworkPlayerSpawner] Spawned and registered with NetworkGameManager.");
        }
    }

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



    public void Init(NetworkRunner runner)

    {

        _runner = runner;

        Debug.Log($"[NetworkPlayerSpawner] Initialized with Runner. IsServer: {_runner.IsServer}");

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
        Debug.Log($"[NetworkPlayerSpawner] SpawnPlayer called for player {player.PlayerId}. IsServer: {_runner.IsServer}, IsClient: {_runner.IsClient}");

        // Check if player already has an object
        var existingObject = _runner.GetPlayerObject(player);
        if (existingObject != null)
        {
            Debug.LogWarning($"[NetworkPlayerSpawner] Player {player.PlayerId} already has a spawned object: {existingObject.name}");
            return;
        }

        GameObject heroPrefab = null;
        int teamId = -1; // Default to FreeForAll

        if (lobbyManager != null && lobbyManager.LobbyPlayers.ContainsKey(player))
        {
            var lobbyData = lobbyManager.LobbyPlayers[player];
            int heroId = lobbyData.SelectedHeroId;
            teamId = lobbyData.TeamID; // Get teamId from lobby
            var heroNetworkObject = HeroManager.Instance?.GetHeroPrefab(heroId);
            heroPrefab = heroNetworkObject?.gameObject;
            Debug.Log($"[NetworkPlayerSpawner] Using hero prefab {heroId} for player {player.PlayerId}: {(heroPrefab != null ? heroPrefab.name : "Not Found")}");
            Debug.Log($"[NetworkPlayerSpawner] Got TeamId {teamId} from lobby data for player {player.PlayerId}");
        }
        else
        {
            // Fallback to generic player prefab if no lobby data
            heroPrefab = playerPrefab;
            Debug.LogWarning($"[NetworkPlayerSpawner] Lobby data not found for player {player.PlayerId}. Using generic player prefab and default team.");
        }

        if (heroPrefab == null)
        {
            Debug.LogError("[NetworkPlayerSpawner] No hero prefab available for spawning!");
            return;
        }

        // Verify the prefab has NetworkObject component
        var networkObject = heroPrefab.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            Debug.LogError($"[NetworkPlayerSpawner] Hero prefab {heroPrefab.name} does not have a NetworkObject component!");
            return;
        }

        Vector3 spawnPosition = GetSpawnPosition(teamId);
        Quaternion spawnRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        Debug.Log($"[NetworkPlayerSpawner] About to spawn player {player.PlayerId} (Team {teamId}) at position: {spawnPosition}");

        var playerObject = _runner.Spawn(
            heroPrefab,
            spawnPosition,
            spawnRotation,
            player
        );

        if (playerObject != null)
        {
            Debug.Log($"[NetworkPlayerSpawner] Successfully spawned NetworkObject for player {player.PlayerId}. Object ID: {playerObject.Id}, IsValid: {playerObject.IsValid}");
            Debug.Log($"[NetworkPlayerSpawner] Spawned object position: {playerObject.transform.position}, Active: {playerObject.gameObject.activeSelf}");
        }
        else
        {
            Debug.LogError($"[NetworkPlayerSpawner] Failed to spawn player {player.PlayerId} - Runner.Spawn returned null!");
        }
    }

    

    private Vector3 GetSpawnPosition(int teamId)
    {
        Debug.Log($"[NetworkPlayerSpawner] GetSpawnPosition called for TeamId: {teamId}");

        // For FreeForAll (teamId = -1), use any spawn point with teamId = -1
        if (teamId == -1)
        {
            Debug.Log("[NetworkPlayerSpawner] FreeForAll mode - looking for FreeForAll spawn points (teamId = -1)");
            var freeForAllSpawns = spawnPoints.Where(p => p.teamId == -1).ToList();
            if (freeForAllSpawns.Count > 0)
            {
                var availableSpawns = freeForAllSpawns.Where(p => !occupiedSpawnIndices.Contains(spawnPoints.IndexOf(p))).ToList();
                if (availableSpawns.Count == 0) availableSpawns = freeForAllSpawns; // Use any if all are occupied

                var spawnPoint = availableSpawns[Random.Range(0, availableSpawns.Count)];
                if (Runner.IsServer)
                {
                    occupiedSpawnIndices.Add(spawnPoints.IndexOf(spawnPoint));
                }
                Debug.Log($"[NetworkPlayerSpawner] Selected FreeForAll spawn point at: {spawnPoint.transform.position} and marked it as occupied.");
                return spawnPoint.transform.position;
            }
        }

        // Try to find a team-specific spawn point
        var teamSpawnPoints = spawnPoints.Where(p => p.teamId == teamId).ToList();
        if (teamSpawnPoints.Count > 0)
        {
            var availableSpawns = teamSpawnPoints.Where(p => !occupiedSpawnIndices.Contains(spawnPoints.IndexOf(p))).ToList();
            if (availableSpawns.Count == 0) availableSpawns = teamSpawnPoints;

            var spawnPoint = availableSpawns[Random.Range(0, availableSpawns.Count)];
            if (Runner.IsServer)
            {
                occupiedSpawnIndices.Add(spawnPoints.IndexOf(spawnPoint));
            }
            Debug.Log($"[NetworkPlayerSpawner] Selected team spawn point at: {spawnPoint.transform.position} and marked it as occupied.");
            return spawnPoint.transform.position;
        }

        // If no team-specific spawns, use any available spawn point as a fallback
        if (spawnPoints.Count > 0)
        {
            Debug.LogWarning($"[NetworkPlayerSpawner] No spawn points found for team {teamId}. Using any available spawn point.");
            var availableSpawns = spawnPoints.Where(p => !occupiedSpawnIndices.Contains(spawnPoints.IndexOf(p))).ToList();
            if (availableSpawns.Count == 0) availableSpawns = spawnPoints;

            var spawnPoint = availableSpawns[Random.Range(0, availableSpawns.Count)];
            if (Runner.IsServer)
            {
                occupiedSpawnIndices.Add(spawnPoints.IndexOf(spawnPoint));
            }
            Debug.Log($"[NetworkPlayerSpawner] Selected fallback spawn point at: {spawnPoint.transform.position} and marked it as occupied.");
            return spawnPoint.transform.position;
        }

        // Fallback to random position if no spawn points are found at all
        Debug.LogError("[NetworkPlayerSpawner] No spawn points found in scene. Using random position as fallback.");
        return new Vector3(Random.Range(-spawnRadius, spawnRadius), 1, Random.Range(-spawnRadius, spawnRadius));
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

