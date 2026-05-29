using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GossipSDK.Core.Configuration;
using GossipSDK.Core;

/// <summary>
/// Attaches to the Canvas in the "Multiplayer" demo scene.
/// Clears existing UI on Start, builds the multiplayer demo screen.
/// Connect GossipManager's MultiplayerComponent in the Inspector.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class MultiplayerDemoUI : MonoBehaviour
{
    // ── Colours ───────────────────────────────────────────────────────────────
    static readonly Color TopBarBg   = Hex("#171E33");
    static readonly Color BodyBg     = Hex("#1E2845");
    static readonly Color AccentBlue = Hex("#6899F8");
    static readonly Color CheckGreen = Hex("#01B574");
    static readonly Color StatusBg   = Hex("#304470");
    static readonly Color Orange     = Hex("#FF6E05");
    static readonly Color LabelGray  = Hex("#898888");
    static readonly Color White      = Color.white;

    [Header("Settings")]
    public GossipSettings gossipSettings;

    // ── Runtime ───────────────────────────────────────────────────────────────
    TextMeshProUGUI _feedbackLabel;
    Coroutine       _feedbackRoutine;
    float           _lastEventTime  = -1f;
    string          _lastEventName  = "";

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        ClearCanvas();
        BuildUI();
    }

    void Update()
    {
        if (_feedbackLabel == null || _lastEventTime < 0) return;
        float elapsed = Time.realtimeSinceStartup - _lastEventTime;
        _feedbackLabel.text = $"Sent: {_lastEventName} — {Mathf.FloorToInt(elapsed)}s ago";
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

        // Root vertical
        var root = MakeRect("Root", transform);
        FillParent(root);
        var vl = root.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.childControlWidth = true; vl.childControlHeight = false;
        vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;

        // ── TOP BAR ──────────────────────────────────────────────────────────
        BuildTopBar(root);

        // ── BODY ─────────────────────────────────────────────────────────────
        var body = MakePanel("Body", root, BodyBg, flexibleHeight: 1);
        SetLayoutElement(body.gameObject, flexibleHeight: 1);
        var bodyVl = body.gameObject.AddComponent<VerticalLayoutGroup>();
        bodyVl.padding = new RectOffset(20, 20, 20, 20);
        bodyVl.spacing = 14;
        bodyVl.childControlWidth = true; bodyVl.childControlHeight = false;
        bodyVl.childForceExpandWidth = true; bodyVl.childForceExpandHeight = false;

        // Title
        MakeTMP("Title", body, "Multiplayer tracker demo", 14, White);

        // Subtitle
        var sub = MakeTMP("Subtitle", body, "Simulates players joining/leaving rooms", 10, AccentBlue);
        SetLayoutElement(sub.gameObject, preferredHeight: 16);

        // ── PLAYER EVENTS SECTION ─────────────────────────────────────────────
        var playerLabel = MakeTMP("PlayerLabel", body, "PLAYER EVENTS", 9, LabelGray);
        playerLabel.fontStyle = FontStyles.UpperCase;

        var playerRow = MakeRect("PlayerRow", body);
        var playerHl  = playerRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        playerHl.spacing = 8;
        playerHl.childControlHeight = true; playerHl.childForceExpandHeight = false;
        playerHl.childControlWidth  = false; playerHl.childForceExpandWidth = false;
        SetLayoutElement(playerRow.gameObject, preferredHeight: 36);

        MakeEventButton("Player Joined",  playerRow, () => FireEvent("PlayerJoined"));
        MakeEventButton("Player Left",    playerRow, () => FireEvent("PlayerLeft"));

        // ── ROOM EVENTS SECTION ───────────────────────────────────────────────
        var roomLabel = MakeTMP("RoomLabel", body, "ROOM EVENTS", 9, LabelGray);
        roomLabel.fontStyle = FontStyles.UpperCase;

        var roomRow = MakeRect("RoomRow", body);
        var roomHl  = roomRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        roomHl.spacing = 8;
        roomHl.childControlHeight = true; roomHl.childForceExpandHeight = false;
        roomHl.childControlWidth  = false; roomHl.childForceExpandWidth = false;
        SetLayoutElement(roomRow.gameObject, preferredHeight: 36);

        MakeEventButton("Room Started",  roomRow, () => FireEvent("RoomStarted"));
        MakeEventButton("Room Stopped",  roomRow, () => FireEvent("RoomStopped"));

        // ── FEEDBACK BAR ─────────────────────────────────────────────────────
        var fbar    = MakePanel("FeedbackBar", body, StatusBg, flexibleWidth: 1);
        SetLayoutElement(fbar.gameObject, preferredHeight: 32);
        var fbarHl  = fbar.gameObject.AddComponent<HorizontalLayoutGroup>();
        fbarHl.padding = new RectOffset(10, 10, 0, 0);
        fbarHl.spacing = 6;
        fbarHl.childAlignment = TextAnchor.MiddleLeft;
        fbarHl.childControlHeight = true; fbarHl.childForceExpandHeight = true;

        MakePanel("Dot", fbar, CheckGreen, width: 8, height: 8);
        _feedbackLabel = MakeTMP("FeedbackLabel", fbar, "Press a button to send an event", 10, White);
        _feedbackLabel.alignment = TextAlignmentOptions.MidlineLeft;
        SetLayoutElement(_feedbackLabel.gameObject, flexibleWidth: 1);

        // ── INSPECTOR HINT CARD ───────────────────────────────────────────────
        var card    = MakePanel("HintCard", body, StatusBg);
        SetLayoutElement(card.gameObject, preferredHeight: 100);
        var cardVl  = card.gameObject.AddComponent<VerticalLayoutGroup>();
        cardVl.padding = new RectOffset(12, 12, 10, 10);
        cardVl.spacing = 6;
        cardVl.childControlWidth = true; cardVl.childControlHeight = false;
        cardVl.childForceExpandWidth = true; cardVl.childForceExpandHeight = false;

        MakeTMP("HintTitle", card, "Configure match type", 11, White);

        string[] steps = {
            "1. Select GossipManager in Hierarchy",
            "2. Find MultiplayerComponent",
            "3. Set Match Type"
        };
        foreach (var step in steps)
        {
            var stepTMP = MakeTMP("Step", card, step, 10, Orange);
            SetLayoutElement(stepTMP.gameObject, preferredHeight: 16);
        }
    }

    // ── Event button helper ───────────────────────────────────────────────────
    void MakeEventButton(string label, Transform parent, UnityEngine.Events.UnityAction onClick)
    {
        var panel = MakePanel(label, parent, StatusBg, width: 140, height: 36);
        var btn   = panel.gameObject.AddComponent<Button>();
        btn.onClick.AddListener(onClick);
        var tmp   = MakeTMP("Label", panel, label, 11, White);
        tmp.alignment = TextAlignmentOptions.Center;
        FillParent(tmp.rectTransform);
    }

    void FireEvent(string eventName)
    {
        _lastEventName = eventName;
        _lastEventTime = Time.realtimeSinceStartup;

        // Send to Gossip SDK
        try { Gossip.Instance?.UserEventTracker?.CaptureEvent("Multiplayer", eventName); }
        catch { /* SDK not initialised in editor */ }
    }

    // ── Top bar ───────────────────────────────────────────────────────────────
    void BuildTopBar(Transform parent)
    {
        var topBar = MakePanel("TopBar", parent, TopBarBg, height: 36);
        var hl     = topBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(12, 12, 0, 0);
        hl.childAlignment = TextAnchor.MiddleLeft;
        hl.childControlWidth = false; hl.childControlHeight = true;
        hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;

        var title = MakeTMP("TopTitle", topBar, "Gossip Analytics SDK Setup", 11, White);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        SetLayoutElement(title.gameObject, flexibleWidth: 1);

        string envName = gossipSettings != null ? gossipSettings.SelectedEnvironment.ToString() : "Dev";
        var badge      = MakePanel("EnvBadge", topBar, AccentBlue, width: 60, height: 20);
        var badgeTMP   = MakeTMP("EnvLabel", badge, envName.ToUpper(), 9, White);
        badgeTMP.alignment = TextAlignmentOptions.Center;
        FillParent(badgeTMP.rectTransform);
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
        if (width > 0 || height > 0) rt.sizeDelta = new Vector2(width > 0 ? width : 0, height > 0 ? height : 0);
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
