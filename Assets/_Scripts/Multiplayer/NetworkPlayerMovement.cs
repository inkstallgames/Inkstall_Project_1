using Fusion;
using UnityEngine;

public class NetworkPlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    
    [Header("Client Prediction")]
    [Tooltip("Enable client-side prediction for instant local movement")]
    public bool enableClientPrediction = false; // Disabled to prevent rubber banding
    
    [Networked] public Vector3 AimDirection { get; set; }
    
    private CharacterController characterController;
    private PlayerCameraController cameraController;
    private Vector3 movement;
    
    // Client prediction variables
    private Vector3 _predictedPosition;
    private Quaternion _predictedRotation;
    private bool _isLocalPlayer;
    
    // Movement speed logging
    private Vector3 _lastPosition;
    private float _logTimer = 0f;
    private const float LOG_INTERVAL = 2f; // Log every 2 seconds

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
        }
        
        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            characterController = gameObject.AddComponent<CharacterController>();
        }
        
        // Initialize predicted values
        _predictedPosition = transform.position;
        _predictedRotation = transform.rotation;
        _lastPosition = transform.position;
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput<PlayerInputData>(out var input))
        {
            // SERVER-AUTHORITATIVE MOVEMENT
            // Server processes all movement, NetworkTransformInterpolation handles visual smoothing
            
            Vector3 positionBeforeMove = transform.position;
            
            // Movement - move relative to where player is looking
            if (input.movement.sqrMagnitude > 0.01f)
            {
                movement = (transform.forward * input.movement.y + transform.right * input.movement.x).normalized;
                characterController.Move(movement * moveSpeed * Runner.DeltaTime);
                
                // Log movement speed periodically
                _logTimer += Runner.DeltaTime;
                if (_logTimer >= LOG_INTERVAL)
                {
                    float actualSpeed = Vector3.Distance(positionBeforeMove, transform.position) / Runner.DeltaTime;
                    string playerType = Runner.IsServer ? "HOST" : "CLIENT";
                    string authority = _isLocalPlayer ? "LOCAL" : "REMOTE";
                    
                    Debug.Log($"[MOVEMENT SPEED] {playerType} ({authority}) | " +
                             $"Speed: {actualSpeed:F2} units/sec | " +
                             $"Expected: {moveSpeed:F2} | " +
                             $"DeltaTime: {Runner.DeltaTime:F4}s | " +
                             $"Tick: {Runner.Tick}");
                    
                    _logTimer = 0f;
                }
            }
            
            // Handle Shooting
            if (input.isShooting)
            {
                Shoot();
            }
            
            // Rotation - face where camera is looking
            if (input.aimDirection != Vector3.zero)
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
            
            // Reset log timer if not moving
            if (input.movement.sqrMagnitude <= 0.01f)
            {
                _logTimer = 0f;
            }
        }
    }
    
    // Render() removed - NetworkTransformInterpolation handles all visual smoothing
    // This prevents rubber banding while maintaining smooth 60 FPS visuals
    
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
