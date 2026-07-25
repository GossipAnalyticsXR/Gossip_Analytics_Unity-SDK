using UnityEngine;
using GossipSDK.Tracking.GameplayMetrics;
using GossipSDK.Core;
using System;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class PositionTrackerComponent : GossipBasicComponent
    {
        [Header("Sampling")]
        [SerializeField] private float sampleInterval = 0.15f;

        [Header("Thresholds")]
        [SerializeField] private float minStepMeters = 0.75f;  // gate de distancia
        [SerializeField] private float maxSilenceTime = 3f;     // heartbeat (presencia si esta quieto)

        private Vector3 lastLoggedPos;
        private float lastSampleTime;
        private float lastSentTime;

        private void Start()
        {
            lastLoggedPos = transform.position;
            lastSampleTime = Time.time;
            lastSentTime = Time.time;
            Emit(); // primer punto al entrar
        }

        private void Update()
        {
            float now = Time.time;
            if (now - lastSampleTime < sampleInterval) return;

            Vector3 pos = transform.position;
            bool moved = Vector3.Distance(pos, lastLoggedPos) >= minStepMeters;
            bool heartbeat = (now - lastSentTime) >= maxSilenceTime;

            if (moved || heartbeat) Emit();

            lastSampleTime = now;
        }

        private void Emit()
        {
            Vector3 pos = transform.position;
            Gossip.Instance?.PositionTracker?.CapSession(new PositionTracker.EntityData
            {
                X = pos.x, Y = pos.y, Z = pos.z,      // Y se conserva (3D / vuelo)
                TimestampUtc = DateTime.UtcNow.ToString("o")
            });
            lastLoggedPos = pos;
            lastSentTime = Time.time;
        }
    }
}
