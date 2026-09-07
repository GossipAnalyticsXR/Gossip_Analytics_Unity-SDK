using System.Collections;
using System;
using UnityEngine;
using GossipSDK.Tracking.GameplayMetrics;
using GossipSDK.Core;

namespace GossipSDK.Components 
{
    [DisallowMultipleComponent]
    public class ExperienceInfoComponent : MonoBehaviour
    {
        public bool autoReportOnStart = true;
        public string appVersion = "0.0.0";
        public string targetHardware = "Unity";
        public bool sendImmediately = false;

        private double awakeTime;
        private bool alreadySent = false;

        private void Awake()
        {
            awakeTime = Time.realtimeSinceStartupAsDouble;
        }

        private void Start()
        {
            if (!autoReportOnStart) return;
            if (Gossip.Instance == null)
            {
                StartCoroutine(WaitAndSend());
                return;
            }
            SendLoadInfoInternal();
        }

        private IEnumerator WaitAndSend()
        {
            yield return new WaitUntil(() => Gossip.Instance != null);
            SendLoadInfoInternal();
        }

        private void SendLoadInfoInternal()
        {

            if (alreadySent) return;
            alreadySent = true;

            double now = Time.realtimeSinceStartupAsDouble;
            double loadSeconds = Math.Max(0.0, now - awakeTime);
            double loadMs = loadSeconds * 1000.0;

            var tracker = GossipSDK.Core.Gossip.Instance?.ExperienceInfoTracker;
            if (tracker != null)
            {
                // Runtime fallbacks if developer did not configure via Instrumentation Manager
                if (string.IsNullOrEmpty(appVersion) || appVersion == "0.0.0")
                    appVersion = Application.version;
                if (string.IsNullOrEmpty(targetHardware) || targetHardware == "Unity")
                {
                    #if UNITY_ANDROID
                    targetHardware = "Android XR";
                    #elif UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
                    targetHardware = "PC VR";
                    #elif UNITY_IOS
                    targetHardware = "iOS";
                    #else
                    targetHardware = Application.platform.ToString();
                    #endif
                }

                // El instante lo pone Gossip cuando la sesion queda lista; -1 mientras no.
                // Se manda null en vez de -1 para que el backend guarde "no lo se" y no un
                // numero negativo que luego habria que filtrar en cada consulta.
                var nucleo = GossipSDK.Core.Gossip.Instance;
                double? initMs = (nucleo != null && nucleo.SdkInitMs >= 0.0)
                    ? nucleo.SdkInitMs
                    : (double?)null;

                // "awake_to_start" describe literalmente lo que se acaba de medir arriba:
                // now - awakeTime. No es la carga de la experiencia, y la etiqueta lo dice
                // para que el dia que cambie el reloj no se mezclen las dos poblaciones.
                tracker.CapExperienceInfo(loadMs, appVersion, targetHardware, "awake_to_start", initMs);
                if (sendImmediately)
                    tracker.SendDataToSocket();
            }
            else
            {
                Debug.LogWarning("[ExperienceInfoComponent] ExperienceInfoTracker not available.");
            }
        }

        public void SendLoadInfo(double loadTimeMs, string appVersionOverride = null, string targetHardwareOverride = null, bool sendNow = false)
        {

            if (alreadySent) return;
            alreadySent = true;

            var tracker = GossipSDK.Core.Gossip.Instance?.ExperienceInfoTracker;
            if (tracker == null)
            {
                Debug.LogWarning("[ExperienceInfoComponent] ExperienceInfoTracker not available.");
                return;
            }

            // "integrator": el numero lo pone la app, no lo mide el SDK. Va con su propia
            // etiqueta para no confundirlo con nada que hayamos cronometrado nosotros.
            var nucleoIntegrador = GossipSDK.Core.Gossip.Instance;
            double? initMsIntegrador = (nucleoIntegrador != null && nucleoIntegrador.SdkInitMs >= 0.0)
                ? nucleoIntegrador.SdkInitMs
                : (double?)null;

            tracker.CapExperienceInfo(loadTimeMs, appVersionOverride ?? appVersion, targetHardwareOverride ?? targetHardware, "integrator", initMsIntegrador);
            if (sendNow) tracker.SendDataToSocket();
        }
    }
}
