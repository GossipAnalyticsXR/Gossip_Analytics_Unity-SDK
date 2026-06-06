# Gossip Analytics — Unity XR SDK

**Analytics for VR/XR, built for Unity. Drop it in, and in ~6 steps you’re sending real user
behavior to your dashboard — heatmaps, gaze, hand & controller use, comfort, and UX issues.**

Most of the setup is auto-assigned: trackers, interactable detection, and permissions come
pre-configured — you just deselect what you don’t need. It’s framework-agnostic (works with
Unity XR Interaction Toolkit and Meta Interaction SDK), non-destructive, and **uninstalls in
2 clicks**.

---

## What you get

A live dashboard at [app.gossipanalytics.com](https://app.gossipanalytics.com) with:

- Sessions & users — counts, active users, average session time, sessions per day, versions.

- Spatial heatmaps — movement, gaze (eye), and hand/controller density over your scene.

- Immersion — presence, comfort/ergonomics (head/hand position, FOV), posture (sitting/standing),
  playable area.

- UX issues — crashes, freezes, and behavioral friction (interaction struggle, exploration
  without progress, presence drops).

- Device & performance — FPS, memory, battery, connectivity, peripherals, audio reactions.

- Events & flows — object interactions, passthrough, and user journeys.

---

## Why it’s fast to adopt

- ~6 steps to running. Quick Setup wires the manager, settings, and trackers for you.

- Auto-assigned where it can be. Interactables are auto-detected and instrumented; trackers and
  permissions come pre-selected — deselect anything you don’t want.

- Framework-agnostic. Interaction capture works automatically with Unity XRI and Meta
  Interaction SDK — no glue code.

- Non-destructive & reversible. A 2-click uninstall removes every SDK component and asset and
  leaves your scenes, scripts, and other packages untouched.

- Three environments. Separate Dev / Beta / Production keys so test data never pollutes your
  live reports.

---

## Requirements

- Unity 2022.3 or newer (declared in the package manifest; developed and tested on Unity 6 / 6000.x).

- An XR provider: OpenXR or Oculus XR Plugin (you install one, based on your target device).

- Auto-installed on first import (you’ll get a one-click prompt): Input System, XR Plugin Management,
  and the SDK’s runtime dependencies.

---

## Install (≈6 steps)

1. Add the package. Unity → Window ▸ Package Manager ▸ + ▸ Add package from git URL… and paste:
   https://github.com/GossipAnalyticsXR/Gossip_Analytics_Unity-SDK.git

2. Install dependencies. On first load the SDK detects what’s missing and offers to install it —
   click Install. (Install an XR provider manually if prompted.)

3. Import the Sample. Package Manager → Gossip Analytics SDK → Samples ▸ Import (brings in the
   manager prefab).

4. Run Quick Setup. Window ▸ Gossip Analytics ▸ Quick Setup — adds the manager and settings to
   your scene (once; it persists across scenes).

5. Paste your API key & choose an environment. In the GossipSettings Inspector, paste your
   Dev/Beta/Prod key and pick the matching Environment. (Get keys at app.gossipanalytics.com.)

6. Review & build. Open Window ▸ Gossip Analytics ▸ Instrumentation Manager, deselect anything
   you don’t want, then Play or Build & Run. Open your dashboard to see the data.

---

## What you configure yourself (honest 2 minutes)

Almost everything is automatic, but a few things need your input for accurate data:

- API key + environment — required (step 5).

- Enable Heatmaps — keep the Enable Heatmaps toggle on in GossipSettings (on by default) for
  spatial / gaze / hand heatmaps.

- Play-area bounds — set worldMinXZ / worldMaxXZ on the heatmap / eye / audio trackers to your
  actual play area, so heatmaps line up.

- Posture thresholds — set the sit/crouch height thresholds to your player’s real standing height.

- App-level passthrough — if your app toggles passthrough, wire
  OnPassthroughEnabled() / OnPassthroughDisabled(). (Guardian-boundary passthrough is captured
  automatically.)

That’s it — no per-object code for interactions, no manual tracker wiring.

---

## Uninstall (2 clicks)

Window ▸ Gossip Analytics ▸ Uninstall SDK → confirm. It removes all SDK components from your scenes,
the settings and instrumentation assets, the imported sample, and the package itself. **Your own
scenes, scripts, and other packages are left untouched.**

---

## Environments

The SDK keeps separate Dev / Beta / Production keys. Pick the environment in GossipSettings that
matches where you’re deploying — data is sent to that environment’s dashboard. A build-time check
warns you if you’re about to build with the Dev environment so live users don’t end up in your
Dev reports.

---

## Notes

- Active subscription required — data is collected and shown on the dashboard while your Gossip
  Analytics subscription is active.

- Image captures (scene / gaze / interaction snapshots that power image heatmaps) are uploaded
  only in the Production environment.

---

## Support

- Dashboard & API keys: [app.gossipanalytics.com](https://app.gossipanalytics.com)
- Docs: see the SDK’s GitHub repository
- Issues / contact: support@gossipanalytics.com

---

*Privacy note: audio-reaction detection processes brief microphone samples *on-device* for emotion
signals; no audio recordings are stored or transmitted.*
