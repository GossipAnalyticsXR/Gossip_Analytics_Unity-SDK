using System;
using System.Collections.Generic;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using Newtonsoft.Json;

namespace GossipSDK.Tracking.GameplayMetrics
{
    [Serializable]
    public class AvatarTracker : GenericSocketConnection<AvatarTracker.EntityData, AvatarTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingAvatar";

        [Serializable]
        public class EntityData : Data
        {
            public string AvatarId { get; set; }
            public string TypePay { get; set; }
            public string AvatarName { get; set; }
            public string Variant { get; set; }
            public string Brand { get; set; }
            public string Price { get; set; }
            public string ColorHex { get; set; }
            public Dictionary<string, string> Meta { get; set; } = new Dictionary<string, string>();
            public string TimestampUtc { get; set; }

            [JsonConstructor] public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }

        public void CapAvatar(string avatarId, string typePay, string avatarName, string variant = null, string brand = null, string price = null, string colorHex = null, Dictionary<string, string> meta = null)
        {
            var e = new EntityData
            {
                AvatarId = avatarId ?? "",
                TypePay = typePay ?? "",
                AvatarName = avatarName ?? "",
                Variant = variant ?? "",
                ColorHex = colorHex ?? "",
                Brand = brand ?? "",
                Price = price ?? "",
                Meta = meta ?? new Dictionary<string, string>(),
                TimestampUtc = DateTime.UtcNow.ToString("o")
            };

            CapSession(e);
        }
    }
}
