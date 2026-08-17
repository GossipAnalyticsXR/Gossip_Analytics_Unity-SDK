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
        /// <summary>Seconds between usage heartbeats. Each send reports the elapsed time
        /// since the previous one, so the backend can sum them into total usage.</summary>
        [SerializeField] private float heartbeatSeconds = 60f;

        /// <summary>Shortest interval worth reporting, to avoid noise when several
        /// lifecycle callbacks fire back to back.</summary>
        private const double MinReportedSeconds = 0.5d;

        private double lastSendTime;
        private float timer;
        public bool sendImmediately = true;


        private void Start()
        {
            SendAllPeripherals(0);
            lastSendTime = Time.realtimeSinceStartupAsDouble;
        }

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer < heartbeatSeconds) return;
            timer = 0f;
            SendElapsed();
        }

        private void OnDestroy()
        {
            SendElapsed();
        }

        private void OnApplicationQuit()
        {
            SendElapsed();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) SendElapsed();
        }

        private void OnDisable()
        {
            SendElapsed();
        }

        /// <summary>
        /// Reports the time elapsed since the last send. Sending deltas (instead of the
        /// total at shutdown) means usage survives an abrupt quit, which on standalone
        /// headsets is the common case.
        /// </summary>
        private void SendElapsed()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            double delta = now - lastSendTime;
            if (delta < MinReportedSeconds) return;

            lastSendTime = now;
            SendAllPeripherals(delta);
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
