using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Alien : MonoBehaviour
{
    [SerializeField] private GameObject deathEffect;
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
        Destroy(gameObject);
        Instantiate(deathEffect, transform.position, transform.rotation);
        GameObject newAlien = Instantiate(alienPrefab, transform.position, transform.rotation);
        newAlien.transform.LookAt(player);
    }
}
