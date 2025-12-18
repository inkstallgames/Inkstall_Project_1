using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    
    [Header("UI References")]
    [SerializeField] private Button watchAdButton;  // Assign this in the Unity Inspector
    
    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    
    private void Start()
    {
        // Subscribe to key count changes
        if (KeyManager.Instance != null)
        {
            KeyManager.Instance.OnKeysChanged += UpdateAdButtonState;
        }

        // Initial update of the ad button state
        UpdateAdButtonState();
    }
    
    private void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        if (KeyManager.Instance != null)
        {
            KeyManager.Instance.OnKeysChanged -= UpdateAdButtonState;
        }
    }
    
    // This method will be called whenever the keys count changes
    private void UpdateAdButtonState()
    {
        if (watchAdButton != null && KeyManager.Instance != null)
        {
            // Deactivate the watch ad button if keys count is maxKeys or more
            int currentKeys = KeyManager.Instance.GetCurrentKeyCount();
            int maxKeys = KeyManager.Instance.maxKeys;
            
            bool shouldBeActive = currentKeys < maxKeys;
            
            Debug.Log($"[UIManager] UpdateAdButtonState: Keys={currentKeys}, MaxKeys={maxKeys}, Active={shouldBeActive}");
            
            watchAdButton.gameObject.SetActive(shouldBeActive);
        }
        else
        {
            Debug.LogWarning("[UIManager] UpdateAdButtonState: Missing references (watchAdButton or KeyManager)");
        }
    }
    
    // Call this method from other scripts if you need to manually update the button state
    public void RefreshAdButtonState()
    {
        UpdateAdButtonState();
    }
}
