using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GossipSDK.Core.Configuration;

/// <summary>
/// Reusable UI component for scenes that have no visible gameplay (Only VR,
/// ServerStatus, Heatmaps, Platform Information).
///
/// Attach this script to a Canvas in the scene (or drop the prefab
/// Samples/Prefabs/InspectorSceneUI.prefab), configure the public fields in
/// the Inspector, and the UI is built automatically on Start.
///
/// USAGE IN PREFAB:
///   Scene name, step texts and gossipSettings are set via the Inspector.
///   The Canvas children are regenerated each time the scene runs so the
///   prefab stays clean in source control.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class InspectorSceneUI : MonoBehaviour
{
    // ── Colours ───────────────────────────────────────────────────────────────
    static readonly Color TopBarBg   = Hex("#171E33");
    static readonly Color BodyBg     = Hex("#1E2845");
    static readonly Color AccentBlue = Hex("#6899F8");
    static readonly Color CheckGreen = Hex("#01B574");
    static readonly Color StatusBg   = Hex("#304470");
    static readonly Color Orange     = Hex("#FF6E05");
    static readonly Color White      = Color.white;

    // ── Inspector-configurable fields ─────────────────────────────────────────
    [Header("Scene")]
    [Tooltip("Name shown in the title, e.g. \"Only VR\", \"Heatmaps\"")]
    public string sceneName = "Scene";

    [Header("Hint card steps (configurable per scene)")]
    public string step1 = "Open the Inspector in the Unity Editor";
    public string step2 = "Select the GossipManager in the Hierarchy";
    public string step3 = "Press Play to start the tracking session";

    [Header("SDK Settings")]
    [Tooltip("Drag the GossipAnalyticsSettings asset here")]
    public GossipSettings gossipSettings;

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        ClearCanvas();
        BuildUI();
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
        if (GetComponent<CanvasScaler>() == null)
        {
            var cs = gameObject.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        }
        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

        // Root vertical layout
        var root = MakeRect("Root", transform);
        FillParent(root);
        var vl = root.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.childControlWidth = true; vl.childControlHeight = false;
        vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;

        // ── TOP BAR ──────────────────────────────────────────────────────────
        var topBar = MakePanel("TopBar", root, TopBarBg, height: 36);
        var topHl  = topBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        topHl.padding = new RectOffset(12, 12, 0, 0);
        topHl.childAlignment = TextAnchor.MiddleLeft;
        topHl.childControlWidth = false; topHl.childControlHeight = true;
        topHl.childForceExpandWidth = false; topHl.childForceExpandHeight = true;

        var topTitle = MakeTMP("TopTitle", topBar, "Gossip Analytics SDK Setup", 11, White);
        topTitle.alignment = TextAlignmentOptions.MidlineLeft;
        SetLayoutElement(topTitle.gameObject, flexibleWidth: 1);

        string envName = gossipSettings != null ? gossipSettings.SelectedEnvironment.ToString() : "Dev";
        var badge      = MakePanel("EnvBadge", topBar, AccentBlue, width: 60, height: 20);
        var badgeTMP   = MakeTMP("EnvLabel", badge, envName.ToUpper(), 9, White);
        badgeTMP.alignment = TextAlignmentOptions.Center;
        FillParent(badgeTMP.rectTransform);

        // ── BODY ─────────────────────────────────────────────────────────────
        var body    = MakePanel("Body", root, BodyBg, flexibleHeight: 1);
        SetLayoutElement(body.gameObject, flexibleHeight: 1);
        var bodyVl  = body.gameObject.AddComponent<VerticalLayoutGroup>();
        bodyVl.padding = new RectOffset(20, 20, 20, 20);
        bodyVl.spacing = 14;
        bodyVl.childControlWidth = true; bodyVl.childControlHeight = false;
        bodyVl.childForceExpandWidth = true; bodyVl.childForceExpandHeight = false;

        // Title row: scene name + "Inspector" pill
        var titleRow = MakeRect("TitleRow", body);
        SetLayoutElement(titleRow.gameObject, preferredHeight: 22);
        var titleHl  = titleRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        titleHl.spacing = 8;
        titleHl.childAlignment = TextAnchor.MiddleLeft;
        titleHl.childControlHeight = false; titleHl.childForceExpandHeight = false;
        titleHl.childControlWidth  = false; titleHl.childForceExpandWidth  = false;

        MakeTMP("SceneTitle", titleRow, sceneName, 14, White);

        var pill    = MakePanel("InspectorPill", titleRow, Orange, width: 64, height: 18);
        var pillTMP = MakeTMP("PillLabel", pill, "Inspector", 8, White);
        pillTMP.alignment = TextAlignmentOptions.Center;
        FillParent(pillTMP.rectTransform);

        // Subtitle
        var sub = MakeTMP("Subtitle", body,
            "Tracking runs in the background — no visible gameplay", 10, AccentBlue);
        SetLayoutElement(sub.gameObject, preferredHeight: 16);

        // ── HINT CARD ────────────────────────────────────────────────────────
        var card    = MakePanel("HintCard", body, StatusBg);
        SetLayoutElement(card.gameObject, preferredHeight: 110);
        var cardVl  = card.gameObject.AddComponent<VerticalLayoutGroup>();
        cardVl.padding = new RectOffset(14, 14, 12, 12);
        cardVl.spacing = 8;
        cardVl.childControlWidth = true; cardVl.childControlHeight = false;
        cardVl.childForceExpandWidth = true; cardVl.childForceExpandHeight = false;

        MakeTMP("HintTitle", card, "How to explore this scene", 11, White);

        string[] steps = { step1, step2, step3 };
        for (int i = 0; i < steps.Length; i++)
        {
            var stepTMP = MakeTMP($"Step{i+1}", card, $"{i+1}. {steps[i]}", 10, Orange);
            SetLayoutElement(stepTMP.gameObject, preferredHeight: 16);
        }

        // ── STATUS BAR ───────────────────────────────────────────────────────
        var statusBar = MakePanel("StatusBar", root, StatusBg, height: 32);
        var statusHl  = statusBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        statusHl.padding = new RectOffset(12, 12, 0, 0);
        statusHl.spacing = 6;
        statusHl.childAlignment = TextAnchor.MiddleLeft;
        statusHl.childControlHeight = true; statusHl.childForceExpandHeight = true;

        MakePanel("Dot", statusBar, CheckGreen, width: 8, height: 8);

        var statusLabel = MakeTMP("StatusLabel", statusBar,
            $"Status: Active — sending to {envName}", 10, White);
        statusLabel.alignment = TextAlignmentOptions.MidlineLeft;
        SetLayoutElement(statusLabel.gameObject, flexibleWidth: 1);

        // ── DASHBOARD BUTTON ─────────────────────────────────────────────────
        var btnRow   = MakePanel("BtnRow", root, BodyBg, height: 48);
        var btnRowHl = btnRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        btnRowHl.padding = new RectOffset(20, 20, 8, 8);
        btnRowHl.childAlignment = TextAnchor.MiddleCenter;
        btnRowHl.childControlHeight = true; btnRowHl.childForceExpandHeight = true;

        var btn    = MakePanel("DashboardBtn", btnRow, AccentBlue, flexibleWidth: 1, height: 32);
        var btnBtn = btn.gameObject.AddComponent<Button>();
        btnBtn.onClick.AddListener(() => Application.OpenURL("https://app.gossipanalytics.com"));
        var btnTMP = MakeTMP("BtnLabel", btn,
            "Verify in Gossip Analytics Dashboard \u2192", 11, White);
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
        tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = text; tmp.fontSize = size; tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }

    static void FillParent(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
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

    static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out Color c); return c; }
}
