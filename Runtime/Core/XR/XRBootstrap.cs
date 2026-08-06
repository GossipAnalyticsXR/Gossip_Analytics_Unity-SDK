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

            HeadPose = new OpenXRHeadPoseProvider(cam.transform);
            HandControllers = new OpenXRHandControllerPoseProvider();
            XRProviders.Session = new OpenXRSessionProvider();

            // Eye gaze last, guarded so a provider init failure can never break head/hand tracking.
            try
            {
#if META_CORE
                EyeGaze = new CompositeEyeGazeProvider(
                    new MetaEyeGazeProvider(cam.transform),
                    new OpenXREyeGazeProvider(cam.transform));
#else
                EyeGaze = new OpenXREyeGazeProvider(cam.transform);
#endif
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[XRBootstrap] Eye provider init failed, using OpenXR only: {e.Message}");
                EyeGaze = new OpenXREyeGazeProvider(cam.transform);
            }

            Debug.Log($"[XRBootstrap] Providers initialized");
            Debug.Log($"[XRBootstrap] EyeGaze: {XRProviders.EyeGaze?.GetType().Name}");
            Debug.Log($"[XRBootstrap] HeadPose: {XRProviders.HeadPose?.GetType().Name}");
            Debug.Log("[XRBootstrap] HandController provider ready");
            Debug.Log($"[XRBootstrap] Session: {XRProviders.Session?.State}");
        }
    }
}
