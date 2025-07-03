using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

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
    }

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
