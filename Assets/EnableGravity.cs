using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnableGravity : MonoBehaviour
{
    Rigidbody rb;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void OnCollisionEnter()
    {
        if(!rb.useGravity)
        {
            rb.useGravity = true;
        }
    }
}
