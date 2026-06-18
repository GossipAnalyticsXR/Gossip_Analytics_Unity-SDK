using UnityEngine;
using Cysharp.Threading.Tasks;

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
}
