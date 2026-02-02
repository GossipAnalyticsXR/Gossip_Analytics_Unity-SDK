using System;
using UnityEngine;
using UnityEngine.UI;
using GossipSDK.Core;
using GossipSDK.Tracking.Conectivity;
using Cysharp.Threading.Tasks;

[DisallowMultipleComponent]
public class ServerStatusComponent : MonoBehaviour
{
    [Tooltip("Optional UI Text to show server status (if not set, will use Debug.Log)")]
    public Text statusText;

    [Tooltip("Check on Start")]
    public bool checkOnStart = true;

    [Tooltip("Interval in seconds to re-check server status (0 = disabled)")]
    public float pollInterval = 30f;

    private ServerStatusTracker tracker;
    private void Awake()
    {
        tracker = Gossip.Instance?.ServerStatusTracker;
        if (tracker == null)
        {
            Debug.LogWarning("[ServerStatusComponent] No ServerStatusTracker available on Gossip.");
        }
    }

    private void OnEnable()
    {
        if (tracker != null)
            tracker.OnStatusUpdated += HandleStatus;
    }

    private void OnDisable()
    {
        if (tracker != null)
            tracker.OnStatusUpdated -= HandleStatus;
    }

    private async void Start()
    {
        if (checkOnStart)
            await DoCheck();

        if (pollInterval > 0f)
            PollLoop().Forget();
    }

    private async UniTaskVoid PollLoop()
    {
        while (enabled)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(pollInterval));
            await DoCheck();
        }
    }

    private async UniTask DoCheck()
    {
        if (tracker == null) return;
        await tracker.CheckServerAsync();
    }

    private void HandleStatus(GossipSDK.Tracking.Conectivity.ServerStatusTracker.EntityData e)
    {
        string msg = $"Server: {e.ServerName} | Status: {e.Status} | Ping: {(e.PingMs?.ToString() ?? "--")} ms | Load: {(e.LoadPercent?.ToString("F1") ?? "--")}%";

        if (statusText != null)
        {
            statusText.text = msg;
        }
        else
        {
            Debug.Log("[ServerStatusComponent] " + msg);
        }
    }

    public void ShowTemporaryMessage(string txt, float seconds = 3f)
    {
        if (statusText != null)
        {
            statusText.text = txt;
        }
        else
        {
            Debug.Log(txt);
        }
    }
}