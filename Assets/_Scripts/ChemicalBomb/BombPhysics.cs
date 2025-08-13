using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BombPhysics : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        // Play particle effect on collision
        collision.gameObject.GetComponentInChildren<ParticleSystem>().Play();
        Destroy(collision.gameObject);
        
        // and also play sound effect

        if (collision.gameObject.CompareTag("Alien"))
        {
            collision.gameObject.SetActive(false);
            
            collision.gameObject.GetComponent<Alien>().PlayDeathEffect();
        }
        
    }
}
