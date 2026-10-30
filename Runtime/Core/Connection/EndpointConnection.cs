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

                // El id de sesion se lee AQUI, en el hilo principal. Dentro del hilo de
                // fondo Gossip.CurrentSessionId es un global que SessionManager puede
                // haber cambiado ya: al arrancar cierra la sesion huerfana cambiandolo
                // dos veces, y el sobre saldria con la sesion SIGUIENTE. Medido el
                // 18-oct-2026 en Hospital: cinco sesiones empiezan con una a tres
                // muestras fechadas tres segundos despues del ultimo punto de la sesion
                // anterior, y su duracion pasa de minutos a horas.
                //
                // Es la misma regla que SceneIdentity ya deja escrita para el GUID, y la
                // que GenericSocketConnection.CapSession ya aplica al bufer de tracking.
                // Los cinco envios de este fichero entran en un hilo de fondo, asi que
                // los cinco capturan el id antes de entrar.
                //
                // El playerID no se captura: no cambia dentro de una ejecucion, porque
                // SessionManager llama a SetCurrentIds siempre con el mismo.
                string sessionIdActual = gossip.CurrentSessionId;
                string json = await UniTask.RunOnThreadPool(() =>
                {
                    string imageBase64 = Convert.ToBase64String(png);
                    var envelope = new
                    {
                        EventType = "HeatmapSceneImageTracker",
                        PlayerID = gossip.CurrentPlayerId,
                        SessionID = sessionIdActual,
                        sceneUser = spec.SceneName,
                        TimestampUtc = DateTime.UtcNow.ToString("o"),
                        SceneName = spec.SceneName,
                        SceneGuid = spec.SceneGuid,
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
public async UniTask UploadHeatmapPanorama(HeatmapPanoramaSpec spec, byte[] jpg, Action<bool> callback)
{
try
{
if (spec == null || jpg == null || jpg.Length == 0) { callback?.Invoke(false); return; }
var gossip = Gossip.Instance;
if (!ValidateSession(gossip)) { callback?.Invoke(false); return; }

// El id se lee en el hilo principal, no dentro del pool. Ver UploadHeatmapScene.
string sessionIdActual = gossip.CurrentSessionId;
string json = await UniTask.RunOnThreadPool(() =>
{
string imageBase64 = Convert.ToBase64String(jpg);
var envelope = new
{
EventType = "HeatmapPanoramaTracker",
PlayerID = gossip.CurrentPlayerId,
SessionID = sessionIdActual,
TimestampUtc = DateTime.UtcNow.ToString("o"),
SceneName = spec.SceneName,
SceneGuid = spec.SceneGuid,
SceneVersion = spec.Version,
Camera = new { Position = new { x = spec.PositionX, y = spec.PositionY, z = spec.PositionZ } },
YawOffsetDeg = spec.YawOffsetDeg,
Image = new { Format = "jpg", DataBase64 = imageBase64, Width = spec.ImageWidth, Height = spec.ImageHeight },
// La procedencia y la ventana real de la captura. El spec las trae desde la 2.0.42
// -HeatmapDwellPanoramaCapture sella "dwell" y sus seis caras a faceInterval,
// HeatmapPanoramaAutoCapture sella "auto" y las seis en el mismo fotograma- pero
// este sobre se escribe campo a campo y no las enumeraba, asi que se tiraban aqui.
// Medido el 24-oct-2026: 174/174 ficheros .cs leidos, 0 fallos, CaptureSource solo
// en el spec y los dos captadores. Y en el dato: la unica panoramica capturada
// desde que existe la 2.0.42 -6adbf4d7, del 23-oct, con sdkVersion 2.0.43 en su
// sesion- llego con los tres campos nulos. El DTO del Backend-SDK los acepta
// opcionales desde el PR #153 y el procesador los persiste: faltaba este lado.
CaptureSource = spec.CaptureSource,
CaptureStartUtc = spec.CaptureStartUtc,
CaptureEndUtc = spec.CaptureEndUtc
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
                var sceneGuid = GossipSDK.Core.Configuration.SceneIdentity.CurrentSceneGuid(gossip.Settings);
                var objId = interactedObject.GetInstanceID().ToString();
                var objName = interactedObject.name;
                var appVersion = Application.version;

                // El id se lee en el hilo principal, no dentro del pool. Ver UploadHeatmapScene.
                string sessionIdActual = gossip.CurrentSessionId;
                string json = await UniTask.RunOnThreadPool(() =>
                {
                    string base64 = Convert.ToBase64String(png);
                    var envelope = new
                    {
                        EventType = "InteractionImageTracker",
                        PlayerID = gossip.CurrentPlayerId,
                        SessionID = sessionIdActual,
                        TimestampUtc = DateTime.UtcNow.ToString("o"),
                        SceneName = sceneName,
                        SceneGuid = sceneGuid,
                        SceneVersion = appVersion,
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
                var sceneGuid = GossipSDK.Core.Configuration.SceneIdentity.CurrentSceneGuid(gossip.Settings);

                string hitName = hit.collider != null ? hit.collider.gameObject.name : null;
                string hitTag = hit.collider != null ? hit.collider.gameObject.tag : null;
                Vector3 hitPoint = hit.point;
                var appVersion = Application.version;

                // El id se lee en el hilo principal, no dentro del pool. Ver UploadHeatmapScene.
                string sessionIdActual = gossip.CurrentSessionId;
                string json = await UniTask.RunOnThreadPool(() =>
                {
                    string base64 = Convert.ToBase64String(png);
                    string depthBase64 = depthPng != null ? Convert.ToBase64String(depthPng) : null;
                    var envelope = new
                    {
                        EventType = "EyeGazeImageTracker",
                        PlayerID = gossip.CurrentPlayerId,
                        SessionID = sessionIdActual,
                        TimestampUtc = DateTime.UtcNow.ToString("o"),
                        SceneName = sceneName,
                        SceneGuid = sceneGuid,
                        SceneVersion = appVersion,
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

                // El id se lee en el hilo principal, no dentro del pool. Ver UploadHeatmapScene.
                string sessionIdActual = gossip.CurrentSessionId;
                string json = await UniTask.RunOnThreadPool(() =>
                {
                    return JsonConvert.SerializeObject(new
                    {
                        EventType = "AudioReactionTracker",
                        PlayerID = gossip.CurrentPlayerId,
                        SessionID = sessionIdActual,
                        SceneName = data.SceneName,
                        SceneGuid = data.SceneGuid,
                        TimestampUtc = data.TimestampUtc,

                        // Que version del SDK produjo esta reaccion. Sin esto no se
                        // puede cortar por epoca: medido el 06/09/2026, las 298
                        // reacciones de la app salian sin version, y los cortes del
                        // disparador y el criterio para retirar VoiceQuality se quedaban
                        // en «por fecha», que es mas fragil.
                        SdkVersion = Constants.SdkVersion,

                        Metrics = new
                        {
                            data.EventSeverity,
                            data.VoiceChange,
                            data.VoiceBurstiness,
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
