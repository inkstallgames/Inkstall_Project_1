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
        Debug.Log("[NetworkBombBehaviour] RequestThrow() called — throw queued.");
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
    /// into the PlayerInputData struct.
    /// </summary>
    public void CollectInput(ref PlayerInputData inputData)
    {
        inputData.isThrowingBomb = wantsToThrow;
        inputData.throwDirection = (targetPoint - (throwPoint != null ? throwPoint.position : transform.position)).normalized;
        if (wantsToThrow)
        {
            Debug.Log($"[NetworkBombBehaviour] CollectInput — sending throw input. Direction: {inputData.throwDirection}");
        }
        wantsToThrow = false; // consumed
    }

    /// <summary>
    /// Call this from ThirdPersonController.OnInput to pack bomb-throw data
    /// into the NetworkInputData struct (used by the actual Fusion input pipeline).
    /// </summary>
    public void CollectNetworkInput(ref StarterAssets.NetworkInputData inputData)
    {
        inputData.isThrowingBomb = wantsToThrow;
        inputData.throwDirection = (targetPoint - (throwPoint != null ? throwPoint.position : transform.position)).normalized;
        if (wantsToThrow)
        {
            Debug.Log($"[NetworkBombBehaviour] CollectNetworkInput — sending throw input. Direction: {inputData.throwDirection}");
        }
        wantsToThrow = false; // consumed
    }

    // ---------------------------------------------------------------
    // Networked simulation (runs on server / state authority)
    // ---------------------------------------------------------------

    public override void FixedUpdateNetwork()
    {
        if (!GetInput<StarterAssets.NetworkInputData>(out var input))
        {
            return;
        }

        if (input.isThrowingBomb)
        {
            Debug.Log($"[NetworkBombBehaviour] FixedUpdateNetwork — throw input received on {(Object.HasStateAuthority ? "SERVER" : "CLIENT")}. Bombs: {CurrentBombs}");
            TryThrow(input.throwDirection);
        }
    }

    private void TryThrow(Vector3 direction)
    {
        // Only the server actually spawns
        if (!Object.HasStateAuthority)
        {
            Debug.Log("[NetworkBombBehaviour] TryThrow — skipped, not state authority.");
            return;
        }

        // Cooldown check
        if (!ThrowCooldownTimer.ExpiredOrNotRunning(Runner))
        {
            Debug.Log("[NetworkBombBehaviour] TryThrow — skipped, cooldown active.");
            return;
        }

        // Ammo check
        if (CurrentBombs <= 0)
        {
            Debug.Log($"[NetworkBombBehaviour] TryThrow — FAILED, player {Object.InputAuthority} has no bombs left.");
            return;
        }

        // Consume ammo & start cooldown
        CurrentBombs--;
        ThrowCooldownTimer = TickTimer.CreateFromSeconds(Runner, throwCooldown);
        Debug.Log($"[NetworkBombBehaviour] TryThrow — ammo consumed. Bombs remaining: {CurrentBombs}/{MaxBombs}");

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
            Debug.Log($"[NetworkBombBehaviour] BOMB SPAWNED successfully at {spawnPos}. Direction: {direction}, Force: {throwForce}");

            // Scale
            bomb.transform.localScale *= ballScale;

            // Set source player on the projectile
            var projectile = bomb.GetComponent<NetworkBombProjectile>();
            if (projectile != null)
            {
                projectile.Initialize(Object.InputAuthority, bombDamage);
            }

            // Ignore collisions between bomb and the throwing player
            var bombColliders = bomb.GetComponentsInChildren<Collider>();
            var playerColliders = GetComponentsInChildren<Collider>();
            foreach (var bc in bombColliders)
            {
                foreach (var pc in playerColliders)
                {
                    Physics.IgnoreCollision(bc, pc);
                }
            }

            // Apply physics velocity directly (more reliable than AddForce with NetworkTransform)
            var rb = bomb.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                Vector3 velocity = CalculateThrowVelocity(spawnPos, direction);
                rb.velocity = velocity;
                Debug.Log($"[NetworkBombBehaviour] Velocity set on bomb: {velocity} (magnitude: {velocity.magnitude})");
            }
            else
            {
                Debug.LogError("[NetworkBombBehaviour] BOMB MISSING RIGIDBODY — no velocity applied!");
            }

            // Notify all clients about the throw for effects
            RPC_OnBombThrown(spawnPos);
        }
        else
        {
            Debug.LogError($"[NetworkBombBehaviour] BOMB SPAWN FAILED — Runner.Spawn returned null! Prefab: {chemicalBallPrefab}");
        }
    }

    private Vector3 CalculateThrowVelocity(Vector3 startPoint, Vector3 direction)
    {
        // Add an upward arc to create a projectile trajectory
        Vector3 dir = direction.normalized;
        dir.y += 0.3f;  // upward arc for visible projectile motion
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

