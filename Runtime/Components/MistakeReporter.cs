using System;
using UnityEngine;
using GossipSDK.Core;
using GossipSDK.Tracking.GameplayMetrics;


[DisallowMultipleComponent]
public class MistakeReporter : MonoBehaviour
{
    public bool autoReportOnStart = true;
    public string mistakeType = "WrongAction";

    public int severity = 0;

    private MistakeTracker tracker;

    private void Awake()
    {
        try
        {
            tracker = Gossip.Instance?.GetType().GetProperty("MistakeTracker")?.GetValue(Gossip.Instance) as MistakeTracker;
        }
        catch { tracker = null; }

        if (tracker == null)
        {
            tracker = new MistakeTracker();
        }
    }

    private void Start()
    {
        if (autoReportOnStart)
            ReportMistake(this.gameObject, "Demo Mistake");
    }

    public void ReportMistake(GameObject obj,string customType = null, int? customSeverity = null)
    {
        try
        {
            var pos = obj.transform.position;
            var data = new MistakeTracker.EntityData
            {
                ObjectName = obj.name,
                ObjectTag = obj.tag,
                MistakeType = string.IsNullOrEmpty(customType) ? mistakeType : customType,
                Severity = customSeverity ?? severity,
                X = pos.x,
                Y = pos.y,
                Z = pos.z,
                SceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                TimestampUtc = DateTime.UtcNow.ToString("o")
            };

            tracker.CapSession(data);

            if (Gossip.Instance?.Settings?.EnableDebug == true)
                Debug.Log($"[MistakeReporter] Reported mistake: {data.MistakeType} obj={data.ObjectName} pos=({data.X:F2},{data.Y:F2},{data.Z:F2})");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }
}
