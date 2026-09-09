using System;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using Newtonsoft.Json;

namespace GossipSDK.Tracking.PlatformSpecification
{
    [Serializable]
    public class MicPermissionTracker
        : GenericSocketConnection<MicPermissionTracker.EntityData, MicPermissionTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingMicPermission";

        [Serializable]
        public class EntityData : Data
        {
            public string PlayerID { get; set; }
            public string SessionID { get; set; }
            public bool MicDenied { get; set; }

            /// <summary>
            /// Por que el tracker de audio no esta midiendo, cuando no lo esta:
            /// ok, xr_inactive, permissions_timeout, mic_permission_denied,
            /// no_microphone, microphone_start_failed, no_samples.
            ///
            /// MicDenied solo cubria una de esas siete. Las demas dejaban el mismo
            /// rastro que 'no hubo reacciones' -- cero -- y hacia falta un cable USB
            /// y logcat para distinguirlas. Null en los envios que no lo traen.
            /// </summary>
            public string AudioTrackerStatus { get; set; }
            public string TimestampUtc { get; set; }

            [JsonConstructor]
            public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }
    }
}
