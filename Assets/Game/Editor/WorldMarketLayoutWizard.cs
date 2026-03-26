#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 천하탭(MTS) 레이아웃 자동 생성.
/// 메뉴: StockThreeKingdoms/천하/천하탭 만들기 (MTS Layout)
/// - 상단: Faction Market Share(가로 스택 막대)
/// - 중단: NewsTicker + Castle Stocks 리스트(ScrollRect + 템플릿 2종)
/// - 팝업: CityDetailPanel(기본 비활성)
/// </summary>
public static class WorldMarketLayoutWizard
{
    const string MenuPath = "StockThreeKingdoms/천하/천하탭 만들기 (MTS Layout)";
    const float ContentTopInset = 160f;    // GlobalUI TopBar(140) + 여유
    const float ContentBottomInset = 180f; // GlobalUI BottomTabBar(160) + 여유

    [MenuItem(MenuPath, false, 0)]
    public static void CreateWorldMarketLayout()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        RectTransform parent = canvas.transform as RectTransform;
        parent = EnsureContentRoot(parent);

        var existing = GameObject.Find("WorldMarketRoot");
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("천하탭", "씬에 WorldMarketRoot가 이미 있습니다. 새로 만들까요?", "새로 생성", "취소"))
                return;
        }

        GameObject root = new GameObject("WorldMarketRoot", typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(root, "Create World Market Layout");
        root.transform.SetParent(parent, false);

        var rootImg = root.GetComponent<Image>();
        rootImg.color = new Color(0.05f, 0.06f, 0.08f, 0.98f);

        var rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0, 0);
        rootRt.anchorMax = new Vector2(1, 1);
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        var vlg = root.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 14f;
        vlg.padding = new RectOffset(18, 18, 18, 18);
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        CreateFactionMarketSharePanel(root.transform);
        CreateCastleStocksPanel(root.transform);
        CreateCityDetailPanel(root.transform);

        Selection.activeGameObject = root;
        Debug.Log("[WorldMarketLayoutWizard] 천하탭(MTS) 레이아웃 생성 완료. 씬 저장 후 런타임 바인딩 스크립트를 연결하세요.");
    }

    static RectTransform EnsureContentRoot(RectTransform canvasRoot)
    {
        var t = canvasRoot.Find("ContentRoot");
        RectTransform rt;
        if (t == null)
        {
            var go = new GameObject("ContentRoot", typeof(RectTransform));
            go.transform.SetParent(canvasRoot, false);
            rt = go.GetComponent<RectTransform>();
            StretchFull(rt);
            rt.offsetMin = new Vector2(20f, ContentBottomInset);
            rt.offsetMax = new Vector2(-20f, -ContentTopInset);
        }
        else
            rt = t as RectTransform;

        return rt;
    }

    static void CreateFactionMarketSharePanel(Transform parent)
    {
        GameObject panel = CreatePanel(parent, "FactionMarketSharePanel", "Faction Market Share");
        panel.GetComponent<LayoutElement>().minHeight = 132f;

        var body = new GameObject("Body", typeof(RectTransform));
        body.transform.SetParent(panel.transform, false);
        var bodyV = body.AddComponent<VerticalLayoutGroup>();
        bodyV.spacing = 8f;
        bodyV.childAlignment = TextAnchor.UpperLeft;
        bodyV.childControlWidth = true;
        bodyV.childControlHeight = true;
        bodyV.childForceExpandWidth = true;
        bodyV.childForceExpandHeight = false;

        Sprite knob = TryGetUiKnobSprite();

        // 가로 100% 스택 막대 + Segments(앵커는 런타임에서 비율로 설정)
        var barRoot = new GameObject("FactionShareBar", typeof(RectTransform), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(barRoot, "FactionShareBar");
        barRoot.transform.SetParent(body.transform, false);
        var barLe = barRoot.GetComponent<LayoutElement>();
        barLe.minHeight = 36f;
        barLe.preferredHeight = 36f;
        barLe.flexibleWidth = 1f;

        var segments = new GameObject("Segments", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(segments, "BarSegments");
        segments.transform.SetParent(barRoot.transform, false);
        StretchFull(segments.GetComponent<RectTransform>());

        Image imgWei = CreateBarStackSegment(segments.transform, "SegmentWei", new Color(0.20f, 0.55f, 0.90f), knob);
        Image imgShu = CreateBarStackSegment(segments.transform, "SegmentShu", new Color(0.35f, 0.80f, 0.55f), knob);
        Image imgWu = CreateBarStackSegment(segments.transform, "SegmentWu", new Color(0.95f, 0.40f, 0.35f), knob);
        Image imgOth = CreateBarStackSegment(segments.transform, "SegmentOthers", new Color(0.55f, 0.58f, 0.66f), knob);

        // Legend — 막대 바로 아래 한 줄
        var legend = new GameObject("Legend", typeof(RectTransform), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(legend, "Legend");
        legend.transform.SetParent(body.transform, false);
        var legendLe = legend.GetComponent<LayoutElement>();
        legendLe.minHeight = 40f;
        legendLe.flexibleWidth = 1f;

        var legendH = legend.AddComponent<HorizontalLayoutGroup>();
        legendH.spacing = 6f;
        legendH.childAlignment = TextAnchor.MiddleLeft;
        legendH.childControlWidth = true;
        legendH.childControlHeight = true;
        legendH.childForceExpandWidth = true;
        legendH.childForceExpandHeight = true;

        CreateLegendRow(legend.transform, "WEI", new Color(0.20f, 0.55f, 0.90f));
        CreateLegendRow(legend.transform, "SHU", new Color(0.35f, 0.80f, 0.55f));
        CreateLegendRow(legend.transform, "WU", new Color(0.95f, 0.40f, 0.35f));
        CreateLegendRow(legend.transform, "OTHERS", new Color(0.55f, 0.58f, 0.66f));

        WireWorldMarketPieChartUI(barRoot, legend.transform, imgWei, imgShu, imgWu, imgOth);
    }

    static Sprite TryGetUiKnobSprite()
    {
        var s = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        if (s != null) return s;
        return Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
    }

    static Image CreateBarStackSegment(Transform parent, string name, Color color, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        StretchFull(go.GetComponent<RectTransform>());
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.type = Image.Type.Simple;
        img.raycastTarget = false;
        return img;
    }

    static void WireWorldMarketPieChartUI(GameObject pieRoot, Transform legend, Image wei, Image shu, Image wu, Image oth)
    {
        var ui = pieRoot.GetComponent<WorldMarketPieChartUI>();
        if (ui == null) ui = pieRoot.AddComponent<WorldMarketPieChartUI>();
        var so = new SerializedObject(ui);
        so.FindProperty("segmentWei").objectReferenceValue = wei;
        so.FindProperty("segmentShu").objectReferenceValue = shu;
        so.FindProperty("segmentWu").objectReferenceValue = wu;
        so.FindProperty("segmentOthers").objectReferenceValue = oth;
        so.FindProperty("textWei").objectReferenceValue = legend.Find("WEIRow/Label")?.GetComponent<TextMeshProUGUI>();
        so.FindProperty("textShu").objectReferenceValue = legend.Find("SHURow/Label")?.GetComponent<TextMeshProUGUI>();
        so.FindProperty("textWu").objectReferenceValue = legend.Find("WURow/Label")?.GetComponent<TextMeshProUGUI>();
        so.FindProperty("textOthers").objectReferenceValue = legend.Find("OTHERSRow/Label")?.GetComponent<TextMeshProUGUI>();
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void CreateLegendRow(Transform parent, string label, Color c)
    {
        var row = new GameObject($"{label}Row", typeof(RectTransform), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        var rowLe = row.GetComponent<LayoutElement>();
        rowLe.minHeight = 34f;
        rowLe.flexibleWidth = 1f;
        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 10f;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = false;
        h.childForceExpandWidth = false;

        var dot = new GameObject("Dot", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        dot.transform.SetParent(row.transform, false);
        dot.GetComponent<Image>().color = c;
        dot.GetComponent<Image>().raycastTarget = false;
        var dle = dot.GetComponent<LayoutElement>();
        dle.preferredWidth = 18f;
        dle.preferredHeight = 18f;

        CreateTMP(row.transform, "Label", $"{label}: --%", 24, FontStyles.Bold, TextAlignmentOptions.Left);
    }

    static void CreateCastleStocksPanel(Transform parent)
    {
        GameObject panel = CreatePanel(parent, "CastleStocksPanel", "Castle Stocks");
        panel.GetComponent<LayoutElement>().flexibleHeight = 1f;
        panel.GetComponent<LayoutElement>().minHeight = 520f;

        // News ticker placeholder
        var ticker = new GameObject("NewsTicker", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        ticker.transform.SetParent(panel.transform, false);
        ticker.GetComponent<Image>().color = new Color(0.12f, 0.10f, 0.08f, 0.96f);
        var tLe = ticker.GetComponent<LayoutElement>();
        tLe.minHeight = 72f;
        tLe.preferredHeight = 84f;
        CreateTMP(ticker.transform, "TickerText", "[NEWS] 이벤트/점령/급등락 텍스트가 여기로 흐릅니다.", 24, FontStyles.Normal, TextAlignmentOptions.Left, pad: new Vector4(18, 0, 0, 0));

        // 필터 칩(전체 / 내 투자 / 전쟁 중 / 등급순) UI 연결용 자리
        var filterRow = new GameObject("FilterChipsReserved", typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
        filterRow.transform.SetParent(panel.transform, false);
        var filterLe = filterRow.GetComponent<LayoutElement>();
        filterLe.minHeight = 48f;
        filterLe.preferredHeight = 52f;
        var filterH = filterRow.GetComponent<HorizontalLayoutGroup>();
        filterH.spacing = 10f;
        filterH.padding = new RectOffset(4, 4, 4, 4);
        filterH.childAlignment = TextAnchor.MiddleLeft;
        filterH.childControlWidth = false;
        filterH.childForceExpandWidth = false;
        Color hintCol = new Color(0.48f, 0.52f, 0.58f, 1f);
        CreateTMP(filterRow.transform, "FilterHint", "필터 칩: 전체 · 내 투자 · 전쟁 중 · 등급순 (버튼 연결 예정)", 20, FontStyles.Italic, TextAlignmentOptions.Left, color: hintCol);

        // ScrollRect list
        var scrollGo = new GameObject("CastleStocksScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
        scrollGo.transform.SetParent(panel.transform, false);
        scrollGo.GetComponent<Image>().color = new Color(0, 0, 0, 0.08f);
        var sLe = scrollGo.GetComponent<LayoutElement>();
        sLe.flexibleHeight = 1f;
        sLe.minHeight = 520f;

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollGo.transform, false);
        var vpRt = viewport.GetComponent<RectTransform>();
        StretchFull(vpRt);
        viewport.GetComponent<Image>().color = new Color(1, 1, 1, 0.001f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0.5f, 1);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0, 1200);

        var contentV = content.AddComponent<VerticalLayoutGroup>();
        contentV.spacing = 10f;
        contentV.padding = new RectOffset(0, 0, 0, 0);
        contentV.childAlignment = TextAnchor.UpperCenter;
        contentV.childControlWidth = true;
        contentV.childForceExpandWidth = true;
        contentV.childForceExpandHeight = false;

        var csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var sr = scrollGo.GetComponent<ScrollRect>();
        sr.viewport = vpRt;
        sr.content = contentRt;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;

        // 카드 템플릿(비활성) → 런타임 WorldMarketCastleVirtualList가 풀링
        GameObject template = CreateCastleStockCardTemplate(content.transform);
        CreateNewsRowTemplate(content.transform);

        var vlist = scrollGo.AddComponent<WorldMarketCastleVirtualList>();
        var vso = new SerializedObject(vlist);
        vso.FindProperty("scrollRect").objectReferenceValue = sr;
        vso.FindProperty("content").objectReferenceValue = contentRt;
        vso.FindProperty("cellTemplate").objectReferenceValue = template;
        vso.FindProperty("cellStride").floatValue = 188f;
        vso.FindProperty("filterChipsReservedArea").objectReferenceValue = filterRow.GetComponent<RectTransform>();
        vso.FindProperty("listHeaderText").objectReferenceValue = panel.transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
        vso.ApplyModifiedPropertiesWithoutUndo();

        Object.DestroyImmediate(csf);
        Object.DestroyImmediate(contentV);
    }

    static GameObject CreateCastleStockCardTemplate(Transform parent)
    {
        var card = new GameObject("CastleStockCardTemplate", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(WorldMarketCastleCardView), typeof(Outline));
        card.transform.SetParent(parent, false);
        var img = card.GetComponent<Image>();
        img.color = new Color(0.10f, 0.12f, 0.16f, 0.98f);
        var le = card.GetComponent<LayoutElement>();
        le.minHeight = 172f;

        var outline = card.GetComponent<Outline>();
        outline.effectColor = new Color(1f, 0.78f, 0.22f, 0.92f);
        outline.effectDistance = new Vector2(4f, -4f);
        outline.useGraphicAlpha = false;
        outline.enabled = false;

        var gloss = new GameObject("GlossOverlay", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        gloss.transform.SetParent(card.transform, false);
        gloss.transform.SetAsFirstSibling();
        var glossRt = gloss.GetComponent<RectTransform>();
        StretchFull(glossRt);
        var gImg = gloss.GetComponent<Image>();
        gImg.color = new Color(1f, 0.94f, 0.78f, 0.09f);
        gImg.raycastTarget = false;
        gloss.GetComponent<LayoutElement>().ignoreLayout = true;
        gloss.SetActive(false);

        var h = card.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(14, 14, 12, 12);
        h.spacing = 12f;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = false;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = false;

        var left = new GameObject("Left", typeof(RectTransform), typeof(LayoutElement));
        left.transform.SetParent(card.transform, false);
        left.GetComponent<LayoutElement>().flexibleWidth = 1f;
        var leftV = left.AddComponent<VerticalLayoutGroup>();
        leftV.spacing = 5f;
        leftV.childAlignment = TextAnchor.UpperLeft;
        leftV.childControlWidth = true;
        leftV.childForceExpandWidth = true;
        leftV.childForceExpandHeight = false;

        CreateTMP(left.transform, "GradeBadge", "SS", 26, FontStyles.Bold, TextAlignmentOptions.Left, color: new Color(1f, 0.82f, 0.35f, 1f));

        CreateTMP(left.transform, "CastleName", "Luoyang (C01)", 28, FontStyles.Bold, TextAlignmentOptions.Left);
        Color sub = new Color(0.55f, 0.58f, 0.64f, 1f);
        CreateTMP(left.transform, "CastleIdLine", "Region", 20, FontStyles.Normal, TextAlignmentOptions.Left, color: sub);

        var buyRow = new GameObject("BuyRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        buyRow.transform.SetParent(left.transform, false);
        var br = buyRow.GetComponent<HorizontalLayoutGroup>();
        br.spacing = 10f;
        br.childAlignment = TextAnchor.MiddleLeft;
        br.childControlWidth = false;
        br.childForceExpandWidth = false;
        buyRow.GetComponent<LayoutElement>().minHeight = 46f;

        CreateTMP(buyRow.transform, "BuyLabel", "매수", 22, FontStyles.Normal, TextAlignmentOptions.Left, color: sub);

        var buyBg = new GameObject("BuyPriceBg", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        buyBg.transform.SetParent(buyRow.transform, false);
        buyBg.GetComponent<Image>().color = new Color(0.18f, 0.42f, 0.28f, 0.95f);
        buyBg.GetComponent<Image>().raycastTarget = false;
        buyBg.GetComponent<LayoutElement>().minWidth = 168f;
        buyBg.GetComponent<LayoutElement>().preferredHeight = 42f;
        var bgrt = buyBg.GetComponent<RectTransform>();
        bgrt.sizeDelta = new Vector2(200f, 42f);

        var buyTmp = CreateTMP(buyBg.transform, "BuyPrice", "1,250", 26, FontStyles.Bold, TextAlignmentOptions.Center);
        buyTmp.margin = Vector4.zero;

        var sentRow = new GameObject("SentRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        sentRow.transform.SetParent(left.transform, false);
        var srH = sentRow.GetComponent<HorizontalLayoutGroup>();
        srH.spacing = 8f;
        srH.childAlignment = TextAnchor.MiddleLeft;
        CreateTMP(sentRow.transform, "Arrow", "▲", 24, FontStyles.Bold, TextAlignmentOptions.Left);
        CreateTMP(sentRow.transform, "ChangePct", "심리 +0.0 pts", 22, FontStyles.Normal, TextAlignmentOptions.Left);

        var invRow = new GameObject("InvestRow", typeof(RectTransform), typeof(VerticalLayoutGroup));
        invRow.transform.SetParent(left.transform, false);
        var iv = invRow.GetComponent<VerticalLayoutGroup>();
        iv.spacing = 2f;
        iv.childAlignment = TextAnchor.UpperLeft;
        iv.childControlWidth = true;
        iv.childForceExpandWidth = true;
        CreateTMP(invRow.transform, "TroopsLine", "내 병력: —", 22, FontStyles.Normal, TextAlignmentOptions.Left, color: new Color(0.85f, 0.88f, 0.93f, 1f));
        CreateTMP(invRow.transform, "RoiLine", "수익률: —", 22, FontStyles.Normal, TextAlignmentOptions.Left);
        CreateTMP(invRow.transform, "StakeLine", "", 20, FontStyles.Normal, TextAlignmentOptions.Left, color: sub);

        var gov = new GameObject("Governor", typeof(RectTransform), typeof(LayoutElement));
        gov.transform.SetParent(card.transform, false);
        gov.GetComponent<LayoutElement>().preferredWidth = 112f;

        var portrait = new GameObject("Portrait", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        portrait.transform.SetParent(gov.transform, false);
        portrait.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.24f, 1f);
        portrait.GetComponent<Image>().raycastTarget = false;
        portrait.GetComponent<LayoutElement>().preferredWidth = 100f;
        portrait.GetComponent<LayoutElement>().preferredHeight = 120f;
        var prt = portrait.GetComponent<RectTransform>();
        prt.sizeDelta = new Vector2(100f, 120f);

        var pIni = new GameObject("PortraitInitial", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        pIni.transform.SetParent(gov.transform, false);
        StretchFull(pIni.GetComponent<RectTransform>());
        var pit = pIni.GetComponent<TextMeshProUGUI>();
        pit.text = "조";
        pit.fontSize = 34;
        pit.fontStyle = FontStyles.Bold;
        pit.alignment = TextAlignmentOptions.Center;
        pit.color = new Color(0.88f, 0.90f, 0.94f, 1f);
        pit.raycastTarget = false;

        card.SetActive(false);
        return card;
    }

    static void CreateNewsRowTemplate(Transform parent)
    {
        var row = new GameObject("NewsRowTemplate", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<Image>().color = new Color(0.10f, 0.08f, 0.07f, 0.92f);
        row.GetComponent<LayoutElement>().minHeight = 84f;

        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(16, 16, 12, 12);
        h.spacing = 12f;
        h.childAlignment = TextAnchor.MiddleLeft;

        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        icon.transform.SetParent(row.transform, false);
        icon.GetComponent<Image>().color = new Color(0.85f, 0.70f, 0.20f, 1f);
        icon.GetComponent<Image>().raycastTarget = false;
        var ile = icon.GetComponent<LayoutElement>();
        ile.preferredWidth = 44f;
        ile.preferredHeight = 44f;

        CreateTMP(row.transform, "Text", "[URGENT] Cao Cao's army occupies Luoyang! Value multiplier up 1.5x.", 24, FontStyles.Normal, TextAlignmentOptions.Left);

        row.SetActive(false);
    }

    static void CreateCityDetailPanel(Transform parent)
    {
        var modal = new GameObject("CityDetailPanel", typeof(RectTransform), typeof(Image));
        modal.transform.SetParent(parent, false);
        StretchFull(modal.GetComponent<RectTransform>());
        modal.GetComponent<Image>().color = new Color(0, 0, 0, 0.55f);

        var modalBtn = modal.AddComponent<Button>();
        modalBtn.transition = Selectable.Transition.None;

        var modalLe = modal.AddComponent<LayoutElement>();
        modalLe.ignoreLayout = true;

        // Panel
        var panel = new GameObject("PanelRoot", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(modal.transform, false);
        var pRt = panel.GetComponent<RectTransform>();
        pRt.anchorMin = new Vector2(0, 0);
        pRt.anchorMax = new Vector2(1, 0.74f);
        pRt.pivot = new Vector2(0.5f, 0);
        pRt.offsetMin = Vector2.zero;
        pRt.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.12f, 0.98f);

        var pv = panel.AddComponent<VerticalLayoutGroup>();
        pv.padding = new RectOffset(22, 22, 18, 18);
        pv.spacing = 14f;
        pv.childControlWidth = true;
        pv.childForceExpandWidth = true;
        pv.childForceExpandHeight = false;

        // Header
        var header = new GameObject("Header", typeof(RectTransform), typeof(LayoutElement));
        header.transform.SetParent(panel.transform, false);
        header.GetComponent<LayoutElement>().minHeight = 120f;
        var hh = header.AddComponent<HorizontalLayoutGroup>();
        hh.spacing = 14f;
        hh.childAlignment = TextAnchor.MiddleLeft;
        hh.childControlWidth = true;
        hh.childForceExpandWidth = true;

        var titleBox = new GameObject("TitleBox", typeof(RectTransform), typeof(LayoutElement));
        titleBox.transform.SetParent(header.transform, false);
        titleBox.GetComponent<LayoutElement>().flexibleWidth = 1f;
        var tv = titleBox.AddComponent<VerticalLayoutGroup>();
        tv.spacing = 6f;
        tv.childAlignment = TextAnchor.MiddleLeft;
        CreateTMP(titleBox.transform, "CastleTitle", "Luoyang (SS)", 44, FontStyles.Bold, TextAlignmentOptions.Left);
        CreateTMP(titleBox.transform, "BuyLine", "Buy Price 1,250  ▲ 5.2%", 30, FontStyles.Bold, TextAlignmentOptions.Left, color: new Color(0.50f, 0.95f, 0.60f));

        // Chart panel
        var chart = new GameObject("SentimentChart", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        chart.transform.SetParent(panel.transform, false);
        chart.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 0.90f);
        chart.GetComponent<LayoutElement>().minHeight = 260f;
        CreateTMP(chart.transform, "ChartTitle", "Luoyang Sentiment Value (7-Day)", 26, FontStyles.Bold, TextAlignmentOptions.Left, pad: new Vector4(16, 0, 0, 0));

        // Executive Info
        var exec = new GameObject("ExecutiveInfo", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        exec.transform.SetParent(panel.transform, false);
        exec.GetComponent<Image>().color = new Color(0.14f, 0.13f, 0.10f, 0.92f);
        exec.GetComponent<LayoutElement>().minHeight = 240f;

        var eh = exec.AddComponent<HorizontalLayoutGroup>();
        eh.padding = new RectOffset(16, 16, 16, 16);
        eh.spacing = 14f;
        eh.childAlignment = TextAnchor.MiddleLeft;

        var face = new GameObject("GovernorPortrait", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        face.transform.SetParent(exec.transform, false);
        face.GetComponent<Image>().color = new Color(0.22f, 0.24f, 0.30f, 1f);
        face.GetComponent<Image>().raycastTarget = false;
        face.GetComponent<LayoutElement>().preferredWidth = 170f;
        face.GetComponent<LayoutElement>().preferredHeight = 200f;

        var execText = new GameObject("InfoText", typeof(RectTransform), typeof(LayoutElement));
        execText.transform.SetParent(exec.transform, false);
        execText.GetComponent<LayoutElement>().flexibleWidth = 1f;
        var ev = execText.AddComponent<VerticalLayoutGroup>();
        ev.spacing = 6f;
        ev.childAlignment = TextAnchor.UpperLeft;
        CreateTMP(execText.transform, "GovernorName", "Governor\nCao Cao (SS Grade)", 28, FontStyles.Bold, TextAlignmentOptions.Left);
        CreateTMP(execText.transform, "Charm", "Charm: 94", 26, FontStyles.Italic, TextAlignmentOptions.Left);
        CreateTMP(execText.transform, "Buff", "[Golden Apple]\nCastle Value +20%", 24, FontStyles.Normal, TextAlignmentOptions.Left, color: new Color(0.95f, 0.88f, 0.55f));

        // Action buttons
        var actions = new GameObject("Actions", typeof(RectTransform), typeof(LayoutElement));
        actions.transform.SetParent(panel.transform, false);
        actions.GetComponent<LayoutElement>().minHeight = 120f;
        var ah = actions.AddComponent<HorizontalLayoutGroup>();
        ah.spacing = 16f;
        ah.childAlignment = TextAnchor.MiddleCenter;
        ah.childControlWidth = true;
        ah.childForceExpandWidth = true;

        CreateActionButton(actions.transform, "BuyButton", "[DEPLOY ARMY\n(BUY)]", new Color(0.65f, 0.20f, 0.18f));
        CreateActionButton(actions.transform, "SellButton", "[RECALL ARMY\n(SELL)]", new Color(0.20f, 0.35f, 0.65f));

        modal.SetActive(false);
    }

    static GameObject CreatePanel(Transform parent, string name, string title)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        panel.transform.SetParent(parent, false);

        panel.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.14f, 0.92f);
        var le = panel.GetComponent<LayoutElement>();
        le.minHeight = 220f;
        le.flexibleWidth = 1f;

        var v = panel.AddComponent<VerticalLayoutGroup>();
        v.spacing = 12f;
        v.padding = new RectOffset(16, 16, 16, 16);
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        CreateTMP(panel.transform, "Title", title, 30, FontStyles.Bold, TextAlignmentOptions.Left);
        return panel;
    }

    static Button CreateActionButton(Transform parent, string name, string label, Color bg)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = bg;
        var le = go.GetComponent<LayoutElement>();
        le.minHeight = 108f;
        le.flexibleWidth = 1f;

        var tmpGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        tmpGo.transform.SetParent(go.transform, false);
        var rt = tmpGo.GetComponent<RectTransform>();
        StretchFull(rt);
        var tmp = tmpGo.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 26;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return go.GetComponent<Button>();
    }

    static TextMeshProUGUI CreateTMP(Transform parent, string name, string text, int size, FontStyles style, TextAlignmentOptions align, Color? color = null, Vector4? pad = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.color = color ?? Color.white;
        tmp.raycastTarget = false;

        if (pad.HasValue)
            tmp.margin = pad.Value;

        var le = go.GetComponent<LayoutElement>();
        le.minHeight = Mathf.Max(32, size + 10);
        le.flexibleWidth = 1f;
        return tmp;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
#endif

