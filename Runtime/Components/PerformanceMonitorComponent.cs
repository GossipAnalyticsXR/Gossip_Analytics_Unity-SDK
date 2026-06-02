using System.Collections;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using GossipSDK.Core;
using GossipSDK.Tracking.GameplayMetrics;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class PerformanceMonitorComponent : MonoBehaviour
    {
        [Tooltip("Seconds between samples")]
        [SerializeField] private float sampleInterval = 5f;

        [Header("Freeze Detection")]
        [SerializeField] private float freezeFpsThreshold       = 10f;
        [SerializeField] private int   freezeConsecutiveSamples = 2;

        private int   _consecutiveLowFpsSamples = 0;
        private float _lowFpsStartTime          = 0f;

        private float lastTime;
        private int frames;
        private float fpsTimer;

        private void Start()
        {
            if (Gossip.Instance == null)
            {
                enabled = false;
                StartCoroutine(WaitAndSend());
                return;
            }
            lastTime = Time.realtimeSinceStartup;
            frames = 0;
            fpsTimer = 0f;
        }

        private IEnumerator WaitAndSend()
        {
            yield return new WaitUntil(() => Gossip.Instance != null);
            enabled = true;
            lastTime = Time.realtimeSinceStartup;
            frames = 0;
            fpsTimer = 0f;
        }

        private void Update()
        {
            frames++;
            fpsTimer += Time.unscaledDeltaTime;

            float now = Time.realtimeSinceStartup;
            if (now - lastTime < sampleInterval) return;

            float currentFps = fpsTimer > 0f ? frames / fpsTimer : 0f;
            SampleAndCap(currentFps);

            // reset counters
            lastTime = now;
            frames = 0;
            fpsTimer = 0f;
        }

        private void SampleAndCap(float currentFps)
        {
            try
            {
                long totalAllocated = 0;
                long totalReserved = 0;
                long monoUsed = 0;

                // Profiler APIs can throw on some platforms, keep fallbacks
                try { totalAllocated = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(); } catch { totalAllocated = GC.GetTotalMemory(false); }
                try { totalReserved = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong(); } catch { totalReserved = totalAllocated; }
                try { monoUsed = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong(); } catch { monoUsed = 0; }

                var data = new MemoryTracker.EntityData
                {
                    TotalAllocatedBytes = totalAllocated,
                    TotalReservedBytes = totalReserved,
                    MonoUsedBytes = monoUsed,
                    GcCollectionsGen0 = GC.CollectionCount(0),
                    GcCollectionsGen1 = GC.CollectionCount(1),
                    GcCollectionsGen2 = GC.CollectionCount(2),
                    CurrentFPS = currentFps,
                    TimestampUtc = DateTime.UtcNow.ToString("o")
                };

                // Prefer direct property if available
                var memTracker = Gossip.Instance?.MemoryTracker;
                if (memTracker != null)
                {
                    memTracker.CapSession(data);
                }
                else
                {
                    if (Gossip.Instance?.Settings?.EnableDebug == true)
                        Debug.LogWarning("[PerformanceMonitor] MemoryTracker not available (null).");
                }

                if (Gossip.Instance?.Settings?.EnableDebug == true)
                {
                    Debug.Log($"[PerformanceMonitor] CapSession memAlloc={totalAllocated} reserved={totalReserved} mono={monoUsed} fps={currentFps:F1}");
                }

                // ----- Freeze detection -----
                FreezeTracker freezeTracker = Gossip.Instance?.FreezeTracker;
                if (currentFps > 0f && currentFps < freezeFpsThreshold)
                {
                    _consecutiveLowFpsSamples++;
                    if (_consecutiveLowFpsSamples == 1) _lowFpsStartTime = Time.realtimeSinceStartup;
                    if (_consecutiveLowFpsSamples >= freezeConsecutiveSamples && freezeTracker != null)
                    {
                        float durationMs = (Time.realtimeSinceStartup - _lowFpsStartTime) * 1000f;
                        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                        freezeTracker.CapFreeze(currentFps, durationMs, scene);
                        _consecutiveLowFpsSamples = 0;
                    }
                }
                else
                {
                    _consecutiveLowFpsSamples = 0;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }
}
