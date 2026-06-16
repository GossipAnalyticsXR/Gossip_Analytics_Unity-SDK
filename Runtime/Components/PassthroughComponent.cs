using UnityEngine;
using System.Collections;
using System.Reflection;
using GossipSDK.Core;
using GossipSDK.Tracking.GameplayMetrics;
using UnityEngine.SceneManagement;

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
        StartCoroutine(WaitAndInit());
    }

    private IEnumerator WaitAndInit()
    {
        yield return new WaitUntil(() => Gossip.Instance != null);
        TrySubscribeGuardianBoundary();
        if (onActiveStart) OnPassthroughEnabled();
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
        UnsubscribeGuardianBoundary();
    }

    private void OnDestroy()
    {
        if (isActive)
        {
            OnPassthroughDisabled();
        }
        UnsubscribeGuardianBoundary();
    }

    public void ReportPassthrough(bool enabled, float duration = 0f)
    {
        if (Tracker == null)
        {
            Debug.LogWarning("[PassthroughComponent] PassthroughTracker not available.");
            return;
        }

        Tracker.CapPassthrough(
            enabled,
            passthroughMode,
            exposure > 0f ? (float?)exposure : null,
            qualityMetric > 0f ? (float?)qualityMetric : null,
            duration > 0f ? (float?)duration : null,
            gameObject.name,
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

        if (sendImmediately)
            Tracker.SendDataToSocket();

        if (Gossip.Instance?.Settings?.EnableDebug == true)
            Debug.Log($"[PassthroughComponent] Reported passthrough. enabled={enabled} duration={duration:F2}s mode={passthroughMode}");
        }

    // ── Guardian-boundary passthrough (system signal) ─────────────────────
    // Uses reflection so OVR types are never referenced directly;
    // compiles cleanly when Meta XR SDK is absent.

    private System.Reflection.EventInfo  _guardianEvtInfo;
    private System.Delegate              _guardianHandler;

    private void TrySubscribeGuardianBoundary()
    {
        try
        {
            var ovrManagerType = System.Type.GetType(
                "OVRManager, OVRPlugin", throwOnError: false);
            if (ovrManagerType == null) return;          // Meta XR SDK absent

            _guardianEvtInfo = ovrManagerType.GetEvent(
                "BoundaryVisibilityChanged",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static);
            if (_guardianEvtInfo == null) return;        // API not available

            var handlerType  = _guardianEvtInfo.EventHandlerType;
            var handlerMethod = typeof(PassthroughComponent)
                .GetMethod("OnGuardianBoundaryChanged",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
            if (handlerMethod == null) return;

            _guardianHandler = System.Delegate.CreateDelegate(
                handlerType, this, handlerMethod);
            _guardianEvtInfo.AddEventHandler(null, _guardianHandler);
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogWarning(
                "[PassthroughComponent] Guardian-boundary subscription failed: " + ex.Message);
        }
    }

    private void UnsubscribeGuardianBoundary()
    {
        if (_guardianEvtInfo != null && _guardianHandler != null)
        {
            try { _guardianEvtInfo.RemoveEventHandler(null, _guardianHandler); }
            catch { /* best-effort */ }
            _guardianEvtInfo  = null;
            _guardianHandler  = null;
        }
    }

    // Called by the reflected delegate when OVR guardian boundary visibility changes.
    // visible=true  => player has left/is approaching the guardian boundary (system passthrough on).
    // visible=false => player back in safe zone (system passthrough off).
    private void OnGuardianBoundaryChanged(bool visible)
    {
        if (Tracker == null) return;
        Tracker.CapPassthrough(
            enabled:    visible,
            mode:       "system_guardian",
            objectName: gameObject.name,
            sceneName:  UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

        if (sendImmediately)
            Tracker.SendDataToSocket();

        if (Gossip.Instance?.Settings?.EnableDebug == true)
            UnityEngine.Debug.Log(
                $"[PassthroughComponent] Guardian boundary visible={visible} => passthrough Source=system_guardian");
    }

}
