using Fusion;
using UnityEngine;

public class NetworkPlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float maxHealth = 100f;
    
    [Networked] public float CurrentHealth { get; set; }
    [Networked] public Vector2 AimDirection { get; set; }
    [Networked] public bool IsDead { get; set; }
    
    private Rigidbody2D rb;
    private Vector2 movement;
    private Vector2 mousePosition;
    private Camera mainCamera;
    private PlayerNetworkData networkData;

    public override void Spawned()
    {
        if (Object.HasInputAuthority)
        {
            mainCamera = Camera.main;
            networkData = GetComponent<PlayerNetworkData>();
            CurrentHealth = maxHealth;
            IsDead = false;
        }
        
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            rb.freezeRotation = true;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput<PlayerInputData>(out var input) && !IsDead)
        {
            // Movement
            movement = input.movement.normalized;
            rb.velocity = movement * moveSpeed;
            
            // Rotation (aiming)
            if (input.aimDirection != Vector2.zero)
            {
                AimDirection = input.aimDirection;
                float angle = Mathf.Atan2(AimDirection.y, AimDirection.x) * Mathf.Rad2Deg - 90f;
                transform.rotation = Quaternion.Lerp(transform.rotation, 
                    Quaternion.Euler(0, 0, angle), 
                    rotationSpeed * Runner.DeltaTime);
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
        rb.velocity = Vector2.zero;
        // Handle player death (respawn, score, etc.)
        // This should be handled by the GameManager
    }
}
