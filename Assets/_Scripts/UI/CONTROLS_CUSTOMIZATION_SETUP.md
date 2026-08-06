# Mobile Controls Customization Setup Guide

## Overview
This system allows players to customize the position and size of mobile control buttons through:
- **Drag-to-reposition**: Move buttons by dragging them in edit mode
- **Pinch-to-resize**: Scale buttons using two-finger pinch gestures
- **Settings panel**: Fine-tune individual button settings
- **Persistent settings**: All customizations are saved automatically

## Installation Steps

### 1. Add Components to Existing Buttons

For each mobile button prefab (Jump_Button, Settings_Button, Joystick_Move):

1. Open the prefab in the editor
2. Add the `CustomizableButton` component
3. Configure the component:
   - **Button ID**: Unique identifier (e.g., "JumpButton", "SettingsButton", "MoveJoystick")
   - **Button Display Name**: User-friendly name shown in settings
   - **Default Values**: Set the default position and size
   - **Allow Repositioning/Resizing**: Enable customization options

### 2. Create ControlCustomizer Instance

1. Create an empty GameObject named "ControlCustomizer"
2. Add the `ControlCustomizer` component
3. Configure settings:
   - **Min/Max Button Scale**: Set size limits (0.5f - 2.0f recommended)
   - **UI Layer Mask**: Set to "UI" layer
   - **Visual Feedback Colors**: Customize edit mode colors

### 3. Create Settings Panel

1. Create a new Canvas for the settings panel
2. Add the `ControlsSettingsPanel` component
3. Create UI elements:
   - Settings panel GameObject
   - Edit mode toggle button
   - Save/Reset/Close buttons
   - Scrollable list for individual button settings
   - Global scale slider
   - Toggle all visibility button

### 4. Create Button Setting Entry Prefab

1. Create a prefab for individual button settings:
   - Button name text
   - Scale slider with value display
   - Visibility toggle
   - Highlight and reset buttons
2. Add the `ButtonSettingEntry` component
3. Assign to the `buttonSettingPrefab` field in ControlsSettingsPanel

## Integration with Existing Controls

### For Jump Button:
```csharp
// The CustomizableButton component will automatically work with
// your existing HoldableButton component, maintaining all
// current functionality while adding customization features.
```

### For Joystick:
```csharp
// Add CustomizableButton to the joystick container
// The UIVirtualJoystick functionality remains unchanged
// Only the position and size become customizable
```

### For Settings Button:
```csharp
// The settings button can open the ControlsSettingsPanel
// Add this to your existing settings button click handler:
if (ControlsSettingsPanel.Instance != null)
{
    ControlsSettingsPanel.Instance.ShowPanel();
}
```

## Usage Instructions

### For Players:
1. Open settings menu
2. Tap "Enter Edit Mode"
3. Drag buttons to reposition them
4. Pinch buttons to resize them
5. Use sliders for precise adjustments
6. Tap "Exit Edit Mode" to save changes

### For Developers:
1. All settings are automatically saved to PlayerPrefs
2. Settings persist between game sessions
3. Default values can be reset anytime
4. Buttons maintain their original functionality

## Key Features

### Automatic Integration:
- Works with existing `HoldableButton` and `UIVirtualJoystick` components
- Preserves all current button functionality
- No changes needed to existing input handling code

### Visual Feedback:
- Yellow borders in edit mode
- Semi-transparent overlay
- Resize handles at corners
- Highlight effect for easy identification

### Input Methods:
- Single finger drag for repositioning
- Two finger pinch for resizing
- Mouse scroll wheel support in editor
- Touch-friendly interface

### Data Persistence:
- Uses PlayerPrefs for cross-session storage
- Individual button settings saved separately
- Easy to reset to defaults
- Minimal memory footprint

## Troubleshooting

### Common Issues:

1. **Buttons not responding in edit mode**
   - Ensure ControlCustomizer instance exists
   - Check UI layer mask settings
   - Verify Canvas render mode

2. **Settings not saving**
   - Check PlayerPrefs write permissions
   - Ensure SaveSettings() is called
   - Verify button ID uniqueness

3. **Visual indicators not showing**
   - Check edit mode state
   - Verify indicator creation
   - Ensure proper Canvas hierarchy

### Performance Considerations:
- System is optimized for mobile devices
- Minimal impact on performance
- Efficient touch input handling
- Lazy loading of visual indicators

## Extension Ideas

### Future Enhancements:
1. **Button grouping**: Group related buttons together
2. **Layout presets**: Save multiple button layouts
3. **Haptic feedback**: Add vibration for edit mode actions
4. **Button themes**: Different visual styles for buttons
5. **Import/Export**: Share button configurations between devices

### Advanced Features:
1. **Dynamic button addition**: Add/remove buttons at runtime
2. **Gesture recording**: Record and replay button movements
3. **Accessibility mode**: Larger buttons and high contrast
4. **Multi-language support**: Localized button names

## Support

For issues or questions:
1. Check the console for error messages
2. Verify all required components are present
3. Ensure proper prefab setup
4. Test with different Canvas render modes

This system is designed to be robust and user-friendly while maintaining compatibility with your existing mobile control implementation.
