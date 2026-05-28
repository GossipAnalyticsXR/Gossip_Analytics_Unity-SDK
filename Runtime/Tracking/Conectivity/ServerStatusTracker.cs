using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;
using GossipSDK.Core;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;

namespace GossipSDK.Tracking.Conectivity
{
    [Serializable]
    public class ServerStatusTracker : GenericSocketConnection<ServerStatusTracker.EntityData, ServerStatusTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingServerStatus";

        [Serializable]
        public class EntityData : Data
        {
            public string ServerName { get; set; }
            public string Status { get; set; }
            public int? PingMs { get; set; }
            public float? LoadPercent { get; set; }
            public Dictionary<string, string> Meta { get; set; } = new Dictionary<string, string>();
            public string TimestampUtc { get; set; }

            [JsonConstructor] public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }

        public event Action<EntityData> OnStatusUpdated;

        public async UniTask<bool> CheckServerAsync(string serverUrl = null, int timeoutSeconds = 6, int maxAttempts = 2)
        {
            try
            {
                var gossip = Gossip.Instance;
                serverUrl ??= gossip?.Settings?.GetActiveServerUrl();

                if (string.IsNullOrWhiteSpace(serverUrl))
                {
                    UnityEngine.Debug.LogWarning("[ServerStatusTracker] Server URL no especificada.");
                    PublishStatus("unknown", "no-server-url", null, null, new Dictionary<string, string> { { "note", "ServerURL missing" } });
                    return false;
                }

                serverUrl = serverUrl.TrimEnd('/');

                var candidates = new[] { "/health", "/status", "" };
                Exception lastEx = null;
                bool anySuccess = false;

                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    foreach (var path in candidates)
                    {
                        string url = serverUrl + path;

                        try
                        {
                            var sw = new System.Diagnostics.Stopwatch();
                            sw.Start();

                            using var uwr = UnityWebRequest.Get(url);
                            uwr.timeout = timeoutSeconds;

                            try
                            {
                                var header = gossip?.Settings?.ApiKeyHeader;
                                var value = gossip?.Settings?.ApiKeyValue;
                                if (!string.IsNullOrWhiteSpace(header) && !string.IsNullOrWhiteSpace(value))
                                    uwr.SetRequestHeader(header, value);
                            }
                            catch { }

                            var op = uwr.SendWebRequest();
                            while (!op.isDone) await UniTask.Yield();

#if UNITY_2020_1_OR_NEWER
                            bool success = uwr.result == UnityWebRequest.Result.Success;
#else
                            bool success = !uwr.isNetworkError && !uwr.isHttpError;
#endif
                            sw.Stop();
                            int pingMs = (int)sw.ElapsedMilliseconds;

                            if (success)
                            {
                                float? loadPercent = null;
                                string status = "ok";
                                var text = uwr.downloadHandler?.text;
                                if (!string.IsNullOrWhiteSpace(text))
                                {
                                    try
                                    {
                                        var j = JObject.Parse(text);
                                        if (j.TryGetValue("status", StringComparison.OrdinalIgnoreCase, out var s)) status = s.ToString();
                                        if (j.TryGetValue("loadPercent", StringComparison.OrdinalIgnoreCase, out var lp))
                                        {
                                            if (float.TryParse(lp.ToString(), out var lpv)) loadPercent = lpv;
                                        }
                                        else if (j.TryGetValue("load", StringComparison.OrdinalIgnoreCase, out var l2))
                                        {
                                            if (float.TryParse(l2.ToString(), out var l2v)) loadPercent = l2v;
                                        }
                                    }
                                    catch (Exception)
                                    {
                                    }
                                }

                                PublishStatus(url, status, pingMs, loadPercent, new Dictionary<string, string> { { "checkedPath", path } });
                                anySuccess = true;
                                break;
                            }
                            else
                            {
                                lastEx = new Exception($"Non-success HTTP ({uwr.responseCode}) - {uwr.error}");
                                UnityEngine.Debug.LogWarning($"[ServerStatusTracker] HTTP {(int)uwr.responseCode} from {url}: {uwr.error}");
                            }
                        }
                        catch (Exception ex)
                        {
                            lastEx = ex;
                            UnityEngine.Debug.LogWarning($"[ServerStatusTracker] Attempt {attempt} to {url} failed: {ex.Message}");
                        }
                    }

                    if (anySuccess) break;

                    await UniTask.Delay(TimeSpan.FromMilliseconds(300 * attempt));
                }

                if (!anySuccess)
                {
                    PublishStatus(serverUrl, "down", null, null, new Dictionary<string, string> { { "reason", lastEx?.Message ?? "no response" } });
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(new Exception("[ServerStatusTracker] CheckServerAsync failed", ex));
                PublishStatus("error", "error", null, null, new Dictionary<string, string> { { "exception", ex.Message } });
                return false;
            }
        }

        private void PublishStatus(string serverNameOrTag, string status, int? pingMs, float? loadPercent, Dictionary<string, string> meta = null)
        {
            try
            {
                var e = new EntityData
                {
                    ServerName = serverNameOrTag ?? string.Empty,
                    Status = status ?? (pingMs.HasValue ? "ok" : "down"),
                    PingMs = pingMs,
                    LoadPercent = loadPercent,
                    Meta = meta ?? new Dictionary<string, string>(),
                    TimestampUtc = DateTime.UtcNow.ToString("o")
                };

                CapSession(e);

                try { OnStatusUpdated?.Invoke(e); } catch { }

                if (Gossip.Instance?.Settings?.EnableDebug == true)
                {
                    UnityEngine.Debug.Log($"[ServerStatusTracker] Status published: status={e.Status} ping={e.PingMs} load={e.LoadPercent}");
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(new Exception("[ServerStatusTracker] PublishStatus failed", ex));
            }
        }
    }
}
