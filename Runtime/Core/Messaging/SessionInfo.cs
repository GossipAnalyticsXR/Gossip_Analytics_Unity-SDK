using GossipSDK.Core.Session;
using UnityEngine;

namespace GossipSDK.Core.Messaging
{
    public class SessionInfo : ISessionInfo
    {
        [field: SerializeField]
        public string PlayerID { get; set; }
        
        [field: SerializeField]
        public string SessionID { get; set; }
        
    }
}