using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using GossipSDK.Core;
using GossipSDK.Tracking.GameplayMetrics;
using GossipSDK.Utilities;

namespace GossipSDK.Components
{
    /// <summary>
    /// Enumera los objetos instrumentados de la escena y los envia como inventario.
    ///
    /// Para que el dashboard pueda decir 3 de 8 no se toco nunca necesita saber
    /// cuantos habia, y eso NO esta en las interacciones: ahi solo aparecen los que
    /// alguien toco. Este componente aporta el denominador.
    ///
    /// Limite conocido y a proposito: FindObjectsByType ve lo que esta cargado y
    /// activo en ese momento, asi que un prefab instanciado despues no entra en el
    /// inventario. Por eso la regla del dashboard es que el denominador sea la UNION
    /// del inventario con lo que se toco, nunca solo el inventario.
    /// </summary>
    [DisallowMultipleComponent]
    public class SceneInventoryComponent : MonoBehaviour
    {
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
            StartCoroutine(EnviarCuandoHayaGossip(SceneManager.GetActiveScene()));
        }

        private void OnActiveSceneChanged(Scene from, Scene to)
        {
            StartCoroutine(EnviarCuandoHayaGossip(to));
        }

        /// <remarks>
        /// Mismo motivo que en InteractableComponent: lo que se envia antes de que
        /// Gossip exista se pierde en silencio, asi que se espera con tope y se avisa.
        /// </remarks>
        private IEnumerator EnviarCuandoHayaGossip(Scene escena)
        {
            float esperado = 0f;
            while (Gossip.Instance == null && esperado < 10f)
            {
                esperado += Time.unscaledDeltaTime;
                yield return null;
            }

            var tracker = Gossip.Instance?.SceneInventoryTracker;
            if (tracker == null)
            {
                Debug.LogWarning("[SceneInventory] SceneInventoryTracker not available on Gossip. Scene inventory not sent.");
                yield break;
            }

            // Un frame mas: los objetos que se activan en su propio Start todavia no
            // estan cuando corre el nuestro.
            yield return null;

            var encontrados = Object.FindObjectsByType<InteractableComponent>(FindObjectsSortMode.None);
            var nombreEscena = escena.IsValid() ? escena.name : SceneManager.GetActiveScene().name;
            var version = GossipVersion.App;
            var enviados = 0;

            foreach (var instrumentado in encontrados)
            {
                if ((Object)instrumentado == null) continue;

                var go = instrumentado.gameObject;
                if (go.scene.IsValid() && go.scene.name != nombreEscena) continue;

                tracker.CapSceneObject(
                    go.name,
                    Jerarquia.RutaDe(go.transform),
                    go.tag,
                    "Interactable",
                    nombreEscena,
                    version);

                enviados++;
            }

            if (enviados == 0) yield break;

            // CapSceneObject solo escribe en la base local. Sin esta linea el inventario
            // se queda en el aparato, que es lo que le pasaba a LevelChangeComponent.
            tracker.SendDataToSocket();

            if (Gossip.Instance?.Settings?.EnableDebug == true)
            {
                Debug.Log("[SceneInventory] " + enviados + " instrumented objects reported for scene " + nombreEscena);
            }
        }
    }
}
