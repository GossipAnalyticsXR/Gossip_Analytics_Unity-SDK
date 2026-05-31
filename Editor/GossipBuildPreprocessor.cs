#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace GossipSDK.Editor
{
    public class GossipBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var guids = AssetDatabase.FindAssets("t:GossipSettings");
            if (guids.Length == 0) return;

            var path     = AssetDatabase.GUIDToAssetPath(guids[0]);
            var settings = AssetDatabase.LoadAssetAtPath<GossipSettings>(path);
            if (settings == null) return;

            if (settings.EnableDebug)
            {
                settings.EnableDebug = false;
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                UnityEngine.Debug.Log("[Gossip Analytics] EnableDebug auto-disabled for build.");
            }
        }
    }
}
#endif
