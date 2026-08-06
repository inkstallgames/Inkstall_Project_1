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
    [SerializeField] private int maxEnergy = 20;
    [SerializeField] private int currentEnergy = 20;
    [SerializeField] private int energyPerShot = 1; // Consume 1 energy per shot
    [SerializeField] private float reloadTime = 2f; // Reload time when energy reaches zero
    [Tooltip("Unlimited energy system - no reserve energy needed")]

    [Header("Effects")]
    [SerializeField] private GameObject laserBeamPrefab;
    [SerializeField] private GameObject laserImpactPrefab;
    [SerializeField] private GameObject muzzleFlashPrefab;
    [SerializeField] private AudioClip laserShootSound;
    [SerializeField] private AudioClip laserOverheatSound;
    [SerializeField] private AudioClip laserReloadSound;
    [SerializeField] private float soundVolume = 1.0f;

    [Networked] public int CurrentEnergy { get; set; }
    [Networked] private TickTimer FireCooldownTimer { get; set; }
    [Networked] private TickTimer ReloadTimer { get; set; }
    [Networked] public bool IsReloading { get; set; }
    [Networked] private TickTimer PostReloadCooldown { get; set; }
    [Networked] public NetworkBool HasDamageIncreasePowerup { get; set; }
    
    public int MaxEnergy => maxEnergy;

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
    private bool wantsToShoot;
    private bool wantsToReload;
    private bool isShootingContinuously; // Track continuous firing state
    private NetworkWeaponEquipSystem equipSystem;
    private PlayerNetworkData playerData;
    private float lastShotTime;
    private bool isLocalPlayer => Object != null && Object.HasInputAuthority;
    
    // Decoupled Visual State NetworkBool
    [Networked] public NetworkBool IsFiringLaser { get; set; }
    private bool _lastIsFiringLaser;
    private float _nextVisualRaycastTime;
    private bool _cachedBeamDidHit;
    private Vector3 _cachedBeamHitPoint;
    private Vector3 _cachedBeamHitNormal;
    [Tooltip("How often the visual laser raycasts. Damage still runs on the network tick.")]
    [SerializeField] private float visualRaycastInterval = 0.05f;
    
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
    private ParticleSystem _cachedImpactParticles;
    
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
        if (fp == null || PlayerCamera == null) return;

        Vector3 beamStart    = fp.position;
        Vector3 camOrigin    = PlayerCamera.transform.position;
        Vector3 direction    = PlayerCamera.transform.forward;

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
        wantsToShoot = true;
    }

    public void StopShooting()
    {
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
        // Set shooting state based on wantsToShoot (frame-persistent)
        if (wantsToShoot)
        {
            isShootingContinuously = true;
        }
        // Only reset if wantsToShoot is false AND we were previously shooting
        else if (!wantsToShoot && isShootingContinuously)
        {
            isShootingContinuously = false;
        }
        
        inputData.isShooting = isShootingContinuously;
        inputData.isReloading = wantsToReload;
        wantsToReload = false;
        
        if (PlayerCamera != null)
        {
            inputData.aimDirection = PlayerCamera.transform.forward;
            inputData.aimOrigin = PlayerCamera.transform.position;
        }
        
        // DON'T reset wantsToShoot here - let it persist until explicitly cleared
        // wantsToShoot = false; // REMOVED THIS LINE
    }

    public override void FixedUpdateNetwork()
    {
        // Process reload completion first (independent of client input!)
        if (IsReloading && ReloadTimer.ExpiredOrNotRunning(Runner))
        {
            if (Object.HasStateAuthority)
            {
                // Unlimited energy - always reload to full capacity
                CurrentEnergy = maxEnergy;
                IsReloading = false;
                ReloadTimer = TickTimer.None;
                
                // Add a small cooldown after reload to prevent immediate firing
                PostReloadCooldown = TickTimer.CreateFromSeconds(Runner, 0.1f);
                
                RPC_UpdateEnergy(CurrentEnergy);
            }
        }

        if (!GetInput<StarterAssets.NetworkInputData>(out var input))
        {
            return;
        }

        // Process manual reload request (from UI button or R key) - unlimited energy
        if (input.isReloading && !IsReloading && CurrentEnergy < maxEnergy)
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
        
        
        // Auto-reload when empty for unlimited energy system
        if (!input.isShooting && CurrentEnergy <= 0 && !IsReloading)
        {
            if (Object.HasStateAuthority)
            {
                IsReloading = true;
                ReloadTimer = TickTimer.CreateFromSeconds(Runner, reloadTime);
                RPC_PlayReloadSound();
            }
        }
        else if (input.isShooting)
        {
            // Energy regeneration disabled for unlimited system
            // No regen timer needed since we use reload system
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
            
            // Auto-reload when out of energy (unlimited system)
            IsReloading = true;
            ReloadTimer = TickTimer.CreateFromSeconds(Runner, reloadTime);
            
            // Play reload start sound using a GameObject so we can stop it later
            if (laserReloadSound != null)
            {
                RPC_PlayReloadSound();
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
                    int actualDamage = HasDamageIncreasePowerup ? damage * 2 : damage;
                    targetPlayerData.RPC_TakeDamage(actualDamage, Object.InputAuthority, true, "Laser");
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
        }

        RPC_UpdateEnergy(CurrentEnergy);
    }



    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdateEnergy(int newEnergy)
    {
        CurrentEnergy = newEnergy;
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayReloadSound()
    {
        // Only play reload sound for the local player who is reloading
        // Remote players should NOT hear reload sounds (private action)
        if (laserReloadSound != null && Object.HasInputAuthority)
        {
            // Local player only: 2D centered sound for immediate feedback
            GameObject tempAudioObject = new GameObject("TempReloadSound");
            AudioSource tempAudioSource = tempAudioObject.AddComponent<AudioSource>();
            
            tempAudioSource.clip = laserReloadSound;
            tempAudioSource.volume = soundVolume;
            tempAudioSource.spatialBlend = 0f; // 2D sound
            tempAudioSource.playOnAwake = false;
            tempAudioSource.Play();
            
            Destroy(tempAudioObject, laserReloadSound.length + 0.1f);
        }
        // Remote players: NO SOUND - reload is private
    }

    public void AddEnergy(int amount)
    {
        if (!Object.HasStateAuthority) return;
        // Unlimited energy system - just reload to full capacity
        CurrentEnergy = maxEnergy;
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
        
        RPC_UpdateEnergy(CurrentEnergy);
    }

    private System.Collections.IEnumerator ShowMuzzleFlash()
    {
        
        Transform fp = ActiveFirePoint;
        if (muzzleFlashPrefab != null && fp != null)
        {
            // Create temporary muzzle flash for single shot
            GameObject tempMuzzleFlash = Instantiate(muzzleFlashPrefab, fp.position, fp.rotation);
            tempMuzzleFlash.transform.SetParent(fp);
            
            // CRITICAL: Ensure the GameObject is active!
            tempMuzzleFlash.SetActive(true);
            
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
        _nextVisualRaycastTime = 0f;
        _cachedBeamDidHit = false;
        if (continuousBeam != null)
        {
            return;
        }

        Transform fp = ActiveFirePoint;
        // Local player uses camera forward for crosshair accuracy.
        // Remote player reads the networked aim direction written by the shooter — the only reliable source.
        Vector3 direction = isLocalPlayer
            ? (PlayerCamera != null ? PlayerCamera.transform.forward : Vector3.forward)
            : (NetworkedAimDirection != Vector3.zero ? NetworkedAimDirection : transform.forward);
        Vector3 origin = isLocalPlayer
            ? (PlayerCamera != null ? PlayerCamera.transform.position : transform.position)
            : (NetworkedAimOrigin != Vector3.zero ? NetworkedAimOrigin : transform.position);

        // Always start the beam at the gun barrel so it visually fires from the gun.
        Vector3 beamStart = fp != null ? fp.position : transform.position;

        
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
            else // LaserBeamEffect not found on beam prefab!

            if (continuousBeam != null)
            {
                // Always world space — positions updated every pre-render via RenderPipelineManager
                continuousBeam.useWorldSpace = true;
                continuousBeam.positionCount = 2;
                continuousBeam.SetPosition(0, beamStart);
                continuousBeam.SetPosition(1, beamStart + direction * 100f);
                continuousBeam.enabled = true;
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
            }
            
            // Get the particle system and make sure it plays
            _cachedImpactParticles = continuousImpact.GetComponent<ParticleSystem>();
            if (_cachedImpactParticles != null)
            {
                var main = _cachedImpactParticles.main;
                main.loop = true; // Ensure looping for continuous mode
                _cachedImpactParticles.Play();
            }
            else
            {
                // ParticleSystem component not found on impact prefab
            }
            
        }
        else if (!didHit)
        {
            // NO HIT DETECTED - Impact effect not created
        }
        else if (laserImpactPrefab == null)
        {
            // LASER IMPACT PREFAB IS NULL - Assign it in Inspector!
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
        
        // Industry-standard continuous laser sound with 3D spatial audio
        if (laserShootSound != null && continuousLaserAudio != null)
        {
            continuousLaserAudio.clip = laserShootSound;
            
            // Configure based on player type (industry standard)
            if (isLocalPlayer)
            {
                // Local player: 2D centered sound for own weapon
                continuousLaserAudio.spatialBlend = 0f;
                continuousLaserAudio.volume = soundVolume;
                continuousLaserAudio.rolloffMode = AudioRolloffMode.Linear;
                continuousLaserAudio.minDistance = 0f;
                continuousLaserAudio.maxDistance = 0f;
            }
            else
            {
                // Remote player: Full 3D spatial audio
                continuousLaserAudio.spatialBlend = 1f;
                continuousLaserAudio.volume = soundVolume;
                continuousLaserAudio.rolloffMode = AudioRolloffMode.Logarithmic;
                continuousLaserAudio.minDistance = 1f;
                continuousLaserAudio.maxDistance = 50f;
                continuousLaserAudio.dopplerLevel = 0f;
            }
            
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
            aimOrigin = PlayerCamera != null ? PlayerCamera.transform.position : transform.position;
            aimDir    = PlayerCamera != null ? PlayerCamera.transform.forward  : transform.forward;
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
        bool didHit;
        Vector3 hitPoint;
        Vector3 hitNormal;

        // Throttle visual raycasts — beam position still updates every frame from the cache.
        if (Time.unscaledTime >= _nextVisualRaycastTime)
        {
            _nextVisualRaycastTime = Time.unscaledTime + Mathf.Max(0.02f, visualRaycastInterval);
            didHit = Physics.Raycast(aimOrigin, aimDir, out hit, range, hitLayers);
            hitPoint = didHit ? hit.point : aimOrigin + aimDir * range;
            hitNormal = didHit ? hit.normal : -aimDir;
            _cachedBeamDidHit = didHit;
            _cachedBeamHitPoint = hitPoint;
            _cachedBeamHitNormal = hitNormal;
        }
        else
        {
            didHit = _cachedBeamDidHit;
            hitPoint = didHit ? _cachedBeamHitPoint : aimOrigin + aimDir * range;
            hitNormal = _cachedBeamHitNormal;
        }

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
                continuousImpact.transform.rotation = Quaternion.LookRotation(hitNormal);
                if (_cachedImpactParticles != null && !_cachedImpactParticles.isPlaying)
                    _cachedImpactParticles.Play();
            }
        }
        else if (didHit && laserImpactPrefab != null)
        {
            // Lazily create impact if it wasn't created during StartContinuousBeam
            // (e.g. initial raycast missed because player was looking at the sky)
            continuousImpact = Instantiate(laserImpactPrefab, hitPoint, Quaternion.LookRotation(hitNormal));
            continuousImpact.transform.SetParent(transform);
            continuousImpact.SetActive(true);

            var impactEffect = continuousImpact.GetComponent<LaserImpactEffect>();
            if (impactEffect != null) impactEffect.SetContinuousMode(true);

            _cachedImpactParticles = continuousImpact.GetComponent<ParticleSystem>();
            if (_cachedImpactParticles != null)
            {
                var main = _cachedImpactParticles.main;
                main.loop = true;
                _cachedImpactParticles.Play();
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
            _cachedImpactParticles = null;
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
            StartContinuousBeam();
        }
        else if (!IsFiringLaser && _lastIsFiringLaser)
        {
            StopContinuousBeam();
        }

        _lastIsFiringLaser = IsFiringLaser;
    }

    private void LateUpdate()
    {
        // Safety check: don't access networked properties before Spawned() is called
        if (Object == null || !Object.IsValid) return;
        
        // Single visual update per frame, after Cinemachine positions the camera.
        if (IsFiringLaser)
        {
            UpdateContinuousBeam();
        }
    }
}
