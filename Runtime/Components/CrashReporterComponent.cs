using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using GossipSDK.Core;
using GossipSDK.Tracking.GameplayMetrics;

[DisallowMultipleComponent]
public class CrashReporterComponent : MonoBehaviour
{
    [Header("Capture Settings")]
    [SerializeField] private bool captureExceptions = true;
    [SerializeField] private bool captureErrors     = false;

    private CrashTracker Tracker => Gossip.Instance?.CrashTracker;

    private bool _ready = false;

    private void OnEnable()  { StartCoroutine(WaitAndSubscribe()); }
    private void OnDisable() { Application.logMessageReceived -= OnLog; _ready = false; }

    private IEnumerator WaitAndSubscribe()
    {
        yield return new WaitUntil(() => Gossip.Instance != null);
        Application.logMessageReceived += OnLog;
        _ready = true;
    }

    private void OnLog(string logString, string stackTrace, LogType type)
    {
        if (!_ready) return;
        if (Tracker == null) return;

        bool isException = type == LogType.Exception && captureExceptions;
        bool isError     = type == LogType.Error     && captureErrors;
        if (!isException && !isError) return;

        string scene = SceneManager.GetActiveScene().name;
        Tracker.CapCrash(type.ToString(), logString, stackTrace, scene);
    }
}
