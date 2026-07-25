using R3;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
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
    [DefaultExecutionOrder(-100)]
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

        private static GossipManager _instance;

        private bool autoTrackersInitialized;
        private GameObject autoTrackersHost;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

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
                Gossip.HandControllerTracker,
                Gossip.BoundaryPressureSummaryTracker,
                Gossip.MicPermissionTracker
            };
        }

        private void TrackComponent(GossipBasicComponent component)
        {
            if (Gossip == null) return;
            switch (component)
            {
                case PositionTrackerComponent:
                    // La captura de posicion ahora la hace PositionTrackerComponent.Update() (gate de distancia). No capturar aqui.
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
            WaitAndAutoAddTrackers().Forget();
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

        private async UniTaskVoid WaitAndAutoAddTrackers()
        {
            await UniTask.WaitUntil(() => Gossip != null && Gossip.IsSessionReady);

            if (autoTrackersInitialized) return;
            autoTrackersInitialized = true;

            autoTrackersHost = new GameObject("GossipAutoTrackers");
            DontDestroyOnLoad(autoTrackersHost);

            SceneManager.sceneLoaded += (_, __) => EnsureTrackers();

            EnsureTrackers();
        }

        private void EnsureTrackers()
        {
            var cam = Camera.main;
            var camGO = cam != null ? cam.gameObject : null;

            void Ensure(System.Type t, GameObject host)
            {
                if (host == null) return;
                if (UnityEngine.Object.FindObjectOfType(t) == null)
                    host.AddComponent(t);
            }

            // GROUP A: own-transform readers -- attach to Camera GO
            Ensure(typeof(PositionTrackerComponent),            camGO);
            Ensure(typeof(RotationAndVelocityTrackerComponent), camGO);
            Ensure(typeof(UserBalanceTrackerComponent),         camGO);
            Ensure(typeof(PlayerMovementHeatmapComponent),      camGO);

            // GROUP B: all others -- attach to persistent autoTrackersHost
            Ensure(typeof(UserPostureComponent),                autoTrackersHost);
            Ensure(typeof(EyeTrackingComponent),                autoTrackersHost);
            Ensure(typeof(AudioReactionTrackerComponent),       autoTrackersHost);
            Ensure(typeof(DistanceTrackerComponent),            autoTrackersHost);
            Ensure(typeof(HandControllerTrackingComponent),     autoTrackersHost);
            Ensure(typeof(PerformanceMonitorComponent),         autoTrackersHost);
            Ensure(typeof(BatteryMonitorComponent),             autoTrackersHost);
            Ensure(typeof(ConnectivityMonitorComponent),        autoTrackersHost);
            Ensure(typeof(InputUsageTrackerComponent),          autoTrackersHost);
            Ensure(typeof(AudioVolumeTrackerComponent),         autoTrackersHost);
            Ensure(typeof(PlatformMonitorComponent),            autoTrackersHost);
            Ensure(typeof(RealityModeMonitor),                  autoTrackersHost);
            Ensure(typeof(ExperienceInfoComponent),             autoTrackersHost);
            Ensure(typeof(PlayableAreaComponent),               autoTrackersHost);
            Ensure(typeof(PeripheralAutoTrackerComponent),      autoTrackersHost);
            Ensure(typeof(PauseComponent),                      autoTrackersHost);
            Ensure(typeof(DifficultyComponent),                 autoTrackersHost);
            Ensure(typeof(MultiplayerTrackerComponent),         autoTrackersHost);
            Ensure(typeof(ServerStatusComponent),               autoTrackersHost);
            Ensure(typeof(AvatarTrackerComponent),              autoTrackersHost);
            Ensure(typeof(AccessoriesComponent),                autoTrackersHost);
            Ensure(typeof(CrashReporterComponent),              autoTrackersHost);
            Ensure(typeof(AdComponent),                         autoTrackersHost);
            Ensure(typeof(PassthroughComponent),                autoTrackersHost);
            Ensure(typeof(BoundaryPressureComponent), autoTrackersHost);
            Ensure(typeof(MicPermissionComponent), autoTrackersHost);

            // Camera wiring: refresh to current Camera.main on every call
            var posture = UnityEngine.Object.FindObjectOfType<UserPostureComponent>();
            if (posture != null && cam != null) posture.headTransform = cam.transform;

            var eye = UnityEngine.Object.FindObjectOfType<EyeTrackingComponent>();
            if (eye != null && cam != null) eye.cam = cam.transform;

            var audioRx = UnityEngine.Object.FindObjectOfType<AudioReactionTrackerComponent>();
            if (audioRx != null && cam != null) audioRx.trackedTransform = cam.transform;

            var dist = UnityEngine.Object.FindObjectOfType<DistanceTrackerComponent>();
            if (dist != null && cam != null) dist.PlayerTransform = cam.transform;
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
