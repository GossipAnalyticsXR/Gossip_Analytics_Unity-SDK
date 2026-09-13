using System;
using System.Collections.Generic;
using UnityEngine;
using GossipSDK.Core.Data;
using GossipSDK.Core.Session;

namespace GossipSDK.Core.Messaging
{
    [Serializable]
    public class Message<T> : SessionInfo where T : IData
    {
        [field: SerializeField]
        public string EventType { get; set; }

        [field: SerializeField]
        public string SceneUser { get; set; }

        [field: SerializeField]
        public string TimeMovement { get; set; }

        [field: SerializeField]
        public List<T> Messages { get; set; } = new List<T>();

        [field: SerializeField]
        public string Engine { get; set; }

        // Verdad del runtime, no inferencia del hardware: un PeripheralType «desktop»
        // tambien lo emite un cliente con build de PC. Va en TODOS los sobres porque
        // el punto de inyeccion (GenericSocketConnection) es unico.
        [field: SerializeField]
        public bool IsEditor { get; set; }

        // Version del SDK que produjo el lote, desde Constants.SdkVersion. Va en el sobre
        // por el mismo motivo que IsEditor y por el mismo sitio: el punto de inyeccion
        // (GenericSocketConnection) es unico, asi que la llevan TODOS los tipos de evento.
        //
        // Antes la version solo viajaba en el payload de AudioReactionTracker, asi que
        // cortar el dato por epoca se hacia comparando fechas a mano, que es fragil.
        [field: SerializeField]
        public string SdkVersion { get; set; }
    }
}
