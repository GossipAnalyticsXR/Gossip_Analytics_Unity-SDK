using UnityEngine;

namespace GossipSDK.Core.XR
{
    public interface IEyeGazeProvider
    {
        bool IsAvailable
        {
            get;
        }
        bool TryGetEyeGaze(out Ray gaze);
        string TrackingSource
        {
            get;
        }
    }
}
