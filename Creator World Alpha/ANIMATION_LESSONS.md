# Unity Animation System - Lessons Recap

## Overview

This document summarizes the animation lessons learned while setting up the player animation system in Unity using Mecanim.

---

## Lesson 1-2: Basic State Transitions

### What We Learned
- **States** represent individual animations or blend trees
- **Transitions** connect states with arrows
- **Conditions** determine when transitions occur

### How to Create a Transition
1. Right-click on source state → **Make Transition**
2. Click on destination state
3. Click the arrow to configure in Inspector

### Condition Types
| Type | Example | Use Case |
|------|---------|----------|
| Float Greater | `Speed > 0.1` | Start moving |
| Float Less | `Speed < 0.1` | Stop moving |
| Bool True | `IsCrouching = true` | Enter crouch |
| Bool False | `IsCrouching = false` | Exit crouch |

### Key Insight
Using **floats** (like Speed) instead of booleans allows for smoother control and easier blend tree integration later.

---

## Lesson 3: Animation Looping

### The Problem
Animations play once and stop instead of looping continuously.

### The Fix
1. Select the `.fbx` file in Project window
2. Go to **Inspector → Animation tab**
3. Check **Loop Time** ✓
4. Click **Apply**

### Additional Options
- **Loop Pose**: Smooths the loop transition if start/end poses don't match
- **Cycle Offset**: Adjusts where the animation starts (0-1)

---

## Lesson 4-5: 1D Blend Trees

### What is a 1D Blend Tree?
A blend tree that uses **ONE parameter** to blend between multiple animations.

### When to Use
- Simple idle → walk → run progression
- Any linear blend (crouch idle → crouch walk)

### How to Create
1. Right-click in Animator → **Create State → From New Blend Tree**
2. Double-click to enter the blend tree
3. Set **Blend Type** to `1D`
4. Choose your **Parameter** (e.g., Speed)
5. Add motions with **+** button

### Thresholds
| Animation | Threshold | When it Plays |
|-----------|-----------|---------------|
| Idle | 0 | Speed = 0 |
| Walk | 0.5 | Speed = 0.5 |
| Run | 1.0 | Speed = 1.0 |

Values between thresholds = blended animation.

---

## Lesson 6: 2D Blend Trees

### What is a 2D Blend Tree?
A blend tree that uses **TWO parameters** to blend based on direction.

### Blend Types
| Type | Best For |
|------|----------|
| **2D Simple Directional** | One animation per direction, no diagonals |
| **2D Freeform Directional** | Multiple animations, supports diagonals |
| **2D Freeform Cartesian** | Non-directional blending |

### Position Layout (Cross Pattern)
```
              (0, 1) Forward
                  ●
                  |
(-1, 0) Left ●────●────● (1, 0) Right
                  |    (0, 0) Idle
                  |
              (0, -1) Backward
```

### Important: Position Values
- Use **1** (not 0.5) for outer positions
- Matches normalized input values from code
- Prevents blending issues with diagonals

### Parameters
- **MoveX**: Horizontal direction (-1 left, +1 right)
- **MoveZ**: Vertical direction (-1 back, +1 forward)

---

## Lesson 7: Movement Modes

### Turn-to-Face Mode (Unarmed)
- Character **rotates to face movement direction**
- Only needs **forward walk animation**
- MoveX = 0, MoveZ = speed
- Good for: exploration, adventure games

### Strafe Mode (Aiming)
- Character **faces camera direction**
- Needs **directional animations** (forward, left, right, back)
- MoveX/MoveZ from velocity relative to facing
- Good for: shooters, combat

### Code Implementation
```csharp
// In PlayerController.ApplyRotation()
if (isAiming)
{
    // Face camera direction (strafe mode)
    targetRotation = Quaternion.Euler(0, cameraYaw, 0);
}
else if (isMoving)
{
    // Face movement direction (turn-to-face mode)
    targetRotation = movementHandler.GetTargetRotation();
}
```

---

## Lesson 8: Transition Settings

### Has Exit Time
| Setting | Behavior |
|---------|----------|
| ✓ Checked | Waits until animation reaches Exit Time % before transitioning |
| ✗ Unchecked | Transitions **immediately** when condition is met |

**Rule:** For responsive gameplay, usually **uncheck** Has Exit Time.

### Transition Duration
Controls how long the blend between animations takes.

| Value | Feel |
|-------|------|
| 0.05s | Nearly instant, can feel jerky |
| 0.1s | Snappy but smooth |
| 0.15s | Balanced |
| 0.25s | Smooth but delayed |
| 0.4s+ | Cinematic, sluggish for gameplay |

### Other Settings
- **Exit Time**: The % of animation that must play (only if Has Exit Time is checked)
- **Transition Offset**: Where to start the destination animation (0-1)
- **Interruption Source**: Can this transition be interrupted by others?

---

## Current Animator Structure

```
PlayerAnimator.controller
│
├── Base Layer
│   │
│   ├── Blend Locomotion (2D Freeform Directional) ← DEFAULT STATE
│   │   ├── Unarmed_Idle (0, 0)
│   │   ├── Unarmed_Walk_Forward (0, 0.5)
│   │   ├── Unarmed_Walk_Left (-0.5, 0)
│   │   └── Unarmed_Walk_Right (0.5, 0)
│   │
│   └── Crouch (1D Blend Tree)
│       ├── Crouching Idle (threshold: 0)
│       └── Crouched Walking (threshold: 1)
│
├── Transitions
│   ├── Locomotion → Crouch (IsCrouching = true)
│   └── Crouch → Locomotion (IsCrouching = false)
│
└── Parameters
    ├── Speed (Float) - normalized movement speed
    ├── MoveX (Float) - horizontal direction
    ├── MoveZ (Float) - forward/back direction
    ├── VelocityY (Float) - vertical velocity for jumps
    ├── IsGrounded (Bool)
    ├── IsCrouching (Bool)
    ├── IsSprinting (Bool)
    └── IsWalking (Bool)
```

---

## Key Code Files

### PlayerAnimation.cs
- Updates animator parameters every frame
- Handles movement mode switching (strafe vs turn-to-face)
- Location: `Assets/_Project/Scripts/Player/PlayerAnimation.cs`

### PlayerController.cs
- Handles character rotation based on movement mode
- Location: `Assets/_Project/Scripts/Player/PlayerController.cs`

### MovementHandler.cs
- Calculates movement direction and speed
- Provides `NormalizedSpeed` for animator
- Location: `Assets/_Project/Scripts/Player/Movement/MovementHandler.cs`

---

## Common Issues & Fixes

### Animation Doesn't Loop
**Fix:** Enable Loop Time on the FBX import settings

### Transition Feels Delayed
**Fix:** Uncheck "Has Exit Time" and reduce Transition Duration

### Moonwalking (Wrong Direction)
**Fix:** Check blend tree positions match expected directions. Use position 1 instead of 0.5.

### Animation Plays Too Slow
**Fix:** Check the Speed column in blend tree (should be 1, not 0.01 or -1)

### Character Doesn't Turn
**Fix:** Check ApplyRotation() in PlayerController is using GetTargetRotation() when not aiming

---

## Next Steps

- [ ] Add running animation to locomotion blend tree
- [ ] Add jump animations
- [ ] Add weapon/combat animations with Animation Layers
- [ ] Add Animation Events for footsteps, effects

---

## Resources

- Unity Manual: [Animator Controller](https://docs.unity3d.com/Manual/class-AnimatorController.html)
- Unity Manual: [Blend Trees](https://docs.unity3d.com/Manual/class-BlendTree.html)
- Mixamo: [Free Animations](https://www.mixamo.com/)
