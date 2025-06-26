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
    
    // Cache raycast hit to avoid GC allocations
    private RaycastHit hitInfo;
    // Cache ray to avoid GC allocations in Update
    private Ray interactionRay;
    
    // Cache components to avoid repeated GetComponent calls
    private DisableOnPropSpawn cachedDisabler;

    void Start()
    {
        // Get or add audio source component
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Default to main camera if none assigned
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            if (playerCamera == null)
            {
                Debug.LogError("No camera assigned to PlayerInteraction and no Camera.main found!");
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
        
        // Use cached ray instead of creating a new one
        interactionRay.origin = playerCamera.transform.position;
        interactionRay.direction = playerCamera.transform.forward;
        
        if (Physics.Raycast(interactionRay, out hitInfo, interactDistance))
        {
            GameObject hitObject = hitInfo.collider.gameObject;
            
            // Check if it's a collectible
            CollectibleProp collectible = hitInfo.collider.GetComponent<CollectibleProp>();
            if (collectible != null)
            {
                // Play pickup sound if available
                if (pickupSound != null && audioSource != null)
                {
                    audioSource.PlayOneShot(pickupSound);
                }
                
                collectible.Interact();
                return;
            }

            // Check if it's a rotating door
            DoorInteraction door = hitInfo.collider.GetComponent<DoorInteraction>();
            if (door != null)
            {
                // Check if the door interaction is disabled (locked)
                if (!door.enabled)
                {
                    HandleLockedInteraction(hitInfo.collider);
                    Debug.Log("This door is locked!");
                    return;
                }               
                door.Interact();
                return;
            }                
            // Optional: Debug info
            Debug.Log($"🟤 No interactable found on: {hitObject.name}");
        }
    }
    
    // Handle locked interaction with proper sound
    private void HandleLockedInteraction(Collider collider)
    {
        // Reuse cached disabler if possible
        cachedDisabler = collider.GetComponent<DisableOnPropSpawn>();
        if (cachedDisabler != null)
        {
            cachedDisabler.PlayLockedSound();
        }
        else
        {
            PlayLockedDoorSound();
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
