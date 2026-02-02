using System;
using UnityEngine;
using UnityEngine.XR;
using GossipSDK.Core;
using GossipSDK.Tracking.PlatformSpecification;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class RealityModeMonitor : MonoBehaviour
    {
        private string currentMode = "Unknown";
        private double modeStartTime;

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

        private string DetectCurrentMode()
        {
            bool xrActive = XRSettings.isDeviceActive;

            if (xrActive)
            {
                string deviceName = XRSettings.loadedDeviceName;
                if (!string.IsNullOrEmpty(deviceName))
                {
                    if (deviceName.ToLower().Contains("oculus")
                        || deviceName.ToLower().Contains("meta")
                        || deviceName.ToLower().Contains("openxr"))
                    {
                        return "VR";
                    }

                    if (deviceName.ToLower().Contains("ar")
                        || deviceName.ToLower().Contains("mixed"))
                    {
                        return "MR";
                    }

                    return "XRUnknown";
                }

                return "XR";
            }

            return "2D";
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
