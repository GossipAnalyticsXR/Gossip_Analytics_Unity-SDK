using System;
using UnityEngine;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using GossipSDK.Core;
using Newtonsoft.Json;

namespace GossipSDK.Tracking.GameplayMetrics
{
    [Serializable]
    public class HandControllerTracker
        : GenericSocketConnection<
            HandControllerTracker.EntityData,
            HandControllerTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingHandController";

        [Serializable]
        public class EntityData : Data
        {
            public string Hand
            {
                get; set;
            }
            public float Pitch
            {
                get; set;
            }
            public float HandElevation
            {
                get; set;
            }

            // LA POSE CRUDA, EN ESPACIO DE TRACKING.
            //
            // HandElevation no es una medida sino una interpretacion: el SDK
            // estima un hombro a partir de la cabeza y devuelve un angulo. El
            // fallo del 22-11-2026 -mezclaba espacio de tracking con espacio de
            // mundo- estuvo 265 sesiones en produccion y NO se podia comprobar
            // con los datos guardados, porque el angulo ya venia cocinado. Hubo
            // que reproducirlo con una sonda en el casco.
            //
            // Estas seis son la medida. Nullable a proposito: si el proveedor de
            // cabeza no responde, el campo va null, no un 0 que tiene pinta de
            // dato.
            public float? PosX
            {
                get; set;
            }
            public float? PosY
            {
                get; set;
            }
            public float? PosZ
            {
                get; set;
            }
            public float? HeadPosX
            {
                get; set;
            }
            public float? HeadPosY
            {
                get; set;
            }
            public float? HeadPosZ
            {
                get; set;
            }

            public string SceneName
            {
                get; set;
            }
            public string PlayerID
            {
                get; set;
            }
            public string SessionID
            {
                get; set;
            }
            public string TimestampUtc
            {
                get; set;
            }

            [JsonConstructor]
            public EntityData()
            {
            }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData>
        {
        }

        public void CapHandPose(
            string hand,
            Quaternion rotation,
            string sceneName = null)
        {
            var euler = rotation.eulerAngles;

            var data = new EntityData
            {
                Hand = hand,
                Pitch = NormalizeAngle(euler.x),

                SceneName = sceneName
                    ?? UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,

                PlayerID = Gossip.Instance?.PlayerID,
                SessionID = Gossip.Instance?.SessionID,
                TimestampUtc = DateTime.UtcNow.ToString("o")
            };

            CapSession(data);
        }

        private static float NormalizeAngle(float angle)
        {
            if (angle > 180f)
                angle -= 360f;
            return angle;
        }
    }
}
