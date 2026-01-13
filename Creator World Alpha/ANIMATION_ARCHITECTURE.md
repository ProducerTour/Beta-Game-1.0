# Animation Architecture - Creator World Alpha

> **CRITICAL**: This document defines the source of truth for animation system integration.
> Any changes to blend trees, thresholds, or animation parameters MUST follow these rules.

## Core Principle

**Animations are purely visual. They do NOT drive position.**

The CharacterController handles all movement. Animations only reflect what the movement system is doing.

---

## Blend Tree Parameter System

### The Golden Rule

> **Blend tree positions and code thresholds MUST match exactly.**

If the blend tree has Walk Forward at position `(0, 0.5)`, the code MUST output `MoveZ = 0.5` when walking at full speed.

### Current Blend Tree Positions (PlayerAnimator.controller)

#### Locomotion Blend Tree (2D Freeform Cartesian)

> **IMPORTANT**: Use Freeform Cartesian (not Directional) for cross/grid position layouts.
> Freeform Directional uses angular proximity and causes oscillation with cross patterns.
| Animation | Position (X, Y) | When to Reach |
|-----------|-----------------|---------------|
| Idle | (0, 0) | Speed = 0 |
| Walk Forward | (0, 0.5) | Speed = WalkSpeed |
| Run Forward | (0, 1.0) | Speed = RunSpeed or higher |
| Walk Left | (-0.5, 0) | Strafe left (aiming mode) |
| Walk Right | (0.5, 0) | Strafe right (aiming mode) |
| Run Left | (-1.0, 0) | Fast strafe left (aiming mode) |
| Run Right | (1.0, 0) | Fast strafe right (aiming mode) |

#### Crouch Blend Tree (2D Freeform Directional)
| Animation | Position (X, Y) | When to Reach |
|-----------|-----------------|---------------|
| Crouch Idle | (0, 0) | Speed = 0 |
| Crouch Walk Forward | (0, 0.5) | Speed = CrouchSpeed |
| Crouch Walk Backward | (0, -0.5) | Moving backward (aiming) |
| Crouch Strafe Left | (-0.5, 0) | Strafe left |
| Crouch Strafe Right | (0.5, 0) | Strafe right |

### Required Code Thresholds (BlendTreeConfig.cs)

```csharp
IdleThreshold = 0f;      // Matches Idle at (0, 0)
WalkThreshold = 0.5f;    // Matches Walk Forward at (0, 0.5)
RunThreshold = 1.0f;     // Matches Run Forward at (0, 1.0)
SprintThreshold = 1.0f;  // No separate sprint animation - uses Run
```

**WARNING**: If you change blend tree positions, you MUST update these thresholds.

---

## Movement Mode

The animation system uses **8-way locomotion** where the character always faces the camera direction.

### 8-Way Locomotion (Always Active)
- Character **always faces camera direction**
- True strafing when moving sideways
- `MoveX` = local velocity X component (-1 to 1) - strafe direction
- `MoveZ` = local velocity Z component (-1 to 1) - forward/backward

```
Input → Character faces camera → Strafe/forward animations blend based on velocity
```

### How It Works
- Moving forward (W): MoveZ positive → Forward animation
- Moving backward (S): MoveZ negative → (needs backward animation)
- Moving left (A): MoveX negative → Left strafe animation
- Moving right (D): MoveX positive → Right strafe animation
- Diagonal (W+A): Blend of forward + left strafe

---

## Smoothing Rules

### THE RULE: Single Smoothing Only

> **Use EITHER manual Lerp OR animator damping. NEVER BOTH.**

Double smoothing causes:
- Animation oscillation (ping-pong effect)
- Sluggish response
- Parameters overshooting and bouncing back

### Correct Implementation (PlayerAnimation.cs)

```csharp
// CORRECT: Single smoothing via animator damping
animator.SetFloat(AnimatorHashes.MoveX, targetValue, dampTime, Time.deltaTime);
animator.SetFloat(AnimatorHashes.MoveZ, targetValue, dampTime, Time.deltaTime);
```

### WRONG Implementation (DO NOT DO THIS)

```csharp
// WRONG: Double smoothing - causes oscillation
smoothValue = Mathf.Lerp(smoothValue, targetValue, Time.deltaTime * speed);  // First smooth
animator.SetFloat(hash, smoothValue, dampTime, Time.deltaTime);              // Second smooth
```

### Recommended Damp Time Values
- `0.1f` - Responsive, good for fast-paced gameplay
- `0.15f` - Slightly smoother transitions
- `0.05f` - Near-instant, may look snappy

---

## Animation Import Settings (FBX)

### Critical Loop Settings for Humanoid Animations

> **This is the fix that resolved the main ping-pong oscillation issue.**

When importing Mixamo or other humanoid animations, the FBX import settings can cause orientation/position blending at loop points, which makes the character oscillate left-to-right during locomotion.

### Required Settings (in .fbx.meta files)

```yaml
loopBlendOrientation: 0      # DO NOT blend orientation at loop points
loopBlendPositionXZ: 0       # DO NOT blend XZ position at loop points
keepOriginalOrientation: 1   # Keep the animation's original orientation
loopBlendPositionY: 1        # OK to blend Y position (for foot grounding)
keepOriginalPositionY: 1     # Keep original Y position
keepOriginalPositionXZ: 0    # Allow XZ to be controlled by CharacterController
```

### How to Set in Unity Inspector

1. Select the FBX file in Project window
2. Go to **Animation** tab
3. Select the animation clip
4. Under **Root Transform Rotation**:
   - **Bake Into Pose**: Checked
   - **Based Upon**: Original
5. Under **Root Transform Position (XZ)**:
   - **Bake Into Pose**: Checked
6. Click **Apply**

### Why This Matters

When `loopBlendOrientation: 1`, Unity tries to smoothly blend the character's rotation between the end and start of the loop. For locomotion animations that have subtle hip rotation during the gait cycle, this causes:

1. End of loop: Character facing slightly left
2. Start of loop: Character facing slightly right
3. Unity blends between them → oscillation

Setting `loopBlendOrientation: 0` and `keepOriginalOrientation: 1` tells Unity to NOT blend rotation at loop points, eliminating the oscillation.

### Files That Need This Fix

All locomotion animations should have these settings:
- `Unarmed_Idle.fbx`
- `Unarmed_Walk_Forward.fbx`
- `Unarmed_Run_Forward.fbx`
- `Unarmed_Walk_Left.fbx` / `Right.fbx`
- `Unarmed_Run_Left.fbx` / `Right.fbx`
- Any other looping locomotion clips

---

## NormalizedSpeed Calculation

The `NormalizedSpeed` property in `MovementHandler.cs` maps physical speed to blend tree positions:

```
Physical Speed          → NormalizedSpeed (MoveZ)
─────────────────────────────────────────────────
0 m/s                   → 0.0   (Idle)
WalkSpeed (2.5 m/s)     → 0.5   (Walk Forward)
RunSpeed (4.0 m/s)      → 1.0   (Run Forward)
SprintSpeed (5.0 m/s)   → 1.0   (Run Forward - no separate animation)
```

The mapping is linear interpolation between these points.

---

## File Responsibilities

| File | Responsibility |
|------|----------------|
| `BlendTreeConfig.cs` | Threshold values that MUST match blend tree positions |
| `BlendTreeConfig.asset` | Instance with actual threshold values |
| `PlayerAnimation.cs` | Calculates MoveX/MoveZ and applies to animator |
| `MovementHandler.cs` | Calculates NormalizedSpeed from physical speed |
| `PlayerAnimator.controller` | Blend tree with animation positions |

---

## Common Mistakes and Fixes

### Mistake 1: Animation Speed Doesn't Match Movement

**Symptom**: Character slides/floats, feet don't match ground speed

**Cause**: Threshold/position mismatch

**Fix**: Ensure `BlendTreeConfig` thresholds match `PlayerAnimator.controller` blend tree Y positions

### Mistake 2: Ping-Pong / Oscillation

**Symptom**: Character rapidly switches between left and right animations

**Cause**:
1. Wrong blend tree type for position layout
2. Double smoothing
3. Using velocity-based direction in non-aiming mode

**Fix**:
1. Use **Freeform Cartesian** (not Directional) for cross/grid blend positions
2. Remove manual Lerp, use only animator damping
3. In non-aiming mode, set `MoveX = 0` (no strafing)

**Blend Type Guide:**
- `Freeform Directional`: For circular motion layouts (e.g., 8-way run in a circle)
- `Freeform Cartesian`: For cross/grid layouts (forward/strafe axes) ← **USE THIS**
- `Simple Directional`: When motions represent different directions, not speeds

### Mistake 3: Sluggish Animation Response

**Symptom**: Animation lags behind movement

**Cause**: Excessive damping or double smoothing

**Fix**: Reduce `parameterDampTime` to 0.1f or lower

### Mistake 4: Moonwalking

**Symptom**: Character faces one direction but animates walking another

**Cause**: Not converting world velocity to local space

**Fix**: Use `transform.InverseTransformDirection(worldVelocity)` for aiming mode

---

## Modifying the System

### To Add a New Speed Tier (e.g., Jog)

1. Add animation to blend tree at specific Y position (e.g., 0.75 for jog)
2. Add threshold to `BlendTreeConfig.cs` (e.g., `JogThreshold = 0.75f`)
3. Update asset file with new value
4. Modify `GetNormalizedSpeed()` to interpolate through new tier

### To Add Backward Movement

1. Add Walk Backward animation to blend tree at (0, -0.5)
2. Add Run Backward animation to blend tree at (0, -1.0)
3. Modify `GetForwardMovementDirection()` to return negative MoveZ when moving backward

### To Change Blend Tree Type

If changing from 2D Freeform Directional:
- 1D blend: Only needs Speed parameter
- 2D Simple Directional: Positions must be evenly distributed
- 2D Freeform Cartesian: Can have irregular positions

---

## Debugging

### Enable Animation Debug Info

In Unity Editor, select the Player object and check the Animator window to see:
- Current state
- Parameter values
- Blend tree weights

### Console Debug

Call `playerAnimation.GetDebugInfo()` to log current values:
```
Speed: 0.50, Move: (0.00, 0.50), Grounded: True
```

### Common Debug Checks

1. **MoveX oscillating?** → Check if aiming mode is flickering or double smoothing exists
2. **MoveZ not reaching 0.5/1.0?** → Check threshold configuration
3. **Blend tree showing wrong animation?** → Check blend tree positions in Animator window

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        INPUT SYSTEM                              │
│                    (IInputService.MoveInput)                     │
└─────────────────────────────┬───────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      MOVEMENT HANDLER                            │
│  • Calculates currentSpeed from input + config                   │
│  • NormalizedSpeed maps to blend tree positions                  │
│  • Returns movement direction for CharacterController            │
└─────────────────────────────┬───────────────────────────────────┘
                              │
              ┌───────────────┴───────────────┐
              │                               │
              ▼                               ▼
┌─────────────────────────┐     ┌─────────────────────────────────┐
│   CHARACTER CONTROLLER   │     │         PLAYER ANIMATION         │
│  • Applies actual move   │     │  • Gets NormalizedSpeed          │
│  • Handles collision     │     │  • Calculates MoveX/MoveZ        │
│  • Positions character   │     │  • Applies to animator           │
└─────────────────────────┘     └───────────────┬─────────────────┘
                                                │
                                                ▼
                              ┌─────────────────────────────────────┐
                              │            ANIMATOR                  │
                              │  • Blend tree interpolates          │
                              │  • Plays appropriate animation      │
                              │  • VISUAL ONLY - no position change │
                              └─────────────────────────────────────┘
```

---

## Version History

| Date | Change | Author |
|------|--------|--------|
| 2026-01-06 | Initial architecture document | Claude |
| 2026-01-06 | Fixed threshold/position mismatch (0.35→0.5, 0.7→1.0) | Claude |
| 2026-01-06 | Removed double smoothing from PlayerAnimation.cs | Claude |
| 2026-01-06 | Fixed FBX import settings (loopBlendOrientation, keepOriginalOrientation) - **main fix** | Claude |
| 2026-01-06 | Changed to 8-way locomotion - character always faces camera, uses strafe animations | Claude |
