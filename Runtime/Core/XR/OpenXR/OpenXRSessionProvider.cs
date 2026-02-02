using GossipSDK.Core.XR;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace GossipSDK.XR
{
    public sealed class OpenXRSessionProvider : IXRSessionProvider
    {
        public XRSessionState State
        {
            get
            {
                var xrManager = XRGeneralSettings.Instance?.Manager;
                if (xrManager == null)
                    return XRSessionState.Unknown;

                if (xrManager.activeLoader == null)
                    return XRSessionState.Ready;

                return xrManager.isInitializationComplete
                    ? XRSessionState.Running
                    : XRSessionState.Ready;
            }
        }
    }
}
