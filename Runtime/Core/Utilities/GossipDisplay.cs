using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace GossipSDK.Utilities
{
    /// <summary>
    /// El objetivo de refresco del visor, en Hz, leido del propio dispositivo.
    ///
    /// Por que existe: el backend puntuaba el FPS contra un 72 fijo. Ese numero no es
    /// el objetivo de nadie -- una Quest 3 corre a 72, 90 o 120 segun lo que elija el
    /// usuario en sus ajustes. Medido el 11-sep-2026, la card marcaba 73,99 fps, o sea
    /// por encima del supuesto techo, y la puntuacion saturaba al 100% sin saber contra
    /// que corria. Preguntandoselo al visor no hace falta ninguna constante.
    ///
    /// Se lee del XRDisplaySubsystem, que es API del modulo XR de Unity y no de OVR:
    /// PlatformMonitorComponent ya lo usa para MotionToPhoton, asi que la fuente esta
    /// probada en este proyecto. Referenciar tipos de Oculus directamente es lo que da
    /// CS0234 cuando el paquete no esta.
    ///
    /// Devuelve 0 cuando no se puede leer --sin XR, en editor, o si el proveedor no lo
    /// expone--. Cero NO es un objetivo: aguas abajo significa que no se ha medido, y
    /// una muestra sin objetivo no debe puntuar en vez de puntuar mal.
    /// </summary>
    public static class GossipDisplay
    {
        // Se reutiliza para no reservar una lista en cada muestra.
        private static readonly List<XRDisplaySubsystem> _pantallas =
            new List<XRDisplaySubsystem>();

        public static float TargetHz()
        {
            try
            {
                _pantallas.Clear();
                SubsystemManager.GetInstances(_pantallas);

                for (int i = 0; i < _pantallas.Count; i++)
                {
                    if (_pantallas[i] == null) continue;

                    float hz;
                    if (_pantallas[i].TryGetDisplayRefreshRate(out hz) && hz > 0f)
                        return hz;
                }
            }
            catch
            {
                // Un fallo leyendo el refresco no puede tumbar la muestra de FPS.
            }

            return 0f;
        }
    }
}
