using GossipSDK.Core.XR;
using UnityEngine;

namespace GossipSDK.XR
{
    public class XRBootstrap : MonoBehaviour
    {
        public static IEyeGazeProvider EyeGaze
        {
            get; private set;
        }
    
        public static IHeadPoseProvider HeadPose
        {
            get; private set;
        }

        public static IHandControllerPoseProvider HandControllers
        {
            get; private set;
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[XRBootstrap] No Main Camera found.");
                return;
            }

            // OpenXR-first
            EyeGaze = new OpenXREyeGazeProvider(cam.transform);
            HeadPose = new OpenXRHeadPoseProvider(cam.transform);
            HandControllers = new OpenXRHandControllerPoseProvider();
            XRProviders.Session = new OpenXRSessionProvider();

            Debug.Log($"[XRBootstrap] Providers initialized");
            Debug.Log($"[XRBootstrap] EyeGaze: {XRProviders.EyeGaze?.GetType().Name}");
            Debug.Log($"[XRBootstrap] HeadPose: {XRProviders.HeadPose?.GetType().Name}");
            Debug.Log("[XRBootstrap] HandController provider ready");
            Debug.Log($"[XRBootstrap] Session: {XRProviders.Session?.State}");
        }
    }
}
