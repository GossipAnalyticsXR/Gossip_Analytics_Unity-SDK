using System;
using UnityEngine;
using GossipSDK.Core;
using GossipSDK.XR;
using GossipSDK.Tracking.GameplayMetrics;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class UserBalanceTrackerComponent : MonoBehaviour
    {
        [SerializeField] private float sampleInterval = 0.5f;
        [SerializeField] private string postureState = "";

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
