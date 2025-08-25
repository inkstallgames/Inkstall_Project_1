using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Alien : MonoBehaviour
{
    [SerializeField] private ParticleSystem disappearEffectPrefab;
    [SerializeField] private AudioClip alienDyingSound;
    [SerializeField] private AudioClip alienDissappearedSound;
    [SerializeField] private GameObject coinPrefab;

    private AudioSource audioSource;

    void OnEnable()
    {
        audioSource = GetComponent<AudioSource>();
        StartCoroutine(AlienBehaviour());
    }

    public IEnumerator AlienBehaviour()
    {
        // Get the Transformed into Alien particle Effect In Children and play it
        ParticleSystem transformedIntoAlienEffect = GetComponentInChildren<ParticleSystem>();
        transformedIntoAlienEffect.Play();
        audioSource.PlayOneShot(alienDyingSound);
        
        yield return new WaitForSeconds(2f);
        ParticleSystem effect = Instantiate(disappearEffectPrefab, transform.position, Quaternion.identity);
        effect.Play();
        audioSource.PlayOneShot(alienDissappearedSound);
        Instantiate(coinPrefab, this.gameObject.transform.position, Quaternion.identity);
        // Now disable the GameObject
        gameObject.SetActive(false);
    }   
}