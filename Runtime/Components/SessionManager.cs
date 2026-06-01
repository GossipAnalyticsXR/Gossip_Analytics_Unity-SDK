using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using GossipSDK.Core;
using GossipSDK.Tracking.GameplayMetrics;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class SessionManager : MonoBehaviour
    {
        [SerializeField] private string sessionId;

        private string persistentPlayerId;

        private string playerId;
        private double sessionStartTimeRealtime;
        private bool sessionStarted = false;

        private void Awake()
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                sessionId = Guid.NewGuid().ToString();

            if (!string.IsNullOrWhiteSpace(persistentPlayerId))
                playerId = persistentPlayerId;
            else
                playerId = TryGetPlatformUserId() ?? Guid.NewGuid().ToString();

            try
            {
                Gossip.Instance?.SetCurrentIds(playerId, sessionId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SessionManager] Could not set Gossip current ids: {ex.Message}");
            }

            // Check for orphaned session from previous run (e.g., editor Stop)
            if (PlayerPrefs.HasKey("gossip_pending_session_id"))
            {
                long orphanStartUnix = long.Parse(PlayerPrefs.GetString("gossip_pending_session_start", "0"));
                double orphanDuration = System.Math.Max(0.0, (double)(System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() - orphanStartUnix));
                SendSessionEvent("session_end", orphanDuration);
                PlayerPrefs.DeleteKey("gossip_pending_session_id");
                PlayerPrefs.DeleteKey("gossip_pending_session_start");
                PlayerPrefs.Save();
            }
            
            double duration = 0;
            SendSessionEvent("start_session", duration);

            sessionStartTimeRealtime = Time.realtimeSinceStartupAsDouble;
            sessionStarted = true;
            PlayerPrefs.SetString("gossip_pending_session_id", sessionId);
            PlayerPrefs.SetString("gossip_pending_session_start", System.DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
            PlayerPrefs.Save();
        }

        private string TryGetPlatformUserId()
        {
            try
            {
                var gossipInstance = Gossip.Instance;
                if (gossipInstance == null) return null;

                var platformAdapterProp = gossipInstance.GetType().GetProperty("PlatformAdapter");
                if (platformAdapterProp == null) return null;

                var adapter = platformAdapterProp.GetValue(gossipInstance);
                if (adapter == null) return null;

                var getUserIdMethod = adapter.GetType().GetMethod("GetUserId");
                if (getUserIdMethod == null) return null;

                var idObj = getUserIdMethod.Invoke(adapter, null);
                if (idObj is string idStr && !string.IsNullOrWhiteSpace(idStr))
                    return idStr;
            }
            catch
            {
            }

            return null;
        }

        private void OnApplicationQuit()
        {
            if (!sessionStarted)
            {
                // nothing to send
                return;
            }

            double totalDuration = Time.realtimeSinceStartupAsDouble - sessionStartTimeRealtime;
            sessionStarted = false;
            if ((UnityEngine.Object)Gossip.Instance != null)
                SendSessionEvent("session_end", totalDuration);
            else
                Debug.LogWarning("[SessionManager] Gossip.Instance null on quit -- session_end not sent");
            PlayerPrefs.DeleteKey("gossip_pending_session");
            PlayerPrefs.Save();
        }

        private void OnDestroy()
        {
            if (!sessionStarted)
            {
                // nothing to send
                return;
            }

            double totalDuration = Time.realtimeSinceStartupAsDouble - sessionStartTimeRealtime;
            sessionStarted = false;
            if ((UnityEngine.Object)Gossip.Instance != null)
                SendSessionEvent("session_end", totalDuration);
            else
                Debug.LogWarning("[SessionManager] Gossip.Instance null on destroy -- session_end not sent");
        }

        private void SendSessionEvent(string eventType, double durationSeconds)
        {
            try
            {
                var tracker = Gossip.Instance?.SessionTracker;
                if (tracker == null)
                {
                    if (Gossip.Instance?.Settings?.EnableDebug == true)
                        Debug.Log("[SessionManager] SessionTracker not available.");
                    return;
                }

                var recordMethod = tracker.GetType().GetMethod("RecordEvent", new Type[] { typeof(string), typeof(double) });
                if (recordMethod != null)
                {
                    recordMethod.Invoke(tracker, new object[] { eventType, durationSeconds });
                    return;
                }

                var data = new SessionTracker.EntityData
                {
                    EventType = eventType,
                    TimestampUtc = DateTime.UtcNow.ToString("o"),
                    DurationSeconds = durationSeconds,
                    SceneName = SceneManager.GetActiveScene().name,
                    PlayerId = playerId,
                    SessionId = sessionId
                };

                var capMethod = tracker.GetType().GetMethod("CapSession");
                if (capMethod != null)
                {
                    capMethod.Invoke(tracker, new object[] { data });
                    if (Gossip.Instance?.Settings?.EnableDebug == true)
                        Debug.Log($"[SessionManager] Fallback CapSession sent: {eventType} duration={durationSeconds:F3}s");
                    return;
                }

                try
                {
                    tracker.CapSession(data);
                    if (Gossip.Instance?.Settings?.EnableDebug == true)
                        Debug.Log($"[SessionManager] CapSession sent: {eventType} duration={durationSeconds:F3}s");
                }
                catch (Exception ex)
                {
                    Debug.LogException(new Exception("[SessionManager] Could not send session event (no method found)", ex));
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception($"[SessionManager] SendSessionEvent failed for {eventType}: {ex.Message}", ex));
            }
        }
    }
}
