using UnityEngine;
using UnityEngine.SceneManagement;
using GossipSDK.Core;
using GossipSDK.Heatmaps;
using System;
using GossipSDK.Tracking;
using GossipSDK.XR;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class EyeTrackingComponent : MonoBehaviour
    {
        [Header("Fixation")]
        [SerializeField] private float fixationThreshold = 0.25f;
        [SerializeField] private float maxDistance = 20f;
        [SerializeField] private LayerMask raycastLayers = ~0;

        [Header("Image Throttling")]
        [SerializeField] private float minDistanceDelta = 0.5f;
        [SerializeField] private float minRotationDelta = 10f;
        [SerializeField] private float minTimeBetweenImages = 5f;
        [SerializeField] private float headFallbackCooldownMultiplier = 1.5f;
        [SerializeField] private float headFallbackThresholdMultiplier = 2.5f;

        [Header("Heatmap")]
        [SerializeField] private Vector2 worldMinXZ = new(-5, -5);
        [SerializeField] private Vector2 worldMaxXZ = new(5, 5);
        [SerializeField] private float cellSizeMeters = 0.5f;
        [SerializeField] private float heatmapFlushInterval = 10f;

        [Header("Auto Bounds")]
        [SerializeField] public bool autoBounds = true;

        public Transform cam;
        private string sceneName;

        private HeatmapManager heatmap;
        private float fixationTimer;
        private float heatmapTimer;

        private GameObject currentObject;
        private GameObject lastImageObject;

        private Vector3 lastCamPos;
        private Quaternion lastCamRot;
        private float lastImageTime;

        private const string SOURCE_EYE = "eye";
        private const string SOURCE_HEAD = "head";

        private void Awake()
        {
            sceneName = SceneManager.GetActiveScene().name;

            if (cam != null)
            {
                lastCamPos = cam.position;
                lastCamRot = cam.rotation;
            }

            lastImageTime = -minTimeBetweenImages;
        }

        private void Start()
        {
            if (autoBounds && HeatmapBoundsResolver.ResolvePlayAreaXZ(
                out Vector2 resolvedMin, out Vector2 resolvedMax))
            {
                worldMinXZ = resolvedMin;
                worldMaxXZ = resolvedMax;
            }

            heatmap = new HeatmapManager(sceneName, worldMinXZ, worldMaxXZ, cellSizeMeters);
        }

        private void Update()
        {
            if (cam == null)
                return;

            heatmapTimer += Time.deltaTime;

            Ray gazeRay;
            string source;

            var eyeProvider = XRBootstrap.EyeGaze;

            if (eyeProvider != null &&
                eyeProvider.IsAvailable &&
                eyeProvider.TryGetEyeGaze(out gazeRay))
            {
                source = eyeProvider.TrackingSource;
            }
            else if (XRBootstrap.HeadPose != null &&
                     XRBootstrap.HeadPose.TryGetPose(out Vector3 pos, out Quaternion rot))
            {
                gazeRay = new Ray(pos, rot * Vector3.forward);
                source = SOURCE_HEAD;
            }
            else
            {
                return;
            }

            if (!Physics.Raycast(gazeRay, out RaycastHit hit, maxDistance, raycastLayers))
            {
                currentObject = null;
                fixationTimer = 0f;
                return;
            }

            heatmap.RegisterHit(hit.point);

            if (hit.collider.gameObject != currentObject)
            {
                currentObject = hit.collider.gameObject;
                fixationTimer = 0f;
                return;
            }

            fixationTimer += Time.deltaTime;

            if (fixationTimer < fixationThreshold)
                return;

            ProcessFixation(hit, gazeRay, source);
            fixationTimer = 0f;

            if (heatmapTimer >= heatmapFlushInterval)
            {
                FlushHeatmap();
                heatmapTimer = 0f;
            }
        }

        private void ProcessFixation(RaycastHit hit, Ray gazeRay, string source)
        {
            SendFixation(hit, fixationTimer, source);

            if (!ShouldSendImage(hit, source))
                return;

            if (Gossip.Instance?.Settings?.SelectedEnvironment
                == Core.Configuration.GossipSettings.Environment.Production)
            {
                EyeGazeImageTracker.Track(gazeRay, hit, fixationTimer, source);
                CacheImageState(hit);
            }
        }

        private bool ShouldSendImage(RaycastHit hit, string source)
        {
            float cooldown = source == SOURCE_HEAD
                ? minTimeBetweenImages * headFallbackCooldownMultiplier
                : minTimeBetweenImages;

            if (Time.time - lastImageTime < cooldown)
                return false;

            if (hit.collider.gameObject != lastImageObject)
                return true;

            float dist = Vector3.Distance(cam.position, lastCamPos);
            float angle = Quaternion.Angle(cam.rotation, lastCamRot);

            float mul = source == SOURCE_HEAD ? headFallbackThresholdMultiplier : 1f;

            return dist > minDistanceDelta * mul
                || angle > minRotationDelta * mul;
        }

        private void CacheImageState(RaycastHit hit)
        {
            lastImageTime = Time.time;
            lastImageObject = hit.collider.gameObject;
            lastCamPos = cam.position;
            lastCamRot = cam.rotation;
        }

        private void SendFixation(RaycastHit hit, float duration, string source)
        {
            var tracker = Gossip.Instance?.EyeTrackingTracker;
            if (tracker == null)
                return;

            var go = hit.collider.gameObject;

            tracker.Capture(new Tracking.GameplayMetrics.EyeTrackingTracker.EntityData
            {
                HitObjectName = go.name,
                HitObjectTag = go.tag,
                HitX = hit.point.x,
                HitY = hit.point.y,
                HitZ = hit.point.z,
                FixationDurationSeconds = duration,
                SceneName = sceneName,
                TrackingSource = source,
                TimestampUtc = DateTime.UtcNow.ToString("o")
            });
        }

        private void FlushHeatmap()
        {
            if (heatmap == null) return;
            Gossip.Instance?.HeatmapTracker?
                .CapFromHeatmap(heatmap, "eye_gaze", true);
        }

        private void OnDisable() => FlushHeatmap();
    }
}
