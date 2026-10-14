# Project Directory Architecture - SteamRush

The `_Project` directory adheres to Unity Game Development standards and team conventions:

```
Assets/
└── _Project/
    ├── Scripts/              # All C# source code for the project
    │   ├── Core/             # Shared foundational systems (GameManager, EventBus, Audio, Save/Load, etc.)
    │   └── Features/         # Modular business logic features (Runner, RelayQueue, Obstacles, Track, UI, StreamIntegration, etc.)
    ├── Scenes/               # All Unity Scenes
    │   ├── Main/             # Primary additive scenes (Core_Scene, Gameplay_Scene, UI_Scene)
    │   ├── Phuoc/            # Individual test / prototype scenes for Phuoc
    │   ├── MinhHuy/          # Individual test / prototype scenes for Minh Huy
    │   ├── NguyenHuy/        # Individual test / prototype scenes for Nguyen Huy
    │   ├── Truong/           # Individual test / prototype scenes for Truong
    │   └── Tu/               # Individual test / prototype scenes for Tu
    ├── Prefabs/              # Prefabs & Prefab Variants (Runner, Baton, Obstacles, Buffs, TrackTiles, etc.)
    ├── Models/               # 3D Models (.fbx, .obj, .blend) for Runner, Arena, 3D Props
    ├── Animations/           # Animator Controllers, Animation Clips, Avatar Masks (Run, Sprint, Jump, Slide, etc.)
    ├── Materials/            # Low-poly materials, URP Shaders, Color Palettes
    ├── Textures/             # Texture maps, Color Palettes, Gift icons (Rose, Donut, Shield, etc.)
    ├── VFX/                  # Visual effects, Particle Systems, Trail Renderers
    ├── Audio/                # Project Audio
    │   ├── BGM/              # Marathon background music, high-tempo Sprint tracks
    │   └── SFX/              # Footsteps, jumping, sliding, impacts, gift notifications, baton swing
    └── UI/                   # UI Canvas Prefabs, TextMeshPro fonts, UI Sprites
```

### Development Guidelines:
1. **Member Scenes**: Used strictly for isolated sandbox testing. Never include them in official Build Settings.
2. **Avoid Shared Scene Edits**: Utilize additive scene workflows in `Scenes/Main/` and build features via Prefabs to avoid merge conflicts.
3. **C# Namespaces**: Follow the pattern `SteamRush.<System>.<FeatureName>`.
   - **Shared Systems (Core)**:
     - `SteamRush.Core`: GameManager, EventBus, ServiceLocator, SceneLoader.
     - `SteamRush.Core.Pooling`: ObjectPool, PoolableObject.
   - **Feature-Specific (Features)**:
     - `SteamRush.Features.Runner`: RunnerController, RunnerMovement, StumbleHandler.
     - `SteamRush.Features.Relay`: RelayQueueManager, BatonHandoverController.
     - `SteamRush.Features.Obstacles`: ObstacleBase, DynamicObstacle, SingleObstacleSpawner.
     - `SteamRush.Features.Gifts`: IGiftAction, GiftActionRegistry, GiftManager.
     - `SteamRush.Features.StreamIntegration`: TikTokLiveClient, TikTokGiftRouter, FactionTugOfWarManager.
     - `SteamRush.Features.UI`: GlobalProgressBar, EnergyGaugeUI, RunnerNameplateController, VictoryScreen.
     - `SteamRush.Features.Track`: TrackProgressTracker, TrackTileLooper, FinishLineArchway.
   - **Personal Prototype Namespaces**:
     - Pattern: `SteamRush.<MemberName>.<Feature>`
     - Examples: `SteamRush.Phuoc.RunnerPrototype`, `SteamRush.MinhHuy.GiftTest`, `SteamRush.Truong.TrackTest`.

### C# Coding Conventions:

```csharp
namespace SteamRush.Features.Runner
{
    using UnityEngine;
    using SteamRush.Core;

    public class RunnerController : MonoBehaviour
    {
        // 1. SerializeField variables: _camelCase
        [SerializeField] private float _baseMoveSpeed = 8.0f;
        [SerializeField] private float _sprintMultiplier = 1.5f;

        // 2. Private internal variables: _camelCase
        private bool _isSprinting;
        private float _currentSpeed;

        // 3. Public Properties: PascalCase
        public float CurrentSpeed => _currentSpeed;
        public bool IsSprinting => _isSprinting;

        // 4. Methods: PascalCase
        private void Awake()
        {
            _currentSpeed = _baseMoveSpeed;
        }

        public void SetSprint(bool enable)
        {
            _isSprinting = enable;
            _currentSpeed = enable ? _baseMoveSpeed * _sprintMultiplier : _baseMoveSpeed;
        }
    }
}
```
