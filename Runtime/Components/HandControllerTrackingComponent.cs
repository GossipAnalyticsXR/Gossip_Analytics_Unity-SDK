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
    private Transform _head;
    private Transform Head
    {
        get
        {
            if ((UnityEngine.Object)_head == null && Camera.main != null) _head = Camera.main.transform;
            return _head;
        }
    }

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
            Send("left",  lPos, lRot);
        }

        if (provider.TryGetRightPose(out var rPos, out var rRot))
        {
            Send("right", rPos, rRot);
        }
    }

    private void Send(string hand, Vector3 pos, Quaternion rot)
    {
        float elevation = 0f;
        var head = Head;
        if ((UnityEngine.Object)head != null)
        {
            Vector3 shoulder = head.position - new Vector3(0f, 0.2f, 0f);
            Vector3 arm = pos - shoulder;
            float horiz = new Vector2(arm.x, arm.z).magnitude;
            elevation = Mathf.Atan2(arm.y, horiz) * Mathf.Rad2Deg;
        }
        Gossip.Instance?.HandControllerTracker?.CapSession(
            new HandControllerTracker.EntityData
            {
                Hand = hand, Pitch = rot.eulerAngles.x, Yaw = rot.eulerAngles.y, Roll = rot.eulerAngles.z,
                HandElevation = elevation,
                TimestampUtc = System.DateTime.UtcNow.ToString("o")
            });
    }
}
