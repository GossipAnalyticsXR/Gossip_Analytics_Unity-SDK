using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Android; // Necesario para verificar permiso sin pedirlo
using GossipSDK.Core;
using GossipSDK.Tracking.GameplayMetrics;
using GossipSDK.Tracking;
using GossipSDK.Heatmaps;
using UnityEngine.SceneManagement;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class AudioReactionTrackerComponent : MonoBehaviour
    {
        [Header("Recording")]
        public int sampleRate = 16000;
        public float bufferSeconds = 3f;

        /// <summary>
        /// Segundos que se esperan TRAS el disparo antes de copiar el snippet.
        ///
        /// El buffer guarda siempre los ultimos bufferSeconds. Si se copia en el
        /// instante del disparo, el fichero TERMINA ahi: se graba el momento previo
        /// a la reaccion y la vocalizacion queda fuera. Medido sobre 140 clips, el
        /// pico caia en el ultimo 20% del clip en el 74% de los casos, contra un 20%
        /// por puro azar.
        ///
        /// Esperando 1,5 s la ventana pasa a ser [disparo-1,5 s, disparo+1,5 s] sin
        /// cambiar el tamano del buffer, ni la duracion del fichero, ni el coste.
        /// A 0 se comporta exactamente como antes.
        /// </summary>
        public float postTriggerSeconds = 1.5f;
        public float analysisWindowSeconds = 0.3f;

        [Header("Decision thresholds")]
        [Range(0, 1)] public float minEmotionalScore = 0.4f;
        public float cooldownSeconds = 6f;

        [Header("Heatmap Settings")]
        public Transform trackedTransform;
        public Vector2 worldMinXZ = new Vector2(-5, -5);
        public Vector2 worldMaxXZ = new Vector2(5, 5);
        public float cellSizeMeters = 0.5f;
        public float heatmapFlushInterval = 10f;

        [Header("Auto Bounds")]
        [SerializeField] public bool autoBounds = true;

        [Header("Scoring Weights (must sum to 1.0)")]
        [SerializeField] private float scoreWeightEnergy = 0.4f;
        [SerializeField] private float scoreWeightVoice = 0.3f;
        [SerializeField] private float scoreWeightMovement = 0.3f;

        [Header("Signal Gate Thresholds")]
        [SerializeField] private float signalThresholdEnergy = 0.3f;
        [SerializeField] private float signalThresholdVoice = 0.25f;
        [SerializeField] private float signalThresholdMovement = 0.25f;

        [Header("Fast Trigger Thresholds")]
        [SerializeField] private float fastTriggerEnergyThreshold = 0.5f;
        [SerializeField] private float fastTriggerConditionThreshold = 0.25f;

        [Header("Normalisation Ceilings")]
        [SerializeField] private float rmsNormCeiling = 0.1f;
        [SerializeField] private float movementSpeedCeiling = 1.5f;

        [Header("Baseline Adaptation")]
        [SerializeField] private float baselineLerpRate = 0.02f;

        /// <summary>
        /// Rango en dB sobre la linea base con el que se normaliza el cambio de voz.
        ///
        /// La formula anterior era un cociente lineal recortado a 1:
        ///     clamp01((rms - baselineRms) / baselineRms)
        /// que llega a 1 en cuanto rms dobla la linea base. Medido: V valia 1 en el
        /// 68% de los 140 clips viejos y en el 98% de los 112 nuevos, y el umbral
        /// V >= 0,25 se cumplia en el 100% de los clips de las TRES bandas
        /// acusticas, incluidas las inaudibles. Una senal que nunca se apaga no
        /// discrimina: es una constante disfrazada de medida.
        ///
        /// En dB no satura. Con span 12: 3 dB sobre la base dan V ~ 0,25 (el
        /// umbral), 6 dB dan 0,50 y hacen falta 12 dB para llegar a 1.
        ///
        /// A 0 se vuelve exactamente a la formula anterior, para poder revertir
        /// sin deshacer el commit.
        /// </summary>
        [SerializeField] private float voiceChangeSpanDb = 12f;

        /// <summary>
        /// Suelo de audibilidad absoluto, en dBFS. Por debajo de este nivel el
        /// "cambio de voz" no mide voz: mide el baseline derrumbandose en
        /// silencio, y cualquier crujido queda decenas de dB por encima de el.
        ///
        /// Medido el 05/09/2026 con gafas (99 muestras de diag, 11 clips): las
        /// reacciones reales dispararon entre -34 y -18 dBFS; el unico falso
        /// positivo de la sesion estaba a -79.7 dBFS. Con -60 quedan 20 dB de
        /// margen por debajo y 26 dB por encima.
        ///
        /// A -200 la puerta queda desactivada, para poder revertir el
        /// comportamiento desde el Inspector sin deshacer el commit.
        /// </summary>
        [SerializeField] private float voiceFloorDbfs = -60f;

        private HeatmapManager heatmapManager;
        private AudioClip micClip;
        private float[] ringBuffer;
        private int ringIndex;
        private int bufferSize;
        private int analysisWindowSize;
        private int lastMicPos = -1;

        // Disparo aplazado. El snippet se copia desde Update(), no con un await:
        // asi no hay nada que cancelar si el componente se desactiva y no se leen
        // objetos ya destruidos.
        private bool snippetPending;
        private float snippetDueTime;
        private string snippetSceneName;
        private float snippetE, snippetV, snippetQuality, snippetM, snippetScore;
        private int snippetSignals;

        private float baselineRms = 0.01f;
        private float _diagTimer = 0f;
        private float lastTriggerTime;
        private float heatmapTimer;
        private float _currentMovementIntensity = 0f;
        private Vector3 _lastTrackedPos;

        void Start()
        {
            if (!UnityEngine.XR.XRSettings.enabled)
            {
                Debug.LogWarning(
                    "[AudioReactionTracker] XR not active - tracker requires " +
                    "a headset for movement detection. Component disabled.", this);
                enabled = false;
                return;
            }
            bufferSize = Mathf.CeilToInt(sampleRate * bufferSeconds);
            analysisWindowSize = Mathf.CeilToInt(sampleRate * analysisWindowSeconds);
            ringBuffer = new float[bufferSize];

            if (autoBounds && HeatmapBoundsResolver.ResolveSceneBoundsXZ(
                out Vector2 resolvedMin, out Vector2 resolvedMax))
            {
                worldMinXZ = resolvedMin;
                worldMaxXZ = resolvedMax;
            }

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
            // Si no hay clip o no esta grabando, no hacemos nada
            if (micClip == null || Microphone.devices.Length == 0 || !Microphone.IsRecording(Microphone.devices[0]))
                return;

            heatmapTimer += Time.deltaTime;

            int micPos = Microphone.GetPosition(null);
            if (micPos < 0)
                return;

            int clipSamples = micClip.samples;

            // Primera vuelta: solo fijamos el cursor.
            if (lastMicPos < 0)
            {
                lastMicPos = micPos;
                return;
            }

            // Muestras nuevas desde la ultima lectura, contando la vuelta del clip.
            // Antes se cogian SIEMPRE las ultimas 256 sin cursor: a 16 kHz son 16 ms
            // exactos, asi que con frames mas largos se perdia audio y con frames mas
            // cortos se duplicaba. El buffer no era continuo.
            int available = micPos - lastMicPos;
            if (available < 0)
                available += clipSamples;
            if (available <= 0)
                return;

            // Si un frame largo dejo mas muestras de las que caben, nos quedamos con
            // las mas recientes: perder lo viejo es mejor que desordenar el buffer.
            // bufferSize podria superar la longitud del clip si alguien sube
            // bufferSeconds por encima de los 10 s con que se abre el microfono.
            int maxCatchUp = Mathf.Min(bufferSize, clipSamples);
            if (available > maxCatchUp)
            {
                lastMicPos = (micPos - maxCatchUp + clipSamples) % clipSamples;
                available = maxCatchUp;
            }

            // El clip del microfono es circular: puede hacer falta leer en dos tramos.
            int firstChunk = Mathf.Min(available, clipSamples - lastMicPos);
            AppendFromMic(lastMicPos, firstChunk);
            if (available > firstChunk)
                AppendFromMic(0, available - firstChunk);

            lastMicPos = (lastMicPos + available) % clipSamples;

            // Si hay un snippet esperando y ya le toca, se copia AQUI: el buffer
            // acaba de rellenarse este frame, asi que trae el audio mas reciente.
            if (snippetPending && Time.time >= snippetDueTime)
            {
                snippetPending = false;
                TriggerSnippet(snippetE, snippetV, snippetQuality, snippetM,
                               snippetScore, snippetSignals, snippetSceneName);
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

            // 2. VERIFICACION DE SEGURIDAD
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                Debug.LogWarning("[AudioTracker] Sin permiso de microfono. Abortando.");
                yield break; 
            }
#endif

            // 3. RE-DETECCION DINAMICA DE DISPOSITIVOS
            float retryTime = 0;
            while (Microphone.devices.Length == 0 && retryTime < 2.0f)
            {
                yield return new WaitForSecondsRealtime(0.2f);
                retryTime += 0.2f;
            }

            if (Microphone.devices.Length == 0)
            {
                Debug.LogError("[AudioTracker] No se detecto ningun microfono fisico disponible.");
                yield break;
            }

            // 4. INICIO CON SELECCION EXPLICITA
            string deviceName = Microphone.devices[0];
            bool success = false;

            try
            {
                micClip = Microphone.Start(deviceName, true, 10, sampleRate);
                lastMicPos = -1;
                success = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AudioTracker] Fallo critico al iniciar grabacion: {e.Message}");
                success = false;
            }

            // El yield return debe estar FUERA del bloque try-catch
            if (success)
            {
                yield return new WaitUntil(() => Microphone.GetPosition(deviceName) > 0);
                Debug.Log($"[AudioTracker] Microfono '{deviceName}' iniciado y capturando.");
            }
        }

        // Copia count muestras del clip del microfono, desde offset, al ring buffer.
        void AppendFromMic(int offset, int count)
        {
            if (count <= 0)
                return;

            float[] temp = new float[count];
            micClip.GetData(temp, offset);

            for (int i = 0; i < count; i++)
            {
                ringBuffer[ringIndex] = temp[i];
                ringIndex = (ringIndex + 1) % bufferSize;
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

            // Cuanto sube el nivel sobre la linea base, en dB. Es la magnitud que
            // se queria medir; el cociente lineal la aplastaba contra el techo.
            float baseRef = Mathf.Max(baselineRms, 1e-5f);
            float voiceChangeDb = 20f * Mathf.Log10(Mathf.Max(rms, 1e-7f) / baseRef);

            float voiceChange = voiceChangeSpanDb > 0f
                ? Mathf.Clamp01(voiceChangeDb / voiceChangeSpanDb)
                : Mathf.Clamp01((rms - baselineRms) / Mathf.Max(baselineRms, 0.001f));

            // Puerta acustica: sin nivel absoluto no hay voz que medir. Sin
            // esto, en silencio baselineRms cae hasta el suelo de 1e-5 y un
            // ruido de 1e-4 vale +20 dB, que con span 12 satura voiceChange a 1
            // y dispara un clip vacio.
            if (20f * Mathf.Log10(Mathf.Max(rms, 1e-7f)) < voiceFloorDbfs)
                voiceChange = 0f;

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
            _diagTimer += Time.deltaTime;
            if (Gossip.Instance?.Settings?.EnableDebug == true && _diagTimer >= 1f)
            {
                _diagTimer = 0f;
                int micPos = Microphone.GetPosition(Microphone.devices.Length > 0 ? Microphone.devices[0] : null);
                Debug.Log("[AudioReaction:diag] rawAmp=" + rms + " base=" + baselineRms + " vDb=" + voiceChangeDb + " E=" + E + " V=" + V_eff + " M=" + M + " score=" + score + " signals=" + signals + " micPos=" + micPos);
            }

                        if (Gossip.Instance?.Settings?.EnableDebug == true && (E > 0.1f || V_eff > 0.1f || M > 0.1f))
                Debug.Log($"[AudioReaction] E={E:F2} V={V_eff:F2} M={M:F2} score={score:F2} signals={signals} gates(E>={signalThresholdEnergy} V>={signalThresholdVoice} M>={signalThresholdMovement} minScore={minEmotionalScore} fastE>={fastTriggerEnergyThreshold})");

if (Time.time < lastTriggerTime + cooldownSeconds)
                return;

            if ((signals >= 2 && score >= minEmotionalScore) ||
                (E >= fastTriggerEnergyThreshold && (V_eff >= fastTriggerConditionThreshold || M >= fastTriggerConditionThreshold)))
            {
                ArmSnippet(E, voiceChange, quality, M, score, signals);
            }
            else
            {
                baselineRms = Mathf.Lerp(baselineRms, rms, baselineLerpRate);
            }
        }

        /// <summary>
        /// Marca el disparo y deja el snippet armado para copiarse mas tarde.
        ///
        /// Aqui va todo lo que depende del INSTANTE del disparo: el cooldown, el
        /// punto del heatmap y el nombre de la escena. Leerlos despues de la espera
        /// los falsearia si la escena cambia en ese segundo y medio.
        /// </summary>
        void ArmSnippet(float E, float V, float Qv, float M, float score, int signals)
        {
            if (trackedTransform == null) return;
            if (Gossip.Instance == null) return;

            lastTriggerTime = Time.time;
            heatmapManager.RegisterHit(trackedTransform.position);

            string sceneName = SceneManager.GetActiveScene().name;

            // Sin espera se copia ya: identico al comportamiento anterior.
            if (postTriggerSeconds <= 0f)
            {
                TriggerSnippet(E, V, Qv, M, score, signals, sceneName);
                return;
            }

            // Solo cabe un snippet a la vez. Con el cooldown por defecto (6 s) no
            // puede ocurrir, pero si alguien lo baja por debajo de postTriggerSeconds
            // esto evita que un disparo nuevo pise al que estaba esperando.
            if (snippetPending) return;

            snippetPending = true;
            snippetDueTime = Time.time + Mathf.Min(postTriggerSeconds, bufferSeconds);
            snippetSceneName = sceneName;
            snippetE = E;
            snippetV = V;
            snippetQuality = Qv;
            snippetM = M;
            snippetScore = score;
            snippetSignals = signals;
        }

        async void TriggerSnippet(float E, float V, float Qv, float M, float score,
                                  int signals, string sceneName)
        {
            // Se revalida: entre el disparo y esta copia ha pasado postTriggerSeconds
            // y el objeto o el SDK pueden haber desaparecido.
            if (trackedTransform == null) return;
            if (Gossip.Instance == null)
            {
                return;
            }

            float[] snippet = new float[bufferSize];

            int idx = ringIndex;
            for (int i = 0; i < bufferSize; i++)
            {
                snippet[i] = ringBuffer[idx];
                idx = (idx + 1) % bufferSize;
            }

            byte[] audio = FloatToWav(snippet, sampleRate);

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

            var endpoint = Gossip.Instance?.EndpointClient;

        GossipSDK.Core.Connection.EndpointConnection tempEndpoint = null;

        if (endpoint == null)
        {
            var s = Gossip.Instance?.Settings;

            if (s != null && !string.IsNullOrWhiteSpace(s.ApiKeyValue))
            {
                tempEndpoint = new GossipSDK.Core.Connection.EndpointConnection(s.ApiKeyHeader, s.ApiKeyValue);

                endpoint = tempEndpoint;

            }

        }

        if (endpoint == null) { Debug.LogWarning("[AudioReaction] No endpoint and no API key; skipping upload"); return; }

        try { await endpoint.UploadAudioReaction(data, audio, success => { if (Gossip.Instance?.Settings?.EnableDebug == true) Debug.Log(success ? "[AudioReaction] Uploaded via endpoint" : "[AudioReaction] Upload failed"); }); }

        finally { tempEndpoint?.Dispose(); }


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
