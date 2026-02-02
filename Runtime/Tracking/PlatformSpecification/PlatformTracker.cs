using System;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using Newtonsoft.Json;
using UnityEngine;

namespace GossipSDK.Tracking.PlatformSpecification
{
    [Serializable]
    public class PlatformTracker : GenericSocketConnection<PlatformTracker.EntityData, PlatformTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingDataHardwareAndSoftware";

        [Serializable]
        public class EntityData : Data
        {
            [field: SerializeField] public string Version { get; set; }
            [field: SerializeField] public string ConnectionSpeed { get; set; }
            [field: SerializeField] public bool RequiresWifi { get; set; }
            [field: SerializeField] public bool RequiresMobileData { get; set; }
            [field: SerializeField] public bool GeneralSound { get; set; }
            [field: SerializeField] public bool ControllersLatency { get; set; }
            [field: SerializeField] public bool AmountDevicesInGame { get; set; }
            [field: SerializeField] public bool HandStatus { get; set; }
            [field: SerializeField] public bool ControllerStatus { get; set; }
            [field: SerializeField] public string Model { get; set; }
            [field: SerializeField] public string Device { get; set; }
            [field: SerializeField] public string Resolution { get; set; }
            [field: SerializeField] public string PlatformName { get; set; }
            [JsonConstructor] public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData>
        { }
    }
}
