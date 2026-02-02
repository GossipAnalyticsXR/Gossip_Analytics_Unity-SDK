using System;
using System.Collections.Generic;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using Newtonsoft.Json;

namespace GossipSDK.Tracking.GameplayMetrics
{
    [Serializable]
    public class ExperienceInfoTracker : GenericSocketConnection<ExperienceInfoTracker.EntityData, ExperienceInfoTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingExperienceInfo";

        [Serializable]
        public class EntityData : Data
        {
            public double LoadTimeMs { get; set; }

            public string AppVersion { get; set; }

            public string TargetHardware { get; set; }

            public string SceneName { get; set; }
            public string TimestampUtc { get; set; }

            [JsonConstructor] public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }

        public void CapExperienceInfo(double loadTimeMs, string appVersion, string targetHardware)
        {
            try
            {
                var data = new EntityData
                {
                    LoadTimeMs = loadTimeMs,
                    AppVersion = appVersion ?? string.Empty,
                    TargetHardware = targetHardware ?? string.Empty,
                    SceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    TimestampUtc = DateTime.UtcNow.ToString("o")
                };

                CapSession(data);

                if (GossipSDK.Core.Gossip.Instance?.Settings?.EnableDebug == true)
                    UnityEngine.Debug.Log($"[ExperienceInfoTracker] CapExperienceInfo loadMs={loadTimeMs:F1} app='{data.AppVersion}' target='{data.TargetHardware}'");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(new Exception("[ExperienceInfoTracker] CapExperienceInfo failed", ex));
            }
        }
    }
}
