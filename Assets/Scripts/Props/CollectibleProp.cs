using UnityEngine;

public class CollectibleProp : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioClip fakePickupSound;
    [SerializeField] private AudioClip realInteractSound;
    [Range(0f, 1f)] [SerializeField] private float soundVolume = 0.7f;
    
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
            
            // Notify GameManager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.CollectProp();
            }
            else
            {
                Debug.LogWarning("GameManager instance not found when collecting prop.");
            }
            
            gameObject.SetActive(false);
            return true;
        }
        else
        {
            // For real props, just play the sound
            if (realInteractSound != null)
            {
                AudioSource.PlayClipAtPoint(realInteractSound, transform.position, soundVolume);
            }
            return false;
        }
    }
}