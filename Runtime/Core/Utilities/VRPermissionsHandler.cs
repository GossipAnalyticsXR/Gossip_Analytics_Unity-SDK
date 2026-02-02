using UnityEngine;
using UnityEngine.Android;
using System.Collections;
using System.Collections.Generic;

public class VRPermissionsHandler : MonoBehaviour
{
    private readonly string[] permissions = new string[]
    {
        "com.oculus.permission.EYE_TRACKING",
        "com.oculus.permission.USE_SCENE",
        "horizonos.permission.HEADSET_CAMERA",
        Permission.Microphone
    };

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
        yield return new WaitForSecondsRealtime(0.5f);

        foreach (var permission in permissions)
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

        Debug.Log("[VRPermissionsHandler] Secuencia terminada. Sistema listo.");
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
