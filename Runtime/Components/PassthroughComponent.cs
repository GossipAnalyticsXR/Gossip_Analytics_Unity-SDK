using UnityEngine;
using System.Collections;
using System.Reflection;
using GossipSDK.Core;
using GossipSDK.Tracking.GameplayMetrics;
using GossipSDK.Core.Utilities;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class PassthroughComponent : MonoBehaviour
{
    public bool onActiveStart = false;
    public bool enableAutoDetect = false;
    public bool enablePassthroughDiag = false;
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

    // -- Auto-detection reflection cache ----------------------------------------
    private PropertyInfo _instanceProp;
    private PropertyInfo _ptEnabledProp;
    private MethodInfo _getBoundaryVisibilityMi;
    private float _pollAccumulator = 0f;
    private float _diagTimer        = 0f;

    private PassthroughTracker Tracker => Gossip.Instance?.PassthroughTracker;

    private void Start()
    {
        CacheAutoDetectTypes();
        StartCoroutine(WaitAndInit());
    }

    private IEnumerator WaitAndInit()
    {
        yield return new WaitUntil(() => Gossip.Instance != null);
        if (onActiveStart) OnPassthroughEnabled();
    }

    private void Update()
    {
        if (isActive) activeTimer += Time.deltaTime;

        if (enableAutoDetect)
        {
            // -- Auto-detection: throttled poll every ~0.3 s ----------------------
            _pollAccumulator += Time.deltaTime;
            if (_pollAccumulator >= 0.3f)
            {
                _pollAccumulator = 0f;

                bool ptEnabled = false;
                bool boundaryVisible = false;

                // CASO 2: OVRManager.instance.isInsightPassthroughEnabled
                try
                {
                    if (_instanceProp != null && _ptEnabledProp != null)
                    {
                        object ovrInstance = _instanceProp.GetValue(null);
                        if (ovrInstance != null)
                            ptEnabled = (bool)_ptEnabledProp.GetValue(ovrInstance);
                    }
                }
                catch { /* reflection failure -- leave ptEnabled false */ }

                // CASO 3: OVRPlugin.GetBoundaryVisibility(out BoundaryVisibility) static
                try
                {
                    if (_getBoundaryVisibilityMi != null)
                    {
                        object[] args = new object[] { null };
                        _getBoundaryVisibilityMi.Invoke(null, args);
                        object bv = args[0];
                        boundaryVisible = (bv != null && System.Convert.ToInt32(bv) == 1);
                    }
                }
                catch { /* reflection failure -- leave boundaryVisible false */ }

                bool passthroughActive = ptEnabled || boundaryVisible;

                if (passthroughActive && !isActive)
                {
                    passthroughMode = ptEnabled ? "App" : "Boundary";
                    if (Gossip.Instance?.Settings?.EnableDebug == true)
                        Debug.Log("[PassthroughComponent] auto-detect: passthrough ON mode=" + passthroughMode);
                    OnPassthroughEnabled();
                }
                else if (!passthroughActive && isActive)
                {
                    if (Gossip.Instance?.Settings?.EnableDebug == true)
                        Debug.Log("[PassthroughComponent] auto-detect: passthrough OFF");
                    OnPassthroughDisabled();
                }
            }
        } // end if (enableAutoDetect)

        if (enablePassthroughDiag)
        {
            _diagTimer += Time.deltaTime;
            if (_diagTimer >= 1f)
            {
                _diagTimer = 0f;
                int ovrMgr   = (_instanceProp != null) ? 1 : 0;
                int ovrPlugin = (_getBoundaryVisibilityMi != null) ? 1 : 0;
                string insightPT = "n/a";
                if (_instanceProp != null && _ptEnabledProp != null)
                {
                    try
                    {
                        object inst = _instanceProp.GetValue(null);
                        if (inst != null)
                            insightPT = ((bool)_ptEnabledProp.GetValue(inst)) ? "1" : "0";
                    }
                    catch { }
                }
                string boundaryVis = "n/a";
                if (_getBoundaryVisibilityMi != null)
                {
                    try
                    {
                        object[] args = new object[] { null };
                        _getBoundaryVisibilityMi.Invoke(null, args);
                        object bv = args[0];
                        boundaryVis = (bv != null) ? System.Convert.ToInt32(bv).ToString() : "n/a";
                    }
                    catch { }
                }
                string camClear = "n/a";
                string camAlpha = "n/a";
                if (Camera.main != null)
                {
                    camClear = Camera.main.clearFlags.ToString();
                    camAlpha = Camera.main.backgroundColor.a.ToString("F2");
                }
                UnityEngine.Debug.LogWarning(
                    "[PT:diag] ovrMgr=" + ovrMgr +
                    " ovrPlugin=" + ovrPlugin +
                    " insightPT=" + insightPT +
                    " boundaryVis=" + boundaryVis +
                    " camClear=" + camClear +
                    " camAlpha=" + camAlpha);
            }
        }
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
            Debug.Log("[PassthroughComponent] Reported passthrough. enabled=" + enabled + " mode=" + passthroughMode);
    }

    // -- Auto-detection type cache -----------------------------------------------
    // Caches OVRManager and OVRPlugin reflection handles once at Start().
    // If Meta XR SDK is absent, all handles remain null and detection is skipped.

    private void CacheAutoDetectTypes()
    {
        try
        {
            System.Type ovrManagerType = ReflectionUtil.FindType("OVRManager");
            if (ovrManagerType != null)
            {
                _instanceProp = ovrManagerType.GetProperty(
                    "instance",
                    BindingFlags.Public | BindingFlags.Static);
                _ptEnabledProp = ovrManagerType.GetProperty(
                    "isInsightPassthroughEnabled",
                    BindingFlags.Public | BindingFlags.Instance);
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogWarning(
                "[PassthroughComponent] CacheAutoDetect OVRManager failed: " + ex.Message);
        }

        try
        {
            System.Type ovrPluginType = ReflectionUtil.FindType("OVRPlugin");
            if (ovrPluginType != null)
            {
                _getBoundaryVisibilityMi = ovrPluginType.GetMethod(
                    "GetBoundaryVisibility",
                    BindingFlags.Public | BindingFlags.Static);
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogWarning(
                "[PassthroughComponent] CacheAutoDetect OVRPlugin failed: " + ex.Message);
        }
    }

}
