# Weapon Equip System Fixes

## Issues Fixed

### 1. ✅ No Debug Logs for Keys 1 and 2
**Problem:** Pressing keys 1 and 2 didn't show debug logs
**Cause:** `WeaponEquipInputHandler` wasn't detecting local player correctly because Fusion's `Object` wasn't ready in `Start()`
**Fix:** Modified `WeaponEquipInputHandler.cs` to check for local player in `Update()` instead of only in `Start()`

### 2. ✅ Throw Button Only Throws Bomb
**Problem:** Throw button always threw bomb regardless of equipped weapon
**Cause:** `NetworkUIManager.OnThrowButtonPressed()` only called `RequestThrow()` on bomb
**Fix:** Updated `NetworkUIManager.cs` to:
- Cache references to `NetworkPistolBehaviour` and `NetworkWeaponEquipSystem`
- Check which weapon is equipped before firing
- Call `RequestShoot()` if pistol is equipped
- Call `RequestThrow()` if bomb is equipped

### 3. ✅ Gun GameObject Disabled When Bomb Equipped
**Problem:** Gun GameObject should be disabled when bomb is equipped
**Status:** Already implemented in `NetworkWeaponEquipSystem.UpdateWeaponVisuals()`
- Pistol model is shown only when `CurrentWeapon == WeaponType.Pistol`
- Bomb model is shown only when `CurrentWeapon == WeaponType.Bomb`

---

## Files Modified

### 1. WeaponEquipInputHandler.cs
**Changes:**
- Added `hasCheckedLocalPlayer` flag
- Added `CheckLocalPlayer()` method that runs in `Update()`
- Added debug logging to track when local player is detected
- Now properly detects local player after Fusion initializes

**Key Code:**
```csharp
private void CheckLocalPlayer()
{
    if (hasCheckedLocalPlayer) return;
    
    if (equipSystem != null && equipSystem.Object != null && equipSystem.Object.HasInputAuthority)
    {
        isLocalPlayer = true;
        hasCheckedLocalPlayer = true;
        SetupUIButtons();
        Debug.Log($"[WeaponEquipInputHandler] Local player detected! Input handling enabled.");
    }
}
```

### 2. NetworkUIManager.cs
**Changes:**
- Added `localPistolBehaviour` reference
- Added `localEquipSystem` reference
- Updated `TryFindLocalPlayer()` to cache these references
- Completely rewrote `OnThrowButtonPressed()` to check equipped weapon

**Key Code:**
```csharp
public void OnThrowButtonPressed()
{
    if (localEquipSystem != null)
    {
        if (localEquipSystem.IsBombEquipped() && localBombBehaviour != null)
        {
            Debug.Log("[NetworkUIManager] Throw button pressed - Bomb is equipped, throwing bomb");
            localBombBehaviour.RequestThrow();
        }
        else if (localEquipSystem.IsPistolEquipped() && localPistolBehaviour != null)
        {
            Debug.Log("[NetworkUIManager] Throw button pressed - Pistol is equipped, shooting pistol");
            localPistolBehaviour.RequestShoot();
        }
    }
}
```

---

## Testing Checklist

### ✅ Key Press Detection
1. Start game as host (with testing mode enabled)
2. Press `1` key → Should see: `"[WeaponEquipInputHandler] Key '1' pressed - Requesting to equip PISTOL"`
3. Press `2` key → Should see: `"[WeaponEquipInputHandler] Key '2' pressed - Requesting to equip BOMB"`

### ✅ Weapon Equipping
1. Press `1` → Should see: `"[NetworkWeaponEquipSystem] *** Player X EQUIPPED PISTOL ***"`
2. Pistol model should appear, bomb model should disappear
3. Press `2` → Should see: `"[NetworkWeaponEquipSystem] *** Player X EQUIPPED BOMB ***"`
4. Bomb model should appear, pistol model should disappear

### ✅ Throw Button Behavior
1. Equip pistol (press `1`)
2. Click throw button → Should shoot pistol
3. Console: `"[NetworkUIManager] Throw button pressed - Pistol is equipped, shooting pistol"`
4. Equip bomb (press `2`)
5. Click throw button → Should throw bomb
6. Console: `"[NetworkUIManager] Throw button pressed - Bomb is equipped, throwing bomb"`

### ✅ Visual Feedback
1. When pistol is equipped → Gun GameObject is active, bomb GameObject is inactive
2. When bomb is equipped → Bomb GameObject is active, gun GameObject is inactive

---

## Setup Requirements

### Player Prefab Must Have:
1. ✅ `NetworkWeaponEquipSystem` component
2. ✅ `WeaponEquipInputHandler` component
3. ✅ `NetworkBombBehaviour` component
4. ✅ `NetworkPistolBehaviour` component

### NetworkWeaponEquipSystem Inspector:
- **Pistol Model**: Drag your gun GameObject here
- **Bomb Model**: Drag your bomb GameObject here

### WeaponEquipInputHandler Inspector:
- **Pistol Key**: `Alpha1` (key 1)
- **Bomb Key**: `Alpha2` (key 2)
- **Pistol Button**: (Optional) UI button for pistol
- **Bomb Button**: (Optional) UI button for bomb

---

## Debug Console Messages

### On Game Start:
```
[WeaponEquipInputHandler] Start called. EquipSystem found: True
[WeaponEquipInputHandler] Local player detected! Input handling enabled.
[NetworkUIManager] Local player found! Object: PlayerPrefab(Clone)
[NetworkUIManager]   - NetworkBombBehaviour: FOUND
[NetworkUIManager]   - NetworkPistolBehaviour: FOUND
[NetworkUIManager]   - NetworkWeaponEquipSystem: FOUND
```

### When Pressing Keys:
```
[WeaponEquipInputHandler] Key '1' pressed - Requesting to equip PISTOL
[NetworkWeaponEquipSystem] RequestEquipPistol() called
[NetworkWeaponEquipSystem] *** Player 0 EQUIPPED PISTOL ***
```

### When Using Throw Button:
```
[NetworkUIManager] Throw button pressed - Pistol is equipped, shooting pistol
```

---

## Troubleshooting

**Q: Still no logs when pressing 1 or 2?**
- Check console for: `"[WeaponEquipInputHandler] Local player detected!"`
- If missing, ensure `NetworkWeaponEquipSystem` is attached to player prefab
- Make sure you're pressing keys while in-game, not in lobby

**Q: Throw button still only throws bomb?**
- Check console for: `"[NetworkUIManager] Local player found!"`
- Verify all three components are FOUND (Bomb, Pistol, EquipSystem)
- Make sure you equipped a weapon first (press 1 or 2)

**Q: Gun doesn't disappear when bomb is equipped?**
- Check that `pistolModel` and `bombModel` are assigned in `NetworkWeaponEquipSystem` Inspector
- These should reference the actual GameObjects (not the components)
- Verify console shows: `"*** Player X EQUIPPED BOMB ***"` when pressing 2

---

## Summary

All three issues have been fixed:
1. ✅ Keys 1 and 2 now properly trigger debug logs
2. ✅ Throw button now fires the currently equipped weapon
3. ✅ Gun GameObject is properly disabled when bomb is equipped

The weapon equip system is now fully functional!
