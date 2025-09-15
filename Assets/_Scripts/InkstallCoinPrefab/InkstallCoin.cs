using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InkstallCoin : MonoBehaviour
{
    private AudioSource audioSource;

    [SerializeField] private AudioClip collectSound;    
    
    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        StartCoroutine(InkCoinBehaviour());
    }

    private IEnumerator InkCoinBehaviour()
    {
        audioSource.PlayOneShot(collectSound);
        yield return new WaitForSeconds(1.5f);
        Destroy(gameObject);
    }


}
