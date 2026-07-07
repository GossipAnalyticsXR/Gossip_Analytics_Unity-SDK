using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using GossipSDK.Core;
using GossipSDK.XR;
using GossipSDK.Tracking;
using GossipSDK.Tracking.PlatformSpecification;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class BoundaryPressureComponent : MonoBehaviour
    {
        private const float SampleInterval = 0.2f;
        private const float NearEdgeThreshold = 0.25f;
        private const float PeriodicEmitInterval = 60f;

        private bool _hadPressure;
        private bool _summaryEmitted;
        private Coroutine _sampleCoroutine;
        private Coroutine _periodicCoroutine;

        private Vector2[] _poly;

        private void OnEnable()
        {
            _hadPressure = false;
            _summaryEmitted = false;
            _poly = null;
            _sampleCoroutine = StartCoroutine(SampleLoop());
            _periodicCoroutine = StartCoroutine(PeriodicEmitLoop());
        }

        private void OnDisable()
        {
            if (_sampleCoroutine != null)
            {
                StopCoroutine(_sampleCoroutine);
                _sampleCoroutine = null;
            }
            if (_periodicCoroutine != null)
            {
                StopCoroutine(_periodicCoroutine);
                _periodicCoroutine = null;
            }
            StartCoroutine(EmitSummary());
        }

        private void OnApplicationQuit()
        {
            StartCoroutine(EmitSummary());
        }

        private IEnumerator PeriodicEmitLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(PeriodicEmitInterval);
                yield return EmitSummary();
            }
        }

        private IEnumerator SampleLoop()
        {
            var wait = new WaitForSeconds(SampleInterval);
            while (true)
            {
                yield return wait;
                SampleOnce();
            }
        }

        private void SampleOnce()
        {
            bool previousHadPressure = _hadPressure;

#if UNITY_ANDROID && !UNITY_EDITOR
            if (OVRManager.boundary != null && OVRManager.boundary.GetConfigured())
            {
                EvaluateBoundaryNode(OVRBoundary.Node.Head);

                var controllers = XRBootstrap.HandControllers;
                if (controllers != null && controllers.IsSupported)
                {
                    if (controllers.TryGetLeftPose(out _, out _))
                    {
                        EvaluateBoundaryNode(OVRBoundary.Node.HandLeft);
                    }
                    if (controllers.TryGetRightPose(out _, out _))
                    {
                        EvaluateBoundaryNode(OVRBoundary.Node.HandRight);
                    }
                }
            }
            else
            {
                SampleOncePolygonFallback();
            }
#else
            SampleOncePolygonFallback();
#endif

            if (_hadPressure != previousHadPressure)
            {
                Debug.Log($"[BoundaryPressure] state changed -> hadPressure={_hadPressure}");
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void EvaluateBoundaryNode(OVRBoundary.Node node)
        {
            var r = OVRManager.boundary.TestNode(node, OVRBoundary.BoundaryType.PlayArea);
            if (r.IsTriggering || r.ClosestDistance <= NearEdgeThreshold)
            {
                _hadPressure = true;
            }
        }
#endif

        private void SampleOncePolygonFallback()
        {
            if (_poly == null)
            {
                _poly = TryGetBoundaryPolygon();
            }
            if (_poly == null || _poly.Length < 3)
                return;

            var headPose = XRBootstrap.HeadPose;
            if (headPose != null && headPose.IsAvailable)
            {
                if (headPose.TryGetPose(out var hPos, out _))
                {
                    EvaluatePoint(new Vector2(hPos.x, hPos.z));
                }
            }

            var controllers = XRBootstrap.HandControllers;
            if (controllers != null && controllers.IsSupported)
            {
                if (controllers.TryGetLeftPose(out var lPos, out _))
                {
                    EvaluatePoint(new Vector2(lPos.x, lPos.z));
                }
                if (controllers.TryGetRightPose(out var rPos, out _))
                {
                    EvaluatePoint(new Vector2(rPos.x, rPos.z));
                }
            }
        }

        private void EvaluatePoint(Vector2 p)
        {
            if (_hadPressure)
                return;
            bool inside = BoundaryPressureHelper.Contains(_poly, p);
            if (!inside)
            {
                _hadPressure = true;
                return;
            }
            float d = BoundaryPressureHelper.DistanceToNearestBoundary(_poly, p);
            if (d <= NearEdgeThreshold)
                _hadPressure = true;
        }

        private IEnumerator EmitSummary()
        {
            if (_summaryEmitted)
                yield break;
            _summaryEmitted = true;
            yield return new WaitUntil(() => (UnityEngine.Object)Gossip.Instance != null);
            var tracker = Gossip.Instance?.BoundaryPressureSummaryTracker;
            if (tracker == null)
                yield break;
            var data = new BoundaryPressureSummaryTracker.EntityData
            {
                PlayerID = Gossip.Instance?.PlayerID,
                SessionID = Gossip.Instance?.SessionID,
                HadBoundaryPressure = _hadPressure,
                SceneId = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                TimestampUtc = DateTime.UtcNow.ToString("o")
            };
#if UNITY_ANDROID && !UNITY_EDITOR
            bool ovrPresent = OVRManager.boundary != null;
            bool ovrConfigured = ovrPresent && OVRManager.boundary.GetConfigured();
            Debug.Log($"[BoundaryPressure] ovr={ovrPresent} configured={ovrConfigured} hadPressure={_hadPressure} polyNull={_poly == null}");
#else
            Debug.Log($"[BoundaryPressure] ovr=false configured=false hadPressure={_hadPressure} polyNull={_poly == null}");
#endif
            tracker.CapSession(data);
        }

        private static Vector2[] TryGetBoundaryPolygon()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                var boundary = OVRManager.boundary;
                if (boundary != null && boundary.GetConfigured())
                {
                    Vector3[] pts = boundary.GetGeometry(OVRBoundary.BoundaryType.PlayArea);
                    if (pts != null && pts.Length >= 3)
                    {
                        var poly = new Vector2[pts.Length];
                        for (int i = 0; i < pts.Length; i++)
                            poly[i] = new Vector2(pts[i].x, pts[i].z);
                        return poly;
                    }
                }
            }
            catch { }
#endif
            try
            {
                var subsystems = new List<XRInputSubsystem>();
                SubsystemManager.GetInstances(subsystems);
                for (int i = 0; i < subsystems.Count; i++)
                {
                    var pts = new List<Vector3>();
                    if (subsystems[i].TryGetBoundaryPoints(pts) && pts.Count >= 3)
                    {
                        var poly = new Vector2[pts.Count];
                        for (int j = 0; j < pts.Count; j++)
                            poly[j] = new Vector2(pts[j].x, pts[j].z);
                        return poly;
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
