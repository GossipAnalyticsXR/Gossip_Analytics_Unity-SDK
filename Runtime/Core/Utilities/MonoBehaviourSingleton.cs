using UnityEngine;

namespace GossipSDK.Core.Utilities
{
    public class MonoBehaviourSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        private static readonly object _lock = new object();
        private static bool _shuttingDown = false;

        public static T Instance
        {
            get
            {
                if (_shuttingDown) return null;

                if (_instance != null) return _instance;

                lock (_lock)
                {
                    if (_instance != null) return _instance;

                    _instance = FindFirstObjectByType<T>();
                    if (_instance != null) return _instance;

                    if (!Application.isPlaying)
                    {
                        return null;
                    }

                    var go = new GameObject(typeof(T).Name);
                    _instance = go.AddComponent<T>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        protected virtual void OnApplicationQuit()
        {
            _shuttingDown = true;
        }

        protected virtual void OnDestroy()
        {
            _shuttingDown = true;
        }
    }
}

