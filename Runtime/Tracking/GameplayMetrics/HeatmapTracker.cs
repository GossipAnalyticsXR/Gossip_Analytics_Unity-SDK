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

            [JsonProperty("TimestampUtc")] public string TimestampUtc { get; set; }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }

        public void CapFromHeatmap(
            Tracking.HeatmapManager hm,
            string heatmapSource,
            bool sparse = true,
            bool rowMajor = true)
        {
            if (Gossip.Instance?.Settings?.EnableHeatmaps == false)
            {
                if (Gossip.Instance?.Settings?.EnableDebug == true)
                {
                    Debug.Log("[HeatmapTracker] Heatmaps disabled by settings");
                }
                return;
            }

            if (hm == null) return;

            try
            {
                var (cols, rows, cellSize, origin) = hm.GetGridInfo();
                var matrix = hm.GetCountsCopy();

                if (sparse)
                {
                    CapSparse(hm, heatmapSource, cols, rows, cellSize, origin, matrix);
                }
                else
                {
                    CapDense(hm, heatmapSource, cols, rows, cellSize, origin, matrix, rowMajor);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception(
                    $"[HeatmapTracker] Capture failed ({heatmapSource})", ex));
            }
        }

        private void CapSparse(
            Tracking.HeatmapManager hm,
            string source,
            int cols,
            int rows,
            float cellSize,
            Vector2 origin,
            int[,] matrix)
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

            CapSession(new EntityData
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
                TimestampUtc = DateTime.UtcNow.ToString("o")
            });
        }

        private void CapDense(
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

            CapSession(new EntityData
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
