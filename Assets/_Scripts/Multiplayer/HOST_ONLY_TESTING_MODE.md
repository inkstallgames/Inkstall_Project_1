# Host-Only Testing Mode

## Overview
This testing mode allows you to test multiplayer features **without needing a second client**. The host can play solo to test game mechanics, weapons, movement, etc.

## ⚠️ IMPORTANT
**This is for TESTING ONLY!** Always disable this before building for production/release.

---

## How to Enable Testing Mode

### Step 1: Find NetworkStarter in Scene
1. Open the **Lobby** scene (or whichever scene has the NetworkStarter)
2. Find the **NetworkStarter** GameObject in the hierarchy
3. Select it to view in the Inspector

### Step 2: Enable Testing Mode
1. In the Inspector, look for the **"TESTING MODE"** section at the top
2. Check the box: **Enable Host Only Testing**
3. Save the scene

### Step 3: Test
1. Click Play in Unity Editor
2. Click "Host" button
3. You can now start the game and play solo without waiting for a client!

---

## How to Disable Testing Mode (Revert to Normal)

### Step 1: Find NetworkStarter
1. Open the **Lobby** scene
2. Select the **NetworkStarter** GameObject

### Step 2: Disable Testing Mode
1. In the Inspector, look for the **"TESTING MODE"** section
2. **UNCHECK** the box: **Enable Host Only Testing**
3. Save the scene

### Step 3: Normal Multiplayer Restored
- Host will now require clients to join
- Normal multiplayer behavior is restored

---

## What Changes When Testing Mode is Enabled?

### Normal Mode (Testing Mode OFF):
- GameMode: `Host`
- Host creates a room and waits for clients
- Requires at least 2 players (host + 1 client) to test multiplayer features
- Console: `"Attempting to connect to Photon Cloud..."`

### Testing Mode (Testing Mode ON):
- GameMode: `Host` (same as normal - Fusion handles this automatically)
- Host can play solo immediately
- No client needed for testing
- Console: `"*** TESTING MODE ENABLED *** Starting as Host (can play solo without client)"`

---

## Use Cases for Testing Mode

✅ **Good for:**
- Testing weapon mechanics (pistol, bomb)
- Testing player movement
- Testing UI and HUD
- Testing game logic
- Quick iteration on features
- Solo debugging

❌ **NOT for:**
- Testing actual multiplayer synchronization
- Testing network latency
- Testing client-server communication
- Production builds
- Final testing before release

---

## Console Debug Messages

When testing mode is **ENABLED**, you'll see:
```
[NetworkStarter] *** TESTING MODE ENABLED *** Starting as Host (can play solo without client)
```

When testing mode is **DISABLED** (normal), you'll see:
```
[NetworkStarter] Attempting to connect to Photon Cloud...
```

---

## Quick Reference

| Action | Location | Setting |
|--------|----------|---------|
| **Enable Testing** | NetworkStarter Inspector | ✅ Enable Host Only Testing |
| **Disable Testing** | NetworkStarter Inspector | ❌ Enable Host Only Testing |
| **Check Status** | Console | Look for "TESTING MODE ENABLED" message |

---

## Troubleshooting

**Q: I enabled testing mode but still can't play solo?**
- Make sure you saved the scene after checking the box
- Restart Unity Editor
- Check console for "TESTING MODE ENABLED" message

**Q: How do I know if testing mode is active?**
- Check the NetworkStarter Inspector - box should be checked
- Console will show "*** TESTING MODE ENABLED ***" when hosting

**Q: Can I build the game with testing mode enabled?**
- **NO!** Always disable testing mode before building
- Testing mode is for Unity Editor testing only

---

## Remember!
🔴 **ALWAYS DISABLE TESTING MODE BEFORE BUILDING FOR PRODUCTION!** 🔴
