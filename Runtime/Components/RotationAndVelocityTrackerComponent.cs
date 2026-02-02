using UnityEngine;
using GossipSDK.Components;
using GossipSDK.Tracking.GameplayMetrics;
using GossipSDK.Core;
using System;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class RotationAndVelocityTrackerComponent : GossipBasicComponent
    {
        [Header("Sampling")]
        [SerializeField] private float sampleInterval = 0.2f;

        [Header("Thresholds")]
        [SerializeField] private float minAngularDelta = 3f;   // degrees
        [SerializeField] private float minSpeedDelta = 0.05f;  // m/s
        [SerializeField] private float maxSilenceTime = 2.5f;  // heartbeat

        private Vector3 lastPosition;
        private Quaternion lastRotation;
        private float lastSampleTime;

        // Last SENT values (important)
        private float lastSentSpeed;
        private float lastSentAngularSpeed;
        private Quaternion lastSentRotation;
        private float lastSentTime;

        private void Start()
        {
            lastPosition = transform.position;
            lastRotation = transform.rotation;
            lastSampleTime = Time.time;

            lastSentRotation = transform.rotation;
            lastSentSpeed = 0f;
            lastSentAngularSpeed = 0f;
            lastSentTime = Time.time;
        }

        private void Update()
        {
            float now = Time.time;
            float dt = now - lastSampleTime;
            if (dt < sampleInterval)
                return;

            Vector3 pos = transform.position;
            Quaternion rot = transform.rotation;

            // -------- SPEED --------
            Vector3 deltaPos = pos - lastPosition;
            float speed = deltaPos.magnitude / dt;

            // -------- ANGULAR SPEED --------
            Quaternion deltaRot = rot * Quaternion.Inverse(lastRotation);
            deltaRot.ToAngleAxis(out float angle, out _);
            if (angle > 180f)
                angle = 360f - angle;
            float angularSpeed = Mathf.Abs(angle) / dt;

            bool significantRotation =
                Quaternion.Angle(rot, lastSentRotation) >= minAngularDelta;

            bool significantSpeedChange =
                Mathf.Abs(speed - lastSentSpeed) >= minSpeedDelta;

            bool heartbeat =
                (now - lastSentTime) >= maxSilenceTime;

            if (significantRotation || significantSpeedChange || heartbeat)
            {
                var entity = new RotationTracker.EntityData
                {
                    RotX = rot.eulerAngles.x,
                    RotY = rot.eulerAngles.y,
                    RotZ = rot.eulerAngles.z,
                    Speed = speed,
                    AngularSpeed = angularSpeed,
                    TimestampUtc = DateTime.UtcNow.ToString("o"),
                    ObjectName = gameObject.name
                };

                Gossip.Instance?.RotationTracker?.CapSession(entity);

                if (Gossip.Instance?.Settings?.EnableDebug == true)
                {
                    Debug.Log(
                        $"[RotationAndVelocity] SEND {gameObject.name} " +
                        $"speed={speed:F2} ang={angularSpeed:F1} rotΔ={Quaternion.Angle(rot, lastSentRotation):F1}"
                    );
                }

                lastSentRotation = rot;
                lastSentSpeed = speed;
                lastSentAngularSpeed = angularSpeed;
                lastSentTime = now;
            }

            lastPosition = pos;
            lastRotation = rot;
            lastSampleTime = now;
        }
    }
}
