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
            [field: SerializeField] public string TimestampUtc { get; set; }

            [JsonConstructor]
            public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }
    }
}
