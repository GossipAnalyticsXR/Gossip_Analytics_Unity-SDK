using System;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using Newtonsoft.Json;

namespace GossipSDK.Tracking.GameplayMetrics
{
    /// <summary>
    /// Cambio de nivel: el jugador pasa de una escena a otra.
    ///
    /// No confundir con DifficultyTracker, que mide dificultad. Este tracker es
    /// solo el transporte: quien detecta el cambio y calcula el tiempo de estancia
    /// es LevelChangeComponent.
    /// </summary>
    [Serializable]
    public class LevelChangeTracker : GenericSocketConnection<LevelChangeTracker.EntityData, LevelChangeTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingLevelChange";

        public string EventTypeForEndpoint => "TrackingLevelChange";

        [Serializable]
        public class EntityData : Data
        {
            public string FromScene { get; set; }
            public string ToScene { get; set; }
            public float DwellMs { get; set; }
            public bool IsSessionStart { get; set; }
            public string TimestampUtc { get; set; }

            [JsonConstructor] public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }

        /// <summary>
        /// Envia un cambio de nivel. La primera escena de la sesion se envia
        /// igual, con isSessionStart en true y fromScene vacio: el backend la
        /// guarda y es la agregacion la que decide si cuenta como cambio.
        /// </summary>
        public void CapLevelChange(string fromScene, string toScene, float dwellMs = 0f, bool isSessionStart = false)
        {
            var data = new EntityData
            {
                FromScene = fromScene ?? "",
                ToScene = toScene ?? "",
                DwellMs = dwellMs,
                IsSessionStart = isSessionStart,
                TimestampUtc = DateTime.UtcNow.ToString("o")
            };

            CapSession(data);
        }
    }
}
