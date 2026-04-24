using UnityEngine;
using Fusion;

/// <summary>
/// Simple pistol fire animation script.
/// Handles the -3 degree recoil animation when pistol is fired.
/// Attach this to your FPS hands GameObject.
/// </summary>
public class PistolRecoilAnimation : NetworkBehaviour
{
    [Header("Pistol Fire Animation Settings")]
    [SerializeField] private bool enableRecoil = true;
    [SerializeField] private float recoilAmount = 3f;         // Rotation amount (-3 degrees)
    [SerializeField] private float recoilSpeed = 15f;        // Speed of recoil animation
    [SerializeField] private float returnSpeed = 8f;         // Speed of return to normal
    
    [Header("Reload Animation Settings")]
    [SerializeField] private bool enableReload = true;
    [SerializeField] private float reloadSpeed = 8f;         // Speed of reload animation
    [SerializeField] private float xRotationAmount = -3f;    // X rotation amount for reload (-3 degrees)
    [SerializeField] private float positionY1 = -1.8f;       // First Y position
    [SerializeField] private float positionY2 = -1.73f;      // Second Y position
    [SerializeField] private float waitAfterRotation = 0.3f; // Wait time after rotation reaches -3°
    [SerializeField] private AudioClip reloadSound;          // Reload sound clip
    [SerializeField] private float soundDelay = 0.2f;        // Delay before playing sound (0.2s)
    
    // Private variables
    private Vector3 _initialPosition;
    private Quaternion _initialRotation;
    private float _currentRecoil = 0f;
    private float _targetRecoil = 0f;
    private float _recoilVelocity = 0f;
    private bool _isRecoiling = false;
    private bool _isReturning = false;
    
    // Reload animation variables
    private Vector3 _currentReloadPosition;
    private Vector3 _targetReloadPosition;
    private Quaternion _currentReloadRotation;
    private Quaternion _targetReloadRotation;
    private Vector3 _reloadTransitionVelocity = Vector3.zero;
    private bool _isReloading = false;
    private bool _pendingReload = false; // Flag to track if reload is waiting for fire animation to complete
    private int _reloadStage = 0;  // 0: idle, 1: rotate, 2: wait, 3: pos1, 4: pos2, 5: pos1, 6: pos2, 7: restore
    private float _waitTimer = 0f;
    private bool _soundPlayed = false; // Track if sound has been played for this reload
    
    public override void Spawned()
    {
        // Store initial transform values
        _initialPosition = transform.localPosition;
        _initialRotation = transform.localRotation;
        
        // Check if reload sound is assigned
        if (reloadSound != null)
        {
            // Reload sound assigned
        }
        else
        {
            // Reload sound is NOT assigned! Please assign it in the Inspector.
        }
    }
    
    public override void Render()
    {
        if (!Object.HasInputAuthority) return;
        
        // Update animations based on priority
        if (_isReloading && enableReload)
        {
            UpdateReloadAnimation();
            ApplyReloadTransform();
        }
        else if (enableRecoil)
        {
            UpdateRecoilAnimation();
            ApplyTransform();
        }
    }
    
    private void UpdateRecoilAnimation()
    {
        if (_isRecoiling)
        {
            // Animate to target recoil position
            _currentRecoil = Mathf.SmoothDamp(_currentRecoil, _targetRecoil, ref _recoilVelocity, 1f / recoilSpeed);
            
            // Check if we've reached the target recoil position
            if (Mathf.Abs(_currentRecoil - _targetRecoil) < 0.01f && _targetRecoil < 0)
            {
                // Start returning to normal
                _targetRecoil = 0f;
                _isRecoiling = false;
                _isReturning = true;
            }
        }
        else if (_isReturning)
        {
            // Return to normal position
            _currentRecoil = Mathf.SmoothDamp(_currentRecoil, 0f, ref _recoilVelocity, 1f / returnSpeed);
            
            // Reset when close enough to zero (more forgiving)
            if (Mathf.Abs(_currentRecoil) < 0.01f) // Increased from 0.001f to 0.01f
            {
                _currentRecoil = 0f;
                _targetRecoil = 0f;
                _recoilVelocity = 0f;
                _isRecoiling = false;
                _isReturning = false;
                
                // Check if reload is pending after fire animation completes
                if (_pendingReload)
                {
                    Debug.Log("[RELOAD] Fire animation completed, starting pending reload...");
                    StartReloadAnimation();
                }
            }
        }
    }
    
    private void ApplyTransform()
    {
        // Apply rotation recoil
        Quaternion recoilRotation = _initialRotation * Quaternion.Euler(_currentRecoil, 0f, 0f);
        transform.localRotation = recoilRotation;
    }
    
    private void UpdateReloadAnimation()
    {
        switch (_reloadStage)
        {
            case 1: // Rotate X to -3° (tilt pistol down)
                _currentReloadRotation = Quaternion.Slerp(_currentReloadRotation, _targetReloadRotation, Time.deltaTime * reloadSpeed);
                Debug.Log($"[RELOAD] Stage 1 - Rotating: Current={_currentReloadRotation.eulerAngles}, Target={_targetReloadRotation.eulerAngles}, Angle={Quaternion.Angle(_currentReloadRotation, _targetReloadRotation):F1}° remaining");
                if (Quaternion.Angle(_currentReloadRotation, _targetReloadRotation) < 1f)
                {
                    _reloadStage = 2;
                    _waitTimer = 0f; // Reset wait timer
                    Debug.Log($"[RELOAD] Stage 1 Complete - Moving to Stage 2: Wait for {waitAfterRotation}s");
                }
                break;
                
            case 2: // Wait after rotation reaches -3°
                _waitTimer += Time.deltaTime;
                if (_waitTimer >= waitAfterRotation)
                {
                    _reloadStage = 3;
                    _targetReloadPosition = new Vector3(_initialPosition.x, positionY1, _initialPosition.z);
                }
                break;
                
            case 3: // Move down (magazine drops out)
                _currentReloadPosition = Vector3.SmoothDamp(_currentReloadPosition, _targetReloadPosition, ref _reloadTransitionVelocity, 1f / reloadSpeed);
                if (Vector3.Distance(_currentReloadPosition, _targetReloadPosition) < 0.01f)
                {
                    _reloadStage = 4;
                    _targetReloadPosition = new Vector3(_initialPosition.x, positionY2, _initialPosition.z);
                }
                break;
                
            case 4: // Move up (new magazine inserted)
                _currentReloadPosition = Vector3.SmoothDamp(_currentReloadPosition, _targetReloadPosition, ref _reloadTransitionVelocity, 1f / reloadSpeed);
                if (Vector3.Distance(_currentReloadPosition, _targetReloadPosition) < 0.01f)
                {
                    _reloadStage = 5;
                    _targetReloadPosition = new Vector3(_initialPosition.x, positionY1, _initialPosition.z);
                }
                break;
                
            case 5: // Move down again (magazine seats)
                _currentReloadPosition = Vector3.SmoothDamp(_currentReloadPosition, _targetReloadPosition, ref _reloadTransitionVelocity, 1f / reloadSpeed);
                if (Vector3.Distance(_currentReloadPosition, _targetReloadPosition) < 0.01f)
                {
                    _reloadStage = 6;
                    _targetReloadPosition = new Vector3(_initialPosition.x, positionY2, _initialPosition.z);
                }
                break;
                
            case 6: // Move up final (magazine fully seated)
                _currentReloadPosition = Vector3.SmoothDamp(_currentReloadPosition, _targetReloadPosition, ref _reloadTransitionVelocity, 1f / reloadSpeed);
                if (Vector3.Distance(_currentReloadPosition, _targetReloadPosition) < 0.01f)
                {
                    _reloadStage = 7;
                    _targetReloadRotation = _initialRotation; // Rotate X back to 0°
                }
                break;
                
            case 7: // Rotate X back to 0° (ready position)
                _currentReloadRotation = Quaternion.Slerp(_currentReloadRotation, _targetReloadRotation, Time.deltaTime * reloadSpeed);
                if (Quaternion.Angle(_currentReloadRotation, _targetReloadRotation) < 1f)
                {
                    _reloadStage = 0;
                    _isReloading = false;
                    _currentReloadPosition = _initialPosition;
                    _currentReloadRotation = _initialRotation;
                }
                break;
        }
    }
    
    private void ApplyReloadTransform()
    {
        transform.localRotation = _currentReloadRotation;
        transform.localPosition = _currentReloadPosition;
    }
    
    /// <summary>
    /// Check if pistol is ready to fire (recoil animation completed)
    /// </summary>
    /// <returns>True if pistol has returned to original position and can fire again</returns>
    public bool IsReadyToFire()
    {
        // Prevent firing during reload animation
        return !_isReloading;
    }
    
    /// <summary>
    /// Check if reload animation is currently playing
    /// </summary>
    /// <returns>True if pistol is currently reloading</returns>
    public bool IsReloading()
    {
        return _isReloading;
    }
    
    /// <summary>
    /// Triggers pistol reload animation with the specified sequence
    /// </summary>
    public void TriggerReloadAnimation()
    {
        if (!enableReload || _isReloading) return;
        
        // Check if fire animation is still playing, if so, wait for it to complete
        if (_isRecoiling || _isReturning)
        {
            Debug.Log("[RELOAD] Fire animation still playing, waiting for it to complete before reload...");
            _pendingReload = true; // Set flag to start reload after fire animation completes
            return;
        }
        
        // Start reload animation immediately
        StartReloadAnimation();
    }
    
    /// <summary>
    /// Actually starts the reload animation
    /// </summary>
    private void StartReloadAnimation()
    {
        // Initialize reload animation
        _isReloading = true;
        _reloadStage = 1;
        _pendingReload = false; // Clear pending flag
        _soundPlayed = false; // Reset sound flag
        
        // Set current values to initial position
        _currentReloadPosition = _initialPosition;
        _currentReloadRotation = _initialRotation;
        
        // Set first target: rotation X to -3°
        _targetReloadRotation = _initialRotation * Quaternion.Euler(xRotationAmount, 0f, 0f);
        
        // Start delayed sound playback
        StartCoroutine(PlayReloadSoundDelayed());
        
        Debug.Log($"[RELOAD] Reload animation started!");
        Debug.Log($"[RELOAD] Initial Rotation: {_initialRotation.eulerAngles}");
        Debug.Log($"[RELOAD] Target Rotation: {_targetReloadRotation.eulerAngles}");
        Debug.Log($"[RELOAD] X Rotation Amount: {xRotationAmount}");
    }
    
    /// <summary>
    /// Coroutine to play reload sound with delay
    /// </summary>
    private System.Collections.IEnumerator PlayReloadSoundDelayed()
    {
        // Wait for the specified delay
        yield return new WaitForSeconds(soundDelay);
        
        // Play reload sound after delay
        if (reloadSound != null && !_soundPlayed)
        {
            Debug.Log($"[RELOAD] Playing reload sound: {reloadSound.name}");
            
            // Create a temporary GameObject with AudioSource for 2D sound
            GameObject tempAudioObject = new GameObject("TempReloadSound");
            AudioSource tempAudioSource = tempAudioObject.AddComponent<AudioSource>();
            
            // Configure for 2D sound (equal in both ears)
            tempAudioSource.clip = reloadSound;
            tempAudioSource.volume = 1.0f;
            tempAudioSource.spatialBlend = 0f; // 0 = 2D sound, 1 = 3D sound
            tempAudioSource.playOnAwake = false;
            
            // Play the sound
            tempAudioSource.Play();
            
            // Destroy the temporary object after sound finishes
            Destroy(tempAudioObject, reloadSound.length + 0.1f);
            
            _soundPlayed = true; // Mark sound as played
        }
        else if (reloadSound == null)
        {
            Debug.LogWarning("[RELOAD] Reload sound is not assigned! Please assign it in the Inspector.");
        }
    }
    
    /// <summary>
    /// Triggers pistol fire animation - rotates hands from 0 to -3 degrees and back to 0
    /// Call this method when the player fires the pistol
    /// </summary>
    public void TriggerPistolFire()
    {
        if (!enableRecoil) return;
        
        // Check if pistol is ready to fire (prevent spamming)
        if (!IsReadyToFire())
        {
            Debug.Log("[PistolRecoilAnimation] Pistol not ready to fire - animation still in progress!");
            return;
        }
        
        // Reset and start the recoil animation
        _currentRecoil = 0f;
        _targetRecoil = -recoilAmount; // -3 degrees by default
        _recoilVelocity = 0f;
        _isRecoiling = true;
        
        Debug.Log($"[PistolRecoilAnimation] Pistol fire triggered! Rotation: 0° → {-recoilAmount}° → 0°");
    }
    
    /// <summary>
    /// Custom recoil with specific amount
    /// </summary>
    public void TriggerCustomRecoil(float customRecoilAmount)
    {
        if (!enableRecoil) return;
        
        // Check if pistol is ready to fire (prevent spamming)
        if (!IsReadyToFire())
        {
            Debug.Log("[PistolRecoilAnimation] Pistol not ready to fire - animation still in progress!");
            return;
        }
        
        _currentRecoil = 0f;
        _targetRecoil = -customRecoilAmount;
        _recoilVelocity = 0f;
        _isRecoiling = true;
        
        Debug.Log($"[PistolRecoilAnimation] Custom recoil triggered! Rotation: 0° → {-customRecoilAmount}° → 0°");
    }
    
    /// <summary>
    /// Reset to initial position and rotation
    /// </summary>
    public void ResetToInitial()
    {
        // Reset fire animation
        _currentRecoil = 0f;
        _targetRecoil = 0f;
        _recoilVelocity = 0f;
        _isRecoiling = false;
        _isReturning = false;
        
        // Reset reload animation
        _isReloading = false;
        _pendingReload = false; // Clear pending reload flag
        _soundPlayed = false; // Reset sound played flag
        _reloadStage = 0;
        _currentReloadPosition = _initialPosition;
        _targetReloadPosition = _initialPosition;
        _currentReloadRotation = _initialRotation;
        _targetReloadRotation = _initialRotation;
        _reloadTransitionVelocity = Vector3.zero;
        
        transform.localPosition = _initialPosition;
        transform.localRotation = _initialRotation;
    }
    
    private void OnValidate()
    {
        recoilAmount = Mathf.Max(0f, recoilAmount);
        recoilSpeed = Mathf.Max(0.1f, recoilSpeed);
        returnSpeed = Mathf.Max(0.1f, returnSpeed);
    }
}
