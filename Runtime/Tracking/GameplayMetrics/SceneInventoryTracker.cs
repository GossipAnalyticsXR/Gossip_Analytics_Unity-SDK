using System;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using Newtonsoft.Json;

namespace GossipSDK.Tracking.GameplayMetrics
{
    /// <summary>
    /// Inventario de la escena: que objetos instrumentados HAY, se toquen o no.
    ///
    /// Sin esto el dashboard solo conoce los objetos que alguien toco, asi que no
    /// puede decir 3 de 8 no se toco nunca: le falta el denominador. Este tracker
    /// es solo el transporte; quien enumera la escena es SceneInventoryComponent.
    ///
    /// Una fila por objeto, no una lista dentro de una fila: el sobre ya agrupa
    /// mensajes, asi que el backend no tiene que desanidar nada.
    /// </summary>
    [Serializable]
    public class SceneInventoryTracker : GenericSocketConnection<SceneInventoryTracker.EntityData, SceneInventoryTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingSceneInventory";

        public string EventTypeForEndpoint => "TrackingSceneInventory";

        [Serializable]
        public class EntityData : Data
        {
            /// <summary>
            /// Nombre del GameObject. NO es unico: medido el 19/09/2026 en Hospital Zone,
            /// dos objetos distintos se llamaban igual, y en el dashboard eran una sola fila.
            /// </summary>
            public string ObjectName { get; set; }

            /// <summary>Ruta en la jerarquia. Es lo unico que separa dos objetos con el mismo nombre.</summary>
            public string ObjectPath { get; set; }

            public string ObjectTag { get; set; }

            /// <summary>Que clase de objeto instrumentado es. Hoy solo Interactable.</summary>
            public string Kind { get; set; }

            public string SceneName { get; set; }

            /// <summary>
            /// Version de la build. El inventario solo se compara DENTRO de una version:
            /// entre versiones la escena es otra, y el denominador tambien.
            /// </summary>
            public string AppVersion { get; set; }

            public string TimestampUtc { get; set; }

            [JsonConstructor] public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }

        /// <summary>
        /// Anota un objeto del inventario. Solo escribe en la base local; quien empuja
        /// al servidor es SendDataToSocket, que el componente llama una vez al final.
        /// </summary>
        public void CapSceneObject(
            string objectName,
            string objectPath,
            string objectTag,
            string kind,
            string sceneName,
            string appVersion)
        {
            var data = new EntityData
            {
                ObjectName = objectName ?? "",
                ObjectPath = objectPath ?? "",
                ObjectTag = objectTag ?? "",
                Kind = kind ?? "",
                SceneName = sceneName ?? "",
                AppVersion = appVersion ?? "",
                TimestampUtc = DateTime.UtcNow.ToString("o")
            };

            CapSession(data);
        }
    }
}
