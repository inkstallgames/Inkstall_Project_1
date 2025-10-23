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
        if (coinPrefab != null)
        {
            Debug.Log($"Attempting to instantiate coin at position: {transform.position}");
            GameObject coin = Instantiate(coinPrefab, transform.position, Quaternion.identity);
            if (coin != null)
            {
                Debug.Log($"Successfully instantiated coin at: {coin.transform.position}");
                Debug.Log($"Coin active in hierarchy: {coin.activeInHierarchy}");
                Debug.Log($"Coin name: {coin.name}");
            }
            else
            {
                Debug.LogError("Failed to instantiate coin (returned null)");
            }
        }
        else
        {
            Debug.LogError("Coin Prefab is not assigned in the Inspector!");
        }

        // Points Added UI
        
        // Add coins when alien is defeated
        if (CoinsManager.Instance != null)
        {
            CoinsManager.Instance.AddCoins(20, "Alien Defeated");
        }
        
        // Now disable the GameObject
        gameObject.SetActive(false);
    }   
}