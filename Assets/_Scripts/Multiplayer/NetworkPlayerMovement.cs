using Fusion;
using UnityEngine;

public class NetworkPlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    
    [Header("Client Prediction")]
    [Tooltip("Enable client-side prediction for instant local movement")]
    public bool enableClientPrediction = true;
    
    [Networked] public Vector3 AimDirection { get; set; }
    
    private CharacterController characterController;
    private PlayerCameraController cameraController;
    private Vector3 movement;
    
    // Client prediction variables
    private Vector3 _predictedPosition;
    private Quaternion _predictedRotation;
    private bool _isLocalPlayer;

    public override void Spawned()
    {
        _isLocalPlayer = Object.HasInputAuthority;
        
        // Always get camera controller reference for input authority
        if (_isLocalPlayer)
        {
            cameraController = GetComponent<PlayerCameraController>();
            if (cameraController == null)
            {
                Debug.LogWarning("[NetworkPlayerMovement] PlayerCameraController not found on spawned player!");
            }
            
            Debug.Log("[NetworkPlayerMovement] Client-side prediction ENABLED for local player");
        }
        
        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            characterController = gameObject.AddComponent<CharacterController>();
        }
        
        // Initialize predicted values
        _predictedPosition = transform.position;
        _predictedRotation = transform.rotation;
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput<PlayerInputData>(out var input))
        {
            // SERVER-AUTHORITATIVE MOVEMENT
            // For local player with prediction: Skip movement here, only do it in Render()
            // For server/remote players: Process movement normally
            
            bool shouldMoveHere = !_isLocalPlayer || !enableClientPrediction || Runner.IsServer;
            
            // Movement - move relative to where player is looking
            if (shouldMoveHere && input.movement.sqrMagnitude > 0.01f)
            {
                movement = (transform.forward * input.movement.y + transform.right * input.movement.x).normalized;
                characterController.Move(movement * moveSpeed * Runner.DeltaTime);
            }
            
            // Handle Shooting (always process)
            if (input.isShooting)
            {
                Shoot();
            }
            
            // Rotation - face where camera is looking
            if (shouldMoveHere && input.aimDirection != Vector3.zero)
            {
                AimDirection = input.aimDirection;
                Vector3 lookDirection = AimDirection;
                lookDirection.y = 0; // Keep only horizontal rotation
                
                if (lookDirection.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                    transform.rotation = Quaternion.Slerp(transform.rotation, 
                        targetRotation, 
                        rotationSpeed * Runner.DeltaTime);
                }
            }
        }
    }
    
    public override void Render()
    {
        // CLIENT-SIDE PREDICTION
        // This runs every frame (60+ FPS) ONLY on the local player with prediction enabled
        // Provides instant visual feedback while server processes authoritative movement
        
        if (!_isLocalPlayer || !enableClientPrediction || Runner.IsServer) return;
        
        // Get current input (even between network ticks)
        var input = GetComponent<PlayerInputHandler>();
        if (input == null) return;
        
        Vector2 moveInput = input.GetMovementInput();
        
        // Predict movement locally for smooth visuals
        if (moveInput.sqrMagnitude > 0.01f)
        {
            Vector3 predictedMovement = (transform.forward * moveInput.y + transform.right * moveInput.x).normalized;
            
            // Use CharacterController.Move for proper collision
            characterController.Move(predictedMovement * moveSpeed * Time.deltaTime);
        }
        
        // Predict rotation locally
        if (cameraController != null)
        {
            Vector3 aimDir = cameraController.GetCameraForward();
            if (aimDir != Vector3.zero)
            {
                Vector3 lookDirection = aimDir;
                lookDirection.y = 0;
                
                if (lookDirection.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                    transform.rotation = Quaternion.Slerp(transform.rotation, 
                        targetRotation, 
                        rotationSpeed * Time.deltaTime);
                }
            }
        }
    }
    
    private void Shoot()
    {
        // Simple raycast shooting
        if (Runner.IsServer)
        {
            RaycastHit hit;
            Vector3 shootDirection = cameraController != null ? cameraController.GetCameraForward() : transform.forward;
            
            if (Physics.Raycast(transform.position + Vector3.up, shootDirection, out hit, 100f))
            {
                Debug.Log($"Hit: {hit.collider.name}");
                
                // Check if we hit another player
                var hitPlayerData = hit.collider.GetComponent<PlayerNetworkData>();
                if (hitPlayerData != null && hitPlayerData.Object.InputAuthority != Object.InputAuthority)
                {
                    // Use the proper damage system via PlayerNetworkData
                    hitPlayerData.RPC_TakeDamage(25, Object.InputAuthority);
                }
            }
        }
    }
}
