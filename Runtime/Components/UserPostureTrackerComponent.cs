using System;
using UnityEngine;
using UnityEngine.XR;
using GossipSDK.Core;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class UserPostureComponent : MonoBehaviour
    {
        [Header("Sampling")]
        public float sampleInterval = 0.5f;
        public bool autoReportOnStart = true;

        [Header("Head / thresholds")]
        [Tooltip("Optional head transform (VR head). If null, uses this.transform")]
        public Transform headTransform;

        [Tooltip("If head Y relative to origin <= sitThreshold => Sitting")]
        public float sitThreshold = 0.9f;

        [Tooltip("If head Y relative to origin <= crouchThreshold => Crouching (should be > sitThreshold)")]
        public float crouchThreshold = 1.2f;

        //private bool registerHeatmapHit = false;
        private bool enableLocalDebug = false;

        [Header("Relative thresholds (calibrated to user height)")]
        [Tooltip("Fraction of standing head height below which posture is Sitting (default 0.65)")]
        public float sitRatio = 0.65f;

        [Tooltip("Fraction of standing head height below which posture is Crouching (default 0.80)")]
        public float crouchRatio = 0.80f;

        // Running max of head height seen this session.
        // Updated in SampleAndSend() on every sample.
        private float _standingHeadY = 0f;

        // Minimum plausible standing head height.
        // Used as a cold-start guard: relative thresholds are applied only
        // once _standingHeadY reaches this floor (a standing adult always exceeds 1.0 m).
        private const float _minStandingHeadY = 1.0f;

        // Banda de altura de cabeza plausible para una persona con visor. Es la
        // misma que el backend usa para declarar postureSignalPlausible, asi que
        // las dos puntas de la cadena juzgan la senal con el mismo criterio.
        private const float _minPlausibleHeadY = 0.8f;
        private const float _maxPlausibleHeadY = 2.2f;

        // El aviso de arranque en frio se emite UNA vez por sesion: el muestreo
        // es continuo y un log por muestra son miles de lineas.
        private bool _avisoArranqueEnFrioEmitido = false;

        // Los tres unicos estados que el backend sabe leer. Antes del PR #308
        // cualquier otro se contaba como de pie; hoy se cuenta aparte, pero
        // sigue siendo un dato perdido. Se valida aqui, en la puerta.
        public const string PostureSitting = "Sitting";
        public const string PostureStanding = "Standing";
        public const string PostureCrouching = "Crouching";

        float timer = 0f;

        void Start()
        {
            timer = 0f;
            if (autoReportOnStart)
                SampleAndSend();
        }

        void Update()
        {
            timer += Time.deltaTime;
            if (timer >= sampleInterval)
            {
                timer = 0f;
                SampleAndSend();
            }
        }

        public void PushPostureState(string posture)
        {
            if (string.IsNullOrWhiteSpace(posture)) return;

            string normalizado = NormalizarPostura(posture);
            if (normalizado == null)
            {
                Debug.LogError(
                    "[UserPostureTracker] PushPostureState recibio un estado desconocido: " + posture +
                    ". Solo se aceptan " + PostureSitting + ", " + PostureStanding +
                    " o " + PostureCrouching + ". La muestra NO se envia.");
                return;
            }

            if (!TryGetHeadPosition(out Vector3 headPos))
            {
                Debug.LogError(
                    "[UserPostureTracker] PushPostureState no encontro pose del visor. " +
                    "HeadY saldria en espacio de mundo, que es la altitud dentro del nivel " +
                    "y no la altura sobre el suelo. La muestra NO se envia.");
                return;
            }

            TrySend(normalizado, headPos);
        }

        // Mayusculas y espacios de sobra son un error de tipeo, no un estado
        // nuevo: se normalizan en vez de tirar la muestra. Lo que no coincide
        // con ninguno de los tres devuelve null y no sale del dispositivo.
        string NormalizarPostura(string posture)
        {
            string limpio = posture.Trim();
            if (string.Equals(limpio, PostureSitting, StringComparison.OrdinalIgnoreCase)) return PostureSitting;
            if (string.Equals(limpio, PostureStanding, StringComparison.OrdinalIgnoreCase)) return PostureStanding;
            if (string.Equals(limpio, PostureCrouching, StringComparison.OrdinalIgnoreCase)) return PostureCrouching;
            return null;
        }

        void SampleAndSend()
        {
            // Sin pose del visor no se manda muestra. El fallback a
            // transform.position media la ALTITUD del jugador dentro del nivel:
            // medido en produccion el 12-sep-2026, 4 sesiones del 14 de julio con
            // HeadY media -588,79 m y un pico de +847,98 m, que arrastraron la
            // media de 124 sesiones de 1,28 m hasta -17,15 m. Un proxy sobre
            // coordenadas de mundo es peor que no tener proxy: la misma regla que
            // el PR #90 dejo escrita en PlayableAreaComponent.
            if (!TryGetHeadPosition(out Vector3 headPos)) return;

            // El techo solo crece con alturas que puedan ser la cabeza de una
            // persona. Medido el 12-sep-2026 en el rango limpio: el maximo se
            // despega 0,321 m de la media con desviacion 0,092 m, o sea 3,5 sigma,
            // y en la peor sesion 1,209 m, que pone el techo en unos 2,5 m. Un
            // gesto (el visor en la mano) no puede fijar el techo de la sesion.
            if (headPos.y >= _minPlausibleHeadY && headPos.y <= _maxPlausibleHeadY)
            {
                _standingHeadY = Mathf.Max(_standingHeadY, headPos.y);
            }

            string posture = InferPostureFromHeadY(headPos.y);

            TrySend(posture, headPos);
        }

        // devicePosition del HMD viene en espacio de TRACKING, o sea altura sobre
        // el suelo de la sala, que es lo que la postura necesita.
        // headTransform.position es espacio de MUNDO y ahi va dentro la locomocion
        // del juego: teleport, joystick, plataforma, ascensor. Y cuando
        // headTransform es null era peor: se leia el GameObject vacio del SDK, que
        // es de donde salio el 0,56 m del arranque en frio del 2.0.16.
        bool TryGetHeadPosition(out Vector3 posicion)
        {
            var hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (hmd.isValid &&
                hmd.TryGetFeatureValue(CommonUsages.devicePosition, out posicion))
            {
                return true;
            }

            posicion = Vector3.zero;
            return false;
        }

        string InferPostureFromHeadY(float headWorldY)
        {
            // Cold-start guard: use absolute thresholds until _standingHeadY
            // is calibrated to at least _minStandingHeadY (1.0 m).
            // A standing adult always exceeds 1.0 m; below that the running max
            // has not yet seen a full-standing sample, so fall back to absolute thresholds.
            if (_standingHeadY < _minStandingHeadY)
            {
                // Medido el 12-09-2026: si el transform que se lee no es la cabeza,
                // _standingHeadY no llega nunca a 1,0 m, esta rama no se sale nunca y
                // cada muestra sale etiquetada con los umbrales absolutos. En el log de
                // un visor real la Y valia 0,56 m toda la sesion y las 120 muestras
                // salieron como Sitting, con total confianza y sin que nadie lo supiera.
                //
                // La guarda ya detectaba el caso; lo que faltaba era contarlo.
                if (!_avisoArranqueEnFrioEmitido)
                {
                    _avisoArranqueEnFrioEmitido = true;
                    Debug.LogWarning(
                        "[Gossip] Postura sin calibrar: la altura maxima de cabeza vista " +
                        "es " + _standingHeadY.ToString("F2") + " m, por debajo del minimo " +
                        "de " + _minStandingHeadY.ToString("F2") + " m. Se estan usando umbrales " +
                        "absolutos, no la calibracion por usuario. Comprueba que headTransform " +
                        "apunta a la camara XR."
                    );
                }

                if (headWorldY <= sitThreshold) return PostureSitting;
                if (headWorldY <= crouchThreshold) return PostureCrouching;
                return PostureStanding;
            }

            // Relative classification against the session running-max head height.
            if (headWorldY <= _standingHeadY * sitRatio) return PostureSitting;
            if (headWorldY <= _standingHeadY * crouchRatio) return PostureCrouching;
            return PostureStanding;
        }

        void TrySend(string postureState, Vector3 headPos)
        {

            try
            {
                var gossip = GossipSDK.Core.Gossip.Instance;
                if (gossip == null)
                {
                    if (enableLocalDebug) Debug.LogWarning("[UserPostureComponent] Gossip.Instance is null.");
                    return;
                }

                var prop = gossip.GetType().GetProperty("UserPostureTracker");
                var trackerObj = prop?.GetValue(gossip);
                if (trackerObj != null)
                {
                    var cap = trackerObj.GetType().GetMethod("CapSession");
                    if (cap != null)
                    {
                        try
                        {
                            var paramType = cap.GetParameters()[0].ParameterType;
                            var entity = Activator.CreateInstance(paramType);

                            void TrySet(string name, object val)
                            {
                                var p = paramType.GetProperty(name);
                                if (p != null && p.CanWrite) { p.SetValue(entity, val); return; }
                                var f = paramType.GetField(name);
                                if (f != null) f.SetValue(entity, val);
                            }

                            TrySet("PostureState", postureState);
                            TrySet("HeadX", headPos.x);
                            TrySet("HeadY", headPos.y);
                            TrySet("HeadZ", headPos.z);
                            Transform _h = headTransform != null ? headTransform : transform;
                            Vector3 _fwd = _h.forward;
                            TrySet("HeadPitch", Mathf.Asin(Mathf.Clamp(_fwd.y, -1f, 1f)) * Mathf.Rad2Deg);
                            TrySet("HeadYaw",   _h.eulerAngles.y);
                            TrySet("TimestampUtc", DateTime.UtcNow.ToString("o"));
                            TrySet("SceneName", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                            TrySet("PlayerId", gossip.PlayerID ?? "");
                            TrySet("SessionId", gossip.SessionID ?? "");

                            cap.Invoke(trackerObj, new object[] { entity });

                            if ((gossip.Settings?.EnableDebug == true) || enableLocalDebug)
                                Debug.Log($"[UserPostureComponent] Sent posture='{postureState}' pos=({headPos.x:F2},{headPos.y:F2},{headPos.z:F2})");
                            return;
                        }
                        catch (Exception ex)
                        {
                            if (enableLocalDebug) Debug.LogWarning($"[UserPostureComponent] CapSession entity send failed: {ex.Message}");
                        }
                    }

                    var capStr = trackerObj.GetType().GetMethod("CapturePosture") ?? trackerObj.GetType().GetMethod("Capture");
                    if (capStr != null)
                    {
                        try
                        {
                            capStr.Invoke(trackerObj, new object[] { postureState });
                            if ((gossip.Settings?.EnableDebug == true) || enableLocalDebug)
                                Debug.Log($"[UserPostureComponent] Invoked CapturePosture('{postureState}')");
                            return;
                        }
                        catch { }
                    }
                }

                var gCapProp = gossip.GetType().GetProperty("UserPostureTracker");
                var gTracker = gCapProp?.GetValue(gossip);
                var gCap = gTracker?.GetType().GetMethod("CapSession");
                if (gCap != null)
                {
                    try
                    {
                        var paramType = gCap.GetParameters()[0].ParameterType;
                        var inst = Activator.CreateInstance(paramType);
                        var trySet = new Action<string, object>((n, v) =>
                        {
                            var p = paramType.GetProperty(n);
                            if (p != null && p.CanWrite) p.SetValue(inst, v);
                        });
                        trySet("PostureState", postureState);
                        trySet("HeadX", headPos.x);
                        trySet("HeadY", headPos.y);
                        trySet("HeadZ", headPos.z);
                        Transform _h = headTransform != null ? headTransform : transform;
                        Vector3 _fwd = _h.forward;
                        trySet("HeadPitch", Mathf.Asin(Mathf.Clamp(_fwd.y, -1f, 1f)) * Mathf.Rad2Deg);
                        trySet("HeadYaw",   _h.eulerAngles.y);
                        trySet("TimestampUtc", DateTime.UtcNow.ToString("o"));
                        gCap.Invoke(gTracker, new object[] { inst });
                        if ((gossip.Settings?.EnableDebug == true) || enableLocalDebug)
                            Debug.Log($"[UserPostureComponent] (fallback) Sent posture '{postureState}'");
                    }
                    catch (Exception ex)
                    {
                        if (enableLocalDebug) Debug.LogWarning($"[UserPostureComponent] Fallback send failed: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                if (enableLocalDebug) Debug.LogWarning($"[UserPostureComponent] TrySend failed: {ex.Message}");
            }
        }
    }
}
