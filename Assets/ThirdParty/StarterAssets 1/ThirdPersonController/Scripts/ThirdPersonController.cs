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
    }

    [RequireComponent(typeof(CharacterController))]
    public class ThirdPersonController : NetworkBehaviour, INetworkRunnerCallbacks
    {
        [Header("Player")]
        [Tooltip("Move speed of the character in m/s")]
        public float MoveSpeed = 4.0f;

        [Tooltip("Sprint speed of the character in m/s")]
        public float SprintSpeed = 6.5f;

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
        public float JumpTimeout = 0.50f;

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
        private int _animIDFreeFall;  
        private int _animIDMotionSpeed;

        private Animator _animator;
        private CharacterController _controller;
        private StarterAssetsInputs _nativeInput;
        private GameObject _mainCamera;
        private bool _hasAnimator;
        private const float _threshold = 0.01f;
        private NetworkInputData _latestInput;

        // Networked animation state - synced from state authority to all clients
        [Networked] public float NetworkedAnimationBlend { get; set; }
        [Networked] public float NetworkedMotionSpeed { get; set; }
        [Networked] public NetworkBool NetworkedGrounded { get; set; }
        [Networked] public float NetworkedVerticalVelocity { get; set; }

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
            _hasAnimator = TryGetComponent(out _animator);
            _controller = GetComponent<CharacterController>();
            
            // Get StarterAssetsInputs component for THIS specific player instance
            _nativeInput = GetComponent<StarterAssetsInputs>();
            
            // Disable CharacterController temporarily to allow position to be set correctly
            if (_controller != null)
            {
                _controller.enabled = false;
            }

            AssignAnimationIDs();
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;
            
            // Re-enable CharacterController after position is set
            if (_controller != null)
            {
                _controller.enabled = true;
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

                // Debug: Log input received on server
                if (Object.HasStateAuthority && data.move.sqrMagnitude > 0.01f && Time.frameCount % 60 == 0)
                {
                    Debug.Log($"[FixedUpdateNetwork] SERVER received input - Move: {data.move}, Player: {Object.InputAuthority.PlayerId}, Position: {transform.position}");
                }

                // Apply camera rotation from input (server authoritative)
                if (Object.HasStateAuthority)
                {
                    _cinemachineTargetYaw = data.cameraYaw;
                    _cinemachineTargetPitch = data.cameraPitch;
                }

                // Movement, jumping, and gravity should be simulated for all clients to see.
                JumpAndGravity(data);
                GroundedCheck();
                Move(data);

                // Sync animation state to all clients via networked properties
                NetworkedAnimationBlend = _animationBlend;
                NetworkedMotionSpeed = data.move.magnitude;
                NetworkedGrounded = Grounded;
                NetworkedVerticalVelocity = _verticalVelocity;
            }
            else
            {
                // Debug: Log when no input received
                if (Object.HasStateAuthority && Time.frameCount % 120 == 0)
                {
                    Debug.LogWarning($"[FixedUpdateNetwork] SERVER - No input received for Player {Object.InputAuthority.PlayerId}");
                }
                
                // If no input, still apply gravity and check grounded state
                JumpAndGravity(default);
                GroundedCheck();
                Move(default);
            }
        }

        public override void Render()
        {
            // Read animation state from [Networked] properties so all clients
            // (including clients viewing the host character) see correct animations.
            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, NetworkedAnimationBlend);
                _animator.SetFloat(_animIDMotionSpeed, NetworkedMotionSpeed);
                _animator.SetBool(_animIDGrounded, NetworkedGrounded);
                _animator.SetBool(_animIDJump, NetworkedVerticalVelocity > 0f && !NetworkedGrounded);
                _animator.SetBool(_animIDFreeFall, NetworkedVerticalVelocity < 0f && !NetworkedGrounded);
            }
        }

        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
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

            // accelerate or decelerate to target speed
            _speed = Mathf.Lerp(_speed, targetSpeed, Runner.DeltaTime * SpeedChangeRate);

            // round speed to 3 decimal places
            _speed = Mathf.Round(_speed * 1000f) / 1000f;

            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Runner.DeltaTime * SpeedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            // Character always faces camera direction (rotates when camera rotates, not when moving)
            float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _cinemachineTargetYaw, ref _rotationVelocity, RotationSmoothTime);
            transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);

            // Calculate movement direction relative to camera facing
            Vector3 inputDirection = new Vector3(input.move.x, 0.0f, input.move.y).normalized;
            Vector3 targetDirection = Vector3.zero;

            if (input.move != Vector2.zero)
            {
                // Move relative to camera direction
                targetDirection = Quaternion.Euler(0.0f, _cinemachineTargetYaw, 0.0f) * inputDirection;
            }

            // Only server moves the CharacterController (authoritative)
            // Clients receive position updates via NetworkTransform and interpolate visually
            if (Object.HasStateAuthority)
            {
                Vector3 horizontalMovement = targetDirection.normalized * (_speed * Runner.DeltaTime);
                Vector3 verticalMovement = new Vector3(0.0f, _verticalVelocity, 0.0f) * Runner.DeltaTime;
                _controller.Move(horizontalMovement + verticalMovement);
            }
        }

        private void JumpAndGravity(NetworkInputData input)
        {
            if (Grounded)
            {
                _fallTimeoutDelta = FallTimeout;

                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                if (input.jump && _jumpTimeoutDelta <= 0.0f)
                {
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                }

                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Runner.DeltaTime;
                }
            }
            else  
            {
                _jumpTimeoutDelta = JumpTimeout;
                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Runner.DeltaTime;
                }
            }

            if (_verticalVelocity < _terminalVelocity)
            {
                _verticalVelocity += Gravity * Runner.DeltaTime;
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
            Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
        }

        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                if (FootstepAudioClips.Length > 0)
                {
                    var index = UnityEngine.Random.Range(0, FootstepAudioClips.Length);
                    AudioSource.PlayClipAtPoint(FootstepAudioClips[index], transform.TransformPoint(_controller.center), FootstepAudioVolume);
                }
            }
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                AudioSource.PlayClipAtPoint(LandingAudioClip, transform.TransformPoint(_controller.center), FootstepAudioVolume);
            }
        }

        #region INetworkRunnerCallbacks

        // Cached reference for bomb input
        private NetworkBombBehaviour _cachedBombBehaviour;

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var data = new NetworkInputData();
            if (_nativeInput != null)
            {
                data.move = _nativeInput.move;
                data.look = _nativeInput.look;
                data.jump = _nativeInput.jump;
                data.sprint = _nativeInput.sprint;
                data.cameraYaw = _cinemachineTargetYaw;
                data.cameraPitch = _cinemachineTargetPitch;
                _nativeInput.jump = false;
                
                // Debug: Log input when movement detected
                if (data.move.sqrMagnitude > 0.01f && Time.frameCount % 60 == 0)
                {
                    Debug.Log($"[OnInput] Sending input to server - Move: {data.move}, Sprint: {data.sprint}, Yaw: {data.cameraYaw:F1}");
                }
            }
            else
            {
                Debug.LogError($"[OnInput] _nativeInput is NULL for Player {Object.InputAuthority.PlayerId}");
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

            input.Set(data);
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
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
            if (!Object.HasInputAuthority || CinemachineCameraTarget == null)
            {
                return;
            }

            if (_mainCamera == null)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
                if (_mainCamera == null) return; // Still no camera, exit
            }

            if (_latestInput.look.sqrMagnitude >= _threshold && !LockCameraPosition)
            {
                float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;
                _cinemachineTargetYaw += _latestInput.look.x * deltaTimeMultiplier;
                _cinemachineTargetPitch += _latestInput.look.y * deltaTimeMultiplier;
            }

            _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);
            CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride, _cinemachineTargetYaw, 0.0f);
        }
    }
}