using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class ControlCustomizer : MonoBehaviour
{
    [Header("Customization Settings")]
    public bool isEditMode = false;
    public float minButtonScale = 0.5f;
    public float maxButtonScale = 2.0f;
    public LayerMask uiLayerMask;
    
    [Header("Visual Feedback")]
    public Color editModeColor = Color.yellow;
    public Color normalModeColor = Color.white;
    
    private List<CustomizableButton> customizableButtons = new List<CustomizableButton>();
    private Canvas parentCanvas;
    private bool isDragging = false;
    private bool isResizing = false;
    private CustomizableButton selectedButton;
    private Vector2 dragOffset;
    private Vector2 initialResizeSize;
    private Vector2 initialResizePosition;
    
    public static ControlCustomizer Instance { get; private set; }
    
    private void Awake()
    {
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
        parentCanvas = GetComponentInParent<Canvas>();
        FindAllCustomizableButtons();
        LoadAllButtonSettings();
    }
    
    private void FindAllCustomizableButtons()
    {
        customizableButtons.Clear();
        CustomizableButton[] buttons = FindObjectsOfType<CustomizableButton>();
        customizableButtons.AddRange(buttons);
    }
    
    public void RegisterButton(CustomizableButton button)
    {
        if (!customizableButtons.Contains(button))
        {
            customizableButtons.Add(button);
        }
    }
    
    public void UnregisterButton(CustomizableButton button)
    {
        customizableButtons.Remove(button);
    }
    
    public void ToggleEditMode()
    {
        isEditMode = !isEditMode;
        
        foreach (var button in customizableButtons)
        {
            if (button != null)
            {
                button.SetEditMode(isEditMode);
            }
        }
        
        // Show/hide edit mode indicators
        if (isEditMode)
        {
            ShowEditModeIndicators();
        }
        else
        {
            HideEditModeIndicators();
            SaveAllButtonSettings();
        }
    }
    
    public void SetEditMode(bool enabled)
    {
        if (isEditMode != enabled)
        {
            ToggleEditMode();
        }
    }
    
    private void ShowEditModeIndicators()
    {
        // You can add visual indicators here like borders or highlights
        Debug.Log("Edit Mode Enabled - Drag buttons to reposition, pinch to resize");
    }
    
    private void HideEditModeIndicators()
    {
        // Hide visual indicators
        Debug.Log("Edit Mode Disabled - Settings saved");
    }
    
    private void Update()
    {
        if (!isEditMode) return;
        
        HandleTouchInput();
    }
    
    private void HandleTouchInput()
    {
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    HandleTouchStart(touch.position);
                    break;
                    
                case TouchPhase.Moved:
                    HandleTouchMove(touch.position);
                    break;
                    
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    HandleTouchEnd();
                    break;
            }
        }
        else if (Input.touchCount == 2)
        {
            HandlePinchResize();
        }
        
        // Handle mouse input for editor testing
        #if UNITY_EDITOR
        HandleMouseInput();
        #endif
    }
    
    #if UNITY_EDITOR
    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            HandleTouchStart(Input.mousePosition);
        }
        else if (Input.GetMouseButton(0))
        {
            HandleTouchMove(Input.mousePosition);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            HandleTouchEnd();
        }
        
        // Handle resize with keyboard in editor
        if (selectedButton != null)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                ResizeButton(selectedButton, scroll > 0 ? 1.1f : 0.9f);
            }
        }
    }
    #endif
    
    private void HandleTouchStart(Vector2 screenPosition)
    {
        // Check if we hit a button
        selectedButton = GetButtonAtPosition(screenPosition);
        
        if (selectedButton != null)
        {
            isDragging = true;
            RectTransform buttonRect = selectedButton.GetComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                buttonRect.parent as RectTransform,
                screenPosition,
                parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera,
                out Vector2 localPoint
            );
            dragOffset = localPoint - buttonRect.anchoredPosition;
        }
    }
    
    private void HandleTouchMove(Vector2 screenPosition)
    {
        if (isDragging && selectedButton != null)
        {
            RectTransform buttonRect = selectedButton.GetComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                buttonRect.parent as RectTransform,
                screenPosition,
                parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera,
                out Vector2 localPoint
            );
            
            Vector2 newPosition = localPoint - dragOffset;
            buttonRect.anchoredPosition = newPosition;
            selectedButton.UpdateButtonData();
        }
    }
    
    private void HandleTouchEnd()
    {
        isDragging = false;
        isResizing = false;
        selectedButton = null;
    }
    
    private void HandlePinchResize()
    {
        if (Input.touchCount != 2) return;
        
        Touch touch1 = Input.GetTouch(0);
        Touch touch2 = Input.GetTouch(1);
        
        // Find if both touches are on the same button
        CustomizableButton button1 = GetButtonAtPosition(touch1.position);
        CustomizableButton button2 = GetButtonAtPosition(touch2.position);
        
        if (button1 != null && button1 == button2)
        {
            selectedButton = button1;
            
            // Calculate pinch distance
            float currentDistance = Vector2.Distance(touch1.position, touch2.position);
            float previousDistance = Vector2.Distance(
                touch1.position - touch1.deltaPosition,
                touch2.position - touch2.deltaPosition
            );
            
            if (Mathf.Abs(currentDistance - previousDistance) > 1f)
            {
                float scaleFactor = currentDistance / previousDistance;
                ResizeButton(selectedButton, scaleFactor);
            }
        }
    }
    
    private CustomizableButton GetButtonAtPosition(Vector2 screenPosition)
    {
        foreach (var button in customizableButtons)
        {
            if (button == null || !button.IsVisible()) continue;
            
            RectTransform buttonRect = button.GetComponent<RectTransform>();
            if (RectTransformUtility.RectangleContainsScreenPoint(buttonRect, screenPosition))
            {
                return button;
            }
        }
        return null;
    }
    
    private void ResizeButton(CustomizableButton button, float scaleFactor)
    {
        RectTransform buttonRect = button.GetComponent<RectTransform>();
        Vector2 currentSize = buttonRect.sizeDelta;
        
        // Apply scale limits
        float newScale = button.CurrentScale * scaleFactor;
        newScale = Mathf.Clamp(newScale, minButtonScale, maxButtonScale);
        
        // Calculate new size
        Vector2 baseSize = button.GetOriginalSize();
        Vector2 newSize = baseSize * newScale;
        
        buttonRect.sizeDelta = newSize;
        button.SetScale(newScale);
        button.UpdateButtonData();
    }
    
    public void SaveAllButtonSettings()
    {
        foreach (var button in customizableButtons)
        {
            if (button != null)
            {
                button.SaveSettings();
            }
        }
        
        PlayerPrefs.Save();
        Debug.Log("All button settings saved");
    }
    
    public void LoadAllButtonSettings()
    {
        foreach (var button in customizableButtons)
        {
            if (button != null)
            {
                button.LoadSettings();
            }
        }
        
        Debug.Log("All button settings loaded");
    }
    
    public void ResetAllButtonsToDefaults()
    {
        foreach (var button in customizableButtons)
        {
            if (button != null)
            {
                button.ResetToDefaults();
            }
        }
        
        SaveAllButtonSettings();
        Debug.Log("All buttons reset to defaults");
    }
    
    public List<CustomizableButton> GetAllButtons()
    {
        return new List<CustomizableButton>(customizableButtons);
    }
}
