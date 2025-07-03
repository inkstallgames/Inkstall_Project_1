using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Highlight Settings")]
    [SerializeField] private LayerMask highlightableLayers = -1; // Default to everything
    [SerializeField] private Color highlightColor = Color.white;
    [SerializeField] private float highlightCheckFrequency = 0.1f;

    private GameObject currentHighlightedObject;
    private float highlightTimer;

    private void Start()
    {
        // Validate camera reference
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            Debug.Log("PlayerCamera not assigned, using Camera.main");

            if (playerCamera == null)
            {
                Debug.LogError("No camera found! Please assign a camera to PlayerInteraction.");
                enabled = false;
                return;
            }
        }
    }

    private void Update()
    {
        // Handle interaction input
        if (Input.GetKeyDown(interactKey))
        {
            TryInteract();
        }

        // Handle highlight checking with throttling for performance
        highlightTimer += Time.deltaTime;
        if (highlightTimer >= highlightCheckFrequency)
        {
            highlightTimer = 0f;
            CheckForHighlight();
        }
    }

    private void CheckForHighlight()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit hit;

        // If we hit something that can be highlighted
        if (Physics.Raycast(ray, out hit, interactDistance, highlightableLayers))
        {
            GameObject hitObject = hit.collider.gameObject;

            // Check if it's a prop we want to highlight
            bool isHighlightable = hitObject.CompareTag("Prop") ||
                                  hitObject.GetComponent<CollectibleProp>() != null ||
                                  hitObject.GetComponent<PropIdentity>() != null;

            if (isHighlightable)
            {
                // If it's not the same object we're already highlighting
                if (currentHighlightedObject != hitObject)
                {
                    // Unhighlight previous object
                    UnhighlightCurrentObject();

                    // Highlight new object
                    currentHighlightedObject = hitObject;
                    HighlightObject(currentHighlightedObject);
                }
            }
            else
            {
                // Not a highlightable object, remove any current highlight
                UnhighlightCurrentObject();
            }
        }
        else
        {
            // Nothing hit, remove any current highlight
            UnhighlightCurrentObject();
        }
    }

    private void HighlightObject(GameObject obj)
    {
        if (obj == null) return;

        // Add the SelectionOutline component if it doesn't exist
        SelectionOutline outline = obj.GetComponent<SelectionOutline>();
        if (outline == null)
        {
            outline = obj.AddComponent<SelectionOutline>();
        }

        // Set the highlight color and enable it
        outline.OutlineColor = highlightColor;
        outline.EnableOutline(true);
    }

    private void UnhighlightCurrentObject()
    {
        if (currentHighlightedObject == null) return;

        // Disable the outline if it exists
        SelectionOutline outline = currentHighlightedObject.GetComponent<SelectionOutline>();
        if (outline != null)
        {
            outline.EnableOutline(false);
        }

        currentHighlightedObject = null;
    }

    private void TryInteract()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactDistance))
        {
            GameObject hitObject = hit.collider.gameObject;

            // Try to interact with collectible props
            if (hitObject.TryGetComponent<CollectibleProp>(out var collectible))
            {
                collectible.Interact();
                return;
            }

            // Try to interact with doors
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

            // Try to interact with drawers
            if (hitObject.TryGetComponent<DrawerMech>(out var drawer))
            {
                drawer.Interact();
                return;
            }
        }
    }
}