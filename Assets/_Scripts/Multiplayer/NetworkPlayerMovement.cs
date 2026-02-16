using Fusion;
using UnityEngine;

public class NetworkPlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float maxHealth = 100f;
    
    [Networked] public float CurrentHealth { get; set; }
    [Networked] public Vector3 AimDirection { get; set; }
    [Networked] public bool IsDead { get; set; }
    
    private CharacterController characterController;
    private PlayerCameraController cameraController;
    private Vector3 movement;

    public override void Spawned()
    {
        if (Object.HasInputAuthority)
        {
            cameraController = GetComponent<PlayerCameraController>();
            CurrentHealth = maxHealth;
            IsDead = false;
        }
        
        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            characterController = gameObject.AddComponent<CharacterController>();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput<PlayerInputData>(out var input) && !IsDead)
        {
            // Movement - move relative to where player is looking
            if (input.movement.sqrMagnitude > 0.01f)
            {
                // Get movement direction based on player's current forward/right
                Vector3 forward = transform.forward;
                Vector3 right = transform.right;
                
                movement = (forward * input.movement.y + right * input.movement.x).normalized;
                characterController.Move(movement * moveSpeed * Runner.DeltaTime);
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
    }
    
    public void TakeDamage(float damage)
    {
        if (IsDead) return;
        
        CurrentHealth -= damage;
        
        if (CurrentHealth <= 0)
        {
            Die();
        }
    }
    
    private void Die()
    {
        IsDead = true;
        if (characterController != null)
        {
            characterController.Move(Vector3.zero);
        }
        // Handle player death (respawn, score, etc.)
        // This should be handled by the GameManager
    }
}
