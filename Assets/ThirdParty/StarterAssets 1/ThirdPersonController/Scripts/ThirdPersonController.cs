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
    public enum FootstepType
    {
        None,
        Walking,
        Running,
        Sprinting
    }

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

    [RequireComponent(typeof(NetworkCharacterController), typeof(NetworkTransform))]
    public class ThirdPersonController : NetworkBehaviour, INetworkRunnerCallbacks
    {
        [Header("Player")]
        [Tooltip("Move speed of the character in m/s")]
        public float MoveSpeed = 4.0f;

        [Tooltip("Sprint speed of the character in m/s")]
        public float SprintSpeed = 6.0f;

        [Tooltip("How fast the character turns to face movement direction")]
        [Range(0.0f, 0.3f)]
        public float RotationSmoothTime = 0.12f;

        [Tooltip("Acceleration and deceleration")]
        public float SpeedChangeRate = 10.0f;

        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        [Header("Footstep Audio - Industry Standard")]
        [Tooltip("Audio source for footstep sounds")]
        public AudioSource footstepAudioSource;
        [Tooltip("Walking footstep sounds (randomly selected)")]
        public AudioClip[] walkingFootsteps;
        [Range(0, 1)] [Tooltip("Volume of walking footsteps")]
        public float walkingFootstepVolume = 0.5f;

        [Tooltip("Running footstep sounds (randomly selected)")]
        public AudioClip[] runningFootsteps;
        [Range(0, 1)] [Tooltip("Volume of running footsteps")]
        public float runningFootstepVolume = 0.5f;

        [Tooltip("Sprinting footstep sounds (randomly selected)")]
        public AudioClip[] sprintingFootsteps;
        [Range(0, 1)] [Tooltip("Volume of sprinting footsteps")]
        public float sprintingFootstepVolume = 0.5f;

        [Tooltip("Time between footsteps when walking")]
        public float walkingFootstepInterval = 0.5f;
        [Tooltip("Time between footsteps when running")]
        public float runningFootstepInterval = 0.35f;
        [Tooltip("Time between footsteps when sprinting")]
        public float sprintingFootstepInterval = 0.25f;
        [Tooltip("Threshold for joystick magnitude to trigger running")]
        public float runThresholdSound = 0.5f;
        [Tooltip("Threshold for joystick magnitude to trigger sprinting")]
        public float sprintThresholdSound = 0.8f;

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
        
        // Industry-standard footstep system
        private float _nextFootstepTime;
        private FootstepType _currentFootstepType = FootstepType.None;
        private bool _previouslyGrounded;

        // Networked animation state - synced from state authority to all clients
        [Networked] public float NetworkedAnimationBlend { get; set; }
        [Networked] public float NetworkedMotionSpeed { get; set; }
        [Networked] public NetworkBool NetworkedGrounded { get; set; }
        [Networked] public float NetworkedVerticalVelocity { get; set; }
        [Networked] public NetworkBool NetworkedIsJumping { get; set; }
        
        // Remote camera aim sync
        [Networked] public float NetworkedCameraYaw { get; set; }
        [Networked] public float NetworkedCameraPitch { get; set; }
        
        // Networked movement state - authoritative position from server
        [Networked] public Vector3 NetworkedPosition { get; set; }
        [Networked] public float NetworkedSpeed { get; set; }

        private bool _callbacksRegistered = false;
        private bool _inputComponentEnabled = false;
        private bool _sensitivitySubscribed = false;
        private bool _cameraTargetAssigned = false;

        private void UpdateInputAuthorityState()
        {
            if (Object == null || !Object.IsValid) return;

            bool hasInputAuthority = Object.HasInputAuthority;

            if (hasInputAuthority)
            {
                // 1. Dynamically register callbacks when input authority is established
                if (!_callbacksRegistered)
                {
                    Runner.AddCallbacks(this);
                    _callbacksRegistered = true;
                    Debug.Log($"[ThirdPersonController] Registered input callbacks for local player.");
                }

                // 2. Dynamically enable input component when input authority is established
                if (_nativeInput != null && !_inputComponentEnabled)
                {
                    _nativeInput.enabled = true;
                    _inputComponentEnabled = true;
                    Debug.Log($"[ThirdPersonController] Enabled input component for local player.");
                }

                // 3. Listen for sensitivity updates mid-game if this is our player
                if (!_sensitivitySubscribed)
                {
                    MultiplayerSettingsManager.OnSensitivityChangedEvent += UpdateSensitivity;
                    _sensitivitySubscribed = true;
                }

                // 4. Set up camera target
                if (!_cameraTargetAssigned)
                {
                    var cameraController = GetComponent<PlayerCameraController>();
                    if (cameraController != null)
                    {
                        var cameraTarget = cameraController.GetCameraTarget();
                        if (cameraTarget != null)
                        {
                            CinemachineCameraTarget = cameraTarget.gameObject;
                            _cameraTargetAssigned = true;
                        }
                    }
                }
            }
        }

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

        private void Update()
        {
            // Dynamically monitor and manage input authority registration
            UpdateInputAuthorityState();

            // Handle movement sounds for all players
            HandleMovementSounds();
        }

        public override void Spawned()
        {
            // Initialize components for ALL players (needed for FixedUpdateNetwork)
            _networkController = GetComponent<NetworkCharacterController>();
            
            // Set gravity once — avoid per-tick state writes that can cause sync churn
            if (_networkController != null)
            {
                _networkController.gravity = Gravity;
            }
            
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
            
            AssignAnimationIDs();
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;

            // Initialize networked position
            if (Object.HasStateAuthority)
            {
                NetworkedPosition = transform.position;
            }

            // Initialize camera yaw based on spawn rotation for all players
            _cinemachineTargetYaw = transform.eulerAngles.y;
            _cinemachineTargetPitch = 0f;

            // Setup footstep audio system
            SetupFootstepAudio();
            _previouslyGrounded = Grounded;

            // Force an initial authority state check in Spawned
            UpdateInputAuthorityState();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (_sensitivitySubscribed)
            {
                MultiplayerSettingsManager.OnSensitivityChangedEvent -= UpdateSensitivity;
                _sensitivitySubscribed = false;
            }
            if (_callbacksRegistered)
            {
                runner.RemoveCallbacks(this);
                _callbacksRegistered = false;
            }
            _inputComponentEnabled = false;
            _cameraTargetAssigned = false;
        }

        private void SetupFootstepAudio()
        {
            // Setup footstep audio source for industry-standard system
            if (footstepAudioSource == null)
            {
                footstepAudioSource = gameObject.AddComponent<AudioSource>();
                footstepAudioSource.playOnAwake = false;
                footstepAudioSource.loop = false; // Individual footstep sounds
                footstepAudioSource.spatialBlend = 1.0f; // 3D sound
                footstepAudioSource.volume = 0.7f;
                
                // Standard 3D settings for multiplayer audibility
                footstepAudioSource.minDistance = 2f;
                footstepAudioSource.maxDistance = 25f;
                footstepAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            }
        }

        private void HandleMovementSounds()
        {
            // Industry-standard footstep system
            if (footstepAudioSource == null)
            {
                return;
            }
            
            // Use networked grounded state for proxies, local state for owner
            bool currentGrounded = Object.HasInputAuthority ? Grounded : (bool)NetworkedGrounded;

            // LANDING SOUND DETECTION: Triggered when transitioning from air to ground
            if (currentGrounded && !_previouslyGrounded)
            {
                if (LandingAudioClip != null)
                {
                    footstepAudioSource.PlayOneShot(LandingAudioClip, FootstepAudioVolume);
                }
            }
            _previouslyGrounded = currentGrounded;
            
            // Only play footsteps when grounded
            if (!currentGrounded)
            {
                _currentFootstepType = FootstepType.None;
                return;
            }
            
            // Get movement state (local input for owner, networked speed for proxies)
            float magnitude = 0f;
            bool isSprinting = false;
            
            if (Object.HasInputAuthority && _nativeInput != null)
            {
                magnitude = _nativeInput.move.magnitude;
                isSprinting = _nativeInput.sprint;
            }
            else if (!Object.HasInputAuthority)
            {
                // For remote players, use networked properties (synced across network)
                magnitude = NetworkedMotionSpeed;
                // Infer sprinting from speed magnitude
                isSprinting = magnitude > sprintThresholdSound;
            }
            
            // Determine footstep type based on movement
            FootstepType newFootstepType = FootstepType.None;
            
            if (magnitude > 0.01f)
            {
                if (isSprinting || magnitude > sprintThresholdSound)
                {
                    newFootstepType = FootstepType.Sprinting;
                }
                else if (magnitude > runThresholdSound)
                {
                    newFootstepType = FootstepType.Running;
                }
                else
                {
                    newFootstepType = FootstepType.Walking;
                }
            }
            
            
            // Handle footstep timing and playback
            if (newFootstepType != FootstepType.None)
            {
                if (newFootstepType != _currentFootstepType)
                {
                    // Footstep type changed, reset timing
                    _currentFootstepType = newFootstepType;
                    _nextFootstepTime = Time.time;
                }
                
                // Check if it's time for next footstep
                if (Time.time >= _nextFootstepTime)
                {
                    PlayFootstep(newFootstepType);
                    
                    // Set next footstep time based on type
                    switch (newFootstepType)
                    {
                        case FootstepType.Walking:
                            _nextFootstepTime = Time.time + walkingFootstepInterval;
                            break;
                        case FootstepType.Running:
                            _nextFootstepTime = Time.time + runningFootstepInterval;
                            break;
                        case FootstepType.Sprinting:
                            _nextFootstepTime = Time.time + sprintingFootstepInterval;
                            break;
                    }
                }
            }
            else
            {
                _currentFootstepType = FootstepType.None;
            }
        }
        
        private void PlayFootstep(FootstepType footstepType)
        {
            AudioClip[] footstepClips = null;
            float volumeToUse = 1.0f;
            
            switch (footstepType)
            {
                case FootstepType.Walking:
                    footstepClips = walkingFootsteps;
                    volumeToUse = walkingFootstepVolume;
                    break;
                case FootstepType.Running:
                    footstepClips = runningFootsteps;
                    volumeToUse = runningFootstepVolume;
                    break;
                case FootstepType.Sprinting:
                    footstepClips = sprintingFootsteps;
                    volumeToUse = sprintingFootstepVolume;
                    break;
            }
            
            if (footstepClips != null && footstepClips.Length > 0)
            {
                // Select random footstep from array
                AudioClip randomFootstep = footstepClips[UnityEngine.Random.Range(0, footstepClips.Length)];
                footstepAudioSource.PlayOneShot(randomFootstep, volumeToUse);
            }
            else
            {
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

                if (Object.HasStateAuthority)
                {
                    NetworkedCameraYaw = data.cameraYaw;
                    NetworkedCameraPitch = data.cameraPitch;
                }

                // Only apply network camera rotation for proxies or the server.
                // Doing this for the local player causes past ticks to overwrite the 
                // butter-smooth LateUpdate rotation, causing severe 'resistance' and stuttering.
                if (!Object.HasInputAuthority)
                {
                    _cinemachineTargetYaw = data.cameraYaw;
                    _cinemachineTargetPitch = data.cameraPitch;
                }

                // GroundedCheck() removed — its Physics.CheckSphere result was
                // immediately overwritten by _networkController.Grounded inside
                // JumpAndGravity, and the physics query can diverge between client
                // and server, introducing misprediction.
                JumpAndGravity(data);
                Move(data);
                
                // RUBBERBANDING FIX: Set character rotation deterministically using the
                // networked input's cameraYaw. This runs inside FixedUpdateNetwork which
                // IS replayed during Fusion's client-side resimulation, ensuring the
                // exact same rotation is applied on every re-prediction pass.
                // Previously, LateUpdate was the sole writer of transform.rotation for
                // the local player, but LateUpdate does NOT replay during resimulation
                // — causing rotation state to diverge and triggering snap corrections.
                transform.rotation = Quaternion.Euler(0f, data.cameraYaw, 0f);
                
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
                // For proxies without input: DO NOT instantly snap the visual angles here!
                // We keep NetworkedCameraYaw/Pitch updated from Server, but we smoothly Lerp 
                // the visual variables (_cinemachineTargetYaw/Pitch) in LateUpdate instead.
                
                // If no input, still apply gravity and check grounded state
                JumpAndGravity(default);
                Move(default);
                
                // Reset animation state when no input
                NetworkedAnimationBlend = 0f;
                NetworkedMotionSpeed = 0f;
                NetworkedGrounded = Grounded;
                NetworkedVerticalVelocity = _verticalVelocity;
                
                if (Grounded) NetworkedIsJumping = false;
            }
            
            // CRITICAL: Log any jump execution without input
            if (_jumpTimeoutDelta <= 0f && Grounded && _latestInput.jump == false && _verticalVelocity > 0f)
            {
            }
        } // Added missing closing brace here

        public override void Render()
        {
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
                // Only set Direction parameter if it exists in the animator
                if (HasParameter(_fullBodyAnimator, _animIDDirection))
                {
                    _fullBodyAnimator.SetFloat(_animIDDirection, direction);
                }
                
                // Set animator speed to normal (no more reverse playback)
                _fullBodyAnimator.speed = 1f;
                
                // Basic animation parameters
                _fullBodyAnimator.SetFloat(_animIDSpeed, NetworkedAnimationBlend);
                _fullBodyAnimator.SetBool(_animIDGrounded, NetworkedGrounded);
                _fullBodyAnimator.SetBool(_animIDJump, NetworkedIsJumping && !NetworkedGrounded);
                
                if (_latestInput.isShooting && HasParameter(_fullBodyAnimator, _animIDFire)) 
                    _fullBodyAnimator.SetTrigger(_animIDFire);
                if (_latestInput.equipBomb && HasParameter(_fullBodyAnimator, _animIDEquipGranade)) 
                    _fullBodyAnimator.SetTrigger(_animIDEquipGranade);
                

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
                // Only set Direction parameter if it exists in the animator
                if (HasParameter(_animator, _animIDDirection))
                {
                    _animator.SetFloat(_animIDDirection, direction);
                }
                
                // Set animator speed to normal (no more reverse playback)
                _animator.speed = 1f;
                
                // Set the blend value (always positive)
                _animator.SetFloat(_animIDSpeed, NetworkedAnimationBlend);
                _animator.SetBool(_animIDGrounded, NetworkedGrounded);
                _animator.SetBool(_animIDJump, NetworkedIsJumping && !NetworkedGrounded);
                
                if (_latestInput.isShooting && HasParameter(_animator, _animIDFire)) 
                    _animator.SetTrigger(_animIDFire);
                if (_latestInput.equipBomb && HasParameter(_animator, _animIDEquipGranade)) 
                    _animator.SetTrigger(_animIDEquipGranade);
                if (_latestInput.isThrowingBomb && HasParameter(_animator, _animIDThrowGranade)) 
                    _animator.SetTrigger(_animIDThrowGranade);
            }
        }

        private bool HasParameter(Animator animator, int parameterHash)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            
            foreach (var param in animator.parameters)
            {
                if (param.nameHash == parameterHash)
                    return true;
            }
            return false;
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

            float inputMagnitude = input.move.magnitude;
            
            // Gradual walk-to-run transition based on joystick magnitude
            float targetSpeed = 0f;
            
            if (inputMagnitude > 0.01f)  // Player is moving
            {
                if (input.sprint)
                {
                    // Sprint button pressed: Use full sprint speed
                    targetSpeed = SprintSpeed;  // 6.0 m/s
                }
                else
                {
                    // No sprint: Gradual transition from walk to run based on joystick
                    // 0.0 - 0.5 magnitude = Walk speed (4.0 m/s)
                    // 0.5 - 1.0 magnitude = Gradual increase to run speed (6.0 m/s)
                    float walkThreshold = 0.5f;
                    
                    if (inputMagnitude <= walkThreshold)
                    {
                        // Pure walking zone
                        targetSpeed = MoveSpeed;  // 4.0 m/s
                    }
                    else
                    {
                        // Transition zone: Gradually increase from walk to run speed
                        float transitionFactor = (inputMagnitude - walkThreshold) / (1.0f - walkThreshold);  // 0 to 1
                        targetSpeed = Mathf.Lerp(MoveSpeed, SprintSpeed, transitionFactor);  
                    }
                }
            }
            
            // Smooth acceleration/deceleration for natural movement
            float accelerationRate = 10.0f;  // Configurable acceleration
            float decelerationRate = 15.0f;  // Faster deceleration for responsiveness
            
            if (targetSpeed > _speed)
            {
                // Accelerating: Use acceleration rate
                _speed = Mathf.MoveTowards(_speed, targetSpeed, accelerationRate * Runner.DeltaTime);
            }
            else
            {
                // Decelerating: Use deceleration rate for snappy stops
                _speed = Mathf.MoveTowards(_speed, targetSpeed, decelerationRate * Runner.DeltaTime);
            }
            
            // Smooth animation blending
            float animationAcceleration = 8.0f;  // Slightly faster for responsive animation
            _animationBlend = Mathf.MoveTowards(_animationBlend, targetSpeed, animationAcceleration * Runner.DeltaTime);
            if (_animationBlend < 0.01f) _animationBlend = 0f;
            // Normalize animation blend to 0-1 range for Animator
            float normalizedSpeed = _animationBlend / SprintSpeed; // SprintSpeed is max speed
            normalizedSpeed = Mathf.Clamp01(normalizedSpeed);
            
            // Calculate movement direction - character always faces camera, so move relative to camera
            Vector3 targetDirection = Vector3.zero;

            if (input.move != Vector2.zero)
            {
                // Calculate movement direction relative to camera
                Vector3 inputDirection = new Vector3(input.move.x, 0f, input.move.y).normalized;
                
                // Use networked camera yaw to prevent rubberbanding
                float cameraYawRad = input.cameraYaw * Mathf.Deg2Rad;
                Vector3 forward = new Vector3(Mathf.Sin(cameraYawRad), 0f, Mathf.Cos(cameraYawRad));
                Vector3 right = new Vector3(Mathf.Cos(cameraYawRad), 0f, -Mathf.Sin(cameraYawRad));
                
                // Calculate world-space movement direction
                Vector3 worldDirection = (forward * input.move.y + right * input.move.x).normalized;
                targetDirection = worldDirection;
            }

            // Sync Custom ThirdPersonController settings directly into the NetworkCharacterController
            if (_networkController != null)
            {
                _networkController.maxSpeed = _speed;
                
                // NetworkCharacterController handles scaling internally
                _networkController.Move(targetDirection.normalized);
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

            if (Grounded)
            {
                _fallTimeoutDelta = FallTimeout;
                _verticalVelocity = _networkController.Velocity.y;

                if (_verticalVelocity <= 0.1f)
                {
                    NetworkedIsJumping = false;
                }

                if (input.jump && _jumpTimeoutDelta <= 0.0f)
                {
                    // Simple jump validation
                    if (_networkController.Velocity.y <= 0.1f)
                    {
                        float jumpImpulse = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                        _networkController.jumpImpulse = jumpImpulse;
                        _networkController.Jump();
                        _verticalVelocity = jumpImpulse;
                        
                        NetworkedIsJumping = true;
                        
                        // Reset jump timeout
                        _jumpTimeoutDelta = JumpTimeout;
                    }
                }

                if (_jumpTimeoutDelta > 0.0f)
                {
                    _jumpTimeoutDelta -= Runner.DeltaTime;
                    _jumpTimeoutDelta = Mathf.Max(0f, _jumpTimeoutDelta);
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

        /* 
        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f && _networkController != null)
                AudioSource.PlayClipAtPoint(LandingAudioClip, transform.position, FootstepAudioVolume);
        }
        */

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
                // No input available
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
            if (Object == null || !Object.IsValid) return;

            // Smooth interpolation for remote players to avoid 60Hz tick snapping
            if (!Object.HasInputAuthority)
            {
                // Smoothly lerp the visual pitch/yaw towards the networked target values
                _cinemachineTargetYaw = Mathf.LerpAngle(_cinemachineTargetYaw, NetworkedCameraYaw, Time.deltaTime * 15f);
                _cinemachineTargetPitch = Mathf.LerpAngle(_cinemachineTargetPitch, NetworkedCameraPitch, Time.deltaTime * 15f);
            }

            // Apply body rotation ONLY for remote players (smooth visual interpolation).
            // For the local player, transform.rotation is set deterministically in
            // FixedUpdateNetwork using input.cameraYaw. Setting it here would corrupt
            // the rotation state between Fusion resimulation ticks, causing rubberbanding.
            if (!Object.HasInputAuthority)
            {
                transform.rotation = Quaternion.Euler(0f, _cinemachineTargetYaw, 0f);
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