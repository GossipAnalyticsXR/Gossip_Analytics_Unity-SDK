using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using GossipSDK.Core;
using GossipSDK.Tracking.Conectivity;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class ConnectivityMonitorComponent : MonoBehaviour
    {
        [Header("Speed Test")]
        public string testFileUrl = "https://speed.cloudflare.com/__down?bytes=200000";
        public float periodicTestInterval = 0f;
        public int speedTestTimeoutSeconds = 5;

        private NetworkReachability lastReachability;
        private Coroutine periodicRoutine;
        private float _lastSpeedTestTime = -999f;
        private const float SpeedTestCooldownSeconds = 30f;

        private void Start()
        {
            lastReachability = Application.internetReachability;
            if (Gossip.Instance == null)
            {
                StartCoroutine(WaitAndSend());
                return;
            }
            StartCoroutine(SendConnectivitySnapshot());

            if (periodicTestInterval > 0)
                periodicRoutine = StartCoroutine(PeriodicCheck());
        }

        private IEnumerator WaitAndSend()
        {
            yield return new WaitUntil(() => Gossip.Instance != null);
            yield return SendConnectivitySnapshot();
        }

        private void Update()
        {
            if (Application.internetReachability != lastReachability)
            {
                lastReachability = Application.internetReachability;
                StartCoroutine(SendConnectivitySnapshot());
            }
        }

        private IEnumerator PeriodicCheck()
        {
            while (true)
            {
                yield return new WaitForSeconds(periodicTestInterval);
                yield return SendConnectivitySnapshot();
            }
        }

        private IEnumerator SendConnectivitySnapshot()
        {
            bool isOnline = Application.internetReachability != NetworkReachability.NotReachable;
            string connType = GetConnectionType(Application.internetReachability);

            float? mbps = null;
            if (isOnline)
            bool runSpeedTest = (Time.realtimeSinceStartup - _lastSpeedTestTime) >= SpeedTestCooldownSeconds;
            if (runSpeedTest)
            {
                _lastSpeedTestTime = Time.realtimeSinceStartup;
                yield return MeasureDownloadSpeed(v => mbps = v);
            }
            else
            {
                mbps = -1f;
            }

            var tracker = Gossip.Instance?.ConnectivityTracker;
            if (tracker == null)
                yield break;

            var data = new ConnectivityTracker.EntityData
            {
                ConnectionType = connType,
                IsOnline = isOnline,
                DownloadMbps = mbps,
                Reachability = Application.internetReachability.ToString(),
                SceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                TimestampUtc = DateTime.UtcNow.ToString("o")
            };

            tracker.CapConnectivity(data);

            if (Gossip.Instance?.Settings?.EnableDebug == true)
            {
                Debug.Log($"[Connectivity] {connType} | Online={isOnline} | Mbps={mbps?.ToString("F2") ?? "N/A"}");
            }
        }

        private string GetConnectionType(NetworkReachability reachability)
        {
            return reachability switch
            {
                NetworkReachability.ReachableViaLocalAreaNetwork => "WiFi",
                NetworkReachability.ReachableViaCarrierDataNetwork => "Mobile",
                _ => "None"
            };
        }

        private IEnumerator MeasureDownloadSpeed(Action<float> onResult)
        {
            float startTime = Time.realtimeSinceStartup;
            using var request = UnityWebRequest.Get(testFileUrl);
            request.timeout = speedTestTimeoutSeconds;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onResult?.Invoke(0f);
                yield break;
            }

            float duration = Time.realtimeSinceStartup - startTime;

            float mbps = (request.downloadedBytes * 8f) / (duration * 1_000_000f);
            onResult?.Invoke(Mathf.Max(0f, mbps));
        }

        private void OnDestroy()
        {
            if (periodicRoutine != null)
                StopCoroutine(periodicRoutine);
        }

        private void OnApplicationQuit()
        {
            if (periodicRoutine != null)
                StopCoroutine(periodicRoutine);
        }
    }
}
