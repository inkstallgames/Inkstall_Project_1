using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class ControlsSettingsPanel : MonoBehaviour
{
    [Header("UI References")]
    public GameObject settingsPanel;
    public Button editModeToggleButton;
    public TextMeshProUGUI editModeButtonText;
    public Button resetAllButton;
    public Button saveButton;
    public Button closeButton;
    
    [Header("Button Settings")]
    public Transform buttonSettingsContainer;
    public GameObject buttonSettingPrefab;
    public ScrollRect buttonSettingsScrollRect;
    
    [Header("Global Settings")]
    public Slider globalScaleSlider;
    public TextMeshProUGUI globalScaleValueText;
    public Button toggleAllVisibilityButton;
    public TextMeshProUGUI toggleAllVisibilityText;
    
    [Header("Edit Mode Settings")]
    public Color editModeActiveColor = Color.green;
    public Color editModeInactiveColor = Color.red;
    
    private List<ButtonSettingEntry> buttonSettingEntries = new List<ButtonSettingEntry>();
    private bool isInitialized = false;
    
    private void Start()
    {
        InitializePanel();
        CreateButtonSettingEntries();
        RefreshButtonList();
        
        isInitialized = true;
    }
    
    private void InitializePanel()
    {
        // Set up button listeners
        if (editModeToggleButton != null)
        {
            editModeToggleButton.onClick.AddListener(ToggleEditMode);
        }
        
        if (resetAllButton != null)
        {
            resetAllButton.onClick.AddListener(ResetAllButtons);
        }
        
        if (saveButton != null)
        {
            saveButton.onClick.AddListener(SaveAndClose);
        }
        
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePanel);
        }
        
        if (globalScaleSlider != null)
        {
            globalScaleSlider.onValueChanged.AddListener(OnGlobalScaleChanged);
            globalScaleSlider.minValue = 0.5f;
            globalScaleSlider.maxValue = 2f;
            globalScaleSlider.value = 1f;
        }
        
        if (toggleAllVisibilityButton != null)
        {
            toggleAllVisibilityButton.onClick.AddListener(ToggleAllVisibility);
        }
        
        // Initially hide the panel
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
        
        UpdateEditModeUI();
    }
    
    private void CreateButtonSettingEntries()
    {
        if (buttonSettingPrefab == null || buttonSettingsContainer == null)
        {
            Debug.LogWarning("Button setting prefab or container not assigned!");
            return;
        }
        
        // Clear existing entries
        foreach (Transform child in buttonSettingsContainer)
        {
            Destroy(child.gameObject);
        }
        buttonSettingEntries.Clear();
        
        // Create entries for all customizable buttons
        List<CustomizableButton> buttons = ControlCustomizer.Instance?.GetAllButtons();
        if (buttons == null) return;
        
        foreach (var button in buttons)
        {
            if (button == null || !button.showInEditMode) continue;
            
            GameObject entryObj = Instantiate(buttonSettingPrefab, buttonSettingsContainer);
            ButtonSettingEntry entry = entryObj.GetComponent<ButtonSettingEntry>();
            
            if (entry != null)
            {
                entry.Initialize(button);
                entry.OnSettingsChanged += OnButtonSettingsChanged;
                buttonSettingEntries.Add(entry);
            }
        }
    }
    
    private void RefreshButtonList()
    {
        // Update all button entries with current values
        foreach (var entry in buttonSettingEntries)
        {
            if (entry != null && entry.TargetButton != null)
            {
                entry.RefreshValues();
            }
        }
    }
    
    public void ShowPanel()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            RefreshButtonList();
            UpdateEditModeUI();
        }
    }
    
    public void ClosePanel()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
        
        // Exit edit mode when closing
        if (ControlCustomizer.Instance != null && ControlCustomizer.Instance.isEditMode)
        {
            ControlCustomizer.Instance.ToggleEditMode();
        }
    }
    
    private void ToggleEditMode()
    {
        if (ControlCustomizer.Instance != null)
        {
            ControlCustomizer.Instance.ToggleEditMode();
            UpdateEditModeUI();
        }
    }
    
    private void UpdateEditModeUI()
    {
        bool isEditMode = ControlCustomizer.Instance?.isEditMode ?? false;
        
        if (editModeButtonText != null)
        {
            editModeButtonText.text = isEditMode ? "Exit Edit Mode" : "Enter Edit Mode";
        }
        
        if (editModeToggleButton != null)
        {
            ColorBlock colors = editModeToggleButton.colors;
            colors.normalColor = isEditMode ? editModeActiveColor : editModeInactiveColor;
            editModeToggleButton.colors = colors;
        }
        
        // Update button setting entries
        foreach (var entry in buttonSettingEntries)
        {
            if (entry != null)
            {
                entry.SetEditMode(isEditMode);
            }
        }
    }
    
    private void ResetAllButtons()
    {
        if (ControlCustomizer.Instance != null)
        {
            ControlCustomizer.Instance.ResetAllButtonsToDefaults();
            RefreshButtonList();
            
            // Show confirmation
            Debug.Log("All buttons reset to default positions and sizes");
        }
    }
    
    private void SaveAndClose()
    {
        if (ControlCustomizer.Instance != null)
        {
            ControlCustomizer.Instance.SaveAllButtonSettings();
        }
        
        ClosePanel();
    }
    
    private void OnGlobalScaleChanged(float value)
    {
        if (globalScaleValueText != null)
        {
            globalScaleValueText.text = value.ToString("F2") + "x";
        }
        
        // Apply global scale to all buttons
        List<CustomizableButton> buttons = ControlCustomizer.Instance?.GetAllButtons();
        if (buttons != null)
        {
            foreach (var button in buttons)
            {
                if (button != null)
                {
                    RectTransform rect = button.GetComponent<RectTransform>();
                    Vector2 originalSize = button.GetOriginalSize();
                    rect.sizeDelta = originalSize * value;
                    button.SetScale(value);
                }
            }
        }
    }
    
    private void ToggleAllVisibility()
    {
        List<CustomizableButton> buttons = ControlCustomizer.Instance?.GetAllButtons();
        if (buttons == null) return;
        
        // Check if any buttons are hidden
        bool anyHidden = buttons.Any(b => b != null && !b.IsVisible());
        
        // Toggle visibility
        bool newVisibility = anyHidden;
        
        foreach (var button in buttons)
        {
            if (button != null)
            {
                button.SetVisibility(newVisibility);
            }
        }
        
        if (toggleAllVisibilityText != null)
        {
            toggleAllVisibilityText.text = newVisibility ? "Hide All" : "Show All";
        }
        
        RefreshButtonList();
    }
    
    private void OnButtonSettingsChanged(CustomizableButton button)
    {
        // This is called when individual button settings are changed
        // You can add validation or additional logic here
    }
    
    private void Update()
    {
        // Update edit mode UI if it changes externally
        if (isInitialized && ControlCustomizer.Instance != null)
        {
            bool currentEditMode = ControlCustomizer.Instance.isEditMode;
            
            // Update UI if edit mode state changed
            if (editModeButtonText != null)
            {
                string expectedText = currentEditMode ? "Exit Edit Mode" : "Enter Edit Mode";
                if (editModeButtonText.text != expectedText)
                {
                    UpdateEditModeUI();
                }
            }
        }
    }
    
    // Public methods for external access
    public void SetEditModeFromExternal(bool enabled)
    {
        if (ControlCustomizer.Instance != null && ControlCustomizer.Instance.isEditMode != enabled)
        {
            ControlCustomizer.Instance.SetEditMode(enabled);
            UpdateEditModeUI();
        }
    }
    
    public bool IsPanelVisible()
    {
        return settingsPanel != null && settingsPanel.activeSelf;
    }
    
    public void RefreshPanel()
    {
        if (IsPanelVisible())
        {
            RefreshButtonList();
            UpdateEditModeUI();
        }
    }
}

// Helper class for individual button setting entries
public class ButtonSettingEntry : MonoBehaviour
{
    [Header("UI Components")]
    public TextMeshProUGUI buttonNameText;
    public Slider scaleSlider;
    public TextMeshProUGUI scaleValueText;
    public Toggle visibilityToggle;
    public Button highlightButton;
    public Button resetButton;
    
    private CustomizableButton targetButton;
    public CustomizableButton TargetButton => targetButton;
    
    public System.Action<CustomizableButton> OnSettingsChanged;
    
    public void Initialize(CustomizableButton button)
    {
        targetButton = button;
        
        // Set up UI listeners
        if (scaleSlider != null)
        {
            scaleSlider.onValueChanged.AddListener(OnScaleChanged);
            scaleSlider.minValue = 0.5f;
            scaleSlider.maxValue = 2f;
        }
        
        if (visibilityToggle != null)
        {
            visibilityToggle.onValueChanged.AddListener(OnVisibilityChanged);
        }
        
        if (highlightButton != null)
        {
            highlightButton.onClick.AddListener(HighlightButton);
        }
        
        if (resetButton != null)
        {
            resetButton.onClick.AddListener(ResetButton);
        }
        
        RefreshValues();
    }
    
    public void RefreshValues()
    {
        if (targetButton == null) return;
        
        // Update button name
        if (buttonNameText != null)
        {
            buttonNameText.text = targetButton.ButtonDisplayName;
        }
        
        // Update scale
        if (scaleSlider != null)
        {
            scaleSlider.value = targetButton.CurrentScale;
        }
        
        if (scaleValueText != null)
        {
            scaleValueText.text = targetButton.CurrentScale.ToString("F2") + "x";
        }
        
        // Update visibility
        if (visibilityToggle != null)
        {
            visibilityToggle.isOn = targetButton.IsVisible();
        }
    }
    
    public void SetEditMode(bool editMode)
    {
        // Enable/disable controls based on edit mode
        if (scaleSlider != null) scaleSlider.interactable = editMode;
        if (visibilityToggle != null) visibilityToggle.interactable = editMode;
        if (highlightButton != null) highlightButton.interactable = editMode;
        if (resetButton != null) resetButton.interactable = editMode;
    }
    
    private void OnScaleChanged(float value)
    {
        if (targetButton == null) return;
        
        // Update scale value text
        if (scaleValueText != null)
        {
            scaleValueText.text = value.ToString("F2") + "x";
        }
        
        // Apply scale to button
        RectTransform rect = targetButton.GetComponent<RectTransform>();
        Vector2 originalSize = targetButton.GetOriginalSize();
        rect.sizeDelta = originalSize * value;
        targetButton.SetScale(value);
        
        OnSettingsChanged?.Invoke(targetButton);
    }
    
    private void OnVisibilityChanged(bool isVisible)
    {
        if (targetButton != null)
        {
            targetButton.SetVisibility(isVisible);
            OnSettingsChanged?.Invoke(targetButton);
        }
    }
    
    private void HighlightButton()
    {
        if (targetButton != null)
        {
            targetButton.HighlightButton(true);
            
            // Remove highlight after 2 seconds
            Invoke(nameof(RemoveHighlight), 2f);
        }
    }
    
    private void RemoveHighlight()
    {
        if (targetButton != null)
        {
            targetButton.HighlightButton(false);
        }
    }
    
    private void ResetButton()
    {
        if (targetButton != null)
        {
            targetButton.ResetToDefaults();
            RefreshValues();
            OnSettingsChanged?.Invoke(targetButton);
        }
    }
}
