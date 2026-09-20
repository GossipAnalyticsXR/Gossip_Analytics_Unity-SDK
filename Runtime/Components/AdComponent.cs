using System;
using System.Collections;
using UnityEngine;
using GossipSDK.Core;
using GossipSDK.Tracking.GameplayMetrics;
using GossipSDK.Utilities;

namespace GossipSDK.Components
{
    [DisallowMultipleComponent]
    public class AdComponent : MonoBehaviour
    {
        public string adId;
        public string typePay;

        public string adNetwork;
        public string placementId;

        [Header("Auto behavior")]
        public bool autoStartOnEnable = false;

        public bool autoEndOnDisable = true;
        public bool sendImmediately = false;

        private AdTracker tracker => Gossip.Instance?.GetType().GetProperty("AdTracker")?.GetValue(Gossip.Instance) as AdTracker
                                     ?? (Gossip.Instance != null ? (Gossip.Instance.AdTracker ?? null) : null);

        private string resolvedAdId;

        /// <summary>
        /// La identidad del anuncio se resuelve UNA vez, no en cada OnEnable.
        ///
        /// Antes, un adId vacio se sustituia por un Guid NUEVO cada vez que el objeto
        /// se encendia. Medido en gafas el 20/09/2026: dos ciclos encender/apagar
        /// dieron dos Guid distintos, y por tanto dos filas nuevas en el informe que
        /// no se pueden agregar entre si ni comparar entre rangos, porque un Guid no
        /// se repite jamas. Un anuncio que en el juego es UNO salia como N.
        ///
        /// Ahora, sin adId, la identidad es la ruta del objeto en la jerarquia. No se
        /// inventa nada: se observa donde esta el objeto, y eso es lo mismo en cada
        /// encendido, en cada sesion y en cada build.
        /// </summary>
        private void Awake()
        {
            if (!string.IsNullOrEmpty(adId))
            {
                resolvedAdId = adId;
                return;
            }

            resolvedAdId = Jerarquia.RutaDe(transform);
            Debug.LogWarning(
                "[AdComponent] " + resolvedAdId
                    + ": adId esta vacio, uso la ruta del objeto como identidad."
                    + " Ponle un adId para que el informe hable de tu anuncio y no"
                    + " de tu jerarquia.");
        }

        private void OnEnable()
        {
            if (autoStartOnEnable) StartCoroutine(WaitAndStart());
        }

        private IEnumerator WaitAndStart()
        {
            yield return new WaitUntil(() => Gossip.Instance != null);
            StartAd();
        }

        private void OnDisable()
        {
            if (autoEndOnDisable)
                EndAd();
        }

        public void StartAd()
        {
            var t = tracker;
            if (t == null)
            {
                Debug.LogWarning("[AdComponent] AdTracker not available.");
                return;
            }

            t.StartAdSession(resolvedAdId, adNetwork, placementId);
            if (sendImmediately) t.SendDataToSocket();
        }

        public void EndAd()
        {
            var t = tracker;
            if (t == null)
            {
                Debug.LogWarning("[AdComponent] AdTracker not available.");
                return;
            }

            t.EndAdSession(resolvedAdId, adNetwork, placementId);
            if (sendImmediately) t.SendDataToSocket();
        }

        public void RecordImpression(int? impressionCount = null)
        {
            var t = tracker;
            if (t == null)
            {
                Debug.LogWarning("[AdComponent] AdTracker not available.");
                return;
            }

            t.CapImpression(resolvedAdId, typePay, adNetwork, placementId, impressionCount);
            if (sendImmediately) t.SendDataToSocket();
        }

        public void RecordInteraction(string interactionType, int? interactionCount = null)
        {
            var t = tracker;
            if (t == null)
            {
                Debug.LogWarning("[AdComponent] AdTracker not available.");
                return;
            }

            t.CapInteraction(resolvedAdId, typePay, interactionType, adNetwork, placementId, interactionCount);
            if (sendImmediately) t.SendDataToSocket();
        }

        public void RecordReward(bool granted, string rewardType = null, double? rewardAmount = null)
        {
            var t = tracker;
            if (t == null)
            {
                Debug.LogWarning("[AdComponent] AdTracker not available.");
                return;
            }

            t.CapReward(resolvedAdId, typePay, granted, rewardType, rewardAmount, adNetwork, placementId);
            if (sendImmediately) t.SendDataToSocket();
        }

        public void OnAdOpened() => StartAd();
        public void OnAdClosed() => EndAd();

#if UNITY_EDITOR
        [ContextMenu("Editor: Start Ad")]
        private void Editor_StartAd() => StartAd();

        [ContextMenu("Editor: End Ad")]
        private void Editor_EndAd() => EndAd();

        [ContextMenu("Editor: Impression")]
        private void Editor_Impression() => RecordImpression(1);

        [ContextMenu("Editor: Interaction")]
        private void Editor_Interaction() => RecordInteraction("click", 1);

        [ContextMenu("Editor: Reward")]
        private void Editor_Reward() => RecordReward(true, "coins", 100);
#endif
    }
}
