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
            // FindObjectsInactive.Include, y esto SUSTITUYE a la regla anterior.
            //
            // Hasta el 4-oct-2026 aqui ponia Exclude, con este motivo escrito: "se cuenta
            // lo que el usuario pudo ver; con los inactivos dentro, numerador y denominador
            // se movian en direcciones opuestas". La regla se cambia a proposito, no por
            // descuido, y el motivo es que P1 no mide una sesion: mide una propiedad de la
            // interfaz AUTORADA. Un rotulo sin etiqueta de narracion esta sin etiquetar lo
            // vea alguien o no, igual que WCAG-EM evalua el contenido y no la visita.
            //
            // Lo que lo decidio fue una medicion. El 4-oct-2026, en Hospital Zone con el
            // SDK 2.0.34, esta pasada emitio 0/0: los siete TextMeshPro de la escena son
            // rotulos que aparecen al interactuar y estaban desactivados en el frame de la
            // carga. Con Exclude, P1 no puede medir nada en una app cuya interfaz nace bajo
            // demanda, que son casi todas en un visor. Un denominador indeterminado es malo;
            // un denominador que siempre vale cero no es ni eso.
            //
            // P4 y P6 conservan Exclude y no se tocan: sus criterios SI son de la sesion
            // -- el angulo al que se vio un texto, lo que habia en pantalla --, y ahi la
            // regla vieja sigue siendo la correcta.
            //
            // Consecuencia que la card tiene que declarar: el denominador pasa a ser la
            // interfaz declarada, se haya visto o no.
            var graphics = FindObjectsByType<Graphic>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            int denominator = 0;
            int numerator = 0;

            foreach (var graphic in graphics)
            {
                // La puerta de A11yTextReader, la misma que usan P3, P4 y U1.
                //
                // Lo que habia aqui era `graphic is Text || GetType().Name ==
                // "TextMeshProUGUI"`, y esa comparacion por NOMBRE EXACTO dejaba fuera dos
                // cosas. La primera, el texto de mundo: `TextMeshPro` hereda de `TMP_Text`
                // igual que `TextMeshProUGUI`, y las dos salen en un barrido de `Graphic`,
                // pero su nombre no coincide, asi que un rotulo flotando en la escena no
                // entraba ni al numerador ni al denominador. La segunda, cualquier clase
                // que el cliente derive de `TextMeshProUGUI`. Y era asimetrico ademas:
                // `graphic is Text` SI acepta las clases derivadas de Text.
                //
                // IsTextComponent sube por la cadena de herencia comparando el nombre
                // COMPLETO del tipo contra "UnityEngine.UI.Text" y "TMPro.TMP_Text", asi
                // que acepta las dos familias enteras y nada mas.
                //
                // Efecto lateral que conviene vigilar: a partir de aqui P1 y P3 recorren la
                // MISMA poblacion y aplican la misma exclusion de decorativos, asi que sus
                // denominadores -- text_narration_coverage y text_background_coverage --
                // tienen que coincidir. Si algun dia no coinciden, una de las dos se ha
                // desviado.
                if (!A11yTextReader.IsTextComponent(graphic))
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
