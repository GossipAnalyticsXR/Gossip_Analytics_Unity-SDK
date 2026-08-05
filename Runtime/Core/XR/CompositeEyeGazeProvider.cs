using UnityEngine;
using GossipSDK.Core.XR;

namespace GossipSDK.XR
{
    // Tries each eye-gaze provider in order and uses the first whose eye tracking is
    // available. Returns false when none produce eye gaze, so the tracking component
    // falls back to head pose.
    public sealed class CompositeEyeGazeProvider : IEyeGazeProvider
    {
        private readonly IEyeGazeProvider[] _providers;
        private string _lastSource = "head";

        public CompositeEyeGazeProvider(params IEyeGazeProvider[] providers)
        {
            _providers = providers;
        }

        public bool IsAvailable
        {
            get
            {
                foreach (var p in _providers)
                    if (p != null && p.IsAvailable) return true;
                return false;
            }
        }

        public string TrackingSource => _lastSource;

        public bool TryGetEyeGaze(out Ray gaze)
        {
            foreach (var p in _providers)
            {
                if (p != null && p.IsAvailable && p.TryGetEyeGaze(out gaze))
                {
                    _lastSource = p.TrackingSource;
                    return true;
                }
            }

            gaze = default;
            return false;
        }
    }
}
