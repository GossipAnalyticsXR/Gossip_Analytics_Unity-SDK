using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using GossipSDK.Core;
using GossipSDK.Tracking;
using GossipSDK.Tracking.GameplayMetrics;
using GossipSDK.Heatmaps;
using System.Collections.Generic;
using GossipSDK.Core.XR;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class InteractableComponent : MonoBehaviour
    {
        [Tooltip("E.g. Pickup, Button, Open, Inspect")]
        public bool autoTriggerOnStart = false;
        public bool autoStartOnEnable = false;

        [Header("Image Capture Optimization")]
        public bool captureImageOnInteraction = true;
        public float minTimeBetweenImages = 5f;
        public float objectCooldown = 30f;

        private static Dictionary<int, float> lastCaptureTimes = new Dictionary<int, float>();
        private static float globalLastImageTime;

        [Header("Heatmap")]
        public bool registerHeatmapHit = true;
        public float heatmapCellSize = 1f;
        public Vector2 heatmapWorldMin = new Vector2(-50, -50);
        public Vector2 heatmapWorldMax = new Vector2(50, 50);

        private string currentInteractionId;
        private double currentInteractionStartTimeRealtime;

        [Header("Heatmap Flush")]
        public float flushIntervalSeconds = 10f;

        private static float lastFlushTime;

        private InteractionTracker Tracker => Gossip.Instance?.InteractionTracker;

        private static HeatmapManager heatmap;
        private static string heatmapScene;

        private void Awake()
        {
            if (!registerHeatmapHit) return;

            string scene = SceneManager.GetActiveScene().name;

            if (heatmap == null || heatmapScene != scene)
            {
                heatmapScene = scene;

                heatmap = new HeatmapManager(
                    sceneName: scene,
                    worldMinXZ: heatmapWorldMin,
                    worldMaxXZ: heatmapWorldMax,
                    cellSizeMeters: heatmapCellSize
                );

                if (Gossip.Instance?.Settings?.EnableDebug == true)
                    Debug.Log($"[Interactable] Heatmap created for scene={scene}");
            }
        }

        private void Start()
        {
            StartCoroutine(WaitAndSend());
        }

        private IEnumerator WaitAndSend()
        {
            yield return new WaitUntil(() => Gossip.Instance != null);

            if (autoTriggerOnStart)  OnInteractInstant("Demo Shoot");
            if (autoStartOnEnable)   OnInteractStart("Demo Start Interaction");
        }

        private void Update()
        {
            if (!registerHeatmapHit || heatmap == null)
                return;

            if (Time.time - lastFlushTime >= flushIntervalSeconds)
            {
                lastFlushTime = Time.time;
                FlushHeatmap();
            }
        }

        private void OnDisable()
        {
            if (autoStartOnEnable && !string.IsNullOrEmpty(currentInteractionId))
                OnInteractEnd("Demo end Interaction");
        }

        public void OnInteractInstant(string interactionType)
        {
            try
            {
                Vector3 pos = transform.position;
                string scene = SceneManager.GetActiveScene().name;
                string ts = DateTime.UtcNow.ToString("o");

                var inputType = XRInteractionInputResolver.GetCurrentInputType().ToString();

                Tracker?.CapInteractionInstant(
                    gameObject.name,
                    gameObject.tag,
                    inputType,
                    interactionType,
                    pos,
                    scene,
                    ts
                );

                if (registerHeatmapHit)
                    heatmap?.RegisterHit(pos);

                TryCaptureImage(interactionType);
            }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        public void OnInteractStart(string interactionType)
        {
            try
            {
                if (!string.IsNullOrEmpty(currentInteractionId))
                    OnInteractEnd(interactionType);

                currentInteractionId = Guid.NewGuid().ToString();
                currentInteractionStartTimeRealtime = Time.realtimeSinceStartupAsDouble;

                var inputType = XRInteractionInputResolver.GetCurrentInputType().ToString();

                Vector3 pos = transform.position;
                string scene = SceneManager.GetActiveScene().name;
                string ts = DateTime.UtcNow.ToString("o");

                Tracker?.CapInteractionStart(
                    currentInteractionId,
                    gameObject.name,
                    gameObject.tag,
                    inputType,
                    interactionType,
                    pos,
                    scene,
                    ts
                );

                if (registerHeatmapHit)
                    heatmap?.RegisterHit(pos);

                TryCaptureImage(interactionType);
            }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        public void OnInteractEnd(string interactionType)
        {

            try
            {
                if (string.IsNullOrEmpty(currentInteractionId))
                    return;

                double now = Time.realtimeSinceStartupAsDouble;
                double duration = Math.Max(0.0, now - currentInteractionStartTimeRealtime);

                var inputType = XRInteractionInputResolver.GetCurrentInputType().ToString();

                Vector3 pos = transform.position;
                string scene = SceneManager.GetActiveScene().name;
                string ts = DateTime.UtcNow.ToString("o");

                Tracker?.CapInteractionEnd(
                    currentInteractionId,
                    gameObject.name,
                    gameObject.tag,
                    inputType,
                    interactionType,
                    pos,
                    scene,
                    currentInteractionStartTimeRealtime,
                    now,
                    duration,
                    ts
                );

                currentInteractionId = null;
                currentInteractionStartTimeRealtime = 0.0;
            }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        public static void FlushHeatmap()
        {
            if (heatmap == null) return;

            Gossip.Instance?.HeatmapTracker?.CapFromHeatmap(
                heatmap,
                heatmapSource: "interaction",
                sparse: true,
                rowMajor: true
            );

            if (Gossip.Instance?.Settings?.EnableDebug == true)
                Debug.Log("[Interactable] Heatmap flushed (interaction)");
        }

        private void TryCaptureImage(string interactionType)
        {
            if (!captureImageOnInteraction) return;

            int objId = gameObject.GetInstanceID();
            float currentTime = Time.time;

            if (currentTime - globalLastImageTime < minTimeBetweenImages) return;

            if (lastCaptureTimes.TryGetValue(objId, out float lastTime))
            {
                if (currentTime - lastTime < objectCooldown) return;
            }

            InteractionImageTracker.Track(gameObject, interactionType);
            globalLastImageTime = currentTime;
            lastCaptureTimes[objId] = currentTime;
        }
    }
}
