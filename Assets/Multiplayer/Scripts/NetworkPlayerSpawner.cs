using Fusion;
using UnityEngine;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
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
        Quaternion spawnRotation = GetSpawnRotation(teamId);

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
                
                // Restore Kills and Deaths from GameManager so they persist across respawns
                if (gameManager != null)
                {
                    if (gameManager.PlayerKills.ContainsKey(player))
                    {
                        playerNetworkData.Kills = gameManager.PlayerKills.Get(player);
                    }
                    if (gameManager.PlayerDeaths.ContainsKey(player))
                    {
                        playerNetworkData.Deaths = gameManager.PlayerDeaths.Get(player);
                    }
                }
                
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

                var spawnPoint = availableSpawns[UnityEngine.Random.Range(0, availableSpawns.Count)];
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

            var spawnPoint = availableSpawns[UnityEngine.Random.Range(0, availableSpawns.Count)];
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

            var spawnPoint = availableSpawns[UnityEngine.Random.Range(0, availableSpawns.Count)];
            occupiedSpawnIndices.Add(spawnPoints.IndexOf(spawnPoint));
            // Debug.Log($"[NetworkPlayerSpawner] Selected fallback spawn point at: {spawnPoint.transform.position} and marked it as occupied.");
            return spawnPoint.transform.position;
        }

        // Fallback to random position if no spawn points are found at all
        // Debug.LogError("[NetworkPlayerSpawner] No spawn points found in scene. Using random position as fallback.");
        return new Vector3(UnityEngine.Random.Range(-spawnRadius, spawnRadius), 1, UnityEngine.Random.Range(-spawnRadius, spawnRadius));
    }
    
    /// <summary>
    /// Get spawn point rotation for the specified team
    /// </summary>
    private Quaternion GetSpawnRotation(int teamId)
    {
        // For FreeForAll (teamId = -1), use any spawn point with teamId = -1
        if (teamId == -1)
        {
            var freeForAllSpawns = spawnPoints.Where(p => p.teamId == -1).ToList();
            if (freeForAllSpawns.Count > 0)
            {
                var availableSpawns = freeForAllSpawns.Where(p => !occupiedSpawnIndices.Contains(spawnPoints.IndexOf(p))).ToList();
                if (availableSpawns.Count == 0) availableSpawns = freeForAllSpawns;
                var spawnPoint = availableSpawns[UnityEngine.Random.Range(0, availableSpawns.Count)];
                Debug.Log($"[NetworkPlayerSpawner] Selected FreeForAll spawn rotation: {spawnPoint.transform.rotation}");
                return spawnPoint.transform.rotation;
            }
        }
        
        // Try to find a team-specific spawn point
        var teamSpawnPoints = spawnPoints.Where(p => p.teamId == teamId).ToList();
        if (teamSpawnPoints.Count > 0)
        {
            var availableSpawns = teamSpawnPoints.Where(p => !occupiedSpawnIndices.Contains(spawnPoints.IndexOf(p))).ToList();
            if (availableSpawns.Count == 0) availableSpawns = teamSpawnPoints;
            var spawnPoint = availableSpawns[UnityEngine.Random.Range(0, availableSpawns.Count)];
            Debug.Log($"[NetworkPlayerSpawner] Selected team spawn rotation: {spawnPoint.transform.rotation}");
            return spawnPoint.transform.rotation;
        }
        
        // If no team-specific spawns, use any available spawn point as fallback
        if (spawnPoints.Count > 0)
        {
            Debug.LogWarning($"[NetworkPlayerSpawner] No spawn points found for team {teamId}. Using any available spawn point rotation.");
            var availableSpawns = spawnPoints.Where(p => !occupiedSpawnIndices.Contains(spawnPoints.IndexOf(p))).ToList();
            if (availableSpawns.Count == 0) availableSpawns = spawnPoints;
            var spawnPoint = availableSpawns[UnityEngine.Random.Range(0, availableSpawns.Count)];
            Debug.Log($"[NetworkPlayerSpawner] Selected fallback spawn rotation: {spawnPoint.transform.rotation}");
            return spawnPoint.transform.rotation;
        }
        
        // Fallback to identity rotation if no spawn points are found at all
        Debug.LogError("[NetworkPlayerSpawner] No spawn points found in scene. Using identity rotation as fallback.");
        return Quaternion.identity;
    }



    // Empty required callbacks

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {}

    // Cached reference for bomb input collection
    private NetworkBombBehaviour _cachedLocalBombBehaviour;
    
    // PERFORMANCE: Jump optimization with cached reflection
    private System.Reflection.FieldInfo _jumpFieldCache;
    private System.Reflection.MethodInfo _jumpMethodCache;
    private bool _reflectionCached = false;
    
    // PERFORMANCE: Input optimization
    private bool _jumpRequested = false;
    private float _lastJumpTime = 0f;
    private float _jumpCooldown = 0.1f;
    
    // PERFORMANCE: Surface validation caching
    private bool _lastValidationResult = true;
    private float _lastValidationTime = 0f;

    public override void FixedUpdateNetwork()
    {
        // PERFORMANCE: Only process input when necessary
        if (GetInput<PlayerInputData>(out var input))
        {
            // PERFORMANCE: Only check jump when button is pressed (not held)
            if (input.isJumping && !_jumpRequested)
            {
                _jumpRequested = true;
                _lastJumpTime = Time.time;
                ExecuteJumpOptimized();
            }
            else if (!input.isJumping && _jumpRequested)
            {
                _jumpRequested = false;
            }
        }
    }
    
    private void ExecuteJump()
    {
        // PERFORMANCE: Use cached reflection to avoid expensive lookups
        if (!_reflectionCached) CacheReflectionMethods();
        
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
            // PERFORMANCE: Simplified surface validation for FPS
            if (IsValidJumpSurfaceFast(localPlayer.transform))
            {
                // PERFORMANCE: Use cached reflection instead of runtime lookup
                if (_jumpFieldCache != null)
                {
                    _jumpFieldCache.SetValue(thirdPersonController, true);
                    StartCoroutine(LimitJumpVelocityOptimized(localPlayer));
                }
                else if (_jumpMethodCache != null)
                {
                    _jumpMethodCache.Invoke(thirdPersonController, null);
                    StartCoroutine(LimitJumpVelocityOptimized(localPlayer));
                }
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
        
        return false; // Added missing return statement
    }
    
    /// <summary>
    /// PERFORMANCE: Cache reflection calls to avoid expensive runtime lookups
    /// </summary>
    private void CacheReflectionMethods()
    {
        if (_reflectionCached) return;
        
        var type = typeof(ThirdPersonController);
        _jumpFieldCache = type.GetField("jump", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        _jumpMethodCache = type.GetMethod("Jump", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        _reflectionCached = true;
        
        Debug.Log("[NetworkPlayerSpawner] Reflection methods cached for performance");
    }
    
    /// <summary>
    /// PERFORMANCE: Fast surface validation with minimal raycasts
    /// </summary>
    private bool IsValidJumpSurfaceFast(Transform playerTransform)
    {
        // PERFORMANCE: Cache validation for 0.1 seconds to avoid raycasts every frame
        if (Time.time - _lastValidationTime < 0.1f) 
            return _lastValidationResult;
        
        Vector3 playerPos = playerTransform.position;
        RaycastHit hit;
        
        // PERFORMANCE: Single raycast instead of multiple
        if (Physics.Raycast(playerPos + Vector3.up * 0.1f, Vector3.down, out hit, 1.0f))
        {
            // Quick stair edge detection for FPS
            float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
            if (slopeAngle > 45f) 
            {
                _lastValidationTime = Time.time;
                _lastValidationResult = false;
                return false;
            }
            
            // Quick ground distance check
            if (hit.distance > 0.5f) 
            {
                _lastValidationTime = Time.time;
                _lastValidationResult = false;
                return false;
            }
        }
        
        _lastValidationTime = Time.time;
        _lastValidationResult = true;
        return true;
    }
    
    /// <summary>
    /// PERFORMANCE: Optimized velocity limiting with reduced checks
    /// </summary>
    private System.Collections.IEnumerator LimitJumpVelocityOptimized(GameObject player)
    {
        float maxJumpVelocity = 8f;
        float duration = 0.2f; // Reduced duration for performance
        float elapsed = 0f;
        
        var characterController = player.GetComponent<CharacterController>();
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            
            // PERFORMANCE: Only check CharacterController (most common case)
            if (characterController != null)
            {
                Vector3 pos = player.transform.position;
                RaycastHit hit;
                
                if (Physics.Raycast(pos, Vector3.down, out hit, 5f)) // Reduced range
                {
                    float heightAboveGround = pos.y - hit.point.y;
                    if (heightAboveGround > 3f) // Reduced threshold
                    {
                        characterController.Move(Vector3.down * Time.deltaTime * 8f); // Reduced force
                    }
                }
            }
            
            yield return null;
        }
    }
    
    /// <summary>
    /// PERFORMANCE: Optimized jump execution with cached reflection
    /// </summary>
    private void ExecuteJumpOptimized()
    {
        // PERFORMANCE: Use cached reflection to avoid expensive lookups
        if (!_reflectionCached) CacheReflectionMethods();
        
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
            // PERFORMANCE: Only validate surface when actually jumping
            if (IsValidJumpSurfaceOptimized(localPlayer.transform))
            {
                // PERFORMANCE: Use cached reflection instead of runtime lookup
                if (_jumpFieldCache != null)
                {
                    _jumpFieldCache.SetValue(thirdPersonController, true);
                    StartCoroutine(LimitJumpVelocityOptimized(localPlayer));
                }
                else if (_jumpMethodCache != null)
                {
                    _jumpMethodCache.Invoke(thirdPersonController, null);
                    StartCoroutine(LimitJumpVelocityOptimized(localPlayer));
                }
            }
        }
    }
    
    /// <summary>
    /// PERFORMANCE: Optimized surface validation with caching
    /// </summary>
    private bool IsValidJumpSurfaceOptimized(Transform playerTransform)
    {
        // PERFORMANCE: Cache validation result to avoid raycasts every frame
        if (Time.time - _lastValidationTime < 0.1f) 
            return _lastValidationResult;
        
        // Simplified validation for FPS games - only check critical cases
        Vector3 playerPos = playerTransform.position;
        RaycastHit hit;
        
        // Single raycast for performance
        if (Physics.Raycast(playerPos + Vector3.up * 0.1f, Vector3.down, out hit, 1.0f))
        {
            // Quick stair edge detection
            float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
            if (slopeAngle > 45f) return false;
            
            // Quick ground distance check
            if (hit.distance > 0.5f) return false;
        }
        
        _lastValidationTime = Time.time;
        _lastValidationResult = true;
        return true;
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

