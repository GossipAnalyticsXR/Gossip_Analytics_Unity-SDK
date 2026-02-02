using System;
using UnityEngine;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using Newtonsoft.Json;

namespace GossipSDK.Tracking.PlatformSpecification
{
    [Serializable]
    public class RealityModeTracker
        : GenericSocketConnection<RealityModeTracker.EntityData, RealityModeTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingRealityModeTransition";

        [Serializable]
        public class EntityData : Data
        {
            public string FromMode { get; set; }
            public string ToMode { get; set; }
            public double DurationInPreviousMode { get; set; }
            public string SceneName { get; set; }
            public string TimestampUtc { get; set; }
            [JsonConstructor] public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }
    }
}
