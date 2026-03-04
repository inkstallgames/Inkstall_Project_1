using Fusion;
using UnityEngine;

/// <summary>
/// Multiplayer pistol shooting behaviour using raycast.
/// Attach to each player prefab alongside NetworkBombBehaviour.
/// </summary>
public class NetworkPistolBehaviour : NetworkBehaviour
{
    [Header("Pistol Settings")]
    [SerializeField] private Transform firePoint;
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
    [SerializeField] private GameObject muzzleFlashPlaceholder;
    [SerializeField] private ParticleSystem muzzleFlash;
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

        // Ensure muzzle flash placeholder is disabled on start
        if (muzzleFlashPlaceholder != null)
        {
            muzzleFlashPlaceholder.SetActive(false);
        }
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
            Debug.Log("[NetworkPistolBehaviour] TryShoot — skipped, pistol not equipped.");
            return;
        }

        if (!FireCooldownTimer.ExpiredOrNotRunning(Runner))
        {
            return;
        }

        if (CurrentAmmo <= 0)
        {
            Debug.Log($"[NetworkPistolBehaviour] Out of ammo! Player {Object.InputAuthority}");
            return;
        }

        CurrentAmmo--;
        FireCooldownTimer = TickTimer.CreateFromSeconds(Runner, fireRate);
        
        Debug.Log($"[NetworkPistolBehaviour] SHOT FIRED! Player {Object.InputAuthority.PlayerId} | Ammo: {CurrentAmmo}/{maxAmmo} | Direction: {direction}");

        Vector3 shootOrigin = firePoint != null ? firePoint.position : origin;
        
        if (Physics.Raycast(shootOrigin, direction, out RaycastHit hit, range, hitLayers))
        {
            Debug.Log($"[NetworkPistolBehaviour] Hit: {hit.collider.name} at distance {hit.distance}");

            var playerData = hit.collider.GetComponentInParent<PlayerNetworkData>();
            if (playerData != null)
            {
                Debug.Log($"[NetworkPistolBehaviour] *** RAYCAST HIT PLAYER *** Target: {playerData.PlayerName} (ID:{playerData.Object.InputAuthority.PlayerId}) | Shooter: Player {Object.InputAuthority.PlayerId}");
                
                if (playerData.Object.InputAuthority != Object.InputAuthority)
                {
                    playerData.RPC_TakeDamage(damage, Object.InputAuthority);
                    Debug.Log($"[NetworkPistolBehaviour] Dealt {damage} damage to {playerData.PlayerName}");
                }
                else
                {
                    Debug.Log($"[NetworkPistolBehaviour] Cannot shoot yourself! Ignoring hit.");
                }
            }

            RPC_OnShot(shootOrigin, hit.point, true, hit.point, hit.normal);
        }
        else
        {
            Vector3 endPoint = shootOrigin + direction * range;
            RPC_OnShot(shootOrigin, endPoint, false, Vector3.zero, Vector3.zero);
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
            Debug.Log($"[NetworkPistolBehaviour] Magazine already full!");
            return;
        }

        if (ReserveAmmo <= 0)
        {
            Debug.Log($"[NetworkPistolBehaviour] No reserve ammo!");
            return;
        }

        IsReloading = true;
        ReloadTimer = TickTimer.CreateFromSeconds(Runner, reloadTime);
        RPC_OnReloadStart();
        Debug.Log($"[NetworkPistolBehaviour] Reloading... Player {Object.InputAuthority}");
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

        Debug.Log($"[NetworkPistolBehaviour] Reload complete! Ammo: {CurrentAmmo}/{maxAmmo}, Reserve: {ReserveAmmo}");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnShot(Vector3 origin, Vector3 endPoint, bool didHit, Vector3 hitPoint, Vector3 hitNormal)
    {
        Debug.Log($"[NetworkPistolBehaviour] RPC_OnShot called! MuzzleFlashPlaceholder is {(muzzleFlashPlaceholder != null ? "ASSIGNED" : "NULL")}");
        
        // Show muzzle flash placeholder
        if (muzzleFlashPlaceholder != null)
        {
            Debug.Log($"[NetworkPistolBehaviour] Starting muzzle flash coroutine");
            StartCoroutine(ShowMuzzleFlash());
        }
        else
        {
            Debug.LogWarning($"[NetworkPistolBehaviour] MuzzleFlashPlaceholder is NULL! Assign it in the Inspector.");
        }
        
        if (muzzleFlash != null)
        {
            muzzleFlash.Play();
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

    private System.Collections.IEnumerator ShowMuzzleFlash()
    {
        Debug.Log($"[NetworkPistolBehaviour] ShowMuzzleFlash coroutine started");
        Debug.Log($"[NetworkPistolBehaviour] Placeholder active in hierarchy: {muzzleFlashPlaceholder.activeInHierarchy}");
        Debug.Log($"[NetworkPistolBehaviour] Placeholder self active: {muzzleFlashPlaceholder.activeSelf}");
        
        muzzleFlashPlaceholder.SetActive(true);
        Debug.Log($"[NetworkPistolBehaviour] Muzzle flash ENABLED - activeSelf: {muzzleFlashPlaceholder.activeSelf}, activeInHierarchy: {muzzleFlashPlaceholder.activeInHierarchy}");
        
        yield return new WaitForSeconds(0.05f);
        
        muzzleFlashPlaceholder.SetActive(false);
        Debug.Log($"[NetworkPistolBehaviour] Muzzle flash DISABLED");
    }
}
