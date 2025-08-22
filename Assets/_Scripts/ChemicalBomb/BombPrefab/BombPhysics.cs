using UnityEngine;

public class BombPhysics : MonoBehaviour
{
    [Header("Effects")]
    [SerializeField] private ParticleSystem hitEffect;
    [SerializeField] private AudioClip hitSound;
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
    }
    
    private void PlayHitEffect()
    {
        // Play particle effect
        if (hitEffect != null)
        {
            Instantiate(hitEffect, transform.position, Quaternion.identity);
        }
    }

    private void PlayHitSound()
    {
        // Play sound effect
        if (hitSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(hitSound);
        }
    }
   
}