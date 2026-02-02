using System;
using UnityEngine;
using GossipSDK.Core;
using GossipSDK.Tracking.GameplayMetrics;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class UserBalanceTrackerComponent : MonoBehaviour
    {
        [SerializeField] private float sampleInterval = 0.5f;
        [SerializeField] private string postureState = "";

        private Vector3 lastPosition;
        private float lastSampleTime;
        private bool started;

        private void Start()
        {
            lastPosition = transform.position;
            lastSampleTime = Time.time;
            started = true;
        }

        private void Update()
        {

            if (!started)
                return;

            float now = Time.time;
            float dt = now - lastSampleTime;
            if (dt < sampleInterval)
                return;

            Vector3 currentPos = transform.position;

            float swayMagnitude = Vector3.Distance(currentPos, lastPosition);

            float swayFrequency = dt > 0f ? 1f / dt : 0f;

            lastPosition = currentPos;
            lastSampleTime = now;

            try
            {
                var tracker = Gossip.Instance?.UserBalanceTracker;
                if (tracker == null)
                    return;

                tracker.CaptureSample(
                    currentPos,
                    swayMagnitude,
                    swayFrequency,
                    postureState
                );

                if (Gossip.Instance?.Settings?.EnableDebug == true)
                {
                    Debug.Log(
                        $"[UserBalanceTrackerComponent] COP={currentPos} " +
                        $"mag={swayMagnitude:F3} freq={swayFrequency:F2}"
                    );
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
        public void SetPostureState(string state)
        {
            postureState = state ?? string.Empty;
        }
    }
}
