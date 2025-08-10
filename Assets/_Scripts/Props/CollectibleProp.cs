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

    public void Interact()
    {
        
    }
}