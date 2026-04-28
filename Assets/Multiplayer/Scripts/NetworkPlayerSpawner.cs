using Fusion;

using UnityEngine;

using Fusion.Sockets;

using System.Collections.Generic;

using System.Linq;

using StarterAssets;



public class NetworkPlayerSpawner : NetworkBehaviour
{

    [SerializeField] private GameObject playerPrefab;       // Team A — playerAmutureprefab
    [SerializeField] private GameObject teamBPlayerPrefab;  // Team B — playerAmutureprefab1

    [SerializeField] private LayerMask groundLayer;



    [Header("Spawning")]

    [SerializeField] private float spawnRadius = 2f;

    [SerializeField] private int maxSpawnAttempts = 10;



    private List<PlayerSpawnPoint> spawnPoints = new List<PlayerSpawnPoint>();
    private HashSet<int> occupiedSpawnIndices = new HashSet<int>();

    private NetworkLobbyManager lobbyManager;

    private NetworkGameManager gameManager;

    private NetworkRunner _runner;



    private void Awake()

    {

        // Find all spawn points in the scene

        spawnPoints = FindObjectsOfType<PlayerSpawnPoint>().ToList();



        // Get references to managers

        lobbyManager = FindObjectOfType<NetworkLobbyManager>();

        gameManager = NetworkGameManager.Instance;



        if (spawnPoints.Count == 0)

        {

            // Debug.LogWarning("No spawn points found in the scene. Using default spawn behavior.");

        }

    }



    public void Init(NetworkRunner runner)

    {

        _runner = runner;

        // Debug.Log($"[NetworkPlayerSpawner] Initialized with Runner. IsServer: {_runner.IsServer}");

    }



    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)

    {

        // Debug.Log($"Player {player.PlayerId} joined the game");



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
        // Debug.Log($"[NetworkPlayerSpawner] SpawnPlayer called for player {player.PlayerId}. IsServer: {_runner.IsServer}, IsClient: {_runner.IsClient}");

        // Check if player already has an object
        var existingObject = _runner.GetPlayerObject(player);
        if (existingObject != null)
        {
            // Debug.LogWarning($"[NetworkPlayerSpawner] Player {player.PlayerId} already has a spawned object: {existingObject.name}");
            return;
        }


        // Get team from lobby data if available, otherwise default to FreeForAll (-1)
        int teamId = -1;
        if (lobbyManager != null && lobbyManager.LobbyPlayers.ContainsKey(player))
        {
            teamId = lobbyManager.LobbyPlayers[player].TeamID;
            // Debug.Log($"[NetworkPlayerSpawner] Got TeamId {teamId} from lobby data for player {player.PlayerId}");
        }

        // Pick prefab based on team
        GameObject prefabToSpawn = playerPrefab; // default / Team A
        if (teamId == 1 && teamBPlayerPrefab != null)
        {
            prefabToSpawn = teamBPlayerPrefab;
            // Debug.Log($"[NetworkPlayerSpawner] Player {player.PlayerId} is Team B — using teamBPlayerPrefab.");
        }
        else
        {
            // Debug.Log($"[NetworkPlayerSpawner] Player {player.PlayerId} is Team A (or unassigned) — using playerPrefab.");
        }

        if (prefabToSpawn == null)
        {
            // Debug.LogError("[NetworkPlayerSpawner] Prefab to spawn is null! Check Inspector assignments.");
            return;
        }

        // Verify the prefab has NetworkObject component
        var networkObject = prefabToSpawn.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            // Debug.LogError($"[NetworkPlayerSpawner] Prefab '{prefabToSpawn.name}' does not have a NetworkObject component!");
            return;
        }

        Vector3 spawnPosition = GetSpawnPosition(teamId);
        Quaternion spawnRotation = Quaternion.Euler(0f, 0f, 0f);

        // Debug.Log($"[NetworkPlayerSpawner] Spawning player {player.PlayerId} with prefab '{prefabToSpawn.name}' at {spawnPosition}");

        var playerObject = _runner.Spawn(
            prefabToSpawn,
            spawnPosition,
            spawnRotation,
            player
        );

        if (playerObject != null)
        {
            _runner.SetPlayerObject(player, playerObject);
            
            // Set the team data on the spawned player
            var playerNetworkData = playerObject.GetComponent<PlayerNetworkData>();
            if (playerNetworkData != null)
            {
                playerNetworkData.TeamId = teamId;
                // Debug.Log($"[NetworkPlayerSpawner] Set TeamId {teamId} on PlayerNetworkData for player {player.PlayerId}");
            }
            else
            {
                // Debug.LogError($"[NetworkPlayerSpawner] PlayerNetworkData component not found on spawned player {player.PlayerId}!");
            }
            
            // Debug.Log($"[NetworkPlayerSpawner] Successfully spawned and registered player {player.PlayerId}. Object ID: {playerObject.Id}");
        }
        else
        {
            // Debug.LogError($"[NetworkPlayerSpawner] Failed to spawn player {player.PlayerId} - Runner.Spawn returned null!");
        }
    }

    

    private Vector3 GetSpawnPosition(int teamId)
    {
        // Debug.Log($"[NetworkPlayerSpawner] GetSpawnPosition called for TeamId: {teamId}");

        // For FreeForAll (teamId = -1), use any spawn point with teamId = -1
        if (teamId == -1)
        {
            // Debug.Log("[NetworkPlayerSpawner] FreeForAll mode - looking for FreeForAll spawn points (teamId = -1)");
            var freeForAllSpawns = spawnPoints.Where(p => p.teamId == -1).ToList();
            if (freeForAllSpawns.Count > 0)
            {
                var availableSpawns = freeForAllSpawns.Where(p => !occupiedSpawnIndices.Contains(spawnPoints.IndexOf(p))).ToList();
                if (availableSpawns.Count == 0) availableSpawns = freeForAllSpawns; // Use any if all are occupied

                var spawnPoint = availableSpawns[Random.Range(0, availableSpawns.Count)];
                occupiedSpawnIndices.Add(spawnPoints.IndexOf(spawnPoint));
                // Debug.Log($"[NetworkPlayerSpawner] Selected FreeForAll spawn point at: {spawnPoint.transform.position} and marked it as occupied.");
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
            occupiedSpawnIndices.Add(spawnPoints.IndexOf(spawnPoint));
            // Debug.Log($"[NetworkPlayerSpawner] Selected team spawn point at: {spawnPoint.transform.position} and marked it as occupied.");
            return spawnPoint.transform.position;
        }

        // If no team-specific spawns, use any available spawn point as a fallback
        if (spawnPoints.Count > 0)
        {
            // Debug.LogWarning($"[NetworkPlayerSpawner] No spawn points found for team {teamId}. Using any available spawn point.");
            var availableSpawns = spawnPoints.Where(p => !occupiedSpawnIndices.Contains(spawnPoints.IndexOf(p))).ToList();
            if (availableSpawns.Count == 0) availableSpawns = spawnPoints;

            var spawnPoint = availableSpawns[Random.Range(0, availableSpawns.Count)];
            occupiedSpawnIndices.Add(spawnPoints.IndexOf(spawnPoint));
            // Debug.Log($"[NetworkPlayerSpawner] Selected fallback spawn point at: {spawnPoint.transform.position} and marked it as occupied.");
            return spawnPoint.transform.position;
        }

        // Fallback to random position if no spawn points are found at all
        // Debug.LogError("[NetworkPlayerSpawner] No spawn points found in scene. Using random position as fallback.");
        return new Vector3(Random.Range(-spawnRadius, spawnRadius), 1, Random.Range(-spawnRadius, spawnRadius));
    }



    // Empty required callbacks

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {}

    // Cached reference for bomb input collection
    private NetworkBombBehaviour _cachedLocalBombBehaviour;

    public override void FixedUpdateNetwork()
    {
        // Process jump input for all players
        if (GetInput<PlayerInputData>(out var input))
        {
            if (input.isJumping)
            {
                ExecuteJump();
            }
        }
    }
    
    private void ExecuteJump()
    {
        // Get the local player object
        var localPlayerNetworkObj = Runner.GetPlayerObject(Runner.LocalPlayer);
        if (localPlayerNetworkObj == null) return;
        
        // Get the actual GameObject from NetworkObject
        var localPlayer = localPlayerNetworkObj.gameObject;
        if (localPlayer == null) return;
        
        // Get the ThirdPersonController
        var thirdPersonController = localPlayer.GetComponent<ThirdPersonController>();
        if (thirdPersonController == null) return;
        
        // Get CharacterController to check if grounded
        var characterController = localPlayer.GetComponent<CharacterController>();
        if (characterController != null && characterController.isGrounded)
        {
            // Additional validation to prevent jumps on unstable surfaces (stair edges)
            if (IsValidJumpSurface(localPlayer.transform))
            {
                // Try to access the jump field through reflection
                var jumpField = thirdPersonController.GetType().GetField("jump", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                if (jumpField != null)
                {
                    jumpField.SetValue(thirdPersonController, true);
                    Debug.Log($"[NetworkPlayerSpawner] Jump executed for Player {Runner.LocalPlayer.PlayerId}");
                    
                    // Limit jump velocity to prevent excessive heights
                    StartCoroutine(LimitJumpVelocity(localPlayer));
                }
                else
                {
                    // Fallback: try to call Jump method
                    var jumpMethod = thirdPersonController.GetType().GetMethod("Jump", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        
                    if (jumpMethod != null)
                    {
                        jumpMethod.Invoke(thirdPersonController, null);
                        Debug.Log($"[NetworkPlayerSpawner] Jump method called for Player {Runner.LocalPlayer.PlayerId}");
                        
                        // Limit jump velocity to prevent excessive heights
                        StartCoroutine(LimitJumpVelocity(localPlayer));
                    }
                }
            }
            else
            {
                Debug.Log($"[NetworkPlayerSpawner] Jump blocked - unstable surface detected for Player {Runner.LocalPlayer.PlayerId}");
            }
        }
    }
    
    /// <summary>
    /// Validates if the player is on a stable surface suitable for jumping
    /// Prevents jumps on unstable surfaces like stair edges (especially lower stair edges)
    /// </summary>
    private bool IsValidJumpSurface(Transform playerTransform)
    {
        // Method 1: Specific check for lower stair edge scenario
        // This is the main culprit - player partially on stair, partially on ground below
        Vector3 playerPos = playerTransform.position;
        
        // Cast multiple rays to detect unstable positioning
        bool isOnStairEdge = false;
        
        // Ray from center (main ground check)
        RaycastHit centerHit;
        if (Physics.Raycast(playerPos + Vector3.up * 0.1f, Vector3.down, out centerHit, 1.0f))
        {
            // Check if we hit a stair (step-like object)
            if (IsStairObject(centerHit.collider.gameObject))
            {
                // Additional check: Are we also close to ground below? (lower stair edge scenario)
                RaycastHit belowHit;
                Vector3 belowCheckPos = playerPos + Vector3.down * 0.8f; // Check lower position
                if (Physics.Raycast(belowCheckPos, Vector3.down, out belowHit, 0.5f))
                {
                    // If we found ground below the stair, we're on a lower stair edge
                    float heightDifference = centerHit.point.y - belowHit.point.y;
                    if (heightDifference > 0.3f && heightDifference < 1.0f) // Typical stair height
                    {
                        isOnStairEdge = true;
                        Debug.Log($"[NetworkPlayerSpawner] Lower stair edge detected: height diff {heightDifference:F2}m");
                    }
                }
            }
        }
        
        // Block jump if on stair edge
        if (isOnStairEdge)
        {
            return false;
        }
        
        // Method 2: Check surface angle using raycast
        RaycastHit hit;
        if (Physics.Raycast(playerPos + Vector3.up * 0.1f, Vector3.down, out hit, 0.5f))
        {
            // Check if surface is too steep (like stair edges)
            float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
            if (slopeAngle > 45f) // Max 45 degree slope for safe jumping
            {
                return false;
            }
            
            // Check if we're on a thin edge (like stair edges)
            if (hit.distance > 0.3f) // Too far from actual ground
            {
                return false;
            }
        }
        
        // Method 3: Check for stable ground below
        RaycastHit groundCheck;
        if (Physics.Raycast(playerPos, Vector3.down, out groundCheck, 1.0f))
        {
            // Ensure we have solid ground beneath
            float groundDistance = Vector3.Distance(playerPos, groundCheck.point);
            if (groundDistance > 0.5f) // Too high above ground
            {
                return false;
            }
        }
        else
        {
            // No ground detected below
            return false;
        }
        
        // Method 4: Check velocity to prevent mid-air jumps
        var rb = playerTransform.GetComponent<Rigidbody>();
        if (rb != null && rb.velocity.y > 2f) // Already moving upward
        {
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Checks if an object is likely a stair or step
    /// </summary>
    private bool IsStairObject(GameObject obj)
    {
        // Check object name for stair-related keywords
        string objName = obj.name.ToLower();
        if (objName.Contains("stair") || objName.Contains("step") || objName.Contains("stairs"))
        {
            return true;
        }
        
        // Check if object has stair-like dimensions (flat top, relatively thin)
        var collider = obj.GetComponent<Collider>();
        if (collider != null)
        {
            Bounds bounds = collider.bounds;
            float height = bounds.size.y;
            float width = Mathf.Max(bounds.size.x, bounds.size.z);
            
            // Stairs are typically wider than they are tall, and have specific height ranges
            if (height > 0.1f && height < 1.0f && width > 0.5f)
            {
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Limits jump velocity to prevent excessive jump heights
    /// Runs for a short duration after jump to cap upward velocity
    /// </summary>
    private System.Collections.IEnumerator LimitJumpVelocity(GameObject player)
    {
        float maxJumpVelocity = 8f; // Maximum allowed upward velocity
        float duration = 0.3f; // How long to enforce the limit
        float elapsed = 0f;
        
        var characterController = player.GetComponent<CharacterController>();
        var rb = player.GetComponent<Rigidbody>();
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            
            // Method 1: If using CharacterController (no Rigidbody)
            if (characterController != null)
            {
                // We can't directly limit CharacterController velocity, but we can detect excessive height
                Vector3 pos = player.transform.position;
                
                // If player is too high above ground, apply downward force
                RaycastHit hit;
                if (Physics.Raycast(pos, Vector3.down, out hit, 10f))
                {
                    float heightAboveGround = pos.y - hit.point.y;
                    if (heightAboveGround > 4f) // Too high
                    {
                        // Move player down gradually
                        characterController.Move(Vector3.down * Time.deltaTime * 10f);
                        Debug.Log($"[NetworkPlayerSpawner] Limiting excessive jump height: {heightAboveGround:F2}m");
                    }
                }
            }
            
            // Method 2: If using Rigidbody
            if (rb != null)
            {
                Vector3 velocity = rb.velocity;
                if (velocity.y > maxJumpVelocity)
                {
                    velocity.y = maxJumpVelocity;
                    rb.velocity = velocity;
                    Debug.Log($"[NetworkPlayerSpawner] Capped jump velocity to {maxJumpVelocity:F2}");
                }
            }
            
            yield return null;
        }
    }
    
    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        // Do not collect input if the game start countdown is active
        if (NetworkLobbyManager.Instance != null && NetworkLobbyManager.Instance.GameStartTimer.IsRunning)
        {
            return;
        }

        // Do not collect input if the settings panel is open
        if (NetworkUIManager.Instance != null && NetworkUIManager.Instance.IsSettingsPanelActive)
        {
            return;
        }

        PlayerInputData data = new PlayerInputData();

        // Use joystick input if available (Android), fall back to keyboard (Editor)
        if (NetworkJoystickControl.Instance != null)
        {
            data.movement = NetworkJoystickControl.Instance.MovementInput;
        }
        else
        {
            data.movement = new Vector2(
                Input.GetAxis("Horizontal"),
                Input.GetAxis("Vertical")
            );
        }
        
        // Get jump input
        data.isJumping = Input.GetButton("Jump") || (NetworkUIManager.Instance != null && NetworkUIManager.Instance.IsJumpHeld);

        // Get aim direction from local player's camera
        var localPlayer = runner.GetPlayerObject(runner.LocalPlayer);
        if (localPlayer != null)
        {
            var cameraController = localPlayer.GetComponent<PlayerCameraController>();
            if (cameraController != null)
            {
                data.aimDirection = cameraController.GetCameraForward();
            }
        }

        // Bomb throw input — try cached reference first, then GetPlayerObject, then fallback scan
        if (_cachedLocalBombBehaviour == null || _cachedLocalBombBehaviour.Object == null)
        {
            _cachedLocalBombBehaviour = null;

            if (localPlayer != null)
            {
                _cachedLocalBombBehaviour = localPlayer.GetComponent<NetworkBombBehaviour>();
            }
            else
            {
                // Fallback: scan all NetworkBombBehaviours for the one with input authority
                var allBombs = FindObjectsOfType<NetworkBombBehaviour>();
                foreach (var bomb in allBombs)
                {
                    if (bomb.Object != null && bomb.Object.HasInputAuthority)
                    {
                        _cachedLocalBombBehaviour = bomb;
                        // Debug.Log($"[NetworkPlayerSpawner] OnInput — found local bomb behaviour via fallback scan: {bomb.gameObject.name}");
                        break;
                    }
                }

                if (_cachedLocalBombBehaviour == null && Time.frameCount % 120 == 0)
                {
                    // Debug.LogWarning("[NetworkPlayerSpawner] OnInput — local player object is NULL. Bomb input will NOT be sent.");
                }
            }
        }

        if (_cachedLocalBombBehaviour != null)
        {
            _cachedLocalBombBehaviour.CollectInput(ref data);
        }

        // Single input.Set call with all data combined
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

