using UnityEngine;
using StarterAssets;
using UnityEngine.EventSystems;

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

    [Header("Camera Control Settings")]
    [SerializeField] private float touchSensitivity = 1.0f;
    [SerializeField] private float minSwipeDelta = 5f;
    
    private Vector2 touchStartPos;
    private bool isTouchingRightSide = false;

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
            
            // Sync camera control settings if available
            touchSensitivity = firstPersonController.touchSensitivity;
            minSwipeDelta = firstPersonController.minTouchDelta;
        }
    }

    private void Update()
    {
        HandleCameraControl();
    }

    private void HandleCameraControl()
    {
        if (starterAssetsInputs == null) return;

        // Handle touch input for camera look
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);

                // Check if touch is over UI (ignore if it's over UI elements)
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                    continue;

                // Only process touches on the right side of the screen
                bool isRightSide = touch.position.x > Screen.width / 2;
                if (!isRightSide) continue;

                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        touchStartPos = touch.position;
                        isTouchingRightSide = true;
                        break;

                    case TouchPhase.Moved:
                        if (isTouchingRightSide)
                        {
                            Vector2 delta = touch.position - touchStartPos;

                            // Only process if movement exceeds minimum delta
                            if (delta.magnitude > minSwipeDelta)
                            {
                                // Invert the Y-axis for natural camera control
                                delta.y = -delta.y;
                                
                                // Pass the look input to the StarterAssetsInputs
                                starterAssetsInputs.LookInput(delta * touchSensitivity * Time.deltaTime);
                                
                                // Update start position to prevent huge jumps
                                touchStartPos = touch.position;
                            }
                        }
                        break;

                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        isTouchingRightSide = false;
                        starterAssetsInputs.LookInput(Vector2.zero);
                        break;
                }

                // We only need one touch for camera control
                if (isTouchingRightSide) break;
            }
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
