#if UNITY_EDITOR
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

            // ── Bottom help ──
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "After applying, call OnInteractInstant(\"grab\") or OnInteractStart/End from your input handler " +
                "on each instrumented object, or use autoTriggerOnStart for testing.",
                MessageType.Info);
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
    }
}
#endif
