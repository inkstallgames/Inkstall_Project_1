using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Audio")]
    [SerializeField] private AudioClip lockedDoorSound;
    [SerializeField] private AudioClip pickupSound;
    private AudioSource audioSource;

    private RaycastHit hitInfo;
    private Ray interactionRay = new Ray();
    private Component[] componentCache = new Component[4];

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            if (playerCamera == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("No camera assigned to PlayerInteraction and no Camera.main found!");
#endif
            }
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(interactKey))
        {
            TryInteract();
        }
    }

    void TryInteract()
    {
        if (playerCamera == null) return;

        interactionRay.origin = playerCamera.transform.position;
        interactionRay.direction = playerCamera.transform.forward;

        if (Physics.Raycast(interactionRay, out hitInfo, interactDistance))
        {
            GameObject hitObject = hitInfo.collider.gameObject;

            // 1. Check if it's a collectible
            CollectibleProp collectible = hitObject.GetComponent<CollectibleProp>();
            if (collectible != null)
            {
                bool collected = collectible.Interact();
                if (collected && pickupSound != null && audioSource != null)
                {
                    audioSource.PlayOneShot(pickupSound);
                }
                return;
            }

            // 2. Check if it's a door
            DoorInteraction door = hitObject.GetComponent<DoorInteraction>();
            if (door != null)
            {
                if (!door.enabled)
                {
                    // Optionally play lockedDoorSound here
                }

                door.Interact();
                return;
            }

            // 3. Check drawers
            DrawerMech drawer = hitObject.GetComponent<DrawerMech>();
            if (drawer != null)
            {
                drawer.Interact();
                return;
            }
        }
    }
}
