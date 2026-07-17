using System;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using GossipSDK.Heatmaps;
using UnityEngine.SceneManagement;
using GossipSDK.Tracking.GameplayMetrics;

namespace GossipSDK.Core.Connection
{
    public class EndpointConnection : IDisposable
    {
        private readonly string apiKeyHeader;
        private readonly string apiKeyValue;

        public EndpointConnection(string apiKeyHeader, string apiKeyValue)
        {
            this.apiKeyHeader = apiKeyHeader;
            this.apiKeyValue = apiKeyValue;
        }

        private string GetUploadUrl()
        {
            var settings = Gossip.Instance.Settings;
            string baseUrl = settings.GetActiveServerUrl().TrimEnd('/');
            string path = settings.IngestPath.StartsWith("/") ? settings.IngestPath : "/" + settings.IngestPath;
            return baseUrl + path;
        }

        public async UniTask UploadHeatmapScene(HeatmapSceneSpec spec, byte[] png, Action<bool> callback)
        {
            try
            {
                if (spec == null || png == null || png.Length == 0)
                {
                    callback?.Invoke(false);
                    return;
                }

                var gossip = Gossip.Instance;
                if (!ValidateSession(gossip)) { callback?.Invoke(false); return; }

                string json = await UniTask.RunOnThreadPool(() =>
                {
                    string imageBase64 = Convert.ToBase64String(png);
                    var envelope = new
                    {
                        EventType = "HeatmapSceneImageTracker",
                        PlayerID = gossip.CurrentPlayerId,
                        SessionID = gossip.CurrentSessionId,
                        sceneUser = spec.SceneName,
                        TimestampUtc = DateTime.UtcNow.ToString("o"),
                        SceneName = spec.SceneName,
                        SceneVersion = spec.Version,
                        Image = new { Format = "png", DataBase64 = imageBase64 },
                        messages = new[] { spec }
                    };
                    return JsonConvert.SerializeObject(envelope);
                });

                bool ok = await PostJsonAsync(GetUploadUrl(), json);
                callback?.Invoke(ok);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                callback?.Invoke(false);
            }
        }

        public async UniTask UploadInteractionImage(GameObject interactedObject, string interactionType, byte[] png, Action<bool> callback)
        {
            try
            {
                var gossip = Gossip.Instance;
                if (png == null || png.Length == 0 || !ValidateSession(gossip)) { callback?.Invoke(false); return; }

                var cam = Camera.main;
                var camPos = cam.transform.position;
                var camRot = cam.transform.rotation.eulerAngles;
                var camFov = cam.fieldOfView;
                var camAspect = cam.aspect;
                var sceneName = SceneManager.GetActiveScene().name;
                var objId = interactedObject.GetInstanceID().ToString();
                var objName = interactedObject.name;

                string json = await UniTask.RunOnThreadPool(() =>
                {
                    string base64 = Convert.ToBase64String(png);
                    var envelope = new
                    {
                        EventType = "InteractionImageTracker",
                        PlayerID = gossip.CurrentPlayerId,
                        SessionID = gossip.CurrentSessionId,
                        TimestampUtc = DateTime.UtcNow.ToString("o"),
                        SceneName = sceneName,
                        Interaction = new { ObjectId = objId, ObjectName = objName, InteractionType = interactionType },
                        Camera = new
                        {
                            Position = new { x = camPos.x, y = camPos.y, z = camPos.z },
                            Rotation = new { x = camRot.x, y = camRot.y, z = camRot.z },
                            Fov = camFov,
                            Aspect = camAspect
                        },
                        Image = new { Format = "png", DataBase64 = base64 }
                    };
                    return JsonConvert.SerializeObject(envelope);
                });

                bool ok = await PostJsonAsync(GetUploadUrl(), json);
                callback?.Invoke(ok);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                callback?.Invoke(false);
            }
        }

public async UniTask UploadEyeGazeImage(Ray gazeRay, RaycastHit hit, float fixationDuration, string trackingSource, byte[] png, Vector3 camPos, Vector3 camEuler, float camFov, float camAspect, byte[] depthPng, int depthWidth, int depthHeight, float depthMaxMeters, Action<bool> callback)       
        {
            try
            {
                var gossip = Gossip.Instance;
                if (png == null || png.Length == 0 || !ValidateSession(gossip)) { callback?.Invoke(false); return; }

                var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

                string hitName = hit.collider != null ? hit.collider.gameObject.name : null;
                string hitTag = hit.collider != null ? hit.collider.gameObject.tag : null;
                Vector3 hitPoint = hit.point;

                string json = await UniTask.RunOnThreadPool(() =>
                {
                    string base64 = Convert.ToBase64String(png);
                    string depthBase64 = depthPng != null ? Convert.ToBase64String(depthPng) : null;
                    var envelope = new
                    {
                        EventType = "EyeGazeImageTracker",
                        PlayerID = gossip.CurrentPlayerId,
                        SessionID = gossip.CurrentSessionId,
                        TimestampUtc = DateTime.UtcNow.ToString("o"),
                        SceneName = sceneName,
                        Gaze = new
                        {
                            Origin = new { x = gazeRay.origin.x, y = gazeRay.origin.y, z = gazeRay.origin.z },
                            Direction = new { x = gazeRay.direction.x, y = gazeRay.direction.y, z = gazeRay.direction.z },
                            FixationDurationSeconds = fixationDuration,
                            TrackingSource = trackingSource
                        },
                        Hit = new
                        {
                            ObjectName = hitName,
                            ObjectTag = hitTag,
                            Point = new { x = hitPoint.x, y = hitPoint.y, z = hitPoint.z }
                        },
                        Camera = new
                        {
                            Position = new { x = camPos.x, y = camPos.y, z = camPos.z },
                            Rotation = new { x = camEuler.x, y = camEuler.y, z = camEuler.z },
                            Fov = camFov,
                            Aspect = camAspect
                        },
                        Image = new { Format = "png", DataBase64 = base64 },
                        Depth = (depthPng != null) ? new { Format = "png", DataBase64 = depthBase64, Width = depthWidth, Height = depthHeight, MaxMeters = depthMaxMeters } : (object)null
                    };
                    return JsonConvert.SerializeObject(envelope);
                });

                bool ok = await PostJsonAsync(GetUploadUrl(), json);
                callback?.Invoke(ok);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                callback?.Invoke(false);
            }
        }

        public async UniTask UploadAudioReaction(
            AudioReactionTracker.EntityData data,
            byte[] audio,
            Action<bool> callback)
        {
            try
            {
                var gossip = Gossip.Instance;
                if (audio == null || audio.Length == 0 || !ValidateSession(gossip))
                {
                    callback?.Invoke(false);
                    return;
                }

                string json = await UniTask.RunOnThreadPool(() =>
                {
                    return JsonConvert.SerializeObject(new
                    {
                        EventType = "AudioReactionTracker",
                        PlayerID = gossip.CurrentPlayerId,
                        SessionID = gossip.CurrentSessionId,
                        SceneName = data.SceneName,
                        TimestampUtc = data.TimestampUtc,

                        Metrics = new
                        {
                            data.EventSeverity,
                            data.VoiceChange,
                            data.VoiceQuality,
                            data.MovementIntensity,
                            data.EmotionalScore,
                            data.TriggerMode
                        },

                        Audio = new
                        {
                            Format = "wav",
                            SampleRate = 16000,
                            Channels = 1,
                            DataBase64 = Convert.ToBase64String(audio)
                        }
                    });
                });

                bool ok = await PostJsonAsync(GetUploadUrl(), json);
                callback?.Invoke(ok);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                callback?.Invoke(false);
            }
        }



        private bool ValidateSession(Gossip gossip)
        {
            if (string.IsNullOrEmpty(gossip.CurrentPlayerId) || string.IsNullOrEmpty(gossip.CurrentSessionId))
            {
                Debug.LogError("[EndpointConnection] Missing PlayerID or SessionID");
                return false;
            }
            return true;
        }

        public async UniTask<bool> PostJsonAsync(string url, string json)
        {
            try
            {
                using (var uwr = new UnityWebRequest(url, "POST"))
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                    uwr.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    uwr.downloadHandler = new DownloadHandlerBuffer();
                    uwr.SetRequestHeader("Content-Type", "application/json");

                    if (!string.IsNullOrEmpty(apiKeyHeader))
                        uwr.SetRequestHeader(apiKeyHeader, apiKeyValue);

                    Gossip.Instance?.NetworkUsageTracker?.RegisterSent(bodyRaw.Length);

                    await uwr.SendWebRequest();

                    if (uwr.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning($"[EndpointConnection] POST failed: {uwr.responseCode} - {uwr.error}");
                        return false;
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }

        public void Dispose() { }
    }
}
