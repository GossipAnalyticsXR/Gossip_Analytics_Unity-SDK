using UnityEngine;
using UnityEngine.Rendering;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using UnityEngine.Experimental.Rendering;

public static class CaptureUtils
{
    public static async UniTask<byte[]> CapturePngAsync()
    {
        var cam = Camera.main;
        if (cam == null) return null;

        int targetHeight = 540;
        float aspectRatio = (float)Screen.width / Screen.height;
        int targetWidth = Mathf.RoundToInt(targetHeight * aspectRatio);

        var rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 24, GraphicsFormat.R8G8B8A8_SRGB);
        RenderTexture previousRT = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = previousRT;

        try
        {
            var request = await AsyncGPUReadback.Request(rt, 0, GraphicsFormat.R8G8B8A8_SRGB);

            if (request.hasError)
            {
                return null;
            }

            var rawData = request.GetData<byte>();
            byte[] cpuBuffer = rawData.ToArray();

            return ImageConversion.EncodeArrayToPNG(
                cpuBuffer,
                GraphicsFormat.R8G8B8A8_SRGB,
                (uint)targetWidth,
                (uint)targetHeight
            );
        }
        finally
        {
            RenderTexture.ReleaseTemporary(rt);
        }
    }
}