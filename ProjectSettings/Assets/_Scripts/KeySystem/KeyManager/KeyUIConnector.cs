using UnityEngine;
using TMPro;

/// <summary>
/// Add this component to each scene to connect the scene's key UI to the persistent KeyManager
/// </summary>
public class KeyUIConnector : MonoBehaviour
{
    [Header("Scene UI References")]
    [SerializeField] private TextMeshProUGUI keyCountText;

    private void Start()
    {
        ConnectUI();
    }

    private void OnEnable()
    {
        // Delay to ensure KeyManager is ready
        Invoke(nameof(ConnectUI), 0.1f);
    }

    private void ConnectUI()
    {
        // Connect this scene's UI to the persistent KeyManager
        if (KeyManager.Instance != null && keyCountText != null)
        {
            KeyManager.Instance.SetKeyTextUI(keyCountText);
            Debug.Log($"[KeyUIConnector] Connected key UI in scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            Debug.Log($"[KeyUIConnector] Current keys: {KeyManager.Instance.GetCurrentKeyCount()}");
        }
        else
        {
            if (KeyManager.Instance == null)
            {
                Debug.LogError("[KeyUIConnector] KeyManager.Instance is NULL! Make sure KeyManager exists in the scene.");
            }
            if (keyCountText == null)
            {
                Debug.LogError("[KeyUIConnector] keyCountText is not assigned! Please assign it in the Inspector.");
            }
        }
    }
}
