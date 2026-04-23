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
    // Collision (runs on server due to state authority)
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
                // Debug.Log($"[NetworkBombProjectile] Bomb from player {SourcePlayer} hit player {playerData.Object.InputAuthority}!");

                // Deal damage using the existing RPC on PlayerNetworkData
                playerData.RPC_TakeDamage(Damage, SourcePlayer);
            }
        }

        // Play effects locally on the server immediately
        Vector3 hitPos = collision.contacts[0].point;
        PlayHitEffects(hitPos);

        // Despawn the bomb — clients will play effects in Despawned()
        Runner.Despawn(Object);
    }

    // ---------------------------------------------------------------
    // Despawned — runs on ALL clients when the object is removed
    // ---------------------------------------------------------------

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        // Play effects at the bomb's last known position on clients
        // (server already played them in OnCollisionEnter)
        if (runner.IsServer) return; // Server already played in OnCollisionEnter

        PlayHitEffects(transform.position);
    }

    // ---------------------------------------------------------------
    // Effects helper
    // ---------------------------------------------------------------

    private void PlayHitEffects(Vector3 position)
    {
        // Spawn hit particle effect
        if (hitEffect != null)
        {
            Instantiate(hitEffect, position, Quaternion.identity);
        }

        // Industry-standard audio: 3D spatial sound for explosions
        if (hitSound != null)
        {
            if (NetworkAudioManager.Instance != null)
            {
                NetworkAudioManager.Instance.PlaySound(hitSound, position, hitSoundVolume, false);
            }
            else
            {
                // Fallback: 3D positioned sound
                AudioSource.PlayClipAtPoint(hitSound, position, hitSoundVolume);
            }
        }
    }
}
