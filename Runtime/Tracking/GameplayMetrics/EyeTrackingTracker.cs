using System;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;

namespace GossipSDK.Tracking.GameplayMetrics
{
    [Serializable]
    public class EyeTrackingTracker
        : GenericSocketConnection<EyeTrackingTracker.EntityData, EyeTrackingTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingEye";

        public void Capture(EntityData data)
        {
            CapSession(data);
        }

        [Serializable]
        public class EntityData : Data
        {
            public string HitObjectName { get; set; }
            public string HitObjectTag { get; set; }

            public float HitX { get; set; }
            public float HitY { get; set; }
            public float HitZ { get; set; }

            public float FixationDurationSeconds { get; set; }
            public string SceneName { get; set; }
            public string TrackingSource { get; set; }
            public string TimestampUtc { get; set; }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }
    }
}
