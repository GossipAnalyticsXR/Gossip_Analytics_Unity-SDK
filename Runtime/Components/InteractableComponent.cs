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

                _xrInteractable    = comp;
                _adapterIndex      = i;
                _selectionPropInfo = _resolvedTypes[i].GetProperty(
                    _adapters[i].SelectionMember,
                    BindingFlags.Public | BindingFlags.Instance);
                string raw = comp.GetType().Name;
                string lbl = raw;
                if (lbl.EndsWith("Interactable")) lbl = lbl.Substring(0, lbl.Length - "Interactable".Length);
                if (lbl.StartsWith("XR")) lbl = lbl.Substring(2);
                _xrLabel = string.IsNullOrEmpty(lbl) ? raw : lbl;
                break; // use first matching framework
            }
        }

        // ── Event-based interactable hook (no compile-time dependency) ──────────────────
        private bool TrySubscribeEvent(Component comp, string member, System.Action cb)
        {
            if ((UnityEngine.Object)comp == null) return false;
            try
            {
                var t = comp.GetType();
                object evt = t.GetProperty(member, BindingFlags.Public | BindingFlags.Instance)?.GetValue(comp)
                          ?? t.GetField(member,    BindingFlags.Public | BindingFlags.Instance)?.GetValue(comp);
                if (evt == null) return false;
                var add = evt.GetType().GetMethod("AddListener");
                if (add == null) return false;
                var pType = add.GetParameters()[0].ParameterType;
                System.Delegate del;
                if (pType == typeof(UnityEngine.Events.UnityAction))
                {
                    del = (UnityEngine.Events.UnityAction)(() => cb());
                }
                else if (pType.IsGenericType &&
                         pType.GetGenericTypeDefinition() == typeof(UnityEngine.Events.UnityAction<>))
                {
                    var argT = pType.GetGenericArguments()[0];
                    del = (System.Delegate)GetType()
                        .GetMethod("MakeIgnoringAction", BindingFlags.NonPublic | BindingFlags.Instance)
                        .MakeGenericMethod(argT)
                        .Invoke(this, new object[] { cb });
                }
                else return false;
                add.Invoke(evt, new object[] { del });
                return true;
            }
            catch { return false; }
        }

        private UnityEngine.Events.UnityAction<T> MakeIgnoringAction<T>(System.Action cb) => _ => cb();

        private void FireInstant(string label)
        {
            OnInteractStart(label);
            OnInteractEnd(label);
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

            // ── Subscribe to event-driven interactables (UI Button, XR Activate, XR Poke) ──
            foreach (var c in GetComponents<Component>())
            {
                TrySubscribeEvent(c, "onClick",     () => FireInstant("UIButton"));
                TrySubscribeEvent(c, "activated",   () => FireInstant("XRActivate"));
                TrySubscribeEvent(c, "pokeEntered", () => OnInteractStart("XRPoke"));
                TrySubscribeEvent(c, "pokeExited",  () => OnInteractEnd("XRPoke"));
            }

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
                        if (_countPropInfo == null && val != null)
                            _countPropInfo = val.GetType().GetProperty("Count");
                        isSelected = _countPropInfo != null && val != null && (int)_countPropInfo.GetValue(val) > 0;
                    }

                    if (isSelected && !_wasSelected) OnInteractStart(_xrLabel);
                    if (!isSelected && _wasSelected) OnInteractEnd(_xrLabel);
                    _wasSelected = isSelected;
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

                Tracker?.CapInteractionStart(
                    currentInteractionId,
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
