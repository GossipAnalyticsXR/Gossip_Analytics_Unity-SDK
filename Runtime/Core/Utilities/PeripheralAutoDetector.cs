using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Haptics;

namespace GossipSDK.Utilities
{
    public static class PeripheralAutoDetector
    {
        public static List<DetectedPeripheral> Detect()
        {
            var peripherals = new List<DetectedPeripheral>();

            // XR Headset
            if (XRSettings.isDeviceActive)
            {
                peripherals.Add(new DetectedPeripheral
                {
                    Name = XRSettings.loadedDeviceName,
                    Type = "hmd",
                    Brand = InferBrand(),
                    IsHaptic = false
                });
            }

            // XR devices (controllers, hand tracking, eye tracking, body trackers, base stations).
            // VR controllers do NOT show up in Gamepad.all, so they are enumerated here.
            var xrDevices = new List<UnityEngine.XR.InputDevice>();
            InputDevices.GetDevices(xrDevices);

            foreach (var device in xrDevices)
            {
                if (!device.isValid)
                    continue;

                var type = ClassifyXrDevice(device.characteristics);
                if (type == null)
                    continue; // the headset is already reported above

                bool isHaptic = false;
                HapticCapabilities capabilities;
                if (device.TryGetHapticCapabilities(out capabilities))
                    isHaptic = capabilities.supportsImpulse || capabilities.supportsBuffer;

                peripherals.Add(new DetectedPeripheral
                {
                    Name = string.IsNullOrEmpty(device.name) ? type : device.name,
                    Type = type,
                    Brand = string.IsNullOrEmpty(device.manufacturer) ? InferBrand() : device.manufacturer,
                    IsHaptic = isHaptic
                });
            }
            // Gamepads
            foreach (var gamepad in Gamepad.all)
            {
                peripherals.Add(new DetectedPeripheral
                {
                    Name = gamepad.displayName,
                    Type = "controller",
                    Brand = InferBrand(),
                    IsHaptic = gamepad is IHaptics
                });
            }

            // Mobile touch device
            if (Input.touchSupported && !XRSettings.isDeviceActive)
            {
                peripherals.Add(new DetectedPeripheral
                {
                    Name = "Touchscreen",
                    Type = "mobile",
                    Brand = InferBrand(),
                    IsHaptic = false
                });
            }

            // Desktop input
            if (Keyboard.current != null || Mouse.current != null)
            {
                peripherals.Add(new DetectedPeripheral
                {
                    Name = "Keyboard/Mouse",
                    Type = "desktop",
                    Brand = InferBrand(),
                    IsHaptic = false
                });
            }

            return peripherals;
        }

        /// <summary>
        /// Maps XR device characteristics to the peripheral type reported to analytics.
        /// Returns null for the headset, which is already reported separately.
        /// </summary>
        private static string ClassifyXrDevice(InputDeviceCharacteristics characteristics)
        {
            if ((characteristics & InputDeviceCharacteristics.HeadMounted) != 0)
                return null;

            if ((characteristics & InputDeviceCharacteristics.Controller) != 0)
                return "xr-controller";

            if ((characteristics & InputDeviceCharacteristics.HandTracking) != 0)
                return "hand-tracking";

            if ((characteristics & InputDeviceCharacteristics.EyeTracking) != 0)
                return "eye-tracking";

            if ((characteristics & InputDeviceCharacteristics.TrackingReference) != 0)
                return "tracking-reference";

            if ((characteristics & InputDeviceCharacteristics.TrackedDevice) != 0)
                return "tracker";

            return "other";
        }
        private static string InferBrand()
        {
            var xr = XRSettings.loadedDeviceName?.ToLower() ?? "";

            if (xr.Contains("oculus") || xr.Contains("meta"))
                return "Meta";

            if (Application.platform == RuntimePlatform.Android)
                return "Android";

            if (Application.platform == RuntimePlatform.WindowsPlayer)
                return "PC";

            if (Application.platform == RuntimePlatform.IPhonePlayer)
                return "Apple";

            return "Unknown";
        }
    }

    public class DetectedPeripheral
    {
        public string Name;
        public string Brand;
        public string Type;
        public bool IsHaptic;
    }
}
