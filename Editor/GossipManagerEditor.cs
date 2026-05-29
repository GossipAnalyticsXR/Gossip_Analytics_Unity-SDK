#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using GossipSDK;
using GossipSDK.Core.Configuration;

namespace GossipSDK.Editor
{
    [CustomEditor(typeof(GossipManager))]
    public class GossipManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var version = GetSDKVersion();
            var versionStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }
            };
            EditorGUILayout.LabelField("Gossip Analytics SDK v" + version, versionStyle);
            EditorGUILayout.Space(4);

            base.OnInspectorGUI();

            var manager = (GossipManager)target;
            var serializedSettings = serializedObject.FindProperty("settings");

            if (serializedSettings != null && serializedSettings.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "⚠️ Settings is not assigned. Drag your GossipAnalyticsSettings asset into the Settings field before pressing Play.",
                    MessageType.Error
                );

                EditorGUILayout.Space(4);
                if (GUILayout.Button("Auto-Find Settings"))
                {
                    var guids = UnityEditor.AssetDatabase.FindAssets("t:GossipSettings");
                    if (guids.Length == 1)
                    {
                        var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                        var found = UnityEditor.AssetDatabase.LoadAssetAtPath<GossipSettings>(path);
                        serializedSettings.objectReferenceValue = found;
                        serializedObject.ApplyModifiedProperties();
                        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
                        UnityEditor.EditorUtility.DisplayDialog(
                            "Gossip Analytics",
                            "Settings found and assigned! Remember to save your scene (Ctrl+S).",
                            "OK");
                    }
                    else if (guids.Length > 1)
                    {
                        UnityEditor.EditorUtility.DisplayDialog(
                            "Gossip Analytics",
                            guids.Length + " GossipSettings assets found. Please assign one manually in the Inspector.",
                            "OK");
                    }
                    else
                    {
                        bool create = UnityEditor.EditorUtility.DisplayDialog(
                            "Gossip Analytics",
                            "No GossipSettings asset found. Create one now at Assets/GossipAnalyticsSettings.asset?",
                            "Create", "Cancel");
                        if (create)
                        {
                            var newSettings = UnityEngine.ScriptableObject.CreateInstance<GossipSettings>();
                            UnityEditor.AssetDatabase.CreateAsset(newSettings, "Assets/GossipAnalyticsSettings.asset");
                            UnityEditor.AssetDatabase.SaveAssets();
                            serializedSettings.objectReferenceValue = newSettings;
                            serializedObject.ApplyModifiedProperties();
                            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
                        }
                    }
                }
            }
        }

        private static string GetSDKVersion()
        {
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(GossipManagerEditor).Assembly);
            return info != null ? info.version : "Unknown";
        }
    }

    public class GossipManagerBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            int managerCount = 0;
            GossipManager foundManager = null;
            
            var buildScenes = UnityEditor.EditorBuildSettings.scenes;
            var activeScenePath = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;
            
            foreach (var buildScene in buildScenes)
            {
                if (!buildScene.enabled) continue;
                
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    buildScene.path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
                
                var managers = UnityEngine.Object.FindObjectsByType<GossipManager>(
                    UnityEngine.FindObjectsSortMode.None);
                
                if (managers.Length > 0)
                {
                    managerCount++;
                    foundManager = managers[0];
                }
                
                if (scene.path != activeScenePath)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
                }
            }
            
            if (managerCount == 0)
            {
                UnityEngine.Debug.LogError(
                    "[Gossip Analytics] Build aborted: GossipManager was not found in any enabled build scene.");
                throw new BuildFailedException(
                    "Gossip Analytics: GossipAnalyticsManager prefab was not found in any build scene. Add it to your first/main scene before building.");
            }
            
            if (managerCount > 1)
            {
                UnityEngine.Debug.LogWarning(
                    "[Gossip Analytics] GossipAnalyticsManager was found in multiple scenes. It uses DontDestroyOnLoad — add it to the first scene only.");
            }
            
            if (foundManager != null)
            {
                var settingsField = typeof(GossipManager).GetField(
                    "settings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var settingsValue = settingsField?.GetValue(foundManager);
                if (settingsValue == null)
                {
                    UnityEngine.Debug.LogError(
                        "[GossipManager] Build aborted: GossipManager in scene '" +
                        foundManager.gameObject.scene.name + "' has no GossipSettings assigned. " +
                        "Assign the GossipAnalyticsSettings asset before building.");
                    throw new BuildFailedException(
                        "[GossipManager] " + foundManager.gameObject.scene.name +
                        ": GossipManager has no Settings assigned. Assign before building.");
                }
            }
        }
        }
    }
#endif
