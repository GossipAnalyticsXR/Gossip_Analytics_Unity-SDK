using System;
using UnityEngine;
using GossipSDK.Core;
using GossipSDK.Tracking.GameplayMetrics;

[DisallowMultipleComponent]
public class DifficultyComponent : MonoBehaviour
{
    public bool autoReportOnStart = true;

    public bool sendImmediately = false;

    public string defaultDifficultyId = "normal";
    public float defaultNumeric = 0.5f;

    public void Start()
    {
        if(autoReportOnStart)
            NotifyDifficulty(defaultDifficultyId, defaultNumeric, "start");
    }

    public void NotifyDifficulty(string difficultyId, float numericValue = 0f, string reason = "player_selected")
    {
        try
        {
            var tracker = Gossip.Instance?.DifficultyTracker;
            if (tracker == null)
            {
                Debug.LogWarning("[DifficultyComponent] DifficultyTracker not available on Gossip.");
                return;
            }

            tracker.CapDifficulty(difficultyId, numericValue, reason);

            if (sendImmediately)
            {
                tracker.SendDataToSocket();
            }

            if (Gossip.Instance?.Settings?.EnableDebug == true)
                Debug.Log($"[DifficultyComponent] Notified difficulty '{difficultyId}' value={numericValue} reason={reason}");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }
}
