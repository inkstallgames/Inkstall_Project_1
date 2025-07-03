using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    private void Update()
    {
        if (Input.GetKeyDown(interactKey))
        {
            TryInteract();
        }
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