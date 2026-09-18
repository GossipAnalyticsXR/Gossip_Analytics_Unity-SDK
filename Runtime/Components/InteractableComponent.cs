using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using GossipSDK.Core;
using GossipSDK.Tracking;
using GossipSDK.Tracking.GameplayMetrics;
using GossipSDK.Heatmaps;
using System.Collections.Generic;
using System.Reflection;
using GossipSDK.Core.XR;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class InteractableComponent : MonoBehaviour
    {
        [Tooltip("E.g. Pickup, Button, Open, Inspect")]
        public bool autoTriggerOnStart = false;
        public bool autoStartOnEnable = false;

        [Header("Image Capture Optimization")]
        public bool captureImageOnInteraction = true;
        public float minTimeBetweenImages = 5f;
        public float objectCooldown = 30f;

        private static Dictionary<int, float> lastCaptureTimes = new Dictionary<int, float>();
        private static float globalLastImageTime;

        [Header("Heatmap")]
        public bool registerHeatmapHit = true;
        public float heatmapCellSize = 1f;
        public Vector2 heatmapWorldMin = new Vector2(-50, -50);
        public Vector2 heatmapWorldMax = new Vector2(50, 50);

        private string currentInteractionId;
        private double currentInteractionStartTimeRealtime;
        private string _lastInteractionType;

        [Header("Heatmap Flush")]
        public float flushIntervalSeconds = 10f;

        [Header("Interaction")]
        [Tooltip("Interactores que NO cuentan como interaccion de usuario. Un objeto que " +
                 "descansa en su socket esta seleccionado desde el frame 0, y eso es el estado " +
                 "de reposo de la escena, no algo que el usuario haga. Se compara por subcadena " +
                 "contra el nombre del tipo, sin distinguir mayusculas.")]
        public string[] interactoresIgnorados = new string[] { "Socket", "Snap" };

        private static float lastFlushTime;

        private InteractionTracker Tracker => Gossip.Instance?.InteractionTracker;

        private static HeatmapManager heatmap;
        private static string heatmapScene;


        // -----------------------------------------------------------------------
        // XR Framework Auto-Wiring (reflection-only, framework-agnostic)
        // -----------------------------------------------------------------------

        private enum SelectionKind { Bool, CollectionCount }

        private struct FrameworkAdapter
        {
            public string TypeName;        // Full or short type name to locate via reflection
            public string SelectionMember; // Property name that indicates active selection
            public SelectionKind Kind;     // Bool: property is bool; CollectionCount: .Count > 0
        }

        // Add new interaction frameworks here -- no compile-time dependency required.
        private static readonly FrameworkAdapter[] _adapters = new FrameworkAdapter[]
        {
            // Unity XRI 2.x / 3.x -- IXRSelectInteractable.interactorsSelecting
            new FrameworkAdapter
            {
                TypeName        = "IXRSelectInteractable",
                SelectionMember = "interactorsSelecting",
                Kind            = SelectionKind.CollectionCount,
            },
            // Meta Interaction SDK v60+ -- IInteractable.SelectingInteractorViews
            new FrameworkAdapter
            {
                TypeName        = "IInteractable",
                SelectionMember = "SelectingInteractorViews",
                Kind            = SelectionKind.CollectionCount,
            },
        };

        // Resolved once at startup (AppDomain scan, same pattern as InstrumentationManagerWindow.ResolveXrTypes)
        private static readonly System.Type[] _resolvedTypes = new System.Type[2];
        private static bool _typesResolved;

        // Per-instance auto-wire state
        private Component    _xrInteractable;    // matched component (Unity Object null check)
        private int          _adapterIndex = -1; // which adapter matched (-1 = none)
        private PropertyInfo _selectionPropInfo; // cached PropertyInfo for SelectionMember
        private PropertyInfo _countPropInfo;     // cached PropertyInfo for Count on the collection
        private bool         _wasSelected;
        private bool         _primerSondeo = true;
        private string       _uiLabel;      // nombre de clase del Selectable, si lo hay
        private bool         _uiCableado;   // true si se engancho el onClick de Unity UI
        private string       _xrLabel;           // interactionType label = component type name

        private static void ResolveAdapterTypes()
        {
            if (_typesResolved) return;
            _typesResolved = true;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var t in asm.GetTypes())
                    {
                        for (int i = 0; i < _adapters.Length; i++)
                        {
                            if (_resolvedTypes[i] == null &&
                                (t.Name == _adapters[i].TypeName ||
                                 t.FullName == _adapters[i].TypeName))
                            {
                                _resolvedTypes[i] = t;
                            }
                        }
                    }
                }
                catch { /* skip assemblies that refuse reflection */ }
            }
        }

        private void TryWireXrFramework()
        {
            ResolveAdapterTypes();
            for (int i = 0; i < _adapters.Length; i++)
            {
                if (_resolvedTypes[i] == null) continue;
                Component comp = GetComponent(_resolvedTypes[i]);
                if ((UnityEngine.Object)comp == null) continue;

                PropertyInfo prop = _resolvedTypes[i].GetProperty(
                    _adapters[i].SelectionMember,
                    BindingFlags.Public | BindingFlags.Instance);

                // Si el tipo casa por nombre pero no expone la propiedad que esperamos, NO es
                // nuestro framework: se sigue buscando. Antes se aceptaba el match y se hacia
                // break igual, con lo que un IInteractable de otra libreria -- un nombre muy
                // comun -- dejaba el componente "cableado" a algo inutil, mudo, y sin llegar a
                // probar los adaptadores restantes.
                if (prop == null) continue;

                _xrInteractable    = comp;
                _adapterIndex      = i;
                _selectionPropInfo = prop;
                string raw = comp.GetType().Name;
                string lbl = raw;
                if (lbl.EndsWith("Interactable")) lbl = lbl.Substring(0, lbl.Length - "Interactable".Length);
                if (lbl.StartsWith("XR")) lbl = lbl.Substring(2);
                _xrLabel = string.IsNullOrEmpty(lbl) ? raw : lbl;
                break; // use first matching framework
            }

            if (_adapterIndex < 0)
                TryWireUnityUi();
        }

        // Un boton de Unity UI no es un interactable de XR: no implementa IXRSelectInteractable,
        // asi que el sondeo de Update no lo ve nunca. Medido el 15-sep-2026 en VR-Anatomy-Lab:
        // QuizButton lleva UnityEngine.UI.Button + Image + este componente, su onClick va a
        // GameObject.SetActive, y no habia emitido un solo mensaje en toda su vida. Un no-op
        // mudo, que es peor que un error.
        //
        // Se engancha al onClick, que es el UnityEvent SIN argumentos de Button. Los eventos con
        // argumento -- onValueChanged de Toggle, Slider o Dropdown -- necesitan un UnityAction<T>
        // con el tipo correcto y quedan fuera de este cambio a proposito: se avisan por el log en
        // vez de fallar en silencio.
        private void TryWireUnityUi()
        {
            try
            {
                Component selectable = null;
                foreach (var c in GetComponents<Component>())
                {
                    if ((UnityEngine.Object)c == null) continue;
                    if (EsSelectableDeUi(c.GetType())) { selectable = c; break; }
                }

                if ((UnityEngine.Object)selectable == null)
                {
                    Debug.LogWarning("[Interactable] " + gameObject.name + ": no hay interactable de XR ni Selectable de UI. Este componente no va a medir nada por si solo; llama a OnInteractStart, OnInteractEnd o OnInteractInstant desde tu codigo.");
                    return;
                }

                _uiLabel = selectable.GetType().Name;

                object evento = LeerMiembro(selectable, "onClick");
                if (evento == null)
                {
                    Debug.LogWarning("[Interactable] " + gameObject.name + ": " + _uiLabel + " no expone onClick. Engancha OnInteractInstant a su evento a mano.");
                    return;
                }

                MethodInfo add = evento.GetType().GetMethod("AddListener", BindingFlags.Public | BindingFlags.Instance);
                if (add == null) return;

                ParameterInfo[] ps = add.GetParameters();
                if (ps.Length != 1) return;

                MethodInfo destino = GetType().GetMethod("PulsacionDeUi", BindingFlags.NonPublic | BindingFlags.Instance);
                if (destino == null) return;

                Delegate manejador = Delegate.CreateDelegate(ps[0].ParameterType, this, destino, false);
                if (manejador == null)
                {
                    Debug.LogWarning("[Interactable] " + gameObject.name + ": el onClick de " + _uiLabel + " lleva argumento y todavia no se engancha solo.");
                    return;
                }

                add.Invoke(evento, new object[] { manejador });
                _uiCableado = true;
            }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        private static bool EsSelectableDeUi(Type t)
        {
            while (t != null)
            {
                if (t.FullName == "UnityEngine.UI.Selectable") return true;
                t = t.BaseType;
            }
            return false;
        }

        private static object LeerMiembro(object obj, string nombre)
        {
            if (obj == null) return null;
            Type t = obj.GetType();
            PropertyInfo p = t.GetProperty(nombre, BindingFlags.Public | BindingFlags.Instance);
            if (p != null) { try { return p.GetValue(obj); } catch { return null; } }
            FieldInfo f = t.GetField(nombre, BindingFlags.Public | BindingFlags.Instance);
            if (f != null) { try { return f.GetValue(obj); } catch { return null; } }
            return null;
        }

        // La pulsacion es instantanea por definicion: no tiene principio y fin que medir.
        private void PulsacionDeUi()
        {
            OnInteractInstant(string.IsNullOrEmpty(_uiLabel) ? "Press" : _uiLabel);
        }

        // Cuenta los interactores que SI son una interaccion de usuario. Un socket sosteniendo
        // el objeto no lo es: esta seleccionado desde el frame 0, y por eso cada arranque de la
        // app emitia un start por objeto instrumentado que no cerraba nunca, con una duracion
        // medida desde el arranque en vez de desde el agarre.
        //
        // Medido el 14-sep-2026 sobre interactiontrackings: de 979 interacciones, 662 (68 %)
        // nacian en rafagas de 3 o mas dentro de 200 ms -- 3,94 objetos por rafaga, un bucle, no
        // una mano -- y cerraban al 13,9 %. Lo que hacia una persona (317, 1,02 por ventana)
        // cerraba al 74,8 %.
        //
        // XRI no ofrece una interfaz que distinga un socket: XRSocketInteractor hereda de
        // XRBaseInteractor como los demas, asi que por reflexion lo unico disponible es el
        // nombre del tipo. Por eso la lista es configurable en el inspector.
        private int CuentaInteractoresDeUsuario(object coleccion)
        {
            if (coleccion == null) return 0;

            var recorrible = coleccion as System.Collections.IEnumerable;
            if (recorrible == null)
            {
                // No es recorrible: se cae al comportamiento anterior, contar por .Count.
                if (_countPropInfo == null)
                    _countPropInfo = coleccion.GetType().GetProperty("Count");
                if (_countPropInfo == null) return 0;
                return (int)_countPropInfo.GetValue(coleccion);
            }

            int cuenta = 0;
            foreach (var interactor in recorrible)
            {
                if (interactor == null) continue;
                if (EsInteractorIgnorado(interactor.GetType().Name)) continue;
                cuenta++;
            }
            return cuenta;
        }

        private bool EsInteractorIgnorado(string nombreDeTipo)
        {
            if (string.IsNullOrEmpty(nombreDeTipo)) return false;
            if (interactoresIgnorados == null) return false;

            for (int i = 0; i < interactoresIgnorados.Length; i++)
            {
                string patron = interactoresIgnorados[i];
                if (string.IsNullOrEmpty(patron)) continue;
                if (nombreDeTipo.IndexOf(patron, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private void Awake()
        {
            if (!registerHeatmapHit) return;

            string scene = SceneManager.GetActiveScene().name;

            if (heatmap == null || heatmapScene != scene)
            {
                heatmapScene = scene;

                heatmap = new HeatmapManager(
                    sceneName: scene,
                    worldMinXZ: heatmapWorldMin,
                    worldMaxXZ: heatmapWorldMax,
                    cellSizeMeters: heatmapCellSize
                );

                if (Gossip.Instance?.Settings?.EnableDebug == true)
                    Debug.Log($"[Interactable] Heatmap created for scene={scene}");
            }
        }

        private void Start()
        {
            StartCoroutine(WaitAndSend());
        }

        private IEnumerator WaitAndSend()
        {
            yield return new WaitUntil(() => Gossip.Instance != null);
            TryWireXrFramework();

            if (autoTriggerOnStart)  OnInteractInstant("Demo Shoot");
            if (autoStartOnEnable)   OnInteractStart("Demo Start Interaction");
        }

        private void Update()
        {
            if (!registerHeatmapHit || heatmap == null)
                return;

            if (Time.time - lastFlushTime >= flushIntervalSeconds)
            {
                lastFlushTime = Time.time;
                FlushHeatmap();
            }

            // XR Framework auto-wire polling
            if (_adapterIndex >= 0 && (UnityEngine.Object)_xrInteractable != null && _selectionPropInfo != null)
            {
                try
                {
                    object val = _selectionPropInfo.GetValue(_xrInteractable);
                    bool isSelected;
                    if (_adapters[_adapterIndex].Kind == SelectionKind.Bool)
                    {
                        isSelected = (bool)val;
                    }
                    else
                    {
                        isSelected = CuentaInteractoresDeUsuario(val) > 0;
                    }

                    if (_primerSondeo)
                    {
                        // El primer sondeo solo fija el punto de partida. Si al cablear el
                        // objeto ya esta cogido, eso no es una interaccion que hayamos visto
                        // empezar, y emitir un start aqui seria inventarse un agarre.
                        _primerSondeo = false;
                        _wasSelected = isSelected;
                    }
                    else
                    {
                        if (isSelected && !_wasSelected) OnInteractStart(_xrLabel);
                        if (!isSelected && _wasSelected) OnInteractEnd(_xrLabel);
                        _wasSelected = isSelected;
                    }
                }
                catch { /* reflection error -- silently skip */ }
            }
        }

        private void OnDisable()
        {
            if (string.IsNullOrEmpty(currentInteractionId)) return;

            // Close any open interaction -- developer-called or auto-started
            var t = Tracker;
            if (t == null) { currentInteractionId = null; return; }

            t.CapInteractionCancelled(
                gameObject.name, gameObject.tag,
                _lastInteractionType ?? "Unknown",
                XRInteractionInputResolver.GetCurrentInputType().ToString(),
                transform.position.x, transform.position.y, transform.position.z,
                SceneManager.GetActiveScene().name,
                currentInteractionId);

            currentInteractionId = null;
            _wasSelected = false; // reset auto-wire state on disable
            _primerSondeo = true;  // al re-activarse, el primer sondeo vuelve a ser linea base
        }

        public void OnInteractInstant(string interactionType)
        {
            try
            {
                Vector3 pos = transform.position;
                string scene = SceneManager.GetActiveScene().name;
                string ts = DateTime.UtcNow.ToString("o");

                var inputType = XRInteractionInputResolver.GetCurrentInputType().ToString();

                Tracker?.CapInteractionInstant(
                    gameObject.name,
                    gameObject.tag,
                    inputType,
                    interactionType,
                    pos,
                    scene,
                    ts
                );

                if (registerHeatmapHit)
                    heatmap?.RegisterHit(pos);

                TryCaptureImage(interactionType);
            }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        public void OnInteractStart(string interactionType)
        {
            try
            {
                if (!string.IsNullOrEmpty(currentInteractionId))
                    OnInteractEnd(interactionType);

                currentInteractionId = Guid.NewGuid().ToString();
                _lastInteractionType = interactionType;
                currentInteractionStartTimeRealtime = Time.realtimeSinceStartupAsDouble;

                var inputType = XRInteractionInputResolver.GetCurrentInputType().ToString();

                Vector3 pos = transform.position;
                string scene = SceneManager.GetActiveScene().name;
                string ts = DateTime.UtcNow.ToString("o");

                // El `start` NO se tira si el tracker todavia no esta montado.
                //
                // `Tracker` es `Gossip.Instance?.InteractionTracker`, y `GossipManager` anade los
                // componentes a lo largo de varios frames: en la primera interaccion de una sesion
                // puede ser null. Con el `?.` de antes, ese `start` desaparecia sin error, y el
                // `end` -que llega segundos despues, ya con tracker- si salia: una interaccion con
                // final y sin principio, que el dashboard no puede emparejar.
                //
                // Medido en Mongo el 17-sep-2026 sobre 90 dias: 9 `end` sin `start` en 140 sesiones,
                // 4 de ellos en la POSICION 1 de su sesion cuando por azar se esperarian 1,22, y una
                // sesion con tres seguidos en los primeros seis segundos.
                var tracker = Tracker;
                if (tracker != null)
                {
                    tracker.CapInteractionStart(
                        currentInteractionId,
                        gameObject.name,
                        gameObject.tag,
                        inputType,
                        interactionType,
                        pos,
                        scene,
                        ts
                    );
                }
                else
                {
                    StartCoroutine(EnviarStartCuandoHayaTracker(
                        currentInteractionId,
                        gameObject.name,
                        gameObject.tag,
                        inputType,
                        interactionType,
                        pos,
                        scene,
                        ts));
                }

                if (registerHeatmapHit)
                    heatmap?.RegisterHit(pos);

                TryCaptureImage(interactionType);
            }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        /// <summary>
        /// Manda el `start` en cuanto el tracker exista, con su instante ORIGINAL.
        /// </summary>
        /// <remarks>
        /// El `ts` viaja como parametro a proposito: si se recalculara al enviar, la
        /// interaccion quedaria fechada cuando el SDK termino de arrancar y no cuando el
        /// usuario agarro el objeto. La espera esta acotada; si el tracker no aparece, se
        /// avisa por consola en vez de perderlo en silencio, que es lo que pasaba antes.
        /// </remarks>
        private IEnumerator EnviarStartCuandoHayaTracker(
            string interactionId,
            string objectName,
            string objectTag,
            string inputType,
            string interactionType,
            Vector3 pos,
            string scene,
            string timestampUtc)
        {
            float esperado = 0f;
            while (Tracker == null && esperado < 10f)
            {
                esperado += Time.unscaledDeltaTime;
                yield return null;
            }

            var tracker = Tracker;
            if (tracker == null)
            {
                Debug.LogWarning("[Interactable] " + objectName +
                    ": el tracker no aparecio en 10 s, el start de esta interaccion se pierde.");
                yield break;
            }

            tracker.CapInteractionStart(
                interactionId,
                objectName,
                objectTag,
                inputType,
                interactionType,
                pos,
                scene,
                timestampUtc);
        }


        public void OnInteractEnd(string interactionType)
        {

            try
            {
                if (string.IsNullOrEmpty(currentInteractionId))
                    return;

                double now = Time.realtimeSinceStartupAsDouble;
                double duration = Math.Max(0.0, now - currentInteractionStartTimeRealtime);

                var inputType = XRInteractionInputResolver.GetCurrentInputType().ToString();

                Vector3 pos = transform.position;
                string scene = SceneManager.GetActiveScene().name;
                string ts = DateTime.UtcNow.ToString("o");

                Tracker?.CapInteractionEnd(
                    currentInteractionId,
                    gameObject.name,
                    gameObject.tag,
                    inputType,
                    interactionType,
                    pos,
                    scene,
                    currentInteractionStartTimeRealtime,
                    now,
                    duration,
                    ts
                );

                currentInteractionId = null;
                currentInteractionStartTimeRealtime = 0.0;
            }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        public static void FlushHeatmap()
        {
            if (heatmap == null) return;

            Gossip.Instance?.HeatmapTracker?.CapFromHeatmap(
                heatmap,
                heatmapSource: "interaction",
                sparse: true,
                rowMajor: true
            );

            if (Gossip.Instance?.Settings?.EnableDebug == true)
                Debug.Log("[Interactable] Heatmap flushed (interaction)");
        }

        private void TryCaptureImage(string interactionType)
        {
            if (!captureImageOnInteraction) return;

            int objId = gameObject.GetInstanceID();
            float currentTime = Time.time;

            if (currentTime - globalLastImageTime < minTimeBetweenImages) return;

            if (lastCaptureTimes.TryGetValue(objId, out float lastTime))
            {
                if (currentTime - lastTime < objectCooldown) return;
            }

            InteractionImageTracker.Track(gameObject, interactionType);
            globalLastImageTime = currentTime;
            lastCaptureTimes[objId] = currentTime;
        }
    }
}
