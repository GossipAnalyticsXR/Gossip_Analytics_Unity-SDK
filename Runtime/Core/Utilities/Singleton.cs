using System;

namespace GossipSDK.Core.Utilities
{
    public abstract class Singleton<T> where T : class, new()
    {
        private static readonly Lazy<T> _instance = new Lazy<T>(() => new T());

        public static T Instance => _instance.Value;

        protected Singleton()
        {
            if (_instance.IsValueCreated)
            {
                throw new InvalidOperationException("Instance already created.");
            }
        }
    }
}