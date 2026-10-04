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

                var hayCaja = TryCalcularCaja(go, out var centro, out var tamano);

                tracker.CapSceneObject(
                    go.name,
                    Jerarquia.RutaDe(go.transform),
                    go.tag,
                    "Interactable",
                    nombreEscena,
                    version,
                    hayCaja ? centro.x : (float?)null,
                    hayCaja ? centro.y : (float?)null,
                    hayCaja ? centro.z : (float?)null,
                    hayCaja ? tamano.x : (float?)null,
                    hayCaja ? tamano.y : (float?)null,
                    hayCaja ? tamano.z : (float?)null);

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

        /// <summary>
        /// Caja del objeto en coordenadas de MUNDO, uniendo la de todos sus Renderer;
        /// si no tiene ninguno, la de sus Collider. Devuelve false cuando no hay ni una
        /// cosa ni la otra: ese objeto NO tiene caja, y eso se manda como nulo, no como
        /// cero.
        /// Se incluyen los desactivados a proposito: un objeto que arranca oculto sigue
        /// ocupando su sitio en la escena.
        /// </summary>
        private static bool TryCalcularCaja(GameObject go, out Vector3 centro, out Vector3 tamano)
        {
            centro = Vector3.zero;
            tamano = Vector3.zero;
            if ((Object)go == null) return false;

            var caja = new Bounds();
            var hay = false;

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if ((Object)r == null) continue;
                if (!hay) { caja = r.bounds; hay = true; }
                else caja.Encapsulate(r.bounds);
            }

            if (!hay)
            {
                foreach (var c in go.GetComponentsInChildren<Collider>(true))
                {
                    if ((Object)c == null) continue;
                    if (!hay) { caja = c.bounds; hay = true; }
                    else caja.Encapsulate(c.bounds);
                }
            }

            if (!hay) return false;
            centro = caja.center;
            tamano = caja.size;
            return true;
        }
    }
}
