using UnityEngine;
using UnityEngine.XR;
using GossipSDK.Core.XR;

namespace GossipSDK.XR
{
    public sealed class OpenXRHandControllerPoseProvider : IHandControllerPoseProvider
    {
        private InputDevice left;
        private InputDevice right;

        public bool IsSupported => left.isValid || right.isValid;

        public OpenXRHandControllerPoseProvider()
        {
            left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        }

        public bool TryGetLeftPose(out Vector3 pos, out Quaternion rot)
        {
            return TryGetPose(left, out pos, out rot);
        }

        public bool TryGetRightPose(out Vector3 pos, out Quaternion rot)
        {
            return TryGetPose(right, out pos, out rot);
        }

        private bool TryGetPose(InputDevice device, out Vector3 pos, out Quaternion rot)
        {
            if (device.isValid &&
                device.TryGetFeatureValue(CommonUsages.devicePosition, out pos) &&
                device.TryGetFeatureValue(CommonUsages.deviceRotation, out rot))
            {
                return true;
            }

            pos = default;
            rot = default;
            return false;
        }
    }
}
