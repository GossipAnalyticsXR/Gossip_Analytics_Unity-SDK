using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using GossipSDK.A11y;
using GossipSDK.Core;
using GossipSDK.Tracking.A11y;

namespace GossipSDK.Tracking.A11y
{
    public class TextNarrationCoverageTracker : MonoBehaviour
    {
        // Este componente puede colgar de un objeto con DontDestroyOnLoad: el catalogo
        // del Instrumentation Manager lo engancha al GossipManager, y GossipManager.Awake
        // llama a DontDestroyOnLoad. Midiendo solo en Start() eso era UNA medicion por
        // arranque de la app: P1 describia la primera escena y ninguna mas.
        private bool _medicionPendiente;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += AlCargarEscena;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= AlCargarEscena;
        }

        private void Start()
        {
            // La escena que ya estaba cargada cuando aparecio este componente puede no
            // disparar sceneLoaded, asi que se mide tambien aqui. Si las dos vias caen
            // en el mismo frame, el guardia de abajo deja una sola medicion.
            StartCoroutine(MedirAlFinalDelFrame());
        }

        private void AlCargarEscena(Scene escena, LoadSceneMode modo)
        {
            StartCoroutine(MedirAlFinalDelFrame());
        }

        private IEnumerator MedirAlFinalDelFrame()
        {
            if (_medicionPendiente)
                yield break;

            // Al final del frame ya existe la interfaz que la escena instancia en su
            // propio Start. Medir antes dejaba fuera dialogos, listas y tooltips.
            _medicionPendiente = true;
            yield return new WaitForEndOfFrame();
            _medicionPendiente = false;

            Evaluate();
        }

        public void Evaluate()
        {
            // FindObjectsInactive.Exclude: se cuenta lo que el usuario pudo ver. Con los
            // inactivos dentro, numerador y denominador se movian en direcciones opuestas
            // y el porcentaje no era ni optimista ni pesimista, era indeterminado.
            var graphics = FindObjectsByType<Graphic>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

            int denominator = 0;
            int numerator = 0;

            foreach (var graphic in graphics)
            {
                bool isText =
                    graphic is Text ||
                    graphic.GetType().Name == "TextMeshProUGUI";

                if (!isText)
                    continue;

                var label = graphic.GetComponent<GossipA11yLabel>();
                if (label != null && label.decorative)
                    continue;

                denominator++;

                if (label != null && !string.IsNullOrWhiteSpace(label.label))
                    numerator++;
            }

            Gossip.Instance?.A11yTracker?.CapCheck(
                metricKey: "text_narration_coverage",
                numerator: numerator,
                denominator: denominator,
                // scope: "scene", no "screen". Esta pasada recorre TODOS los Graphic activos
                // que hay cargados en ese instante -- escenas aditivas incluidas -- y la fila
                // se atribuye a la escena activa, que viaja en Meta.SceneId. No hay
                // granularidad de pantalla: declararla seria prometer que dos canvas de la
                // misma escena se pueden separar, y no se pueden. Por eso ScreenId se queda
                // en null a proposito, y no por olvido.
                scope: "scene",
                meta: BuildMeta()
            );
        }

        private A11yTracker.MetaData BuildMeta()
        {
            return new A11yTracker.MetaData
            {
                Platform = Application.platform.ToString(),
                AppVersion = Application.version,
                SdkVersion = Constants.SdkVersion,
                Locale = Application.systemLanguage.ToString(),
                SceneId = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,

                // Vacio en el editor; en un player es el GUID de la build. Sin el, dos
                // builds distintas de la misma version de app dan filas indistinguibles.
                BuildId = Application.buildGUID
            };
        }
    }
}
