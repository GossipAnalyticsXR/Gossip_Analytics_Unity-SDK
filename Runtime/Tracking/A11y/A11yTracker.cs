using System;
using GossipSDK.Core;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using Newtonsoft.Json;

namespace GossipSDK.Tracking.A11y
{
    [Serializable]
    public class A11yTracker
        : GenericSocketConnection<A11yTracker.EntityData, A11yTracker.TrackerMessage>
    {
        protected override string EventName => "a11y_check";

        // Las dos formas que puede tomar una medida de accesibilidad. El reparto del
        // 25-sep-2026 es 7 checks y 9 senales, y hasta hoy este tracker solo sabia
        // hablar en checks.
        public const string KindCheck = "check";
        public const string KindSignal = "signal";

        // Propiedades, no campos. El bufer local es LiteDB y su BsonMapper solo mapea
        // propiedades: con campos planos la fila se guardaba vacia, se releia con los
        // valores por defecto y el evento salia con MetricKey null y Numerator 0.
        // Medido en gafas el 24-sep-2026: el sobre llegaba correcto y el cuerpo vacio.
        // Este era el unico EntityData de los 39 del SDK declarado con campos.
        //
        // Aqui vivia tambien 'Complies'. Salio el 25-sep-2026 por dos razones:
        //   1. 'complies = numerator > 0' no es el umbral de ninguna metrica. Con esa
        //      regla, un texto etiquetado de cada mil hace que P1 cumpla. El propio
        //      comentario del metodo decia que "el umbral se evalua en el backend",
        //      o sea que el valor nacia para ser sobrescrito.
        //   2. El umbral vive en una sola constante compartida con su cita. Calcularlo
        //      tambien aqui era un segundo sitio donde vive un umbral.
        // El emisor manda numerador, denominador y tipo. El veredicto lo pone quien
        // tiene el umbral. Nadie se rompe: el 25-sep-2026 el evento 'a11y_check' sigue
        // sin darse de alta en la ingesta -- 43 processors en Backend-SDK y ninguno de
        // a11y -- asi que este contrato no tiene todavia un solo lector.
        [Serializable]
        public class EntityData : Data
        {
            public string MetricKey { get; set; }
            public string Kind { get; set; }
            public int Numerator { get; set; }
            public int Denominator { get; set; }
            public string Scope { get; set; }
            public MetaData Meta { get; set; }
            public string TimestampUtc { get; set; }
        }

        [Serializable]
        public class MetaData
        {
            public string Platform { get; set; }
            public string AppVersion { get; set; }
            public string SdkVersion { get; set; }
            public string Locale { get; set; }
            public string ScreenId { get; set; }
            public string SceneId { get; set; }
            public string BuildId { get; set; }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }

        /// <summary>
        /// Un check puntua: hay una regla fuera de Gossip que se cumple o no, y alguien
        /// puede arreglarla.
        /// </summary>
        public void CapCheck(
            string metricKey,
            int numerator,
            int denominator,
            string scope,
            MetaData meta
        )
        {
            Cap(KindCheck, metricKey, numerator, denominator, scope, meta);
        }

        /// <summary>
        /// Una senal describe uso y no puntua nunca. Nueve de las dieciseis metricas de
        /// la seccion son senales, y hasta hoy no habia forma de emitir ninguna.
        /// </summary>
        public void CapSignal(
            string metricKey,
            int numerator,
            int denominator,
            string scope,
            MetaData meta
        )
        {
            Cap(KindSignal, metricKey, numerator, denominator, scope, meta);
        }

        private void Cap(
            string kind,
            string metricKey,
            int numerator,
            int denominator,
            string scope,
            MetaData meta
        )
        {
            var data = new EntityData
            {
                MetricKey = metricKey,
                Kind = kind,
                Numerator = numerator,
                Denominator = denominator,
                Scope = scope,
                Meta = meta,
                TimestampUtc = DateTime.UtcNow.ToString("o")
            };

            CapSession(data);
        }
    }
}
