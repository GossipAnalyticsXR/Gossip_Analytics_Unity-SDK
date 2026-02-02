using System;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using Newtonsoft.Json;
using UnityEngine;

namespace GossipSDK.Tracking.Conectivity
{
    [Serializable]
    public class ConnectivityTracker
        : GenericSocketConnection<ConnectivityTracker.EntityData, ConnectivityTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingConnectivity";

        [Serializable]
        public class EntityData : Data
        {
            [field: SerializeField] public string ConnectionType { get; set; } // Wifi | Mobile | None
            [field: SerializeField] public bool IsOnline { get; set; }
            [field: SerializeField] public float? DownloadMbps { get; set; }
            [field: SerializeField] public string Reachability { get; set; }
            [field: SerializeField] public string SceneName { get; set; }
            [field: SerializeField] public string TimestampUtc { get; set; }

            [JsonConstructor]
            public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }

        public void CapConnectivity(EntityData data)
        {
            CapSession(data);
        }
    }
}
