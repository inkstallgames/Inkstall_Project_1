using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private Camera playerMainCamera;
    [SerializeField] private GameObject openCloseButton;

    private DoorInteraction currentDoor;   // Track which door we're looking at

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

    }

    private void Update()
    {
        CheckRaycastInteraction();
    }

    private void CheckRaycastInteraction()
    {
        Ray ray = new Ray(playerMainCamera.transform.position, playerMainCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            GameObject hitObject = hit.collider.gameObject;
            currentDoor = null;

            //Check for Locked door
            if (hitObject.TryGetComponent<DoorInteraction>(out var lockedDoor) && lockedDoor.IsLocked())
                {
                    float distanceToObject = Vector3.Distance(transform.position, hitObject.transform.position);
                    if (distanceToObject <= interactDistance)
                    {
                        openCloseButton.SetActive(true);
                        currentDoor = lockedDoor;
                    }
                }

            else    
            {
                openCloseButton.SetActive(false);
            }    

            // Handle regular doors
            if (hitObject.TryGetComponent<DoorInteraction>(out var unlockedDoor) && !unlockedDoor.IsLocked())
                {
                    float distanceToObject = Vector3.Distance(transform.position, hitObject.transform.position);
                    if (distanceToObject <= interactDistance)
                    {
                        currentDoor = unlockedDoor;
                        openCloseButton.SetActive(true);
                    }
                }
            else    
            {
                openCloseButton.SetActive(false);
            }    
            
            // Check for drawer
            if (hitObject.TryGetComponent<DrawerMech>(out var drawer))
            {
                // Only show prompt if DrawerMech is enabled
                if (drawer.enabled)
                {
                    // Check if we're close enough to the drawer
                        float distanceToObject = Vector3.Distance(transform.position, hitObject.transform.position);
                        if (distanceToObject <= interactDistance)
                        {
                            openCloseButton.SetActive(true);
                        }
                }
            }
            else    
            {
                openCloseButton.SetActive(false);
            }    

            
            // Check for collectible prop
            if (hitObject.TryGetComponent<CollectibleProp>(out var collectible))
                {
                    // Check if we're close enough to the prop
                    float distanceToObject = Vector3.Distance(transform.position, hitObject.transform.position);
                    if (distanceToObject <= interactDistance)
                    {
                        openCloseButton.SetActive(true);
                    }
                }       
            
            
        }
        else
        {
            openCloseButton.SetActive(false);
        }
    }
}
