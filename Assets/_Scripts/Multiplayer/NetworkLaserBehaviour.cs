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
    [SerializeField] private int energyPerShot = 10;
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
    private NetworkWeaponEquipSystem equipSystem;
    private PlayerNetworkData playerData;
    private float lastShotTime;

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

    public void CollectNetworkInput(ref StarterAssets.NetworkInputData inputData)
    {
        inputData.isShooting = wantsToShoot;
        
        if (playerCamera != null)
        {
            inputData.aimDirection = playerCamera.transform.forward;
            inputData.aimOrigin = playerCamera.transform.position;
        }
        
        wantsToShoot = false;
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

        if (input.isShooting && !IsOverheated)
        {
            TryShoot(input.aimOrigin, input.aimDirection);
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

    private void TryShoot(Vector3 origin, Vector3 direction)
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

        if (CurrentEnergy < energyPerShot)
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

        CurrentEnergy -= energyPerShot;
        FireCooldownTimer = TickTimer.CreateFromSeconds(Runner, fireRate);
        
        Debug.Log($"[NetworkLaserBehaviour] *** LASER FIRED! *** Player {Object.InputAuthority.PlayerId} | Energy: {CurrentEnergy}/{maxEnergy} (-{energyPerShot}) | Direction: {direction}");

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
        Debug.Log($"[NetworkLaserBehaviour] *** LASER VISUAL EFFECTS *** Player {Object.InputAuthority.PlayerId} - Beam and muzzle flash triggered");
        
        // Show muzzle flash
        if (muzzleFlashPrefab != null)
        {
            StartCoroutine(ShowMuzzleFlash());
        }
        
        // Play laser sound
        if (laserShootSound != null)
        {
            AudioSource.PlayClipAtPoint(laserShootSound, origin, soundVolume);
        }

        // Create laser beam effect
        if (laserBeamPrefab != null)
        {
            GameObject laserBeam = Instantiate(laserBeamPrefab, origin, Quaternion.LookRotation(endPoint - origin));
            
            // Configure the laser beam
            LineRenderer lineRenderer = laserBeam.GetComponent<LineRenderer>();
            if (lineRenderer != null)
            {
                lineRenderer.startWidth = beamWidth;
                lineRenderer.endWidth = beamWidth;
                lineRenderer.material.color = laserColor;
                lineRenderer.SetPosition(0, origin);
                lineRenderer.SetPosition(1, endPoint);
                
                // Add glow effect
                if (lineRenderer.material.HasProperty("_EmissionColor"))
                {
                    lineRenderer.material.SetColor("_EmissionColor", laserColor);
                }
            }
            
            // Destroy beam after a short duration
            Destroy(laserBeam, 0.15f);
        }

        // Create impact effect
        if (didHit && laserImpactPrefab != null)
        {
            GameObject impact = Instantiate(laserImpactPrefab, hitPoint, Quaternion.LookRotation(hitNormal));
            Destroy(impact, 1f);
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
}
