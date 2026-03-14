# Gossip Analytics — Unity SDK

Unity SDK to send XR analytics (VR/AR/MR) telemetry to Gossip Analytics:
- Session lifecycle
- User/device context
- Interactions, movement, eye/hand signals (when available)
- Optional heatmaps (and Production-only image heatmaps)

---

## Requirements
- Unity: Unity: 2021.3 LTS+
- OpenXR-first: **XR Bootstrap is required** (see Quickstart)

---

## Install

### Unity Package Manager (Git URL)
1. Open **Window → Package Manager**
2. Click **+** → **Install package from git URL**
3. Paste:
   - `https://github.com/[ORG]/[REPO].git#[TAG_OR_COMMIT]`

### Manual import
Import the SDK into:
- `Assets/GossipSDK`

---

## Dependencies
Install these dependencies, then restart Unity:

- UniTask `2.5.10`
  - `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.10`
- SocketIOUnity `v1.1.4`
  - `https://github.com/itisnajim/SocketIOUnity.git#v1.1.4`
- Input System
  - Project Settings → Player → Other Settings → **Active Input Handling = Both**
- Meta XR Core SDK
- Meta MR Utility Kit
- Oculus XR Plugin
- XR Core Utilities
- XR Legacy Input Helpers
- XR Plugin Management

---

## Quickstart

1) Create Settings asset
- Create folder: `Assets/Resources/`
- Create asset: **Create → Gossip → Settings**
- Do not rename it: `GossipAnalyticsSettings`

2) Configure credentials
Fill in the **URL** and **ApiKey** for each environment:
- Dev / Beta / Production

3) Select Environment
Choose where data will be sent:
- Dev / Beta / Production

4) Do not modify
- `Ingest Path`

5) Add the SDK Manager prefab
Add to your scene:
- `Samples/GossipManager`

6) OpenXR-first (required)
Add to your scene:
- **XR Bootstrap** (required)
- **VR Permissions Handler** (optional if your project already requests permissions)

---

## Heatmaps & Production-only images
- Enable heatmaps: set `enableHeatmaps` in Settings
- Image heatmaps are sent **only in Production**:
  - Heatmap Orthographic Image (once per version)
  - Interaction Image (via Interaction component)
  - Eye Gaze Image (via EyeTrackingComponent)

---

## Troubleshooting (fast checks)
- URLs + ApiKeys match the selected Environment
- `enableHeatmaps` is enabled (if expected)
- Required components exist in-scene (Manager, XR Bootstrap, tracker components)
- Mic/Camera permissions granted (if using audio/MR-related trackers)

---

## Support
- Email: support@gossipanalytics.com
- GitHub Issues: use this repo’s Issues tab for bugs & requests

---

## Documentation
- [Trackers reference](docs/trackers.md)
- [Security policy](SECURITY.md)
- [Third-party notices](THIRD_PARTY_NOTICES.md)

---

## Changelog
See [CHANGELOG](CHANGELOG).

---

## License
See [LICENSE](LICENSE.md)

---

## Notes:
The full documentation lives inside the Gossip Analytics dashboard, in the Guide section, where you’ll find deeper explanations, advanced setup instructions and support from the AI Guide built into the platform.
