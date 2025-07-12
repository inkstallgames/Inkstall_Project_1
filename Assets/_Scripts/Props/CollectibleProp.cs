using UnityEngine;
using StarterAssets; // Add this for FirstPersonController reference

public class CollectibleProp : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioClip fakePickupSound;
    [SerializeField] private AudioClip realInteractSound;
    [Range(0f, 1f)][SerializeField] private float soundVolume = 0.7f;

    private AudioSource audioSource;
    private bool isCollected = false;
    private PropIdentity propIdentity;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D sound
            audioSource.volume = soundVolume;
        }

        // Get PropIdentity component if available
        propIdentity = GetComponent<PropIdentity>();
    }

    public bool Interact()
    {
        if (isCollected) return false;

        bool isFake = propIdentity != null && propIdentity.isFake;

        if (isFake)
        {
            isCollected = true;
            // For fake props, immediately disable the object after playing sound
            if (fakePickupSound != null)
            {
                AudioSource.PlayClipAtPoint(fakePickupSound, transform.position, soundVolume);
            }

            // Notify GameManager that a fake prop was collected
            if (GameManager.Instance != null)
            {
                GameManager.Instance.CollectFakeProp();

                // Check if all fake props have been collected for immediate win feedback
                if (GameManager.Instance.FakePropsLeft() == 0)
                {
                    // Make sure the timer stops immediately
                    if (GameManager.Instance.gameTimer != null)
                    {
                        GameManager.Instance.gameTimer.PauseTimer();
                    }
                    else
                    {
                        // Find and stop any active GameTimer in the scene if not assigned in GameManager
                        GameTimer[] timers = FindObjectsOfType<GameTimer>();
                        if (timers.Length > 0)
                        {
                            foreach (GameTimer timer in timers)
                            {
                                timer.PauseTimer();
                            }
                        }
                    }

                    // Find and disable the player controller directly
                    DisablePlayerMovementDirectly();
                }
            }
            else
            {
                // GameManager instance not found when collecting fake prop
            }

            gameObject.SetActive(false);
            return true;
        }
        else
        {
            // For real props, play the sound and reduce chances
            if (realInteractSound != null)
            {
                AudioSource.PlayClipAtPoint(realInteractSound, transform.position, soundVolume);
            }

            // Handle wrong guess with the GameManager
            if (GameManager.Instance != null)
            {
                bool gameEnded = GameManager.Instance.HandleWrongGuess(transform.position);

                // If out of chances, disable this prop
                if (gameEnded)
                {
                    isCollected = true;
                    gameObject.SetActive(false);

                    // Find and disable the player controller directly as a backup
                    DisablePlayerMovementDirectly();
                }
            }

            return false;
        }
    }

    // Helper method to directly disable player movement
    private void DisablePlayerMovementDirectly()
    {
        // Try to find the player controller in the scene
        FirstPersonController[] controllers = FindObjectsOfType<FirstPersonController>();
        if (controllers.Length > 0)
        {
            foreach (FirstPersonController controller in controllers)
            {
                controller.enabled = false;
            }
        }
    }
}