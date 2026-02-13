using Fusion;
using UnityEngine;

/// <summary>
/// Networked bomb projectile — attach to the bomb prefab alongside
/// NetworkObject, Rigidbody, and a Collider.
/// Handles collision detection, player damage, effects, and auto-despawn.
/// </summary>
public class NetworkBombProjectile : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private float lifetime = 5f;           // Auto-despawn after this many seconds

    [Header("Effects")]
    [SerializeField] private ParticleSystem hitEffect;       // Particle spawned on impact
    [SerializeField] private AudioClip hitSound;             // Sound played on impact
    [SerializeField] private float hitSoundVolume = 1.0f;

    // --- Networked State ---
    [Networked] public PlayerRef SourcePlayer { get; set; }  // Who threw this bomb
    [Networked] public int Damage { get; set; }              // How much damage to deal
    [Networked] private TickTimer LifetimeTimer { get; set; }

    private bool hasCollided = false;

    // ---------------------------------------------------------------
    // Initialization (called by NetworkBombBehaviour after spawn)
    // ---------------------------------------------------------------

    /// <summary>
    /// Set the source player and damage. Called on the server right after spawning.
    /// </summary>
    public void Initialize(PlayerRef source, int damage)
    {
        SourcePlayer = source;
        Damage = damage;
    }

    public override void Spawned()
    {
        // Start the auto-despawn timer
        if (Object.HasStateAuthority)
        {
            LifetimeTimer = TickTimer.CreateFromSeconds(Runner, lifetime);
        }
    }

    // ---------------------------------------------------------------
    // Networked tick — auto-despawn check
    // ---------------------------------------------------------------

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        if (LifetimeTimer.Expired(Runner))
        {
            Runner.Despawn(Object);
        }
    }

    // ---------------------------------------------------------------
    // Collision (runs on server due to NetworkRigidbody / state authority)
    // ---------------------------------------------------------------

    private void OnCollisionEnter(Collision collision)
    {
        if (hasCollided) return;
        if (!Object.HasStateAuthority) return; // Only server processes hits

        hasCollided = true;

        // Check if we hit a player
        var playerData = collision.gameObject.GetComponentInParent<PlayerNetworkData>();

        if (playerData != null)
        {
            // Don't damage yourself
            if (playerData.Object.InputAuthority != SourcePlayer)
            {
                Debug.Log($"[NetworkBombProjectile] Bomb from player {SourcePlayer} hit player {playerData.Object.InputAuthority}!");

                // Deal damage using the existing RPC on PlayerNetworkData
                playerData.RPC_TakeDamage(Damage, SourcePlayer);
            }
        }

        // Play effects on all clients
        RPC_OnHit(collision.contacts[0].point);

        // Despawn the bomb
        Runner.Despawn(Object);
    }

    // ---------------------------------------------------------------
    // RPCs for visual/audio effects
    // ---------------------------------------------------------------

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnHit(Vector3 hitPosition)
    {
        // Spawn hit particle effect
        if (hitEffect != null)
        {
            Instantiate(hitEffect, hitPosition, Quaternion.identity);
        }

        // Play hit sound
        if (hitSound != null)
        {
            AudioSource.PlayClipAtPoint(hitSound, hitPosition, hitSoundVolume);
        }
    }
}
