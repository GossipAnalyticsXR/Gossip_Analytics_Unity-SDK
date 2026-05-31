using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Android; // Necesario para verificar permiso sin pedirlo
using GossipSDK.Core;
using GossipSDK.Tracking.GameplayMetrics;
using GossipSDK.Tracking;
using UnityEngine.SceneManagement;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class AudioReactionTrackerComponent : MonoBehaviour
    {
        [Header("Recording")]
        public int sampleRate = 16000;
        public float bufferSeconds = 3f;
        public float analysisWindowSeconds = 0.3f;

        [Header("Decision thresholds")]
        [Range(0, 1)] public float minEmotionalScore = 0.6f;
        public float cooldownSeconds = 6f;

        [Header("Heatmap Settings")]
        public Transform trackedTransform;
        public Vector2 worldMinXZ = new Vector2(-25, -25);
        public Vector2 worldMaxXZ = new Vector2(25, 25);
        public float cellSizeMeters = 0.5f;
        public float heatmapFlushInterval = 10f;

        [Header("Scoring Weights (must sum to 1.0)")]
        [SerializeField] private float scoreWeightEnergy = 0.4f;
        [SerializeField] private float scoreWeightVoice = 0.3f;
        [SerializeField] private float scoreWeightMovement = 0.3f;

        [Header("Signal Gate Thresholds")]
        [SerializeField] private float signalThresholdEnergy = 0.5f;
        [SerializeField] private float signalThresholdVoice = 0.4f;
        [SerializeField] private float signalThresholdMovement = 0.4f;

        [Header("Normalisation Ceilings")]
        [SerializeField] private float rmsNormCeiling = 0.1f;
        [SerializeField] private float movementSpeedCeiling = 2f;

        private HeatmapManager heatmapManager;
        private AudioClip micClip;
        private float[] ringBuffer;
        private int ringIndex;
        private int bufferSize;
        private int analysisWindowSize;

        private float baselineRms = 0.01f;
        private float lastTriggerTime;
        private float heatmapTimer;
        private float _currentMovementIntensity = 0f;
        private Vector3 _lastTrackedPos;

        void Start()
        {
            bufferSize = Mathf.CeilToInt(sampleRate * bufferSeconds);
            analysisWindowSize = Mathf.CeilToInt(sampleRate * analysisWindowSeconds);
            ringBuffer = new float[bufferSize];

            heatmapManager = new HeatmapManager(
                SceneManager.GetActiveScene().name,
                worldMinXZ,
                worldMaxXZ,
                cellSizeMeters
            );

            if (trackedTransform == null)
                trackedTransform = Camera.main?.transform ?? transform;
            _lastTrackedPos = trackedTransform != null ? trackedTransform.position : Vector3.zero;

            StartCoroutine(InitializeMicrophone());
        }

        void Update()
        {
            // Si no hay clip o no está grabando, no hacemos nada
            if (micClip == null || Microphone.devices.Length == 0 || !Microphone.IsRecording(Microphone.devices[0]))
                return;

            heatmapTimer += Time.deltaTime;

            int micPos = Microphone.GetPosition(null);
            if (micPos <= 0)
                return;

            int readSize = Mathf.Min(256, micPos);
            float[] temp = new float[readSize];

            int offset = Mathf.Max(0, micPos - readSize);
            micClip.GetData(temp, offset);

            foreach (var s in temp)
            {
                ringBuffer[ringIndex] = s;
                ringIndex = (ringIndex + 1) % bufferSize;
            }

            AnalyzeWindow();

            if (heatmapTimer >= heatmapFlushInterval)
            {
                FlushHeatmap();
                heatmapTimer = 0f;
            }

            if (trackedTransform != null)
            {
                float delta = Vector3.Distance(trackedTransform.position, _lastTrackedPos)
                               / Mathf.Max(Time.deltaTime, 0.001f);
                _currentMovementIntensity = Mathf.Clamp01(delta / movementSpeedCeiling);
                _lastTrackedPos = trackedTransform.position;
            }
        }

        private void FlushHeatmap()
        {
            var tracker = Gossip.Instance?.HeatmapTracker;
            if (tracker == null || heatmapManager == null)
                return;

            tracker.CapFromHeatmap(
                heatmapManager,
                heatmapSource: "audio_reaction",
                sparse: true
            );
        }

        IEnumerator InitializeMicrophone()
        {
            // 1. ESPERAR AL GESTOR CENTRAL
            yield return new WaitUntil(() => VRPermissionsHandler.IsReady);

            // 2. VERIFICACIÓN DE SEGURIDAD
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                Debug.LogWarning("[AudioTracker] Sin permiso de micrófono. Abortando.");
                yield break; 
            }
#endif

            // 3. RE-DETECCIÓN DINÁMICA DE DISPOSITIVOS
            float retryTime = 0;
            while (Microphone.devices.Length == 0 && retryTime < 2.0f)
            {
                yield return new WaitForSecondsRealtime(0.2f);
                retryTime += 0.2f;
            }

            if (Microphone.devices.Length == 0)
            {
                Debug.LogError("[AudioTracker] No se detectó ningún micrófono físico disponible.");
                yield break;
            }

            // 4. INICIO CON SELECCIÓN EXPLÍCITA
            string deviceName = Microphone.devices[0];
            bool success = false;

            try
            {
                micClip = Microphone.Start(deviceName, true, 10, sampleRate);
                success = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AudioTracker] Fallo crítico al iniciar grabación: {e.Message}");
                success = false;
            }

            // El yield return debe estar FUERA del bloque try-catch
            if (success)
            {
                yield return new WaitUntil(() => Microphone.GetPosition(deviceName) > 0);
                Debug.Log($"[AudioTracker] Micrófono '{deviceName}' iniciado y capturando.");
            }
        }

        void AnalyzeWindow()
        {
            float[] window = new float[analysisWindowSize];

            for (int i = 0; i < analysisWindowSize; i++)
            {
                int idx = (ringIndex - i + bufferSize) % bufferSize;
                window[i] = ringBuffer[idx];
            }

            float rms = ComputeRMS(window);
            float quality = ComputeQuality(window);

            float voiceChange = Mathf.Clamp01((rms - baselineRms) / Mathf.Max(baselineRms, 0.001f));
            float V_eff = voiceChange * quality;

            float E = Mathf.Clamp01(rms / rmsNormCeiling);
            float M = _currentMovementIntensity;

            float score = scoreWeightEnergy * E + scoreWeightVoice * V_eff + scoreWeightMovement * M;

            int signals = 0;
            if (E >= signalThresholdEnergy)
                signals++;
            if (V_eff >= signalThresholdVoice)
                signals++;
            if (M >= signalThresholdMovement)
                signals++;

            if (Time.time < lastTriggerTime + cooldownSeconds)
                return;

            if ((signals >= 2 && score >= minEmotionalScore) ||
                (E >= 0.8f && (V_eff >= 0.35f || M >= 0.35f)))
            {
                TriggerSnippet(E, voiceChange, quality, M, score, signals);
            }
            else
            {
                baselineRms = Mathf.Lerp(baselineRms, rms, 0.02f);
            }
        }

        async void TriggerSnippet(float E, float V, float Qv, float M, float score, int signals)
        {
            if (trackedTransform == null) return;
            lastTriggerTime = Time.time;

            heatmapManager.RegisterHit(trackedTransform.position);

            float[] snippet = new float[bufferSize];

            int idx = ringIndex;
            for (int i = 0; i < bufferSize; i++)
            {
                snippet[i] = ringBuffer[idx];
                idx = (idx + 1) % bufferSize;
            }

            byte[] audio = FloatToWav(snippet, sampleRate);

            string sceneName = SceneManager.GetActiveScene().name;

            var tracker = Gossip.Instance?.AudioReactionTracker;
            if (tracker == null)
                return;

            var data = new AudioReactionTracker.EntityData
            {
                EventSeverity = E,
                VoiceChange = V,
                VoiceQuality = Qv,
                MovementIntensity = M,
                EmotionalScore = score,
                TriggerMode = signals == 3 ? "3_of_3" : "2_of_3",
                SceneName = sceneName,
                TimestampUtc = DateTime.UtcNow.ToString("o")
            };

            await Gossip.Instance.EndpointClient.UploadAudioReaction(
                data,
                audio,
                success =>
                {
                    if (Gossip.Instance.Settings.EnableDebug)
                        Debug.Log(success
                            ? "[AudioReaction] Uploaded via endpoint"
                            : "[AudioReaction] Upload failed");
                });


            Debug.LogWarning("[AudioTracker] AUDIO SNIPPET TRIGGERED");
        }

        float ComputeRMS(float[] samples)
        {
            double sum = 0;
            foreach (var s in samples)
                sum += s * s;
            return Mathf.Sqrt((float)(sum / samples.Length));
        }

        float ComputeQuality(float[] samples)
        {
            int clipped = 0;
            foreach (var s in samples)
                if (Mathf.Abs(s) > 0.98f)
                    clipped++;

            return 1f - Mathf.Clamp01((float)clipped / samples.Length);
        }

        byte[] FloatToWav(float[] samples, int sampleRate)
        {
            short[] pcm = new short[samples.Length];
            for (int i = 0; i < samples.Length; i++)
                pcm[i] = (short)Mathf.Clamp(samples[i] * short.MaxValue, short.MinValue, short.MaxValue);

            byte[] pcmBytes = new byte[pcm.Length * 2];
            Buffer.BlockCopy(pcm, 0, pcmBytes, 0, pcmBytes.Length);

            using (var stream = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(stream))
            {
                int byteRate = sampleRate * 2;

                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + pcmBytes.Length);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

                writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write((short)2);
                writer.Write((short)16);

                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(pcmBytes.Length);
                writer.Write(pcmBytes);

                return stream.ToArray();
            }
        }

    }
}
