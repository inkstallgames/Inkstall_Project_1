using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    
    [Header("Audio")]
    [SerializeField] private AudioClip lockedDoorSound;
    private AudioSource audioSource;
    
    // Cache raycast data to prevent GC allocations
    private Ray interactionRay;
    private RaycastHit hitInfo;
    
    // Cache component references
    private CollectibleProp cachedCollectible;
    private DoorInteraction cachedDoor;
    private SlidingDoor cachedSlidingDoor;
    private DrawerMech cachedDrawer;
    private DisableOnPropSpawn cachedDisabler;

    void Start()
    {
        // Add audio source component if it doesn't exist
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Initialize interactionRay
        interactionRay = new Ray();
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
        // Reuse Ray object instead of creating a new one
        interactionRay.origin = playerCamera.transform.position;
        interactionRay.direction = playerCamera.transform.forward;
        
        if (Physics.Raycast(interactionRay, out hitInfo, interactDistance))
        {
            GameObject hitObject = hitInfo.collider.gameObject;
            
            // Check if it's a collectible
            cachedCollectible = hitObject.GetComponent<CollectibleProp>();
            if (cachedCollectible != null)
            {
                cachedCollectible.Interact();
                return;
            }

            // Check if it's a rotating door
            cachedDoor = hitObject.GetComponent<DoorInteraction>();
            if (cachedDoor != null)
            {
                // Check if the door interaction is disabled (locked)
                if (!cachedDoor.enabled)
                {
                    // Try to find DisableOnPropSpawn component to play locked sound
                    cachedDisabler = hitObject.GetComponent<DisableOnPropSpawn>();
                    if (cachedDisabler != null)
                    {
                        cachedDisabler.PlayLockedSound();
                    }
                    else
                    {
                        // Play locked door sound
                        PlayLockedDoorSound();
                    }
                    Debug.Log("This door is locked!");
                    return;
                }
                
                cachedDoor.Interact();
                return;
            }

            // Check if it's a sliding door
            cachedSlidingDoor = hitObject.GetComponent<SlidingDoor>();
            if (cachedSlidingDoor != null)
            {
                // Check if the sliding door is disabled (locked)
                if (!cachedSlidingDoor.enabled)
                {
                    // Try to find DisableOnPropSpawn component to play locked sound
                    cachedDisabler = hitObject.GetComponent<DisableOnPropSpawn>();
                    if (cachedDisabler != null)
                    {
                        cachedDisabler.PlayLockedSound();
                    }
                    else
                    {
                        // Play locked door sound
                        PlayLockedDoorSound();
                    }
                    Debug.Log("This door is locked!");
                    return;
                }
                
                cachedSlidingDoor.Interact();
                return;
            }

            // Check if it's a drawer
            cachedDrawer = hitObject.GetComponent<DrawerMech>();
            if (cachedDrawer != null)
            {
                // Check if the drawer interaction is disabled (locked)
                if (!cachedDrawer.enabled)
                {
                    // Try to find DisableOnPropSpawn component to play locked sound
                    cachedDisabler = hitObject.GetComponent<DisableOnPropSpawn>();
                    if (cachedDisabler != null)
                    {
                        cachedDisabler.PlayLockedSound();
                    }
                    else
                    {
                        // Play locked door sound
                        PlayLockedDoorSound();
                    }
                    Debug.Log("This drawer is locked!");
                    return;
                }
                
                cachedDrawer.Interact();
                return;
            }

            // Optional: Debug info
            Debug.Log($"🟤 No interactable found on: {hitObject.name}");
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
