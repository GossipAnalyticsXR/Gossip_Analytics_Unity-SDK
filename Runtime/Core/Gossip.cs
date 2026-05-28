using UnityEngine;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using System;
using UnityEngine.SceneManagement;

using GossipSDK.Core.Configuration;
using GossipSDK.Core.Utilities;
using GossipSDK.Tracking.GameplayMetrics;
using GossipSDK.Tracking.PlatformSpecification;
using GossipSDK.Core.Connection;
using GossipSDK.Tracking;
using GossipSDK.Core.Transport;
using GossipSDK.Tracking.Conectivity;
using GossipSDK.Tracking.UserInformation;
using GossipSDK.Tracking.A11y;

namespace GossipSDK.Core
{
    public class Gossip : MonoBehaviourSingleton<Gossip>
    {
        private GossipSettings settings;
        public GossipSettings Settings => settings;

        public static void Initialize(GossipSettings injectedSettings)
        {
            if (Instance != null) Instance.settings = injectedSettings;
        }

        public ITransport Transport { get; private set; }

        public EndpointConnection EndpointClient { get; private set; }

        public PositionTracker PositionTracker { get; private set; }
        public SessionTracker SessionTracker { get; private set; }
        public PerformanceTracker PerformanceTracker { get; private set; }
        public RotationTracker RotationTracker { get; private set; }
        public PlatformTracker PlatformTracker { get; private set; }
        public BatteryTracker BatteryTracker { get; private set; }
        public MemoryTracker MemoryTracker { get; private set; }
        public InteractionTracker InteractionTracker { get; private set; }
        public DistanceTracker DistanceTracker { get; private set; }
        public MistakeTracker MistakeTracker { get; private set; }
        public AvatarTracker AvatarTracker { get; private set; }
        public DifficultyTracker DifficultyTracker { get; private set; }
        public PauseTracker PauseTracker { get; private set; }
        public ServerStatusTracker ServerStatusTracker { get; private set; }
        public AccessoriesTracker AccessoriesTracker { get; private set; }
        public MultiplayerTracker MultiplayerTracker { get; private set; }
        public PassthroughTracker PassthroughTracker { get; private set; }
        public ExperienceInfoTracker ExperienceInfoTracker { get; private set; }
        public AdTracker AdTracker { get; private set; }
        public ConnectivityTracker ConnectivityTracker { get; private set; }
        public RealityModeTracker RealityModeTracker { get; private set; }
        public PlayableAreaTracker PlayableAreaTracker { get; private set; }
        public AudioVolumeTracker AudioVolumeTracker { get; private set; }
        public InputUsageTracker InputUsageTracker { get; private set; }
        public EyeTrackingTracker EyeTrackingTracker { get; private set; }
        public AudioReactionTracker AudioReactionTracker { get; private set; }
        public HandControllerTracker HandControllerTracker { get; private set; }
        public A11yTracker A11yTracker { get; private set; }

        public UserInfoTracker UserInfoTracker { get; private set; }
        public UserPostureTracker UserPostureTracker { get; private set; }
        public UserEventTracker UserEventTracker { get; private set; }
        public UserBalanceTracker UserBalanceTracker { get; private set; }
        public PeripheralTracker PeripheralTracker { get; private set; }

        public NetworkUsageTracker NetworkUsageTracker { get; private set; }

        public string CurrentPlayerId { get; private set; }
        public string CurrentSessionId { get; private set; }

        public HeatmapTracker HeatmapTracker { get; private set; }
        public bool ApiKeyValid { get; private set; } = true;
        public bool IsSessionReady { get; private set; }


        private void Awake()
        {

            try
            {
                string effectiveServer = settings?.GetActiveServerUrl();
                string effectiveApiKey = settings?.ApiKeyValue;

                if (settings != null && settings.UseHttpEndpoint)
                {
                    if (string.IsNullOrWhiteSpace(effectiveApiKey))
                    {
                        Debug.LogWarning("[Gossip] API key is empty or null. Transport disabled — no data will be sent.");
                        Transport = null;
                        EndpointClient = null;
                        return;
                    }

                    EndpointClient = new EndpointConnection(settings.ApiKeyHeader, effectiveApiKey);
                    if (settings.EnableDebug)
                        Debug.Log($"[Gossip] Endpoint client prepared. Server: {effectiveServer}");
                }

            }
            catch (System.Exception ex)
            {
                Debug.LogException(new System.Exception("Gossip: Could not initialize EndpointClient", ex));
            }

            if (PositionTracker == null) PositionTracker = new PositionTracker();
            if (SessionTracker == null) SessionTracker = new SessionTracker();
            if (PerformanceTracker == null) PerformanceTracker = new PerformanceTracker();
            if (RotationTracker == null) RotationTracker = new RotationTracker();
            if (PlatformTracker == null) PlatformTracker = new PlatformTracker();
            if (BatteryTracker == null) BatteryTracker = new BatteryTracker();
            if (MemoryTracker == null) MemoryTracker = new MemoryTracker();
            if (InteractionTracker == null) InteractionTracker = new InteractionTracker();
            if (HeatmapTracker == null) HeatmapTracker = new HeatmapTracker();
            if (DistanceTracker == null) DistanceTracker = new DistanceTracker();
            if (MistakeTracker == null) MistakeTracker = new MistakeTracker();
            if (AvatarTracker == null) AvatarTracker = new AvatarTracker();
            if (DifficultyTracker == null) DifficultyTracker = new DifficultyTracker();
            if (PauseTracker == null) PauseTracker = new PauseTracker();
            if (ServerStatusTracker == null) ServerStatusTracker = new ServerStatusTracker();
            if (AccessoriesTracker == null) AccessoriesTracker = new AccessoriesTracker();
            if (MultiplayerTracker == null) MultiplayerTracker = new MultiplayerTracker();
            if (PassthroughTracker == null) PassthroughTracker = new PassthroughTracker();
            if (ExperienceInfoTracker == null) ExperienceInfoTracker = new ExperienceInfoTracker();
            if (AdTracker == null) AdTracker = new AdTracker();
            if (ConnectivityTracker == null) ConnectivityTracker = new ConnectivityTracker();
            if (RealityModeTracker == null) RealityModeTracker = new RealityModeTracker();
            if (PlayableAreaTracker == null) PlayableAreaTracker = new PlayableAreaTracker();
            if (AudioVolumeTracker == null) AudioVolumeTracker = new AudioVolumeTracker();
            if (InputUsageTracker == null) InputUsageTracker = new InputUsageTracker();
            if (EyeTrackingTracker == null) EyeTrackingTracker = new EyeTrackingTracker();
            if (AudioReactionTracker == null) AudioReactionTracker = new AudioReactionTracker();
            if(A11yTracker == null) A11yTracker = new A11yTracker();

            if (UserInfoTracker == null) UserInfoTracker = new UserInfoTracker();
            UserInfoTracker.CaptureOnce();
            if (!string.IsNullOrEmpty(Settings?.GetActiveServerUrl()))
            {
                UserInfoTracker.SendDataToSocket();
            }

            if (UserPostureTracker == null) UserPostureTracker = new UserPostureTracker();
            if (UserEventTracker == null) UserEventTracker = new UserEventTracker();
            if (UserBalanceTracker == null) UserBalanceTracker = new UserBalanceTracker();
            if (PeripheralTracker == null) PeripheralTracker = new PeripheralTracker();
            if (HandControllerTracker == null) HandControllerTracker = new HandControllerTracker();

            if (Settings != null && Settings.UseHttpEndpoint)
            {
                if (string.IsNullOrWhiteSpace(Settings.ApiKeyValue))
                {
                    Debug.LogWarning("[Gossip] Cannot register HTTP transport: ApiKeyValue is empty.");
                    return;
                }

                var transport = new EndpointTransport(Settings.ApiKeyHeader, Settings.ApiKeyValue, maxRetries: 2, timeoutSeconds: 10);
                RegisterTransport(transport);

                if (EndpointClient == null)
                    EndpointClient = new EndpointConnection(Settings.ApiKeyHeader, Settings.ApiKeyValue);

                if (Settings.EnableDebug)
                    Debug.Log("[Gossip] EndpointTransport registered (HTTP).");
            }
        }

        public void InvalidateApiKey(long responseCode)
        {
            if (!ApiKeyValid) return;

            ApiKeyValid = false;
            StopResendWorker();

            Debug.LogError(
                $"[Gossip] API KEY INVALID OR UNAUTHORIZED (HTTP {responseCode}). " +
                $"All tracking has been DISABLED."
            );
        }

        public void RegisterTransport(ITransport transport)
        {
            Transport = transport;
            if (Settings?.EnableDebug == true)
                Debug.Log("[Gossip] Transport registered.");
        }

        private void OnEnable()
        {
            if (!ApiKeyValid)
            {
                if (Settings?.EnableDebug == true)
                    Debug.LogWarning("[Gossip] SDK disabled due to invalid API key.");
                return;
            }

            StartResendWorker();
        }

        protected override void OnDestroy()
        {
            NetworkUsageTracker = null;
            UserPostureTracker = null;
            StopResendWorker();
        }

        public void SetCurrentIds(string playerId, string sessionId)
        {
            CurrentPlayerId = playerId;
            CurrentSessionId = sessionId;

            IsSessionReady = true;

            if (settings?.EnableDebug == true)
            {
                Debug.Log($"[Gossip] CurrentPlayerId='{CurrentPlayerId}' CurrentSessionId='{CurrentSessionId}'");
            }
        }

        public void RegisterSocketConnection(SocketConnection socketConnection)
        {
            if (socketConnection == null)
            {
                if (Settings?.EnableDebug == true)
                    Debug.Log("[Gossip] RegisterSocketConnection called with null - skipping NetworkUsageTracker creation.");
                return;
            }

            try
            {
                NetworkUsageTracker = new NetworkUsageTracker(socketConnection);

                if (Settings?.EnableDebug == true)
                    Debug.Log("[Gossip] NetworkUsageTracker registered.");
            }
            catch (System.Exception ex)
            {
                Debug.LogException(new System.Exception("Gossip.RegisterSocketConnection failed", ex));
            }
        }

        public string PlayerID => CurrentPlayerId;
        public string SessionID => CurrentSessionId;

        private float resendIntervalSeconds = 30f;

        private CancellationTokenSource resendCts;

        public void StartResendWorker()
        {
            if (string.IsNullOrEmpty(Settings?.GetActiveServerUrl())) return;
            StopResendWorker();

            resendCts = new CancellationTokenSource();
            RunResendLoopAsync(resendCts.Token).Forget();
        }

        public void StopResendWorker()
        {
            if (resendCts != null)
            {
                try { resendCts.Cancel(); } catch { }
                try { resendCts.Dispose(); } catch { }
                resendCts = null;
            }
        }

        private async UniTaskVoid RunResendLoopAsync(CancellationToken token)
        {
            if (Settings?.EnableDebug == true) Debug.Log("[Gossip] ResendWorker started.");
            try
            {
                var delayMs = Math.Max(1000, (int)(resendIntervalSeconds * 1000f));

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var trackers = new List<object>
                {
                    UserPostureTracker,
                    UserEventTracker,
                    UserBalanceTracker,
                    PositionTracker,
                    InteractionTracker,
                    HeatmapTracker,
                    PlatformTracker,
                    BatteryTracker,
                    MemoryTracker,
                    RotationTracker,
                    PerformanceTracker,
                    SessionTracker,
                    DistanceTracker,
                    MistakeTracker,
                    AvatarTracker,
                    DifficultyTracker,
                    PauseTracker,
                    ServerStatusTracker,
                    AccessoriesTracker,
                    MultiplayerTracker,
                    PassthroughTracker,
                    ExperienceInfoTracker,
                    AdTracker,
                    ConnectivityTracker,
                    AudioReactionTracker,
                    HandControllerTracker,
                    A11yTracker,
                    EyeTrackingTracker,
                    InputUsageTracker
                };

                        foreach (var t in trackers)
                        {
                            if (t == null) continue;
                            if (string.IsNullOrEmpty(Settings?.GetActiveServerUrl())) continue;

                            try
                            {
                                var tType = t.GetType();
                                var getPending = tType.GetMethod("GetPendingCount");
                                var sendMethod = tType.GetMethod("SendDataToSocket");

                                int pending = 0;
                                if (getPending != null)
                                {
                                    var pendingObj = getPending.Invoke(t, null);
                                    if (pendingObj is int i) pending = i;
                                    else if (pendingObj is long l) pending = (int)l;
                                }

                                if (pending > 0)
                                {
                                    if (Settings?.EnableDebug == true)
                                        Debug.Log($"[Gossip.ResendWorker] {tType.Name} pending={pending} -> sending");


                                    if (Settings?.UseHttpEndpoint == true && string.IsNullOrWhiteSpace(Settings.ApiKeyValue))
                                    {
                                        if (Settings.EnableDebug) Debug.Log("[Gossip.ResendWorker] HTTP endpoint enabled but ApiKey empty — skipping resend loop.");
                                        break;
                                    }
                                    sendMethod?.Invoke(t, null);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[Gossip.ResendWorker] error checking tracker {t.GetType().Name}: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(new Exception("[Gossip.ResendWorker] loop error", ex));
                    }

                    try
                    {
                        await UniTask.Delay(delayMs, cancellationToken: token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
            finally
            {
                if (Settings?.EnableDebug == true) Debug.Log("[Gossip] ResendWorker stopped.");
            }
        }
    }
}
