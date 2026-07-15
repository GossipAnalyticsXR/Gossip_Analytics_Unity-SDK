using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace GossipSDK.Heatmaps
{
    // SPIKE aislado: captura color + profundidad de Camera.main y guarda 2 PNG en persistentDataPath.
    // No toca ningun tracker ni eye-gaze. Trigger: click derecho en el componente -> "Capture Depth Test".
    public class GossipDepthCaptureTest : MonoBehaviour
    {
        [SerializeField] private int resolution = 512;
        private Material _depthMat;

        [ContextMenu("Capture Depth Test")]
        public void CaptureDepthTest()
        {
            var cam = Camera.main;
            if (cam == null) { Debug.LogError("[DepthTest] No Camera.main"); return; }
            if (_depthMat == null)
            {
                var sh = Shader.Find("Gossip/DepthEncode");
                if (sh == null) { Debug.LogError("[DepthTest] Shader 'Gossip/DepthEncode' not found"); return; }
                _depthMat = new Material(sh);
            }
            int w = resolution;
            int h = Mathf.RoundToInt(resolution / cam.aspect);
            if (h < 1) h = resolution;

            var depthRT = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var cmd = new CommandBuffer { name = "GossipDepthCapture" };
            cmd.SetRenderTarget(depthRT);
            cmd.ClearRenderTarget(true, true, Color.white);
            var proj = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true);
            cmd.SetViewProjectionMatrices(cam.worldToCameraMatrix, proj);
            var renderers = Object.FindObjectsOfType<Renderer>();
            foreach (var r in renderers)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                int subMeshes = (r.sharedMaterials != null && r.sharedMaterials.Length > 0) ? r.sharedMaterials.Length : 1;
                for (int sm = 0; sm < subMeshes; sm++)
                    cmd.DrawRenderer(r, _depthMat, sm, 0);
            }
            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Release();
            byte[] depthPng = ReadRTToPng(depthRT, w, h);
            RenderTexture.ReleaseTemporary(depthRT);

            var colorRT = RenderTexture.GetTemporary(w, h, 24);
            var prevTarget = cam.targetTexture;
            cam.targetTexture = colorRT;
            cam.Render();
            cam.targetTexture = prevTarget;
            byte[] colorPng = ReadRTToPng(colorRT, w, h);
            RenderTexture.ReleaseTemporary(colorRT);

            string dir = Application.persistentDataPath;
            File.WriteAllBytes(Path.Combine(dir, "gossip_depth.png"), depthPng);
            File.WriteAllBytes(Path.Combine(dir, "gossip_color.png"), colorPng);
            Debug.Log("[DepthTest] Saved gossip_depth.png + gossip_color.png to: " + dir);
        }

        private static byte[] ReadRTToPng(RenderTexture rt, int w, int h)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            byte[] png = tex.EncodeToPNG();
            Object.Destroy(tex);
            return png;
        }
    }
}
