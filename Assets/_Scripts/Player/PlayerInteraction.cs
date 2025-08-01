using UnityEngine;
using UnityEngine.UI;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float rayDistance = 10f;
    [SerializeField] private Camera playerMainCamera;
    [SerializeField] private GameObject openCloseButton;
    [SerializeField] private GameObject useKeyButton; // Reference to the use key button

    [SerializeField] private float interactDistance = 3f;

    private DoorInteraction currentDoor;   // Track which door we're looking at
    private DrawerMech currentDrawer;   // Track which drawer we're looking at
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
        if (openCloseButton != null)
        {
            openCloseButton.SetActive(false);
            
            // Add click listener to the open/close button
            Button openCloseButtonComponent = openCloseButton.GetComponent<Button>();
            if (openCloseButtonComponent != null)
            {
                openCloseButtonComponent.onClick.AddListener(InteractWithCurrentDoor);
            }
            else
            {
                Debug.LogError("Open/Close button doesn't have a Button component!");
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
        
        // Disable buttons by default
        if (openCloseButton != null)
        {
            openCloseButton.SetActive(false);
        }
        
        if (useKeyButton != null)
        {
            useKeyButton.SetActive(false);
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
                        openCloseButton.SetActive(true);
                        
                        // Only show use key button if:
                        // 1. The door is locked
                        // 2. We've previously tried to open this specific door (showUseKeyButton is true)
                        // 3. We're still looking at the same door as before
                        if (doorInteraction.IsLocked() && showUseKeyButton && doorInteraction == previousDoor)
                        {
                            useKeyButton.SetActive(true);
                        }
                    }
                }
            }
            
            // Check for drawer (keeping existing functionality)
            if (hitObject.CompareTag("Drawer"))
            {
                DrawerMech drawerInteraction = hitObject.GetComponent<DrawerMech>();
                
                // Check if we're within interaction distance
                float distanceToObject = Vector3.Distance(transform.position, hitObject.transform.position);
                if (distanceToObject <= interactDistance)
                {
                    currentDrawer = drawerInteraction;
                    openCloseButton.SetActive(true);
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
    
    // Method to interact with the current door
    private void InteractWithCurrentDoor()
    {
        if (currentDoor != null)
        {
            // Try to open the door
            currentDoor.TryOpenDoor();
            
            // If the door is locked, enable the use key button
            if (currentDoor.IsLocked())
            {
                showUseKeyButton = true;
                
                // Immediately show the use key button if we're still looking at the door
                if (useKeyButton != null)
                {
                    useKeyButton.SetActive(true);
                }
            }
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
}
