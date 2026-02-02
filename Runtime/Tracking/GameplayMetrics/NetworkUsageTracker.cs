using System;
using GossipSDK.Core.Connection;
using GossipSDK.Core;
using Newtonsoft.Json;
using Cysharp.Threading.Tasks;

namespace GossipSDK.Tracking.GameplayMetrics
{
    public class NetworkUsageTracker
    {
        [Serializable]
        public struct EntityData
        {
            public long SentBytes;
            public long ReceivedBytes;
            public long TotalBytes;
            public string Timestamp;
        }

        [Serializable]
        public struct TrackerMessage
        {
            public string PlayerID;
            public string SessionID;
            public EntityData Payload;
        }

        private long sentBytes;
        private long receivedBytes;
        private readonly SocketConnection connection;

        public NetworkUsageTracker(SocketConnection socket)
        {
            connection = socket;
            sentBytes = 0;
            receivedBytes = 0;
        }

        public void RegisterSent(int bytes)
        {
            sentBytes += bytes;
        }

        public void RegisterReceived(int bytes)
        {
            receivedBytes += bytes;
        }

        public void SendDataToSocket()
        {
            var data = new EntityData
            {
                SentBytes = sentBytes,
                ReceivedBytes = receivedBytes,
                TotalBytes = sentBytes + receivedBytes,
                Timestamp = DateTime.UtcNow.ToString("o")
            };

            var message = new TrackerMessage
            {
                PlayerID = Gossip.Instance?.PlayerID ?? string.Empty,
                SessionID = Gossip.Instance?.SessionID ?? string.Empty,
                Payload = data
            };

            try
            {
                string json = JsonConvert.SerializeObject(message, Formatting.Indented);
                connection.EmitStringAsJSONAsync("TrackingNetworkUsage", json).Forget();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(new Exception("[NetworkUsageTracker] Failed to send network usage", ex));
            }
        }
    }
}
