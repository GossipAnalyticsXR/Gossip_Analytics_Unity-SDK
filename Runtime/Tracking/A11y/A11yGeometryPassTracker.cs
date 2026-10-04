using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using GossipSDK.A11y;
using GossipSDK.Components;
using GossipSDK.Core;

namespace GossipSDK.Tracking.A11y
{
    /// <summary>
    /// La pasada geometrica de Accessibility. Recorre la escena una vez por
    /// carga y emite las metricas cuya cifra publicada es un angulo, que hoy
    /// son dos: O1 (tamano de diana) y P4 (legibilidad angular del texto).
    ///
    /// Mismo ciclo que TextNarrationCoverageTracker, y por el mismo motivo: el
    /// catalogo del Instrumentation Manager engancha el componente al
    /// GossipManager, que es DontDestroyOnLoad, asi que medir solo en Start()
    /// seria UNA medicion por arranque de la app y no una por escena.
    ///
    /// LOS INACTIVOS QUEDAN FUERA, y esto es una decision, no un descuido. En
    /// este mismo SDK conviven las dos reglas, cada una con su razon escrita:
    /// SceneInventoryComponent SI cuenta los desactivados, porque describe la
    /// escena y "un objeto que arranca oculto sigue ocupando su sitio"; y
    /// TextNarrationCoverageTracker NO los cuenta, porque mide "lo que el
    /// usuario pudo ver". Estas metricas son del segundo tipo: no se puede
    /// apuntar a una diana que no esta, ni leer un texto que no se dibujo. Por
    /// eso la caja se calcula aqui con una copia del metodo del inventario y
    /// NO reusandolo: el suyo recorre los hijos con includeInactive en true, y
    /// tocarlo cambiaria un dato que ya esta en produccion.
    /// </summary>
    public class A11yGeometryPassTracker : MonoBehaviour
    {
        /// <summary>O1. Dianas cuyo lado corto llega al minimo publicado.</summary>
        public const string ClaveDeDiana = "target_size_coverage";

        /// <summary>P4. Textos en camara que se leen a su distancia.</summary>
        public const string ClaveDeTexto = "text_legibility_coverage";

        /// <summary>
        /// Sondas de cobertura. No son cards: son el denominador del
        /// denominador, y por eso salen como SENAL y nunca como check. Sin
        /// ellas, un elemento que la pasada no supo medir desapareceria en
        /// silencio y el porcentaje de arriba parecerian mas limpio de lo que
        /// es. Es la misma idea que ACC-49 pide para el fondo de P3.
        /// </summary>
        public const string ClaveDeCajasMedidas = "target_box_coverage";
        public const string ClaveDeTextosMedidos = "text_measurable_coverage";

        /// <summary>
        /// O5. Dianas cuya vecina mas cercana queda a la distancia minima o
        /// mas. SENAL, no check, y el motivo esta escrito en el registro de
        /// umbrales del front: WCAG 2.2 2.5.8 es normativa, pero su cifra -un
        /// circulo de 24 px CSS- no tiene equivalente publicado en un visor.
        /// Lo que si es una propiedad comprobable del texto de la norma es que
        /// ese circulo mide lo mismo que el minimo de diana, asi que aqui se
        /// reparte con LOS MISMOS 22,0 mm de O1. Sustituir la cifra de WCAG
        /// por la de Meta es una suposicion, y por eso esta fila informa y no
        /// juzga.
        /// </summary>
        public const string ClaveDeSeparacion = "target_spacing_coverage";

        /// <summary>U1. Subtitulos que caben en el presupuesto de Meta.</summary>
        public const string ClaveDeSubtitulo = "caption_composition_coverage";

        /// <summary>Senal: subtitulos cuyo texto y filas se pudieron leer.</summary>
        public const string ClaveDeSubtitulosMedidos = "caption_measurable_coverage";

        /// <summary>
        /// La guia de subtitulos de Meta: dos filas como mucho, y unos 32
        /// caracteres por fila. Lo que se comprueba es el PRESUPUESTO -filas
        /// por caracteres- y no el reparto fila a fila, porque el texto de
        /// cada linea solo se puede sacar bajando a la estructura interna de
        /// TextMeshPro, y este ensamblado no referencia TextMeshPro a
        /// proposito. Un bloque de dos filas con 20 y 40 caracteres pasa aqui
        /// y no deberia; uno de 70 caracteres en dos filas no pasa. El corte
        /// esta del lado conservador en el caso que importa, que es el texto
        /// largo.
        /// </summary>
        public const int MaximoDeFilasDeSubtitulo = 2;

        public const int MaximoDeCaracteresPorFila = 32;

        private const string AmbitoDeEscena = "scene";

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
            // La escena que ya estaba cargada cuando aparecio este componente
            // puede no disparar sceneLoaded. Si las dos vias caen en el mismo
            // frame, el guardia de abajo deja una sola medicion.
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

            // Al final del frame ya existe la interfaz que la escena instancia
            // en su propio Start, y los layouts ya estan resueltos: antes, el
            // tamano preferido de un texto todavia no significa nada.
            _medicionPendiente = true;
            yield return new WaitForEndOfFrame();
            _medicionPendiente = false;

            Evaluate();
        }

        public void Evaluate()
        {
            MedirDianas();
            MedirSubtitulos();

            // O1 no necesita camara: su cifra esta dada a una distancia fija,
            // asi que es un tamano. P4 si la necesita, y sin camara NO se
            // emite: una fila con distancia inventada es peor que ninguna.
            var camara = Camera.main;
            if ((Object)camara != null)
            {
                MedirTextos(camara);
            }
        }

        // ---------- O1 ----------

        private void MedirDianas()
        {
            var dianas = FindObjectsByType<InteractableComponent>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

            int encontradas = 0;
            int conCaja = 0;
            int conformes = 0;
            var centros = new List<Vector3>();

            foreach (var diana in dianas)
            {
                if ((Object)diana == null) continue;
                encontradas++;

                Vector3 tamano;
                Vector3 centro;
                if (!TryCajaVisible(diana.gameObject, out tamano, out centro)) continue;
                conCaja++;
                centros.Add(centro);

                var lado = A11yGeometry.LadoMenorDeLaCaraMayor(tamano.x, tamano.y, tamano.z);
                if (A11yGeometry.DianaCumple(lado)) conformes++;
            }

            Emitir(ClaveDeDiana, conformes, conCaja, true);
            Emitir(ClaveDeCajasMedidas, conCaja, encontradas, false);

            int separadas;
            var conVecina = ContarSeparadas(centros, out separadas);
            Emitir(ClaveDeSeparacion, separadas, conVecina, false);
        }

        /// <summary>
        /// Cuantas dianas tienen a su vecina mas cercana a la distancia minima
        /// o mas. Devuelve el denominador -las que TIENEN vecina- y deja el
        /// numerador en el parametro de salida.
        ///
        /// Una diana sola no puede incumplir una regla de separacion, asi que
        /// no entra al denominador: con menos de dos cajas la fraccion es 0/0,
        /// y un 0/0 no puntua. Lo que se compara es la distancia entre centros
        /// contra el diametro minimo, porque dos circulos de ese diametro
        /// dejan de tocarse exactamente cuando sus centros se separan esa
        /// misma distancia.
        /// </summary>
        public static int ContarSeparadas(List<Vector3> centros, out int separadas)
        {
            separadas = 0;
            if (centros == null || centros.Count < 2) return 0;

            var minimo = A11yGeometry.TamanoMinimoDeDiana();

            for (var i = 0; i < centros.Count; i++)
            {
                var masCerca = float.MaxValue;
                for (var j = 0; j < centros.Count; j++)
                {
                    if (i == j) continue;
                    var d = Vector3.Distance(centros[i], centros[j]);
                    if (d < masCerca) masCerca = d;
                }

                if (masCerca >= minimo) separadas++;
            }

            return centros.Count;
        }

        /// <summary>
        /// Caja de mundo del objeto uniendo sus Renderer activos y, si no tiene
        /// ninguno, sus Collider activos. Devuelve false cuando no hay ni una
        /// cosa ni la otra: ese objeto no tiene caja, y eso NO es una caja de
        /// tamano cero.
        ///
        /// Copia deliberada de SceneInventoryComponent.TryCalcularCaja con dos
        /// diferencias, las dos a proposito: solo mira hijos activos, y
        /// descarta los componentes deshabilitados, que no dibujan ni chocan.
        /// </summary>
        private static bool TryCajaVisible(GameObject go, out Vector3 tamano, out Vector3 centro)
        {
            tamano = Vector3.zero;
            centro = Vector3.zero;
            if ((Object)go == null) return false;

            var caja = new Bounds();
            var hay = false;

            foreach (var r in go.GetComponentsInChildren<Renderer>(false))
            {
                if ((Object)r == null) continue;
                if (!r.enabled) continue;
                if (!hay) { caja = r.bounds; hay = true; }
                else caja.Encapsulate(r.bounds);
            }

            if (!hay)
            {
                foreach (var c in go.GetComponentsInChildren<Collider>(false))
                {
                    if ((Object)c == null) continue;
                    if (!c.enabled) continue;
                    if (!hay) { caja = c.bounds; hay = true; }
                    else caja.Encapsulate(c.bounds);
                }
            }

            if (!hay) return false;

            tamano = caja.size;
            centro = caja.center;
            return true;
        }

        // ---------- P4 ----------

        private void MedirTextos(Camera camara)
        {
            var graficos = FindObjectsByType<Graphic>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

            var planos = GeometryUtility.CalculateFrustumPlanes(camara);
            var esquinas = new Vector3[4];
            var ojo = camara.transform.position;

            int enCamara = 0;
            int medibles = 0;
            int conformes = 0;

            foreach (var grafico in graficos)
            {
                if ((Object)grafico == null) continue;
                if (!A11yTextReader.IsTextComponent(grafico)) continue;

                // Misma exclusion que P1: lo marcado como decorativo no se
                // juzga, aqui tampoco.
                var etiqueta = grafico.GetComponent<GossipA11yLabel>();
                if ((Object)etiqueta != null && etiqueta.decorative) continue;

                var rect = grafico.rectTransform;
                if ((Object)rect == null) continue;

                rect.GetWorldCorners(esquinas);
                var caja = new Bounds(esquinas[0], Vector3.zero);
                for (int i = 1; i < 4; i++) caja.Encapsulate(esquinas[i]);

                // El requisito de Daydream habla de lo que el usuario ve. Lo
                // que esta fuera del tronco de vision no entra al denominador,
                // y eso significa que esta metrica depende de hacia donde
                // mirase la persona en ese instante. Esta escrito en el
                // catalogo y es la regla, no un efecto secundario.
                if (!GeometryUtility.TestPlanesAABB(planos, caja)) continue;
                enCamara++;

                TextReading lectura;
                if (!A11yTextReader.TryRead(grafico, out lectura)) continue;
                if (!lectura.HasPreferredSize) continue;
                if (!lectura.HasLineCount || lectura.LineCount <= 0) continue;

                var escala = rect.lossyScale.y;
                if (escala <= 0f) continue;

                // El alto PREFERIDO es el que ocupa el texto; el del rect puede
                // ser el de una caja mucho mas alta con el texto arriba.
                var altoDeLinea = (lectura.PreferredHeight * escala) / lectura.LineCount;
                if (altoDeLinea <= 0f) continue;

                var distancia = Vector3.Distance(ojo, caja.center);
                if (distancia <= 0f) continue;

                medibles++;
                if (A11yGeometry.TextoCumple(altoDeLinea, distancia)) conformes++;
            }

            Emitir(ClaveDeTexto, conformes, medibles, true);
            Emitir(ClaveDeTextosMedidos, medibles, enCamara, false);
        }

        // ---------- U1 ----------

        /// <summary>
        /// Los subtitulos son los objetos que llevan GossipA11yCaption. No se
        /// adivinan: el marcador existe porque sin el no hay forma de separar
        /// un subtitulo de un parrafo, y aplicar la regla de 32 caracteres a
        /// un parrafo da un numero que no significa nada.
        ///
        /// Esta medida no es geometria y vive aqui igualmente: necesita el
        /// lector de texto y el final del frame, exactamente igual que P4, y
        /// un tercer componente solo para ella seria una casilla mas que
        /// alguien tiene que acordarse de marcar.
        /// </summary>
        private void MedirSubtitulos()
        {
            var marcas = FindObjectsByType<GossipA11yCaption>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

            int encontrados = 0;
            int medibles = 0;
            int conformes = 0;

            foreach (var marca in marcas)
            {
                if ((Object)marca == null) continue;
                encontrados++;

                var texto = PrimerTextoDe(marca.gameObject);
                if (texto == null) continue;

                TextReading lectura;
                if (!A11yTextReader.TryRead(texto, out lectura)) continue;
                if (!lectura.HasText || lectura.Text == null) continue;
                if (!lectura.HasLineCount || lectura.LineCount <= 0) continue;

                medibles++;
                if (CumpleLaComposicion(lectura.Text.Length, lectura.LineCount)) conformes++;
            }

            Emitir(ClaveDeSubtitulo, conformes, medibles, true);
            Emitir(ClaveDeSubtitulosMedidos, medibles, encontrados, false);
        }

        /// <summary>
        /// El presupuesto de Meta: como mucho dos filas, y como mucho 32
        /// caracteres por cada fila que ocupe.
        /// </summary>
        public static bool CumpleLaComposicion(int caracteres, int filas)
        {
            if (filas <= 0) return false;
            if (filas > MaximoDeFilasDeSubtitulo) return false;
            return caracteres <= MaximoDeCaracteresPorFila * filas;
        }

        /// <summary>
        /// El primer Graphic del objeto que el lector reconozca como texto.
        /// Se busca por Graphic y no por un tipo concreto porque TMP_Text y
        /// UnityEngine.UI.Text son los dos Graphic, y este ensamblado no
        /// referencia TextMeshPro.
        /// </summary>
        private static Graphic PrimerTextoDe(GameObject go)
        {
            if ((Object)go == null) return null;

            foreach (var grafico in go.GetComponents<Graphic>())
            {
                if ((Object)grafico == null) continue;
                if (A11yTextReader.IsTextComponent(grafico)) return grafico;
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

            var meta = BuildMeta();

            if (esCheck)
            {
                tracker.CapCheck(clave, numerador, denominador, AmbitoDeEscena, meta);
            }
            else
            {
                tracker.CapSignal(clave, numerador, denominador, AmbitoDeEscena, meta);
            }
        }

        /// <summary>
        /// Copia de BuildMeta de TextNarrationCoverageTracker, que es privado.
        /// Dos copias del mismo calculo es justo el patron que este proyecto
        /// persigue, asi que queda anotado: cuando se toque P1 para que cuente
        /// tambien el texto 3D, las dos se colapsan en un metodo de A11yTracker
        /// y este desaparece.
        /// </summary>
        private static A11yTracker.MetaData BuildMeta()
        {
            return new A11yTracker.MetaData
            {
                Platform = Application.platform.ToString(),
                AppVersion = Application.version,
                SdkVersion = Constants.SdkVersion,
                Locale = Application.systemLanguage.ToString(),
                SceneId = SceneManager.GetActiveScene().name,

                // Vacio en el editor; en un player es el GUID de la build.
                BuildId = Application.buildGUID
            };
        }
    }
}
