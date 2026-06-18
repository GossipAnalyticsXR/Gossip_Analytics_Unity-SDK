using UnityEngine;
using Newtonsoft.Json;
using System;
using GossipSDK.Core;
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
            if (gossip.EndpointClient == null) { if (gossip.Settings?.EnableDebug == true) Debug.LogWarning("[InteractionImage] EndpointClient null"); return; }

            await gossip.EndpointClient.UploadInteractionImage(
                         interactedObject,
                         interactionType,
                         png,
                         success => {
                             if (gossip.Settings.EnableDebug)
                                 Debug.Log(success ? $"[InteractionImage] Uploaded: {interactedObject.name}" : "[InteractionImage] Upload failed");
                         });
        }
    }
}
