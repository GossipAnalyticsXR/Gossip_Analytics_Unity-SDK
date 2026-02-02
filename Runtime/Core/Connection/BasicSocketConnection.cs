using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;

namespace GossipSDK.Core.Connection
{
    public class BasicSocketConnection<T1> : GenericSocketConnection<T1, Message<T1>> where T1 : IData
    { }
}