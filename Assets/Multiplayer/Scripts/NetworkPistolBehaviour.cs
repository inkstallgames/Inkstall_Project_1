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
    private bool hasPredictedFx;
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
        hasPredictedFx = true;
        var fp = ActiveFirePoint;

        SpawnPooledMuzzleFlash(fp, predicted: true);

        if (shootSound != null && isLocalPlayer)
        {
            if (NetworkAudioManager.Instance != null)
                NetworkAudioManager.Instance.PlaySound(shootSound, fp != null ? fp.position : transform.position, soundVolume * 0.7f, true);
            else
                AudioSource.PlayClipAtPoint(shootSound, transform.position, soundVolume * 0.7f);
        }

        if (pistolRecoilAnimation != null)
        {
            pistolRecoilAnimation.TriggerPistolFire();
        }

        SpawnPredictedBulletTrail();

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
        Transform fp = ActiveFirePoint;

        // Local player already played muzzle/sound/recoil via prediction — skip the duplicate.
        bool skipLocalPredictedFx = isLocalPlayer && hasPredictedFx;
        if (skipLocalPredictedFx)
        {
            hasPredictedFx = false;
        }
        else
        {
            if (isLocalPlayer && pistolRecoilAnimation != null)
            {
                pistolRecoilAnimation.TriggerPistolFire();
            }

            SpawnPooledMuzzleFlash(fp, predicted: false);

            if (shootSound != null)
            {
                Vector3 soundPos = fp != null ? fp.position : origin;
                if (NetworkAudioManager.Instance != null)
                {
                    NetworkAudioManager.Instance.PlaySound(shootSound, soundPos, soundVolume, isLocalPlayer);
                }
                else if (isLocalPlayer)
                {
                    AudioSource.PlayClipAtPoint(shootSound, soundPos, soundVolume);
                }
                else
                {
                    AudioSource.PlayClipAtPoint(shootSound, origin, soundVolume);
                }
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
                GameObject playerHitEffect = CombatVfxPool.Get(
                    playerHitEffectPrefab,
                    hitPoint,
                    Quaternion.LookRotation(hitNormal));
                CombatVfxPool.Release(playerHitEffectPrefab, playerHitEffect, 2f);
            }
            else if (!hitPlayer && hitEffectPrefab != null)
            {
                GameObject hitEffect = CombatVfxPool.Get(
                    hitEffectPrefab,
                    hitPoint + hitNormal * 0.01f,
                    hitEffectPrefab.transform.rotation);
                CombatVfxPool.Release(hitEffectPrefab, hitEffect, 2f);
            }
        }
    }

    private void SpawnPooledMuzzleFlash(Transform fp, bool predicted)
    {
        if (muzzleFlashPrefab == null || fp == null) return;

        GameObject flash = CombatVfxPool.Get(muzzleFlashPrefab, fp.position, fp.rotation, fp);
        if (flash == null) return;

        var muzzleEffect = flash.GetComponent<MuzzleFlashEffect>();
        if (muzzleEffect != null)
            muzzleEffect.EmitBurst();

        CombatVfxPool.Release(muzzleFlashPrefab, flash, predicted ? 0.12f : 0.3f);
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

        GameObject trail = CombatVfxPool.Get(bulletTrailPrefab, startPosition, rotation);
        StartCoroutine(AnimateBulletTrail(trail, startPosition, endPosition));
    }

    /// <summary>
    /// Moves a short TrailRenderer streak from muzzle to impact.
    /// Trail history is kept brief so it reads as a tracer tip, not a static line.
    /// </summary>
    private IEnumerator AnimateBulletTrail(GameObject trailObject, Vector3 startPosition, Vector3 endPosition)
    {
        TrailRenderer trailRenderer = trailObject != null ? trailObject.GetComponent<TrailRenderer>() : null;
        float trailTime = bulletTrailLifetime;

        if (trailRenderer != null)
        {
            trailRenderer.emitting = false;
            trailRenderer.Clear();
            trailRenderer.time = trailTime;
            trailRenderer.widthMultiplier = Mathf.Max(0.04f, trailRenderer.widthMultiplier);
            trailRenderer.emitting = true;
        }

        if (trailObject != null)
            trailObject.transform.position = startPosition;

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
            if (trailRenderer != null)
            {
                trailRenderer.Clear();
            }
            CombatVfxPool.Release(bulletTrailPrefab, trailObject);
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
