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
    [SerializeField] private int maxAmmo = 30;
    [SerializeField] private int currentAmmo = 30;
    [SerializeField] private int reserveAmmo = 90;
    [SerializeField] private float reloadTime = 1.5f;

    [Header("Effects")]
    [SerializeField] private GameObject muzzleFlashPrefab; // Particle system muzzle flash
    [SerializeField] private GameObject bulletTrailPrefab;
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private AudioClip shootSound;
    [SerializeField] private AudioClip reloadSound;
    [SerializeField] private float soundVolume = 1.0f;

    [Networked] public int CurrentAmmo { get; set; }
    [Networked] public int ReserveAmmo { get; set; }
    [Networked] private TickTimer FireCooldownTimer { get; set; }
    [Networked] private TickTimer ReloadTimer { get; set; }
    [Networked] public bool IsReloading { get; set; }

    private Camera playerCamera;
    private bool wantsToShoot;
    private bool wantsToReload;
    private NetworkWeaponEquipSystem equipSystem;
    private PlayerNetworkData playerData;
    private bool isLocalPlayer;
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
            ReserveAmmo = reserveAmmo;
            IsReloading = false;
        }

        isLocalPlayer = Object.HasInputAuthority;

        if (isLocalPlayer)
        {
            playerCamera = Camera.main;
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
        
        // Log which fire points are assigned
        Debug.Log($"[NetworkPistolBehaviour] Spawned | Player: {(isLocalPlayer ? "LOCAL" : "REMOTE")} | ArmFirePoint: {(armFirePoint != null ? armFirePoint.name : "NULL")} | BodyFirePoint: {(bodyFirePoint != null ? bodyFirePoint.name : "NULL")}");
    }

    public void RequestShoot()
    {
        wantsToShoot = true;
    }

    public void RequestReload()
    {
        wantsToReload = true;
    }

    public void CollectNetworkInput(ref StarterAssets.NetworkInputData inputData)
    {
        inputData.isShooting = wantsToShoot;
        inputData.isReloading = wantsToReload;
        
        if (playerCamera != null)
        {
            inputData.aimDirection = playerCamera.transform.forward;
            inputData.aimOrigin = playerCamera.transform.position;
        }
        
        wantsToShoot = false;
        wantsToReload = false;
    }

    public override void FixedUpdateNetwork()
    {
        if (!GetInput<StarterAssets.NetworkInputData>(out var input))
        {
            return;
        }

        if (input.isReloading && !IsReloading)
        {
            TryReload();
        }

        if (input.isShooting && !IsReloading)
        {
            TryShoot(input.aimOrigin, input.aimDirection);
        }

        if (IsReloading && ReloadTimer.ExpiredOrNotRunning(Runner))
        {
            FinishReload();
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
            return;
        }

        if (!FireCooldownTimer.ExpiredOrNotRunning(Runner))
        {
            return;
        }

        if (CurrentAmmo <= 0)
        {
            return;
        }

        CurrentAmmo--;
        FireCooldownTimer = TickTimer.CreateFromSeconds(Runner, fireRate);

        // Use the active fire point for VFX origin, fall back to camera origin
        Transform fp = ActiveFirePoint;
        Vector3 effectsOrigin = fp != null ? fp.position : origin;
        
        if (Physics.Raycast(origin, direction, out RaycastHit hit, range, hitLayers))
        {
            var hitPlayerData = hit.collider.GetComponentInParent<PlayerNetworkData>();
            if (hitPlayerData != null)
            {
                if (hitPlayerData.Object.InputAuthority != Object.InputAuthority)
                {
                    hitPlayerData.RPC_TakeDamage(damage, Object.InputAuthority);
                }
            }

            RPC_OnShot(effectsOrigin, hit.point, true, hit.point, hit.normal);
        }
        else
        {
            Vector3 endPoint = origin + direction * range;
            RPC_OnShot(effectsOrigin, endPoint, false, Vector3.zero, Vector3.zero);
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
            return;
        }

        if (CurrentAmmo >= maxAmmo)
        {
            return;
        }

        if (ReserveAmmo <= 0)
        {
            return;
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

        int ammoNeeded = maxAmmo - CurrentAmmo;
        int ammoToReload = Mathf.Min(ammoNeeded, ReserveAmmo);
        
        CurrentAmmo += ammoToReload;
        ReserveAmmo -= ammoToReload;
        IsReloading = false;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnShot(Vector3 origin, Vector3 endPoint, bool didHit, Vector3 hitPoint, Vector3 hitNormal)
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
                muzzleEffect.SetContinuousMode(false); // Single burst mode for pistol
                muzzleEffect.Play();
                
                // Auto-destroy after effect
                Destroy(tempMuzzleFlash, 0.3f);
            }
            else
            {
                Destroy(tempMuzzleFlash, 0.3f);
            }
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

        if (bulletTrailPrefab != null)
        {
            GameObject trail = Instantiate(bulletTrailPrefab, origin, Quaternion.identity);
            LineRenderer lineRenderer = trail.GetComponent<LineRenderer>();
            if (lineRenderer != null)
            {
                lineRenderer.SetPosition(0, origin);
                lineRenderer.SetPosition(1, endPoint);
                Destroy(trail, 0.1f);
            }
        }

        if (didHit && hitEffectPrefab != null)
        {
            GameObject hitEffect = Instantiate(hitEffectPrefab, hitPoint, Quaternion.LookRotation(hitNormal));
            Destroy(hitEffect, 2f);
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
        ReserveAmmo += amount;
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
        // Set reserve ammo to maximum (150), capped to never exceed
        ReserveAmmo = Mathf.Min(reserveAmmo, 150);
        IsReloading = false;
        
        Debug.Log($"[NetworkPistolBehaviour] Ammo reset on kill - Magazine: {CurrentAmmo}/{maxAmmo}, Reserve: {ReserveAmmo}/150");
    }
}

