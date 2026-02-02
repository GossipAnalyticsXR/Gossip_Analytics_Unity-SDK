using UnityEngine;

namespace GossipSDK.Heatmaps
{
    public static class HeatmapSceneBoundsUtility
    {
        public static Bounds CalculateSceneBounds()
        {
            var renderers = Object.FindObjectsOfType<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(Vector3.zero, Vector3.one * 10f);

            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return bounds;
        }
    }
}