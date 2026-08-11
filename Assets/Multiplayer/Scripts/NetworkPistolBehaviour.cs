using System.Collections;
using Fusion;
using UnityEngine;

/// <summary>
/// Multiplayer pistol shooting behaviour using raycast.
/// Attach to each player prefab alongside NetworkBombBehaviour.
/// </summary>
public class NetworkPistolBehaviour : NetworkBehaviour
{
    [Header("Pistol Settings")]
    [Tooltip("Shoot point on the FPS arm model (used for local player VFX)")]
    [SerializeField] private Transform armFirePoint;
    
    [Tooltip("Shoot point on the full body model (used for remote player VFX)")]
    [SerializeField] private Transform bodyFirePoint;
    
    [SerializeField] private float fireRate = 0.2f;
    [SerializeField] private float range = 100f;
    [SerializeField] private int damage = 15;
    [SerializeField] private LayerMask hitLayers = -1;

    [Header("Ammo")]
    [SerializeField] private int maxAmmo = 20;
    [SerializeField] private int currentAmmo = 20;
    [SerializeField] private float reloadTime = 1.5f;
    [Tooltip("Unlimited ammo system - no reserve ammo needed")]

    [Header("Effects")]
    [SerializeField] private GameObject muzzleFlashPrefab; // Particle system muzzle flash
    [SerializeField] private GameObject bulletTrailPrefab;
    [Tooltip("This is not a particle system anymore. Instead a quad will spawn where the bullet hits.")]
    [SerializeField] private GameObject hitEffectPrefab;
    [Tooltip("Particle effect spawned at the hit point when a player is hit")]
    [SerializeField] private GameObject playerHitEffectPrefab;
    [SerializeField] private AudioClip shootSound;
    [SerializeField] private AudioClip reloadSound;
    [SerializeField] private float soundVolume = 1.0f;
    [Tooltip("Visual speed of the tracer tip (lower = more readable streak)")]
    [SerializeField] private float bulletTrailSpeed = 180f;
    [Tooltip("How long trail history lasts — short = streak, long = full line")]
    [SerializeField] private float bulletTrailLifetime = 0.07f;
    [Tooltip("Minimum time for the tip to travel so the streak is visible")]
    [SerializeField] private float bulletTrailMinTravelTime = 0.05f;
    [Tooltip("Cap travel time so long-range shots still feel snappy")]
    [SerializeField] private float bulletTrailMaxTravelTime = 0.28f;

    [Networked] public int CurrentAmmo { get; set; }
    [Networked] private TickTimer FireCooldownTimer { get; set; }
    [Networked] private TickTimer ReloadTimer { get; set; }
    [Networked] public bool IsReloading { get; set; }
    [Networked] public NetworkBool HasAutoFirePowerup { get; set; }
    
    public int MaxAmmo => maxAmmo;

    private Camera playerCamera;
    private Camera PlayerCamera
    {
        get
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }
            return playerCamera;
        }
    }
    
    // Client-side prediction for instant shooting
    private bool hasPredictedShot;
    private Vector3 predictedShotOrigin;
    private Vector3 predictedShotDirection;
    private float predictedShotTime;
    private bool hasPredictedTrail;
    private bool wantsToShoot;
    private bool wantsToReload;
    
    // Client-side prediction for instant reloading
    private bool hasPredictedReload;
    private float predictedReloadTime;
    private NetworkWeaponEquipSystem equipSystem;
    private PlayerNetworkData playerData;
    private bool isLocalPlayer => Object != null && Object.HasInputAuthority;
    private PistolRecoilAnimation pistolRecoilAnimation; // Reference to pistol recoil script

    /// <summary>
    /// Returns the correct fire point based on whether this is the local or remote player.
    /// Local player uses arm model shoot point, remote player uses full body shoot point.
    /// </summary>
    private Transform ActiveFirePoint
    {
        get
        {
            if (isLocalPlayer)
            {
                return armFirePoint;
            }
            else
            {
                return bodyFirePoint;
            }
        }
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            CurrentAmmo = currentAmmo;
            IsReloading = false;
        }

        equipSystem = GetComponent<NetworkWeaponEquipSystem>();
        playerData = GetComponent<PlayerNetworkData>();
        
        // Get reference to pistol recoil animation script
        pistolRecoilAnimation = GetComponentInChildren<PistolRecoilAnimation>();

        // Ensure muzzle flash prefab is disabled on start
        if (muzzleFlashPrefab != null)
        {
            muzzleFlashPrefab.SetActive(false);
        }
    }

    public void RequestShoot()
    {
        wantsToShoot = true;
        
        // Client-side prediction for instant shooting feedback
        if (Object.HasInputAuthority && !hasPredictedShot)
        {
            PredictShoot();
        }
    }
    
    /// <summary>
    /// Predict shooting locally for instant feedback (Among Us style)
    /// </summary>
    private void PredictShoot()
    {
        if (CurrentAmmo <= 0)
        {
            if (!IsReloading && !hasPredictedReload)
            {
                PredictReload();
            }
            return;
        }
        
        if (!FireCooldownTimer.ExpiredOrNotRunning(Runner)) return;
        if (IsReloading) return;
        
        // Store prediction data
        hasPredictedShot = true;
        predictedShotTime = Time.time;
        
        if (PlayerCamera != null)
        {
            predictedShotOrigin = PlayerCamera.transform.position;
            predictedShotDirection = PlayerCamera.transform.forward;
        }
        else
        {
            var fp = ActiveFirePoint;
            predictedShotOrigin = fp != null ? fp.position : transform.position;
            predictedShotDirection = transform.forward;
        }
        
        // Play instant shooting effects
        PlayPredictedShootEffects();
    }
    
    /// <summary>
    /// Play predicted shooting effects instantly
    /// </summary>
    private void PlayPredictedShootEffects()
    {
        // Instant muzzle flash
        var fp = ActiveFirePoint;
        if (muzzleFlashPrefab != null && fp != null)
        {
            GameObject tempMuzzleFlash = Instantiate(muzzleFlashPrefab, fp.position, fp.rotation);
            tempMuzzleFlash.transform.SetParent(fp); // Parent to fire point
            Destroy(tempMuzzleFlash, 0.1f); // Shorter duration for predicted effect
        }
        
        // Instant shooting sound
        if (shootSound != null && isLocalPlayer)
        {
            GameObject tempAudioObject = new GameObject("PredictedShootSound");
            AudioSource tempAudioSource = tempAudioObject.AddComponent<AudioSource>();
            
            tempAudioSource.clip = shootSound;
            tempAudioSource.volume = soundVolume * 0.7f; // Slightly quieter for predicted
            tempAudioSource.spatialBlend = 0f; // 2D sound
            tempAudioSource.playOnAwake = false;
            tempAudioSource.Play();
            
            Destroy(tempAudioObject, shootSound.length + 0.1f);
        }
        
        // Instant recoil animation
        if (pistolRecoilAnimation != null)
        {
            pistolRecoilAnimation.TriggerPistolFire();
        }

        // Instant tracer from the live muzzle so it stays glued to the gun while moving
        SpawnPredictedBulletTrail();
        
        // Predict ammo decrease ONLY for clients without state authority.
        // Host applies this in FixedUpdateNetwork to prevent double-consumption.
        if (!Object.HasStateAuthority)
        {
            CurrentAmmo--;
        }
    }

    /// <summary>
    /// Local-only tracer: raycasts from the camera and draws from the current muzzle.
    /// Avoids the late RPC trail that freezes at an old world position while you strafe.
    /// </summary>
    private void SpawnPredictedBulletTrail()
    {
        var fp = ActiveFirePoint;
        if (bulletTrailPrefab == null || fp == null || PlayerCamera == null)
        {
            return;
        }

        Vector3 origin = PlayerCamera.transform.position;
        Vector3 direction = PlayerCamera.transform.forward;
        Vector3 endPoint = origin + direction * range;
        if (Physics.Raycast(origin, direction, out RaycastHit hit, range, hitLayers))
        {
            endPoint = hit.point;
        }

        hasPredictedTrail = true;
        SpawnBulletTrailVfx(fp.position, endPoint);
    }

    public void RequestReload()
    {
        wantsToReload = true;
        
        // Client-side prediction for instant reload feedback
        if (Object.HasInputAuthority && !hasPredictedReload && !IsReloading)
        {
            PredictReload();
        }
    }
    
    /// <summary>
    /// Predict reloading locally for instant feedback
    /// </summary>
    private void PredictReload()
    {
        if (CurrentAmmo >= maxAmmo) return;
        if (IsReloading) return;
        
        // Store prediction data
        hasPredictedReload = true;
        predictedReloadTime = Time.time;
        
        // Play instant reload effects
        PlayPredictedReloadEffects();
    }
    
    /// <summary>
    /// Play predicted reload effects instantly
    /// </summary>
    private void PlayPredictedReloadEffects()
    {
        // Instant reload animation
        if (pistolRecoilAnimation != null)
        {
            pistolRecoilAnimation.TriggerReloadAnimation();
        }
        
        // Predict reload state ONLY for clients without state authority.
        // Host applies this in FixedUpdateNetwork to prevent skipping the reload timer.
        if (!Object.HasStateAuthority)
        {
            IsReloading = true;
        }
    }

    public void CollectNetworkInput(ref StarterAssets.NetworkInputData inputData)
    {
        inputData.isShooting = wantsToShoot;
        inputData.isReloading = wantsToReload;
        
        if (PlayerCamera != null)
        {
            inputData.aimDirection = PlayerCamera.transform.forward;
            inputData.aimOrigin = PlayerCamera.transform.position;
        }
        
        wantsToShoot = false;
        wantsToReload = false;
    }

    public override void FixedUpdateNetwork()
    {
        // Process reload completion first (independent of client input!)
        if (IsReloading && ReloadTimer.ExpiredOrNotRunning(Runner))
        {
            FinishReload();
        }

        if (!GetInput<StarterAssets.NetworkInputData>(out var input))
        {
            return;
        }

        // Process reload request
        if (input.isReloading && !IsReloading)
        {
            TryReload();
        }

        if (input.isShooting && !IsReloading)
        {
            TryShoot(input.aimOrigin, input.aimDirection);
        }
    }

    private void TryShoot(Vector3 origin, Vector3 direction)
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        if (equipSystem != null && !equipSystem.IsPistolEquipped())
        {
            // Clear prediction if pistol not equipped
            if (Object.HasInputAuthority) ClearShootPrediction();
            return;
        }

        // Additional safety check - prevent firing during reload
        if (IsReloading)
        {
            // Clear prediction if reloading
            if (Object.HasInputAuthority) ClearShootPrediction();
            return;
        }

        if (!FireCooldownTimer.ExpiredOrNotRunning(Runner))
        {
            // Clear prediction if on cooldown
            if (Object.HasInputAuthority) ClearShootPrediction();
            return;
        }

        if (CurrentAmmo <= 0)
        {
            // Clear prediction if no ammo
            if (Object.HasInputAuthority) ClearShootPrediction();
            
            // Auto reload when out of ammo and trying to shoot (unlimited ammo)
            if (!IsReloading)
            {
                TryReload();
            }
            
            return;
        }
        
        // Clear client-side prediction when server processes the shot
        if (Object.HasInputAuthority)
        {
            ClearShootPrediction();
        }

        CurrentAmmo--;
        FireCooldownTimer = TickTimer.CreateFromSeconds(Runner, fireRate);

        // Use the active fire point for VFX origin, fall back to camera origin
        Transform fp = ActiveFirePoint;
        Vector3 effectsOrigin = fp != null ? fp.position : origin;
        
        if (Physics.Raycast(origin, direction, out RaycastHit hit, range, hitLayers))
        {
            bool hitPlayer = false;
            var hitPlayerData = hit.collider.GetComponentInParent<PlayerNetworkData>();
            if (hitPlayerData != null)
            {
                if (hitPlayerData.Object.InputAuthority != Object.InputAuthority)
                {
                    hitPlayerData.RPC_TakeDamage(damage, Object.InputAuthority, false, "Pistol");
                    hitPlayer = true;
                }
            }
            else
            {
                var hitMysteryBox = hit.collider.GetComponentInParent<MysteryBox>();
                if (hitMysteryBox != null)
                {
                    hitMysteryBox.RPC_OnShot(Object.InputAuthority);
                }
            }

            RPC_OnShot(effectsOrigin, hit.point, true, hit.point, hit.normal, hitPlayer);
        }
        else
        {
            Vector3 endPoint = origin + direction * range;
            RPC_OnShot(effectsOrigin, endPoint, false, Vector3.zero, Vector3.zero, false);
        }
    }

    private void TryReload()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        if (IsReloading)
        {
            // Clear prediction if already reloading
            if (Object.HasInputAuthority) ClearReloadPrediction();
            return;
        }

        if (CurrentAmmo >= maxAmmo)
        {
            // Clear prediction if full ammo
            if (Object.HasInputAuthority) ClearReloadPrediction();
            return;
        }
        
        // Clear client-side prediction when server processes the reload
        if (Object.HasInputAuthority)
        {
            ClearReloadPrediction();
        }

        IsReloading = true;
        ReloadTimer = TickTimer.CreateFromSeconds(Runner, reloadTime);
        RPC_OnReloadStart();
    }

    private void FinishReload()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        // Unlimited ammo - always reload to full capacity
        CurrentAmmo = maxAmmo;
        IsReloading = false;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnShot(Vector3 origin, Vector3 endPoint, bool didHit, Vector3 hitPoint, Vector3 hitNormal, bool hitPlayer)
    {
        // Trigger pistol fire animation on FPS hands (only for local player)
        if (isLocalPlayer && pistolRecoilAnimation != null)
        {
            pistolRecoilAnimation.TriggerPistolFire();
        }
        
        // Get the correct fire point for this player's view
        Transform fp = ActiveFirePoint;
        
        // Muzzle flash at the correct shoot point
        if (muzzleFlashPrefab != null && fp != null)
        {
            GameObject tempMuzzleFlash = Instantiate(muzzleFlashPrefab, fp.position, fp.rotation);
            tempMuzzleFlash.transform.SetParent(fp);
            
            // CRITICAL: Ensure the GameObject is active!
            tempMuzzleFlash.SetActive(true);
            
            var muzzleEffect = tempMuzzleFlash.GetComponent<MuzzleFlashEffect>();
            if (muzzleEffect != null)
            {
                // Use EmitBurst() which calls ParticleSystem.Emit() directly.
                // This guarantees particles spawn on this exact frame and never
                // misses a trigger, unlike Play() which can fail on rapid calls.
                muzzleEffect.EmitBurst();
            }
            
            // Auto-destroy after effect finishes
            Destroy(tempMuzzleFlash, 0.3f);
        }

        if (shootSound != null)
        {
            if (isLocalPlayer)
            {
                // Local player: Use 2D sound for equal stereo balance (FPS hands)
                GameObject tempAudioObject = new GameObject("TempShootSound");
                AudioSource tempAudioSource = tempAudioObject.AddComponent<AudioSource>();
                
                // Configure for 2D sound (equal in both ears)
                tempAudioSource.clip = shootSound;
                tempAudioSource.volume = soundVolume;
                tempAudioSource.spatialBlend = 0f; // 0 = 2D sound, 1 = 3D sound
                tempAudioSource.playOnAwake = false;
                
                // Play the sound
                tempAudioSource.Play();
                
                // Destroy the temporary object after sound finishes
                Destroy(tempAudioObject, shootSound.length + 0.1f);
            }
            else
            {
                // Remote player: Use 3D positioned sound so others can hear where they're shooting from
                AudioSource.PlayClipAtPoint(shootSound, origin, soundVolume);
            }
        }

        // Bullet trail: remotes use shot-time origin from the RPC.
        // Local player already spawned a predicted tracer from the live muzzle.
        if (bulletTrailPrefab != null)
        {
            if (isLocalPlayer && hasPredictedTrail)
            {
                hasPredictedTrail = false;
            }
            else
            {
                // Prefer networked shot origin so the streak doesn't follow a moving body.
                Vector3 trailStart = origin != Vector3.zero
                    ? origin
                    : (fp != null ? fp.position : endPoint);

                SpawnBulletTrailVfx(trailStart, endPoint);
            }
        }

        if (didHit)
        {
            if (hitPlayer && playerHitEffectPrefab != null)
            {
                // Player hit: spawn player-hit particle effect at the impact point
                GameObject playerHitEffect = Instantiate(playerHitEffectPrefab, hitPoint, Quaternion.LookRotation(hitNormal));
                Destroy(playerHitEffect, 2f);
            }
            else if (!hitPlayer && hitEffectPrefab != null)
            {
                // Surface hit: spawn bullet hole decal (quad) slightly offset from the surface to prevent z-fighting
                GameObject hitEffect = Instantiate(hitEffectPrefab, hitPoint + hitNormal * 0.01f, hitEffectPrefab.transform.rotation);
                Destroy(hitEffect, 2f);
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnReloadStart()
    {
        // Trigger reload animation on FPS hands (only for local player)
        if (isLocalPlayer && pistolRecoilAnimation != null)
        {
            pistolRecoilAnimation.TriggerReloadAnimation();
        }

        // Play reload sound via PistolRecoilAnimation (it handles the sound)
        // The sound is played by the animation script, not here
    }

    /// <summary>
    /// Gets the current reload progress (0.0 to 1.0). Returns 0 if not reloading.
    /// </summary>
    public float GetReloadProgress(NetworkRunner runner)
    {
        if (!IsReloading || !ReloadTimer.IsRunning) return 0f;
        
        float remainingTime = ReloadTimer.RemainingTime(runner) ?? 0f;
        float progress = remainingTime / reloadTime;
        return Mathf.Clamp01(progress);
    }

    public void AddAmmo(int amount)
    {
        if (!Object.HasStateAuthority) return;
        // Unlimited ammo - just reload to full capacity
        CurrentAmmo = maxAmmo;
    }

    /// <summary>
    /// Resets pistol ammo to full (magazine + reserve) when player gets a kill.
    /// Reserve ammo is capped at 30 to prevent exceeding limit on multiple kills.
    /// Called by NetworkGameManager on kill.
    /// </summary>
    public void ResetAmmoOnKill()
    {
        if (!Object.HasStateAuthority) return;
        
        CurrentAmmo = maxAmmo;
        IsReloading = false;
    }

    /// <summary>
    /// Spawns a short traveling tracer (not a full-length line that pops in and fades).
    /// </summary>
    private void SpawnBulletTrailVfx(Vector3 startPosition, Vector3 endPosition)
    {
        if (bulletTrailPrefab == null)
        {
            return;
        }

        Vector3 direction = endPosition - startPosition;
        Quaternion rotation = direction.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(direction.normalized)
            : Quaternion.identity;

        GameObject trail = Instantiate(bulletTrailPrefab, startPosition, rotation);
        StartCoroutine(AnimateBulletTrail(trail, startPosition, endPosition));
    }

    /// <summary>
    /// Moves a short TrailRenderer streak from muzzle to impact.
    /// Trail history is kept brief so it reads as a tracer tip, not a static line.
    /// </summary>
    private IEnumerator AnimateBulletTrail(GameObject trailObject, Vector3 startPosition, Vector3 endPosition)
    {
        TrailRenderer trailRenderer = trailObject.GetComponent<TrailRenderer>();
        float trailTime = bulletTrailLifetime;

        if (trailRenderer != null)
        {
            trailRenderer.emitting = false;
            trailRenderer.Clear();
            trailRenderer.time = trailTime;
            // Prefer a tapered streak: thicker at the tip (newest), thinner at the tail.
            trailRenderer.widthMultiplier = Mathf.Max(0.04f, trailRenderer.widthMultiplier);
            trailRenderer.emitting = true;
        }

        trailObject.transform.position = startPosition;

        // One physics/render sample at the muzzle so the first vertex is anchored there,
        // without holding the tip still long enough to look like a stuck world line.
        yield return null;
        if (trailObject == null)
        {
            yield break;
        }

        float distance = Vector3.Distance(startPosition, endPosition);
        float duration = distance / Mathf.Max(1f, bulletTrailSpeed);
        duration = Mathf.Clamp(duration, bulletTrailMinTravelTime, bulletTrailMaxTravelTime);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (trailObject == null)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Ease out slightly so the tip spends a bit more time readable near impact.
            float eased = 1f - (1f - t) * (1f - t);
            trailObject.transform.position = Vector3.Lerp(startPosition, endPosition, eased);
            yield return null;
        }

        if (trailObject == null)
        {
            yield break;
        }

        trailObject.transform.position = endPosition;
        if (trailRenderer != null)
        {
            trailRenderer.emitting = false;
            trailTime = trailRenderer.time;
        }

        yield return new WaitForSeconds(trailTime);

        if (trailObject != null)
        {
            Destroy(trailObject);
        }
    }
    
    /// <summary>
    /// Clear client-side shooting prediction
    /// </summary>
    private void ClearShootPrediction()
    {
        if (hasPredictedShot)
        {
            hasPredictedShot = false;
            predictedShotOrigin = Vector3.zero;
            predictedShotDirection = Vector3.zero;
            predictedShotTime = 0f;
        }
    }
    
    /// <summary>
    /// Clear client-side reload prediction
    /// </summary>
    private void ClearReloadPrediction()
    {
        if (hasPredictedReload)
        {
            hasPredictedReload = false;
            predictedReloadTime = 0f;
        }
    }
}
