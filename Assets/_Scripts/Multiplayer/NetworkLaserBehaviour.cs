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
    [SerializeField] private Transform firePoint;
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
    [Networked] private bool IsOverheated { get; set; }

    private Camera playerCamera;
    private bool wantsToShoot;
    private bool isShootingContinuously; // Track continuous firing state
    private NetworkWeaponEquipSystem equipSystem;
    private PlayerNetworkData playerData;
    private float lastShotTime;
    
    // Continuous beam effects
    private LineRenderer continuousBeam;
    private GameObject continuousImpact;
    private GameObject continuousMuzzleFlash;
    private bool isBeamActive = false;

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            CurrentEnergy = currentEnergy;
            IsOverheated = false;
        }

        if (Object.HasInputAuthority)
        {
            playerCamera = Camera.main;
        }

        equipSystem = GetComponent<NetworkWeaponEquipSystem>();
        playerData = GetComponent<PlayerNetworkData>();

        // Force energy per shot to override Inspector values
        energyPerShot = 2; // ~50 seconds duration (100 energy / 2 per tick)

        // Ensure muzzle flash is disabled on start
        if (muzzleFlashPrefab != null)
        {
            muzzleFlashPrefab.SetActive(false);
        }
    }

    public void RequestShoot()
    {
        Debug.Log("[NetworkLaserBehaviour] RequestShoot() called!");
        wantsToShoot = true;
    }

    public void StopShooting()
    {
        Debug.Log("[NetworkLaserBehaviour] StopShooting() called!");
        wantsToShoot = false;
    }

    public void CollectNetworkInput(ref StarterAssets.NetworkInputData inputData)
    {
        Debug.Log($"[NetworkLaserBehaviour] *** CollectNetworkInput *** wantsToShoot: {wantsToShoot} | isShootingContinuously: {isShootingContinuously} | inputData.isShooting: {inputData.isShooting}");
        
        // Set shooting state based on wantsToShoot (frame-persistent)
        if (wantsToShoot)
        {
            isShootingContinuously = true;
            Debug.Log("[NetworkLaserBehaviour] *** INPUT: wantsToShoot true -> isShootingContinuously true");
        }
        // Only reset if wantsToShoot is false AND we were previously shooting
        else if (!wantsToShoot && isShootingContinuously)
        {
            isShootingContinuously = false;
            Debug.Log("[NetworkLaserBehaviour] *** INPUT: wantsToShoot false & was shooting -> isShootingContinuously false");
        }
        
        inputData.isShooting = isShootingContinuously;
        
        if (playerCamera != null)
        {
            inputData.aimDirection = playerCamera.transform.forward;
            inputData.aimOrigin = playerCamera.transform.position;
        }
        
        // DON'T reset wantsToShoot here - let it persist until explicitly cleared
        // wantsToShoot = false; // REMOVED THIS LINE
        
        Debug.Log($"[NetworkLaserBehaviour] *** CollectNetworkInput END *** inputData.isShooting: {inputData.isShooting} | isShootingContinuously: {isShootingContinuously}");
    }

    public override void FixedUpdateNetwork()
    {
        if (!GetInput<StarterAssets.NetworkInputData>(out var input))
        {
            return;
        }

        // Regenerate energy when not shooting
        if (!input.isShooting && !IsOverheated && CurrentEnergy < maxEnergy)
        {
            if (EnergyRegenTimer.ExpiredOrNotRunning(Runner))
            {
                if (Object.HasStateAuthority)
                {
                    int oldEnergy = CurrentEnergy;
                    CurrentEnergy = Mathf.Min(maxEnergy, CurrentEnergy + Mathf.RoundToInt(energyRegenRate * Runner.DeltaTime));
                    int energyGained = CurrentEnergy - oldEnergy;
                    
                    if (energyGained > 0)
                    {
                        Debug.Log($"[NetworkLaserBehaviour] *** ENERGY RECHARGING *** Player {Object.InputAuthority.PlayerId} | Energy: {oldEnergy} → {CurrentEnergy}/{maxEnergy} (+{energyGained})");
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
                Debug.Log($"[NetworkLaserBehaviour] *** RECHARGE DELAY STARTED *** Player {Object.InputAuthority.PlayerId} - Energy regen paused for {regenDelay}s");
            }
        }

        if (input.isShooting && !IsOverheated && CurrentEnergy > 0)
        {
            Debug.Log($"[NetworkLaserBehaviour] *** SHOOTING CONDITIONS MET *** isShooting: {input.isShooting} | !IsOverheated: {!IsOverheated} | CurrentEnergy: {CurrentEnergy} | isBeamActive: {isBeamActive}");
            
            // Start continuous beam if not already active
            if (!isBeamActive)
            {
                Debug.Log("[NetworkLaserBehaviour] *** BRANCH: Starting new beam ***");
                TryShoot(input.aimOrigin, input.aimDirection, false); // Initial shot, NO energy consumption
            }
            else if (isBeamActive)
            {
                Debug.Log("[NetworkLaserBehaviour] *** BRANCH: Updating continuous beam ***");
                // Update continuous beam
                Vector3 origin = firePoint != null ? firePoint.position : input.aimOrigin;
                Vector3 direction = input.aimDirection;
                float maxDistance = 100f;
                
                RaycastHit hit;
                Vector3 endPoint = origin + direction * maxDistance;
                bool didHit = Physics.Raycast(origin, direction, out hit, maxDistance, hitLayers);
                
                if (didHit)
                {
                    endPoint = hit.point;
                    // Apply damage continuously
                    if (Object.HasStateAuthority)
                    {
                        var hitCollider = hit.collider;
                        var targetNetworkObj = hitCollider.GetComponent<NetworkObject>();
                        if (targetNetworkObj != null)
                        {
                            var targetPlayer = targetNetworkObj.GetComponent<PlayerNetworkData>();
                            if (targetPlayer != null && targetPlayer.Object.InputAuthority != Object.InputAuthority)
                            {
                                targetPlayer.RPC_TakeDamage(damage, Object.InputAuthority);
                                Debug.Log($"[NetworkLaserBehaviour] Continuous beam hit: {hitCollider.name} for {damage} damage");
                            }
                        }
                    }
                }
                
                // Consume energy for continuous beam
                if (Object.HasStateAuthority)
                {
                    Debug.Log($"[NetworkLaserBehaviour] *** ENERGY PER SHOT VALUE *** {energyPerShot}");
                    CurrentEnergy -= energyPerShot;
                    Debug.Log($"[NetworkLaserBehaviour] *** CONTINUOUS ENERGY CONSUMPTION *** Energy: {CurrentEnergy + energyPerShot} → {CurrentEnergy} (-{energyPerShot})");
                    if (CurrentEnergy <= 0)
                    {
                        CurrentEnergy = 0;
                        Debug.Log($"[NetworkLaserBehaviour] *** ENERGY DEPLETED! *** Player {Object.InputAuthority.PlayerId} | Energy: {CurrentEnergy}/{maxEnergy}");
                        StopContinuousBeam();
                    }
                    RPC_UpdateEnergy(CurrentEnergy);
                }
                
                // Update existing beam positions instead of recreating it
                UpdateContinuousBeam(origin, endPoint, hit.point, hit.normal, didHit);
            }
        }
        else
        {
            Debug.Log($"[NetworkLaserBehaviour] *** STOPPING BEAM CONDITIONS *** isShooting: {input.isShooting} | IsOverheated: {IsOverheated} | CurrentEnergy: {CurrentEnergy} | isBeamActive: {isBeamActive}");
            // Stop continuous beam when not shooting or out of energy/overheated
            StopContinuousBeam();
        }

        // Check if we can start regenerating
        if (EnergyRegenTimer.ExpiredOrNotRunning(Runner) && IsOverheated && CurrentEnergy > 0)
        {
            if (Object.HasStateAuthority)
            {
                IsOverheated = false;
                Debug.Log($"[NetworkLaserBehaviour] *** OVERHEAT CLEARED! *** Player {Object.InputAuthority.PlayerId} - Laser ready to fire again!");
            }
        }
    }

    private void TryShoot(Vector3 origin, Vector3 direction, bool consumeEnergy = true)
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        
        if (equipSystem != null && !equipSystem.IsLaserEquipped())
        {
            Debug.Log("[NetworkLaserBehaviour] TryShoot — skipped, laser not equipped.");
            return;
        }

        if (!FireCooldownTimer.ExpiredOrNotRunning(Runner))
        {
            return;
        }

        if (CurrentEnergy < energyPerShot && consumeEnergy)
        {
            Debug.Log($"[NetworkLaserBehaviour] *** ENERGY DEPLETED! *** Player {Object.InputAuthority.PlayerId} | Energy: {CurrentEnergy}/{maxEnergy} | Need: {energyPerShot}");
            
            // Overheat if we try to shoot with no energy
            if (!IsOverheated)
            {
                IsOverheated = true;
                Debug.Log($"[NetworkLaserBehaviour] *** LASER OVERHEAT! *** Player {Object.InputAuthority.PlayerId} - Weapon disabled until energy regenerates");
                RPC_OnOverheat();
            }
            return;
        }

        // Only consume energy if this is not the initial beam setup
        if (consumeEnergy)
        {
            CurrentEnergy -= energyPerShot;
            FireCooldownTimer = TickTimer.CreateFromSeconds(Runner, fireRate);
            
            Debug.Log($"[NetworkLaserBehaviour] *** LASER FIRED! *** Player {Object.InputAuthority.PlayerId} | Energy: {CurrentEnergy}/{maxEnergy} (-{energyPerShot}) | Direction: {direction}");
        }
        else
        {
            Debug.Log($"[NetworkLaserBehaviour] *** INITIAL BEAM SETUP *** Player {Object.InputAuthority.PlayerId} | No energy consumed | Direction: {direction}");
        }

        Vector3 shootOrigin = firePoint != null ? firePoint.position : origin;
        
        if (Physics.Raycast(shootOrigin, direction, out RaycastHit hit, range, hitLayers))
        {
            Debug.Log($"[NetworkLaserBehaviour] Laser hit: {hit.collider.name} at distance {hit.distance}");

            var targetPlayerData = hit.collider.GetComponentInParent<PlayerNetworkData>();
            if (targetPlayerData != null)
            {
                Debug.Log($"[NetworkLaserBehaviour] *** LASER HIT PLAYER *** Target: {targetPlayerData.PlayerName} (ID:{targetPlayerData.Object.InputAuthority.PlayerId}) | Shooter: Player {Object.InputAuthority.PlayerId}");
                
                if (targetPlayerData.Object.InputAuthority != Object.InputAuthority)
                {
                    targetPlayerData.RPC_TakeDamage(damage, Object.InputAuthority);
                    Debug.Log($"[NetworkLaserBehaviour] Dealt {damage} laser damage to {targetPlayerData.PlayerName}");
                }
                else
                {
                    Debug.Log($"[NetworkLaserBehaviour] Cannot shoot yourself! Ignoring hit.");
                }
            }

            RPC_OnLaserShot(shootOrigin, hit.point, true, hit.point, hit.normal);
        }
        else
        {
            Vector3 endPoint = shootOrigin + direction * range;
            RPC_OnLaserShot(shootOrigin, endPoint, false, Vector3.zero, Vector3.zero);
        }

        RPC_UpdateEnergy(CurrentEnergy);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnLaserShot(Vector3 origin, Vector3 endPoint, bool didHit, Vector3 hitPoint, Vector3 hitNormal)
    {
        Debug.Log($"[NetworkLaserBehaviour] *** CONTINUOUS BEAM STARTED *** Player {Object.InputAuthority.PlayerId}");
        
        // Start continuous beam instead of instant effects
        StartContinuousBeam(origin, endPoint, hitPoint, hitNormal, didHit);
        
        // Play laser sound (looping sound could be added here for continuous beam)
        if (laserShootSound != null)
        {
            AudioSource.PlayClipAtPoint(laserShootSound, origin, soundVolume);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnOverheat()
    {
        Debug.Log($"[NetworkLaserBehaviour] *** LASER OVERHEAT EFFECT *** Player {Object.InputAuthority.PlayerId} - Visual/sound effects triggered");
        
        if (laserOverheatSound != null)
        {
            AudioSource.PlayClipAtPoint(laserOverheatSound, transform.position, soundVolume);
        }
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

    private System.Collections.IEnumerator ShowMuzzleFlash()
    {
        Debug.Log($"[NetworkLaserBehaviour] *** LASER MUZZLE FLASH *** Player {Object.InputAuthority.PlayerId} - Muzzle flash visible");
        
        if (muzzleFlashPrefab != null)
        {
            muzzleFlashPrefab.SetActive(true);
        }
        
        yield return new WaitForSeconds(0.05f); // Short flash duration
        
        if (muzzleFlashPrefab != null)
        {
            muzzleFlashPrefab.SetActive(false);
            Debug.Log($"[NetworkLaserBehaviour] *** LASER MUZZLE FLASH *** Player {Object.InputAuthority.PlayerId} - Muzzle flash hidden");
        }
    }

    // Public method to check if player can use laser
    public bool CanUseLaser()
    {
        return true; // Prefab separation handles team restrictions
    }
    
    private void StartContinuousBeam(Vector3 origin, Vector3 direction, Vector3 hitPoint, Vector3 hitNormal, bool didHit)
    {
        Debug.Log($"[NetworkLaserBehaviour] *** StartContinuousBeam CALLED *** isBeamActive: {isBeamActive} | didHit: {didHit}");
        
        if (isBeamActive)
        {
            Debug.Log("[NetworkLaserBehaviour] *** BEAM ALREADY ACTIVE - SKIPPING CREATION ***");
            return;
        }
        
        // Create continuous beam
        if (laserBeamPrefab != null)
        {
            GameObject beamObj = Instantiate(laserBeamPrefab, origin, Quaternion.LookRotation(direction));
            continuousBeam = beamObj.GetComponent<LineRenderer>();
            beamObj.transform.SetParent(transform);
            
            if (continuousBeam != null)
            {
                continuousBeam.positionCount = 2;
                continuousBeam.SetPosition(0, origin);
                continuousBeam.SetPosition(1, didHit ? hitPoint : origin + direction * range);
                continuousBeam.enabled = true;
                Debug.Log("[NetworkLaserBehaviour] *** CONTINUOUS BEAM CREATED ***");
            }
        }
        
        // Create continuous impact
        if (didHit && laserImpactPrefab != null)
        {
            continuousImpact = Instantiate(laserImpactPrefab, hitPoint, Quaternion.LookRotation(hitNormal));
            continuousImpact.transform.SetParent(transform);
            Debug.Log("[NetworkLaserBehaviour] *** CONTINUOUS IMPACT CREATED ***");
        }
        
        // Create continuous muzzle flash
        if (muzzleFlashPrefab != null && firePoint != null)
        {
            continuousMuzzleFlash = Instantiate(muzzleFlashPrefab, firePoint.position, firePoint.rotation);
            continuousMuzzleFlash.transform.SetParent(firePoint);
            continuousMuzzleFlash.SetActive(true);
            Debug.Log("[NetworkLaserBehaviour] *** CONTINUOUS MUZZLE FLASH CREATED ***");
        }
        
        isBeamActive = true;
        Debug.Log("[NetworkLaserBehaviour] *** CONTINUOUS BEAM STARTED ***");
    }
    
    private void UpdateContinuousBeam(Vector3 origin, Vector3 endPoint, Vector3 hitPoint, Vector3 hitNormal, bool didHit)
    {
        Debug.Log($"[NetworkLaserBehaviour] *** UpdateContinuousBeam CALLED *** isBeamActive: {isBeamActive} | continuousBeam: {continuousBeam != null}");
        
        // Only update positions if beam is already active
        if (isBeamActive && continuousBeam != null)
        {
            // Update beam positions
            continuousBeam.SetPosition(0, origin);
            continuousBeam.SetPosition(1, endPoint);
            Debug.Log($"[NetworkLaserBehaviour] *** BEAM POSITIONS UPDATED *** {origin} → {endPoint}");
            
            // Update continuous impact position
            if (continuousImpact != null && didHit)
            {
                continuousImpact.transform.position = hitPoint;
                continuousImpact.transform.rotation = Quaternion.LookRotation(hitNormal);
            }
            
            // Update continuous muzzle flash position
            if (continuousMuzzleFlash != null && firePoint != null)
            {
                continuousMuzzleFlash.transform.position = firePoint.position;
                continuousMuzzleFlash.transform.rotation = firePoint.rotation;
            }
        }
        else
        {
            Debug.Log($"[NetworkLaserBehaviour] *** BEAM UPDATE SKIPPED *** isBeamActive: {isBeamActive} | continuousBeam: {continuousBeam != null}");
        }
    }
    
    private void StopContinuousBeam()
    {
        Debug.Log($"[NetworkLaserBehaviour] *** StopContinuousBeam CALLED *** isBeamActive: {isBeamActive}");
        
        if (isBeamActive)
        {
            Debug.Log("[NetworkLaserBehaviour] *** CONTINUOUS BEAM STOPPED ***");
            isBeamActive = false;
            
            // Clean up continuous beam
            if (continuousBeam != null)
            {
                Destroy(continuousBeam.gameObject);
                continuousBeam = null;
            }
            
            // Clean up continuous muzzle flash
            if (continuousMuzzleFlash != null)
            {
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
    }
    
    private void Update()
    {
        // Update continuous beam if active (visual only, runs on all clients)
        if (isBeamActive && continuousBeam != null && playerCamera != null)
        {
            Vector3 origin = firePoint != null ? firePoint.position : transform.position;
            Vector3 direction = playerCamera.transform.forward;
            float maxDistance = 100f;
            
            RaycastHit hit;
            Vector3 endPoint = origin + direction * maxDistance;
            bool didHit = Physics.Raycast(origin, direction, out hit, maxDistance, hitLayers);
            
            if (didHit)
            {
                endPoint = hit.point;
                
                // Update impact effect
                if (continuousImpact != null)
                {
                    continuousImpact.transform.position = hit.point;
                    continuousImpact.transform.rotation = Quaternion.LookRotation(hit.normal);
                }
            }
            
            // Update beam
            continuousBeam.SetPosition(0, origin);
            continuousBeam.SetPosition(1, endPoint);
            
            // Update muzzle flash
            if (continuousMuzzleFlash != null && firePoint != null)
            {
                continuousMuzzleFlash.transform.position = firePoint.position;
                continuousMuzzleFlash.transform.rotation = firePoint.rotation;
            }
        }
    }
}
