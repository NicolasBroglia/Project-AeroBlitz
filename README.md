# AeroBlitz

**Couch-multiplayer aerial chase made for the EPAM Game Jam 2026.** Up to 5 players use their phones as controllers through AirConsole. One player is the **Snitch** and tries to survive; everyone else is a **Hunter** chasing them across stacked flight lanes in an isometric arena.

Built in **Unity 6** (6000.4) with **C#**, the Input System and URP, for WebGL (AirConsole) and Windows.

<!-- Add a gameplay GIF or screenshot here: it's the first thing reviewers look at. -->

---

## Gameplay
- **Hunters vs Snitch:** the hunters chase, the snitch flees. Tagging the snitch triggers a **Clash**.
- **Clash minigame:** a short button-mashing duel between the hunter and the snitch.
- **Vertical flight lanes:** players switch between discrete height levels. A depth indicator (a ground shadow scaled by altitude) and brightness by lane show how high everyone is.
- **Dash** with a cooldown, tuned differently for each role. Hunters are faster but dash less often.
- **Power-ups** that are collected in the arena and shown in the UI.
- **Timed matches** with scoring, including draw cases.

## Technical highlights
- **Game flow state machine** in `GameManager`: `Lobby → Countdown → ActiveMatch → Clash → PostTag → MatchOver`, with an `OnGameStateChanged` event that drives the UI and input locking.
- **Character state machines** for each role: Hunter (`Idle / Chasing / Stunned / Clashing / CapturedSnitch`) and Snitch (`Idle / Fleeing / Clashing / Captured`).
- **Physics-based movement** on Rigidbodies (interpolation, continuous collision detection), with acceleration, a max-speed clamp, a timed dash override and smooth lane changes.
- **Two input paths behind one abstraction** (`InputSystemManager`): phones over **AirConsole** (WebGL) and **local PC** play with Unity's Input System, plus bot filling.
- **Network command router** that separates UI commands sent from the phone (for example "start game") from per-frame movement input.
- **Lobby** for online and offline modes: device connect/disconnect handling, per-player colors (5 max) and dynamic spawning with spawn rotation.
- **Camera:** isometric rig with dynamic zoom and padding that keeps every player in frame.
- **Rendering:** an X-ray shader so players stay visible behind obstacles, dynamic shadows, baked level lighting and build optimizations for publishing.

## My contributions (Nicolás Broglia)
- AirConsole integration and the online/offline multiplayer setup (phone controllers, dash, up/down inputs, player limit, lobby).
- `GameManager` (game states, match flow, clash event, input locking during the countdown) and the `SpawnManager` with height levels.
- `HunterController` and `SnitchController` gameplay, the dash, and the depth-detection improvements.
- Camera systems (dynamic camera, isometric rig, camera bounds) and the `DepthIndicator`.
- X-ray shader, materials, dynamic shadows, and the Hunter and Snitch models.
- Main menu, debug GUI tools, and reorganizing the scripts for readability.

## Team
- **Nicolás Broglia** ([@NicolasBroglia](https://github.com/NicolasBroglia)): gameplay, multiplayer, camera, rendering
- **[@JeremiasRonco](https://github.com/JeremiasRonco)**: gameplay scene, player UI and notifications, power-ups, scoring, lighting, optimization and builds

## Project structure
```
Assets/Scripts/
├── GameManager.cs            Match flow and game state machine
├── SpawnManager.cs           Spawning and height levels
├── NetworkCommandRouter.cs   Phone → game UI commands
├── InputManager.cs           AirConsole input handling
├── IsometricCameraRig.cs     Camera
├── DepthIndicator.cs         Altitude shadow and tether
├── Hunter/  Snitch/          Role controllers and state machines
├── PowerUp/                  Collectable, manager, receiver
├── UI/                       Main menu, in-game UI
└── Util/                     InputSystemManager (input abstraction)
```

## Running it
1. Open the project in **Unity 6000.4.9f1**.
2. Open the main scene in `Assets/Scenes` and press Play. Local PC mode works without phones.
3. For phone controllers, build for **WebGL** and run it through AirConsole (see the [AirConsole Unity plugin docs](https://github.com/AirConsole/airconsole-unity-plugin)).

## Third-party
- [AirConsole Unity plugin](https://github.com/AirConsole/airconsole-unity-plugin) (LGPL-3.0)
- Newtonsoft.Json (MIT) and TextMesh Pro (Liberation Sans font, OFL)
