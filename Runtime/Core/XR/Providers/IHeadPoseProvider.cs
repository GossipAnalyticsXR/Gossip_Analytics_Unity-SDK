using UnityEngine;

namespace GossipSDK.Core.XR
{
    public interface IHeadPoseProvider
    {
        bool IsAvailable
        {
            get;
        }

        bool TryGetPose(out Vector3 position, out Quaternion rotation);
    }
}
