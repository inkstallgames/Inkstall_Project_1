# Weapon Equip System Setup Guide

## Overview
This equip system allows players to switch between pistol and bomb. Only one weapon can be equipped at a time. The "Throw" button will fire whichever weapon is currently equipped.

## Components Created
1. **NetworkWeaponEquipSystem.cs** - Core equip system (attach to player prefab)
2. **WeaponEquipInputHandler.cs** - Handles UI buttons and keyboard input (attach to player prefab)
3. **Modified NetworkInputData** - Added `equipPistol` and `equipBomb` fields
4. **Modified NetworkBombBehaviour** - Now checks if bomb is equipped before throwing
5. **Modified NetworkPistolBehaviour** - Now checks if pistol is equipped before shooting

## Setup Instructions

### 1. Player Prefab Setup
Add these components to your player prefab:
- **NetworkWeaponEquipSystem** (new)
- **WeaponEquipInputHandler** (new)
- NetworkBombBehaviour (already exists)
- NetworkPistolBehaviour (already exists)

### 2. Configure NetworkWeaponEquipSystem
In the Inspector for NetworkWeaponEquipSystem:
- **Pistol Model**: Drag your pistol GameObject here (the visual model)
- **Bomb Model**: Drag your bomb GameObject here (the visual model that shows in hand)

The system will automatically show/hide these models based on which weapon is equipped.

### 3. Configure WeaponEquipInputHandler
In the Inspector for WeaponEquipInputHandler:
- **Pistol Key**: Default is `1` key (Alpha1)
- **Bomb Key**: Default is `2` key (Alpha2)
- **Pistol Button**: (Optional) Drag your UI button for equipping pistol
- **Bomb Button**: (Optional) Drag your UI button for equipping bomb

### 4. UI Button Setup (Optional)
If you want UI buttons for equipping weapons:

1. Create two UI buttons in your Canvas
2. Name them "PistolButton" and "BombButton"
3. Drag them to the WeaponEquipInputHandler component on your player prefab
4. The script will automatically hook up the onClick events

### 5. How It Works

**Equipping Weapons:**
- Press `1` key or click Pistol Button → Equips pistol
- Press `2` key or click Bomb Button → Equips bomb
- Only one weapon can be equipped at a time

**Using Weapons:**
- When **Pistol** is equipped:
  - Throw button fires the pistol
  - Bomb throw button does nothing (bomb not equipped)
  
- When **Bomb** is equipped:
  - Throw button throws a bomb
  - Pistol shoot button does nothing (pistol not equipped)

**Visual Feedback:**
- The pistol model is visible when pistol is equipped
- The bomb model is visible when bomb is equipped
- Only one model is visible at a time

### 6. Default Weapon
By default, players spawn with the **Pistol** equipped. You can change this in `NetworkWeaponEquipSystem.Spawned()` method.

## Testing

1. Start the game
2. Press `1` to equip pistol - pistol model should appear
3. Use throw button - should fire pistol
4. Press `2` to equip bomb - bomb model should appear, pistol model should hide
5. Use throw button - should throw bomb
6. Check console logs for equip confirmations

## Console Debug Messages
The system logs these messages for debugging:
- `[NetworkWeaponEquipSystem] Player X equipped Pistol/Bomb`
- `[NetworkPistolBehaviour] TryShoot — skipped, pistol not equipped.`
- `[NetworkBombBehaviour] TryThrow — skipped, bomb not equipped.`

## Notes
- The equip system is fully networked - all players see the correct weapon on each player
- The system uses Fusion's networked state to sync weapon selection across clients
- The throw button behavior automatically adapts based on equipped weapon
- No changes needed to existing throw button - it works with both weapons
