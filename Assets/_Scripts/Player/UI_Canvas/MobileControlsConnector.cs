using UnityEngine;
using StarterAssets;

public class MobileControlsConnector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UIVirtualJoystick moveJoystick;
    [SerializeField] private UIVirtualButton jumpButton;
    [SerializeField] private UICanvasControllerInput canvasInput;
    [SerializeField] private Transform playerCapsule;

    private StarterAssetsInputs starterAssetsInputs;
    private FirstPersonController firstPersonController;
    private SuperKidAnimationController animationController;

    [Header("Joystick Settings")]
    [SerializeField] private float runThreshold = 0.7f;
    [SerializeField] private bool autoSprintWhenJoystickPushedFar = true;

    private void Awake()
    {
        // Find references if not assigned
        if (moveJoystick == null)
        {
            moveJoystick = FindObjectOfType<UIVirtualJoystick>();
        }

        if (jumpButton == null)
        {
            jumpButton = FindObjectOfType<UIVirtualButton>();
        }

        if (canvasInput == null)
        {
            canvasInput = FindObjectOfType<UICanvasControllerInput>();
        }

        if (playerCapsule == null)
        {
            // Try to find the player capsule by tag or name
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerCapsule = player.transform;
            }
        }

        // Get components from player
        if (playerCapsule != null)
        {
            starterAssetsInputs = playerCapsule.GetComponent<StarterAssetsInputs>();
            firstPersonController = playerCapsule.GetComponent<FirstPersonController>();
            
            // Find the Super_Kid child object
            Transform superKid = playerCapsule.Find("Super_Kid");
            if (superKid != null)
            {
                animationController = superKid.GetComponent<SuperKidAnimationController>();
                if (animationController == null)
                {
                    animationController = superKid.gameObject.AddComponent<SuperKidAnimationController>();
                }
            }
        }
    }

    private void Start()
    {
        // Set up joystick event
        if (moveJoystick != null)
        {
            moveJoystick.joystickOutputEvent.AddListener(OnJoystickMoved);
        }

        // Set up jump button event
        if (jumpButton != null)
        {
            jumpButton.buttonStateOutputEvent.AddListener(OnJumpButtonPressed);
        }

        // Sync run threshold with FirstPersonController
        if (firstPersonController != null)
        {
            runThreshold = firstPersonController.runThreshold;
        }
    }

    private void OnJoystickMoved(Vector2 joystickValue)
    {
        if (starterAssetsInputs == null) return;

        // Pass joystick value to input system
        starterAssetsInputs.MoveInput(joystickValue);

        // Auto sprint when joystick is pushed far
        if (autoSprintWhenJoystickPushedFar)
        {
            bool shouldSprint = joystickValue.magnitude > runThreshold;
            starterAssetsInputs.SprintInput(shouldSprint);
        }
    }

    private void OnJumpButtonPressed(bool isPressed)
    {
        if (starterAssetsInputs == null) return;

        // Pass jump button state to input system
        starterAssetsInputs.JumpInput(isPressed);
    }
}
