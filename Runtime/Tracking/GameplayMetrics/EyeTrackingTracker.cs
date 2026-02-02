using System;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using UnityEngine;

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
            [SerializeField] public string HitObjectName { get; set; }
            [SerializeField] public string HitObjectTag { get; set; }

            [SerializeField] public float HitX { get; set; }
            [SerializeField] public float HitY { get; set; }
            [SerializeField] public float HitZ { get; set; }

            [SerializeField] public float FixationDurationSeconds { get; set; }
            [SerializeField] public string SceneName { get; set; }
            [SerializeField] public string TrackingSource { get; set; }
            [SerializeField] public string TimestampUtc { get; set; }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }
    }
}
