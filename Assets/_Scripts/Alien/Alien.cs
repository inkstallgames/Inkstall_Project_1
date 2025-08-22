using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Alien : MonoBehaviour
{
    [SerializeField] private ParticleSystem disappearEffectPrefab;
    [SerializeField] private AudioClip dissappearSound;
    [SerializeField] private GameObject coinPrefab;

    private AudioSource audioSource;

    void OnEnable()
    {
        audioSource = GetComponent<AudioSource>();
        StartCoroutine(DestroyAlien());
    }

    public IEnumerator DestroyAlien()
    {
        yield return new WaitForSeconds(3f);
        ParticleSystem effect = Instantiate(disappearEffectPrefab, transform.position, Quaternion.identity);
        effect.Play();
        audioSource.PlayOneShot(dissappearSound);
        Instantiate(coinPrefab, this.gameObject.transform.position, Quaternion.identity);
        yield return new WaitForSeconds(2f);
        coinPrefab.SetActive(false);
        yield return new WaitForSeconds(1f);
        // Now disable the GameObject
        gameObject.SetActive(false);
    }   
}