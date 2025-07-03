using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class DoorStateHandler : MonoBehaviour
{
    [Header("Locked Door Sound")]
    public AudioClip lockedDoorSound;
    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void PlayLockedSound()
    {
        if (lockedDoorSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(lockedDoorSound);
        }
    }
}
