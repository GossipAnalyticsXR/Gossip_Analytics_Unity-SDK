using LiteDB;
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using Cysharp.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using Debug = UnityEngine.Debug;

namespace GossipSDK.Core.Connection
{
    [Serializable]
    public abstract class GenericSocketConnection<T1, T2> : SocketConnection where T1 : IData where T2 : Message<T1>
    {
        protected virtual string EventName { get; } = "";

        private readonly object dbLock = new object();
        private string localDBPath;
        protected string LocalDBPath => localDBPath;
        protected string DataBaseName => GetType().Name;

        [field: SerializeField]
        public T2 Data { get; private set; } = Activator.CreateInstance<T2>();

        private readonly bool usePerProcessDbInEditor = true;
        private bool initialized;

        public void Initialize()
        {
            if (initialized)
                return;
            initialized = true;

            string folder = Path.Combine(Application.persistentDataPath, "Gossip");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            if (Application.isEditor && usePerProcessDbInEditor)
            {
                int pid = Process.GetCurrentProcess().Id;
                localDBPath = Path.Combine(folder, $"Gossip_{pid}.db");
            }
            else
            {
                localDBPath = Path.Combine(folder, "Gossip.db");
            }

            try
            {
                string oldPath = Path.Combine(Application.dataPath, "Gossip", "Gossip.db");
                if (File.Exists(oldPath) && !File.Exists(localDBPath))
                {
                    File.Move(oldPath, localDBPath);
                    Debug.Log($"[GenericSocketConnection] Migrated DB from {oldPath} to {localDBPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GenericSocketConnection] DB migration failed: {ex.Message}");
            }
        }


        private LiteDatabase CreateLiteDatabaseWithRetry(int maxAttempts = 4, int baseDelayMs = 100)
        {
            if (string.IsNullOrEmpty(localDBPath))
                Initialize();

            string connectionString = $"Filename={LocalDBPath};Mode=Shared";

            Exception lastEx = null;
            var rnd = new System.Random();

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    return new LiteDatabase(connectionString);
                }
                catch (IOException ex)
                {
                    lastEx = ex;
                    int backoff = baseDelayMs * (int)Math.Pow(2, attempt - 1);
                    int jitter = rnd.Next(0, Math.Min(500, backoff / 2));
                    int wait = backoff + jitter;
                    if (Application.isEditor || (Gossip.Instance?.Settings?.EnableDebug == true))
                        Debug.LogWarning($"[GenericSocketConnection] LiteDB open attempt {attempt} failed (will retry {wait}ms): {ex.Message}");
                    Thread.Sleep(wait);
                }
                catch (Exception ex)
                {
                    Debug.LogException(new Exception($"[GenericSocketConnection] Unexpected error opening LiteDB: {ex.Message}", ex));
                    throw;
                }
            }

            throw new Exception($"Could not open LiteDB after {maxAttempts} attempts", lastEx);
        }

        public void CapSession(T1 entityData)
        {
            if (string.IsNullOrEmpty(localDBPath))
                Initialize();

            try
            {
                lock (dbLock)
                {
                    using var db = CreateLiteDatabaseWithRetry();
                    ILiteCollection<T1> col = db.GetCollection<T1>(DataBaseName);
                    col.Insert(entityData);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception($"CapSession failed for {DataBaseName}: {ex.Message}", ex));
            }
        }

        public void SendDataToSocket()
        {
            var serverURL = Gossip.Instance?.Settings?.GetActiveServerUrl();
            if (string.IsNullOrWhiteSpace(serverURL))
            {
                Debug.LogError("[GenericSocketConnection] ServerURL is null or empty. Cannot send.");
                return;
            }

            SendDataToSocketAsync(serverURL).Forget();
        }

        public async UniTask<bool> SendDataToSocketAsync(string serverURL)
        {
            if (string.IsNullOrEmpty(localDBPath))
                Initialize();

            var gossip = Gossip.Instance;

            if (gossip == null || !gossip.IsSessionReady)
            {
                Debug.LogWarning("[GenericSocketConnection] Session not ready. Skipping send.");
                return false;
            }

            if (gossip != null && !gossip.ApiKeyValid)
            {
                if (gossip.Settings?.EnableDebug == true)
                    Debug.LogWarning($"[GenericSocketConnection] API key invalid. Send blocked for {EventName}.");
                return false;
            }


            if (string.IsNullOrWhiteSpace(EventName))
            {
                Debug.LogError("No EventName to emit");
                return false;
            }

            await UniTask.SwitchToMainThread();

            bool useHttp = gossip?.Settings?.UseHttpEndpoint == true;

            if (gossip != null && !gossip.ApiKeyValid)
            {
                if (gossip.Settings?.EnableDebug == true)
                    Debug.LogWarning($"[GenericSocketConnection] API key invalid. Skipping send for {EventName}.");
                return false;
            }

            if (useHttp)
            {
                var apiKey = gossip?.Settings?.ApiKeyValue;
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    if (gossip?.Settings?.EnableDebug == true)
                        Debug.LogWarning($"[GenericSocketConnection] HTTP endpoint is enabled but ApiKeyValue is empty. Skipping send for {EventName}.");
                    return false;
                }
            }

            if (!useHttp)
            {
                if (!IsConnected)
                {
                    var apiKey = gossip?.Settings?.ApiKeyValue;
                    if (string.IsNullOrWhiteSpace(apiKey))
                    {
                        if (gossip?.Settings?.EnableDebug == true)
                            Debug.LogWarning($"[GenericSocketConnection] HTTP endpoint is enabled but ApiKeyValue is empty. Skipping send for {EventName}.");
                        return false;
                    }

                    var connected = await ConnectAsync(serverURL);
                    if (!connected)
                    {
                        Debug.LogWarning($"[GenericSocketConnection] Could not connect to {serverURL}");
                        return false;
                    }
                }
            }

            try
            {
                List<T1> snapshot;
                lock (dbLock)
                {
                    using var db = CreateLiteDatabaseWithRetry();
                    ILiteCollection<T1> col = db.GetCollection<T1>(DataBaseName);
                    snapshot = new List<T1>(col.FindAll());
                }

                Data.Messages = snapshot;

                if (gossip != null)
                {
                    Data.PlayerID = gossip.CurrentPlayerId;
                    Data.SessionID = gossip.CurrentSessionId;

                    Data.EventType = EventName;
                    Data.Engine = Constants.Engine;
                    Data.SceneUser = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                    Data.TimeMovement = DateTime.UtcNow.ToString("o");
                }


                if (string.IsNullOrEmpty(Data.PlayerID))
                {
                    Debug.LogError("[GenericSocketConnection] PlayerID is missing. Aborting send.");
                    return false;
                }

                Debug.Log($"[GenericSocketConnection] Preparing send {EventName} - PlayerID={Data.PlayerID}, SessionID={Data.SessionID}, Items={Data.Messages?.Count}");

                if (useHttp)
                {
                    string json = JsonConvert.SerializeObject(Data, Formatting.Indented);

                    string ingestPath = gossip.Settings.IngestPath ?? "/ingest";
                    string endpoint = serverURL?.TrimEnd('/') + (ingestPath.StartsWith("/") ? ingestPath : "/" + ingestPath);

                    bool postSuccess = false;
                    Exception lastEx = null;

                    try
                    {
                        try
                        {
                            var nu = Gossip.Instance?.NetworkUsageTracker;
                            if (nu != null)
                            {
                                int bytes = System.Text.Encoding.UTF8.GetByteCount(json);
                                nu.RegisterSent(bytes);
                            }
                        }
                        catch { }

                        if (gossip?.Transport != null)
                        {
                            postSuccess = await gossip.Transport.PostJsonAsync(endpoint, json, gossip.Settings.ApiKeyHeader, gossip.Settings.ApiKeyValue);
                        }
                        else if (gossip?.EndpointClient != null)
                        {
                            postSuccess = await gossip.EndpointClient.PostJsonAsync(endpoint, json);
                        }
                        else
                        {
                            using var tmp = new EndpointConnection(gossip.Settings.ApiKeyHeader, gossip.Settings.ApiKeyValue);
                            postSuccess = await tmp.PostJsonAsync(endpoint, json);
                        }
                    }
                    catch (Exception ex)
                    {
                        lastEx = ex;
                        Debug.LogException(new Exception($"[GenericSocketConnection] HTTP post failed: {ex.Message}", ex));
                        postSuccess = false;
                    }

                    if (postSuccess)
                    {
                        lock (dbLock)
                        {
                            using var db = CreateLiteDatabaseWithRetry();
                            ILiteCollection<T1> col = db.GetCollection<T1>(DataBaseName);
                            col.DeleteMany(_ => true);
                        }

                        Debug.Log($"[GenericSocketConnection] POST {EventName} -> {endpoint} (items={Data.Messages?.Count})");
                        return true;
                    }
                    else
                    {
                        if (lastEx != null)
                            Debug.LogWarning($"[GenericSocketConnection] POST failed for {EventName}: {lastEx.Message}");
                        else
                            Debug.LogWarning($"[GenericSocketConnection] POST failed for {EventName} (no exception).");
                        return false;
                    }
                }
                else
                {
                    string json = JsonConvert.SerializeObject(Data, Formatting.Indented);
                    await EmitStringAsJSONAsync(EventName, json);

                    lock (dbLock)
                    {
                        using var db = CreateLiteDatabaseWithRetry();
                        ILiteCollection<T1> col = db.GetCollection<T1>(DataBaseName);
                        col.DeleteMany(_ => true);
                    }

                    Debug.Log($"[GenericSocketConnection] Emitted {EventName} via socket (items={Data.Messages?.Count})");
                    return true;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(new Exception($"SendDataToSocketAsync failed for {EventName}: {exception.Message}", exception));
            }

            Debug.LogError("Could not emit");
            return false;
        }

        public int GetPendingCount()
        {
            if (string.IsNullOrEmpty(localDBPath))
                Initialize();

            try
            {
                lock (dbLock)
                {
                    using var db = CreateLiteDatabaseWithRetry();
                    var col = db.GetCollection<T1>(DataBaseName);
                    return (int)col.Count();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GenericSocketConnection] GetPendingCount failed for {DataBaseName}: {ex.Message}");
                return 0;
            }
        }
    }
}
