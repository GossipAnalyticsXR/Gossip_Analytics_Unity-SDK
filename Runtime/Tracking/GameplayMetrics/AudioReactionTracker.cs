using System;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using Newtonsoft.Json;

namespace GossipSDK.Tracking.GameplayMetrics
{
    [Serializable]
    public class AudioReactionTracker
        : GenericSocketConnection<AudioReactionTracker.EntityData, AudioReactionTracker.TrackerMessage>
    {
        protected override string EventName => "AudioReactionTracker";

        [Serializable]
        public class EntityData : Data
        {
            public byte[] AudioData
            {
                get; set;
            }
            public float EventSeverity
            {
                get; set;
            }
            public float VoiceChange
            {
                get; set;
            }
            public float VoiceQuality
            {
                get; set;
            }
            /// <summary>Pico/rms de la ventana, normalizado. Ver burstinessFloorDb.</summary>
            public float VoiceBurstiness
            {
                get; set;
            }
            public float MovementIntensity
            {
                get; set;
            }
            public float EmotionalScore
            {
                get; set;
            }
            public string TriggerMode
            {
                get; set;
            }
            public string SceneName
            {
                get; set;
            }

            public string TimestampUtc
            {
                get; set;
            }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData>
        {
        }

        public void SendSnippet(EntityData data)
        {
            try
            {
                string json = JsonConvert.SerializeObject(data);
                UnityEngine.Debug.LogWarning($"[AudioTracker DEBUG] Intentando enviar evento '{EventName}'. Estado Socket: {(this.IsConnected ? "CONECTADO" : "DESCONECTADO")}. Payload JSON: {json}");

                if (!this.IsConnected)
                {
                    UnityEngine.Debug.LogError("[AudioTracker] ERROR CRÍTICO: El tracker está intentando enviar datos pero el socket está cerrado. ¿Llamaste a Gossip.Instance.Connect() o similar?");
                }

                CapSession(data);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[AudioTracker] Error al serializar o enviar: {e.Message}");
            }
        }
    }
}
