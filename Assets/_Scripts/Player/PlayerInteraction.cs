using UnityEngine;
using TMPro;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI interactionPromptText;

    private void Start()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            if (playerCamera == null)
            {
                enabled = false;
                return;
            }
        }

        // Hide the prompt initially
        if (interactionPromptText != null)
        {
            interactionPromptText.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        // Check for interaction
        if (Input.GetKeyDown(interactKey))
        {
            TryInteract();
        }

        // Update interaction prompt
        UpdateInteractionPrompt();
    }

    private void UpdateInteractionPrompt()
    {
        if (interactionPromptText == null) return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            GameObject hitObject = hit.collider.gameObject;
            bool isInteractable = false;

            // Check for door
            if (hitObject.TryGetComponent<LockedDoor>(out var lockedDoor))
            {
                if (!lockedDoor.IsLocked() && hitObject.TryGetComponent<DoorInteraction>(out var door) && door.enabled)
                {
                    float distanceToObject = Vector3.Distance(transform.position, hitObject.transform.position);
                    if (distanceToObject <= interactDistance)
                    {
                        isInteractable = true;
                    }
                }
            }
            // Handle regular doors
            else if (hitObject.TryGetComponent<DoorInteraction>(out var door) && door.enabled)
            {
                float distanceToObject = Vector3.Distance(transform.position, hitObject.transform.position);
                if (distanceToObject <= interactDistance)
                {
                    isInteractable = true;
                }
            }
            // Check for drawer
            else if (hitObject.TryGetComponent<DrawerMech>(out var drawer))
            {
                // Only show prompt if DrawerMech is enabled
                if (drawer.enabled)
                {
                    // Check if we're close enough to the drawer
                    float distanceToObject = Vector3.Distance(transform.position, hitObject.transform.position);
                    if (distanceToObject <= interactDistance)
                    {
                        isInteractable = true;
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
                    isInteractable = true;
                }
            }

            // Update UI
            interactionPromptText.gameObject.SetActive(isInteractable);
        }
        else
        {
            interactionPromptText.gameObject.SetActive(false);
        }
    }

    private void TryInteract()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            GameObject hitObject = hit.collider.gameObject;

            // First check for LockedDoor
            if (hitObject.TryGetComponent<LockedDoor>(out var lockedDoor))
            {
                if (lockedDoor.IsLocked())
                {
                    lockedDoor.OnInteractAttempt();
                }
                // If door is unlocked, let the DoorInteraction handle it
                else if (hitObject.TryGetComponent<DoorInteraction>(out var door) && door.enabled)
                {
                    door.Interact();
                }
                return;
            }

            // Handle regular doors (without LockedDoor component)
            if (hitObject.TryGetComponent<DoorInteraction>(out var regularDoor) && regularDoor.enabled)
            {
                regularDoor.Interact();
                return;
            }

            if (hitObject.TryGetComponent<CollectibleProp>(out var collectible))
            {
                collectible.Interact();
                return;
            }

            if (hitObject.TryGetComponent<DrawerMech>(out var drawer))
            {
                drawer.Interact();
                return;
            }
        }
    }
}
