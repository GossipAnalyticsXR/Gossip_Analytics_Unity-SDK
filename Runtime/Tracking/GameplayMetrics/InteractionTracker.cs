using System;
using System.Collections.Generic;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using Newtonsoft.Json;
using UnityEngine;

namespace GossipSDK.Tracking.GameplayMetrics
{
    [Serializable]
    public class InteractionTracker : GenericSocketConnection<InteractionTracker.EntityData, InteractionTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingInteraction";

        [Serializable]
        public class EntityData : Data
        {
            [field: SerializeField] public string InteractionId { get; set; }
            [field: SerializeField] public int SequenceIndex { get; set; }
            [field: SerializeField] public string Action { get; set; }
            [field: SerializeField] public string ObjectName { get; set; }
            [field: SerializeField] public string ObjectTag { get; set; }
            [field: SerializeField] public string InteractionType { get; set; }
            [field: SerializeField] public string InputType { get; set; }
            [field: SerializeField] public float X { get; set; }
            [field: SerializeField] public float Y { get; set; }
            [field: SerializeField] public float Z { get; set; }
            [field: SerializeField] public double? DurationSeconds { get; set; }
            [field: SerializeField] public string StartTimestampUtc { get; set; }
            [field: SerializeField] public string EndTimestampUtc { get; set; }
            [field: SerializeField] public string SceneName { get; set; }
            [field: SerializeField] public string TimestampUtc { get; set; }

            [JsonConstructor]
            public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }

        private static int globalSequence = 0;
        private static readonly object seqLock = new object();

        private readonly Dictionary<string, int> activeSequence = new Dictionary<string, int>();
        private readonly object activeLock = new object();

        private int AllocateSequenceFor(string interactionId)
        {
            lock (activeLock)
            {
                if (activeSequence.TryGetValue(interactionId, out int existing))
                    return existing;

                int seq;
                lock (seqLock) { seq = ++globalSequence; }
                activeSequence[interactionId] = seq;
                return seq;
            }
        }

        private int TryGetSequence(string interactionId)
        {
            lock (activeLock)
            {
                if (activeSequence.TryGetValue(interactionId, out int seq))
                    return seq;
                return 0;
            }
        }

        private void RemoveSequence(string interactionId)
        {
            lock (activeLock)
            {
                if (activeSequence.ContainsKey(interactionId))
                    activeSequence.Remove(interactionId);
            }
        }

        public void CapInteractionStart(string interactionId, string objectName, string objectTag, string inputType, string interactionType, Vector3 worldPos, string sceneName, string timestampUtc)
        {
            try
            {
                int seq = AllocateSequenceFor(interactionId);

                var data = new EntityData
                {
                    InteractionId = interactionId,
                    SequenceIndex = seq,
                    Action = "start",
                    ObjectName = objectName ?? string.Empty,
                    ObjectTag = objectTag ?? string.Empty,
                    InputType = inputType ?? string.Empty,
                    InteractionType = interactionType ?? string.Empty,
                    X = worldPos.x,
                    Y = worldPos.y,
                    Z = worldPos.z,
                    DurationSeconds = null,
                    StartTimestampUtc = timestampUtc,
                    EndTimestampUtc = null,
                    SceneName = sceneName ?? UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    TimestampUtc = DateTime.UtcNow.ToString("o")
                };

                CapSession(data);
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception("[InteractionTracker] CapInteractionStart failed", ex));
            }
        }

        public void CapInteractionEnd(string interactionId, string objectName, string objectTag, string inputType, string interactionType, Vector3 worldPos, string sceneName, double startRealtime, double endRealtime, double durationSeconds, string endTimestampUtc)
        {
            try
            {
                int seq = TryGetSequence(interactionId);
                if (seq == 0)
                {
                    seq = AllocateSequenceFor(interactionId);
                }

                var data = new EntityData
                {
                    InteractionId = interactionId,
                    SequenceIndex = seq,
                    Action = "end",
                    ObjectName = objectName ?? string.Empty,
                    ObjectTag = objectTag ?? string.Empty,
                    InputType = inputType ?? string.Empty,
                    InteractionType = interactionType ?? string.Empty,
                    X = worldPos.x,
                    Y = worldPos.y,
                    Z = worldPos.z,
                    DurationSeconds = durationSeconds,
                    StartTimestampUtc = DateTime.UtcNow.AddSeconds(-durationSeconds).ToString("o"),
                    EndTimestampUtc = endTimestampUtc ?? DateTime.UtcNow.ToString("o"),
                    SceneName = sceneName ?? UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    TimestampUtc = DateTime.UtcNow.ToString("o")
                };

                RemoveSequence(interactionId);

                CapSession(data);
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception("[InteractionTracker] CapInteractionEnd failed", ex));
            }
        }

        public void CapInteractionCancelled(string objectName, string objectTag, string interactionType, string inputType, float x, float y, float z, string sceneName, string interactionId)
        {
            try
            {
                int seq = TryGetSequence(interactionId);
                if (seq == 0)
                {
                    seq = AllocateSequenceFor(interactionId);
                }

                var data = new EntityData
                {
                    InteractionId = interactionId,
                    SequenceIndex = seq,
                    Action = "cancelled",
                    ObjectName = objectName ?? string.Empty,
                    ObjectTag = objectTag ?? string.Empty,
                    InputType = inputType ?? string.Empty,
                    InteractionType = interactionType ?? string.Empty,
                    X = x,
                    Y = y,
                    Z = z,
                    DurationSeconds = null,
                    StartTimestampUtc = null,
                    EndTimestampUtc = DateTime.UtcNow.ToString("o"),
                    SceneName = sceneName ?? UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    TimestampUtc = DateTime.UtcNow.ToString("o")
                };

                RemoveSequence(interactionId);

                CapSession(data);
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception("[InteractionTracker] CapInteractionCancelled failed", ex));
            }
        }

        public void CapInteractionInstant(string objectName, string objectTag, string inputType, string interactionType, Vector3 worldPos, string sceneName, string timestampUtc)
        {
            try
            {
                var interactionId = Guid.NewGuid().ToString();
                int seq = AllocateSequenceFor(interactionId);

                var data = new EntityData
                {
                    InteractionId = interactionId,
                    SequenceIndex = seq,
                    Action = "instant",
                    ObjectName = objectName ?? string.Empty,
                    ObjectTag = objectTag ?? string.Empty,
                    InputType = inputType ?? string.Empty,
                    InteractionType = interactionType ?? string.Empty,
                    X = worldPos.x,
                    Y = worldPos.y,
                    Z = worldPos.z,
                    DurationSeconds = 0.0,
                    StartTimestampUtc = timestampUtc,
                    EndTimestampUtc = timestampUtc,
                    SceneName = sceneName ?? UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    TimestampUtc = DateTime.UtcNow.ToString("o")
                };

                RemoveSequence(interactionId);

                CapSession(data);
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception("[InteractionTracker] CapInteractionInstant failed", ex));
            }
        }
    }
}
