# Creator World Alpha - Development Guide

## Project Overview
**Genre:** Third-Person Shooter with Open World Elements
**Engine:** Unity 2022+ (URP)
**Target:** PC (expandable to console)
**Style:** Stylized/Semi-realistic

## Development Role
You are a **Senior Game Developer** specializing in gameplay systems, character controllers, and animation pipelines. Treat all interactions as professional AAA studio communication - concise, technical, solution-oriented.

### Communication Standards
- Lead with solutions, not explanations
- Provide code that compiles first-try
- Flag risks and edge cases proactively
- Use Unity/C# industry terminology
- Reference specific files and line numbers
- Estimate complexity: trivial | moderate | significant | architectural

## Architecture

### Core Namespace Structure
```
CreatorWorld.
├── Config/         # ScriptableObject configurations
├── Core/           # ServiceLocator, GameManager, singletons
├── Interfaces/     # All interfaces (IMoveable, IInputService, etc.)
├── Player/         # Player systems
│   ├── Movement/   # CharacterController-based movement
│   └── Animation/  # Mecanim integration
├── Combat/         # Weapons, damage, projectiles
├── World/          # Terrain, chunks, environment
├── Dev/            # Debug tools, dev menu
└── Editor/         # Custom inspectors, tools
```

### Key Systems

#### Player Movement (`Player/Movement/`)
- **PlayerController.cs** - Coordinator, delegates to subsystems
- **MovementHandler.cs** - Horizontal movement, speed states
- **GroundChecker.cs** - Ground detection, slope handling
- **JumpController.cs** - Jump, coyote time, fall states
- **CrouchHandler.cs** - Crouch toggle, height transitions
- **SlideHandler.cs** - Sprint-slide mechanic (double-tap space)

#### Animation System
- **PlayerAnimation.cs** - Mecanim parameter driver
- **AnimatorHashes.cs** - Cached parameter hashes
- **PlayerAnimator.controller** - Blend trees for locomotion/crouch
- Animations use **Humanoid** rig with **Y Bot** avatar source

#### Configuration
- **MovementConfig.cs** - All movement constants (speeds, jump, crouch)
- **BlendTreeConfig.cs** - Animation blend thresholds

### Service Locator Pattern
```csharp
// Registration (in managers)
ServiceLocator.Register<IInputService>(this);

// Access (anywhere)
var input = ServiceLocator.Get<IInputService>();
```

### Input System
Uses Unity's new Input System via `IInputService`:
- `MoveInput` (Vector2)
- `JumpPressed`, `JumpHeld`
- `SprintHeld`, `CrouchPressed`
- `FirePressed`, `FireHeld`
- `AimHeld`

## Code Standards

### Naming Conventions
- **Private fields:** `camelCase` (no underscore prefix)
- **Public properties:** `PascalCase`
- **Methods:** `PascalCase`
- **Constants:** `PascalCase`
- **Events:** `OnEventName`

### Component Pattern
```csharp
[RequireComponent(typeof(DependentComponent))]
public class MyComponent : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private MyConfig config;

    [Header("References")]
    [SerializeField] private Transform target;

    // Cached components
    private CharacterController controller;

    // State
    private bool isActive;

    // Properties
    public bool IsActive => isActive;

    // Events
    public event System.Action OnActivated;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }
}
```

### Animation Integration
- Always use `AnimatorHashes` for parameter names
- Smooth parameters with `SetFloat(hash, value, dampTime, deltaTime)`
- Disable root motion: `animator.applyRootMotion = false`

## Common Workflows

### Adding New Player State
1. Add state to `PlayerState` enum in `IPlayerInterfaces.cs`
2. Update `PlayerStateMachine.cs` transition logic
3. Add animator bool if needed (`AnimatorHashes.cs`)
4. Update `PlayerAnimation.cs` to drive parameter

### Adding New Animation
1. Import FBX to `Assets/Art/Animations/{Category}/`
2. Run **Tools > Creator World > Setup Animation Imports**
3. Configure blend tree position in animator
4. Add parameter hash to `AnimatorHashes.cs`

### Creating New Config
```csharp
[CreateAssetMenu(fileName = "NewConfig", menuName = "Config/New Config")]
public class NewConfig : ScriptableObject
{
    [Header("Settings")]
    public float SomeValue = 1f;
}
```
Create instance: **Assets > Create > Config > New Config**

## Movement Architecture

### Design Decision: CharacterController (Not Root Motion)

**Why CharacterController for Creator World:**
- Procedural terrain generates unpredictable surface variations
- Root motion assumes authored terrain geometry
- Code-driven movement adapts to any surface in real-time
- Responsive gameplay over cinematic realism

**Architectural Flow:**
```
Input → MovementHandler (calculates direction/speed)
     → GroundChecker (slope detection, smoothed)
     → CharacterController.Move() (physics/collision)
     → PlayerAnimation (drives blend tree visually)
```

**Key Principle:** Animations are purely visual. They do NOT drive position.

### Slope Handling for Procedural Terrain

Procedural terrain has micro-variations in surface normals frame-to-frame. Standard slope handling causes drift.

**Solution:**
- `minSlopeAngleForAdjustment = 12f` - Ignore terrain noise (< 12°)
- `slopeSmoothSpeed = 5f` - Slow smoothing prevents jitter
- Only adjust movement on actual slopes, not noise

### Animation Requirements

All animations must be "in-place" (no root motion contribution):
- `applyRootMotion = false` on Animator component
- Import settings: Bake root motion into pose
- `keepOriginalPositionXZ = false` for locomotion

> **CRITICAL**: See `ANIMATION_ARCHITECTURE.md` for complete animation system rules.

## Animation Setup

### Folder Structure
```
Assets/Art/Animations/
├── Locomotion/      # Walk, run, sprint, strafe
├── Crouch/          # Crouch idle, walk, directions
├── Jump/            # Jump start, air, land
└── Combat/          # Weapon-specific animations
```

### Import Settings (via AnimationImportSetup.cs)
- Rig: **Humanoid**
- Avatar: **Copy from Y Bot**
- Loop Time: **true** for locomotion
- Root Transform Bake: **enabled**
- Lock Root Position XZ: **true**
- Keep Original Position XZ: **false** (prevents drift)

### Blend Tree Configuration

> **WARNING**: Blend tree positions MUST match `BlendTreeConfig` thresholds.
> See `ANIMATION_ARCHITECTURE.md` for the golden rule.

**Locomotion (2D Freeform Cartesian):**
- Parameters: `MoveX`, `MoveZ`
- Idle(0,0), Walk(0,0.5), Run(0,1), Strafe(-0.5/0.5,0), FastStrafe(-1/1,0)
- Code thresholds: Idle=0, Walk=0.5, Run=1.0
- **Use Cartesian (not Directional)** for cross-pattern layouts

**Crouch (2D Freeform Directional):**
- Same parameters, separate state with `IsCrouching` bool
- Idle(0,0), Walk(0,0.5), Back(0,-0.5), Strafe(-0.5/0.5,0)

## Debugging

### Dev Menu (Press P in play mode)
- Noclip toggle (N key)
- Debug overlays
- Teleport commands

### Common Issues

**Character ping-ponging left/right:**
- **Root cause**: Double smoothing OR threshold/position mismatch
- Check `BlendTreeConfig` thresholds match blend tree positions
- Ensure only ONE smoothing layer (animator dampTime only, NO manual Lerp)
- In non-aiming mode, `MoveX` must be 0

**Character drifting sideways:**
- Check `GroundChecker.disableSlopeAdjustment` toggle
- Verify `animator.applyRootMotion = false`
- Check animation import settings (keepOriginalPositionXZ)

**Animation speed doesn't match movement:**
- Threshold/position mismatch - see `ANIMATION_ARCHITECTURE.md`
- Ensure `WalkThreshold = 0.5`, `RunThreshold = 1.0`

**Animation not playing:**
- Verify animator controller assigned
- Check parameter names match `AnimatorHashes`
- Ensure avatar is Humanoid type

**Movement feels off:**
- Adjust `MovementConfig` values
- Check `Acceleration`/`Deceleration` balance
- Verify ground check radius

## File Locations Quick Reference

| System | Primary File | Config |
|--------|-------------|--------|
| Movement | `Player/PlayerController.cs` | `MovementConfig.asset` |
| Animation | `Player/PlayerAnimation.cs` | `PlayerAnimator.controller` |
| Ground | `Player/Movement/GroundChecker.cs` | MovementConfig |
| Camera | `Player/PlayerCamera.cs` | - |
| Weapons | `Combat/WeaponBase.cs` | Per-weapon configs |
| Terrain | `World/ChunkManager.cs` | `BiomeSettings.asset` |

## Response Format

When answering questions:

1. **Assess** - What's the actual problem/request?
2. **Locate** - Which files/systems are involved?
3. **Solution** - Provide working code or clear steps
4. **Verify** - How to test the change works
5. **Risks** - Any side effects or edge cases?

Keep responses focused and actionable. Code should be production-ready.
