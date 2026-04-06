# Key Refresh Timer System - Setup Instructions

## Overview
The Key Refresh Timer system automatically adds keys back to the player's inventory over time. When a player uses a key and has less than the maximum (10 keys), a 10-minute timer starts. When the timer completes, one key is added back.

**Key Features:**
- Timer persists even when the app is closed
- Automatically handles multiple keys if app was closed for extended periods
- Stops timer when keys reach maximum (10)
- Only starts timer when keys are below maximum

## Setup Instructions

### 1. Add KeyRefreshTimer to Scene
1. In your main scene (or a persistent scene), create an empty GameObject
2. Name it "KeyRefreshTimer"
3. Add the `KeyRefreshTimer` component to it
4. Configure settings in Inspector:
   - **Key Refresh Time In Minutes**: Set to 10 (default)
   - **Max Keys**: Set to 10 (default)

### 2. Verify KeyManager Integration
The `KeyManager.cs` has been updated to automatically notify the timer when a key is used. No additional setup needed.

### 3. Optional: Add Timer UI Display
If you want to show the countdown timer to players:

1. In your UI Canvas, create a TextMeshProUGUI element for the timer
2. Optionally create a Panel to contain the timer (can be shown/hidden automatically)
3. Create an empty GameObject in your Canvas
4. Add the `KeyTimerUI` component to it
5. Assign references in Inspector:
   - **Timer Text**: Drag your TextMeshProUGUI element
   - **Timer Panel**: (Optional) Drag your panel if you want it to show/hide automatically

## How It Works

### User Flow:
1. Player starts with 10 keys (default)
2. Player uses 1 key → Now has 9 keys
3. Timer automatically starts (10 minutes)
4. After 10 minutes → 1 key is added back (now has 10 keys)
5. Timer stops because keys are at maximum

### Edge Cases Handled:
- **App Closed**: Timer continues counting. When app reopens, it calculates elapsed time and adds appropriate keys
- **Multiple Keys**: If app was closed for 30 minutes, 3 keys will be added when reopened
- **Max Keys**: Timer won't start if player already has 10 keys
- **Timer Active**: Won't start multiple timers, only one runs at a time

## Testing

### Test 1: Basic Timer
1. Start with 10 keys
2. Use 1 key (should have 9 keys)
3. Wait 10 minutes
4. Verify 1 key was added (should have 10 keys)

### Test 2: Persistent Timer (App Closed)
1. Use 1 key (should have 9 keys)
2. Close the app completely
3. Wait 10+ minutes
4. Reopen the app
5. Verify key was added

### Test 3: Multiple Keys Recovery
1. Use 5 keys (should have 5 keys)
2. Close the app
3. Wait 30+ minutes
4. Reopen the app
5. Verify 3 keys were added (should have 8 keys)
6. Wait 20 more minutes
7. Verify 2 more keys added (should have 10 keys, timer stops)

### Debug Methods
You can call these methods for testing:
```csharp
// Force check timer (useful for testing without waiting)
KeyRefreshTimer.Instance.ForceCheckTimer();

// Get remaining time in seconds
float remaining = KeyRefreshTimer.Instance.GetRemainingTime();

// Get formatted time string
string timeStr = KeyRefreshTimer.Instance.GetRemainingTimeFormatted();

// Check if timer is active
bool isActive = KeyRefreshTimer.Instance.IsTimerActive();
```

## Customization

### Change Timer Duration
In the KeyRefreshTimer Inspector, modify:
- **Key Refresh Time In Minutes**: Change from 10 to any value you want

### Change Maximum Keys
In the KeyRefreshTimer Inspector, modify:
- **Max Keys**: Change from 10 to any value you want

**Note**: Make sure this matches the max keys in KeyManager!

## Technical Details

### Data Persistence
The system uses PlayerPrefs to store:
- `KeyRefreshTimerStart`: Binary timestamp when timer started
- `KeyRefreshTimerActive`: Whether timer is currently active
- `KeysCount`: Current number of keys (from KeyManager)

### Time Calculation
Uses `DateTime.Now` and `DateTime.FromBinary()` to accurately track time even when app is closed. This is more reliable than using `Time.deltaTime` which only works when app is running.

## Troubleshooting

**Timer not starting:**
- Verify KeyRefreshTimer GameObject is in the scene
- Check that keys are below maximum (10)
- Ensure KeyManager.Instance is not null

**Timer not persisting:**
- Check PlayerPrefs are being saved (PlayerPrefs.Save() is called)
- Verify OnApplicationQuit() and OnApplicationPause() are being called

**Keys not being added:**
- Check KeyManager.AddKeys() method is working
- Verify max keys setting matches between KeyManager and KeyRefreshTimer
- Check Debug.Log messages in console for timer events
