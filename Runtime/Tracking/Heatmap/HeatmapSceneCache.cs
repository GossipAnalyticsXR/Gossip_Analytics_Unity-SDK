using UnityEngine;

namespace GossipSDK.Heatmaps
{
    public static class HeatmapSceneCache
    {
        private const string KeyPrefix = "GOSSIP_HEATMAP_UPLOADED_";


        public static bool WasUploaded(HeatmapSceneSpec spec)
        {
            return IsUploaded(spec);
        }

        public static bool IsUploaded(HeatmapSceneSpec spec)
        {
            string key = BuildKey(spec);
            return PlayerPrefs.GetInt(key, 0) == 1;
        }

        public static void MarkUploaded(HeatmapSceneSpec spec)
        {
            string key = BuildKey(spec);
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
        }

        private static string BuildKey(HeatmapSceneSpec spec)
        {
            return $"{KeyPrefix}{spec.SceneName}_{spec.Version}";
        }
    }
}