using UnityEngine;
using UnityEngine.Android;
using System.Collections;
using System.Collections.Generic;

public class VRPermissionsHandler : MonoBehaviour
{
    // --- Per-permission toggles (editable in Inspector and Instrumentation Manager) ---
    [Tooltip("Request Eye Tracking permission on Meta Quest. Required for gaze analytics.")]
    public bool enableEyeTracking = true;

    [Tooltip("Request Scene/Spatial permission on Meta Quest. Required for environment heatmaps.")]
    public bool enableSpatialScene = true;

    [Tooltip("Request Headset Camera permission on Meta Quest. Required for passthrough and MR.")]
    public bool enableHeadsetCamera = true;

    [Tooltip("Request Microphone permission. Audio is processed on-device and immediately discarded. No recordings stored or transmitted.")]
    public bool enableMicrophone = true;

    public static bool IsReady = false;
    private bool _isAppFocused = true;

    void Awake()
    {
        IsReady = false;
        DontDestroyOnLoad(this.gameObject);

#if UNITY_ANDROID && !UNITY_EDITOR
        StartCoroutine(RequestPermissionsSequence());
#else
        IsReady = true;
#endif
    }

    private IEnumerator RequestPermissionsSequence()
    {
        yield return new WaitForSecondsRealtime(0.1f);

        // Build list of enabled permissions
        var permissionsToRequest = new List<string>();
        if (enableMicrophone)     permissionsToRequest.Add(Permission.Microphone);
        if (enableEyeTracking)    permissionsToRequest.Add("com.oculus.permission.EYE_TRACKING");
        if (enableSpatialScene)   permissionsToRequest.Add("com.oculus.permission.USE_SCENE");
        if (enableHeadsetCamera)  permissionsToRequest.Add("horizonos.permission.HEADSET_CAMERA");

        foreach (var permission in permissionsToRequest)
        {
            if (!Permission.HasUserAuthorizedPermission(permission))
            {
                _isAppFocused = false;
                Permission.RequestUserPermission(permission);
                float timeout = 0f;
                while (!Permission.HasUserAuthorizedPermission(permission) && !_isAppFocused && timeout < 10f)
                {
                    yield return new WaitForSecondsRealtime(0.2f);
                    timeout += 0.2f;
                }
                yield return new WaitForSecondsRealtime(0.2f);
            }
        }

        Debug.Log("[VRPermissionsHandler] Permission sequence complete. System ready.");
        IsReady = true;
    }

    public static IEnumerator RequestEyeTrackingPermission()
    {
        if (!OVRPlugin.eyeTrackingSupported)
            yield break;

        const string eyePermission = "com.oculus.permission.EYE_TRACKING";

        if (!Permission.HasUserAuthorizedPermission(eyePermission))
        {
            Permission.RequestUserPermission(eyePermission);

            float timeout = 0f;
            while (!Permission.HasUserAuthorizedPermission(eyePermission) && timeout < 10f)
            {
                yield return new WaitForSecondsRealtime(0.2f);
                timeout += 0.2f;
            }

            Debug.Log("[VRPermissionsHandler] Eye Tracking permission result: "
                + Permission.HasUserAuthorizedPermission(eyePermission));
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        _isAppFocused = hasFocus;
    }
}
