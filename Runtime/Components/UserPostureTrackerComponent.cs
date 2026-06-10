using System;
using UnityEngine;
using GossipSDK.Core;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class UserPostureComponent : MonoBehaviour
    {
        [Header("Sampling")]
        public float sampleInterval = 0.5f;
        public bool autoReportOnStart = true;

        [Header("Head / thresholds")]
        [Tooltip("Optional head transform (VR head). If null, uses this.transform")]
        public Transform headTransform;

        [Tooltip("If head Y relative to origin <= sitThreshold => Sitting")]
        public float sitThreshold = 0.9f;

        [Tooltip("If head Y relative to origin <= crouchThreshold => Crouching (should be > sitThreshold)")]
        public float crouchThreshold = 1.2f;

        //private bool registerHeatmapHit = false;
        private bool enableLocalDebug = false;

        float timer = 0f;
        string lastPosture = "Unknown";

        void Start()
        {
            timer = 0f;
            if (autoReportOnStart)
                SampleAndSend();
        }

        void Update()
        {
            timer += Time.deltaTime;
            if (timer >= sampleInterval)
            {
                timer = 0f;
                SampleAndSend();
            }
        }

        public void PushPostureState(string posture)
        {
            if (string.IsNullOrWhiteSpace(posture)) return;
            lastPosture = posture;
            TrySend(posture, GetHeadPosition());
        }

        void SampleAndSend()
        {
            Vector3 headPos = GetHeadPosition();
            string posture = InferPostureFromHeadY(headPos.y);
            lastPosture = posture;

            TrySend(posture, headPos);
        }

        Vector3 GetHeadPosition()
        {
            if (headTransform != null) return headTransform.position;
            return transform.position;
        }

        string InferPostureFromHeadY(float headWorldY)
        {
            if (headWorldY <= sitThreshold) return "Sitting";
            if (headWorldY <= crouchThreshold) return "Crouching";
            return "Standing";
        }

        void TrySend(string postureState, Vector3 headPos)
        {

            try
            {
                var gossip = GossipSDK.Core.Gossip.Instance;
                if (gossip == null)
                {
                    if (enableLocalDebug) Debug.LogWarning("[UserPostureComponent] Gossip.Instance is null.");
                    return;
                }

                var prop = gossip.GetType().GetProperty("UserPostureTracker");
                var trackerObj = prop?.GetValue(gossip);
                if (trackerObj != null)
                {
                    var cap = trackerObj.GetType().GetMethod("CapSession");
                    if (cap != null)
                    {
                        try
                        {
                            var paramType = cap.GetParameters()[0].ParameterType;
                            var entity = Activator.CreateInstance(paramType);

                            void TrySet(string name, object val)
                            {
                                var p = paramType.GetProperty(name);
                                if (p != null && p.CanWrite) { p.SetValue(entity, val); return; }
                                var f = paramType.GetField(name);
                                if (f != null) f.SetValue(entity, val);
                            }

                            TrySet("PostureState", postureState);
                            TrySet("HeadX", headPos.x);
                            TrySet("HeadY", headPos.y);
                            TrySet("HeadZ", headPos.z);
                            Transform _h = headTransform != null ? headTransform : transform;
                            Vector3 _fwd = _h.forward;
                            TrySet("HeadPitch", Mathf.Asin(Mathf.Clamp(_fwd.y, -1f, 1f)) * Mathf.Rad2Deg);
                            TrySet("HeadYaw",   _h.eulerAngles.y);
                            TrySet("TimestampUtc", DateTime.UtcNow.ToString("o"));
                            TrySet("SceneName", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                            TrySet("PlayerId", gossip.PlayerID ?? "");
                            TrySet("SessionId", gossip.SessionID ?? "");

                            cap.Invoke(trackerObj, new object[] { entity });

                            if ((gossip.Settings?.EnableDebug == true) || enableLocalDebug)
                                Debug.Log($"[UserPostureComponent] Sent posture='{postureState}' pos=({headPos.x:F2},{headPos.y:F2},{headPos.z:F2})");
                            return;
                        }
                        catch (Exception ex)
                        {
                            if (enableLocalDebug) Debug.LogWarning($"[UserPostureComponent] CapSession entity send failed: {ex.Message}");
                        }
                    }

                    var capStr = trackerObj.GetType().GetMethod("CapturePosture") ?? trackerObj.GetType().GetMethod("Capture");
                    if (capStr != null)
                    {
                        try
                        {
                            capStr.Invoke(trackerObj, new object[] { postureState });
                            if ((gossip.Settings?.EnableDebug == true) || enableLocalDebug)
                                Debug.Log($"[UserPostureComponent] Invoked CapturePosture('{postureState}')");
                            return;
                        }
                        catch { }
                    }
                }

                var gCapProp = gossip.GetType().GetProperty("UserPostureTracker");
                var gTracker = gCapProp?.GetValue(gossip);
                var gCap = gTracker?.GetType().GetMethod("CapSession");
                if (gCap != null)
                {
                    try
                    {
                        var paramType = gCap.GetParameters()[0].ParameterType;
                        var inst = Activator.CreateInstance(paramType);
                        var trySet = new Action<string, object>((n, v) =>
                        {
                            var p = paramType.GetProperty(n);
                            if (p != null && p.CanWrite) p.SetValue(inst, v);
                        });
                        trySet("PostureState", postureState);
                        trySet("HeadX", headPos.x);
                        trySet("HeadY", headPos.y);
                        trySet("HeadZ", headPos.z);
                        Transform _h = headTransform != null ? headTransform : transform;
                        Vector3 _fwd = _h.forward;
                        trySet("HeadPitch", Mathf.Asin(Mathf.Clamp(_fwd.y, -1f, 1f)) * Mathf.Rad2Deg);
                        trySet("HeadYaw",   _h.eulerAngles.y);
                        trySet("TimestampUtc", DateTime.UtcNow.ToString("o"));
                        gCap.Invoke(gTracker, new object[] { inst });
                        if ((gossip.Settings?.EnableDebug == true) || enableLocalDebug)
                            Debug.Log($"[UserPostureComponent] (fallback) Sent posture '{postureState}'");
                    }
                    catch (Exception ex)
                    {
                        if (enableLocalDebug) Debug.LogWarning($"[UserPostureComponent] Fallback send failed: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                if (enableLocalDebug) Debug.LogWarning($"[UserPostureComponent] TrySend failed: {ex.Message}");
            }
        }
    }
}
