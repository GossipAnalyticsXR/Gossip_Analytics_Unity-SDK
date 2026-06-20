using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using GossipSDK.Core;

namespace GossipSDK.Heatmaps
{
    public class HeatmapSceneAutoCapture : MonoBehaviour
    {
        [SerializeField] private int textureSize = 2048;
        [SerializeField] private float padding = 2f;
        [SerializeField] private float captureDelay = 0.5f;
        [SerializeField] private float cameraHeight = 100f;

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(captureDelay);

            var gossip = GossipSDK.Core.Gossip.Instance;

            if (gossip == null || gossip.Settings == null)
            {
                Debug.LogWarning("[HeatmapScene] skip: gossip/Settings null");
                yield break;
            }

            if (gossip.Settings.EnableDebug) Debug.Log("[HeatmapScene] Start");

            if (!gossip.Settings.EnableHeatmaps)
            {
                if (gossip.Settings.EnableDebug) Debug.Log("[HeatmapScene] skip: EnableHeatmaps false");
                yield break;
            }

            var spec = HeatmapSceneSpec.CreateCurrentSceneSpec();
            spec.PlayerID  = gossip.CurrentPlayerId;
            spec.SessionID = gossip.CurrentSessionId;

            if (HeatmapSceneCache.WasUploaded(spec))
            {
                if (gossip.Settings.EnableDebug) Debug.Log($"[HeatmapScene] skip: WasUploaded true ({spec.SceneName}_{spec.Version})");
                yield break;
            }

            if (gossip.Settings.EnableDebug) Debug.Log("[HeatmapScene] capturing...");
            yield return CaptureAndUpload(spec);
        }

        private IEnumerator CaptureAndUpload(HeatmapSceneSpec spec)
        {
            Bounds bounds = HeatmapSceneBoundsUtility.CalculateSceneBounds();

            GameObject camObj = new GameObject("HeatmapCaptureCamera");
            Camera cam = camObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.enabled = false;

            float size = Mathf.Max(bounds.extents.x, bounds.extents.z) + padding;
            cam.orthographicSize = size;

            cam.transform.position = bounds.center + Vector3.up * cameraHeight;
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            RenderTexture rt = new RenderTexture(textureSize, textureSize, 24);
            cam.targetTexture = rt;

            yield return null;
            cam.Render();

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(textureSize, textureSize, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, textureSize, textureSize), 0, 0);
            tex.Apply();

            byte[] png = tex.EncodeToPNG();

            RenderTexture.active = null;
            cam.targetTexture = null;

            Object.Destroy(rt);
            Object.Destroy(tex);
            Object.Destroy(camObj);

            HeatmapSceneUploader.Enqueue(spec, png);
        }
    }
}
