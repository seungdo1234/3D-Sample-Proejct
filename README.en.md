# 3D Sample Project

[한국어](README.md) | **English**

> A top-down 3D survival shooter built on Unity 6.
> More than a single game — this project **designs a reusable in-house game framework and builds the game on top of it.**

<p>
  <img alt="Unity" src="https://img.shields.io/badge/Unity-6000.3-000000?logo=unity">
  <img alt="URP" src="https://img.shields.io/badge/Render-URP%2017.3-blue">
  <img alt="C#" src="https://img.shields.io/badge/C%23-async%2Fawait-239120?logo=csharp">
  <img alt="UniTask" src="https://img.shields.io/badge/Async-UniTask-ff69b4">
  <img alt="Addressables" src="https://img.shields.io/badge/Resource-Addressables-orange">
</p>

<p align="center">
    <img src="docs/games/shooting_survivors.png" alt="Shooting Survivors" width="520">
</p>

<p align="center">
  <a href="https://deff-s.itch.io/shooting_survivors">
    <img alt="Play on itch.io" src="https://img.shields.io/badge/▶%20Play%20Now-itch.io-fa5c5c?logo=itchdotio&logoColor=white">
  </a>
</p>

---


## 🛠 Tech Stack

`Unity 6` · `C#` · `URP` · `UniTask` · `Addressables` · `Cinemachine` ·
`Input System` · `DOTween` · `TextMesh Pro` · `Newtonsoft.Json`

---

## 🧩 Two Layers

The code is split into a **reusable engine layer (`Framework`)** and the **game-specific logic built on top of it (`Game`)**.

> This repository also serves as **a space to accumulate and grow a personal game framework refined across projects.**
> Once a new feature or system is proven, it is absorbed into the `Framework` layer and reused in the next project.
<br>

<details open>
<summary><h3>🧱 Framework — Reusable Engine Layer (expand/collapse)</h3></summary>

> A reusable engine foundation independent of game genre. Abstracts booting · initialization · resources · UI · events · pooling · saving.

### 📂 Structure

```
Assets/_Dev/_Scripts/Framework/
├── Core/           Boot/initialization pipeline (Bootstrapper, GameBase, Initialization/)
├── Addressable/    Resource manager + editor code-generation tools
├── Event/          struct-based event bus
├── Pool/           Addressable object pool
├── Save/           AES encryption + JSON/Newtonsoft save
├── Scene/          Scene transitions (with transition effects)
├── Singleton/      3 singleton base types
└── UI/             Canvas/UI bases + meta-tag-driven auto registration
```

### 🏛 Architecture Highlights

#### ① Consistent Boot Pipeline — [`Bootstrapper`](Assets/_Dev/_Scripts/Framework/Core/Bootstrapper.cs)
- **Auto-collects and instantiates** all persistent prefabs (managers/SDKs) tagged with the `"Bootstrap"` label via Addressables
- Sorts `IBootInitializable` by **Phase → Order**: same step runs **in parallel**, different steps run **sequentially via a barrier**
- **Retry + required/optional failure policy** — a failed required item aborts boot; optional items are skipped
- Caches `EnsureReady()` as a `UniTask?` once → **initializes identically no matter which scene you press Play from**
  (any single scene can run standalone in the editor)

#### ② Per-Scene Game Lifecycle — [`GameBase`](Assets/_Dev/_Scripts/Framework/Core/GameBase.cs)
- A **template-method pattern** —
  `PreInitialize → SpawnSystemContainer → InitializeSystems → PostInitialize → OnSceneOpened` — unifies every scene's game controller
- Systems also initialize with a **parallel/sequential barrier** based on `InitOrder` (same philosophy as Bootstrapper)
- Yields self-boot while a transition is running, otherwise boots itself → **scene-standalone runnable**

#### ③ struct-Based Event Bus — [`EventManager`](Assets/_Dev/_Scripts/Framework/Event/EventManager.cs)
- **Zero direct references** between systems — all communication via **struct messages** like `OnPlayerDeadEvent` (minimal boxing/GC)
- Supports both pub/sub and a **request-response (Func) pattern**

#### ④ Meta-Tag-Driven UI Automation — [`UIMetaTag`](Assets/_Dev/_Scripts/Framework/UI/Meta/UIMetaTag.cs) → [code generation](Assets/_Dev/_Scripts/Framework/Addressable/Editor/AddressableKeyGenerator.cs)
- Attach `UIMetaTag` (owner / preload / open-on-start / unlock condition) to a prefab
- One editor menu click **auto-generates `UIKeys` · `UI_REGISTRY` C# code**
- The canvas controller reads the registry to **auto-preload/open only its own group's UI**
  → fully **data-driven**, without `enum switches` or a separate spawn-config SO
- **Content-conditional loading to minimize spawns** — each UI's `requireContent` (ContentType bit flags) is
  judged by `CheckContent()`, so **only the UI for content unlocked in the current scene** is preloaded.
  Locked/inactive content UI is never loaded into memory, avoiding unnecessary instantiation

#### ⑤ Dual-Scope Resource Management — [`AddressableManager`](Assets/_Dev/_Scripts/Framework/Addressable/AddressableManager.cs) / [`AddressablePoolManager`](Assets/_Dev/_Scripts/Framework/Pool/AddressablePoolManager.cs)
- Separate **Scene-scope / Global-scope** handles → only scene-specific resources are auto-released on scene change (leak prevention)
- The pool uses an `epoch` counter to **discard late-finishing load results when the scene changes mid-load** (leak prevention), and a `_loading` dictionary to prevent duplicate concurrent loads
- All poolable targets unify under `PoolObject` for **single-dictionary management** → no manager code changes when new types are added. Items are taken from the pool via **`obj as T` reference casting** instead of `GetComponent`, minimizing cost

#### ⑥ Local Save — [`AESCrypto`](Assets/_Dev/_Scripts/Framework/Save/AESCrypto.cs)
- Encrypts save data with AES to **make casual tampering harder** (key/IV stored locally on the device)
- Provides two serialization backends: JSON / Newtonsoft

### 🧭 Boot / Initialization Pipeline Diagram
[`Bootstrapper`](Assets/_Dev/_Scripts/Framework/Core/Bootstrapper.cs) drives the [`IBootInitializable`](Assets/_Dev/_Scripts/Framework/Core/Initialization/IBootInitializable.cs) items in [`InitPhase`](Assets/_Dev/_Scripts/Framework/Core/Initialization/InitPhase.cs) order, while [`LoadSceneManager`](Assets/_Dev/_Scripts/Framework/Scene/LoadSceneManager.cs) · [`InitSceneDirector`](Assets/_Dev/_Scripts/Framework/Core/Initialization/InitSceneDirector.cs) wire up scene transitions and the loading UI.

![Boot Pipeline](docs/diagrams/boot-pipeline.png)

### 🧰 Custom Editor Productivity Tools

> Click a tool name to open its **detail page** (with screenshots).

| Tool | Role |
|---|---|
| [**Addressable Manager**](docs/tools/addressable-manager.md) | Register prefab groups + apply labels, then generate `ADR_KEY` / `UI_KEY` / `UI_REGISTRY` code in one step |
| [**Scene Selector**](docs/tools/scene-selector.md) | Instantly Open/Play Prod/Test scenes via shortcut (customizable search folders) |
| [**Folder Navigation**](docs/tools/folder-navigation.md) | Bookmark frequently used folders for quick navigation |
| [**Audio Trimmer**](docs/tools/audio-trimmer.md) | Trim audio clips in the editor + waveform preview + WAV export |

</details>

<details>
<summary><h3>🎮 Game — Game-Specific Logic (expand/collapse)</h3></summary>

> The "Shooting Survivors" game built on the framework abstractions above. Reuses `GameBase` / `UIBase` / the event bus as-is.

### 📂 Structure

```
Assets/_Dev/_Scripts/Game/
├── Behaviors/      Player / Enemy / Weapon (composition structure)
├── GameCore/       MainGame / LobbyGame
├── Systems/        Spawn / Map / Camera
├── UI/             HUD / Result / Lobby
└── Events/         game event struct definitions
```

### 🕹 Game Flow

```
Initialize scene (boot · loading UI)
  └─ Bootstrapper: instantiate/initialize persistent managers tagged "Bootstrap"
       └─ Lobby scene (LobbyGame): start UI
            └─ transition into the Game scene with effects
                 └─ MainGame: initialize Map · Spawn · Camera systems
                      └─ curtain opens → OnGameStartEvent → gameplay starts
                           └─ colliding with an enemy → game over → result popup
```

The player **aims with the mouse and shoots** the enemies pouring in from a ring just off-camera in every direction,
while the map extends infinitely as the player moves.

### 🏛 Architecture Highlights

#### ① Composition-Based Characters — [`Player`](Assets/_Dev/_Scripts/Game/Behaviors/Player/Player.cs) / [`Enemy`](Assets/_Dev/_Scripts/Game/Behaviors/Enemy/Enemy.cs)
- The root MonoBehaviour creates/initializes **plain C# components** (Status / Movement / Visual / Death …)
  and drives only their `Update / FixedUpdate` ticks
- **Composition + explicit dependency order** instead of inheritance → easy to test and reuse; Player and Enemy share the same pattern

#### ② Infinite Map — [`MapSystem`](Assets/_Dev/_Scripts/Game/Systems/Map/MapSystem.cs)
- Keeps a fixed number of N×N tiles and **repositions them cell-by-cell (tile recycling)** as the player moves
- **Deterministic obstacle placement** seeded by cell coordinates (same cell = always the same layout)
- Reuses `HashSet` / `List` buffers to avoid per-frame GC

#### ③ System Assembly — [`EntitySpawnSystem`](Assets/_Dev/_Scripts/Game/Systems/Spawn/EntitySpawnSystem.cs) / [`WeaponSpawnSystem`](Assets/_Dev/_Scripts/Game/Systems/Spawn/WeaponSpawnSystem.cs) / [`GameCameraSystem`](Assets/_Dev/_Scripts/Game/Systems/Camera/GameCameraSystem.cs)
- Each system is placed in the container as an `IGameInitializable` and initialized in order by `GameBase`
- Responsibilities are split: enemy pooling · circular-ring spawning · repositioning of left-behind enemies, weapon spawning, and Cinemachine camera wiring

### 🧭 System / Character Diagrams

**Game scene system structure ([MainGame](Assets/_Dev/_Scripts/Game/GameCore/MainGame.cs))** — on top of the [`GameBase`](Assets/_Dev/_Scripts/Framework/Core/GameBase.cs) template, the `IGameInitializable` systems and canvas controllers are assembled.

![Game Architecture](docs/diagrams/game-architecture.png)

**Lobby scene structure ([LobbyGame](Assets/_Dev/_Scripts/Game/GameCore/LobbyGame.cs))** — an example of reusing the same [`GameBase`](Assets/_Dev/_Scripts/Framework/Core/GameBase.cs) / [`UIBase`](Assets/_Dev/_Scripts/Framework/UI/Core/UIBase.cs) abstractions.

![Lobby Architecture](docs/diagrams/lobby-architecture.png)

**Character composition — Player / Enemy** — the same pattern where a root manager owns and drives plain C# components.

| Player | Enemy |
|:---:|:---:|
| ![Player Composition](docs/diagrams/player-composition.png) | ![Enemy Composition](docs/diagrams/enemy-composition.png) |

</details>

---

