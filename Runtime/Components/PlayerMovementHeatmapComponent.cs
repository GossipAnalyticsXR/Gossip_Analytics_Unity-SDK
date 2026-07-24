using UnityEngine;
using UnityEngine.SceneManagement;
using GossipSDK.Core;
using GossipSDK.Tracking;
using GossipSDK.Heatmaps;
using System;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class PlayerMovementHeatmapComponent : MonoBehaviour
    {
        [Header("Heatmap Settings")]
        public Vector2 worldMinXZ = new Vector2(-5, -5);
        public Vector2 worldMaxXZ = new Vector2(5, 5);
        public float cellSizeMeters = 1.0f;
        public float sampleInterval = 0.25f;
        public float heatmapFlushInterval = 10f;

        [Header("Auto Bounds")]
        [SerializeField] public bool autoBounds = true;

        private HeatmapManager heatmapManager;
        private float sampleTimer;
        private float flushTimer;

        private void Start()
        {

            if (autoBounds && HeatmapBoundsResolver.ResolveSceneBoundsXZ(
                out Vector2 resolvedMin, out Vector2 resolvedMax))
            {
                worldMinXZ = resolvedMin;
                worldMaxXZ = resolvedMax;
            }

            heatmapManager = new HeatmapManager(
                SceneManager.GetActiveScene().name,
                worldMinXZ,
                worldMaxXZ,
                cellSizeMeters
            );

            // Diagnóstico Build 1: modo de tracking real que resolvió el runtime + puntos de guardián.
            var _diagSubs = new System.Collections.Generic.List<UnityEngine.XR.XRInputSubsystem>();
            UnityEngine.SubsystemManager.GetInstances(_diagSubs);
            if (_diagSubs.Count > 0)
            {
            var _pts = new System.Collections.Generic.List<Vector3>();
            bool _ok = _diagSubs[0].TryGetBoundaryPoints(_pts);
            Debug.Log($"[GossipDiag] trackingOrigin={_diagSubs[0].GetTrackingOriginMode()} boundaryOk={_ok} boundaryPoints={_pts.Count} sceneMin={worldMinXZ} sceneMax={worldMaxXZ}");
            }
        }

        private void Update()
        {

            sampleTimer += Time.deltaTime;
            flushTimer += Time.deltaTime;

            if (sampleTimer >= sampleInterval)
            {
                sampleTimer = 0f;
                RegisterPosition();
            }

            if (flushTimer >= heatmapFlushInterval)
            {
                FlushHeatmap();
                flushTimer = 0f;
            }
        }

        private void RegisterPosition()
        {
            if (heatmapManager == null) return;

            Vector3 pos = transform.position;
            heatmapManager.RegisterHit(pos);
        }

        private void FlushHeatmap()
        {
            var tracker = Gossip.Instance?.HeatmapTracker;
            if (tracker == null || heatmapManager == null) return;

            tracker.CapFromHeatmap(
                heatmapManager,
                heatmapSource: "player_movement",
                sparse: true
            );

            Debug.Log($"[GossipDiag] movement hits registered={heatmapManager.HitsRegistered} discarded={heatmapManager.HitsDiscarded}");

            if (Gossip.Instance?.Settings?.EnableDebug == true)
                Debug.Log("[Heatmap] Player movement heatmap flushed");
        }

        private void OnDisable()
        {
            FlushHeatmap();
        }
    }
}
