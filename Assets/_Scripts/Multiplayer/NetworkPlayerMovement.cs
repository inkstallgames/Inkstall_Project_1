using Fusion;
using UnityEngine;

public class NetworkPlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    
    [Networked] public Vector3 AimDirection { get; set; }
    
    private CharacterController characterController;
    private PlayerCameraController cameraController;
    private Vector3 movement;

    public override void Spawned()
    {
        // Always get camera controller reference for input authority
        if (Object.HasInputAuthority)
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
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput<PlayerInputData>(out var input))
        {
            // Movement - move relative to where player is looking
            if (input.movement.sqrMagnitude > 0.01f)
            {
                movement = (transform.forward * input.movement.y + transform.right * input.movement.x).normalized;
                characterController.Move(movement * moveSpeed * Runner.DeltaTime);
            }
            
            // Handle Shooting
            if (input.isShooting)
            {
                Shoot();
            }
            
            // Rotation - face where camera is looking
            // Use direct rotation for local player, server will sync to others
            if (input.aimDirection != Vector3.zero)
            {
                AimDirection = input.aimDirection;
                Vector3 lookDirection = AimDirection;
                lookDirection.y = 0; // Keep only horizontal rotation
                
                if (lookDirection.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                    
                    // For local player: use faster rotation for responsive feel
                    // For remote players: server handles the rotation
                    if (Object.HasInputAuthority)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, 
                            targetRotation, 
                            rotationSpeed * Runner.DeltaTime);
                    }
                    else
                    {
                        // Remote players get smoother rotation from server
                        transform.rotation = Quaternion.Slerp(transform.rotation, 
                            targetRotation, 
                            rotationSpeed * 0.5f * Runner.DeltaTime);
                    }
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
