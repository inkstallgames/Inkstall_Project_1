using Fusion;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Networked bomb projectile — attach to the bomb prefab alongside
/// NetworkObject, Rigidbody, and a Collider.
///
/// Damage rules:
/// - Direct body hit: 50% of max health
/// - Splash (inside radius): up to 40% of max health at the explosion center,
///   linearly falling off to 0% at the radius edge
/// - Outside radius: no damage
///
/// Note: Players use CharacterController, which does not reliably fire
/// Rigidbody OnCollisionEnter. Body hits are detected via proximity checks.
/// </summary>
public class NetworkBombProjectile : NetworkBehaviour
{
    private const int MaxPlayerHealth = 100;

    [Header("Settings")]
    [SerializeField] private float lifetime = 5f;

    [Header("Area Damage")]
    [SerializeField] private float explosionRadius = 5f;
    [Tooltip("Damage when the grenade hits a player body (fraction of max health).")]
    [SerializeField] private float directHitDamagePercent = 0.50f;
    [Tooltip("Max splash at explosion center (fraction of max health). Falls off to 0 at radius edge.")]
    [SerializeField] private float maxSplashDamagePercent = 0.40f;
    [SerializeField] private LayerMask damageLayers = -1;

    [Header("Body Hit Detection")]
    [Tooltip("How close the grenade must be to a player collider to count as a direct body hit.")]
    [SerializeField] private float bodyHitRadius = 0.55f;

    [Header("Effects")]
    [SerializeField] private ParticleSystem hitEffect;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private float hitSoundVolume = 1.0f;

    [Networked] public PlayerRef SourcePlayer { get; set; }
    [Networked] public int Damage { get; set; }
    [Networked] private TickTimer LifetimeTimer { get; set; }

    private bool hasExploded;

    public void Initialize(PlayerRef source, int damage)
    {
        SourcePlayer = source;
        Damage = damage;
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
            LifetimeTimer = TickTimer.CreateFromSeconds(Runner, lifetime);

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || hasExploded) return;

        if (LifetimeTimer.Expired(Runner))
        {
            Explode(transform.position, null);
            return;
        }

        PlayerNetworkData directHit = FindDirectBodyHit();
        if (directHit != null)
            Explode(transform.position, directHit);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasExploded) return;
        if (!Object || !Object.HasStateAuthority) return;

        Vector3 explosionPos = collision.contactCount > 0
            ? collision.GetContact(0).point
            : transform.position;

        PlayerNetworkData directHit = collision.collider != null
            ? collision.collider.GetComponentInParent<PlayerNetworkData>()
            : null;

        if (directHit != null && directHit.Object != null && directHit.Object.InputAuthority == SourcePlayer)
            directHit = null;

        Explode(explosionPos, directHit);
    }

    private PlayerNetworkData FindDirectBodyHit()
    {
        PlayerNetworkData closest = null;
        float closestDist = float.MaxValue;

        foreach (var player in EnumeratePlayers())
        {
            if (!IsValidEnemyTarget(player)) continue;

            float dist = DistanceToPlayerBody(transform.position, player);
            if (dist <= bodyHitRadius && dist < closestDist)
            {
                closestDist = dist;
                closest = player;
            }
        }

        return closest;
    }

    private IEnumerable<PlayerNetworkData> EnumeratePlayers()
    {
        if (Runner == null) yield break;

        foreach (var playerRef in Runner.ActivePlayers)
        {
            var obj = Runner.GetPlayerObject(playerRef);
            if (obj == null) continue;
            var data = obj.GetComponent<PlayerNetworkData>();
            if (data != null)
                yield return data;
        }
    }

    private bool IsValidEnemyTarget(PlayerNetworkData player)
    {
        if (player == null || player.Object == null) return false;
        if (player.Object.InputAuthority == SourcePlayer) return false;
        if (player.Health <= 0) return false;
        return true;
    }

    private static float DistanceToPlayerBody(Vector3 point, PlayerNetworkData player)
    {
        var cc = player.GetComponent<CharacterController>();
        if (cc != null)
        {
            Vector3 center = player.transform.TransformPoint(cc.center);
            float halfHeight = Mathf.Max(0f, (cc.height * 0.5f) - cc.radius);
            Vector3 top = center + Vector3.up * halfHeight;
            Vector3 bottom = center - Vector3.up * halfHeight;
            Vector3 closestOnSegment = ClosestPointOnSegment(point, bottom, top);
            return Mathf.Max(0f, Vector3.Distance(point, closestOnSegment) - cc.radius);
        }

        Vector3 bodyPoint = player.transform.position + Vector3.up * 1.0f;
        return Vector3.Distance(point, bodyPoint);
    }

    private static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float lengthSq = ab.sqrMagnitude;
        if (lengthSq < 0.0001f) return a;
        float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSq);
        return a + ab * t;
    }

    private void Explode(Vector3 explosionPos, PlayerNetworkData directHitPlayer)
    {
        if (hasExploded) return;
        hasExploded = true;

        ApplyAreaDamage(explosionPos, directHitPlayer);
        PlayHitEffects(explosionPos);

        if (Object != null && Object.IsValid)
            Runner.Despawn(Object);
    }

    private void ApplyAreaDamage(Vector3 explosionPos, PlayerNetworkData directHitPlayer)
    {
        var damagedPlayers = new HashSet<PlayerNetworkData>();

        if (directHitPlayer != null)
            ApplyDamageToPlayer(directHitPlayer, GetDirectHitDamage(), damagedPlayers);

        foreach (var playerData in EnumeratePlayers())
        {
            if (playerData == null || damagedPlayers.Contains(playerData))
                continue;

            if (playerData == directHitPlayer)
                continue;

            if (playerData.Object == null || playerData.Health <= 0)
                continue;

            float distance = DistanceToPlayerBody(explosionPos, playerData);
            if (distance > explosionRadius)
                continue;

            int splashDamage = GetSplashDamage(distance);
            if (splashDamage <= 0)
                continue;

            ApplyDamageToPlayer(playerData, splashDamage, damagedPlayers);
        }
    }

    private void ApplyDamageToPlayer(PlayerNetworkData playerData, int damage, HashSet<PlayerNetworkData> damagedPlayers)
    {
        if (playerData == null || playerData.Object == null || damage <= 0)
            return;

        if (!damagedPlayers.Add(playerData))
            return;

        playerData.RPC_TakeDamage(damage, SourcePlayer, false, "Bomb");
    }

    private int GetDirectHitDamage()
    {
        return Mathf.RoundToInt(MaxPlayerHealth * directHitDamagePercent);
    }

    private int GetSplashDamage(float distance)
    {
        if (explosionRadius <= 0f || distance >= explosionRadius)
            return 0;

        float t = 1f - (distance / explosionRadius);
        return Mathf.RoundToInt(MaxPlayerHealth * maxSplashDamagePercent * t);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (runner.IsServer) return;
        if (!hasExploded)
            PlayHitEffects(transform.position);
    }

    private void PlayHitEffects(Vector3 position)
    {
        if (hitEffect != null)
            Instantiate(hitEffect, position, Quaternion.identity);

        if (hitSound != null)
        {
            if (NetworkAudioManager.Instance != null)
                NetworkAudioManager.Instance.PlaySound(hitSound, position, hitSoundVolume, false);
            else
                AudioSource.PlayClipAtPoint(hitSound, position, hitSoundVolume);
        }
    }
}
