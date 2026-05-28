using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GossipSDK.Core.Configuration;
using GossipSDK.Core;

/// <summary>
/// Attaches to the Canvas in the "Audio Volume" demo scene.
/// Clears existing UI, builds slider + live percentage + feedback bar.
/// BUG FIX: percentage now updates live on every slider value change.
/// BUG FIX: card label says "Master volume" (not "Player events").
/// </summary>
[RequireComponent(typeof(Canvas))]
public class AudioVolumeDemoUI : MonoBehaviour
{
    // ── Colours ───────────────────────────────────────────────────────────────
    static readonly Color TopBarBg   = Hex("#171E33");
    static readonly Color BodyBg     = Hex("#1E2845");
    static readonly Color AccentBlue = Hex("#6899F8");
    static readonly Color CheckGreen = Hex("#01B574");
    static readonly Color StatusBg   = Hex("#304470");
    static readonly Color LabelGray  = Hex("#898888");
    static readonly Color White      = Color.white;
    static readonly Color CardBg     = Color.white;

    [Header("Settings")]
    public GossipSettings gossipSettings;

    // ── Runtime ───────────────────────────────────────────────────────────────
    Slider          _slider;
    TextMeshProUGUI _percentLabel;
    TextMeshProUGUI _feedbackLabel;
    Image           _feedbackDot;
    Coroutine       _fadeRoutine;

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        ClearCanvas();
        BuildUI();
    }

    void ClearCanvas()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
    }

    // ─────────────────────────────────────────────────────────────────────────
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
        MakeTMP("Title", body, "Audio volume tracker demo", 14, White);

        // Subtitle
        var sub = MakeTMP("Subtitle", body, "Drag slider to simulate a volume change", 10, AccentBlue);
        SetLayoutElement(sub.gameObject, preferredHeight: 16);

        // ── WHITE CARD ───────────────────────────────────────────────────────
        var card    = MakePanel("VolumeCard", body, CardBg);
        SetLayoutElement(card.gameObject, preferredHeight: 80);
        var cardVl  = card.gameObject.AddComponent<VerticalLayoutGroup>();
        cardVl.padding = new RectOffset(16, 16, 12, 12);
        cardVl.spacing = 8;
        cardVl.childControlWidth = true; cardVl.childControlHeight = false;
        cardVl.childForceExpandWidth = true; cardVl.childForceExpandHeight = false;

        // Label row: "Master volume" (LEFT) + "XX%" (RIGHT, in AccentBlue)
        var labelRow = MakeRect("LabelRow", card);
        SetLayoutElement(labelRow.gameObject, preferredHeight: 20);
        var hl = labelRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.childControlHeight = true; hl.childForceExpandHeight = true;
        hl.childControlWidth  = false; hl.childForceExpandWidth  = false;

        // "Master volume" label — BUG FIX: was "Player events"
        var volLabel = MakeTMP("VolumeLabel", labelRow, "Master volume", 11, Hex("#333333"));
        SetLayoutElement(volLabel.gameObject, flexibleWidth: 1);

        _percentLabel = MakeTMP("PercentLabel", labelRow, "0%", 11, AccentBlue);
        _percentLabel.alignment = TextAlignmentOptions.MidlineRight;

        // Slider
        _slider = BuildSlider(card);

        // ── FEEDBACK BAR ─────────────────────────────────────────────────────
        var fbar   = MakePanel("FeedbackBar", body, StatusBg);
        SetLayoutElement(fbar.gameObject, preferredHeight: 32);
        var fbarHl = fbar.gameObject.AddComponent<HorizontalLayoutGroup>();
        fbarHl.padding = new RectOffset(10, 10, 0, 0);
        fbarHl.spacing = 6;
        fbarHl.childAlignment = TextAnchor.MiddleLeft;
        fbarHl.childControlHeight = true; fbarHl.childForceExpandHeight = true;

        _feedbackDot = MakePanel("Dot", fbar, CheckGreen, width: 8, height: 8);
        _feedbackLabel = MakeTMP("FeedbackLabel", fbar, "Event fires on slider release", 10, White);
        _feedbackLabel.alignment = TextAlignmentOptions.MidlineLeft;
        SetLayoutElement(_feedbackLabel.gameObject, flexibleWidth: 1);

        // Start transparent — shown only after slider release
        SetFeedbackAlpha(0f);

        // ── FOOTER ───────────────────────────────────────────────────────────
        var footer = MakeTMP("Footer", body,
            "Event fires on slider release \u2192 Dashboard: Audio \u2192 Volume", 9, LabelGray);
        SetLayoutElement(footer.gameObject, preferredHeight: 16);
    }

    // ── Slider builder ────────────────────────────────────────────────────────
    Slider BuildSlider(Transform parent)
    {
        var sliderGO = new GameObject("Slider", typeof(RectTransform));
        sliderGO.transform.SetParent(parent, false);
        SetLayoutElement(sliderGO, preferredHeight: 20);

        var sliderRT = sliderGO.GetComponent<RectTransform>();
        sliderRT.anchorMin = new Vector2(0, 0.5f);
        sliderRT.anchorMax = new Vector2(1, 0.5f);
        sliderRT.sizeDelta = new Vector2(0, 20);

        var slider = sliderGO.AddComponent<Slider>();
        slider.minValue = 0; slider.maxValue = 100; slider.value = 0;
        slider.wholeNumbers = true;

        // Background
        var bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(sliderGO.transform, false);
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        bgGO.GetComponent<Image>().color = Hex("#E0E0E0");

        // Fill area
        var fillAreaGO = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        var faRT = fillAreaGO.GetComponent<RectTransform>();
        faRT.anchorMin = new Vector2(0,0); faRT.anchorMax = new Vector2(1,1);
        faRT.offsetMin = new Vector2(5,0); faRT.offsetMax = new Vector2(-5,0);

        var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = new Vector2(0,1);
        fillRT.sizeDelta = new Vector2(10,0);
        fillGO.GetComponent<Image>().color = AccentBlue;

        slider.fillRect = fillRT;

        // Handle slide area
        var handleAreaGO = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleAreaGO.transform.SetParent(sliderGO.transform, false);
        var haRT = handleAreaGO.GetComponent<RectTransform>();
        haRT.anchorMin = Vector2.zero; haRT.anchorMax = Vector2.one;
        haRT.offsetMin = new Vector2(10,0); haRT.offsetMax = new Vector2(-10,0);

        var handleGO = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGO.transform.SetParent(handleAreaGO.transform, false);
        var handleRT = handleGO.GetComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(20,20);
        handleGO.GetComponent<Image>().color = AccentBlue;

        slider.handleRect    = handleRT;
        slider.targetGraphic = handleGO.GetComponent<Image>();

        // ── BUG FIX: live percentage update ──────────────────────────────────
        slider.onValueChanged.AddListener(OnSliderChanged);
        slider.onValueChanged.AddListener(_ => { /* additional listeners */ });

        // Fire on release via EventTrigger
        var et = sliderGO.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        var entry = new UnityEngine.EventSystems.EventTrigger.Entry
            { eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp };
        entry.callback.AddListener(_ => OnSliderReleased());
        et.triggers.Add(entry);

        return slider;
    }

    // ── Callbacks ─────────────────────────────────────────────────────────────

    /// <summary>BUG FIX: updates the percentage label live as the slider moves.</summary>
    void OnSliderChanged(float value)
    {
        if (_percentLabel != null)
            _percentLabel.text = $"{Mathf.RoundToInt(value)}%";
    }

    void OnSliderReleased()
    {
        int pct = Mathf.RoundToInt(_slider.value);

        // Send event to Gossip SDK
        try { Gossip.Instance?.UserEventTracker?.CaptureEvent("Audio", $"VolumeChange:{pct}"); }
        catch { /* SDK not initialised in editor */ }

        // Show feedback
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _feedbackLabel.text = $"Event sent: AudioVolume {{ level: {pct}% }}";
        SetFeedbackAlpha(1f);
        _fadeRoutine = StartCoroutine(FadeOutFeedback(3f));
    }

    IEnumerator FadeOutFeedback(float delay)
    {
        yield return new WaitForSeconds(delay);
        float t = 0f;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            SetFeedbackAlpha(1f - (t / 0.5f));
            yield return null;
        }
        SetFeedbackAlpha(0f);
    }

    void SetFeedbackAlpha(float a)
    {
        if (_feedbackLabel != null) _feedbackLabel.alpha = a;
        if (_feedbackDot   != null) { var c = _feedbackDot.color; c.a = a; _feedbackDot.color = c; }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    void BuildTopBar(Transform parent)
    {
        var topBar = MakePanel("TopBar", parent, TopBarBg, height: 36);
        var hl = topBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(12, 12, 0, 0);
        hl.childAlignment = TextAnchor.MiddleLeft;
        hl.childControlWidth = false; hl.childControlHeight = true;
        hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;

        var title = MakeTMP("TopTitle", topBar, "Gossip Analytics SDK Setup", 11, White);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        SetLayoutElement(title.gameObject, flexibleWidth: 1);

        string envName = gossipSettings != null ? gossipSettings.SelectedEnvironment.ToString() : "Dev";
        var badge    = MakePanel("EnvBadge", topBar, AccentBlue, width: 60, height: 20);
        var badgeTMP = MakeTMP("EnvLabel", badge, envName.ToUpper(), 9, White);
        badgeTMP.alignment = TextAlignmentOptions.Center;
        FillParent(badgeTMP.rectTransform);
    }

    static RectTransform MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static Image MakePanel(string name, Transform parent, Color color,
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

    static TextMeshProUGUI MakeTMP(string name, Transform parent, string text, float size, Color color)
    {
        var rt  = MakeRect(name, parent);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
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
