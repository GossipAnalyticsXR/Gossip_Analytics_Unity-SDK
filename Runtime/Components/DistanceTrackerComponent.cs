using System;
using UnityEngine;
using GossipSDK.Core;
using GossipSDK.Tracking.GameplayMetrics;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class DistanceTrackerComponent : MonoBehaviour
    {
        [SerializeField] private Transform playerTransform;
        [SerializeField] private float sampleInterval = 1f;
        [SerializeField] private float minDistanceThreshold = 0.01f;

        private float lastTime;

        private void Start()
        {
            lastTime = Time.time;

            if (playerTransform == null)
            {
                Debug.LogWarning("[DistanceTrackerComponent] PlayerTransform not assigned.");
            }
        }

        private void Update()
        {
            if (playerTransform == null)
                return;

            float now = Time.time;
            if (now - lastTime < sampleInterval)
                return;
            lastTime = now;

            float distance = Vector3.Distance(playerTransform.position, transform.position);

            if (distance < minDistanceThreshold)
                return;

            try
            {
                var tracker = Gossip.Instance?.DistanceTracker;
                if (tracker == null)
                    return;

                tracker.RecordDistance(
                    distance,
                    transform.position,
                    playerTransform.position
                );

                if (Gossip.Instance.Settings?.EnableDebug == true)
                {
                    Debug.Log($"[DistanceTrackerComponent] Player ↔ Object = {distance:F2}m");
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }
}
