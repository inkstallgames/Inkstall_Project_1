 using UnityEngine;
using Fusion;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using System;
using System.Collections.Generic;
using Fusion.Sockets;

namespace StarterAssets
{
    public struct NetworkInputData : INetworkInput
    {
        public Vector2 move;
        public Vector2 look;
        public bool jump;
        public bool sprint;
        public float cameraYaw;
        public float cameraPitch;
        
        // Bomb throw
        public bool isThrowingBomb;
        public Vector3 throwDirection;
        
        // Pistol shooting
        public bool isShooting;
        public bool isReloading;
        public Vector3 aimDirection;
        public Vector3 aimOrigin;
        
        // Weapon equipping
        public bool equipPrimary; // Equips pistol for Team A, laser for Team B
        public bool equipBomb;
    }

    [RequireComponent(typeof(NetworkCharacterController))]
    public class ThirdPersonController : NetworkBehaviour, INetworkRunnerCallbacks
    {
        [Header("Player")]
        [Tooltip("Move speed of the character in m/s")]
        public float MoveSpeed = 4.0f;

        [Tooltip("Sprint speed of the character in m/s")]
        public float SprintSpeed = 3.64f;

        [Tooltip("How fast the character turns to face movement direction")]
        [Range(0.0f, 0.3f)]
        public float RotationSmoothTime = 0.12f;

        [Tooltip("Acceleration and deceleration")]
        public float SpeedChangeRate = 10.0f;

        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        [Space(10)]
        [Tooltip("The height the player can jump")]
        public float JumpHeight = 1.2f;

        [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
        public float Gravity = -15.0f;

        [Space(10)]
        [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
        public float JumpTimeout = 0.25f;

        [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
        public float FallTimeout = 0.15f;

        [Header("Player Grounded")]
        [Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
        public bool Grounded = true;

        [Tooltip("Useful for rough ground")]
        public float GroundedOffset = -0.14f;

        [Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
        public float GroundedRadius = 0.28f;

        [Tooltip("What layers the character uses as ground")]
        public LayerMask GroundLayers;

        [Header("Cinemachine")]
        [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
        public GameObject CinemachineCameraTarget;

        [Tooltip("How far in degrees can you move the camera up")]
        public float TopClamp = 70.0f;

        [Tooltip("How far in degrees can you move the camera down")]
        public float BottomClamp = -30.0f;

        [Tooltip("Additional degress to override the camera. Useful for fine tuning camera position when locked")]
        public float CameraAngleOverride = 0.0f;

        [Tooltip("For locking the camera position on all axis")]
        public bool LockCameraPosition = false;

        [Header("Multiplayer Sensitivity")]
        [Tooltip("Base sensitivity loaded independently from single-player game.")]
        public float mpCameraSensitivity = 0.2f;
        private const string MP_SENSITIVITY_KEY = "MultiplayerSensitivity";

        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;
        private float _speed;
        private float _animationBlend;
        private float _targetRotation = 0.0f;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;
        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFire;
        private int _animIDEquipGranade;
        private int _animIDThrowGranade;
        
        // NEW: Animation parameters for backward movement
        private int _animIDIsMovingBackward;
        private int _animIDHasPistol;
        private int _animIDPistolWalkBackward;
        private int _animIDPistolRunBackward;
        private int _animIDDirection;

        private Animator _animator;
        private Animator _armAnimator;
        private Animator _fullBodyAnimator;
        private NetworkCharacterController _networkController;
        private StarterAssetsInputs _nativeInput;
        private GameObject _mainCamera;
        private bool _hasAnimator;
        private bool _hasArmAnimator;
        private bool _hasFullBodyAnimator;
        private const float _threshold = 0.01f;
        private Vector3 _lastInputDirection = Vector3.zero;
        private NetworkInputData _latestInput;

        // Networked animation state - synced from state authority to all clients
        [Networked] public float NetworkedAnimationBlend { get; set; }
        [Networked] public float NetworkedMotionSpeed { get; set; }
        [Networked] public NetworkBool NetworkedGrounded { get; set; }
        [Networked] public float NetworkedVerticalVelocity { get; set; }
        
        // Networked movement state - authoritative position from server
        [Networked] public Vector3 NetworkedPosition { get; set; }
        [Networked] public float NetworkedSpeed { get; set; }

        private bool IsCurrentDeviceMouse
        {
            get
            {
                // Always return true for keyboard/mouse in multiplayer
                return true;
            }
        }

        private void Awake()
        {
            // Don't get StarterAssetsInputs here - it will be retrieved in Spawned()
            // to ensure we get the correct component for this specific player instance
        }

        private void Start()
        {
            // Cursor is left unlocked so players can interact with UI (throw button, etc.)
        }

        public override void Spawned()
        {
            // Initialize components for ALL players (needed for FixedUpdateNetwork)
            _networkController = GetComponent<NetworkCharacterController>();
            
            // Get PlayerVisualManager to identify arm and full body models
            var visualManager = GetComponent<PlayerVisualManager>();
            
            // Find both animators separately (search in inactive GameObjects too)
            if (visualManager != null)
            {
                // Get arm animator from first person arms (local player)
                var armModel = visualManager.GetType().GetField("firstPersonArms", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (armModel != null)
                {
                    var armObjects = armModel.GetValue(visualManager) as GameObject[];
                    if (armObjects != null && armObjects.Length > 0 && armObjects[0] != null)
                    {
                        _armAnimator = armObjects[0].GetComponentInChildren<Animator>(true);
                        _hasArmAnimator = _armAnimator != null;
                        if (_hasArmAnimator)
                        {
                            _armAnimator.enabled = true;
                                                    }
                    }
                }
                
                // Get full body animator from third person body (remote players)
                var bodyModel = visualManager.GetType().GetField("thirdPersonBody", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (bodyModel != null)
                {
                    var bodyObjects = bodyModel.GetValue(visualManager) as GameObject[];
                    if (bodyObjects != null && bodyObjects.Length > 0 && bodyObjects[0] != null)
                    {
                        _fullBodyAnimator = bodyObjects[0].GetComponentInChildren<Animator>(true);
                        _hasFullBodyAnimator = _fullBodyAnimator != null;
                        if (_hasFullBodyAnimator)
                        {
                            _fullBodyAnimator.enabled = true;
                                                    }
                    }
                }
            }
            
            // Fallback: Get Animator component for backward compatibility
            _animator = GetComponent<Animator>();
            _hasAnimator = _animator != null;
            
            if (!_hasAnimator)
            {
                _animator = GetComponentInChildren<Animator>();
                _hasAnimator = _animator != null;
            }
            
                        
            // Get StarterAssetsInputs component for THIS specific player instance
            _nativeInput = GetComponent<StarterAssetsInputs>();
            
            // Load custom standalone multiplayer sensitivity
            mpCameraSensitivity = PlayerPrefs.GetFloat(MP_SENSITIVITY_KEY, 0.2f);
            
            // Listen for sensitivity updates mid-game if this is our player
            if (Object.HasInputAuthority)
            {
                MultiplayerSettingsManager.OnSensitivityChangedEvent += UpdateSensitivity;
            }
            
            AssignAnimationIDs();
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;

            // Initialize networked position
            if (Object.HasStateAuthority)
            {
                NetworkedPosition = transform.position;
            }

            // Enable/disable input components based on authority
            if (_nativeInput != null)
            {
                _nativeInput.enabled = Object.HasInputAuthority;
            }

            // Initialize camera yaw based on spawn rotation for all players
            _cinemachineTargetYaw = transform.eulerAngles.y;
            _cinemachineTargetPitch = 0f;

            if (Object.HasInputAuthority)
            {
                Runner.AddCallbacks(this);

                // Set up camera target
                var cameraController = GetComponent<PlayerCameraController>();
                if (cameraController != null)
                {
                    var cameraTarget = cameraController.GetCameraTarget();
                    if (cameraTarget != null)
                    {
                        CinemachineCameraTarget = cameraTarget.gameObject;
                    }
                }
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Object.HasInputAuthority)
            {
                MultiplayerSettingsManager.OnSensitivityChangedEvent -= UpdateSensitivity;
            }
        }

        private void UpdateSensitivity(float newSensitivity)
        {
            mpCameraSensitivity = newSensitivity;
        }

        private void SetupCameraAndInput()
        {
            var cameraController = GetComponent<PlayerCameraController>();
            if (cameraController != null)
            {
                var cameraTarget = cameraController.GetCameraTarget();
                if (cameraTarget != null)
                {
                    CinemachineCameraTarget = cameraTarget.gameObject;
                }
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (GetInput(out NetworkInputData data))
            {
                _latestInput = data;

                // Only apply network camera rotation for proxies or the server.
                // Doing this for the local player causes past ticks to overwrite the 
                // butter-smooth LateUpdate rotation, causing severe 'resistance' and stuttering.
                if (!Object.HasInputAuthority)
                {
                    _cinemachineTargetYaw = data.cameraYaw;
                    _cinemachineTargetPitch = data.cameraPitch;
                }

                // Check Grounded state using physics spheres manually
                GroundedCheck();
                JumpAndGravity(data);
                Move(data);
                
                // Sync animation state to all clients via networked properties
                float normalizedSpeed = SprintSpeed > 0 ? _animationBlend / SprintSpeed : 0f;
                normalizedSpeed = Mathf.Clamp01(normalizedSpeed);
                NetworkedAnimationBlend = normalizedSpeed;
                NetworkedMotionSpeed = data.move.magnitude;
                NetworkedGrounded = Grounded;
                NetworkedVerticalVelocity = _verticalVelocity;
            }
            else
            {
                // If no input, still apply gravity and check grounded state
                GroundedCheck();
                JumpAndGravity(default);
                Move(default);
                
                // Reset animation state when no input
                NetworkedAnimationBlend = 0f;
                NetworkedMotionSpeed = 0f;
                NetworkedGrounded = Grounded;
                NetworkedVerticalVelocity = _verticalVelocity;
            }
            
            // Removed the if statement here
            // CRITICAL: Log any jump execution without input
            if (_jumpTimeoutDelta <= 0f && Grounded && _latestInput.jump == false && _verticalVelocity > 0f)
            {
                                            }
        }

        public override void Render()
        {
            // Update arm animator (for local player first-person view)
            if (_hasArmAnimator && _armAnimator != null)
            {
                if (!_armAnimator.enabled) _armAnimator.enabled = true;
                
                // Detect if moving backward relative to facing direction
                bool isMovingBackward = false;
                if (_latestInput.move.sqrMagnitude > 0.01f)
                {
                    // Calculate actual movement direction in world space
                    Vector3 inputDirection = new Vector3(_latestInput.move.x, 0f, _latestInput.move.y).normalized;
                    Quaternion desiredRotation = Quaternion.Euler(0.0f, _cinemachineTargetYaw, 0.0f);
                    Vector3 worldMoveDirection = desiredRotation * inputDirection;
                    
                    // Check if world movement is opposite to character's forward
                    float dotProduct = Vector3.Dot(worldMoveDirection, transform.forward);
                    isMovingBackward = dotProduct < -0.5f;
                }
                
                // Set global animator speed for reverse playback
                float animatorSpeed = isMovingBackward ? -1f : 1f;
                _armAnimator.speed = animatorSpeed;
                
                _armAnimator.SetFloat(_animIDSpeed, NetworkedAnimationBlend);
                _armAnimator.SetBool(_animIDGrounded, NetworkedGrounded);
                _armAnimator.SetBool(_animIDJump, NetworkedVerticalVelocity > 0f && !NetworkedGrounded);
                
                if (_latestInput.isShooting) _armAnimator.SetTrigger(_animIDFire);
                if (_latestInput.equipBomb) _armAnimator.SetTrigger(_animIDEquipGranade);
                if (_latestInput.isThrowingBomb) _armAnimator.SetTrigger(_animIDThrowGranade);
            }
            
            // Update full body animator (for remote players and shadows)
            if (_hasFullBodyAnimator && _fullBodyAnimator != null)
            {
                if (!_fullBodyAnimator.enabled) _fullBodyAnimator.enabled = true;
                
                // NEW: Joystick angle-based direction calculation (0-360°)
                float direction = 0f;
                bool isMovingBackward = false;
                if (_latestInput.move.sqrMagnitude > 0.01f)
                {
                    // Calculate joystick angle in degrees (0-360°)
                    // 0° = forward, 90° = right, 180° = backward, 270° = left
                    float joystickAngle = Mathf.Atan2(_latestInput.move.x, _latestInput.move.y) * Mathf.Rad2Deg;
                    if (joystickAngle < 0) joystickAngle += 360f; // Convert negative angles to positive
                    
                    // Determine backward movement based on joystick angle
                    // Forward: 0° to 90° and 270° to 360° (180° total)
                    // Backward: 90° to 270° (180° total)
                    isMovingBackward = joystickAngle > 90f && joystickAngle < 270f;
                    
                    // Set direction parameter: 1 for forward, -1 for backward, 0 for strafing
                    if (isMovingBackward)
                    {
                        direction = -1f; // Backward
                    }
                    else if (joystickAngle < 90f || joystickAngle > 270f)
                    {
                        direction = 1f; // Forward
                    }
                    else
                    {
                        direction = 0f; // Strafing (left/right)
                    }
                    
                                                        }
                _fullBodyAnimator.SetFloat(_animIDDirection, direction);
                
                // Set animator speed to normal (no more reverse playback)
                _fullBodyAnimator.speed = 1f;
                
                // Basic animation parameters
                _fullBodyAnimator.SetFloat(_animIDSpeed, NetworkedAnimationBlend);
                _fullBodyAnimator.SetBool(_animIDGrounded, NetworkedGrounded);
                _fullBodyAnimator.SetBool(_animIDJump, NetworkedVerticalVelocity > 0f && !NetworkedGrounded);
                
                if (_latestInput.isShooting) _fullBodyAnimator.SetTrigger(_animIDFire);
                if (_latestInput.equipBomb) _fullBodyAnimator.SetTrigger(_animIDEquipGranade);
                
                // NEW: Pistol backward movement animations with 2D blend tree support
                bool hasPistol = _cachedEquipSystem != null && _cachedEquipSystem.CurrentWeapon == NetworkWeaponEquipSystem.WeaponType.Pistol;
                _fullBodyAnimator.SetBool(_animIDHasPistol, hasPistol);
                _fullBodyAnimator.SetBool(_animIDIsMovingBackward, isMovingBackward);
                
                // Optional: Keep backward bool parameters for compatibility with existing systems
                if (hasPistol && isMovingBackward && _latestInput.move.sqrMagnitude > 0.01f)
                {
                    bool isRunning = _latestInput.sprint && NetworkedAnimationBlend > 0.5f;
                    
                    if (isRunning)
                    {
                        _fullBodyAnimator.SetBool(_animIDPistolRunBackward, true);
                        _fullBodyAnimator.SetBool(_animIDPistolWalkBackward, false);
                    }
                    else
                    {
                        _fullBodyAnimator.SetBool(_animIDPistolWalkBackward, true);
                        _fullBodyAnimator.SetBool(_animIDPistolRunBackward, false);
                    }
                }
                else
                {
                    // Reset pistol backward animations when not applicable
                    _fullBodyAnimator.SetBool(_animIDPistolWalkBackward, false);
                    _fullBodyAnimator.SetBool(_animIDPistolRunBackward, false);
                }
            }
            
            // Fallback: Update legacy single animator if dual animators not found
            if (!_hasArmAnimator && !_hasFullBodyAnimator && _hasAnimator && _animator != null)
            {
                // NEW: Joystick angle-based direction calculation (0-360°)
                float direction = 0f;
                bool isMovingBackward = false;
                if (_latestInput.move.sqrMagnitude > 0.01f)
                {
                    // Calculate joystick angle in degrees (0-360°)
                    // 0° = forward, 90° = right, 180° = backward, 270° = left
                    float joystickAngle = Mathf.Atan2(_latestInput.move.x, _latestInput.move.y) * Mathf.Rad2Deg;
                    if (joystickAngle < 0) joystickAngle += 360f; // Convert negative angles to positive
                    
                    // Determine backward movement based on joystick angle
                    // Forward: 0° to 90° and 270° to 360° (180° total)
                    // Backward: 90° to 270° (180° total)
                    isMovingBackward = joystickAngle > 90f && joystickAngle < 270f;
                    
                    // Set direction parameter: 1 for forward, -1 for backward, 0 for strafing
                    if (isMovingBackward)
                    {
                        direction = -1f; // Backward
                    }
                    else if (joystickAngle < 90f || joystickAngle > 270f)
                    {
                        direction = 1f; // Forward
                    }
                    else
                    {
                        direction = 0f; // Strafing (left/right)
                    }
                    
                                                        }
                _animator.SetFloat(_animIDDirection, direction);
                
                // Set animator speed to normal (no more reverse playback)
                _animator.speed = 1f;
                
                // Set the blend value (always positive)
                _animator.SetFloat(_animIDSpeed, NetworkedAnimationBlend);
                _animator.SetBool(_animIDGrounded, NetworkedGrounded);
                _animator.SetBool(_animIDJump, NetworkedVerticalVelocity > 0f && !NetworkedGrounded);
                
                if (_latestInput.isShooting) _animator.SetTrigger(_animIDFire);
                if (_latestInput.equipBomb) _animator.SetTrigger(_animIDEquipGranade);
                if (_latestInput.isThrowingBomb) _animator.SetTrigger(_animIDThrowGranade);
            }
        }

        private void AssignAnimationIDs()
        {
            // Try to detect which animator controller is being used
            string armControllerName = _hasArmAnimator ? _armAnimator.runtimeAnimatorController?.name ?? "" : "";
            string fullBodyControllerName = _hasFullBodyAnimator ? _fullBodyAnimator.runtimeAnimatorController?.name ?? "" : "";
            string fallbackControllerName = _hasAnimator ? _animator.runtimeAnimatorController?.name ?? "" : "";
            
                        
            // Use "MotionSpeed" for HeroAnimationController, "Speed" for others
            if (armControllerName.Contains("Hero") || fullBodyControllerName.Contains("Hero") || fallbackControllerName.Contains("Hero"))
            {
                _animIDSpeed = Animator.StringToHash("MotionSpeed");
                            }
            else if (armControllerName.Contains("AlienArm") || fullBodyControllerName.Contains("AlienBody") || 
                     (armControllerName.Contains("Alien") && _hasArmAnimator) || 
                     (fullBodyControllerName.Contains("Alien") && _hasFullBodyAnimator) ||
                     fallbackControllerName.Contains("Alien"))
            {
                _animIDSpeed = Animator.StringToHash("MotionSpeed");
                            }
            else
            {
                _animIDSpeed = Animator.StringToHash("Speed");
                            }
            
            _animIDGrounded = Animator.StringToHash("isGrounded");
            _animIDJump = Animator.StringToHash("isJumping");
            _animIDFire = Animator.StringToHash("Fire");
            _animIDEquipGranade = Animator.StringToHash("EquipGrenade");
            _animIDThrowGranade = Animator.StringToHash("ThrowGrenade");
            
            // NEW: Animation parameters for backward movement
            _animIDIsMovingBackward = Animator.StringToHash("isMovingBackward");
            _animIDHasPistol = Animator.StringToHash("hasPistol");
            _animIDPistolWalkBackward = Animator.StringToHash("pistolWalkBackward");
            _animIDPistolRunBackward = Animator.StringToHash("pistolRunBackward");
            _animIDDirection = Animator.StringToHash("Direction");
        }

        private void GroundedCheck()
        {
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
        }

        private void Move(NetworkInputData input)
        {
            if (_mainCamera == null && Object.HasInputAuthority)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            }

            float targetSpeed = input.sprint ? SprintSpeed : MoveSpeed;
            if (input.move == Vector2.zero) targetSpeed = 0.0f;

            float inputMagnitude = input.move.magnitude;

            // CRITICAL FIX: Instant speed changes - eliminate momentum for immediate direction response
            // When direction changes, speed changes instantly without acceleration/deceleration
            _speed = targetSpeed; // Direct assignment instead of Lerp

            // round speed to 3 decimal places
            // CRITICAL FIX: Instant animation blend changes - eliminate momentum
            _animationBlend = targetSpeed; // Direct assignment instead of Lerp
            if (_animationBlend < 0.01f) _animationBlend = 0f;
            // Normalize animation blend to 0-1 range for Animator
            float normalizedSpeed = _animationBlend / SprintSpeed; // SprintSpeed is max speed
            normalizedSpeed = Mathf.Clamp01(normalizedSpeed);
            
            // CRITICAL FIX: Complete stop on direction change - no movement until next frame
            bool shouldBlockMovement = false;
            
            if (input.move != Vector2.zero)
            {
                // Get camera rotation for rotation calculation
                float cameraYawRad = _cinemachineTargetYaw * Mathf.Deg2Rad;
                Vector3 forward = new Vector3(Mathf.Sin(cameraYawRad), 0f, Mathf.Cos(cameraYawRad));
                Vector3 right = new Vector3(Mathf.Cos(cameraYawRad), 0f, -Mathf.Sin(cameraYawRad));
                
                // CENTER POINT ROTATION: Rotate based on input, not movement direction
                Vector3 rotationInput = (forward * input.move.y + right * input.move.x).normalized;
                if (rotationInput != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(rotationInput);
                    transform.rotation = targetRotation; // Rotate in place from center
                }
                
                // Calculate world-space movement direction for movement logic
                Vector3 worldDirection = (forward * input.move.y + right * input.move.x).normalized;
                
                // COMPLETE STOP: Check for direction change and block movement
                Vector3 currentInputDirection = worldDirection;
                bool directionChanged = Vector3.Dot(currentInputDirection, _lastInputDirection) < 0.95f;
                if (directionChanged && _lastInputDirection != Vector3.zero)
                {
                    // BLOCK MOVEMENT: Don't allow movement this frame after direction change
                    if (_networkController != null)
                    {
                        _networkController.Velocity = new Vector3(0f, _networkController.Velocity.y, 0f);
                        _speed = 0f;
                        _animationBlend = 0f;
                        shouldBlockMovement = true; // Block movement for this frame
                    }
                }
                _lastInputDirection = currentInputDirection;
            }

            // Calculate movement direction - character always faces camera, so move relative to camera
            Vector3 targetDirection = Vector3.zero;
            Vector3 movementInputDirection = Vector3.zero;

            if (input.move != Vector2.zero && !shouldBlockMovement)
            {
                // Use camera yaw angle instead of camera transform to avoid diagonal issues
                float cameraYawRad = _cinemachineTargetYaw * Mathf.Deg2Rad;
                
                // Calculate forward and right vectors from camera yaw only (no pitch influence)
                Vector3 forward = new Vector3(Mathf.Sin(cameraYawRad), 0f, Mathf.Cos(cameraYawRad));
                Vector3 right = new Vector3(Mathf.Cos(cameraYawRad), 0f, -Mathf.Sin(cameraYawRad));
                
                // Calculate movement direction based on input relative to camera yaw
                targetDirection = (forward * input.move.y + right * input.move.x).normalized;
                movementInputDirection = targetDirection;
            }

            // Sync Custom ThirdPersonController settings directly into the NetworkCharacterController
            if (_networkController != null)
            {
                _networkController.maxSpeed = _speed;
                _networkController.gravity = Gravity;
                
                // CRITICAL FIX: Complete stop when no input (direction changes already handled above)
                if (input.move == Vector2.zero)
                {
                    // Complete stop when no input
                    _networkController.Velocity = new Vector3(0f, _networkController.Velocity.y, 0f);
                }
                
                // NetworkCharacterController expects a raw normalized direction vector, NOT a pre-calculated delta!
                // It internally scales by DeltaTime, Gravity, and maxSpeed.
                _networkController.Move(targetDirection.normalized);
                
                // CRITICAL FIX: Don't override rotation - let it follow camera direction immediately
                // transform.rotation = currentCameraRotation; // Already set above
            }

            if (Object.HasStateAuthority)
            {
                NetworkedPosition = transform.position;
                NetworkedSpeed = _speed;
            }
        }

         private void JumpAndGravity(NetworkInputData input)
        {
            if (_networkController == null) return;

            // Use the Native Grounded state from the controller
            Grounded = _networkController.Grounded;

            if (input.jump && (int)Time.frameCount % 10 == 0)
            {
            }

            if (Grounded)
            {
                _fallTimeoutDelta = FallTimeout;
                
                // CRITICAL: Force zero vertical velocity when grounded and no jump input
                if (!_latestInput.jump && _networkController.Velocity.y > 0.1f)
                {
                                        _networkController.Velocity = new Vector3(_networkController.Velocity.x, 0f, _networkController.Velocity.z);
                    _verticalVelocity = 0f;
                }
                
                _verticalVelocity = _networkController.Velocity.y;

                if (input.jump && _jumpTimeoutDelta <= 0.0f)
                {
                    // CRITICAL: Triple-check that we actually have jump input to prevent auto-jumps
                    if (!_latestInput.jump)
                    {
                                                return;
                    }
                    
                    // CRITICAL: Additional check - ensure we're not getting velocity from elsewhere
                    if (_networkController.Velocity.y > 0.1f && !input.jump)
                    {
                                                return;
                    }
                    
                    // CRITICAL FIX: Force allow jump while sprinting - bypass timeout check when sprinting
                    if (input.sprint && _jumpTimeoutDelta <= 0.1f) // Allow jump with small timeout when sprinting
                    {
                                            }
                    else if (_jumpTimeoutDelta > 0.0f)
                    {
                                                return; // Block jump if timeout and not sprinting
                    }
                    
                    // Sync impulse height and trigger native jump
                    float jumpImpulse = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                    _networkController.jumpImpulse = jumpImpulse;
                    _networkController.Jump();
                    _verticalVelocity = jumpImpulse;
                    
                    // Reset jump timeout immediately after successful jump
                    _jumpTimeoutDelta = JumpTimeout;
                    
                                    }
                else if (input.jump && _jumpTimeoutDelta > 0.0f)
                {
                                    }

                if (_jumpTimeoutDelta > 0.0f)
                {
                    _jumpTimeoutDelta -= Runner.DeltaTime;
                    _jumpTimeoutDelta = Mathf.Max(0f, _jumpTimeoutDelta); // Never go below 0
                }
            }
            else  
            {
                _jumpTimeoutDelta = JumpTimeout;
                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Runner.DeltaTime;
                }
                
                _verticalVelocity = _networkController.Velocity.y;
            }
            
            // Gravity is tracked internally by _networkController.Move(), we do not apply it mathematically here.
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);
            if (Grounded) Gizmos.color = transparentGreen;
            else Gizmos.color = transparentRed;
            Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
        }

        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                if (FootstepAudioClips.Length > 0 && _networkController != null)
                {
                    var index = UnityEngine.Random.Range(0, FootstepAudioClips.Length);
                    AudioSource.PlayClipAtPoint(FootstepAudioClips[index], transform.position, FootstepAudioVolume);
                }
            }
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f && _networkController != null)
                AudioSource.PlayClipAtPoint(LandingAudioClip, transform.position, FootstepAudioVolume);
        }

        #region INetworkRunnerCallbacks

        // Cached reference for bomb input
        private NetworkBombBehaviour _cachedBombBehaviour;
        // Cached reference for pistol input
        private NetworkPistolBehaviour _cachedPistolBehaviour;
        // Cached reference for laser input
        private NetworkLaserBehaviour _cachedLaserBehaviour;
        // Cached reference for weapon equip system
        private NetworkWeaponEquipSystem _cachedEquipSystem;

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var data = new NetworkInputData();

            // Do not collect input if the settings panel is open
            if (NetworkUIManager.Instance != null && NetworkUIManager.Instance.IsSettingsPanelActive)
            {
                // Ensure camera doesn't drift
                data.cameraYaw = _cinemachineTargetYaw;
                data.cameraPitch = _cinemachineTargetPitch;
                input.Set(data);
                return;
            }

            if (_nativeInput != null)
            {
                data.move = _nativeInput.move;
                data.look = _nativeInput.look;
                data.jump = _nativeInput.jump;
                data.sprint = _nativeInput.sprint;
                data.cameraYaw = _cinemachineTargetYaw;
                data.cameraPitch = _cinemachineTargetPitch;
                _nativeInput.jump = false;
            }
            else
            {
                            }
            
            // Collect weapon equip input
            if (_cachedEquipSystem == null)
            {
                _cachedEquipSystem = GetComponent<NetworkWeaponEquipSystem>();
            }
            if (_cachedEquipSystem != null)
            {
                _cachedEquipSystem.CollectNetworkInput(ref data);
            }
            
            // Collect bomb throw input
            if (_cachedBombBehaviour == null)
            {
                _cachedBombBehaviour = GetComponent<NetworkBombBehaviour>();
            }
            if (_cachedBombBehaviour != null)
            {
                _cachedBombBehaviour.CollectNetworkInput(ref data);
            }

            // Collect pistol shooting input
            if (_cachedPistolBehaviour == null)
            {
                _cachedPistolBehaviour = GetComponent<NetworkPistolBehaviour>();
            }
            if (_cachedPistolBehaviour != null)
            {
                _cachedPistolBehaviour.CollectNetworkInput(ref data);
            }

            // Collect laser shooting input
            if (_cachedLaserBehaviour == null)
            {
                _cachedLaserBehaviour = GetComponent<NetworkLaserBehaviour>();
            }
            if (_cachedLaserBehaviour != null)
            {
                _cachedLaserBehaviour.CollectNetworkInput(ref data);
            }

            input.Set(data);
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player){}
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player){}
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data){}
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress){}

        #endregion

        private void LateUpdate()
        {
            // CRITICAL FIX: Force character to always face camera direction, overriding any unwanted rotation
            if (Object.HasInputAuthority)
            {
                Quaternion cameraRotation = Quaternion.Euler(0.0f, _cinemachineTargetYaw, 0.0f);
                transform.rotation = cameraRotation;
            }
            
            if (_mainCamera == null)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
                if (_mainCamera == null) return; // Still no camera, exit
            }

            if (_latestInput.look.sqrMagnitude >= _threshold && !LockCameraPosition)
            {
                // Both Mouse and Touch deltas are physical pixel offsets per frame.
                // Multiplying by Time.deltaTime causes severe input sluggishness/lag!
                bool isTouch = _nativeInput != null && _nativeInput.isTouchLook;
                
                // Keep sensitivity 1:1 to what it was before
                float adjustedSensitivity = mpCameraSensitivity;
                
                _cinemachineTargetYaw += _latestInput.look.x * adjustedSensitivity;
                
                // Y-axis inversion: touch screen deltas have opposite vertical polarity
                // compared to mouse deltas, so we invert only for touch input.
                float verticalLook = _latestInput.look.y;
                if (isTouch)
                {
                    verticalLook *= -1f;
                }
                
                _cinemachineTargetPitch += verticalLook * adjustedSensitivity;
            }

            _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);
            CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride, _cinemachineTargetYaw, 0.0f);
        }
    }
}