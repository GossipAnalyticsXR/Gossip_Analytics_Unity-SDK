using System;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using UnityEngine;

namespace GossipSDK.Tracking.GameplayMetrics
{
    [Serializable]
    public class InputUsageTracker
        : GenericSocketConnection<InputUsageTracker.EntityData, InputUsageTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingInputUsage";

        private float controllerTime;
        private float handTime;

        public void RegisterControllerUsage(float delta)
        {
            controllerTime += delta;
        }

        public void RegisterHandUsage(float delta)
        {
            handTime += delta;
        }

        public void CaptureSnapshot()
        {
            float total = controllerTime + handTime;
            if (total <= 0f) return;

            CapSession(new EntityData
            {
                ControllerUsagePercent = controllerTime / total * 100f,
                HandUsagePercent = handTime / total * 100f,
                SampleDurationSeconds = total,
                PrimaryInput = controllerTime >= handTime ? "controller" : "hand"
            });

            controllerTime = 0f;
            handTime = 0f;
        }

        [Serializable]
        public class EntityData : Data
        {
            [SerializeField] public float ControllerUsagePercent { get; set; }
            [SerializeField] public float HandUsagePercent { get; set; }
            [SerializeField] public float SampleDurationSeconds { get; set; }
            [SerializeField] public string PrimaryInput { get; set; }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }
    }
}
