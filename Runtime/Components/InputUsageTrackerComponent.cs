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
        private float captureTimer = 0f;
        private const float captureInterval = 5f;

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

            captureTimer += delta;
            if (captureTimer >= captureInterval)
            {
                captureTimer = 0f;
                if ((UnityEngine.Object)tracker != null)
                    tracker.CaptureSnapshot();
            }
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
            if ((UnityEngine.Object)tracker != null)
                tracker.CaptureSnapshot();
            tracker?.SendDataToSocket();
        }

        private void OnApplicationQuit()
        {
            if ((UnityEngine.Object)tracker != null)
                tracker.CaptureSnapshot();
            tracker?.SendDataToSocket();
        }

        private void OnDestroy()
        {
            if ((UnityEngine.Object)tracker != null)
                tracker.CaptureSnapshot();
            tracker?.SendDataToSocket();
        }
    }
}
