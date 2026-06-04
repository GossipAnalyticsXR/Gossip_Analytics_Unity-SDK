using System;
using System.Collections.Generic;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using Newtonsoft.Json;

namespace GossipSDK.Tracking.GameplayMetrics
{
    [Serializable]
    public class DifficultyTracker : GenericSocketConnection<DifficultyTracker.EntityData, DifficultyTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TRACKING_DIFFICULTY_CHANGE";

        public string EventTypeForEndpoint => "TRACKING_DIFFICULTY_CHANGE";

        [Serializable]
        public class EntityData : Data
        {
            public string SceneName { get; set; }
            public string DifficultyId { get; set; }
            public float NumericDifficulty { get; set; }
            public string Reason { get; set; }
            public string TimestampUtc { get; set; }

            [JsonConstructor] public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }

        public void CapDifficulty(string difficultyId, float numericValue = 0f, string reason = null, string scene = null)
        {
            var data = new EntityData
            {
                SceneName = scene ?? UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                DifficultyId = difficultyId ?? "",
                NumericDifficulty = numericValue,
                Reason = reason ?? "unknown",
                TimestampUtc = DateTime.UtcNow.ToString("o")
            };

            CapSession(data);
        }
    }
}
