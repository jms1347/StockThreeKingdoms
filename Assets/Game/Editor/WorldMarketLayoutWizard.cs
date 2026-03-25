#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 천하탭(MTS) 레이아웃 자동 생성.
/// 메뉴: StockThreeKingdoms/천하/천하탭 만들기 (MTS Layout)
/// - 상단: Faction Market Share(파이 차트 자리)
/// - 중단: NewsTicker + Castle Stocks 리스트(ScrollRect + 템플릿 2종)
/// - 팝업: CityDetailPanel(기본 비활성)
/// </summary>
public static class WorldMarketLayoutWizard
{
    const string MenuPath = "StockThreeKingdoms/천하/천하탭 만들기 (MTS Layout)";

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

        CreateTopBar(root.transform);
        CreateFactionMarketSharePanel(root.transform);
        CreateCastleStocksPanel(root.transform);
        CreateCityDetailPanel(root.transform);

        Selection.activeGameObject = root;
        Debug.Log("[WorldMarketLayoutWizard] 천하탭(MTS) 레이아웃 생성 완료. 씬 저장 후 런타임 바인딩 스크립트를 연결하세요.");
    }

    static void CreateTopBar(Transform parent)
    {
        GameObject bar = new GameObject("TopBar", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        bar.transform.SetParent(parent, false);
        bar.GetComponent<Image>().color = new Color(0.10f, 0.12f, 0.16f, 0.92f);
        var le = bar.GetComponent<LayoutElement>();
        le.minHeight = 96f;
        le.preferredHeight = 112f;

        var hlg = bar.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(18, 18, 10, 10);
        hlg.spacing = 12f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;

        var userBox = new GameObject("UserBox", typeof(RectTransform));
        userBox.transform.SetParent(bar.transform, false);
        var userLe = userBox.AddComponent<LayoutElement>();
        userLe.preferredWidth = 420f;

        CreateTMP(userBox.transform, "UserLabel", "User: ZhugeMaster01", 28, FontStyles.Bold, TextAlignmentOptions.Left);

        var assetsBox = new GameObject("AssetsBox", typeof(RectTransform));
        assetsBox.transform.SetParent(bar.transform, false);
        var assetsLe = assetsBox.AddComponent<LayoutElement>();
        assetsLe.flexibleWidth = 1f;

        var assetsH = assetsBox.AddComponent<HorizontalLayoutGroup>();
        assetsH.childAlignment = TextAnchor.MiddleCenter;
        assetsH.spacing = 24f;
        assetsH.childControlWidth = false;
        assetsH.childForceExpandWidth = false;

        CreateTMP(assetsBox.transform, "TotalAssets", "Total Assets: 1,500,000 Gold", 26, FontStyles.Bold, TextAlignmentOptions.Center);
        CreateTMP(assetsBox.transform, "Food", "Food: 80,000", 26, FontStyles.Bold, TextAlignmentOptions.Center);
    }

    static void CreateFactionMarketSharePanel(Transform parent)
    {
        GameObject panel = CreatePanel(parent, "FactionMarketSharePanel", "Faction Market Share");
        panel.GetComponent<LayoutElement>().minHeight = 260f;

        var body = new GameObject("Body", typeof(RectTransform));
        body.transform.SetParent(panel.transform, false);
        var bodyH = body.AddComponent<HorizontalLayoutGroup>();
        bodyH.spacing = 16f;
        bodyH.childAlignment = TextAnchor.MiddleLeft;
        bodyH.childControlWidth = true;
        bodyH.childControlHeight = true;
        bodyH.childForceExpandWidth = true;

        // Pie chart placeholder
        var pie = new GameObject("PieChart", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        pie.transform.SetParent(body.transform, false);
        var pieImg = pie.GetComponent<Image>();
        pieImg.color = new Color(0.20f, 0.24f, 0.32f, 1f);
        pieImg.raycastTarget = false;
        var pieLe = pie.GetComponent<LayoutElement>();
        pieLe.preferredWidth = 220f;
        pieLe.preferredHeight = 220f;

        // Legend placeholder
        var legend = new GameObject("Legend", typeof(RectTransform), typeof(LayoutElement));
        legend.transform.SetParent(body.transform, false);
        var legendLe = legend.GetComponent<LayoutElement>();
        legendLe.flexibleWidth = 1f;

        var v = legend.AddComponent<VerticalLayoutGroup>();
        v.spacing = 10f;
        v.childAlignment = TextAnchor.UpperLeft;
        v.childControlWidth = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        CreateLegendRow(legend.transform, "WEI", new Color(0.20f, 0.55f, 0.90f));
        CreateLegendRow(legend.transform, "SHU", new Color(0.35f, 0.80f, 0.55f));
        CreateLegendRow(legend.transform, "WU", new Color(0.95f, 0.40f, 0.35f));
        CreateLegendRow(legend.transform, "OTHERS", new Color(0.55f, 0.58f, 0.66f));
    }

    static void CreateLegendRow(Transform parent, string label, Color c)
    {
        var row = new GameObject($"{label}Row", typeof(RectTransform), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<LayoutElement>().minHeight = 34f;
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
        GameObject panel = CreatePanel(parent, "CastleStocksPanel", "Castle Stocks (0/50 Active)");
        panel.GetComponent<LayoutElement>().flexibleHeight = 1f;
        panel.GetComponent<LayoutElement>().minHeight = 700f;

        // News ticker placeholder
        var ticker = new GameObject("NewsTicker", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        ticker.transform.SetParent(panel.transform, false);
        ticker.GetComponent<Image>().color = new Color(0.12f, 0.10f, 0.08f, 0.96f);
        var tLe = ticker.GetComponent<LayoutElement>();
        tLe.minHeight = 72f;
        tLe.preferredHeight = 84f;
        CreateTMP(ticker.transform, "TickerText", "[NEWS] 이벤트/점령/급등락 텍스트가 여기로 흐릅니다.", 24, FontStyles.Normal, TextAlignmentOptions.Left, pad: new Vector4(18, 0, 0, 0));

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

        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var sr = scrollGo.GetComponent<ScrollRect>();
        sr.viewport = vpRt;
        sr.content = contentRt;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;

        // Templates (inactive)
        CreateCastleStockCardTemplate(content.transform);
        CreateNewsRowTemplate(content.transform);
    }

    static void CreateCastleStockCardTemplate(Transform parent)
    {
        var card = new GameObject("CastleStockCardTemplate", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        card.transform.SetParent(parent, false);
        var img = card.GetComponent<Image>();
        img.color = new Color(0.12f, 0.15f, 0.20f, 0.98f);
        var le = card.GetComponent<LayoutElement>();
        le.minHeight = 164f;

        var h = card.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(16, 16, 14, 14);
        h.spacing = 14f;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = false;
        h.childForceExpandWidth = false;

        // Left: rank badge + name
        var left = new GameObject("Left", typeof(RectTransform), typeof(LayoutElement));
        left.transform.SetParent(card.transform, false);
        left.GetComponent<LayoutElement>().preferredWidth = 520f;
        var leftV = left.AddComponent<VerticalLayoutGroup>();
        leftV.spacing = 6f;
        leftV.childAlignment = TextAnchor.MiddleLeft;
        leftV.childControlWidth = true;
        leftV.childForceExpandWidth = true;
        leftV.childForceExpandHeight = false;

        CreateTMP(left.transform, "CastleName", "1  SS  Luoyang  洛陽", 30, FontStyles.Bold, TextAlignmentOptions.Left);
        CreateTMP(left.transform, "PriceLine", "1,250 Gold  ▲ 1.8%   Market Cap: 6.25M   Dividend: 5%", 24, FontStyles.Normal, TextAlignmentOptions.Left, color: new Color(0.80f, 0.90f, 1f));

        // Right: governor portrait placeholder
        var right = new GameObject("Governor", typeof(RectTransform), typeof(LayoutElement));
        right.transform.SetParent(card.transform, false);
        right.GetComponent<LayoutElement>().preferredWidth = 140f;
        var portrait = new GameObject("Portrait", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        portrait.transform.SetParent(right.transform, false);
        portrait.GetComponent<Image>().color = new Color(0.22f, 0.24f, 0.30f, 1f);
        portrait.GetComponent<Image>().raycastTarget = false;
        portrait.GetComponent<LayoutElement>().preferredHeight = 140f;

        card.SetActive(false);
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

