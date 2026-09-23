using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using GossipSDK.Tracking.GameplayMetrics;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class InputUsageTrackerComponent : MonoBehaviour
    {
        private InputUsageTracker tracker;
        private float lastSampleTime;
        private float captureTimer = 0f;
        private const float captureInterval = 5f;

        private void Start()
        {
            tracker = GossipSDK.Core.Gossip.Instance?.InputUsageTracker;
            lastSampleTime = Time.time;
        }

        private void Update()
        {
            if (tracker == null) return;

            float delta = Time.deltaTime;

            // Se pregunta por las MANOS primero, y no al reves.
            //
            // Con el orden anterior el `else if` de las manos no llegaba a evaluarse:
            // `IsUsingController` preguntaba si EXISTE un XRController en
            // `InputSystem.devices`, y con los Touch emparejados eso es cierto en cada
            // frame aunque esten en la mesa. Es la misma trampa que ya nos comimos en
            // NoIteraction, donde el mando esta conectado siempre.
            if (IsUsingHands())
                tracker.RegisterHandUsage(delta);
            else if (IsUsingController())
                tracker.RegisterControllerUsage(delta);

            captureTimer += delta;
            if (captureTimer >= captureInterval)
            {
                captureTimer = 0f;
                if (tracker != null)
                    tracker.CaptureSnapshot();
            }
        }

        // Una sola lista, reutilizada: `GetDevicesWithCharacteristics` pide una y esto
        // corre en cada Update. Se limpia y se consume en la misma llamada.
        private static readonly List<InputDevice> dispositivos = new List<InputDevice>();

        /// Hay algun dispositivo de este tipo que ademas este SIENDO SEGUIDO ahora.
        /// `isValid` solo dice que existe; `isTracked` dice que el sistema lo ve.
        private static bool AlgunoSeguido(InputDeviceCharacteristics caracteristicas)
        {
            dispositivos.Clear();
            InputDevices.GetDevicesWithCharacteristics(caracteristicas, dispositivos);
            for (int i = 0; i < dispositivos.Count; i++)
            {
                InputDevice d = dispositivos[i];
                if (!d.isValid) continue;
                if (d.TryGetFeatureValue(CommonUsages.isTracked, out bool seguido) && seguido)
                    return true;
            }
            return false;
        }

        // Antes esto vivia detras de #if UNITY_XR_HANDS, un define que el SDK NO declara:
        // GossipSDK.Runtime.asmdef solo define META_CORE y package.json no pide ninguna
        // dependencia. O sea que compilaba a `return false` siempre, y HandUsagePercent
        // era cero por construccion, no por medicion.
        //
        // Ahora se pregunta por el mismo sitio que ya usa XRInteractionInputResolver:
        // UnityEngine.XR, que es modulo integrado. Sin paquete, sin define, sin
        // referencia nueva en el asmdef.
        private bool IsUsingHands()
        {
            return AlgunoSeguido(InputDeviceCharacteristics.HandTracking);
        }

        private bool IsUsingController()
        {
            return AlgunoSeguido(
                InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.HeldInHand);
        }

        private void OnDisable()
        {
            if (tracker != null)
                tracker.CaptureSnapshot();
            tracker?.SendDataToSocket();
        }

        private void OnApplicationQuit()
        {
            if (tracker != null)
                tracker.CaptureSnapshot();
            tracker?.SendDataToSocket();
        }

        private void OnDestroy()
        {
            if (tracker != null)
                tracker.CaptureSnapshot();
            tracker?.SendDataToSocket();
        }
    }
}
