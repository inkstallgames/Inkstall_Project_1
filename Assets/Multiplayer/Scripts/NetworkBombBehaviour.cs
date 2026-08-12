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
    [SerializeField] private int startingBombs = 3;             // Bombs each player starts with
    [SerializeField] private int maxBombCapacity = 5;           // Hard cap on carried grenades
    [SerializeField] private int mysteryBoxGrenades = 5;        // Grenades granted by mystery box (capped by max)

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
    [Networked] public NetworkBool HasGrenadeAmmoPowerup { get; set; }

    // --- Local-only state ---
    private Camera playerCamera;
    private Vector3 targetPoint;
    private bool wantsToThrow;
    private NetworkWeaponEquipSystem equipSystem;
    
    // Client-side prediction for instant throwing
    private bool hasPredictedThrow;
    private Vector3 predictedThrowDirection;
    private float predictedThrowTime;
    private GameObject _currentPredictedGrenade;
    // Host already threw on click — skip duplicate spawn in FixedUpdateNetwork
    private bool throwHandledOnClick;

    // --- Public accessors ---
    public int BombDamage => bombDamage;

    // ---------------------------------------------------------------
    // Lifecycle
    // ---------------------------------------------------------------

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            MaxBombs = maxBombCapacity;
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
    /// Throws on the click frame (host) or predicts instantly (client). Cooldown still applies.
    /// </summary>
    public void RequestThrow()
    {
        if (Object == null || !Object.HasInputAuthority) return;

        // Aim must be fresh on the click frame (don't wait for Update)
        if (playerCamera == null) playerCamera = Camera.main;
        UpdateTargetPoint();

        if (!CanThrowNow()) return;

        Vector3 direction = GetThrowDirection();

        // Instant visual on the click — never wait for the next simulation tick
        if (!hasPredictedThrow)
        {
            PredictThrow(direction);
        }

        // Host / state authority: spawn the real bomb on this click (cooldown still enforced in TryThrow)
        if (Object.HasStateAuthority)
        {
            TryThrow(direction);
            wantsToThrow = false;
            throwHandledOnClick = true;
            return;
        }

        // Client: send throw through Fusion input for the server to confirm
        wantsToThrow = true;
    }

    private bool CanThrowNow()
    {
        if (Runner == null) return false;
        if (CurrentBombs <= 0) return false;
        if (!ThrowCooldownTimer.ExpiredOrNotRunning(Runner)) return false;
        return chemicalBallPrefab != null;
    }

    private Vector3 GetThrowDirection()
    {
        Vector3 origin = throwPoint != null ? throwPoint.position : transform.position + Vector3.up;
        Vector3 dir = (targetPoint - origin);
        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = playerCamera != null ? playerCamera.transform.forward : transform.forward;
        }
        return dir.normalized;
    }
    
    /// <summary>
    /// Predict grenade throwing locally for instant feedback.
    /// </summary>
    private void PredictThrow(Vector3 direction)
    {
        if (!CanThrowNow()) return;
        
        predictedThrowDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : GetThrowDirection();
        predictedThrowTime = Time.time;
        
        if (!CreatePredictedGrenade()) return;

        hasPredictedThrow = true;
        PlayThrowEffectsLocal();
    }
    
    /// <summary>
    /// Create a local predicted grenade for instant visual feedback.
    /// </summary>
    private bool CreatePredictedGrenade()
    {
        if (chemicalBallPrefab == null) return false;

        Vector3 spawnPos = throwPoint != null ? throwPoint.position : transform.position + Vector3.up;
        
        GameObject predictedGrenade = Instantiate(chemicalBallPrefab.gameObject, spawnPos, Quaternion.identity);
        predictedGrenade.name = "PredictedGrenade";

        // Strip Fusion components so the clone can move with plain physics this frame
        foreach (var behaviour in predictedGrenade.GetComponentsInChildren<NetworkBehaviour>(true))
        {
            Destroy(behaviour);
        }
        foreach (var networkObject in predictedGrenade.GetComponentsInChildren<NetworkObject>(true))
        {
            Destroy(networkObject);
        }
        
        var rb = predictedGrenade.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = predictedGrenade.AddComponent<Rigidbody>();
        }
        rb.isKinematic = false;
        rb.detectCollisions = true;
        rb.useGravity = true;
        rb.mass = 0.5f;
        rb.velocity = CalculateThrowVelocity(spawnPos, predictedThrowDirection);
        
        predictedGrenade.transform.localScale = Vector3.one * ballScale;
        
        var collisionHandler = predictedGrenade.AddComponent<PredictedGrenadeCollision>();
        collisionHandler.Initialize(this);
        
        Destroy(predictedGrenade, 3f);
        
        _currentPredictedGrenade = predictedGrenade;
        return true;
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

        // Only raycast when the player is about to throw (keeps aim fresh without per-frame cost)
        if (wantsToThrow || hasPredictedThrow)
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
        if (throwHandledOnClick)
        {
            inputData.isThrowingBomb = false;
            throwHandledOnClick = false;
            wantsToThrow = false;
            return;
        }

        inputData.isThrowingBomb = wantsToThrow;
        inputData.throwDirection = GetThrowDirection();
        wantsToThrow = false; // consumed
    }

    /// <summary>
    /// Call this from ThirdPersonController.OnInput to pack bomb-throw data
    /// into the NetworkInputData struct (used by the actual Fusion input pipeline).
    /// </summary>
    public void CollectNetworkInput(ref StarterAssets.NetworkInputData inputData)
    {
        if (throwHandledOnClick)
        {
            inputData.isThrowingBomb = false;
            throwHandledOnClick = false;
            wantsToThrow = false;
            return;
        }

        inputData.isThrowingBomb = wantsToThrow;
        inputData.throwDirection = GetThrowDirection();
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

        // Check if bomb is equipped removed allowing instant throw.

        // Cooldown check
        if (!ThrowCooldownTimer.ExpiredOrNotRunning(Runner))
        {
            if (Object.HasInputAuthority) ClearPrediction();
            return;
        }

        // Ammo check
        if (CurrentBombs <= 0)
        {
            if (Object.HasInputAuthority) ClearPrediction();
            return;
        }

        // Consume ammo & start cooldown between throws
        CurrentBombs--;
        if (throwCooldown > 0f)
        {
            ThrowCooldownTimer = TickTimer.CreateFromSeconds(Runner, throwCooldown);
        }

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

            // Remove predicted visual after the real bomb exists (avoids a blank gap)
            if (Object.HasInputAuthority)
            {
                ClearPrediction();
            }
        }
        else
        {
            // Bomb spawn failed — drop prediction so player can retry after cooldown logic
            if (Object.HasInputAuthority) ClearPrediction();
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
        // Input authority already played effects via prediction on click
        if (Object.HasInputAuthority)
        {
            ClearPrediction();
            return;
        }

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
            
            // Clean up predicted grenade if it exists
            if (_currentPredictedGrenade != null)
            {
                Destroy(_currentPredictedGrenade);
                _currentPredictedGrenade = null;
            }
            
            // prediction cleared — no log (keeps Console clean in play)
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
    
    /// <summary>
    /// Mystery box grenade ammo: tries to grant <see cref="mysteryBoxGrenades"/>,
    /// but never exceeds <see cref="maxBombCapacity"/> (e.g. 3 + 5 → only +2 to reach 5).
    /// </summary>
    public void GrantGrenadeAmmoPowerup()
    {
        if (!Object.HasStateAuthority) return;

        MaxBombs = maxBombCapacity;
        CurrentBombs = Mathf.Min(CurrentBombs + mysteryBoxGrenades, MaxBombs);
        HasGrenadeAmmoPowerup = CurrentBombs >= MaxBombs;
    }

    /// <summary>True when the player is already at max grenade capacity.</summary>
    public bool IsGrenadeAmmoFull => CurrentBombs >= MaxBombs && MaxBombs > 0;
    
    /// <summary>
    /// Play visual explosion effect for predicted grenade (client-side only)
    /// </summary>
    public void PlayPredictedExplosionEffect(Vector3 position)
    {
        // Play explosion sound locally
        if (throwSound != null)
        {
            AudioSource.PlayClipAtPoint(throwSound, position, throwSoundVolume * 0.7f);
        }
        
        // Create simple explosion effect
        if (throwEffect != null)
        {
            // Instantiate effect at collision point
            var effect = Instantiate(throwEffect, position, Quaternion.identity);
            effect.Play();
            Destroy(effect.gameObject, 2f);
        }
    }
}

/// <summary>
/// Handles collision for predicted grenades to provide visual feedback
/// </summary>
public class PredictedGrenadeCollision : MonoBehaviour
{
    private NetworkBombBehaviour _bombBehaviour;
    private bool _hasExploded = false;
    
    public void Initialize(NetworkBombBehaviour bombBehaviour)
    {
        _bombBehaviour = bombBehaviour;
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (_hasExploded) return;
        _hasExploded = true;
        
        // Play visual explosion effect
        _bombBehaviour?.PlayPredictedExplosionEffect(transform.position);
        
        // Destroy the predicted grenade after collision
        Destroy(gameObject, 0.1f);
    }
}

