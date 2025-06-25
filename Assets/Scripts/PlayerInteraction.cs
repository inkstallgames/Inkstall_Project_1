using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    
    [Header("Audio")]
    [SerializeField] private AudioClip lockedDoorSound;
    private AudioSource audioSource;

    void Start()
    {
        // Add audio source component if it doesn't exist
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
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
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            // Check if it's a collectible
            CollectibleProp collectible = hit.collider.GetComponent<CollectibleProp>();
            if (collectible != null)
            {
                collectible.Interact();
                return;
            }

            // Check if it's a rotating door
            DoorInteraction door = hit.collider.GetComponent<DoorInteraction>();
            if (door != null)
            {
                // Check if the door interaction is disabled (locked)
                if (!door.enabled)
                {
                    // Try to find DisableOnPropSpawn component to play locked sound
                    DisableOnPropSpawn disabler = hit.collider.GetComponent<DisableOnPropSpawn>();
                    if (disabler != null)
                    {
                        disabler.PlayLockedSound();
                    }
                    else
                    {
                        // Play locked door sound
                        PlayLockedDoorSound();
                    }
                    Debug.Log("This door is locked!");
                    return;
                }
                
                door.Interact();
                return;
            }

            // Check if it's a sliding door
            SlidingDoor sliding = hit.collider.GetComponent<SlidingDoor>();
            if (sliding != null)
            {
                // Check if the sliding door is disabled (locked)
                if (!sliding.enabled)
                {
                    // Try to find DisableOnPropSpawn component to play locked sound
                    DisableOnPropSpawn disabler = hit.collider.GetComponent<DisableOnPropSpawn>();
                    if (disabler != null)
                    {
                        disabler.PlayLockedSound();
                    }
                    else
                    {
                        // Play locked door sound
                        PlayLockedDoorSound();
                    }
                    Debug.Log("This door is locked!");
                    return;
                }
                
                sliding.Interact();
                return;
            }

            // Check if it's a drawer
            DrawerMech drawer = hit.collider.GetComponent<DrawerMech>();
            if (drawer != null)
            {
                // Check if the drawer interaction is disabled (locked)
                if (!drawer.enabled)
                {
                    // Try to find DisableOnPropSpawn component to play locked sound
                    DisableOnPropSpawn disabler = hit.collider.GetComponent<DisableOnPropSpawn>();
                    if (disabler != null)
                    {
                        disabler.PlayLockedSound();
                    }
                    else
                    {
                        // Play locked door sound
                        PlayLockedDoorSound();
                    }
                    Debug.Log("This drawer is locked!");
                    return;
                }
                
                drawer.Interact();
                return;
            }

            // Optional: Debug info
            Debug.Log($"🟤 No interactable found on: {hit.collider.gameObject.name}");
        }
    }
    
    private void PlayLockedDoorSound()
    {
        if (lockedDoorSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(lockedDoorSound);
        }
    }
}
