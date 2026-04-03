# FPS Animation Controller Setup Guide
## Deadshot.io Style Animations

### 🎯 Overview
This system combines procedural animations with keyframe animations for smooth, responsive FPS hands movement similar to deadshot.io.

### 📁 Required Components
1. **FPSAnimationController.cs** - Main animation controller
2. **Animator Controller** - Unity Animator setup
3. **Animation Clips** - Movement, shooting, reload animations

### 🎮 Animator Controller Parameters

#### Float Parameters:
- `MovementBlend` (0-1): Controls walk/run intensity
- `AimBlend` (0-1): Aiming down sights blend
- `RecoilBlend` (0-1): Weapon recoil intensity
- `VerticalVelocity`: Jump/fall detection

#### Bool Parameters:
- `IsGrounded`: Grounded state for jump animations

#### Trigger Parameters:
- `Shoot`: Fire weapon animation
- `Reload`: Reload animation
- `Equip`: Weapon equip animation

### 🏃 Movement Animation Setup

#### 1. Create Animation Clips
- **Idle**: Breathing idle animation
- **Walk**: Walking animation (1.5 speed)
- **Run**: Running animation (2.5 speed)
- **Jump**: Jump initiation
- **Fall**: Falling animation
- **Land**: Landing recovery

#### 2. Blend Tree Setup
```
Movement Blend Tree:
├── Idle (0.0)
├── Walk (0.5) 
└── Run (1.0)
```

#### 3. Layer Setup
- **Base Layer**: Movement animations
- **Aim Layer**: Aiming overrides (weight: 1.0)
- **Action Layer**: Shoot/Reload actions (weight: 1.0)

### 🎯 Procedural Animation Settings

#### Movement Intensity:
```csharp
// For subtle movement (like deadshot.io)
proceduralIntensity = 0.5f;
proceduralFrequency = 1.0f;

// For more responsive movement
proceduralIntensity = 1.0f;
proceduralFrequency = 1.5f;
```

#### Keyframe Animation Examples:

#### Idle Animation:
```
Position: (0, 0, 0) → (0, 0.002, 0) → (0, 0, 0)
Rotation: (0, 0, 0) → (0, 0.5, 0) → (0, 0, 0)
Time: 0s → 2s → 4s
```

#### Walk Animation:
```
Position: (0, 0, 0) → (0.02, 0.01, 0) → (0, 0, 0) → (-0.02, 0.01, 0) → (0, 0, 0)
Rotation: (0, 0, 0) → (2, 1, 0) → (0, 0, 0) → (-2, -1, 0) → (0, 0, 0)
Time: 0s → 0.5s → 1s → 1.5s → 2s
```

#### Run Animation:
```
Position: (0, 0, 0) → (0.035, 0.02, 0) → (0, 0, 0) → (-0.035, 0.02, 0) → (0, 0, 0)
Rotation: (0, 0, 0) → (4, 2, 1) → (0, 0, 0) → (-4, -2, -1) → (0, 0, 0)
Time: 0s → 0.3s → 0.6s → 0.9s → 1.2s
```

### 🔫 Weapon Animation Setup

#### Shoot Animation:
```
Position: (0, 0, 0) → (0, 0, -0.05) → (0, 0, 0)
Rotation: (0, 0, 0) → (15, 0, 2) → (0, 0, 0)
Time: 0s → 0.1s → 0.2s
```

#### Aim Transition:
```
Position: (0, 0, 0) → (0.02, -0.01, 0.03)
Rotation: (0, 0, 0) → (-5, 2, 0)
Blend: Based on AimBlend parameter
```

### 🎨 Visual Settings

#### Deadshot.io Style:
- **Movement**: Subtle, responsive
- **Recoil**: Quick, snappy
- **Aiming**: Smooth transitions
- **Breathing**: Very subtle idle

#### Configuration:
```csharp
animationBlendSpeed = 10f;      // Fast transitions
proceduralIntensity = 0.7f;     // Moderate procedural
proceduralFrequency = 1.2f;     // Slightly faster than default
recoilRecoveryTime = 0.15f;     // Quick recoil recovery
aimTransitionSpeed = 8f;        // Smooth aim transitions
```

### 🔧 Setup Instructions

#### 1. Create Animator Controller
1. Right-click → Create → Animator Controller
2. Name it "FPSAnimatorController"
3. Add parameters listed above
4. Create blend trees and layers

#### 2. Add to FPS Hands
1. Add `FPSAnimationController` to your FPS hands object
2. Assign the Animator Controller
3. Configure settings in inspector

#### 3. Test Animations
1. Press Play
2. Check debug GUI for animation states
3. Adjust `proceduralIntensity` for desired feel

### 🎮 Integration

#### Connect to Existing Systems:
```csharp
// In your weapon script
FPSAnimationController animController = GetComponent<FPSAnimationController>();

// When shooting
animController.OnShoot();

// When reloading
animController.OnReload();

// When equipping
animController.OnEquip();
```

### 📊 Performance Tips

- **Optimize**: Keep animation clips under 2 seconds
- **LOD**: Reduce procedural intensity at distance
- **Pooling**: Reuse animation clips
- **Baking**: Bake complex animations when possible

### 🎯 Deadshot.io Specific Features

#### Characteristic Elements:
1. **Smooth Movement**: No jarring transitions
2. **Responsive Controls**: Immediate feedback
3. **Natural Weight**: Realistic physics feel
4. **Clean Aiming**: Smooth ADS transitions
5. **Subtle Details**: Breathing, micro-movements

#### Implementation Notes:
- Use curves for natural motion
- Blend procedural with keyframe
- Keep movements subtle but noticeable
- Prioritize responsiveness over realism
