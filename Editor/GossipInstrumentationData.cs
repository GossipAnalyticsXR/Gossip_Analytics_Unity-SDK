using System.Collections.Generic;
using UnityEngine;

namespace GossipSDK.Editor
{
    [CreateAssetMenu(menuName = "Gossip Analytics/Instrumentation Map")]
    public class GossipInstrumentationData : ScriptableObject
    {
        // Key: scene name, Value: list of full GameObject hierarchy paths
        public List<SceneInstrumentationEntry> scenes = new List<SceneInstrumentationEntry>();
    }

    [System.Serializable]
    public class SceneInstrumentationEntry
    {
        public string sceneName;
        public List<string> instrumentedPaths = new List<string>(); // e.g. "Hospital/Heart"
        public List<string> newObjectPaths    = new List<string>(); // detected but not yet reviewed
    }
}
