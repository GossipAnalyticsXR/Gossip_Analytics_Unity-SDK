using UnityEngine;
using Cysharp.Threading.Tasks;

public struct CapturedFrame
{
    public byte[] Png;
    public Vector3 Position;
    public Vector3 EulerAngles;
    public float Fov;
    public float Aspect;
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

        try
        {
            cam.targetTexture = rt;
            cam.Render();

            Vector3 capturedPosition = cam.transform.position;
            Vector3 capturedEulerAngles = cam.transform.rotation.eulerAngles;
            float capturedFov = cam.fieldOfView;
            float capturedAspect = cam.aspect;

            cam.targetTexture = previousRT;

            RenderTexture.active = rt;

            tex = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            tex.Apply();

            return UniTask.FromResult(new CapturedFrame
            {
                Png = tex.EncodeToPNG(),
                Position = capturedPosition,
                EulerAngles = capturedEulerAngles,
                Fov = capturedFov,
                Aspect = capturedAspect
            });
        }
        finally
        {
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(rt);

            if (tex != null) UnityEngine.Object.Destroy(tex);
        }
    }
}
