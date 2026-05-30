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
            GossipManager manager = null;
            var existingManagers = Object.FindObjectsByType<GossipManager>(FindObjectsSortMode.None);

            if (existingManagers.Length > 0)
            {
                manager = existingManagers[0];
                Debug.Log("[Gossip Analytics] GossipManager already exists in the scene. Using existing instance.");
            }
            else
            {
                // Search for prefab in imported Samples location
                var prefabGuids = AssetDatabase.FindAssets("GossipAnalyticsManager t:Prefab", new[] { "Assets/Samples" });

                if (prefabGuids.Length == 0)
                {
                    EditorUtility.DisplayDialog(
                        "Gossip Analytics — Prefab Not Found",
                        "Please import the Samples first:\n\nPackage Manager \u2192 Gossip Analytics SDK \u2192 Samples \u2192 Import.\n\nThen run Quick Setup again.",
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

            // STEP 4 — Mark scene dirty and show completion dialog
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            EditorUtility.DisplayDialog(
                "Gossip Analytics — Setup Complete ✅",
                "GossipAnalyticsManager is ready in your scene.\n\nThe Inspector will show your GossipAnalyticsSettings. Get your 3 API Keys (Dev, Beta, Production) from the Gossip Analytics dashboard -> Company Overview section, enter them, then press Check Connection to verify.\n\nNext: open Window -> Gossip Analytics -> 2 -- Instrumentation Manager to select which objects to track interactions on.",
                "OK");

            EditorApplication.delayCall += () => {
                Selection.activeObject = settingsAsset;
                EditorGUIUtility.PingObject(settingsAsset);
            };
        }
    }
}
#endif
