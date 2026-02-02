using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using GossipSDK.Core;
using GossipSDK.Tracking.PlatformSpecification;
using GossipSDK.Utilities;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class PeripheralAutoTrackerComponent : MonoBehaviour
    {
        private double startTime;
        public bool sendImmediately = true;


        private void Start()
        {
            SendAllPeripherals(0);
            startTime = Time.realtimeSinceStartupAsDouble;
        }

        private void OnDestroy()
        {
            double duration = Time.realtimeSinceStartupAsDouble - startTime;
            SendAllPeripherals(duration);
        }

        private void OnApplicationQuit()
        {
            double duration = Time.realtimeSinceStartupAsDouble - startTime;
            SendAllPeripherals(duration);
        }

        private void OnDisable()
        {
            double duration = Time.realtimeSinceStartupAsDouble - startTime;
            SendAllPeripherals(duration);
        }

        private void SendAllPeripherals(double duration)
        {
            var tracker = Gossip.Instance?.PeripheralTracker;
            if (tracker == null) return;

            var peripherals = PeripheralAutoDetector.Detect();

            foreach (var p in peripherals)
            {
                var data = new PeripheralTracker.EntityData
                {
                    PeripheralName = p.Name,
                    Brand = p.Brand,
                    PeripheralType = p.Type,
                    IsHaptic = p.IsHaptic,
                    UsageDurationSeconds = duration,
                    SceneName = SceneManager.GetActiveScene().name,
                    TimestampUtc = DateTime.UtcNow.ToString("o")
                };

                tracker.CapSession(data);

                if (sendImmediately)
                    tracker.SendDataToSocket();

                if (Gossip.Instance?.Settings?.EnableDebug == true)
                {
                    Debug.Log($"[PeripheralAuto] {p.Type} {p.Name} ({p.Brand}) duration={duration:F1}s");
                }
            }
        }
    }
}
