using UnityEngine;
using GossipSDK.Core.XR;
using GossipSDK.Tracking;
using GossipSDK.Core;
using GossipSDK.XR;
using GossipSDK.Tracking.GameplayMetrics;

[DisallowMultipleComponent]
public class HandControllerTrackingComponent : MonoBehaviour
{
    [SerializeField] private float sampleInterval = 0.5f;
    private float timer;

    private void Start()
    {
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer < sampleInterval)
            return;
        timer = 0f;

        var provider = XRBootstrap.HandControllers;
        if (provider == null || !provider.IsSupported)
            return;

        if (provider.TryGetLeftPose(out var lPos, out var lRot))
        {
            Send("left", lRot);
        }

        if (provider.TryGetRightPose(out var rPos, out var rRot))
        {
            Send("right", rRot);
        }
    }

    private void Send(string hand, Quaternion rot)
    {
        Gossip.Instance?.HandControllerTracker?.CapSession(
            new HandControllerTracker.EntityData
            {
                Hand = hand,
                Pitch = rot.eulerAngles.x,
                Yaw = rot.eulerAngles.y,
                Roll = rot.eulerAngles.z,
                TimestampUtc = System.DateTime.UtcNow.ToString("o")
            }
        );
    }
}
