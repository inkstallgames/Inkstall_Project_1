using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class InkstallCoin : MonoBehaviour
{
    private AudioSource audioSource;

    [SerializeField] private AudioClip collectSound; 
    [SerializeField] private TextMeshPro textMeshPro;   
    
    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        textMeshPro = GetComponent<TextMeshPro>();
        StartCoroutine(InkCoinBehaviour());
    }

    private IEnumerator InkCoinBehaviour()
    {
        audioSource.PlayOneShot(collectSound);
        yield return new WaitForSeconds(1.5f);
        textMeshPro.text = "+20";
        Destroy(gameObject);
    }


}
