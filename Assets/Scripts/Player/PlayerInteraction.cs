using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Highlight Settings")]
    [SerializeField] private float highlightCheckFrequency = 0.1f; // How often to check for highlightable objects
    [SerializeField] private LayerMask highlightableLayers = -1; // Default to everything

    private GameObject currentHighlightedObject;
    private float highlightTimer;

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

            // Check if object has any highlightable component
            if (hitObject.GetComponent<IHighlightable>() != null ||
                hitObject.GetComponent<CollectibleProp>() != null ||
                hitObject.GetComponent<PropIdentity>() != null ||
                hitObject.GetComponent<DoorInteraction>() != null ||
                hitObject.GetComponent<DrawerMech>() != null)
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

        // Try to use IHighlightable interface if implemented
        IHighlightable highlightable = obj.GetComponent<IHighlightable>();
        if (highlightable != null)
        {
            highlightable.Highlight();
            return;
        }

        // Otherwise use the PropHighlighter component if it exists
        PropHighlighter highlighter = obj.GetComponent<PropHighlighter>();
        if (highlighter == null)
        {
            // Add highlighter component if it doesn't exist
            highlighter = obj.AddComponent<PropHighlighter>();
        }
        highlighter.Highlight();
    }

    private void UnhighlightCurrentObject()
    {
        if (currentHighlightedObject == null) return;

        // Try to use IHighlightable interface if implemented
        IHighlightable highlightable = currentHighlightedObject.GetComponent<IHighlightable>();
        if (highlightable != null)
        {
            highlightable.Unhighlight();
        }
        else
        {
            // Otherwise use the PropHighlighter component if it exists
            PropHighlighter highlighter = currentHighlightedObject.GetComponent<PropHighlighter>();
            if (highlighter != null)
            {
                highlighter.Unhighlight();
            }
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