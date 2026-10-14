using System;
using System.Collections.Generic;
using GossipSDK.Core;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using Newtonsoft.Json;
using UnityEngine;

namespace GossipSDK.Tracking.GameplayMetrics
{
    [Serializable]
    public class HeatmapTracker
        : GenericSocketConnection<HeatmapTracker.EntityData, HeatmapTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingHeatmap";


        [Serializable]
        public class SparseCell
        {
            [JsonProperty("CellX")] public int CellX { get; set; }
            [JsonProperty("CellY")] public int CellY { get; set; }
            [JsonProperty("Count")] public int Count { get; set; }
            [JsonProperty("CenterX")] public float CenterX { get; set; }
            [JsonProperty("CenterZ")] public float CenterZ { get; set; }
        }

        // Una muestra cruda de la ruta, con su instante. El histograma de celdas no
        // guarda ni el orden ni el tiempo: suma. Behavior view necesita las dos cosas,
        // asi que viajan aparte y SIN tocar nada de lo que ya se enviaba.
        //
        // T son los milisegundos ANTES del TimestampUtc del sobre, asi que el instante
        // absoluto de cada punto es TimestampUtc - T. Se eligio asi para no depender de
        // cuando empezo la sesion ni de la cadencia del flush.
        [Serializable]
        public class PathPoint
        {
            [JsonProperty("X")] public float X { get; set; }
            [JsonProperty("Z")] public float Z { get; set; }
            [JsonProperty("T")] public int T { get; set; }
        }

        [Serializable]
        public class EntityData : Data
        {
            [JsonProperty("SceneName")] public string SceneName { get; set; }
            [JsonProperty("HeatmapSource")] public string HeatmapSource { get; set; }
            [JsonProperty("SceneVersion", NullValueHandling = NullValueHandling.Ignore)] public string SceneVersion { get; set; }

            [JsonProperty("Cols")] public int Cols { get; set; }
            [JsonProperty("Rows")] public int Rows { get; set; }
            [JsonProperty("CellSize")] public float CellSize { get; set; }
            [JsonProperty("OriginX")] public float OriginX { get; set; }
            [JsonProperty("OriginZ")] public float OriginZ { get; set; }

            [JsonProperty("CountsFlat", NullValueHandling = NullValueHandling.Ignore)]
            public List<int> CountsFlat { get; set; }

            [JsonProperty("SparseCells", NullValueHandling = NullValueHandling.Ignore)]
            public List<SparseCell> SparseCells { get; set; }

            // Opcional a proposito: con NullValueHandling.Ignore, un tracker que no la
            // asigne manda exactamente el mismo JSON que antes. Hoy solo la rellena
            // PlayerMovementHeatmapComponent; eye, interactable y audio no se tocan.
            //
            // Viaja solo por la rama sparse (CapSparse). CapDense no la lleva porque el
            // movimiento captura con sparse: true.
            [JsonProperty("Path", NullValueHandling = NullValueHandling.Ignore)]
            public List<PathPoint> Path { get; set; }

            [JsonProperty("TimestampUtc")] public string TimestampUtc { get; set; }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }

        // Devuelve si la captura quedo guardada, para que quien tenga un bufer propio -la
        // ruta de PlayerMovementHeatmapComponent- sepa si puede vaciarlo. La matriz de
        // celdas no lo necesita: es acumulativa y se recupera sola en el siguiente volcado.
        public bool CapFromHeatmap(
            Tracking.HeatmapManager hm,
            string heatmapSource,
            bool sparse = true,
            bool rowMajor = true,
            List<PathPoint> path = null)
        {
            if (Gossip.Instance?.Settings?.EnableHeatmaps == false)
            {
                if (Gossip.Instance?.Settings?.EnableDebug == true)
                {
                    Debug.Log("[HeatmapTracker] Heatmaps disabled by settings");
                }
                return false;
            }

            if (hm == null) return false;

            try
            {
                var (cols, rows, cellSize, origin) = hm.GetGridInfo();
                var matrix = hm.GetCountsCopy();

                if (sparse)
                {
                    return CapSparse(hm, heatmapSource, cols, rows, cellSize, origin, matrix, path);
                }

                return CapDense(hm, heatmapSource, cols, rows, cellSize, origin, matrix, rowMajor);
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception(
                    $"[HeatmapTracker] Capture failed ({heatmapSource})", ex));
                return false;
            }
        }

        private bool CapSparse(
            Tracking.HeatmapManager hm,
            string source,
            int cols,
            int rows,
            float cellSize,
            Vector2 origin,
            int[,] matrix,
            List<PathPoint> path = null)
        {
            var sparseCells = new List<SparseCell>();

            for (int x = 0; x < cols; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    int cnt = matrix[x, y];
                    if (cnt <= 0) continue;

                    sparseCells.Add(new SparseCell
                    {
                        CellX = x,
                        CellY = y,
                        Count = cnt,
                        CenterX = origin.x + (x + 0.5f) * cellSize,
                        CenterZ = origin.y + (y + 0.5f) * cellSize
                    });
                }
            }

            // Un sobre sin celdas no es un dato, es ruido. La matriz acumula y no
            // se vacia al capturar -HeatmapManager no tiene Clear ni Reset-, asi que
            // todo lo registrado viaja en el siguiente flush que si traiga celdas:
            // esta guarda no puede perder nada, solo deja de enviar el vacio.
            // Medido el 10-10-2026 en produccion: de los 338 documentos de
            // interaccion de Hospital Zone 1.1.4, 273 (81 %) iban vacios, y 29 de
            // sus 43 sesiones no mandaron otra cosa.
            if (sparseCells.Count == 0)
            {
                if (Gossip.Instance?.Settings?.EnableDebug == true)
                {
                    Debug.Log($"[HeatmapTracker] Skipped empty capture ({source})");
                }

                return false;
            }

            return CapSession(new EntityData
            {
                SceneName = hm.SceneName,
                HeatmapSource = source,
                SceneVersion = Application.version,
                Cols = cols,
                Rows = rows,
                CellSize = cellSize,
                OriginX = origin.x,
                OriginZ = origin.y,
                SparseCells = sparseCells,
                Path = (path != null && path.Count > 0) ? path : null,
                TimestampUtc = DateTime.UtcNow.ToString("o")
            });
        }

        private bool CapDense(
            Tracking.HeatmapManager hm,
            string source,
            int cols,
            int rows,
            float cellSize,
            Vector2 origin,
            int[,] matrix,
            bool rowMajor)
        {
            var flat = new List<int>(cols * rows);

            if (rowMajor)
            {
                for (int y = 0; y < rows; y++)
                    for (int x = 0; x < cols; x++)
                        flat.Add(matrix[x, y]);
            }
            else
            {
                for (int x = 0; x < cols; x++)
                    for (int y = 0; y < rows; y++)
                        flat.Add(matrix[x, y]);
            }

            return CapSession(new EntityData
            {
                SceneName = hm.SceneName,
                HeatmapSource = source,
                SceneVersion = Application.version,
                Cols = cols,
                Rows = rows,
                CellSize = cellSize,
                OriginX = origin.x,
                OriginZ = origin.y,
                CountsFlat = flat,
                TimestampUtc = DateTime.UtcNow.ToString("o")
            });
        }
    }
}
