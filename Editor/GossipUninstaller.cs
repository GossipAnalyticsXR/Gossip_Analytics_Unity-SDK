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
                "This will remove only the components and assets added by Gossip Analytics. Your scenes, scripts and assets will not be affected.\n\nWhat will be removed:\n  - GossipAnalyticsManager from your scenes\n  - InteractableComponent from instrumented objects\n  - Tracker components added via Instrumentation Manager\n  - VRPermissionsHandler from your scenes\n  - GossipAnalyticsSettings asset\n  - GossipInstrumentationData asset\n  - Imported Samples folder (Assets/Samples/Gossip Analytics SDK/)\n  - The com.gossip.core package\n\nWhat will NOT be touched:\n  - Your scenes and GameObjects\n  - Your scripts and prefabs\n  - Any other package or asset in your project",
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
            var scenes = EditorBuildSettings.scenes;
            int total = scenes.Length;
            string originalScenePath = SceneManager.GetActiveScene().path;

            for (int i = 0; i < total; i++)
            {
                var sceneRef = scenes[i];
                if (!sceneRef.enabled) continue;

                EditorUtility.DisplayProgressBar(
                    "Gossip Analytics — Uninstalling...",
                    string.Format("Removing Gossip components from scenes... (scene {0} of {1})", i + 1, total),
                    (float)(i + 1) / (total + 2));

                try
                {
                    var scene = EditorSceneManager.OpenScene(sceneRef.path, OpenSceneMode.Single);
                    RemoveGossipComponentsFromScene(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[GossipUninstaller] Could not process scene: " + sceneRef.path + "\n" + ex.Message);
                }
            }

            // Restore original scene if possible
            if (!string.IsNullOrEmpty(originalScenePath))
            {
                try { EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single); }
                catch { /* scene may have been removed or renamed */ }
            }
        }

        private static void RemoveGossipComponentsFromScene(Scene scene)
        {
            // Collect all root GameObjects (including inactive)
            var roots = scene.GetRootGameObjects();

            // Build list of Gossip-specific types to remove explicitly
            var gossipTypes = GetGossipComponentTypes();

            foreach (var root in roots)
            {
                var allComponents = root.GetComponentsInChildren<Component>(true);
                foreach (var comp in allComponents)
                {
                    if (comp == null) continue;
                    var compType = comp.GetType();
                    if (IsGossipComponent(compType, gossipTypes))
                    {
                        try
                        {
                            Undo.DestroyObjectImmediate(comp);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning("[GossipUninstaller] Failed to remove component " + compType.Name + ": " + ex.Message);
                        }
                    }
                }
            }
        }

        private static bool IsGossipComponent(Type compType, List<Type> explicitTypes)
        {
            // Match explicit known types
            if (explicitTypes.Contains(compType)) return true;
            // Match any MonoBehaviour whose namespace starts with GossipSDK
            var ns = compType.Namespace ?? string.Empty;
            if (ns.StartsWith("GossipSDK") && typeof(MonoBehaviour).IsAssignableFrom(compType)) return true;
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
                "GossipEyeTracker"
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
                "Gossip Analytics — Uninstalling...",
                "Deleting Gossip assets...",
                0.75f);

            var paths = new[]
            {
                "Assets/GossipAnalyticsSettings.asset",
                "Assets/GossipInstrumentationData.asset",
                "Assets/Samples/Gossip Analytics SDK"
            };

            foreach (var path in paths)
            {
                try
                {
                    if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null ||
                        System.IO.Directory.Exists(path) ||
                        System.IO.File.Exists(path))
                    {
                        bool deleted = AssetDatabase.DeleteAsset(path);
                        if (!deleted)
                            Debug.LogWarning("[GossipUninstaller] Could not delete: " + path);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[GossipUninstaller] Error deleting " + path + ": " + ex.Message);
                }
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
        }
    }
}
