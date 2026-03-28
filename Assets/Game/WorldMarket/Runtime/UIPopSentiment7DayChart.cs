using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 7일 인구·민심 차트 — 인구는 7일 평균 기준 편차, 민심은 100 기준(0~200) 편차로 같은 중앙에 두고 값이 어긋날 때만 선이 벌어짐. 점 옆 수치 표시.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class UIPopSentiment7DayChart : MonoBehaviour
{
    [SerializeField] float lineWidthPixels = 8f;
    [SerializeField] Color populationLineColor = new Color(0.38f, 0.65f, 0.98f, 1f);
    [SerializeField] Color sentimentLineColor = new Color(1f, 0.82f, 0.35f, 1f);
    [SerializeField] float labelOffsetAbovePoint = 24f;
    [SerializeField] float labelOffsetBelowPoint = 14f;
    [SerializeField] int valueLabelFontSize = 14;
    [SerializeField] int axisLabelFontSize = 15;

    const float AxisLeft = 48f;
    const float AxisRight = 44f;
    const float AxisBottom = 34f;
    const float AxisTop = 58f;

    LineRenderer _popLine;
    LineRenderer _sentLine;
    Transform _linesRoot;
    RectTransform _plotAreaRt;
    UIPopSentiment7DayChartGrid _grid;

    Transform _axisRoot;
    Transform _markersRoot;
    Transform _labelsRoot;

    readonly List<TextMeshProUGUI> _popLabels = new List<TextMeshProUGUI>();
    readonly List<TextMeshProUGUI> _sentLabels = new List<TextMeshProUGUI>();
    readonly List<Image> _popDots = new List<Image>();
    readonly List<Image> _sentDots = new List<Image>();

    TextMeshProUGUI _legendPop;
    TextMeshProUGUI _legendSent;
    readonly List<TextMeshProUGUI> _leftAxisLabels = new List<TextMeshProUGUI>();
    readonly List<TextMeshProUGUI> _rightAxisLabels = new List<TextMeshProUGUI>();
    readonly List<TextMeshProUGUI> _bottomDateLabels = new List<TextMeshProUGUI>();

    float[] _pop;
    float[] _sent;
    int _count;
    bool _hasSeries;
    static Sprite _dotSprite;

    void Awake() => EnsureBuilt();

    void OnRectTransformDimensionsChange() => ApplyIfSeries();

    void OnEnable() => ApplyIfSeries();

    void EnsureBuilt()
    {
        if (_linesRoot != null)
            return;

        var plotGo = new GameObject("PlotArea", typeof(RectTransform));
        plotGo.transform.SetParent(transform, false);
        _plotAreaRt = plotGo.GetComponent<RectTransform>();
        ApplyPlotAreaMargins(_plotAreaRt);

        _grid = plotGo.AddComponent<UIPopSentiment7DayChartGrid>();

        var lrGo = new GameObject("LineRenderers", typeof(RectTransform));
        lrGo.transform.SetParent(transform, false);
        StretchFull(lrGo.GetComponent<RectTransform>());
        _linesRoot = lrGo.transform;

        _popLine = CreateLineRenderer("PopulationLine", populationLineColor);
        _sentLine = CreateLineRenderer("SentimentLine", sentimentLineColor);

        var ax = new GameObject("AxesAndLegend", typeof(RectTransform));
        ax.transform.SetParent(transform, false);
        StretchFull(ax.GetComponent<RectTransform>());
        _axisRoot = ax.transform;

        _legendPop = CreateAxisTmp(_axisRoot, "LegendPop", TextAlignmentOptions.TopLeft);
        _legendPop.fontSize = axisLabelFontSize + 3;
        _legendPop.fontStyle = FontStyles.Bold;
        _legendPop.text = "인구 7일";

        _legendSent = CreateAxisTmp(_axisRoot, "LegendSent", TextAlignmentOptions.TopLeft);
        _legendSent.fontSize = axisLabelFontSize + 3;
        _legendSent.fontStyle = FontStyles.Bold;
        _legendSent.text = "민심 0-200";

        for (int i = 0; i < 3; i++)
            _leftAxisLabels.Add(CreateAxisTmp(_axisRoot, $"LAxis{i}", TextAlignmentOptions.MidlineRight));
        for (int i = 0; i < 3; i++)
            _rightAxisLabels.Add(CreateAxisTmp(_axisRoot, $"RAxis{i}", TextAlignmentOptions.MidlineLeft));
        for (int i = 0; i < 7; i++)
            _bottomDateLabels.Add(CreateAxisTmp(_axisRoot, $"Day{i}", TextAlignmentOptions.Top));

        var mk = new GameObject("Markers", typeof(RectTransform));
        mk.transform.SetParent(transform, false);
        StretchFull(mk.GetComponent<RectTransform>());
        _markersRoot = mk.transform;

        var lblGo = new GameObject("PointValueLabels", typeof(RectTransform));
        lblGo.transform.SetParent(transform, false);
        StretchFull(lblGo.GetComponent<RectTransform>());
        _labelsRoot = lblGo.transform;
    }

    void ApplyPlotAreaMargins(RectTransform plotRt)
    {
        plotRt.anchorMin = Vector2.zero;
        plotRt.anchorMax = Vector2.one;
        plotRt.pivot = new Vector2(0.5f, 0.5f);
        plotRt.offsetMin = new Vector2(AxisLeft, AxisBottom);
        plotRt.offsetMax = new Vector2(-AxisRight, -AxisTop);
    }

    TextMeshProUGUI CreateAxisTmp(Transform parent, string name, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        tmp.fontSize = axisLabelFontSize;
        tmp.color = new Color(0.72f, 0.74f, 0.78f, 1f);
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        tmp.richText = true;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(96f, 26f);
        return tmp;
    }

    LineRenderer CreateLineRenderer(string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(_linesRoot, false);
        StretchFull(go.GetComponent<RectTransform>());

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = false;
        lr.numCornerVertices = 3;
        lr.numCapVertices = 3;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.allowOcclusionWhenDynamic = false;
        lr.alignment = LineAlignment.TransformZ;

        var sh = Shader.Find("Sprites/Default");
        if (sh == null)
            sh = Shader.Find("Unlit/Color");
        if (sh != null)
            lr.sharedMaterial = new Material(sh);

        lr.startColor = lr.endColor = color;
        lr.startWidth = lr.endWidth = 0.05f;
        lr.positionCount = 0;

        return lr;
    }

    static void StretchFull(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    public void SetSeries(IReadOnlyList<float> population7, IReadOnlyList<float> sentiment7)
    {
        EnsureBuilt();
        _hasSeries = false;
        _pop = null;
        _sent = null;
        _count = 0;

        if (population7 == null || sentiment7 == null || population7.Count < 2 || sentiment7.Count < 2)
        {
            ClearVisuals();
            HideChrome();
            return;
        }

        int c = Mathf.Min(7, Mathf.Min(population7.Count, sentiment7.Count));
        if (c < 2)
        {
            ClearVisuals();
            HideChrome();
            return;
        }

        _count = c;
        _pop = new float[c];
        _sent = new float[c];
        for (int i = 0; i < c; i++)
        {
            _pop[i] = population7[i];
            _sent[i] = sentiment7[i];
        }

        _hasSeries = true;
        ApplyGeometryAndLabels();
    }

    void ApplyIfSeries()
    {
        if (_plotAreaRt != null)
            ApplyPlotAreaMargins(_plotAreaRt);
        if (_hasSeries)
            ApplyGeometryAndLabels();
    }

    void ClearVisuals()
    {
        if (_popLine != null) _popLine.positionCount = 0;
        if (_sentLine != null) _sentLine.positionCount = 0;
        SetAllValueLabelsActive(false);
        SetAllDotsActive(false);
        if (_grid != null)
            _grid.RequestRedraw();
    }

    void HideChrome()
    {
        if (_legendPop != null) _legendPop.gameObject.SetActive(false);
        if (_legendSent != null) _legendSent.gameObject.SetActive(false);
        foreach (var t in _leftAxisLabels)
            if (t != null) t.gameObject.SetActive(false);
        foreach (var t in _rightAxisLabels)
            if (t != null) t.gameObject.SetActive(false);
        foreach (var t in _bottomDateLabels)
            if (t != null) t.gameObject.SetActive(false);
    }

    void SetAllValueLabelsActive(bool on)
    {
        foreach (var t in _popLabels)
            if (t != null) t.gameObject.SetActive(on);
        foreach (var t in _sentLabels)
            if (t != null) t.gameObject.SetActive(on);
    }

    void SetAllDotsActive(bool on)
    {
        foreach (var im in _popDots)
            if (im != null) im.gameObject.SetActive(on);
        foreach (var im in _sentDots)
            if (im != null) im.gameObject.SetActive(on);
    }

    void ApplyGeometryAndLabels()
    {
        if (!_hasSeries || _pop == null || _sent == null || _count < 2)
        {
            ClearVisuals();
            HideChrome();
            return;
        }

        var chartRt = transform as RectTransform;
        if (chartRt == null) return;

        Rect r = chartRt.rect;
        if (r.width < 8f || r.height < 8f)
        {
            ClearVisuals();
            HideChrome();
            return;
        }

        Vector2 center = r.center;
        float plotLeft = r.xMin + AxisLeft;
        float plotRight = r.xMax - AxisRight;
        float plotBottom = r.yMin + AxisBottom;
        float plotTop = r.yMax - AxisTop;
        float innerW = Mathf.Max(2f, plotRight - plotLeft);
        float innerH = Mathf.Max(2f, plotTop - plotBottom);

        float popMin = _pop[0], popMax = _pop[0], popSum = 0f;
        float sentMin = _sent[0], sentMax = _sent[0];
        for (int i = 0; i < _count; i++)
        {
            float p = _pop[i];
            float s = _sent[i];
            if (p < popMin) popMin = p;
            if (p > popMax) popMax = p;
            popSum += p;
            if (s < sentMin) sentMin = s;
            if (s > sentMax) sentMax = s;
        }

        float popMean = popSum / _count;
        float popDevMax = 1f;
        for (int i = 0; i < _count; i++)
            popDevMax = Mathf.Max(popDevMax, Mathf.Abs(_pop[i] - popMean));
        float popDenom = Mathf.Max(popDevMax * 1.2f, Mathf.Max(popMean * 0.015f, 1f));

        const float SentimentNeutral = 100f;
        const float SentimentHalfRange = 100f;
        const float MidBand = 0.4f;

        LayoutLegendAndAxes(r, center, popMin, popMean, popMax, sentMin, sentMax);

        float lineWidthWorld = PixelsToWorldLineWidth(chartRt);
        if (_popLine != null)
        {
            _popLine.startWidth = _popLine.endWidth = lineWidthWorld;
            _popLine.startColor = _popLine.endColor = populationLineColor;
        }

        if (_sentLine != null)
        {
            _sentLine.startWidth = _sentLine.endWidth = lineWidthWorld;
            _sentLine.startColor = _sentLine.endColor = sentimentLineColor;
        }

        SyncLineSorting();

        _popLine.positionCount = _count;
        _sentLine.positionCount = _count;

        EnsureValueLabelCount(_popLabels, _count, populationLineColor);
        EnsureValueLabelCount(_sentLabels, _count, sentimentLineColor);
        EnsureDotCount(_popDots, _count, populationLineColor);
        EnsureDotCount(_sentDots, _count, sentimentLineColor);

        for (int i = 0; i < _count; i++)
        {
            float t = _count > 1 ? i / (float)(_count - 1) : 0f;
            float lx = plotLeft + t * innerW;
            float popNorm = Mathf.Clamp((_pop[i] - popMean) / popDenom, -1f, 1f);
            float py = plotBottom + innerH * (0.5f + MidBand * popNorm);
            float sentNorm = Mathf.Clamp((_sent[i] - SentimentNeutral) / SentimentHalfRange, -1f, 1f);
            float sy = plotBottom + innerH * (0.5f + MidBand * sentNorm);

            Vector3 wpPop = chartRt.TransformPoint(new Vector3(lx, py, 0f));
            Vector3 wpSent = chartRt.TransformPoint(new Vector3(lx, sy, 0f));
            _popLine.SetPosition(i, wpPop);
            _sentLine.SetPosition(i, wpSent);

            var pl = _popLabels[i];
            var sl = _sentLabels[i];
            pl.gameObject.SetActive(true);
            sl.gameObject.SetActive(true);
            pl.text = $"{Mathf.RoundToInt(_pop[i]):N0}";
            sl.text = $"{Mathf.RoundToInt(_sent[i])}";
            float stagger = (i % 2) * 20f;
            PlaceValueLabel(chartRt, pl.rectTransform, lx, py, populationLineColor, stagger, true);
            PlaceValueLabel(chartRt, sl.rectTransform, lx, sy, sentimentLineColor, stagger, false);

            PlaceDot(chartRt, _popDots[i].rectTransform, lx, py, populationLineColor);
            PlaceDot(chartRt, _sentDots[i].rectTransform, lx, sy, sentimentLineColor);
            _popDots[i].gameObject.SetActive(true);
            _sentDots[i].gameObject.SetActive(true);
        }

        for (int i = _count; i < _popLabels.Count; i++)
            _popLabels[i].gameObject.SetActive(false);
        for (int i = _count; i < _sentLabels.Count; i++)
            _sentLabels[i].gameObject.SetActive(false);
        for (int i = _count; i < _popDots.Count; i++)
            _popDots[i].gameObject.SetActive(false);
        for (int i = _count; i < _sentDots.Count; i++)
            _sentDots[i].gameObject.SetActive(false);

        if (_grid != null)
            _grid.RequestRedraw();
    }

    void LayoutLegendAndAxes(Rect r, Vector2 center, float popMin, float popMean, float popMax, float sentMin,
        float sentMax)
    {
        if (_legendPop != null) _legendPop.fontSize = axisLabelFontSize + 3;
        if (_legendSent != null) _legendSent.fontSize = axisLabelFontSize + 3;
        foreach (var t in _leftAxisLabels)
            if (t != null) t.fontSize = axisLabelFontSize;
        foreach (var t in _rightAxisLabels)
            if (t != null) t.fontSize = axisLabelFontSize;
        foreach (var t in _bottomDateLabels)
            if (t != null) t.fontSize = axisLabelFontSize;

        float topY = r.yMax - 12f;
        _legendPop.color = populationLineColor;
        _legendSent.color = sentimentLineColor;
        _legendPop.text = "인구 7일";
        _legendSent.text = "민심 0-200";

        LayoutTmp(_legendPop.rectTransform,
            new Vector2(r.xMin + 8f - center.x, topY - center.y));
        LayoutTmp(_legendSent.rectTransform,
            new Vector2(r.xMin + 118f - center.x, topY - center.y));

        _leftAxisLabels[0].text = FormatPopAxis(popMin);
        _leftAxisLabels[1].text = FormatPopAxis(popMean);
        _leftAxisLabels[2].text = FormatPopAxis(popMax);
        _leftAxisLabels[0].color = new Color(0.55f, 0.72f, 0.95f, 0.95f);
        _leftAxisLabels[1].color = _leftAxisLabels[0].color;
        _leftAxisLabels[2].color = _leftAxisLabels[0].color;

        float plotBottom = r.yMin + AxisBottom;
        float plotTop = r.yMax - AxisTop;
        float innerH = Mathf.Max(2f, plotTop - plotBottom);
        for (int k = 0; k < 3; k++)
        {
            float ty = plotBottom + (k / 2f) * innerH;
            LayoutTmp(_leftAxisLabels[k].rectTransform,
                new Vector2(r.xMin + 4f - center.x, ty - center.y));
        }

        _rightAxisLabels[0].text = $"{Mathf.RoundToInt(sentMin)}";
        _rightAxisLabels[1].text = "100";
        _rightAxisLabels[2].text = $"{Mathf.RoundToInt(sentMax)}";
        _rightAxisLabels[0].color = new Color(0.95f, 0.82f, 0.45f, 0.95f);
        _rightAxisLabels[1].color = _rightAxisLabels[0].color;
        _rightAxisLabels[2].color = _rightAxisLabels[0].color;
        for (int k = 0; k < 3; k++)
        {
            float ty = plotBottom + (k / 2f) * innerH;
            LayoutTmp(_rightAxisLabels[k].rectTransform,
                new Vector2(r.xMax - 4f - center.x, ty - center.y));
        }

        DateTime end = DateTime.Today;
        float plotLeft = r.xMin + AxisLeft;
        float plotRight = r.xMax - AxisRight;
        float innerW = Mathf.Max(2f, plotRight - plotLeft);
        for (int i = 0; i < _count; i++)
        {
            float t = _count > 1 ? i / (float)(_count - 1) : 0f;
            float lx = plotLeft + t * innerW;
            DateTime day = end.AddDays(-(_count - 1 - i));
            _bottomDateLabels[i].text = $"{day.Month}/{day.Day}";
            _bottomDateLabels[i].color = new Color(0.65f, 0.68f, 0.72f, 1f);
            LayoutTmp(_bottomDateLabels[i].rectTransform,
                new Vector2(lx - center.x, r.yMin + 4f - center.y));
            _bottomDateLabels[i].gameObject.SetActive(true);
        }

        for (int i = _count; i < _bottomDateLabels.Count; i++)
            _bottomDateLabels[i].gameObject.SetActive(false);

        _leftAxisLabels[0].gameObject.SetActive(true);
        _leftAxisLabels[1].gameObject.SetActive(true);
        _leftAxisLabels[2].gameObject.SetActive(true);
        _rightAxisLabels[0].gameObject.SetActive(true);
        _rightAxisLabels[1].gameObject.SetActive(true);
        _rightAxisLabels[2].gameObject.SetActive(true);
        _legendPop.gameObject.SetActive(true);
        _legendSent.gameObject.SetActive(true);
    }

    static string FormatPopAxis(float v)
    {
        if (v >= 1_000_000f)
            return $"{v / 1_000_000f:0.#}M";
        if (v >= 10_000f)
            return $"{v / 1000f:0.#}k";
        return $"{Mathf.RoundToInt(v):N0}";
    }

    static void LayoutTmp(RectTransform labelRt, Vector2 anchoredFromCenter)
    {
        labelRt.anchorMin = labelRt.anchorMax = new Vector2(0.5f, 0.5f);
        labelRt.pivot = new Vector2(0.5f, 0.5f);
        labelRt.anchoredPosition = anchoredFromCenter;
    }

    void PlaceValueLabel(RectTransform chartRt, RectTransform labelRt, float localX, float localY, Color col,
        float staggerPx, bool abovePoint)
    {
        labelRt.SetParent(_labelsRoot, false);
        labelRt.anchorMin = labelRt.anchorMax = new Vector2(0.5f, 0.5f);
        Vector2 center = chartRt.rect.center;
        var tmp = labelRt.GetComponent<TextMeshProUGUI>();
        if (abovePoint)
        {
            labelRt.pivot = new Vector2(0.5f, 0f);
            labelRt.anchoredPosition = new Vector2(localX - center.x,
                localY - center.y + labelOffsetAbovePoint + staggerPx);
            labelRt.sizeDelta = new Vector2(78f, 22f);
            if (tmp != null)
            {
                tmp.alignment = TextAlignmentOptions.Bottom;
            }
        }
        else
        {
            labelRt.pivot = new Vector2(0.5f, 1f);
            labelRt.anchoredPosition = new Vector2(localX - center.x,
                localY - center.y - labelOffsetBelowPoint - staggerPx);
            labelRt.sizeDelta = new Vector2(56f, 20f);
            if (tmp != null)
            {
                tmp.alignment = TextAlignmentOptions.Top;
            }
        }

        if (tmp != null)
        {
            tmp.color = col;
            tmp.fontSize = valueLabelFontSize;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
        }
    }

    void PlaceDot(RectTransform chartRt, RectTransform dotRt, float localX, float localY, Color col)
    {
        dotRt.SetParent(_markersRoot, false);
        dotRt.anchorMin = dotRt.anchorMax = new Vector2(0.5f, 0.5f);
        dotRt.pivot = new Vector2(0.5f, 0.5f);
        Vector2 c = chartRt.rect.center;
        dotRt.anchoredPosition = new Vector2(localX - c.x, localY - c.y);
        dotRt.sizeDelta = new Vector2(13f, 13f);
        var img = dotRt.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = GetDotSprite();
            img.color = col;
        }
    }

    static Sprite GetDotSprite()
    {
        if (_dotSprite != null)
            return _dotSprite;
        const int s = 24;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float rad = s * 0.42f;
        Vector2 o = new Vector2(s * 0.5f, s * 0.5f);
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), o);
                float a = d <= rad ? 1f : (d <= rad + 1.2f ? 1f - (d - rad) / 1.2f : 0f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
            }
        }

        tex.Apply();
        _dotSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return _dotSprite;
    }

    float PixelsToWorldLineWidth(RectTransform chartRt)
    {
        Canvas c = chartRt.GetComponentInParent<Canvas>();
        float scale = c != null ? c.scaleFactor : 1f;
        Vector3 a = chartRt.TransformPoint(Vector3.zero);
        Vector3 b = chartRt.TransformPoint(new Vector3(lineWidthPixels / scale, 0f, 0f));
        return Mathf.Max(0.0008f, Vector3.Distance(a, b));
    }

    void SyncLineSorting()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        int order = canvas.overrideSorting ? canvas.sortingOrder : canvas.rootCanvas.sortingOrder;
        int lineOrder = order + 25;
        if (_popLine != null)
        {
            _popLine.sortingLayerID = canvas.sortingLayerID;
            _popLine.sortingOrder = lineOrder;
        }

        if (_sentLine != null)
        {
            _sentLine.sortingLayerID = canvas.sortingLayerID;
            _sentLine.sortingOrder = lineOrder + 1;
        }
    }

    void EnsureValueLabelCount(List<TextMeshProUGUI> list, int need, Color tint)
    {
        while (list.Count < need)
        {
            var go = new GameObject($"Val_{list.Count}", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(_labelsRoot, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
            tmp.fontSize = valueLabelFontSize;
            tmp.color = tint;
            tmp.alignment = TextAlignmentOptions.Bottom;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = false;
            tmp.richText = true;
            list.Add(tmp);
        }
    }

    void EnsureDotCount(List<Image> list, int need, Color tint)
    {
        while (list.Count < need)
        {
            var go = new GameObject($"Dot_{list.Count}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_markersRoot, false);
            var img = go.GetComponent<Image>();
            img.sprite = GetDotSprite();
            img.color = tint;
            img.raycastTarget = false;
            img.preserveAspect = true;
            list.Add(img);
        }
    }
}
