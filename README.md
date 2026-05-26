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

### 2. Initialization

1. Import the GossipSDK package into your **Assets** folder. It will create:
   > `Assets / Gossip Analytics`

2. Install the dependencies listed above. If you cannot find a package by name in the Package Manager, use the Git URL to add it, then restart Unity.

3. Create the Gossip settings asset: **Create > GossipAnalytics > Settings**. This creates a `GossipAnalyticsSettings` asset in your `Resources` folder.
   > **Important: do not rename the `GossipAnalyticsSettings` asset.**

4. Enter the **API Keys** provided by Gossip Analytics in the corresponding environment fields (Dev / Beta / Production).

5. Select the **Environment** you want to use. All data will be sent to that environment.

6. Do not modify **Ingest Path**.

7. To set up the scene, add the **GossipManager** prefab from the `Samples` folder. It includes the core manager components required for the SDK to run.

---

## Trackers

### User Trackers

**User Info**  
Fires automatically when the session starts. Records: device language, user age, username, city code, device brand, device model, OS name, OS version, battery status.

**User Posture**  
Records the player's posture. Attach `UserPostureComponent` to the player's head. Configure: `Sit Threshold`, `Crouch Threshold`, `Head Transform`.  
Records: posture state, head position (X, Y, Z), scene name.

**User Events**  
Records UI events or any important custom events triggered from script:
```csharp
Gossip.Instance.UserEventTracker?.CaptureEvent(
    string eventName,
    string category,
    string text,              // optional
    Vector3 position,         // optional
    Dictionary<string,object> properties  // optional
);
```

**User Balance**  
Records player body stability. Attach `UserBalanceTrackerComponent` to the player's head.  
Records: position (X, Y, Z), oscillation magnitude, oscillation frequency, posture state.

---

### Gameplay / Monetization / Content Trackers

**Accessories**  
Records in-game accessory sales, modifications, and purchases. Use `AccessoriesComponent`.  
Call: `ReportPurchased(name, price, brand, totalPurchase)`

**Ads**  
Records ad lifecycle events (start, end, impressions, interactions, rewards). Use `AdComponent`.  
Configurable fields: `adId`, `adNetwork`, `placementId`.  
Voids: `StartAd`, `EndAd`, `RecordImpression`, `RecordInteraction`, `RecordReward`.

**Audio Reaction**  
Records audio snippets when the player has a strong vocal reaction. Use `AudioReactionTrackerComponent`.  
Records: audio clip, event severity, voice change, voice quality, movement intensity, emotion score, trigger mode.  
Requires microphone permission in Project Settings.

**Audio Volume**  
Records current game volume or volume changes. Use `AudioVolumeTrackerComponent`.  
Assign an Audio Mixer; set parameter names: `masterParam`, `musicParam`, `sfxParam`.

**Avatar**  
Records avatar events (found, modified, added, created). Use `AvatarTrackerComponent`.  
Call: `NotifyAvatar(id, name, variant, brand, price, color)`

**Battery Monitor**  
Records battery level and status. Use `BatteryMonitorComponent`.

**Connectivity**  
Records device connection speed and state. Use `ConnectivityMonitorComponent`.  
Records: connection type, online status, download MB, reachability, scene name.  
Configurable: detection URL.

**Difficulty**  
Records the current game difficulty. Use `DifficultyComponent`.  
Records: scene name, difficulty label, numeric difficulty, reason (optional).

**Distance**  
Records the distance between a specific object and the player. Use `DistanceTrackerComponent`.  
Assign `playerTransform`. Records: object position (X, Y, Z), player position (X, Y, Z), scene name.

**Experience Info**  
Records experience load time and basic app info. Use `ExperienceInfoComponent`.  
Set `autoReportOnStart = true` to fire automatically, or call `SendLoadInfo()` manually.  
Records: load time, app version, hardware info.

**Eye Tracking**  
Records gaze collisions with objects. Attach `EyeTrackingComponent` to the player's camera/view.  
Records: object name, object tag, hit position (X, Y, Z), fixation duration, scene name, tracking source.  
Also feeds heatmaps. Configuration required:
- OVR Manager > General > Eye Tracking Support: **Required**
- Edit > Project Settings > XR Plug-in Management: enable **Oculus**
- Foveated Rendering Method: **Eye Tracked Foveated Rendering**

**Heatmap**  
Creates heatmaps from trackers that support it (position, eye tracking, interactions).  
Do not place this in the scene manually — the compatible components handle it automatically.

**Hand Controller**  
Records hand/controller movement and angle during the experience. Use `HandControllerTrackingComponent`.  
Place on any global object. Do not use one per hand (causes data duplication).

**Input Usage**  
Records total controller vs. hands usage time. Use `InputUsageTrackerComponent`.  
Sends a report automatically when the session ends.

**Interaction**  
Records player interactions with objects. Use `InteractableComponent`.  
- Timed interactions: `OnInteractStart` / `OnInteractEnd`
- Instant interactions: `OnInteractInstant`  
Records: object name, tag, interaction type, position, scene name. Also feeds heatmaps.

**Mistake**  
Records developer-defined errors (not automatic). Use `MistakeReporter`.  
Call: `ReportMistake(objectName, tag, error, severity, position, scene)`

**Multiplayer**  
Records multiplayer room events. Use `MultiplayerTrackerComponent`.  
Voids: `OnPlayerJoined`, `OnPlayerLeft`, `StartTracking`, `StopTracking`.  
Records: players joining/leaving, active player count.

**Memory / Performance**  
Records memory and FPS metrics at configurable intervals. Use `PerformanceMonitorComponent`.  
Records: allocated bytes, reserved bytes, unique usage, GC collections (Gen0/1/2), FPS.

**Passthrough**  
Records passthrough state and mode. Use `PassthroughComponent`.  
Set `active = true` to detect automatically.  
Records: active state, passthrough mode, exposure, quality metric.

**Pause**  
Records pause and resume events and duration. Use `PauseComponent`.  
Call: `OnPause()` / `OnResume()`

**Peripherals**  
Records active peripherals and usage time. Use `PeripheralAutoTrackerComponent`.  
Requires the **Input System** package.  
Records: peripheral name, brand, type, haptic support, usage time, scene name.

**Position**  
Records the player's position over time. Use `PositionTrackerComponent` on the player.  
Records: position (X, Y, Z), scene name. Feeds heatmaps.

**Player Movement Heatmap**  
Creates a heatmap of the player's movement path. Use `PlayerMovementHeatmapComponent`.  
Configure heatmap parameters inside the component.

**Reality Mode**  
Records reality mode state and changes (VR/MR/etc.). Use `RealityModeMonitor`.  
Detects mode changes automatically.  
Records: current mode, new mode, previous mode duration, scene name.

**Rotation and Velocity**  
Records player rotation and velocity. Attach `RotationAndVelocityTrackerComponent` to the player.  
Records: rotation (X, Y, Z), speed, angular velocity, timestamp, object name.

**Server Status**  
Shows Gossip server diagnostics for developers. Use `ServerStatusComponent`.  
Records: server name, status, ping (ms), load percentage, metadata.

**Session**  
Records session start, end, and lifecycle events automatically. Use `SessionManager` (included in GossipManager).  
Records: event name, timestamp, duration, player ID, session ID.

---

## Other Components

**VR Permissions Handler**  
Requests required permissions: spatial data, camera, microphone, eye/head tracking.  
If you already have a permission script, you do not need to add this to the scene.

**XR Bootstrap**  
Initializes the OpenXR system. Required in every scene since the SDK is OpenXR-first.  
Add to the scene to avoid XR-related errors.

---

## Image Heatmaps

**Heatmap Orthographic Image**  
Fires once per app version when the environment is **Production**.  
Calculates the scene bounds and sends a heatmap image to the server.

**Interaction Image**  
Fires automatically via the `InteractableComponent` when interactions are recorded.  
Only sends in **Production** mode.

**Eye Gaze Image**  
Fires automatically via `EyeTrackingComponent`.  
Only sends in **Production** mode.

---

## Important Notes

- This SDK is **OpenXR-first**. You must add **XR Bootstrap** to every scene.
- To enable heatmaps, check **enableHeatmaps** in the GossipAnalyticsSettings asset.
- All component-based trackers must be present in the scene to function.
- Verify your **API Keys** and **Environment** are set correctly in GossipAnalyticsSettings.
- Keep your Gossip Analytics subscription active for the SDK to send data.
- Images (heatmaps) are only sent in **Production** environment.
- The SDK requires **microphone** and **camera** permissions.
- **Do not rename the `GossipAnalyticsSettings` asset.**

---

*Gossip Analytics — support@gossipanalytics.com*
