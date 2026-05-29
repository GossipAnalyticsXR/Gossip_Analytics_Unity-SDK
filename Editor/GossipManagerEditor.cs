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
