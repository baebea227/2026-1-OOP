# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**"It Maybe Takes Two?"** — A 3D Unity co-op puzzle game for 2 players. Built for a Kookmin University OOP course (2026-1).

- **Engine**: Unity with the new Input System
- **Networking**: Photon Fusion 2.x (Shared mode)
- **Team branches**: `Player` (배상혁 / 입력·이동·UI), `Object` (양준영 / 스테이지·오브젝트), `Server` (이규원 / 네트워크·룸 관리)

## Common Commands

Unity has no CLI build/test system here. Work is done inside the Unity Editor. From the terminal you can:

```powershell
# Open the project in Unity Hub (adjust Unity version path as needed)
# Unity does not expose a test-runner CLI for this project configuration

# Git workflow (feature branches → main via PR)
git checkout -b <branch>
git push origin <branch>
# Then open a PR to main
```

To run the game: open `Assets/Scenes/MainScene.unity` in the Unity Editor and press Play.  
For isolated tests: `PlayerTestScene.unity` (movement/input) or `Server_test_scene.unity` (networking).

## Architecture

### Networking Layer (Photon Fusion)

All networked behaviours extend `NetworkBehaviour`. The core classes:

- [Assets/dev_Bae/NetworkInputManager.cs](Assets/dev_Bae/NetworkInputManager.cs) — implements `INetworkRunnerCallbacks`; spawns/despawns player prefabs on join/leave, and polls `PlayerInputHandler` each tick

**Critical rule**: always use `Runner.DeltaTime` (not `Time.deltaTime`) inside `FixedUpdateNetwork()` so physics stays deterministic across clients.

### Input Pipeline

```
UnityEngine.InputSystem
    → PlayerInputHandler (caches values, stores jump as one-frame flag)
        → PlayerNetworkInput (struct sent over the network each tick)
            → PlayerMovement.FixedUpdateNetwork()
```

`PlayerInputHandler` sets its static `Local` reference in `Spawned()` (after `HasInputAuthority` is available) and disables itself when the player lacks input authority (prevents controlling remote avatars).

### Player Movement

[Assets/dev_Bae/PlayerMovement.cs](Assets/dev_Bae/PlayerMovement.cs) — extends `NetworkBehaviour`, drives a `CharacterController`.

Speed states: walk = 2, run = 5, sprint = 8 (m/s).  
Jumping and gravity are applied inside `FixedUpdateNetwork`. Animations are synced via `[Networked]` properties.

### Object Interaction System

- `PhysicsObject` — lightweight; one player can grab and throw
- `HeavyObject` — requires two players pushing simultaneously
- Both use an **ownership field** (`ownerId`) to prevent simultaneous conflicting interactions

### Stage / Puzzle Mechanics

Interactable base class: `InteractableObject` (abstract).  
Concrete types:
- `Lever` — one-time activation, reveals scaffolding
- `Button` — toggles bridge sections
- `PressurePlate` — requires both players to activate simultaneously (exit condition)

`Stage` tracks state and clear conditions; `StageManager` loads stages and evaluates them.

### Game Flow / Controllers (HW spec)

MVC-inspired separation:

| Layer | Classes |
|---|---|
| Model | `Character`, `PlayerStatus`, object state |
| Controller | `PlayerController`, `GameController`, `UIController` |
| View | `UISystem` |

`GameManager` handles session/room-level flow; `RoomManager` manages Photon room properties (max 2 players).

## Naming Conventions

| Suffix | Purpose |
|---|---|
| `Manager` | Singleton-style system owner (GameManager, StageManager) |
| `Controller` | Input or state bridge (PlayerController, UIController) |
| `Handler` | Event / input handler (PlayerInputHandler) |
| `Object` | Physics entity (PhysicsObject, HeavyObject) |
| `System` | UI or framework subsystem (UISystem) |

Private fields use `camelCase`; public members and types use `PascalCase`.

## Performance Targets (from HW2.md)

- Input response ≤ 50 ms
- Network sync delay ≤ 100 ms
- Physics tick rate: 30 Hz
- Zero object clipping, 100% exception coverage
- UI must maintain ≥ 50% background visibility
