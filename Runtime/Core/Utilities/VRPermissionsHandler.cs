using UnityEngine;
using UnityEngine.Android;
using System.Collections;
using System.Collections.Generic;

public class VRPermissionsHandler : MonoBehaviour
{
    // --- Per-permission toggles (editable in Inspector and Instrumentation Manager) ---
    [Tooltip("Request Eye Tracking permission on Meta Quest. Required for gaze analytics.")]
    public bool enableEyeTracking = true;

    [Tooltip("Request Scene/Spatial permission on Meta Quest. RESERVED: the environment heatmap that recreates the room will need it. Nothing in the SDK calls the Scene API today (measured 10-sep-2026).")]
    public bool enableSpatialScene = true;

    [Tooltip("Request Headset Camera permission on Meta Quest. RESERVED for future MR capture. Nothing reads the headset camera today, and passthrough detection does NOT go through it (measured 10-sep-2026).")]
    public bool enableHeadsetCamera = true;

    [Tooltip("Request Microphone permission. Audio is processed on-device and immediately discarded. No recordings stored or transmitted.")]
    public bool enableMicrophone = true;

    public static bool IsReady = false;
    private bool _isAppFocused = true;

    // Guardia de instancia unica. IsReady es static: sin esta guarda, un segundo
    // VRPermissionsHandler en escena volvia IsReady a false en su Awake y apagaba
    // el permiso YA concedido para todos los que lo consultan --
    // AudioReactionTrackerComponent, MicPermissionComponent y GossipManager.
    // Es la misma clase de fallo que se cerro en SessionManager.
    private static VRPermissionsHandler _instance;

    void Awake()
    {
        if ((UnityEngine.Object)_instance != null && _instance != this)
        {
            Debug.LogWarning(
                "[VRPermissionsHandler] Ya hay un gestor de permisos vivo. Este " +
                "duplicado no toca IsReady ni vuelve a pedir permisos.");
            enabled = false;
            return;
        }
        _instance = this;

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

        // Los permisos se piden en UNA sola llamada, no de uno en uno.
        //
        // Medido el 10-sep-2026 en gafas (sesion 1c6bf4ed, Hospital Zone): pedirlos
        // por separado costo TRES esperas de ~11 s = ~33 s de arranque con la
        // telemetria parada. Cada RequestUserPermission que no abre dialogo agota
        // el plazo entero, porque la unica salida temprana es que vuelva el FOCO y
        // aqui el foco no se pierde: lo que parpadea es el montaje del visor
        // (~100 ms), y este gestor no lo escucha. Con una sola llamada el plazo se
        // paga una vez, no una por permiso.
        var pendientes = new List<string>();
        foreach (var permission in permissionsToRequest)
        {
            if (!Permission.HasUserAuthorizedPermission(permission))
                pendientes.Add(permission);
        }

        if (pendientes.Count > 0)
        {
            _isAppFocused = false;
            Permission.RequestUserPermissions(pendientes.ToArray());

            float timeout = 0f;
            while (!TodosConcedidos(pendientes) && !_isAppFocused && timeout < 10f)
            {
                yield return new WaitForSecondsRealtime(0.2f);
                timeout += 0.2f;
            }

            yield return new WaitForSecondsRealtime(0.2f);
        }

        Debug.Log("[VRPermissionsHandler] Permission sequence complete. System ready.");
        IsReady = true;
    }

    // Cierta solo cuando NO queda ningun permiso pendiente. El bucle de espera
    // pregunta por el grupo entero, no por uno.
    private static bool TodosConcedidos(List<string> permisos)
    {
        foreach (var p in permisos)
        {
            if (!Permission.HasUserAuthorizedPermission(p)) return false;
        }

        return true;
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
