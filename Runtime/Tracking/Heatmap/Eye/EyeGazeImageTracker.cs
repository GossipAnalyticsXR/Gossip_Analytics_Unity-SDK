using UnityEngine;
using UnityEngine.Rendering;
using Cysharp.Threading.Tasks;
using GossipSDK.Core;
using GossipSDK.Core.Connection;
using System;

namespace GossipSDK.Heatmaps
{
    public static class EyeGazeImageTracker
    {
        public static void Track(Ray gazeRay, RaycastHit hit, float fixationDuration, string trackingSource)
        {
            TrackAsync(gazeRay, hit, fixationDuration, trackingSource).Forget();
        }

        private static async UniTaskVoid TrackAsync(Ray gazeRay, RaycastHit hit, float fixationDuration, string trackingSource)
        {
            var gossip = Gossip.Instance;
            if (gossip == null) return;

            byte[] png = await CaptureUtils.CapturePngAsync();
            if (png == null) return;

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
                if (gossip.Settings?.EnableDebug == true) Debug.LogWarning("[EyeGazeImage] No endpoint and no API key; skipping upload");
                return;
            }

            try
            {
                await endpoint.UploadEyeGazeImage(
                    gazeRay, hit, fixationDuration, trackingSource, png,
                    success => {
                        if (gossip.Settings.EnableDebug)
                            Debug.Log(success ? "[EyeGazeImage] Uploaded" : "[EyeGazeImage] Upload failed");
                    });
            }
            finally
            {
                tempEndpoint?.Dispose();
            }
        }
    }
}
