using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BombPhysics : MonoBehaviour
{
    bool hasCollided = false;
    Rigidbody rb;
    
    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        Destroy(gameObject);
        // Play particle effect on collision
        // and also play sound effect

        if (collision.gameObject.CompareTag("Alien"))
        {
            collision.gameObject.SetActive(false);
            
            collision.gameObject.GetComponent<Alien>().PlayDeathEffect();
        }
        if (!hasCollided)
        {
            hasCollided = true;
            rb.useGravity = true;
        }
    }
}
