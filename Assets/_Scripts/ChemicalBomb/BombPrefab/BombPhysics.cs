using UnityEngine;
using System;

public class BombPhysics : MonoBehaviour
{
    // Static event that RoomManagers can subscribe to
    public static event Action<GameObject> OnAlienDestroyed;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem hitEffect;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private GameObject alienPrefab;
    private Transform player;
    
    private AudioSource audioSource;
    private bool hasCollided = false;  // Flag to track if collision has been processed

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
        // Skip if already processed a collision
        if (hasCollided) return;
        
        GameObject collidedObject = collision.gameObject;
        
        // Check if the object itself or any of its parents has the AlienProp tag
        if (HasAlienPropTag(collidedObject))
        {
            // Find the root AlienProp object (could be parent)
            GameObject alienPropObject = FindAlienPropObject(collidedObject);
            
            // Mark as collided to prevent multiple triggers
            hasCollided = true;
            
            // Use the found AlienProp object for the collision handling
            AlienFound(collision);
            PlayHitEffect();
            PlayHitSound();

            // Notify any listeners (like RoomManager) that an alien was destroyed
            OnAlienDestroyed?.Invoke(alienPropObject);

            Destroy(alienPropObject);
            Destroy(gameObject);
            
            return;
        }

        // Handle other collisions
        hasCollided = true;
        PlayHitEffect();
        PlayHitSound();
        Destroy(gameObject);
    }

    // Check if the object or any of its parents has the AlienProp tag
    private bool HasAlienPropTag(GameObject obj)
    {
        // Check current object
        if (obj.CompareTag("AlienProp"))
        {
            return true;
        }
        
        // Check all parents
        Transform parent = obj.transform.parent;
        while (parent != null)
        {
            if (parent.CompareTag("AlienProp"))
            {
                return true;
            }
            parent = parent.parent;
        }
        
        return false;
    }
    
    // Find the object with AlienProp tag (either self or parent)
    private GameObject FindAlienPropObject(GameObject obj)
    {
        // Check current object
        if (obj.CompareTag("AlienProp"))
        {
            return obj;
        }
        
        // Check all parents
        Transform parent = obj.transform.parent;
        while (parent != null)
        {
            if (parent.CompareTag("AlienProp"))
            {
                return parent.gameObject;
            }
            parent = parent.parent;
        }
        
        // Fallback to original object if no AlienProp found (shouldn't happen due to HasAlienPropTag check)
        return obj;
    }

    // Static event to notify when an alien is found
    public static event System.Action OnAlienFound;

    public void AlienFound(Collision collision)
    {
        // 1) Instantiate the new alien first
        GameObject newAlien = Instantiate(alienPrefab, collision.gameObject.transform.position, collision.gameObject.transform.rotation);

        // 2) Make the new alien look at the player
        if (player != null)
        {
            newAlien.transform.LookAt(player);
        }

        // Notify subscribers (RoomManager) that an alien was found
        OnAlienFound?.Invoke();
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