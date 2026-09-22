using System;
using GossipSDK.Components;
using UnityEngine;
using UnityEngine.XR;
using GossipSDK.Core;
using GossipSDK.Tracking.GameplayMetrics;

[DisallowMultipleComponent]
public class PauseComponent : MonoBehaviour
{
    [Tooltip("If true, request immediate send after cap (use sparingly)")]
    public bool sendImmediately = false;

    [Tooltip("Min duration (seconds) to consider as a pause when sending a non-zero duration")]
    public double minPauseSeconds = 0.05;

    private PauseTracker tracker => Gossip.Instance?.PauseTracker;

    private double pauseStartRealtime = -1.0;

    private bool isPausedLocal = false;
    private SessionManager _sessionManager;

    private void OnEnable()
    {
        _presenciaConocida = false;
        _sessionManager = FindObjectOfType<SessionManager>();
    }

    public void OnPause()
    {
        try
        {
            if (tracker == null)
            {
                Debug.LogWarning("[PauseComponent] PauseTracker not available.");
            }

            if (isPausedLocal)
            {
                if (Gossip.Instance?.Settings?.EnableDebug == true)
                    Debug.Log("[PauseComponent] OnPause called but already paused - ignoring.");
                return;
            }

            pauseStartRealtime = Time.realtimeSinceStartupAsDouble;
            isPausedLocal = true;

            tracker?.CapPauseEvent("pause", 0.0);
            if ((UnityEngine.Object)_sessionManager != null) _sessionManager.RecordPause();
            if (sendImmediately) tracker?.SendDataToSocket();

            if (Gossip.Instance?.Settings?.EnableDebug == true)
                Debug.Log("[PauseComponent] CapSession pause (start stored).");
        }
        catch (Exception ex) { Debug.LogException(ex); }
    }

    public void OnResume()
    {
        if (!isPausedLocal) return;

        try
        {
            double duration = 0.0;

            if (isPausedLocal && pauseStartRealtime >= 0.0)
            {
                var now = Time.realtimeSinceStartupAsDouble;
                duration = now - pauseStartRealtime;

                if (duration < minPauseSeconds) duration = 0.0;
            }

            pauseStartRealtime = -1.0;
            isPausedLocal = false;

            if (tracker == null)
            {
                Debug.LogWarning("[PauseComponent] PauseTracker not available for resume.");
            }

            tracker?.CapPauseEvent("resume", duration);
            if ((UnityEngine.Object)_sessionManager != null) _sessionManager.RecordResume(duration);
            if (sendImmediately) tracker?.SendDataToSocket();

            if (Gossip.Instance?.Settings?.EnableDebug == true)
                Debug.Log($"[PauseComponent] CapSession resume (duration={duration:F3}s).");
        }
        catch (Exception ex) { Debug.LogException(ex); }
    }

    // Deteccion de visor puesto o quitado SIN depender de Meta. userPresence es
    // una feature estandar de OpenXR -la publican Quest, PICO y Vive-, mientras
    // que OVRManager solo existe si el proyecto tiene el paquete de Meta. Hasta
    // ahora esto solo funcionaba en Quest, y solo en build de Android.
    //
    // Se sondea cada medio segundo y no cada frame: quitarse el visor no es un
    // gesto que pida precision de milisegundos, y asi no se paga por fotograma.
    private const float INTERVALO_PRESENCIA_S = 0.5f;
    private float _proximaSonda;
    private bool _presenciaConocida;
    private bool _teniaPresencia;

    private void Update()
    {
        if (Time.unscaledTime < _proximaSonda) return;
        _proximaSonda = Time.unscaledTime + INTERVALO_PRESENCIA_S;

        var visor = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (!visor.isValid) return;
        if (!visor.TryGetFeatureValue(CommonUsages.userPresence, out bool presente))
            return;

        // La primera lectura solo fija el punto de partida, no es un cambio.
        if (!_presenciaConocida)
        {
            _presenciaConocida = true;
            _teniaPresencia = presente;
            return;
        }

        if (presente == _teniaPresencia) return;
        _teniaPresencia = presente;

        if (presente) OnResume(); else OnPause();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) OnPause(); else OnResume();
    }

    [ContextMenu("Simulate Pause")]
    public void SimulatePause() => OnPause();

    [ContextMenu("Simulate Resume")]
    public void SimulateResume() => OnResume();
}
