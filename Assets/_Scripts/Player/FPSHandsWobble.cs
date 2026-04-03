using UnityEngine;
using Fusion;
using StarterAssets;

/// <summary>
/// Ultra-smooth FPS hands wobble system with professional interpolation.
/// Provides natural movement feedback without any sudden transitions.
/// Industry-standard approach used in AAA FPS games.
/// </summary>
public class FPSHandsWobble : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private bool enableWobble = true;
    [SerializeField] private float walkSwingDistance = 0.02f;    // How far hands swing when walking (like -1 to +1)
    [SerializeField] private float runSwingDistance = 0.035f;     // How far hands swing when running
    [SerializeField] private float movementSmooth = 8f;
    [SerializeField] private float speedMultiplier = 1f;        // Adjust to match your preference
    
    [Header("Weapon Sway")]
    [SerializeField] private bool enableWeaponSway = true;
    [SerializeField] private float swayAmount = 0.015f;
    [SerializeField] private float swaySmooth = 15f;
    [SerializeField] private float maxSwayDistance = 0.08f;
    
    [Header("Breathing")]
    [SerializeField] private bool enableBreathing = true;
    [SerializeField] private float breathingSpeed = 1.5f;
    [SerializeField] private float breathingAmount = 0.002f;
    [SerializeField] private float breathingSmooth = 8f;
    
    [Header("Jump Effects")]
    [SerializeField] private bool enableJumpEffects = true;
    [SerializeField] private float jumpLiftAmount = 0.06f;
    [SerializeField] private float fallAmount = 0.04f;
    [SerializeField] private float landingBounce = 0.025f;
    [SerializeField] private float jumpSmooth = 18f;
    
    // Position and rotation tracking
    private Vector3 _initialPosition;
    private Quaternion _initialRotation;
    private Vector3 _currentPosition;
    private Quaternion _currentRotation;
    private Vector3 _targetPosition;
    private Quaternion _targetRotation;
    
    // Movement state
    private bool _isMoving = false;
    private bool _isGrounded = true;
    private bool _wasGrounded = true;
    private float _currentSpeed = 0f;
    private float _previousSpeed = 0f;
    private float _swingPhase = 0f;           // Current phase in walking cycle
    private Vector3 _moveDirection = Vector3.forward;
    private ThirdPersonController _controller; // Cache reference
    
    // Sway tracking
    private Vector3 _currentSway;
    private Vector3 _targetSway;
    private Vector3 _currentSwayVelocity;
    
    // Jump tracking
    private float _jumpOffset = 0f;
    private float _targetJumpOffset = 0f;
    private float _jumpVelocity = 0f;
    
    // Breathing tracking
    private float _breathingOffset = 0f;
    private float _targetBreathingOffset = 0f;
    private float _breathingVelocity = 0f;
    
    // Wobble tracking
    private float _wobbleTime = 0f;
    private Vector3 _currentWobble;
    private Vector3 _targetWobble;
    private Vector3 _wobbleVelocity;
    
    public override void Spawned()
    {
        _initialPosition = transform.localPosition;
        _initialRotation = transform.localRotation;
        _currentPosition = _initialPosition;
        _currentRotation = _initialRotation;
        _targetPosition = _initialPosition;
        _targetRotation = _initialRotation;
        
        // Cache controller reference
        _controller = GetComponentInParent<ThirdPersonController>();
        
        ResetAllTracking();
    }
    
    private void ResetAllTracking()
    {
        _currentSway = Vector3.zero;
        _targetSway = Vector3.zero;
        _currentSwayVelocity = Vector3.zero;
        
        _jumpOffset = 0f;
        _targetJumpOffset = 0f;
        _jumpVelocity = 0f;
        
        _breathingOffset = 0f;
        _targetBreathingOffset = 0f;
        _breathingVelocity = 0f;
        
        _wobbleTime = 0f;
        _currentWobble = Vector3.zero;
        _targetWobble = Vector3.zero;
        _wobbleVelocity = Vector3.zero;
    }
    
    public override void Render()
    {
        if (!Object.HasInputAuthority || !enableWobble) return;
        
        UpdateMovementState();
        CalculateTargetEffects();
        ApplySmoothInterpolation();
    }
    
    private void UpdateMovementState()
    {
        if (_controller != null)
        {
            _previousSpeed = _currentSpeed;
            _currentSpeed = _controller.NetworkedSpeed;
            _isMoving = _currentSpeed > 0.1f;
            
            // Get movement direction from input
            var inputData = GetComponentInParent<StarterAssetsInputs>();
            if (inputData != null && inputData.move.sqrMagnitude > 0.01f)
            {
                Vector3 inputDir = new Vector3(inputData.move.x, 0f, inputData.move.y).normalized;
                
                // Get camera rotation from main camera
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    Quaternion cameraRotation = Quaternion.Euler(0f, mainCamera.transform.eulerAngles.y, 0f);
                    _moveDirection = cameraRotation * inputDir;
                }
                else
                {
                    _moveDirection = inputDir; // Fallback
                }
            }
            
            // Update swing phase based on ACTUAL player speed (0 to max)
            if (_isMoving && _isGrounded)
            {
                // Use player's actual speed for dynamic swing frequency
                // Maps speed 0-max to appropriate swing frequency
                float minSwingFrequency = 1.5f;  // Slow walk steps/sec
                float maxSwingFrequency = 3.5f;  // Fast run steps/sec
                
                // Calculate swing frequency based on current speed (0 to max)
                float speedRatio = _currentSpeed / _controller.SprintSpeed; // 0 to 1 based on max speed
                float swingFrequency = Mathf.Lerp(minSwingFrequency, maxSwingFrequency, speedRatio) * speedMultiplier;
                
                _swingPhase += Time.deltaTime * swingFrequency * Mathf.PI * 2f; // Convert to radians
            }
            
            // Smooth movement state transitions
            bool newGroundedState = _controller.Grounded;
            _wasGrounded = _isGrounded;
            _isGrounded = newGroundedState;
            
            // CRITICAL: Detect jump moment when going from grounded to not grounded
            if (_wasGrounded && !_isGrounded && enableJumpEffects)
            {
                Debug.Log($"[FPSHandsWobble] Jump detected! Was grounded: {_wasGrounded}, Is grounded: {_isGrounded}");
                TriggerLandingBounce(); // This will handle the jump lift
            }
            
            // Trigger landing bounce when landing
            if (!_wasGrounded && _isGrounded && enableJumpEffects)
            {
                Debug.Log($"[FPSHandsWobble] Landing detected! Was grounded: {_wasGrounded}, Is grounded: {_isGrounded}");
                TriggerLandingBounce();
            }
        }
    }
    
    private void CalculateTargetEffects()
    {
        // Calculate realistic walking/running swing based on actual player speed
        if (_isMoving && _isGrounded && _controller != null)
        {
            // Determine swing distance based on player speed
            float normalizedSpeed = _currentSpeed / _controller.MoveSpeed; // 0 to 1+ range
            float swingDistance = Mathf.Lerp(walkSwingDistance, runSwingDistance, normalizedSpeed);
            
            // Create natural CURVED swing pattern (not linear)
            // This creates a pendulum-like arc motion
            float swingValue = Mathf.Sin(_swingPhase); // -1 to +1
            
            // CURVED SWING - Create arc motion like real arm swing
            // Left position: hand moves back slightly
            // Right position: hand moves forward slightly
            // Center: hand returns to neutral
            
            float horizontalSwing = swingValue * swingDistance;                    // Main left-right movement
            float depthSwing = Mathf.Sin(_swingPhase * 0.5f) * swingDistance * 0.3f; // Forward/back curve
            float verticalSwing = Mathf.Abs(Mathf.Sin(_swingPhase * 2f)) * swingDistance * 0.1f; // Slight up/down
            
            // Combine for natural curved swing (fixed world space)
            Vector3 curvedSwing = Vector3.right * horizontalSwing;           // Left-right
            curvedSwing += Vector3.forward * depthSwing;                     // Forward-back curve
            curvedSwing += Vector3.up * verticalSwing;                        // Slight vertical
            
            _targetWobble = curvedSwing;
        }
        else
        {
            // IMMEDIATE return to center position when speed is 0
            _targetWobble = Vector3.zero; // No smooth damp - immediate return
        }
        
        // Calculate breathing
        if (enableBreathing && _isGrounded && !_isMoving)
        {
            float breathingPhase = _wobbleTime * breathingSpeed;
            float breathingValue = Mathf.Sin(breathingPhase) * breathingAmount * 0.3f;
            float breathingValue2 = Mathf.Abs(Mathf.Sin(breathingPhase * 2f)) * breathingAmount;
            
            _targetBreathingOffset = breathingValue2;
        }
        else
        {
            _targetBreathingOffset = 0f;
        }
        
        // Calculate jump effects with proper jump detection
        if (enableJumpEffects)
        {
            if (!_isGrounded)
            {
                // In air - add both vertical and minimal horizontal movement
                _targetJumpOffset = Mathf.SmoothDamp(_targetJumpOffset, -fallAmount, ref _jumpVelocity, 0.3f);
                
                // Add very subtle horizontal drift while in air (keep in camera view)
                float airDriftAmount = 0.005f; // Reduced from 0.015f to stay in frame
                Vector3 airDrift = Vector3.right * Mathf.Sin(_swingPhase * 0.5f) * airDriftAmount;
                _targetWobble += airDrift;
            }
            else if (_wasGrounded && !_isGrounded)
            {
                // JUST JUMPED - this triggers when leaving ground
                _targetJumpOffset = jumpLiftAmount;
                _jumpVelocity = 0f;
                
                // Add minimal forward push when jumping (keep in camera view)
                Vector3 jumpPush = Vector3.forward * jumpLiftAmount * 0.1f; // Reduced from 0.3f to 0.1f
                _targetWobble += jumpPush;
                
                Debug.Log($"[FPSHandsWobble] Jump effects applied! Lift: {jumpLiftAmount}");
            }
            else
            {
                // Grounded - return to zero
                _targetJumpOffset = Mathf.SmoothDamp(_targetJumpOffset, 0f, ref _jumpVelocity, 0.2f);
            }
        }
        else
        {
            _targetJumpOffset = 0f;
        }
        
        // Calculate weapon sway
        if (enableWeaponSway)
        {
            CalculateWeaponSway();
        }
        else
        {
            _targetSway = Vector3.zero;
        }
        
        // Combine all effects into target position
        _targetPosition = _initialPosition + _targetWobble + _currentSway;
        _targetPosition.y += _targetJumpOffset + _targetBreathingOffset;
    }
    
    private void CalculateWeaponSway()
    {
        // Get mouse input with smoothing
        Vector2 mouseInput = Vector2.zero;
        
        var inputs = GetComponentInParent<StarterAssetsInputs>();
        if (inputs != null)
        {
            mouseInput = inputs.look;
        }
        else
        {
            mouseInput = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        }
        
        // Calculate target sway with limits
        Vector3 rawSway = new Vector3(-mouseInput.x * swayAmount, -mouseInput.y * swayAmount, 0f);
        
        // Clamp to maximum sway distance
        if (rawSway.magnitude > maxSwayDistance)
        {
            rawSway = rawSway.normalized * maxSwayDistance;
        }
        
        // Smoothly interpolate sway
        _targetSway = Vector3.SmoothDamp(_targetSway, rawSway, ref _currentSwayVelocity, 1f / swaySmooth);
    }
    
    private void ApplySmoothInterpolation()
    {
        // Ultra-smooth position interpolation
        _currentPosition = Vector3.SmoothDamp(_currentPosition, _targetPosition, ref _wobbleVelocity, 1f / movementSmooth);
        
        // Apply final position
        transform.localPosition = _currentPosition;
        
        // Optional: Add subtle rotation based on movement
        if (enableWeaponSway)
        {
            Vector3 rotationOffset = new Vector3(_currentSway.y * 2f, _currentSway.x * 2f, 0f);
            _targetRotation = _initialRotation * Quaternion.Euler(rotationOffset);
            _currentRotation = Quaternion.Slerp(_currentRotation, _targetRotation, swaySmooth * Time.deltaTime);
            transform.localRotation = _currentRotation;
        }
    }
    
    private void TriggerLandingBounce()
    {
        _targetJumpOffset = landingBounce;
        _jumpVelocity = 0f;
    }
    
    // Public methods for external events
    public void TriggerRecoil(float amount = 0.04f)
    {
        Vector3 recoil = new Vector3(
            Random.Range(-amount * 0.4f, amount * 0.4f),
            -amount * 1.2f,
            Random.Range(-amount * 0.2f, amount * 0.2f)
        );
        
        _targetSway += recoil;
    }
    
    public void AddImpulse(Vector3 direction, float force)
    {
        _targetSway += direction * force;
    }
    
    private void OnValidate()
    {
        walkSwingDistance = Mathf.Max(0f, walkSwingDistance);
        runSwingDistance = Mathf.Max(0f, runSwingDistance);
        movementSmooth = Mathf.Max(0.1f, movementSmooth);
        speedMultiplier = Mathf.Max(0.1f, speedMultiplier);
        swayAmount = Mathf.Max(0f, swayAmount);
        swaySmooth = Mathf.Max(0.1f, swaySmooth);
        maxSwayDistance = Mathf.Max(0f, maxSwayDistance);
        breathingSpeed = Mathf.Max(0.1f, breathingSpeed);
        breathingAmount = Mathf.Max(0f, breathingAmount);
        breathingSmooth = Mathf.Max(0.1f, breathingSmooth);
        jumpLiftAmount = Mathf.Max(0f, jumpLiftAmount);
        fallAmount = Mathf.Max(0f, fallAmount);
        landingBounce = Mathf.Max(0f, landingBounce);
        jumpSmooth = Mathf.Max(0.1f, jumpSmooth);
    }
    
    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying)
        {
            // Draw initial position
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.TransformPoint(_initialPosition), 0.02f);
            
            // Draw current position
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, 0.015f);
            
            // Draw target position
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.TransformPoint(_targetPosition), 0.01f);
        }
    }
}
