using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GossipAnalytics.Editor
{
    public static class GossipUninstaller
    {
        // ---- MenuItems ----

        [MenuItem("Window/Gossip Analytics/Uninstall SDK", false, 100)]
        public static void RunUninstall()
        {
            bool confirm = EditorUtility.DisplayDialog(
                "Uninstall Gossip Analytics SDK",
                "This will remove:\n\n- All Gossip Analytics components from your scenes (GossipAnalyticsManager, InteractableComponent, VRPermissionsHandler, and all tracker components)\n\n- GossipAnalyticsSettings asset\n\n- GossipInstrumentationData asset\n\n- Assets/Samples/Gossip Analytics SDK/ folder\n\n- The SDK package itself (com.gossip.core)\n\nThis will NOT remove:\n\n- Your own scripts or scenes\n\n- Dependency packages (LiteDB, R3, UniTask, XR packages)\n\n- README, CHANGELOG or LICENSE files (removed with the package automatically)",
                "Uninstall",
                "Cancel");

            if (!confirm) return;

            try
            {
                RunStep1_RemoveSceneComponents();
                RunStep2_DeleteAssets();
                RunStep3_RemovePackage();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            EditorUtility.DisplayDialog(
                "Gossip Analytics removed",
                "All Gossip Analytics components and assets have been removed from your project. Your scenes and scripts are untouched.\n\nWe hope to see you again soon.",
                "Close");
        }

        [MenuItem("Window/Gossip Analytics/Uninstall SDK", true, 100)]
        public static bool RunUninstall_Validate()
        {
            return true;
        }

        // ---- Step 1: Remove SDK components from all build scenes ----

private static void RunStep1_RemoveSceneComponents()
        {
            string originalScenePath = SceneManager.GetActiveScene().path;
            var processed = new HashSet<string>();

            // 1) Clean ALL open scenes in-place (covers active scene even if NOT in Build Settings).
            var open = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var sc = SceneManager.GetSceneAt(i);
                if (sc.isLoaded) open.Add(sc);
            }
            foreach (var sc in open)
            {
                try
                {
                    RemoveGossipComponentsFromScene(sc);
                    if (!string.IsNullOrEmpty(sc.path)) { EditorSceneManager.SaveScene(sc); processed.Add(sc.path); }
                }
                catch (Exception ex) { Debug.LogWarning("[GossipUninstaller] Open scene " + sc.path + ": " + ex.Message); }
            }

            // 2) Build Settings scenes not yet processed.
            var buildScenes = EditorBuildSettings.scenes;
            for (int i = 0; i < buildScenes.Length; i++)
            {
                var sceneRef = buildScenes[i];
                if (!sceneRef.enabled || processed.Contains(sceneRef.path)) continue;

                EditorUtility.DisplayProgressBar("Gossip Analytics - Uninstalling...",
                    string.Format("Removing Gossip components... ({0} of {1})", i + 1, buildScenes.Length),
                    (float)(i + 1) / (buildScenes.Length + 2));

                try
                {
                    var scene = EditorSceneManager.OpenScene(sceneRef.path, OpenSceneMode.Single);
                    RemoveGossipComponentsFromScene(scene);
                    EditorSceneManager.SaveScene(scene);
                    processed.Add(sceneRef.path);
                }
                catch (Exception ex) { Debug.LogWarning("[GossipUninstaller] Scene " + sceneRef.path + ": " + ex.Message); }
            }

            if (!string.IsNullOrEmpty(originalScenePath) && SceneManager.GetActiveScene().path != originalScenePath)
            {
                try { EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single); } catch { }
            }
        }

        private static void RemoveGossipComponentsFromScene(Scene scene)
        {
            var gossipTypes = GetGossipComponentTypes();
            string managerPrefabGuid = ResolveManagerPrefabGuid();

            // Step A — destroy SDK-owned GameObjects entirely (guards verified before destruction).
            var toDestroy = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                {
                    var go = tr.gameObject;
                    if (go.transform.childCount != 0) continue;             // never destroy a GO with children
                    if (IsSdkOwnedGameObject(go, managerPrefabGuid, gossipTypes)) toDestroy.Add(go);
                }

            foreach (var go in toDestroy)
                if (go != null) { try { Undo.DestroyObjectImmediate(go); } catch (Exception ex) { Debug.LogWarning("[GossipUninstaller] GO: " + ex.Message); } }

            // Step B — remove SDK components remaining on USER objects (component only).
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null) continue;
                foreach (var comp in root.GetComponentsInChildren<Component>(true))
                {
                    if (comp == null) continue;
                    if (IsGossipComponent(comp.GetType(), gossipTypes))
                    {
                        try { Undo.DestroyObjectImmediate(comp); }
                        catch (Exception ex) { Debug.LogWarning("[GossipUninstaller] Comp " + comp.GetType().Name + ": " + ex.Message); }
                    }
                }
            }
        }

        private static string ResolveManagerPrefabGuid()
        {
            var pg = AssetDatabase.FindAssets("GossipAnalyticsManager t:Prefab", new[] { "Assets/Samples" });
            if (pg.Length > 0) return pg[0];
            return "29e83dfafdc2bf442a91a4129b076e3e"; // fallback to known prefab GUID
        }

        private static bool IsSdkOwnedGameObject(GameObject go, string managerPrefabGuid, List<Type> gossipTypes)
        {
            // Manager (healthy or with core removed): match by source prefab GUID — most robust.
            if (PrefabUtility.IsPartOfPrefabInstance(go))
            {
                var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (src != null)
                {
                    var g = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(src));
                    if (!string.IsNullOrEmpty(managerPrefabGuid) && g == managerPrefabGuid) return true;
                }
            }

            // Standalone SDK hosts: only if ALL non-Transform components are SDK-owned.
            if (!AllComponentsAreSdk(go, gossipTypes)) return false;
            if (GoHasComponentNamed(go, "GossipManager")) return true;
            if (go.name == "VRPermissionsHandler" && GoHasComponentNamed(go, "VRPermissionsHandler")) return true;
            if (go.name == "Gossip Device Trackers") return true;
            return false;
        }

        private static bool AllComponentsAreSdk(GameObject go, List<Type> gossipTypes)
        {
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;      // missing script (core removed) -> ignore
                if (comp is Transform) continue;
                if (!IsGossipComponent(comp.GetType(), gossipTypes)) return false; // user component present
            }
            return true;
        }

        private static bool GoHasComponentNamed(GameObject go, string typeName)
        {
            foreach (var comp in go.GetComponents<Component>())
                if (comp != null && comp.GetType().Name == typeName) return true;
            return false;
        }

        private static bool IsGossipComponent(Type compType, List<Type> explicitTypes)
        {
            if (explicitTypes.Contains(compType)) return true;
            if (!typeof(MonoBehaviour).IsAssignableFrom(compType)) return false;
            var asmName = compType.Assembly.GetName().Name;
            if (asmName == "GossipSDK.Runtime" || asmName == "GossipSDK.Editor") return true; // robust catch-all
            var ns = compType.Namespace ?? string.Empty;
            if (ns.StartsWith("GossipSDK")) return true;
            return false;
        }

                private static List<Type> GetGossipComponentTypes()
        {
            var result = new List<Type>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            // Explicit type names to always remove
            var explicitNames = new HashSet<string>
            {
                "GossipAnalyticsManager",
                "InteractableComponent",
                "VRPermissionsHandler",
                "GossipPositionTracker",
                "GossipRotationTracker",
                "GossipBalanceTracker",
                "GossipPostureTracker",
                "GossipHeadsetTracker",
                "GossipControllerTracker",
                "GossipEyeTracker",
                "PauseComponent",
                "PassthroughComponent",
            };

            foreach (var asm in assemblies)
            {
                try
                {
                    foreach (var t in asm.GetTypes())
                    {
                        if (explicitNames.Contains(t.Name) && typeof(Component).IsAssignableFrom(t))
                            result.Add(t);
                    }
                }
                catch { /* skip assemblies that throw on GetTypes() */ }
            }

            return result;
        }

        // ---- Step 2: Delete known Gossip assets ----

        private static void RunStep2_DeleteAssets()
        {
            EditorUtility.DisplayProgressBar(
                "Gossip Analytics â Uninstalling...",
                "Deleting Gossip assets...",
                0.75f);

            // Step 2 — delete settings asset (search by type AND by filename as fallback)
            EditorUtility.DisplayProgressBar("Gossip Uninstaller", "Removing settings asset...", 0.4f);

            var deletedPaths = new System.Collections.Generic.List<string>();

            // Primary: find by ScriptableObject type
            string[] settingsGuids = AssetDatabase.FindAssets("t:GossipAnalyticsSettings");
            foreach (var guid in settingsGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.DeleteAsset(path))
                {
                    deletedPaths.Add(path);
                    Debug.Log($"GossipSDK Uninstaller: deleted {path}");
                }
            }

            // Fallback: find by filename in case type search missed it
            string[] nameGuids = AssetDatabase.FindAssets("GossipAnalyticsSettings");
            foreach (var guid in nameGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!deletedPaths.Contains(path))
                {
                    if (AssetDatabase.DeleteAsset(path))
                    {
                        Debug.Log($"GossipSDK Uninstaller (fallback): deleted {path}");
                    }
                    else
                    {
                        Debug.LogWarning($"GossipSDK Uninstaller: could not delete {path} — delete it manually.");
                    }
                }
            }
            // Delete GossipInstrumentationData asset
            string[] dataGuids = AssetDatabase.FindAssets("t:GossipInstrumentationData");
            foreach (var guid in dataGuids)
            {
                try
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    AssetDatabase.DeleteAsset(path);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[GossipUninstaller] Error deleting data: " + ex.Message);
                }
            }

            // FIX 2: Delete Assets/Gossip Analytics/ folder if empty
            string gossipDir = "Assets/Gossip Analytics";
            if (AssetDatabase.IsValidFolder(gossipDir))
            {
                var remaining = AssetDatabase.FindAssets("", new[] { gossipDir });
                if (remaining.Length == 0)
                    AssetDatabase.DeleteAsset(gossipDir);
            }

            // Delete Samples folder
            string samplesPath = "Assets/Samples/Gossip Analytics SDK";
            try
            {
                if (AssetDatabase.IsValidFolder(samplesPath) ||
                    System.IO.Directory.Exists(samplesPath))
                AssetDatabase.DeleteAsset(samplesPath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GossipUninstaller] Error deleting samples: " + ex.Message);
            }

            AssetDatabase.Refresh();
        }

        // ---- Step 3: Remove package ----

        private static void RunStep3_RemovePackage()
        {
            EditorUtility.DisplayProgressBar(
                "Gossip Analytics — Uninstalling...",
                "Removing com.gossip.core package...",
                0.9f);

            try
            {
                UnityEditor.PackageManager.Client.Remove("com.gossip.core");
                Debug.Log("[GossipUninstaller] Package removal requested: com.gossip.core");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GossipUninstaller] Could not remove package com.gossip.core: " + ex.Message);
            }

            // Clean up all cached versions of com.gossip.core from Library/PackageCache
            try
            {
                string projectRoot    = System.IO.Path.GetDirectoryName(Application.dataPath);
                string packageCache   = System.IO.Path.Combine(projectRoot, "Library", "PackageCache");
                if (System.IO.Directory.Exists(packageCache))
                {
                    var gossipDirs = System.IO.Directory.GetDirectories(packageCache, "com.gossip.core@*");
                    foreach (var dir in gossipDirs)
                    {
                        System.IO.Directory.Delete(dir, true);
                        Debug.Log("[GossipUninstaller] Deleted PackageCache entry: " + dir);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GossipUninstaller] Could not clean PackageCache: " + ex.Message);
            }
        }
    }
}
