using UnityEngine;
using UnityEngine.XR;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using GossipSDK.Tracking.GameplayMetrics;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class InputUsageTrackerComponent : MonoBehaviour
    {
        private InputUsageTracker tracker;
        private float lastSampleTime;

        private void Start()
        {
            tracker = GossipSDK.Core.Gossip.Instance?.InputUsageTracker;
            lastSampleTime = Time.time;
        }

        private void Update()
        {
            if (tracker == null) return;

            float delta = Time.deltaTime;

            if (IsUsingController())
                tracker.RegisterControllerUsage(delta);
            else if (IsUsingHands())
                tracker.RegisterHandUsage(delta);
        }

        private bool IsUsingController()
        {
            foreach (var device in InputSystem.devices)
            {
                if (device is XRController)
                    return true;
            }
            return false;
        }

        private bool IsUsingHands()
        {
#if UNITY_XR_HANDS
            return UnityEngine.XR.Hands.XRHandSubsystemHelpers
                .GetSubsystem()?.running == true;
#else
            return false;
#endif
        }

        private void OnDisable()
        {
            tracker?.SendDataToSocket();
        }

        private void OnApplicationQuit()
        {
            tracker?.SendDataToSocket();
        }

        private void OnDestroy()
        {
            tracker?.SendDataToSocket();
        }
    }
}
