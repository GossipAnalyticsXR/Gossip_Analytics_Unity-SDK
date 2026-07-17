using UnityEngine;
using UnityEngine.Rendering;
using Cysharp.Threading.Tasks;

public struct CapturedFrame
{
    public byte[] Png;
    public Vector3 Position;
    public Vector3 EulerAngles;
    public float Fov;
    public float Aspect;
    public byte[] DepthPng;
    public int DepthWidth;
    public int DepthHeight;
    public float DepthMaxMeters;
}

public static class CaptureUtils
{
    public static UniTask<byte[]> CapturePngAsync()
    {
        var cam = Camera.main;
        if (cam == null) return UniTask.FromResult<byte[]>(null);

        int targetHeight = 540;
        float aspectRatio = (float)Screen.width / Screen.height;
        int targetWidth = Mathf.RoundToInt(targetHeight * aspectRatio);

        var rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 24);

        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousRT = cam.targetTexture;
        Texture2D tex = null;

        try
        {
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = previousRT;

            RenderTexture.active = rt;

            tex = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            tex.Apply();

            return UniTask.FromResult(tex.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(rt);

            if (tex != null) UnityEngine.Object.Destroy(tex);
        }
    }

        public static UniTask<CapturedFrame> CaptureFrameAsync()
    {
        var cam = Camera.main;
        if (cam == null) return UniTask.FromResult(default(CapturedFrame));

        int targetHeight = 540;
        float aspectRatio = (float)Screen.width / Screen.height;
        int targetWidth = Mathf.RoundToInt(targetHeight * aspectRatio);

        var rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 24);

        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousRT = cam.targetTexture;
        Texture2D tex = null;

        byte[] colorPng;
        Vector3 capturedPosition;
        Vector3 capturedEulerAngles;
        float capturedFov;
        float capturedAspect;

        try
        {
            cam.targetTexture = rt;
            cam.Render();

            capturedPosition = cam.transform.position;
            capturedEulerAngles = cam.transform.rotation.eulerAngles;
            capturedFov = cam.fieldOfView;
            capturedAspect = cam.aspect;

            cam.targetTexture = previousRT;

            RenderTexture.active = rt;

            tex = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            tex.Apply();

            colorPng = tex.EncodeToPNG();
        }
        finally
        {
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(rt);

            if (tex != null) UnityEngine.Object.Destroy(tex);
        }

        byte[] depthPng = null;
        int depthWidth = 0;
        int depthHeight = 0;
        float depthMaxMeters = 50f;

        var depthShader = Resources.Load<Shader>("GossipDepthEncode");
        if (depthShader != null)
        {
            var depthMat = new Material(depthShader);

            int depthTargetHeight = 256;
            int depthTargetWidth = Mathf.RoundToInt(depthTargetHeight * aspectRatio);

            var depthRt = RenderTexture.GetTemporary(depthTargetWidth, depthTargetHeight, 24);
            Texture2D depthTex = null;

            try
            {
                var cmd = new CommandBuffer { name = "GossipDepthCapture" };
                cmd.SetRenderTarget(depthRt);
                cmd.ClearRenderTarget(true, true, Color.white);
                cmd.SetViewProjectionMatrices(cam.worldToCameraMatrix, GL.GetGPUProjectionMatrix(cam.projectionMatrix, true));

                var renderers = Object.FindObjectsOfType<Renderer>();
                foreach (var r in renderers)
                {
                    if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                    if (((1 << r.gameObject.layer) & cam.cullingMask) == 0) continue;

                    int subMeshes = (r.sharedMaterials != null && r.sharedMaterials.Length > 0) ? r.sharedMaterials.Length : 1;
                    for (int sub = 0; sub < subMeshes; sub++)
                        cmd.DrawRenderer(r, depthMat, sub, 0);
                }

                Graphics.ExecuteCommandBuffer(cmd);
                cmd.Release();

                RenderTexture.active = depthRt;
                depthTex = new Texture2D(depthTargetWidth, depthTargetHeight, TextureFormat.RGB24, false);
                depthTex.ReadPixels(new Rect(0, 0, depthTargetWidth, depthTargetHeight), 0, 0);
                depthTex.Apply();

                depthPng = depthTex.EncodeToPNG();
                depthWidth = depthTargetWidth;
                depthHeight = depthTargetHeight;
            }
            finally
            {
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(depthRt);

                if (depthTex != null) UnityEngine.Object.Destroy(depthTex);
                UnityEngine.Object.Destroy(depthMat);
            }
        }

        return UniTask.FromResult(new CapturedFrame {
            Png = colorPng,
            Position = capturedPosition,
            EulerAngles = capturedEulerAngles,
            Fov = capturedFov,
            Aspect = capturedAspect,
            DepthPng = depthPng,
            DepthWidth = depthWidth,
            DepthHeight = depthHeight,
            DepthMaxMeters = depthMaxMeters
            });
    }
}
