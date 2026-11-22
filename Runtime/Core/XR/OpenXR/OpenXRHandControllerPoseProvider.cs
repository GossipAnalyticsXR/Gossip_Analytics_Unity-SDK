using UnityEngine;
using UnityEngine.XR;
using GossipSDK.Core.XR;

namespace GossipSDK.XR
{
    public sealed class OpenXRHandControllerPoseProvider : IHandControllerPoseProvider
    {
        private InputDevice left;
        private InputDevice right;

        public bool IsSupported
        {
            get
            {
                EnsureDevices();
                return left.isValid || right.isValid;
            }
        }

        public OpenXRHandControllerPoseProvider()
        {
            left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        }

        public bool TryGetLeftPose(out Vector3 pos, out Quaternion rot)
        {
            EnsureDevices();
            return TryGetPose(left, out pos, out rot);
        }

        public bool TryGetRightPose(out Vector3 pos, out Quaternion rot)
        {
            EnsureDevices();
            return TryGetPose(right, out pos, out rot);
        }

        private void EnsureDevices()
        {
            if (!left.isValid)
                left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if (!right.isValid)
                right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        }

        private bool TryGetPose(InputDevice device, out Vector3 pos, out Quaternion rot)
        {
            /*
             * UNA POSE SIN TRACKING NO ES UN DATO.
             *
             * `devicePosition` sigue devolviendo true cuando el mando se pierde:
             * entrega la ULTIMA pose conocida, congelada. Medido el 22-11-2026 en
             * Quest 3, la mano derecha se quedo clavada en (1.95, 0.68, 1.97)
             * durante cinco segundos y se grabaron diez muestras de una mano que
             * no estaba. Una mano sostenida por una persona siempre tiene jitter;
             * un valor identico bit a bit diez veces seguidas no es una postura,
             * es una lectura muerta.
             *
             * Solo se rechaza cuando el dispositivo DICE que no esta trackeado. Si
             * no publica `trackingState` se sigue como antes, porque es preferible
             * un dato de mas a quedarse sin manos en una plataforma que no lo
             * reporte.
             */
            InputTrackingState estado;
            if (device.TryGetFeatureValue(CommonUsages.trackingState, out estado) &&
                ((estado & InputTrackingState.Position) == 0 ||
                 (estado & InputTrackingState.Rotation) == 0))
            {
                pos = default;
                rot = default;
                return false;
            }

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
