using UnityEngine;

public class BombPhysics : MonoBehaviour
{
    [Header("Effects")]
    [SerializeField] private ParticleSystem explosionEffect;
    [SerializeField] private AudioClip explosionSound;
    
    private AudioSource audioSource;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Handle alien collision
        AlienProp alien = collision.gameObject.GetComponent<AlienProp>();
        if (alien != null)
        {
            alien.AlienFound();
            PlayHitEffect();
            PlayHitSound();

            Destroy(gameObject);
            Destroy(alien.gameObject);
            return;
        }

        // Handle other collisions
        PlayHitEffect();
        PlayHitSound();
        Destroy(gameObject);
    }

    private void PlayHitEffect()
    {
        // Play particle effect
        if (explosionEffect != null)
        {
            Instantiate(explosionEffect, transform.position, Quaternion.identity);
        }
    }

    private void PlayHitSound()
    {
        // Play sound effect
        if (explosionSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(explosionSound);
        }
    }
}