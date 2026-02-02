using System;
using System.Collections.Generic;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using Newtonsoft.Json;

namespace GossipSDK.Tracking.GameplayMetrics
{
    [Serializable]
    public class MistakeTracker : GenericSocketConnection<MistakeTracker.EntityData, MistakeTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingMistake";

        [Serializable]
        public class EntityData : Data
        {
            public string ObjectName { get; set; }
            public string ObjectTag { get; set; }
            public string MistakeType { get; set; }
            public int Severity { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
            public string SceneName { get; set; }
            public string TimestampUtc { get; set; }

            [JsonConstructor]
            public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }
    }
}
