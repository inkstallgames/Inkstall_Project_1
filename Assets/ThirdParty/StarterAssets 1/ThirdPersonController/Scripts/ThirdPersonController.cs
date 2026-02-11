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
            if (Cursor.lockState != CursorLockMode.Locked && Object.HasInputAuthority)
            {
                Cursor.lockState = CursorLockMode.Locked;
            }
        }

        public override void Spawned()
        {
            Debug.Log($"[ThirdPersonController] Spawned() called. PlayerID: {Object.InputAuthority.PlayerId}, HasInputAuthority: {Object.HasInputAuthority}, Position: {transform.position}");

            // Initialize components for ALL players (needed for FixedUpdateNetwork)
            _hasAnimator = TryGetComponent(out _animator);
            _controller = GetComponent<CharacterController>();
            
            // Get StarterAssetsInputs component for THIS specific player instance
            _nativeInput = GetComponent<StarterAssetsInputs>();
            if (_nativeInput != null)
            {
                Debug.Log($"[Spawned] StarterAssetsInputs found on GameObject: {gameObject.name}, enabled: {_nativeInput.enabled}");
            }
            else
            {
                Debug.LogError($"[Spawned] StarterAssetsInputs component NOT found on GameObject: {gameObject.name}");
            }
            
            // Disable CharacterController temporarily to allow position to be set correctly
            if (_controller != null)
            {
                _controller.enabled = false;
            }
            
            Debug.Log($"Spawned: _controller is {(_controller == null ? "null" : "assigned")}, _animator is {(_animator == null ? "null" : "assigned")}");

            AssignAnimationIDs();
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;
            
            // Re-enable CharacterController after position is set
            if (_controller != null)
            {
                _controller.enabled = true;
                Debug.Log($"[ThirdPersonController] CharacterController re-enabled at position: {transform.position}");
            }

            // Enable/disable input components based on authority
            if (_nativeInput != null)
            {
                _nativeInput.enabled = Object.HasInputAuthority;
                Debug.Log($"[ThirdPersonController] StarterAssetsInputs enabled: {_nativeInput.enabled} for Player {Object.InputAuthority.PlayerId} on GameObject: {gameObject.name}");
            }
            else
            {
                Debug.LogError($"[ThirdPersonController] StarterAssetsInputs component is NULL for Player {Object.InputAuthority.PlayerId}");
            }

            // Initialize camera yaw for ALL players based on spawn rotation
            // This is critical for correct movement direction regardless of spawn rotation
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
                        Debug.Log($"[ThirdPersonController] CinemachineCameraTarget assigned: {CinemachineCameraTarget.name}");
                    }
                }

                // Log initial spawn rotation and camera setup
                Debug.Log($"[ThirdPersonController] SPAWN DEBUG - Player {Object.InputAuthority.PlayerId}:");
                Debug.Log($"  Character rotation: {transform.eulerAngles}");
                Debug.Log($"  Camera yaw (_cinemachineTargetYaw): {_cinemachineTargetYaw}");
                Debug.Log($"  Camera pitch (_cinemachineTargetPitch): {_cinemachineTargetPitch}");
                if (_mainCamera != null)
                {
                    Debug.Log($"  MainCamera rotation: {_mainCamera.transform.eulerAngles}");
                }

                Debug.Log($"[ThirdPersonController] Setup camera for local player {Object.InputAuthority.PlayerId}");
            }
            else
            {
                Debug.Log($"[ThirdPersonController] Remote player spawned - Player {Object.InputAuthority.PlayerId}, spawn rotation: {transform.eulerAngles.y}, _cinemachineTargetYaw: {_cinemachineTargetYaw}");
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

                // Movement, jumping, and gravity should be simulated for all clients to see.
                JumpAndGravity(data);
                GroundedCheck();
                Move(data);
            }
            else
            {
                // If no input, still apply gravity and check grounded state
                JumpAndGravity(default);
                GroundedCheck();
                Move(default);
            }
        }

        public override void Render()
        {
            if (!Object.HasInputAuthority)
            {
                // Uncomment to see which players are being blocked
                // Debug.Log($"[ThirdPersonController] LateUpdate blocked for Player {Object.InputAuthority.PlayerId} - not local player");
                return;
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

            if (_hasAnimator)
            {
                _animator.SetBool(_animIDGrounded, Grounded);
            }
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

            Vector3 inputDirection = new Vector3(input.move.x, 0.0f, input.move.y).normalized;
            Vector3 targetDirection = Vector3.zero;

            if (input.move != Vector2.zero)
            {
                _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + _cinemachineTargetYaw;
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity, RotationSmoothTime);
                transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
                
                targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;
            }

            // Only server/host applies CharacterController.Move()
            // Clients (including local) get position from NetworkTransform
            if (Object.HasStateAuthority)
            {
                Vector3 horizontalMovement = targetDirection.normalized * (_speed * Runner.DeltaTime);
                Vector3 verticalMovement = new Vector3(0.0f, _verticalVelocity, 0.0f) * Runner.DeltaTime;
                _controller.Move(horizontalMovement + verticalMovement);
            }

            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, _animationBlend);
                _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
            }
        }

        private void JumpAndGravity(NetworkInputData input)
        {
            if (Grounded)
            {
                _fallTimeoutDelta = FallTimeout;
                if (_hasAnimator)
                {
                    _animator.SetBool(_animIDJump, false);
                    _animator.SetBool(_animIDFreeFall, false);
                }

                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                if (input.jump && _jumpTimeoutDelta <= 0.0f)
                {
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDJump, true);
                    }
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
                else
                {
                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDFreeFall, true);
                    }
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

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var data = new NetworkInputData();
            if (_nativeInput != null)
            {
                data.move = _nativeInput.move;
                data.look = _nativeInput.look;
                data.jump = _nativeInput.jump;
                data.sprint = _nativeInput.sprint;
                _nativeInput.jump = false;
            }
            else
            {
                Debug.LogError($"[OnInput] _nativeInput is NULL for Player {Object.InputAuthority.PlayerId}");
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
                if (Object.HasInputAuthority && CinemachineCameraTarget == null)
                {
                    Debug.LogWarning("[LateUpdate] CinemachineCameraTarget is NULL!");
                }
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
                float oldYaw = _cinemachineTargetYaw;
                _cinemachineTargetYaw += _latestInput.look.x * deltaTimeMultiplier;
                float verticalLook = _latestInput.look.y;
                if (!Object.HasStateAuthority) // If this is a client, invert the vertical input
                {
                    verticalLook *= -1;
                }
                _cinemachineTargetPitch += verticalLook * deltaTimeMultiplier;
                Debug.Log($"[LateUpdate] Camera rotation - oldYaw: {oldYaw}, newYaw: {_cinemachineTargetYaw}, lookInput: {_latestInput.look}");
            }

            _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);
            CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride, _cinemachineTargetYaw, 0.0f);
        }
    }
}