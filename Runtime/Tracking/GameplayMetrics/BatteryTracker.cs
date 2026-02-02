using System;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using UnityEngine;
using Newtonsoft.Json;

namespace GossipSDK.Tracking.GameplayMetrics
{
    [Serializable]
    public class BatteryTracker : GenericSocketConnection<BatteryTracker.EntityData, BatteryTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingBatteryUsage";

        [Serializable]
        public class EntityData : Data
        {
            [field: SerializeField] public float BatteryLevel { get; set; }
            [field: SerializeField] public string BatteryStatus { get; set; }
            [field: SerializeField] public string TimestampUtc { get; set; }
            [JsonConstructor] public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData>
        { }
    }
}
