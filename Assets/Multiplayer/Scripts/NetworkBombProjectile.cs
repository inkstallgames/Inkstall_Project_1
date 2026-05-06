using Fusion;
using UnityEngine;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Networked bomb projectile — attach to the bomb prefab alongside
/// NetworkObject, Rigidbody, and a Collider.
/// Handles collision detection, player damage, effects, and auto-despawn.
/// </summary>
public class NetworkBombProjectile : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private float lifetime = 5f;           // Auto-despawn after this many seconds

    [Header("Area Damage")]
    [SerializeField] private float explosionRadius = 5f;       // Maximum damage radius
    [SerializeField] private float maxDamagePercent = 0.5f;   // Maximum damage percentage (50% = full damage at center)
    [SerializeField] private LayerMask damageLayers = -1;      // Layers that can take damage

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

        // Get explosion position
        Vector3 explosionPos = collision.contacts[0].point;

        // Apply area damage with small delay to allow thrower to move away
        StartCoroutine(DelayedAreaDamage(explosionPos));

        // Play effects locally on server immediately
        PlayHitEffects(explosionPos);

        // Despawn bomb — clients will play effects in Despawned()
        Runner.Despawn(Object);
    }

    /// <summary>
    /// Apply area damage after small delay to allow thrower to move away
    /// </summary>
    private IEnumerator DelayedAreaDamage(Vector3 explosionPos)
    {
        yield return new WaitForSeconds(0.1f); // Small delay
        ApplyAreaDamage(explosionPos);
    }

    /// <summary>
    /// Apply area damage to all players within explosion radius
    /// </summary>
    private void ApplyAreaDamage(Vector3 explosionPos)
    {
        Debug.Log($"[NetworkBombProjectile] Area damage at {explosionPos}, radius: {explosionRadius}");

        // Find all colliders within explosion radius
        Collider[] hitColliders = Physics.OverlapSphere(explosionPos, explosionRadius, damageLayers);

        foreach (var collider in hitColliders)
        {
            // Check if we hit a player
            var playerData = collider.gameObject.GetComponentInParent<PlayerNetworkData>();

            if (playerData != null)
            {
                // Calculate distance from explosion center
                float distance = Vector3.Distance(explosionPos, playerData.transform.position);

                // Check if this is a direct body hit (very close to explosion center)
                bool isDirectHit = distance <= 1.0f; // Within 1 meter = direct hit
                int finalDamage;

                // Check if this is self-damage or enemy damage
                if (playerData.Object.InputAuthority == SourcePlayer)
                {
                    // Self-damage: 25 damage for very close, then distance-based
                    if (distance <= 1.0f)
                    {
                        finalDamage = 25; // Very close: Fixed 25 damage
                        Debug.Log($"[NetworkBombProjectile] SELF-DAMAGE - Very close ({distance:F1}m) -> 25 damage");
                    }
                    else
                    {
                        // Random damage based on distance for medium and far ranges
                        float selfDamagePercent = CalculateDamagePercent(distance);
                        float maxPossibleDamage = Damage * selfDamagePercent;
                        float minPossibleDamage = maxPossibleDamage * 0.3f; // Minimum 30% of max
                        
                        // Random damage between min and max
                        finalDamage = Mathf.RoundToInt(Random.Range(minPossibleDamage, maxPossibleDamage));
                        
                        Debug.Log($"[NetworkBombProjectile] SELF-DAMAGE - Distance {distance:F1}m -> Random {finalDamage} damage (Range: {minPossibleDamage:F0}-{maxPossibleDamage:F0})");
                    }
                }
                else
                {
                    // Enemy damage: 50 for direct hit, then random distance-based
                    if (distance <= 1.0f)
                    {
                        finalDamage = 50; // Direct hit: Fixed 50 damage
                        Debug.Log($"[NetworkBombProjectile] ENEMY DIRECT HIT - Very close ({distance:F1}m) -> 50 damage");
                    }
                    else
                    {
                        // Random damage based on distance for medium and far ranges
                        float enemyDamagePercent = CalculateDamagePercent(distance);
                        float maxPossibleDamage = Damage * enemyDamagePercent;
                        float minPossibleDamage = maxPossibleDamage * 0.3f; // Minimum 30% of max
                        
                        // Random damage between min and max
                        finalDamage = Mathf.RoundToInt(Random.Range(minPossibleDamage, maxPossibleDamage));
                        
                        Debug.Log($"[NetworkBombProjectile] ENEMY AREA DAMAGE - Distance {distance:F1}m -> Random {finalDamage} damage (Range: {minPossibleDamage:F0}-{maxPossibleDamage:F0})");
                    }
                }

                // Check if this is self-damage
                bool isSelfDamage = playerData.Object.InputAuthority == SourcePlayer;
                string damageType = isSelfDamage ? "[SELF-DAMAGE]" : "[ENEMY DAMAGE]";
                Debug.Log($"[NetworkBombProjectile] Player {playerData.Object.InputAuthority} takes {finalDamage} damage {damageType}");

                // Apply damage using existing RPC on PlayerNetworkData
                playerData.RPC_TakeDamage(finalDamage, SourcePlayer);
            }
        }
    }

    /// <summary>
    /// Calculate damage percentage based on distance from explosion center
    /// </summary>
    private float CalculateDamagePercent(float distance)
    {
        if (distance <= 0f) return maxDamagePercent; // At center = max damage

        // Linear falloff from center to edge
        float normalizedDistance = distance / explosionRadius;
        float damagePercent = maxDamagePercent * (1f - normalizedDistance);

        // Clamp between 0 and maxDamagePercent
        return Mathf.Clamp(damagePercent, 0f, maxDamagePercent);
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
    
        
        
    /// <summary>
    /// Draw area damage zones with colors for debugging
    /// </summary>
    private void OnDrawGizmos()
    {
        if (Selection.Contains(gameObject))
        {
            Vector3 explosionPos = transform.position;
            
            // Draw damage zones with different colors
            DrawDamageZone(explosionPos, 0f, 1f, Color.red, "Direct Hit (100%)");           // Red: Direct hit zone
            DrawDamageZone(explosionPos, 1f, 2f, new Color(1f, 0.3f, 0f, 0.5f), "High Damage (80%)");   // Orange-red: High damage
            DrawDamageZone(explosionPos, 2f, 3f, new Color(1f, 0.6f, 0f, 0.5f), "Medium Damage (60%)"); // Orange: Medium damage
            DrawDamageZone(explosionPos, 3f, 4f, new Color(1f, 0.8f, 0f, 0.5f), "Low Damage (40%)");    // Yellow-orange: Low damage
            DrawDamageZone(explosionPos, 4f, 5f, new Color(0.5f, 0.5f, 0f, 0.5f), "Very Low Damage (20%)"); // Green-yellow: Very low damage
            
            // Draw explosion radius border
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(explosionPos, explosionRadius);
            
            // Labels
            string info = $"Explosion Radius: {explosionRadius}m\nDirect Hit: 1.0m (100%)\nMax Damage: {maxDamagePercent:P0}";
            UnityEditor.Handles.Label(explosionPos + Vector3.up * 0.1f, info);
        }
    }
    
    /// <summary>
    /// Draw a colored damage zone ring with detailed damage info
    /// </summary>
    private void DrawDamageZone(Vector3 center, float innerRadius, float outerRadius, Color color, string label)
    {
        // Draw filled sphere with transparency
        Gizmos.color = new Color(color.r, color.g, color.b, 0.2f); // Semi-transparent
        Gizmos.DrawSphere(center, outerRadius);
        
        // Draw wireframe for inner radius
        Gizmos.color = color;
        Gizmos.DrawWireSphere(center, innerRadius);
        Gizmos.DrawWireSphere(center, outerRadius);
        
        // Calculate damage for this zone
        float avgDistance = (innerRadius + outerRadius) / 2f;
        float damagePercent = CalculateDamagePercent(avgDistance);
        int selfDamage = Mathf.RoundToInt(Damage * damagePercent);
        int enemyDirectDamage = Mathf.RoundToInt(100 * 0.5f); // 50% of 100HP = 50
        
        // Create detailed label with distance and damage info
        string detailedLabel = $"{label}\nDistance: {innerRadius:F1}m-{outerRadius:F1}m\nSelf-Damage: -{selfDamage}\nEnemy Direct: -{enemyDirectDamage}";
        
        // Draw label
        Vector3 labelPos = center + Vector3.up * (innerRadius + 0.3f);
        UnityEditor.Handles.Label(labelPos, detailedLabel);
    }
}
