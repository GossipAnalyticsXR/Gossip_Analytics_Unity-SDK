using System;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using Newtonsoft.Json;
using UnityEngine;

namespace GossipSDK.Tracking.PlatformSpecification
{
    [Serializable]
    public class PeripheralTracker
        : GenericSocketConnection<PeripheralTracker.EntityData, PeripheralTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingPeripherals";

        [Serializable]
        public class EntityData : Data
        {
            [field: SerializeField] public string PeripheralName { get; set; }
            [field: SerializeField] public string Brand { get; set; }
            [field: SerializeField] public string PeripheralType { get; set; }
            [field: SerializeField] public bool IsHaptic { get; set; }
            [field: SerializeField] public double UsageDurationSeconds { get; set; }
            [field: SerializeField] public string SceneName { get; set; }
            [field: SerializeField] public string TimestampUtc { get; set; }

            [JsonConstructor]
            public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }
    }
}