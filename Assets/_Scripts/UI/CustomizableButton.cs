using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CustomizableButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [Header("Button Identification")]
    public string buttonId;
    public string buttonDisplayName;
    
    [Header("Customization Settings")]
    public bool allowRepositioning = true;
    public bool allowResizing = true;
    public bool showInEditMode = true;
    
    [Header("Visual Settings")]
    public Color editModeBorderColor = Color.yellow;
    public Color editModeBackgroundColor = new Color(1f, 1f, 0f, 0.3f);
    public float borderWidth = 3f;
    
    [Header("Default Values")]
    public Vector2 defaultAnchoredPosition;
    public Vector2 defaultSizeDelta;
    
    private ButtonSettingsData buttonData;
    private RectTransform rectTransform;
    private Image buttonImage;
    private Button buttonComponent;
    private HoldableButton holdableButton;
    
    // Visual feedback components
    private Image borderImage;
    private Image backgroundImage;
    private GameObject editModeIndicator;
    
    // State tracking
    private bool isEditMode = false;
    private float currentScale = 1f;
    
    public string ButtonId => buttonId;
    public string ButtonDisplayName => string.IsNullOrEmpty(buttonDisplayName) ? buttonId : buttonDisplayName;
    public float CurrentScale => currentScale;
    
    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        buttonImage = GetComponent<Image>();
        buttonComponent = GetComponent<Button>();
        holdableButton = GetComponent<HoldableButton>();
        
        // Generate button ID if not set
        if (string.IsNullOrEmpty(buttonId))
        {
            buttonId = gameObject.name;
        }
        
        // Store default values if not set
        if (defaultSizeDelta == Vector2.zero)
        {
            defaultSizeDelta = rectTransform.sizeDelta;
        }
        
        if (defaultAnchoredPosition == Vector2.zero)
        {
            defaultAnchoredPosition = rectTransform.anchoredPosition;
        }
        
        // Initialize button data
        buttonData = new ButtonSettingsData(buttonId, defaultAnchoredPosition, defaultSizeDelta);
        
        // Register with ControlCustomizer
        if (ControlCustomizer.Instance != null)
        {
            ControlCustomizer.Instance.RegisterButton(this);
        }
    }
    
    private void Start()
    {
        LoadSettings();
        CreateEditModeIndicators();
    }
    
    private void OnDestroy()
    {
        if (ControlCustomizer.Instance != null)
        {
            ControlCustomizer.Instance.UnregisterButton(this);
        }
    }
    
    private void CreateEditModeIndicators()
    {
        // Create border indicator
        GameObject borderObj = new GameObject("EditModeBorder");
        borderObj.transform.SetParent(transform, false);
        borderObj.SetActive(false);
        
        borderImage = borderObj.AddComponent<Image>();
        borderImage.color = editModeBorderColor;
        borderImage.sprite = CreateBorderSprite();
        borderImage.type = Image.Type.Sliced;
        
        RectTransform borderRect = borderObj.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = new Vector2(-borderWidth, -borderWidth);
        borderRect.offsetMax = new Vector2(borderWidth, borderWidth);
        
        // Create background indicator
        GameObject bgObj = new GameObject("EditModeBackground");
        bgObj.transform.SetParent(transform, false);
        bgObj.SetActive(false);
        
        backgroundImage = bgObj.AddComponent<Image>();
        backgroundImage.color = editModeBackgroundColor;
        
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        
        editModeIndicator = new GameObject("EditModeIndicator");
        editModeIndicator.transform.SetParent(transform, false);
        editModeIndicator.SetActive(false);
        
        // Add resize handles (optional visual enhancement)
        CreateResizeHandles();
    }
    
    private void CreateResizeHandles()
    {
        // Create corner handles for resize indication
        string[] handleNames = { "TopLeft", "TopRight", "BottomLeft", "BottomRight" };
        Vector2[] anchorPositions = { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        
        for (int i = 0; i < handleNames.Length; i++)
        {
            GameObject handle = new GameObject($"ResizeHandle_{handleNames[i]}");
            handle.transform.SetParent(editModeIndicator.transform, false);
            
            Image handleImage = handle.AddComponent<Image>();
            handleImage.color = Color.white;
            
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = anchorPositions[i];
            handleRect.anchorMax = anchorPositions[i];
            handleRect.sizeDelta = Vector2.one * 20f;
            handleRect.anchoredPosition = Vector2.zero;
        }
    }
    
    private Sprite CreateBorderSprite()
    {
        // Create a simple border sprite
        Texture2D texture = new Texture2D(8, 8);
        Color[] pixels = new Color[64];
        
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }
        
        // Set border pixels
        for (int i = 0; i < 8; i++)
        {
            pixels[i] = editModeBorderColor; // Top
            pixels[i + 56] = editModeBorderColor; // Bottom
            pixels[i * 8] = editModeBorderColor; // Left
            pixels[i * 8 + 7] = editModeBorderColor; // Right
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.one * 0.5f);
    }
    
    public void SetEditMode(bool enabled)
    {
        isEditMode = enabled;
        
        // Show/hide edit mode indicators
        if (borderImage != null) borderImage.gameObject.SetActive(enabled);
        if (backgroundImage != null) backgroundImage.gameObject.SetActive(enabled);
        if (editModeIndicator != null) editModeIndicator.SetActive(enabled);
        
        // Enable/disable button interaction in edit mode
        if (buttonComponent != null)
        {
            buttonComponent.interactable = !enabled;
        }
        
        // Update visual appearance
        UpdateEditModeVisuals(enabled);
    }
    
    private void UpdateEditModeVisuals(bool editMode)
    {
        if (buttonImage != null)
        {
            Color originalColor = buttonImage.color;
            if (editMode)
            {
                // Slightly tint the button in edit mode
                buttonImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, originalColor.a * 0.8f);
            }
            else
            {
                // Restore original color
                buttonImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1f);
            }
        }
    }
    
    public void UpdateButtonData()
    {
        if (buttonData != null && rectTransform != null)
        {
            buttonData.anchoredPosition = rectTransform.anchoredPosition;
            buttonData.sizeDelta = rectTransform.sizeDelta;
            buttonData.scale = currentScale;
        }
    }
    
    public void SaveSettings()
    {
        UpdateButtonData();
        if (buttonData != null)
        {
            buttonData.SaveToPlayerPrefs();
        }
    }
    
    public void LoadSettings()
    {
        if (buttonData != null && rectTransform != null)
        {
            buttonData.LoadFromPlayerPrefs(defaultAnchoredPosition, defaultSizeDelta);
            
            // Apply loaded settings
            rectTransform.anchoredPosition = buttonData.anchoredPosition;
            rectTransform.sizeDelta = buttonData.sizeDelta;
            currentScale = buttonData.scale;
            
            // Apply visibility
            gameObject.SetActive(buttonData.isVisible);
        }
    }
    
    public void ResetToDefaults()
    {
        if (buttonData != null && rectTransform != null)
        {
            buttonData.ResetToDefaults(defaultAnchoredPosition, defaultSizeDelta);
            
            // Apply default settings
            rectTransform.anchoredPosition = defaultAnchoredPosition;
            rectTransform.sizeDelta = defaultSizeDelta;
            currentScale = 1f;
            
            gameObject.SetActive(true);
        }
    }
    
    public void SetScale(float scale)
    {
        currentScale = Mathf.Clamp(scale, 0.5f, 2f);
    }
    
    public Vector2 GetOriginalSize()
    {
        return defaultSizeDelta;
    }
    
    public bool IsVisible()
    {
        return gameObject.activeSelf;
    }
    
    public void SetVisibility(bool visible)
    {
        gameObject.SetActive(visible);
        if (buttonData != null)
        {
            buttonData.isVisible = visible;
        }
    }
    
    // Button interaction methods (pass-through to original button functionality)
    public void OnPointerDown(PointerEventData eventData)
    {
        if (isEditMode) return;
        
        // Pass through to HoldableButton if it exists
        if (holdableButton != null)
        {
            holdableButton.OnPointerDown(eventData);
        }
    }
    
    public void OnPointerUp(PointerEventData eventData)
    {
        if (isEditMode) return;
        
        // Pass through to HoldableButton if it exists
        if (holdableButton != null)
        {
            holdableButton.OnPointerUp(eventData);
        }
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        if (isEditMode) return;
        
        // Handle any drag functionality if needed
        // Usually buttons don't have drag behavior, but this is here if needed
    }
    
    // Additional helper methods
    public void SetButtonId(string newId)
    {
        buttonId = newId;
        if (buttonData != null)
        {
            buttonData.buttonId = newId;
        }
    }
    
    public ButtonSettingsData GetButtonData()
    {
        UpdateButtonData();
        return buttonData;
    }
    
    public void HighlightButton(bool highlight)
    {
        if (borderImage != null)
        {
            borderImage.color = highlight ? Color.green : editModeBorderColor;
        }
    }
}
