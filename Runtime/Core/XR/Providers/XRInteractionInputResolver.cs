using System.Collections.Generic;
using UnityEngine.XR;

namespace GossipSDK.Core.XR
{
    /// <summary>
    /// Que esta usando la persona AHORA: mano, mando, o no se sabe.
    ///
    /// Lo sella InteractableComponent en cada interaccion -start, instant y el
    /// cancelado-, asi que esto es lo que separa las pestanas Hand y Controller del
    /// dashboard. Si aqui se responde mal, una sesion entera hecha con las manos
    /// aparece como de mando y la pestana Hand se queda vacia.
    /// </summary>
    public static class XRInteractionInputResolver
    {
        // Una sola lista, reutilizada: GetDevicesWithCharacteristics pide una y esto
        // se llama en cada interaccion. Se limpia y se consume en la misma llamada.
        private static readonly List<InputDevice> dispositivos = new List<InputDevice>();

        public static InteractionInputType GetCurrentInputType()
        {
            // La mano PRIMERO, y no al reves.
            //
            // Hasta el 22-sep-2026 esto preguntaba por UN dispositivo por nodo, con
            // GetDeviceAtXRNode(XRNode.LeftHand / RightHand), y decidia que era una
            // mano si ese dispositivo NO reportaba trigger. El problema no es el
            // trigger: es que por nodo solo viene UN dispositivo, y cual sea no lo
            // decide este codigo. Con los Touch emparejados, si en ese nodo esta el
            // mando -valido y con trigger-, la respuesta era Controller aunque las
            // manos estuvieran siendo seguidas en ese mismo instante.
            //
            // Es la misma trampa que el PR #150 quito un piso mas arriba, en
            // InputUsageTrackerComponent, y la misma de NoIteraction: preguntar si el
            // mando EXISTE en vez de si se esta usando. Ahora se pregunta por donde
            // pregunta el tracker ya arreglado, y con su mismo orden.
            if (AlgunoSeguido(InputDeviceCharacteristics.HandTracking))
                return InteractionInputType.Hand;

            if (AlgunoSeguido(
                    InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.HeldInHand))
                return InteractionInputType.Controller;

            return InteractionInputType.Unknown;
        }

        /// <summary>
        /// Hay algun dispositivo de este tipo que ademas este SIENDO SEGUIDO ahora.
        /// isValid solo dice que existe; isTracked dice que el sistema lo ve. Esa es
        /// la diferencia entre un mando encima de la mesa y un mando en la mano.
        /// </summary>
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
    }
}
