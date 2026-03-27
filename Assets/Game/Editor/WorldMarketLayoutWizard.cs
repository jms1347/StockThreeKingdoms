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
/// - 중단: 필터 탭(전체·보유·교전·이벤트·우량) + Castle Stocks 리스트(ScrollRect + 템플릿 2종)
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
        panel.GetComponent<LayoutElement>().minHeight = 118f;
        var panelV = panel.GetComponent<VerticalLayoutGroup>();
        panelV.padding = new RectOffset(12, 12, 10, 10);
        panelV.spacing = 8f;
        var titleTmp = panel.transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
        if (titleTmp != null)
            titleTmp.fontSize = 26f;

        var body = new GameObject("Body", typeof(RectTransform));
        body.transform.SetParent(panel.transform, false);
        var bodyV = body.AddComponent<VerticalLayoutGroup>();
        bodyV.spacing = 4f;
        bodyV.childAlignment = TextAnchor.UpperLeft;
        bodyV.childControlWidth = true;
        bodyV.childControlHeight = true;
        bodyV.childForceExpandWidth = true;
        bodyV.childForceExpandHeight = false;

        Sprite barSprite = TryGetUiSquareSprite();

        // 가로 100% 스택 막대 (LayoutElement.flexibleWidth 비율)
        var barRoot = new GameObject("FactionShareBar", typeof(RectTransform), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(barRoot, "FactionShareBar");
        barRoot.transform.SetParent(body.transform, false);
        var barLe = barRoot.GetComponent<LayoutElement>();
        barLe.minHeight = 28f;
        barLe.preferredHeight = 28f;
        barLe.flexibleWidth = 1f;

        var segments = new GameObject("Segments", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        Undo.RegisterCreatedObjectUndo(segments, "BarSegments");
        segments.transform.SetParent(barRoot.transform, false);
        StretchFull(segments.GetComponent<RectTransform>());
        var segH = segments.GetComponent<HorizontalLayoutGroup>();
        segH.spacing = 0f;
        segH.childAlignment = TextAnchor.MiddleCenter;
        segH.childControlWidth = true;
        segH.childControlHeight = true;
        segH.childForceExpandWidth = true;
        segH.childForceExpandHeight = true;
        segH.padding = new RectOffset(0, 0, 0, 0);

        Image imgWei = CreateBarStackSegment(segments.transform, "SegmentWei", new Color(0.20f, 0.55f, 0.90f), barSprite);
        Image imgShu = CreateBarStackSegment(segments.transform, "SegmentShu", new Color(0.35f, 0.80f, 0.55f), barSprite);
        Image imgWu = CreateBarStackSegment(segments.transform, "SegmentWu", new Color(0.95f, 0.40f, 0.35f), barSprite);
        Image imgOth = CreateBarStackSegment(segments.transform, "SegmentOthers", new Color(0.55f, 0.58f, 0.66f), barSprite);

        // Legend — 한 줄 4열, 막대·도트·글자 모두 작게
        var legend = new GameObject("Legend", typeof(RectTransform), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(legend, "Legend");
        legend.transform.SetParent(body.transform, false);
        var legendLe = legend.GetComponent<LayoutElement>();
        legendLe.minHeight = 30f;
        legendLe.flexibleWidth = 1f;

        var legendH = legend.AddComponent<HorizontalLayoutGroup>();
        legendH.spacing = 2f;
        legendH.childAlignment = TextAnchor.MiddleLeft;
        legendH.childControlWidth = true;
        legendH.childControlHeight = true;
        legendH.childForceExpandWidth = true;
        legendH.childForceExpandHeight = false;

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

    /// <summary>세력 막대: 타원 Knob 대신 직사각형 UISprite.</summary>
    static Sprite TryGetUiSquareSprite()
    {
        var s = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        if (s != null) return s;
        s = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        if (s != null) return s;
        return TryGetUiKnobSprite();
    }

    static Image CreateBarStackSegment(Transform parent, string name, Color color, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        var le = go.GetComponent<LayoutElement>();
        le.minWidth = 0f;
        le.preferredWidth = 0f;
        le.flexibleWidth = 1f;
        le.minHeight = 0f;
        le.flexibleHeight = 1f;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = Vector2.zero;
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
        const int legendFontSize = 18;
        const float dotSize = 18f;

        var row = new GameObject($"{label}Row", typeof(RectTransform), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        var rowLe = row.GetComponent<LayoutElement>();
        rowLe.minHeight = 22f;
        rowLe.flexibleWidth = 1f;
        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 4f;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;

        var dot = new GameObject("Dot", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        dot.transform.SetParent(row.transform, false);
        var dotImg = dot.GetComponent<Image>();
        dotImg.sprite = TryGetUiSquareSprite();
        dotImg.type = Image.Type.Simple;
        dotImg.color = c;
        dotImg.raycastTarget = false;
        var dle = dot.GetComponent<LayoutElement>();
        dle.minWidth = dotSize;
        dle.minHeight = dotSize;
        dle.preferredWidth = dotSize;
        dle.preferredHeight = dotSize;
        var dotRt = dot.GetComponent<RectTransform>();
        dotRt.sizeDelta = new Vector2(dotSize, dotSize);

        string legendKey = label switch { "WEI" => "위", "SHU" => "촉", "WU" => "오", _ => "기타" };
        var tmp = CreateTMP(row.transform, "Label", $"{legendKey} 점유 —%", legendFontSize, FontStyles.Bold, TextAlignmentOptions.Left);
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        var labelLe = tmp.GetComponent<LayoutElement>();
        if (labelLe != null)
        {
            labelLe.minHeight = 22f;
            labelLe.flexibleWidth = 1f;
        }
    }

    static void CreateCastleStocksPanel(Transform parent)
    {
        GameObject panel = CreatePanel(parent, "CastleStocksPanel", "Castle Stocks");
        panel.GetComponent<LayoutElement>().flexibleHeight = 1f;
        panel.GetComponent<LayoutElement>().minHeight = 520f;

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
        // Mask는 Image 알파로 스텐실을 만듦. 알파가 너무 낮으면(≈0) 자식 UI가 전부 클리핑됨. ShowMaskGraphic 끄면 보이지 않음.
        viewport.GetComponent<Image>().color = new Color(1, 1, 1, 1f);
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

        RectTransform filterRowRt = CreateFilterTabBarRow(panel.transform, vlist);
        filterRowRt.SetSiblingIndex(1);

        var vso = new SerializedObject(vlist);
        vso.FindProperty("scrollRect").objectReferenceValue = sr;
        vso.FindProperty("content").objectReferenceValue = contentRt;
        vso.FindProperty("cellTemplate").objectReferenceValue = template;
        vso.FindProperty("cellStride").floatValue = 232f;
        vso.FindProperty("filterChipsReservedArea").objectReferenceValue = filterRowRt;
        vso.FindProperty("listHeaderText").objectReferenceValue = panel.transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
        vso.ApplyModifiedPropertiesWithoutUndo();

        Object.DestroyImmediate(csf);
        Object.DestroyImmediate(contentV);
    }

    /// <summary>CastleStocksPanel 자식: 제목 다음에 두는 필터 탭 바. 마이그레이션 메뉴에서도 사용.</summary>
    static RectTransform CreateFilterTabBarRow(Transform castleStocksPanel, WorldMarketCastleVirtualList virtualList)
    {
        var filterRow = new GameObject("FilterTabs", typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup), typeof(WorldMarketFilterTabBar));
        Undo.RegisterCreatedObjectUndo(filterRow, "FilterTabs");
        filterRow.transform.SetParent(castleStocksPanel, false);
        var filterLe = filterRow.GetComponent<LayoutElement>();
        filterLe.minHeight = 32f;
        filterLe.preferredHeight = 32f;
        filterLe.flexibleHeight = 0f;
        var filterH = filterRow.GetComponent<HorizontalLayoutGroup>();
        filterH.spacing = 3f;
        filterH.padding = new RectOffset(2, 2, 0, 0);
        filterH.childAlignment = TextAnchor.MiddleCenter;
        filterH.childControlWidth = true;
        filterH.childForceExpandWidth = true;

        var specs = new (string name, string label, WorldMarketCastleListFilter f)[]
        {
            ("FilterTab_All", "전체", WorldMarketCastleListFilter.All),
            ("FilterTab_My", "내 투자", WorldMarketCastleListFilter.MyHoldings),
            ("FilterTab_War", "전쟁 중", WorldMarketCastleListFilter.War),
            ("FilterTab_Event", "이벤트", WorldMarketCastleListFilter.Event),
            ("FilterTab_Premium", "우량", WorldMarketCastleListFilter.Premium),
            ("FilterTab_Attn", "요주의·B~D", WorldMarketCastleListFilter.Attention),
        };

        var buttons = new Button[specs.Length];
        for (int i = 0; i < specs.Length; i++)
        {
            buttons[i] = CreateFilterTabChipButton(filterRow.transform, specs[i].name, specs[i].label);
        }

        var tabBar = filterRow.GetComponent<WorldMarketFilterTabBar>();
        var so = new SerializedObject(tabBar);
        so.FindProperty("castleList").objectReferenceValue = virtualList;
        var tabsProp = so.FindProperty("tabs");
        tabsProp.arraySize = specs.Length;
        for (int i = 0; i < specs.Length; i++)
        {
            var el = tabsProp.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("button").objectReferenceValue = buttons[i];
            el.FindPropertyRelative("filter").enumValueIndex = (int)specs[i].f;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        return filterRow.GetComponent<RectTransform>();
    }

    static Button CreateFilterTabChipButton(Transform parent, string name, string label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.14f, 0.16f, 0.20f, 0.96f);
        var le = go.GetComponent<LayoutElement>();
        le.minWidth = 0f;
        le.flexibleWidth = 1f;
        le.minHeight = 30f;
        le.preferredHeight = 30f;
        le.flexibleHeight = 0f;
        var btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.28f, 0.44f, 0.68f, 1f);
        colors.pressedColor = new Color(0.20f, 0.32f, 0.52f, 1f);
        btn.colors = colors;

        var tmp = CreateTMP(go.transform, "Label", label, 15, FontStyles.Bold, TextAlignmentOptions.Center);
        tmp.color = new Color(0.78f, 0.80f, 0.84f, 1f);
        var tle = tmp.GetComponent<LayoutElement>();
        if (tle != null)
            tle.ignoreLayout = true;
        StretchFull(tmp.GetComponent<RectTransform>());
        return btn;
    }

    static void CreateNameRowWithStatusIcons(Transform left)
    {
        var nameRow = new GameObject("NameRow", typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
        Undo.RegisterCreatedObjectUndo(nameRow, "NameRow");
        nameRow.transform.SetParent(left, false);
        var nr = nameRow.GetComponent<HorizontalLayoutGroup>();
        nr.spacing = 6f;
        nr.padding = new RectOffset(0, 0, 0, 0);
        nr.childAlignment = TextAnchor.MiddleLeft;
        nr.childControlWidth = true;
        nr.childControlHeight = true;
        nr.childForceExpandWidth = true;
        nr.childForceExpandHeight = false;
        nameRow.GetComponent<LayoutElement>().minHeight = 36f;

        var gradeTmp = CreateTMP(nameRow.transform, "GradeBadge", "SS", 24, FontStyles.Bold, TextAlignmentOptions.Left,
            color: new Color(1f, 0.82f, 0.35f, 1f));
        var gLe = gradeTmp.GetComponent<LayoutElement>();
        gLe.flexibleWidth = 0f;
        gLe.minWidth = 40f;
        gLe.preferredWidth = 44f;

        CreateTMP(nameRow.transform, "CastleName", "낙양", 28, FontStyles.Bold, TextAlignmentOptions.Left);

        var icons = new GameObject("StatusIcons", typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
        Undo.RegisterCreatedObjectUndo(icons, "StatusIcons");
        icons.transform.SetParent(nameRow.transform, false);
        var ih = icons.GetComponent<HorizontalLayoutGroup>();
        ih.spacing = 3f;
        ih.childAlignment = TextAnchor.MiddleLeft;
        ih.childControlWidth = false;
        ih.childForceExpandWidth = false;
        var iconsLe = icons.GetComponent<LayoutElement>();
        iconsLe.minWidth = 72f;
        iconsLe.preferredHeight = 26f;
        iconsLe.flexibleWidth = 0f;

        Sprite knob = TryGetUiKnobSprite();
        CreateStatusIconImage(icons.transform, "IconWar", new Color(0.98f, 0.38f, 0.36f, 1f), knob);
        CreateStatusIconImage(icons.transform, "IconDisaster", new Color(1f, 0.68f, 0.28f, 1f), knob);
        CreateStatusIconImage(icons.transform, "IconFavorable", new Color(0.45f, 0.92f, 0.58f, 1f), knob);
    }

    static void CreateStatusIconImage(Transform parent, string name, Color iconTint, Sprite knob)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        go.SetActive(false);
        var img = go.GetComponent<Image>();
        img.sprite = knob;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
        img.color = iconTint;
        img.raycastTarget = false;
        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth = 22f;
        le.preferredHeight = 22f;
        le.minWidth = 22f;
        le.minHeight = 22f;
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(22f, 22f);
    }

    static Image CreateFullCardOverlay(Transform card, string name, Color color, bool active)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(card, false);
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        StretchFull(go.GetComponent<RectTransform>());
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        go.SetActive(active);
        return img;
    }

    static void CreateStakeGaugeBar(Transform card)
    {
        var bar = new GameObject("StakeGaugeBar", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(bar, "StakeGaugeBar");
        bar.transform.SetParent(card, false);
        var ble = bar.GetComponent<LayoutElement>();
        ble.minHeight = 5f;
        ble.preferredHeight = 5f;
        ble.flexibleHeight = 0f;
        ble.flexibleWidth = 1f;
        bar.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 5f);
        bar.GetComponent<Image>().sprite = TryGetUiSquareSprite();
        bar.GetComponent<Image>().type = Image.Type.Simple;
        bar.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f);
        bar.GetComponent<Image>().raycastTarget = false;

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(fill, "StakeGaugeFill");
        fill.transform.SetParent(bar.transform, false);
        StretchFull(fill.GetComponent<RectTransform>());
        var fImg = fill.GetComponent<Image>();
        fImg.type = Image.Type.Filled;
        fImg.fillMethod = Image.FillMethod.Horizontal;
        fImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        fImg.fillAmount = 0f;
        fImg.color = new Color(1f, 0.82f, 0.35f, 0.45f);
        fImg.raycastTarget = false;
    }

    static GameObject CreateCastleCardActionButton(Transform parent, string name, string label, Color bg)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = bg;
        var le = go.GetComponent<LayoutElement>();
        le.minHeight = 42f;
        le.preferredHeight = 46f;
        le.flexibleWidth = 1f;
        le.flexibleHeight = 0f;
        go.GetComponent<Button>().transition = Selectable.Transition.ColorTint;
        var tmp = CreateTMP(go.transform, "Label", label, 15, FontStyles.Bold, TextAlignmentOptions.Center, color: Color.white);
        var tle = tmp.GetComponent<LayoutElement>();
        if (tle != null) tle.ignoreLayout = true;
        StretchFull(tmp.GetComponent<RectTransform>());
        return go;
    }

    static GameObject CreateCastleStockCardTemplate(Transform parent)
    {
        var card = new GameObject("CastleStockCardTemplate", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(WorldMarketCastleCardView), typeof(Outline));
        card.transform.SetParent(parent, false);
        var img = card.GetComponent<Image>();
        img.color = new Color(0.10f, 0.12f, 0.16f, 0.98f);
        var le = card.GetComponent<LayoutElement>();
        le.minHeight = 228f;
        le.preferredHeight = 228f;
        le.flexibleHeight = 0f;

        var outline = card.GetComponent<Outline>();
        outline.effectColor = new Color(1f, 0.78f, 0.22f, 0.92f);
        outline.effectDistance = new Vector2(4f, -4f);
        outline.useGraphicAlpha = false;
        outline.enabled = false;

        var gloss = new GameObject("GlossOverlay", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        gloss.transform.SetParent(card.transform, false);
        gloss.transform.SetAsFirstSibling();
        StretchFull(gloss.GetComponent<RectTransform>());
        gloss.GetComponent<Image>().color = new Color(1f, 0.94f, 0.78f, 0.09f);
        gloss.GetComponent<Image>().raycastTarget = false;
        gloss.GetComponent<LayoutElement>().ignoreLayout = true;
        gloss.SetActive(false);

        var cardV = card.AddComponent<VerticalLayoutGroup>();
        cardV.spacing = 4f;
        cardV.padding = new RectOffset(8, 8, 8, 8);
        cardV.childAlignment = TextAnchor.UpperCenter;
        cardV.childControlWidth = true;
        cardV.childForceExpandWidth = true;
        cardV.childControlHeight = true;
        cardV.childForceExpandHeight = false;

        var mainRow = new GameObject("MainRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(mainRow, "MainRow");
        mainRow.transform.SetParent(card.transform, false);
        var mrLe = mainRow.GetComponent<LayoutElement>();
        mrLe.minHeight = 210f;
        mrLe.preferredHeight = 212f;
        mrLe.flexibleHeight = 0f;
        mrLe.flexibleWidth = 1f;
        var mrH = mainRow.GetComponent<HorizontalLayoutGroup>();
        mrH.spacing = 6f;
        mrH.padding = new RectOffset(0, 0, 0, 0);
        mrH.childAlignment = TextAnchor.UpperLeft;
        mrH.childControlWidth = true;
        mrH.childControlHeight = true;
        mrH.childForceExpandWidth = true;
        mrH.childForceExpandHeight = true;

        Color sub = new Color(0.48f, 0.51f, 0.56f, 1f);
        Color personalGold = new Color(1f, 0.88f, 0.48f, 1f);

        // —— 1구역: 식별 ——
        var z1 = new GameObject("Zone1", typeof(RectTransform), typeof(LayoutElement), typeof(VerticalLayoutGroup));
        Undo.RegisterCreatedObjectUndo(z1, "Zone1");
        z1.transform.SetParent(mainRow.transform, false);
        z1.GetComponent<LayoutElement>().minWidth = 108f;
        z1.GetComponent<LayoutElement>().preferredWidth = 118f;
        z1.GetComponent<LayoutElement>().flexibleWidth = 0f;
        var z1v = z1.GetComponent<VerticalLayoutGroup>();
        z1v.spacing = 4f;
        z1v.childAlignment = TextAnchor.UpperLeft;
        z1v.childControlWidth = true;
        z1v.childForceExpandWidth = true;

        var z1Row = new GameObject("Z1Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        z1Row.transform.SetParent(z1.transform, false);
        var z1rH = z1Row.GetComponent<HorizontalLayoutGroup>();
        z1rH.spacing = 6f;
        z1rH.childAlignment = TextAnchor.UpperLeft;
        z1rH.childControlWidth = true;
        z1rH.childForceExpandWidth = true;
        z1rH.childControlHeight = true;
        z1rH.childForceExpandHeight = false;

        var accentBar = new GameObject("GradeAccentBar", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(accentBar, "GradeAccentBar");
        accentBar.transform.SetParent(z1Row.transform, false);
        var abImg = accentBar.GetComponent<Image>();
        abImg.sprite = TryGetUiSquareSprite();
        abImg.type = Image.Type.Simple;
        abImg.color = new Color(1f, 0.82f, 0.35f, 1f);
        abImg.raycastTarget = false;
        var abLe = accentBar.GetComponent<LayoutElement>();
        abLe.preferredWidth = 5f;
        abLe.minWidth = 5f;
        abLe.flexibleWidth = 0f;
        abLe.minHeight = 72f;
        abLe.preferredHeight = 88f;
        abLe.flexibleHeight = 1f;

        var nameCol = new GameObject("NameColumn", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        nameCol.transform.SetParent(z1Row.transform, false);
        nameCol.GetComponent<LayoutElement>().flexibleWidth = 1f;
        var ncV = nameCol.GetComponent<VerticalLayoutGroup>();
        ncV.spacing = 2f;
        ncV.childAlignment = TextAnchor.UpperLeft;
        ncV.childControlWidth = true;
        ncV.childForceExpandWidth = true;

        CreateNameRowWithStatusIcons(nameCol.transform);
        CreateTMP(nameCol.transform, "CastleIdLine", "지역 · ID", 15, FontStyles.Normal, TextAlignmentOptions.Left, color: sub);

        // —— 2구역: 시세·추세·스파크라인 ——
        var z2 = new GameObject("Zone2", typeof(RectTransform), typeof(LayoutElement), typeof(VerticalLayoutGroup));
        Undo.RegisterCreatedObjectUndo(z2, "Zone2");
        z2.transform.SetParent(mainRow.transform, false);
        z2.GetComponent<LayoutElement>().flexibleWidth = 1f;
        z2.GetComponent<LayoutElement>().minWidth = 120f;
        var z2v = z2.GetComponent<VerticalLayoutGroup>();
        z2v.spacing = 3f;
        z2v.childAlignment = TextAnchor.UpperLeft;
        z2v.childControlWidth = true;
        z2v.childForceExpandWidth = true;

        CreateTMP(z2.transform, "BuyLabel", "매수가", 16, FontStyles.Normal, TextAlignmentOptions.Left, color: sub);

        var buyBg = new GameObject("BuyPriceBg", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        buyBg.transform.SetParent(z2.transform, false);
        buyBg.GetComponent<Image>().sprite = TryGetUiSquareSprite();
        buyBg.GetComponent<Image>().type = Image.Type.Simple;
        buyBg.GetComponent<Image>().color = new Color(0.11f, 0.13f, 0.17f, 0.98f);
        buyBg.GetComponent<Image>().raycastTarget = false;
        buyBg.GetComponent<LayoutElement>().minHeight = 44f;
        buyBg.GetComponent<LayoutElement>().preferredHeight = 48f;
        buyBg.GetComponent<LayoutElement>().flexibleWidth = 1f;

        var buyTmp = CreateTMP(buyBg.transform, "BuyPrice", "6,533 Gold", 32, FontStyles.Bold, TextAlignmentOptions.Left);
        buyTmp.margin = new Vector4(8f, 0f, 6f, 0f);
        buyTmp.color = Color.white;

        var sentRow = new GameObject("SentRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        sentRow.transform.SetParent(z2.transform, false);
        sentRow.GetComponent<LayoutElement>().minHeight = 26f;
        var srH = sentRow.GetComponent<HorizontalLayoutGroup>();
        srH.spacing = 6f;
        srH.childAlignment = TextAnchor.MiddleLeft;
        CreateTMP(sentRow.transform, "Arrow", "▲", 21, FontStyles.Bold, TextAlignmentOptions.Left);
        CreateTMP(sentRow.transform, "ChangePct", "+1.20%", 19, FontStyles.Bold, TextAlignmentOptions.Left);

        var sparkHost = new GameObject("SparklineHost", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(sparkHost, "SparklineHost");
        sparkHost.transform.SetParent(z2.transform, false);
        var shImg = sparkHost.GetComponent<Image>();
        shImg.sprite = TryGetUiSquareSprite();
        shImg.type = Image.Type.Simple;
        shImg.color = new Color(0.06f, 0.08f, 0.11f, 0.55f);
        shImg.raycastTarget = false;
        sparkHost.GetComponent<LayoutElement>().minHeight = 38f;
        sparkHost.GetComponent<LayoutElement>().preferredHeight = 42f;
        sparkHost.GetComponent<LayoutElement>().flexibleWidth = 1f;

        var sparkGo = new GameObject("Sparkline", typeof(RectTransform), typeof(UIMiniSparklineGraphic));
        Undo.RegisterCreatedObjectUndo(sparkGo, "Sparkline");
        sparkGo.transform.SetParent(sparkHost.transform, false);
        StretchFull(sparkGo.GetComponent<RectTransform>());

        // —— 3구역: 내 투자 ——
        var z3 = new GameObject("Zone3Personal", typeof(RectTransform), typeof(LayoutElement), typeof(VerticalLayoutGroup));
        Undo.RegisterCreatedObjectUndo(z3, "Zone3Personal");
        z3.transform.SetParent(mainRow.transform, false);
        z3.SetActive(false);
        var z3le = z3.GetComponent<LayoutElement>();
        z3le.minWidth = 132f;
        z3le.preferredWidth = 142f;
        z3le.flexibleWidth = 0f;
        var z3v = z3.GetComponent<VerticalLayoutGroup>();
        z3v.spacing = 5f;
        z3v.childAlignment = TextAnchor.UpperRight;
        z3v.childControlWidth = true;
        z3v.childForceExpandWidth = true;

        var roiBox = new GameObject("RoiBox", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        roiBox.transform.SetParent(z3.transform, false);
        roiBox.GetComponent<Image>().sprite = TryGetUiSquareSprite();
        roiBox.GetComponent<Image>().type = Image.Type.Simple;
        roiBox.GetComponent<Image>().color = new Color(1f, 0.82f, 0.35f, 0.14f);
        roiBox.GetComponent<Image>().raycastTarget = false;
        roiBox.GetComponent<LayoutElement>().minHeight = 30f;
        roiBox.GetComponent<LayoutElement>().preferredHeight = 34f;
        roiBox.GetComponent<LayoutElement>().flexibleWidth = 1f;

        var roiTmp = CreateTMP(roiBox.transform, "RoiText", "+15.2%", 18, FontStyles.Bold, TextAlignmentOptions.Center, color: personalGold);
        var roiTle = roiTmp.GetComponent<LayoutElement>();
        if (roiTle != null) roiTle.ignoreLayout = true;
        StretchFull(roiTmp.GetComponent<RectTransform>());

        CreateTMP(z3.transform, "TroopsLine", "1,250명", 18, FontStyles.Bold, TextAlignmentOptions.Right, color: personalGold);
        CreateTMP(z3.transform, "StakeLine", "지분 12%", 15, FontStyles.Normal, TextAlignmentOptions.Right, color: new Color(0.88f, 0.80f, 0.52f, 1f));

        // —— 4구역: 투입 / 회수 ——
        var z4 = new GameObject("Zone4Actions", typeof(RectTransform), typeof(LayoutElement), typeof(VerticalLayoutGroup));
        Undo.RegisterCreatedObjectUndo(z4, "Zone4Actions");
        z4.transform.SetParent(mainRow.transform, false);
        z4.GetComponent<LayoutElement>().minWidth = 76f;
        z4.GetComponent<LayoutElement>().preferredWidth = 82f;
        z4.GetComponent<LayoutElement>().flexibleWidth = 0f;
        var z4v = z4.GetComponent<VerticalLayoutGroup>();
        z4v.spacing = 8f;
        z4v.childAlignment = TextAnchor.UpperCenter;
        z4v.childControlWidth = true;
        z4v.childForceExpandWidth = true;
        z4v.childControlHeight = true;
        z4v.childForceExpandHeight = true;

        CreateCastleCardActionButton(z4.transform, "DeployButton", "투입", new Color(0.16f, 0.48f, 0.32f, 0.98f));
        var recallGo = CreateCastleCardActionButton(z4.transform, "RecallButton", "회수", new Color(0.82f, 0.42f, 0.22f, 0.98f));
        recallGo.SetActive(false);

        CreateStakeGaugeBar(card.transform);
        CreateFullCardOverlay(card.transform, "DisasterOverlay", new Color(0.04f, 0.05f, 0.08f, 0.42f), false);
        CreateFullCardOverlay(card.transform, "WarTint", new Color(0.55f, 0.1f, 0.1f, 0f), false);

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

