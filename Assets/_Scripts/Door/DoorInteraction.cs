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
    [SerializeField] private AudioClip doorUnlockSound;
    [SerializeField] private AudioClip doorLockedSound;

    [Header("Timer Settings")]
    [SerializeField] private bool shouldStartTimer = false;

    public Button openCloseButton;
    public Button useKeyButton;
    public GameObject useKeyButtonObject;

    // State
    private bool isDoorOpen = false;
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
        if (openCloseButton != null)
        {
            openCloseButton.onClick.AddListener(TryOpenDoor);
        }

        if (useKeyButton != null)
        {
            useKeyButton.onClick.AddListener(TryUnlockDoor);
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
    
    
    public void TryOpenDoor()
    {
        if (isLocked)
        {
            PlayDoorLockedAnimation();
            PlayDoorLockedSound();
            EnableUseKeyButton();
            return;
        }

        if (!isLocked)
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

    public void EnableUseKeyButton()
    {
        useKeyButtonObject.SetActive(true);
    }

    public void DisableUseKeyButton()
    {
        useKeyButtonObject.SetActive(false);
    }   

    private void TryUnlockDoor()
    {
        if (KeyManager.Instance != null && KeyManager.Instance.GetCurrentKeyCount() > 0)
        {
            UnlockDoor();
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
            DisableUseKeyButton();
            isLocked = false;
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
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.ShowChancesText();
                }
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
    }
    
}
