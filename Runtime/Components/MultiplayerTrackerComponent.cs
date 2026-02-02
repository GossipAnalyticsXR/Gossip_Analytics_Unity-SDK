using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GossipSDK.Core;
using GossipSDK.Tracking.GameplayMetrics;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class MultiplayerTrackerComponent : MonoBehaviour
    {
        [Header("Match Info")]
        public string roomId;
        public string matchType;

        [Header("Tracking")]
        public float snapshotIntervalSeconds = 10f;
        public bool autoReportOnStart = true;

        private Coroutine snapshotRoutine;

        void Start()
        {
            if (autoReportOnStart)
            {
                StartTracking();
            }
        }

        void OnDestroy()
        {
            StopTracking();
            CaptureSnapshot();
        }

        public void StartTracking()
        {
            if (snapshotRoutine != null)
                return;

            snapshotRoutine = StartCoroutine(SnapshotLoop());
        }

        public void StopTracking()
        {
            if (snapshotRoutine != null)
            {
                StopCoroutine(snapshotRoutine);
                snapshotRoutine = null;
            }
        }

        IEnumerator SnapshotLoop()
        {
            while (true)
            {
                CaptureSnapshot();
                yield return new WaitForSeconds(snapshotIntervalSeconds);
            }
        }

        public void CaptureSnapshot()
        {
            var tracker = Gossip.Instance?.MultiplayerTracker;
            if (tracker == null)
                return;

            List<MultiplayerTracker.PlayerInfo> players = CollectPlayers();

            tracker.CapMatchSnapshot(
                roomId,
                matchType,
                players
            );
        }

        protected virtual List<MultiplayerTracker.PlayerInfo> CollectPlayers()
        {
            var list = new List<MultiplayerTracker.PlayerInfo>();

            list.Add(new MultiplayerTracker.PlayerInfo
            {
                PlayerId = SystemInfo.deviceUniqueIdentifier,
                DisplayName = "LocalPlayer",
                PingMs = null,
                Meta = new Dictionary<string, string>
                {
                    { "platform", Application.platform.ToString() }
                }
            });

            return list;
        }

        public void OnPlayerJoined()
        {
            CaptureSnapshot();
        }

        public void OnPlayerLeft()
        {
            CaptureSnapshot();
        }
    }
}
