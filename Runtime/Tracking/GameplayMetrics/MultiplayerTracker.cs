using System;
using System.Collections.Generic;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using GossipSDK.Core;
using Newtonsoft.Json;

namespace GossipSDK.Tracking.GameplayMetrics
{
    /// <summary>
    /// Procedencia de PlayerCount. Va en cada snapshot para que el dato pueda decir si
    /// esta medido o si nadie lo dijo.
    ///
    /// Se usan constantes de texto y no un enum porque el valor viaja en JSON al backend
    /// y se guarda como string: un enum obligaria a mantener el mapeo en los dos lados.
    /// </summary>
    public static class PlayerCountSources
    {
        /// <summary>El integrador nos dio la lista de jugadores.</summary>
        public const string Integrator = "integrator";

        /// <summary>El SDK lo leyo de la capa de red presente en el proyecto.</summary>
        public const string NetworkAuto = "network-auto";

        /// <summary>Nadie nos lo dijo y no hay capa de red reconocible. NO es lo mismo que 1.</summary>
        public const string Unknown = "unknown";
    }

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

            /// <summary>
            /// De donde sale PlayerCount. Sin esto un 1 puede ser "hubo un jugador" o
            /// "nadie nos lo dijo", que son cosas distintas y hasta ahora se guardaban igual.
            /// Ver PlayerCountSources.
            /// </summary>
            public string CountSource { get; set; } = PlayerCountSources.Unknown;
            public string TimestampUtc { get; set; }

            [JsonConstructor] public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }

        /// <summary>
        /// Firma historica. Se conserva para no romper a nadie: una llamada directa con una
        /// lista propia significa que el integrador SI nos dijo los jugadores.
        /// </summary>
        public void CapMatchSnapshot(string roomId, string matchType, List<PlayerInfo> players)
        {
            CapMatchSnapshot(roomId, matchType, players, PlayerCountSources.Integrator);
        }

        public void CapMatchSnapshot(string roomId, string matchType, List<PlayerInfo> players, string countSource)
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
                    CountSource = string.IsNullOrEmpty(countSource) ? PlayerCountSources.Unknown : countSource,
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
