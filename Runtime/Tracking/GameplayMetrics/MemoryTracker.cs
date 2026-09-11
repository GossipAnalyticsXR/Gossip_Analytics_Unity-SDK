using System;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using Newtonsoft.Json;
using UnityEngine;

namespace GossipSDK.Tracking.GameplayMetrics
{
    [Serializable]
    public class MemoryTracker : GenericSocketConnection<MemoryTracker.EntityData, MemoryTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingMemoryUsage";

        [Serializable]
        public class EntityData : Data
        {
            [field: SerializeField] public long TotalAllocatedBytes { get; set; }
            [field: SerializeField] public long TotalReservedBytes { get; set; }
            [field: SerializeField] public long MonoUsedBytes { get; set; }
            [field: SerializeField] public int GcCollectionsGen0 { get; set; }
            [field: SerializeField] public int GcCollectionsGen1 { get; set; }
            [field: SerializeField] public int GcCollectionsGen2 { get; set; }
            [field: SerializeField] public float CurrentFPS { get; set; }
            // El objetivo de refresco del visor en el momento de esta muestra, en Hz.
            // Viaja CON la muestra y no en `devices` a proposito: el usuario puede cambiar
            // el refresco sin cerrar la app, asi que es una propiedad del instante, no del
            // usuario. Cero significa que no se pudo leer: no es un objetivo de 0 Hz.
            [field: SerializeField] public float TargetHz { get; set; }
            [field: SerializeField] public string TimestampUtc { get; set; }

            [JsonConstructor]
            public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }
    }
}
