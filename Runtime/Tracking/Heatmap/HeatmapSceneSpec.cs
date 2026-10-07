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

        /// <summary>
        /// Con que regla se calculo el marco. Sella el convenio en el dato: sin esto
        /// nadie puede saber si un MinX/MaxX guardado es de los 100 m de antes o del
        /// marco instrumentado, y eso es lo que costo un ano cazar en la panoramica.
        /// </summary>
        public string BoundsRule;
        public string TimestampUtc;

        public HeatmapOccluder[] Occluders;

        public static HeatmapSceneSpec CreateCurrentSceneSpec(int width = 2048, int height = 2048)
        {
            string rule;
            var bounds = HeatmapSceneBoundsUtility.CalculateFrame(out rule);

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
                BoundsRule = rule,
                TimestampUtc = DateTime.UtcNow.ToString("o"),
                Occluders = HeatmapOccluderUtility.Collect(),
            };
        }
    }
}
