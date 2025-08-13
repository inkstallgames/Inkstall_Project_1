using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Alien : MonoBehaviour
{
    [SerializeField] private GameObject deathEffect;
    [SerializeField] private GameObject alienPrefab;
    
    public void PlayDeathEffect()
    {
        Destroy(gameObject);
        Instantiate(deathEffect, transform.position, transform.rotation);
        Instantiate(alienPrefab, transform.position, transform.rotation);
    }
}
