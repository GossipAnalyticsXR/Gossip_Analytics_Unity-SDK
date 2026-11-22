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
                /*
                 * EL FALLBACK DEVUELVE EL MISMO ESPACIO QUE EL DISPOSITIVO.
                 *
                 * El camino de arriba devuelve `devicePosition`, que esta en
                 * espacio de TRACKING. Este devolvia `fallbackTransform.position`,
                 * que es la camara en espacio de MUNDO: un mismo metodo con dos
                 * sistemas de coordenadas segun la rama, y quien llama no puede
                 * saber cual le toco.
                 *
                 * Esa mezcla ya costo un detector. HandControllerTrackingComponent
                 * restaba la mano -tracking- de la cabeza -mundo- y el desfase del
                 * rig entraba entero en el brazo horizontal: medido el 22-11-2026
                 * en Quest 3, con el brazo levantado el campo daba 15.8 grados y
                 * eran 53.8. Se arreglo en el PR #250; esto cierra la puerta por
                 * la que entro, y HeadPose tiene tres consumidores -manos, balance
                 * y mirada-.
                 *
                 * La camara cuelga de la raiz de tracking, asi que basta con
                 * expresarla en el sistema de su padre.
                 */
                Transform rig = fallbackTransform.parent;
                position = rig != null
                    ? rig.InverseTransformPoint(fallbackTransform.position)
                    : fallbackTransform.position;
                rotation = rig != null
                    ? Quaternion.Inverse(rig.rotation) * fallbackTransform.rotation
                    : fallbackTransform.rotation;
                return true;
            }

            position = default;
            rotation = default;
            return false;
        }
    }
}
