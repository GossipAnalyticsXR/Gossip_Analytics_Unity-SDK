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
