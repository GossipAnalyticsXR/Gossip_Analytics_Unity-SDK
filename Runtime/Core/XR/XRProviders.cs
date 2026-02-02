using GossipSDK.Core.XR;
using GossipSDK.XR;

namespace GossipSDK.Core.XR
{
    public static class XRProviders
    {
        public static IEyeGazeProvider EyeGaze
        {
            get; internal set;
        }
        public static IHeadPoseProvider HeadPose
        {
            get; internal set;
        }
        public static IXRSessionProvider Session
        {
            get; internal set;
        }

        public static bool IsRunning =>
            Session != null && Session.State == XRSessionState.Running;
    }
}
