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
    private NetworkWeaponEquipSystem equipSystem;
    
    // Client-side prediction for instant throwing
    private bool hasPredictedThrow;
    private Vector3 predictedThrowDirection;
    private float predictedThrowTime;

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

        equipSystem = GetComponent<NetworkWeaponEquipSystem>();
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
        
        // Client-side prediction for instant feedback
        if (Object.HasInputAuthority && !hasPredictedThrow)
        {
            PredictThrow();
        }
    }
    
    /// <summary>
    /// Predict grenade throwing locally for instant feedback (Among Us style)
    /// </summary>
    private void PredictThrow()
    {
        if (CurrentBombs <= 0) return;
        if (!ThrowCooldownTimer.ExpiredOrNotRunning(Runner)) return;
        
        // Store prediction data
        hasPredictedThrow = true;
        predictedThrowDirection = (targetPoint - (throwPoint != null ? throwPoint.position : transform.position)).normalized;
        predictedThrowTime = Time.time;
        
        // Create local predicted grenade
        CreatePredictedGrenade();
        
        // Play local effects instantly
        PlayThrowEffectsLocal();
    }
    
    /// <summary>
    /// Create a local predicted grenade for instant visual feedback
    /// </summary>
    private void CreatePredictedGrenade()
    {
        if (chemicalBallPrefab == null || throwPoint == null) return;
        
        // Instantiate local predicted grenade (not networked)
        GameObject predictedGrenade = Instantiate(chemicalBallPrefab.gameObject, throwPoint.position, Quaternion.identity);
        
        // Add simple Rigidbody for physics
        var rb = predictedGrenade.AddComponent<Rigidbody>();
        rb.velocity = predictedThrowDirection * throwForce;
        rb.useGravity = true;
        rb.mass = 0.5f;
        
        // Set visual properties
        var renderer = predictedGrenade.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = new Color(ballColor.r, ballColor.g, ballColor.b, 0.7f); // Semi-transparent
        }
        
        // Scale
        predictedGrenade.transform.localScale = Vector3.one * ballScale;
        
        // Auto-destroy after 2 seconds
        Destroy(predictedGrenade, 2f);
    }
    
    /// <summary>
    /// Play throw effects instantly on client
    /// </summary>
    private void PlayThrowEffectsLocal()
    {
        // Play throw sound locally
        if (throwSound != null)
        {
            AudioSource.PlayClipAtPoint(throwSound, throwPoint != null ? throwPoint.position : transform.position, throwSoundVolume);
        }
        
        // Play throw effect
        if (throwEffect != null)
        {
            throwEffect.Play();
        }
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
            TryThrow(input.throwDirection);
        }
    }

    private void TryThrow(Vector3 direction)
    {
        // Only the server actually spawns
        if (!Object.HasStateAuthority)
        {
            return;
        }
        
        // Clear client-side prediction when server processes the throw
        if (Object.HasInputAuthority)
        {
            ClearPrediction();
        }

        // Check if bomb is equipped removed allowing instant throw.

        // Cooldown check
        if (!ThrowCooldownTimer.ExpiredOrNotRunning(Runner))
        {
            // Clear prediction if cooldown failed
            if (Object.HasInputAuthority) ClearPrediction();
            return;
        }

        // Ammo check
        if (CurrentBombs <= 0)
        {
            // Clear prediction if no ammo
            if (Object.HasInputAuthority) ClearPrediction();
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
            }

            // Notify all clients about the throw for effects
            RPC_OnBombThrown(spawnPos);
        }
        else
        {
            // Bomb spawn failed
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

        // Industry-standard audio: 3D spatial sound for grenade throws
        if (throwSound != null)
        {
            if (NetworkAudioManager.Instance != null)
            {
                NetworkAudioManager.Instance.PlaySound(throwSound, position, throwSoundVolume, false);
            }
            else
            {
                // Fallback: 3D positioned sound
                AudioSource.PlayClipAtPoint(throwSound, position, throwSoundVolume);
            }
        }
    }

    /// <summary>
    /// Clear client-side prediction when server confirms the throw
    /// </summary>
    private void ClearPrediction()
    {
        if (hasPredictedThrow)
        {
            hasPredictedThrow = false;
            predictedThrowDirection = Vector3.zero;
            predictedThrowTime = 0f;
            
            Debug.Log("[NetworkBombBehaviour] Client prediction cleared");
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

