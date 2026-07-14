using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GossipSDK.Heatmaps
{
    [Serializable]
    public class HeatmapSceneSpec
    {
        public string PlayerID;
        public string SessionID;

        public string SceneName;

        public int ImageWidth;
        public int ImageHeight;

        public float MinX;
        public float MaxX;
        public float MinZ;
        public float MaxZ;

        public string UpAxis = "Y";
        public string Version;
        public string TimestampUtc;

        public HeatmapOccluder[] Occluders;

        public static HeatmapSceneSpec CreateCurrentSceneSpec(int width = 2048, int height = 2048)
        {
            var bounds = HeatmapSceneBoundsUtility.CalculateSceneBounds();

            return new HeatmapSceneSpec
            {
                SceneName = SceneManager.GetActiveScene().name,
                ImageWidth = width,
                ImageHeight = height,

                MinX = bounds.min.x,
                MaxX = bounds.max.x,
                MinZ = bounds.min.z,
                MaxZ = bounds.max.z,

                Version = Application.version,
                TimestampUtc = DateTime.UtcNow.ToString("o"),
                Occluders = HeatmapOccluderUtility.Collect(),
            };
        }
    }
}
