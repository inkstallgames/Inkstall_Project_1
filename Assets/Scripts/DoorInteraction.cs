using UnityEngine;

public class DoorInteraction : MonoBehaviour
{
    [Header("Door Settings")]
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float doorSpeed = 2f;
    [SerializeField] private AudioClip doorOpenSound;
    [SerializeField] private AudioClip doorCloseSound;

    private bool isDoorOpen = false;
    private bool isDoorMoving = false;
    private Quaternion closedRotation;
    private Quaternion openRotation;
    private AudioSource audioSource;

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
        openRotation = Quaternion.Euler(transform.eulerAngles + new Vector3(0, openAngle, 0));
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
    }

    private void ToggleDoor()
    {
        if (isDoorMoving) return;

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
        
        // Smoothly rotate to target
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, doorSpeed * Time.deltaTime);
        
        // Check if door reached target position (within a small threshold)
        if (Quaternion.Angle(transform.rotation, targetRotation) < 0.1f)
        {
            transform.rotation = targetRotation; // Snap to exact position
            isDoorMoving = false;
        }
    }
}
