using System;
using GossipSDK.Core;
using GossipSDK.Core.Connection;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
#if GOSSIP_A11Y_MODULE
using UnityEngine;
using UnityEngine.Accessibility;
#endif

namespace GossipSDK.Tracking.A11y
{
    /// <summary>
    /// Las cuatro senales de accesibilidad que publica el sistema operativo:
    /// lector de pantalla, escala de letra, negrita y subtitulos. Alimentan R1 y
    /// R2, que no son conformidad de la app sino perfil de quien la usa.
    ///
    /// UNA FILA POR LECTURA, no un valor por sesion. Las cuatro tienen evento de
    /// cambio en la propia API de Unity -- screenReaderStatusChanged,
    /// fontScaleChanged, boldTextStatusChanged y closedCaptioningStatusChanged --,
    /// asi que guardar solo la primera lectura seria guardar otra cosa. El modelo
    /// del backend ya espera este historico (PR #152).
    ///
    /// NULL NO ES FALSE. Si el modulo no esta, o la plataforma no lo puebla, las
    /// senales viajan null: "no se pudo leer". Un false dice "el sistema lo tiene
    /// apagado". Aplastar lo primero contra lo segundo le comeria el denominador
    /// entero a R1 y R2.
    ///
    /// PROPIEDADES, NO CAMPOS. El bufer local es LiteDB y su BsonMapper solo mapea
    /// propiedades: con campos planos la fila se guarda vacia y el evento sale con
    /// el cuerpo en blanco. Medido en gafas el 24-sep-2026 con A11yTracker, y por
    /// eso existe A11yPayloadContractTests, que ahora cubre tambien este tipo.
    /// </summary>
    [Serializable]
    public class A11ySettingsTracker
        : GenericSocketConnection<
            A11ySettingsTracker.EntityData,
            A11ySettingsTracker.TrackerMessage
        >
    {
        protected override string EventName => "a11y_settings";

        // Por que se tomo la lectura. Llega tal cual al backend, que no lo
        // normaliza: si este vocabulario cambiara, se quiere ver el cambio.
        public const string TriggerSessionStart = "session_start";
        public const string TriggerChanged = "changed";

        [Serializable]
        public class EntityData : Data
        {
            public bool? ScreenReaderEnabled { get; set; }
            public float? FontScale { get; set; }
            public bool? BoldTextEnabled { get; set; }
            public bool? ClosedCaptioningEnabled { get; set; }
            public string Trigger { get; set; }
            public string Platform { get; set; }
            public string SdkVersion { get; set; }
            public string TimestampUtc { get; set; }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }

        private bool suscrito;

        /// <summary>
        /// La lectura de apertura, y el alta a los cuatro eventos de cambio.
        ///
        /// Se llama desde `Gossip.Awake`, en el mismo sitio y con la misma forma
        /// que `UserInfoTracker.CaptureOnce()`. Automatico: el cliente no anade
        /// nada. Si dependiera de que alguien pusiera un componente en la escena,
        /// la cobertura de R1 y R2 pasaria a ser una decision suya y su
        /// denominador dejaria de significar lo que dice.
        ///
        /// `CapSession` escribe en LiteDB en el momento de la llamada, no en una
        /// corrutina, asi que la lectura de apertura sobrevive a un cierre
        /// abrupto -- que es el modo de fallo que ya nos costo P3.
        /// </summary>
        public void CaptureOnce()
        {
            Cap(TriggerSessionStart);

            if (suscrito) return;
            suscrito = true;

#if GOSSIP_A11Y_MODULE
            AssistiveSupport.screenReaderStatusChanged += AlCambiarLector;
            AccessibilitySettings.fontScaleChanged += AlCambiarEscala;
            AccessibilitySettings.boldTextStatusChanged += AlCambiarNegrita;
            AccessibilitySettings.closedCaptioningStatusChanged += AlCambiarSubtitulos;
#endif
        }

        /// <summary>
        /// Lo llama `Gossip.OnDestroy`. Sin esto, en el editor cada recarga de
        /// dominio dejaria un delegado colgado apuntando a una instancia muerta.
        /// </summary>
        public void Unsubscribe()
        {
            if (!suscrito) return;
            suscrito = false;

#if GOSSIP_A11Y_MODULE
            AssistiveSupport.screenReaderStatusChanged -= AlCambiarLector;
            AccessibilitySettings.fontScaleChanged -= AlCambiarEscala;
            AccessibilitySettings.boldTextStatusChanged -= AlCambiarNegrita;
            AccessibilitySettings.closedCaptioningStatusChanged -= AlCambiarSubtitulos;
#endif
        }

#if GOSSIP_A11Y_MODULE
        // Los cuatro manejadores capturan el estado COMPLETO, no solo la senal que
        // cambio: una fila con una sola senal y tres null seria indistinguible de
        // una lectura donde no se pudo leer el resto.
        private void AlCambiarLector(bool _) => Cap(TriggerChanged);

        private void AlCambiarEscala(float _) => Cap(TriggerChanged);

        private void AlCambiarNegrita(bool _) => Cap(TriggerChanged);

        private void AlCambiarSubtitulos(bool _) => Cap(TriggerChanged);
#endif

        private void Cap(string trigger)
        {
            var data = new EntityData
            {
                ScreenReaderEnabled = null,
                FontScale = null,
                BoldTextEnabled = null,
                ClosedCaptioningEnabled = null,
                Trigger = trigger,
                Platform = null,
                SdkVersion = Constants.SdkVersion,
                TimestampUtc = DateTime.UtcNow.ToString("o")
            };

#if GOSSIP_A11Y_MODULE
            // El modulo existe en el proyecto, pero su API solo esta poblada en
            // Android e iOS -- documentado por Unity, y medido ahi: Quest reporta
            // RuntimePlatform.Android, asi que entra por esta rama. En el editor
            // de escritorio no entra, y las cuatro senales se quedan null, que es
            // exactamente lo que son: no leidas.
            var plataforma = Application.platform;
            if (
                plataforma == RuntimePlatform.Android
                || plataforma == RuntimePlatform.IPhonePlayer
            )
            {
                data.Platform = plataforma.ToString();

                // Cada senal en su propio try: si una plataforma expone el lector
                // y no la escala de letra, se guarda la que hay en vez de perder
                // la lectura entera.
                try { data.ScreenReaderEnabled = AssistiveSupport.isScreenReaderEnabled; }
                catch (Exception) { data.ScreenReaderEnabled = null; }

                try { data.FontScale = AccessibilitySettings.fontScale; }
                catch (Exception) { data.FontScale = null; }

                try { data.BoldTextEnabled = AccessibilitySettings.isBoldTextEnabled; }
                catch (Exception) { data.BoldTextEnabled = null; }

                try { data.ClosedCaptioningEnabled = AccessibilitySettings.isClosedCaptioningEnabled; }
                catch (Exception) { data.ClosedCaptioningEnabled = null; }
            }
#endif

            // Sin ninguna senal leida no se envia. El backend tambien lo descarta
            // (PR #152), pero no mandarlo ahorra el viaje y deja claro aqui que una
            // lectura vacia no es una lectura.
            if (
                data.ScreenReaderEnabled == null
                && data.FontScale == null
                && data.BoldTextEnabled == null
                && data.ClosedCaptioningEnabled == null
            )
            {
                return;
            }

            CapSession(data);
        }
    }
}
