using UnityEngine;
using UnityEngine.Rendering;
using Cysharp.Threading.Tasks;
using GossipSDK.Core;
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
            if (gossip.EndpointClient == null) { if (gossip.Settings?.EnableDebug == true) Debug.LogWarning("[EyeGazeImage] EndpointClient null"); return; }

            await gossip.EndpointClient.UploadEyeGazeImage(
                gazeRay, hit, fixationDuration, trackingSource, png,
                success => {
                    if (gossip.Settings.EnableDebug)
                        Debug.Log(success ? "[EyeGazeImage] Uploaded" : "[EyeGazeImage] Upload failed");
                });
        } 
    }
}
