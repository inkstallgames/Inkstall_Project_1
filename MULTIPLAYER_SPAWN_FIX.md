# Multiplayer Player Spawning Fix

## Problem
When other players join the game, they are not being loaded into the mall scene correctly and their characters are not spawned. Only the host loads correctly.

## Root Cause
1. **NetworkGameManager** was losing its reference to `NetworkPlayerSpawner` when scenes changed from Lobby to Mall
2. Multiple spawning systems were conflicting (NetworkStarter and NetworkGameManager both trying to spawn)
3. The spawner wasn't being re-initialized with the NetworkRunner after scene loads

## Changes Made

### 1. NetworkGameManager.cs
- Added `RefreshPlayerSpawner()` method that:
  - Finds the NetworkPlayerSpawner in the current scene
  - Initializes it with the NetworkRunner
  - Logs success/failure for debugging
- Modified `Awake()` to call `RefreshPlayerSpawner()`
- Modified `InitializeGame()` to call `RefreshPlayerSpawner()` before spawning players
- Added more debug logging to track spawning process

### 2. NetworkStarter.cs
- Modified `OnSceneLoadDone()` to:
  - Check if NetworkGameManager is in "Starting" state
  - If so, let NetworkGameManager handle spawning (avoids duplicate spawning)
  - Only spawn players here if joining mid-game
  - Check if players already have player objects before spawning
  - Added extensive debug logging

## What to Check

### In Unity Editor:
1. **Mall Scene (Building1.unity or your game scene)**:
   - Ensure there is a GameObject with `NetworkPlayerSpawner` component
   - Ensure there are `PlayerSpawnPoint` components in the scene
   - Check that spawn points have correct `teamId` values (-1 for FreeForAll, 0/1 for teams)

2. **NetworkGameManager**:
   - Should be in the scene or as a DontDestroyOnLoad object
   - Should have the `playerPrefab` field assigned (if used)

3. **Console Logs to Watch**:
   - `[NetworkGameManager] NetworkPlayerSpawner found and assigned`
   - `[NetworkGameManager] Spawning player X`
   - `[NetworkStarter] Game is starting, NetworkGameManager will handle player spawning`
   - `[NetworkPlayerSpawner] SpawnPlayer called for player X`

### Testing Steps:
1. Start as Host in Lobby
2. Select hero and map
3. Start game - Host should spawn correctly
4. Have another player join the session
5. Check console for spawning logs
6. Verify the joining player spawns in the mall scene

## Potential Issues to Check

If players still don't spawn:
1. **Missing NetworkPlayerSpawner**: Check that the mall scene has a GameObject with NetworkPlayerSpawner component
2. **Missing Spawn Points**: Ensure PlayerSpawnPoint components exist in the scene
3. **Lobby Data**: Verify that LobbyPlayers dictionary contains the joining player's data
4. **Hero Prefab**: Ensure HeroManager has the correct hero prefabs registered
5. **Network Authority**: Only the server/host should spawn players

## Debug Commands
Add these to your console to check state:
- Check if NetworkGameManager exists: `NetworkGameManager.Instance != null`
- Check game state: `NetworkGameManager.Instance.CurrentGameState`
- Check active players: `Runner.ActivePlayers.Count()`
