using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class InkstallCoin : MonoBehaviour
{
    private AudioSource audioSource;

    [SerializeField] private AudioClip collectSound;
    private TextMeshProUGUI collectText;
    
    private void OnEnable()
    {
        audioSource = GetComponent<AudioSource>();
        
        // Find the TextMeshPro text in the scene using the tag
        
        GameObject textObj = GameObject.FindGameObjectWithTag("CollectPointsTextTag");
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
            yield return new WaitForSeconds(2f);
            collectText.gameObject.SetActive(false); 
        }
        yield return new WaitForSeconds(1.5f);
        gameObject.SetActive(false);
    }
}
