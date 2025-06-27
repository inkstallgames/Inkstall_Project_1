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
    private Ray interactionRay = new Ray(); // Initialize to prevent null reference
    
    // Cache components to avoid repeated GetComponent calls
    private DisableOnPropSpawn cachedDisabler;
    private QuizTrigger cachedQuizTrigger;

    // Object pool for component lookups to avoid GC allocations
    private Component[] componentCache = new Component[4];

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
        
        // Use cached ray instead of creating a new one
        interactionRay.origin = playerCamera.transform.position;
        interactionRay.direction = playerCamera.transform.forward;
        
        if (Physics.Raycast(interactionRay, out hitInfo, interactDistance))
        {
            GameObject hitObject = hitInfo.collider.gameObject;
            
            // First check if it's a collectible
            CollectibleProp collectible = hitObject.GetComponent<CollectibleProp>();
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

            // Check if it's a door
            DoorInteraction door = hitObject.GetComponent<DoorInteraction>();
            if (door != null)
            {
                // Check if the door interaction is disabled (locked)
                if (!door.enabled)
                {
                    // Get the QuizTrigger component - important to get it here instead of caching earlier
                    cachedQuizTrigger = hitObject.GetComponent<QuizTrigger>();
                    
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"Door is locked. QuizTrigger present: {(cachedQuizTrigger != null)}");
#endif
                    
                    // If there's a QuizTrigger, use it
                    if (cachedQuizTrigger != null)
                    {
                        cachedQuizTrigger.TriggerQuiz();
                    }
                    else
                    {
                        // Fall back to the old behavior
                        HandleLockedInteraction(hitInfo.collider);
                    }
                    return;
                }               
                door.Interact();
                return;
            }
            
            // Check if it's a drawer or other interactable
            DrawerMech drawer = hitObject.GetComponent<DrawerMech>();
            if (drawer != null)
            {
                // Check if the drawer interaction is disabled (locked)
                if (!drawer.enabled)
                {
                    // Get the QuizTrigger component - important to get it here instead of caching earlier
                    cachedQuizTrigger = hitObject.GetComponent<QuizTrigger>();
                    
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"Drawer is locked. QuizTrigger present: {(cachedQuizTrigger != null)}");
#endif
                    
                    // If there's a QuizTrigger, use it
                    if (cachedQuizTrigger != null)
                    {
                        cachedQuizTrigger.TriggerQuiz();
                    }
                    else
                    {
                        // Fall back to the old behavior
                        HandleLockedInteraction(hitInfo.collider);
                    }
                    return;
                }
                drawer.Interact();
                return;
            }
        }
    }
    
    void HandleLockedInteraction(Collider collider)
    {
        // Check if there's a DisableOnPropSpawn component
        cachedDisabler = collider.GetComponent<DisableOnPropSpawn>();
        if (cachedDisabler != null)
        {
            cachedDisabler.PlayLockedSound();
        }
        else if (audioSource != null && lockedDoorSound != null)
        {
            // Fall back to playing the locked sound directly
            audioSource.PlayOneShot(lockedDoorSound);
        }
    }
}
