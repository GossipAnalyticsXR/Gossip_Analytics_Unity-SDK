using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Provider;
using GossipSDK.Tracking.PlatformSpecification;
using GossipSDK.Core;

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
                SendPlatformInfo();
            }
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
            var hmdDevices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
            UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
                UnityEngine.XR.InputDeviceCharacteristics.HeadMounted, hmdDevices);
            bool hasLatencyData = hmdDevices.Count > 0 &&
                UnityEngine.XR.Provider.XRStats.TryGetStat(hmdDevices[0], "MotionToPhoton", out latencyMs);

            var data = new PlatformTracker.EntityData
            {
                Version = Application.version,
                PlatformName = Application.platform.ToString(),
                Model = SystemInfo.deviceModel,
                Device = SystemInfo.deviceName,
                Resolution = $"{Screen.width}x{Screen.height}",
                GeneralSound = AudioListener.volume > 0f,
                ControllersLatency = hasLatencyData,
                AmountDevicesInGame = hasDevicesInGame,
            };

            tracker.CapSession(data);

            if (Gossip.Instance?.Settings?.EnableDebug == true)
                Debug.Log($"[PlatformMonitor] CapSession platform info: {data.PlatformName} {data.Model}");
        }
    }
}
