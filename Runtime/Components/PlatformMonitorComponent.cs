using System.Collections;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Provider;
using GossipSDK.Tracking.PlatformSpecification;
using GossipSDK.Core;
using GossipSDK.Utilities;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class PlatformMonitorComponent : MonoBehaviour
    {
        [SerializeField] private bool autoReportOnStart = true;

        private void Start()
        {
            if (autoReportOnStart)
            {
                if (Gossip.Instance == null)
                {
                    StartCoroutine(WaitAndSend());
                    return;
                }
                SendPlatformInfo();
            }
        }

        private IEnumerator WaitAndSend()
        {
            yield return new WaitUntil(() => Gossip.Instance != null);
            SendPlatformInfo();
        }

        public void SendPlatformInfo()
        {
            var tracker = Gossip.Instance?.PlatformTracker;
            if (tracker == null)
            {
                if (Gossip.Instance?.Settings?.EnableDebug == true)
                    Debug.Log("[PlatformMonitor] PlatformTracker not available.");
                return;
            }

            var xrDevices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
            UnityEngine.XR.InputDevices.GetDevices(xrDevices);
            bool hasDevicesInGame = xrDevices.Count > 0;

            float latencyMs = 0f;
            var displaySubsystems = new System.Collections.Generic.List<UnityEngine.XR.XRDisplaySubsystem>();
            SubsystemManager.GetInstances(displaySubsystems);
            bool hasLatencyData = displaySubsystems.Count > 0 &&
                UnityEngine.XR.Provider.XRStats.TryGetStat(displaySubsystems[0], "MotionToPhoton", out latencyMs);

            string rawModel = SystemInfo.deviceModel;
            string brand;
            if (rawModel.StartsWith("Oculus") || rawModel.StartsWith("Meta"))
                brand = "Meta";
            else if (rawModel.StartsWith("Pico"))
                brand = "PICO";
            else if (rawModel.StartsWith("HTC") || rawModel.StartsWith("Vive"))
                brand = "HTC";
            else if (rawModel.StartsWith("Samsung"))
                brand = "Samsung";
            else if (rawModel.StartsWith("MacBook") || rawModel.StartsWith("iMac") ||
                     rawModel.StartsWith("Mac") || rawModel.StartsWith("iPhone") ||
                     rawModel.StartsWith("iPad"))
                brand = "Apple";
            else
                brand = rawModel.Contains(" ") ? rawModel.Split(' ')[0] : rawModel;
            int xrW = UnityEngine.XR.XRSettings.eyeTextureWidth;
            int xrH = UnityEngine.XR.XRSettings.eyeTextureHeight;
            int resW = xrW > 0 ? xrW : Screen.width;
            int resH = xrH > 0 ? xrH : Screen.height;

            var controllerChars = UnityEngine.XR.InputDeviceCharacteristics.Controller;
            var controllers = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
            UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(controllerChars, controllers);
            float trackingAccuracy = 0f;
            if (controllers.Count > 0)
            {
                int fullyTracked = 0;
                foreach (var ctrl in controllers)
                {
                    if (ctrl.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trackingState,
                        out UnityEngine.XR.InputTrackingState state))
                    {
                        bool hasPos = (state & UnityEngine.XR.InputTrackingState.Position) != 0;
                        bool hasRot = (state & UnityEngine.XR.InputTrackingState.Rotation) != 0;
                        if (hasPos && hasRot) fullyTracked++;
                    }
                }
                trackingAccuracy = (float)fullyTracked / controllers.Count;
            }

            // La escala de render, que es el denominador de la resolucion: resW/resH ya
            // son la textura de ojo de la app, no el panel. Se lee junto a ellas para que
            // las tres salgan del mismo instante. 0 si no hay XR.
            float renderScale = 0f;
            try { renderScale = UnityEngine.XR.XRSettings.eyeTextureResolutionScale; }
            catch { renderScale = 0f; }

            var data = new PlatformTracker.EntityData
            {
                // Aqui habia un sustituto: si Application.version venia vacia o "0.0.0"
                // este sitio mandaba "1.0.0" y los otros nueve la cruda, asi que el cruce
                // de AppVersion contra devices.version se rompia en silencio. Ahora los
                // diez pasan por el mismo sitio y no hay convenio que cumplir.
                Version = GossipVersion.App,
                PlatformName = Application.platform.ToString(),
                Model = SystemInfo.deviceModel,
                Device = SystemInfo.deviceModel,
                Brand = brand,
                Resolution = $"{resW}x{resH}",
                RenderScale = renderScale,
                GeneralSound = AudioListener.volume > 0f,
                ControllersLatency = hasLatencyData,
                MotionToPhotonMs = hasLatencyData ? latencyMs : 0f,
                AmountDevicesInGame = hasDevicesInGame,
                TrackingAccuracy = trackingAccuracy,
                OsVersion = SystemInfo.operatingSystem,
            };

            tracker.CapSession(data);

            if (Gossip.Instance?.Settings?.EnableDebug == true)
                Debug.Log($"[PlatformMonitor] CapSession platform info: {data.PlatformName} {data.Model}");
        }
    }
}
