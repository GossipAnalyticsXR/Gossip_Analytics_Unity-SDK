using System;
using System.Collections.Generic;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using GossipSDK.Core;
using Newtonsoft.Json;

namespace GossipSDK.Tracking.GameplayMetrics
{
    [Serializable]
    public class PauseTracker : GenericSocketConnection<PauseTracker.EntityData, PauseTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingPause";

        public string EventTypeForEndpoint => "TRACKING_SESSION_PAUSE";

        [Serializable]
        public class EntityData : Data
        {
            public string EventType { get; set; }
            public string SceneName { get; set; }
            public double DurationSeconds { get; set; }
            public string PlayerID { get; set; }
            public string SessionID { get; set; }
            public string TimestampUtc { get; set; }

            [JsonConstructor] public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }

        public void CapPauseEvent(string eventType, double durationSeconds = 0.0, string sceneName = null)
        {
            var data = new EntityData
            {
                EventType = eventType ?? "pause",
                SceneName = sceneName ?? UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                DurationSeconds = durationSeconds,
                PlayerID = Gossip.Instance?.PlayerID,
                SessionID = Gossip.Instance?.SessionID,
                TimestampUtc = DateTime.UtcNow.ToString("o")
            };

            CapSession(data);
        }
    }
}
