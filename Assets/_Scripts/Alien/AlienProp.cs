using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AlienProp : MonoBehaviour
{
    [SerializeField] private GameObject alienPrefab;
    private Transform player;

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
    }
    
    public void AlienFound()
    {
        // 1) Instantiate the new alien first
        GameObject newAlien = Instantiate(alienPrefab, transform.position, transform.rotation);

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
        
        // 6) Destroy alien after animation
       Invoke("DestroyAlien", 2f);
    }

    public void DestroyAlien()
    {
        Destroy(alienPrefab.gameObject);
    }
}
