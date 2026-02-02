using UnityEngine;
using UnityEngine.XR;
using GossipSDK.Core.XR;

namespace GossipSDK.XR
{
    public sealed class OpenXREyeGazeProvider : IEyeGazeProvider
    {
        private readonly Transform fallbackCamera;
        private InputDevice eyeDevice;
        private bool eyeAvailableChecked;

        public bool IsAvailable => eyeDevice.isValid;

        public string TrackingSource =>
            eyeDevice.isValid ? "eye" : "head";

        public OpenXREyeGazeProvider(Transform cameraTransform)
        {
            fallbackCamera = cameraTransform;
            TryResolveEyeDevice();
        }

        public bool TryGetEyeGaze(out Ray gaze)
        {
            if (!eyeAvailableChecked)
                TryResolveEyeDevice();

            if (eyeDevice.isValid &&
                eyeDevice.TryGetFeatureValue(CommonUsages.eyesData, out Eyes eyes) &&
                eyes.TryGetFixationPoint(out Vector3 fixationPoint) &&
                fixationPoint != Vector3.zero)
            {
                Vector3 origin = fallbackCamera.position;
                gaze = new Ray(origin, (fixationPoint - origin).normalized);
                return true;
            }

            if (fallbackCamera != null)
            {
                gaze = new Ray(
                    fallbackCamera.position,
                    fallbackCamera.forward
                );
                return true;
            }

            gaze = default;
            return false;
        }

        private void TryResolveEyeDevice()
        {
            eyeAvailableChecked = true;
            eyeDevice = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
        }
    }
}
