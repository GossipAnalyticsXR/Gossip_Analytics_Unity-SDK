using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using GossipSDK.Core;

namespace GossipSDK.Tracking.GameplayMetrics
{
    [Serializable]
    public class SessionTracker : GenericSocketConnection<SessionTracker.EntityData, SessionTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingSession";

        [Serializable]
        public class EntityData : Data
        {
            [field: SerializeField] public string EventType { get; set; }
            [field: SerializeField] public string TimestampUtc { get; set; }
            [field: SerializeField] public double DurationSeconds { get; set; }
            [field: SerializeField] public string SceneName { get; set; }
            [field: SerializeField] public string PlayerId { get; set; }
            [field: SerializeField] public string SessionId { get; set; }
            public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }

        private string lastEventType = null;
        private DateTime lastEventTime = DateTime.MinValue;
        private readonly double dedupeWindowSeconds = 0.5;

        public void RecordEvent(string eventType, double durationSeconds = 0.0)
        {
            try
            {
                var now = DateTime.UtcNow;
                if (string.Equals(eventType, lastEventType, StringComparison.OrdinalIgnoreCase))
                {
                    var dt = (now - lastEventTime).TotalSeconds;
                    if (dt < dedupeWindowSeconds)
                    {
                        if (Gossip.Instance?.Settings?.EnableDebug == true)
                            Debug.Log($"[SessionTracker] Ignored duplicate event '{eventType}' within {dt:F3}s");
                        return;
                    }
                }

                lastEventType = eventType;
                lastEventTime = now;

                var data = new EntityData
                {
                    EventType = eventType,
                    TimestampUtc = now.ToString("o"),
                    DurationSeconds = durationSeconds,
                    SceneName = SceneManager.GetActiveScene().name,
                    PlayerId = Gossip.Instance?.PlayerID,
                    SessionId = Gossip.Instance?.SessionID
                };

                CapSession(data);
                if (Gossip.Instance?.Settings?.EnableDebug == true)
                    Debug.Log($"[SessionTracker] Recorded {eventType} duration={durationSeconds:F3}s scene={data.SceneName} player={data.PlayerId}");
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception("[SessionTracker] RecordEvent failed", ex));
            }
        }
    }
}