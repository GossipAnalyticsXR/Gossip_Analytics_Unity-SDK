#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GossipSDK.Components;

namespace GossipSDK.Editor
{
    public class InstrumentationManagerWindow : EditorWindow
    {
        // ─── State ──────────────────────────────────────────────────────────
        private GossipInstrumentationData _data;
        private Dictionary<string, List<ScannedObject>> _sceneObjects = new Dictionary<string, List<ScannedObject>>();
        private Dictionary<string, bool> _sceneFoldouts = new Dictionary<string, bool>();
        private bool _hasNewObjects = false;
        private Vector2 _scrollPos;
        private bool _isScanning = false;
        private int _selectedTab = 0;
        private readonly string[] _tabLabels = new string[] { "🎯 Interactables", "📡 Trackers", "🔐 Permissions" };

        private static readonly string[] InteractableKeywords = new[]
        {
            "Interactable", "Grabbable", "Pickup", "Interactor", "Button", "Lever", "Trigger"
        };

        private static readonly string[] ExcludeNameKeywords = new[]
        {
            "wall", "floor", "ceiling", "ground", "terrain", "sky",
            "ambient", "light", "camera", "canvas", "event"
        };

        // ─── Inner type ──────────────────────────────────────────────────────
        private class ScannedObject
        {
            public string sceneName;
            public string hierarchyPath;
            public string objectName;
            public bool isChecked;
            public bool hasInteractable;
            public bool isNew;
        }

        // ─── Tracker types ────────────────────────────────────────────────────────
        public enum TrackerTarget { Player, Camera, AnyObject, Automatic }

        [System.Serializable]
        public class TrackerInfo
        {
            public string componentTypeName;
            public string displayName;
            public string description;
            public string category;
            public TrackerTarget target;
            public bool requiresConfiguration;
        }

        private static readonly List<TrackerInfo> _recommendedTrackers = new List<TrackerInfo>
        {
            // SPATIAL
            new TrackerInfo { componentTypeName = "PositionTrackerComponent",            displayName = "Position Tracker",           description = "Tracks player position (X,Y,Z) over time. Feeds heatmaps.",                                    category = "Spatial", target = TrackerTarget.Player,    requiresConfiguration = false },
            new TrackerInfo { componentTypeName = "RotationAndVelocityTrackerComponent", displayName = "Rotation & Velocity",         description = "Tracks player rotation, speed, and angular velocity.",                                        category = "Spatial", target = TrackerTarget.Player,    requiresConfiguration = false },
            new TrackerInfo { componentTypeName = "UserPostureTrackerComponent",         displayName = "Posture Tracker",             description = "Detects standing/sitting/crouching. Requires sit and crouch thresholds in Inspector.",         category = "Spatial", target = TrackerTarget.Player,    requiresConfiguration = true  },
            new TrackerInfo { componentTypeName = "UserBalanceTrackerComponent",         displayName = "Balance Tracker",             description = "Records body stability and oscillation. Attach to player head.",                               category = "Spatial", target = TrackerTarget.Player,    requiresConfiguration = false },
            // DEVICE & PERFORMANCE
            new TrackerInfo { componentTypeName = "PerformanceMonitorComponent",         displayName = "Performance Monitor",         description = "Tracks FPS and memory usage automatically.",                                                   category = "Device",  target = TrackerTarget.AnyObject, requiresConfiguration = false },
            new TrackerInfo { componentTypeName = "BatteryMonitorComponent",             displayName = "Battery Monitor",             description = "Tracks battery level and charging status automatically.",                                     category = "Device",  target = TrackerTarget.AnyObject, requiresConfiguration = false },
            new TrackerInfo { componentTypeName = "ConnectivityMonitorComponent",        displayName = "Connectivity Monitor",        description = "Tracks network connection type and speed automatically.",                                     category = "Device",  target = TrackerTarget.AnyObject, requiresConfiguration = false },
            new TrackerInfo { componentTypeName = "HandControllerTrackingComponent",     displayName = "Hand & Controller Tracking",  description = "Tracks hand and controller movement. Place once anywhere in scene.",                          category = "Device",  target = TrackerTarget.AnyObject, requiresConfiguration = false },
            new TrackerInfo { componentTypeName = "InputUsageTrackerComponent",          displayName = "Input Usage Tracker",         description = "Tracks time using controllers vs hand tracking. Reports on session end.",                     category = "Device",  target = TrackerTarget.AnyObject, requiresConfiguration = false },
            // XR SPECIFIC
            new TrackerInfo { componentTypeName = "EyeTrackingComponent",               displayName = "Eye Tracking",                description = "Tracks gaze hits and fixation. Requires OVR Manager + Eye Tracked Foveated Rendering. Attach to camera.", category = "XR", target = TrackerTarget.Camera, requiresConfiguration = true },
        };

        // ─── Menu entry ──────────────────────────────────────────────────────
        [MenuItem("Window/Gossip Analytics/Instrumentation Manager")]
        public static void Open()
        {
            GetWindow<InstrumentationManagerWindow>("\ud83c\udfaf Instrumentation Manager");
        }

        // ─── Lifecycle ───────────────────────────────────────────────────────
        private void OnEnable()
        {
            LoadOrCreateData();
            ScanAllScenes();
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        }

        private void OnHierarchyChanged()
        {
            // Re-scan open scenes and detect new interactable objects
            var openSceneNames = new HashSet<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isLoaded) openSceneNames.Add(s.name);
            }

            bool foundNew = false;
            foreach (var sceneName in openSceneNames)
            {
                var s = SceneManager.GetSceneByName(sceneName);
                if (!s.isLoaded) continue;

                var scannedPaths = new HashSet<string>();
                foreach (var root in s.GetRootGameObjects())
                    CollectInteractableObjects(root, sceneName, scannedPaths);

                var storedEntry = _data?.scenes.FirstOrDefault(e => e.sceneName == sceneName);
                var storedPaths = storedEntry?.instrumentedPaths ?? new List<string>();

                foreach (var path in scannedPaths)
                {
                    if (!storedPaths.Contains(path))
                    {
                        foundNew = true;
                        break;
                    }
                }
                if (foundNew) break;
            }

            if (foundNew && !_hasNewObjects)
            {
                _hasNewObjects = true;
                titleContent.text = "\ud83c\udfaf Instrumentation Manager (!)";
                Repaint();
            }
        }

        // ─── GUI ─────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            // ── Tab toolbar ──
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabLabels, GUILayout.Height(30));
            EditorGUILayout.Space(4);

            if (_selectedTab == 0)
            {
                // ── Header ──
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Gossip Analytics \u2014 Instrumentation Manager", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "Select which objects to track interactions on. Click Apply to add InteractableComponent.",
                    EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space(4);
    
                // ── New-objects warning ──
                if (_hasNewObjects)
                {
                    EditorGUILayout.HelpBox(
                        "\u26a0 New interactable objects detected. Click Refresh to review them.",
                        MessageType.Warning);
                    EditorGUILayout.Space(4);
                }
    
                // ── Toolbar ──
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Refresh", GUILayout.Width(70)))
                {
                    ScanAllScenes();
                    _hasNewObjects = false;
                    titleContent.text = "\ud83c\udfaf Instrumentation Manager";
                }
                if (GUILayout.Button("Select All Scenes", GUILayout.Width(120)))
                    SetAllChecked(true);
                if (GUILayout.Button("Deselect All", GUILayout.Width(90)))
                    SetAllChecked(false);
                GUILayout.FlexibleSpace();
                GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
                if (GUILayout.Button("Apply Selected", GUILayout.Width(110)))
                    ApplyInstrumentation();
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(6);
    
                // ── Scene list ──
                var enabledBuildSceneCount = UnityEditor.EditorBuildSettings.scenes.Count(s => s.enabled);
                if (enabledBuildSceneCount == 0)
                {
                    EditorGUILayout.HelpBox(
                        "No scenes found in Build Settings. Add your scene via File → Build Settings before instrumenting.",
                        MessageType.Warning);
                }
                else
                {
                    _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
        
                    foreach (var kvp in _sceneObjects)
                    {
                        string sceneName = kvp.Key;
                        var objects = kvp.Value;
        
                        int instrumentedCount = objects.Count(o => o.hasInteractable);
                        int totalCount = objects.Count;
        
                        if (!_sceneFoldouts.ContainsKey(sceneName))
                            _sceneFoldouts[sceneName] = true;
        
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
                        // ── Scene header row ──
                        EditorGUILayout.BeginHorizontal();
                        _sceneFoldouts[sceneName] = EditorGUILayout.Foldout(
                            _sceneFoldouts[sceneName],
                            $"  {sceneName}   ({instrumentedCount} instrumented / {totalCount} total)",
                            true,
                            EditorStyles.foldoutHeader);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Select All in Scene", GUILayout.Width(130)))
                            foreach (var o in objects) o.isChecked = true;
                        EditorGUILayout.EndHorizontal();
        
                        if (_sceneFoldouts[sceneName])
                        {
                            EditorGUILayout.Space(2);
                            foreach (var obj in objects)
                                DrawObjectRow(obj);
                            EditorGUILayout.Space(2);
                        }
        
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.Space(4);
                    }
        
                    EditorGUILayout.EndScrollView();
                }
    
                // ── Bottom help ──
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "After applying, call OnInteractInstant(\"grab\") or OnInteractStart/End from your input handler " +
                    "on each instrumented object, or use autoTriggerOnStart for testing.",
                    MessageType.Info);
            }
            else if (_selectedTab == 1)
            {
                DrawTrackersTab();
            }
            else if (_selectedTab == 2)
            {
                DrawPermissionsTab();
            }

        }
        private void DrawObjectRow(ScannedObject obj)
        {
            EditorGUILayout.BeginHorizontal();
            obj.isChecked = EditorGUILayout.Toggle(obj.isChecked, GUILayout.Width(20));

            EditorGUILayout.LabelField(obj.objectName, GUILayout.MinWidth(150), GUILayout.ExpandWidth(true));

            // Status badge
            if (obj.hasInteractable)
            {
                GUI.color = new Color(0.4f, 0.9f, 0.4f);
                EditorGUILayout.LabelField("\u2705 Instrumented", GUILayout.Width(110));
            }
            else if (obj.isNew)
            {
                GUI.color = new Color(1f, 0.85f, 0.2f);
                EditorGUILayout.LabelField("\u26a0 New", GUILayout.Width(110));
            }
            else
            {
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                EditorGUILayout.LabelField("\u25cb Detected", GUILayout.Width(110));
            }
            GUI.color = Color.white;

            // Remove button for already-instrumented objects
            if (obj.hasInteractable)
            {
                GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                    RemoveInstrumentationForObject(obj);
                GUI.backgroundColor = Color.white;
            }
            else
            {
                GUILayout.Space(64);
            }

            EditorGUILayout.EndHorizontal();
        }

        // ─── Scanning ────────────────────────────────────────────────────────
        private void ScanAllScenes()
        {
            _sceneObjects.Clear();

            // Track which scenes were already open
            var alreadyOpen = new HashSet<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isLoaded) alreadyOpen.Add(s.path);
            }

            foreach (var buildScene in EditorBuildSettings.scenes)
            {
                if (!buildScene.enabled) continue;

                string scenePath = buildScene.path;
                string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);

                bool wasOpen = alreadyOpen.Contains(scenePath);
                Scene scene;

                try
                {
                    if (!wasOpen)
                        scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    else
                        scene = SceneManager.GetSceneByPath(scenePath);

                    if (!scene.isLoaded) continue;

                    var scannedPaths = new HashSet<string>();
                    var result = new List<ScannedObject>();

                    foreach (var root in scene.GetRootGameObjects())
                        CollectInteractableObjectsForScan(root, sceneName, scannedPaths, result);

                    _sceneObjects[sceneName] = result;
                    _sceneFoldouts.TryAdd(sceneName, true);
                }
                finally
                {
                    if (!wasOpen)
                    {
                        var s = SceneManager.GetSceneByPath(scenePath);
                        if (s.isLoaded)
                            EditorSceneManager.CloseScene(s, true);
                    }
                }
            }

            // Also scan currently open scenes not in build settings
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded || string.IsNullOrEmpty(s.name)) continue;
                if (_sceneObjects.ContainsKey(s.name)) continue;

                var scannedPaths = new HashSet<string>();
                var result = new List<ScannedObject>();
                foreach (var root in s.GetRootGameObjects())
                    CollectInteractableObjectsForScan(root, s.name, scannedPaths, result);

                if (result.Count > 0)
                    _sceneObjects[s.name] = result;
            }

            Repaint();
        }

        private void CollectInteractableObjectsForScan(
            GameObject go, string sceneName,
            HashSet<string> visited, List<ScannedObject> result)
        {
            if (!IsInteractable(go)) goto children;

            string path = GetHierarchyPath(go);
            if (!visited.Add(path)) goto children;

            var storedEntry = _data?.scenes.FirstOrDefault(e => e.sceneName == sceneName);
            bool isInstrumented = go.GetComponent<InteractableComponent>() != null;
            bool isStored = storedEntry?.instrumentedPaths.Contains(path) ?? false;

            result.Add(new ScannedObject
            {
                sceneName = sceneName,
                hierarchyPath = path,
                objectName = go.name,
                isChecked = isInstrumented || isStored,
                hasInteractable = isInstrumented,
                isNew = !isStored && !isInstrumented
            });

        children:
            foreach (Transform child in go.transform)
                CollectInteractableObjectsForScan(child.gameObject, sceneName, visited, result);
        }

        private void CollectInteractableObjects(
            GameObject go, string sceneName, HashSet<string> paths)
        {
            if (IsInteractable(go))
                paths.Add(GetHierarchyPath(go));
            foreach (Transform child in go.transform)
                CollectInteractableObjects(child.gameObject, sceneName, paths);
        }

        // ─── Filter ──────────────────────────────────────────────────────────
        private bool IsInteractable(GameObject go)
        {
            // Exclude: fully static
            if ((int)GameObjectUtility.GetStaticEditorFlags(go) != 0)
                return false;

            // Exclude: name contains excluded keywords
            string nameLower = go.name.ToLowerInvariant();
            foreach (var kw in ExcludeNameKeywords)
                if (nameLower.Contains(kw)) return false;

            // Exclude: no renderer AND no collider
            if (go.GetComponent<Renderer>() == null && go.GetComponent<Collider>() == null)
                return false;

            // Include: component name contains interactable keywords
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                string typeName = comp.GetType().Name;
                foreach (var kw in InteractableKeywords)
                    if (typeName.Contains(kw)) return true;
            }

            // Include: has Rigidbody and is not fully static
            if (go.GetComponent<Rigidbody>() != null)
                return true;

            // Include: tag contains "Interactable" or "Pickup"
            try
            {
                if (go.CompareTag("Interactable") || go.CompareTag("Pickup"))
                    return true;
            }
            catch { /* tag not defined */ }

            return false;
        }

        // ─── Apply ───────────────────────────────────────────────────────────
        private void ApplyInstrumentation()
        {
            int addedCount = 0;
            int removedCount = 0;
            var dirtyScenes = new HashSet<Scene>();

            foreach (var kvp in _sceneObjects)
            {
                string sceneName = kvp.Key;
                var objects = kvp.Value;

                var storedEntry = GetOrCreateSceneEntry(sceneName);

                foreach (var obj in objects)
                {
                    // Find the live GameObject in open scenes
                    GameObject go = FindGameObjectByPath(obj.hierarchyPath, sceneName);
                    if (go == null) continue;

                    Scene goScene = go.scene;

                    if (obj.isChecked && !obj.hasInteractable)
                    {
                        var comp = Undo.AddComponent<InteractableComponent>(go);
                        if (comp != null) comp.registerHeatmapHit = true;
                        obj.hasInteractable = true;
                        if (!storedEntry.instrumentedPaths.Contains(obj.hierarchyPath))
                            storedEntry.instrumentedPaths.Add(obj.hierarchyPath);
                        dirtyScenes.Add(goScene);
                        addedCount++;
                    }
                    else if (!obj.isChecked && obj.hasInteractable)
                    {
                        var comp = go.GetComponent<InteractableComponent>();
                        if (comp != null)
                        {
                            Undo.DestroyObjectImmediate(comp);
                            obj.hasInteractable = false;
                        }
                        storedEntry.instrumentedPaths.Remove(obj.hierarchyPath);
                        dirtyScenes.Add(goScene);
                        removedCount++;
                    }
                }
            }

            // Save data asset
            if (_data != null)
            {
                EditorUtility.SetDirty(_data);
                AssetDatabase.SaveAssets();
            }

            // Mark scenes dirty
            foreach (var s in dirtyScenes)
                EditorSceneManager.MarkSceneDirty(s);

            int sceneCount = dirtyScenes.Count;
            EditorUtility.DisplayDialog(
                "Gossip Analytics \u2014 Applied!",
                $"Applied!  {addedCount} object(s) instrumented, {removedCount} removed, across {sceneCount} scene(s).",
                "OK");

            ScanAllScenes();
        }

        private void RemoveInstrumentationForObject(ScannedObject obj)
        {
            GameObject go = FindGameObjectByPath(obj.hierarchyPath, obj.sceneName);
            if (go == null) return;

            var comp = go.GetComponent<InteractableComponent>();
            if (comp != null) Undo.DestroyObjectImmediate(comp);

            obj.hasInteractable = false;
            obj.isChecked = false;

            var entry = _data?.scenes.FirstOrDefault(e => e.sceneName == obj.sceneName);
            entry?.instrumentedPaths.Remove(obj.hierarchyPath);

            if (_data != null)
            {
                EditorUtility.SetDirty(_data);
                AssetDatabase.SaveAssets();
            }

            EditorSceneManager.MarkSceneDirty(go.scene);
            Repaint();
        }

        // ─── Persistence ─────────────────────────────────────────────────────
        private void LoadOrCreateData()
        {
            string[] guids = AssetDatabase.FindAssets("t:GossipInstrumentationData");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _data = AssetDatabase.LoadAssetAtPath<GossipInstrumentationData>(path);
            }
            else
            {
                _data = ScriptableObject.CreateInstance<GossipInstrumentationData>();
                AssetDatabase.CreateAsset(_data, "Assets/GossipInstrumentationData.asset");
                AssetDatabase.SaveAssets();
            }
        }

        private SceneInstrumentationEntry GetOrCreateSceneEntry(string sceneName)
        {
            if (_data == null) return new SceneInstrumentationEntry { sceneName = sceneName };
            var entry = _data.scenes.FirstOrDefault(e => e.sceneName == sceneName);
            if (entry == null)
            {
                entry = new SceneInstrumentationEntry { sceneName = sceneName };
                _data.scenes.Add(entry);
            }
            return entry;
        }

        // ─── Helpers ─────────────────────────────────────────────────────────
        private static string GetHierarchyPath(GameObject go)
        {
            string path = go.name;
            Transform t = go.transform.parent;
            while (t != null)
            {
                path = t.name + "/" + path;
                t = t.parent;
            }
            return path;
        }

        private static GameObject FindGameObjectByPath(string path, string sceneName)
        {
            // Search in all loaded scenes that match
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded) continue;
                if (!string.IsNullOrEmpty(sceneName) && s.name != sceneName) continue;

                foreach (var root in s.GetRootGameObjects())
                {
                    var found = FindByPath(root, path);
                    if (found != null) return found;
                }
            }
            return null;
        }

        private static GameObject FindByPath(GameObject root, string path)
        {
            string rootName = path.Contains("/") ? path.Substring(0, path.IndexOf('/')) : path;
            if (root.name != rootName) return null;
            if (!path.Contains("/")) return root;

            string remainder = path.Substring(path.IndexOf('/') + 1);
            foreach (Transform child in root.transform)
            {
                var found = FindByPath(child.gameObject, remainder);
                if (found != null) return found;
            }
            return null;
        }

        private void SetAllChecked(bool value)
        {
            foreach (var kvp in _sceneObjects)
                foreach (var obj in kvp.Value)
                    obj.isChecked = value;
        }

        // ─── Trackers tab ───────────────────────────────────────────────────────
        private void DrawTrackersTab()
        {
            EditorGUILayout.HelpBox("Add recommended tracker components to your scene. Spatial trackers go on your player, Device trackers can go on any object.", MessageType.Info);
            EditorGUILayout.Space(6);

            // Auto-detect player and camera
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject == null)
            {
                try
                {
                    var xrOriginType = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
                        .FirstOrDefault(t => t.FullName == "UnityEngine.XR.Interaction.Toolkit.XROrigin");
                    if (xrOriginType != null)
                    {
                        var xrOrigin = FindObjectOfType(xrOriginType) as Component;
                        if (xrOrigin != null) return xrOrigin.gameObject;
                    }
                }
                catch { }
            }
            Camera mainCamera = Camera.main;

            string playerStatus = playerObject != null ? $"✅ {playerObject.name}" : "⚠ Not found (tag your player 'Player')";
            string cameraStatus = mainCamera   != null ? $"✅ {mainCamera.name}"   : "⚠ No main camera found";
            EditorGUILayout.LabelField($"Player: {playerStatus}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Camera: {cameraStatus}", EditorStyles.miniLabel);
            EditorGUILayout.Space(8);

            var categories = _recommendedTrackers.Select(t => t.category).Distinct().ToList();
            foreach (var category in categories)
            {
                EditorGUILayout.LabelField(category, EditorStyles.boldLabel);

                var trackersInCategory = _recommendedTrackers.Where(t => t.category == category).ToList();
                foreach (var tracker in trackersInCategory)
                {
                    var componentType = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
                        .FirstOrDefault(t => t.Name == tracker.componentTypeName);

                    GameObject targetObj = tracker.target == TrackerTarget.Player  ? playerObject
                                        : tracker.target == TrackerTarget.Camera   ? (mainCamera?.gameObject)
                                        : null;

                    bool alreadyInScene = componentType != null &&
                        FindObjectOfType(componentType) != null;

                    EditorGUILayout.BeginHorizontal();

                    string badge = alreadyInScene ? "✅" : "○";
                    EditorGUILayout.LabelField(badge, GUILayout.Width(20));

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField(tracker.displayName, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(tracker.description, EditorStyles.wordWrappedMiniLabel);
                    if (tracker.requiresConfiguration)
                        EditorGUILayout.LabelField("⚠ Requires configuration in Inspector after adding.", EditorStyles.miniLabel);
                    EditorGUILayout.EndVertical();

                    if (alreadyInScene)
                    {
                        GUI.enabled = false;
                        GUILayout.Button("Added", GUILayout.Width(70));
                        GUI.enabled = true;
                    }
                    else if (componentType == null)
                    {
                        GUI.enabled = false;
                        GUILayout.Button("N/A", GUILayout.Width(70));
                        GUI.enabled = true;
                    }
                    else
                    {
                        bool canAdd = tracker.target == TrackerTarget.AnyObject || targetObj != null;
                        GUI.enabled = canAdd;
                        if (GUILayout.Button("Add", GUILayout.Width(70)))
                        {
                            GameObject addTarget = targetObj ?? (playerObject ?? new GameObject("GossipTrackers"));
                            Undo.AddComponent(addTarget, componentType);
                            EditorUtility.SetDirty(addTarget);
                            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                            Debug.Log($"[Gossip Analytics] Added {tracker.displayName} to {addTarget.name}");
                        }
                        GUI.enabled = true;
                    }

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space(4);
                }

                EditorGUILayout.Space(8);
            }
        }

        // ─── Permissions tab ───────────────────────────────────────────────────
        private void DrawPermissionsTab()
        {
            EditorGUILayout.HelpBox(
                "VRPermissionsHandler automatically requests device permissions on Android/Meta Quest at runtime.\n\n" +
                "In the Unity Editor and on non-Android platforms it does nothing — IsReady is set to true immediately.",
                MessageType.Info);
            EditorGUILayout.Space(6);

            // Check if VRPermissionsHandler is in the current scene
            var handlerType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
                .FirstOrDefault(t => t.Name == "VRPermissionsHandler");

            bool handlerInScene = handlerType != null &&
                FindObjectOfType(handlerType) != null;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                handlerInScene ? "✅  VRPermissionsHandler is in the scene" : "○  VRPermissionsHandler is NOT in the scene",
                handlerInScene ? EditorStyles.boldLabel : EditorStyles.label);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);

            if (!handlerInScene)
            {
                EditorGUILayout.HelpBox(
                    "Without VRPermissionsHandler, your Meta Quest build will NOT request eye tracking, microphone, or camera permissions at launch. Add it before building for Android.",
                    MessageType.Warning);
                EditorGUILayout.Space(4);

                GUI.enabled = handlerType != null;
                if (GUILayout.Button("Add VRPermissionsHandler to Scene", GUILayout.Height(30)))
                {
                    var gossipManager = FindObjectOfType<GossipSDK.GossipManager>();
                    GameObject target = gossipManager != null
                        ? gossipManager.gameObject
                        : new GameObject("VRPermissionsHandler");
                    Undo.AddComponent(target, handlerType);
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                    Debug.Log("[Gossip Analytics] VRPermissionsHandler added to " + target.name);
                }
                GUI.enabled = true;

                if (handlerType == null)
                    EditorGUILayout.LabelField("⚠ VRPermissionsHandler type not found in project.", EditorStyles.miniLabel);
            }
            else
            {
                if (GUILayout.Button("Remove from Scene", GUILayout.Width(140)))
                {
                    var instance = FindObjectOfType(handlerType) as Component;
                    if (instance != null)
                    {
                        Undo.DestroyObjectImmediate(instance);
                        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                    }
                }
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Permissions requested on Android launch:", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            var permissions = new (string name, string permissionId, string note)[]
            {
                ("Eye Tracking",    "com.oculus.permission.EYE_TRACKING",  "Required for EyeTrackingComponent to capture gaze data."),
                ("Scene / Spatial", "com.oculus.permission.USE_SCENE",     "Required for spatial mapping and environment understanding."),
                ("Headset Camera",  "horizonos.permission.HEADSET_CAMERA", "Required for passthrough and MR camera access."),
                ("Microphone",      "android.permission.RECORD_AUDIO",     "Required for AudioReactionTrackerComponent."),
            };

            foreach (var perm in permissions)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(perm.name, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(perm.permissionId, EditorStyles.miniLabel);
                EditorGUILayout.LabelField(perm.note, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(
                "Permissions are requested sequentially at app launch with a 10s timeout per permission.",
                EditorStyles.wordWrappedMiniLabel);
        }
    }
}
#endif
