using UnityEngine;
using UnityEngine.SceneManagement;
using GossipSDK.Core;
using System;

namespace GossipSDK.Heatmaps
{
    [DisallowMultipleComponent]
    public class HeatmapSceneCapture : MonoBehaviour
    {
        [Header("Capture Camera")]
        [SerializeField] private Camera captureCamera;

        [Header("World Bounds (XZ)")]
        [SerializeField] private Vector2 worldMinXZ;
        [SerializeField] private Vector2 worldMaxXZ;

        [Header("Output")]
        [SerializeField] private int imageSize = 2048;

        [Header("Debug")]
        [SerializeField] private bool logDebug = true;

        private bool captured;

        private void Awake()
        {
            if (captureCamera == null)
            {
                Debug.LogError("[HeatmapSceneCapture] Capture camera not assigned.");
                enabled = false;
                return;
            }

            captureCamera.orthographic = true;
            captureCamera.enabled = false; // no render a pantalla
        }

        private void Start()
        {
            if (!IsHeatmapEnabled()) return;

            // Solo una vez por escena
            if (!captured)
            {
                CaptureScene();
                captured = true;
            }
        }

        private bool IsHeatmapEnabled()
        {
            var gossip = Gossip.Instance;
            return gossip != null &&
                   gossip.Settings != null &&
                   gossip.Settings.EnableHeatmaps;
        }

        private void CaptureScene()
        {
            if (logDebug)
                Debug.Log("[HeatmapSceneCapture] Capturing heatmap scene image");

            var rt = new RenderTexture(imageSize, imageSize, 24, RenderTextureFormat.ARGB32);
            var prevRT = RenderTexture.active;

            captureCamera.targetTexture = rt;
            RenderTexture.active = rt;

            captureCamera.Render();

            var tex = new Texture2D(imageSize, imageSize, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, imageSize, imageSize), 0, 0);
            tex.Apply();

            captureCamera.targetTexture = null;
            RenderTexture.active = prevRT;
            Destroy(rt);

            var png = tex.EncodeToPNG();
            Destroy(tex);

            var spec = BuildSceneSpec();

            if (logDebug)
            {
                Debug.Log(
                    $"[HeatmapSceneCapture] Scene captured\n" +
                    $"Scene={spec.SceneName}\n" +
                    $"Bounds=({spec.MinX},{spec.MinZ}) > ({spec.MaxX},{spec.MaxZ})"
                );
            }

            HeatmapSceneUploader.Enqueue(spec, png);
        }

        private HeatmapSceneSpec BuildSceneSpec()
        {
            return new HeatmapSceneSpec
            {
                SceneName = SceneManager.GetActiveScene().name,
                ImageWidth = imageSize,
                ImageHeight = imageSize,
                MinX = worldMinXZ.x,
                MaxX = worldMaxXZ.x,
                MinZ = worldMinXZ.y,
                MaxZ = worldMaxXZ.y,
                UpAxis = "Y",
                Version = Application.version,
                TimestampUtc = DateTime.UtcNow.ToString("o")
            };
        }
    }
}
