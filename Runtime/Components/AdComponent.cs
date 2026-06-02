using System;
using System.Collections;
using UnityEngine;
using GossipSDK.Core;
using GossipSDK.Tracking.GameplayMetrics;

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

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(adId))
                resolvedAdId = Guid.NewGuid().ToString();
            else
                resolvedAdId = adId;

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
