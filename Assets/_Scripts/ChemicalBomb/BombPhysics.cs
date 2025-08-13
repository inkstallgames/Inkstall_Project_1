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
        if (collision.gameObject.CompareTag("Alien"))
        {
            collision.gameObject.SetActive(false);
            this.gameObject.SetActive(false);
        }
        if (!hasCollided)
        {
            hasCollided = true;
            rb.useGravity = true;
        }
    }
}
