# Multiplayer Pistol Shooting Setup Instructions

## Overview
This guide explains how to implement pistol shooting with raycast in your multiplayer game using Photon Fusion.

## Files Created
1. **NetworkPistolBehaviour.cs** - Main pistol shooting logic with raycast
2. **PistolInputHandler.cs** - Handles keyboard/mouse and UI button input
3. **PistolAmmoUI.cs** - Displays ammo count on screen
4. **BulletTrail.cs** - Visual bullet trail effect
5. **ThirdPersonController.cs** - Updated to include shooting input in NetworkInputData

---

## Step-by-Step Setup

### 1. Update Player Prefab

Add the following components to your **Player Prefab** (the one with NetworkObject):

#### A. Add NetworkPistolBehaviour
1. Select your player prefab
2. Add Component → **NetworkPistolBehaviour**
3. Configure the settings:
   - **Fire Point**: Create an empty GameObject as a child of your player (position it where bullets should spawn, e.g., in front of the player's chest/gun)
   - **Fire Rate**: 0.2 (5 shots per second)
   - **Range**: 100
   - **Damage**: 15
   - **Hit Layers**: Set to layers that can be hit (e.g., Default, Player)
   - **Max Ammo**: 30
   - **Reserve Ammo**: 90
   - **Reload Time**: 1.5 seconds

#### B. Add PistolInputHandler
1. Add Component → **PistolInputHandler**
2. Configure:
   - **Shoot Key**: Mouse0 (left click)
   - **Reload Key**: R
   - Leave UI buttons empty for now (optional)

### 2. Create Visual Effects (Optional but Recommended)

#### A. Bullet Trail Prefab
1. Create a new GameObject: Right-click in Hierarchy → Create Empty → Name it "BulletTrail"
2. Add Component → **Line Renderer**
3. Configure Line Renderer:
   - Width: 0.05
   - Positions: 2 points
   - Material: Create a new material with Sprites/Default shader
   - Color: Yellow or white
4. Add Component → **BulletTrail** script
5. Save as prefab in your Prefabs folder
6. Assign this prefab to **NetworkPistolBehaviour → Bullet Trail Prefab**

#### B. Muzzle Flash (Optional)
1. Add a Particle System as a child of your Fire Point
2. Configure for a quick flash effect
3. Assign to **NetworkPistolBehaviour → Muzzle Flash**

#### C. Hit Effect (Optional)
1. Create a particle system for bullet impact
2. Save as prefab
3. Assign to **NetworkPistolBehaviour → Hit Effect Prefab**

### 3. Setup UI

#### A. Create Ammo Display
1. In your Canvas, create a new UI element:
   - Right-click Canvas → UI → Panel (name it "AmmoPanel")
2. Add TextMeshPro text elements:
   - **Ammo Text**: Shows "30 / 90" format
   - **Reload Text**: Shows "RELOADING..." (hide by default)
3. Add an empty GameObject to the Canvas
4. Add Component → **PistolAmmoUI**
5. Assign the text elements to the script fields

#### B. Optional: Shoot/Reload Buttons (for mobile)
1. Create UI Buttons for Shoot and Reload
2. Assign them to **PistolInputHandler → Shoot Button** and **Reload Button**

### 4. Configure Layers

Make sure your player objects are on a layer that can be hit by raycasts:
1. Create a "Player" layer if you don't have one
2. Set your player prefab to this layer
3. In **NetworkPistolBehaviour → Hit Layers**, include the Player layer

### 5. Test in Multiplayer

#### Single Player Test:
1. Start the game as Host
2. Press Left Mouse Button or tap Shoot button to fire
3. Press R to reload
4. Check console for hit detection logs

#### Multiplayer Test:
1. Build and run as Host
2. Start another instance as Client
3. Shoot at the other player
4. Verify damage is applied (check health bar)
5. Verify visual effects appear on both clients

---

## How It Works

### Network Architecture

1. **Input Collection** (`PistolInputHandler`)
   - Local player presses shoot/reload
   - Calls `RequestShoot()` or `RequestReload()` on NetworkPistolBehaviour

2. **Input Transmission** (`ThirdPersonController.OnInput`)
   - Collects pistol input via `CollectNetworkInput()`
   - Packs into `NetworkInputData` struct
   - Sends to server every network tick

3. **Server-Side Execution** (`NetworkPistolBehaviour.FixedUpdateNetwork`)
   - Server receives input
   - Performs raycast from camera position
   - Applies damage if player is hit
   - Sends RPC to all clients for visual effects

4. **Client-Side Effects** (`RPC_OnShot`)
   - All clients play muzzle flash
   - All clients spawn bullet trail
   - All clients spawn hit effect

### Key Features

✅ **Server-Authoritative**: All shooting logic runs on server to prevent cheating
✅ **Raycast-Based**: Instant hit detection using Physics.Raycast
✅ **Networked Ammo**: Ammo count synced across all clients
✅ **Visual Effects**: Muzzle flash, bullet trails, and hit effects
✅ **Reload System**: Timed reload with reserve ammo
✅ **Damage System**: Integrates with existing PlayerNetworkData health system

---

## Troubleshooting

### Shooting doesn't work
- Check that NetworkPistolBehaviour is attached to player prefab
- Verify Fire Point is assigned
- Check Hit Layers includes target layers
- Look for errors in console

### No damage applied
- Ensure target has PlayerNetworkData component
- Check that players are on different teams (or remove team check)
- Verify Hit Layers includes player layer

### Visual effects don't show
- Check that prefabs are assigned in NetworkPistolBehaviour
- Verify RPC is being called (check console logs)
- Make sure materials are assigned to Line Renderer

### Ammo UI doesn't update
- Verify PistolAmmoUI is in the scene
- Check that it finds the local player (console log)
- Ensure TextMeshPro is imported

---

## Customization

### Change Fire Rate
Modify `fireRate` in NetworkPistolBehaviour (lower = faster)

### Change Damage
Modify `damage` in NetworkPistolBehaviour

### Change Range
Modify `range` in NetworkPistolBehaviour

### Add Recoil
In `RPC_OnShot`, add camera shake or weapon kickback

### Add Spread
In `TryShoot`, add random offset to aim direction:
```csharp
Vector3 spread = new Vector3(
    Random.Range(-0.02f, 0.02f),
    Random.Range(-0.02f, 0.02f),
    0
);
direction += spread;
```

---

## Next Steps

1. **Add weapon switching** - Create multiple weapon types
2. **Add headshot detection** - Check raycast hit point
3. **Add weapon animations** - Trigger shooting animations
4. **Add sound effects** - Assign AudioClips to NetworkPistolBehaviour
5. **Add crosshair** - Create UI crosshair that changes on hit
6. **Add weapon pickups** - Spawn weapons in the world

---

## Support

If you encounter issues:
1. Check Unity console for errors
2. Enable debug logs in NetworkPistolBehaviour
3. Verify all components are on the correct GameObjects
4. Test in single player first before multiplayer
