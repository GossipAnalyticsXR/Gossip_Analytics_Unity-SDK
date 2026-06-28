using UnityEngine;

namespace GossipSDK.Tracking
{
    public static class BoundaryPressureHelper
    {
        // Point-in-polygon: ray-casting algorithm on XZ plane
        public static bool Contains(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            int n = poly.Length;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = poly[i].x, yi = poly[i].y;
                float xj = poly[j].x, yj = poly[j].y;
                bool intersect = ((yi > p.y) != (yj > p.y))
                    && (p.x < (xj - xi) * (p.y - yi) / (yj - yi) + xi);
                if (intersect)
                    inside = !inside;
            }
            return inside;
        }

        // Minimum distance from point p to the nearest edge of poly
        public static float DistanceToNearestBoundary(Vector2[] poly, Vector2 p)
        {
            float minDist = float.MaxValue;
            int n = poly.Length;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float d = PointToSegmentDistance(p, poly[j], poly[i]);
                if (d < minDist)
                    minDist = d;
            }
            return minDist;
        }

        private static float PointToSegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 1e-10f)
                return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
            Vector2 proj = a + t * ab;
            return Vector2.Distance(p, proj);
        }
    }
}
