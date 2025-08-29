using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.UI;

[RequireComponent(typeof(AudioSource))]
public class DoorInteraction : MonoBehaviour
{
    [Header("Door Settings")]
    [SerializeField] private float XAngle = 0f;
    [SerializeField] private float YAngle = -90f;
    [SerializeField] private float ZAngle = 0f;
    [SerializeField] private float doorSpeed = 2f;
    [SerializeField] private AudioClip doorOpenSound;
    [SerializeField] private AudioClip doorCloseSound;

    [Header("Lock Settings")]
    [SerializeField] private bool isLocked = false;
    [SerializeField] private AudioClip doorLockedSound;
    [SerializeField] private AudioClip doorUnlockSound;
    [SerializeField] public bool isUnlockable = false;
    [SerializeField] public bool isRoomCompleted = false;
    [SerializeField] public int doorID; // Unique identifier for this door (now an int to match ProgressManager)

    [Header("Timer Settings")]
    [SerializeField] private bool shouldStartTimer = false;

    [Header("Game Activation Settings")]
    [SerializeField] private GameObject chemicalBomb;
    [SerializeField] private Button throwButton;
    [SerializeField] private TextMeshProUGUI gameTimer;
    [SerializeField] private GameObject bombsUI;

    // State
    private bool isDoorOpen = false;
    private bool gameElementsActivated = false;
    private bool isDoorMoving = false;
    private Quaternion closedRotation;
    private Quaternion openRotation;
    private AudioSource audioSource;
    private GameTimer attachedTimer;
    public Animator lockedDoorAnimator;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        if (lockedDoorAnimator == null)
        {
            lockedDoorAnimator = GetComponent<Animator>();
        }

        // Store the initial rotation as closed rotation
        closedRotation = transform.rotation;
        openRotation = Quaternion.Euler(transform.eulerAngles + new Vector3(XAngle, YAngle, ZAngle));

        // Get the GameTimer component if it exists
        attachedTimer = GetComponent<GameTimer>();
    }

    private void Start()
    {
        // Load door states from online database
        if (ProgressManager.Instance != null && ProgressManager.Instance.isDataLoaded)
        {
            // Use ProgressManager to get door state
            Debug.Log($"[DoorInteraction] Door {doorID} - ProgressManager already ready at Start, current isUnlockable={isUnlockable}");
            
            // Ensure door data exists in database
            ProgressManager.Instance.EnsureDoorDataExists(doorID, gameObject.name);
            
            // Update door state from database
            ProgressManager.Instance.UpdateDoorInteraction(this);
            
            Debug.Log($"[DoorInteraction] Door {doorID} - After immediate DB update: isUnlockable={isUnlockable}");
        }
        else
        {
            Debug.LogWarning($"[DoorInteraction] Door {doorID} - ProgressManager not ready at Start, will update when data is loaded");
            StartCoroutine(WaitForProgressManager());
        }
    }
    
    private IEnumerator WaitForProgressManager()
    {
        // Wait for ProgressManager to be ready (up to 10 seconds)
        float timeout = 10f;
        float elapsed = 0f;
        
        Debug.Log($"[DoorInteraction] Door {doorID} - Starting to wait for ProgressManager, current isUnlockable={isUnlockable}");
        
        while (elapsed < timeout)
        {
            if (ProgressManager.Instance != null && ProgressManager.Instance.isDataLoaded)
            {
                // ProgressManager is ready, ensure door data exists
                ProgressManager.Instance.EnsureDoorDataExists(doorID, gameObject.name);
                
                // Update door state
                Debug.Log($"[DoorInteraction] ProgressManager now ready after {elapsed}s, updating door {doorID}, current isUnlockable={isUnlockable}");
                ProgressManager.Instance.UpdateDoorInteraction(this);
                Debug.Log($"[DoorInteraction] Door {doorID} - After delayed DB update: isUnlockable={isUnlockable}");
                yield break;
            }
            
            // Wait before checking again
            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
        }
        
        // If we get here, ProgressManager didn't become ready within the timeout period
        Debug.LogWarning($"[DoorInteraction] Timed out waiting for ProgressManager for door {doorID}");
    }

    private void Update()
    {
        // Door movement animation
        if (isDoorMoving)
        {
            AnimateDoor();
        }
    }

    public bool IsLocked()
    {
        return isLocked;
    }

    public bool IsUnlockable()
    {
        return isUnlockable;
    }

    public void SetUnlockable(bool unlockable)
    {
        isUnlockable = unlockable;
        SaveDoorStatesToDatabase();
        Debug.Log($"[DoorInteraction] Door {gameObject.name} unlockable status set to: {unlockable}");
    }

    public void SetRoomCompleted(bool completed)
    {
        isRoomCompleted = completed;
        SaveDoorStatesToDatabase();
        Debug.Log($"[DoorInteraction] Door {gameObject.name} room completion status set to: {completed}");
    }

    public int GetDoorID()
    {
        return doorID;
    }

    public void TryOpenDoor()
    {
        if (isLocked)
        {
            PlayDoorLockedAnimation();
            PlayDoorLockedSound();
            // The use key button will be enabled by PlayerInteraction
        }
        else if (!isLocked)
        {
            if (!isDoorOpen && !gameElementsActivated)
            {
                ActivateGameElements();
                gameElementsActivated = true;
            }
            ToggleDoorOpenClose();
        }
    }

    private void PlayDoorLockedSound()
    {
        if (doorLockedSound != null && audioSource != null)
        {
            if (!audioSource.isPlaying)
            {
                audioSource.PlayOneShot(doorLockedSound);
            }
        }
    }

    private void ActivateGameElements()
    {
        if (chemicalBomb != null)
        {
            chemicalBomb.SetActive(true);
        }
        if (throwButton != null)
        {
            throwButton.gameObject.SetActive(true);
        }
        if (bombsUI != null)
        {
            bombsUI.gameObject.SetActive(true);
        }
        if (shouldStartTimer)
        {
            if (gameTimer != null)
            {
                gameTimer.gameObject.SetActive(true);
            }
        }
    }

    public void PlayDoorLockedAnimation()
    {
        if (lockedDoorAnimator != null)
        {
            lockedDoorAnimator.SetBool("isLocked", true);
            Invoke("StopDoorLockedAnimation", 0.5f);
        }
    }

    private void StopDoorLockedAnimation()
    {
        if (lockedDoorAnimator != null)
        {
            lockedDoorAnimator.SetBool("isLocked", false);
        }
    }

    // These methods are no longer needed as PlayerInteraction will handle the UI
    // Keeping them empty for backward compatibility
    public void EnableUseKeyButton() { }
    public void DisableUseKeyButton() { }

    // Public so PlayerInteraction can call it
    public void TryUnlockDoor()
    {
        if (KeyManager.Instance.GetCurrentKeyCount() > 0)
        {
            UnlockDoor();
            // The use key button will be disabled by PlayerInteraction
        }
        else
        {
            Debug.Log("[DoorInteraction] Cannot unlock door, no keys available");
        }
    }

    public void UnlockDoor()
    {
        if (isLocked)
        {
            KeyManager.Instance.UseKey();
            audioSource.PlayOneShot(doorUnlockSound);
            isLocked = false;
            lockedDoorAnimator.enabled = false;
            
            // Update the door status in the online database
            if (ProgressManager.Instance != null)
            {
                ProgressManager.Instance.StartCoroutine(
                    ProgressManager.Instance.UpdateDoorStatus(doorID, isUnlockable, isRoomCompleted)
                );
            }
            else
            {
                Debug.LogWarning($"[DoorInteraction] ProgressManager not available, couldn't update door {doorID} status");
            }
        }
    }

    private void ToggleDoorOpenClose()
    {
        isDoorOpen = !isDoorOpen;
        isDoorMoving = true;

        if (audioSource != null)
        {
            AudioClip clip = isDoorOpen ? doorOpenSound : doorCloseSound;
            if (clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
        
        // If this is the first time opening the door and it's not already marked as completed
        if (isDoorOpen && !isRoomCompleted)
        {
            // Mark the room as completed in the database
            if (ProgressManager.Instance != null)
            {
                ProgressManager.Instance.MarkRoomAsCompleted(doorID);
                isRoomCompleted = true;
            }
            else
            {
                Debug.LogWarning($"[DoorInteraction] ProgressManager not available, couldn't mark room for door {doorID} as completed");
            }
        }
    }

    private void AnimateDoor()
    {
        Quaternion targetRotation = isDoorOpen ? openRotation : closedRotation;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, doorSpeed * 100 * Time.deltaTime);

        if (Quaternion.Angle(transform.rotation, targetRotation) < 0.1f)
        {
            transform.rotation = targetRotation;
            isDoorMoving = false;

            if (isDoorOpen && shouldStartTimer && attachedTimer != null && attachedTimer.HasBeenTriggered())
            {

            }
        }
    }

    private void DisableDoorInteraction()
    {
        if (lockedDoorAnimator != null)
        {
            lockedDoorAnimator.SetBool("isLocked", false);
        }
    }

    // For backward compatibility
    public void ResetDoor()
    {
        if (isDoorOpen)
        {
            isDoorOpen = false;
            isDoorMoving = true;

            if (audioSource != null && doorCloseSound != null)
            {
                audioSource.PlayOneShot(doorCloseSound);
            }
        }
        GetComponent<Animator>().enabled = true;
    }

    // Added for GameManager to lock doors during reset
    public void LockDoor()
    {
        isLocked = true;
        
        // Re-enable the animator if it was disabled during unlock
        if (lockedDoorAnimator != null)
        {
            lockedDoorAnimator.enabled = true;
        }
        
        // Reset game elements activation state
        gameElementsActivated = false;
    }

    // Update door visuals based on current state
    public void UpdateDoorVisuals()
    {
        Debug.Log($"[DoorInteraction] === Updating visuals for door {doorID} ===");
        Debug.Log($"[DoorInteraction] Current state - isUnlockable: {isUnlockable}, isRoomCompleted: {isRoomCompleted}, isLocked: {isLocked}");
        
        // If the door has an animator, update its parameters
        if (lockedDoorAnimator != null)
        {
            Debug.Log($"[DoorInteraction] Found animator. Current animator state - IsUnlockable: {lockedDoorAnimator.GetBool("IsUnlockable")}");
            lockedDoorAnimator.SetBool("IsUnlockable", isUnlockable);
        }
        else
        {
            Debug.LogWarning("[DoorInteraction] No animator found on door!");
        }
        
        // If the door is unlockable, make sure it's properly set up
        if (isUnlockable)
        {
            Debug.Log($"[DoorInteraction] Door {doorID} is unlockable - performing unlockable door setup");
            // Additional setup for unlockable doors if needed
        }
        
        Debug.Log($"[DoorInteraction] === Finished updating visuals for door {doorID} ===\n");
    }

    // Save door states to online database
    private void SaveDoorStatesToDatabase()
    {
        if (ProgressManager.Instance != null)
        {
            ProgressManager.Instance.StartCoroutine(
                ProgressManager.Instance.UpdateDoorStatus(doorID, isUnlockable, isRoomCompleted)
            );
            Debug.Log($"[DoorInteraction] Saved door states for {doorID} to online database: isUnlockable={isUnlockable}, isRoomCompleted={isRoomCompleted}");
        }
        else
        {
            Debug.LogWarning($"[DoorInteraction] ProgressManager not available, couldn't save door {doorID} states");
        }
    }
}
