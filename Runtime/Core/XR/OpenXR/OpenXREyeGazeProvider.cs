using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using GossipSDK.Core.XR;

namespace GossipSDK.XR
{
    public sealed class OpenXREyeGazeProvider : IEyeGazeProvider
    {
        private readonly Transform fallbackCamera;
        private readonly List<InputDevice> _deviceBuffer = new List<InputDevice>();
        private InputDevice eyeDevice;

        // Resolve on demand: on Quest Pro the eye-tracking device appears a moment after
        // startup and after the permission prompt, so keep retrying until it is valid.
        public bool IsAvailable
        {
            get
            {
                if (!eyeDevice.isValid)
                    TryResolveEyeDevice();
                return eyeDevice.isValid;
            }
        }

        private string _lastSource = "head";
        public string TrackingSource => _lastSource;

        public OpenXREyeGazeProvider(Transform cameraTransform)
        {
            fallbackCamera = cameraTransform;
            TryResolveEyeDevice();
        }

        public bool TryGetEyeGaze(out Ray gaze)
        {
            if (eyeDevice.isValid && TryReadEyeRay(out gaze))
            {
                _lastSource = "eye";
                return true;
            }

            if (fallbackCamera != null)
            {
                gaze = new Ray(fallbackCamera.position, fallbackCamera.forward);
                _lastSource = "head";
                return true;
            }

            gaze = default;
            return false;
        }

        private bool TryReadEyeRay(out Ray gaze)
        {
            // Primary: eye-gaze POSE (OpenXR Eye Gaze Interaction — what Quest Pro exposes).
            if (eyeDevice.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos) &&
                eyeDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot))
            {
                // Pose is in tracking space; convert to world via the camera rig.
                Transform rig = fallbackCamera != null ? fallbackCamera.parent : null;
                Vector3 worldPos = rig != null ? rig.TransformPoint(pos) : pos;
                Quaternion worldRot = rig != null ? rig.rotation * rot : rot;
                gaze = new Ray(worldPos, worldRot * Vector3.forward);
                return true;
            }

            // Secondary: older eyesData/fixation-point API (some runtimes only expose this).
            if (eyeDevice.TryGetFeatureValue(CommonUsages.eyesData, out Eyes eyes) &&
                eyes.TryGetFixationPoint(out Vector3 fixationPoint) &&
                fixationPoint != Vector3.zero &&
                fallbackCamera != null)
            {
                Vector3 origin = fallbackCamera.position;
                gaze = new Ray(origin, (fixationPoint - origin).normalized);
                return true;
            }

            gaze = default;
            return false;
        }

        private void TryResolveEyeDevice()
        {
            _deviceBuffer.Clear();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.EyeTracking, _deviceBuffer);
            eyeDevice = _deviceBuffer.Count > 0 ? _deviceBuffer[0] : default;
        }
    }
}
