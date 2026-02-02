using System;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using Newtonsoft.Json;
using UnityEngine;

namespace GossipSDK.Tracking.GameplayMetrics
{
    [Serializable]
    public class PerformanceTracker : GenericSocketConnection<PerformanceTracker.EntityData, PerformanceTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingPerformance";

        [Serializable]
        public class EntityData : Data
        {
            [field: SerializeField]
            public float AvgFPS { get; set; }

            [field: SerializeField]
            public float MinFPS { get; set; }

            [field: SerializeField]
            public float MaxFPS { get; set; }

            [field: SerializeField]
            public float MemoryMB { get; set; }

            [field: SerializeField]
            public string SceneName { get; set; }

            [field: SerializeField]
            public string TimestampUtc { get; set; }

            [JsonConstructor]
            public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData>
        { }
    }
}
