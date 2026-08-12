using System.Collections.Generic;
using UnityEngine;
using GossipSDK.Core;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace GossipSDK.Heatmaps
{
    // Auto-detects "zones of heat" (dwell = user stands still) and captures a 360 panorama
    // at each one, trickled across several frames so no single frame pays the full 6-face
    // render+readback cost. No dev configuration, no markers -- positions are discovered at
    // runtime from user behavior. This component is additive: it does not touch
    // HeatmapPanoramaAutoCapture (the setup-baseline capture keeps working unchanged).
    //
    // FPS guardrail (the whole point of this component): never render during locomotion,
    // only while the user is holding still (dwell); at most one cube face per frame while
    // trickling; the equirect assembly runs on the threadpool; upload reuses the existing
    // pipeline. If the user moves away mid-trickle, the partial capture is discarded.
    public class HeatmapDwellPanoramaCapture : MonoBehaviour
    {
        [Header("Dwell detection")]
        [SerializeField] private float dwellSeconds = 2.0f;
        [SerializeField] private float dwellStillRadius = 0.5f;
        [SerializeField] private float newZoneRadius = 1.5f;
        [SerializeField] private float abortRadius = 0.75f;
        [SerializeField] private float faceInterval = 0.5f;
        [SerializeField] private int maxZonesPerSession = 10;

        [Header("Capture quality (dwell captures; the setup baseline uses its own settings)")]
        [SerializeField] private int faceSize = 1024;
        [SerializeField] private int equirectW = 2048;
        [SerializeField] private int equirectH = 1024;
        [SerializeField] private int jpgQuality = 85;

        // Hands/controllers/body always sit right next to the head (the capture point), so
        // any renderer whose bounds center is within this radius is hidden for the
        // duration of the trickle and restored right after. Same rule as the setup baseline.
        private const float HandHideRadius = 0.8f;

        private readonly List<Vector3> capturedZones = new List<Vector3>();

        // Cached main camera; refreshed only when null (e.g. after a scene reload) to avoid
        // paying a Camera.main lookup every single frame.
        private Camera cachedMainCamera;

        // Dwell accumulator (only relevant while NOT trickling a capture).
        private bool hasDwellAnchor;
        private Vector3 dwellAnchor;
        private float dwellTimer;

        // Trickle capture state machine (only relevant while isCapturing).
        private bool isCapturing;
        private Vector3 captureAnchor;
        private GameObject camObj;
        private Camera cam;
        private RenderTexture faceRT;
        private Texture2D faceTex;
        private GossipEquirect.FaceBasis[] faceBases;
        private Color32[][] faceColors;
        private int nextFace;
        private float faceTimer;
        private List<Renderer> hiddenRenderers;

        private void Update()
        {
            var gossip = Gossip.Instance;
            if (gossip == null || gossip.Settings == null || !gossip.Settings.EnableHeatmaps) return;

            if (cachedMainCamera == null) cachedMainCamera = Camera.main;
            if (cachedMainCamera == null) return;
            Vector3 headPos = cachedMainCamera.transform.position;

            if (isCapturing) TickCapture(headPos);
            else TickDwellDetection(headPos);
        }

        private void TickDwellDetection(Vector3 headPos)
        {
            if (capturedZones.Count >= maxZonesPerSession) return;

            if (!hasDwellAnchor)
            {
                dwellAnchor = headPos;
                dwellTimer = 0f;
                hasDwellAnchor = true;
                return;
            }

            if (Vector3.Distance(headPos, dwellAnchor) > dwellStillRadius)
            {
                // Moved outside the still-radius: restart the anchor at the new spot.
                dwellAnchor = headPos;
                dwellTimer = 0f;
                return;
            }

            dwellTimer += Time.deltaTime;
            if (dwellTimer < dwellSeconds) return;

            // Confirmed dwell (zone-of-heat candidate). Dedup against zones already
            // captured this session before starting a new trickle.
            for (int i = 0; i < capturedZones.Count; i++)
            {
                if (Vector3.Distance(dwellAnchor, capturedZones[i]) < newZoneRadius)
                {
                    // Not a new zone -- reset and wait for the user to dwell elsewhere.
                    hasDwellAnchor = false;
                    return;
                }
            }

            StartCapture(dwellAnchor);
        }

        private void StartCapture(Vector3 anchor)
        {
            isCapturing = true;
            hasDwellAnchor = false;
            captureAnchor = anchor;
            nextFace = 0;
            faceTimer = 0f;
            faceColors = new Color32[6][];
            faceBases = GossipEquirect.BuildFaceBases(0f);

            camObj = new GameObject("HeatmapDwellPanoramaCaptureCamera");
            cam = camObj.AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 90f;
            cam.aspect = 1f;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 1000f;
            cam.transform.position = captureAnchor;

            faceRT = new RenderTexture(faceSize, faceSize, 24, RenderTextureFormat.ARGB32);
            faceTex = new Texture2D(faceSize, faceSize, TextureFormat.RGB24, false);

            // Collect VR hands/controllers/body renderers near the capture point; hidden
            // per-face around each cam.Render() (see RenderFace) so the user's own view never
            // loses them for a whole frame -- only the capture camera's view does.
            hiddenRenderers = new List<Renderer>();
            var sceneRenderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (var rend in sceneRenderers)
            {
                if (rend == null || !rend.enabled) continue;
                if ((rend.bounds.center - captureAnchor).sqrMagnitude <= HandHideRadius * HandHideRadius)
                {
                    hiddenRenderers.Add(rend);
                }
            }
        }

        private void TickCapture(Vector3 headPos)
        {
            if (Vector3.Distance(headPos, captureAnchor) > abortRadius)
            {
                AbortCapture();
                return;
            }

            faceTimer += Time.deltaTime;
            if (faceTimer < faceInterval) return;
            faceTimer = 0f;

            RenderFace(nextFace);
            nextFace++;

            if (nextFace >= 6) FinishCapture();
        }

private void RenderFace(int f)
{
// Hide near renderers ONLY for this face, saving each one's enabled-state right now
// and restoring exactly that -- so if an owner (e.g. XR tracking) disabled one between
// faces, we don't force it back on.
int n = hiddenRenderers != null ? hiddenRenderers.Count : 0;
bool[] prevEnabled = n > 0 ? new bool[n] : null;
for (int i = 0; i < n; i++)
{
var r = hiddenRenderers[i];
if ((UnityEngine.Object)r == null) continue;
prevEnabled[i] = r.enabled;
r.enabled = false;
}

cam.transform.rotation = Quaternion.LookRotation(faceBases[f].forward, faceBases[f].up);
cam.targetTexture = faceRT;
cam.Render();

RenderTexture.active = faceRT;
faceTex.ReadPixels(new Rect(0, 0, faceSize, faceSize), 0, 0);
faceTex.Apply();
faceColors[f] = faceTex.GetPixels32();
RenderTexture.active = null;

for (int i = 0; i < n; i++)
{
var r = hiddenRenderers[i];
if ((UnityEngine.Object)r == null) continue;
r.enabled = prevEnabled[i];
}
}

        private void RestoreHiddenRenderers()
        {
            if (hiddenRenderers == null) return;
            foreach (var rend in hiddenRenderers)
            {
                if ((UnityEngine.Object)rend != null) rend.enabled = true;
            }
            hiddenRenderers = null;
        }

        private void CleanupCameraResources()
        {
            if (cam != null) cam.targetTexture = null;
            if (faceTex != null) { Object.Destroy(faceTex); faceTex = null; }
            if (faceRT != null) { Object.Destroy(faceRT); faceRT = null; }
            if (camObj != null) { Object.Destroy(camObj); camObj = null; }
            cam = null;
        }

        private void AbortCapture()
        {
            RestoreHiddenRenderers();
            CleanupCameraResources();
            isCapturing = false;

            var gossip = Gossip.Instance;
            if (gossip != null && gossip.Settings != null && gossip.Settings.EnableDebug)
                Debug.Log("[HeatmapDwellPanorama] trickle aborted -- user moved beyond AbortRadius");
        }

private void FinishCapture()
        {
            RestoreHiddenRenderers();
            CleanupCameraResources();
            isCapturing = false;

            var capturedPos = captureAnchor;
            var bases = faceBases;
            var colors = faceColors;
            capturedZones.Add(capturedPos);

            var gossip = Gossip.Instance;
            if (gossip == null) return;

            // Bind scene metadata NOW (synchronously), before the threadpool assembly, so a
            // scene change during BuildAndUploadAsync cannot mislabel this capture's scene.
            var spec = HeatmapPanoramaSpec.CreateForCurrentScene(capturedPos, 0f, equirectW, equirectH);
            spec.PlayerID = gossip.CurrentPlayerId;
            spec.SessionID = gossip.CurrentSessionId;

            BuildAndUploadAsync(spec, bases, colors, gossip).Forget();
        }

        private async UniTaskVoid BuildAndUploadAsync(HeatmapPanoramaSpec spec, GossipEquirect.FaceBasis[] bases, Color32[][] colors, Gossip gossip)
        {
            int w = equirectW;
            int h = equirectH;
            int fs = faceSize;

            var pixels = await UniTask.RunOnThreadPool(() => GossipEquirect.BuildEquirect(colors, bases, w, h, fs));

            var outTex = new Texture2D(w, h, TextureFormat.RGB24, false);
            outTex.SetPixels32(pixels);
            outTex.Apply();
            byte[] jpg = outTex.EncodeToJPG(jpgQuality);
            Object.Destroy(outTex);

            if (gossip.Settings != null && gossip.Settings.EnableDebug)
                Debug.Log("[HeatmapDwellPanorama] captured dwell zone position=(" + spec.PositionX.ToString("F4") + ", " + spec.PositionY.ToString("F4") + ", " + spec.PositionZ.ToString("F4") + ") width=" + w + " height=" + h + " zonesThisSession=" + capturedZones.Count);

            HeatmapPanoramaUploader.Enqueue(spec, jpg);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // New scene: abort any in-flight trickle (the temp capture camera lived in the
            // old scene and is now gone), and reset per-scene dedup + zone budget so each
            // scene gets its own set of dwell captures with no cross-scene XZ dedup.
            if (isCapturing)
            {
                RestoreHiddenRenderers();
                CleanupCameraResources();
                isCapturing = false;
            }
            capturedZones.Clear();
            hasDwellAnchor = false;
            dwellTimer = 0f;
            cachedMainCamera = null;
        }

        private void OnDestroy()
        {
            RestoreHiddenRenderers();
            CleanupCameraResources();
        }
    }
}
