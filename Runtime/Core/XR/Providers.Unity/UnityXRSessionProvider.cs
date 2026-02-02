using UnityEngine.XR;

namespace GossipSDK.XR
{
    public class UnityXRSessionProvider : IXRSessionProvider
    {
        public XRSessionState State
        {
            get
            {
                if (!XRSettings.enabled)
                    return XRSessionState.Unknown;

                return XRSettings.isDeviceActive
                    ? XRSessionState.Running
                    : XRSessionState.Ready;
            }
        }
    }
}
