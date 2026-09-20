using System.Text;
using UnityEngine;

namespace GossipSDK.Utilities
{
    /// <summary>
    /// La ruta de un objeto dentro de la jerarquia de su escena.
    ///
    /// Es lo unico que distingue dos objetos que se llaman igual, y el ingest ya cuenta
    /// con ella: el upsert de sceneinventories filtra por
    /// {appId, AppVersion, SceneName, ObjectPath} y descarta el mensaje que llega sin
    /// ruta, con el comentario "Sin ruta no hay identidad".
    ///
    /// Vivia copiada, identica, en SceneInventoryComponent y en AdComponent. Una sola
    /// copia es lo que garantiza que la ruta que viaja con una interaccion se pueda
    /// comparar, caracter a caracter, con la que viaja en el inventario.
    /// </summary>
    public static class Jerarquia
    {
        /// <summary>
        /// Nombres de la raiz de la escena al objeto, separados por "/". Cadena vacia si
        /// el transform ya fue destruido.
        /// </summary>
        public static string RutaDe(Transform t)
        {
            if ((Object)t == null) return string.Empty;

            var ruta = new StringBuilder(t.name);
            var padre = t.parent;
            while (padre != null)
            {
                ruta.Insert(0, padre.name + "/");
                padre = padre.parent;
            }

            return ruta.ToString();
        }
    }
}
