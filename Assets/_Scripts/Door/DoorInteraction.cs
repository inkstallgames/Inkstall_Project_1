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
    [SerializeField] public int doorID; // Unique identifier int to match ProgressManager

    [Header("Timer Settings")]
    [SerializeField] private bool shouldStartTimer = false;

    [Header("Game Activation Settings")]
    [SerializeField] private GameObject chemicalBomb;
    [SerializeField] private Button throwButton;
    [SerializeField] private TextMeshProUGUI gameTimer;
    [SerializeField] private Image bombsUI;
    [SerializeField] private Image remainingAliensCountContainer;


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
        try
        {
            // Subscribe to the OnDataLoaded event to update door state when data is loaded
            ProgressManager.OnDataLoaded += OnProgressDataLoaded;

            // Load door states from online database
            if (ProgressManager.Instance != null && ProgressManager.Instance.isDataLoaded)
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
        catch (System.Exception e)
        {
            if (doorID >= 1 && doorID <= 24)
            {
                Debug.LogError($"[DoorInteraction] Error in Start (Door {doorID}): {e.Message}");
            }
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from the event when this object is destroyed
        ProgressManager.OnDataLoaded -= OnProgressDataLoaded;
    }

    private void OnProgressDataLoaded()
    {
        try
        {
            if (ProgressManager.Instance != null && ProgressManager.Instance.isDataLoaded)
            {
                // Ensure door data exists in database
                ProgressManager.Instance.EnsureDoorDataExists(doorID, gameObject.name);

                // Update door state from database
                ProgressManager.Instance.UpdateDoorInteraction(this);
            }
        }
        catch (System.Exception e)
        {
            if (doorID >= 1 && doorID <= 24)
            {
                Debug.LogError($"[DoorInteraction] Error in OnProgressDataLoaded (Door {doorID}): {e.Message}");
            }
        }
    }

    private IEnumerator WaitForProgressManager()
    {
        float timeout = 10f;
        float elapsed = 0f;

        if (doorID >= 1 && doorID <= 24)
        {
            Debug.Log($"[DoorInteraction] Door {doorID} - Waiting for ProgressManager to be ready");
        }

        while (elapsed < timeout)
        {
            try
            {
                if (ProgressManager.Instance != null && ProgressManager.Instance.isDataLoaded)
                {
                    ProgressManager.Instance.EnsureDoorDataExists(doorID, gameObject.name);

                    // Get door data directly from ProgressManager
                    DoorData doorData = ProgressManager.Instance.GetDoorData(doorID);

                    if (doorData != null)
                    {
                        // Update door state from database
                        if (doorID >= 1 && doorID <= 24)
                        {
                            Debug.Log($"[DoorInteraction] Door {doorID} - ProgressManager ready after {elapsed:F1}s. Updating door state.");
                        }

                        isUnlockable = doorData.isUnlockable;
                        isRoomCompleted = doorData.isRoomCompleted;

                        // Update visuals based on the new state
                        UpdateDoorVisuals();

                        if (doorID >= 1 && doorID <= 24)
                        {
                            Debug.Log($"[DoorInteraction] Door {doorID} - Updated from database: isUnlockable={isUnlockable}, isRoomCompleted={isRoomCompleted}");
                        }
                    }
                    else if (doorID >= 1 && doorID <= 24)
                    {
                        Debug.LogWarning($"[DoorInteraction] Door {doorID} - No door data found in database after waiting");
                    }

                    yield break;
                }
            }
            catch (System.Exception e)
            {
                if (doorID >= 1 && doorID <= 24)
                {
                    Debug.LogError($"[DoorInteraction] Error in WaitForProgressManager: {e.Message}");
                }
                // Continue waiting even if there's an error
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // If we get here, ProgressManager didn't become ready within the timeout period
        if (doorID >= 1 && doorID <= 24)
        {
            Debug.LogWarning($"[DoorInteraction] Door {doorID} - Timed out waiting for ProgressManager");
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
    }

    public void SetRoomCompleted(bool completed)
    {
        isRoomCompleted = completed;
        SaveDoorStatesToDatabase();
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
    }

    public void UnlockDoor()
    {
        if (isLocked)
        {
            KeyManager.Instance.UseKey();
            audioSource.PlayOneShot(doorUnlockSound);
            isLocked = false;
            lockedDoorAnimator.enabled = false;
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
        try
        {
            // If the door has an animator, update its parameters
            if (lockedDoorAnimator != null && HasAnimatorParameter("IsUnlockable"))
            {
                lockedDoorAnimator.SetBool("IsUnlockable", isUnlockable);
            }

            // If the door is unlockable, make sure it's properly set up
            if (isUnlockable)
            {
                isLocked = false; // Ensure unlockable doors are not locked
            }
        }
        catch (System.Exception e)
        {
            if (doorID >= 1 && doorID <= 24)
            {
                Debug.LogError($"[DoorInteraction] Error in UpdateDoorVisuals (Door {doorID}): {e.Message}");
            }
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

    // Save door states to online database
    private void SaveDoorStatesToDatabase()
    {
        if (ProgressManager.Instance != null)
        {
            ProgressManager.Instance.StartCoroutine(
                ProgressManager.Instance.UpdateDoorStatus(doorID, isUnlockable, isRoomCompleted)
            );
        }
        else if (doorID >= 1 && doorID <= 24)
        {
            Debug.LogWarning($"[DoorInteraction] Door {doorID} - ProgressManager not available, couldn't save door states");
        }
    }
}
