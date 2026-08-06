#if META_CORE
using UnityEngine;
using GossipSDK.Core.XR;

namespace GossipSDK.XR
{
    // Meta Quest eye tracking via the Meta XR SDK (OVREyeGaze). Quest Pro does not expose
    // eye gaze through the generic OpenXR InputDevice API, so this provider is tried first
    // on Meta devices; other runtimes fall back to OpenXREyeGazeProvider.
    public sealed class MetaEyeGazeProvider : IEyeGazeProvider
    {
        private OVREyeGaze eyeGaze;

        public bool IsAvailable => eyeGaze != null && eyeGaze.EyeTrackingEnabled;
        public string TrackingSource => "eye";

        public MetaEyeGazeProvider(Transform cameraTransform)
        {
            try
            {
                var go = new GameObject("GossipMetaEyeGaze");
                if (cameraTransform != null)
                    go.transform.SetParent(cameraTransform.parent, false);
                eyeGaze = go.AddComponent<OVREyeGaze>();
                eyeGaze.Eye = OVREyeGaze.EyeId.Left; // tune on device (or combine L/R)
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GossipMetaEyeGaze] init failed, falling back: {e.Message}");
                eyeGaze = null;
            }
        }

        public bool TryGetEyeGaze(out Ray gaze)
        {
            if (eyeGaze != null && eyeGaze.EyeTrackingEnabled && eyeGaze.Confidence >= 0.5f)
            {
                gaze = new Ray(eyeGaze.transform.position, eyeGaze.transform.forward);
                return true;
            }

            gaze = default;
            return false;
        }
    }
}
#endif
