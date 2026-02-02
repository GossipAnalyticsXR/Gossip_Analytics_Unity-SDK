using System;
using UnityEngine;
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

                float area = width * depth;

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
