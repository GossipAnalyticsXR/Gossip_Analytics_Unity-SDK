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

            // El visor ya NO cuelga de XRSettings.isDeviceActive.
            //
            // Medido el 06/09/2026 sobre 112 sesiones: 18 traian a la vez `hmd` y
            // `mobile`, que por construccion se excluian (uno pedia la bandera en
            // positivo y el otro en negado). Eso solo pasa si la bandera cambia de
            // valor dentro de la misma sesion. De ella dependia que el visor
            // existiera o no en el dato: 18 sesiones salieron con mandos y sin
            // visor, y 39 con visor y sin mandos.
            //
            // InputDevices ya trae el visor con la caracteristica HeadMounted, y es
            // la misma fuente que los mandos, asi que los dos aparecen o no aparecen
            // juntos. Ver ClassifyXrDevice.
            bool hayXr = false;

            // XR devices (controllers, hand tracking, eye tracking, body trackers, base stations).
            // VR controllers do NOT show up in Gamepad.all, so they are enumerated here.
            var xrDevices = new List<UnityEngine.XR.InputDevice>();
            InputDevices.GetDevices(xrDevices);

            foreach (var device in xrDevices)
            {
                if (!device.isValid)
                    continue;

                // Cualquier dispositivo XR valido, visor incluido, marca que la sesion
                // es XR. De esto dependen Touchscreen y Keyboard/Mouse mas abajo.
                hayXr = true;

                var type = ClassifyXrDevice(device.characteristics);
                if (type == null)
                    continue;

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
            // Gamepads: solo cuando NO hay XR, mismo criterio que la pantalla tactil
            // y el teclado.
            //
            // En XR los mandos ya los enumera InputDevices con su nombre y su tipo
            // reales. Lo que Gamepad.all anade encima son duplicados con nombre
            // ilegible: medido el 06/09/2026, 23 documentos `xr-controller` (Oculus
            // Touch 19, Quest Pro Touch 4) y 23 `controller` llamados «Device 0x...»,
            // en practicamente las mismas sesiones (22 de 23 solapan). El comentario
            // que decia que los mandos de VR no salen en Gamepad.all no se sostiene
            // contra el dato.
            if (!hayXr)
            {
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
            }

            // Pantalla tactil: solo cuando NO hay XR. En una Quest, que es Android,
            // Input.touchSupported es cierto, asi que esto llenaba el dato de
            // «Touchscreen» en sesiones de gafas cada vez que la bandera de XR
            // fallaba. Una pantalla tactil solo es el periferico de entrada real en
            // una experiencia que no es XR.
            if (Input.touchSupported && !hayXr)
            {
                peripherals.Add(new DetectedPeripheral
                {
                    Name = "Touchscreen",
                    Type = "mobile",
                    Brand = InferBrand(),
                    IsHaptic = false
                });
            }

            // Teclado y raton: mismo criterio. Dentro del editor de Unity siempre
            // hay teclado, y eso no convierte una sesion de gafas en una sesion de
            // escritorio. Solo cuenta como periferico si la experiencia no es XR.
            if (!hayXr && (Keyboard.current != null || Mouse.current != null))
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
        /// El visor devuelve «hmd»: es la unica fuente que lo reporta desde que
        /// desaparecio la rama de XRSettings.
        /// </summary>
        private static string ClassifyXrDevice(InputDeviceCharacteristics characteristics)
        {
            if ((characteristics & InputDeviceCharacteristics.HeadMounted) != 0)
                return "hmd";

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
            // XRSettings.loadedDeviceName es la MISMA bandera inestable que hacia
            // desaparecer el visor. Cuando venia vacia, esto caia a la plataforma y
            // devolvia «Android» para una Quest. Medido el 06/09/2026: 13 documentos
            // con marca «Oculus» y 10 con «Android», siendo todos gafas.
            //
            // SystemInfo no depende de que XR haya levantado, asi que se mira tambien
            // ahi antes de caer a la plataforma.
            var pistas = ((XRSettings.loadedDeviceName ?? "") + " " +
                          (SystemInfo.deviceModel ?? "") + " " +
                          (SystemInfo.deviceName ?? "")).ToLower();

            if (pistas.Contains("oculus") || pistas.Contains("meta") || pistas.Contains("quest"))
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
