#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using GossipSDK;
using GossipSDK.Core.Configuration;

namespace GossipSDK.Editor
{
    public static class GossipAnalyticsSetup
    {
                [MenuItem("Window/Gossip Analytics/1 — Quick Setup", false, 1)]
                public static void RunQuickSetup()
                {
                                // STEP 1 — Check if GossipManager is already in the scene

                                // B — do not run during recompile (avoids the missing-script transient that duplicates)
                                if (EditorApplication.isCompiling)
                                {
                                    EditorUtility.DisplayDialog("Gossip Analytics",
                                        "Unity is still compiling. Wait until it finishes, then run Quick Setup again.", "OK");
                                    return;
                                }

                                GossipManager manager = null;

                                // Resolve the manager prefab GUID dynamically (to detect broken instances)
                                string managerPrefabGuid = null;
                                {
                                    var pg = AssetDatabase.FindAssets("GossipAnalyticsManager t:Prefab", new[] { "Assets/Samples" });
                                    if (pg.Length > 0) managerPrefabGuid = pg[0];
                                }

                                // A — healthy managers (includes inactive)
                                var healthy = Object.FindObjectsByType<GossipManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);

                                // C — broken instances: prefab instance of the manager prefab WITHOUT the GossipManager component
                                var strays = new System.Collections.Generic.List<GameObject>();
                                if (!string.IsNullOrEmpty(managerPrefabGuid))
                                {
                                    foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                                    {
                                        foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                                        {
                                            var go = tr.gameObject;
                                            if (go.GetComponent<GossipManager>() != null) continue;            // healthy, handled below
                                            if (!PrefabUtility.IsPartOfPrefabInstance(go)) continue;
                                            if (PrefabUtility.GetNearestPrefabInstanceRoot(go) != go) continue; // only instance roots
                                            var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
                                            if (src == null) continue;
                                            var srcGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(src));
                                            if (srcGuid == managerPrefabGuid) strays.Add(go);                   // broken instance
                                        }
                                    }
                                }

                                // A — extra healthy managers (beyond the first) are also strays
                                for (int idx = 1; idx < healthy.Length; idx++)
                                    if (healthy[idx] != null) strays.Add(healthy[idx].gameObject);

                                // Cleanup with explicit CONFIRMATION (destructive operation)
                                if (strays.Count > 0)
                                {
                                    bool clean = EditorUtility.DisplayDialog(
                                        "Gossip Analytics - Clean up",
                                        "Found " + strays.Count + " extra or broken GossipAnalyticsManager object(s) (from previous runs or a package re-import). Remove them and keep a single clean manager?",
                                        "Remove", "Keep");
                                    if (clean)
                                        foreach (var s in strays)
                                            if (s != null) Undo.DestroyObjectImmediate(s);
                                }

                                // Reuse the first healthy manager if any remain
                                if (healthy.Length > 0 && healthy[0] != null)
                                {
                                    manager = healthy[0];
                                    Debug.Log("[Gossip Analytics] Using existing GossipManager.");
                                }
                                else
                                {
                                                    // Search for prefab in imported Samples location
                                                    var prefabGuids = AssetDatabase.FindAssets("GossipAnalyticsManager t:Prefab", new[] { "Assets/Samples" });

                                                    // Auto-import the Samples if the prefab isn't present yet (no manual step needed)
                                                    if (prefabGuids.Length == 0)
                                                    {
                                                                            if (TryImportSamples())
                                                                            {
                                                                                                        AssetDatabase.Refresh();
                                                                                                        prefabGuids = AssetDatabase.FindAssets("GossipAnalyticsManager t:Prefab", new[] { "Assets/Samples" });
                                                                            }
                                                    }

                                                    if (prefabGuids.Length == 0)
                                                    {
                                                                            EditorUtility.DisplayDialog(
                                                                                                        "Gossip Analytics - Prefab Not Found",
                                                                                                        "Could not import the Samples automatically. Please import them manually:\n\nPackage Manager -> Gossip Analytics SDK -> Samples -> Import.\n\nThen run Quick Setup again.",
                                                                                                        "OK");
                                                                            return;
                                                    }

                                                    var prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[0]);
                                                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                                                    var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

                                                    if (instance != null)
                                                    {
                                                                            instance.transform.position = Vector3.zero;
                                                                            manager = instance.GetComponent<GossipManager>();
                                                    }

                                                    if (manager == null)
                                                    {
                                                                            EditorUtility.DisplayDialog(
                                                                                                        "Gossip Analytics — Setup Error",
                                                                                                        "Failed to instantiate GossipAnalyticsManager prefab. Please check the prefab is valid.",
                                                                                                        "OK");
                                                                            return;
                                                    }
                                }

                                // STEP 2 — Find or create GossipAnalyticsSettings
                                GossipSettings settingsAsset = null;
                                var settingsGuids = AssetDatabase.FindAssets("t:GossipSettings");

                                if (settingsGuids.Length > 0)
                                {
                                                    var settingsPath = AssetDatabase.GUIDToAssetPath(settingsGuids[0]);
                                                    settingsAsset = AssetDatabase.LoadAssetAtPath<GossipSettings>(settingsPath);
                                                    Debug.Log("[Gossip Analytics] Found existing GossipSettings at: " + settingsPath);
                                }
                                else
                                {
                                                    settingsAsset = ScriptableObject.CreateInstance<GossipSettings>();
                                                    AssetDatabase.CreateAsset(settingsAsset, "Assets/GossipAnalyticsSettings.asset");
                                                    AssetDatabase.SaveAssets();
                                                    Debug.Log("[Gossip Analytics] Created new GossipSettings at Assets/GossipAnalyticsSettings.asset");
                                }

                                // STEP 3 — Assign settings to GossipManager via SerializedObject
                                var serializedManager = new SerializedObject(manager);
                                var settingsProp = serializedManager.FindProperty("settings");

                                if (settingsProp != null)
                                {
                                                    settingsProp.objectReferenceValue = settingsAsset;
                                                    serializedManager.ApplyModifiedProperties();
                                }
                                else
                                {
                                                    Debug.LogWarning("[Gossip Analytics] Could not find 'settings' property on GossipManager. Please assign the settings asset manually.");
                                }

                                // STEP 4 — Guardar la escena y mostrar el dialogo de cierre
                                //
                                // MarkSceneDirty solo pone el asterisco: el manager vive en memoria hasta que
                                // alguien guarda. El asset de settings, en cambio, ya esta en disco por el
                                // AssetDatabase.SaveAssets() de arriba, y el desinstalador SI guarda cada escena
                                // que toca (GossipUninstaller.cs:88 y :108). Esa asimetria es la que muerde:
                                // quien cierra Unity sin guardar se queda con los settings creados y SIN manager
                                // en la escena, y concluye que el SDK no funciona. Medido el 14-10-2026 en un
                                // proyecto real: GossipAnalyticsSettings.asset escrito a las 17:17 y la escena
                                // en disco a las 17:08, nueve minutos ANTES.
                                var activeScene = SceneManager.GetActiveScene();
                                EditorSceneManager.MarkSceneDirty(activeScene);

                                // Una escena sin ruta no se ha guardado nunca: ahi SaveScene abriria un dialogo
                                // de fichero, asi que no se fuerza y se avisa en el texto.
                                bool sceneSaved = !string.IsNullOrEmpty(activeScene.path)
                                                    && EditorSceneManager.SaveScene(activeScene);

                                EditorUtility.DisplayDialog(
                                                    "Gossip Analytics — Setup Complete ✅",
                                                    (sceneSaved
                                                                    ? "GossipAnalyticsManager is ready in your scene, and the scene has been saved.\n\n"
                                                                    : "GossipAnalyticsManager is ready in your scene, but the scene is NOT saved yet. Save it now (File -> Save) or the manager will be gone when you close Unity.\n\n")
                                                    + "The Inspector will show your GossipAnalyticsSettings. Get your 3 API Keys (Dev, Beta, Production) from the Gossip Analytics dashboard -> Company Overview section, enter them, then press Check Connection to verify.\n\nNext: open Window -> Gossip Analytics -> 2 -- Instrumentation Manager to select which objects to track interactions on.",
                                                    "OK");

                                EditorApplication.delayCall += () => {
                                                    EditorUtility.FocusProjectWindow();
                                                    Selection.activeObject = settingsAsset;
                                                    EditorGUIUtility.PingObject(settingsAsset);
                                                    var inspectorType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
                                                    if (inspectorType != null)
                                                                            EditorWindow.GetWindow(inspectorType);
                                };
                }

                private static bool TryImportSamples()
                {
                                try
                                {
                                                    var pkg = UnityEditor.PackageManager.PackageInfo.FindForPackageName("com.gossip.core");
                                                    string version = pkg != null ? pkg.version : string.Empty;
                                                    var samples = UnityEditor.PackageManager.UI.Sample.FindByPackage("com.gossip.core", version);
                                                    if (samples == null) return false;
                                                    bool any = false;
                                                    foreach (var sample in samples)
                                                    {
                                                                            if (sample.Import(
                                                                                                            UnityEditor.PackageManager.UI.Sample.ImportOptions.OverridePreviousImports |
                                                                                                            UnityEditor.PackageManager.UI.Sample.ImportOptions.HideImportWindow))
                                                                            {
                                                                                                        any = true;
                                                                            }
                                                    }
                                                    return any;
                                }
                                catch (System.Exception e)
                                {
                                                    Debug.LogWarning("[Gossip Analytics] Auto-import of Samples failed: " + e.Message);
                                                    return false;
                                }
                }
    }
}
#endif
