using UnityEngine;
using GossipSDK.Core;
using GossipSDK.Tracking.GameplayMetrics;
using System;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class BatteryMonitorComponent : MonoBehaviour
    {
        [SerializeField] private float sampleInterval = 30f;

        private float timer;

        private void Update()
        {
            timer += Time.unscaledDeltaTime;
            if (timer >= sampleInterval)
            {
                timer = 0f;
                SendBatterySample();
            }
        }

        private void SendBatterySample()
        {
            var tracker = Gossip.Instance?.BatteryTracker;
            if (tracker == null)
            {
                if (Gossip.Instance?.Settings?.EnableDebug == true)
                    Debug.Log("[BatteryMonitor] BatteryTracker not available.");
                return;
            }

            var data = new BatteryTracker.EntityData
            {
                BatteryLevel = SystemInfo.batteryLevel,
                BatteryStatus = SystemInfo.batteryStatus.ToString(),
                TimestampUtc = DateTime.UtcNow.ToString("o")
            };

            tracker.CapSession(data);

            if (Gossip.Instance?.Settings?.EnableDebug == true)
                Debug.Log($"[BatteryMonitor] CapSession battery: level={data.BatteryLevel}, status={data.BatteryStatus}");
        }
    }
}
