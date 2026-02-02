using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using GossipSDK.Core;

namespace GossipSDK.Tracking.GameplayMetrics
{
    [Serializable]
    public class UserBalanceTracker : GenericSocketConnection<UserBalanceTracker.EntityData, UserBalanceTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingUserBalance";

        [Serializable]
        public class EntityData : Data
        {
            public float CopX { get; set; }
            public float CopY { get; set; }
            public float CopZ { get; set; }

            public float SwayMagnitude { get; set; }
            public float SwayFrequency { get; set; }

            public string PostureState { get; set; }

            public string SceneName { get; set; }
            public string TimestampUtc { get; set; }

            [JsonConstructor] public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }

        public void CaptureSample(Vector3 cop, float swayMagnitude, float swayFrequency, string postureState = null)
        {
            try
            {
                var data = new EntityData
                {
                    CopX = cop.x,
                    CopY = cop.y,
                    CopZ = cop.z,
                    SwayMagnitude = swayMagnitude,
                    SwayFrequency = swayFrequency,
                    PostureState = postureState ?? string.Empty,
                    SceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    TimestampUtc = DateTime.UtcNow.ToString("o")
                };

                CapSession(data);

                if (Gossip.Instance?.Settings?.EnableDebug == true)
                {
                    Debug.Log($"[UserBalanceTracker] Captured COP=({data.CopX:F2},{data.CopY:F2},{data.CopZ:F2}) sway={data.SwayMagnitude:F3}Hz={data.SwayFrequency:F2} state={data.PostureState}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception("[UserBalanceTracker] CaptureSample failed", ex));
            }
        }

        public void CaptureSampleFromTransform(Transform t, float swayMagnitude, float swayFrequency, string postureState = null)
        {
            var pos = t != null ? t.position : Vector3.zero;
            CaptureSample(pos, swayMagnitude, swayFrequency, postureState);
        }

        public int GetPendingCountSafe()
        {
            try { return GetPendingCount(); }
            catch { return 0; }
        }
    }
}
