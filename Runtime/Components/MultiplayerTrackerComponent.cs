using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GossipSDK.Core;
using GossipSDK.Core.Utilities;
using System.Reflection;
using GossipSDK.Tracking.GameplayMetrics;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class MultiplayerTrackerComponent : MonoBehaviour
    {
        [Header("Match Info")]
        public string roomId;
        public string matchType;

        [Header("Tracking")]
        public float snapshotIntervalSeconds = 10f;
        public bool autoReportOnStart = true;

        private Coroutine snapshotRoutine;
        private string cachedCountSource;

        void Start()
        {
            if (autoReportOnStart)
            {
                StartTracking();
            }
        }

        void OnDestroy()
        {
            StopTracking();
            CaptureSnapshot();
        }

        public void StartTracking()
        {
            if (snapshotRoutine != null)
                return;

            snapshotRoutine = StartCoroutine(SnapshotLoop());
        }

        public void StopTracking()
        {
            if (snapshotRoutine != null)
            {
                StopCoroutine(snapshotRoutine);
                snapshotRoutine = null;
            }
        }

        IEnumerator SnapshotLoop()
        {
            while (true)
            {
                CaptureSnapshot();
                yield return new WaitForSeconds(snapshotIntervalSeconds);
            }
        }

        public void CaptureSnapshot()
        {
            var tracker = Gossip.Instance?.MultiplayerTracker;
            if (tracker == null)
                return;

            List<MultiplayerTracker.PlayerInfo> players = CollectPlayers();
            string source = ResolveCountSource();

            // Si nadie sobrescribio CollectPlayers(), la lista trae solo este dispositivo y
            // no vale como medida. Antes de rendirse, se le pregunta a la capa de red.
            if (source == PlayerCountSources.Unknown)
            {
                NetworkPlayerCount fromNetwork = NetworkPlayerCountResolver.Resolve();
                if (fromNetwork.Known)
                {
                    players = BuildPlaceholders(fromNetwork.Count);
                    source = PlayerCountSources.NetworkAuto;
                }
            }

            tracker.CapMatchSnapshot(
                roomId,
                matchType,
                players,
                source
            );
        }

        /// <summary>
        /// La capa de red nos da CUANTOS, no QUIENES. Se rellena la lista con marcadores para
        /// que PlayerCount cuadre, y sin inventar identidades: sin PlayerId y sin ping, que es
        /// justo lo que no sabemos.
        /// </summary>
        private static List<MultiplayerTracker.PlayerInfo> BuildPlaceholders(int count)
        {
            var list = new List<MultiplayerTracker.PlayerInfo>();

            for (int i = 0; i < count; i++)
            {
                list.Add(new MultiplayerTracker.PlayerInfo
                {
                    PlayerId = null,
                    DisplayName = null,
                    PingMs = null
                });
            }

            return list;
        }

        /// <summary>
        /// De donde sale el numero de jugadores de este snapshot.
        ///
        /// La implementacion por defecto de CollectPlayers() devuelve SIEMPRE uno: el propio
        /// dispositivo. Sin una subclase que la sobrescriba, PlayerCount vale 1 en todas las
        /// sesiones y eso no es una medida, es un hueco.
        ///
        /// Se mira si el metodo esta declarado en otra clase distinta de esta: es la forma
        /// fiable de saber si alguien nos dio los jugadores de verdad. Se cachea porque el
        /// tipo no cambia en ejecucion y esto corre cada 10 s.
        /// </summary>
        private string ResolveCountSource()
        {
            if (cachedCountSource != null)
                return cachedCountSource;

            MethodInfo collect = GetType().GetMethod(
                "CollectPlayers",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
            );

            bool overridden = collect != null
                && collect.DeclaringType != typeof(MultiplayerTrackerComponent);

            cachedCountSource = overridden
                ? PlayerCountSources.Integrator
                : PlayerCountSources.Unknown;

            return cachedCountSource;
        }

        protected virtual List<MultiplayerTracker.PlayerInfo> CollectPlayers()
        {
            var list = new List<MultiplayerTracker.PlayerInfo>();

            list.Add(new MultiplayerTracker.PlayerInfo
            {
                PlayerId = SystemInfo.deviceUniqueIdentifier,
                DisplayName = "LocalPlayer",
                PingMs = null,
                Meta = new Dictionary<string, string>
                {
                    { "platform", Application.platform.ToString() }
                }
            });

            return list;
        }

        public void OnPlayerJoined()
        {
            CaptureSnapshot();
        }

        public void OnPlayerLeft()
        {
            CaptureSnapshot();
        }
    }
}
