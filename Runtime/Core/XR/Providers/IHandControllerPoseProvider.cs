using UnityEngine;

namespace GossipSDK.Core.XR
{
    public interface IHandControllerPoseProvider
    {
        bool IsSupported
        {
            get;
        }
        bool TryGetLeftPose(out Vector3 pos, out Quaternion rot);
        bool TryGetRightPose(out Vector3 pos, out Quaternion rot);
    }
}
