using System;
using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using SocketIOClient;
using SocketIOClient.Newtonsoft.Json;

namespace GossipSDK.Core.Connection
{
    [Serializable]
    public class SocketConnection : IDisposable
    {
        public bool IsConnected => Socket is { Connected: true };

        private SocketIOUnity Socket { get; set; }

        private void RegisterSentBytes(string json)
        {
            if (Gossip.Instance?.NetworkUsageTracker == null) return;
            int bytes = System.Text.Encoding.UTF8.GetByteCount(json);
            Gossip.Instance.NetworkUsageTracker.RegisterSent(bytes);
        }

        private void RegisterReceivedBytes(string json)
        {
            if (Gossip.Instance?.NetworkUsageTracker == null) return;
            int bytes = System.Text.Encoding.UTF8.GetByteCount(json);
            Gossip.Instance.NetworkUsageTracker.RegisterReceived(bytes);
        }

        public virtual async UniTask<bool> ConnectAsync(string serverURL)
        {
            var uri = new Uri(serverURL);

            Socket = new SocketIOUnity(uri, new SocketIOOptions
            {
                EIO = 4,
                Transport = SocketIOClient.Transport.TransportProtocol.WebSocket,
                ExtraHeaders = new Dictionary<string, string>
                {
                    { "x-api-key", "api-key-company-gossip" }
                }
            });

            Socket.JsonSerializer = new NewtonsoftJsonSerializer();

            Socket.OnConnected += OnConnected;
            Socket.OnDisconnected += OnDisconnected;

            Socket.OnAny((eventName, response) =>
            {
                try
                {
                    if (response != null)
                    {
                        // Representación JSON completa del mensaje recibido
                        string json = response.ToString();
                        RegisterReceivedBytes(json);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SocketConnection] Error tracking received bytes: {ex.Message}");
                }
            });

            await Socket.ConnectAsync();
            return Socket.Connected;
        }

        public void Emit(string eventName, params object[] data)
        {
            try
            {
                // Convert to JSON for byte tracking
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(data);
                RegisterSentBytes(json);
            }
            catch { }

            Socket.Emit(eventName, data);
        }

        public async UniTask EmitStringAsJSONAsync(string eventName, string json)
        {
            RegisterSentBytes(json);
            await Socket.EmitAsync(eventName, json);
        }

        public async UniTask EmitAsync(string eventName, CancellationToken cancellationToken, params object[] data)
        {
            try
            {
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(data);
                RegisterSentBytes(json);
            }
            catch { }

            await Socket.EmitAsync(eventName, cancellationToken, data);
        }

        protected virtual void OnConnected(object sender, EventArgs eventArgs)
        {
            Debug.Log($"{GetType().Name}.OnConnected\nID: {Socket.Id}, Connected: {Socket.Connected}");
        }

        protected virtual void OnDisconnected(object sender, string reason)
        {
            Debug.Log($"{GetType().Name}.OnDisconnected\nID: {Socket.Id}, Connected: {Socket.Connected}");
        }

        public virtual void Dispose()
        {
            Socket.OnConnected -= OnConnected;
            Socket.OnDisconnected -= OnDisconnected;

            Socket.Dispose();
        }
    }
}
