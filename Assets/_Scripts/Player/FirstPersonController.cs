using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	[RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
	[RequireComponent(typeof(PlayerInput))]
#endif
	public class FirstPersonController : MonoBehaviour
	{
		[Header("Player")]
		[Tooltip("Move speed of the character in m/s")]
		public float MoveSpeed = 4.0f;
		[Tooltip("Sprint speed of the character in m/s")]
		public float SprintSpeed = 6.0f;
		[Tooltip("Rotation speed of the character")]
		public float RotationSpeed = 1f;
		[Tooltip("Acceleration and deceleration")]
		public float SpeedChangeRate = 10.0f;
		[Tooltip("Prevents wall climbing by limiting consecutive jumps")]
		public bool preventWallClimbing = true;

		[Space(10)]
		[Tooltip("The height the player can jump")]
		public float JumpHeight = 1.2f;
		[Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
		public float Gravity = -15.0f;

		[Space(10)]
		[Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
		public float JumpTimeout = 0.1f;
		[Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
		public float FallTimeout = 0.15f;

		[Header("Player Grounded")]
		[Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
		public bool Grounded = true;
		[Tooltip("Useful for rough ground")]
		public float GroundedOffset = -0.14f;
		[Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
		public float GroundedRadius = 0.5f;
		[Tooltip("What layers the character uses as ground")]
		public LayerMask GroundLayers;

		[Header("Mobile Controls")]
		[Tooltip("Joystick magnitude threshold for running")]
		public float runThreshold = 0.7f;

		[Header("Cinemachine")]
		[Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
		public GameObject CinemachineCameraTarget;
		[Tooltip("How far in degrees can you move the camera up")]
		public float TopClamp = 90.0f;
		[Tooltip("How far in degrees can you move the camera down")]
		public float BottomClamp = -90.0f;

		[Header("Camera Arc Motion")]
		[Tooltip("Enable PUBG-style camera arc motion when looking up/down")]
		public bool enableCameraArcMotion = true;
		[Tooltip("Height of the camera arc when looking up/down")]
		public float cameraArcHeight = 2f;
		[Tooltip("How quickly the camera moves along the arc")]
		public float cameraArcSpeed = 10f;
		[Tooltip("How much to reduce camera distance when looking up/down")]
		public float cameraZoomAmount = 1.5f;
		[Tooltip("How quickly the camera zooms in/out")]
		public float cameraZoomSpeed = 8f;

		[Header("Camera Smoothing")]
		[Tooltip("Enable smoothing for camera rotation")]
		public bool enableCameraSmoothing = true;
		[Tooltip("How quickly the camera rotation smooths to target value (higher = faster)")]
		public float cameraSmoothingSpeed = 10f;

		[Header("Movement Settings")]
		[Tooltip("If true, player will move at the same speed in all directions. If false, speed varies by direction.")]
		public bool uniformMovementSpeed = false;

		[Header("Player Sounds")]
		[Tooltip("Audio source for walking sound")]
		public AudioSource walkAudioSource;
		[Tooltip("Audio source for running sound")]
		public AudioSource runAudioSource;
		[Tooltip("Sound played when walking (looping)")]
		public AudioClip walkSound;
		[Tooltip("Sound played when running (looping)")]
		public AudioClip runSound;
		[Tooltip("Sound played when jumping")]
		public AudioClip jumpSound;
		[Tooltip("Threshold for joystick magnitude to trigger running")]
		public float runThresholdSound = 0.7f;

		// cinemachine
		private float _cinemachineTargetPitch;
		private float _currentPitch;

		// player
		private float _speed;
		private float _rotationVelocity;
		private float _verticalVelocity;
		private float _terminalVelocity = 53.0f;
		
		// Anti wall-climbing variables
		private int _consecutiveJumps = 0;
		private float _lastJumpTime = -10f;
		private const int MAX_CONSECUTIVE_JUMPS = 2;
		private const float JUMP_COOLDOWN = 1.0f;

		// timeout deltatime
		private float _jumpTimeoutDelta;
		private float _fallTimeoutDelta;

		// touch input
		private Vector2 touchStartPos;
		private Vector2 previousTouchPos;
		private bool isTouching = false;
		public float minTouchDelta = 10f;
		public float touchSensitivity = 0.1f;
		[Tooltip("Whether to use screen split for touch controls (left: movement, right: camera)")]
		public bool useSplitScreenTouch = true;
		[Tooltip("Additional sensitivity reduction for WebGL platform (lower = less sensitive)")]
		public float webGLSensitivityMultiplier = 0.05f;

		// PlayerPrefs key for sensitivity (matching OptionsMenuManager)
		private const string SENSITIVITY_KEY = "ScreenSensitivity";
		private const float DEFAULT_SENSITIVITY = 0.2f;
		private const float SENSITIVITY_MULTIPLIER = 1f; // Match this with your OptionsMenuManager's sensitivityMultiplier

#if ENABLE_INPUT_SYSTEM
		private PlayerInput _playerInput;
#endif
		private CharacterController _controller;
		private StarterAssetsInputs _input;
		private GameObject _mainCamera;

		private const float _threshold = 0.01f;

		private bool IsCurrentDeviceMouse
		{
			get
			{
#if ENABLE_INPUT_SYSTEM
				return _playerInput.currentControlScheme == "KeyboardMouse";
#else
				return false;
#endif
			}
		}

		// Camera arc motion variables
		private Vector3 _originalCameraOffset;
		private float _originalCameraDistance;
		private bool _cameraInitialized = false;
		private Cinemachine.CinemachineVirtualCamera _virtualCamera;
		private Cinemachine.Cinemachine3rdPersonFollow _thirdPersonFollow;

		private void Awake()
		{
			// get a reference to our main camera
			if (_mainCamera == null)
			{
				_mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
			}
            
			// Get reference to the virtual camera component
			_virtualCamera = GameObject.FindObjectOfType<Cinemachine.CinemachineVirtualCamera>();
		}

		private void Start()
		{
			_controller = GetComponent<CharacterController>();
			_input = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM
			_playerInput = GetComponent<PlayerInput>();
#else
			// Removed debug log error
#endif

			// Initialize camera components
			if (_virtualCamera != null)
			{
				_thirdPersonFollow = _virtualCamera.GetCinemachineComponent<Cinemachine.Cinemachine3rdPersonFollow>();
				if (_thirdPersonFollow != null)
				{
					_originalCameraOffset = _thirdPersonFollow.ShoulderOffset;
					_originalCameraDistance = _thirdPersonFollow.CameraDistance;
					_cameraInitialized = true;
				}
			}

			// Load sensitivity from PlayerPrefs
			float savedSensitivity = PlayerPrefs.GetFloat(SENSITIVITY_KEY, DEFAULT_SENSITIVITY);
			touchSensitivity = savedSensitivity * SENSITIVITY_MULTIPLIER;

			// Initialize current pitch to match target pitch
			_currentPitch = _cinemachineTargetPitch;

			// reset our timeouts on start
			_jumpTimeoutDelta = JumpTimeout;
			_fallTimeoutDelta = FallTimeout;
			
			// Setup audio sources if needed
			SetupAudioSources();
		}

		private void SetupAudioSources()
		{
			// Setup walking audio source
			if (walkAudioSource == null)
			{
				walkAudioSource = gameObject.AddComponent<AudioSource>();
				walkAudioSource.playOnAwake = false;
				walkAudioSource.loop = true;
				walkAudioSource.spatialBlend = 1.0f; // 3D sound
				walkAudioSource.volume = AudioManager.Instance != null ? AudioManager.Instance.sfxVolume : 0.7f;
			}
			
			// Setup running audio source
			if (runAudioSource == null)
			{
				runAudioSource = gameObject.AddComponent<AudioSource>();
				runAudioSource.playOnAwake = false;
				runAudioSource.loop = true;
				runAudioSource.spatialBlend = 1.0f; // 3D sound
				runAudioSource.volume = AudioManager.Instance != null ? AudioManager.Instance.sfxVolume : 0.7f;
			}
			
			// Assign clips if available
			if (walkSound != null)
			{
				walkAudioSource.clip = walkSound;
			}
			
			if (runSound != null)
			{
				runAudioSource.clip = runSound;
			}
		}

		private void Update()
		{
			JumpAndGravity();
			GroundedCheck();
			Move();
			HandleMobileInput();
			
			// Reset consecutive jumps counter if we've been grounded for a while
			if (Grounded && Time.time - _lastJumpTime > JUMP_COOLDOWN)
			{
				_consecutiveJumps = 0;
			}
		}

		private void LateUpdate()
		{
			CameraRotation();
		}

		private void GroundedCheck()
		{
			// set sphere position, with offset
			Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
			Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
		}

		private void CameraRotation()
		{
			// if there is an input
			if (_input.look.sqrMagnitude >= _threshold)
			{
				float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

				_cinemachineTargetPitch += _input.look.y * RotationSpeed * deltaTimeMultiplier;
				_rotationVelocity = _input.look.x * RotationSpeed * deltaTimeMultiplier;

				// clamp our pitch rotation
				_cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

				// Apply smoothing to the pitch rotation if enabled
				if (enableCameraSmoothing)
				{
					// Smoothly interpolate current pitch toward target pitch
					_currentPitch = Mathf.Lerp(_currentPitch, _cinemachineTargetPitch, Time.deltaTime * cameraSmoothingSpeed);
					
					// Update Cinemachine camera target pitch with smoothed value
					CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_currentPitch, 0.0f, 0.0f);
				}
				else
				{
					// Direct update without smoothing
					CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, 0.0f, 0.0f);
				}

				// Apply PUBG-style camera arc motion if enabled
				if (enableCameraArcMotion && _cameraInitialized && _thirdPersonFollow != null)
				{
					// Calculate normalized pitch (-1 to 1 range)
					float normalizedPitch = Mathf.Clamp(_cinemachineTargetPitch / 90.0f, -1.0f, 1.0f);
					
					// Calculate vertical offset based on pitch
					float verticalOffset = Mathf.Sin(normalizedPitch * Mathf.PI * 0.5f) * cameraArcHeight;
					
					// Calculate distance reduction based on pitch (more reduction when looking up/down)
					float pitchFactor = Mathf.Abs(normalizedPitch);
					float distanceReduction = pitchFactor * cameraZoomAmount;
					
					// Create new shoulder offset with the vertical component modified
					Vector3 newOffset = _originalCameraOffset;
					newOffset.y = _originalCameraOffset.y + verticalOffset;
					
					// Apply the new offset to create arc motion
					_thirdPersonFollow.ShoulderOffset = Vector3.Lerp(
						_thirdPersonFollow.ShoulderOffset,
						newOffset,
						Time.deltaTime * cameraArcSpeed
					);
					
					// Apply distance reduction to bring camera closer when looking up/down
					_thirdPersonFollow.CameraDistance = Mathf.Lerp(
						_thirdPersonFollow.CameraDistance,
						_originalCameraDistance - distanceReduction,
						Time.deltaTime * cameraZoomSpeed
					);
				}

				// rotate the player left and right
				transform.Rotate(Vector3.up * _rotationVelocity);
			}
			// We removed the else block to maintain camera position when not looking
		}

		private void Move()
		{
			// set target speed based on move speed, sprint speed and if sprint is pressed
			float targetSpeed = MoveSpeed;

			// Calculate movement direction angle in degrees (0 = right, 90 = forward, 180 = left, 270 = backward)
			float movementAngle = 0;
			if (_input.move != Vector2.zero)
			{
				movementAngle = Mathf.Atan2(_input.move.y, _input.move.x) * Mathf.Rad2Deg;
				// Convert to 0-360 range
				if (movementAngle < 0) movementAngle += 360f;
			}

			// Only apply directional speed modifications if not using uniform movement
			if (!uniformMovementSpeed)
			{
				// Check if movement is in the forward quadrant (between 40 and 140 degrees)
				bool isMovingForward = movementAngle >= 40f && movementAngle <= 140f;

				// Only apply sprint speed when moving in the forward quadrant
				if ((_input.sprint || _input.move.magnitude > runThreshold) && isMovingForward)
				{
					targetSpeed = SprintSpeed;
				}
			}
			else
			{
				// In uniform movement mode, always allow sprinting in any direction
				if (_input.sprint || _input.move.magnitude > runThreshold)
				{
					targetSpeed = SprintSpeed;
				}
			}

			// a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

			// note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is no input, set the target speed to 0
			if (_input.move == Vector2.zero) targetSpeed = 0.0f;

			// a reference to the players current horizontal velocity
			float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

			float speedOffset = 0.1f;
			float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

			// accelerate or decelerate to target speed
			if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
			{
				// creates curved result rather than a linear one giving a more organic speed change
				// note T in Lerp is clamped, so we don't need to clamp our speed
				_speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);

				// round speed to 3 decimal places
				_speed = Mathf.Round(_speed * 1000f) / 1000f;
			}
			else
			{
				_speed = targetSpeed;
			}

			// normalise input direction
			Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

			// note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is a move input rotate player when the player is moving
			if (_input.move != Vector2.zero)
			{
				// move
				inputDirection = transform.right * _input.move.x + transform.forward * _input.move.y;
			}

			// move the player
			_controller.Move(inputDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
			
			// Handle movement sounds
			HandleMovementSounds();
		}
		
		private void HandleMovementSounds()
		{
			// Check if we have the required components
			if (walkAudioSource == null || runAudioSource == null || walkSound == null || runSound == null)
				return;
				
			// Only play sounds when grounded
			if (!Grounded)
			{
				StopMovementSounds();
				return;
			}
			
			// Check if player is moving
			if (_input.move != Vector2.zero)
			{
				// Determine if running based on sprint input or joystick magnitude
				bool isRunning = _input.sprint || _input.move.magnitude > runThresholdSound;
				
				// Handle walking sound
				if (!isRunning)
				{
					// Stop running sound if it's playing
					if (runAudioSource.isPlaying)
						runAudioSource.Stop();
						
					// Start walking sound if not already playing
					if (!walkAudioSource.isPlaying)
					{
						walkAudioSource.clip = walkSound;
						walkAudioSource.Play();
					}
				}
				// Handle running sound
				else
				{
					// Stop walking sound if it's playing
					if (walkAudioSource.isPlaying)
						walkAudioSource.Stop();
						
					// Start running sound if not already playing
					if (!runAudioSource.isPlaying)
					{
						runAudioSource.clip = runSound;
						runAudioSource.Play();
					}
				}
			}
			// If not moving, stop all movement sounds
			else
			{
				StopMovementSounds();
			}
		}
		
		private void StopMovementSounds()
		{
			// Stop walking sound if it exists and is playing
			if (walkAudioSource != null && walkAudioSource.isPlaying)
				walkAudioSource.Stop();
				
			// Stop running sound if it exists and is playing
			if (runAudioSource != null && runAudioSource.isPlaying)
				runAudioSource.Stop();
		}

		private void JumpAndGravity()
		{
			if (Grounded)
			{
				// reset the fall timeout timer
				_fallTimeoutDelta = FallTimeout;

				// stop our velocity dropping infinitely when grounded
				if (_verticalVelocity < 0.0f)
				{
					_verticalVelocity = -2f;
				}

				// Jump - with anti wall-climbing protection
				if (_input.jump && _jumpTimeoutDelta <= 0.0f)
				{
					// Check if we should prevent this jump (anti wall-climbing)
					bool allowJump = true;
					
					if (preventWallClimbing)
					{
						// If we jumped recently, increment consecutive jumps counter
						if (Time.time - _lastJumpTime < 0.5f)
						{
							_consecutiveJumps++;
							
							// If too many consecutive jumps, block jumping temporarily
							if (_consecutiveJumps >= MAX_CONSECUTIVE_JUMPS)
							{
								allowJump = false;
								_lastJumpTime = Time.time; // Reset timer
							}
						}
						else
						{
							// Reset counter if enough time has passed
							_consecutiveJumps = 0;
						}
					}
					
					if (allowJump)
					{
						// Record jump time
						_lastJumpTime = Time.time;
						
						// the square root of H * -2 * G = how much velocity needed to reach desired height
						_verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
						
						// Play jump sound
						PlayJumpSound();
					}
				}

				// jump timeout
				if (_jumpTimeoutDelta >= 0.0f)
				{
					_jumpTimeoutDelta -= Time.deltaTime;
				}
			}
			else
			{
				// reset the jump timeout timer
				_jumpTimeoutDelta = JumpTimeout;

				// fall timeout
				if (_fallTimeoutDelta >= 0.0f)
				{
					_fallTimeoutDelta -= Time.deltaTime;
				}

				// if we are not grounded, do not jump
				_input.jump = false;
			}

			// apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
			if (_verticalVelocity < _terminalVelocity)
			{
				_verticalVelocity += Gravity * Time.deltaTime;
			}
		}
		
		private void PlayJumpSound()
		{
			if (jumpSound != null && AudioManager.Instance != null)
			{
				AudioManager.Instance.PlaySFXAtPoint(jumpSound, transform.position);
			}
		}

		private void HandleMobileInput()
		{
			// Reset look input at the beginning of the frame
			_input.look = Vector2.zero;

			// Track camera rotation touch
			bool foundCameraTouch = false;
			Vector2 cameraDelta = Vector2.zero;

			// Handle touch input for camera look
			if (Input.touchCount > 0)
			{
				// First pass: Process right side touches for camera control
				for (int i = 0; i < Input.touchCount; i++)
				{
					UnityEngine.Touch touch = Input.GetTouch(i);

					// Check if touch is over UI
					if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
						continue;

					// If using split screen, only use right side for camera rotation
					bool isRightSide = touch.position.x > Screen.width / 2;
					if (useSplitScreenTouch && !isRightSide)
						continue; // Skip left side touches when using split screen

					// Process camera rotation touch
					switch (touch.phase)
					{
						case UnityEngine.TouchPhase.Began:
							touchStartPos = touch.position;
							previousTouchPos = touch.position;
							isTouching = true;
							foundCameraTouch = true;
							break;

						case UnityEngine.TouchPhase.Moved:
							if (isTouching)
							{
								// Calculate delta from previous frame position
								cameraDelta = touch.position - previousTouchPos;

								// Only process if movement exceeds minimum delta
								if (cameraDelta.magnitude > 0)
								{
									// Invert the Y-axis to fix the up/down movement
									cameraDelta.y = -cameraDelta.y;
									
									// Apply platform-specific sensitivity adjustments
									float adjustedSensitivity = touchSensitivity;
									
									// Apply WebGL-specific sensitivity reduction
#if UNITY_WEBGL && !UNITY_EDITOR
									adjustedSensitivity *= webGLSensitivityMultiplier;
#endif
									
									// Use touch delta for camera movement
									_input.look = cameraDelta * adjustedSensitivity;
									foundCameraTouch = true;
								}

								// Always update previous position
								previousTouchPos = touch.position;
							}
							break;

						case UnityEngine.TouchPhase.Stationary:
							// No movement, but still tracking this touch
							foundCameraTouch = true;
							break;

						case UnityEngine.TouchPhase.Ended:
						case UnityEngine.TouchPhase.Canceled:
							isTouching = false;
							break;
					}

					// IMPORTANT CHANGE: Don't break the loop here
					// This allows us to process all touches, including camera rotation
					// while the joystick is being used
					// if (foundCameraTouch)
					//    break;
				}

				// Second pass: Process left side touches for joystick movement
				// This allows simultaneous movement and camera rotation
				if (useSplitScreenTouch)
				{
					// We don't need to do anything here since the joystick control
					// is handled by another component (UIVirtualJoystick)
					// The key change is removing the break statement above
					// to allow both joystick and camera touches to be processed
				}
			}
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

			// when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
			Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
		}

		private void OnDisable()
		{
			// Stop all sounds when disabled
			StopMovementSounds();
		}
	}
}
