using System;
using UnityEngine;
using GossipSDK.Core;
using GossipSDK.Tracking.GameplayMetrics;

[DisallowMultipleComponent]
public class AvatarTrackerComponent : MonoBehaviour
{
    [Header("Avatar Info")]
    [SerializeField] private string avatarId   = "avatar_01";
    [SerializeField] private string avatarName = "Default Avatar";
    [SerializeField] private string variant    = "";
    [SerializeField] private string brand      = "";
    [SerializeField] private string price      = "0.00";
    [SerializeField] private Color  color      = Color.white;

    [Header("Payment")]
    [SerializeField] private PaymentType paymentType = PaymentType.Card;

    [Header("Settings")]
    [SerializeField] private bool autoReportOnStart = false;

    private void Start()
    {
        if (autoReportOnStart)
            NotifyAvatar();
    }

    public void NotifyAvatar(
        string id           = null,
        PaymentType? typePay = null,
        string name         = null,
        string variantVal   = null,
        string brnd         = null,
        string pr           = null,
        Color? col          = null)
    {
        try
        {
            string aId   = string.IsNullOrEmpty(id)   ? avatarId   : id;
            string aP    = (typePay ?? paymentType).ToString();
            string aName = string.IsNullOrEmpty(name) ? avatarName : name;
            string varr  = string.IsNullOrEmpty(variantVal) ? variant : variantVal;
            string Brnd  = string.IsNullOrEmpty(brnd) ? brand : brnd;
            string PR    = string.IsNullOrEmpty(pr)   ? price  : pr;
            string hex   = ColorUtility.ToHtmlStringRGB(col ?? color);

            var direct = Gossip.Instance?.AvatarTracker;
            if (direct != null)
            {
                direct.CapAvatar(aId, aP, aName, varr, Brnd, PR, hex, null);
            }
            else
            {
                Debug.LogWarning("[AvatarTrackerComponent] AvatarTracker not available on Gossip instance.");
            }

            if (Gossip.Instance?.Settings?.EnableDebug == true)
                Debug.Log($"[AvatarTrackerComponent] Notified avatar: id={aId} name={aName} variant={varr} color={hex}");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }
}
