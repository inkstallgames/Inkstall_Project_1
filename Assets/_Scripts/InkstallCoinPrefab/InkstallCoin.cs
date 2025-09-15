using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class InkstallCoin : MonoBehaviour
{
    private AudioSource audioSource;
    [SerializeField] private AudioClip collectSound;    
    [SerializeField] private int points = 10; // Points this coin is worth
    [SerializeField] private float destroyDelay = 1.5f;
    [SerializeField] private GameObject floatingTextPrefab; // Assign in inspector
    [SerializeField] private Color textColor = Color.yellow;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Show points added
            ShowPointsAdded(points);
            
            // Add coins to the manager
            if (CoinsManager.Instance != null)
            {
                CoinsManager.Instance.AddCoins(1); // Assuming 1 coin per pickup
            }
            
            // Start destroy coroutine
            StartCoroutine(DestroyCoin());
        }
    }

    private void ShowPointsAdded(int points)
    {
        if (floatingTextPrefab != null)
        {
            GameObject floatingText = Instantiate(floatingTextPrefab, transform.position, Quaternion.identity);
            floatingText.transform.SetParent(FindObjectOfType<Canvas>().transform, false);
            
            // Get the FloatingText component and show the text
            var ft = floatingText.GetComponent<FloatingText>();
            if (ft != null)
            {
                ft.ShowText($"+{points}", transform.position, textColor);
            }
        }
    }

    IEnumerator DestroyCoin()
    {
        // Play collect sound
        audioSource.PlayOneShot(collectSound);
        
        // Optional: Add any collection effects here (like scaling down, fading out)
        
        // Wait for the specified delay
        yield return new WaitForSeconds(destroyDelay);
        
        // Destroy the coin
        Destroy(gameObject);
    }


}
