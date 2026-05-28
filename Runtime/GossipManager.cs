using R3;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GossipSDK.Core;
using GossipSDK.Core.Configuration;
using GossipSDK.Components;
using GossipSDK.XR;
using GossipSDK.Tracking.GameplayMetrics;
using Cysharp.Threading.Tasks;
using UnityEngine.Android;

namespace GossipSDK
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRBootstrap))]
    public class GossipManager : MonoBehaviour
    {
        [Header("Tick Intervals")]
        [SerializeField] private float trackInterval = 2f;
        [SerializeField] private float deployInterval = 5f;

        [Header("Settings")]
        [SerializeField] private GossipSettings settings;

        private readonly List<GossipBasicComponent> gossipComponents = new List<GossipBasicComponent>();
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        private Gossip Gossip => Gossip.Instance;
        private bool heatmapCreated;

        private bool isTrackRoutineRunning = false;
        private bool isDeployRoutineRunning = false;

        private void OnEnable()
        {
            if (settings == null)
            {
                Debug.LogError("[GossipManager] GossipSettings not assigned. Please drag your GossipAnalyticsSettings asset into the Settings field on GossipManager.");
                return;
            }
            Gossip.Initialize(settings);
            StartCoroutine(WaitForPermissionsBeforeStart());
        }

        private void StartSubscriptions()
        {
            disposables.Clear();

            if (trackInterval > 0)
            {
                Observable.Interval(TimeSpan.FromSeconds(trackInterval))
                    .Subscribe(_ =>
                    {
                        if (!isTrackRoutineRunning) StartCoroutine(OnTrackRoutine());
                    })
                    .AddTo(disposables);
            }

            if (deployInterval > 0)
            {
                Observable.Interval(TimeSpan.FromSeconds(deployInterval))
                    .Subscribe(_ =>
                    {
                        if (!isDeployRoutineRunning) StartCoroutine(OnDeployRoutine());
                    })
                    .AddTo(disposables);
            }
        }

        private IEnumerator OnTrackRoutine()
        {
            if (Gossip == null) yield break;
            isTrackRoutineRunning = true;

            for (int i = gossipComponents.Count - 1; i >= 0; i--)
            {
                var component = gossipComponents[i];
                if (component == null) { gossipComponents.RemoveAt(i); continue; }

                TrackComponent(component);

                if (i % 25 == 0) yield return null;
            }
            isTrackRoutineRunning = false;
        }

        private IEnumerator OnDeployRoutine()
        {
            if (Gossip == null) yield break;
            isDeployRoutineRunning = true;

            foreach (var component in gossipComponents)
            {
                if (component != null) DeployTrackedComponent(component);
                yield return null;
            }

            yield return StartCoroutine(DeploySystemTrackersStepByStep());
            isDeployRoutineRunning = false;
        }

        private IEnumerator DeploySystemTrackersStepByStep()
        {
            var trackers = GetTrackersList();

            foreach (var tracker in trackers)
            {
                if (tracker is IGossipTracker gossipTracker)
                {
                    if (gossipTracker.GetPendingCount() > 0)
                    {
                        gossipTracker.SendDataToSocket();
                        yield return null;
                    }
                }
            }
        }

        private List<object> GetTrackersList()
        {
            return new List<object>
            {
                Gossip.PlatformTracker, Gossip.BatteryTracker, Gossip.MemoryTracker,
                Gossip.NetworkUsageTracker, Gossip.InteractionTracker, Gossip.HeatmapTracker,
                Gossip.MistakeTracker, Gossip.DistanceTracker, Gossip.AvatarTracker,
                Gossip.DifficultyTracker, Gossip.PauseTracker, Gossip.MultiplayerTracker,
                Gossip.PassthroughTracker, Gossip.AccessoriesTracker, Gossip.ExperienceInfoTracker,
                Gossip.AdTracker, Gossip.ConnectivityTracker, Gossip.AudioVolumeTracker,
                Gossip.InputUsageTracker, Gossip.EyeTrackingTracker, Gossip.AudioReactionTracker,
                Gossip.PeripheralTracker, Gossip.RealityModeTracker, Gossip.PlayableAreaTracker,
                Gossip.UserPostureTracker, Gossip.UserEventTracker, Gossip.UserBalanceTracker,
                Gossip.HandControllerTracker
            };
        }

        private void TrackComponent(GossipBasicComponent component)
        {
            if (Gossip == null) return;
            switch (component)
            {
                case PositionTrackerComponent c:
                    var pos = c.transform.position;
                    Gossip.PositionTracker?.CapSession(new PositionTracker.EntityData { X = pos.x, Y = pos.y, Z = pos.z });
                    break;
                case RotationAndVelocityTrackerComponent rotComp:
                    if (Gossip?.Settings?.EnableDebug == true) Debug.Log($"Tracking {rotComp.name}");
                    break;
            }
        }

        private void DeployTrackedComponent(GossipBasicComponent component)
        {
            if (component is PositionTrackerComponent) Gossip?.PositionTracker?.SendDataToSocket();
            else if (component is RotationAndVelocityTrackerComponent) Gossip?.RotationTracker?.SendDataToSocket();
        }

        public void RegisterComponent(GossipBasicComponent component) => gossipComponents.Add(component);

        private IEnumerator WaitForPermissionsBeforeStart()
        {
            yield return new WaitUntil(() => VRPermissionsHandler.IsReady);

            StartSubscriptions();
            WaitAndCreateHeatmap().Forget();
        }

        private async UniTaskVoid WaitAndCreateHeatmap()
        {
            if (Gossip == null || Gossip.Instance.Settings == null)
                return;
            if (Gossip.Instance.Settings.SelectedEnvironment != Core.Configuration.GossipSettings.Environment.Production)
                return;

            await UniTask.WaitUntil(() => Gossip != null && Gossip.IsSessionReady);

            if (heatmapCreated)
                return;
            heatmapCreated = true;
            var go = new GameObject("GossipHeatmapSceneCapture");
            DontDestroyOnLoad(go);
            go.AddComponent<GossipSDK.Heatmaps.HeatmapSceneAutoCapture>();
        }


        public bool UnregisterComponent(GossipBasicComponent component)
        {
            return component != null && gossipComponents.Remove(component);
        }

        private void OnDisable() => disposables.Clear();
        private void OnDestroy() { disposables.Dispose(); gossipComponents.Clear(); }
    }

    public interface IGossipTracker
    {
        int GetPendingCount();
        void SendDataToSocket();
    }
}
