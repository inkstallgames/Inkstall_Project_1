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
    [SerializeField] private bool forceHandsVisible = true;  // NEW: Force hands visible even when disabled
    [SerializeField] private float walkSwingDistance = 0.025f;    // How far hands swing when walking (deadshot.io style)
    [SerializeField] private float runSwingDistance = 0.04f;     // How far hands swing when running
    [SerializeField] private float movementSmooth = 10f;       // Smoother transitions like deadshot.io
    [SerializeField] private float speedMultiplier = 1.2f;      // Slightly more responsive
    [SerializeField] private float walkFrequency = 2.2f;       // Walking step frequency (Hz)
    [SerializeField] private float runFrequency = 3.8f;        // Running step frequency (Hz)
    [SerializeField] private float accelerationInfluence = 0.3f; // How much acceleration affects swing
    
    [Header("Weapon Sway")]
    [SerializeField] private bool enableWeaponSway = true;
    [SerializeField] private float swayAmount = 0.02f;           // Increased for deadshot.io feel
    [SerializeField] private float swaySmooth = 20f;             // Faster response
    [SerializeField] private float maxSwayDistance = 0.1f;      // More dynamic sway
    [SerializeField] private float swayRecoverySpeed = 8f;      // How fast sway returns to center
    [SerializeField] private float aimSwayMultiplier = 0.3f;     // Reduced sway when aiming
    
    [Header("Breathing")]
    [SerializeField] private bool enableBreathing = true;
    [SerializeField] private float breathingSpeed = 1.5f;
    [SerializeField] private float breathingAmount = 0.002f;
    [SerializeField] private float breathingSmooth = 8f;
    
    [Header("Deadshot.io Style Effects")]
    [SerializeField] private bool enableDynamicEffects = true;
    [SerializeField] private float landImpactAmount = 0.08f;     // Landing impact strength
    [SerializeField] private float quickTurnSway = 0.015f;       // Sway when turning quickly
    [SerializeField] private float strafeSwayAmount = 0.02f;      // Extra sway when strafing
    [SerializeField] private float recoilInfluence = 0.5f;         // How much recoil affects overall movement
    [SerializeField] private float momentumCarry = 0.7f;           // How much movement carries over
    
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
    private float _acceleration = 0f;         // Current acceleration
    private Vector3 _lastVelocity = Vector3.zero;
    private float _lastYaw = 0f;              // For quick turn detection
    private Vector3 _momentum = Vector3.zero; // Carried over movement
    
    // Sway tracking
    private Vector3 _currentSway;
    private Vector3 _targetSway;
    private Vector3 _currentSwayVelocity;
    
    // Jump tracking
    private float _jumpOffset = 0f;
    private float _targetJumpOffset = 0f;
    private float _jumpVelocity = 0f;
    private float _jumpLiftAmount = 0.06f;
    private float _fallAmount = 0.04f;
    private float _landingBounce = 0.025f;
    private float _jumpSmooth = 18f;
    
    // Breathing tracking
    private float _breathingOffset = 0f;
    private float _targetBreathingOffset = 0f;
    private float _breathingVelocity = 0f;
    
    // Wobble tracking
    private float _wobbleTime = 0f;
    private Vector3 _currentWobble;
    private Vector3 _targetWobble;
    private Vector3 _wobbleVelocity;
    
    private void Start()
    {
        if (_initialPosition == Vector3.zero)
        {
            _initialPosition = transform.localPosition;
            _initialRotation = transform.localRotation;
            _currentPosition = _initialPosition;
            _currentRotation = _initialRotation;
            _targetPosition = _initialPosition;
            _targetRotation = _initialRotation;
        }
        
        // Cache controller reference if not already cached
        if (_controller == null)
        {
            _controller = GetComponentInParent<ThirdPersonController>();
        }
        
        // Apply initial transform
        ApplyInitialTransform();
    }
    
    public override void Spawned()
    {
        // Store initial transform values
        _initialPosition = transform.localPosition;
        _initialRotation = transform.localRotation;
        _currentPosition = _initialPosition;
        _currentRotation = _initialRotation;
        _targetPosition = _initialPosition;
        _targetRotation = _initialRotation;
        
        // Cache controller reference
        _controller = GetComponentInParent<ThirdPersonController>();
        
        // Ensure hands are visible immediately ONLY for local player
        if (forceHandsVisible && Object != null && Object.HasInputAuthority)
        {
            EnsureHandsVisible();
        }
        
        ResetAllTracking();
        
        // Apply initial position to ensure hands are visible
        if (Object != null && Object.HasInputAuthority)
        {
            ApplyInitialTransform();
        }
    }
    
    private void EnsureHandsVisible()
    {
        // Make sure the GameObject is active
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
        
        // Check if renderer exists and enable it
        var renderer = GetComponent<Renderer>();
        if (renderer != null && !renderer.enabled)
        {
            renderer.enabled = true;
        }
        
        // Check all child renderers
        var childRenderers = GetComponentsInChildren<Renderer>();
        foreach (var childRenderer in childRenderers)
        {
            if (!childRenderer.enabled)
            {
                childRenderer.enabled = true;
            }
        }
    }
    
    private void ApplyInitialTransform()
    {
        // Force apply initial transform to ensure hands are visible
        transform.localPosition = _initialPosition;
        transform.localRotation = _initialRotation;
        
        // Apply to children as well
        foreach (Transform child in transform)
        {
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
        }
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
            
            // Calculate acceleration for dynamic effects
            Vector3 currentVelocity = _moveDirection * _currentSpeed;
            _acceleration = Vector3.Distance(currentVelocity, _lastVelocity) / Time.deltaTime;
            _lastVelocity = currentVelocity;
            
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
                    
                    // Detect quick turns for deadshot.io style sway
                    float currentYaw = mainCamera.transform.eulerAngles.y;
                    float yawDelta = Mathf.Abs(Mathf.DeltaAngle(currentYaw, _lastYaw));
                    if (yawDelta > 90f && Time.deltaTime > 0.01f) // Quick turn detection
                    {
                        _momentum += Vector3.right * quickTurnSway * (yawDelta / 180f);
                    }
                    _lastYaw = currentYaw;
                }
                else
                {
                    _moveDirection = inputDir; // Fallback
                }
            }
            
            // Update swing phase based on ACTUAL player speed (deadshot.io style)
            if (_isMoving && _isGrounded)
            {
                // Use specific frequencies for more realistic movement
                float targetFrequency = _currentSpeed > _controller.MoveSpeed * 0.8f ? runFrequency : walkFrequency;
                float speedRatio = _currentSpeed / _controller.SprintSpeed;
                float swingFrequency = Mathf.Lerp(walkFrequency, runFrequency, speedRatio) * speedMultiplier;
                
                // Add acceleration influence for dynamic feel
                float accelerationBonus = _acceleration * accelerationInfluence * 0.01f;
                swingFrequency += accelerationBonus;
                
                _swingPhase += Time.deltaTime * swingFrequency * Mathf.PI * 2f; // Convert to radians
            }
            
            // Apply momentum carry-over (deadshot.io style)
            if (_momentum.magnitude > 0.001f)
            {
                _momentum *= momentumCarry; // Gradually reduce momentum
                if (_momentum.magnitude < 0.001f) _momentum = Vector3.zero;
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
            
            // Create natural CURVED swing pattern (deadshot.io style)
            // This creates a pendulum-like arc motion with more dynamic feel
            float swingValue = Mathf.Sin(_swingPhase); // -1 to +1
            
            // Enhanced CURVED SWING - Create arc motion like real arm swing
            float horizontalSwing = swingValue * swingDistance;                    // Main left-right movement
            float depthSwing = Mathf.Sin(_swingPhase * 0.5f) * swingDistance * 0.4f; // Forward/back curve (enhanced)
            float verticalSwing = Mathf.Abs(Mathf.Sin(_swingPhase * 2f)) * swingDistance * 0.15f; // Slight up/down (enhanced)
            
            // Combine for natural curved swing (fixed world space)
            Vector3 curvedSwing = Vector3.right * horizontalSwing;           // Left-right
            curvedSwing += Vector3.forward * depthSwing;                     // Forward-back curve
            curvedSwing += Vector3.up * verticalSwing;                        // Slight vertical
            
            // Add momentum carry-over for deadshot.io feel
            curvedSwing += _momentum;
            
            // Add strafe-specific sway
            var inputData = GetComponentInParent<StarterAssetsInputs>();
            if (inputData != null && Mathf.Abs(inputData.move.x) > 0.5f)
            {
                float strafeDirection = Mathf.Sign(inputData.move.x);
                curvedSwing += Vector3.forward * strafeDirection * strafeSwayAmount * Mathf.Sin(_swingPhase * 1.5f);
            }
            
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
        
        // Calculate weapon sway with enhanced deadshot.io feel
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
        
        // ALWAYS force hands visible in editor, regardless of script state
        #if UNITY_EDITOR
        if (forceHandsVisible && gameObject != null && !gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            Debug.Log("[FPSHandsWobble] Forced hands GameObject active in editor");
        }
        #endif
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
