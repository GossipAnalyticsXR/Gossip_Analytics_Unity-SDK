using System;
using UnityEngine;
using GossipSDK.Core;
using GossipSDK.XR;
using GossipSDK.Tracking.GameplayMetrics;

namespace GossipSDK.Components
{
    /// <summary>Muestrea la posicion de la cabeza y calcula tres campos derivados.
    ///
    /// PARA QUE SERVIAN, Y PARA QUE PODRIAN SERVIR TODAVIA
    ///
    /// El balanceo postural se estudia como predictor de mareo en XR: la teoria de
    /// la inestabilidad postural liga mas balanceo con mas probabilidad de
    /// cybersickness. La relacion existe en la literatura, pero no es limpia: hay
    /// trabajos que la sostienen y otros que la matizan, incluido uno titulado
    /// literalmente 'It's complicated'. O sea, sirve como senal de riesgo, no
    /// como diagnostico.
    ///
    /// - SwayMagnitude SI puede alimentar esa senal, pero no como esta calculado
    ///   aqui. Las medidas estandar se sacan de la SERIE, no muestra a muestra:
    ///   RMS respecto a la posicion central, longitud del recorrido, area de la
    ///   elipse que contiene el 95% de las posiciones. Y la serie ya existe:
    ///   CopX/Y/Z poblados al 100%, cada 0,5 s, desde mayo de 2026. Se puede
    ///   calcular retroactivamente sobre las 48.208 filas sin tocar el SDK.
    ///
    /// - PostureState tambien, y por eso se mantiene: de pie se balancea mas y se
    ///   marea mas que sentado, asi que es el moderador natural de esa lectura.
    ///   Ademas puede alimentar Flows and Heatmaps. Hoy no lo escribe nadie; si se
    ///   quiere aqui, lo natural es que se lo pase UserPostureTrackerComponent,
    ///   que ya calcula la postura, en vez de esperar a que la app llame al setter.
    ///
    /// - SwayFrequency NO, y no es cuestion de arreglar el calculo. Con
    ///   sampleInterval a 0,5 s el muestreo es de 2 Hz y por Nyquist el techo
    ///   resoluble es 1 Hz. Para tener frecuencia hay que subir el muestreo, y eso
    ///   multiplica el volumen de filas: es una decision de coste, no de codigo.
    ///
    /// Y el matiz que no se puede saltar: esto mide la CABEZA, no el centro de
    /// presion. Es un proxy de la senal que mide la posturografia, no la senal.</summary>
    [DisallowMultipleComponent]
    public class UserBalanceTrackerComponent : MonoBehaviour
    {
        [SerializeField] private float sampleInterval = 0.5f;
        /// <summary>Nadie lo escribe. Barrido de los 161 .cs del repo el 13-09-2026:
        /// SetPostureState solo aparece en su propia declaracion, cero llamadas. La
        /// postura de verdad la produce UserPostureTrackerComponent, que tiene su
        /// propia coleccion y su propia card; esto es un duplicado que sale siempre
        /// vacio. Medido en Mongo el 12-09-2026: PostureState null en 48.208 de
        /// 48.208 filas desde mayo. Pendiente de retirar: quitarlo se lleva por
        /// delante un metodo publico, y eso es una decision de version.</summary>
        [SerializeField] private string postureState = "";

        /// <summary>OJO: 5 cm. Con sampleInterval a 0,5 s, cualquier desplazamiento
        /// de cabeza por encima de 10 cm/s satura swayMagnitude en 1, que es menos
        /// que un balanceo tranquilo. Y el valor es recomputable: la distancia entre
        /// CopX/Y/Z consecutivos ya esta en la coleccion. Antes del 2.0.15 este campo
        /// salia 0 exacto el 90% del tiempo, pero eso era otro fallo: se comparaba
        /// contra el transform del rig y no contra la pose de cabeza. Esa parte esta
        /// arreglada; la saturacion no.</summary>
        [SerializeField] private float swayMagnitudeCeiling = 0.05f;
        private Vector3 lastPosition;
        private float lastSampleTime;
        private bool started;

        private float _swayFreqTimer = 0f;
        private int _swayDirectionChanges = 0;
        private float _lastSwayDeltaX = 0f;
        private float _measuredSwayFrequency = 0f;

        // lastPosition ya no se puede sembrar en Start() con transform.position:
        // seria una posicion del rig comparada contra poses de cabeza. Se siembra
        // con la primera pose real, y esa muestra no se emite.
        private bool _tieneLastPosition = false;
        private void Start()
        {
            lastSampleTime = Time.time;
            started = true;
        }

        private void Update()
        {

            if (!started)
                return;

            float now = Time.time;
            float dt = now - lastSampleTime;
            if (dt < sampleInterval)
                return;

            // La pose de la cabeza se pide al subsistema XR, no al transform de este
            // GameObject, asi que da igual de donde cuelgue el componente.
            //
            // Medido el 12-09-2026 en el log de un visor real: colgado del root de
            // XR Origin devolvia COP=(0.37, 0.00, 2.00) en las 120 muestras de la
            // sesion, con Y=0 a ras de suelo, mientras el heatmap de la MISMA sesion
            // veia 949 celdas distintas. En Mongo, 48208 filas desde mayo con una
            // sola posicion por sesion.
            //
            // devicePosition viene en espacio de TRACKING, relativo al origen, que es
            // lo que quiere una medida de cuerpo: no la arrastra la locomocion del
            // juego. Mismo criterio que PlayableArea en 2.0.15.
            //
            // Si la pose no esta disponible no se muestrea: un dato inventado es peor
            // que un hueco.
            var headPose = XRBootstrap.HeadPose;
            if (headPose == null || !headPose.IsAvailable)
            {
                lastSampleTime = now;
                return;
            }

            if (!headPose.TryGetPose(out Vector3 currentPos, out _))
            {
                lastSampleTime = now;
                return;
            }

            // Primera pose de la sesion: se siembra lastPosition y no se emite. Sin
            // esto el primer sway saldria de restar a una posicion que nunca existio.
            if (!_tieneLastPosition)
            {
                lastPosition = currentPos;
                _tieneLastPosition = true;
                lastSampleTime = now;
                return;
            }

            float deltaX = currentPos.x - lastPosition.x;
            if (Mathf.Abs(deltaX) > 0.001f && Mathf.Sign(deltaX) != Mathf.Sign(_lastSwayDeltaX))
                _swayDirectionChanges++;
            _lastSwayDeltaX = deltaX;
            // Esto NO puede dar una frecuencia. La ventana se cierra al llegar a 1 s
            // y las muestras entran cada sampleInterval = 0,5 s, asi que a cada ventana
            // le caben DOS: _swayDirectionChanges solo puede valer 0, 1 o 2, y el campo
            // solo puede valer 0, 0,5 o 1. Tres valores posibles. Los 1,97302508 que hay
            // en Mongo no salen de este codigo: son de un productor anterior.
            _swayFreqTimer += dt;
            if (_swayFreqTimer >= 1f)
            {
                _measuredSwayFrequency = _swayDirectionChanges / 2f;
                _swayDirectionChanges = 0;
                _swayFreqTimer = 0f;
            }

            float rawSwayMagnitude = Vector3.Distance(currentPos, lastPosition);
            float swayMagnitude = Mathf.Clamp01(rawSwayMagnitude / swayMagnitudeCeiling);

            float swayFrequency = _measuredSwayFrequency;

            lastPosition = currentPos;
            lastSampleTime = now;

            try
            {
                var tracker = Gossip.Instance?.UserBalanceTracker;
                if (tracker == null)
                    return;

                tracker.CaptureSample(
                    currentPos,
                    swayMagnitude,
                    swayFrequency,
                    postureState
                );

                if (Gossip.Instance?.Settings?.EnableDebug == true)
                {
                    Debug.Log(
                        $"[UserBalanceTrackerComponent] COP={currentPos} " +
                        $"mag={swayMagnitude:F3} freq={swayFrequency:F2}"
                    );
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
        public void SetPostureState(string state)
        {
            postureState = state ?? string.Empty;
        }
    }
}
