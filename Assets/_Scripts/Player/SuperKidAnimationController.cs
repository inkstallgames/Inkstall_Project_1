using UnityEngine;
using StarterAssets;

public class SuperKidAnimationController : MonoBehaviour
{
    [Header("Animation Parameters")]
    [SerializeField] private string idleParam = "IsIdle";
    [SerializeField] private string walkingParam = "IsWalking";
    [SerializeField] private string walkingRightForwardParam = "IsWalkingRightForward";
    [SerializeField] private string walkingRightParam = "IsWalkingRight";
    [SerializeField] private string walkingRightBackwardParam = "IsWalkingRightBackward";
    [SerializeField] private string walkingBackwardParam = "IsWalkingBackward";
    [SerializeField] private string walkingLeftBackwardParam = "IsWalkingLeftBackward";
    [SerializeField] private string walkingLeftParam = "IsWalkingLeft";
    [SerializeField] private string walkingLeftForwardParam = "IsWalkingLeftForward";
    [SerializeField] private string runningParam = "IsRunning";
    [SerializeField] private string runningRightForwardParam = "IsRunningRightForward";
    [SerializeField] private string runningLeftForwardParam = "IsRunningLeftForward";
    [SerializeField] private string jumpingParam = "IsJumping";

    [Header("Movement Settings")]
    [SerializeField] private float runThreshold = 0.1f; // Joystick magnitude threshold for running
    [SerializeField] private float directionThreshold = 0.1f; // Threshold for determining direction - lowered to match FirstPersonController

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // References
    private Animator animator;
    private StarterAssetsInputs starterAssetsInputs;
    private FirstPersonController firstPersonController;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        // Get references from parent (PlayerCapsule)
        Transform parent = transform.parent;
        if (parent != null)
        {
            starterAssetsInputs = parent.GetComponent<StarterAssetsInputs>();
            firstPersonController = parent.GetComponent<FirstPersonController>();
        }

        if (starterAssetsInputs == null)
        {
            Debug.LogError("StarterAssetsInputs not found on parent!");
        }

        if (firstPersonController == null)
        {
            Debug.LogError("FirstPersonController not found on parent!");
        }
    }

    private void Update()
    {
        UpdateAnimationState();
    }

    private void UpdateAnimationState()
    {
        if (animator == null || starterAssetsInputs == null)
            return;

        // Get input values
        Vector2 moveInput = starterAssetsInputs.move;
        bool isJumping = !firstPersonController.Grounded;

        // Calculate magnitude of movement
        float magnitude = moveInput.magnitude;
        // Use Vector2.zero check to match FirstPersonController's movement detection
        bool isMoving = moveInput != Vector2.zero;
        bool isRunning = magnitude > runThreshold;

        // Reset all animation states
        ResetAllAnimationStates();

        // Set jumping state first (highest priority)
        if (isJumping)
        {
            animator.SetBool(jumpingParam, true);
            if (showDebugLogs) Debug.Log($"Animation State: Jumping");
            return;
        }

        // If not moving, set to idle
        if (!isMoving)
        {
            animator.SetBool(idleParam, true);
            if (showDebugLogs) Debug.Log($"Animation State: Idle");
            return;
        }

        // Calculate the movement direction angle
        // Using Atan2(y, x) for standard mathematical angle calculation
        float angle = Mathf.Atan2(moveInput.y, moveInput.x) * Mathf.Rad2Deg;

        // Make sure angle is between 0 and 360
        if (angle < 0) angle += 360f;

        if (showDebugLogs) Debug.Log($"Move Input: {moveInput}, Magnitude: {magnitude}, Angle: {angle}");

        // Check if we're in the forward quadrant (30-150 degrees) for running
        bool isInForwardRunningQuadrant = angle >= 30f && angle <= 150f;

        // Apply animations based on the specified angle ranges
        if (angle >= 350f || angle < 10f)
        {
            // Walking Right (350-10 degrees)
            animator.SetBool(walkingRightParam, true);
            if (showDebugLogs) Debug.Log($"Animation State: Walking Right");
        }
        else if (angle >= 10f && angle < 70f)
        {
            // Right-Forward (10-70 degrees)
            if (isRunning && angle >= 40f && angle < 70f)
            {
                // Running Right-Forward (30-70 degrees with high magnitude)
                animator.SetBool(runningRightForwardParam, true);
                if (showDebugLogs) Debug.Log($"Animation State: Running Right Forward");
            }
            else
            {
                // Walking Right-Forward
                animator.SetBool(walkingRightForwardParam, true);
                if (showDebugLogs) Debug.Log($"Animation State: Walking Right Forward");
            }
        }
        else if (angle >= 70f && angle <= 110f)
        {
            // Forward (70-110 degrees)
            if (isRunning)
            {
                // Running Forward
                animator.SetBool(runningParam, true);
                if (showDebugLogs) Debug.Log($"Animation State: Running Forward");
            }
            else
            {
                // Walking Forward
                animator.SetBool(walkingParam, true);
                if (showDebugLogs) Debug.Log($"Animation State: Walking Forward");
            }
        }
        else if (angle > 110f && angle < 170f)
        {
            // Left-Forward (110-150 degrees)
            if (isRunning && angle >= 110f && angle <= 140f)
            {
                // Running Left-Forward (110-150 degrees with high magnitude)
                animator.SetBool(runningLeftForwardParam, true);
                if (showDebugLogs) Debug.Log($"Animation State: Running Left Forward");
            }
            else
            {
                // Walking Left-Forward
                animator.SetBool(walkingLeftForwardParam, true);
                if (showDebugLogs) Debug.Log($"Animation State: Walking Left Forward");
            }
        }
        else if (angle >= 170f && angle < 190f)
        {
            // Walking Left (170-190 degrees)
            animator.SetBool(walkingLeftParam, true);
            if (showDebugLogs) Debug.Log($"Animation State: Walking Left");
        }
        else if (angle >= 190f && angle < 260f)
        {
            // Walking Left-Backward (190-260 degrees)
            animator.SetBool(walkingLeftBackwardParam, true);
            if (showDebugLogs) Debug.Log($"Animation State: Walking Left Backward");
        }
        else if (angle >= 260f && angle < 280f)
        {
            // Walking Backward (260-280 degrees)
            animator.SetBool(walkingBackwardParam, true);
            if (showDebugLogs) Debug.Log($"Animation State: Walking Backward");
        }
        else if (angle >= 280f && angle < 350f)
        {
            // Walking Right-Backward (280-350 degrees)
            animator.SetBool(walkingRightBackwardParam, true);
            if (showDebugLogs) Debug.Log($"Animation State: Walking Right Backward");
        }
    }

    private void ResetAllAnimationStates()
    {
        animator.SetBool(idleParam, false);
        animator.SetBool(walkingParam, false);
        animator.SetBool(walkingRightForwardParam, false);
        animator.SetBool(walkingRightParam, false);
        animator.SetBool(walkingRightBackwardParam, false);
        animator.SetBool(walkingBackwardParam, false);
        animator.SetBool(walkingLeftBackwardParam, false);
        animator.SetBool(walkingLeftParam, false);
        animator.SetBool(walkingLeftForwardParam, false);
        animator.SetBool(runningParam, false);
        animator.SetBool(runningRightForwardParam, false);
        animator.SetBool(runningLeftForwardParam, false);
        animator.SetBool(jumpingParam, false);
    }
}
