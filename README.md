# Producer Tour Unity

Rust-style multiplayer survival game built with Unity.

## Quick Start

### 1. Create Unity Project

1. Open **Unity Hub**
2. Click **New Project**
3. Select **3D (URP)** template
4. Set location to this folder: `/Users/nolangriffis/Documents/producer-tour-unity`
5. Unity will merge with existing Assets folder

### 2. Install Required Packages

Open **Window > Package Manager** and install:

```
From Unity Registry:
- Input System (com.unity.inputsystem)
- Cinemachine (com.unity.cinemachine)

From Git URL (Add package from git URL):
- glTFast: https://github.com/atteneder/glTFast.git
```

After installing Input System, Unity will ask to restart - click **Yes**.

### 3. Migrate Assets from Three.js Project

Run the migration script:

```bash
cd /Users/nolangriffis/Documents/producer-tour-unity
./scripts/migrate_assets.sh
```

This copies:
- XBot character model
- 85 animation files (locomotion, rifle, pistol)
- Weapon models with textures
- Environment assets (trees, rocks, props)
- Terrain textures

### 4. Configure XBot for Humanoid

1. In Project window, find `Assets/Art/Models/Characters/xbot.glb`
2. In Inspector, click **Rig** tab
3. Set **Animation Type** to **Humanoid**
4. Click **Configure** to verify bone mapping
5. Click **Apply**

### 5. Create Test Scene

1. **File > New Scene** (Basic URP)
2. Save as `Assets/_Project/Scenes/Game.unity`
3. Add empty GameObject named **Managers**
4. Add components:
   - GameManager
   - ChunkManager

### 6. Create Player Prefab

1. Drag `xbot.glb` into scene
2. Add components:
   - Character Controller (Height: 2, Radius: 0.3)
   - PlayerController
   - PlayerCamera (attach to Main Camera)
   - PlayerAnimation
   - PlayerHealth
   - Player Input (assign PlayerInputActions asset)
3. Create prefab: drag to `Assets/_Project/Prefabs/Player/`

### 7. Play!

Press **Play** - you should have:
- Procedural terrain generating around you
- WASD movement
- Mouse look
- Sprinting (Shift)
- Crouching (C or Ctrl)
- Jumping (Space)

---

## Project Structure

```
Assets/
├── _Project/           # Game code
│   ├── Scripts/        # C# scripts
│   ├── Prefabs/        # Prefabs
│   ├── Scenes/         # Scenes
│   └── Settings/       # Input, rendering
│
├── Art/                # Imported assets
│   ├── Models/         # GLB/FBX models
│   ├── Textures/       # PNG/JPG textures
│   ├── Animations/     # Animation clips
│   └── Materials/      # Unity materials
│
└── Plugins/            # Third-party packages
```

## Controls

| Action | Key |
|--------|-----|
| Move | WASD |
| Look | Mouse |
| Sprint | Shift (hold) |
| Crouch | C or Ctrl |
| Jump | Space |
| Fire | Left Mouse |
| Aim | Right Mouse |
| Reload | R |
| Interact | E |
| Inventory | Tab |
| Hotbar | 1-6 |

## Core Scripts

| Script | Purpose |
|--------|---------|
| `GameManager` | Singleton, game state, scene management |
| `PlayerController` | Movement, jumping, crouching |
| `PlayerCamera` | Third-person camera with collision |
| `PlayerAnimation` | Animator parameter management |
| `PlayerHealth` | Health, hunger, thirst, stamina |
| `ChunkManager` | Chunk loading/unloading |
| `Chunk` | Individual terrain mesh + decorations |
| `TerrainGenerator` | Procedural height/biome generation |
| `WeaponBase` | Base weapon class |
| `ObjectPool` | Generic object pooling |

## Next Steps

- [ ] Set up Animator Controller with blend trees
- [ ] Implement networking (Mirror or Fish-Net)
- [ ] Add inventory system
- [ ] Implement building system
- [ ] Add AI enemies
- [ ] Polish terrain texturing (splatmap)
- [ ] Add audio system

---

See [ARCHITECTURE.md](ARCHITECTURE.md) for detailed technical documentation.
