using UnityEngine;
using UnityEngine.XR;

namespace GossipSDK.XR
{
    public sealed class OpenXRHeadPoseProvider : GossipSDK.Core.XR.IHeadPoseProvider
    {
        private readonly Transform fallbackTransform;
        private readonly InputDevice hmd;

        public bool IsAvailable => hmd.isValid || fallbackTransform != null;

        public OpenXRHeadPoseProvider(Transform fallbackCamera)
        {
            fallbackTransform = fallbackCamera;
            hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        }

        public bool TryGetPose(out Vector3 position, out Quaternion rotation)
        {
            if (hmd.isValid &&
                hmd.TryGetFeatureValue(CommonUsages.devicePosition, out position) &&
                hmd.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation))
            {
                return true;
            }

            if (fallbackTransform != null)
            {
                position = fallbackTransform.position;
                rotation = fallbackTransform.rotation;
                return true;
            }

            position = default;
            rotation = default;
            return false;
        }
    }
}
