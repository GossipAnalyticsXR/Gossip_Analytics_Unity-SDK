using UnityEngine;
using System;
using System.Reflection;

namespace GossipSDK.Heatmaps
{
    /// <summary>
    /// Derives worldMinXZ / worldMaxXZ automatically from the play area.
    /// Attempt 1: OVR guardian boundary via reflection (optional package).
    /// Attempt 2: HeatmapSceneBoundsUtility scene-renderer fallback.
    /// </summary>
    public static class HeatmapBoundsResolver
    {
        public static bool ResolvePlayAreaXZ(out Vector2 min, out Vector2 max, float padding = 0.5f)
        {
            if (TryResolveFromOVR(out min, out max, padding))
                return true;

            if (TryResolveFromSceneBounds(out min, out max, padding))
                return true;

            min = default;
            max = default;
            return false;
        }

        // -- Attempt 1: OVR boundary via reflection --
        private static bool TryResolveFromOVR(out Vector2 min, out Vector2 max, float padding)
        {
            min = default;
            max = default;
            try
            {
                // OVRManager.boundary (static property, type OVRBoundary)
                Type ovrManagerType = Type.GetType("OVRManager, Assembly-CSharp");
                if (ovrManagerType == null)
                    ovrManagerType = Type.GetType("OVRManager, OculusIntegration");
                if (ovrManagerType == null)
                    return false;

                PropertyInfo boundaryProp = ovrManagerType.GetProperty(
                    "boundary", BindingFlags.Public | BindingFlags.Static);
                if (boundaryProp == null)
                    return false;

                object boundary = boundaryProp.GetValue(null);
                if (boundary == null)
                    return false;

                // boundary.GetConfigured()
                Type boundaryType = boundary.GetType();
                MethodInfo getConfigured = boundaryType.GetMethod("GetConfigured");
                if (getConfigured == null)
                    return false;

                bool configured = (bool)getConfigured.Invoke(boundary, null);
                if (!configured)
                    return false;

                // OVRBoundary.BoundaryType.PlayArea enum value
                Type ovrBoundaryType = Type.GetType("OVRBoundary, Assembly-CSharp");
                if (ovrBoundaryType == null)
                    ovrBoundaryType = Type.GetType("OVRBoundary, OculusIntegration");

                object playAreaValue = null;
                if (ovrBoundaryType != null)
                {
                    Type btEnum = ovrBoundaryType.GetNestedType("BoundaryType");
                    if (btEnum != null)
                        playAreaValue = Enum.Parse(btEnum, "PlayArea");
                }

                if (playAreaValue == null)
                    return false;

                // boundary.GetGeometry(OVRBoundary.BoundaryType.PlayArea)
                MethodInfo getGeometry = boundaryType.GetMethod("GetGeometry");
                if (getGeometry == null)
                    return false;

                var points = getGeometry.Invoke(boundary, new object[] { playAreaValue }) as Vector3[];
                if (points == null || points.Length < 3)
                    return false;

                return ComputeMinMaxXZ(points, padding, out min, out max);
            }
            catch
            {
                return false;
            }
        }

        // -- Attempt 2: scene renderer bounds --
        private static bool TryResolveFromSceneBounds(out Vector2 min, out Vector2 max, float padding)
        {
            min = default;
            max = default;
            try
            {
                Bounds b = HeatmapSceneBoundsUtility.CalculateSceneBounds();
                // Reject degenerate fallback (10x10 centred at origin = no renderers found)
                if (b.size.x <= 0f || b.size.z <= 0f)
                    return false;

                min = new Vector2(b.min.x - padding, b.min.z - padding);
                max = new Vector2(b.max.x + padding, b.max.z + padding);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // -- Helpers --
        private static bool ComputeMinMaxXZ(Vector3[] pts, float padding,
            out Vector2 min, out Vector2 max)
        {
            float minX = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxZ = float.MinValue;
            foreach (var p in pts)
            {
                if (p.x < minX) minX = p.x;
                if (p.z < minZ) minZ = p.z;
                if (p.x > maxX) maxX = p.x;
                if (p.z > maxZ) maxZ = p.z;
            }
            min = new Vector2(minX - padding, minZ - padding);
            max = new Vector2(maxX + padding, maxZ + padding);
            return true;
        }
    }
}
