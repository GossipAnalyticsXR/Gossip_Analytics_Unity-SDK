using System;
using UnityEngine;
using GossipSDK.Core;
using GossipSDK.Core.Connection;
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

            // Wait briefly for API key (not for EndpointClient, which may never be set)
            float waited = 0f;
            while (string.IsNullOrWhiteSpace(gossip.Settings?.ApiKeyValue) && gossip.EndpointClient == null && waited < 15f)
            {
                await UniTask.Delay(500);
                waited += 0.5f;
            }

            var endpoint = gossip.EndpointClient;
            EndpointConnection tempEndpoint = null;
            if (endpoint == null && gossip.Settings != null && !string.IsNullOrWhiteSpace(gossip.Settings.ApiKeyValue))
            {
                tempEndpoint = new EndpointConnection(gossip.Settings.ApiKeyHeader, gossip.Settings.ApiKeyValue);
                endpoint = tempEndpoint;
            }

            if (endpoint == null)
            {
                Debug.LogWarning("[HeatmapUploader] No endpoint and no API key; skipping image upload");
                return;
            }

            bool success = false;
            try
            {
                await endpoint.UploadHeatmapScene(
                    spec,
                    png,
                    result => success = result
                );
            }
            finally
            {
                tempEndpoint?.Dispose();
            }

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
