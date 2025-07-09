using UnityEngine;

public class DoorInteraction : MonoBehaviour
{
    [Header("Door Settings")]
    [SerializeField] private float XAngle = 0f;
    [SerializeField] private float YAngle = -90f;
    [SerializeField] private float ZAngle = 0f;
    [SerializeField] private float doorSpeed = 2f;
    [SerializeField] private AudioClip doorOpenSound;
    [SerializeField] private AudioClip doorCloseSound;

    [Header("Timer Settings")]
    [SerializeField] private bool shouldStartTimer = false; // Unchecked by default

    private bool isDoorOpen = false;
    private bool isDoorMoving = false;
    private Quaternion closedRotation;
    private Quaternion openRotation;
    private AudioSource audioSource;
    private GameTimer attachedTimer; // Reference to the timer attached to this door
    private bool isEnabled = false; // Track if this script is enabled

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Store the initial rotation as closed rotation
        closedRotation = transform.rotation;
        
        // Calculate the open rotation based on the openAngle
        openRotation = Quaternion.Euler(transform.eulerAngles + new Vector3(XAngle, YAngle, ZAngle));
        
        // Get the GameTimer component attached to this door
        attachedTimer = GetComponent<GameTimer>();
        
        // Log warning if timer should start but no GameTimer is attached
        if (shouldStartTimer && attachedTimer == null)
        {
            Debug.LogWarning("DoorInteraction is set to start timer, but no GameTimer component is attached to this door. Add a GameTimer component to this door object.");
        }
        
        // Set the enabled flag
        isEnabled = enabled;
    }

    private void OnEnable()
    {
        // When this script is enabled, make sure we have a reference to the timer
        if (attachedTimer == null)
        {
            attachedTimer = GetComponent<GameTimer>();
        }
        
        isEnabled = true;
    }
    
    private void OnDisable()
    {
        isEnabled = false;
    }

    private void Update()
    {
        // Door movement animation
        if (isDoorMoving)
        {
            AnimateDoor();
        }
    }

    // This is the method called by PlayerInteraction script
    public void Interact()
    {
        // Toggle door open/close
        ToggleDoor();
        
        // Start the timer if:
        // 1. We should start the timer (flag is checked)
        // 2. The script is enabled
        // 3. We have a valid timer reference
        // 4. The timer hasn't been triggered before
        if (shouldStartTimer && isEnabled && attachedTimer != null && !attachedTimer.HasBeenTriggered())
        {
            attachedTimer.ResumeTimer();
            Debug.Log("Door interaction started the timer for the first time!");
            
            // We'll show the chances text in the AnimateDoor method after the door has opened
        }
    }

    private void ToggleDoor()
    {
        // Toggle the door state immediately
        isDoorOpen = !isDoorOpen;
        isDoorMoving = true;

        // Play appropriate sound
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
        // Get target rotation based on door state
        Quaternion targetRotation = isDoorOpen ? openRotation : closedRotation;
        
        // Use MoveTowards for a more direct rotation without smooth ending
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, doorSpeed * 100 * Time.deltaTime);
        
        // Check if door reached target position
        if (Quaternion.Angle(transform.rotation, targetRotation) < 0.1f)
        {
            transform.rotation = targetRotation; // Snap to exact position
            isDoorMoving = false;
            
            // Show chances text when the door has finished opening (not closing)
            if (isDoorOpen && shouldStartTimer && attachedTimer != null && attachedTimer.HasBeenTriggered())
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.ShowChancesText();
                }
            }
        }
    }
}
