using UnityEngine;
using StarterAssets;

public class PlayerMovementController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private StarterAssetsInputs _input;
    [SerializeField] private FirstPersonController _fpsController;
    [SerializeField] private Animator _animator;

    [Header("Movement Settings")]
    [SerializeField] private float _walkThreshold = 0.3f; // Minimum joystick magnitude to start walking
    [SerializeField] private float _runThreshold = 0.8f;  // Minimum joystick magnitude to start running
    [SerializeField] private float _strafeThreshold = 0.5f; // Threshold to determine if moving sideways

    [Header("Animation Parameters")]
    [SerializeField] private string _idleParam = "IsIdle";
    [SerializeField] private string _walkForwardParam = "IsWalking";
    [SerializeField] private string _walkBackwardParam = "IsWalkingBackward";
    [SerializeField] private string _walkLeftParam = "IsWalkingLeft";
    [SerializeField] private string _walkRightParam = "IsWalkingRight";
    [SerializeField] private string _walkLeftForwardParam = "IsWalkingLeftForward";
    [SerializeField] private string _walkRightForwardParam = "IsWalkingRightForward";
    [SerializeField] private string _walkLeftBackwardParam = "IsWalkingLeftBackward";
    [SerializeField] private string _walkRightBackwardParam = "IsWalkingRightBackward";
    [SerializeField] private string _runForwardParam = "IsRunning";
    [SerializeField] private string _runLeftForwardParam = "IsRunningLeftForward";
    [SerializeField] private string _runRightForwardParam = "IsRunningRightForward";
    [SerializeField] private string _jumpParam = "IsJumping";

    // Movement state tracking
    private MovementState _currentState = MovementState.Idle;

    private enum MovementState
    {
        Idle,
        WalkForward,
        WalkBackward,
        WalkLeft,
        WalkRight,
        WalkLeftForward,
        WalkRightForward,
        WalkLeftBackward,
        WalkRightBackward,
        RunForward,
        RunLeftForward,
        RunRightForward,
        Jumping
    }

    private void Awake()
    {
        // Get references if not set in inspector
        if (_input == null)
            _input = GetComponent<StarterAssetsInputs>();
        
        if (_fpsController == null)
            _fpsController = GetComponent<FirstPersonController>();
        
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        UpdateMovementState();
        UpdateAnimations();
    }

    private void UpdateMovementState()
    {
        // Get input magnitude and direction
        Vector2 moveInput = _input.move;
        float magnitude = moveInput.magnitude;

        // Check if player is jumping
        bool isJumping = !_fpsController.Grounded;
        if (isJumping)
        {
            _currentState = MovementState.Jumping;
            return;
        }

        // If not moving, set to idle
        if (magnitude < _walkThreshold)
        {
            _currentState = MovementState.Idle;
            return;
        }

        // Determine if running based on sprint input or joystick magnitude
        bool isRunning = _input.sprint || magnitude > _runThreshold;

        // Determine movement direction based on joystick input
        float forwardAmount = moveInput.y;
        float rightAmount = moveInput.x;
        
        // Forward/backward movement
        bool isForward = forwardAmount > _walkThreshold;
        bool isBackward = forwardAmount < -_walkThreshold;
        
        // Left/right movement
        bool isRight = rightAmount > _walkThreshold;
        bool isLeft = rightAmount < -_walkThreshold;

        // Determine the movement state based on direction and running state
        if (isForward)
        {
            if (isLeft)
            {
                _currentState = isRunning && magnitude > _runThreshold ? 
                    MovementState.RunLeftForward : MovementState.WalkLeftForward;
            }
            else if (isRight)
            {
                _currentState = isRunning && magnitude > _runThreshold ? 
                    MovementState.RunRightForward : MovementState.WalkRightForward;
            }
            else
            {
                _currentState = isRunning && magnitude > _runThreshold ? 
                    MovementState.RunForward : MovementState.WalkForward;
            }
        }
        else if (isBackward)
        {
            if (isLeft)
            {
                _currentState = MovementState.WalkLeftBackward;
            }
            else if (isRight)
            {
                _currentState = MovementState.WalkRightBackward;
            }
            else
            {
                _currentState = MovementState.WalkBackward;
            }
        }
        else
        {
            if (isLeft)
            {
                _currentState = MovementState.WalkLeft;
            }
            else if (isRight)
            {
                _currentState = MovementState.WalkRight;
            }
        }
    }

    private void UpdateAnimations()
    {
        if (_animator == null)
            return;

        // Reset all animation parameters
        _animator.SetBool(_idleParam, false);
        _animator.SetBool(_walkForwardParam, false);
        _animator.SetBool(_walkBackwardParam, false);
        _animator.SetBool(_walkLeftParam, false);
        _animator.SetBool(_walkRightParam, false);
        _animator.SetBool(_walkLeftForwardParam, false);
        _animator.SetBool(_walkRightForwardParam, false);
        _animator.SetBool(_walkLeftBackwardParam, false);
        _animator.SetBool(_walkRightBackwardParam, false);
        _animator.SetBool(_runForwardParam, false);
        _animator.SetBool(_runLeftForwardParam, false);
        _animator.SetBool(_runRightForwardParam, false);
        _animator.SetBool(_jumpParam, false);

        // Set the current animation parameter
        switch (_currentState)
        {
            case MovementState.Idle:
                _animator.SetBool(_idleParam, true);
                break;
            case MovementState.WalkForward:
                _animator.SetBool(_walkForwardParam, true);
                break;
            case MovementState.WalkBackward:
                _animator.SetBool(_walkBackwardParam, true);
                break;
            case MovementState.WalkLeft:
                _animator.SetBool(_walkLeftParam, true);
                break;
            case MovementState.WalkRight:
                _animator.SetBool(_walkRightParam, true);
                break;
            case MovementState.WalkLeftForward:
                _animator.SetBool(_walkLeftForwardParam, true);
                break;
            case MovementState.WalkRightForward:
                _animator.SetBool(_walkRightForwardParam, true);
                break;
            case MovementState.WalkLeftBackward:
                _animator.SetBool(_walkLeftBackwardParam, true);
                break;
            case MovementState.WalkRightBackward:
                _animator.SetBool(_walkRightBackwardParam, true);
                break;
            case MovementState.RunForward:
                _animator.SetBool(_runForwardParam, true);
                break;
            case MovementState.RunLeftForward:
                _animator.SetBool(_runLeftForwardParam, true);
                break;
            case MovementState.RunRightForward:
                _animator.SetBool(_runRightForwardParam, true);
                break;
            case MovementState.Jumping:
                _animator.SetBool(_jumpParam, true);
                break;
        }
    }

    // This method can be called from UI events for debugging
    public void LogCurrentState()
    {
        Debug.Log($"Current Movement State: {_currentState}");
    }
}
