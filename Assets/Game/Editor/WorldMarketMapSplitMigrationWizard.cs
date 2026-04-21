#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 천하(WorldMarketRoot)에 지도/리스트 분할 UI를 붙입니다.
/// - 마이그레이션: 기존 CastleStocksPanel을 ListViewRoot 아래로 옮김
/// - 신규 위저드: ListViewRoot를 먼저 만들고 그 안에 CastleStocks 생성 후 <see cref="SetupSplitUi"/> 호출
/// </summary>
public static class WorldMarketMapSplitMigrationWizard
{
    const string MenuPath = "StockThreeKingdoms/Layout/천하 메뉴/천하 지도·리스트 분할 (마이그레이션)";

    [MenuItem(MenuPath, false, 21)]
    public static void Migrate()
    {
        var rootGo = GameObject.Find("WorldMarketRoot");
        if (rootGo == null)
        {
            EditorUtility.DisplayDialog("천하", "씬에 WorldMarketRoot가 없습니다.", "확인");
            return;
        }

        var root = rootGo.transform;
        if (root.Find("ListViewRoot") != null)
        {
            EditorUtility.DisplayDialog("천하", "이미 분할된 레이아웃입니다.", "확인");
            return;
        }

        var stocks = root.Find("CastleStocksPanel");
        if (stocks == null)
        {
            EditorUtility.DisplayDialog("천하", "CastleStocksPanel을 찾을 수 없습니다.", "확인");
            return;
        }

        Undo.SetCurrentGroupName("WorldMarket map/list split");
        int gid = Undo.GetCurrentGroup();

        var listRoot = new GameObject("ListViewRoot", typeof(RectTransform), typeof(LayoutElement), typeof(VerticalLayoutGroup));
        Undo.RegisterCreatedObjectUndo(listRoot, "ListViewRoot");
        listRoot.transform.SetParent(root, false);
        var listLe = listRoot.GetComponent<LayoutElement>();
        listLe.flexibleHeight = 1f;
        listLe.minHeight = 280f;
        var listV = listRoot.GetComponent<VerticalLayoutGroup>();
        listV.childControlWidth = true;
        listV.childForceExpandWidth = true;
        listV.childControlHeight = true;
        listV.childForceExpandHeight = true;

        Undo.SetTransformParent(stocks, listRoot.transform, "Move CastleStocksPanel");

        SetupSplitUi(rootGo, listRoot);

        Undo.CollapseUndoOperations(gid);
        Selection.activeGameObject = rootGo;
        EditorUtility.DisplayDialog("천하", "지도·리스트 분할을 적용했습니다. 씬을 저장하세요.\nCityDetailPanel에 TMP를 추가해 marchPointsTravelLineText를 연결하면 MP 안내가 표시됩니다.", "확인");
    }

    /// <summary>신규 「천하탭 만들기」 위저드에서 호출 — ListViewRoot가 이미 CastleStocks의 부모일 때.</summary>
    public static void SetupSplitUiForNewWizard(GameObject worldMarketRoot, GameObject listViewRoot)
    {
        if (worldMarketRoot == null || listViewRoot == null) return;
        SetupSplitUi(worldMarketRoot, listViewRoot);
    }

    /// <summary>ViewModeRow + MapViewRoot + WorldMarketViewModeController 연결 (중복 시 스킵).</summary>
    public static void SetupSplitUi(GameObject worldMarketRoot, GameObject listViewRoot)
    {
        if (worldMarketRoot == null || listViewRoot == null) return;
        var tr = worldMarketRoot.transform;
        if (tr.Find("ViewModeRow") != null)
            return;

        Undo.SetCurrentGroupName("WorldMarket split UI");
        int gid = Undo.GetCurrentGroup();

        var modeRow = new GameObject("ViewModeRow", typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
        Undo.RegisterCreatedObjectUndo(modeRow, "ViewModeRow");
        var modeLe = modeRow.GetComponent<LayoutElement>();
        modeLe.minHeight = 42f;
        modeLe.preferredHeight = 44f;
        modeLe.flexibleHeight = 0f;
        var modeH = modeRow.GetComponent<HorizontalLayoutGroup>();
        modeH.spacing = 10f;
        modeH.padding = new RectOffset(4, 4, 4, 4);
        modeH.childAlignment = TextAnchor.MiddleCenter;
        modeH.childControlWidth = true;
        modeH.childForceExpandWidth = true;

        var tg = modeRow.AddComponent<ToggleGroup>();
        tg.allowSwitchOff = false;

        Toggle listTog = CreateToggleChip(modeRow.transform, "ListToggle", "리스트 보기", true, tg);
        Toggle mapTog = CreateToggleChip(modeRow.transform, "MapToggle", "지도 보기", false, tg);

        modeRow.transform.SetParent(tr, false);
        modeRow.transform.SetSiblingIndex(1);

        var mapRoot = new GameObject("MapViewRoot", typeof(RectTransform), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(mapRoot, "MapViewRoot");
        mapRoot.transform.SetParent(tr, false);
        var mapLe = mapRoot.GetComponent<LayoutElement>();
        mapLe.flexibleHeight = 1f;
        mapLe.minHeight = 360f;
        mapRoot.SetActive(false);

        BuildMapScroll(mapRoot.transform);

        var modeCtrl = worldMarketRoot.GetComponent<WorldMarketViewModeController>();
        if (modeCtrl == null)
            modeCtrl = Undo.AddComponent<WorldMarketViewModeController>(worldMarketRoot);
        var soMode = new SerializedObject(modeCtrl);
        soMode.FindProperty("mapViewRoot").objectReferenceValue = mapRoot;
        soMode.FindProperty("listViewRoot").objectReferenceValue = listViewRoot;
        soMode.FindProperty("mapToggle").objectReferenceValue = mapTog;
        soMode.FindProperty("listToggle").objectReferenceValue = listTog;
        soMode.ApplyModifiedPropertiesWithoutUndo();

        Undo.CollapseUndoOperations(gid);
    }

    static Toggle CreateToggleChip(Transform parent, string name, string label, bool isOn, ToggleGroup group)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Toggle), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        var le = go.GetComponent<LayoutElement>();
        le.minHeight = 36f;
        le.flexibleWidth = 1f;
        var img = go.GetComponent<Image>();
        img.color = new Color(0.14f, 0.16f, 0.2f, 0.96f);
        var t = go.GetComponent<Toggle>();
        t.toggleTransition = Toggle.ToggleTransition.None;
        t.group = group;
        t.isOn = isOn;
        t.targetGraphic = img;

        var check = new GameObject("Background", typeof(RectTransform), typeof(Image));
        check.transform.SetParent(go.transform, false);
        StretchFull(check.GetComponent<RectTransform>());
        check.GetComponent<Image>().color = new Color(0.22f, 0.38f, 0.62f, isOn ? 0.95f : 0f);

        var tmpGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        tmpGo.transform.SetParent(go.transform, false);
        StretchFull(tmpGo.GetComponent<RectTransform>());
        var tmp = tmpGo.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 16;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.9f, 0.91f, 0.93f, 1f);

        t.graphic = img;
        return t;
    }

    static void BuildMapScroll(Transform mapRoot)
    {
        var scrollGo = new GameObject("WorldMapScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(WorldMarketMapScrollZoom), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(scrollGo, "WorldMapScroll");
        scrollGo.transform.SetParent(mapRoot, false);
        var sLe = scrollGo.GetComponent<LayoutElement>();
        sLe.flexibleHeight = 1f;
        sLe.flexibleWidth = 1f;
        sLe.minHeight = 320f;
        var sRt = scrollGo.GetComponent<RectTransform>();
        StretchFull(sRt);
        scrollGo.GetComponent<Image>().color = new Color(0.07f, 0.08f, 0.1f, 1f);

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        Undo.RegisterCreatedObjectUndo(viewport, "Viewport");
        viewport.transform.SetParent(scrollGo.transform, false);
        var vpRt = viewport.GetComponent<RectTransform>();
        StretchFull(vpRt);
        viewport.GetComponent<Image>().color = Color.white;
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        var content = new GameObject("MapContent", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(content, "MapContent");
        content.transform.SetParent(viewport.transform, false);
        var cRt = content.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0f, 0f);
        cRt.anchorMax = new Vector2(0f, 0f);
        cRt.pivot = new Vector2(0f, 0f);
        cRt.anchoredPosition = Vector2.zero;
        cRt.sizeDelta = new Vector2(1100f, 1100f);

        var bg = new GameObject("MapBackground", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(content.transform, false);
        StretchFull(bg.GetComponent<RectTransform>());
        bg.GetComponent<Image>().color = new Color(0.1f, 0.12f, 0.16f, 1f);

        var sr = scrollGo.GetComponent<ScrollRect>();
        sr.viewport = vpRt;
        sr.content = cRt;
        sr.horizontal = true;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 28f;

        var zoom = scrollGo.GetComponent<WorldMarketMapScrollZoom>();
        var zso = new SerializedObject(zoom);
        zso.FindProperty("scrollRect").objectReferenceValue = sr;
        zso.FindProperty("zoomTarget").objectReferenceValue = cRt;
        zso.ApplyModifiedPropertiesWithoutUndo();

        var mapView = scrollGo.AddComponent<WorldMarketMapViewController>();
        var mvSo = new SerializedObject(mapView);
        mvSo.FindProperty("mapScroll").objectReferenceValue = sr;
        mvSo.FindProperty("mapContent").objectReferenceValue = cRt;
        mvSo.FindProperty("mapWorldMax").floatValue = 1000f;
        mvSo.FindProperty("mapMargin").floatValue = 40f;
        mvSo.ApplyModifiedPropertiesWithoutUndo();
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
