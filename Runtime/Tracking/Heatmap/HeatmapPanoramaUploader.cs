using System;
using UnityEngine;
using GossipSDK.Core;
using GossipSDK.Core.Connection;
using Cysharp.Threading.Tasks;

namespace GossipSDK.Heatmaps
{
public static class HeatmapPanoramaUploader
{
public static void Enqueue(HeatmapPanoramaSpec spec, byte[] jpg)
{
UploadAsync(spec, jpg).Forget();
}

private static async UniTaskVoid UploadAsync(
HeatmapPanoramaSpec spec,
byte[] jpg
)
{
var gossip = Gossip.Instance;
if (gossip == null)
return;

if (gossip.Settings.EnableDebug)
{
Debug.Log("[HeatmapPanoramaUploader] UniTask START");
Debug.Log($"Scene: {spec.SceneName}");
Debug.Log($"JPG size: {jpg.Length}");
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
Debug.LogWarning("[HeatmapPanoramaUploader] No endpoint and no API key; skipping panorama upload");
return;
}

bool success = false;
try
{
await endpoint.UploadHeatmapPanorama(
spec,
jpg,
result => success = result
);
}
finally
{
tempEndpoint?.Dispose();
}

if (success)
{
if (gossip.Settings.EnableDebug)
Debug.Log("[HeatmapPanoramaUploader] Upload SUCCESS");
}
else
{
Debug.LogWarning("[HeatmapPanoramaUploader] Upload FAILED");
}
}
}
}
