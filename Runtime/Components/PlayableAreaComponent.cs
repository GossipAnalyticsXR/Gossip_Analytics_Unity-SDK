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

        private void Start()
        {
            if (autoReportOnStart)
                ReportPlayableArea();
        }

        public void ReportPlayableArea()
        {
            try
            {
                var tracker = Gossip.Instance?.PlayableAreaTracker;
                if (tracker == null) return;

                Bounds bounds = GetBounds();
                float width = bounds.size.x;
                float height = bounds.size.y;
                float depth = bounds.size.z;

                float area   = GetPlayableAreaSquareMeters();
                var data = new PlayableAreaTracker.EntityData
                {
                    AreaType = ResolveAreaType(),
                    Width = width,
                    Height = height,
                    Depth = depth,
                    AreaSquareMeters = area,
                    SceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    TimestampUtc = DateTime.UtcNow.ToString("o")
                };

                tracker.CapSession(data);

                if (Gossip.Instance.Settings?.EnableDebug == true)
                {
                    Debug.Log(
                        $"[PlayableArea] {data.AreaType} area={area:F2}m² size=({width:F1},{height:F1},{depth:F1})"
                    );
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


        private float GetPlayableAreaSquareMeters()
        {
        #if UNITY_ANDROID && !UNITY_EDITOR
            // Intento 1: Meta OpenXR boundary (Quest 2/3/Pro)
            try
            {
                var boundary = OVRManager.boundary;
                if (boundary != null && boundary.GetConfigured())
                {
                    Vector3[] points = boundary.GetGeometry(OVRBoundary.BoundaryType.PlayArea);
                    if (points != null && points.Length >= 3)
                        return CalculatePolygonArea(points);
                }
            }
            catch { /* OVRManager no disponible en este dispositivo */ }

            // Intento 2: Unity XR subsystem generico
            try
            {
                var inputSubsystems = new List<XRInputSubsystem>();
                SubsystemManager.GetInstances(inputSubsystems);
                if (inputSubsystems.Count > 0)
                {
                    var pts = new List<Vector3>();
                    if (inputSubsystems[0].TryGetBoundaryPoints(pts) && pts.Count >= 3)
                        return CalculatePolygonArea(pts.ToArray());
                }
            }
            catch { /* subsistema no disponible */ }
        #endif

            // Fallback: misma logica que antes (Collider > Renderer > 0)
            Bounds b = GetBounds();
            return b.size.x * b.size.z;
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
