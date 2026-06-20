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
            if (!XRSettings.isDeviceActive)
                return "2D";

            bool passthroughActive = false;
            object raw = null;
            try
            {
                System.Type ovrType = ReflectionUtil.FindType("OVRManager");
                if (ovrType != null)
                {
                    PropertyInfo instProp = ovrType.GetProperty("instance",
                        BindingFlags.Public | BindingFlags.Static);
                    if (instProp != null)
                    {
                        object inst = instProp.GetValue(null);
                        if (inst != null)
                        {
                            PropertyInfo ptProp = ovrType.GetProperty(
                                "isInsightPassthroughEnabled",
                                BindingFlags.Public | BindingFlags.Instance);
                            if (ptProp != null)
                            {
                                raw = ptProp.GetValue(inst);
                                passthroughActive = raw is bool b && b;
                            }
                        }
                    }
                }
            }
            catch { }

            string mode = passthroughActive ? "MR" : "VR";

            if (Gossip.Instance?.Settings?.EnableDebug == true)
                Debug.Log($"[RealityModeMonitor] mode={mode} ptRaw={raw} device={UnityEngine.XR.XRSettings.loadedDeviceName}");

            return mode;
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
