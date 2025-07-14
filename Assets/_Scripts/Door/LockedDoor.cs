using UnityEngine;
using System.Collections;
using TMPro;    

public class LockedDoor : MonoBehaviour
{
    [Header("Door Settings")]
    [SerializeField] private bool isLocked = true;

    [Header("References")]
    [SerializeField] private DoorInteraction doorInteraction;
    [SerializeField] private BoxCollider triggerZone;
    [SerializeField] private Animator doorAnimator; // Reference to the Animator component
    [SerializeField] private TextMeshProUGUI doorPromptText;

    // Keep track of the door the player is currently near
    private LockedDoor currentNearbyDoor;
    private Coroutine promptCoroutine;
    [SerializeField] private float promptDuration = 2.0f;


    [Header("Audio Settings")]
    [SerializeField] private AudioClip unlockSound;
    [SerializeField] private AudioClip lockedSound;
    [Range(0f, 1f)][SerializeField] private float soundVolume = 0.7f;

    private AudioSource audioSource;
    private int currentkeys;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
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

        // Hide door prompt initially
        if (doorPromptText != null)
        {
            doorPromptText.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
       // Check for key press when near a locked door
        if (currentNearbyDoor != null && Input.GetKeyDown(KeyCode.F))
        {
            UnlockDoor(currentNearbyDoor);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && isLocked)
        {
            PlayerNearDoor(this);
        }
    }


    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && isLocked)
        {
            PlayerLeftDoor(this);
        }
    }

    // Called when player enters the trigger zone of a locked door
    private void PlayerNearDoor(LockedDoor door)
    {
        currentNearbyDoor = door;
        ShowDoorPrompt();
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

            // Disable Animator
            if (doorAnimator != null)
            {
                doorAnimator.enabled = false;
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

    // Try to unlock the door
    private void UnlockDoor(LockedDoor door)
    {
        if (KeyManager.Instance.UseKey())
        {
            // Unlock the door
            door.Unlock();

            // Show success message
            ShowTemporaryMessage("Door unlocked!");

            // Clear the current nearby door reference
            currentNearbyDoor = null;
        }
        else
        {
            // Show no keys message
            ShowTemporaryMessage("No keys available!");
        }
    }

    // Show a temporary message
    public void ShowTemporaryMessage(string message)
    {
        if (doorPromptText != null)
        {
            // Cancel any existing coroutine
            if (promptCoroutine != null)
            {
                StopCoroutine(promptCoroutine);
            }

            doorPromptText.text = message;
            doorPromptText.gameObject.SetActive(true);

            // Start coroutine to hide the message after a delay
            promptCoroutine = StartCoroutine(HidePromptAfterDelay());
        }
    }


    // Called when player exits the trigger zone of a locked door
    public void PlayerLeftDoor(LockedDoor door)
    {
        if (currentNearbyDoor == door)
        {
            currentNearbyDoor = null;
            HideDoorPrompt();
        }
    }

    // Show the door prompt UI
    private void ShowDoorPrompt()
    {
        if (doorPromptText != null)
        {
            // Cancel any existing coroutine
            if (promptCoroutine != null)
            {
                StopCoroutine(promptCoroutine);
            }

            // Show appropriate message based on whether we have keys
            if (KeyManager.Instance.GetKeyCount() > 0)
            {
                doorPromptText.text = "Door is locked. Press F to use a key.";
            }
            else
            {
                doorPromptText.text = "Not enough keys.";
            }

            doorPromptText.gameObject.SetActive(true);
        }
    }

    // Hide the door prompt UI
    private void HideDoorPrompt()
    {
        if (doorPromptText != null)
        {
            // Cancel any existing coroutine
            if (promptCoroutine != null)
            {
                StopCoroutine(promptCoroutine);
            }

            doorPromptText.gameObject.SetActive(false);
        }
    }

    // Coroutine to hide the prompt after a delay
    private IEnumerator HidePromptAfterDelay()
    {
        yield return new WaitForSeconds(promptDuration);
        doorPromptText.gameObject.SetActive(false);
    }
}
