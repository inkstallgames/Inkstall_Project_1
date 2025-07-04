using UnityEngine;

public class CollectibleKey : MonoBehaviour
{
    [Header("Key Settings")]
    [SerializeField] private int keyAmount = 1;
    [SerializeField] private bool rotateKey = true;
    [SerializeField] private float rotationSpeed = 50f;
    [SerializeField] private float bobSpeed = 1f;
    [SerializeField] private float bobHeight = 0.2f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip pickupSound;
    [Range(0f, 1f)] [SerializeField] private float soundVolume = 0.7f;
    
    private Vector3 startPosition;
    private bool isCollected = false;
    
    private void Start()
    {
        startPosition = transform.position;
    }
    
    private void Update()
    {
        if (!isCollected)
        {
            // Rotate the key
            if (rotateKey)
            {
                transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
            }
            
            // Bob the key up and down
            float newY = startPosition.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Check if the player entered the trigger
        if (!isCollected && other.CompareTag("Player"))
        {
            CollectKey();
        }
    }
    
    private void CollectKey()
    {
        isCollected = true;
        
        // Play pickup sound
        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, soundVolume);
        }
        
        // Add keys to the player's inventory
        if (KeySystem.Instance != null)
        {
            KeySystem.Instance.AddKeys(keyAmount);
        }
        else
        {
            Debug.LogError("KeySystem instance not found! Make sure it exists in the scene.");
        }
        
        // Hide the key
        gameObject.SetActive(false);
    }
}
