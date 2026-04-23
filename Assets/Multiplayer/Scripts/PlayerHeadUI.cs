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
    
    [Tooltip("Maximum distance where UI is visible")]
    public float maxVisibleDistance = 50f;
    
    [Tooltip("Distance where health bar hides (name only)")]
    public float nameOnlyDistance = 20f;
    
    [Tooltip("Should UI always face camera (billboard effect)")]
    public bool faceCamera = true;
    
    [Tooltip("Show UI only for enemies (hide for local player)")]
    public bool showOnlyForEnemies = false;
    
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
            
            // Industry standard scale for world space UI
            uiCanvas.transform.localScale = Vector3.one * 0.001f;
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
        
        // Distance-based visibility (industry standard LOD)
        float distance = Vector3.Distance(transform.position, mainCamera.transform.position);
        UpdateVisibility(distance);
        
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
    
    void UpdateVisibility(float distance)
    {
        // Hide if too far away (performance optimization)
        if (distance > maxVisibleDistance)
        {
            uiCanvas.gameObject.SetActive(false);
            return;
        }
        
        uiCanvas.gameObject.SetActive(true);
        
        // LOD system: hide health bar at distance
        if (distance > nameOnlyDistance)
        {
            healthBar.gameObject.SetActive(false);
        }
        else
        {
            healthBar.gameObject.SetActive(true);
        }
        
        // Hide for local player if specified
        if (showOnlyForEnemies && playerData.Object.HasInputAuthority)
        {
            uiCanvas.gameObject.SetActive(false);
        }
    }
    
    void UpdateUI()
    {
        if (playerData == null) return;
        
        // Update player name
        if (playerNameText != null)
        {
            playerNameText.text = playerData.PlayerName;
            
            // Team colors (industry standard)
            playerNameText.color = GetTeamColor(playerData.TeamId);
        }
        
        // Update health bar
        if (healthBar != null)
        {
            healthBar.value = playerData.Health / 100f;
            
            // Health bar color based on health level (industry standard)
            Image fillImage = healthBar.fillRect.GetComponent<Image>();
            if (fillImage != null)
            {
                if (playerData.Health > 60f)
                    fillImage.color = Color.green;
                else if (playerData.Health > 30f)
                    fillImage.color = Color.yellow;
                else
                    fillImage.color = Color.red;
            }
        }
    }
    
    Color GetTeamColor(int teamId)
    {
        switch (teamId)
        {
            case 0: return Color.blue;    // Team A
            case 1: return Color.red;     // Team B
            default: return Color.white;  // No team
        }
    }
}
