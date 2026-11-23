using System;
using System.Collections.Generic;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using Newtonsoft.Json;

namespace GossipSDK.Tracking.GameplayMetrics
{
    [Serializable]
    public class DistanceTracker : GenericSocketConnection<DistanceTracker.EntityData, DistanceTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingDistance";

        [Serializable]
        public class EntityData : Data
        {
            public float DistanceMeters { get; set; }

            /*
             * UNA DISTANCIA A UN OBJETO SIN NOMBRE NO SE PUEDE USAR.
             *
             * Medido el 23-11-2026 sobre las tres bases: ObjPosX/Y/Z son 0 en los
             * 17.824 documentos, y DistanceMeters coincide exactamente con el modulo
             * de PlayerPos -2,4250459 contra 2,42504621, siete cifras, tres de tres-.
             * O sea que el componente funciona y esta pegado a un objeto que esta en
             * el origen del mundo: lo guardado es la distancia del jugador al origen.
             *
             * Eso NO es un fallo del SDK, es como queda instalado. Pero sin identidad
             * del objeto no habia forma de verlo desde los datos, y hubo que deducirlo
             * a mano. RotationTracker ya manda ObjectName; este no. Con el, el mismo
             * diagnostico sale de una consulta.
             */
            public string ObjectName { get; set; }

            public float ObjPosX { get; set; }
            public float ObjPosY { get; set; }
            public float ObjPosZ { get; set; }

            public float PlayerPosX { get; set; }
            public float PlayerPosY { get; set; }
            public float PlayerPosZ { get; set; }
            public string SceneName { get; set; }
            
            public string TimestampUtc { get; set; }

            [JsonConstructor]
            public EntityData()
            {
            }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }

        public void RecordDistance (float meters, UnityEngine.Vector3 objectPosition, UnityEngine.Vector3 playerPosition, string objectName = null)
        {
            try
            {
                var data = new EntityData
                {
                    DistanceMeters = meters,

                    ObjectName = objectName,
                    ObjPosX = objectPosition.x,
                    ObjPosY = objectPosition.y,
                    ObjPosZ = objectPosition.z,

                    PlayerPosX = playerPosition.x,
                    PlayerPosY = playerPosition.y,
                    PlayerPosZ = playerPosition.z,

                    SceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    TimestampUtc = DateTime.UtcNow.ToString("o")
                };

                CapSession(data);

                if (GossipSDK.Core.Gossip.Instance?.Settings?.EnableDebug == true)
                {
                    UnityEngine.Debug.Log(
                        $"[DistanceTracker] Player↔Object {meters:F2}m " +
                        $"Obj({data.ObjPosX:F1},{data.ObjPosY:F1},{data.ObjPosZ:F1})"
                    );
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(new Exception("[DistanceTracker] RecordDistance failed", ex));
            }
        }
    }
}
