using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float rayDistance = 10f;
    [SerializeField] private Camera playerMainCamera;
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private LayerMask interactionLayers = ~0; // Default to all layers

    [SerializeField] private GameObject interactButton;
    [SerializeField] private GameObject useKeyButton;
    [SerializeField] private TextMeshProUGUI interactionText; // Text to show when room is completed
    [SerializeField] private GameObject gameTimer; // Reference to the game timer
    [SerializeField] private Image crosshairImage;  // Reference the crosshair Image component

    private DoorInteraction doorInteraction;   // Track which door we're looking at
    private DrawerMech drawerMech;      // Track which drawer we're looking at

    private float nextRaycastTime;
    [SerializeField] private float raycastInterval = 0.1f;

    
    
    private void Start()
    {
        // Cache the main camera at start
        playerMainCamera = playerMainCamera != null ? playerMainCamera : Camera.main;
        if (playerMainCamera == null)
        {
            Debug.LogError("No camera assigned and no main camera found in the scene!");
            enabled = false;
            return;
        }

        // Make sure the buttons are initially disabled
        if (interactButton != null)
        {
            interactButton.SetActive(false);

            // Add click listener to the open/close button
            Button interactButtonComponent = interactButton.GetComponent<Button>();
            if (interactButtonComponent != null)
            {
                interactButtonComponent.onClick.AddListener(OnInteractButtonClicked);
            }
            else
            {
                Debug.LogError("Interact button doesn't have a Button component!");
            }
        }

        if (useKeyButton != null)
        {
            useKeyButton.SetActive(false);
            Button useKeyBtnComponent = useKeyButton.GetComponent<Button>();
            if (useKeyBtnComponent != null)
            {
                useKeyBtnComponent.onClick.AddListener(OnUseKeyButtonClicked);
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
        if (Time.time >= nextRaycastTime)
        {
            CheckRaycastInteraction();
            nextRaycastTime = Time.time + raycastInterval;
        }

    }

    private void CheckRaycastInteraction()
    {
        // Store the previous door reference to check if we're still looking at the same door
        DoorInteraction previousDoor = doorInteraction;
        DrawerMech previousDrawer = drawerMech;

        // Reset current door reference
        doorInteraction = null;
        drawerMech = null;
        

        // Cache camera transform for better performance
        Transform cameraTransform = playerMainCamera.transform;
        
        // Cast ray from camera center with layer mask
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, interactionLayers))
        {
            GameObject hitObject = hit.collider.gameObject;

            // Check if we hit a door
            if (hitObject.CompareTag("Door"))
            {
                DoorInteraction rayDoorInteraction = hitObject.GetComponent<DoorInteraction>();
                if (rayDoorInteraction != null && rayDoorInteraction.enabled)
                {
                    float distanceSqr = (transform.position - hitObject.transform.position).sqrMagnitude;
                    if (distanceSqr <= interactDistance * interactDistance)
                    {
                        this.doorInteraction = rayDoorInteraction;

                        // Check the state of the door to determine which button to show.
                        if (rayDoorInteraction.IsLocked() && rayDoorInteraction.IsUnlockable())
                        {
                            // If the door is locked AND unlockable, show the 'Use Key' button.
                            if (useKeyButton != null) useKeyButton.SetActive(true);
                            if (interactButton != null) interactButton.SetActive(false);
                        }
                        else
                        {
                            // For all other cases (unlocked, or locked but not unlockable), show the 'Interact' button.
                            if (interactButton != null) interactButton.SetActive(true);
                            if (useKeyButton != null) useKeyButton.SetActive(false);
                        }
                    }
                    else
                    {
                        // Player is looking at a door but is out of range.
                        if (interactButton != null) interactButton.SetActive(false);
                        if (useKeyButton != null) useKeyButton.SetActive(false);
                    }
                }
            }


            // Check for drawer 
            else if (hitObject.CompareTag("SlidingDoor"))
            {
                // Get the drawer interaction component
                DrawerMech drawerInteraction = hitObject.GetComponent<DrawerMech>();
                if (drawerInteraction != null && drawerInteraction.enabled)
                {
                    // Check if we're within interaction distance (using sqrMagnitude for better performance)
                    float distanceSqr = (transform.position - hitObject.transform.position).sqrMagnitude;
                    if (distanceSqr <= interactDistance * interactDistance)
                    {
                        // We're looking at a drawer and within range
                        drawerMech = drawerInteraction;
                        if(interactButton != null && !interactButton.activeSelf)
                        {
                            interactButton.SetActive(true);
                        }
                    }
                    else
                    {
                        if(interactButton != null && interactButton.activeSelf)
                        {
                            interactButton.SetActive(false);
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
                    if (gameTimer != null && gameTimer.activeInHierarchy)
                    {
                        // Show building collider message
                        if (interactionText != null)
                        {
                            interactionText.gameObject.SetActive(true);
                            interactionText.text = "if you Enter you will lose your progress";
                            // Hide crosshair when showing interaction text
                            if (crosshairImage != null )
                            {
                                crosshairImage.gameObject.SetActive(false);
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
                    if (interactionText != null && !interactionText.gameObject.activeSelf)
                    {
                        interactionText.gameObject.SetActive(true);
                        interactionText.text = "Complete Levels on this Floor First to go ahead";
                        // Hide crosshair when showing interaction text
                        if (crosshairImage != null && crosshairImage.gameObject.activeSelf)
                        {
                            crosshairImage.gameObject.SetActive(false);
                        }
                    }
                }
            } 
            else
            {
                if(interactButton != null && interactButton.gameObject.activeSelf)
               { 
                interactButton.SetActive(false);
               }
               if(useKeyButton != null && useKeyButton.gameObject.activeSelf)
               {
                useKeyButton.SetActive(false);
               }
               if(interactionText != null && interactionText.gameObject.activeSelf)
               {
                interactionText.gameObject.SetActive(false);
               }
               if(crosshairImage != null && !crosshairImage.gameObject.activeSelf)
               {
                crosshairImage.gameObject.SetActive(true);
               }
            }     
        }
    }

    // Called when the interact button is clicked
    public void OnInteractButtonClicked()
    {
        if (doorInteraction != null)
        {
            doorInteraction.Interact();
        }
        else if (drawerMech != null)
        {
            drawerMech.Interact();
        }
    }

    private void OnUseKeyButtonClicked()
    {
        if (doorInteraction != null)
        {
            doorInteraction.UnlockDoor();
            useKeyButton.SetActive(false);
            if (interactionText != null)
            {
                interactionText.gameObject.SetActive(false);
            }
            if (crosshairImage != null)
            {
                crosshairImage.gameObject.SetActive(true);
            }
        }
    }

}
