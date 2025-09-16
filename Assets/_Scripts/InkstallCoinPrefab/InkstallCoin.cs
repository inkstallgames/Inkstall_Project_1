using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class InkstallCoin : MonoBehaviour
{
    private AudioSource audioSource;

    [SerializeField] private AudioClip collectSound;
    [SerializeField] private string collectTextTag = "CollectPointsTextTag";   // Tag to find the TextMeshPro text in the scene
    private TextMeshProUGUI collectText;
    
    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        
        // Find the TextMeshPro text in the scene using the tag
        GameObject textObj = GameObject.FindGameObjectWithTag(collectTextTag);
        if (textObj != null)
        {
            collectText = textObj.GetComponent<TextMeshProUGUI>();
            if (collectText != null)
            {
                collectText.gameObject.SetActive(false);
            }
        }
        
        StartCoroutine(InkCoinBehaviour());
    }

    private IEnumerator InkCoinBehaviour()
    {
        audioSource.PlayOneShot(collectSound);
        
        // Show the collect text if it's assigned
        if (collectText != null)
        {
            collectText.gameObject.SetActive(true);
            yield return new WaitForSeconds(1.5f);
            collectText.gameObject.SetActive(false);
        }
        else
        {
            yield return new WaitForSeconds(1.5f);
        }   
        Destroy(gameObject);
    }
}
