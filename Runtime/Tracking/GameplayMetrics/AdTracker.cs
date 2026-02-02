using System;
using System.Collections.Generic;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using GossipSDK.Core;
using Newtonsoft.Json;
using UnityEngine;

namespace GossipSDK.Tracking.GameplayMetrics
{
    [Serializable]
    public class AdTracker : GenericSocketConnection<AdTracker.EntityData, AdTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingAdEvent";

        private readonly Dictionary<string, double> adStartTimes = new Dictionary<string, double>();

        [Serializable]
        public class EntityData : Data
        {
            public string EventType { get; set; }
            public string TypePay { get; set; }
            public string AdId { get; set; }
            public string AdNetwork { get; set; }
            public string PlacementId { get; set; }

            public string InteractionType { get; set; }

            public bool? RewardGranted { get; set; }
            public string RewardType { get; set; }
            public double? RewardAmount { get; set; }

            public double? DurationSeconds { get; set; }

            public int? ImpressionCount { get; set; }
            public int? InteractionCount { get; set; }

            public string SceneName { get; set; }
            public string TimestampUtc { get; set; }

            [JsonConstructor]
            public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }

        public void CapImpression(string adId, string typePay,string adNetwork = null, string placementId = null, int? impressionCount = null)
        {
            try
            {
                var e = new EntityData
                {
                    EventType = "impression",
                    TypePay = typePay,
                    AdId = adId ?? string.Empty,
                    AdNetwork = adNetwork ?? string.Empty,
                    PlacementId = placementId ?? string.Empty,
                    ImpressionCount = impressionCount,
                    SceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    TimestampUtc = DateTime.UtcNow.ToString("o")
                };

                CapSession(e);

                if (Gossip.Instance?.Settings?.EnableDebug == true)
                    Debug.Log($"[AdTracker] Impression adId={adId} network={adNetwork} placement={placementId}");
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception("[AdTracker] CapImpression failed", ex));
            }
        }

        public void CapInteraction(string adId, string typePay, string interactionType, string adNetwork = null, string placementId = null, int? interactionCount = null)
        {
            try
            {
                var e = new EntityData
                {
                    EventType = "interaction",
                    TypePay = typePay,
                    AdId = adId ?? string.Empty,
                    AdNetwork = adNetwork ?? string.Empty,
                    PlacementId = placementId ?? string.Empty,
                    InteractionType = interactionType ?? string.Empty,
                    InteractionCount = interactionCount,
                    SceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    TimestampUtc = DateTime.UtcNow.ToString("o")
                };

                CapSession(e);

                if (Gossip.Instance?.Settings?.EnableDebug == true)
                    Debug.Log($"[AdTracker] Interaction adId={adId} type={interactionType}");
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception("[AdTracker] CapInteraction failed", ex));
            }
        }

        public void CapReward(string adId, string typePay, bool granted, string rewardType = null, double? rewardAmount = null, string adNetwork = null, string placementId = null)
        {
            try
            {
                var e = new EntityData
                {
                    EventType = "reward",
                    TypePay = typePay,
                    AdId = adId ?? string.Empty,
                    AdNetwork = adNetwork ?? string.Empty,
                    PlacementId = placementId ?? string.Empty,
                    RewardGranted = granted,
                    RewardType = rewardType ?? string.Empty,
                    RewardAmount = rewardAmount,
                    SceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    TimestampUtc = DateTime.UtcNow.ToString("o")
                };

                CapSession(e);

                if (Gossip.Instance?.Settings?.EnableDebug == true)
                    Debug.Log($"[AdTracker] Reward adId={adId} granted={granted} type={rewardType} amt={rewardAmount}");
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception("[AdTracker] CapReward failed", ex));
            }
        }

        public void StartAdSession(string adId, string adNetwork = null, string placementId = null)
        {
            try
            {
                if (string.IsNullOrEmpty(adId)) adId = Guid.NewGuid().ToString();

                adStartTimes[adId] = Time.realtimeSinceStartupAsDouble;

                var e = new EntityData
                {
                    EventType = "ad_start",
                    AdId = adId,
                    AdNetwork = adNetwork ?? string.Empty,
                    PlacementId = placementId ?? string.Empty,
                    TimestampUtc = DateTime.UtcNow.ToString("o"),
                    SceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                };

                CapSession(e);

                if (Gossip.Instance?.Settings?.EnableDebug == true)
                    Debug.Log($"[AdTracker] Ad start adId={adId} network={adNetwork}");
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception("[AdTracker] StartAdSession failed", ex));
            }
        }

        public void EndAdSession(string adId, string adNetwork = null, string placementId = null)
        {
            try
            {
                double? duration = null;
                if (!string.IsNullOrEmpty(adId) && adStartTimes.TryGetValue(adId, out double start))
                {
                    duration = Math.Max(0.0, Time.realtimeSinceStartupAsDouble - start);
                    adStartTimes.Remove(adId);
                }

                var e = new EntityData
                {
                    EventType = "ad_end",
                    AdId = adId ?? string.Empty,
                    AdNetwork = adNetwork ?? string.Empty,
                    PlacementId = placementId ?? string.Empty,
                    DurationSeconds = duration,
                    TimestampUtc = DateTime.UtcNow.ToString("o"),
                    SceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                };


                CapSession(e);

                if (Gossip.Instance?.Settings?.EnableDebug == true)
                    Debug.Log($"[AdTracker] Ad end adId={adId} duration={duration?.ToString("F3") ?? "null"}");
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception("[AdTracker] EndAdSession failed", ex));
            }
        }
    }
}
