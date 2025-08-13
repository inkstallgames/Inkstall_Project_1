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
        Alien alien = collision.gameObject.GetComponent<Alien>();
        if (alien != null)
        {
            alien.PlayDeathEffect();
            PlayExplosionEffects();
            Destroy(gameObject);
            return;
        }

        // Handle other collisions (walls, environment)
        PlayExplosionEffects();
        Destroy(gameObject);
    }

    private void PlayExplosionEffects()
    {
        // Play particle effect
        if (explosionEffect != null)
        {
            Instantiate(explosionEffect, transform.position, Quaternion.identity);
        }

        // Play sound effect
        if (explosionSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(explosionSound);
        }
    }
}