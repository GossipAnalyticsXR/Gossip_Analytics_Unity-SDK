using System;
using UnityEngine;
using UnityEngine.XR;
using GossipSDK.Core;
using GossipSDK.Tracking.PlatformSpecification;
using System.Reflection;
using GossipSDK.Core.Utilities;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class RealityModeMonitor : MonoBehaviour
    {
        private string currentMode = "Unknown";
        private double modeStartTime;

        // Cada cuanto se vuelve a buscar en escena una fuente que todavia no aparecio.
        private const float SourceRescanSeconds = 1f;

        private Component passthroughLayer;
        private Component arCameraManager;
        private float lastSourceScan = -999f;

        private RealityModeTracker tracker => Gossip.Instance?.RealityModeTracker;

        private void Start()
        {
            currentMode = DetectCurrentMode();
            modeStartTime = Time.realtimeSinceStartupAsDouble;
        }

        private void Update()
        {
            string newMode = DetectCurrentMode();
            if (newMode != currentMode)
            {
                double now = Time.realtimeSinceStartupAsDouble;
                double duration = now - modeStartTime;

                SendTransition(currentMode, newMode, duration);

                currentMode = newMode;
                modeStartTime = now;
            }
        }

        // Lo que enciende el passthrough depende del rig, y hasta la 2.0.24 aqui solo se
        // miraba OVRManager. En un proyecto OpenXR ese tipo no esta en escena, asi que la
        // lectura era null y el modo salia VR siempre: medido el 14-sep-2026 en Quest 3,
        // 5863 de 5863 lecturas con ptRaw vacio, 93 de ellas mientras habia passthrough.
        // Ahora se prueban tres fuentes y source= dice CUAL contesto, para que el log sea
        // una medicion y no un hueco.
        //
        // OJO: el passthrough que enciende el SISTEMA (doble toque, guardian, menu) no es
        // ninguna de las tres. Ahi el shell sustituye a la app, la app deja de pintar y no
        // queda bandera dentro que lo reporte; desde dentro es indistinguible de abrir el
        // menu. Eso no se detecta aqui y no se debe inventar.
        private string DetectCurrentMode()
        {
            if (!XRSettings.isDeviceActive)
                return "2D";

            bool passthroughActive = false;
            string source = "none";
            object raw = null;

            try
            {
                RefreshSources();

                // 1. Rig clasico de Meta. La bandera de OVRManager es NECESARIA: quien
                //    compone el passthrough es el OVRManager, no la capa, y sin el la capa
                //    no pinta nada por muy encendida que este.
                //
                //    Esto no es teoria. El 14-sep-2026, en Quest 3 y con un rig OpenXR sin
                //    OVRManager, se creo una OVRPassthroughLayer con enabled=True y
                //    hidden=False: 982 de 982 lecturas dijeron MR, se escribio una fila
                //    "MR > VR duration=15,02s", y el usuario no vio su habitacion en ningun
                //    momento. Quince segundos de mixed reality inventados. Por eso la capa
                //    refina la bandera en vez de sustituirla.
                object ovr = OVRManagerInstance();
                if (ovr != null)
                {
                    source = "ovrmanager";
                    raw = ReadMember(ovr, "isInsightPassthroughEnabled");
                    passthroughActive = raw is bool ovrFlag && ovrFlag;

                    // 2. La capa refina: con la bandera encendida, una capa deshabilitada u
                    //    oculta significa que el usuario NO esta viendo su habitacion.
                    if (passthroughActive && (UnityEngine.Object)passthroughLayer != null)
                    {
                        Behaviour layer = passthroughLayer as Behaviour;
                        object hiddenRaw = ReadMember(passthroughLayer, "hidden");
                        bool visible = layer != null
                                       && layer.enabled
                                       && !(hiddenRaw is bool hidden && hidden);

                        source = "ovrmanager+layer";
                        raw = visible;
                        passthroughActive = visible;
                    }
                }
                else if ((UnityEngine.Object)passthroughLayer != null)
                {
                    // Hay capa y no hay OVRManager: medido, esto no pinta passthrough. Se
                    // dice en el log y NO se cuenta como MR.
                    source = "layer-sin-ovrmanager";
                    raw = false;
                }

                // 3. AR Foundation sobre OpenXR. La documentacion de com.unity.xr.meta-openxr
                //    dice que el passthrough se enciende y se apaga habilitando el
                //    ARCameraManager, o sea que su enabled ES el estado.
                if (!passthroughActive && (UnityEngine.Object)arCameraManager != null)
                {
                    Behaviour arCamera = arCameraManager as Behaviour;
                    bool active = arCamera != null && arCamera.enabled;

                    source = "arcameramanager";
                    raw = active;
                    passthroughActive = active;
                }
            }
            catch { }

            string mode = passthroughActive ? "MR" : "VR";

            if (Gossip.Instance?.Settings?.EnableDebug == true)
                Debug.Log($"[RealityModeMonitor] mode={mode} source={source} ptRaw={raw} device={UnityEngine.XR.XRSettings.loadedDeviceName}");

            return mode;
        }

        // Buscar en escena es caro y esto corre en Update, asi que la referencia se guarda
        // y solo se reintenta la que falta, y como mucho una vez por segundo.
        private void RefreshSources()
        {
            bool missingAny = (UnityEngine.Object)passthroughLayer == null
                              || (UnityEngine.Object)arCameraManager == null;
            if (!missingAny)
                return;

            if (Time.realtimeSinceStartup - lastSourceScan < SourceRescanSeconds)
                return;

            lastSourceScan = Time.realtimeSinceStartup;

            if ((UnityEngine.Object)passthroughLayer == null)
                passthroughLayer = FindComponent(ReflectionUtil.FindType("OVRPassthroughLayer"));

            if ((UnityEngine.Object)arCameraManager == null)
                arCameraManager = FindComponent(
                    ReflectionUtil.FindTypeByFullName("UnityEngine.XR.ARFoundation.ARCameraManager"));
        }

        // FindObjectOfType(Type) esta obsoleto desde Unity 2023 y FindAnyObjectByType(Type) no
        // existe antes, asi que se resuelve por reflexion: el SDK compila en las dos.
        private static Component FindComponent(Type type)
        {
            if (type == null)
                return null;

            try
            {
                MethodInfo finder = typeof(UnityEngine.Object).GetMethod(
                        "FindAnyObjectByType",
                        BindingFlags.Public | BindingFlags.Static,
                        null, new[] { typeof(Type) }, null)
                    ?? typeof(UnityEngine.Object).GetMethod(
                        "FindObjectOfType",
                        BindingFlags.Public | BindingFlags.Static,
                        null, new[] { typeof(Type) }, null);

                return finder != null ? finder.Invoke(null, new object[] { type }) as Component : null;
            }
            catch
            {
                return null;
            }
        }

        private static object OVRManagerInstance()
        {
            Type ovrType = ReflectionUtil.FindType("OVRManager");
            if (ovrType == null)
                return null;

            PropertyInfo instProp = ovrType.GetProperty("instance",
                BindingFlags.Public | BindingFlags.Static);

            return instProp != null ? instProp.GetValue(null) : null;
        }

        // hidden es un campo publico, no una propiedad, asi que hay que mirar los dos.
        private static object ReadMember(object target, string memberName)
        {
            if (target == null)
                return null;

            Type type = target.GetType();

            PropertyInfo prop = type.GetProperty(memberName,
                BindingFlags.Public | BindingFlags.Instance);
            if (prop != null)
                return prop.GetValue(target);

            FieldInfo field = type.GetField(memberName,
                BindingFlags.Public | BindingFlags.Instance);
            return field != null ? field.GetValue(target) : null;
        }

        private void SendTransition(string from, string to, double duration)
        {
            try
            {
                if (tracker == null) return;

                var data = new RealityModeTracker.EntityData
                {
                    FromMode = from,
                    ToMode = to,
                    DurationInPreviousMode = duration,
                    SceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    TimestampUtc = DateTime.UtcNow.ToString("o")
                };

                tracker.CapSession(data);

                if (Gossip.Instance?.Settings?.EnableDebug == true)
                {
                    Debug.Log($"[RealityModeMonitor] {from} > {to} | duration={duration:F2}s");
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }
}
