using System;
using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;
using GossipSDK.Core;
using GossipSDK.Tracking.PlatformSpecification;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class PlayableAreaComponent : MonoBehaviour
    {
        public bool autoReportOnStart = true;

        [Tooltip("Optional: override area type (2D, 3D, XR)")]
        public string areaTypeOverride = "";

        // -- proxy tracking fields --
        private Transform _tracked;
        private Vector2 _minXZ;
        private Vector2 _maxXZ;
        private bool _hasSamples = false;
        private float _reportTimer = 0f;

        private void Start()
        {
            _tracked = Camera.main != null ? Camera.main.transform : transform;
            // No immediate report: wait for Update to accumulate samples
        }

        private void Update()
        {
            Vector3 pos = _tracked.position;
            if (!_hasSamples)
            {
                _minXZ = new Vector2(pos.x, pos.z);
                _maxXZ = new Vector2(pos.x, pos.z);
                _hasSamples = true;
            }
            else
            {
                _minXZ.x = Mathf.Min(_minXZ.x, pos.x);
                _minXZ.y = Mathf.Min(_minXZ.y, pos.z);
                _maxXZ.x = Mathf.Max(_maxXZ.x, pos.x);
                _maxXZ.y = Mathf.Max(_maxXZ.y, pos.z);
            }

            _reportTimer += Time.deltaTime;
            if (_reportTimer >= 30f)
            {
                _reportTimer = 0f;
                ReportPlayableArea();
            }
        }

        private void OnApplicationQuit()
        {
            if (_hasSamples)
                ReportPlayableArea();
        }

        private void OnDisable()
        {
            if (_hasSamples)
                ReportPlayableArea();
        }

        public void ReportPlayableArea()
        {
            try
            {
                var tracker = Gossip.Instance?.PlayableAreaTracker;
                if (tracker == null) return;

                float area = 0f;
                float width = 0f;
                float height = 0f;
                float depth = 0f;
                string resolvedAreaType = "";

#if UNITY_ANDROID && !UNITY_EDITOR
                // Path (a): Meta OVR guardian boundary
                try
                {
                    var boundary = OVRManager.boundary;
                    if (boundary != null && boundary.GetConfigured())
                    {
                        Vector3[] points = boundary.GetGeometry(OVRBoundary.BoundaryType.PlayArea);
                        if (points != null && points.Length >= 3)
                        {
                            area = CalculatePolygonArea(points);
                            resolvedAreaType = "guardian";
                        }
                    }
                }
                catch { /* OVRManager not available */ }
#endif

                // Path (b): Unity XR subsystem generic (all subsystems)
                if (string.IsNullOrEmpty(resolvedAreaType))
                {
                    try
                    {
                        var inputSubsystems = new List<XRInputSubsystem>();
                        SubsystemManager.GetInstances(inputSubsystems);
                        for (int i = 0; i < inputSubsystems.Count; i++)
                        {
                            var pts = new List<Vector3>();
                            if (inputSubsystems[i].TryGetBoundaryPoints(pts) && pts.Count >= 3)
                            {
                                area = CalculatePolygonArea(pts.ToArray());
                                resolvedAreaType = "guardian";
                                break;
                            }
                        }
                    }
                    catch { /* subsystem not available */ }
                }

                // Path (c): proxy from accumulated movement bounding box
                if (string.IsNullOrEmpty(resolvedAreaType))
                {
                    if (_hasSamples)
                    {
                        width = _maxXZ.x - _minXZ.x;
                        depth = _maxXZ.y - _minXZ.y;
                        area = width * depth;
                        height = 0f;
                        resolvedAreaType = "proxy_used_area";
                    }
                }

                // Path (d): no data at all -- skip report, do not send zeros
                if (string.IsNullOrEmpty(resolvedAreaType))
                    return;

                // For guardian paths extract width/depth from bounds for metadata
                if (resolvedAreaType == "guardian")
                {
                    Bounds b = GetBounds();
                    width = b.size.x;
                    height = b.size.y;
                    depth = b.size.z;
                }

                string finalAreaType = !string.IsNullOrEmpty(areaTypeOverride)
                    ? areaTypeOverride
                    : resolvedAreaType;

                var data = new PlayableAreaTracker.EntityData
                {
                    AreaType = finalAreaType,
                    Width = width,
                    Height = height,
                    Depth = depth,
                    AreaSquareMeters = area,
                    SceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    TimestampUtc = DateTime.UtcNow.ToString("o")
                };

                tracker.CapSession(data);

                if (Gossip.Instance?.Settings?.EnableDebug == true)
                {
                    Debug.Log("[PlayableArea] via=" + resolvedAreaType
                        + " area=" + area.ToString("F2")
                        + " w=" + width.ToString("F1")
                        + " d=" + depth.ToString("F1"));
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private Bounds GetBounds()
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
                return col.bounds;

            Renderer rend = GetComponent<Renderer>();
            if (rend != null)
                return rend.bounds;

            return new Bounds(transform.position, Vector3.zero);
        }

        private float CalculatePolygonArea(Vector3[] pts)
        {
            float area = 0f;
            int n = pts.Length;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = pts[i];
                Vector3 b = pts[(i + 1) % n];
                area += a.x * b.z - b.x * a.z;
            }
            return Mathf.Abs(area) * 0.5f;
        }

        private string ResolveAreaType()
        {
            if (!string.IsNullOrEmpty(areaTypeOverride))
                return areaTypeOverride;

            if (UnityEngine.XR.XRSettings.isDeviceActive)
                return "XR";

            return "3D";
        }
    }
}
