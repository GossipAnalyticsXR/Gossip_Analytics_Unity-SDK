using UnityEngine;
using UnityEngine.SceneManagement;
using GossipSDK.Core;
using GossipSDK.Heatmaps;
using System;
using GossipSDK.Tracking;
using GossipSDK.XR;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class EyeTrackingComponent : MonoBehaviour
    {
        [Header("Fixation")]
        [SerializeField] private float fixationThreshold = 0.25f;
        // Una fijacion es mirar AL MISMO SITIO, no estar sobre el mismo GameObject. Medido el
        // 28-09-2026 sobre 20.454 mensajes: el suelo de Hospital Zone es UN solo objeto de
        // 36 x 41 m, asi que el cronometro no se reiniciaba mientras el rayo siguiera cayendo
        // en el, y salian fijaciones de hasta 51 s. El suelo se llevaba 3.925 s contra 903 s de
        // TODOS los objetos juntos. En SampleScene, con objetos pequenos, la mediana es 0,26 s:
        // misma definicion, resultado opuesto, solo cambia el tamano de la geometria.
        // Se corta por las dos vias, porque fallan en casos distintos: girar la cabeza sobre un
        // objeto grande mueve el ANGULO, y andar mirando al suelo mueve los METROS.
        [SerializeField] private float fixationMaxDriftMeters = 0.5f;
        [SerializeField] private float fixationMaxDriftDegrees = 10f;
        [SerializeField] private float maxDistance = 20f;
        [SerializeField] private LayerMask raycastLayers = ~0;

        [Header("Image Throttling")]
        [SerializeField] private float minDistanceDelta = 0.5f;
        [SerializeField] private float minRotationDelta = 10f;
        [SerializeField] private float minTimeBetweenImages = 5f;
        [SerializeField] private float headFallbackCooldownMultiplier = 1.5f;
        [SerializeField] private float headFallbackThresholdMultiplier = 2.5f;

        [Header("Heatmap")]
        [SerializeField] private Vector2 worldMinXZ = new(-5, -5);
        [SerializeField] private Vector2 worldMaxXZ = new(5, 5);
        [SerializeField] private float cellSizeMeters = 0.5f;
        [SerializeField] private float heatmapFlushInterval = 10f;

        [Header("Auto Bounds")]
        [SerializeField] public bool autoBounds = true;

        public Transform cam;
        private string sceneName;

        private HeatmapManager heatmap;
        private float fixationTimer;
        private Vector3 fixationAnchorPoint;
        private Vector3 fixationAnchorDir;
        private float heatmapTimer;

        private GameObject currentObject;
        private GameObject lastImageObject;
        private RaycastHit pendingHit;
        private Ray pendingGazeRay;
        private string pendingSource;

        private Vector3 lastCamPos;
        private Quaternion lastCamRot;
        private float lastImageTime;

        // Bufer fijo para el raycast de mirada: RaycastNonAlloc no reserva memoria y
        // esto corre en cada Update dentro del visor. 16 impactos a lo largo de 20 m
        // es de sobra; si alguna vez se llenara, se toma el mas cercano de esos 16.
        private readonly RaycastHit[] gazeHits = new RaycastHit[16];

        private const string SOURCE_EYE = "eye";
        private const string SOURCE_HEAD = "head";

        private void Awake()
        {
            sceneName = SceneManager.GetActiveScene().name;

            if (cam != null)
            {
                lastCamPos = cam.position;
                lastCamRot = cam.rotation;
            }

            lastImageTime = -minTimeBetweenImages;
        }

        private void Start()
        {
            if (autoBounds && HeatmapBoundsResolver.ResolveSceneBoundsXZ(
                out Vector2 resolvedMin, out Vector2 resolvedMax))
            {
                worldMinXZ = resolvedMin;
                worldMaxXZ = resolvedMax;
            }

            heatmap = new HeatmapManager(sceneName, worldMinXZ, worldMaxXZ, cellSizeMeters);
        }

        private void Update()
        {
            if (cam == null)
                return;

            heatmapTimer += Time.deltaTime;

            Ray gazeRay;
            string source;

            var eyeProvider = XRBootstrap.EyeGaze;

            if (eyeProvider != null &&
                eyeProvider.IsAvailable &&
                eyeProvider.TryGetEyeGaze(out gazeRay))
            {
                source = eyeProvider.TrackingSource;
            }
            else if (XRBootstrap.HeadPose != null &&
                XRBootstrap.HeadPose.TryGetPose(out Vector3 pos, out Quaternion rot))
            {
                gazeRay = new Ray(pos, rot * Vector3.forward);
                source = SOURCE_HEAD;
            }
            else
            {
                return;
            }

            if (!RaycastIgnoringSelf(gazeRay, out RaycastHit hit))
            {
                TryEmitFixation();
                currentObject = null;
                fixationTimer = 0f;
                return;
            }

            heatmap.RegisterHit(hit.point);

            var golpeaOtro = hit.collider.gameObject != currentObject;
            var seFueDelSitio = !golpeaOtro && currentObject != null &&
                (Vector3.Distance(hit.point, fixationAnchorPoint) > fixationMaxDriftMeters ||
                 Vector3.Angle(gazeRay.direction, fixationAnchorDir) > fixationMaxDriftDegrees);

            if (golpeaOtro || seFueDelSitio)
            {
                TryEmitFixation();
                currentObject = hit.collider.gameObject;
                fixationTimer = 0f;
                fixationAnchorPoint = hit.point;
                fixationAnchorDir = gazeRay.direction;
            }

            fixationTimer += Time.deltaTime;
            pendingHit = hit;
            pendingGazeRay = gazeRay;
            pendingSource = source;

            if (heatmapTimer >= heatmapFlushInterval)
            {
                FlushHeatmap();
                heatmapTimer = 0f;
            }
        }

        private void TryEmitFixation()
        {
            if (currentObject != null && fixationTimer >= fixationThreshold)
                ProcessFixation(pendingHit, pendingGazeRay, pendingSource);
        }

        private void ProcessFixation(RaycastHit hit, Ray gazeRay, string source)
        {
            SendFixation(hit, fixationTimer, source);

            if (!ShouldSendImage(hit, source))
                return;

            if (Gossip.Instance?.Settings?.SelectedEnvironment
                == Core.Configuration.GossipSettings.Environment.Production)
            {
                EyeGazeImageTracker.Track(gazeRay, hit, fixationTimer, source);
                CacheImageState(hit);
            }
        }

        private bool ShouldSendImage(RaycastHit hit, string source)
        {
            float cooldown = source == SOURCE_HEAD
                ? minTimeBetweenImages * headFallbackCooldownMultiplier
                : minTimeBetweenImages;

            if (Time.time - lastImageTime < cooldown)
                return false;

            if (hit.collider.gameObject != lastImageObject)
                return true;

            float dist = Vector3.Distance(cam.position, lastCamPos);
            float angle = Quaternion.Angle(cam.rotation, lastCamRot);

            float mul = source == SOURCE_HEAD ? headFallbackThresholdMultiplier : 1f;

            return dist > minDistanceDelta * mul
                || angle > minRotationDelta * mul;
        }

        private void CacheImageState(RaycastHit hit)
        {
            lastImageTime = Time.time;
            lastImageObject = hit.collider.gameObject;
            lastCamPos = cam.position;
            lastCamRot = cam.rotation;
        }

        private void SendFixation(RaycastHit hit, float duration, string source)
        {
            var tracker = Gossip.Instance?.EyeTrackingTracker;
            if (tracker == null)
                return;

            var go = hit.collider.gameObject;

            tracker.Capture(new Tracking.GameplayMetrics.EyeTrackingTracker.EntityData
            {
                HitObjectName = go.name,
                HitObjectTag = go.tag,
                HitX = hit.point.x,
                HitY = hit.point.y,
                HitZ = hit.point.z,
                FixationDurationSeconds = duration,
                SceneName = sceneName,
                TrackingSource = source,
                TimestampUtc = DateTime.UtcNow.ToString("o")
            });
        }

        private void FlushHeatmap()
        {
            if (heatmap == null) return;
            Gossip.Instance?.HeatmapTracker?
                .CapFromHeatmap(heatmap, "eye_gaze", true);
        }

        // El rayo de mirada podia chocar con el propio rig del jugador. Medido el
        // 26-09-2026 en Hospital Zone sobre 2.067 fijaciones: 155 de ellas y 206,64 s
        // (el 4,28 % del tiempo de mirada) quedaron atribuidas a "XR Origin (XR Rig)".
        // Nadie mira su propio rig: es un oclusor, no un objeto mirado, y ademas tapa
        // lo que hay detras. Se descartan esos impactos y se sigue con el mas cercano
        // que no cuelgue de la raiz de la camara.
        //
        // NO se toca raycastLayers. Excluir el suelo aqui apagaria tambien el
        // heatmap de mirada: sin impacto no hay RegisterHit, y el suelo es el 81,33 %
        // de las fijaciones. Que capas entran es una decision de escena, no de codigo.
        private bool RaycastIgnoringSelf(Ray ray, out RaycastHit hit)
        {
            hit = default(RaycastHit);

            Transform selfRoot = cam != null ? cam.root : null;

            int count = Physics.RaycastNonAlloc(ray, gazeHits, maxDistance, raycastLayers);
            if (count <= 0)
                return false;

            float bestDistance = float.MaxValue;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                Collider candidate = gazeHits[i].collider;
                if ((UnityEngine.Object)candidate == null)
                    continue;

                if (selfRoot != null && candidate.transform.IsChildOf(selfRoot))
                    continue;

                if (gazeHits[i].distance < bestDistance)
                {
                    bestDistance = gazeHits[i].distance;
                    hit = gazeHits[i];
                    found = true;
                }
            }

            return found;
        }

        private void OnDisable()
        {
            TryEmitFixation();
            FlushHeatmap();
        }
    }
}
