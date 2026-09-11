using System;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using GossipSDK.Core;
using GossipSDK.Core.Utilities;
using GossipSDK.Tracking.GameplayMetrics;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class SessionManager : MonoBehaviour
    {
        [SerializeField] private string sessionId;
        [Tooltip("Leave empty to auto-detect from the networking layer in the project. Set 'single' or 'multi' to override.")]
        [SerializeField] private string sessionTypeOverride = "";
        [SerializeField] private string subscriptionTypeOverride = "";


        [Tooltip("Id de jugador del integrador. Vacio = el SDK resuelve uno estable por su cuenta.")]
        [SerializeField] private string persistentPlayerId;

        private const string LocalPlayerIdKey = "gossip_player_id";
        private const string PendingSessionIdKey = "gossip_pending_session_id";
        private const string PendingSessionStartKey = "gossip_pending_session_start";

        // Ultima vez que se vio viva la sesion, en segundos unix. Sin esto, el cierre por
        // huerfana solo puede calcular `ahora - inicio`, y ese `ahora` es el arranque
        // SIGUIENTE: la duracion de cada sesion pasaria a ser el hueco hasta que el usuario
        // vuelve a abrir la app.
        private const string PendingSessionLastSeenKey = "gossip_pending_session_lastseen";

        // Cada cuanto se refresca el latido. A 5 s el cierre por huerfana puede quedarse
        // como mucho 5 s corto: error acotado y hacia abajo, en vez de ilimitado y hacia arriba.
        private const double LastSeenIntervaloSegundos = 5.0;

        private double siguienteLastSeen;

        private string playerId;
        private double sessionStartTimeRealtime;
        private bool sessionStarted = false;

        /// <summary>
        /// Guardia de instancia unica. Este componente vive en el prefab
        /// GossipAnalyticsManager, junto a GossipManager, que ya hace
        /// DontDestroyOnLoad y destruye los duplicados. Pero el orden de Awake ENTRE
        /// componentes del mismo GameObject no esta definido, y Destroy(gameObject) no
        /// surte efecto hasta el final del frame: un duplicado del prefab en otra
        /// escena puede acunar un sessionId y mandar su session_start ANTES de que
        /// GossipManager lo destruya, y esa sesion fantasma ya quedo contada.
        /// GossipManagerEditor ya avisa de que el prefab aparece en varias escenas.
        /// </summary>
        private static SessionManager _instance;

        /// <summary>
        /// Individual o de grupo, y null si no lo sabemos.
        ///
        /// Antes contaba GameObjects con el tag "Player" en la escena local, que no es
        /// multijugador: en red, que los avatares remotos lleven ese tag depende de los
        /// prefabs del integrador, y en un juego de un jugador cualquier segundo objeto
        /// etiquetado Player (un rig espejo, un maniqui) daba "multi".
        ///
        /// null y no "single" cuando no se sabe: el backend excluye null al filtrar, asi que
        /// una sesion sin medir deja de contar como solitaria en vez de inflar ese lado.
        /// Medido el 10-sep-2026: las 107 sesiones de dev estaban TODAS en single y no habia
        /// forma de saber cuantas lo eran de verdad.
        /// </summary>
        private string ResolveSessionType()
        {
            if (!string.IsNullOrEmpty(sessionTypeOverride))
                return sessionTypeOverride;

            NetworkPlayerCount players = NetworkPlayerCountResolver.Resolve();
            if (!players.Known)
                return null;

            return players.Count > 1 ? "multi" : "single";
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
            if ((UnityEngine.Object)_instance != null && _instance != this)
            {
                // A proposito Warning: GossipBuildPreprocessor apaga EnableDebug fuera
                // de Development, y esta linea explica una sesion que NO aparecera.
                Debug.LogWarning(
                    "[SessionManager] Ya hay un SessionManager vivo. Este duplicado no " +
                    "abre sesion ni acuna sessionId.");
                enabled = false;
                return;
            }
            _instance = this;

            if (string.IsNullOrWhiteSpace(sessionId))
                sessionId = Guid.NewGuid().ToString();

            playerId = ResolvePlayerId();

            // A proposito Warning y no Log: GossipBuildPreprocessor apaga EnableDebug en
            // toda build que no sea Development, y esta linea es justo la que dice de donde
            // salio la identidad. Una por sesion.
            Debug.LogWarning("[GossipID] source=" + PlayerIdSource + " id=" + playerId + " paquete=" + Application.identifier);

            SetCurrentIdsSafe(playerId, sessionId);

            // Check for orphaned session from previous run (e.g., editor Stop)
            // Sesion huerfana de la ejecucion anterior (Stop del editor, cierre a lo bruto).
            // El session_end tiene que cerrar ESA sesion, no la que acaba de empezar: el
            // tracker usa siempre los ids actuales. Antes se mandaba con el sessionId nuevo.
            if (PlayerPrefs.HasKey(PendingSessionIdKey))
            {
                string orphanSessionId = PlayerPrefs.GetString(PendingSessionIdKey, string.Empty);

                // La duracion sale de `lastseen`, NO de la hora de ahora. Este session_end se
                // manda en el arranque SIGUIENTE, asi que `ahora - inicio` seria el hueco de
                // reloj de pared hasta que el usuario volvio a abrir la app: horas o dias.
                // Con el quit ya sin enviar nada, esta ruta cierra TODAS las sesiones, asi que
                // ese error dejaria de ser una rareza de 6 fichas y pasaria a ser la norma.
                long orphanStartUnix;
                if (!long.TryParse(PlayerPrefs.GetString(PendingSessionStartKey, "0"), out orphanStartUnix))
                    orphanStartUnix = 0;

                long orphanLastSeenUnix;
                bool hayLastSeen = long.TryParse(PlayerPrefs.GetString(PendingSessionLastSeenKey, ""), out orphanLastSeenUnix);
                if (orphanLastSeenUnix < orphanStartUnix) orphanLastSeenUnix = orphanStartUnix;

                double orphanDuration = System.Math.Max(0.0, (double)(orphanLastSeenUnix - orphanStartUnix));
                DateTime orphanEndUtc = System.DateTimeOffset.FromUnixTimeSeconds(orphanLastSeenUnix).UtcDateTime;

                // Sin `lastseen` no sabemos cuando acabo: es una sesion que arranco con una
                // version anterior del SDK. No se cierra inventandole un cero ni la hora de
                // llegada; se deja como esta y se limpia el pending. Un hueco y un cero medido
                // no pueden acabar pareciendose.
                if (hayLastSeen && orphanStartUnix > 0 && !string.IsNullOrWhiteSpace(orphanSessionId) && orphanSessionId != sessionId)
                {
                    SetCurrentIdsSafe(playerId, orphanSessionId);
                    SendSessionEvent("session_end", orphanDuration, orphanSessionId, orphanEndUtc);
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
            // El latido arranca en la hora de inicio: una sesion que muera antes del primer
            // Update cierra con duracion 0, que es la verdad, y no con un hueco inventado.
            PlayerPrefs.SetString(PendingSessionLastSeenKey, System.DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
            siguienteLastSeen = Time.realtimeSinceStartupAsDouble + LastSeenIntervaloSegundos;
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
        /// No sale del visor el identificador crudo: se mezcla con el nombre de paquete, asi
        /// que el id no es correlacionable entre apps distintas. La sal es Application.identifier
        /// y no la API key a proposito: si la clave se rota, el id no puede cambiar.
        ///
        /// NO se usa SHA256. El linker de IL2CPP se lleva System.Security.Cryptography si nadie
        /// la referencia desde codigo managed, y eso revienta SOLO en el dispositivo, nunca en
        /// el editor: la excepcion caia en el catch y la identidad se iba en silencio al id por
        /// instalacion. Medido el 6-sep-2026 en Quest con el paquete 2.0.3: el unico usuario de
        /// la build 1.1.3 entro con GUID. Se mezcla con FNV-1a, que no depende de la BCL.
        /// </summary>
        private string TryGetDeviceScopedPlayerId()
        {
            try
            {
                string crudo = SystemInfo.deviceUniqueIdentifier;

                if (string.IsNullOrWhiteSpace(crudo) || crudo == SystemInfo.unsupportedIdentifier)
                {
                    Debug.LogWarning("[GossipID] deviceUniqueIdentifier no soportado en esta plataforma; se cae al id por instalacion.");
                    return null;
                }

                string semilla = Application.identifier + ":" + crudo;
                return Mezclar(semilla, 0xcbf29ce484222325UL) + Mezclar(semilla, 0x9e3779b97f4a7c15UL);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GossipID] fallo resolviendo el id por dispositivo: " + ex.GetType().Name + " " + ex.Message);
                return null;
            }
        }

        /// <summary>FNV-1a de 64 bits con semilla inicial variable. Devuelve 16 caracteres hex.</summary>
        private static string Mezclar(string texto, ulong inicial)
        {
            unchecked
            {
                ulong h = inicial;
                byte[] bytes = Encoding.UTF8.GetBytes(texto);
                for (int i = 0; i < bytes.Length; i++)
                {
                    h ^= bytes[i];
                    h *= 0x100000001b3UL;
                }
                return h.ToString("x16");
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
            PlayerPrefs.DeleteKey(PendingSessionLastSeenKey);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Deja escrito CUANDO se vio viva la sesion por ultima vez. Es lo que permite que el
        /// arranque siguiente cierre la huerfana con su duracion real, en vez de con el hueco
        /// de reloj de pared hasta esa reapertura.
        /// </summary>
        private void TouchLastSeen(bool guardarYa)
        {
            PlayerPrefs.SetString(PendingSessionLastSeenKey, System.DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
            if (guardarYa) PlayerPrefs.Save();
        }

        private void Update()
        {
            if (!sessionStarted) return;
            if (Time.realtimeSinceStartupAsDouble < siguienteLastSeen) return;
            siguienteLastSeen = Time.realtimeSinceStartupAsDouble + LastSeenIntervaloSegundos;
            // Sin Save(): PlayerPrefs.Save() escribe a disco y esto corre cada 5 s. Unity
            // vuelca solo al pausar y al salir, y el Save() explicito lo hace OnApplicationPause.
            TouchLastSeen(false);
        }

        /// <summary>
        /// En Android y Quest esta SI se ejecuta antes de que maten el proceso, al contrario
        /// que OnApplicationQuit. Es el momento bueno para dejar el latido en disco.
        /// </summary>
        private void OnApplicationPause(bool pausando)
        {
            if (!sessionStarted || !pausando) return;
            TouchLastSeen(true);
        }

        private void OnApplicationQuit()
        {
            if (!sessionStarted)
            {
                // nothing to send
                return;
            }

            // NO se manda el session_end y NO se borra el pending, a proposito.
            //
            // Medido el 6-sep-2026 sobre 8.482 sesiones: solo 6 tienen `end` (0,07%), y las
            // seis con createdAt igual a updatedAt al milisegundo, o sea cerradas por la ruta
            // de huerfana. Ninguna sesion que arranco con `start` llego jamas a `end` por
            // aqui: en Android el proceso muere antes de que salga el envio asincrono
            // (CapSession solo escribe en LiteDB local, y el envio va con .Forget()).
            //
            // Y al borrar el pending se llevaba por delante la unica via que si funciona: el
            // arranque siguiente cerrando la huerfana. Dejandolo, esa ruta la cierra con su
            // duracion y su hora reales.
            //
            // `sessionStarted = false` SE QUEDA: es lo que hace que OnDestroy, que Unity llama
            // justo despues del quit, salga por su guarda y no duplique el cierre.
            sessionStarted = false;
        }

        private void OnDestroy()
        {
            // Se suelta antes del guardia de sessionStarted: si no, un duplicado que
            // nunca arranco sesion dejaria el static apuntando a un objeto muerto.
            if (_instance == this) _instance = null;

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

        private void SendSessionEvent(string eventType, double durationSeconds, string sessionIdOverride = null, DateTime? occurredAtUtc = null)
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

                // Si el tracker fuese de una version anterior sin el parametro de hora, esta
                // busqueda devuelve null y se cae al camino de EntityData de abajo, que sella
                // la hora igual de bien. Degrada sin perder la fecha.
                var recordMethod = tracker.GetType().GetMethod("RecordEvent", new Type[] { typeof(string), typeof(double), typeof(string), typeof(string), typeof(DateTime?) });
                if (recordMethod != null)
                {
                    recordMethod.Invoke(tracker, new object[] { eventType, durationSeconds, ResolveSessionType(), ResolveSubscriptionType(), occurredAtUtc });
                    return;
                }

                var data = new SessionTracker.EntityData
                {
                    EventType = eventType,
                    TimestampUtc = (occurredAtUtc ?? DateTime.UtcNow).ToString("o"),
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
