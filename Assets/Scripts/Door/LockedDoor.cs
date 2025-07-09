using UnityEngine;

public class LockedDoor : MonoBehaviour
{
    [Header("Door Settings")]
    [SerializeField] private bool isLocked = true;
    
    [Header("References")]
    [SerializeField] private DoorInteraction doorInteraction;
    [SerializeField] private BoxCollider triggerZone;
    [SerializeField] private Animator doorAnimator; // Reference to the Animator component
    
    [Header("Audio Settings")]
    [SerializeField] private AudioClip unlockSound;
    [SerializeField] private AudioClip lockedSound;
    [Range(0f, 1f)] [SerializeField] private float soundVolume = 0.7f;
    
    private AudioSource audioSource;
    
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D sound
            audioSource.volume = soundVolume;
        }
        
        if (doorAnimator == null)
        {
            doorAnimator = GetComponent<Animator>();
            if (doorAnimator == null)
            {
                Debug.LogWarning("No Animator component found on " + gameObject.name);
            }
        }
        
        
        // Make sure the trigger zone has the right settings
        if (triggerZone != null)
        {
            triggerZone.isTrigger = true;
        }
        else
        {
            Debug.LogWarning("No trigger zone assigned to LockedDoor. Player won't be able to interact with it.");
        }
        
        // Disable the door interaction script if the door is locked
        if (doorInteraction != null && isLocked)
        {
            doorInteraction.enabled = false;
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && isLocked)
        {
            // Notify the KeySystem that the player is near this door
            if (KeySystem.Instance != null)
            {
                KeySystem.Instance.PlayerNearDoor(this);
            }
        }
    }

    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && isLocked)
        {
            // Notify the KeySystem that the player left this door
            if (KeySystem.Instance != null)
            {
                KeySystem.Instance.PlayerLeftDoor(this);
            }
        }
    }
    
    // Called by the KeySystem when the player uses a key to unlock this door
    public void Unlock()
    {
        if (isLocked)
        {
            isLocked = false;
            
            // Play unlock sound
            if (unlockSound != null)
            {
                audioSource.clip = unlockSound;
                audioSource.Play();
            }
            
            // Enable the door interaction script
            if (doorInteraction != null)
            {
                doorInteraction.enabled = true;
            }
            else
            {
                Debug.LogError("No DoorInteraction component assigned to this LockedDoor!");
            }
        }
    }
    
    // Check if the door is currently locked
    public bool IsLocked()
    {
        return isLocked;
    }
    
    // Play the locked door sound
    public void PlayLockedSound()
    {
        if (isLocked && lockedSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(lockedSound);
        }
    }

    // Called when the player tries to interact with a locked door
    public void OnInteractAttempt()
    {
        if (isLocked)
        {
            PlayLockedSound();
            
            // Trigger the locked door animation
            if (doorAnimator != null)
            {
                doorAnimator.SetBool("DoorInteractionEnable", true);
                // Optional: Reset the parameter after a short delay if needed
                // This ensures the animation can be triggered again
                Invoke("DisableDoorInteraction", 1f);
            }
        }
    }
    
    // Helper method to reset the animation trigger
    private void DisableDoorInteraction()
    {
        if (doorAnimator != null)
        {
            doorAnimator.SetBool("DoorInteractionEnable", false);
        }
    }
}
