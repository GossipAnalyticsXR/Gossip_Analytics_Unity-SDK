using System;
using System.Collections.Generic;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using Newtonsoft.Json;

namespace GossipSDK.Tracking.GameplayMetrics
{
    [Serializable]
    public class DistanceTracker : GenericSocketConnection<DistanceTracker.EntityData, DistanceTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingDistance";

        [Serializable]
        public class EntityData : Data
        {
            public float DistanceMeters { get; set; }

            public float ObjPosX { get; set; }
            public float ObjPosY { get; set; }
            public float ObjPosZ { get; set; }

            public float PlayerPosX { get; set; }
            public float PlayerPosY { get; set; }
            public float PlayerPosZ { get; set; }
            public string SceneName { get; set; }
            
            public string TimestampUtc { get; set; }

            [JsonConstructor]
            public EntityData()
            {
            }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }

        public void RecordDistance (float meters, UnityEngine.Vector3 objectPosition, UnityEngine.Vector3 playerPosition)
        {
            try
            {
                var data = new EntityData
                {
                    DistanceMeters = meters,

                    ObjPosX = objectPosition.x,
                    ObjPosY = objectPosition.y,
                    ObjPosZ = objectPosition.z,

                    PlayerPosX = playerPosition.x,
                    PlayerPosY = playerPosition.y,
                    PlayerPosZ = playerPosition.z,

                    SceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    TimestampUtc = DateTime.UtcNow.ToString("o")
                };

                CapSession(data);

                if (GossipSDK.Core.Gossip.Instance?.Settings?.EnableDebug == true)
                {
                    UnityEngine.Debug.Log(
                        $"[DistanceTracker] Player↔Object {meters:F2}m " +
                        $"Obj({data.ObjPosX:F1},{data.ObjPosY:F1},{data.ObjPosZ:F1})"
                    );
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(new Exception("[DistanceTracker] RecordDistance failed", ex));
            }
        }
    }
}
