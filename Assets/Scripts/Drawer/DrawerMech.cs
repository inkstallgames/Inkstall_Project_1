using UnityEngine;

public class DrawerMech : MonoBehaviour
{
    [Header("Drawer Settings")]
    [SerializeField] private Vector3 openPosition = new Vector3(0, 0, 0.5f);
    [SerializeField] private float drawerSpeed = 2f;
    [SerializeField] private AudioClip drawerOpenSound;
    [SerializeField] private AudioClip drawerCloseSound;

    private bool isDrawerOpen = false;
    private bool isDrawerMoving = false;
    private Vector3 closedPosition;
    private AudioSource audioSource;

    private void Start()
    {
        // Get or add audio source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Store initial position as closed position
        closedPosition = transform.localPosition;
    }

    private void Update()
    {
        // Drawer movement animation
        if (isDrawerMoving)
        {
            AnimateDrawer();
        }
    }

    // This is the method called by PlayerInteraction script
    public void Interact()
    {
        // Toggle drawer open/close
        ToggleDrawer();
    }

    private void ToggleDrawer()
    {
        if (isDrawerMoving) return;

        isDrawerOpen = !isDrawerOpen;
        isDrawerMoving = true;

        // Play appropriate sound
        if (audioSource != null)
        {
            AudioClip clip = isDrawerOpen ? drawerOpenSound : drawerCloseSound;
            if (clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
    }

    private void AnimateDrawer()
    {
        // Calculate target position
        Vector3 targetPosition = isDrawerOpen ? closedPosition + openPosition : closedPosition;
        
        // Smoothly move to target
        transform.localPosition = Vector3.MoveTowards(
            transform.localPosition, 
            targetPosition, 
            drawerSpeed * Time.deltaTime
        );
        
        // Check if drawer reached target position
        if (Vector3.Distance(transform.localPosition, targetPosition) < 0.001f)
        {
            transform.localPosition = targetPosition; // Snap to exact position
            isDrawerMoving = false;
        }
    }
}
