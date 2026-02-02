using System;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using Newtonsoft.Json;

namespace GossipSDK.Tracking.PlatformSpecification
{
    [Serializable]
    public class PlayableAreaTracker
        : GenericSocketConnection<PlayableAreaTracker.EntityData, PlayableAreaTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingPlayableArea";

        [Serializable]
        public class EntityData : Data
        {
            public string AreaType { get; set; }
            public float Width { get; set; }
            public float Height { get; set; }
            public float Depth { get; set; }
            public float AreaSquareMeters { get; set; }
            public string SceneName { get; set; }
            public string TimestampUtc { get; set; }

            [JsonConstructor]
            public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }
    }
}
