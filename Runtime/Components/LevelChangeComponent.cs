using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using GossipSDK.Core;

/// <summary>
/// Detecta el cambio de nivel y lo envia por LevelChangeTracker.
///
/// Escucha activeSceneChanged y no sceneLoaded a proposito: una carga aditiva no
/// cambia la escena activa, asi que las capas que se superponen quedan fuera sin
/// necesidad de configurar nada.
///
/// La primera escena de la sesion se envia con isSessionStart en true. El backend
/// la guarda y es la agregacion la que decide si cuenta como cambio.
/// </summary>
[DisallowMultipleComponent]
public class LevelChangeComponent : MonoBehaviour
{
    [Tooltip("Escenas que no cuentan como nivel, por ejemplo menus o pantallas de carga. Vacio por defecto: se reporta todo.")]
    public string[] ignoredScenes = new string[0];

    private string currentScene;
    private float enteredAt;
    private bool started;

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private void Start()
    {
        StartCoroutine(WaitAndReportFirstScene());
    }

    // Lo enviado antes de que exista Gossip.Instance se pierde en silencio, asi
    // que la entrada inicial espera a que el singleton este montado.
    private IEnumerator WaitAndReportFirstScene()
    {
        yield return new WaitUntil(() => Gossip.Instance != null);

        string scene = SceneManager.GetActiveScene().name;
        currentScene = scene;
        enteredAt = Time.realtimeSinceStartup;
        started = true;

        if (IsIgnored(scene))
        {
            yield break;
        }

        Send("", scene, 0f, true);
    }

    private void OnActiveSceneChanged(Scene from, Scene to)
    {
        if (!started)
        {
            return;
        }

        string toName = to.name;
        if (string.IsNullOrEmpty(toName) || toName == currentScene)
        {
            return;
        }

        string fromName = currentScene;
        float dwellMs = (Time.realtimeSinceStartup - enteredAt) * 1000f;

        currentScene = toName;
        enteredAt = Time.realtimeSinceStartup;

        // Si cualquiera de las dos puntas esta excluida, el salto no es entre
        // niveles. El reloj ya se ha reiniciado arriba, asi que el siguiente
        // cambio mide desde aqui y no arrastra el tiempo del menu.
        if (IsIgnored(fromName) || IsIgnored(toName))
        {
            return;
        }

        Send(fromName, toName, dwellMs, false);
    }

    private bool IsIgnored(string scene)
    {
        if (ignoredScenes == null || string.IsNullOrEmpty(scene))
        {
            return false;
        }

        for (int i = 0; i < ignoredScenes.Length; i++)
        {
            if (ignoredScenes[i] == scene)
            {
                return true;
            }
        }

        return false;
    }

    private void Send(string fromScene, string toScene, float dwellMs, bool isSessionStart)
    {
        try
        {
            var tracker = Gossip.Instance?.LevelChangeTracker;
            if (tracker == null)
            {
                Debug.LogWarning("[LevelChangeComponent] LevelChangeTracker not available on Gossip.");
                return;
            }

            tracker.CapLevelChange(fromScene, toScene, dwellMs, isSessionStart);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[LevelChangeComponent] Failed to report level change: {e.Message}");
        }
    }
}
