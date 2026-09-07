# Trackers

Every tracker is a Unity component you add to a GameObject in your scene. You do not
add them by hand: open **Window -> Gossip Analytics -> 2 - Instrumentation Manager**,
which lists all of them, adds the ones you keep, and auto-assigns the references it can
resolve (head camera, XROrigin, player transform).

This page is the reference for what each one records and what it needs from you.

## How to read the tables

| Setup | Meaning |
|---|---|
| **Auto** | Add it and it works. No Inspector fields, no code. |
| **Inspector** | Add it and set at least one field in the Inspector before it reports useful data. |
| **Code** | Add it and call the listed method from your own code. Without the call it reports nothing (or only a default). |

Deselecting a tracker in the Instrumentation Manager removes the component. Nothing is
collected for a tracker that is not in the scene.

## Movement and body

| Tracker | Component | Setup | What it records |
|---|---|---|---|
| Position Tracker | `PositionTrackerComponent` | Auto | Player position (X, Y, Z) over time. Feeds the heatmaps. |
| Rotation & Velocity | `RotationAndVelocityTrackerComponent` | Auto | Rotation, speed and angular velocity. |
| Posture Tracker | `UserPostureComponent` | Inspector | Standing / sitting / crouching. Head camera is auto-assigned; you must set `sitThreshold` and `crouchThreshold` in metres to match your rig. |
| Balance Tracker | `UserBalanceTrackerComponent` | Auto | Body stability and oscillation. |
| Distance Tracker | `DistanceTrackerComponent` | Auto | Total player displacement over the session. `playerTransform` is auto-assigned to the main camera. |
| Movement Heatmap | `PlayerMovementHeatmapComponent` | Inspector | Samples position to build a spatial heatmap. Must sit on the Player or XROrigin object; set `worldMinXZ` and `worldMaxXZ` to your scene bounds. |
| Playable Area | `PlayableAreaComponent` | Auto | Guardian / play-area bounds (width, depth, area in m2) on session start. |

## Interaction and input

| Tracker | Component | Setup | What it records |
|---|---|---|---|
| Hand & Controller Tracking | `HandControllerTrackingComponent` | Auto | Hand and controller movement. |
| Input Usage Tracker | `InputUsageTrackerComponent` | Auto | Time spent on controllers versus hand tracking. |
| Peripheral Tracker | `PeripheralAutoTrackerComponent` | Auto | Connected XR peripherals: type, brand and session duration. |
| Eye Tracking | `EyeTrackingComponent` | Inspector | Gaze hits and fixation. Must be attached to the camera. |

## Audio and reactions

| Tracker | Component | Setup | What it records |
|---|---|---|---|
| Audio Volume Tracker | `AudioVolumeTrackerComponent` | Inspector | In-app audio volume. Assign an AudioMixer for per-channel data (master, music, SFX), or leave it empty to read `AudioListener.volume`. |
| Audio Reaction Tracker | `AudioReactionTrackerComponent` | Inspector | Emotional voice reactions captured through the microphone. **Requires the Microphone permission**; add the permission handler from the Instrumentation Manager. |

## Session, platform and health

| Tracker | Component | Setup | What it records |
|---|---|---|---|
| Experience Info | `ExperienceInfoComponent` | Auto | App version, target hardware and scene load time (Awake-to-Start delta). Version is read from Player Settings. |
| Platform Monitor | `PlatformMonitorComponent` | Auto | Platform, device model, screen resolution and audio state on session start. |
| Performance Monitor | `PerformanceMonitorComponent` | Auto | FPS and memory usage. |
| Battery Monitor | `BatteryMonitorComponent` | Auto | Battery level and charging status. |
| Connectivity Monitor | `ConnectivityMonitorComponent` | Auto | Network connection type and speed. |
| Pause Tracker | `PauseComponent` | Auto | Pause and resume events with duration. OS-level pauses (headset removal, Alt+Tab) are detected via `OnApplicationPause`. |
| Crash Reporter | `CrashReporterComponent` | Auto | Unity exceptions and errors, via `Application.logMessageReceived`. `captureExceptions` is true by default. |
| Reality Mode Monitor | `RealityModeMonitor` | Auto | Transitions between VR, MR, 2D and unknown XR modes, with per-mode duration. |
| Passthrough Tracker | `PassthroughComponent` | Code | MR passthrough enable/disable events and active duration. Wire it to your own passthrough toggle. |

## Scene capture

| Tracker | Component | Setup | What it records |
|---|---|---|---|
| Heatmap Scene Capture | `HeatmapSceneAutoCapture` | Auto | A top-down image of the scene on start, uploaded as the heatmap background. Auto-frames to scene bounds. |

## Game design and economy

These report a default on start; the real values only arrive when you call the method.

| Tracker | Component | Setup | What it records |
|---|---|---|---|
| Difficulty Tracker | `DifficultyComponent` | Code | Difficulty level changes. Call `NotifyDifficulty()`. |
| Avatar Tracker | `AvatarTrackerComponent` | Code | Avatar selections and purchases. Call `NotifyAvatar()` from your purchase code. |
| Accessories Tracker | `AccessoriesComponent` | Code | In-app item and accessory purchases. Call `ReportPurchased()` from your purchase code. |
| Ad Tracker | `AdComponent` | Code | Ad impressions, interactions, rewards and duration. Set Ad ID, network and placement in the Inspector, then call `RecordImpression()`, `RecordInteraction()` or `RecordReward()` from your ad SDK callbacks. |

## Multiplayer and backend

| Tracker | Component | Setup | What it records |
|---|---|---|---|
| Multiplayer Tracker | `MultiplayerTrackerComponent` | Inspector | Room / match snapshots. Emits on start with an empty room unless configured. |
| Server Status | `ServerStatusComponent` | Inspector | Polls a game-server status endpoint on an interval. Deselect it if you have no dedicated game server. |

## Notes

- Trackers that say **Auto** still need the Gossip manager in the scene and a valid API
  key in `GossipSettings`. Run **Window -> Gossip Analytics -> 1 - Quick Setup** first.
- A tracker that is added but never configured is not an error: it reports its default
  and shows up in the dashboard with empty or constant values. If a card looks flat,
  check the Setup column here before assuming the data is wrong.
- Objects the user can interact with are instrumented separately, from the
  **Interactables** tab of the Instrumentation Manager, not from this list.
