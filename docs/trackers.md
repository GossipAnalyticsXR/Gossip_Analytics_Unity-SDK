# Trackers — Gossip Analytics Unity SDK (Public Reference)

This document describes the trackers available in the Gossip Analytics Unity SDK at an **integration** level:
- Which trackers exist
- Which component to use (if applicable)
- What each tracker does (high level)
- Common requirements (permissions / scene setup)
- Whether it feeds heatmaps (coordinates or images)

> Note: Trackers are primarily exposed as Unity **Components**.  
> If a tracker is a component, it must be present in the scene to run.  
> Internal network details, payload formats, and ingestion logic are not documented in the public repository.

---

## General rules

- **GossipManager** is the SDK’s main controller: it initializes and coordinates trackers and overall SDK runtime behavior once the experience starts.
- **Settings**: configuration lives in `GossipAnalyticsSettings` under `Assets/Resources/` (Create → Gossip → Settings).
- **OpenXR-first**: you must add **XR Bootstrap** to the scene to ensure XR system compatibility.
- **Permissions**: some trackers require permissions (microphone/camera/spatial data) depending on your app’s usage.
- **Heatmaps**:
  - Some trackers feed heatmaps using **coordinates** (movement, eye, interaction).
  - Image-based heatmap capture may be enabled in **Production** environments depending on configuration.

---

## Trackers table

> Columns:
> - **Tracker**: functional tracker name
> - **Component**: component/script to place in the scene (if applicable)
> - **What it records**: high-level integration description
> - **Setup**: where to place it (guidance only)
> - **Permissions**: typical required permissions (if any)
> - **Heatmap**: whether it feeds heatmaps and which type

### Gameplay / Monetization / Content

| Tracker | Component | What it records (high level) | Setup | Permissions | Heatmap |
|---|---|---|---|---|---|
| AccessoriesTracker | `AccessoriesComponent` | Accessory purchases/acquisitions (product, payment/method, etc.) | Scene (relevant system) | — | No |
| AdTracker | `AdComponent` | Ad start/end, impressions/interactions, rewards | Scene (ad system) | — | No |
| AvatarTracker | `AvatarTrackerComponent` | Avatar acquisition/sale/changes (id, color, price, etc.) | Scene (avatar system) | — | No |
| DifficultyTracker | `DifficultyComponent` | Current level/state difficulty | Scene (game state) | — | No |
| ExperienceInfoTracker | `ExperienceInfoComponent` | Load time and basic experience info | Scene (startup manager) | — | No |

---

### Audio

| Tracker | Component | What it records (high level) | Setup | Permissions | Heatmap |
|---|---|---|---|---|---|
| AudioReactionTracker | `AudioReactionTrackerComponent` | Strong user reactions and an audio snippet (e.g., .wav) | Scene (global) | Microphone | No |
| AudioVolumeTracker | `AudioVolumeTrackerComponent` | In-app volume changes (not device volume) | Scene (audio system) | — | No |

---

### Device / Connectivity / Performance

| Tracker | Component | What it records (high level) | Setup | Permissions | Heatmap |
|---|---|---|---|---|---|
| BatteryTracker | `BatteryMonitorComponent` | Battery level/status over time | Scene (global) | — | No |
| ConnectivityTracker | `ConnectivityMonitorTracker` | Network state, connection type, connectivity metrics | Scene (global) | — | No |
| MemoryTracker | `PerformanceMonitorComponent` | Memory/performance metrics during the session | Scene (global) | — | No |
| PlatformTracker | `PlatformMonitorComponent` | Device info (model, resolution, version, etc.) | Scene (global) | — | No |
| ServerStatusTracker | `ServerStatusComponent` | Server status indicators for developer diagnostics | Scene (dev/debug) | — | No |

---

### Movement / Space / Posture

| Tracker | Component | What it records (high level) | Setup | Permissions | Heatmap |
|---|---|---|---|---|---|
| PositionTracker | `PositionTrackerComponent` | Player position during the experience | Player | — | Yes (coordinates) |
| RotationTracker | `RotationAndVelocityTrackerComponent` | Player rotation/velocity at intervals | Player | — | Yes (coordinates) |
| DistanceTracker | `DistanceTrackerComponent` | Distance between player and the assigned object | Scene (per object) | — | No |
| PlayableAreaTracker | `PlayableAreaComponent` | Play/usage area dimensions | Scene (global) | — | No |
| UserPostureTracker | `UserPostureTrakcerComponent` | User posture state (standing/sitting/other states) | Player (head) | — | No |
| UserBalanceTracker | `UserBalanceTrackerComponent` | User body stability (balance) | Player (head) | — | No |
| RealityModeTracker | `RealityModeMonitor` | Reality mode state/changes (VR/MR/etc.) | Scene (global) | — | No |
| PauseTracker | `PauseComponent` | Pauses/resumes and duration | Scene (pause manager) | — | No |

---

### Interaction / Input / Peripherals

| Tracker | Component | What it records (high level) | Setup | Permissions | Heatmap |
|---|---|---|---|---|---|
| InteractionTracker | `InteractableComponent` | Interactions with assigned objects (event, position, etc.) | Scene (interactable objects) | — | Yes (coordinates) + (images in Production, if enabled) |
| InputUsageTracker | `InputUsageTrackerComponent` | Controller vs hands usage and time spent | Scene (global) | — | No |
| HandControllerTracker | `HandControllerTrackingComponent` | Hand controller usage/rotation (as supported) | Scene (global) | — | No |
| PeripheralTracker | `PeripheralAutoTrackerComponent` | Active peripherals and usage time | Scene (global) | — | No |
| MultiplayerTracker | `MultiplayerTrackerComponent` | Multiplayer room events (joins/leaves/state) | Scene (multiplayer manager) | — | No |
| MistakeTracker | `MistakeReporter` | Developer-reported errors (manual) | Scene (where errors occur) | — | No |

---

### Session / Custom Events

| Tracker | Component | What it records (high level) | Setup | Permissions | Heatmap |
|---|---|---|---|---|---|
| SessionTracker | `SessionManager` | Session start/end and key lifecycle events | Scene (with GossipManager) | — | No |
| UserEventsTracker | (no component) | Custom developer-defined events (called from code) | Code-only | — | No |

---

## Eye Tracking

| Tracker | Component | What it records (high level) | Setup | Permissions | Heatmap |
|---|---|---|---|---|---|
| EyeTrackingTracker | `EyeTrackingComponent` | Gaze signals (when supported by device/runtime) | Player (camera/view) | (platform-dependent) | Yes (coordinates) + (images in Production, if enabled) |

> Note: Eye Tracking is **device-dependent** and requires XR configuration.  
> The SDK is OpenXR-first and can operate using available runtime modes (real/simulated) depending on support.

---

## Heatmaps

### Coordinate-based heatmaps
These trackers feed coordinate-based heatmaps:
- `PlayerMovementHeatmapComponent` (position)
- `EyeTrackingComponent` (gaze)
- `InteractableComponent` (interactions)
