using System;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using Newtonsoft.Json;
using UnityEngine;

namespace GossipSDK.Tracking.PlatformSpecification
{
    [Serializable]
    public class PlatformTracker : GenericSocketConnection<PlatformTracker.EntityData, PlatformTracker.TrackerMessage>
    {
        protected override string EventName { get; } = "TrackingDataHardwareAndSoftware";

        [Serializable]
        public class EntityData : Data
        {
            [field: SerializeField] public string Version { get; set; }
            [field: SerializeField] public string ConnectionSpeed { get; set; }
            [field: SerializeField] public bool RequiresWifi { get; set; }
            [field: SerializeField] public bool RequiresMobileData { get; set; }
            [field: SerializeField] public bool GeneralSound { get; set; }
            [field: SerializeField] public bool ControllersLatency { get; set; }
            [field: SerializeField] public float MotionToPhotonMs { get; set; }
            [field: SerializeField] public float TrackingAccuracy { get; set; }
            [field: SerializeField] public bool AmountDevicesInGame { get; set; }
            [field: SerializeField] public bool HandStatus { get; set; }
            [field: SerializeField] public bool ControllerStatus { get; set; }
            [field: SerializeField] public string Model { get; set; }
            [field: SerializeField] public string Device { get; set; }
            [field: SerializeField] public string Brand { get; set; }
            [field: SerializeField] public string OsVersion { get; set; }
            [field: SerializeField] public string Resolution { get; set; }
            // La escala a la que la app renderiza respecto al DEFECTO que propone el
            // dispositivo. Es el denominador que le faltaba a `Resolution`: esa cadena ya
            // es la textura de ojo de la app (PlatformMonitorComponent lee
            // XRSettings.eyeTextureWidth/Height), no el panel, asi que el backend la
            // estaba dividiendo entre los pixeles de una Quest 3, una constante de otro
            // visor. Con esto, `defecto = Resolution / RenderScale` y la proporcion sale
            // sin comparar contra ningun aparato de referencia. Cero significa que no se
            // pudo leer: no es una escala de 0.
            [field: SerializeField] public float RenderScale { get; set; }
            [field: SerializeField] public string PlatformName { get; set; }
            [JsonConstructor] public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData>
        { }
    }
}
