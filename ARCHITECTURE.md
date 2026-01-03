# Producer Tour Unity - Architecture Document

## Rust-Style Survival Game Architecture

This document outlines the complete architecture for rebuilding Producer Tour as a Unity-based multiplayer survival game inspired by Rust (Facepunch).

---

## 1. Technology Stack

| Layer | Technology | Purpose |
|-------|-----------|---------|
| **Engine** | Unity 2022.3 LTS (or 6000.x) | Game runtime |
| **Rendering** | URP (Universal Render Pipeline) | PBR materials, performance |
| **Physics** | Unity Physics | Collisions, raycasts |
| **Networking** | Mirror or Fish-Net | Rust-style authoritative server |
| **Terrain** | Custom Procedural (not Unity Terrain) | Chunk-based, streamable |
| **Audio** | FMOD or Unity Audio | 3D spatial audio |

---

## 2. Project Structure

```
Assets/
├── _Project/                    # Our game code (prefixed to sort first)
│   ├── Scenes/
│   │   ├── Bootstrap.unity      # Entry point - loads systems
│   │   ├── MainMenu.unity       # UI scene
│   │   └── Game.unity           # Main gameplay scene
│   │
│   ├── Scripts/
│   │   ├── Core/                # Singletons, managers
│   │   │   ├── GameManager.cs
│   │   │   ├── NetworkManager.cs
│   │   │   └── SaveManager.cs
│   │   │
│   │   ├── Player/              # Player systems
│   │   │   ├── PlayerController.cs
│   │   │   ├── PlayerInput.cs
│   │   │   ├── PlayerCamera.cs
│   │   │   ├── PlayerInventory.cs
│   │   │   ├── PlayerHealth.cs
│   │   │   └── PlayerAnimation.cs
│   │   │
│   │   ├── World/               # World generation
│   │   │   ├── WorldGenerator.cs
│   │   │   ├── ChunkManager.cs
│   │   │   ├── Chunk.cs
│   │   │   ├── BiomeData.cs
│   │   │   └── TerrainMeshBuilder.cs
│   │   │
│   │   ├── Combat/              # Weapons, damage
│   │   │   ├── WeaponBase.cs
│   │   │   ├── ProjectileWeapon.cs
│   │   │   ├── MeleeWeapon.cs
│   │   │   ├── DamageSystem.cs
│   │   │   └── Hitbox.cs
│   │   │
│   │   ├── Building/            # Base building (Rust-style)
│   │   │   ├── BuildingManager.cs
│   │   │   ├── BuildingPiece.cs
│   │   │   ├── SocketSystem.cs
│   │   │   └── ToolCupboard.cs
│   │   │
│   │   ├── Items/               # Item system
│   │   │   ├── ItemDatabase.cs
│   │   │   ├── ItemDefinition.cs
│   │   │   ├── ItemInstance.cs
│   │   │   └── LootTable.cs
│   │   │
│   │   ├── AI/                  # NPCs, animals
│   │   │   ├── AIController.cs
│   │   │   ├── AIState.cs
│   │   │   └── Bandit.cs
│   │   │
│   │   ├── Network/             # Multiplayer
│   │   │   ├── NetworkPlayer.cs
│   │   │   ├── NetworkEntity.cs
│   │   │   ├── ChunkSubscription.cs
│   │   │   └── ServerAuthority.cs
│   │   │
│   │   ├── UI/                  # User interface
│   │   │   ├── HUD/
│   │   │   ├── Inventory/
│   │   │   ├── Crafting/
│   │   │   └── ServerBrowser/
│   │   │
│   │   └── Utilities/           # Helpers
│   │       ├── ObjectPool.cs
│   │       ├── Noise.cs
│   │       └── Extensions.cs
│   │
│   ├── Prefabs/
│   │   ├── Player/
│   │   │   └── Player.prefab
│   │   ├── Weapons/
│   │   ├── Items/
│   │   ├── Building/
│   │   ├── Environment/
│   │   └── UI/
│   │
│   ├── ScriptableObjects/       # Data assets
│   │   ├── Items/
│   │   ├── Weapons/
│   │   ├── Biomes/
│   │   └── Config/
│   │
│   └── Settings/
│       ├── Input/
│       │   └── PlayerInputActions.inputactions
│       └── Rendering/
│           └── URP_Settings.asset
│
├── Art/                         # Imported from Three.js project
│   ├── Models/
│   │   ├── Characters/
│   │   │   ├── base_male.fbx
│   │   │   ├── base_female.fbx
│   │   │   └── Hair/
│   │   ├── Weapons/
│   │   │   ├── Pistol/
│   │   │   └── Rifle/
│   │   ├── Environment/
│   │   │   ├── Trees/
│   │   │   ├── Rocks/
│   │   │   └── Props/
│   │   └── NPCs/
│   │       └── Bandit/
│   │
│   ├── Textures/
│   │   ├── Terrain/
│   │   ├── Characters/
│   │   ├── Weapons/
│   │   └── Environment/
│   │
│   ├── Animations/
│   │   ├── Locomotion/
│   │   ├── Combat/
│   │   └── Emotes/
│   │
│   ├── Materials/               # Unity materials (auto-generated + custom)
│   │
│   └── Audio/
│       ├── SFX/
│       ├── Music/
│       └── Ambience/
│
├── Plugins/                     # Third-party
│   └── Mirror/                  # Networking (from Asset Store)
│
└── Resources/                   # Runtime-loaded assets
    └── (use sparingly - prefer Addressables)
```

---

## 3. Core Architecture Pattern

### 3.1 Bootstrap Flow

```
Bootstrap.unity
    │
    ├── GameManager (singleton)
    │   ├── Initialize core systems
    │   ├── Load player prefs
    │   └── Load MainMenu or Game scene
    │
    └── DontDestroyOnLoad objects:
        ├── GameManager
        ├── NetworkManager
        ├── AudioManager
        └── InputManager
```

### 3.2 Scene Structure (Game.unity)

```
Game Scene Hierarchy:
│
├── --- MANAGERS ---
│   ├── WorldManager
│   ├── ChunkManager
│   └── CombatManager
│
├── --- ENVIRONMENT ---
│   ├── Lighting
│   │   ├── Directional Light (Sun)
│   │   └── Reflection Probes
│   ├── Sky
│   │   └── Skybox Volume
│   └── Chunks (generated at runtime)
│       ├── Chunk_0_0
│       ├── Chunk_1_0
│       └── ...
│
├── --- PLAYERS ---
│   └── (spawned at runtime)
│
├── --- ENTITIES ---
│   ├── NPCs
│   ├── Animals
│   └── Loot
│
├── --- UI ---
│   └── GameCanvas
│       ├── HUD
│       ├── Inventory
│       └── DeathScreen
│
└── --- CAMERAS ---
    └── PlayerCamera (follows local player)
```

---

## 4. Procedural Terrain System

### 4.1 Chunk-Based World (Like Rust)

```
World Size: 4km x 4km (configurable)
Chunk Size: 64m x 64m
Total Chunks: 64 x 64 = 4,096 chunks
Loaded at once: ~49 (7x7 around player)

┌─────┬─────┬─────┬─────┬─────┬─────┬─────┐
│     │     │     │     │     │     │     │
├─────┼─────┼─────┼─────┼─────┼─────┼─────┤
│     │     │ LOD │ LOD │ LOD │     │     │
├─────┼─────┼─────┼─────┼─────┼─────┼─────┤
│     │ LOD │ HI  │ HI  │ HI  │ LOD │     │
├─────┼─────┼─────┼─────┼─────┼─────┼─────┤
│     │ LOD │ HI  │ [P] │ HI  │ LOD │     │  [P] = Player
├─────┼─────┼─────┼─────┼─────┼─────┼─────┤
│     │ LOD │ HI  │ HI  │ HI  │ LOD │     │
├─────┼─────┼─────┼─────┼─────┼─────┼─────┤
│     │     │ LOD │ LOD │ LOD │     │     │
├─────┼─────┼─────┼─────┼─────┼─────┼─────┤
│     │     │     │     │     │     │     │
└─────┴─────┴─────┴─────┴─────┴─────┴─────┘
```

### 4.2 Terrain Generation Pipeline

```
Seed (int)
    │
    ▼
[Noise Generation]
    ├── Perlin Noise (base height)
    ├── Simplex Noise (detail)
    ├── Voronoi Noise (biome regions)
    └── Ridged Noise (mountains)
    │
    ▼
[Heightmap] (float[65,65] per chunk)
    │
    ▼
[Biome Assignment]
    ├── Beach (height < 5m)
    ├── Grassland (5-50m)
    ├── Forest (50-100m)
    ├── Mountain (100-200m)
    └── Snow (>200m)
    │
    ▼
[Mesh Generation]
    ├── Vertices from heightmap
    ├── UVs for texture splatting
    └── Normals for lighting
    │
    ▼
[Decoration]
    ├── Trees (GPU instancing)
    ├── Rocks (GPU instancing)
    ├── Grass (GPU instancing)
    └── Props
```

### 4.3 Key Classes

```csharp
// ChunkManager.cs - Manages chunk loading/unloading
public class ChunkManager : MonoBehaviour
{
    public int viewDistance = 3;           // Chunks in each direction
    public int chunkSize = 64;             // Meters

    private Dictionary<Vector2Int, Chunk> loadedChunks;
    private Queue<Vector2Int> loadQueue;

    void Update()
    {
        UpdatePlayerChunk();
        ProcessLoadQueue();
        UnloadDistantChunks();
    }
}

// Chunk.cs - Individual terrain chunk
public class Chunk : MonoBehaviour
{
    public Vector2Int coord;
    public Mesh terrainMesh;
    public MeshCollider meshCollider;
    public List<GameObject> decorations;

    public void Generate(int seed) { }
    public void SetLOD(int level) { }
}
```

---

## 5. Player System

### 5.1 Component Architecture

```
Player (Prefab)
│
├── PlayerController.cs      # Movement, grounding
├── PlayerInput.cs           # New Input System wrapper
├── PlayerCamera.cs          # Third-person/First-person
├── PlayerInventory.cs       # Items, equipment
├── PlayerHealth.cs          # Health, hunger, thirst
├── PlayerAnimation.cs       # Animator controller
├── PlayerBuild.cs           # Building system
│
├── CharacterController      # Unity component
├── Animator                 # Unity component
│
└── [Children]
    ├── CameraRig
    │   ├── CameraPivot
    │   └── Main Camera
    ├── Model
    │   └── Armature + Mesh
    ├── WeaponSocket
    │   └── (equipped weapon)
    └── Hitboxes
        ├── Head
        ├── Body
        └── Limbs
```

### 5.2 Movement (Rust-Style)

```csharp
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 4f;
    public float sprintSpeed = 7f;
    public float crouchSpeed = 2f;
    public float jumpForce = 5f;

    [Header("Camera")]
    public float mouseSensitivity = 2f;
    public float minPitch = -80f;
    public float maxPitch = 80f;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;
    private bool isCrouching;
    private bool isSprinting;

    void Update()
    {
        HandleGrounding();
        HandleMovement();
        HandleJump();
        ApplyGravity();
    }
}
```

---

## 6. Animation System

### 6.1 Animator Controller Structure

```
Base Layer (Locomotion)
├── Idle
├── Walk (BlendTree: 8-direction)
├── Run (BlendTree: 8-direction)
├── Crouch_Idle
├── Crouch_Walk (BlendTree)
├── Jump
├── Fall
└── Land

Upper Body Layer (Override)
├── Empty (lower body only)
├── Rifle_Idle
├── Rifle_Aim
├── Rifle_Fire
├── Rifle_Reload
├── Pistol_Idle
├── Pistol_Aim
├── Pistol_Fire
└── Pistol_Reload

Action Layer (Override)
├── Empty
├── Emote_Wave
├── Emote_Dance
└── Death
```

### 6.2 Animation Parameters

| Parameter | Type | Purpose |
|-----------|------|---------|
| `Speed` | float | Blend walk/run |
| `MoveX` | float | Strafe direction |
| `MoveZ` | float | Forward/back |
| `IsGrounded` | bool | Air/ground state |
| `IsCrouching` | bool | Crouch state |
| `IsSprinting` | bool | Sprint state |
| `WeaponType` | int | 0=Unarmed, 1=Rifle, 2=Pistol |
| `IsAiming` | bool | ADS state |
| `Fire` | trigger | Shoot animation |
| `Reload` | trigger | Reload animation |

---

## 7. Networking Architecture (Rust-Style)

### 7.1 Authority Model

```
┌─────────────────────────────────────────────────────────┐
│                    DEDICATED SERVER                      │
│  ┌─────────────────────────────────────────────────┐    │
│  │              Authoritative State                 │    │
│  │  - Player positions (validated)                  │    │
│  │  - Health/damage calculations                    │    │
│  │  - Loot spawns                                   │    │
│  │  - Building placement                            │    │
│  │  - Chunk state                                   │    │
│  └─────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────┘
         │                    │                    │
         ▼                    ▼                    ▼
    ┌─────────┐          ┌─────────┐          ┌─────────┐
    │ Client1 │          │ Client2 │          │ Client3 │
    │ - Input │          │ - Input │          │ - Input │
    │ - Render│          │ - Render│          │ - Render│
    │ - Predict│          │ - Predict│          │ - Predict│
    └─────────┘          └─────────┘          └─────────┘
```

### 7.2 Chunk Subscription (Like Your Three.js Implementation)

```csharp
// Only send updates for chunks near the player
public class ChunkSubscription
{
    public void OnPlayerMove(Vector3 position)
    {
        Vector2Int currentChunk = WorldToChunk(position);

        // Subscribe to nearby chunks
        for (int x = -viewDistance; x <= viewDistance; x++)
        for (int z = -viewDistance; z <= viewDistance; z++)
        {
            Subscribe(currentChunk + new Vector2Int(x, z));
        }

        // Unsubscribe from distant chunks
        UnsubscribeDistant(currentChunk, viewDistance + 1);
    }
}
```

---

## 8. Asset Import Pipeline

### 8.1 From Three.js Project

| Source | Destination | Import Settings |
|--------|-------------|-----------------|
| `public/models/*.glb` | `Art/Models/` | Scale: 1, Generate Colliders: Off |
| `public/textures/*.png` | `Art/Textures/` | sRGB for albedo, Linear for normal |
| `public/animations/*.glb` | `Art/Animations/` | Rig: Humanoid, Loop: check |

### 8.2 Required Unity Packages

```json
{
  "dependencies": {
    "com.unity.render-pipelines.universal": "14.0.x",
    "com.unity.inputsystem": "1.7.x",
    "com.unity.cinemachine": "2.9.x",
    "com.unity.addressables": "1.21.x",
    "com.atteneder.gltfast": "6.0.x"
  }
}
```

### 8.3 Model Import Checklist

```
For Characters:
☐ Animation Type: Humanoid
☐ Avatar Definition: Create From This Model
☐ Root node: Hips
☐ Extract materials: Yes
☐ Import BlendShapes: Yes (for customization)

For Environment:
☐ Animation Type: None
☐ Generate Colliders: Yes (for trees/rocks)
☐ Read/Write: Off (for performance)
☐ Optimize Mesh: Yes

For Weapons:
☐ Animation Type: None
☐ Generate Colliders: Off
☐ Scale Factor: 1
```

---

## 9. Performance Targets

| Metric | Target | How |
|--------|--------|-----|
| FPS | 60+ | LOD, culling, batching |
| Draw Calls | <500 | GPU instancing for foliage |
| Triangles | <2M visible | LOD system |
| Memory | <4GB | Chunk streaming |
| Load Time | <10s | Async loading |

### 9.1 Optimization Techniques

1. **GPU Instancing** - Trees, rocks, grass
2. **LOD Groups** - 3 levels per asset
3. **Occlusion Culling** - Baked + dynamic
4. **Chunk Streaming** - Load/unload based on distance
5. **Object Pooling** - Projectiles, particles
6. **Texture Atlasing** - Terrain materials
7. **Static Batching** - Non-moving objects

---

## 10. Development Phases

### Phase 1: Foundation
- [ ] Create Unity project with URP
- [ ] Import core assets (character, animations)
- [ ] Basic player controller with movement
- [ ] Camera system (third-person)
- [ ] Input system setup

### Phase 2: World
- [ ] Procedural terrain generation
- [ ] Chunk loading/unloading
- [ ] Basic biome system
- [ ] Tree/rock placement

### Phase 3: Player Systems
- [ ] Full animation controller
- [ ] Inventory system
- [ ] Equipment/weapons
- [ ] Health/hunger/thirst

### Phase 4: Combat
- [ ] Weapon system
- [ ] Damage/hitboxes
- [ ] AI enemies
- [ ] Death/respawn

### Phase 5: Building
- [ ] Socket-based building
- [ ] Tool cupboard
- [ ] Building decay

### Phase 6: Multiplayer
- [ ] Mirror/Fish-Net integration
- [ ] Server authority
- [ ] Player sync
- [ ] Chunk subscription

---

## 11. Next Steps

1. **Delete** the broken Unity projects
2. **Create** new Unity 2022.3 LTS project with URP
3. **Install** required packages (Input System, Cinemachine, glTFast)
4. **Import** character model + locomotion animations
5. **Build** basic player controller
6. **Test** movement before adding complexity

Ready to proceed?
