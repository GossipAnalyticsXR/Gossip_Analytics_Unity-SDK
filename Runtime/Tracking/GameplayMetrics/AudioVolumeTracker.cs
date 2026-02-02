using System;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using Newtonsoft.Json;

namespace GossipSDK.Tracking.GameplayMetrics
{
    [Serializable]
    public class AudioVolumeTracker
        : GenericSocketConnection<AudioVolumeTracker.EntityData, AudioVolumeTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingAudioVolume";

        [Serializable]
        public class EntityData : Data
        {
            public float MasterVolume { get; set; }
            public float MusicVolume { get; set; }
            public float SfxVolume { get; set; }

            public string SceneName { get; set; }
            public string Source { get; set; }
            public string TimestampUtc { get; set; }

            [JsonConstructor]
            public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }
    }
}
