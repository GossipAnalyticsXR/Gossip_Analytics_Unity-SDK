using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GossipSDK.Core.Configuration;
using GossipSDK.Core;

/// <summary>
/// Attaches to the Canvas in the "On Start Demo" scene.
/// Clears any previous UI children on Start, then builds the new session-started screen.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class OnStartDemoUI : MonoBehaviour
{
    // ── Colours ──────────────────────────────────────────────────────────────
    static readonly Color TopBarBg   = Hex("#171E33");
    static readonly Color BodyBg     = Hex("#1E2845");
    static readonly Color AccentBlue = Hex("#6899F8");
    static readonly Color CheckGreen = Hex("#01B574");
    static readonly Color StatusBg   = Hex("#304470");
    static readonly Color White      = Color.white;

    // ── Public refs (assign in Inspector or leave null for procedural build) ──
    [Header("Optional – leave null to auto-build")]
    public Sprite owlLogoSprite;          // 48x48 owl PNG
    public GossipSettings gossipSettings; // drag GossipAnalyticsSettings asset here

    // ── Runtime ──────────────────────────────────────────────────────────────
    TextMeshProUGUI _statusLabel;
    float           _sessionStartTime;

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        _sessionStartTime = Time.realtimeSinceStartup;
        ClearCanvas();
        BuildUI();
    }

    void Update()
    {
        if (_statusLabel == null) return;
        float elapsed = Time.realtimeSinceStartup - _sessionStartTime;
        _statusLabel.text = $"Last event: Session Start — {Mathf.FloorToInt(elapsed)}s ago";
    }

    // ── Clear ─────────────────────────────────────────────────────────────────
    void ClearCanvas()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
    }

    // ── Build ─────────────────────────────────────────────────────────────────
    void BuildUI()
    {
        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

        // Root vertical layout
        var root = MakeRect("Root", transform);
        FillParent(root);
        var vl = root.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.childControlWidth  = true;
        vl.childControlHeight = false;
        vl.childForceExpandWidth  = true;
        vl.childForceExpandHeight = false;

        // ── TOP BAR ──────────────────────────────────────────────────────────
        var topBar = MakePanel("TopBar", root, TopBarBg, height: 36);
        var topHl  = topBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        topHl.padding = new RectOffset(12, 12, 0, 0);
        topHl.childAlignment = TextAnchor.MiddleLeft;
        topHl.childControlWidth  = false;
        topHl.childControlHeight = true;
        topHl.childForceExpandWidth  = false;
        topHl.childForceExpandHeight = true;

        var topTitle = MakeTMP("TopTitle", topBar, "Gossip Analytics SDK Setup", 11, White);
        topTitle.alignment = TextAlignmentOptions.MidlineLeft;
        SetLayoutElement(topTitle.gameObject, flexibleWidth: 1);

        // Environment badge pill
        string envName = gossipSettings != null ? gossipSettings.SelectedEnvironment.ToString() : "Dev";
        var badge      = MakePanel("EnvBadge", topBar, AccentBlue, width: 60, height: 20);
        badge.GetComponent<Image>().pixelsPerUnitMultiplier = 1f;
        // rounded look via outline trick
        var badgeTMP   = MakeTMP("EnvLabel", badge, envName.ToUpper(), 9, White);
        badgeTMP.alignment = TextAlignmentOptions.Center;
        FillParent(badgeTMP.rectTransform);

        // ── BODY ─────────────────────────────────────────────────────────────
        var body    = MakePanel("Body", root, BodyBg, flexibleHeight: 1);
        var bodyVl  = body.gameObject.AddComponent<VerticalLayoutGroup>();
        bodyVl.padding = new RectOffset(20, 20, 20, 20);
        bodyVl.spacing = 12;
        bodyVl.childControlWidth  = true;
        bodyVl.childControlHeight = false;
        bodyVl.childForceExpandWidth  = true;
        bodyVl.childForceExpandHeight = false;
        SetLayoutElement(body.gameObject, flexibleHeight: 1);

        // Owl logo row
        var logoRow = MakeRect("LogoRow", body);
        var logoRowHl = logoRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        logoRowHl.childControlHeight = false;
        logoRowHl.childForceExpandHeight = false;
        SetLayoutElement(logoRow.gameObject, preferredHeight: 56);

        var logoBg = MakePanel("LogoBg", logoRow, TopBarBg, width: 56, height: 56);
        if (owlLogoSprite != null)
        {
            var logoImg = MakeRect("OwlLogo", logoBg);
            logoImg.sizeDelta = new Vector2(48, 48);
            logoImg.anchorMin = logoImg.anchorMax = new Vector2(0.5f, 0.5f);
            logoImg.anchoredPosition = Vector2.zero;
            var img = logoImg.gameObject.AddComponent<Image>();
            img.sprite = owlLogoSprite;
            img.preserveAspect = true;
        }
        var spacer = MakeRect("LogoSpacer", logoRow);
        SetLayoutElement(spacer.gameObject, flexibleWidth: 1);

        // Title
        MakeTMP("Title", body, "Session started", 14, White);

        // Subtitle
        var sub = MakeTMP("Subtitle", body, "Auto-tracking active — no action required", 10, AccentBlue);
        SetLayoutElement(sub.gameObject, preferredHeight: 16);

        // Spacer
        var div = MakeRect("Div", body);
        SetLayoutElement(div.gameObject, preferredHeight: 8);

        // Checklist
        string[] checkItems = {
            "Session started",
            "User info sent (device, OS, language)",
            "Experience info sent (app version)"
        };
        foreach (var item in checkItems)
        {
            var row = MakeRect("Check_" + item.GetHashCode(), body);
            var hl  = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 8;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlHeight = true;
            hl.childForceExpandHeight = false;
            SetLayoutElement(row.gameObject, preferredHeight: 20);

            // Circle icon
            var circle = MakePanel("Circle", row, CheckGreen, width: 16, height: 16);
            var checkTxt = MakeTMP("Check", circle, "✓", 9, White);
            checkTxt.alignment = TextAlignmentOptions.Center;
            FillParent(checkTxt.rectTransform);

            MakeTMP("ItemLabel", row, item, 10, White);
        }

        // ── STATUS BAR ───────────────────────────────────────────────────────
        var statusBar = MakePanel("StatusBar", root, StatusBg, height: 32);
        var statusHl  = statusBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        statusHl.padding = new RectOffset(12, 12, 0, 0);
        statusHl.spacing = 6;
        statusHl.childAlignment = TextAnchor.MiddleLeft;
        statusHl.childControlHeight = true;
        statusHl.childForceExpandHeight = true;

        // Green dot
        MakePanel("Dot", statusBar, CheckGreen, width: 8, height: 8);

        _statusLabel = MakeTMP("StatusLabel", statusBar, "Last event: Session Start — 0s ago", 10, White);
        _statusLabel.alignment = TextAlignmentOptions.MidlineLeft;
        SetLayoutElement(_statusLabel.gameObject, flexibleWidth: 1);

        // ── DASHBOARD BUTTON ─────────────────────────────────────────────────
        var btnRow = MakePanel("BtnRow", root, BodyBg, height: 48);
        var btnRowHl = btnRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        btnRowHl.padding = new RectOffset(20, 20, 8, 8);
        btnRowHl.childAlignment = TextAnchor.MiddleCenter;
        btnRowHl.childControlHeight = true;
        btnRowHl.childForceExpandHeight = true;

        var btn    = MakePanel("DashboardBtn", btnRow, AccentBlue, flexibleWidth: 1, height: 32);
        var btnBtn = btn.gameObject.AddComponent<Button>();
        btnBtn.onClick.AddListener(() => Application.OpenURL("https://gossipanalytics.com"));
        var btnTMP = MakeTMP("BtnLabel", btn, "Open Gossip Analytics Dashboard →", 11, White);
        btnTMP.alignment = TextAlignmentOptions.Center;
        FillParent(btnTMP.rectTransform);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static RectTransform MakeRect(string name, Component parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        return go.GetComponent<RectTransform>();
    }

    static Image MakePanel(string name, Component parent, Color color,
        float width = -1, float height = -1,
        float flexibleWidth = -1, float flexibleHeight = -1)
    {
        var rt = MakeRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        if (width > 0 || height > 0)
            rt.sizeDelta = new Vector2(width > 0 ? width : 0, height > 0 ? height : 0);
        if (flexibleWidth >= 0 || flexibleHeight >= 0)
        {
            var le = rt.gameObject.AddComponent<LayoutElement>();
            if (flexibleWidth  >= 0) le.flexibleWidth  = flexibleWidth;
            if (flexibleHeight >= 0) le.flexibleHeight = flexibleHeight;
        }
        return img;
    }

    static TextMeshProUGUI MakeTMP(string name, Component parent, string text, float size, Color color)
    {
        var rt  = MakeRect(name, parent);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.color     = color;
        tmp.raycastTarget = false;
        return tmp;
    }

    static void FillParent(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void SetLayoutElement(GameObject go,
        float preferredWidth = -1, float preferredHeight = -1,
        float flexibleWidth = -1, float flexibleHeight = -1)
    {
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        if (preferredWidth  >= 0) le.preferredWidth  = preferredWidth;
        if (preferredHeight >= 0) le.preferredHeight = preferredHeight;
        if (flexibleWidth   >= 0) le.flexibleWidth   = flexibleWidth;
        if (flexibleHeight  >= 0) le.flexibleHeight  = flexibleHeight;
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
