using Fusion;
using UnityEngine;

/// <summary>
/// Multiplayer pistol shooting behaviour using raycast.
/// Attach to each player prefab alongside NetworkBombBehaviour.
/// </summary>
public class NetworkPistolBehaviour : NetworkBehaviour
{
    [Header("Pistol Settings")]
    [SerializeField] private Transform firePoint; // Will be auto-detected from active model
    [Tooltip("Name of the shoot point transform (e.g. 'Shootpoint'). Will search in active weapon models.")]
    [SerializeField] private string shootPointName = "Shootpoint";
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

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            CurrentAmmo = currentAmmo;
            ReserveAmmo = reserveAmmo;
            IsReloading = false;
        }

        if (Object.HasInputAuthority)
        {
            playerCamera = Camera.main;
        }

        equipSystem = GetComponent<NetworkWeaponEquipSystem>();
        playerData = GetComponent<PlayerNetworkData>();

        // Ensure muzzle flash prefab is disabled on start
        if (muzzleFlashPrefab != null)
        {
            muzzleFlashPrefab.SetActive(false);
        }
        
        // Auto-detect firePoint if not manually assigned
        if (firePoint == null)
        {
            Debug.Log($"[NetworkPistolBehaviour] Spawned() - Attempting initial firePoint detection | Player: {(Object.HasInputAuthority ? "LOCAL" : "REMOTE")}");
            FindActiveFirePoint();
            
            // Retry after a short delay to ensure PlayerVisualManager has toggled models
            StartCoroutine(DelayedFirePointDetection());
        }
    }
    
    private System.Collections.IEnumerator DelayedFirePointDetection()
    {
        yield return new WaitForSeconds(0.1f); // Wait for PlayerVisualManager to run
        
        if (firePoint == null || !firePoint.gameObject.activeInHierarchy)
        {
            Debug.Log($"[NetworkPistolBehaviour] Delayed retry - Re-detecting firePoint after model visibility change | Player: {(Object.HasInputAuthority ? "LOCAL" : "REMOTE")}");
            FindActiveFirePoint();
        }
    }
    
    /// <summary>
    /// Dynamically finds the active shoot point from either the full body or arms-only model.
    /// This allows the same script to work with both local (arms) and remote (full body) views.
    /// </summary>
    private void FindActiveFirePoint()
    {
        Debug.Log($"[NetworkPistolBehaviour] FindActiveFirePoint() called | Player: {(Object.HasInputAuthority ? "LOCAL" : "REMOTE")}");
        
        // Search for shoot point by name in all active children
        Transform[] allTransforms = GetComponentsInChildren<Transform>(false); // false = only active objects
        foreach (Transform t in allTransforms)
        {
            if (t.name == shootPointName)
            {
                // Check if this shoot point's parent hierarchy is active
                if (t.gameObject.activeInHierarchy)
                {
                    firePoint = t;
                    Debug.Log($"[NetworkPistolBehaviour] ✅ PICKED ACTIVE SHOOT POINT: '{t.name}' at path: {GetTransformPath(t)} | Player: {(Object.HasInputAuthority ? "LOCAL" : "REMOTE")}");
                    return;
                }
            }
        }
        
        // If not found, try searching inactive objects as fallback
        allTransforms = GetComponentsInChildren<Transform>(true); // true = include inactive
        foreach (Transform t in allTransforms)
        {
            if (t.name == shootPointName)
            {
                firePoint = t;
                Debug.LogWarning($"[NetworkPistolBehaviour] ⚠️ PICKED INACTIVE SHOOT POINT: '{t.name}' at path: {GetTransformPath(t)} | Player: {(Object.HasInputAuthority ? "LOCAL" : "REMOTE")}");
                return;
            }
        }
        
        Debug.LogError($"[NetworkPistolBehaviour] ❌ NO SHOOT POINT FOUND with name '{shootPointName}' in player hierarchy! | Player: {(Object.HasInputAuthority ? "LOCAL" : "REMOTE")}");
    }
    
    private string GetTransformPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null && t.parent != transform)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
    
    /// <summary>
    /// Call this to refresh the fire point reference (useful after model visibility changes).
    /// </summary>
    public void RefreshFirePoint()
    {
        FindActiveFirePoint();
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
            // Debug.Log("[NetworkPistolBehaviour] TryShoot — skipped, pistol not equipped.");
            return;
        }

        
        if (!FireCooldownTimer.ExpiredOrNotRunning(Runner))
        {
            return;
        }

        if (CurrentAmmo <= 0)
        {
            // Debug.Log($"[NetworkPistolBehaviour] Out of ammo! Player {Object.InputAuthority}");
            return;
        }

        CurrentAmmo--;
        FireCooldownTimer = TickTimer.CreateFromSeconds(Runner, fireRate);
        
        // Debug.Log($"[NetworkPistolBehaviour] SHOT FIRED! Player {Object.InputAuthority.PlayerId} | Ammo: {CurrentAmmo}/{maxAmmo} | Direction: {direction}");

        // Ensure we have the correct active firePoint
        if (firePoint == null || !firePoint.gameObject.activeInHierarchy)
        {
            FindActiveFirePoint();
        }
        
        // Raycast from camera position for accurate shooting (origin = camera position from input)
        // firePoint is only used for visual effects spawn position
        Vector3 effectsOrigin = firePoint != null ? firePoint.position : origin;
        
        if (Physics.Raycast(origin, direction, out RaycastHit hit, range, hitLayers))
        {
            // Debug.Log($"[NetworkPistolBehaviour] Hit: {hit.collider.name} at distance {hit.distance}");

            var playerData = hit.collider.GetComponentInParent<PlayerNetworkData>();
            if (playerData != null)
            {
                // Debug.Log($"[NetworkPistolBehaviour] *** RAYCAST HIT PLAYER *** Target: {playerData.PlayerName} (ID:{playerData.Object.InputAuthority.PlayerId}) | Shooter: Player {Object.InputAuthority.PlayerId}");
                
                if (playerData.Object.InputAuthority != Object.InputAuthority)
                {
                    playerData.RPC_TakeDamage(damage, Object.InputAuthority);
                    // Debug.Log($"[NetworkPistolBehaviour] Dealt {damage} damage to {playerData.PlayerName}");
                }
                else
                {
                    // Debug.Log($"[NetworkPistolBehaviour] Cannot shoot yourself! Ignoring hit.");
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
            // Debug.Log($"[NetworkPistolBehaviour] Magazine already full!");
            return;
        }

        if (ReserveAmmo <= 0)
        {
            // Debug.Log($"[NetworkPistolBehaviour] No reserve ammo!");
            return;
        }

        IsReloading = true;
        ReloadTimer = TickTimer.CreateFromSeconds(Runner, reloadTime);
        RPC_OnReloadStart();
        // Debug.Log($"[NetworkPistolBehaviour] Reloading... Player {Object.InputAuthority}");
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

        // Debug.Log($"[NetworkPistolBehaviour] Reload complete! Ammo: {CurrentAmmo}/{maxAmmo}, Reserve: {ReserveAmmo}");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnShot(Vector3 origin, Vector3 endPoint, bool didHit, Vector3 hitPoint, Vector3 hitNormal)
    {
        // Debug.Log($"[NetworkPistolBehaviour] RPC_OnShot called!");
        
        // Ensure we have the correct active firePoint for visual effects
        if (firePoint == null || !firePoint.gameObject.activeInHierarchy)
        {
            FindActiveFirePoint();
        }
        
        // Try to use new muzzle flash particle system first
        if (muzzleFlashPrefab != null && firePoint != null)
        {
            GameObject tempMuzzleFlash = Instantiate(muzzleFlashPrefab, firePoint.position, firePoint.rotation);
            tempMuzzleFlash.transform.SetParent(firePoint);
            
            // CRITICAL: Ensure the GameObject is active!
            tempMuzzleFlash.SetActive(true);
            // Debug.Log($"[NetworkPistolBehaviour] Muzzle flash GameObject activated: {tempMuzzleFlash.activeInHierarchy}");
            
            var muzzleEffect = tempMuzzleFlash.GetComponent<MuzzleFlashEffect>();
            if (muzzleEffect != null)
            {
                muzzleEffect.SetContinuousMode(false); // Single burst mode for pistol
                muzzleEffect.Play();
                // Debug.Log("[NetworkPistolBehaviour] *** PISTOL MUZZLE FLASH PARTICLE EFFECT PLAYED ***");
                
                // Auto-destroy after effect
                Destroy(tempMuzzleFlash, 0.3f);
            }
            else
            {
                // Debug.LogWarning("[NetworkPistolBehaviour] MuzzleFlashEffect component not found on muzzle flash prefab!");
                Destroy(tempMuzzleFlash, 0.3f);
            }
        }
        else
        {
            // Debug.LogWarning("[NetworkPistolBehaviour] No muzzle flash prefab assigned!");
        }

        if (shootSound != null)
        {
            AudioSource.PlayClipAtPoint(shootSound, origin, soundVolume);
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
        if (reloadSound != null)
        {
            AudioSource.PlayClipAtPoint(reloadSound, transform.position, soundVolume);
        }
    }

    public void AddAmmo(int amount)
    {
        if (!Object.HasStateAuthority) return;
        ReserveAmmo += amount;
    }
}
