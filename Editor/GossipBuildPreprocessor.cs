#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using GossipSDK.Core.Configuration;

namespace GossipSDK.Editor
{
    public class GossipBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            bool isDevelopmentBuild = (report.summary.options & BuildOptions.Development) != 0;
            if (isDevelopmentBuild) return;

            var guids = AssetDatabase.FindAssets("t:GossipSettings");
            if (guids.Length == 0) return;

            var path     = AssetDatabase.GUIDToAssetPath(guids[0]);
            var settings = AssetDatabase.LoadAssetAtPath<GossipSettings>(path);
            if (settings == null) return;

            if (settings.EnableDebug)
            {
                var so = new SerializedObject(settings);
                so.Update();
                var prop = so.FindProperty("enableDebug");
                if (prop != null)
                {
                    prop.boolValue = false;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                    UnityEngine.Debug.Log("[Gossip Analytics] EnableDebug auto-disabled for build.");
                }
            }
        }
    }
}
#endif
