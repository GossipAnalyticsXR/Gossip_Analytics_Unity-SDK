# Gossip Analytics SDK

Unity SDK for immersive analytics in XR, VR, AR, and 2D/3D games.

**Website:** https://gossipanalytics.com
**Support:** support@gossipanalytics.com

---

## Quick Start

### 1. Dependencies (Unity Package Manager)

Install the following packages before using the SDK:

| Package | Install via |
|---|---|
| **UniTask** | Git URL: `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.10` |
| **SocketIOUnity** | Git URL: `https://github.com/itisnajim/SocketIOUnity.git#v1.1.4` |
| **Input System** | Unity Package Manager (search by name) |
| **Meta XR Core SDK** | Unity Package Manager |
| **Meta MR Utility Kit** | Unity Package Manager |
| **Oculus XR Plugin** | Unity Package Manager |
| **XR Core Utilities** | Unity Package Manager |
| **XR Legacy Input Helpers** | Unity Package Manager |
| **XR Plugin Management** | Unity Package Manager |

> **Input System note:** Go to **Project Settings > Player > Other Settings** and set **Active Input Handling** to **Both**.

After installing all dependencies, restart Unity.

---

### 2. Create the Settings Asset

In your **Resources** folder, right-click and select **Create > GossipAnalytics > Settings**.

> Do **not** rename the asset from `GossipAnalyticsSettings`.

---

### 3. Enter Your API Keys

Open the `GossipAnalyticsSettings` asset in the Inspector.

Enter the API keys provided by Gossip Analytics for each environment:

- **Dev** — for local development and testing
- **Beta** — for staging / pre-release builds
- **Production** — for live, published builds

Get your API keys from the **Gossip Analytics Dashboard:** https://gossipanalytics.com

---

### 4. Select Your Environment

In the Inspector, choose the **Environment** that matches your current build target.
Data will be sent to the selected environment only.

> Do **not** modify the **Ingest Path** field.

---

### 5. Add the GossipManager to Your Scene

Drag the **GossipAnalyticsManager** prefab (located in `Samples/Prefabs/`) into your scene.
This prefab contains all the core components required for the SDK to function.

---

### 6. Enable Heatmaps (optional)

In the `GossipAnalyticsSettings` Inspector, enable the **Enable Heatmaps** toggle.

> Heatmap images are only uploaded when the environment is set to **Production**.

---

## Trackers

### User Trackers

#### User Info
Fires automatically when the session starts. Records: device language, user age, username, city code, device brand and model, OS name and version, battery status.

#### User Posture
Tracks the player's posture (standing, sitting, crouching). Attach **UserPostureComponent** to the player's head.
Configure **Sit Threshold** and **Crouch Threshold**, and assign the **Head Transform**.

#### User Events
Records UI and custom gameplay events. Call from script:
```csharp
Gossip.Instance.UserEventTracker?.CaptureEvent(
    "event name",    // string
    "category",      // string
    "text",          // string
    position,        // Vector3 (optional)
    properties       // Dictionary<string, object> (optional)
);
```

#### User Balance
Tracks player balance and sway. Attach **UserBalanceTrackerComponent** to the player.
Records: position (X, Y, Z), oscillation magnitude and frequency, posture state.

---

### Gameplay Metrics Trackers

#### Accessories
Tracks in-game accessories that are purchased, modified, or sold. Attach **AccessoriesComponent** and call:
```csharp
ReportPurchased("name", "price", "brand", "totalPurchase");
```

#### Ads
Tracks ad events (start, end, impressions, interactions, rewards). Attach **AdComponent**.
Configure `adId`, `adNetwork`, and `placementId`. Call `StartAd()`, `EndAd()`, `RecordImpression()`, `RecordInteraction()`, `RecordReward()`.

#### Audio Reaction
Detects loud or unexpected vocal reactions from the player. Attach **AudioReactionTrackerComponent**.
Ensure microphone permission is enabled in Project Settings.

#### Audio Volume
Tracks the current game volume or volume changes. Attach **AudioVolumeTrackerComponent**.
Assign an **AudioMixer** and set the parameter names in `masterParam`, `musicParam`, and `sfxParam`.

#### Avatar
Tracks avatar creation, modification, and selection. Attach **AvatarTrackerComponent** and call `NotifyAvatar()`.
Records: avatar ID, name, variant, brand, price, color.

#### Battery Monitor
Tracks device battery level and status. Attach **BatteryMonitorComponent**.

#### Connectivity
Tracks network connection type, download speed, and online status. Attach **ConnectivityMonitorComponent**.
You can customize the URL used to measure connectivity speed.

#### Difficulty
Tracks game difficulty changes. Attach **DifficultyComponent**.
Records: scene name, difficulty label, numeric difficulty, reason (optional).

#### Distance
Tracks the distance between a specific object and the player. Attach **DistanceTrackerComponent** and assign `playerTransform`.

#### Experience Info
Tracks experience load time, app version, and hardware. Attach **ExperienceInfoComponent**.
Enable `autoReportOnStart` to fire automatically, or call `SendLoadInfo()` manually.

#### Eye Tracking
Tracks what the player is looking at. Attach **EyeTrackingComponent** to the player's view.
Configure max hit distance and fixation threshold. Also writes to Heatmap.

**Required setup for eye tracking:**
- OVR Manager > General > Eye Tracking Support → **Required**
- Edit > Project Settings > XR Plug-in Management > Enable **Oculus**
- Edit > Project Settings > XR Plug-in Management > Oculus > Foveated Rendering Method → **Eye Tracked Foveated Rendering**

#### Heatmap
Automatically created by trackers that support it (Position, Eye Tracking, Interaction).
Do **not** add this component manually to the scene.

#### Hand Controller
Tracks hand movement and rotation angle during the experience. Attach **HandControllerTrackingComponent** to a global object (not per-hand, to avoid duplicate data).

#### Input Usage
Tracks how long each input device (controller or hand) was used. Attach **InputUsageTrackerComponent**.
A report is sent automatically at the end of the experience.

#### Interaction
Tracks player interactions with objects. Attach **InteractableComponent** to interactable objects.
Supports timed interactions (`OnInteractStart` / `OnInteractEnd`) and instant interactions (`OnInteractInstant`).
Also writes to Heatmap.

#### Mistake
Tracks gameplay errors. Not automatic — you must call it explicitly. Attach **MistakeReporter** and call `ReportMistake()`.
Records: object name, tag, error description, severity, position, scene.

#### Multiplayer
Tracks players joining and leaving rooms. Attach **MultiplayerTrackerComponent**.
Call `OnPlayerJoined()` / `OnPlayerLeft()` for player events, and `StartTracking()` / `StopTracking()` for session tracking.

#### Memory (Performance Monitor)
Tracks memory usage and FPS at a configurable interval. Attach **PerformanceMonitorComponent**.

#### Passthrough
Tracks Passthrough mode state (active, mode, exposure, quality). Attach **PassthroughComponent**.
If the `active` flag is true, detection is automatic.

#### Pause
Tracks when the player pauses and resumes the game, and how long the pause lasted. Attach **PauseComponent**.
Call `OnPause()` and `OnResume()`.

#### Peripherals
Tracks connected peripherals (name, brand, type, haptic support, usage time, scene). Attach **PeripheralAutoTrackerComponent**.
Requires the **Input System** package.

#### Position
Tracks the player's position (X, Y, Z) and current scene. Attach **PositionTrackerComponent** to the player.

#### Player Movement Heatmap
Generates a heatmap of player movement. Attach **PlayerMovementHeatmapComponent** to the player.
Configure heatmap settings directly on this component.

#### Reality Mode
Tracks the current reality mode (VR, MR, AR) and mode transitions. Attach **RealityModeMonitor**.
Detection is automatic.

#### Rotation and Velocity
Tracks rotation (X, Y, Z), speed, angular velocity, and time. Attach **RotationAndVelocityTrackerComponent** to the player or relevant object.

#### Server Status
Shows the current Gossip server status (name, status, ping ms, load %, meta). Attach **ServerStatusComponent**.

#### Session
Tracks when the session starts, pauses, and ends. Attach **SessionManager** — it handles everything automatically.
Records: event name, time, duration, player ID, session ID.

---

## Other Components

### VR Permissions Handler
Requests runtime permissions: spatial data, camera, microphone, and eye/head tracking.
If you already have a permissions script in your project, this component is not required.

### XR Bootstrap
Required for OpenXR support. Place this component in your scene to ensure OpenXR data is initialized correctly and avoid runtime errors.

---

## Image Heatmaps

> All image heatmaps are only sent when the environment is set to **Production**.

### Heatmap Orthographic Image
Fires once per app version. Calculates scene dimensions and sends an orthographic heatmap image to the server.

### Interaction Image
Fires automatically via the **InteractableComponent**. Sends a heatmap image of interaction points.

### Eye Gaze Image
Fires automatically via the **EyeTrackingComponent**. Sends a heatmap image of gaze data.

---

## Important Notes

- This SDK is **OpenXR-first**. Always place the **XR Bootstrap** component in your scene.
- To enable heatmaps, check **Enable Heatmaps** in the `GossipAnalyticsSettings` asset.
- All trackers that use a **Component** must be placed in the scene to function.
- Verify your **API key** is correct if data is not appearing in the Dashboard.
- Keep your Gossip Analytics subscription active for the SDK to function.
- Heatmap images are only sent in **Production** mode.
- This SDK requires **microphone** and **camera** permissions.
- Do **not** rename the `GossipAnalyticsSettings` asset.

---

*Gossip Analytics SDK — https://gossipanalytics.com*
