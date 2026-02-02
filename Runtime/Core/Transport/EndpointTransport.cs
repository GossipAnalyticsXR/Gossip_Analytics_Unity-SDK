using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace GossipSDK.Core.Transport
{
    public class EndpointTransport : ITransport, IDisposable
    {
        private readonly string apiKeyHeader;
        private readonly string apiKeyValue;
        private readonly int maxRetries;
        private readonly int timeoutSeconds;

        public EndpointTransport(string apiKeyHeader = "x-api-key", string apiKeyValue = "api-key-company-gossip", int maxRetries = 2, int timeoutSeconds = 10)
        {
            this.apiKeyHeader = apiKeyHeader;
            this.apiKeyValue = apiKeyValue;
            this.maxRetries = Math.Max(0, maxRetries);
            this.timeoutSeconds = Math.Max(1, timeoutSeconds);
        }

        public async UniTask<bool> PostJsonAsync(string url, string json, string apiKeyHeader = null, string apiKeyValue = null)
        {
            return await SendInternal(url, json, apiKeyHeader, apiKeyValue);
        }

        public async UniTask<bool> SendJsonEventAsync(string url, string json, string apiKeyHeader = null, string apiKeyValue = null)
        {
            return await SendInternal(url, json, apiKeyHeader, apiKeyValue);
        }

        private async UniTask<bool> SendInternal(string url, string json, string headerOverride, string valueOverride)
        {
            if (GossipSDK.Core.Gossip.Instance != null && !GossipSDK.Core.Gossip.Instance.ApiKeyValid)
            {
                if (GossipSDK.Core.Gossip.Instance.Settings?.EnableDebug == true)
                    Debug.LogWarning("[EndpointTransport] ApiKey invalid. HTTP send blocked.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(url) || !Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                Debug.LogWarning($"[EndpointTransport] Invalid URL: '{url}'");
                return false;
            }

            string header = headerOverride ?? apiKeyHeader;
            string value = valueOverride ?? apiKeyValue;

            int attempt = 0;
            var rnd = new System.Random();
            const int baseDelayMs = 500;

            while (attempt <= maxRetries)
            {
                attempt++;

                using (var uwr = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
                {
                    byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json ?? "");
                    uwr.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    uwr.downloadHandler = new DownloadHandlerBuffer();
                    uwr.SetRequestHeader("Content-Type", "application/json");

                    if (!string.IsNullOrEmpty(header) && !string.IsNullOrEmpty(value))
                        uwr.SetRequestHeader(header, value);

                    uwr.timeout = timeoutSeconds;

                    try
                    {
                        string payloadPreview = string.Empty;
                        if (!string.IsNullOrEmpty(json))
                        {
                            payloadPreview = json.Length > 2000 ? json.Substring(0, 2000) + "... (truncated)" : json;
                        }

                        if (GossipSDK.Core.Gossip.Instance?.Settings?.EnableDebug == true)
                        {
                            Debug.Log($"[EndpointTransport] POST attempt={attempt}/{maxRetries + 1} -> {url}");
                            Debug.Log($"[EndpointTransport] Headers: {(string.IsNullOrEmpty(header) ? "<none>" : header)}={(string.IsNullOrEmpty(value) ? "<none>" : "*****")}");
                            if (!string.IsNullOrEmpty(payloadPreview))
                                Debug.Log($"[EndpointTransport] Payload: {payloadPreview}");
                        }

                        var op = uwr.SendWebRequest();
                        while (!op.isDone) await UniTask.Yield();

#if UNITY_2020_1_OR_NEWER
                        bool success = uwr.result == UnityWebRequest.Result.Success;
#else
                bool success = !uwr.isNetworkError && !uwr.isHttpError;
#endif
                        long code = uwr.responseCode;
                        if (code == 401 || code == 403)
                        {
                            GossipSDK.Core.Gossip.Instance?.InvalidateApiKey(code);
                            return false;
                        }
                        string responseText = null;
                        try { responseText = uwr.downloadHandler?.text; } catch { responseText = null; }

                        if (success)
                        {
                            if (GossipSDK.Core.Gossip.Instance?.Settings?.EnableDebug == true)
                            {
                                Debug.Log($"[EndpointTransport] POST success url={url} attempt={attempt} code={(int)code}");
                                if (!string.IsNullOrEmpty(responseText))
                                {
                                    string respPreview = responseText.Length > 2000 ? responseText.Substring(0, 2000) + "..." : responseText;
                                    Debug.Log($"[EndpointTransport] Response (truncated): {respPreview}");
                                }
                            }
                            return true;
                        }
                        else
                        {
                            if (GossipSDK.Core.Gossip.Instance?.Settings?.EnableDebug == true)
                            {
                                string respPreview = !string.IsNullOrEmpty(responseText)
                                    ? (responseText.Length > 2000 ? responseText.Substring(0, 2000) + "..." : responseText)
                                    : "<no body>";
                                Debug.LogWarning($"[EndpointTransport] POST failed url={url} attempt={attempt} code={(int)code} err={uwr.error}");
                                Debug.LogWarning($"[EndpointTransport] Response body (truncated): {respPreview}");
                            }
                        }
                    }
                    catch (UriFormatException ufx)
                    {
                        Debug.LogException(new Exception($"[EndpointTransport] UriFormatException for '{url}': {ufx.Message}", ufx));
                        return false;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[EndpointTransport] Exception POST attempt={attempt} url={url} : {ex.GetType().Name} {ex.Message}");
                        if (GossipSDK.Core.Gossip.Instance?.Settings?.EnableDebug == true)
                            Debug.LogException(ex);
                    }
                }

                if (attempt <= maxRetries)
                {
                    int backoff = baseDelayMs * (int)Math.Pow(2, attempt - 1);
                    int jitter = rnd.Next(0, Math.Min(500, backoff / 2));
                    int waitMs = backoff + jitter;
                    if (GossipSDK.Core.Gossip.Instance?.Settings?.EnableDebug == true)
                        Debug.Log($"[EndpointTransport] Waiting {waitMs}ms before next attempt ({attempt + 1}/{maxRetries + 1})");
                    await UniTask.Delay(waitMs);
                }
            }

            if (GossipSDK.Core.Gossip.Instance?.Settings?.EnableDebug == true)
                Debug.LogWarning($"[EndpointTransport] All attempts failed for {url}");
            return false;
        }

        public void Dispose()
        {
        }
    }
}