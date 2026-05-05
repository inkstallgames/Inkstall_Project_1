using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Industry-standard player head UI system using Canvas World Space
/// Used by professional multiplayer games like Valorant, CS:GO, Overwatch
/// Attach this to the PlayerHeadUI GameObject (parent of Canvas)
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
    
    [Tooltip("Show UI only for enemies (hide for local player)")]
    public bool showOnlyForEnemies = false;
    
    [Header("Occlusion Settings")]
    [Tooltip("Layer mask for occlusion raycast")]
    public LayerMask occlusionLayerMask = -1; // All layers
    
    [Tooltip("Raycast offset to avoid self-collision")]
    public float raycastOffset = 0.1f;
    
    [Header("Performance")]
    [Tooltip("Update interval (0 = every frame)")]
    public float updateInterval = 0f;
    
    // References
    private PlayerNetworkData playerData;
    private Camera mainCamera;
    private float lastUpdateTime;
    
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
            
            // User requested scale
            uiCanvas.transform.localScale = Vector3.one * 0.1f;
        }
        
        // Validate components
        if (playerNameText == null) Debug.LogError("[PlayerHeadUI] PlayerNameText not assigned!");
        if (healthBar == null) Debug.LogError("[PlayerHeadUI] HealthBar not assigned!");
        if (uiCanvas == null) Debug.LogError("[PlayerHeadUI] UICanvas not assigned!");
    }
    
    void LateUpdate()
    {
        // Performance: only update at specified interval
        if (updateInterval > 0f)
        {
            if (Time.time - lastUpdateTime < updateInterval) return;
            lastUpdateTime = Time.time;
        }
        
        if (playerData == null || mainCamera == null) return;
        
        // Occlusion-based visibility
        UpdateVisibility();
        
        // Position and rotation
        UpdateTransform();
        
        // Update UI data
        UpdateUI();
    }
    
    void UpdateTransform()
    {
        // Position above player head
        transform.position = transform.parent.position + headOffset;
        
        // Billboard effect - face camera (industry standard)
        if (faceCamera && mainCamera != null)
        {
            transform.rotation = mainCamera.transform.rotation;
        }
    }
    
    void UpdateVisibility()
    {
        // Show UI to ALL players (including local player)
        // TEMPORARILY DISABLED OCCLUSION CULLING FOR TESTING
        
        Debug.Log($"[PlayerHeadUI] UpdateVisibility called - Player: {playerData?.PlayerName}, HasInputAuthority: {playerData?.Object.HasInputAuthority}");
        
        if (playerData == null)
        {
            Debug.LogError("[PlayerHeadUI] PlayerData is null!");
            return;
        }
        
        if (mainCamera == null)
        {
            Debug.LogError("[PlayerHeadUI] MainCamera is null!");
            return;
        }
        
        if (uiCanvas == null)
        {
            Debug.LogError("[PlayerHeadUI] UICanvas is null!");
            return;
        }
        
        Debug.Log($"[PlayerHeadUI] UI Canvas active before check: {uiCanvas.gameObject.activeSelf}");
        Debug.Log($"[PlayerHeadUI] PlayerNameText active: {playerNameText?.gameObject.activeSelf}");
        Debug.Log($"[PlayerHeadUI] HealthBar active: {healthBar?.gameObject.activeSelf}");
        
        // TEMPORARY FIX: Always show UI for all players (no occlusion culling)
        Debug.Log("[PlayerHeadUI] Showing UI - occlusion culling disabled for testing");
        uiCanvas.gameObject.SetActive(true);
        healthBar.gameObject.SetActive(true);
    }
    
    void UpdateUI()
    {
        if (playerData == null) 
        {
            Debug.LogError("[PlayerHeadUI] PlayerData is null in UpdateUI!");
            return;
        }
        
        Debug.Log($"[PlayerHeadUI] UpdateUI - Player: {playerData.PlayerName}, Health: {playerData.Health}");
        
        // Update player name
        if (playerNameText != null)
        {
            playerNameText.text = playerData.PlayerName;
            
            // Team colors: Red for enemies, Sky Blue for teammates
            playerNameText.color = GetPlayerColor(playerData.TeamId, playerData.Object.HasInputAuthority);
            Debug.Log($"[PlayerHeadUI] Updated player name: {playerData.PlayerName}");
        }
        else
        {
            Debug.LogError("[PlayerHeadUI] PlayerNameText is null in UpdateUI!");
        }
        
        // Update health bar
        if (healthBar != null)
        {
            healthBar.value = playerData.Health / 100f;
            Debug.Log($"[PlayerHeadUI] Updated health bar value: {playerData.Health}/100");
            
            // Health bar color based on health level (industry standard)
            // SAFER: Try multiple ways to get the fill image
            Image fillImage = null;
            
            // Method 1: Try fillRect (most common)
            if (healthBar.fillRect != null)
            {
                fillImage = healthBar.fillRect.GetComponent<Image>();
            }
            
            // Method 2: Try direct component on slider
            if (fillImage == null)
            {
                fillImage = healthBar.GetComponent<Image>();
            }
            
            // Method 3: Try to find child with Image component
            if (fillImage == null)
            {
                fillImage = healthBar.GetComponentInChildren<Image>();
            }
            
            if (fillImage != null)
            {
                // Get player color (red for enemies, sky blue for teammates)
                Color playerColor = GetPlayerColor(playerData.TeamId, playerData.Object.HasInputAuthority);
                
                // Adjust health bar brightness based on health level
                if (playerData.Health > 60f)
                    fillImage.color = playerColor;  // Full health - normal team color
                else if (playerData.Health > 30f)
                    fillImage.color = Color.Lerp(playerColor, Color.yellow, 0.5f);  // Medium health - mix with yellow
                else
                    fillImage.color = Color.Lerp(playerColor, Color.red, 0.7f);  // Low health - mix with red
                Debug.Log($"[PlayerHeadUI] Updated health bar color: {fillImage.color}");
            }
            else
            {
                Debug.LogWarning("[PlayerHeadUI] Could not find health bar fill image - skipping color update");
            }
        }
        else
        {
            Debug.LogError("[PlayerHeadUI] HealthBar is null in UpdateUI!");
        }
    }
    
    Color GetPlayerColor(int teamId, bool isLocalPlayer)
    {
        // If it's the local player, show as teammate (sky blue)
        if (isLocalPlayer)
            return new Color(0.53f, 0.81f, 0.98f, 1f); // Sky Blue
        
        // For remote players, check if they're on the same team as local player
        // Note: You'll need to get local player's team ID for proper comparison
        // For now, using team-based colors
        
        if (teamId == 0)
            return new Color(0.53f, 0.81f, 0.98f, 1f); // Team 0 = Sky Blue (Teammates)
        else if (teamId == 1)
            return Color.red;  // Team 1 = Red (Enemies)
        else
            return Color.red;  // No team = Red (Enemies by default)
    }
}
