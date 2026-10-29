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
        /// P2. Graficos NO textuales que llevan descripcion: el `label` de
        /// GossipA11yLabel relleno, o `decorative` marcado. Un decorativo CUENTA
        /// como resuelto -- WCAG no pide alternativa para lo que no aporta
        /// informacion --, y un label en blanco o solo con espacios NO cuenta.
        /// </summary>
        public const string ClaveDeAlternativa = "alt_text_coverage";

        /// <summary>
        /// R6. Bloques de texto que CABEN en su caja, sobre los que tienen caja
        /// con medida. El rotulo de la card nombra el defecto -- Text already
        /// clipped -- y la clave nombra lo conforme, porque `veredictoDe` aprueba
        /// con numerador mayor o igual que denominador: al reves aprobaria la
        /// pantalla mas rota. La regla esta escrita en el registro de ACC-41.
        ///
        /// No pasa por el tronco de vision. Un texto recortado lo esta mire quien
        /// mire, igual que una imagen necesita descripcion mire quien mire: va por
        /// donde va P2, no por donde va P4.
        /// </summary>
        public const string ClaveDeAjuste = "text_fit_coverage";

        /// <summary>
        /// R5. Bloques de texto que SIGUEN cabiendo si el cuerpo se va al doble,
        /// sobre los que tienen caja con medida. La cifra -- 200 % sin perdida de
        /// contenido -- la fija 1.4.4 y vive en el registro de ACC-41, no aqui.
        ///
        /// Va por donde va R6: la holgura de una caja no depende de hacia donde
        /// mirase la persona.
        /// </summary>
        public const string ClaveDeHolgura = "text_headroom_coverage";

        /// <summary>
        /// ACC-118. Sonda de cobertura de R5, y por el mismo motivo que las
        /// otras dos: sin ella, un bloque que la pasada no supo decidir
        /// desaparece en silencio y R5 publica un 0/0 que se lee como "no habia
        /// nada que medir" cuando dice "no pude decidirlo".
        ///
        /// Numerador: los bloques sobre los que R5 pudo pronunciarse.
        /// Denominador: los que tienen caja con medida, que es la poblacion que
        /// la card declara y la misma de R6.
        /// </summary>
        public const string ClaveDeHolguraMedible = "text_headroom_measurable_coverage";

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

        /// <summary>
        /// R6. El margen de redondeo del ajuste. El alto preferido lo calcula el
        /// componente de texto y el de la caja sale del RectTransform: con un `>`
        /// pelado, un bloque que cabe justo se marcaria como recortado por ruido
        /// de coma flotante. Un recorte de verdad se lleva al menos una linea, que
        /// es mucho mas que este 1 %.
        /// </summary>
        private const float ToleranciaDeAjuste = 1.01f;

        // ---------- ciclo ----------

        // ACC-111. El disparador ya no vive aqui. Las tres pasadas las llama
        // A11yPassScheduler, en el mismo frame y dos segundos despues de que la
        // escena cargue: en el frame de carga la interfaz de la escena todavia no
        // existe. Medido el 25-oct-2026 en Hospital, sesion 11ac70ac -- la pasada
        // de geometria conto UN Graphic en toda la escena mientras el contraste y
        // la narracion contaban siete textos.

        public void Evaluate()
        {
            // ACC-126. Cada escena abre su propia foto: se vacia lo de la
            // anterior y se acuna el sello con el que viajaran TODOS los
            // reenvios de esta. El sello fijo es lo que hace que el backend
            // sobrescriba la fila en vez de sumar otra.
            _textoEnCamara.Clear();
            _textoMedible.Clear();
            _textoConforme.Clear();
            _noTextoVivo.Clear();
            _noTextoDescrito.Clear();
            _hayMuestras = false;
            _selloDeLaEscena = System.DateTime.UtcNow.ToString("o");
            _ultimoConformes = -1;
            _ultimoMedibles = -1;
            _ultimoEnCamara = -1;
            _ultimoDescritos = -1;
            _ultimoNoTextoVivo = -1;

            MedirDianas();
            MedirSubtitulos();

            // R5 y R6 no necesitan camara: la holgura de una caja no depende
            // de donde mire nadie. Viajaban dentro de MuestrearTextos y heredaban
            // su guarda, asi que una escena sin Camera.main las dejaba sin
            // emitir sin que nada lo dijera.
            MedirCajasDeTexto();

            // O1 no necesita camara: su cifra esta dada a una distancia fija,
            // asi que es un tamano. P4 si la necesita, y sin camara NO se
            // emite: una fila con distancia inventada es peor que ninguna.
            var camara = Camera.main;
            if ((Object)camara != null)
            {
                MuestrearTextos(camara);
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

        // ---------- R5 y R6 ----------

        /// <summary>
        /// ACC-116. R5 y R6 tienen barrido propio, y con Include.
        ///
        /// Sus comentarios ya decian que no son metricas de sesion -- "un texto
        /// recortado lo esta mire quien mire", "la holgura de una caja no depende
        /// de hacia donde mirase la persona" --, pero compartian bucle con P4,
        /// que SI lo es, y heredaban su Exclude sin que nadie lo decidiera: la
        /// cabecera de este fichero fijo esa regla cuando solo emitia O1 y P4, y
        /// R5 y R6 llegaron despues. La decision del 4-oct-2026 que dejo a P4 y
        /// P6 con Exclude no las nombra.
        ///
        /// Medido el 27-oct-2026 en Hospital: text_fit_coverage 0/0 y
        /// text_headroom_coverage 0/0 mientras narracion contaba SIETE textos,
        /// porque los siete rotulos de la escena nacen desactivados y aparecen
        /// al interactuar. Es el mismo caso que llevo a P1 a Include el 4-oct.
        ///
        /// Barrido propio y no un filtro dentro del de P4: asi el bucle de P4,
        /// P2 y O1 se queda como estaba y que no se movio se VE, no hay que
        /// demostrarlo. El coste es un segundo FindObjectsByType cada dos
        /// segundos por escena, que es lo que vale no tocar tres metricas que
        /// ya estan validadas.
        /// </summary>
        private void MedirCajasDeTexto()
        {
            var graficos = FindObjectsByType<Graphic>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            int conCaja = 0;
            int ajustados = 0;
            int conMedidaDeHolgura = 0;
            int conHolgura = 0;

            foreach (var grafico in graficos)
            {
                if ((Object)grafico == null) continue;
                if (!A11yTextReader.IsTextComponent(grafico)) continue;

                // Misma exclusion que P1 y P4: lo decorativo no se juzga.
                var etiqueta = grafico.GetComponent<GossipA11yLabel>();
                if ((Object)etiqueta != null && etiqueta.decorative) continue;

                var rect = grafico.rectTransform;
                if ((Object)rect == null) continue;

                TextReading lectura;
                if (!A11yTextReader.TryRead(grafico, out lectura)) continue;
                if (!lectura.HasPreferredSize) continue;

                var cabe = CabeEnSuCaja(lectura, rect);
                if (cabe.HasValue)
                {
                    conCaja++;
                    if (cabe.Value) ajustados++;
                }

                var holgura = HayHuecoAlDoble(lectura, rect);
                if (holgura.HasValue)
                {
                    conMedidaDeHolgura++;
                    if (holgura.Value) conHolgura++;
                }
            }

            Emitir(ClaveDeAjuste, ajustados, conCaja, true);
            Emitir(ClaveDeHolgura, conHolgura, conMedidaDeHolgura, true);
            Emitir(ClaveDeHolguraMedible, conMedidaDeHolgura, conCaja, false);
        }

        // ---------- P4 ----------

        // ---------- P4 y P2: acumuladores de sesion ----------

        /// <summary>
        /// ACC-120. P4 y P2 salen del unico bloque que depende de la camara, y
        /// medir un instante no vale para ellos: medido el 28-oct-2026 en gafas,
        /// a los 2 s la escena tiene 14 graficos y UNO vivo, cero textos; los 7
        /// textos del censo se encienden en los segundos 11 y 23, al coger un
        /// organo y al abrir el quiz, y los 7 entran en camara. Con una sola foto
        /// P4 emitia 0 de 0 para siempre.
        ///
        /// Se acumula por identidad del componente, no por muestreo: un texto
        /// visto en treinta barridos cuenta UNA vez. Eso no es un detalle, es la
        /// condicion para que el denominador siga siendo -textos- y no
        /// -observaciones de texto-, porque el backend suma numeradores y
        /// denominadores de todas las filas de una clave.
        /// </summary>
        private readonly HashSet<int> _textoEnCamara = new HashSet<int>();
        private readonly HashSet<int> _textoMedible = new HashSet<int>();
        private readonly Dictionary<int, bool> _textoConforme =
            new Dictionary<int, bool>();

        /// <summary>
        /// P2 no filtra por tronco de vision, asi que su poblacion es -graficos
        /// no textuales que estuvieron vivos alguna vez-. La etiqueta es un campo
        /// del componente y no cambia durante la sesion, asi que -descrito alguna
        /// vez- y -descrito siempre- son el mismo conjunto.
        /// </summary>
        private readonly HashSet<int> _noTextoVivo = new HashSet<int>();
        private readonly HashSet<int> _noTextoDescrito = new HashSet<int>();
        /// <summary>
        /// ACC-126. El sello de hora de la escena en curso. Todas las fotos de
        /// P4 y P2 viajan con EL MISMO, para que la clave unica del backend
        /// -- appId, sessionID, metricKey, scope, sceneId, timestampUtc -- sea
        /// la misma y la fila se ACTUALICE en vez de sumarse otra.
        /// </summary>
        private string _selloDeLaEscena;

        /// <summary>
        /// ACC-127. Lo ultimo que se envio, para no mandar dos veces el mismo
        /// valor. Los acumuladores solo CRECEN -- un id entra en el conjunto y no
        /// sale, y _textoConforme solo va de true a false --, asi que el numero de
        /// valores distintos de una escena esta acotado por sus textos y no por su
        /// duracion. Medido en Hospital: los siete textos se encienden en los
        /// segundos 11 y 23, o sea DOS valores nuevos en 90 s. El reloj de 10 s
        /// que esto sustituye mandaba nueve fotos para decir dos cosas.
        ///
        /// Arrancan a -1 y no a 0 para que la primera foto de cada escena salga
        /// siempre: un 0 de 0 medido es un valor y tiene que viajar.
        /// </summary>
        private int _ultimoConformes = -1;
        private int _ultimoMedibles = -1;
        private int _ultimoEnCamara = -1;
        private int _ultimoDescritos = -1;
        private int _ultimoNoTextoVivo = -1;
        private bool _hayMuestras;

        /// <summary>
        /// Un muestreo. Devuelve lo que costo, en segundos, para que el reloj
        /// pueda espaciarse solo si en una escena grande sale caro. Sin camara no
        /// muestrea y devuelve 0: una fila con distancia inventada es peor que no
        /// tener fila.
        /// </summary>
        public float MuestrearTextos()
        {
            var camara = Camera.main;
            if ((Object)camara == null) return 0f;

            var inicio = Time.realtimeSinceStartup;
            MuestrearTextos(camara);
            return Time.realtimeSinceStartup - inicio;
        }

        /// <summary>
        /// ACC-121. El cierre de la ultima escena. EmitirTextos cuelga del reloj
        /// -- cambio de escena, OnDisable, quit y pause --, pero ese reloj vive
        /// en el host persistente y este tracker puede no vivir ahi: Ensure
        /// respeta una instancia que el integrador haya colgado de un objeto de
        /// escena, GossipManager.cs:290-295, y entonces la descarga de esa escena
        /// se lleva los acumuladores antes de que el reloj pueda pedirlos, porque
        /// sceneLoaded corre DESPUES de la descarga.
        ///
        /// Emitiendo aqui, el estado se vacia donde muere, viva donde viva. Y no
        /// duplica: EmitirTextos no emite si no hay muestras.
        /// </summary>
        private void OnDisable()
        {
            EmitirTextos();
        }

        /// <summary>
        /// Emite P4 y P2 con lo acumulado. NO vacia: de eso se encarga Evaluate
        /// al abrir la escena siguiente.
        ///
        /// IDEMPOTENTE a proposito -- la llaman el muestreo, el cambio de escena,
        /// OnDisable, OnApplicationQuit y OnApplicationPause, y en Quest varias
        /// pueden caer casi juntas --, asi que sin muestras no emite nada, y si
        /// las cifras no cambiaron desde el ultimo envio tampoco. Dos filas no
        /// darian error: las sumaria el backend y el denominador saldria del
        /// doble.
        /// </summary>
        public void EmitirTextos()
        {
            if (!_hayMuestras) return;

            int conformes = 0;
            foreach (var par in _textoConforme)
            {
                if (par.Value) conformes++;
            }

            // ACC-127. Reenvio por cambio y no por reloj: con las cinco cifras
            // iguales a las ultimas enviadas, este envio seria un payload
            // identico a la fila que ya esta en el backend.
            if (conformes == _ultimoConformes
                && _textoMedible.Count == _ultimoMedibles
                && _textoEnCamara.Count == _ultimoEnCamara
                && _noTextoDescrito.Count == _ultimoDescritos
                && _noTextoVivo.Count == _ultimoNoTextoVivo) return;

            _ultimoConformes = conformes;
            _ultimoMedibles = _textoMedible.Count;
            _ultimoEnCamara = _textoEnCamara.Count;
            _ultimoDescritos = _noTextoDescrito.Count;
            _ultimoNoTextoVivo = _noTextoVivo.Count;

            Emitir(ClaveDeTexto, conformes, _textoMedible.Count, true, _selloDeLaEscena);
            Emitir(ClaveDeTextosMedidos, _textoMedible.Count, _textoEnCamara.Count, false, _selloDeLaEscena);
            Emitir(ClaveDeAlternativa, _noTextoDescrito.Count, _noTextoVivo.Count, true, _selloDeLaEscena);

            // NO se vacia. Esta foto se reenvia con el mismo sello cada vez que
            // cambia, asi que cada envio sustituye al anterior y el acumulado
            // sigue creciendo. Vaciar aqui haria que el envio siguiente mandase
            // ceros y borrase lo que ya se habia medido.
            //
            // Quien vacia es Evaluate, al abrir la escena siguiente.
        }

        private void MuestrearTextos(Camera camara)
        {
            var graficos = FindObjectsByType<Graphic>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

            var planos = GeometryUtility.CalculateFrustumPlanes(camara);
            var esquinas = new Vector3[4];
            var ojo = camara.transform.position;

            // ACC-120. Los contadores ya no son locales de una pasada: viven en
            // los acumuladores de arriba, porque P4 y P2 se emiten una vez por
            // escena con lo visto en toda la sesion, no con lo visto a los 2 s.
            _hayMuestras = true;

            foreach (var grafico in graficos)
            {
                if ((Object)grafico == null) continue;
                // P2 va por aqui: lo que NO es texto es justo lo que esta rama
                // descartaba. Sin barrido nuevo -- este `graficos` ya esta recorrido --
                // y sin componente nuevo: se lee el MISMO GossipA11yLabel que ya leen
                // P1, P4 y P3, no el GossipA11yAltText que vive en el paquete sin un
                // solo lector. Pedir dos componentes para decir lo mismo seria trabajo
                // del cliente a cambio de nada.
                //
                // NO se filtra por tronco de vision, al reves que el texto de abajo y a
                // proposito: una imagen necesita descripcion mire quien mire hacia donde
                // mire. Atarlo a la mirada haria que P2 cambiase de valor segun donde
                // estuviera la cabeza en ese frame.
                if (!A11yTextReader.IsTextComponent(grafico))
                {
                    _noTextoVivo.Add(grafico.GetInstanceID());

                    var descripcion = grafico.GetComponent<GossipA11yLabel>();
                    if (
                        (Object)descripcion != null
                        && (
                            descripcion.decorative
                            || !string.IsNullOrWhiteSpace(descripcion.label)
                        )
                    )
                    {
                        _noTextoDescrito.Add(grafico.GetInstanceID());
                    }

                    continue;
                }

                // Misma exclusion que P1: lo marcado como decorativo no se
                // juzga, aqui tampoco.
                var etiqueta = grafico.GetComponent<GossipA11yLabel>();
                if ((Object)etiqueta != null && etiqueta.decorative) continue;

                var rect = grafico.rectTransform;
                if ((Object)rect == null) continue;

                var id = grafico.GetInstanceID();

                TextReading lectura;
                var leido = A11yTextReader.TryRead(grafico, out lectura);

                rect.GetWorldCorners(esquinas);
                var caja = new Bounds(esquinas[0], Vector3.zero);
                for (int i = 1; i < 4; i++) caja.Encapsulate(esquinas[i]);

                // El requisito de Daydream habla de lo que el usuario ve. Lo
                // que esta fuera del tronco de vision no entra al denominador,
                // y eso significa que esta metrica depende de hacia donde
                // mirase la persona en ese instante. Esta escrito en el
                // catalogo y es la regla, no un efecto secundario.
                if (!GeometryUtility.TestPlanesAABB(planos, caja)) continue;
                _textoEnCamara.Add(id);

                if (!leido) continue;
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

                _textoMedible.Add(id);

                // CASO PEOR. Un texto cuenta como conforme solo si lo fue TODAS
                // las veces que se le vio: si en algun muestreo quedo demasiado
                // lejos o demasiado pequeno, deja de contar. Mismo criterio que
                // ACC-118 uso para R5.
                var cumple = A11yGeometry.TextoCumple(altoDeLinea, distancia);
                bool anterior;
                _textoConforme[id] = _textoConforme.TryGetValue(id, out anterior)
                    ? anterior && cumple
                    : cumple;
            }

            // ACC-120. Aqui ya no se emite: lo hace EmitirTextos al cerrar la escena.
        }

        /// <summary>
        /// R6. Un bloque CABE si lo que el texto pide no supera su caja. Se
        /// compara en coordenadas LOCALES -- el preferido y el rect estan en las
        /// mismas --, asi que la escala no entra: una caja el doble de grande con
        /// su texto el doble de grande sigue cabiendo igual.
        ///
        /// El ancho solo se mira cuando el texto ocupa UNA linea. En cuanto hay
        /// dos, el ancho preferido ya es el de la caja, porque es la caja la que
        /// partio el texto: compararlos seria preguntar dos veces lo mismo.
        ///
        /// Devuelve null cuando la caja no tiene medida, y entonces el bloque no
        /// entra al denominador: sin caja no hay nada que desbordar.
        /// </summary>
        private static bool? CabeEnSuCaja(TextReading lectura, RectTransform rect)
        {
            var caja = rect.rect;
            if (caja.height <= 0f || caja.width <= 0f) return null;

            if (lectura.PreferredHeight > caja.height * ToleranciaDeAjuste)
            {
                return false;
            }

            if (
                lectura.HasLineCount
                && lectura.LineCount == 1
                && lectura.PreferredWidth > caja.width * ToleranciaDeAjuste
            )
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// R5. Un bloque tiene holgura si SIGUE cabiendo con el cuerpo al doble.
        /// 1.4.4 pide 200 % sin perdida de contenido, y ese 200 % lo fija el
        /// registro de ACC-41; aqui solo se aplica.
        ///
        /// El factor del alto no es siempre dos. Si quien parte el texto es el
        /// ancho de la caja, al doblar el cuerpo cada glifo mide el doble, en
        /// cada linea caben la mitad, y el numero de lineas se dobla tambien: el
        /// alto se va a CUATRO veces, no a dos. Si las lineas son saltos escritos
        /// a mano, el texto no se reparte y el alto solo se dobla.
        ///
        /// La diferencia se decide con lo que ya se leyo: si hay mas lineas que
        /// saltos escritos, el ancho esta partiendo el texto.
        /// </summary>
        private static bool? HayHuecoAlDoble(TextReading lectura, RectTransform rect)
        {
            var caja = rect.rect;
            if (caja.height <= 0f || caja.width <= 0f) return null;

            // ACC-118. Aqui habia un `if (!lectura.HasLineCount ...) return null;`
            // que tiraba el bloque entero. No tiene respaldo normativo: 1.4.4
            // solo exceptua subtitulos e imagenes de texto, y "texto que todavia
            // no se ha dibujado" no es ninguna de las dos. Era una limitacion
            // nuestra -- `textInfo.lineCount` vale 0 mientras TMP no haya hecho su
            // layout -- disfrazada de exencion.
            //
            // Medido el 27-oct-2026: UNA fila de R5 con poblacion en cinco meses y
            // dieciseis filas, y esa fue 0/1. En Editor y en visor, 0/0.
            //
            // Sin recuento no se sabe si el ancho parte el texto, asi que se juzga
            // con el CASO PEOR, factor 4. Si cabe al cuadruple, cabe seguro y el
            // aprobado se sostiene. Si NO cabe, no se afirma el fallo: seria
            // suspender con el mas duro de los dos supuestos. Ese bloque queda
            // indeterminado, fuera del denominador, y lo cuenta la sonda de
            // cobertura. WCAG2ICT pide medir "up to the text sizing capabilities of
            // the platform" para software no web; lo que no se puede afirmar no se
            // publica ni como aprobado ni como suspenso.
            if (!lectura.HasLineCount || lectura.LineCount <= 0)
            {
                if (lectura.PreferredHeight * 4f <= caja.height * ToleranciaDeAjuste)
                {
                    return true;
                }

                return null;
            }

            var escritas = lectura.HasText ? LineasEscritas(lectura.Text) : 1;
            var loParteElAncho = lectura.LineCount > escritas;
            var factor = loParteElAncho ? 4f : 2f;

            if (lectura.PreferredHeight * factor > caja.height * ToleranciaDeAjuste)
            {
                return false;
            }

            // Si el ancho no esta partiendo el texto, la linea mas larga tambien
            // se dobla y tiene que seguir entrando.
            if (
                !loParteElAncho
                && lectura.PreferredWidth * 2f > caja.width * ToleranciaDeAjuste
            )
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Las lineas que trae escritas el propio texto. Un texto sin saltos es
        /// una linea escrita, aunque en pantalla se reparta en seis.
        /// </summary>
        private static int LineasEscritas(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return 1;

            var saltos = 1;
            for (int i = 0; i < texto.Length; i++)
            {
                if (texto[i] == '\n') saltos++;
            }

            return saltos;
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

        private void Emitir(string clave, int numerador, int denominador, bool esCheck, string sello = null)
        {
            var instancia = Gossip.Instance;
            if ((Object)instancia == null) return;

            var tracker = instancia.A11yTracker;
            if (tracker == null) return;

            var meta = BuildMeta();

            if (esCheck)
            {
                tracker.CapCheck(clave, numerador, denominador, AmbitoDeEscena, meta, sello);
            }
            else
            {
                tracker.CapSignal(clave, numerador, denominador, AmbitoDeEscena, meta, sello);
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
