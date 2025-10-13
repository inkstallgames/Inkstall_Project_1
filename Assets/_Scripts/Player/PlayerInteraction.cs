using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float rayDistance = 20f;
    [SerializeField] private Camera playerMainCamera;
    [SerializeField] private float interactDistance = 3f;

    [SerializeField] private GameObject interactButton;
    [SerializeField] private GameObject useKeyButton;
    [SerializeField] private TextMeshProUGUI interactionText; // Text to show when room is completed
    [SerializeField] private GameObject gameTimer; // Reference to the game timer
    [SerializeField] private Image crosshairImage;  // Reference the crosshair Image component

    private DoorInteraction doorInteraction;   // Track which door we're looking at
    private DrawerMech drawerMech;      // Track which drawer we're looking at

    
    
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
                interactButtonComponent.onClick.AddListener(OnInteractButtonClicked);
            }
            else
            {
                Debug.LogError("Interact button doesn't have a Button component!");
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
        DoorInteraction previousDoor = doorInteraction;
        DrawerMech previousDrawer = drawerMech;

        // Reset current door reference
        doorInteraction = null;
        drawerMech = null;
        

        // Cast ray from camera center (where crosshair is)
        Ray ray = new Ray(playerMainCamera.transform.position, playerMainCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance))
        {
            GameObject hitObject = hit.collider.gameObject;

            // Check if we hit a door
            if (hitObject.CompareTag("Door"))
            {
                // Get the door interaction component
                DoorInteraction rayDoorInteraction = hitObject.GetComponent<DoorInteraction>();
                if (rayDoorInteraction != null && rayDoorInteraction.enabled)
                {
                    // Check if we're within interaction distance
                    float distanceToObject = Vector3.Distance(transform.position, hitObject.transform.position);
                    if (distanceToObject <= interactDistance)
                    {
                        // We're looking at a door and within range
                        this.doorInteraction = rayDoorInteraction;
                        interactButton.SetActive(true);
                    }
                    else
                    {
                        interactButton.SetActive(false);
                        if(useKeyButton.activeInHierarchy)
                        useKeyButton.SetActive(false);
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
                    // Check if we're within interaction distance
                    float distanceToObject = Vector3.Distance(transform.position, hitObject.transform.position);
                    if (distanceToObject <= interactDistance)
                    {
                        // We're looking at a drawer and within range
                        drawerMech = drawerInteraction;
                        interactButton.SetActive(true);
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
                            if (crosshairImage != null)
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

}
