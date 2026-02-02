using System;
using System.Collections.Generic;
using UnityEngine;
using GossipSDK.Core.Data;
using GossipSDK.Core.Session;

namespace GossipSDK.Core.Messaging
{
    [Serializable]
    public class Message<T> : SessionInfo where T : IData
    {
        [field: SerializeField]
        public string EventType { get; set; }

        [field: SerializeField]
        public string SceneUser { get; set; }

        [field: SerializeField]
        public string TimeMovement { get; set; }

        [field: SerializeField]
        public List<T> Messages { get; set; } = new List<T>();
    }
}