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
    [SerializeField] private GameObject useKeyButton; // Reference to the use key button

    [Header("Lock Settings")]
    [SerializeField] public bool isLockedDoor = false;
    [SerializeField] private AudioClip doorLockedSound;
    [SerializeField] private AudioClip doorUnlockSound;
    [SerializeField] public bool isUnlockable = false;
    [SerializeField] public bool isRoomCompleted = false;
    [SerializeField] public int doorID;   // Unique identifier int to match ProgressManager

    [Header("Timer Settings")]
    [SerializeField] private bool shouldStartTimer = false;
    [SerializeField] private float timerDuration = 60f; // Default duration, can be changed in Inspector

    [Header("Room Settings")]
    public RoomManager roomManager; // Assign this in the inspector

    [Header("Game Activation Settings")]
    [SerializeField] private GameObject chemicalBomb;
    [SerializeField] private Button throwButton;
    [SerializeField] private GameObject gameTimerContainer;
    [SerializeField] private Image bombsUI;
    [SerializeField] private Image remainingAliensCountContainer;
    [SerializeField] private TextMeshProUGUI interactionText;
    [SerializeField] private Image crosshairImage;


    // State
    private bool isDoorOpen = false;
    private bool showUseKeyButton = false;  // Flag to track if we should show the use key button
    private bool gameElementsActivated = false;
    private bool isDoorMoving = false;
    private Quaternion closedRotation;
    private Quaternion openRotation;
    private AudioSource audioSource;
    private GameTimer attachedTimer;
    public Animator lockedDoorAnimator;
    
    // Event to notify when door is unlocked
    public delegate void DoorUnlockedHandler();
    public event DoorUnlockedHandler OnDoorUnlocked;
    
    private bool isUnlocked = false;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        if (AudioManager.Instance != null)
        {
            audioSource.volume = AudioManager.Instance.sfxVolume;
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
        // Subscribe to the OnDataLoaded event to update door state when data is loaded
        ProgressManager.OnDataLoaded += OnProgressDataLoaded;

        // Load door states from online database
        if (ProgressManager.Instance != null)
        {
            if (ProgressManager.Instance.isDataLoaded)
            {
                // Ensure door data exists in database
                ProgressManager.Instance.EnsureDoorDataExists(doorID, gameObject.name);

                // Update door state from database
                ProgressManager.Instance.UpdateDoorInteraction(this);
            }
            else
            {
                StartCoroutine(WaitForProgressManager());
            }
        }
        else
        {
            StartCoroutine(WaitForProgressManager());
        }

        // Update visuals based on current state (will be overridden when data loads)
        UpdateDoorVisuals();
    }

    private void OnDestroy()
    {
        // Unsubscribe from the event when this object is destroyed
        ProgressManager.OnDataLoaded -= OnProgressDataLoaded;
        
        // Clear all event subscribers
        OnDoorUnlocked = null;
    }

    private void OnProgressDataLoaded()
    {
        if (ProgressManager.Instance != null && ProgressManager.Instance.isDataLoaded)
        {
            // Ensure door data exists in database
            ProgressManager.Instance.EnsureDoorDataExists(doorID, gameObject.name);

            // Update door state from database
            ProgressManager.Instance.UpdateDoorInteraction(this);

        }


    }

    private IEnumerator WaitForProgressManager()
    {
        float timeout = 30f;
        float elapsed = 0f;

        // Wait until ProgressManager.Instance is not null
        while (ProgressManager.Instance == null && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // If ProgressManager is still null after timeout, log an error and exit
        if (ProgressManager.Instance == null)
        {
            Debug.LogError($"[DoorInteraction] Timed out waiting for ProgressManager.Instance. Door {doorID} will not be initialized correctly.");
            yield break;
        }

        // Now wait until the data is loaded
        elapsed = 0f;
        while (!ProgressManager.Instance.isDataLoaded && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // If data is still not loaded after timeout, log an error and exit
        if (!ProgressManager.Instance.isDataLoaded)
        {
            Debug.LogError($"[DoorInteraction] Timed out waiting for ProgressManager data to be loaded. Door {doorID} may not be up to date.");
            yield break;
        }

        // Data is loaded, so we can safely update the door
        OnProgressDataLoaded();
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
        return isLockedDoor;
    }

    public bool IsUnlockable()
    {
        return isUnlockable;
    }

    public void SetUnlockable(bool unlockable)
    {
        isUnlockable = unlockable;
        if (isUnlockable && !isUnlocked)
        {
            isUnlocked = true;
            // Notify listeners that the door has been unlocked
            OnDoorUnlocked?.Invoke();
        }
        if (ProgressManager.Instance != null)
        {
            var updates = new System.Collections.Generic.Dictionary<string, object> { { "isUnlockable", unlockable } };
            ProgressManager.Instance.StartCoroutine(ProgressManager.Instance.UpdateDoorStatus(doorID, updates));
        }
        UpdateDoorVisuals();
    }

    public bool IsUnlocked()
    {
        return isUnlocked;
    }

    public void SetRoomCompleted(bool completed)
    {
        isRoomCompleted = completed;
        if (ProgressManager.Instance != null)
        {
            var updates = new System.Collections.Generic.Dictionary<string, object> { { "isRoomCompleted", completed } };
            ProgressManager.Instance.StartCoroutine(ProgressManager.Instance.UpdateDoorStatus(doorID, updates));
        }
    }


    public int GetDoorID()
    {
        return doorID;
    }

    public void Interact()
    {
        // If room is already completed, just show the message and return
        if (isRoomCompleted)
        {
            // Show completed room message
            if (interactionText != null)
            {
                interactionText.gameObject.SetActive(true);
                interactionText.text = "Room Already Completed";

                if (crosshairImage != null)
                {
                    crosshairImage.gameObject.SetActive(false);
                }
            }
            return; // Exit the method early
        }

        // Only check for locked door if room is not completed
        if (isLockedDoor)
        {
            if (isUnlockable && !isRoomCompleted)
            {
                PlayDoorLockedAnimation();
                PlayDoorLockedSound();
                useKeyButton.SetActive(true);
                interactionText.gameObject.SetActive(true);
                interactionText.text = "Use [Key] to Unlock";
                if (crosshairImage != null)
                {
                    crosshairImage.gameObject.SetActive(false);
                }
            }
            else if (!isUnlockable && !isRoomCompleted)
            {
                if (interactionText != null)
                {
                    interactionText.gameObject.SetActive(true);
                    interactionText.text = "Complete Previous Room First";
                }

                if (crosshairImage != null)
                {
                    crosshairImage.gameObject.SetActive(false);
                }
            }
        }
        else if (!isLockedDoor)
        {
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
        if (remainingAliensCountContainer != null)
        {
            remainingAliensCountContainer.gameObject.SetActive(true);
        }

        // Activate chemical bomb if available
        if (chemicalBomb != null)
        {
            chemicalBomb.SetActive(true);
        }

        // Activate throw button if available
        if (throwButton != null)
        {
            throwButton.gameObject.SetActive(true);
        }

        // Activate bombs UI if available
        if (bombsUI != null)
        {
            bombsUI.gameObject.SetActive(true);
        }
        if (shouldStartTimer)
        {
            if (gameTimerContainer != null)
            {
                gameTimerContainer.SetActive(true);
                GameTimer timer = gameTimerContainer.GetComponentInChildren<GameTimer>();
                if (timer != null)
                {
                    timer.StartTimer(timerDuration);
                }
            }
        }

        if (roomManager != null)
        {
            ChemicalBombManager.Instance.InitializeBombs(roomManager.maxBombs);
        }
        ChemicalBombManager.Instance.UpdateBombsUI();
        ChemicalBombManager.Instance.UpdateShopButtonState();
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

    // Public so PlayerInteraction can call it
    // Track if the door was just unlocked but not yet opened
    private bool justUnlocked = false;

    public void UnlockDoor()
    {
        if (KeyManager.Instance.GetCurrentKeyCount() > 0)
        {
            KeyManager.Instance.UseKey();
            audioSource.PlayOneShot(doorUnlockSound);
            isLockedDoor = false;
            lockedDoorAnimator.enabled = false;
            // Flag that the door was just unlocked but not yet opened
            justUnlocked = true;
            
            // Notify FloorSpawnManager that a door was unlocked
            if (FloorSpawnManager.Instance != null)
            {
                FloorSpawnManager.Instance.OnDoorUnlocked(doorID);
            }
            
            // The use key button will be disabled by PlayerInteraction
        }
    }


    private void ToggleDoorOpenClose()
    {
        if (lockedDoorAnimator != null && lockedDoorAnimator.enabled)
        {
            lockedDoorAnimator.enabled = false;
        }

        isDoorOpen = !isDoorOpen;
        isDoorMoving = true;

        // Check if this is the first time opening the door after unlocking
        if (isDoorOpen && justUnlocked)
        {
            ActivateGameElements();
            justUnlocked = false; // Reset the flag after activating
        }

        if (audioSource != null)
        {
            AudioClip clip = isDoorOpen ? doorOpenSound : doorCloseSound;
            if (clip != null)
            {
                audioSource.PlayOneShot(clip);
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
                // Timer update logic can go here if needed
                // For example, you might want to update the timer UI or perform other actions
                // when the door is open and the timer is running
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
        isLockedDoor = true;

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
        try
        {
            // If the door has an animator, update its parameters
            if (lockedDoorAnimator != null && HasAnimatorParameter("IsUnlockable"))
            {
                lockedDoorAnimator.SetBool("IsUnlockable", isUnlockable);
            }
        }
        catch (System.Exception e)
        {
        }
    }

    // Helper method to check if animator has a parameter
    private bool HasAnimatorParameter(string paramName)
    {
        if (lockedDoorAnimator == null) return false;

        foreach (AnimatorControllerParameter param in lockedDoorAnimator.parameters)
        {
            if (param.name == paramName)
            {
                return true;
            }
        }
        return false;
    }

}
