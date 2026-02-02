using System;
using GossipSDK.Core;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using Newtonsoft.Json;

namespace GossipSDK.Tracking.A11y
{
    [Serializable]
    public class A11yTracker
        : GenericSocketConnection<A11yTracker.EntityData, A11yTracker.TrackerMessage>
    {
        protected override string EventName => "a11y_check";

        [Serializable]
        public class EntityData : Data
        {
            public string metric_key;
            public int numerator;
            public int denominator;
            public bool? complies;
            public string scope;
            public MetaData meta;
            public string timestamp_utc;
        }

        [Serializable]
        public class MetaData
        {
            public string platform;
            public string app_version;
            public string sdk_version;
            public string locale;
            public string screen_id;
            public string scene_id;
            public string build_id;
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }

        public void CapCheck(
            string metricKey,
            int numerator,
            int denominator,
            string scope,
            MetaData meta
        )
        {
            bool? complies = null;

            if (denominator > 0)
                complies = numerator > 0; // threshold se evalúa en backend

            var data = new EntityData
            {
                metric_key = metricKey,
                numerator = numerator,
                denominator = denominator,
                complies = complies,
                scope = scope,
                meta = meta,
                timestamp_utc = DateTime.UtcNow.ToString("o")
            };

            CapSession(data);
        }
    }
}
