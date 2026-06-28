using System;
using System.Collections;
using UnityEngine;
using GossipSDK.Core;
using GossipSDK.Tracking.PlatformSpecification;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class MicPermissionComponent : MonoBehaviour
    {
        private bool _emitted;

        private void OnEnable()
        {
            _emitted = false;
            StartCoroutine(EmitOnce());
        }

        private IEnumerator EmitOnce()
        {
            if (_emitted)
                yield break;
            _emitted = true;

            yield return new WaitUntil(() => VRPermissionsHandler.IsReady);

            bool micDenied = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            micDenied = !Permission.HasUserAuthorizedPermission(Permission.Microphone);
#endif

            yield return new WaitUntil(() => (UnityEngine.Object)Gossip.Instance != null);

            var tracker = Gossip.Instance?.MicPermissionTracker;
            if (tracker == null)
                yield break;

            var data = new MicPermissionTracker.EntityData
            {
                PlayerID = Gossip.Instance?.PlayerID,
                SessionID = Gossip.Instance?.SessionID,
                MicDenied = micDenied,
                TimestampUtc = DateTime.UtcNow.ToString("o")
            };
            tracker.CapSession(data);
        }
    }
}
