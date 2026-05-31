#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GossipSDK.Components;
using Object = UnityEngine.Object;

namespace GossipSDK.Editor
{
    public class InstrumentationManagerWindow : EditorWindow
    {
        // --- State ---
        private GossipInstrumentationData _data;
        private Dictionary<string, List<ScannedObject>> _sceneObjects = new Dictionary<string, List<ScannedObject>>();
        private Dictionary<string, bool> _sceneFoldouts = new Dictionary<string, bool>();
        private bool _hasNewObjects = false;
        private Vector2 _scrollPos;
        private static bool _isScanning = false;
        private int _cachedActiveTrackers = 0;
        private double _lastTrackerCountTime = 0;
        private static bool _dataPreloaded = false;
        private SerializedObject _vrHandlerSO;
        private static int _selectedTab = 0;
        private readonly string[] _tabLabels = new string[] { "Interactables", "Trackers", "Permissions" };
        private GameObject _playerObject = null;
        private Camera _mainCamera = null;

        private static readonly string[] InteractableKeywords = new[]
        {
            "Interactable", "Grabbable", "Pickup", "Interactor", "Button", "Lever", "Trigger"
        };

        private static readonly string[] ExcludeNameKeywords = new[]
        {
            "wall", "floor", "ceiling", "ground", "terrain", "sky",
            "ambient", "light", "camera", "canvas", "event",
            "trigger", "collider", "volume", "bounds", "hitbox", "detector", "zone"
        };

        // --- Inner type ---
        private class ScannedObject
        {
            public string sceneName;
            public string hierarchyPath;
            public string objectName;
            public bool isChecked;
            public bool hasInteractable;
            public bool isNew;
        }

        // --- Tracker types ---
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
            new TrackerInfo { componentTypeName = "PositionTrackerComponent", displayName = "Position Tracker", description = "Tracks player position (X,Y,Z) over time. Feeds heatmaps.", category = "Spatial", target = TrackerTarget.Player, requiresConfiguration = false },
            new TrackerInfo { componentTypeName = "RotationAndVelocityTrackerComponent", displayName = "Rotation & Velocity", description = "Tracks player rotation, speed, and angular velocity.", category = "Spatial", target = TrackerTarget.Player, requiresConfiguration = false },
            new TrackerInfo { componentTypeName = "UserPostureTrackerComponent", displayName = "Posture Tracker", description = "Detects standing/sitting/crouching. Requires thresholds in Inspector.", category = "Spatial", target = TrackerTarget.Player, requiresConfiguration = true },
            new TrackerInfo { componentTypeName = "UserBalanceTrackerComponent", displayName = "Balance Tracker", description = "Records body stability and oscillation.", category = "Spatial", target = TrackerTarget.Player, requiresConfiguration = false },
            // DEVICE & PERFORMANCE
            new TrackerInfo { componentTypeName = "PerformanceMonitorComponent", displayName = "Performance Monitor", description = "Tracks FPS and memory usage automatically.", category = "Device", target = TrackerTarget.AnyObject, requiresConfiguration = false },
            new TrackerInfo { componentTypeName = "BatteryMonitorComponent", displayName = "Battery Monitor", description = "Tracks battery level and charging status automatically.", category = "Device", target = TrackerTarget.AnyObject, requiresConfiguration = false },
            new TrackerInfo { componentTypeName = "ConnectivityMonitorComponent", displayName = "Connectivity Monitor", description = "Tracks network connection type and speed automatically.", category = "Device", target = TrackerTarget.AnyObject, requiresConfiguration = false },
            new TrackerInfo { componentTypeName = "HandControllerTrackingComponent", displayName = "Hand & Controller Tracking", description = "Tracks hand and controller movement.", category = "Device", target = TrackerTarget.AnyObject, requiresConfiguration = false },
            new TrackerInfo { componentTypeName = "InputUsageTrackerComponent", displayName = "Input Usage Tracker", description = "Tracks time using controllers vs hand tracking.", category = "Device", target = TrackerTarget.AnyObject, requiresConfiguration = false },
            // XR SPECIFIC
            new TrackerInfo { componentTypeName = "EyeTrackingComponent", displayName = "Eye Tracking", description = "Tracks gaze hits and fixation. Attach to camera.", category = "XR", target = TrackerTarget.Camera, requiresConfiguration = true },
        };

        // --- Menu entry ---
        [MenuItem("Window/Gossip Analytics/2 — Instrumentation Manager", false, 2)]
        public static void Open()
        {
            _selectedTab = 0;
            _dataPreloaded = true;
            _isScanning = true;
            var win = GetWindow<InstrumentationManagerWindow>("Instrumentation Manager");
            try
            {
                win.LoadOrCreateDataAsset();
                win.ScanNow();
            } finally {
                _isScanning = false;
            }
        }

        // --- Lifecycle ---
        private void OnEnable()
        {
            LoadOrCreateDataAsset();
            if (_dataPreloaded) { _dataPreloaded = false; return; }
            ScanAllScenes();
            AutoDetectPlayer();
            AutoAddTrackers();
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        }

        private void OnHierarchyChanged()
        {
            var openSceneNames = new HashSet<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var sc = SceneManager.GetSceneAt(i);
                if (sc.isLoaded) openSceneNames.Add(sc.name);
            }
            bool foundNew = false;
            foreach (var sceneName in openSceneNames)
            {
                var sc = SceneManager.GetSceneByName(sceneName);
                if (!sc.isLoaded) continue;
                var scannedPaths = new HashSet<string>();
                foreach (var root in sc.GetRootGameObjects())
                    CollectInteractableObjects(root, sceneName, scannedPaths);
                var storedEntry = _data?.scenes.FirstOrDefault(e => e.sceneName == sceneName);
                var storedPaths = storedEntry?.instrumentedPaths ?? new List<string>();
                foreach (var path in scannedPaths)
                {
                    if (!storedPaths.Contains(path)) { foundNew = true; break; }
                }
                if (foundNew) break;
            }
            if (foundNew != _hasNewObjects) { _hasNewObjects = foundNew; Repaint(); }
        }

        // --- OnGUI ---
        private void OnGUI()
        {
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabLabels, GUILayout.Height(30));
            EditorGUILayout.Space(4);
            float footerHeight = 36f;
            Rect footerRect = new Rect(0, position.height - footerHeight, position.width, footerHeight);
            Rect contentRect = new Rect(0, 40, position.width, position.height - 40 - footerHeight);
            GUILayout.BeginArea(contentRect);
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            if (_isScanning)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Scanning scenes, please wait...",
                    EditorStyles.centeredGreyMiniLabel, GUILayout.ExpandWidth(true));
                GUILayout.FlexibleSpace();
                Repaint();
                return;
            }
            else if (_selectedTab == 0)
                DrawInteractablesTab();
            else if (_selectedTab == 1)
                DrawTrackersTab();
            else if (_selectedTab == 2)
                DrawPermissionsTab();
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
            DrawFooter(footerRect);
        }

        // --- Footer ---
        private void DrawFooter(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 1f));
            int trackedCount = 0;
            foreach (var kvp in _sceneObjects)
                foreach (var obj in kvp.Value)
                    if (obj.isChecked) trackedCount++;
            if (EditorApplication.timeSinceStartup - _lastTrackerCountTime > 2.0)
            {
                _cachedActiveTrackers = 0;
                foreach (var t in _recommendedTrackers)
                {
                    var tp = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
                        .FirstOrDefault(x => x.Name == t.componentTypeName);
                    if (tp != null && (Object.FindObjectOfType(tp) as Component) != null)
                        _cachedActiveTrackers++;
                }
                _lastTrackerCountTime = EditorApplication.timeSinceStartup;
            }
            int activeTrackers = _cachedActiveTrackers;
            var handler = Object.FindObjectOfType<VRPermissionsHandler>();
            string permsLabel;
            if (handler == null)
                permsLabel = "disabled";
            else
            {
                bool allOn = handler.enableEyeTracking && handler.enableSpatialScene && handler.enableHeadsetCamera && handler.enableMicrophone;
                permsLabel = allOn ? "enabled" : "partially enabled";
            }
            string summary = string.Format("✅ {0} objects tracked  ·  {1} trackers active  ·  Permissions {2}", trackedCount, activeTrackers, permsLabel);
            var summaryStyle = new GUIStyle(EditorStyles.label);
            summaryStyle.normal.textColor = Color.white;
            summaryStyle.alignment = TextAnchor.MiddleLeft;
            summaryStyle.fontSize = 11;
            float btnW = 160f;
            Rect lblRect = new Rect(rect.x + 8, rect.y, rect.width - btnW - 20, rect.height);
            Rect btnRect = new Rect(rect.xMax - btnW - 8, rect.y + 4, btnW, rect.height - 8);
            GUI.Label(lblRect, summary, summaryStyle);
            var doneStyle = new GUIStyle(GUI.skin.button);
            doneStyle.normal.textColor = Color.white;
            doneStyle.fontStyle = FontStyle.Bold;
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.2f, 0.6f, 0.2f, 1f);
            if (GUI.Button(btnRect, "✅ Done — Save & Close", doneStyle))
            {
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                Close();
            }
            GUI.backgroundColor = prevBg;
        }

        // --- Tab: Interactables ---
        private void DrawInteractablesTab()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox("These are the interactive objects detected in your scenes. All are pre-selected - " +
                "deselect any you do not want to track. InteractableComponent will be added automatically.", MessageType.Info);
            EditorGUILayout.Space(4);
            if (_data == null) { EditorGUILayout.HelpBox("No data asset found. Try reopening the window.", MessageType.Error); return; }
            if (_hasNewObjects)
                EditorGUILayout.HelpBox("New interactable objects detected. Click Refresh to review them.", MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Width(70)))
            {
                EditorUtility.DisplayProgressBar("Gossip Analytics", "Scanning scenes, please wait...", 0.2f);
                ScanNow();
                EditorUtility.ClearProgressBar();
                _hasNewObjects = false;
            }
            if (GUILayout.Button("Select All Scenes", GUILayout.Width(115)))
                SetAllChecked(true);
            if (GUILayout.Button("Deselect All", GUILayout.Width(90)))
                SetAllChecked(false);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(6);
            if (_sceneObjects.Count == 0)
            {
                EditorGUILayout.HelpBox("No interactable objects found. Click Refresh after adding objects.", MessageType.Info);
                return;
            }
            foreach (var kvp in _sceneObjects)
            {
                string sceneName = kvp.Key;
                var objs = kvp.Value;
                if (!_sceneFoldouts.ContainsKey(sceneName)) _sceneFoldouts[sceneName] = true;
                EditorGUILayout.BeginHorizontal();
                _sceneFoldouts[sceneName] = EditorGUILayout.Foldout(
                    _sceneFoldouts[sceneName],
                    sceneName + " (" + objs.Count + " objects)",
                    true, EditorStyles.foldoutHeader);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("All", GUILayout.Width(36)))
                    foreach (var obj in objs) obj.isChecked = true;
                if (GUILayout.Button("None", GUILayout.Width(44)))
                    foreach (var obj in objs) obj.isChecked = false;
                EditorGUILayout.EndHorizontal();
                if (_sceneFoldouts[sceneName])
                {
                    var sorted = objs.OrderByDescending(o => o.isNew).ThenBy(o => o.objectName).ToList();
                    foreach (var obj in sorted)
                        DrawObjectRow(obj);
                }
            }
        }

        // --- DrawObjectRow ---
        private void DrawObjectRow(ScannedObject obj)
        {
            EditorGUILayout.BeginHorizontal();
            bool newChecked = EditorGUILayout.Toggle(obj.isChecked, GUILayout.Width(18));
            if (newChecked != obj.isChecked)
            {
                if (!newChecked && obj.hasInteractable)
                {
                    bool confirm = EditorUtility.DisplayDialog(
                        "Deselect " + obj.objectName + "?",
                        "InteractableComponent will be removed from this object. It will no longer track interactions.",
                        "Deselect", "Cancel");
                    if (!confirm) { EditorGUILayout.EndHorizontal(); return; }
                    RemoveInstrumentationForObject(obj);
                    obj.hasInteractable = false;
                }
                obj.isChecked = newChecked;
            }
            EditorGUILayout.LabelField(obj.objectName, GUILayout.ExpandWidth(true));
            if (obj.hasInteractable && !obj.isNew)
            {
                var prevC = GUI.color;
                GUI.color = new Color(0.4f, 0.9f, 0.4f);
                GUILayout.Label("✅ Tracked", GUILayout.Width(80));
                GUI.color = prevC;
            }
            else if (obj.isNew)
            {
                var prevC = GUI.color;
                GUI.color = new Color(0.4f, 0.6f, 1.0f);
                GUILayout.Label(" New", GUILayout.Width(80));
                GUI.color = prevC;
            }
            EditorGUILayout.EndHorizontal();
        }

        // --- Scanning ---
        private void ScanAllScenes()
        {
            _isScanning = true;
            Repaint();
            EditorApplication.delayCall += () =>
            {
                ScanNow();
                _isScanning = false;
                Repaint();
            };
        }

        // --- Sync scan (used by Open() pre-load and Refresh button) ---
        private void ScanNow()
        {
            _sceneObjects.Clear();
            var allStoredPaths = new Dictionary<string, HashSet<string>>();
            if (_data != null)
                foreach (var entry in _data.scenes)
                {
                    if (!allStoredPaths.ContainsKey(entry.sceneName))
                        allStoredPaths[entry.sceneName] = new HashSet<string>(entry.instrumentedPaths);
                }
                // --- D3: collect scenes from all 3 sources ---
                var allScenePaths = new HashSet<string>();

                // Source 1: active scene
                var activeScenePath = EditorSceneManager.GetActiveScene().path;
                if (!string.IsNullOrEmpty(activeScenePath))
                    if (!activeScenePath.Replace("\\", "/").Contains("/Samples/"))
                        allScenePaths.Add(activeScenePath);

                // Source 2: scenes enabled in Build Settings
                foreach (var buildScene in EditorBuildSettings.scenes)
                {
                    if (buildScene.enabled && !string.IsNullOrEmpty(buildScene.path))
                        if (!buildScene.path.Replace("\\", "/").Contains("/Samples/"))
                            allScenePaths.Add(buildScene.path);
                }

                // Source 3: all .unity files in Assets/
                var guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
                foreach (var guid in guids)
                {
                    var scenePath = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(scenePath) && !scenePath.Replace("\\", "/").Contains("/Samples/"))
                        allScenePaths.Add(scenePath);
                }

                // L6: track which scenes were already open before scanning
                var alreadyOpen = new HashSet<string>();
                for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                    alreadyOpen.Add(EditorSceneManager.GetSceneAt(i).path);

                // Scan each scene path (deduplicated)
                foreach (var scenePath in allScenePaths)
                {
                    bool wasAlreadyOpen = alreadyOpen.Contains(scenePath);
                    string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);

                    UnityEditor.SceneManagement.Scene scene;
                    if (wasAlreadyOpen)
                        scene = EditorSceneManager.GetSceneByPath(scenePath);
                    else
                        scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

                    if (!scene.isLoaded) continue;
                    var sceneList = new List<ScannedObject>();
                    _sceneObjects[sceneName] = sceneList;

                    var storedPaths = allStoredPaths.ContainsKey(sceneName) ? allStoredPaths[sceneName] : new HashSet<string>();
                    var scannedPaths = new HashSet<string>();
                    foreach (var root in scene.GetRootGameObjects())
                        CollectInteractableObjectsForScan(root, sceneName, scannedPaths, storedPaths, sceneList);

                    if (!wasAlreadyOpen)
                        EditorSceneManager.CloseScene(scene, true);
                }
            _hasNewObjects = false;
        }

        private void CollectInteractableObjectsForScan(
            GameObject go, string sceneName, HashSet<string> scannedPaths,
            HashSet<string> storedPaths, List<ScannedObject> sceneList)
        {
            if (go == null) return;
            string path = GetHierarchyPath(go);
            if (IsInteractable(go) && !scannedPaths.Contains(path))
            {
                scannedPaths.Add(path);
                bool hadInteractable = go.GetComponent<InteractableComponent>() != null;
                bool wasStored = storedPaths.Contains(path);
                if (!hadInteractable)
                {
                    Undo.AddComponent<InteractableComponent>(go);
                    hadInteractable = true;
                }
                bool isNew = !wasStored;
                sceneList.Add(new ScannedObject
                {
                    sceneName = sceneName,
                    hierarchyPath = path,
                    objectName = go.name,
                    isChecked = true,
                    hasInteractable = hadInteractable,
                    isNew = isNew
                });
                if (isNew && _data != null)
                {
                    var entry = GetOrCreateSceneEntry(sceneName);
                    if (!entry.instrumentedPaths.Contains(path)) entry.instrumentedPaths.Add(path);
                    EditorUtility.SetDirty(_data);
                }
            }
            foreach (Transform child in go.transform)
                CollectInteractableObjectsForScan(child.gameObject, sceneName, scannedPaths, storedPaths, sceneList);
        }

        private void CollectInteractableObjects(GameObject go, string sceneName, HashSet<string> scannedPaths)
        {
            if (go == null) return;
            string path = GetHierarchyPath(go);
            if (IsInteractable(go) && !scannedPaths.Contains(path)) scannedPaths.Add(path);
            foreach (Transform child in go.transform)
                CollectInteractableObjects(child.gameObject, sceneName, scannedPaths);
        }

        private bool IsInteractable(GameObject go)
        {
            if (go == null) return false;
            string lowerName = go.name.ToLower();
            foreach (var keyword in ExcludeNameKeywords)
                if (lowerName.Contains(keyword)) return false;
            foreach (var keyword in InteractableKeywords)
                if (lowerName.Contains(keyword.ToLower())) return true;
            if (go.GetComponent<Rigidbody>() != null) return true;
            if (go.tag == "Interactable" || go.tag == "Pickup") return true;
            return false;
        }

        // --- Apply / Remove ---
        private void ApplyInstrumentation()
        {
            if (_data == null) return;
            var dirtySceneNames = new List<string>();
            foreach (var kvp in _sceneObjects)
            {
                string sceneName = kvp.Key;
                var entry = GetOrCreateSceneEntry(sceneName);
                int changed = 0;
                foreach (var obj in kvp.Value)
                {
                    if (obj.isChecked) { if (!entry.instrumentedPaths.Contains(obj.hierarchyPath)) { entry.instrumentedPaths.Add(obj.hierarchyPath); changed++; } }
                    else { if (entry.instrumentedPaths.Contains(obj.hierarchyPath)) { entry.instrumentedPaths.Remove(obj.hierarchyPath); changed++; } }
                }
                if (changed > 0) dirtySceneNames.Add(sceneName);
            }
            EditorUtility.SetDirty(_data);
            AssetDatabase.SaveAssets();
            foreach (var name in dirtySceneNames)
            {
                var sc = SceneManager.GetSceneByName(name);
                if (sc.isLoaded) EditorSceneManager.MarkSceneDirty(sc);
            }
            EditorUtility.DisplayDialog("Gossip Analytics — Applied!", "Data saved across " + dirtySceneNames.Count + " scene(s).", "OK");
            ScanNow();
        }

        private void RemoveInstrumentationForObject(ScannedObject obj)
        {
            var go = FindGameObjectByPath(obj.hierarchyPath, obj.sceneName);
            if (go != null) { var ic = go.GetComponent<InteractableComponent>(); if (ic != null) Undo.DestroyObjectImmediate(ic); }
            if (_data != null)
            {
                var entry = _data.scenes.FirstOrDefault(e => e.sceneName == obj.sceneName);
                if (entry != null) entry.instrumentedPaths.Remove(obj.hierarchyPath);
                EditorUtility.SetDirty(_data);
            }
        }

        // --- Data helpers ---
        private void LoadOrCreateDataAsset()
        {
            string[] guids = AssetDatabase.FindAssets("t:GossipInstrumentationData");
            if (guids.Length > 0)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                _data = AssetDatabase.LoadAssetAtPath<GossipInstrumentationData>(assetPath);
            }
            else
            {
                _data = ScriptableObject.CreateInstance<GossipInstrumentationData>();
                string dir = "Assets/Gossip Analytics";
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);
                AssetDatabase.CreateAsset(_data, dir + "/GossipInstrumentationData.asset");
                AssetDatabase.SaveAssets();
            }
        }

        private SceneInstrumentationEntry GetOrCreateSceneEntry(string sceneName)
        {
            var entry = _data.scenes.FirstOrDefault(e => e.sceneName == sceneName);
            if (entry == null)
            {
                entry = new SceneInstrumentationEntry { sceneName = sceneName, instrumentedPaths = new List<string>() };
                _data.scenes.Add(entry);
            }
            return entry;
        }

        private static string GetHierarchyPath(GameObject go)
        {
            string path = go.name;
            Transform parent = go.transform.parent;
            while (parent != null) { path = parent.name + "/" + path; parent = parent.parent; }
            return path;
        }

        private static GameObject FindGameObjectByPath(string path, string sceneName)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.isLoaded) return null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var result = FindByPath(root, path);
                if (result != null) return result;
            }
            return null;
        }

        private static GameObject FindByPath(GameObject root, string path)
        {
            if (GetHierarchyPath(root) == path) return root;
            foreach (Transform child in root.transform)
            {
                var result = FindByPath(child.gameObject, path);
                if (result != null) return result;
            }
            return null;
        }

        private void SetAllChecked(bool value)
        {
            foreach (var kvp in _sceneObjects)
                foreach (var obj in kvp.Value)
                    obj.isChecked = value;
        }

        // --- Player auto-detect ---
        private void AutoDetectPlayer()
        {
            // 1. XROrigin via reflection (highest priority - OpenXR standard)
            try
            {
                var xrOriginType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
                    .FirstOrDefault(tp => tp.FullName == "UnityEngine.XR.Interaction.Toolkit.XROrigin" ||
                                          tp.FullName == "Unity.XR.CoreUtils.XROrigin");
                if (xrOriginType != null)
                {
                    var xrOrigin = FindObjectOfType(xrOriginType) as Component;
                    if (xrOrigin != null) { _playerObject = xrOrigin.gameObject; return; }
                }
            }
            catch { }

            // 2. Tag "Player"
            if (_playerObject == null)
                _playerObject = GameObject.FindWithTag("Player");

            // 3. Direct parent of the Main Camera
            if (_playerObject == null)
            {
                var cam = Camera.main;
                if (cam != null && cam.transform.parent != null)
                    _playerObject = cam.transform.parent.gameObject;
            }

            // 4. Root of the Main Camera hierarchy
            if (_playerObject == null)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    var root = cam.transform.root.gameObject;
                    if (root != cam.gameObject) _playerObject = root;
                }
            }

            // 5. Generic OpenXR rig names as last resort
            if (_playerObject == null)
            {
                string[] openXRRigNames = { "XR Origin", "XRRig", "XR Rig", "CameraRig", "PlayerRig", "Player" };
                foreach (var rigName in openXRRigNames)
                {
                    var go = GameObject.Find(rigName);
                    if (go != null) { _playerObject = go; break; }
                }
            }

            _mainCamera = Camera.main;
        }

        // --- Auto-add trackers on open ---
        private void AutoAddTrackers()
        {
            foreach (var info in _recommendedTrackers)
            {
                var trackerType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
                    .FirstOrDefault(tp => tp.Name == info.componentTypeName);
                if (trackerType == null) continue;
                bool isPresent = (Object.FindObjectOfType(trackerType) as Component) != null;
                if (isPresent) continue;
                if (info.target == TrackerTarget.AnyObject)
                {
                    var manager = Object.FindObjectOfType<GossipManager>();
                    if (manager != null) Undo.AddComponent(manager.gameObject, trackerType);
                }
                else if (info.target == TrackerTarget.Player && _playerObject != null)
                {
                    Undo.AddComponent(_playerObject, trackerType);
                }
                else if (info.target == TrackerTarget.Camera)
                {
                    if (_mainCamera == null) _mainCamera = Camera.main;
                    if (_mainCamera != null) Undo.AddComponent(_mainCamera.gameObject, trackerType);
                }
            }
        }

        // --- Tab: Trackers ---
        private void DrawTrackersTab()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "Trackers collect analytics data from your player and device. Spatial trackers " +
                "require a Player object assigned below. Device trackers run automatically.",
                MessageType.Info);
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Player:", GUILayout.Width(60));
            _playerObject = (GameObject)EditorGUILayout.ObjectField(_playerObject, typeof(GameObject), true);
            if (GUILayout.Button("Auto-detect", GUILayout.Width(90)))
            {
                _playerObject = null;
                AutoDetectPlayer();
                if (_playerObject == null)
                    EditorUtility.DisplayDialog("Player Not Found", "No object with tag Player, XROrigin, or XR Rig was found.", "OK");
            }
            EditorGUILayout.EndHorizontal();
            if (_playerObject == null)
            {
                var hintStyle = new GUIStyle(EditorStyles.miniLabel);
                hintStyle.wordWrap = true;
                EditorGUILayout.LabelField(
                    "Assign your XR player root (XROrigin or equivalent). Spatial trackers attach here.", hintStyle);
            }
            if (_mainCamera == null) _mainCamera = Camera.main;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Camera:", GUILayout.Width(60));
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField(_mainCamera, typeof(Camera), true);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(8);
            int missingCount = 0, presentCount = 0;
            foreach (var info in _recommendedTrackers)
            {
                var tp = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
                    .FirstOrDefault(t => t.Name == info.componentTypeName);
                bool present = tp != null && (Object.FindObjectOfType(tp) as Component) != null;
                if (present) presentCount++; else missingCount++;
            }
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (missingCount > 0 && GUILayout.Button("Add All to Scene", GUILayout.Width(130)))
                AddAllTrackers();
            if (presentCount > 0 && GUILayout.Button("Deselect All", GUILayout.Width(90)))
            {
                bool confirm = EditorUtility.DisplayDialog("Deselect All Trackers", "Remove all tracker components from the scene?", "Deselect All", "Cancel");
                if (confirm) RemoveAllTrackers();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);

            string currentCategory = null;
            foreach (var info in _recommendedTrackers)
            {
                if (info.category != currentCategory)
                {
                    currentCategory = info.category;
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField(currentCategory, EditorStyles.boldLabel);
                }
                var trackerType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
                    .FirstOrDefault(tp => tp.Name == info.componentTypeName);
                Component existing = trackerType != null ? (Object.FindObjectOfType(trackerType) as Component) : null;
                bool isPresent = existing != null;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                string statusIcon = isPresent ? "✅" : "○";
                EditorGUILayout.LabelField(statusIcon + "  " + info.displayName, GUILayout.ExpandWidth(true));
                bool playerNeeded = info.target == TrackerTarget.Player || info.target == TrackerTarget.Camera;
                bool canAdd = true; // opt-out model: always allow adding; warnings inform about missing Player
                if (!isPresent)
                {
                    EditorGUI.BeginDisabledGroup(!canAdd);
                    if (GUILayout.Button("Add", GUILayout.Width(50)))
                        AddTracker(info);
                    EditorGUI.EndDisabledGroup();
                }
                else
                {
                    var prevBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
                    if (GUILayout.Button("Deselect", GUILayout.Width(65)))
                    {
                        bool confirm = EditorUtility.DisplayDialog(
                            "Deselect " + info.displayName + "?",
                            "Removing this tracker will stop recording: " + info.description + " Are you sure?",
                            "Deselect", "Cancel");
                        if (confirm)
                        {
                            Undo.DestroyObjectImmediate(existing);
                            EditorSceneManager.MarkSceneDirty(existing.gameObject.scene);
                        }
                    }
                    GUI.backgroundColor = prevBg;
                }
                EditorGUILayout.EndHorizontal();
                var descStyle = new GUIStyle(EditorStyles.miniLabel);
                descStyle.wordWrap = true;
                string desc = info.description;
                if (info.requiresConfiguration) desc += " ⚠ Requires configuration in Inspector after adding.";
                if (playerNeeded && _playerObject == null && !isPresent) desc = "⚠ Assign Player first. " + desc;
                EditorGUILayout.LabelField(desc, descStyle);
                if (isPresent && info.requiresConfiguration)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.HelpBox(
                        "Requires configuration in Inspector — the SDK cannot know your app's specific thresholds. Open the Inspector and configure before building.",
                        MessageType.Warning);
                    if (GUILayout.Button("Open\nInspector", GUILayout.Width(70), GUILayout.Height(38)))
                    {
                        Selection.activeGameObject = existing.gameObject;
                        EditorApplication.delayCall += () =>
                        {
                            var inspectorType = typeof(UnityEditor.Editor).Assembly
                                .GetType("UnityEditor.InspectorWindow");
                            if (inspectorType != null)
                                EditorWindow.GetWindow(inspectorType).Focus();
                        };
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }
        }

        // --- Tracker helpers ---
        private void AddTracker(TrackerInfo info)
        {
            var trackerType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
                .FirstOrDefault(tp => tp.Name == info.componentTypeName);
            if (trackerType == null)
            {
                EditorUtility.DisplayDialog("Not Found", info.componentTypeName + " not found. Make sure the SDK is fully imported.", "OK");
                return;
            }
            GameObject target = null;
            if (info.target == TrackerTarget.Player)
                target = _playerObject;
            else if (info.target == TrackerTarget.Camera)
            {
                if (_mainCamera == null) _mainCamera = Camera.main;
                if (_mainCamera != null) target = _mainCamera.gameObject;
            }
            else
            {
                var manager = Object.FindObjectOfType<GossipManager>();
                if (manager != null) target = manager.gameObject;
            }
            if (target == null)
            {
                var go = new GameObject(info.displayName);
                Undo.RegisterCreatedObjectUndo(go, "Create Tracker");
                target = go;
            }
            Undo.AddComponent(target, trackerType);
            EditorSceneManager.MarkSceneDirty(target.scene);
            _lastTrackerCountTime = 0;
        }

        private void AddAllTrackers()
        {
            foreach (var info in _recommendedTrackers)
            {
                var tp = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
                    .FirstOrDefault(t => t.Name == info.componentTypeName);
                bool present = tp != null && (Object.FindObjectOfType(tp) as Component) != null;
                if (!present) AddTracker(info);
            }
        }

        private void RemoveAllTrackers()
        {
            foreach (var info in _recommendedTrackers)
            {
                var tp = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
                    .FirstOrDefault(t => t.Name == info.componentTypeName);
                if (tp == null) continue;
                var existing = Object.FindObjectOfType(tp) as Component;
                if (existing != null) { EditorSceneManager.MarkSceneDirty(existing.gameObject.scene); Undo.DestroyObjectImmediate(existing); }
                _lastTrackerCountTime = 0;
            }
        }

        // --- Tab: Permissions ---
        private void DrawPermissionsTab()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "These Android permissions are required for full data collection on Android XR devices. " +
                "All are pre-enabled. Deselect only if your app does not use that feature.",
                MessageType.Info);
            EditorGUILayout.Space(4);
            var handler = Object.FindObjectOfType<VRPermissionsHandler>();
            if (handler == null)
            {
                var go = new GameObject("VRPermissionsHandler");
                Undo.RegisterCreatedObjectUndo(go, "Create VRPermissionsHandler");
                Undo.AddComponent<VRPermissionsHandler>(go);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                handler = go.GetComponent<VRPermissionsHandler>();
            }
            EditorGUILayout.HelpBox("✅ VRPermissionsHandler active — Your app will request the selected permissions on Android VR device launch.", MessageType.Info);
            EditorGUILayout.Space(6);
            if (_vrHandlerSO == null || _vrHandlerSO.targetObject != handler)
                _vrHandlerSO = new SerializedObject(handler);
            _vrHandlerSO.Update();
            var propEye = _vrHandlerSO.FindProperty("enableEyeTracking");
            var propSpatial = _vrHandlerSO.FindProperty("enableSpatialScene");
            var propCamera = _vrHandlerSO.FindProperty("enableHeadsetCamera");
            var propMic = _vrHandlerSO.FindProperty("enableMicrophone");

            DrawPermissionRow(_vrHandlerSO, propEye,
                "Eye Tracking",
                "Gaze data and fixation. Powers heat-of-gaze analytics.",
                "Deselecting will stop gaze data capture. Heat-of-gaze analytics will not function.");

            DrawPermissionRow(_vrHandlerSO, propSpatial,
                "Scene / Spatial",
                "Environment mesh for spatial heatmaps.",
                "Deselecting will disable spatial heatmaps. Environment data will not be captured.");

            DrawPermissionRow(_vrHandlerSO, propCamera,
                "Headset Camera",
                "Passthrough and MR features.",
                "Deselecting will disable passthrough and mixed reality features.");

            DrawPermissionRow(_vrHandlerSO, propMic,
                "Microphone",
                "Emotion detection via brief audio samples. Privacy: audio processed on-device and discarded. No recordings stored or transmitted.",
                "Deselecting will disable audio-based emotion detection.");

            _vrHandlerSO.ApplyModifiedProperties();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All", GUILayout.Width(80)))
            {
                if (_vrHandlerSO != null)
                {
                    _vrHandlerSO.Update();
                    foreach (var p in new[]{"enableEyeTracking","enableSpatialScene",
                                             "enableHeadsetCamera","enableMicrophone"})
                    {
                        var prop = _vrHandlerSO.FindProperty(p);
                        if (prop != null) prop.boolValue = true;
                    }
                    _vrHandlerSO.ApplyModifiedProperties();
                }
            }
            if (GUILayout.Button("Deselect All", GUILayout.Width(90)))
            {
                if (_vrHandlerSO != null)
                {
                    _vrHandlerSO.Update();
                    foreach (var p in new[]{"enableEyeTracking","enableSpatialScene",
                                             "enableHeadsetCamera","enableMicrophone"})
                    {
                        var prop = _vrHandlerSO.FindProperty(p);
                        if (prop != null) prop.boolValue = false;
                    }
                    _vrHandlerSO.ApplyModifiedProperties();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(12);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.7f, 0.3f, 0.3f);
            if (GUILayout.Button("Remove Handler", GUILayout.Width(110)))
            {
                string msg = "Deselecting VRPermissionsHandler will disable the following on Android XR devices:" +
                System.Environment.NewLine + "• Eye Tracking — gaze data will not be captured" +
                System.Environment.NewLine + "• Spatial / Scene — heatmap environment data will be lost" +
                System.Environment.NewLine + "• Headset Camera — passthrough and MR will not work" +
                System.Environment.NewLine + "• Microphone — audio reaction tracking will be silent" +
                System.Environment.NewLine + System.Environment.NewLine + "Are you sure?";
                bool confirm = EditorUtility.DisplayDialog("Deselect VRPermissionsHandler", msg, "Deselect Anyway", "Cancel");
                if (confirm && handler != null)
                {
                    Undo.DestroyObjectImmediate(handler.gameObject);
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                    _vrHandlerSO = null;
                }
            }
            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndHorizontal();
        }

        // --- DrawPermissionRow ---
        private void DrawPermissionRow(SerializedObject so, SerializedProperty prop, string label, string description, string deselectImpact)
        {
            if (prop == null) return;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            string icon = prop.boolValue ? "✅" : "○";
            EditorGUILayout.LabelField(icon + "  " + label, GUILayout.ExpandWidth(true));
            bool newVal = EditorGUILayout.Toggle(prop.boolValue, GUILayout.Width(18));
            if (newVal != prop.boolValue)
            {
                if (!newVal)
                {
                    bool confirm = EditorUtility.DisplayDialog(
                        "Deselect " + label + " permission?",
                        deselectImpact,
                        "Deselect", "Cancel");
                    if (confirm) prop.boolValue = false;
                }
                else
                    prop.boolValue = true;
            }
            EditorGUILayout.EndHorizontal();
            var descStyle = new GUIStyle(EditorStyles.miniLabel);
            descStyle.wordWrap = true;
            EditorGUILayout.LabelField(description, descStyle);
            EditorGUILayout.EndVertical();
        }

    }
}
#endif
