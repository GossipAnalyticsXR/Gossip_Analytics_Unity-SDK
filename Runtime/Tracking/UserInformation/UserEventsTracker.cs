using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;

namespace GossipSDK.Tracking.GameplayMetrics
{
    [Serializable]
    public class UserEventTracker : GenericSocketConnection<UserEventTracker.EntityData, UserEventTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingUserEvent";

        [Serializable]
        public class EntityData : Data
        {
            public string EventName { get; set; }
            public string Category { get; set; }
            public string Label { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
            public string SceneName { get; set; }
            public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
            public string TimestampUtc { get; set; }

            [JsonConstructor] public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }

        public void CaptureEvent(string eventName, string category = null, string label = null, Vector3? worldPos = null, Dictionary<string, object> properties = null)
        {
            try
            {
                Vector3 p = worldPos ?? Vector3.zero;
                var data = new EntityData
                {
                    EventName = eventName ?? "unknown",
                    Category = category ?? "default",
                    Label = label,
                    X = p.x,
                    Y = p.y,
                    Z = p.z,
                    SceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    TimestampUtc = DateTime.UtcNow.ToString("o"),
                    Properties = properties ?? new Dictionary<string, object>()
                };

                CapSession(data);

                if (GossipSDK.Core.Gossip.Instance?.Settings?.EnableDebug == true)
                {
                    Debug.Log($"[UserEventTracker] Captured event '{data.EventName}' cat='{data.Category}' label='{data.Label}' pos=({data.X:F2},{data.Y:F2},{data.Z:F2})");
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception("[UserEventTracker] CaptureEvent failed", ex));
            }
        }

        public void CaptureEventAt(string eventName, Transform t, string category = null, string label = null, Dictionary<string, object> properties = null)
        {
            Vector3 pos = t != null ? t.position : Vector3.zero;
            CaptureEvent(eventName, category, label, pos, properties);
        }

        public int GetPendingCountSafe()
        {
            try { return GetPendingCount(); }
            catch { return 0; }
        }
    }
}
