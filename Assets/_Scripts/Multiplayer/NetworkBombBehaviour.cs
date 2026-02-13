using Fusion;
using UnityEngine;

/// <summary>
/// Multiplayer bomb throwing behaviour — attach to each player prefab.
/// Mirrors the offline ChemicalBombBehaviour but uses Photon Fusion networking.
/// The local player provides input; the server spawns the bomb projectile.
/// </summary>
public class NetworkBombBehaviour : NetworkBehaviour
{
    [Header("Throwing Settings")]
    [SerializeField] private Transform throwPoint;              // Point from which the bomb is thrown
    [SerializeField] private NetworkObject chemicalBallPrefab;  // Networked bomb prefab (must have NetworkBombProjectile)
    [SerializeField] private float throwForce = 15f;            // Force applied to the thrown bomb
    [SerializeField] private float throwCooldown = 1.0f;        // Cooldown between throws
    [SerializeField] private float maxThrowDistance = 50f;       // Maximum throw distance
    [SerializeField] private LayerMask hitLayers = -1;           // Layers the aiming ray can hit

    [Header("Ammo")]
    [SerializeField] private int startingBombs = 6;             // Bombs each player starts with

    [Header("Visibility")]
    [SerializeField] private float ballScale = 1.0f;
    [SerializeField] private Color ballColor = Color.green;

    [Header("Effects")]
    [SerializeField] private ParticleSystem throwEffect;
    [SerializeField] private AudioClip throwSound;
    [SerializeField] private float throwSoundVolume = 1.0f;

    [Header("Damage")]
    [SerializeField] private int bombDamage = 25;               // Damage dealt to other players on hit

    // --- Networked State ---
    [Networked] public int CurrentBombs { get; set; }
    [Networked] public int MaxBombs { get; set; }
    [Networked] private TickTimer ThrowCooldownTimer { get; set; }

    // --- Local-only state ---
    private Camera playerCamera;
    private Vector3 targetPoint;
    private bool wantsToThrow;

    // --- Public accessors ---
    public int BombDamage => bombDamage;

    // ---------------------------------------------------------------
    // Lifecycle
    // ---------------------------------------------------------------

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            MaxBombs = startingBombs;
            CurrentBombs = startingBombs;
        }

        if (Object.HasInputAuthority)
        {
            playerCamera = Camera.main;
        }
    }

    // ---------------------------------------------------------------
    // Input (local client only)
    // ---------------------------------------------------------------

    /// <summary>
    /// Call this from your UI throw button or input handler.
    /// Sets the throw flag that will be consumed in FixedUpdateNetwork.
    /// </summary>
    public void RequestThrow()
    {
        wantsToThrow = true;
    }

    private void Update()
    {
        if (!Object.HasInputAuthority) return;

        // Update aim target every frame for responsiveness
        UpdateTargetPoint();
    }

    private void UpdateTargetPoint()
    {
        if (playerCamera == null) return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, maxThrowDistance, hitLayers))
        {
            targetPoint = hit.point;
        }
        else
        {
            targetPoint = playerCamera.transform.position + playerCamera.transform.forward * maxThrowDistance;
        }
    }

    // ---------------------------------------------------------------
    // Fusion input pipeline
    // ---------------------------------------------------------------

    /// <summary>
    /// Call this from your OnInput callback to pack bomb-throw data
    /// into a NetworkBombInput struct. Example usage in your input provider:
    /// <code>
    ///   var bombBehaviour = localPlayer.GetComponent&lt;NetworkBombBehaviour&gt;();
    ///   if (bombBehaviour != null)
    ///       bombBehaviour.CollectInput(ref bombInput);
    ///   input.Set(bombInput);
    /// </code>
    /// </summary>
    public void CollectInput(ref NetworkBombInput bombInput)
    {
        bombInput.isThrowingBomb = wantsToThrow;
        bombInput.throwDirection = (targetPoint - (throwPoint != null ? throwPoint.position : transform.position)).normalized;
        wantsToThrow = false; // consumed
    }

    // ---------------------------------------------------------------
    // Networked simulation (runs on server / state authority)
    // ---------------------------------------------------------------

    public override void FixedUpdateNetwork()
    {
        if (!GetInput<NetworkBombInput>(out var input)) return;

        if (input.isThrowingBomb)
        {
            TryThrow(input.throwDirection);
        }
    }

    private void TryThrow(Vector3 direction)
    {
        // Only the server actually spawns
        if (!Object.HasStateAuthority) return;

        // Cooldown check
        if (!ThrowCooldownTimer.ExpiredOrNotRunning(Runner)) return;

        // Ammo check
        if (CurrentBombs <= 0)
        {
            Debug.Log($"[NetworkBombBehaviour] Player {Object.InputAuthority} has no bombs left.");
            return;
        }

        // Consume ammo & start cooldown
        CurrentBombs--;
        ThrowCooldownTimer = TickTimer.CreateFromSeconds(Runner, throwCooldown);

        // Determine spawn position
        Vector3 spawnPos = throwPoint != null ? throwPoint.position : transform.position + Vector3.up;

        // Spawn the networked bomb
        var bomb = Runner.Spawn(
            chemicalBallPrefab,
            spawnPos,
            Quaternion.identity,
            Object.InputAuthority  // input authority = the player who threw it
        );

        if (bomb != null)
        {
            // Scale
            bomb.transform.localScale *= ballScale;

            // Set source player on the projectile
            var projectile = bomb.GetComponent<NetworkBombProjectile>();
            if (projectile != null)
            {
                projectile.Initialize(Object.InputAuthority, bombDamage);
            }

            // Apply physics force
            var rb = bomb.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 velocity = CalculateThrowVelocity(spawnPos, direction);
                rb.AddForce(velocity, ForceMode.Impulse);
            }

            // Notify all clients about the throw for effects
            RPC_OnBombThrown(spawnPos);
        }
    }

    private Vector3 CalculateThrowVelocity(Vector3 startPoint, Vector3 direction)
    {
        // Add a slight upward arc to account for gravity
        float distance = direction.magnitude;
        Vector3 dir = direction.normalized;
        dir.y += distance * 0.01f;
        dir.Normalize();
        return dir * throwForce;
    }

    // ---------------------------------------------------------------
    // RPCs for visual/audio effects
    // ---------------------------------------------------------------

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnBombThrown(Vector3 position)
    {
        // Particle effect
        if (throwEffect != null)
        {
            throwEffect.Play();
        }

        // Sound effect
        if (throwSound != null)
        {
            AudioSource.PlayClipAtPoint(throwSound, position, throwSoundVolume);
        }
    }

    // ---------------------------------------------------------------
    // Public helpers
    // ---------------------------------------------------------------

    /// <summary>
    /// Add bombs (e.g. from a pickup or shop).
    /// Call on the server only.
    /// </summary>
    public void AddBombs(int amount)
    {
        if (!Object.HasStateAuthority) return;
        CurrentBombs = Mathf.Min(CurrentBombs + amount, MaxBombs);
    }
}

/// <summary>
/// Lightweight input struct for bomb throwing.
/// Register this alongside your existing PlayerInputData in your OnInput callback.
/// </summary>
public struct NetworkBombInput : INetworkInput
{
    public bool isThrowingBomb;
    public Vector3 throwDirection;
}
