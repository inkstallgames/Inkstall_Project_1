using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private Camera playerMainCamera;
    [SerializeField] private Button openCloseButton;

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

        // Hide the button by default
        if (openCloseButton != null)
        {
            openCloseButton.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        CheckRaycastInteraction();
    }

    private void CheckRaycastInteraction()
    {
        bool showOpenCloseButton = false;

        if (showOpenCloseButton)
        {
            openCloseButton.gameObject.SetActive(true);
        }
        else
        {
            openCloseButton.gameObject.SetActive(false);
        }   

        Ray ray = new Ray(playerMainCamera.transform.position, playerMainCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            GameObject hitObject = hit.collider.gameObject;
            currentDoor = null;

            //Check for door
            if (hitObject.tag == "Door")
            {
                // Check for Locked door
                if (hitObject.TryGetComponent<DoorInteraction>(out var door) && door.enabled && door.IsLocked())
                {
                    float distanceToObject = Vector3.Distance(transform.position, hitObject.transform.position);
                    if (distanceToObject <= interactDistance)
                    {
                        currentDoor = door;
                        showOpenCloseButton = true;
                    }
                }

                // Handle regular doors
                else if (hitObject.TryGetComponent<DoorInteraction>(out var door) && door.enabled && !door.IsLocked())
                {
                    float distanceToObject = Vector3.Distance(transform.position, hitObject.transform.position);
                    if (distanceToObject <= interactDistance)
                    {
                        currentDoor = door;
                        showOpenCloseButton = true;
                    }
                }
            }
            // Check for drawer
            else if (hitObject.tag == "Drawer")
            {
                
            }
            else if (hitObject.TryGetComponent<DrawerMech>(out var drawer))
            {
                // Only show prompt if DrawerMech is enabled
                if (drawer.enabled)
                {
                    // Check if we're close enough to the drawer
                    float distanceToObject = Vector3.Distance(transform.position, hitObject.transform.position);
                    if (distanceToObject <= interactDistance)
                    {
                        showOpenCloseButton = true;
                    }
                }
            }
            // Check for collectible prop
            else if (hitObject.TryGetComponent<CollectibleProp>(out var collectible))
            {
                // Check if we're close enough to the prop
                float distanceToObject = Vector3.Distance(transform.position, hitObject.transform.position);
                if (distanceToObject <= interactDistance)
                {
                    showOpenCloseButton = true;
                }
            }
        }
    }


}
