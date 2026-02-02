using UnityEngine;

namespace GossipSDK.XR
{
    public interface IXRSessionProvider
    {
        XRSessionState State
        {
            get;
        }
    }
}
