using System;
using System.Collections.Generic;
using System.Globalization;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>천하 — 성 선택 시 하단 요약(MTS). 7일 추이·고저가·거래량 프록시·지분. 민심/백성 원시 수치는 표시하지 않습니다.</summary>
[DisallowMultipleComponent]
public class WorldMarketCastleSummarySheet : MonoBehaviour
{
    public static WorldMarketCastleSummarySheet InstanceOrNull { get; private set; }

    static readonly Color RiseColor = new Color(0.2f, 1f, 0.65f, 1f);
    static readonly Color FallColor = new Color(1f, 0.35f, 0.35f, 1f);

    CanvasGroup _canvasGroup;
    RectTransform _panelRt;

    TextMeshProUGUI _titleTmp;
    GameObject _hqBadgeGo;
    Image _iconBg;
    TextMeshProUGUI _iconGlyphTmp;
    GameObject _warBadgeGo;

    UIPriceLine7DayGraphic _priceLineGraphic;
    TextMeshProUGUI _trendBiasTmp;

    TextMeshProUGUI _rightPriceTmp;
    TextMeshProUGUI _rightChgTmp;
    TextMeshProUGUI _volTmp;

    TextMeshProUGUI _high7Tmp;
    TextMeshProUGUI _low7Tmp;
    TextMeshProUGUI _metricsStakeTmp;
    TextMeshProUGUI _metricsTroopsTmp;

    TextMeshProUGUI _causeTmp;
    TextMeshProUGUI _eventStripTmp;

    Button _closeBtn;
    Button _investBtn;
    Button _locateBtn;

    Vector2 _sheetRestPos;
    bool _sheetPosCached;
    string _castleId;

    void Awake()
    {
        InstanceOrNull = this;
        BuildUiIfNeeded();
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (InstanceOrNull == this)
            InstanceOrNull = null;
    }

    public static void OpenCastle(string castleId)
    {
        if (string.IsNullOrWhiteSpace(castleId)) return;
        var inst = InstanceOrNull ?? UnityEngine.Object.FindObjectOfType<WorldMarketCastleSummarySheet>(true);
        if (inst == null)
        {
            WorldMarketCastleDetailPopup.OpenCastle(castleId);
            return;
        }

        inst._castleId = castleId.Trim();
        inst.RefreshBody();
        inst.ShowAnim();
    }

    void ShowAnim()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
        }

        if (_panelRt != null)
        {
            _panelRt.DOKill();
            if (!_sheetPosCached)
            {
                _sheetRestPos = _panelRt.anchoredPosition;
                _sheetPosCached = true;
            }

            float drop = Mathf.Max(Screen.height * 0.2f, 460f);
            _panelRt.anchoredPosition = _sheetRestPos + new Vector2(0f, -drop);
            _panelRt.DOAnchorPos(_sheetRestPos, 0.38f).SetEase(Ease.OutCubic).SetUpdate(true);
        }
    }

    public void Close()
    {
        if (_panelRt != null)
        {
            _panelRt.DOKill();
            if (_sheetPosCached)
                _panelRt.anchoredPosition = _sheetRestPos;
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        gameObject.SetActive(false);
        _castleId = null;
    }

    void RefreshBody()
    {
        BuildUiIfNeeded();
        var dm = DataManager.InstanceOrNull;
        if (dm == null || !dm.IsStateReady || string.IsNullOrWhiteSpace(_castleId))
        {
            Close();
            return;
        }

        dm.SyncCastleMarketPricesFromFormula(_castleId);
        dm.castleMasterDataMap.TryGetValue(_castleId, out var master);
        dm.castleStateDataMap.TryGetValue(_castleId, out var st);
        dm.TryGetLiveCastleState(_castleId, out var live);

        if (st == null && live == null)
        {
            Close();
            return;
        }

        bool hasLive = live != null;
        int population = hasLive ? live.currentPopulation : (st != null ? st.currentPopulation : 0);
        float quote = dm.EvaluateCastleQuoteForCastle(_castleId);
        bool isWar = hasLive ? live.isWar : (st != null && st.isWar);
        bool isDisaster = hasLive ? live.isDisaster : (st != null && st.isDisaster);
        bool isFavorable = hasLive ? live.isFavorableEvent : (st != null && st.isFavorableEvent);

        string disp = dm.GetCastleDisplayName(_castleId);
        if (string.IsNullOrWhiteSpace(disp) && master != null)
            disp = master.name;
        if (string.IsNullOrWhiteSpace(disp))
            disp = _castleId;

        if (_titleTmp != null)
            _titleTmp.text = disp;

        bool isHq = !string.IsNullOrWhiteSpace(dm.HomeCastleId)
                    && string.Equals(dm.HomeCastleId.Trim(), _castleId, StringComparison.Ordinal);
        if (_hqBadgeGo != null)
            _hqBadgeGo.SetActive(isHq);

        Grade g = dm.GetCastleRuntimeGrade(_castleId);
        if (_iconBg != null)
            _iconBg.color = GradeChipColor(g);
        if (_iconGlyphTmp != null)
        {
            string glyph = string.IsNullOrWhiteSpace(disp) ? "성" : disp.Trim();
            _iconGlyphTmp.text = glyph.Length > 0 ? glyph.Substring(0, 1) : "?";
        }

        if (_warBadgeGo != null)
            _warBadgeGo.SetActive(isWar);

        var series = dm.GetCastlePriceSeries7DayForUi(_castleId);
        DataManager.GetMinMaxFromPriceSeries(series, out float loPx, out float hiPx);
        if (_priceLineGraphic != null)
        {
            _priceLineGraphic.lineColor = series.Count >= 2 && series[^1] >= series[0]
                ? new Color(0.25f, 0.95f, 0.55f, 1f)
                : new Color(0.95f, 0.35f, 0.38f, 1f);
            _priceLineGraphic.fillTopColor = new Color(0.15f, 0.55f, 0.35f, 0.42f);
            _priceLineGraphic.fillBottomColor = new Color(0.04f, 0.06f, 0.08f, 0.2f);
            _priceLineGraphic.SetPrices(series);
            float wallSup = loPx * 0.9985f;
            float wallRes = hiPx * 1.0015f;
            _priceLineGraphic.SetPsychologicalWallPrices(wallSup, wallRes);
        }

        if (_trendBiasTmp != null)
        {
            bool bull = series.Count >= 2 && series[^1] >= series[0];
            _trendBiasTmp.text = bull ? "Bullish" : "Bearish";
            _trendBiasTmp.color = bull ? RiseColor : FallColor;
        }

        if (_rightPriceTmp != null)
            _rightPriceTmp.text = $"{Mathf.RoundToInt(quote):N0}";

        float pct = st != null ? dm.CalculateChangeRate24h(st) : 0f;
        bool up = pct > 0.02f;
        bool flat = Mathf.Abs(pct) < 0.02f;
        if (_rightChgTmp != null)
        {
            _rightChgTmp.text = flat ? "— 0.00%" : $"{(up ? "▲ " : "▼ ")}{Mathf.Abs(pct):F2}%";
            _rightChgTmp.color = flat ? new Color(0.65f, 0.68f, 0.74f) : (up ? RiseColor : FallColor);
        }

        long volTroop = dm.GetCastleTradingVolumeTroopProxy(_castleId);
        if (_volTmp != null)
            _volTmp.text = FormatVolK(volTroop) + " 兵";

        if (_high7Tmp != null)
            _high7Tmp.text = $"{Mathf.RoundToInt(hiPx):N0}";
        if (_low7Tmp != null)
            _low7Tmp.text = $"{Mathf.RoundToInt(loPx):N0}";

        dm.TryGetUserCastleStock(_castleId, out var userStock);
        bool hasStockFromSo = userStock != null && userStock.troopCount > 0;
        bool hasStockMap = st != null && st.IsUserInvested;
        bool hasStock = hasStockFromSo || hasStockMap;
        int troops = hasStockFromSo ? userStock.troopCount : (st != null ? st.userDeployedTroops : 0);

        int denom = Mathf.Max(1, population);
        float stakePct = hasStock && troops > 0
            ? Mathf.Clamp(troops / (float)denom * 100f, 0f, 100f)
            : 0f;

        if (_metricsStakeTmp != null)
            _metricsStakeTmp.text = $"{stakePct:F1}%";

        if (_metricsTroopsTmp != null)
        {
            _metricsTroopsTmp.gameObject.SetActive(hasStock);
            _metricsTroopsTmp.text = hasStock ? $"{troops:N0} 兵" : "";
        }

        string cause = dm.GetCastlePriceMovementCauseLabel(_castleId);
        if (_causeTmp != null)
        {
            bool has = !string.IsNullOrEmpty(cause);
            _causeTmp.gameObject.SetActive(has);
            _causeTmp.text = has ? $"[{cause}]" : "";
        }

        if (_eventStripTmp != null)
        {
            var bits = new List<string>(4);
            if (isWar) bits.Add("[전쟁]");
            if (isDisaster) bits.Add("[역병]");
            if (isFavorable) bits.Add("[풍년]");
            string ev = bits.Count > 0 ? string.Join(" ", bits) : "특이 이벤트 없음";
            _eventStripTmp.text = "최근 이슈 · " + ev;
        }

        var investMain = _investBtn != null ? _investBtn.transform.Find("LblMain")?.GetComponent<TextMeshProUGUI>() : null;
        var investSub = _investBtn != null ? _investBtn.transform.Find("LblSub")?.GetComponent<TextMeshProUGUI>() : null;
        if (investMain != null)
            investMain.text = hasStock ? "호가창 · 관리" : "투자 및 전장 진입";
        if (investSub != null)
            investSub.text = hasStock ? "MANAGE ORDERS" : "ENTER BATTLEFRONT";
    }

    static string FormatVolK(long v)
    {
        if (v >= 1000000L)
            return (v / 1000000d).ToString("0.0", CultureInfo.InvariantCulture) + "M";
        if (v >= 1000L)
            return (v / 1000d).ToString("0", CultureInfo.InvariantCulture) + "K";
        return v.ToString(CultureInfo.InvariantCulture);
    }

    static Color GradeChipColor(Grade grade)
    {
        switch (grade)
        {
            case Grade.SS: return new Color(0.95f, 0.55f, 0.2f, 1f);
            case Grade.S: return new Color(0.85f, 0.35f, 0.25f, 1f);
            case Grade.A: return new Color(0.28f, 0.52f, 0.95f, 1f);
            case Grade.B: return new Color(0.42f, 0.48f, 0.55f, 1f);
            case Grade.C: return new Color(0.38f, 0.42f, 0.48f, 1f);
            default: return new Color(0.48f, 0.50f, 0.54f, 1f);
        }
    }

    void BuildUiIfNeeded()
    {
        Transform legacy = transform.Find("SheetPanel");
        if (legacy != null && legacy.Find("ChartBandV2") == null)
        {
            Destroy(legacy.gameObject);
            _panelRt = null;
        }

        if (_panelRt != null && transform.Find("SheetPanel/ChartBandV2") != null)
            return;

        _canvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        var rt = transform as RectTransform;
        StretchFull(rt);

        var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image), typeof(Button));
        dim.transform.SetParent(transform, false);
        StretchFull(dim.GetComponent<RectTransform>());
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.52f);
        var dimBtn = dim.GetComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(Close);

        var sheet = new GameObject("SheetPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        sheet.transform.SetParent(transform, false);
        _panelRt = sheet.GetComponent<RectTransform>();
        _panelRt.anchorMin = new Vector2(0.03f, 0f);
        _panelRt.anchorMax = new Vector2(0.97f, 0f);
        _panelRt.pivot = new Vector2(0.5f, 0f);
        _panelRt.sizeDelta = new Vector2(0f, 540f);
        _panelRt.anchoredPosition = Vector2.zero;
        sheet.GetComponent<Image>().color = new Color(0.07f, 0.08f, 0.11f, 0.99f);
        var outline = sheet.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.72f, 0.2f, 0.28f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        var sv = sheet.GetComponent<VerticalLayoutGroup>();
        sv.padding = new RectOffset(16, 16, 14, 16);
        sv.spacing = 10;
        sv.childAlignment = TextAnchor.UpperLeft;
        sv.childControlWidth = true;
        sv.childForceExpandWidth = true;

        BuildHeader(sheet.transform);
        BuildChartBandV2(sheet.transform);
        BuildMetricsRow(sheet.transform);
        BuildCauseStrip(sheet.transform);
        BuildFooter(sheet.transform);
    }

    void BuildHeader(Transform sheet)
    {
        var header = new GameObject("HeaderRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        header.transform.SetParent(sheet, false);
        var hh = header.GetComponent<HorizontalLayoutGroup>();
        hh.childAlignment = TextAnchor.MiddleLeft;
        hh.spacing = 12;
        hh.childControlWidth = true;
        hh.childForceExpandWidth = true;
        header.GetComponent<LayoutElement>().minHeight = 68f;

        var iconWrap = new GameObject("IconWrap", typeof(RectTransform), typeof(LayoutElement));
        iconWrap.transform.SetParent(header.transform, false);
        var iconLe = iconWrap.GetComponent<LayoutElement>();
        iconLe.minWidth = iconLe.preferredWidth = 60f;
        iconLe.minHeight = iconLe.preferredHeight = 60f;

        var iconBgGo = new GameObject("IconBg", typeof(RectTransform), typeof(Image));
        iconBgGo.transform.SetParent(iconWrap.transform, false);
        StretchFull(iconBgGo.GetComponent<RectTransform>());
        _iconBg = iconBgGo.GetComponent<Image>();
        _iconBg.color = GradeChipColor(Grade.A);

        _warBadgeGo = new GameObject("WarBadge", typeof(RectTransform), typeof(TextMeshProUGUI));
        _warBadgeGo.transform.SetParent(iconWrap.transform, false);
        var wbRt = _warBadgeGo.GetComponent<RectTransform>();
        wbRt.anchorMin = new Vector2(1f, 1f);
        wbRt.anchorMax = new Vector2(1f, 1f);
        wbRt.pivot = new Vector2(1f, 1f);
        wbRt.anchoredPosition = new Vector2(6f, 6f);
        wbRt.sizeDelta = new Vector2(24f, 24f);
        var wbTmp = _warBadgeGo.GetComponent<TextMeshProUGUI>();
        wbTmp.text = "⚔";
        wbTmp.fontSize = 14;
        wbTmp.alignment = TextAlignmentOptions.Center;
        wbTmp.color = new Color(1f, 0.35f, 0.35f, 1f);
        _warBadgeGo.SetActive(false);

        var glyphGo = new GameObject("Glyph", typeof(RectTransform), typeof(TextMeshProUGUI));
        glyphGo.transform.SetParent(iconWrap.transform, false);
        StretchFull(glyphGo.GetComponent<RectTransform>());
        _iconGlyphTmp = glyphGo.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            _iconGlyphTmp.font = TMP_Settings.defaultFontAsset;
        _iconGlyphTmp.fontSize = 24;
        _iconGlyphTmp.fontStyle = FontStyles.Bold;
        _iconGlyphTmp.color = Color.white;
        _iconGlyphTmp.alignment = TextAlignmentOptions.Center;

        var titleCol = new GameObject("TitleCol", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        titleCol.transform.SetParent(header.transform, false);
        titleCol.GetComponent<LayoutElement>().flexibleWidth = 1f;

        _titleTmp = CreateTmp(titleCol.transform, "Title", "", 21, FontStyles.Bold, TextAlignmentOptions.Left);
        _titleTmp.color = Color.white;

        _hqBadgeGo = new GameObject("HqBadge", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        _hqBadgeGo.transform.SetParent(titleCol.transform, false);
        var hqTmp = _hqBadgeGo.GetComponent<TextMeshProUGUI>();
        hqTmp.text = "본영";
        hqTmp.fontSize = 12;
        hqTmp.fontStyle = FontStyles.Bold;
        hqTmp.color = new Color(1f, 0.62f, 0.2f, 1f);
        _hqBadgeGo.SetActive(false);

        var closeGo = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        closeGo.transform.SetParent(header.transform, false);
        closeGo.GetComponent<LayoutElement>().minWidth = 44f;
        closeGo.GetComponent<Image>().color = new Color(0.14f, 0.15f, 0.17f, 0.96f);
        _closeBtn = closeGo.GetComponent<Button>();
        var closeLbl = CreateTmp(closeGo.transform, "X", "✕", 20, FontStyles.Bold, TextAlignmentOptions.Center);
        StretchFull(closeLbl.rectTransform);
        closeLbl.color = new Color(0.75f, 0.78f, 0.82f);
        _closeBtn.onClick.AddListener(Close);
    }

    void BuildChartBandV2(Transform sheet)
    {
        var band = new GameObject("ChartBandV2", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        band.transform.SetParent(sheet, false);
        band.GetComponent<LayoutElement>().minHeight = 172f;
        var bh = band.GetComponent<HorizontalLayoutGroup>();
        bh.spacing = 12;
        bh.childAlignment = TextAnchor.UpperLeft;
        bh.childControlWidth = true;
        bh.childForceExpandWidth = true;

        var chartCard = new GameObject("ChartCard", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        chartCard.transform.SetParent(band.transform, false);
        chartCard.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.96f);
        chartCard.GetComponent<LayoutElement>().flexibleWidth = 1.15f;
        var cv = chartCard.GetComponent<VerticalLayoutGroup>();
        cv.padding = new RectOffset(10, 10, 8, 10);
        cv.spacing = 6;
        cv.childControlWidth = true;
        cv.childForceExpandWidth = true;

        var trendRow = new GameObject("TrendRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        trendRow.transform.SetParent(chartCard.transform, false);
        trendRow.GetComponent<LayoutElement>().minHeight = 22f;
        var tr = trendRow.GetComponent<HorizontalLayoutGroup>();
        tr.childAlignment = TextAnchor.MiddleLeft;
        tr.spacing = 8;
        tr.childControlWidth = true;
        tr.childForceExpandWidth = true;

        var t7 = CreateTmp(trendRow.transform, "TrendLbl", "7D TREND", 12, FontStyles.Bold, TextAlignmentOptions.Left);
        t7.color = new Color(0.55f, 0.58f, 0.64f);
        var t7le = t7.gameObject.AddComponent<LayoutElement>();
        t7le.flexibleWidth = 1f;

        _trendBiasTmp = CreateTmp(trendRow.transform, "Bias", "—", 12, FontStyles.Bold, TextAlignmentOptions.Right);
        _trendBiasTmp.color = RiseColor;
        var biasLe = _trendBiasTmp.gameObject.AddComponent<LayoutElement>();
        biasLe.flexibleWidth = 0f;
        biasLe.minWidth = 72f;

        var chartHost = new GameObject("ChartHost", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        chartHost.transform.SetParent(chartCard.transform, false);
        chartHost.GetComponent<Image>().color = new Color(0.03f, 0.04f, 0.06f, 1f);
        chartHost.GetComponent<LayoutElement>().minHeight = 118f;
        var hostRt = chartHost.GetComponent<RectTransform>();
        hostRt.sizeDelta = new Vector2(0f, 118f);

        var chartGo = new GameObject("PriceLine", typeof(RectTransform), typeof(UIPriceLine7DayGraphic));
        chartGo.transform.SetParent(chartHost.transform, false);
        StretchFull(chartGo.GetComponent<RectTransform>());
        _priceLineGraphic = chartGo.GetComponent<UIPriceLine7DayGraphic>();

        var rightCol = new GameObject("RightQuoteCol", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        rightCol.transform.SetParent(band.transform, false);
        rightCol.GetComponent<LayoutElement>().flexibleWidth = 0.95f;
        var rv = rightCol.GetComponent<VerticalLayoutGroup>();
        rv.spacing = 10;
        rv.padding = new RectOffset(8, 8, 4, 4);
        rv.childControlWidth = true;
        rv.childForceExpandWidth = true;

        var priceCard = CreateMiniCard(rightCol.transform, "PriceMini");
        CreateCaption(priceCard.transform, "CURRENT PRICE");
        _rightPriceTmp = CreateTmp(priceCard.transform, "Px", "0", 28, FontStyles.Bold, TextAlignmentOptions.Left);
        _rightPriceTmp.color = new Color(1f, 0.85f, 0.28f, 1f);
        _rightChgTmp = CreateTmp(priceCard.transform, "Chg", "+0.00%", 16, FontStyles.Bold, TextAlignmentOptions.Left);

        var volCard = CreateMiniCard(rightCol.transform, "VolCard");
        CreateCaption(volCard.transform, "TRADING VOL.");
        _volTmp = CreateTmp(volCard.transform, "Vol", "0K 兵", 17, FontStyles.Bold, TextAlignmentOptions.Left);
        _volTmp.color = new Color(0.82f, 0.86f, 0.92f, 1f);
    }

    static GameObject CreateMiniCard(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0.09f, 0.10f, 0.13f, 0.98f);
        var v = go.GetComponent<VerticalLayoutGroup>();
        v.spacing = 4;
        v.padding = new RectOffset(10, 10, 10, 10);
        v.childControlWidth = true;
        v.childForceExpandWidth = true;
        go.GetComponent<LayoutElement>().minHeight = 76f;
        return go;
    }

    void BuildMetricsRow(Transform sheet)
    {
        var row = new GameObject("MetricsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(sheet, false);
        row.GetComponent<LayoutElement>().minHeight = 72f;
        var h = row.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 8;
        h.childAlignment = TextAnchor.UpperLeft;
        h.childControlWidth = true;
        h.childForceExpandWidth = true;

        var highCell = CreateMetricCell(row.transform, "HighCell", "7D HIGH", RiseColor, out _high7Tmp);
        highCell.GetComponent<LayoutElement>().flexibleWidth = 1f;
        var lowCell = CreateMetricCell(row.transform, "LowCell", "7D LOW", FallColor, out _low7Tmp);
        lowCell.GetComponent<LayoutElement>().flexibleWidth = 1f;
        var stakeCell = CreateStakeMetricCell(row.transform);
        stakeCell.GetComponent<LayoutElement>().flexibleWidth = 1f;
    }

    GameObject CreateMetricCell(Transform parent, string name, string cap, Color numCol, out TextMeshProUGUI valTmp)
    {
        var cell = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        cell.transform.SetParent(parent, false);
        cell.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 0.98f);
        var v = cell.GetComponent<VerticalLayoutGroup>();
        v.spacing = 4;
        v.padding = new RectOffset(8, 8, 8, 8);
        v.childControlWidth = true;
        CreateCaption(cell.transform, cap);
        valTmp = CreateTmp(cell.transform, "Val", "—", 18, FontStyles.Bold, TextAlignmentOptions.Left);
        valTmp.color = numCol;
        return cell;
    }

    GameObject CreateStakeMetricCell(Transform parent)
    {
        var cell = new GameObject("StakeCell", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        cell.transform.SetParent(parent, false);
        cell.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 0.98f);
        var v = cell.GetComponent<VerticalLayoutGroup>();
        v.spacing = 2;
        v.padding = new RectOffset(8, 8, 8, 8);
        v.childControlWidth = true;
        CreateCaption(cell.transform, "MY STAKE");
        _metricsStakeTmp = CreateTmp(cell.transform, "StakePct", "0%", 18, FontStyles.Bold, TextAlignmentOptions.Left);
        _metricsStakeTmp.color = new Color(0.35f, 0.85f, 1f, 1f);
        _metricsTroopsTmp = CreateTmp(cell.transform, "StakeTroop", "", 12, FontStyles.Normal, TextAlignmentOptions.Left);
        _metricsTroopsTmp.color = new Color(0.55f, 0.58f, 0.64f);
        return cell;
    }

    void BuildCauseStrip(Transform sheet)
    {
        var strip = new GameObject("CauseStrip", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        strip.transform.SetParent(sheet, false);
        strip.GetComponent<LayoutElement>().minHeight = 36f;
        var v = strip.GetComponent<VerticalLayoutGroup>();
        v.spacing = 4;
        v.childControlWidth = true;

        _causeTmp = CreateTmp(strip.transform, "Cause", "", 13, FontStyles.Bold, TextAlignmentOptions.Left);
        _causeTmp.color = new Color(0.62f, 0.66f, 0.72f);

        _eventStripTmp = CreateTmp(strip.transform, "Events", "", 12, FontStyles.Normal, TextAlignmentOptions.Left);
        _eventStripTmp.color = new Color(0.48f, 0.52f, 0.58f);
    }

    void BuildFooter(Transform sheet)
    {
        var footer = new GameObject("FooterRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        footer.transform.SetParent(sheet, false);
        footer.GetComponent<LayoutElement>().minHeight = 62f;
        var fh = footer.GetComponent<HorizontalLayoutGroup>();
        fh.spacing = 10;
        fh.childAlignment = TextAnchor.MiddleCenter;
        fh.childControlWidth = true;
        fh.childForceExpandWidth = true;

        _investBtn = CreateInvestBattleButton(footer.transform);
        _investBtn.onClick.AddListener(OnInvestClicked);

        _locateBtn = CreateFooterIconButton(footer.transform);
        _locateBtn.onClick.AddListener(OnLocateClicked);
    }

    Button CreateInvestBattleButton(Transform parent)
    {
        var go = new GameObject("InvestBattle", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var le = go.GetComponent<LayoutElement>();
        le.flexibleWidth = 1f;
        le.minHeight = 56f;
        go.GetComponent<Image>().color = new Color(0.42f, 0.30f, 0.06f, 1f);
        var btn = go.GetComponent<Button>();
        var vl = go.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 2;
        vl.childAlignment = TextAnchor.MiddleCenter;
        vl.childControlWidth = true;
        vl.childForceExpandWidth = true;
        vl.padding = new RectOffset(8, 8, 8, 8);

        var main = CreateTmp(go.transform, "LblMain", "투자 및 전장 진입", 16, FontStyles.Bold, TextAlignmentOptions.Center);
        main.color = Color.white;
        var sub = CreateTmp(go.transform, "LblSub", "ENTER BATTLEFRONT", 11, FontStyles.Normal, TextAlignmentOptions.Center);
        sub.color = new Color(0.92f, 0.82f, 0.55f, 0.95f);

        if (btn.GetComponent<WorldMarketGoldButtonShimmer>() == null)
            btn.gameObject.AddComponent<WorldMarketGoldButtonShimmer>();
        return btn;
    }

    Button CreateFooterIconButton(Transform parent)
    {
        var go = new GameObject("LocateBtn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var le = go.GetComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = 56f;
        le.minHeight = 52f;
        go.GetComponent<Image>().color = new Color(0.16f, 0.17f, 0.20f, 1f);
        var btn = go.GetComponent<Button>();
        var vl = go.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 0;
        vl.childAlignment = TextAnchor.MiddleCenter;
        vl.childControlWidth = true;
        vl.padding = new RectOffset(4, 4, 6, 6);
        var glyph = CreateTmp(go.transform, "Glyph", "⌖", 18, FontStyles.Bold, TextAlignmentOptions.Center);
        glyph.color = new Color(0.78f, 0.82f, 0.88f);
        var loc = CreateTmp(go.transform, "LocLbl", "현재지", 10, FontStyles.Bold, TextAlignmentOptions.Center);
        loc.color = new Color(0.55f, 0.58f, 0.62f);
        return btn;
    }

    static void CreateCaption(Transform parent, string txt)
    {
        var t = CreateTmp(parent, "Cap", txt, 11, FontStyles.Bold, TextAlignmentOptions.Left);
        t.color = new Color(0.48f, 0.52f, 0.58f);
    }

    static TextMeshProUGUI CreateTmp(Transform parent, string name, string text, float size, FontStyles style,
        TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.color = Color.white;
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, size + 6f);
        return tmp;
    }

    void OnInvestClicked()
    {
        if (string.IsNullOrWhiteSpace(_castleId)) return;
        string id = _castleId.Trim();
        Close();
        WorldMarketCastleDetailPopup.OpenCastle(id);
    }

    void OnLocateClicked() => Close();

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary><see cref="WorldMarketCastleDetailPopup.EnsureUnderWorldMarketRoot"/>와 동일 위치에 요약 시트를 붙입니다.</summary>
    public static void EnsureUnderWorldMarketRoot(Transform worldMarketRoot)
    {
        if (worldMarketRoot == null) return;
        if (worldMarketRoot.GetComponentInChildren<WorldMarketCastleSummarySheet>(true) != null)
            return;

        var go = new GameObject("CastleSummarySheet", typeof(RectTransform), typeof(LayoutElement),
            typeof(WorldMarketCastleSummarySheet));
        var rt = go.GetComponent<RectTransform>();
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        go.transform.SetParent(worldMarketRoot, false);
        go.transform.SetAsLastSibling();
        StretchFull(rt);
    }
}
