using UnityEngine;
using GossipSDK.Core;
using GossipSDK.Tracking.GameplayMetrics;

[DisallowMultipleComponent]
public class PassthroughComponent : MonoBehaviour
{
    public bool onActiveStart = false;

    [Header("Reporting")]
    public bool sendImmediately = true;

    [Tooltip("Optional mode label (e.g. 'Full', 'Mixed', 'Color').")]
    public string passthroughMode = "Default";

    [Tooltip("Optional exposure value to send when reporting (nullable).")]
    public float exposure = 0f;

    [Tooltip("Optional quality metric (0..1) to send when reporting (nullable).")]
    [Range(0f, 1f)]
    public float qualityMetric = 1f;

    private bool isActive = false;
    private float activeTimer = 0f;

    private PassthroughTracker Tracker => Gossip.Instance?.PassthroughTracker;

    private void Start()
    {
        if(onActiveStart)
            OnPassthroughEnabled();
    }

    private void Update()
    {
        if (isActive) activeTimer += Time.deltaTime;
    }

    public void OnPassthroughEnabled()
    {
        if (isActive) return;
        isActive = true;
        activeTimer = 0f;
        if (Gossip.Instance?.Settings?.EnableDebug == true)
            Debug.Log("[PassthroughComponent] Passthrough enabled.");
    }

    public void OnPassthroughDisabled()
    {
        if (!isActive) return;
        isActive = false;
        float duration = activeTimer;
        activeTimer = 0f;

        ReportPassthrough(false, duration);
    }

    private void OnApplicationQuit()
    {
        if (isActive)
        {
            OnPassthroughDisabled();
        }
    }

    private void OnDisable()
    {
        if (isActive)
        {
            OnPassthroughDisabled();
        }
    }

    private void OnDestroy()
    {
        if (isActive)
        {
            OnPassthroughDisabled();
        }
    }

    public void ReportPassthrough(bool enabled, float duration = 0f)
    {
        if (Tracker == null)
        {
            Debug.LogWarning("[PassthroughComponent] PassthroughTracker not available.");
            return;
        }

        Tracker.CapPassthrough(enabled, passthroughMode, exposure > 0f ? (float?)exposure : null, qualityMetric > 0f ? (float?)qualityMetric : null);

        if (sendImmediately)
            Tracker.SendDataToSocket();

        if (Gossip.Instance?.Settings?.EnableDebug == true)
            Debug.Log($"[PassthroughComponent] Reported passthrough. enabled={enabled} duration={duration:F2}s mode={passthroughMode}");
    }
}
