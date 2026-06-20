using UnityEngine;
using Newtonsoft.Json;
using System;
using GossipSDK.Core;
using GossipSDK.Core.Connection;
using Cysharp.Threading.Tasks;

namespace GossipSDK.Heatmaps
{
    public static class InteractionImageTracker
    {
        public static void Track(GameObject interactedObject, string interactionType)
        {
            var g = Gossip.Instance; if (g == null || g.Settings == null) return;
            if (g.Settings.SelectedEnvironment != Core.Configuration.GossipSettings.Environment.Production)
                return;

            TrackAsync(interactedObject, interactionType).Forget();
        }

        private static async UniTaskVoid TrackAsync(GameObject interactedObject, string interactionType)
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
                if (gossip.Settings?.EnableDebug == true) Debug.LogWarning("[InteractionImage] No endpoint and no API key; skipping upload");
                return;
            }

            try
            {
                await endpoint.UploadInteractionImage(
                    interactedObject,
                    interactionType,
                    png,
                    success => {
                        if (gossip.Settings.EnableDebug)
                            Debug.Log(success ? $"[InteractionImage] Uploaded: {interactedObject.name}" : "[InteractionImage] Upload failed");
                    });
            }
            finally
            {
                tempEndpoint?.Dispose();
            }
        }
    }
}
