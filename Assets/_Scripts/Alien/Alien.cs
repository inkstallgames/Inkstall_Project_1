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
        if (AudioManager.Instance != null)
        {
            audioSource.volume = AudioManager.Instance.sfxVolume;
        }
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
        if (coinPrefab != null)
{
    Instantiate(coinPrefab, transform.position, Quaternion.identity);
}
else
{
    Debug.LogError("Coin Prefab is not assigned in the Inspector!");
}

        // Points Added UI
        
        // Now disable the GameObject
        gameObject.SetActive(false);
    }   
}