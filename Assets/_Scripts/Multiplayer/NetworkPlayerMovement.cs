using Fusion;
using UnityEngine;

public class NetworkPlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float gravity = -9.81f;
    public float groundCheckDistance = 0.2f;
    
    [Networked] public Vector3 AimDirection { get; set; }
    [Networked] public Vector3 Velocity { get; set; }
    
    private PlayerCameraController cameraController;
    private Vector3 movement;
    private bool isGrounded;

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
        
        // Ensure we have a collider for physics interactions
        var capsuleCollider = GetComponent<CapsuleCollider>();
        if (capsuleCollider == null)
        {
            capsuleCollider = gameObject.AddComponent<CapsuleCollider>();
            capsuleCollider.height = 2f;
            capsuleCollider.radius = 0.5f;
            capsuleCollider.center = new Vector3(0, 1f, 0);
        }
        
        // Ensure we have a Rigidbody for physics (kinematic for manual movement control)
        var rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true; // We control movement manually
        rb.useGravity = false; // We handle gravity manually
    }

    public override void FixedUpdateNetwork()
    {
        // Ground check
        isGrounded = Physics.Raycast(transform.position, Vector3.down, groundCheckDistance + 0.1f);
        
        if (GetInput<PlayerInputData>(out var input))
        {
            // Calculate velocity
            Vector3 velocity = Velocity;
            
            // Movement - move relative to where player is looking
            if (input.movement.sqrMagnitude > 0.01f)
            {
                movement = (transform.forward * input.movement.y + transform.right * input.movement.x).normalized;
                velocity.x = movement.x * moveSpeed;
                velocity.z = movement.z * moveSpeed;
            }
            else
            {
                velocity.x = 0;
                velocity.z = 0;
            }
            
            // Apply gravity
            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f; // Small downward force to keep grounded
            }
            else
            {
                velocity.y += gravity * Runner.DeltaTime;
            }
            
            // Store velocity in networked property
            Velocity = velocity;
            
            // Apply movement - this works with client prediction!
            transform.position += velocity * Runner.DeltaTime;
            
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
        else if (!Object.HasInputAuthority)
        {
            // For remote players without input, still apply velocity (from server)
            transform.position += Velocity * Runner.DeltaTime;
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
