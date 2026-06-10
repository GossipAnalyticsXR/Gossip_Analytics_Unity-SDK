using System;
using UnityEngine;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using GossipSDK.Core;
using Newtonsoft.Json;

namespace GossipSDK.Tracking.GameplayMetrics
{
    [Serializable]
    public class HandControllerTracker
        : GenericSocketConnection<
            HandControllerTracker.EntityData,
            HandControllerTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingHandController";

        [Serializable]
        public class EntityData : Data
        {
            public string Hand
            {
                get; set;
            }
            public float Pitch
            {
                get; set;
            }
            public float Yaw
            {
                get; set;
            }
            public float Roll
            {
                get; set;
            }
            public float HandElevation
            {
                get; set;
            }

            public string SceneName
            {
                get; set;
            }
            public string PlayerID
            {
                get; set;
            }
            public string SessionID
            {
                get; set;
            }
            public string TimestampUtc
            {
                get; set;
            }

            [JsonConstructor]
            public EntityData()
            {
            }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData>
        {
        }

        public void CapHandPose(
            string hand,
            Quaternion rotation,
            string sceneName = null)
        {
            var euler = rotation.eulerAngles;

            var data = new EntityData
            {
                Hand = hand,
                Pitch = NormalizeAngle(euler.x),
                Yaw = NormalizeAngle(euler.y),
                Roll = NormalizeAngle(euler.z),

                SceneName = sceneName
                    ?? UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,

                PlayerID = Gossip.Instance?.PlayerID,
                SessionID = Gossip.Instance?.SessionID,
                TimestampUtc = DateTime.UtcNow.ToString("o")
            };

            CapSession(data);
        }

        private static float NormalizeAngle(float angle)
        {
            if (angle > 180f)
                angle -= 360f;
            return angle;
        }
    }
}
