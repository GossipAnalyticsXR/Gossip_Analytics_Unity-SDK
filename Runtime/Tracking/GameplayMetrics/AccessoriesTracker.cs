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
    public class AccessoriesTracker : GenericSocketConnection<AccessoriesTracker.EntityData, AccessoriesTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingPlayerAccessories";

        [Serializable]
        public class AccessoryItem
        {
            [JsonProperty("GeneralAccessoryName")]
            public string AccessoryName { get; set; }

            [JsonProperty("GeneralAccessoryPrice")]
            public string AccessoryPrice { get; set; }

            [JsonProperty("GeneralAccessoryBrand")]
            public string AccessoryBrand { get; set; }

            [JsonProperty("GaneralAccesoryTypePay")]
            public string AccessoryTypePay { get; set; }

            public Dictionary<string, string> Meta { get; set; } = new Dictionary<string, string>();
        }

        [Serializable]
        public class EntityData : Data
        {
            [JsonProperty("PlayerTotalPurchased")]
            public string TotalPurchased { get; set; }

            [JsonProperty("Accessories")]
            public List<AccessoryItem> Accessories { get; set; } = new List<AccessoryItem>();

            [JsonProperty("TimestampUtc")]
            public string TimestampUtc { get; set; }

            [JsonConstructor]
            public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }

        public void CapAccessory(string accessoryName, string accessoryPrice, string accessoryBrand, string accessoryTypePay,string totalPurchased = null, Dictionary<string, string> meta = null)
        {
            try
            {
                var item = new AccessoryItem
                {
                    AccessoryName = accessoryName ?? string.Empty,
                    AccessoryPrice = accessoryPrice ?? string.Empty,
                    AccessoryBrand = accessoryBrand ?? string.Empty,
                    AccessoryTypePay = accessoryTypePay ?? string.Empty,
                    Meta = meta ?? new Dictionary<string, string>()
                };

                var e = new EntityData
                {
                    TotalPurchased = totalPurchased ?? string.Empty,
                    Accessories = new List<AccessoryItem> { item },
                    TimestampUtc = DateTime.UtcNow.ToString("o")
                };

                CapSession(e);

                if (Gossip.Instance?.Settings?.EnableDebug == true)
                    Debug.Log($"[AccessoriesTracker] CapAccessory {item.AccessoryName} price={item.AccessoryPrice} brand={item.AccessoryBrand} totalPurchased={e.TotalPurchased}");
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception("[AccessoriesTracker] CapAccessory failed", ex));
            }
        }

        public void CapAccessoriesSnapshot(List<AccessoryItem> accessories, string totalPurchased = null)
        {
            try
            {
                var e = new EntityData
                {
                    Accessories = accessories ?? new List<AccessoryItem>(),
                    TotalPurchased = totalPurchased ?? string.Empty,
                    TimestampUtc = DateTime.UtcNow.ToString("o")
                };

                CapSession(e);

                if (Gossip.Instance?.Settings?.EnableDebug == true)
                    Debug.Log($"[AccessoriesTracker] CapAccessoriesSnapshot count={e.Accessories.Count} totalPurchased={e.TotalPurchased}");
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception("[AccessoriesTracker] CapAccessoriesSnapshot failed", ex));
            }
        }

        public static AccessoryItem MakeAccessory(string name, string price, string brand, string typePay, Dictionary<string, string> meta = null)
        {
            return new AccessoryItem
            {
                AccessoryName = name ?? string.Empty,
                AccessoryPrice = price ?? string.Empty,
                AccessoryBrand = brand ?? string.Empty,
                AccessoryTypePay = typePay ?? string.Empty,
                Meta = meta ?? new Dictionary<string, string>()
            };
        }
    }
}
