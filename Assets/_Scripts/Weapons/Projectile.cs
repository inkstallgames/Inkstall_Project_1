using Fusion;
using UnityEngine;

public class Projectile : NetworkBehaviour
{
    [Header("Projectile Settings")]
    public float speed = 20f;
    public float damage = 10f;
    public float lifetime = 2f;
    public LayerMask collisionLayers;
    
    [Networked] private TickTimer life { get; set; }
    [Networked] private PlayerRef owner { get; set; }
    
    private Rigidbody2D rb;
    private Vector2 direction;
    
    public void Init(PlayerRef owner, Vector2 direction, float damage = 0f)
    {
        this.owner = owner;
        this.direction = direction.normalized;
        if (damage > 0) this.damage = damage;
        life = TickTimer.CreateFromSeconds(Runner, lifetime);
    }
    
    public override void Spawned()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            rb.isKinematic = true;
        }
        
        // Set initial velocity
        if (Object.HasStateAuthority && rb != null)
        {
            rb.velocity = direction * speed;
            transform.right = direction;
        }
    }
    
    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
        
        // Destroy projectile after lifetime expires
        if (life.Expired(Runner))
        {
            Runner.Despawn(Object);
            return;
        }
        
        // Check for collisions
        RaycastHit2D hit = Physics2D.Raycast(
            transform.position, 
            transform.right, 
            speed * Runner.DeltaTime, 
            collisionLayers
        );
        
        if (hit.collider != null)
        {
            OnHit(hit.collider);
        }
    }
    
    private void OnHit(Collider2D other)
    {
        // Check if we hit a player
        var playerNetworkData = other.GetComponent<PlayerNetworkData>();
        if (playerNetworkData != null)
        {
            // Make sure we are not hitting ourselves
            if (playerNetworkData.Object.InputAuthority == owner)
            {
                // Don't despawn if we hit our own player object, allow it to pass through
                return;
            }

            // Inflict damage by calling the RPC on the player's data component.
            playerNetworkData.RPC_TakeDamage((int)damage, owner);
        }

        // Despawn on any hit (environment, other players, etc.)
        // Ensure the object is still valid before despawning.
        if (Object != null && Object.IsValid)
        {
            Runner.Despawn(Object);
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!Object.HasStateAuthority) return;
        OnHit(other);
    }
}
