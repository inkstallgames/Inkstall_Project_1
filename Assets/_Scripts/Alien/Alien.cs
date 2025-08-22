using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Alien : MonoBehaviour
{
    [SerializeField] private ParticleSystem disappearEffectPrefab;
    [SerializeField] private AudioClip dissappearSound;
    [SerializeField] private GameObject coinPrefab;
    [SerializeField] private AudioClip alienDyingSound;

    AudioSource audioSource;

    // Start is called before the first frame update
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    void OnEnable()
    {
        audioSource.PlayOneShot(alienDyingSound);
        StartCoroutine(DestroyAlien());
    }

    public IEnumerator DestroyAlien()
    {
        yield return new WaitForSeconds(3f);
        ParticleSystem effect = Instantiate(disappearEffectPrefab, transform.position, Quaternion.identity);
        effect.Play();
        audioSource.PlayOneShot(dissappearSound);
        Instantiate(coinPrefab, transform.position, Quaternion.identity);
        yield return new WaitForSeconds(0.1f);
        gameObject.SetActive(false);
    }   
}
 