using System;
using UnityEngine;
using GossipSDK.Core;
using GossipSDK.Tracking.GameplayMetrics;

[DisallowMultipleComponent]
public class PauseComponent : MonoBehaviour
{
    [Tooltip("If true, request immediate send after cap (use sparingly)")]
    public bool sendImmediately = false;

    [Tooltip("Min duration (seconds) to consider as a pause when sending a non-zero duration")]
    public double minPauseSeconds = 0.05;

    private PauseTracker tracker => Gossip.Instance?.PauseTracker;

    private double pauseStartRealtime = -1.0;

    private bool isPausedLocal = false;

    public void OnPause()
    {
        try
        {
            if (tracker == null)
            {
                Debug.LogWarning("[PauseComponent] PauseTracker not available.");
            }

            if (isPausedLocal)
            {
                if (Gossip.Instance?.Settings?.EnableDebug == true)
                    Debug.Log("[PauseComponent] OnPause called but already paused - ignoring.");
                return;
            }

            pauseStartRealtime = Time.realtimeSinceStartupAsDouble;
            isPausedLocal = true;

            tracker?.CapPauseEvent("pause", 0.0);
            if (sendImmediately) tracker?.SendDataToSocket();

            if (Gossip.Instance?.Settings?.EnableDebug == true)
                Debug.Log("[PauseComponent] CapSession pause (start stored).");
        }
        catch (Exception ex) { Debug.LogException(ex); }
    }

    public void OnResume()
    {
        try
        {
            double duration = 0.0;

            if (isPausedLocal && pauseStartRealtime >= 0.0)
            {
                var now = Time.realtimeSinceStartupAsDouble;
                duration = now - pauseStartRealtime;

                if (duration < minPauseSeconds) duration = 0.0;
            }

            pauseStartRealtime = -1.0;
            isPausedLocal = false;

            if (tracker == null)
            {
                Debug.LogWarning("[PauseComponent] PauseTracker not available for resume.");
            }

            tracker?.CapPauseEvent("resume", duration);
            if (sendImmediately) tracker?.SendDataToSocket();

            if (Gossip.Instance?.Settings?.EnableDebug == true)
                Debug.Log($"[PauseComponent] CapSession resume (duration={duration:F3}s).");
        }
        catch (Exception ex) { Debug.LogException(ex); }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) OnPause(); else OnResume();
    }

    [ContextMenu("Simulate Pause")]
    public void SimulatePause() => OnPause();

    [ContextMenu("Simulate Resume")]
    public void SimulateResume() => OnResume();
}
