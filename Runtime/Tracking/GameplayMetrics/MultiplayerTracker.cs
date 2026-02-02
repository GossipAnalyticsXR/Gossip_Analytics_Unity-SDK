using System;
using System.Collections.Generic;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using GossipSDK.Core;
using Newtonsoft.Json;

namespace GossipSDK.Tracking.GameplayMetrics
{
    [Serializable]
    public class MultiplayerTracker : GenericSocketConnection<MultiplayerTracker.EntityData, MultiplayerTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingMultiplayer";

        [Serializable]
        public class PlayerInfo
        {
            public string PlayerId { get; set; }
            public string DisplayName { get; set; }
            public int? PingMs { get; set; }
            public Dictionary<string, string> Meta { get; set; } = new Dictionary<string, string>();
        }

        [Serializable]
        public class EntityData : Data
        {
            public string RoomId { get; set; }
            public string MatchType { get; set; }
            public int PlayerCount { get; set; }
            public List<PlayerInfo> Players { get; set; } = new List<PlayerInfo>();
            public double? AveragePingMs { get; set; }
            public string TimestampUtc { get; set; }

            [JsonConstructor] public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }

        public void CapMatchSnapshot(string roomId, string matchType, List<PlayerInfo> players)
        {
            try
            {
                var e = new EntityData
                {
                    RoomId = roomId ?? "",
                    MatchType = matchType ?? "",
                    Players = players ?? new List<PlayerInfo>(),
                    PlayerCount = players?.Count ?? 0,
                    AveragePingMs = players != null && players.Count > 0 ? (double?)ComputeAveragePing(players) : null,
                    TimestampUtc = DateTime.UtcNow.ToString("o")
                };

                CapSession(e);
                if (Gossip.Instance?.Settings?.EnableDebug == true)
                    UnityEngine.Debug.Log($"[MultiplayerTracker] CapMatchSnapshot room='{roomId}' players={e.PlayerCount} avgPing={e.AveragePingMs}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(new Exception("[MultiplayerTracker] CapMatchSnapshot failed", ex));
            }
        }

        private double ComputeAveragePing(List<PlayerInfo> players)
        {
            double sum = 0;
            int count = 0;
            foreach (var p in players)
            {
                if (p?.PingMs != null)
                {
                    sum += p.PingMs.Value;
                    count++;
                }
            }
            return count == 0 ? 0 : sum / count;
        }
    }
}