using System;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using Newtonsoft.Json;
using UnityEngine;

namespace GossipSDK.Tracking.GameplayMetrics
{
    [Serializable]
    public class RotationTracker : GenericSocketConnection<RotationTracker.EntityData, RotationTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingRotation";

        [Serializable]
        public class EntityData : Data
        {
            [field: SerializeField]
            public float RotX { get; set; }

            [field: SerializeField]
            public float RotY { get; set; }

            [field: SerializeField]
            public float RotZ { get; set; }

            [field: SerializeField]
            public float Speed { get; set; }

            [field: SerializeField]
            public float AngularSpeed { get; set; }

            [field: SerializeField]
            public string TimestampUtc { get; set; }

            [field: SerializeField]
            public string ObjectName { get; set; }

            [JsonConstructor]
            public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData>
        {

        }
    }
}
