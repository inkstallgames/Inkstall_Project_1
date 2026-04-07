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
    
    // Private variables
    private Vector3 _initialPosition;
    private Quaternion _initialRotation;
    private float _currentRecoil = 0f;
    private float _targetRecoil = 0f;
    private float _recoilVelocity = 0f;
    private bool _isRecoiling = false;
    private bool _isReturning = false;
    
    public override void Spawned()
    {
        // Store initial transform values
        _initialPosition = transform.localPosition;
        _initialRotation = transform.localRotation;
    }
    
    public override void Render()
    {
        if (!Object.HasInputAuthority || !enableRecoil) return;
        
        UpdateRecoilAnimation();
        ApplyTransform();
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
            }
        }
    }
    
    private void ApplyTransform()
    {
        // Apply rotation recoil
        Quaternion recoilRotation = _initialRotation * Quaternion.Euler(_currentRecoil, 0f, 0f);
        transform.localRotation = recoilRotation;
    }
    
    /// <summary>
    /// Check if pistol is ready to fire (recoil animation completed)
    /// </summary>
    /// <returns>True if pistol has returned to original position and can fire again</returns>
    public bool IsReadyToFire()
    {
        // INSTANT FIRE: Always ready for maximum fire rate
        return true;
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
        _currentRecoil = 0f;
        _targetRecoil = 0f;
        _recoilVelocity = 0f;
        _isRecoiling = false;
        _isReturning = false;
        
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
