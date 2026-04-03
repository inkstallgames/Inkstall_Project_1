using UnityEngine;
using Fusion;
using StarterAssets;

/// <summary>
/// Deadshot.io style FPS animation controller
/// Provides smooth, responsive animations for FPS hands with procedural effects
/// Combines keyframe animations with procedural movement for natural feel
/// </summary>
public class FPSAnimationController : NetworkBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private bool enableAnimations = true;
    [SerializeField] private Animator fpsAnimator;
    [SerializeField] private float animationBlendSpeed = 10f;
    
    [Header("Movement Animations")]
    [SerializeField] private bool enableMovementAnimations = true;
    [SerializeField] private AnimationCurve walkCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve runCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float walkSpeed = 1.5f;
    [SerializeField] private float runSpeed = 2.5f;
    
    [Header("Procedural Effects")]
    [SerializeField] private bool enableProceduralEffects = true;
    [SerializeField] private float proceduralIntensity = 1f;
    [SerializeField] private float proceduralFrequency = 1f;
    
    [Header("Weapon Animations")]
    [SerializeField] private bool enableWeaponAnimations = true;
    [SerializeField] private float recoilRecoveryTime = 0.2f;
    [SerializeField] private float aimTransitionSpeed = 8f;
    
    // Animation states
    private enum MovementState { Idle, Walking, Running, Jumping, Falling }
    private enum WeaponState { Idle, Aiming, Shooting, Reloading }
    
    private MovementState _currentMovementState = MovementState.Idle;
    private WeaponState _currentWeaponState = WeaponState.Idle;
    
    // Animation parameters
    private float _movementBlend = 0f;
    private float _aimBlend = 0f;
    private float _recoilBlend = 0f;
    private float _animationTime = 0f;
    
    // procedural movement
    private Vector3 _proceduralPosition;
    private Quaternion _proceduralRotation;
    private Vector3 _proceduralVelocity;
    
    // References
    private ThirdPersonController _controller;
    private StarterAssetsInputs _input;
    private Vector3 _initialPosition;
    private Quaternion _initialRotation;
    
    public override void Spawned()
    {
        // Cache references
        _controller = GetComponentInParent<ThirdPersonController>();
        _input = GetComponentInParent<StarterAssetsInputs>();
        
        // Store initial transform
        _initialPosition = transform.localPosition;
        _initialRotation = transform.localRotation;
        _proceduralPosition = _initialPosition;
        _proceduralRotation = _initialRotation;
        
        // Setup animator
        if (fpsAnimator == null)
            fpsAnimator = GetComponent<Animator>();
            
        if (fpsAnimator != null)
        {
            fpsAnimator.playMode = AnimatorPlayMode.Unscaled;
        }
    }
    
    public override void FixedUpdateNetwork()
    {
        if (!Object.HasInputAuthority || !enableAnimations) return;
        
        UpdateAnimationStates();
        UpdateProceduralEffects();
        ApplyAnimations();
    }
    
    private void UpdateAnimationStates()
    {
        if (_controller == null) return;
        
        // Update movement state
        UpdateMovementState();
        
        // Update weapon state
        UpdateWeaponState();
        
        // Update animation time
        _animationTime += Runner.DeltaTime;
    }
    
    private void UpdateMovementState()
    {
        float speed = _controller.NetworkedSpeed;
        bool isGrounded = _controller.Grounded;
        
        // Determine movement state
        if (!isGrounded)
        {
            if (_controller.NetworkedVelocity.y > 0.1f)
                _currentMovementState = MovementState.Jumping;
            else
                _currentMovementState = MovementState.Falling;
        }
        else if (speed > 0.1f)
        {
            if (speed > 4f)
                _currentMovementState = MovementState.Running;
            else
                _currentMovementState = MovementState.Walking;
        }
        else
        {
            _currentMovementState = MovementState.Idle;
        }
        
        // Update movement blend
        float targetBlend = 0f;
        switch (_currentMovementState)
        {
            case MovementState.Walking:
                targetBlend = 0.5f;
                break;
            case MovementState.Running:
                targetBlend = 1f;
                break;
            case MovementState.Jumping:
            case MovementState.Falling:
                targetBlend = 0.3f;
                break;
        }
        
        _movementBlend = Mathf.Lerp(_movementBlend, targetBlend, Runner.DeltaTime * animationBlendSpeed);
    }
    
    private void UpdateWeaponState()
    {
        // Update weapon state based on input
        if (_input != null)
        {
            if (_input.isShooting)
                _currentWeaponState = WeaponState.Shooting;
            else if (_input.aim)
                _currentWeaponState = WeaponState.Aiming;
            else
                _currentWeaponState = WeaponState.Idle;
        }
        
        // Update aim blend
        float targetAimBlend = _currentWeaponState == WeaponState.Aiming ? 1f : 0f;
        _aimBlend = Mathf.Lerp(_aimBlend, targetAimBlend, Runner.DeltaTime * aimTransitionSpeed);
        
        // Update recoil blend (decay over time)
        if (_recoilBlend > 0f)
        {
            _recoilBlend = Mathf.Max(0f, _recoilBlend - Runner.DeltaTime * (1f / recoilRecoveryTime));
        }
    }
    
    private void UpdateProceduralEffects()
    {
        if (!enableProceduralEffects) return;
        
        Vector3 targetPosition = _initialPosition;
        Quaternion targetRotation = _initialRotation;
        
        // Add movement-based procedural animation
        if (_currentMovementState == MovementState.Walking || _currentMovementState == MovementState.Running)
        {
            float frequency = _currentMovementState == MovementState.Running ? runSpeed : walkSpeed;
            float time = _animationTime * frequency * proceduralFrequency;
            
            // Deadshot.io style movement - subtle bounce and sway
            float bounce = Mathf.Sin(time * Mathf.PI * 2f) * 0.01f * proceduralIntensity;
            float sway = Mathf.Sin(time * Mathf.PI * 2f + Mathf.PI * 0.5f) * 0.005f * proceduralIntensity;
            
            targetPosition += Vector3.up * bounce;
            targetPosition += Vector3.right * sway;
            
            // Add slight rotation
            float rotationAmount = Mathf.Sin(time * Mathf.PI * 2f) * 2f * proceduralIntensity;
            targetRotation *= Quaternion.Euler(rotationAmount, rotationAmount * 0.5f, 0f);
        }
        
        // Add jump procedural effects
        if (_currentMovementState == MovementState.Jumping)
        {
            targetPosition += Vector3.up * 0.05f * proceduralIntensity;
            targetRotation *= Quaternion.Euler(-10f, 0f, 0f);
        }
        else if (_currentMovementState == MovementState.Falling)
        {
            targetPosition += Vector3.down * 0.02f * proceduralIntensity;
            targetRotation *= Quaternion.Euler(5f, 0f, 0f);
        }
        
        // Add recoil procedural effects
        if (_recoilBlend > 0f)
        {
            float recoilAmount = _recoilBlend * 0.1f * proceduralIntensity;
            targetPosition += Vector3.back * recoilAmount;
            targetRotation *= Quaternion.Euler(recoilAmount * 20f, 0f, 0f);
        }
        
        // Add aim procedural effects
        if (_aimBlend > 0f)
        {
            float aimAmount = _aimBlend * 0.02f * proceduralIntensity;
            targetPosition += Vector3.forward * aimAmount;
            targetPosition += Vector3.down * aimAmount * 0.5f;
            
            float aimRotation = _aimBlend * 5f * proceduralIntensity;
            targetRotation *= Quaternion.Euler(-aimRotation, aimRotation * 0.3f, 0f);
        }
        
        // Smooth procedural movement
        _proceduralPosition = Vector3.SmoothDamp(_proceduralPosition, targetPosition, ref _proceduralVelocity, 0.05f);
        _proceduralRotation = Quaternion.Slerp(_proceduralRotation, targetRotation, Runner.DeltaTime * 15f);
    }
    
    private void ApplyAnimations()
    {
        // Apply procedural effects
        if (enableProceduralEffects)
        {
            transform.localPosition = _proceduralPosition;
            transform.localRotation = _proceduralRotation;
        }
        
        // Update animator parameters
        if (fpsAnimator != null && enableMovementAnimations)
        {
            fpsAnimator.SetFloat("MovementBlend", _movementBlend);
            fpsAnimator.SetFloat("AimBlend", _aimBlend);
            fpsAnimator.SetFloat("RecoilBlend", _recoilBlend);
            fpsAnimator.SetBool("IsGrounded", _controller.Grounded);
            fpsAnimator.SetFloat("VerticalVelocity", _controller.NetworkedVelocity.y);
        }
    }
    
    // Public methods for external events
    public void OnShoot()
    {
        _recoilBlend = 1f;
        
        if (fpsAnimator != null)
            fpsAnimator.SetTrigger("Shoot");
    }
    
    public void OnReload()
    {
        if (fpsAnimator != null)
            fpsAnimator.SetTrigger("Reload");
    }
    
    public void OnEquip()
    {
        if (fpsAnimator != null)
            fpsAnimator.SetTrigger("Equip");
    }
    
    // Utility methods
    public void SetProceduralIntensity(float intensity)
    {
        proceduralIntensity = Mathf.Clamp01(intensity);
    }
    
    public void SetAnimationSpeed(float speed)
    {
        proceduralFrequency = speed;
    }
    
    // Debug info
    private void OnGUI()
    {
        if (!Object.HasInputAuthority) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label($"Movement State: {_currentMovementState}");
        GUILayout.Label($"Weapon State: {_currentWeaponState}");
        GUILayout.Label($"Movement Blend: {_movementBlend:F2}");
        GUILayout.Label($"Aim Blend: {_aimBlend:F2}");
        GUILayout.Label($"Recoil Blend: {_recoilBlend:F2}");
        GUILayout.Label($"Procedural Intensity: {proceduralIntensity:F2}");
        GUILayout.EndArea();
    }
}
