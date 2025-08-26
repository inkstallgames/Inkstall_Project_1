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
    [SerializeField] private bool isUnlockable = false;
    [SerializeField] public bool isRoomCompleted = false;
    [SerializeField] private string doorID; // Unique identifier for this door

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
        
        // Generate a doorID if not set
        if (string.IsNullOrEmpty(doorID))
        {
            doorID = gameObject.name + "_" + gameObject.GetInstanceID();
            Debug.Log($"[DoorInteraction] Generated doorID: {doorID}");
        }
    }

    private void Start()
    {
        // Load door states from database
        if (ProgressManager.Instance != null && ProgressManager.Instance.isDataLoaded)
        {
            // Use ProgressManager to get door state
            ProgressManager.Instance.UpdateDoorInteraction(this);
        }
        else
        {
            // Fallback to local DataManager if ProgressManager is not available
            LoadDoorStatesFromDatabase();
        }
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

    public string GetDoorID()
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
            
            // Update the door status in the database
            int doorIdInt;
            if (int.TryParse(doorID, out doorIdInt) && ProgressManager.Instance != null)
            {
                ProgressManager.Instance.StartCoroutine(
                    ProgressManager.Instance.UpdateDoorStatus(doorIdInt, isUnlockable, isRoomCompleted)
                );
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
            // Try to parse the door ID to an integer
            int doorIdInt;
            if (int.TryParse(doorID, out doorIdInt))
            {
                // Mark the room as completed in the database
                if (ProgressManager.Instance != null)
                {
                    ProgressManager.Instance.MarkRoomAsCompleted(doorIdInt);
                    isRoomCompleted = true;
                }
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

    // Load door states from database
    private void LoadDoorStatesFromDatabase()
    {
        if (DataManager.Instance != null)
        {
            isUnlockable = DataManager.Instance.LoadDoorUnlockableState(doorID);
            isRoomCompleted = DataManager.Instance.LoadRoomCompletionState(doorID);
            Debug.Log($"[DoorInteraction] Loaded door states for {doorID}: isUnlockable={isUnlockable}, isRoomCompleted={isRoomCompleted}");
        }
        else
        {
            Debug.LogWarning("[DoorInteraction] DataManager instance not found. Using default door states.");
        }
    }
    
    // Save door states to database
    private void SaveDoorStatesToDatabase()
    {
        // Try to save to ProgressManager first
        int doorIdInt;
        if (int.TryParse(doorID, out doorIdInt) && ProgressManager.Instance != null)
        {
            ProgressManager.Instance.StartCoroutine(
                ProgressManager.Instance.UpdateDoorStatus(doorIdInt, isUnlockable, isRoomCompleted)
            );
            return;
        }
        
        // Fallback to local DataManager
        if (DataManager.Instance != null)
        {
            DataManager.Instance.SaveDoorUnlockableState(doorID, isUnlockable);
            DataManager.Instance.SaveRoomCompletionState(doorID, isRoomCompleted);
            Debug.Log($"[DoorInteraction] Saved door states for {doorID}: isUnlockable={isUnlockable}, isRoomCompleted={isRoomCompleted}");
        }
        else
        {
            Debug.LogWarning("[DoorInteraction] DataManager instance not found. Door states not saved.");
        }
    }
}
