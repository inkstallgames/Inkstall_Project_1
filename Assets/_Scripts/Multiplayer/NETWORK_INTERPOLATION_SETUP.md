# Network Interpolation Setup Guide

## Problem
Clients experience jittery movement even at 60 FPS because:
- Network simulation runs in `FixedUpdateNetwork()` at the tick rate (e.g., 60 ticks/sec)
- Visual rendering runs in `Render()` at the frame rate (60 FPS)
- Without interpolation, clients see discrete position updates instead of smooth motion

## Solution
Add interpolation components to smooth visual rendering between network ticks.

---

## Setup Instructions

### 1. Player Character Prefabs

For **BOTH** player prefabs (Team A and Team B):

1. Open the player prefab in Unity
2. Add the `NetworkTransformInterpolation` component:
   - Select the prefab root GameObject
   - Click "Add Component"
   - Search for "Network Transform Interpolation"
   - Add it to the prefab

3. Configure the component:
   - **Interpolate Position**: ✓ Enabled
   - **Interpolate Rotation**: ✓ Enabled
   - **Position Lerp Speed**: 15 (default is good)
   - **Rotation Lerp Speed**: 15 (default is good)
   - **Snap Distance Threshold**: 5 (teleports if >5 units away)
   - **Snap Angle Threshold**: 45 (snaps if >45° rotation difference)

4. **IMPORTANT**: Ensure the prefab has these components:
   - `NetworkObject` (required for networking)
   - `NetworkTransform` (syncs position/rotation over network)
   - `NetworkTransformInterpolation` (NEW - smooths rendering)
   - `ThirdPersonController` (your movement script)

### 2. Bomb Projectile Prefab

For the bomb prefab:

1. Open the bomb prefab in Unity
2. Add the `NetworkRigidbodyInterpolation` component:
   - Select the bomb prefab root GameObject
   - Click "Add Component"
   - Search for "Network Rigidbody Interpolation"
   - Add it to the prefab

3. Configure the component:
   - **Position Lerp Speed**: 20 (faster for projectiles)
   - **Rotation Lerp Speed**: 20 (faster for projectiles)
   - **Snap Distance Threshold**: 10 (higher for fast-moving objects)

4. **IMPORTANT**: Ensure the bomb prefab has these components:
   - `NetworkObject` (required for networking)
   - `NetworkTransform` (syncs position/rotation)
   - `Rigidbody` (for physics)
   - `NetworkRigidbodyInterpolation` (NEW - smooths rendering)
   - `NetworkBombProjectile` (your bomb script)

---

## How It Works

### For Players (NetworkTransformInterpolation)
- **Server/Host**: Runs physics normally in `FixedUpdateNetwork()`
- **Clients**: 
  - Receive position updates from server via `NetworkTransform`
  - `NetworkTransformInterpolation.Render()` smoothly interpolates between updates
  - Only affects visual rendering, not actual network state
  - Automatically disabled for local player (you control directly)

### For Bombs (NetworkRigidbodyInterpolation)
- **Server/Host**: Runs physics simulation with `Rigidbody`
- **Clients**:
  - Rigidbody is made kinematic (no local physics)
  - Receive position updates from server
  - `NetworkRigidbodyInterpolation.Render()` smoothly interpolates
  - Creates illusion of smooth physics on client

---

## Testing

1. **Start a host game**
2. **Join as a client** from another device/instance
3. **On the client**, press **F3** to show FPS counter
4. **Move around** - movement should now be smooth at 60 FPS
5. **Throw bombs** - projectiles should arc smoothly without stuttering

### Expected Results
- ✓ Client sees smooth 60 FPS movement
- ✓ No jittering when other players move
- ✓ Bombs fly smoothly through the air
- ✓ Host performance unchanged (still 60 FPS)

### If Still Jittery
1. Check that `NetworkTransform` component exists on prefabs
2. Verify `InterpolationDataSource` is set to "Snapshots" on `NetworkTransform`
3. Increase lerp speeds (try 20-30 for faster interpolation)
4. Check network stats - high ping (>150ms) may cause issues

---

## Technical Details

### Render() vs FixedUpdateNetwork()
- `FixedUpdateNetwork()`: Runs at network tick rate, updates game state
- `Render()`: Runs every frame (60 FPS), updates visuals only
- Interpolation happens in `Render()` for smooth 60 FPS visuals

### Why Only Clients?
The scripts check `Object.HasInputAuthority` and disable themselves:
- **Your own character**: Component disabled entirely (direct control, no interference)
- **Other players**: Interpolation enabled (smooth remote players)
- **Server**: No interpolation needed (authoritative simulation)

**IMPORTANT**: The interpolation component calls `enabled = false` for the local player to completely prevent any transform conflicts with CharacterController movement.

### Snap Thresholds
Prevents interpolation when objects teleport:
- Respawning
- Scene transitions
- Large position corrections
- Network lag spikes

---

## Files Created
1. `NetworkTransformInterpolation.cs` - For player characters
2. `NetworkRigidbodyInterpolation.cs` - For physics objects (bombs)
3. `NetworkFPSManager.cs` - Ensures 60 FPS (already added to NetworkStarter)
4. This setup guide

---

## Troubleshooting

**Q: Client still feels jittery**
A: Increase lerp speeds to 20-30, or check if NetworkTransform is missing

**Q: Movement feels delayed/laggy**
A: Decrease lerp speeds to 10-12 for more responsive feel

**Q: Players snap/teleport occasionally**
A: Normal for high latency. Increase snap thresholds if too sensitive

**Q: Bombs don't interpolate**
A: Ensure NetworkRigidbodyInterpolation is on the bomb prefab root

**Q: My own character feels weird/jittery**
A: The interpolation component should auto-disable for local player. Check:
   1. Enable "Show Debug Info" on NetworkTransformInterpolation
   2. Look for "Disabled for local player" in console
   3. If not appearing, the component may not be detecting InputAuthority correctly
   4. Make sure you added the component AFTER the NetworkObject component

**Q: Local player was smooth before, jittery after adding interpolation**
A: This was fixed - the component now calls `enabled = false` for local player to avoid transform conflicts with CharacterController.
