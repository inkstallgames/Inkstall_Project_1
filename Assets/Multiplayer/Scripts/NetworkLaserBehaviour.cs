using Fusion;
using UnityEngine;
using UnityEngine.Rendering;

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
    [SerializeField] private int maxEnergy = 30;
    [SerializeField] private int currentEnergy = 30;
    [SerializeField] private int reserveEnergy = 90;
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
    [SerializeField] private AudioClip laserReloadSound;
    [SerializeField] private float soundVolume = 1.0f;

    [Networked] public int CurrentEnergy { get; set; }
    [Networked] public int ReserveEnergy { get; set; }
    [Networked] private TickTimer FireCooldownTimer { get; set; }
    [Networked] private TickTimer EnergyRegenTimer { get; set; }
    [Networked] private TickTimer ReloadTimer { get; set; }
    [Networked] public bool IsReloading { get; set; }
    [Networked] private TickTimer PostReloadCooldown { get; set; }
    
    public int MaxEnergy => maxEnergy;

    private Camera playerCamera;
    private bool wantsToShoot;
    private bool wantsToReload;
    private bool isShootingContinuously; // Track continuous firing state
    private NetworkWeaponEquipSystem equipSystem;
    private PlayerNetworkData playerData;
    private float lastShotTime;
    private bool isLocalPlayer;
    
    // Decoupled Visual State NetworkBool
    [Networked] public NetworkBool IsFiringLaser { get; set; }
    private bool _lastIsFiringLaser;
    
    // Networked aim state so remote clients can draw the beam correctly.
    // Set every FixedUpdateNetwork tick from the shooter's camera input.
    // OPTIMIZED: Only update when direction changes significantly
    [Networked] public Vector3 NetworkedAimOrigin { get; set; }
    [Networked] public Vector3 NetworkedAimDirection { get; set; }
    
    // Optimization: Track last direction to reduce network updates
    private Vector3 _lastNetworkedAimDirection;
    
    // Network optimization
    private float _lastNetworkUpdateTime = 0f;
    private const float NETWORK_UPDATE_INTERVAL = 0.02f; // 50Hz updates
    
    // Continuous beam visual references
    private LineRenderer continuousBeam;
    private GameObject continuousImpact;
    private GameObject continuousMuzzleFlash;
    
    // Track beam destruction
    private int beamDestructionCount = 0;
    
    // HandsCamera reference for pre-render position update
    private Camera _handsCamera;
    
    // Continuous sound management
    private AudioSource continuousLaserAudio;
    
    // Reload sound management
    private AudioSource reloadAudioSource;

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
            ReserveEnergy = reserveEnergy;
        }

        isLocalPlayer = Object.HasInputAuthority;

        if (isLocalPlayer)
        {
            playerCamera = Camera.main;
        }

        equipSystem = GetComponent<NetworkWeaponEquipSystem>();
        playerData = GetComponent<PlayerNetworkData>();
        
        // Force energy per shot to override Inspector values
        energyPerShot = 1;

        // Initialize continuous audio source
        continuousLaserAudio = gameObject.AddComponent<AudioSource>();
        continuousLaserAudio.loop = true;
        continuousLaserAudio.volume = soundVolume;
        continuousLaserAudio.playOnAwake = false;

        // Ensure muzzle flash is disabled on start
        if (muzzleFlashPrefab != null)
        {
            muzzleFlashPrefab.SetActive(false);
        }
        
        Debug.Log($"[NetworkLaserBehaviour] Spawned | Player: {(isLocalPlayer ? "LOCAL" : "REMOTE")} | ArmFirePoint: {(armFirePoint != null ? armFirePoint.name : "NULL")} | BodyFirePoint: {(bodyFirePoint != null ? bodyFirePoint.name : "NULL")}");
    }

    private void OnEnable()
    {
        // Subscribe to URP pre-render callback.
        // This fires right before each camera renders, AFTER all LateUpdates (including
        // Cinemachine). Using this hook guarantees beam positions reflect the camera's
        // FINAL position for the frame — no timing lag possible.
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    /// <summary>
    /// Called by URP right before each camera renders — after Cinemachine LateUpdate.
    /// We only care about HandsCamera (the one that renders the FPS beam for local player).
    /// </summary>
    private void OnBeginCameraRendering(ScriptableRenderContext ctx, Camera cam)
    {
        if (!isLocalPlayer || !IsFiringLaser || continuousBeam == null) return;

        // Lazy-find HandsCamera by tag the first time
        if (_handsCamera == null)
        {
            var go = GameObject.FindWithTag("HandCamera");
            if (go != null) _handsCamera = go.GetComponent<Camera>();
        }

        // Only update when HandsCamera is about to render — all transforms are final at this point
        if (_handsCamera == null || cam != _handsCamera) return;

        Transform fp = ActiveFirePoint;
        if (fp == null || playerCamera == null) return;

        Vector3 beamStart    = fp.position;
        Vector3 camOrigin    = playerCamera.transform.position;
        Vector3 direction    = playerCamera.transform.forward;

        RaycastHit hit;
        bool didHit = Physics.Raycast(camOrigin, direction, out hit, range, hitLayers);
        Vector3 hitPoint = didHit ? hit.point : camOrigin + direction * range;

        continuousBeam.SetPosition(0, beamStart);
        continuousBeam.SetPosition(1, hitPoint);

        // Keep impact marker in sync
        if (continuousImpact != null)
        {
            continuousImpact.SetActive(didHit);
            if (didHit)
            {
                continuousImpact.transform.position = hitPoint;
                continuousImpact.transform.rotation = Quaternion.LookRotation(hit.normal);
            }
        }
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

    /// <summary>
    /// Manually request a reload (e.g. from the UI reload button or R key).
    /// Only starts a reload if the laser is not already reloading, has room in the magazine, and has reserve energy.
    /// </summary>
    public void RequestReload()
    {
        wantsToReload = true;
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
        inputData.isReloading = wantsToReload;
        wantsToReload = false;
        
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

        // Process reload completion first
        if (IsReloading && ReloadTimer.ExpiredOrNotRunning(Runner))
        {
            if (Object.HasStateAuthority)
            {
                int energyNeeded = maxEnergy - CurrentEnergy;
                int energyToReload = Mathf.Min(energyNeeded, ReserveEnergy);

                CurrentEnergy += energyToReload;
                ReserveEnergy -= energyToReload;
                IsReloading = false;
                ReloadTimer = TickTimer.None;
                
                // Add a small cooldown after reload to prevent immediate firing
                PostReloadCooldown = TickTimer.CreateFromSeconds(Runner, 0.1f);
                
                RPC_UpdateEnergy(CurrentEnergy, ReserveEnergy);
            }
        }

        // Process manual reload request (from UI button or R key)
        if (input.isReloading && !IsReloading && CurrentEnergy < maxEnergy && ReserveEnergy > 0)
        {
            if (Object.HasStateAuthority)
            {
                IsReloading = true;
                ReloadTimer = TickTimer.CreateFromSeconds(Runner, reloadTime);

                if (laserReloadSound != null)
                {
                    RPC_PlayReloadSound();
                }
            }
        }
        
        
        // Regenerate energy when not shooting
        if (!input.isShooting && CurrentEnergy < maxEnergy)
        {
            if (IsReloading)
            {
                // Still reloading - don't allow normal regen
                float remainingTime = ReloadTimer.RemainingTime(Runner) ?? 0f;
                // Debug.Log($"[NetworkLaserBehaviour] *** RELOADING... *** Player {Object.InputAuthority.PlayerId} | {remainingTime:F1}s remaining");
                return;
            }
            
            if (EnergyRegenTimer.ExpiredOrNotRunning(Runner) && ReserveEnergy > 0)
            {
                if (Object.HasStateAuthority)
                {
                    int oldEnergy = CurrentEnergy;
                    int maxRegen = Mathf.RoundToInt(energyRegenRate * Runner.DeltaTime);
                    int actualRegen = Mathf.Min(maxEnergy - CurrentEnergy, Mathf.Min(maxRegen, ReserveEnergy));

                    CurrentEnergy += actualRegen;
                    ReserveEnergy -= actualRegen;
                    
                    RPC_UpdateEnergy(CurrentEnergy, ReserveEnergy);
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

        // Check if we can fire - player must NOT be reloading and post-reload cooldown must be expired
        bool canFire = input.isShooting && CurrentEnergy > 0 && !IsReloading && PostReloadCooldown.ExpiredOrNotRunning(Runner) && equipSystem != null && equipSystem.IsLaserEquipped();
        
        // Stop reload sound if player is trying to fire (reload is complete but sound might still be playing)
        if (input.isShooting && !IsReloading && reloadAudioSource != null && reloadAudioSource.isPlaying)
        {
            reloadAudioSource.Stop();
            Destroy(reloadAudioSource.gameObject);
            reloadAudioSource = null;
        }

        if (canFire)
        {
            IsFiringLaser = true;

            // Always keep networked aim in sync while firing (state authority writes, all clients read)
            if (Object.HasStateAuthority)
            {
                // OPTIMIZATION: Only update networked aim when direction changes significantly
                float directionThreshold = 0.01f; // Only update if direction changes by 1%
                bool directionChanged = Vector3.Distance(_lastNetworkedAimDirection, input.aimDirection) > directionThreshold;
                
                // Rate limit network updates to reduce bandwidth
                float currentTime = Time.time;
                if ((currentTime - _lastNetworkUpdateTime >= NETWORK_UPDATE_INTERVAL) || directionChanged)
                {
                    NetworkedAimOrigin    = input.aimOrigin;
                    NetworkedAimDirection = input.aimDirection;
                    _lastNetworkedAimDirection = input.aimDirection;
                    _lastNetworkUpdateTime = currentTime;
                }
                TryShootAuthority(input.aimOrigin, input.aimDirection);
            }
        }
        else
        {
            IsFiringLaser = false;
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
            
            // Only start reload if we have reserve energy
            if (ReserveEnergy > 0)
            {
                IsReloading = true;
                ReloadTimer = TickTimer.CreateFromSeconds(Runner, reloadTime);
                
                // Play reload start sound using a GameObject so we can stop it later
                if (laserReloadSound != null)
                {
                    RPC_PlayReloadSound();
                }
            }
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
                    // Pass true for isLaserDamage to play laser hit sound
                    targetPlayerData.RPC_TakeDamage(damage, Object.InputAuthority, true);
                }
            }
        }

        RPC_UpdateEnergy(CurrentEnergy, ReserveEnergy);
    }



    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdateEnergy(int newEnergy, int newReserve)
    {
        CurrentEnergy = newEnergy;
        ReserveEnergy = newReserve;
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayReloadSound()
    {
        // Clean up any existing reload sound
        if (reloadAudioSource != null)
        {
            Destroy(reloadAudioSource.gameObject);
            reloadAudioSource = null;
        }
        
        if (laserReloadSound != null)
        {
            // Create a GameObject with AudioSource so we can stop it later if needed
            GameObject reloadSoundObj = new GameObject("LaserReloadSound");
            reloadAudioSource = reloadSoundObj.AddComponent<AudioSource>();
            
            reloadAudioSource.clip = laserReloadSound;
            reloadAudioSource.volume = soundVolume;
            reloadAudioSource.spatialBlend = 0f; // 2D sound
            reloadAudioSource.playOnAwake = false;
            reloadAudioSource.Play();
            
            // Auto-destroy after sound finishes (if not stopped earlier)
            Destroy(reloadSoundObj, laserReloadSound.length + 0.1f);
        }
    }

    public void AddEnergy(int amount)
    {
        if (!Object.HasStateAuthority) return;
        ReserveEnergy += amount;
        RPC_UpdateEnergy(CurrentEnergy, ReserveEnergy);
    }

    /// <summary>
    /// Resets laser energy to full when player gets a kill.
    /// Called by NetworkGameManager on kill.
    /// </summary>
    public void ResetEnergyOnKill()
    {
        if (!Object.HasStateAuthority) return;
        
        CurrentEnergy = maxEnergy;
        ReserveEnergy = Mathf.Min(reserveEnergy, 150);
        IsReloading = false;
        ReloadTimer = TickTimer.None;
        
        Debug.Log($"[NetworkLaserBehaviour] Energy reset on kill - Energy: {CurrentEnergy}/{maxEnergy}, Reserve: {ReserveEnergy}");
        RPC_UpdateEnergy(CurrentEnergy, ReserveEnergy);
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
    
    /// <summary>Sets the layer on a GameObject and all its children recursively.</summary>
    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
    
    private void StartContinuousBeam()
    {
        if (continuousBeam != null)
        {
            Debug.Log($"[NetworkLaserBehaviour] *** BEAM ALREADY EXISTS *** Skipping creation");
            return;
        }

        Transform fp = ActiveFirePoint;
        // Local player uses camera forward for crosshair accuracy.
        // Remote player reads the networked aim direction written by the shooter — the only reliable source.
        Vector3 direction = isLocalPlayer
            ? (playerCamera != null ? playerCamera.transform.forward : Vector3.forward)
            : (NetworkedAimDirection != Vector3.zero ? NetworkedAimDirection : transform.forward);
        Vector3 origin = isLocalPlayer
            ? (playerCamera != null ? playerCamera.transform.position : transform.position)
            : (NetworkedAimOrigin != Vector3.zero ? NetworkedAimOrigin : transform.position);

        // Always start the beam at the gun barrel so it visually fires from the gun.
        Vector3 beamStart = fp != null ? fp.position : transform.position;

        Debug.Log($"[NetworkLaserBehaviour] *** CREATING BEAM *** Player: {(isLocalPlayer ? "LOCAL" : "REMOTE")} | BeamStart: {beamStart} | Direction: {direction}");
        
        // Create continuous beam
        if (laserBeamPrefab != null)
        {
            GameObject beamObj = Instantiate(laserBeamPrefab, beamStart, Quaternion.LookRotation(direction));
            beamObj.transform.SetParent(transform);
            continuousBeam = beamObj.GetComponent<LineRenderer>();

            // Local player: put beam on FPS_Hands layer so HandsCamera renders it
            if (isLocalPlayer)
            {
                int fpsHandsLayer = LayerMask.NameToLayer("FPS_Hands");
                if (fpsHandsLayer != -1) SetLayerRecursively(beamObj, fpsHandsLayer);
            }

            var beamEffect = beamObj.GetComponent<LaserBeamEffect>();
            if (beamEffect != null) beamEffect.SetContinuousMode(true);
            else Debug.LogWarning("[NetworkLaserBehaviour] LaserBeamEffect not found on beam prefab!");

            if (continuousBeam != null)
            {
                // Always world space — positions updated every pre-render via RenderPipelineManager
                continuousBeam.useWorldSpace = true;
                continuousBeam.positionCount = 2;
                continuousBeam.SetPosition(0, beamStart);
                continuousBeam.SetPosition(1, beamStart + direction * 100f);
                continuousBeam.enabled = true;
                Debug.Log($"[NetworkLaserBehaviour] *** BEAM CREATED *** Player: {(isLocalPlayer ? "LOCAL" : "REMOTE")}");
            }
            else
            {
                Debug.LogError("[NetworkLaserBehaviour] LineRenderer not found on beam prefab!");
                Destroy(beamObj);
                return;
            }
        }
        else
        {
            Debug.LogError($"[NetworkLaserBehaviour] *** BEAM CREATION FAILED *** laserBeamPrefab is NULL!");
        }
        
        // Hit logic: raycast using the correct origin+direction for this player type
        RaycastHit visualHit;
        bool didHit = Physics.Raycast(origin, direction, out visualHit, range, hitLayers);

        // Create continuous impact
        if (didHit && laserImpactPrefab != null)
        {
            continuousImpact = Instantiate(laserImpactPrefab, visualHit.point, Quaternion.LookRotation(visualHit.normal));
            continuousImpact.transform.SetParent(transform);
            
            // CRITICAL: Ensure the impact GameObject is active
            continuousImpact.SetActive(true);
            
            // NOTE: Impact stays on Default layer for ALL players.
            // Unlike the beam/muzzle flash (which are near the gun barrel and need FPS_Hands
            // layer for the HandsCamera), the impact spawns at a distant world-space hit point.
            // Putting it on FPS_Hands would make it invisible — the HandsCamera's near/far clip
            // can't reach it, and the main camera culls FPS_Hands.
            
            // Set continuous mode for impact particles (if LaserImpactEffect component exists)
            var impactEffect = continuousImpact.GetComponent<LaserImpactEffect>();
            if (impactEffect != null)
            {
                impactEffect.SetContinuousMode(true);
                Debug.Log("[NetworkLaserBehaviour] *** CONTINUOUS IMPACT MODE SET ***");
            }
            
            // Get the particle system and make sure it plays
            var impactParticles = continuousImpact.GetComponent<ParticleSystem>();
            if (impactParticles != null)
            {
                var main = impactParticles.main;
                main.loop = true; // Ensure looping for continuous mode
                impactParticles.Play();
                Debug.Log($"[NetworkLaserBehaviour] *** CONTINUOUS IMPACT PARTICLES STARTED *** IsPlaying: {impactParticles.isPlaying}");
            }
            else
            {
                Debug.LogWarning("[NetworkLaserBehaviour] *** WARNING: ParticleSystem component not found on impact prefab ***");
            }
            
            Debug.Log($"[NetworkLaserBehaviour] *** CONTINUOUS IMPACT CREATED *** Position: {visualHit.point} | Active: {continuousImpact.activeInHierarchy} | Player: {(isLocalPlayer ? "LOCAL" : "REMOTE")}");
        }
        else if (!didHit)
        {
            Debug.Log("[NetworkLaserBehaviour] *** NO HIT DETECTED - Impact effect not created ***");
        }
        else if (laserImpactPrefab == null)
        {
            Debug.LogError("[NetworkLaserBehaviour] *** LASER IMPACT PREFAB IS NULL - Assign it in Inspector! ***");
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
        
        // Start continuous laser sound
        if (laserShootSound != null && continuousLaserAudio != null)
        {
            continuousLaserAudio.clip = laserShootSound;
            continuousLaserAudio.Play();
        }
    }
    
    private void UpdateContinuousBeam()
    {
        if (continuousBeam == null)
        {
            if (!isLocalPlayer)
            {
                StartContinuousBeam();
            }
            return;
        }

        // --- Determine aim origin & direction ---
        Transform fp = ActiveFirePoint;
        Vector3 beamStart = fp != null ? fp.position : transform.position;
        Vector3 aimOrigin;
        Vector3 aimDir;

        if (isLocalPlayer)
        {
            // Local player: use camera for aim direction
            aimOrigin = playerCamera != null ? playerCamera.transform.position : transform.position;
            aimDir    = playerCamera != null ? playerCamera.transform.forward  : transform.forward;
        }
        else
        {
            // Remote player: read the networked aim direction/origin set by the shooter.
            // bodyFirePoint.forward is NOT reliable — its orientation depends on the rig.
            // playerCamera is null for remote players — we must NOT use it.
            aimOrigin = NetworkedAimOrigin    != Vector3.zero ? NetworkedAimOrigin    : beamStart;
            aimDir    = NetworkedAimDirection != Vector3.zero ? NetworkedAimDirection : transform.forward;
        }

        RaycastHit hit;
        bool didHit = Physics.Raycast(aimOrigin, aimDir, out hit, range, hitLayers);
        Vector3 hitPoint = didHit ? hit.point : aimOrigin + aimDir * range;

        // --- Update beam positions (remote only; local beam handled by pre-render hook) ---
        if (!isLocalPlayer)
        {
            continuousBeam.SetPosition(0, beamStart);
            continuousBeam.SetPosition(1, hitPoint);
            if (!continuousBeam.enabled) continuousBeam.enabled = true;
        }

        // --- Update impact (BOTH local and remote) ---
        // The impact is on Default layer so the main camera renders it.
        // It MUST be updated here (not just in the pre-render hook) because in URP
        // stacking the base camera renders BEFORE the HandsCamera overlay.
        if (continuousImpact != null)
        {
            continuousImpact.SetActive(didHit);
            if (didHit)
            {
                continuousImpact.transform.position = hitPoint;
                continuousImpact.transform.rotation = Quaternion.LookRotation(hit.normal);
                var impactParticles = continuousImpact.GetComponent<ParticleSystem>();
                if (impactParticles != null && !impactParticles.isPlaying) impactParticles.Play();
            }
        }
        else if (didHit && laserImpactPrefab != null)
        {
            // Lazily create impact if it wasn't created during StartContinuousBeam
            // (e.g. initial raycast missed because player was looking at the sky)
            continuousImpact = Instantiate(laserImpactPrefab, hitPoint, Quaternion.LookRotation(hit.normal));
            continuousImpact.transform.SetParent(transform);
            continuousImpact.SetActive(true);

            var impactEffect = continuousImpact.GetComponent<LaserImpactEffect>();
            if (impactEffect != null) impactEffect.SetContinuousMode(true);

            var impactParticles = continuousImpact.GetComponent<ParticleSystem>();
            if (impactParticles != null)
            {
                var main = impactParticles.main;
                main.loop = true;
                impactParticles.Play();
            }
        }

        // --- Update muzzle flash position (remote only; local flash parented to fp) ---
        if (!isLocalPlayer)
        {
            Transform fp2 = ActiveFirePoint;
            if (continuousMuzzleFlash != null && fp2 != null)
            {
                continuousMuzzleFlash.transform.position = fp2.position;
                continuousMuzzleFlash.transform.rotation = fp2.rotation;
            }
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
        
        // Stop continuous laser sound
        if (continuousLaserAudio != null && continuousLaserAudio.isPlaying)
        {
            continuousLaserAudio.Stop();
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

    private void LateUpdate()
    {
        // Safety check: don't access networked properties before Spawned() is called
        if (Object == null || !Object.IsValid) return;
        
        // Update beam/impact in LateUpdate — runs AFTER Cinemachine positions the camera,
        // so the local player impact will use the most accurate camera direction.
        // Local player: only impact is updated here (beam is on FPS_Hands, handled by pre-render hook).
        // Remote player: both beam and impact are updated here.
        if (IsFiringLaser && continuousBeam != null)
        {
            UpdateContinuousBeam();
        }
    }
}
