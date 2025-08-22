using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InkstallCoin : MonoBehaviour
{
    private AudioSource audioSource;

    [SerializeField] private AudioClip collectSound;    
    
    private void OnEnable()
    {
        audioSource = GetComponent<AudioSource>();
        StartCoroutine(DestroyCoin());
    }

    private IEnumerator DestroyCoin()
    {
        yield return new WaitForSeconds(3f);
        audioSource.PlayOneShot(collectSound);
        yield return new WaitForSeconds(2f);
        Destroy(gameObject);
    }
}
