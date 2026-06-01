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

        [SerializeField] private float swayMagnitudeCeiling = 0.05f;
        private Vector3 lastPosition;
        private float lastSampleTime;
        private bool started;

        private float _swayFreqTimer = 0f;
        private int _swayDirectionChanges = 0;
        private float _lastSwayDeltaX = 0f;
        private float _measuredSwayFrequency = 0f;
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

            float deltaX = currentPos.x - lastPosition.x;
            if (Mathf.Abs(deltaX) > 0.001f && Mathf.Sign(deltaX) != Mathf.Sign(_lastSwayDeltaX))
                _swayDirectionChanges++;
            _lastSwayDeltaX = deltaX;
            _swayFreqTimer += dt;
            if (_swayFreqTimer >= 1f)
            {
                _measuredSwayFrequency = _swayDirectionChanges / 2f;
                _swayDirectionChanges = 0;
                _swayFreqTimer = 0f;
            }

            float rawSwayMagnitude = Vector3.Distance(currentPos, lastPosition);
            float swayMagnitude = Mathf.Clamp01(rawSwayMagnitude / swayMagnitudeCeiling);

            float swayFrequency = _measuredSwayFrequency;

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
