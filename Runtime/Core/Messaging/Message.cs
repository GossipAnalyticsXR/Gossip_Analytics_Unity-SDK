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
    }
}
