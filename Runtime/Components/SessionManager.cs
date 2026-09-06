using System;
using System.Security.Cryptography;
using System.Text;
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
        [Tooltip("Leave empty to auto-detect from Player tag count. Set 'single' or 'multi' to override.")]
        [SerializeField] private string sessionTypeOverride = "";
        [SerializeField] private string subscriptionTypeOverride = "";


        [Tooltip("Id de jugador del integrador. Vacio = el SDK resuelve uno estable por su cuenta.")]
        [SerializeField] private string persistentPlayerId;

        private const string LocalPlayerIdKey = "gossip_player_id";
        private const string PendingSessionIdKey = "gossip_pending_session_id";
        private const string PendingSessionStartKey = "gossip_pending_session_start";

        private string playerId;
        private double sessionStartTimeRealtime;
        private bool sessionStarted = false;

        private string ResolveSessionType()
        {
            if (!string.IsNullOrEmpty(sessionTypeOverride))
                return sessionTypeOverride;
            var players = GameObject.FindGameObjectsWithTag("Player");
            return players.Length > 1 ? "multi" : "single";
        }

        private string ResolveSubscriptionType()
        {
            if (!string.IsNullOrEmpty(subscriptionTypeOverride))
                return subscriptionTypeOverride;
            return "free_trial";
        }

        public void SetSubscriptionType(string value)
        {
            subscriptionTypeOverride = value ?? string.Empty;
        }

        public void SetSessionType(string value)
        {
            sessionTypeOverride = value ?? string.Empty;
        }
        private void Awake()
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                sessionId = Guid.NewGuid().ToString();

            playerId = ResolvePlayerId();

            if (Gossip.Instance?.Settings?.EnableDebug == true)
                Debug.Log($"[SessionManager] playerId source={PlayerIdSource} id={playerId}");

            SetCurrentIdsSafe(playerId, sessionId);

            // Check for orphaned session from previous run (e.g., editor Stop)
            // Sesion huerfana de la ejecucion anterior (Stop del editor, cierre a lo bruto).
            // El session_end tiene que cerrar ESA sesion, no la que acaba de empezar: el
            // tracker usa siempre los ids actuales. Antes se mandaba con el sessionId nuevo.
            if (PlayerPrefs.HasKey(PendingSessionIdKey))
            {
                string orphanSessionId = PlayerPrefs.GetString(PendingSessionIdKey, string.Empty);
                long orphanStartUnix = long.Parse(PlayerPrefs.GetString(PendingSessionStartKey, "0"));
                double orphanDuration = System.Math.Max(0.0, (double)(System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() - orphanStartUnix));

                if (!string.IsNullOrWhiteSpace(orphanSessionId) && orphanSessionId != sessionId)
                {
                    SetCurrentIdsSafe(playerId, orphanSessionId);
                    SendSessionEvent("session_end", orphanDuration, orphanSessionId);
                    SetCurrentIdsSafe(playerId, sessionId);
                }

                ClearPendingSession();
            }
            
            double duration = 0;
            SendSessionEvent("session_start", duration);

            sessionStartTimeRealtime = Time.realtimeSinceStartupAsDouble;
            sessionStarted = true;
            PlayerPrefs.SetString(PendingSessionIdKey, sessionId);
            PlayerPrefs.SetString(PendingSessionStartKey, System.DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
            PlayerPrefs.Save();
        }

        /// <summary>Fuente de la que salio el id de jugador actual.</summary>
        public string PlayerIdSource { get; private set; } = "unknown";

        /// <summary>
        /// Contrato para que el integrador imponga su propia identidad.
        ///
        /// Sustituye a la resolucion por reflexion que habia aqui, que buscaba una propiedad
        /// "PlatformAdapter" y un metodo "GetUserId" que NO EXISTEN en ningun sitio: medido el
        /// 6-sep-2026 con un barrido de los 156 ficheros .cs del repo (156/156, 0 fallos) y una
        /// busqueda en toda la organizacion. Esa rama devolvia null siempre, en silencio.
        /// </summary>
        public interface IGossipPlayerIdentity
        {
            string GetPlayerId();
            string SourceName { get; }
        }

        private static IGossipPlayerIdentity identityProvider;

        /// <summary>Registra el proveedor del integrador. Se llama antes del Awake.</summary>
        public static void SetIdentityProvider(IGossipPlayerIdentity provider)
        {
            identityProvider = provider;
        }

        /// <summary>
        /// Escalera de resolucion, de mas estable a menos:
        ///
        ///   1. persistentPlayerId del inspector  -> "integrator"
        ///   2. proveedor registrado              -> el nombre que declare
        ///   3. id de dispositivo hasheado        -> "device"
        ///   4. Guid en PlayerPrefs               -> "install"
        ///
        /// Hasta hoy el escalon 4 era el UNICO camino real, asi que la identidad era por
        /// INSTALACION: desinstalar el APK borra PlayerPrefs y creaba un usuario nuevo.
        /// Medido en dev el 6-sep-2026: 89 usuarios para 94 sesiones, New igual a Active en
        /// todos los rangos y Top Users 0.
        /// </summary>
        private string ResolvePlayerId()
        {
            if (!string.IsNullOrWhiteSpace(persistentPlayerId))
            {
                PlayerIdSource = "integrator";
                return persistentPlayerId;
            }

            string fromProvider = TryGetProviderPlayerId();
            if (!string.IsNullOrWhiteSpace(fromProvider))
                return fromProvider;

            string fromDevice = TryGetDeviceScopedPlayerId();
            if (!string.IsNullOrWhiteSpace(fromDevice))
            {
                PlayerIdSource = "device";
                return fromDevice;
            }

            PlayerIdSource = "install";
            return GetOrCreateLocalPlayerId();
        }

        private string TryGetProviderPlayerId()
        {
            if (identityProvider == null)
                return null;

            try
            {
                string id = identityProvider.GetPlayerId();
                if (string.IsNullOrWhiteSpace(id))
                    return null;

                string nombre = identityProvider.SourceName;
                PlayerIdSource = string.IsNullOrWhiteSpace(nombre) ? "provider" : nombre;
                return id;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SessionManager] Identity provider failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Id estable por DISPOSITIVO, no por instalacion. En Android sale de ANDROID_ID, que
        /// esta atado a la clave de firma del APK y sobrevive a desinstalar y reinstalar.
        ///
        /// No sale del visor el identificador crudo: se hashea con el nombre de paquete, asi
        /// que el id no es correlacionable entre apps distintas. La sal es Application.identifier
        /// y no la API key a proposito: si la clave se rota, el id no puede cambiar.
        /// </summary>
        private string TryGetDeviceScopedPlayerId()
        {
            try
            {
                string crudo = SystemInfo.deviceUniqueIdentifier;
                if (string.IsNullOrWhiteSpace(crudo) || crudo == SystemInfo.unsupportedIdentifier)
                    return null;

                using (SHA256 sha = SHA256.Create())
                {
                    byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(Application.identifier + ":" + crudo));
                    StringBuilder sb = new StringBuilder(bytes.Length * 2);
                    for (int i = 0; i < bytes.Length; i++)
                        sb.Append(bytes[i].ToString("x2"));
                    return sb.ToString(0, 32);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SessionManager] Could not resolve device player id: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Id de jugador estable para esta instalacion.
        ///
        /// Antes, cuando la plataforma no daba id, se generaba un Guid nuevo en CADA
        /// arranque. Medido el 5-sep-2026 en dev: 87 "usuarios" que eran 4 dispositivos.
        /// Total Users contaba arranques, Top Users era 0 siempre y Active Users no se
        /// podia distinguir de New Users.
        /// </summary>
        private string GetOrCreateLocalPlayerId()
        {
            try
            {
                string stored = PlayerPrefs.GetString(LocalPlayerIdKey, string.Empty);
                if (!string.IsNullOrWhiteSpace(stored))
                    return stored;

                string generated = Guid.NewGuid().ToString();
                PlayerPrefs.SetString(LocalPlayerIdKey, generated);
                PlayerPrefs.Save();
                return generated;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SessionManager] Could not persist local player id: {ex.Message}");
                return Guid.NewGuid().ToString();
            }
        }

        /// <summary>Id de jugador del integrador. Vacio devuelve al SDK su propia resolucion.</summary>
        public void SetPersistentPlayerId(string value)
        {
            persistentPlayerId = value ?? string.Empty;
        }

        private void SetCurrentIdsSafe(string player, string session)
        {
            try
            {
                Gossip.Instance?.SetCurrentIds(player, session);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SessionManager] Could not set Gossip current ids: {ex.Message}");
            }
        }

        private void ClearPendingSession()
        {
            PlayerPrefs.DeleteKey(PendingSessionIdKey);
            PlayerPrefs.DeleteKey(PendingSessionStartKey);
            PlayerPrefs.Save();
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
            ClearPendingSession();
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
            ClearPendingSession();
        }

        private void SendSessionEvent(string eventType, double durationSeconds, string sessionIdOverride = null)
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

                var recordMethod = tracker.GetType().GetMethod("RecordEvent", new Type[] { typeof(string), typeof(double), typeof(string), typeof(string) });
                if (recordMethod != null)
                {
                    recordMethod.Invoke(tracker, new object[] { eventType, durationSeconds, ResolveSessionType(), ResolveSubscriptionType() });
                    return;
                }

                var data = new SessionTracker.EntityData
                {
                    EventType = eventType,
                    TimestampUtc = DateTime.UtcNow.ToString("o"),
                    DurationSeconds = durationSeconds,
                    SceneName = SceneManager.GetActiveScene().name,
                    PlayerId = playerId,
                    SessionId = sessionIdOverride ?? sessionId,
                    SessionType = ResolveSessionType(),
                    SubscriptionType = ResolveSubscriptionType()
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

        public void RecordPause()                        => SendSessionEvent("session_pause", 0.0);
        public void RecordResume(double durationSeconds) => SendSessionEvent("session_resume", durationSeconds);
    }
}
