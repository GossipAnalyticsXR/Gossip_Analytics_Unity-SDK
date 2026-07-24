using System;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using GossipSDK.Core;
using Newtonsoft.Json;
using UnityEngine;

namespace GossipSDK.Tracking.GameplayMetrics
{
    [Serializable]
    public class PassthroughTracker : GenericSocketConnection<PassthroughTracker.EntityData, PassthroughTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingPassthrough";

        [Serializable]
        public class EntityData : Data
        {
            public bool Enabled { get; set; }
            public string Mode { get; set; }
            public float? Exposure { get; set; }
            public float? QualityMetric { get; set; }
            [field: SerializeField] public float? Duration   { get; set; }
            public string TimestampUtc { get; set; }
            [JsonConstructor] public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }

        public void CapPassthrough(bool enabled, string mode = null, float? exposure = null, float? quality = null, float? duration = null)
        {
            try
            {
                var e = new EntityData
                {
                    Enabled = enabled,
                    Mode = mode ?? string.Empty,
                    Exposure = exposure,
                    QualityMetric = quality,
                    Duration = duration,
                    TimestampUtc = DateTime.UtcNow.ToString("o")
                };

                CapSession(e);
                if (Gossip.Instance?.Settings?.EnableDebug == true)
                    Debug.Log($"[PassthroughTracker] CapPassthrough enabled={enabled} mode={mode} exposure={exposure} quality={quality}");
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception("[PassthroughTracker] CapPassthrough failed", ex));
            }
        }
    }
}
