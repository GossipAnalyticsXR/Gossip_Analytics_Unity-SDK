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
            // La escena que ya estaba cargada cuando aparecio este componente puede
            // no disparar sceneLoaded. Si las dos vias caen en el mismo frame, el
            // guardia de abajo deja una sola medicion.
            StartCoroutine(MedirCuandoAsiente());
        }

        private void AlCargarEscena(Scene escena, LoadSceneMode modo)
        {
            StartCoroutine(MedirCuandoAsiente());
        }

        private IEnumerator MedirCuandoAsiente()
        {
            if (_medicionPendiente)
                yield break;

            _medicionPendiente = true;
            yield return new WaitForSeconds(EsperaDeAsentado);
            _medicionPendiente = false;

            Evaluar();
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
