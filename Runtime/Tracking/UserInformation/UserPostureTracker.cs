using System;
using System.Collections.Generic;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using Newtonsoft.Json;
using GossipSDK.Core;
using UnityEngine;

namespace GossipSDK.Tracking.GameplayMetrics
{
    [Serializable]
    public class UserPostureTracker : GenericSocketConnection<UserPostureTracker.EntityData, UserPostureTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingUserPosture";
        public string EventTypeForEndpoint => "TRACKING_DATA_USER_POSTURE";

        [Serializable]
        public class EntityData : Data
        {
            public string PostureState { get; set; }
            public float HeadY { get; set; }
            public float HeadX { get; set; }
            public float HeadZ { get; set; }
            public string SceneName { get; set; }
            public string TimestampUtc { get; set; }

            [JsonConstructor]
            public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }

        public void CapturePosture(string state, Vector3 headPosition)
        {
            try
            {
                var data = new EntityData
                {
                    PostureState = state ?? "Unknown",
                    HeadX = headPosition.x,
                    HeadY = headPosition.y,
                    HeadZ = headPosition.z,
                    SceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    TimestampUtc = DateTime.UtcNow.ToString("o")
                };

                CapSession(data);

                if (Gossip.Instance?.Settings?.EnableDebug == true)
                    Debug.Log($"[UserPostureTracker] Captured posture {data.PostureState} headY={data.HeadY:F2}");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        public void CapturePostureFromHeadTransform(string postureState, Transform headTransform)
        {
            if (headTransform == null)
            {
                CapturePosture(postureState, Vector3.zero);
            }
            else
            {
                CapturePosture(postureState, headTransform.position);
            }
        }

        public async void CaptureAndSendNow(string state, Vector3 headPosition)
        {
            CapturePosture(state, headPosition);
            var serverURL = Gossip.Instance?.Settings?.ServerURL;
            if (!string.IsNullOrWhiteSpace(serverURL))
            {
                await SendDataToSocketAsync(serverURL);
            }
        }
    }
}
