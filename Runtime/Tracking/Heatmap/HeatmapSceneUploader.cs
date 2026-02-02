using UnityEngine;
using GossipSDK.Core;
using Cysharp.Threading.Tasks;

namespace GossipSDK.Heatmaps
{
    public static class HeatmapSceneUploader
    {
        public static void Enqueue(HeatmapSceneSpec spec, byte[] png)
        {
            UploadAsync(spec, png).Forget();
        }

        private static async UniTaskVoid UploadAsync(
            HeatmapSceneSpec spec,
            byte[] png
        )
        {
            var gossip = Gossip.Instance;
            if (gossip == null)
                return;

            if (gossip.Settings.EnableDebug)
            {
                Debug.Log("[HeatmapUploader] UniTask START");
                Debug.Log($"Scene: {spec.SceneName}");
                Debug.Log($"PNG size: {png.Length}");
            }

            var endpoint = gossip.EndpointClient;
            if (endpoint == null)
            {
                Debug.LogError("[HeatmapUploader] EndpointClient is NULL");
                return;
            }

            bool success = false;

            await endpoint.UploadHeatmapScene(
                spec,
                png,
                result => success = result
            );

            if (success)
            {
                HeatmapSceneCache.MarkUploaded(spec);

                if (gossip.Settings.EnableDebug)
                    Debug.Log("[HeatmapUploader] Upload SUCCESS");
            }
            else
            {
                Debug.LogWarning("[HeatmapUploader] Upload FAILED");
            }
        }
    }
}