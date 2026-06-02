using UnityEngine;
using GossipSDK;
using GossipSDK.Core;
using GossipSDK.Tracking.GameplayMetrics;

[DisallowMultipleComponent]
public class AccessoriesComponent : MonoBehaviour
{
    [Header("Accessory Info")]
    [SerializeField] private string accessoryName = "Default Item";
    [SerializeField] private string accessoryPrice = "0.00";
    [SerializeField] private string accessoryBrand = "";
    [SerializeField] private string totalPurchased = "0.00";

    [Header("Payment")]
    [SerializeField] private PaymentType paymentType = PaymentType.Card;

    [Header("Settings")]
    [SerializeField] private bool sendImmediately = false;
    [SerializeField] private bool autoReportOnStart = false;

    private void Start()
    {
        if (autoReportOnStart)
            ReportPurchased();
    }

    public void ReportPurchased(
        string name = null,
        string price = null,
        string brand = null,
        PaymentType? typePay = null,
        string total = null)
    {
        var tracker = Gossip.Instance?.AccessoriesTracker;
        if (tracker == null) return;

        string n = string.IsNullOrEmpty(name) ? accessoryName : name;
        string p = string.IsNullOrEmpty(price) ? accessoryPrice : price;
        string b = string.IsNullOrEmpty(brand) ? accessoryBrand : brand;
        string tp = (typePay ?? paymentType).ToString();
        string t = string.IsNullOrEmpty(total) ? totalPurchased : total;

        tracker.CapAccessory(n, p, b, tp, t);
        if (sendImmediately) tracker.SendDataToSocket();
    }
}
