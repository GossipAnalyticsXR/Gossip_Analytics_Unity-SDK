#if META_CORE
using UnityEditor;
using UnityEngine;

namespace GossipSDK.Editor
{
    // Ensures Meta eye tracking is enabled at the project level so the SDK's Meta eye-gaze
    // provider can produce real eye data. Applies automatically to any project that imports
    // this SDK -- no manual Unity setup needed. Uses "Supported" so headsets without eye
    // tracking still run (the SDK falls back to head pose there).
    [InitializeOnLoad]
    internal static class GossipMetaEyeTrackingSetup
    {
        static GossipMetaEyeTrackingSetup()
        {
            EditorApplication.delayCall += EnsureEyeTrackingSupported;
        }

        private static void EnsureEyeTrackingSupported()
        {
            var config = OVRProjectConfig.CachedProjectConfig;
            if (config == null) return;
            if (config.eyeTrackingSupport != OVRProjectConfig.FeatureSupport.None) return;

            config.eyeTrackingSupport = OVRProjectConfig.FeatureSupport.Supported;
            OVRProjectConfig.CommitProjectConfig(config);
            Debug.Log("[Gossip] Enabled Meta Eye Tracking Support (required for eye-gaze heatmaps).");
        }
    }
}
#endif
