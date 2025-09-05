using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float rayDistance = 50f;
    [SerializeField] private Camera playerMainCamera;
    [SerializeField] private float interactDistance = 3f;
    
    [SerializeField] private GameObject interactButton;
    [SerializeField] private GameObject useKeyButton; // Reference to the use key button
    [SerializeField] private TextMeshProUGUI interactionText; // Text to show when room is completed
    [SerializeField] private Image crosshairImage;  // Reference the crosshair Image component

    private DoorInteraction currentDoor;   // Track which door we're looking at
    private DrawerMech currentDrawer;      // Track which drawer we're looking at
    
    private bool showUseKeyButton = false; // Flag to track if we should show the use key button

    private void Start()
    {
        if (playerMainCamera == null)
        {
            playerMainCamera = Camera.main;
            if (playerMainCamera == null)
            {
                enabled = false;
                return;
            }
        }

        // Make sure the buttons are initially disabled
        if (interactButton != null)
        {
            interactButton.SetActive(false);

            // Add click listener to the open/close button
            Button interactButtonComponent = interactButton.GetComponent<Button>();
            if (interactButtonComponent != null)
            {
                interactButtonComponent.onClick.AddListener(InteractWithCurrentDoor);
            }
            else
            {
                Debug.LogError("Interact button doesn't have a Button component!");
            }
        }

        // Setup use key button
        if (useKeyButton != null)
        {
            useKeyButton.SetActive(false);

            // Add click listener to the use key button
            Button useKeyButtonComponent = useKeyButton.GetComponent<Button>();
            if (useKeyButtonComponent != null)
            {
                useKeyButtonComponent.onClick.AddListener(UnlockCurrentDoor);
            }
            else
            {
                Debug.LogError("Use Key button doesn't have a Button component!");
            }
        }
        
        // Make sure the interaction text is initially hidden
        if (interactionText != null)
        {
            interactionText.gameObject.SetActive(false);
        }

        // Make sure the crosshair is visible at start
        if (crosshairImage != null)
        {
            crosshairImage.gameObject.SetActive(true);
        }
    }

    private void Update()
    {
        CheckRaycastInteraction();
    }

    private void CheckRaycastInteraction()
    {
        // Store the previous door reference to check if we're still looking at the same door
        DoorInteraction previousDoor = currentDoor;
        DrawerMech previousDrawer = currentDrawer;

        // Reset current door reference
        currentDoor = null;
        currentDrawer = null;

        // Disable buttons and text by default
        if (interactButton != null)
        {
            interactButton.SetActive(false);
        }

        if (useKeyButton != null)
        {
            useKeyButton.SetActive(false);
        }
        
        if (interactionText != null)
        {
            interactionText.gameObject.SetActive(false);
        }

        // Show crosshair when no interaction text is showing
        if (crosshairImage != null)
        {
            crosshairImage.gameObject.SetActive(true);
        }

        // Cast ray from camera center (where crosshair is)
        Ray ray = new Ray(playerMainCamera.transform.position, playerMainCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance))
        {
            GameObject hitObject = hit.collider.gameObject;

            // Check if we hit a door
            if (hitObject.CompareTag("Door"))
            {
                // Get the door interaction component
                DoorInteraction doorInteraction = hitObject.GetComponent<DoorInteraction>();
                if (doorInteraction != null && doorInteraction.enabled)
                {
                    // Check if we're within interaction distance
                    float distanceToObject = Vector3.Distance(transform.position, hitObject.transform.position);
                    if (distanceToObject <= interactDistance)
                    {
                        // We're looking at a door and within range
                        currentDoor = doorInteraction;
                        
                        // Check if the room is completed
                        if (doorInteraction.isRoomCompleted)
                        {
                            // Show completed room message instead of interaction buttons
                            if (interactionText != null)
                            {
                                interactionText.gameObject.SetActive(true);
                                interactionText.text = "Room Already Completed";
                                // Hide crosshair when showing interaction text
                                if (crosshairImage != null)
                                {
                                    crosshairImage.gameObject.SetActive(false);
                                }
                            }
                        }
                        else if (doorInteraction.IsLocked() && !doorInteraction.IsUnlockable())
                        {
                            // Show message for locked and not yet unlockable door
                            if (interactionText != null)
                            {
                                interactionText.gameObject.SetActive(true);
                                interactionText.text = "Complete Previous Room First";
                                // Hide crosshair when showing interaction text
                                if (crosshairImage != null)
                                {
                                    crosshairImage.gameObject.SetActive(false);
                                }
                            }
                        }
                        else
                        {
                            // Show normal interaction buttons
                            interactButton.SetActive(true);

                            // Only show use key button if:
                            // 1. The door is locked
                            // 2. We've previously tried to open this specific door (showUseKeyButton is true)
                            // 3. We're still looking at the same door as before
                            if (doorInteraction.IsLocked() && showUseKeyButton && doorInteraction == previousDoor)
                            {
                                useKeyButton.SetActive(true);
                                
                                // Show interaction text for using key
                                if (interactionText != null)
                                {
                                    interactionText.gameObject.SetActive(true);
                                    interactionText.text = "Press [Key] to Unlock Door";
                                    
                                    // Hide crosshair when showing interaction text
                                    if (crosshairImage != null)
                                    {
                                        crosshairImage.gameObject.SetActive(false);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            // Check if we hit a building collider
            else if (hitObject.CompareTag("BuildingCollider"))
            {
                // Check if we're within interaction distance
                float distanceToObject = Vector3.Distance(transform.position, hitObject.transform.position);
                if (distanceToObject <= interactDistance)
                {
                    // Show building collider message
                    if (interactionText != null)
                    {
                        interactionText.gameObject.SetActive(true);
                        interactionText.text = "Complete Levels on this Floor First to go ahead";
                        // Hide crosshair when showing interaction text
                        if (crosshairImage != null)
                        {
                            crosshairImage.gameObject.SetActive(false);
                        }
                    }
                }
            }
            // Check if we hit a building collider
            else if (hitObject.CompareTag("BuildingPortal"))
            {
                // Check if we're within interaction distance
                float distanceToObject = Vector3.Distance(transform.position, hitObject.transform.position);
                if (distanceToObject <= interactDistance)
                {
                    // Show building collider message
                    if (interactionText != null)
                    {
                        interactionText.gameObject.SetActive(true);
                        interactionText.text = "if you Enter you will lose your progress";
                        // Hide crosshair when showing interaction text
                        if (crosshairImage != null)
                        {
                            crosshairImage.gameObject.SetActive(false);
                        }
                    }
                }
            }
            // Check for drawer 
            else if (hitObject.CompareTag("SlidingDoor") && GameTimer.instance.timerRunning)
            {
                // Get the drawer interaction component
                DrawerMech drawerInteraction = hitObject.GetComponent<DrawerMech>();
                if (drawerInteraction != null && drawerInteraction.enabled)
                {
                    // Check if we're within interaction distance
                    float distanceToObject = Vector3.Distance(transform.position, hitObject.transform.position);
                    if (distanceToObject <= interactDistance)
                    {
                        // We're looking at a drawer and within range
                        Debug.Log("We are looking at the drawer and also wihin range");
                        currentDrawer = drawerInteraction;
                        interactButton.SetActive(true);
                    }
                }
            }

            // If we're no longer looking at the same door, reset the showUseKeyButton flag
            if (currentDoor != previousDoor)
            {
                showUseKeyButton = false;
            }

            // If we're no longer looking at the same drawer, reset the showUseKeyButton flag
            if (currentDrawer != previousDrawer)
            {
                showUseKeyButton = false;
            }
        }
    }

    // Method to interact with the current door
    private void InteractWithCurrentDoor()
    {
        if (currentDoor != null)
        {
            // Try to open the door
            currentDoor.TryOpenDoor();

            // If the door is locked and unlockable, enable the use key button
            if (currentDoor.IsLocked() && currentDoor.IsUnlockable())
            {
                showUseKeyButton = true;

                // Immediately show the use key button if we're still looking at the door
                if (useKeyButton != null)
                {
                    useKeyButton.SetActive(true);
                    
                    // Show interaction text for using key
                    if (interactionText != null)
                    {
                        interactionText.gameObject.SetActive(true);
                        interactionText.text = "Press Key to Unlock the Door";
                        
                        // Hide crosshair when showing interaction text
                        if (crosshairImage != null)
                        {
                            crosshairImage.gameObject.SetActive(false);
                        }
                    }
                }
            }
        }
        else if (currentDrawer != null)
        {
            // If we're looking at a drawer instead of a door, interact with it
            InteractWithCurrentDrawer();
        }
    }

    // Method to unlock the current door
    private void UnlockCurrentDoor()
    {
        if (currentDoor != null && currentDoor.IsLocked())
        {
            currentDoor.TryUnlockDoor();

            // Reset the flag since the door is now unlocked
            showUseKeyButton = false;
        }
    }

    private void InteractWithCurrentDrawer()
    {
        if (currentDrawer != null)
        {
            // Toggle the drawer open/close
            currentDrawer.ToggleDrawerOpenClose();
        }
    }
}
