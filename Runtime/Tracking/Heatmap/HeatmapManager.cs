using System;
using UnityEngine;

namespace GossipSDK.Tracking
{
    [Serializable]
    public class HeatmapManager
    {
        private readonly float cellSize;
        private readonly int cols;
        private readonly int rows;
        private readonly Vector2 origin;
        private readonly int[,] counts;
        public int HitsRegistered { get; private set; }
        public int HitsDiscarded { get; private set; }
        private readonly object lockObj = new object();

        public string SceneName { get; }

        public HeatmapManager(
            string sceneName,
            Vector2 worldMinXZ,
            Vector2 worldMaxXZ,
            float cellSizeMeters)
        {
            SceneName = sceneName;
            cellSize = Mathf.Max(0.05f, cellSizeMeters);

            origin = worldMinXZ;

            float width = Mathf.Max(0.1f, worldMaxXZ.x - worldMinXZ.x);
            float height = Mathf.Max(0.1f, worldMaxXZ.y - worldMinXZ.y);

            cols = Mathf.CeilToInt(width / cellSize);
            rows = Mathf.CeilToInt(height / cellSize);

            counts = new int[cols, rows];
        }

        public bool RegisterHit(Vector3 worldPos)
        {
            int cx = Mathf.FloorToInt((worldPos.x - origin.x) / cellSize);
            int cy = Mathf.FloorToInt((worldPos.z - origin.y) / cellSize);

            if (cx < 0 || cy < 0 || cx >= cols || cy >= rows)
HitsDiscarded++;
            return false;

            lock (lockObj)
            {
                counts[cx, cy]++;
                HitsRegistered++;
            }

            return true;
        }

        public int[,] GetCountsCopy()
        {
            lock (lockObj)
            {
                var copy = new int[cols, rows];
                Array.Copy(counts, copy, counts.Length);
                return copy;
            }
        }

        public (int cols, int rows, float cellSize, Vector2 origin) GetGridInfo()
        {
            return (cols, rows, cellSize, origin);
        }
    }
}
