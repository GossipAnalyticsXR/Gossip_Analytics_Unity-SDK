using System;
using UnityEngine;
using GossipSDK.Core;
using GossipSDK.Tracking.GameplayMetrics;

[DisallowMultipleComponent]
public class DifficultyComponent : MonoBehaviour
{
    /// <summary>
    /// Emitir un cambio de dificultad al arrancar. Por defecto FALSE desde el
    /// 10-sep-2026: con true, el componente reportaba defaultDifficultyId en cada
    /// arranque y esos documentos no eran eleccion de nadie. En el periodo medido
    /// eran 78, todos "normal", y ensuciaban la metrica.
    ///
    /// Comentar el alta automatica en GossipManager no bastaba: el Instrumentation
    /// Manager agrega el componente en tiempo de edicion con Undo.AddComponent, y
    /// Ensure solo actua si el componente NO esta ya en la escena.
    ///
    /// Aviso: es un campo serializado. Una escena o prefab que ya lo tenga puesto
    /// conserva el true guardado y hay que desmarcarlo a mano una vez.
    /// </summary>
    public bool autoReportOnStart = false;

    public bool sendImmediately = false;

    public string defaultDifficultyId = "normal";
    public float defaultNumeric = 0.5f;

    public void Start()
    {
        if(autoReportOnStart)
            NotifyDifficulty(defaultDifficultyId, defaultNumeric, "start");
    }

    public void NotifyDifficulty(string difficultyId, float numericValue = 0f, string reason = "player_selected")
    {
        try
        {
            var tracker = Gossip.Instance?.DifficultyTracker;
            if (tracker == null)
            {
                Debug.LogWarning("[DifficultyComponent] DifficultyTracker not available on Gossip.");
                return;
            }

            tracker.CapDifficulty(difficultyId, numericValue, reason);

            if (sendImmediately)
            {
                tracker.SendDataToSocket();
            }

            if (Gossip.Instance?.Settings?.EnableDebug == true)
                Debug.Log($"[DifficultyComponent] Notified difficulty '{difficultyId}' value={numericValue} reason={reason}");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }
}
