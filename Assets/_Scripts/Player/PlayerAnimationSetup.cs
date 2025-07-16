using UnityEngine;

public class PlayerAnimationSetup : MonoBehaviour
{
    [Header("Animation Clips")]
    [SerializeField] private AnimationClip idleAnimation;
    [SerializeField] private AnimationClip walkForwardAnimation;
    [SerializeField] private AnimationClip walkBackwardAnimation;
    [SerializeField] private AnimationClip walkLeftAnimation;
    [SerializeField] private AnimationClip walkRightAnimation;
    [SerializeField] private AnimationClip walkLeftForwardAnimation;
    [SerializeField] private AnimationClip walkRightForwardAnimation;
    [SerializeField] private AnimationClip walkLeftBackwardAnimation;
    [SerializeField] private AnimationClip walkRightBackwardAnimation;
    [SerializeField] private AnimationClip runForwardAnimation;
    [SerializeField] private AnimationClip runLeftForwardAnimation;
    [SerializeField] private AnimationClip runRightForwardAnimation;
    [SerializeField] private AnimationClip jumpAnimation;

    [Header("Animation Settings")]
    [SerializeField] private float transitionDuration = 0.25f;
    [SerializeField] private float animationSpeed = 1.0f;

    // Reference to the animator
    private Animator _animator;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        if (_animator == null)
        {
            _animator = gameObject.AddComponent<Animator>();
        }

        // Verify we have all necessary animations
        ValidateAnimations();
    }

    private void ValidateAnimations()
    {
        if (idleAnimation == null)
        {
            Debug.LogWarning("Idle animation is missing!");
        }

        if (walkForwardAnimation == null)
        {
            Debug.LogWarning("Walk forward animation is missing!");
        }

        // Add validation for other animations as needed
    }

    // This method can be called from the editor to apply animations to the animator controller
    public void SetupAnimatorController()
    {
        if (_animator == null)
        {
            Debug.LogError("Animator component not found!");
            return;
        }

        // Here you would typically create or modify an animator controller at runtime
        // This is usually done in the Unity Editor rather than at runtime
        // For runtime, you would need to use Animator.runtimeAnimatorController and modify it

        Debug.Log("Animation setup would be done here. This is typically done in the Unity Editor.");
        Debug.Log("Make sure to create an Animator Controller with parameters matching those in PlayerMovementController.");
    }

    // Helper method to show what parameters should be in your animator controller
    public void LogRequiredParameters()
    {
        Debug.Log("Required Animator Parameters:");
        Debug.Log("- IsIdle (bool)");
        Debug.Log("- IsWalkingForward (bool)");
        Debug.Log("- IsWalkingBackward (bool)");
        Debug.Log("- IsWalkingLeft (bool)");
        Debug.Log("- IsWalkingRight (bool)");
        Debug.Log("- IsWalkingLeftForward (bool)");
        Debug.Log("- IsWalkingRightForward (bool)");
        Debug.Log("- IsWalkingLeftBackward (bool)");
        Debug.Log("- IsWalkingRightBackward (bool)");
        Debug.Log("- IsRunningForward (bool)");
        Debug.Log("- IsRunningLeftForward (bool)");
        Debug.Log("- IsRunningRightForward (bool)");
        Debug.Log("- IsJumping (bool)");
    }
}
