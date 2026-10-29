using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GossipSDK.Tracking.A11y
{
    /// <summary>
    /// ACC-111. El reloj de las tres pasadas de accesibilidad.
    ///
    /// Hasta hoy cada una llevaba su propio disparador -- Start y sceneLoaded, el
    /// mismo codigo repetido en tres ficheros -- y median al final del frame en que
    /// carga la escena. Fallaban dos cosas a la vez.
    ///
    /// La primera: en ese frame la interfaz de la escena todavia no existe. Medido
    /// el 25-oct-2026 en la escena Hospital, sesion 11ac70ac: la pasada de
    /// geometria conto UN solo Graphic en toda la escena -- alt_text_coverage 0/1
    /// -- mientras el contraste y la narracion contaban SIETE textos en esa misma
    /// escena y ese mismo instante. P4, P2, R5 y R6 salian a cero por esto, no por
    /// falta de medidor.
    ///
    /// La segunda: con tres relojes sueltos, cada pasada podia caer en un frame
    /// distinto y medir una escena distinta. Aqui hay uno solo y las tres miden lo
    /// mismo.
    ///
    /// Una sola medicion por escena, no dos: el upsert del backend lleva el
    /// timestamp dentro del filtro, asi que una segunda emision no reemplaza a la
    /// primera, se SUMA a ella y el panel publicaria denominadores inflados.
    /// </summary>
    public class A11yPassScheduler : MonoBehaviour
    {
        /// <summary>
        /// Lo que se espera antes de medir. Dos segundos: lo que tarda en existir
        /// una interfaz que la escena instancia en su propio Start, y lo bastante
        /// poco para que una sesion de un minuto llegue a emitir.
        ///
        /// ACC-114. Hasta la 2.0.48 delante de esta espera habia un
        /// WaitForEndOfFrame, resto de cuando cada pasada media al final del frame
        /// en que cargaba la escena. No aportaba -- el asentado lo cubren los dos
        /// segundos -- y era el unico punto donde esta corrutina podia quedarse
        /// colgada sin decir nada: en el Editor con Unity 6000.4.1f1 la traza
        /// incondicional de Evaluar() no imprimio ni una vez, con el componente
        /// montado, habilitado y los tres medidores presentes en la escena.
        /// </summary>
        public const float EsperaDeAsentado = 2f;

        private bool _medicionPendiente;

        /// <summary>
        /// ACC-120. Cada cuanto se vuelve a mirar la escena para P4 y P2. Un
        /// segundo sobra para lo que hay que cazar: medido el 28-oct-2026 en
        /// gafas, el panel de organos se enciende en el segundo 11 y el quiz en
        /// el 23, y se quedan puestos. Lo que no cabe es muestrear por frame.
        /// </summary>
        public const float IntervaloDeMuestreo = 1f;

        /// <summary>
        /// La parte del tiempo de reloj que este medidor puede gastarse en mirar
        /// la escena. NO es una medicion: es una decision de producto, el 1 por
        /// ciento que Daniel fijo el 28-oct-2026.
        ///
        /// El barrido cuesta lo que cuesta la escena del cliente, y eso no lo
        /// sabemos. En la escena medida -- 14 graficos -- son 0,37 ms de media y
        /// 1,68 ms el primero, o sea 0,037 por ciento a un muestreo por segundo.
        /// Una escena de 500 graficos costaria el orden de 13 ms, y entonces
        /// esta guarda espacia sola a 1,3 s en vez de comerse un frame. Un
        /// umbral fijo en milisegundos seria un numero inventado sobre escenas
        /// que no hemos medido; una fraccion del reloj sale del propio coste.
        /// </summary>
        public const float PresupuestoDeCpu = 0.01f;

        /// <summary>
        /// ACC-126. Cada cuanto sale de nuevo la foto acumulada de P4 y P2. No es
        /// el intervalo de muestreo: mirar la escena cuesta 0,37 ms de media y
        /// enviar no, asi que se separan. Cada reenvio lleva el sello de la escena
        /// -- el instante en que asento --, y el ingest hace upsert por
        /// (sesion, clave, escena, sello), asi que el envio nuevo SOBREESCRIBE al
        /// anterior en vez de sumar una fila: el denominador de P4 sigue siendo el
        /// numero de textos de la escena, no su multiplo.
        ///
        /// Por que existe: medido el 28-oct-2026, 1 de 2 corridas con gafas perdio
        /// su fila de P4. La unica emision salia en el cierre y el POST se muere
        /// con el proceso. Con esto el cierre deja de ser el unico momento en que
        /// la metrica puede salvarse.
        /// </summary>
        public const float IntervaloDeReenvio = 10f;

        private float _ultimoReenvio;

        private float _esperaDeMuestreo = IntervaloDeMuestreo;
        private Coroutine _muestreo;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += AlCargarEscena;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= AlCargarEscena;
            EmitirAcumulado();
        }

        /// <summary>
        /// El molde del PR #107. En Quest OnApplicationQuit NO corre al cerrar y
        /// OnApplicationPause si, asi que la emision cuelga de las tres. En el
        /// tracker es idempotente, que es lo que hace seguro llamarla de mas.
        /// </summary>
        private void OnApplicationQuit()
        {
            EmitirAcumulado();
        }

        private void OnApplicationPause(bool pausado)
        {
            if (pausado) EmitirAcumulado();
        }

        private void Start()
        {
            // La escena que ya estaba cargada cuando aparecio este componente puede
            // no disparar sceneLoaded. Si las dos vias caen en el mismo frame, el
            // guardia de abajo deja una sola medicion.
            StartCoroutine(MedirCuandoAsiente());
        }

        private void AlCargarEscena(Scene escena, LoadSceneMode modo)
        {
            // La escena que se va se cierra aqui: lo acumulado es suyo y se emite
            // antes de que la nueva empiece a contar.
            EmitirAcumulado();

            StartCoroutine(MedirCuandoAsiente());
        }

        private IEnumerator MedirCuandoAsiente()
        {
            if (_medicionPendiente)
                yield break;

            _medicionPendiente = true;
            yield return new WaitForSecondsRealtime(EsperaDeAsentado);
            _medicionPendiente = false;

            Evaluar();

            if (_muestreo != null) StopCoroutine(_muestreo);
            // ACC-126. Un reenvio en la primera muestra: la corrida que se
            // pierde es la corta, asi que la foto tiene que existir en el
            // segundo 1 y no en el 10.
            _ultimoReenvio = Time.realtimeSinceStartup - IntervaloDeReenvio;

            _muestreo = StartCoroutine(MuestrearHastaElFinal());
        }

        /// <summary>
        /// El muestreo de P4 y P2. WaitForSecondsRealtime y no WaitForSeconds: si
        /// la app pone timeScale a 0 -- una pausa, un menu -- el texto se sigue
        /// viendo, asi que el medidor no puede pararse con el juego.
        /// </summary>
        private IEnumerator MuestrearHastaElFinal()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(_esperaDeMuestreo);

                var geometria = FindObjectOfType<A11yGeometryPassTracker>();
                if ((Object)geometria == null) continue;

                var coste = geometria.MuestrearTextos();
                _esperaDeMuestreo = Mathf.Max(
                    IntervaloDeMuestreo,
                    coste / PresupuestoDeCpu
                );

                if (Time.realtimeSinceStartup - _ultimoReenvio >= IntervaloDeReenvio)
                {
                    _ultimoReenvio = Time.realtimeSinceStartup;
                    geometria.EmitirTextos();
                }
            }
        }

        private void EmitirAcumulado()
        {
            if (_muestreo != null)
            {
                StopCoroutine(_muestreo);
                _muestreo = null;
            }

            var geometria = FindObjectOfType<A11yGeometryPassTracker>();
            if ((Object)geometria != null) geometria.EmitirTextos();
        }

        /// <summary>
        /// Las tres en el mismo frame. Se buscan en la escena y no con GetComponent:
        /// GossipManager no anade la suya si el integrador ya tenia una colgada de
        /// otro objeto, y en ese caso no comparte GameObject con este reloj.
        /// </summary>
        private static void Evaluar()
        {
            var geometria = FindObjectOfType<A11yGeometryPassTracker>();
            var contraste = FindObjectOfType<A11yContrastPassTracker>();
            var narracion = FindObjectOfType<TextNarrationCoverageTracker>();

            if ((Object)geometria != null) geometria.Evaluate();
            if ((Object)contraste != null) contraste.Evaluate();
            if ((Object)narracion != null) narracion.Evaluate();
        }
    }
}
