using UnityEngine;
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

            var data = new PlatformTracker.EntityData
            {
                Version = Application.version,
                PlatformName = Application.platform.ToString(),
                Model = SystemInfo.deviceModel,
                Device = SystemInfo.deviceName,
                Resolution = $"{Screen.width}x{Screen.height}",
                GeneralSound = AudioListener.volume > 0f,
                ControllersLatency = false, // TODO: not yet measured
                AmountDevicesInGame = false, // TODO: not yet measured
            };

            tracker.CapSession(data);

            if (Gossip.Instance?.Settings?.EnableDebug == true)
                Debug.Log($"[PlatformMonitor] CapSession platform info: {data.PlatformName} {data.Model}");
        }
    }
}
