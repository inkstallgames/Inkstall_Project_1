using UnityEngine;
using System.Collections;
using TMPro;


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
    [SerializeField] private AudioClip unlockSound;
    [SerializeField] private AudioClip lockedSound;

    [Header("Timer Settings")]
    [SerializeField] private bool shouldStartTimer = false;

    // State
    private bool isDoorOpen = false;
    private bool isDoorMoving = false;
    private Quaternion closedRotation;
    private Quaternion openRotation;
    private AudioSource audioSource;
    private GameTimer attachedTimer;
    private Animator doorAnimator;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        doorAnimator = GetComponent<Animator>();
        
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

    public void OpenDoor()
    {
        Debug.Log("Button Pressed");
    }
    public void Interact()
    {
        if (isLocked)
        {
            PlayLockedSound();
            OnInteractAttempt();
            return;
        }

        // Check with GameManager if this door can be opened
        if (GameManager.Instance != null && !GameManager.Instance.CanOpenDoor(gameObject))
        {
            Debug.Log("Cannot open this door until current room is completed");
            return;
        }

        // Toggle door open/close
        ToggleDoor();

        if (shouldStartTimer && attachedTimer != null && !attachedTimer.HasBeenTriggered())
        {
            // Activate this room in the GameManager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ActivateRoom(gameObject);
            }

            attachedTimer.ResumeTimer();
            Debug.Log("Door interaction started the timer for the first time!");
        }
    }

    public void Unlock()
    {
        if (isLocked)
        {
            isLocked = false;

            if (unlockSound != null)
            {
                audioSource.PlayOneShot(unlockSound);
            }

            // Disable Animator if it exists
            if (doorAnimator != null)
            {
                doorAnimator.enabled = false;
            }
        }
    }

    public bool IsLocked()
    {
        return isLocked;
    }

    private void ToggleDoor()
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
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.ShowChancesText();
                }
            }
        }
    }

    private void PlayLockedSound()
    {
        if (isLocked && lockedSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(lockedSound);
        }
    }

    public void OnInteractAttempt()
    {
        if (isLocked)
        {
            PlayLockedSound();

            if (doorAnimator != null)
            {
                doorAnimator.SetBool("DoorInteractionEnable", true);
                Invoke("DisableDoorInteraction", 1f);
            }
        }
    }

    private void DisableDoorInteraction()
    {
        if (doorAnimator != null)
        {
            doorAnimator.SetBool("DoorInteractionEnable", false);
        }
    }

    private void TryUnlockDoor()
    {
        if (KeyManager.Instance != null && KeyManager.Instance.UseKey())
        {
            Unlock();
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
    }
}
