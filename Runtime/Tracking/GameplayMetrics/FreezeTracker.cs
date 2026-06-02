using System;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using Newtonsoft.Json;
using UnityEngine;

namespace GossipSDK.Tracking.GameplayMetrics
{
    [Serializable]
    public class FreezeTracker : GenericSocketConnection<FreezeTracker.EntityData, FreezeTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingFreeze";

        [Serializable]
        public class EntityData : Data
        {
            public float  FPS          { get; set; }
            public float  DurationMs   { get; set; }
            public string SceneName    { get; set; }
            public string TimestampUtc { get; set; }

            [JsonConstructor] public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }

        public void CapFreeze(float fps, float durationMs, string sceneName)
        {
            try
            {
                var e = new EntityData
                {
                    FPS          = fps,
                    DurationMs   = durationMs,
                    SceneName    = sceneName ?? string.Empty,
                    TimestampUtc = DateTime.UtcNow.ToString("o")
                };
                CapSession(e);
                if (GossipSDK.Core.Gossip.Instance?.Settings?.EnableDebug == true)
                    Debug.Log($"[FreezeTracker] CapFreeze fps={fps:F1} durationMs={durationMs:F0} scene={sceneName}");
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception("[FreezeTracker] CapFreeze failed", ex));
            }
        }
    }
}
