using System;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using Newtonsoft.Json;
using UnityEngine;

namespace GossipSDK.Tracking.GameplayMetrics
{
    [Serializable]
    public class CrashTracker : GenericSocketConnection<CrashTracker.EntityData, CrashTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingCrash";

        [Serializable]
        public class EntityData : Data
        {
            public string ErrorType    { get; set; }
            public string ErrorMessage { get; set; }
            public string StackTrace   { get; set; }
            public string SceneName    { get; set; }
            public string TimestampUtc { get; set; }

            [JsonConstructor] public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }

        public void CapCrash(string errorType, string errorMessage, string stackTrace, string sceneName)
        {
            try
            {
                var e = new EntityData
                {
                    ErrorType    = errorType    ?? string.Empty,
                    ErrorMessage = errorMessage ?? string.Empty,
                    StackTrace   = stackTrace   ?? string.Empty,
                    SceneName    = sceneName    ?? string.Empty,
                    TimestampUtc = DateTime.UtcNow.ToString("o")
                };
                CapSession(e);
                if (GossipSDK.Core.Gossip.Instance?.Settings?.EnableDebug == true)
                    Debug.Log($"[CrashTracker] CapCrash type={errorType} msg={errorMessage}");
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception("[CrashTracker] CapCrash failed", ex));
            }
        }
    }
}
