using System;
using UnityEngine;
using GossipSDK.Core;
using GossipSDK.Tracking.GameplayMetrics;

[DisallowMultipleComponent]
public class AvatarTrackerComponent : MonoBehaviour
{
    public bool autoReportOnStart = true;

    [Header("Avatar (optional prefill)")]
    public string avatarId;
    public string avatarPay;
    public string avatarName;
    public string variant;
    public string brand;
    public string price;
    public Color color = Color.white;

    [Tooltip("Enviar automáticamente la información de avatar al Start()")]

    private string ToHex(Color c)
    {
        Color32 cc = c;
        return $"#{cc.r:X2}{cc.g:X2}{cc.b:X2}";
    }

    private void Start()
    {
        if (autoReportOnStart)
            NotifyAvatar();
    }

    public void NotifyAvatar(string id = null, string typePay = null, string name = null, string variantVal = null, string brnd = null, string pr = null, Color? col = null)
    {
        try
        {
            var aId = string.IsNullOrEmpty(id) ? avatarId : id;
            var aP = string.IsNullOrEmpty(typePay) ? avatarPay : typePay;
            var aName = string.IsNullOrEmpty(name) ? avatarName : name;
            var varr = string.IsNullOrEmpty(variantVal) ? variant : variantVal;
            var Brnd = string.IsNullOrEmpty(brnd) ? brand : brnd;
            var PR = string.IsNullOrEmpty(pr) ? price : pr;
            var c = col ?? color;
            string hex = ToHex(c);

            var tracker = Gossip.Instance?.GetType().GetProperty("AvatarTracker")?.GetValue(Gossip.Instance) as AvatarTracker;
            if (tracker == null)
            {
                tracker = Gossip.Instance?.GetType().GetProperty("AvatarTracker")?.GetValue(Gossip.Instance) as AvatarTracker;
            }

            var direct = Gossip.Instance?.AvatarTracker;
            if (direct != null)
            {
                direct.CapAvatar(aId, aName, varr, Brnd, PR, hex, null);
            }
            else if (tracker != null)
            {
                tracker.CapAvatar(aId, aName, varr, Brnd, PR, hex, null);
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
