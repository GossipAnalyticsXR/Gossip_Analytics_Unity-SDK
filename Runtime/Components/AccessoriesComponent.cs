using UnityEngine;
using GossipSDK.Core;
using GossipSDK.Tracking.GameplayMetrics;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class AccessoriesComponent : MonoBehaviour
{
    public bool sendImmediately = false;
    public bool autoReportOnStart = false;

    private void Start()
    {
        if (autoReportOnStart)
            ReportPurchased("demo", "100", "DemoBrand", "Credit", "0.01");
    }

    public void ReportPurchased(string name, string price, string brand, string typePay, string totalPurchased = null)
    {
        var tracker = Gossip.Instance?.AccessoriesTracker;
        if (tracker == null) return;

        tracker.CapAccessory(name, price, brand, typePay, totalPurchased);
        if (sendImmediately) tracker.SendDataToSocket();
    }
}
