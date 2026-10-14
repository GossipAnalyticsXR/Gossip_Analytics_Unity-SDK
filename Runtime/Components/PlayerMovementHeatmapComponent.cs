using UnityEngine;
using UnityEngine.SceneManagement;
using GossipSDK.Core;
using GossipSDK.Tracking;
using GossipSDK.Heatmaps;
using System;
using System.Collections.Generic;
using GossipSDK.Tracking.GameplayMetrics;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class PlayerMovementHeatmapComponent : MonoBehaviour
    {
        [Header("Heatmap Settings")]
        public Vector2 worldMinXZ = new Vector2(-5, -5);
        public Vector2 worldMaxXZ = new Vector2(5, 5);
        public float cellSizeMeters = 1.0f;
        public float sampleInterval = 0.25f;
        public float heatmapFlushInterval = 10f;

        [Header("Auto Bounds")]
        [SerializeField] public bool autoBounds = true;

        private HeatmapManager heatmapManager;
        private float sampleTimer;
        private float flushTimer;

        // La ruta temporal, aparte del histograma. El histograma suma y no guarda ni el
        // orden ni el instante de cada muestra; Behavior view necesita las dos cosas, asi
        // que la muestra cruda se lleva en paralelo y se vuelca con el mismo sobre.
        private struct MuestraDeRuta
        {
            public float X;
            public float Z;
            public double T;
        }

        private readonly List<MuestraDeRuta> rutaBuffer = new List<MuestraDeRuta>();

        // Tope de seguridad: a 4 Hz son mas de 8 minutos sin volcar. Si se alcanza se deja
        // de anadir en lugar de crecer sin limite, y el histograma no se entera.
        private const int MaxMuestrasDeRuta = 2000;

        private void Start()
        {

            if (autoBounds && HeatmapBoundsResolver.ResolveSceneBoundsXZ(
                out Vector2 resolvedMin, out Vector2 resolvedMax))
            {
                worldMinXZ = resolvedMin;
                worldMaxXZ = resolvedMax;
            }

            heatmapManager = new HeatmapManager(
                SceneManager.GetActiveScene().name,
                worldMinXZ,
                worldMaxXZ,
                cellSizeMeters
            );

            // Diagnóstico Build 1: modo de tracking real que resolvió el runtime + puntos de guardián.
            var _diagSubs = new System.Collections.Generic.List<UnityEngine.XR.XRInputSubsystem>();
            UnityEngine.SubsystemManager.GetInstances(_diagSubs);
            if (_diagSubs.Count > 0)
            {
            var _pts = new System.Collections.Generic.List<Vector3>();
            bool _ok = _diagSubs[0].TryGetBoundaryPoints(_pts);
            Debug.Log($"[GossipDiag] trackingOrigin={_diagSubs[0].GetTrackingOriginMode()} boundaryOk={_ok} boundaryPoints={_pts.Count} sceneMin={worldMinXZ} sceneMax={worldMaxXZ}");
            }
        }

        private void Update()
        {

            sampleTimer += Time.deltaTime;
            flushTimer += Time.deltaTime;

            if (sampleTimer >= sampleInterval)
            {
                sampleTimer = 0f;
                RegisterPosition();
            }

            if (flushTimer >= heatmapFlushInterval)
            {
                FlushHeatmap();
                flushTimer = 0f;
            }
        }

        private void RegisterPosition()
        {
            if (heatmapManager == null) return;

            Vector3 pos = transform.position;

            // RegisterHit devuelve false cuando la posicion cae fuera de la rejilla y la
            // descarta (HeatmapManager.HitsDiscarded). La ruta tiene que descartarla tambien:
            // si no, el camino lleva puntos que el histograma rechazo por estar fuera del
            // marco, y en el payload no hay forma de distinguirlos.
            if (!heatmapManager.RegisterHit(pos)) return;

            if (rutaBuffer.Count < MaxMuestrasDeRuta)
            {
                rutaBuffer.Add(new MuestraDeRuta { X = pos.x, Z = pos.z, T = Time.realtimeSinceStartupAsDouble });
            }
        }

        private void FlushHeatmap()
        {
            var tracker = Gossip.Instance?.HeatmapTracker;
            if (tracker == null || heatmapManager == null) return;

            var ruta = ConstruirRuta();

            var guardado = tracker.CapFromHeatmap(
                heatmapManager,
                heatmapSource: "player_movement",
                sparse: true,
                path: ruta
            );

            // Solo se limpia si la fila quedo guardada. Si no, los puntos se quedan y viajan
            // en el siguiente volcado: la matriz de celdas se recupera sola porque es
            // acumulativa, pero la ruta no, y limpiarla a ciegas la perdia entera.
            //
            // No hay riesgo de duplicar: si no se guardo, no hay fila con esos puntos. El
            // tope de MaxMuestrasDeRuta es el que acota el buffer si el fallo se repite.
            if (guardado)
            {
                rutaBuffer.Clear();
            }

            Debug.Log($"[GossipDiag] movement hits registered={heatmapManager.HitsRegistered} discarded={heatmapManager.HitsDiscarded}");

            if (Gossip.Instance?.Settings?.EnableDebug == true)
                Debug.Log("[Heatmap] Player movement heatmap flushed");
        }

        // T viaja como milisegundos ANTES del TimestampUtc que HeatmapTracker pone al
        // construir el sobre, asi que el instante absoluto de cada punto es
        // TimestampUtc - T. Devuelve null cuando no hay nada: con NullValueHandling.Ignore
        // el campo no se serializa y el JSON queda igual que antes.
        //
        // El reloj es realtimeSinceStartupAsDouble y NO timeAsDouble: este ultimo avanza
        // escalado por Time.timeScale, mientras que el TimestampUtc del sobre es reloj de
        // pared. Con una pausa o una camara lenta, TimestampUtc - T daria el instante
        // absoluto equivocado a todos los puntos del volcado.
        private List<HeatmapTracker.PathPoint> ConstruirRuta()
        {
            if (rutaBuffer.Count == 0) return null;

            double ahora = Time.realtimeSinceStartupAsDouble;
            var ruta = new List<HeatmapTracker.PathPoint>(rutaBuffer.Count);

            for (int i = 0; i < rutaBuffer.Count; i++)
            {
                MuestraDeRuta m = rutaBuffer[i];
                ruta.Add(new HeatmapTracker.PathPoint
                {
                    X = m.X,
                    Z = m.Z,
                    T = (int)Math.Round((ahora - m.T) * 1000.0)
                });
            }

            return ruta;
        }

        private void OnDisable()
        {
            FlushHeatmap();
        }
    }
}
