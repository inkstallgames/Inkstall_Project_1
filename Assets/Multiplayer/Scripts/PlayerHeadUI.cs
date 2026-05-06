using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Basic player head UI system
/// Shows player names and health bars above their heads
/// Local player sees others but not themselves
/// </summary>
public class PlayerHeadUI : MonoBehaviour
{
    [Header("UI Elements")]
    public Canvas uiCanvas;
    public TextMeshProUGUI playerNameText;
    public Slider healthBar;
    
    [Header("Settings")]
    [Tooltip("Offset above player head")]
    public Vector3 headOffset = new Vector3(0, 2.2f, 0);
    
    [Tooltip("Should UI always face camera (billboard effect)")]
    public bool faceCamera = true;
    
    // References
    private PlayerNetworkData playerData;
    private Camera mainCamera;
    
    void Start()
    {
        // Get references
        playerData = GetComponentInParent<PlayerNetworkData>();
        mainCamera = Camera.main;
        
        // Configure canvas for world space
        if (uiCanvas != null)
        {
            uiCanvas.renderMode = RenderMode.WorldSpace;
            uiCanvas.worldCamera = mainCamera;
            uiCanvas.transform.localScale = Vector3.one * 0.1f;
        }
        
        // Validate components
        if (playerNameText == null) Debug.LogError("[PlayerHeadUI] PlayerNameText not assigned!");
        if (healthBar == null) Debug.LogError("[PlayerHeadUI] HealthBar not assigned!");
        if (uiCanvas == null) Debug.LogError("[PlayerHeadUI] UICanvas not assigned!");
    }
    
    void LateUpdate()
    {
        if (playerData == null || mainCamera == null) return;
        
        // Position and rotation
        UpdateTransform();
        
        // Update UI data
        UpdateUI();
        
        // Visibility check
        UpdateVisibility();
    }
    
    void UpdateTransform()
    {
        // Position above player head
        transform.position = transform.parent.position + headOffset;
        
        // Billboard effect - face camera
        if (faceCamera && mainCamera != null)
        {
            transform.rotation = mainCamera.transform.rotation;
        }
    }
    
    void UpdateVisibility()
    {
        // Hide UI for local player, show for remote players
        if (playerData != null && playerData.Object.HasInputAuthority)
        {
            uiCanvas.gameObject.SetActive(false);
        }
        else
        {
            uiCanvas.gameObject.SetActive(true);
        }
    }
    
    void UpdateUI()
    {
        if (playerData == null) return;
        
        // Update player name
        if (playerNameText != null)
        {
            playerNameText.text = playerData.PlayerName;
        }
        
        // Update health bar
        if (healthBar != null)
        {
            healthBar.value = playerData.Health / 100f;
        }
    }
}
