using System.Collections;
using UnityEngine;
using GossipSDK.Core;
using Cysharp.Threading.Tasks;

namespace GossipSDK.Heatmaps
{
public class HeatmapPanoramaAutoCapture : MonoBehaviour
{
[SerializeField] private float captureDelay = 0.5f;
[SerializeField] private int faceSize = 2048;
[SerializeField] private int equirectW = 4096;
[SerializeField] private int equirectH = 2048;
[SerializeField] private int jpgQuality = 85;

private static bool s_captured;

private IEnumerator Start()
{
yield return new WaitForSeconds(captureDelay);

var gossip = GossipSDK.Core.Gossip.Instance;

if (gossip == null || gossip.Settings == null)
{
Debug.LogWarning("[HeatmapPanorama] skip: gossip/Settings null");
yield break;
}

if (gossip.Settings.EnableDebug) Debug.Log("[HeatmapPanorama] Start");

if (!gossip.Settings.EnableHeatmaps)
{
if (gossip.Settings.EnableDebug) Debug.Log("[HeatmapPanorama] skip: EnableHeatmaps false");
yield break;
}

if (s_captured)
{
if (gossip.Settings.EnableDebug) Debug.Log("[HeatmapPanorama] skip: already captured this session");
yield break;
}
s_captured = true;

Vector3 capturePos;
var marker = FindObjectOfType<GossipPanoramaCapturePoint>();
if ((UnityEngine.Object)marker != null)
{
capturePos = marker.transform.position;
Debug.Log("[HeatmapPanorama] using GossipPanoramaCapturePoint position=" + capturePos.ToString("F3"));
}
else if (Camera.main != null)
{
capturePos = Camera.main.transform.position;
Debug.Log("[HeatmapPanorama] using Camera.main position=" + capturePos.ToString("F3"));
}
else
{
capturePos = transform.position;
Debug.Log("[HeatmapPanorama] using rig start position=" + capturePos.ToString("F3"));
}

if (gossip.Settings.EnableDebug) Debug.Log("[HeatmapPanorama] capturing...");

var faceColors = new Color32[6][];
var bases = GossipEquirect.BuildFaceBases(0f);

yield return CaptureFaces(capturePos, bases, faceColors);

CaptureAndUploadAsync(capturePos, bases, faceColors, gossip).Forget();
}

private IEnumerator CaptureFaces(Vector3 capturePos, GossipEquirect.FaceBasis[] bases, Color32[][] faceColors)
{
GameObject camObj = new GameObject("HeatmapPanoramaCaptureCamera");
Camera cam = camObj.AddComponent<Camera>();
cam.enabled = false;
cam.clearFlags = CameraClearFlags.Skybox;
cam.fieldOfView = 90f;
cam.aspect = 1f;
cam.nearClipPlane = 0.03f;
cam.farClipPlane = 1000f;
cam.transform.position = capturePos;

RenderTexture faceRT = new RenderTexture(faceSize, faceSize, 24, RenderTextureFormat.ARGB32);
Texture2D faceTex = new Texture2D(faceSize, faceSize, TextureFormat.RGB24, false);
// Auto-hide VR hands/controllers/body: disable renderers whose bounds center is very
// close to the capture point (they always sit right by the head). Restored after capture.
const float HandHideRadius = 0.8f;
var hidden = new System.Collections.Generic.List<Renderer>();
var sceneRenderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
foreach (var rend in sceneRenderers)
{
    if (rend == null || !rend.enabled) continue;
    if ((rend.bounds.center - capturePos).sqrMagnitude <= HandHideRadius * HandHideRadius)
    {
        rend.enabled = false;
        hidden.Add(rend);
    }
}
for (int f = 0; f < 6; f++)
{
cam.transform.rotation = Quaternion.LookRotation(bases[f].forward, bases[f].up);
cam.targetTexture = faceRT;
cam.Render();

RenderTexture.active = faceRT;
faceTex.ReadPixels(new Rect(0, 0, faceSize, faceSize), 0, 0);
faceTex.Apply();
faceColors[f] = faceTex.GetPixels32();
RenderTexture.active = null;

yield return null;
}
foreach (var rend in hidden)
{
    if ((UnityEngine.Object)rend != null) rend.enabled = true;
    }
    
cam.targetTexture = null;
Object.Destroy(faceTex);
Object.Destroy(faceRT);
Object.Destroy(camObj);
}

private async UniTaskVoid CaptureAndUploadAsync(Vector3 capturePos, GossipEquirect.FaceBasis[] bases, Color32[][] faceColors, GossipSDK.Core.Gossip gossip)
{
int w = equirectW;
int h = equirectH;
int fs = faceSize;

var pixels = await UniTask.RunOnThreadPool(() => GossipEquirect.BuildEquirect(faceColors, bases, w, h, fs));

var outTex = new Texture2D(w, h, TextureFormat.RGB24, false);
outTex.SetPixels32(pixels);
outTex.Apply();
byte[] jpg = outTex.EncodeToJPG(jpgQuality);
Object.Destroy(outTex);

var spec = HeatmapPanoramaSpec.CreateForCurrentScene(capturePos, 0f, w, h);
spec.PlayerID = gossip.CurrentPlayerId;
spec.SessionID = gossip.CurrentSessionId;

Debug.Log("[HeatmapPanorama] captured position=" + capturePos.ToString("F4") + " yawOffsetDeg=0.00 width=" + w + " height=" + h);

HeatmapPanoramaUploader.Enqueue(spec, jpg);
}
}
}
