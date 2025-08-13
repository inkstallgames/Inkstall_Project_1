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
}
