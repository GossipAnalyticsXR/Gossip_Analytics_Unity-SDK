using System;
using UnityEngine;
using Newtonsoft.Json;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using GossipSDK.Core.Connection;

namespace GossipSDK.Tracking.GameplayMetrics
{
    [Serializable]
    public class PositionTracker : GenericSocketConnection<PositionTracker.EntityData, PositionTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingPosition";

        [Serializable]
        public class EntityData : Data
        {
            [field: SerializeField]
            public float X { get; set; }
        
            [field: SerializeField]
            public float Y { get; set; }
        
            [field: SerializeField]
            public float Z { get; set; }

            [field: SerializeField]
            public string TimestampUtc { get; set; }
            
            [JsonConstructor]
            public EntityData() {}
        }
        
        [Serializable]
        public class TrackerMessage : Message<EntityData>
        {

        }
    }
}
