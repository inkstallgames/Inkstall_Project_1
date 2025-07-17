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
    [SerializeField] private float runThreshold = 0.7f; // Joystick magnitude threshold for running
    [SerializeField] private float directionThreshold = 0.3f; // Threshold for determining direction

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
        bool isMoving = magnitude > 0.1f;
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
        
        // Normalize the input for direction calculation
        Vector2 normalizedInput = moveInput.normalized;
        
        // Calculate the movement direction angle
        float angle = Mathf.Atan2(normalizedInput.x, normalizedInput.y) * Mathf.Rad2Deg;
        
        // Make sure angle is between 0 and 360
        if (angle < 0) angle += 360f;
        
        if (showDebugLogs) Debug.Log($"Move Input: {moveInput}, Magnitude: {magnitude}, Angle: {angle}");
        
        // Determine direction based on angle
        if (isRunning)
        {
            // Running animations - we have fewer running animations, so use broader angle ranges
            if (angle >= 315 || angle < 45) // Forward
            {
                animator.SetBool(runningParam, true);
                if (showDebugLogs) Debug.Log($"Animation State: Running Forward");
            }
            else if (angle >= 45 && angle < 135) // Right
            {
                animator.SetBool(runningRightForwardParam, true);
                if (showDebugLogs) Debug.Log($"Animation State: Running Right Forward");
            }
            else if (angle >= 225 && angle < 315) // Left
            {
                animator.SetBool(runningLeftForwardParam, true);
                if (showDebugLogs) Debug.Log($"Animation State: Running Left Forward");
            }
            else // Backward - use walking backward since we don't have running backward
            {
                animator.SetBool(walkingBackwardParam, true);
                if (showDebugLogs) Debug.Log($"Animation State: Walking Backward (No Running Backward)");
            }
        }
        else
        {
            // Walking animations - more precise angle ranges for 8-direction movement
            if (angle >= 337.5 || angle < 22.5) // Forward
            {
                animator.SetBool(walkingParam, true);
                if (showDebugLogs) Debug.Log($"Animation State: Walking Forward");
            }
            else if (angle >= 22.5 && angle < 67.5) // Forward-Right
            {
                animator.SetBool(walkingRightForwardParam, true);
                if (showDebugLogs) Debug.Log($"Animation State: Walking Right Forward");
            }
            else if (angle >= 67.5 && angle < 112.5) // Right
            {
                animator.SetBool(walkingRightParam, true);
                if (showDebugLogs) Debug.Log($"Animation State: Walking Right");
            }
            else if (angle >= 112.5 && angle < 157.5) // Backward-Right
            {
                animator.SetBool(walkingRightBackwardParam, true);
                if (showDebugLogs) Debug.Log($"Animation State: Walking Right Backward");
            }
            else if (angle >= 157.5 && angle < 202.5) // Backward
            {
                animator.SetBool(walkingBackwardParam, true);
                if (showDebugLogs) Debug.Log($"Animation State: Walking Backward");
            }
            else if (angle >= 202.5 && angle < 247.5) // Backward-Left
            {
                animator.SetBool(walkingLeftBackwardParam, true);
                if (showDebugLogs) Debug.Log($"Animation State: Walking Left Backward");
            }
            else if (angle >= 247.5 && angle < 292.5) // Left
            {
                animator.SetBool(walkingLeftParam, true);
                if (showDebugLogs) Debug.Log($"Animation State: Walking Left");
            }
            else if (angle >= 292.5 && angle < 337.5) // Forward-Left
            {
                animator.SetBool(walkingLeftForwardParam, true);
                if (showDebugLogs) Debug.Log($"Animation State: Walking Left Forward");
            }
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
