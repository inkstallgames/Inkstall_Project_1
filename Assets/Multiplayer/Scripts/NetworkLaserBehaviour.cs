using Fusion;
using UnityEngine;

/// <summary>
/// Multiplayer laser shooting behaviour for Team B players.
/// Uses continuous laser beam instead of projectile bullets.
/// Higher damage but slower fire rate than pistol.
/// </summary>
public class NetworkLaserBehaviour : NetworkBehaviour
{
    [Header("Laser Settings")]
    [Tooltip("Shoot point on the FPS arm model (used for local player VFX)")]
    [SerializeField] private Transform armFirePoint;
    
    [Tooltip("Shoot point on the full body model (used for remote player VFX)")]
    [SerializeField] private Transform bodyFirePoint;
    [SerializeField] private float fireRate = 0.5f; // Slower than pistol
    [SerializeField] private float range = 150f; // Longer range than pistol
    [SerializeField] private int damage = 25; // Higher damage than pistol
    [SerializeField] private LayerMask hitLayers = -1;
    [SerializeField] private float beamWidth = 0.1f;
    [SerializeField] private Color laserColor = Color.red;

    [Header("Energy (Ammo System)")]
    [SerializeField] private int maxEnergy = 100;
    [SerializeField] private int currentEnergy = 100;
    [SerializeField] private int energyPerShot = 0; // Test value - should consume no energy
    [SerializeField] private float energyRegenRate = 20f; // Energy per second
    [SerializeField] private float regenDelay = 2f; // Delay before regen starts
    [SerializeField] private float reloadTime = 2f; // Reload time when energy reaches zero

    [Header("Effects")]
    [SerializeField] private GameObject laserBeamPrefab;
    [SerializeField] private GameObject laserImpactPrefab;
    [SerializeField] private GameObject muzzleFlashPrefab;
    [SerializeField] private AudioClip laserShootSound;
    [SerializeField] private AudioClip laserOverheatSound;
    [SerializeField] private float soundVolume = 1.0f;

    [Networked] public int CurrentEnergy { get; set; }
    [Networked] private TickTimer FireCooldownTimer { get; set; }
    [Networked] private TickTimer EnergyRegenTimer { get; set; }
    [Networked] private TickTimer ReloadTimer { get; set; }
    [Networked] private bool IsReloading { get; set; }

    private Camera playerCamera;
    private bool wantsToShoot;
    private bool isShootingContinuously; // Track continuous firing state
    private NetworkWeaponEquipSystem equipSystem;
    private PlayerNetworkData playerData;
    private float lastShotTime;
    private bool isLocalPlayer;
    
    // Decoupled Visual State NetworkBool
    [Networked] public NetworkBool IsFiringLaser { get; set; }
    private bool _lastIsFiringLaser;
    
    // Continuous beam visual references
    private LineRenderer continuousBeam;
    private GameObject continuousImpact;
    private GameObject continuousMuzzleFlash;
    
    // Track beam destruction
    private int beamDestructionCount = 0;

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
            CurrentEnergy = currentEnergy;
        }

        isLocalPlayer = Object.HasInputAuthority;

        if (isLocalPlayer)
        {
            playerCamera = Camera.main;
        }

        equipSystem = GetComponent<NetworkWeaponEquipSystem>();
        playerData = GetComponent<PlayerNetworkData>();
        
        // Force energy per shot to override Inspector values
        energyPerShot = 1; // Much slower consumption for longer duration

        // Ensure muzzle flash is disabled on start
        if (muzzleFlashPrefab != null)
        {
            muzzleFlashPrefab.SetActive(false);
        }
        
        // Log which fire points are assigned
        Debug.Log($"[NetworkLaserBehaviour] Spawned | Player: {(isLocalPlayer ? "LOCAL" : "REMOTE")} | ArmFirePoint: {(armFirePoint != null ? armFirePoint.name : "NULL")} | BodyFirePoint: {(bodyFirePoint != null ? bodyFirePoint.name : "NULL")}");
    }
    

    public void RequestShoot()
    {
        // Debug.Log("[NetworkLaserBehaviour] RequestShoot() called!");
        wantsToShoot = true;
    }

    public void StopShooting()
    {
        // Debug.Log("[NetworkLaserBehaviour] StopShooting() called!");
        wantsToShoot = false;
    }

    public void CollectNetworkInput(ref StarterAssets.NetworkInputData inputData)
    {
        // Debug.Log($"[NetworkLaserBehaviour] *** CollectNetworkInput *** wantsToShoot: {wantsToShoot} | isShootingContinuously: {isShootingContinuously} | inputData.isShooting: {inputData.isShooting}");
        
        // Set shooting state based on wantsToShoot (frame-persistent)
        if (wantsToShoot)
        {
            isShootingContinuously = true;
            // Debug.Log("[NetworkLaserBehaviour] *** INPUT: wantsToShoot true -> isShootingContinuously true");
        }
        // Only reset if wantsToShoot is false AND we were previously shooting
        else if (!wantsToShoot && isShootingContinuously)
        {
            isShootingContinuously = false;
            // Debug.Log("[NetworkLaserBehaviour] *** INPUT: wantsToShoot false & was shooting -> isShootingContinuously false");
        }
        
        inputData.isShooting = isShootingContinuously;
        
        if (playerCamera != null)
        {
            inputData.aimDirection = playerCamera.transform.forward;
            inputData.aimOrigin = playerCamera.transform.position;
        }
        
        // DON'T reset wantsToShoot here - let it persist until explicitly cleared
        // wantsToShoot = false; // REMOVED THIS LINE
        
        // Debug.Log($"[NetworkLaserBehaviour] *** CollectNetworkInput END *** inputData.isShooting: {inputData.isShooting} | isShootingContinuously: {isShootingContinuously}");
    }

    public override void FixedUpdateNetwork()
    {
        if (!GetInput<StarterAssets.NetworkInputData>(out var input))
        {
            return;
        }

        // Regenerate energy when not shooting
        if (!input.isShooting && CurrentEnergy < maxEnergy)
        {
            // Check if reload is in progress
            if (IsReloading)
            {
                if (ReloadTimer.ExpiredOrNotRunning(Runner))
                {
                    if (Object.HasStateAuthority)
                    {
                        // Debug.Log($"[NetworkLaserBehaviour] *** RELOAD COMPLETE! *** Player {Object.InputAuthority.PlayerId} | Energy: {CurrentEnergy} → {maxEnergy} (FULL RELOAD)");
                        CurrentEnergy = maxEnergy;
                        IsReloading = false;
                        ReloadTimer = TickTimer.None;
                        RPC_UpdateEnergy(CurrentEnergy);
                    }
                }
                else
                {
                    // Still reloading - don't allow normal regen
                    float remainingTime = ReloadTimer.RemainingTime(Runner) ?? 0f;
                    // Debug.Log($"[NetworkLaserBehaviour] *** RELOADING... *** Player {Object.InputAuthority.PlayerId} | {remainingTime:F1}s remaining");
                    return;
                }
            }
            
            if (EnergyRegenTimer.ExpiredOrNotRunning(Runner))
            {
                if (Object.HasStateAuthority)
                {
                    int oldEnergy = CurrentEnergy;
                    CurrentEnergy = Mathf.Min(maxEnergy, CurrentEnergy + Mathf.RoundToInt(energyRegenRate * Runner.DeltaTime));
                    int energyGained = CurrentEnergy - oldEnergy;
                    
                    if (energyGained > 0)
                    {
                        // Debug.Log($"[NetworkLaserBehaviour] *** ENERGY RECHARGING *** Player {Object.InputAuthority.PlayerId} | Energy: {oldEnergy} → {CurrentEnergy}/{maxEnergy} (+{energyGained})");
                    }
                    
                    RPC_UpdateEnergy(CurrentEnergy);
                }
            }
        }
        else if (input.isShooting)
        {
            // Reset regen timer when shooting
            if (Object.HasStateAuthority)
            {
                EnergyRegenTimer = TickTimer.CreateFromSeconds(Runner, regenDelay);
                // Debug.Log($"[NetworkLaserBehaviour] *** RECHARGE DELAY STARTED *** Player {Object.InputAuthority.PlayerId} - Energy regen paused for {regenDelay}s");
            }
        }

        if (input.isShooting && CurrentEnergy > 0 && !IsReloading && equipSystem != null && equipSystem.IsLaserEquipped())
        {
            IsFiringLaser = true;
            Debug.Log($"[NetworkLaserBehaviour] *** LASER FIRING *** Player: {(isLocalPlayer ? "LOCAL" : "REMOTE")} | Energy: {CurrentEnergy}/{maxEnergy} | IsFiringLaser: TRUE");

            // Only consume energy and deal damage on state authority
            if (Object.HasStateAuthority)
            {
                TryShootAuthority(input.aimOrigin, input.aimDirection);
            }
        }
        else
        {
            IsFiringLaser = false;
            if (input.isShooting)
            {
                Debug.LogWarning($"[NetworkLaserBehaviour] *** LASER NOT FIRING *** Player: {(isLocalPlayer ? "LOCAL" : "REMOTE")} | Energy: {CurrentEnergy} | IsReloading: {IsReloading} | EquipSystem: {(equipSystem != null)} | IsLaserEquipped: {(equipSystem != null ? equipSystem.IsLaserEquipped() : false)}");
            }
        }
    }

    private void TryShootAuthority(Vector3 origin, Vector3 direction)
    {
        if (!FireCooldownTimer.ExpiredOrNotRunning(Runner))
        {
            return;
        }

        CurrentEnergy -= energyPerShot;
        FireCooldownTimer = TickTimer.CreateFromSeconds(Runner, fireRate);

        if (CurrentEnergy <= 0)
        {
            CurrentEnergy = 0;
            IsReloading = true;
            ReloadTimer = TickTimer.CreateFromSeconds(Runner, reloadTime);
        }

        // Raycast from camera position for accurate shooting (origin = camera position from input)
        // firePoint is only used for visual effects spawn position
        if (Physics.Raycast(origin, direction, out RaycastHit hit, range, hitLayers))
        {
            var targetPlayerData = hit.collider.GetComponentInParent<PlayerNetworkData>();
            if (targetPlayerData != null)
            {
                if (targetPlayerData.Object.InputAuthority != Object.InputAuthority)
                {
                    targetPlayerData.RPC_TakeDamage(damage, Object.InputAuthority);
                }
            }
        }

        RPC_UpdateEnergy(CurrentEnergy);
    }



    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdateEnergy(int newEnergy)
    {
        CurrentEnergy = newEnergy;
    }

    public void AddEnergy(int amount)
    {
        if (!Object.HasStateAuthority) return;
        CurrentEnergy = Mathf.Min(maxEnergy, CurrentEnergy + amount);
        RPC_UpdateEnergy(CurrentEnergy);
    }

    /// <summary>
    /// Resets laser energy to full when player gets a kill.
    /// Called by NetworkGameManager on kill.
    /// </summary>
    public void ResetEnergyOnKill()
    {
        if (!Object.HasStateAuthority) return;
        
        CurrentEnergy = maxEnergy;
        IsReloading = false;
        ReloadTimer = TickTimer.None;
        
        Debug.Log($"[NetworkLaserBehaviour] Energy reset on kill - Energy: {CurrentEnergy}/{maxEnergy}");
        RPC_UpdateEnergy(CurrentEnergy);
    }

    private System.Collections.IEnumerator ShowMuzzleFlash()
    {
        // Debug.Log($"[NetworkLaserBehaviour] *** LASER MUZZLE FLASH *** Player {Object.InputAuthority.PlayerId} - Muzzle flash visible");
        
        Transform fp = ActiveFirePoint;
        if (muzzleFlashPrefab != null && fp != null)
        {
            // Create temporary muzzle flash for single shot
            GameObject tempMuzzleFlash = Instantiate(muzzleFlashPrefab, fp.position, fp.rotation);
            tempMuzzleFlash.transform.SetParent(fp);
            
            // CRITICAL: Ensure the GameObject is active!
            tempMuzzleFlash.SetActive(true);
            // Debug.Log($"[NetworkLaserBehaviour] Muzzle flash GameObject activated: {tempMuzzleFlash.activeInHierarchy}");
            
            // Use particle system
            var muzzleEffect = tempMuzzleFlash.GetComponent<MuzzleFlashEffect>();
            if (muzzleEffect != null)
            {
                muzzleEffect.SetContinuousMode(false); // Single burst mode
                muzzleEffect.Play();
                // Debug.Log("[NetworkLaserBehaviour] *** SINGLE MUZZLE FLASH PARTICLE EFFECT PLAYED ***");
            }
            else
            {
                // Debug.LogWarning("[NetworkLaserBehaviour] MuzzleFlashEffect component not found on muzzle flash prefab!");
            }
            
            yield return new WaitForSeconds(0.05f); // Short flash duration
            
            // Clean up
            if (muzzleEffect != null)
            {
                muzzleEffect.Stop();
            }
            Destroy(tempMuzzleFlash);
            // Debug.Log($"[NetworkLaserBehaviour] *** LASER MUZZLE FLASH *** Player {Object.InputAuthority.PlayerId} - Muzzle flash hidden");
        }
    }

    // Public method to check if player can use laser
    public bool CanUseLaser()
    {
        return true; // Prefab separation handles team restrictions
    }
    
    private void StartContinuousBeam()
    {
        if (continuousBeam != null)
        {
            Debug.Log($"[NetworkLaserBehaviour] *** BEAM ALREADY EXISTS *** Skipping creation");
            return;
        }

        Transform fp = ActiveFirePoint;
        Vector3 origin = fp != null ? fp.position : transform.position;
        Vector3 direction = playerCamera != null ? playerCamera.transform.forward : Vector3.forward;

        Debug.Log($"[NetworkLaserBehaviour] *** CREATING BEAM *** Player: {(isLocalPlayer ? "LOCAL" : "REMOTE")} | FirePoint: {(fp != null ? fp.name : "NULL")} | Origin: {origin} | Direction: {direction}");
        
        // Create continuous beam
        if (laserBeamPrefab != null)
        {
            GameObject beamObj = Instantiate(laserBeamPrefab, origin, Quaternion.LookRotation(direction));
            beamObj.transform.SetParent(transform); // Parent to the weapon!
            continuousBeam = beamObj.GetComponent<LineRenderer>();
            
            Debug.Log($"[NetworkLaserBehaviour] *** BEAM PREFAB INSTANTIATED *** BeamObj: {beamObj.name} | LineRenderer: {(continuousBeam != null ? "FOUND" : "NULL")}");
            
            // Set continuous mode to prevent auto-destruct
            var beamEffect = beamObj.GetComponent<LaserBeamEffect>();
            if (beamEffect != null)
            {
                beamEffect.SetContinuousMode(true);
                Debug.Log($"[NetworkLaserBehaviour] *** BEAM EFFECT SET TO CONTINUOUS MODE ***");
            }
            else
            {
                Debug.LogWarning($"[NetworkLaserBehaviour] *** LaserBeamEffect component not found on beam prefab! ***");
            }
            
            if (continuousBeam != null)
            {
                continuousBeam.positionCount = 2;
                continuousBeam.SetPosition(0, origin);
                continuousBeam.SetPosition(1, origin + direction * 100f);
                continuousBeam.enabled = true;
                Debug.Log($"[NetworkLaserBehaviour] *** CONTINUOUS BEAM CREATED SUCCESSFULLY *** #{++beamDestructionCount} | Enabled: {continuousBeam.enabled} | Positions: {origin} -> {origin + direction * 100f}");
            }
            else
            {
                Debug.LogError("[NetworkLaserBehaviour] *** BEAM CREATION FAILED *** LineRenderer component not found!");
                Destroy(beamObj);
                return;
            }
        }
        else
        {
            Debug.LogError($"[NetworkLaserBehaviour] *** BEAM CREATION FAILED *** laserBeamPrefab is NULL!");
        }
        
        // Hit logic now dynamically calculated in Render
        RaycastHit visualHit;
        bool didHit = Physics.Raycast(origin, direction, out visualHit, 100f, hitLayers);

        // Create continuous impact
        if (didHit && laserImpactPrefab != null)
        {
            continuousImpact = Instantiate(laserImpactPrefab, visualHit.point, Quaternion.LookRotation(visualHit.normal));
            continuousImpact.transform.SetParent(transform);
            
            // Set continuous mode for impact particles
            var impactEffect = continuousImpact.GetComponent<LaserImpactEffect>();
            if (impactEffect != null)
            {
                impactEffect.SetContinuousMode(true);
                // Debug.Log("[NetworkLaserBehaviour] *** CONTINUOUS IMPACT MODE SET ***");
            }
            else
            {
                // Debug.Log("[NetworkLaserBehaviour] *** WARNING: LaserImpactEffect component not found on impact prefab ***");
            }
            
            // Get the particle system and make sure it plays
            var impactParticles = continuousImpact.GetComponent<ParticleSystem>();
            if (impactParticles != null)
            {
                impactParticles.Play();
                // Debug.Log("[NetworkLaserBehaviour] *** CONTINUOUS IMPACT PARTICLES STARTED ***");
            }
            else
            {
                // Debug.Log("[NetworkLaserBehaviour] *** WARNING: ParticleSystem component not found on impact prefab ***");
            }
            
            // Debug.Log("[NetworkLaserBehaviour] *** CONTINUOUS IMPACT CREATED ***");
        }
        
        // Create continuous muzzle flash
        Transform fp2 = ActiveFirePoint;
        if (muzzleFlashPrefab != null && fp2 != null)
        {
            continuousMuzzleFlash = Instantiate(muzzleFlashPrefab, fp2.position, fp2.rotation);
            continuousMuzzleFlash.transform.SetParent(fp2);
            
            // Set continuous mode for muzzle flash particles
            var muzzleEffect = continuousMuzzleFlash.GetComponent<MuzzleFlashEffect>();
            if (muzzleEffect != null)
            {
                muzzleEffect.SetContinuousMode(true);
                muzzleEffect.Play();
                // Debug.Log("[NetworkLaserBehaviour] *** CONTINUOUS MUZZLE FLASH MODE SET ***");
            }
            else
            {
                // Fallback to old quad behavior
                continuousMuzzleFlash.SetActive(true);
            }
        }
        
        if (laserShootSound != null) AudioSource.PlayClipAtPoint(laserShootSound, origin, soundVolume);
    }
    
    private void UpdateContinuousBeam()
    {
        // Only update positions if beam is already active
        if (continuousBeam != null && playerCamera != null)
        {
            Transform fp = ActiveFirePoint;
            Vector3 visualOrigin = fp != null ? fp.position : transform.position;
            Vector3 direction = playerCamera.transform.forward;
            float maxDistance = 100f;
            
            // Raycast from camera position for accurate hit detection
            Vector3 cameraOrigin = playerCamera.transform.position;
            RaycastHit hit;
            Vector3 endPoint = cameraOrigin + direction * maxDistance;
            bool didHit = Physics.Raycast(cameraOrigin, direction, out hit, maxDistance, hitLayers);
            
            Vector3 hitPoint = didHit ? hit.point : endPoint;
            Vector3 hitNormal = didHit ? hit.normal : Vector3.zero;

            // Update beam positions (visual starts from firePoint, ends at camera raycast hit)
            continuousBeam.SetPosition(0, visualOrigin);
            continuousBeam.SetPosition(1, hitPoint);
            
            // Ensure beam is enabled
            if (!continuousBeam.enabled)
            {
                continuousBeam.enabled = true;
            }
            
            // Update continuous impact position
            if (continuousImpact != null && didHit)
            {
                continuousImpact.transform.position = hitPoint;
                continuousImpact.transform.rotation = Quaternion.LookRotation(hitNormal);
                
                // Make sure particles are still playing
                var impactParticles = continuousImpact.GetComponent<ParticleSystem>();
                if (impactParticles != null && !impactParticles.isPlaying)
                {
                    impactParticles.Play();
                }
            }
            
            // Update continuous muzzle flash position
            Transform fp2 = ActiveFirePoint;
            if (continuousMuzzleFlash != null && fp2 != null)
            {
                continuousMuzzleFlash.transform.position = fp2.position;
                continuousMuzzleFlash.transform.rotation = fp2.rotation;
            }
        }
        else if (continuousBeam == null)
        {
            // Beam was destroyed unexpectedly - recreate it
            StartContinuousBeam();
        }
    }
    
    void OnDestroy()
    {
        if (continuousBeam != null)
        {
            // Debug.Log($"[NetworkLaserBehaviour] *** ONDESTROY CALLED *** Beam was destroyed externally! #{beamDestructionCount}");
            continuousBeam = null;
        }
    }
    
    private void StopContinuousBeam()
    {
        // Clean up continuous beam
        if (continuousBeam != null)
        {
            Destroy(continuousBeam.gameObject);
            continuousBeam = null;
        }
        
        // Clean up continuous muzzle flash
        if (continuousMuzzleFlash != null)
        {
            var muzzleEffect = continuousMuzzleFlash.GetComponent<MuzzleFlashEffect>();
            if (muzzleEffect != null)
            {
                muzzleEffect.Stop();
            }
            
            Destroy(continuousMuzzleFlash);
            continuousMuzzleFlash = null;
        }
        
        // Clean up continuous impact
        if (continuousImpact != null)
        {
            Destroy(continuousImpact);
            continuousImpact = null;
        }
    }
    
    public override void Render()
    {
        if (IsFiringLaser && !_lastIsFiringLaser)
        {
            Debug.Log($"[NetworkLaserBehaviour] *** RENDER: STARTING BEAM *** Player: {(isLocalPlayer ? "LOCAL" : "REMOTE")} | ActiveFirePoint: {(ActiveFirePoint != null ? ActiveFirePoint.name : "NULL")}");
            StartContinuousBeam();
        }
        else if (!IsFiringLaser && _lastIsFiringLaser)
        {
            Debug.Log($"[NetworkLaserBehaviour] *** RENDER: STOPPING BEAM *** Player: {(isLocalPlayer ? "LOCAL" : "REMOTE")}");
            StopContinuousBeam();
        }

        // Standard graphical continuous beam updates perfectly synced to screen refresh
        if (IsFiringLaser)
        {
            UpdateContinuousBeam();
        }

        _lastIsFiringLaser = IsFiringLaser;
    }
}
