using UnityEngine;

namespace GossipSDK.Utilities
{
    /// <summary>
    /// El unico sitio del SDK donde se decide que se manda como version de la app.
    ///
    /// Por que existe: hasta el PR #82, PlatformMonitorComponent sustituia una version
    /// vacia o 0.0.0 por 1.0.0 mientras los otros nueve sitios mandaban
    /// Application.version cruda. Esa cadena es la clave con la que el backend cruza el
    /// eje de FPS (experienceinfos.AppVersion) con el de resolucion (devices.version)
    /// en compatibilityAppVsDevice, asi que la discrepancia dejaba un eje sin medir.
    ///
    /// El #82 quito la excepcion y los diez volvieron a coincidir, pero coincidian por
    /// convencion: nada impedia que el numero once se inventara su propia regla. Esto
    /// lo cierra por construccion.
    ///
    /// NO normaliza nada, a proposito: devuelve exactamente Application.version, asi
    /// que introducirlo no mueve ni un dato. El dia que haga falta una regla --recortar
    /// espacios, poner una palabra cuando no hay version declarada-- se escribe aqui una
    /// sola vez y la cumplen los diez.
    /// </summary>
    public static class GossipVersion
    {
        public static string App => Application.version;
    }
}
