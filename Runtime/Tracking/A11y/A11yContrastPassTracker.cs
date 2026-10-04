using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using GossipSDK.A11y;
using GossipSDK.Components;
using GossipSDK.Core;

namespace GossipSDK.Tracking.A11y
{
    /// <summary>
    /// La pasada de contraste. Una vez por carga de escena, al final del
    /// frame, igual que la pasada geometrica y por el mismo motivo: el
    /// componente cuelga del GossipManager, que es DontDestroyOnLoad.
    ///
    /// EL FONDO SE RESUELVE DE FORMA ESTRICTA. Para saber si un texto llega a
    /// 4,5:1 hace falta saber CONTRA QUE, y el fondo de un texto en un canvas
    /// no esta declarado en ninguna parte. Habia dos reglas posibles: tirar un
    /// rayo desde la camara y quedarse con el color del primer Renderer de
    /// detras -alta cobertura, pero asume que ese Renderer es el fondo visual
    /// y que su color medio representa el pixel-, o no asumir nada. Esta clase
    /// no asume nada, y lo que queda fuera NO desaparece: viaja en sus propias
    /// senales de cobertura, que es lo que ACC-49 pedia para poder decidir con
    /// el numero delante si el rayo hace falta.
    /// </summary>
    public class A11yContrastPassTracker : MonoBehaviour
    {
        /// <summary>P3. Textos que llegan a su ratio contra el fondo resuelto.</summary>
        public const string ClaveDeTexto = "text_contrast_coverage";

        /// <summary>P6. Elementos no textuales de un control que llegan a 3:1.</summary>
        public const string ClaveDeNoTexto = "nontext_contrast_coverage";

        /// <summary>
        /// ACC-49, primera mitad: de todos los textos de la escena, cuantos
        /// tienen DETRAS una superficie opaca que esta pasada sepa encontrar.
        /// Senal: describe la cobertura del medidor, no la calidad de la app.
        /// </summary>
        public const string ClaveDeFondoHallado = "text_background_coverage";

        /// <summary>
        /// ACC-49, segunda mitad: de los que tienen fondo, cuantos tienen un
        /// fondo de COLOR CONOCIDO. Un Image con sprite y un RawImage son
        /// opacos y aun asi no sirven: lo que se ve son los pixeles de una
        /// textura, y su color no se puede leer sin bajar a la GPU. Separar
        /// las dos preguntas importa porque la respuesta es distinta: la
        /// primera se arregla con un rayo, la segunda no.
        /// </summary>
        public const string ClaveDeFondoSolido = "text_background_solid_coverage";

        /// <summary>Senal: controles cuyo contraste se pudo calcular.</summary>
        public const string ClaveDeNoTextoMedible = "nontext_measurable_coverage";

        private const string AmbitoDeEscena = "scene";

        private bool _medicionPendiente;

        // ---------- ciclo ----------

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

            _medicionPendiente = true;
            yield return new WaitForEndOfFrame();
            _medicionPendiente = false;

            Evaluate();
        }

        public void Evaluate()
        {
            MedirTexto();
            MedirNoTexto();
        }

        // ---------- el resolver de fondo ----------

        /// <summary>
        /// Lo que una superficie candidata dice cuando se le pregunta.
        /// </summary>
        public enum Superficie
        {
            /// <summary>No hay Image ni RawImage aqui: se sigue buscando.</summary>
            Nada,

            /// <summary>Hay uno y es opaco: la busqueda termina.</summary>
            Opaca,

            /// <summary>
            /// Hay uno y NO es opaco. La busqueda termina igual, pero sin
            /// fondo: lo que se ve detras del texto es una mezcla, y el color
            /// de la capa de abajo ya no es el color que el ojo recibe.
            /// Seguir buscando mas atras daria un ratio que no existe.
            /// </summary>
            Translucida
        }

        public struct Fondo
        {
            public bool Hallado;
            public bool ColorConocido;
            public float R;
            public float G;
            public float B;
        }

        /// <summary>
        /// Mira SOLO los componentes de este objeto, sin bajar a sus hijos.
        /// </summary>
        public static Superficie LeerSuperficie(Transform nodo, out Fondo fondo)
        {
            fondo = new Fondo();
            if ((Object)nodo == null) return Superficie.Nada;

            var imagen = nodo.GetComponent<Image>();
            if ((Object)imagen != null && imagen.enabled)
            {
                var c = imagen.color;
                if (!A11yContrast.EsOpaca(c.a)) return Superficie.Translucida;

                fondo.Hallado = true;

                // Sin sprite, un Image pinta un rectangulo del color plano que
                // lleva. Con sprite, ese color es un TINTE sobre una textura.
                fondo.ColorConocido = (Object)imagen.sprite == null;
                fondo.R = c.r;
                fondo.G = c.g;
                fondo.B = c.b;
                return Superficie.Opaca;
            }

            var cruda = nodo.GetComponent<RawImage>();
            if ((Object)cruda != null && cruda.enabled)
            {
                var c = cruda.color;
                if (!A11yContrast.EsOpaca(c.a)) return Superficie.Translucida;

                fondo.Hallado = true;
                fondo.ColorConocido = false;
                fondo.R = c.r;
                fondo.G = c.g;
                fondo.B = c.b;
                return Superficie.Opaca;
            }

            return Superficie.Nada;
        }

        /// <summary>
        /// El fondo de un grafico: primero los HERMANOS que se dibujan por
        /// detras, despues los ANCESTROS, del mas cercano al mas lejano.
        ///
        /// Solo los hermanos con indice MENOR, porque en un canvas ese indice
        /// ES el orden de dibujado: uno con indice mayor se pinta encima del
        /// texto y no es su fondo, es lo que lo tapa.
        ///
        /// Tampoco se mira el PROPIO objeto del texto: un Image en el mismo
        /// GameObject se dibuja debajo o encima segun el orden de componentes,
        /// que no es una regla que se pueda leer, asi que ese caso sale como
        /// no evaluable en vez de resolverse a ojo.
        ///
        /// No se miran los hermanos de los ancestros. Es parte de lo estricto:
        /// esos casos salen como no evaluables y se cuentan en la senal, en vez
        /// de resolverse con una regla que habria que inventar.
        /// </summary>
        public static Fondo ResolverFondo(Graphic delante)
        {
            var vacio = new Fondo();
            if ((Object)delante == null) return vacio;

            var nodo = delante.transform;
            if ((Object)nodo == null) return vacio;

            var padre = nodo.parent;
            Fondo fondo;

            if ((Object)padre != null)
            {
                var mio = nodo.GetSiblingIndex();
                for (var i = mio - 1; i >= 0; i--)
                {
                    var hermano = padre.GetChild(i);
                    var que = LeerSuperficie(hermano, out fondo);
                    if (que == Superficie.Opaca) return fondo;
                    if (que == Superficie.Translucida) return vacio;
                }
            }

            var ancestro = padre;
            while ((Object)ancestro != null)
            {
                var que = LeerSuperficie(ancestro, out fondo);
                if (que == Superficie.Opaca) return fondo;
                if (que == Superficie.Translucida) return vacio;
                ancestro = ancestro.parent;
            }

            return vacio;
        }

        // ---------- P3 ----------

        private void MedirTexto()
        {
            var graficos = FindObjectsByType<Graphic>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

            var considerados = 0;
            var conFondo = 0;
            var medibles = 0;
            var conformes = 0;

            foreach (var grafico in graficos)
            {
                if ((Object)grafico == null) continue;
                if (!A11yTextReader.IsTextComponent(grafico)) continue;

                // Misma exclusion que P1 y que P4: lo marcado como decorativo
                // no se juzga.
                var etiqueta = grafico.GetComponent<GossipA11yLabel>();
                if ((Object)etiqueta != null && etiqueta.decorative) continue;

                considerados++;

                var fondo = ResolverFondo(grafico);
                if (!fondo.Hallado) continue;
                conFondo++;

                if (!fondo.ColorConocido) continue;

                // El texto tambien tiene que ser opaco. Un texto al 40 % sobre
                // blanco no se ve del color que declara, y su ratio real esta
                // por debajo del que saldria de la cuenta.
                var tinta = grafico.color;
                if (!A11yContrast.EsOpaca(tinta.a)) continue;

                medibles++;

                var ratio = A11yContrast.Ratio(
                    tinta.r, tinta.g, tinta.b,
                    fondo.R, fondo.G, fondo.B
                );

                TextReading lectura;
                var hayTamano = false;
                var puntos = 0f;
                var negrita = false;
                if (A11yTextReader.TryRead(grafico, out lectura))
                {
                    hayTamano = lectura.HasFontSize;
                    puntos = lectura.FontSize;
                    negrita = lectura.HasBold && lectura.Bold;
                }

                var grande = A11yContrast.EsTextoGrande(hayTamano, puntos, negrita);
                if (A11yContrast.TextoCumple(ratio, grande)) conformes++;
            }

            Emitir(ClaveDeTexto, conformes, medibles, true);
            Emitir(ClaveDeFondoHallado, conFondo, considerados, false);
            Emitir(ClaveDeFondoSolido, medibles, conFondo, false);
        }

        // ---------- P6 ----------

        /// <summary>
        /// La poblacion de 1.4.11 son los COMPONENTES DE INTERFAZ, no todo lo
        /// que se dibuja: un adorno sin funcion no tiene que contrastar con
        /// nada. Aqui eso son los objetos que ya llevan InteractableComponent,
        /// que es la misma poblacion que mide O1.
        /// </summary>
        private void MedirNoTexto()
        {
            var controles = FindObjectsByType<InteractableComponent>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

            var encontrados = 0;
            var medibles = 0;
            var conformes = 0;

            foreach (var control in controles)
            {
                if ((Object)control == null) continue;
                encontrados++;

                var cara = PrimeraCaraSolida(control.gameObject);
                if ((Object)cara == null) continue;

                var fondo = ResolverFondo(cara);
                if (!fondo.Hallado || !fondo.ColorConocido) continue;

                medibles++;

                var c = cara.color;
                var ratio = A11yContrast.Ratio(c.r, c.g, c.b, fondo.R, fondo.G, fondo.B);
                if (A11yContrast.NoTextoCumple(ratio)) conformes++;
            }

            Emitir(ClaveDeNoTexto, conformes, medibles, true);
            Emitir(ClaveDeNoTextoMedible, medibles, encontrados, false);
        }

        /// <summary>
        /// El primer Image activo, opaco y sin sprite del control o de sus
        /// hijos. Sin sprite por el mismo motivo que en el fondo: con una
        /// textura encima, el color del componente es un tinte y no lo que se
        /// ve. Los hijos inactivos quedan fuera, como en toda esta seccion.
        /// </summary>
        public static Image PrimeraCaraSolida(GameObject go)
        {
            if ((Object)go == null) return null;

            foreach (var imagen in go.GetComponentsInChildren<Image>(false))
            {
                if ((Object)imagen == null) continue;
                if (!imagen.enabled) continue;
                if ((Object)imagen.sprite != null) continue;
                if (!A11yContrast.EsOpaca(imagen.color.a)) continue;
                return imagen;
            }

            return null;
        }

        // ---------- emision ----------

        private void Emitir(string clave, int numerador, int denominador, bool esCheck)
        {
            var instancia = Gossip.Instance;
            if ((Object)instancia == null) return;

            var tracker = instancia.A11yTracker;
            if (tracker == null) return;

            // Tercera copia del mismo bloque -las otras dos estan en
            // TextNarrationCoverageTracker, donde es privado, y en
            // A11yGeometryPassTracker-. Queda anotado aqui tambien: cuando se
            // toque P1 por el texto 3D, las tres se colapsan en un metodo de
            // A11yTracker y estas dos desaparecen.
            var meta = new A11yTracker.MetaData
            {
                Platform = Application.platform.ToString(),
                AppVersion = Application.version,
                SdkVersion = Constants.SdkVersion,
                Locale = Application.systemLanguage.ToString(),
                SceneId = SceneManager.GetActiveScene().name,
                BuildId = Application.buildGUID
            };

            if (esCheck)
            {
                tracker.CapCheck(clave, numerador, denominador, AmbitoDeEscena, meta);
            }
            else
            {
                tracker.CapSignal(clave, numerador, denominador, AmbitoDeEscena, meta);
            }
        }
    }
}
