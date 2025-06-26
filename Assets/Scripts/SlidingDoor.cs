using UnityEngine;

public class SlidingDoor : MonoBehaviour
{
    [Header("Door Settings")]
    [SerializeField] private Vector3 openOffset = new Vector3(2f, 0, 0); // How far the door slides
    [SerializeField] private float slideSpeed = 2f;
    [SerializeField] private AudioClip slideSound;

    private Vector3 closedPosition;
    private Vector3 targetPosition;
    private bool isOpen = false;
    private Vector3 currentVelocity; // For SmoothDamp
    private AudioSource audioSource;
    private float smoothTime; // Cached for performance

    void Awake()
    {
        // Cache the inverse of slideSpeed for better performance
        smoothTime = slideSpeed > 0 ? 1f / slideSpeed : 0.5f;
        
        // Get or add AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && slideSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D sound
        }
    }

    void Start()
    {
        closedPosition = transform.localPosition;
        targetPosition = closedPosition;
    }

    void Update()
    {
        // Using SmoothDamp instead of Lerp to avoid creating new Vector3 objects
        transform.localPosition = Vector3.SmoothDamp(
            transform.localPosition, 
            targetPosition, 
            ref currentVelocity, 
            smoothTime
        );
    }

    public void Interact()
    {
        isOpen = !isOpen;
        targetPosition = isOpen ? closedPosition + openOffset : closedPosition;
        
        // Play sound if available
        if (audioSource != null && slideSound != null)
        {
            audioSource.PlayOneShot(slideSound);
        }
    }
}
