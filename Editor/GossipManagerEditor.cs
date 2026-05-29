#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using GossipSDK;

namespace GossipSDK.Editor
{
    [CustomEditor(typeof(GossipManager))]
    public class GossipManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
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
    }

    public class GossipManagerBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var managers = Object.FindObjectsByType<GossipManager>(FindObjectsSortMode.None);
            foreach (var manager in managers)
            {
                var serializedObj = new UnityEditor.SerializedObject(manager);
                var settingsProp = serializedObj.FindProperty("settings");
                if (settingsProp != null && settingsProp.objectReferenceValue == null)
                {
                    Debug.LogError("[GossipManager] Build aborted: GossipManager in scene '" +
                        manager.gameObject.scene.name + "' has no GossipSettings assigned. " +
                        "Assign the GossipAnalyticsSettings asset before building.");
                    throw new BuildFailedException(
                        "[GossipManager] GossipSettings is not assigned on GossipManager in scene '" +
                        manager.gameObject.scene.name + "'. Assign it before building."
                    );
                }
            }
        }
    }
}
#endif
