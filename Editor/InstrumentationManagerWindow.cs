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
        // ——— State ————————————————————————————————————————————————————————————
        private GossipInstrumentationData _data;
        private Dictionary<string, List<ScannedObject>> _sceneObjects = new Dictionary<string, List<ScannedObject>>();
        private Dictionary<string, bool> _sceneFoldouts = new Dictionary<string, bool>();
        private bool _hasNewObjects = false;
        private Vector2 _scrollPos;
        private bool _isScanning = false;
        private int _selectedTab = 0;
        private readonly string[] _tabLabels = new string[] { " Interactables", " Trackers", " Permissions" };
        private GameObject _playerObject = null;
        private Camera _mainCamera = null;

        private static readonly string[] InteractableKeywords = new[]
        {
            "Interactable", "Grabbable", "Pickup", "Interactor", "Button", "Lever", "Trigger"
        };

        private static readonly string[] ExcludeNameKeywords = new[]
        {
            "wall", "floor", "ceiling", "ground", "terrain", "sky",
            "ambient", "light", "camera", "canvas", "event"
        };

        // ——— Inner type ——————————————————————————————————————————————————————
        private class ScannedObject
        {
            public string sceneName;
            public string hierarchyPath;
            public string objectName;
            public bool isChecked;
            public bool hasInteractable;
            public bool isNew;
        }

        // ——— Tracker types ————————————————————————————————————————————————————
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

        // ——— Menu entry ——————————————————————————————————————————————————————
        [MenuItem("Window/Gossip Analytics/2 — Instrumentation Manager", false, 2)]
        public static void Open()
        {
            GetWindow<InstrumentationManagerWindow>(" Instrumentation Manager");
        }

        // ——— Lifecycle ———————————————————————————————————————————————————————
        private void OnEnable()
        {
            LoadOrCreateData();
            ScanAllScenes();
            AutoDetectPlayer();
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

            if (foundNew != _hasNewObjects)
            {
                _hasNewObjects = foundNew;
                Repaint();
            }
        }

        // ——— OnGUI ———————————————————————————————————————————————————————————
        private void OnGUI()
        {
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabLabels, GUILayout.Height(30));
            EditorGUILayout.Space(4);

            float footerHeight = 36f;
            Rect footerRect = new Rect(0, position.height - footerHeight, position.width, footerHeight);
            Rect contentRect = new Rect(0, 40, position.width, position.height - 40 - footerHeight);

            GUILayout.BeginArea(contentRect);
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            if (_selectedTab == 0)
                DrawInteractablesTab();
            else if (_selectedTab == 1)
                DrawTrackersTab();
            else if (_selectedTab == 2)
                DrawPermissionsTab();

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();

            DrawFooter(footerRect);
        }

        // ——— Footer ——————————————————————————————————————————————————————————
        private void DrawFooter(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 1f));

            int trackedCount = 0;
            foreach (var kvp in _sceneObjects)
                foreach (var obj in kvp.Value)
                    if (obj.isChecked) trackedCount++;

            int activeTrackers = 0;
            foreach (var t in _recommendedTrackers)
            {
                var type = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
                    .FirstOrDefault(tp => tp.Name == t.componentTypeName);
                if (type != null && (Object.FindObjectOfType(type) as Component) != null) activeTrackers++;
            }

            bool permsActive = Object.FindObjectOfType<VRPermissionsHandler>() != null;
            string permsLabel = permsActive ? "enabled" : "disabled";
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

            var prevBgColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.2f, 0.6f, 0.2f, 1f);
            if (GUI.Button(btnRect, "✅ Done — Save & Close", doneStyle))
            {
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                Close();
            }
            GUI.backgroundColor = prevBgColor;
        }

        // ——— Tab: Interactables (opt-out) ————————————————————————————————————
        private void DrawInteractablesTab()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Gossip Analytics — Instrumentation Manager", EditorStyles.boldLabel);

            if (_data == null)
            {
                EditorGUILayout.HelpBox("No data asset found. Try reopening the window.", MessageType.Error);
                return;
            }

            if (_hasNewObjects)
                EditorGUILayout.HelpBox("New interactable objects detected. Click Refresh to review them.", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Width(70)))
            {
                ScanAllScenes();
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
                EditorGUILayout.HelpBox("No interactable objects found in open scenes. Click Refresh after adding objects.", MessageType.Info);
                return;
            }

            foreach (var kvp in _sceneObjects)
            {
                string sceneName = kvp.Key;
                var objs = kvp.Value;

                if (!_sceneFoldouts.ContainsKey(sceneName)) _sceneFoldouts[sceneName] = true;
                _sceneFoldouts[sceneName] = EditorGUILayout.BeginFoldoutHeaderGroup(
                    _sceneFoldouts[sceneName], sceneName + " (" + objs.Count + " objects)");

                if (_sceneFoldouts[sceneName])
                {
                    var sorted = objs.OrderByDescending(o => o.isNew).ThenBy(o => o.objectName).ToList();
                    foreach (var obj in sorted)
                        DrawObjectRow(obj);
                }

                EditorGUILayout.EndFoldoutHeaderGroup();
            }
        }

        // ——— DrawObjectRow ———————————————————————————————————————————————————
        private void DrawObjectRow(ScannedObject obj)
        {
            EditorGUILayout.BeginHorizontal();

            bool newChecked = EditorGUILayout.Toggle(obj.isChecked, GUILayout.Width(18));

            if (newChecked != obj.isChecked)
            {
                if (!newChecked && obj.hasInteractable)
                {
                    bool confirm = EditorUtility.DisplayDialog(
                        "Remove Tracking",
                        "This will remove InteractableComponent from " + obj.objectName + ". This object will no longer track interactions. Remove anyway?",
                        "Remove", "Cancel");
                    if (!confirm)
                    {
                        EditorGUILayout.EndHorizontal();
                        return;
                    }
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

        // ——— Scanning ————————————————————————————————————————————————————————
        private void ScanAllScenes()
        {
            _isScanning = true;
            _sceneObjects.Clear();

            var allStoredPaths = new Dictionary<string, HashSet<string>>();
            if (_data != null)
                foreach (var entry in _data.scenes)
                {
                    if (!allStoredPaths.ContainsKey(entry.sceneName))
                        allStoredPaths[entry.sceneName] = new HashSet<string>(entry.instrumentedPaths);
                }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                string sceneName = scene.name;
                var sceneList = new List<ScannedObject>();
                _sceneObjects[sceneName] = sceneList;

                var storedPaths = allStoredPaths.ContainsKey(sceneName) ? allStoredPaths[sceneName] : new HashSet<string>();
                var scannedPaths = new HashSet<string>();
                foreach (var root in scene.GetRootGameObjects())
                    CollectInteractableObjectsForScan(root, sceneName, scannedPaths, storedPaths, sceneList);
            }

            _hasNewObjects = false;
            _isScanning = false;
            Repaint();
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
                    if (!entry.instrumentedPaths.Contains(path))
                        entry.instrumentedPaths.Add(path);
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
            if (IsInteractable(go) && !scannedPaths.Contains(path))
                scannedPaths.Add(path);
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
            if (go.GetComponent<Rigidbody>() != null)
                return true;
            // Include: tag
            if (go.tag == "Interactable" || go.tag == "Pickup")
                return true;
            return false;
        }

        // ——— Apply / Remove ——————————————————————————————————————————————————
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
                    if (obj.isChecked)
                    {
                        if (!entry.instrumentedPaths.Contains(obj.hierarchyPath))
                        { entry.instrumentedPaths.Add(obj.hierarchyPath); changed++; }
                    }
                    else
                    {
                        if (entry.instrumentedPaths.Contains(obj.hierarchyPath))
                        { entry.instrumentedPaths.Remove(obj.hierarchyPath); changed++; }
                    }
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

            EditorUtility.DisplayDialog(
                "Gossip Analytics — Applied!",
                "Instrumentation data saved across " + dirtySceneNames.Count + " scene(s).",
                "OK");

            ScanAllScenes();
        }

        private void RemoveInstrumentationForObject(ScannedObject obj)
        {
            var go = FindGameObjectByPath(obj.hierarchyPath, obj.sceneName);
            if (go != null)
            {
                var ic = go.GetComponent<InteractableComponent>();
                if (ic != null) Undo.DestroyObjectImmediate(ic);
            }
            if (_data != null)
            {
                var entry = _data.scenes.FirstOrDefault(e => e.sceneName == obj.sceneName);
                if (entry != null) entry.instrumentedPaths.Remove(obj.hierarchyPath);
                EditorUtility.SetDirty(_data);
            }
        }

        // ——— Data helpers ————————————————————————————————————————————————————
        private void LoadOrCreateData()
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
                AssetDatabase.CreateAsset(_data, "Assets/GossipAnalytics/GossipInstrumentationData.asset");
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
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
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

        // ——— Player auto-detect ——————————————————————————————————————————————
        private void AutoDetectPlayer()
        {
            if (_playerObject != null) return;

            var byTag = GameObject.FindGameObjectsWithTag("Player");
            if (byTag.Length > 0) { _playerObject = byTag[0]; return; }

            try
            {
                var xrOriginType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
                    .FirstOrDefault(tp => tp.FullName == "UnityEngine.XR.Interaction.Toolkit.XROrigin");
                if (xrOriginType != null)
                {
                    var xrOrigin = FindObjectOfType(xrOriginType) as Component;
                    if (xrOrigin != null) { _playerObject = xrOrigin.gameObject; return; }
                }
            }
            catch { }

            var allGOs = Object.FindObjectsOfType<GameObject>();
            foreach (var go in allGOs)
            {
                string lname = go.name.ToLower();
                if (lname.Contains("xrrig") || lname.Contains("xr rig"))
                {
                    _playerObject = go;
                    return;
                }
            }

            _mainCamera = Camera.main;
        }

        // ——— Tab: Trackers (opt-out) —————————————————————————————————————————
        private void DrawTrackersTab()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Scene Trackers", EditorStyles.boldLabel);

            // Player / Camera
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

            if (_mainCamera == null) _mainCamera = Camera.main;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Camera:", GUILayout.Width(60));
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField(_mainCamera, typeof(Camera), true);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(8);

            // Global Add All / Remove All
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
            if (presentCount > 0 && GUILayout.Button("Remove All", GUILayout.Width(90)))
            {
                bool confirm = EditorUtility.DisplayDialog("Remove All Trackers", "Remove all tracker components from the scene?", "Remove All", "Cancel");
                if (confirm) RemoveAllTrackers();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);

            // Per-tracker rows
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
                bool canAdd = !playerNeeded || _playerObject != null || info.target == TrackerTarget.Camera;

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
                    if (GUILayout.Button("Remove", GUILayout.Width(60)))
                    {
                        bool confirm = EditorUtility.DisplayDialog(
                            "Remove " + info.displayName,
                            "Removing this tracker will stop recording: " + info.description + " Are you sure?",
                            "Remove", "Cancel");
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

                EditorGUILayout.EndVertical();
            }
        }

        // ——— Tracker helpers —————————————————————————————————————————————————
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
            if (info.target == TrackerTarget.Player && _playerObject != null)
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
                if (existing != null)
                {
                    EditorSceneManager.MarkSceneDirty(existing.gameObject.scene);
                    Undo.DestroyObjectImmediate(existing);
                }
            }
        }

        // ——— Tab: Permissions (opt-out) ——————————————————————————————————————
        private void DrawPermissionsTab()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("VR Permissions", EditorStyles.boldLabel);

            var handler = Object.FindObjectOfType<VRPermissionsHandler>();

            if (handler == null)
            {
                var go = new GameObject("VRPermissionsHandler");
                Undo.RegisterCreatedObjectUndo(go, "Create VRPermissionsHandler");
                Undo.AddComponent<VRPermissionsHandler>(go);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                handler = go.GetComponent<VRPermissionsHandler>();
            }

            EditorGUILayout.HelpBox("✅ VRPermissionsHandler active — Your app will request these permissions on Meta Quest launch.", MessageType.Info);
            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("Permissions included:", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("✅ Eye Tracking", GUILayout.Width(150));
                EditorGUILayout.LabelField("Gaze data and fixation. Powers heat-of-gaze analytics.", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("✅ Scene / Spatial", GUILayout.Width(150));
                EditorGUILayout.LabelField("Environment mesh for spatial heatmaps.", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("✅ Headset Camera", GUILayout.Width(150));
                EditorGUILayout.LabelField("Passthrough and MR features.", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("✅ Microphone", GUILayout.Width(150));
                EditorGUILayout.LabelField("Audio reaction and voice interaction tracking.", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(12);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.7f, 0.3f, 0.3f);
            if (GUILayout.Button("Remove VRPermissionsHandler", GUILayout.Width(200)))
            {
                string msg = "Removing VRPermissionsHandler will disable the following on Meta Quest:" +
                System.Environment.NewLine + "• Eye Tracking — gaze data will not be captured" +
                System.Environment.NewLine + "• Spatial / Scene — heatmap environment data will be lost" +
                System.Environment.NewLine + "• Headset Camera — passthrough and MR will not work" +
                System.Environment.NewLine + "• Microphone — audio reaction tracking will be silent" +
                System.Environment.NewLine + System.Environment.NewLine + "Are you sure?";
                bool confirm = EditorUtility.DisplayDialog("Remove VRPermissionsHandler", msg, "Remove Anyway", "Cancel");
                if (confirm && handler != null)
                {
                    Undo.DestroyObjectImmediate(handler.gameObject);
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                }
            }
            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndHorizontal();
        }

    }
}
#endif
