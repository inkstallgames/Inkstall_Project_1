using Fusion;
using UnityEngine;

public class NetworkPlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    
    [Networked] public Vector3 AimDirection { get; set; }
    [Networked] public Vector3 NetworkedPosition { get; set; }
    [Networked] public Quaternion NetworkedRotation { get; set; }
    
    private CharacterController characterController;
    private PlayerCameraController cameraController;
    private Vector3 movement;
    
    // Client-side smoothing
    private Vector3 smoothPosition;
    private Quaternion smoothRotation;
    private float positionLerpSpeed = 15f;
    private float rotationLerpSpeed = 20f;

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
        
        // Initialize networked properties
        NetworkedPosition = transform.position;
        NetworkedRotation = transform.rotation;
        smoothPosition = transform.position;
        smoothRotation = transform.rotation;
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
        }
        
        // Update networked properties for all players
        if (Runner.IsServer)
        {
            NetworkedPosition = transform.position;
            NetworkedRotation = transform.rotation;
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
            }
        }
    }
    
    public override void Render()
    {
        // Client-side smoothing for other players
        if (!Object.HasInputAuthority)
        {
            // Smooth position interpolation
            smoothPosition = Vector3.Lerp(smoothPosition, NetworkedPosition, 
                positionLerpSpeed * Time.deltaTime);
            transform.position = smoothPosition;
            
            // Smooth rotation interpolation
            smoothRotation = Quaternion.Slerp(smoothRotation, NetworkedRotation, 
                rotationLerpSpeed * Time.deltaTime);
            transform.rotation = smoothRotation;
        }
    }
}
