using System;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using Newtonsoft.Json;

namespace GossipSDK.Tracking.PlatformSpecification
{
    [Serializable]
    public class BoundaryPressureSummaryTracker
        : GenericSocketConnection<BoundaryPressureSummaryTracker.EntityData, BoundaryPressureSummaryTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingBoundaryPressureSummary";

        [Serializable]
        public class EntityData : Data
        {
            public string PlayerID { get; set; }
            public string SessionID { get; set; }
            // null = el SDK no pudo leer el limite del guardian en esta sesion.
            // Newtonsoft lo serializa como null, y el ingest lo guarda como null
            // desde el PR #60 del Backend-SDK.
            public bool? HadBoundaryPressure { get; set; }
            public string SceneId { get; set; }
            public string TimestampUtc { get; set; }

            [JsonConstructor]
            public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }
    }
}
