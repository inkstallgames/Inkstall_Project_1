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
    
    private GameObject lastHitObject = null;

    private void Start()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            if (playerCamera == null)
            {
                Debug.LogError("No camera found! Assign one to PlayerInteraction.");
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
            
            // Check if we're looking at an interactable object
            bool isInteractable = false;
            string promptText = "Press E to open";
            
            // Check for door
            if (hitObject.TryGetComponent<DoorInteraction>(out var door))
            {
                // Only show prompt if DoorInteraction is enabled
                if (door.enabled)
                {
                    // Check if we're close enough to the door
                    float distanceToObject = Vector3.Distance(transform.position, hitObject.transform.position);
                    if (distanceToObject <= interactDistance)
                    {
                        isInteractable = true;
                    }
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
                    promptText = "Press E to interact";
                }
            }
            
            // Show or hide prompt based on what we're looking at
            if (isInteractable)
            {
                interactionPromptText.text = promptText;
                interactionPromptText.gameObject.SetActive(true);
                lastHitObject = hitObject;
            }
            else if (lastHitObject != hitObject)
            {
                interactionPromptText.gameObject.SetActive(false);
                lastHitObject = null;
            }
        }
        else if (interactionPromptText.gameObject.activeSelf)
        {
            interactionPromptText.gameObject.SetActive(false);
            lastHitObject = null;
        }
    }

    private void TryInteract()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            GameObject hitObject = hit.collider.gameObject;

            if (hitObject.TryGetComponent<CollectibleProp>(out var collectible))
            {
                collectible.Interact();
                return;
            }

            if (hitObject.TryGetComponent<DoorInteraction>(out var door))
            {
                if (door.enabled)
                {
                    door.Interact();
                }
                else if (hitObject.TryGetComponent<DoorStateHandler>(out var doorState))
                {
                    doorState.PlayLockedSound();
                }
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
