using UnityEngine;

public class BombPhysics : MonoBehaviour
{
    [Header("Effects")]
    [SerializeField] private ParticleSystem explosionEffect;
    [SerializeField] private AudioClip explosionSound;

    [SerializeField] private GameObject alienPrefab;
    private Transform player;
    
    private AudioSource audioSource;

    private void Start()
    {
        // Find the player object
        player = GameObject.FindGameObjectWithTag("Player").transform;
        
        if (player != null)
        {
            // Make the alien look at the player (only on Y axis)
            Vector3 directionToPlayer = player.position - transform.position;
            directionToPlayer.y = 0; // Keep the rotation only on Y axis
            if (directionToPlayer != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(directionToPlayer);
            }
        }
        
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Handle alien collision
        if (collision.gameObject.tag == "AlienProp")
        {
            AlienFound(collision);
            PlayHitEffect();
            PlayHitSound();

            Destroy(collision.gameObject);
            Destroy(gameObject);
            
            return;
        }

        // Handle other collisions
        PlayHitEffect();
        PlayHitSound();
        Destroy(gameObject);
    }

    public void AlienFound(Collision collision)
    {
        // 1) Instantiate the new alien first
        GameObject newAlien = Instantiate(alienPrefab, collision.gameObject.transform.position, collision.gameObject.transform.rotation);

        // 2) Make the new alien look at the player
        if (player != null)
        {
            newAlien.transform.LookAt(player);
        }
        
        // 3) Get the particle system from the instantiated prefab and play it
        ParticleSystem particleSystem = newAlien.GetComponentInChildren<ParticleSystem>();
        if (particleSystem != null)
        {
            particleSystem.Play();
        }
        
        // 4) Play the alien death animation 
        Animator animator = newAlien.GetComponent<Animator>();
        if (animator != null)
        {
            animator.Play("ZombieDeath"); // Replace with your animation state name
        }

        // 5) Play Death sound Effect
        alienPrefab.GetComponent<AudioSource>().Play();
        
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