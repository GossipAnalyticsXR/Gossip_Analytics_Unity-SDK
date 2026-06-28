using System;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using Newtonsoft.Json;

namespace GossipSDK.Tracking.PlatformSpecification
{
    [Serializable]
    public class MicPermissionTracker
        : GenericSocketConnection<MicPermissionTracker.EntityData, MicPermissionTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingMicPermission";

        [Serializable]
        public class EntityData : Data
        {
            public string PlayerID { get; set; }
            public string SessionID { get; set; }
            public bool MicDenied { get; set; }
            public string TimestampUtc { get; set; }

            [JsonConstructor]
            public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }
    }
}
