using UnityEngine;

namespace GossipSDK.Components
{
    public class GossipBasicComponent : MonoBehaviour
    {
        [SerializeField, HideInInspector]
        private GossipManager _gossipManager;

        protected GossipManager GossipManager
        {
            get
            {
                if (_gossipManager == null)
                    _gossipManager = FindFirstObjectByType<GossipManager>();
                return _gossipManager;
            }
        }

        protected virtual void OnValidate()
        {
            // No side-effects here. Just check.
        }

        protected virtual void OnEnable()
        {
            var gm = GossipManager;
            if (gm == null)
            {
                Debug.LogWarning($"GossipBasicComponent: No GossipManager found in scene for {name}. Please add one to enable telemetry.", this);
                return;
            }
            gm.RegisterComponent(this);
        }

        protected virtual void OnDisable()
        {
            _gossipManager?.UnregisterComponent(this);
        }
    }
}
