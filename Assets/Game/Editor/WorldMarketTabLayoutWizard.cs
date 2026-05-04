#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 천하(WorldMarket) 탭 UI를 <c>GameHub_WorldCanvas</c> 프리팹과 동일한 계층·컴포넌트·참조로 맞춥니다.
/// 기존 노드는 가능한 한 유지하고, 누락된 오브젝트만 생성한 뒤 SerializedObject로 배선을 복구합니다.
/// </summary>
public static class WorldMarketTabLayoutWizard
{
    const string PrefabPath = "Assets/Game/0Scene/GameHub/GameHub_WorldCanvas.prefab";
    const string MenuApplyPrefab = "StockThreeKingdoms/Layout/천하/GameHub WorldCanvas — 천하 탭 레이아웃 적용";
    const string MenuApplyScene = "StockThreeKingdoms/Layout/천하/현재 씬 — WorldMarketRoot 레이아웃 적용";
    const string MenuFloating = "StockThreeKingdoms/Layout/천하/플로팅 UI 생성(상세·요약·본영 이동)";

    static readonly string[] SectionOrder =
    {
        "FactionMarketSharePanel",
        "ViewModeRow",
        "ListViewRoot",
        "MapViewRoot",
    };

    [MenuItem(MenuApplyPrefab, false, 20)]
    static void RunApplyPrefab()
    {
        if (!System.IO.File.Exists(PrefabPath))
        {
            EditorUtility.DisplayDialog("천하 레이아웃", $"프리팹을 찾을 수 없습니다.\n{PrefabPath}", "확인");
            return;
        }

        if (!EditorUtility.DisplayDialog("천하 레이아웃",
                "GameHub_WorldCanvas 프리팹의 WorldMarketRoot 아래 구조를 점검·보강합니다.\n" +
                "누락된 오브젝트는 생성되며, 기존에 이름이 같은 노드는 가능한 한 유지됩니다.\n\n계속할까요?",
                "적용", "취소"))
            return;

        using (var scope = new PrefabUtility.EditPrefabContentsScope(PrefabPath))
        {
            var root = scope.prefabContentsRoot;
            var content = FindRecursive(root.transform, "ContentRoot");
            if (content == null)
            {
                EditorUtility.DisplayDialog("천하 레이아웃", "ContentRoot를 찾지 못했습니다.", "확인");
                return;
            }

            var wm = EnsureWorldMarketRoot(content);
            ApplyFullLayout(wm);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[WorldMarketTabLayoutWizard] 프리팹 적용 완료.");
    }

    [MenuItem(MenuApplyScene, false, 21)]
    static void RunApplyScene()
    {
        var wm = FindWorldMarketRootInOpenScenes();
        if (wm == null)
        {
            EditorUtility.DisplayDialog("천하 레이아웃", "씬에서 WorldMarketRoot를 찾지 못했습니다.", "확인");
            return;
        }

        if (!EditorUtility.DisplayDialog("천하 레이아웃",
                $"대상: {wm.name} ({GetScenePathSafe(wm.gameObject)})\n\n레이아웃을 점검·보강합니다. 계속할까요?",
                "적용", "취소"))
            return;

        Undo.RegisterFullObjectHierarchyUndo(wm.gameObject, "WorldMarket layout");
        ApplyFullLayout(wm);
        EditorUtility.SetDirty(wm.gameObject);
        if (wm.gameObject.scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(wm.gameObject.scene);
        Debug.Log("[WorldMarketTabLayoutWizard] 씬 적용 완료.");
    }

    [MenuItem(MenuFloating, false, 22)]
    static void RunEnsureFloating()
    {
        var wm = FindWorldMarketRootInOpenScenes();
        if (wm == null)
        {
            EditorUtility.DisplayDialog("천하 레이아웃", "씬에서 WorldMarketRoot를 찾지 못했습니다.", "확인");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(wm.gameObject, "WorldMarket floating UI");
        WorldMarketCastleDetailPopup.EnsureUnderWorldMarketRoot(wm);
        WorldMarketCastleSummarySheet.EnsureUnderWorldMarketRoot(wm);
        WorldHqTravelHud.EnsureUnderWorldMarketRoot(wm);
        EditorUtility.SetDirty(wm.gameObject);
        if (wm.gameObject.scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(wm.gameObject.scene);
        Debug.Log("[WorldMarketTabLayoutWizard] 플로팅 UI 보장 완료.");
    }

    static string GetScenePathSafe(GameObject go) =>
        go.scene.IsValid() ? go.scene.path : "(프리팹 인스턴스)";

    static Transform FindWorldMarketRootInOpenScenes()
    {
        foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go != null && go.name == "WorldMarketRoot" && go.transform.parent != null)
                return go.transform;
        }
        return null;
    }

    static Transform FindRecursive(Transform parent, string objectName)
    {
        if (parent == null) return null;
        if (parent.name == objectName) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var ch = parent.GetChild(i);
            var f = FindRecursive(ch, objectName);
            if (f != null) return f;
        }
        return null;
    }

    static Transform EnsureWorldMarketRoot(Transform contentRoot)
    {
        var existing = contentRoot.Find("WorldMarketRoot");
        if (existing != null)
            return existing;

        var go = NewUiGo("WorldMarketRoot", contentRoot);
        StretchFull(go.GetComponent<RectTransform>());
        return go.transform;
    }

    static void ApplyFullLayout(Transform wm)
    {
        EnsureWorldMarketShell(wm);
        var faction = EnsureFactionMarketSharePanel(wm);
        var viewRow = EnsureViewModeRow(wm);
        var listRoot = EnsureListViewRoot(wm);
        var mapRoot = EnsureMapViewRoot(wm);

        ReorderSectionsFirst(wm);

        var vList = WireListBranch(listRoot);
        WireMapBranch(mapRoot, vList);
        WireViewModeController(wm, mapRoot.gameObject, listRoot.gameObject, viewRow);

        EditorUtility.SetDirty(wm.gameObject);
    }

    static void ReorderSectionsFirst(Transform wm)
    {
        int insert = 0;
        foreach (var sectionName in SectionOrder)
        {
            var t = wm.Find(sectionName);
            if (t == null) continue;
            t.SetSiblingIndex(insert++);
        }
    }

    static void EnsureWorldMarketShell(Transform wm)
    {
        var go = wm.gameObject;
        if (go.GetComponent<Image>() == null)
        {
            var img = Undo.AddComponent<Image>(go);
            img.color = new Color(0.05f, 0.06f, 0.08f, 0.98f);
            img.raycastTarget = true;
        }

        var vlg = go.GetComponent<VerticalLayoutGroup>();
        if (vlg == null)
        {
            vlg = Undo.AddComponent<VerticalLayoutGroup>(go);
            vlg.padding = new RectOffset(18, 18, 22, 18);
            vlg.spacing = 24f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
        }

        if (go.GetComponent<WorldMarketViewModeController>() == null)
            Undo.AddComponent<WorldMarketViewModeController>(go);

        StretchFull(wm.GetComponent<RectTransform>());
    }

    static Transform EnsureFactionMarketSharePanel(Transform wm)
    {
        var t = wm.Find("FactionMarketSharePanel");
        if (t == null)
        {
            var go = NewUiGo("FactionMarketSharePanel", wm);
            t = go.transform;
        }

        var le = t.gameObject.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(t.gameObject);
        le.minHeight = 176f;
        le.flexibleWidth = 1f;

        var img = t.gameObject.GetComponent<Image>() ?? Undo.AddComponent<Image>(t.gameObject);
        img.color = new Color(0.08f, 0.10f, 0.14f, 0.92f);

        var vlg = t.gameObject.GetComponent<VerticalLayoutGroup>() ?? Undo.AddComponent<VerticalLayoutGroup>(t.gameObject);
        vlg.padding = new RectOffset(12, 12, 10, 10);
        vlg.spacing = 8f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var titleTf = t.Find("Title");
        if (titleTf == null)
        {
            var titleGo = NewUiGo("Title", t);
            var tmp = titleGo.AddComponent<TextMeshProUGUI>();
            tmp.text = "Faction Market Share";
            tmp.fontSize = 22;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            var tle = titleGo.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(titleGo);
            tle.minHeight = 34f;
            tle.flexibleWidth = 1f;
        }

        var body = t.Find("Body");
        if (body == null)
        {
            var bodyGo = NewUiGo("Body", t);
            body = bodyGo.transform;
            var bv = bodyGo.GetComponent<VerticalLayoutGroup>() ?? Undo.AddComponent<VerticalLayoutGroup>(bodyGo);
            bv.spacing = 4f;
            bv.childControlWidth = true;
            bv.childControlHeight = true;
            bv.childForceExpandWidth = true;
            bv.childForceExpandHeight = false;
        }

        var barTf = body.Find("FactionShareBar");
        if (barTf == null)
        {
            var barGo = NewUiGo("FactionShareBar", body);
            barTf = barGo.transform;
            var ble = barGo.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(barGo);
            ble.minHeight = 28f;
            ble.preferredHeight = 28f;
            ble.flexibleWidth = 1f;

            var segGo = NewUiGo("Segments", barTf);
            var segh = segGo.GetComponent<HorizontalLayoutGroup>() ?? Undo.AddComponent<HorizontalLayoutGroup>(segGo);
            segh.spacing = 0f;
            segh.childControlWidth = true;
            segh.childControlHeight = true;
            segh.childForceExpandWidth = true;
            segh.childForceExpandHeight = true;
            StretchFull(segGo.GetComponent<RectTransform>());

            CreateSegmentImage(segGo.transform, "SegmentWei", new Color(0.20f, 0.55f, 0.90f));
            CreateSegmentImage(segGo.transform, "SegmentShu", new Color(0.35f, 0.80f, 0.55f));
            CreateSegmentImage(segGo.transform, "SegmentWu", new Color(0.95f, 0.40f, 0.35f));
            CreateSegmentImage(segGo.transform, "SegmentOthers", new Color(0.55f, 0.58f, 0.66f));

            var pie = barGo.GetComponent<WorldMarketPieChartUI>() ?? Undo.AddComponent<WorldMarketPieChartUI>(barGo);
            WirePieChart(pie, barTf);
        }
        else if (barTf.GetComponent<WorldMarketPieChartUI>() == null)
            Undo.AddComponent<WorldMarketPieChartUI>(barTf.gameObject);

        var legendTf = body.Find("Legend");
        if (legendTf == null)
        {
            var legGo = NewUiGo("Legend", body);
            legendTf = legGo.transform;
            var lle = legGo.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(legGo);
            lle.minHeight = 30f;
            lle.flexibleWidth = 1f;
            var hl = legGo.GetComponent<HorizontalLayoutGroup>() ?? Undo.AddComponent<HorizontalLayoutGroup>(legGo);
            hl.spacing = 2f;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = true;
            hl.childForceExpandHeight = true;

            CreateLegendCell(legendTf, "Wei", "위 · 25%");
            CreateLegendCell(legendTf, "Shu", "촉 · 25%");
            CreateLegendCell(legendTf, "Wu", "오 · 25%");
            CreateLegendCell(legendTf, "Others", "기타 · 25%");
        }

        var pieChart = barTf.GetComponent<WorldMarketPieChartUI>();
        if (pieChart != null)
            WirePieChart(pieChart, barTf);

        return t;
    }

    static void CreateSegmentImage(Transform parent, string name, Color c)
    {
        if (parent.Find(name) != null) return;
        var go = NewUiGo(name, parent);
        var img = go.GetComponent<Image>() ?? Undo.AddComponent<Image>(go);
        img.color = c;
        img.raycastTarget = false;
        var le = go.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(go);
        le.flexibleWidth = 1f;
        le.flexibleHeight = 1f;
        var rt = go.GetComponent<RectTransform>();
        rt.pivot = new Vector2(0f, 0.5f);
    }

    static void CreateLegendCell(Transform legend, string key, string text)
    {
        var go = NewUiGo("Legend" + key, legend);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 14;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.78f, 0.80f, 0.84f);
        tmp.raycastTarget = false;
        var le = go.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(go);
        le.flexibleWidth = 1f;
        le.minHeight = 24f;
    }

    static void WirePieChart(WorldMarketPieChartUI pie, Transform barTf)
    {
        var seg = barTf.Find("Segments");
        if (seg == null) return;
        var leg = barTf.parent != null ? barTf.parent.Find("Legend") : null;

        var so = new SerializedObject(pie);
        so.FindProperty("segmentWei").objectReferenceValue = seg.Find("SegmentWei")?.GetComponent<Image>();
        so.FindProperty("segmentShu").objectReferenceValue = seg.Find("SegmentShu")?.GetComponent<Image>();
        so.FindProperty("segmentWu").objectReferenceValue = seg.Find("SegmentWu")?.GetComponent<Image>();
        so.FindProperty("segmentOthers").objectReferenceValue = seg.Find("SegmentOthers")?.GetComponent<Image>();

        if (leg != null)
        {
            so.FindProperty("textWei").objectReferenceValue = leg.Find("LegendWei")?.GetComponentInChildren<TextMeshProUGUI>();
            so.FindProperty("textShu").objectReferenceValue = leg.Find("LegendShu")?.GetComponentInChildren<TextMeshProUGUI>();
            so.FindProperty("textWu").objectReferenceValue = leg.Find("LegendWu")?.GetComponentInChildren<TextMeshProUGUI>();
            so.FindProperty("textOthers").objectReferenceValue = leg.Find("LegendOthers")?.GetComponentInChildren<TextMeshProUGUI>();
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Transform EnsureViewModeRow(Transform wm)
    {
        var t = wm.Find("ViewModeRow");
        if (t == null)
        {
            var go = NewUiGo("ViewModeRow", wm);
            t = go.transform;
        }

        var le = t.gameObject.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(t.gameObject);
        le.minHeight = 42f;
        le.preferredHeight = 44f;
        le.flexibleHeight = 0f;

        var h = t.gameObject.GetComponent<HorizontalLayoutGroup>() ?? Undo.AddComponent<HorizontalLayoutGroup>(t.gameObject);
        h.padding = new RectOffset(4, 4, 4, 4);
        h.spacing = 10f;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true;
        h.childControlHeight = false;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = true;

        var tg = t.gameObject.GetComponent<ToggleGroup>() ?? Undo.AddComponent<ToggleGroup>(t.gameObject);
        tg.allowSwitchOff = false;

        Toggle listT;
        if (t.Find("ListToggle") == null)
            listT = CreateLabeledToggle(t, "ListToggle", "리스트", tg, true);
        else
            listT = t.Find("ListToggle").GetComponent<Toggle>();

        Toggle mapT;
        if (t.Find("MapToggle") == null)
            mapT = CreateLabeledToggle(t, "MapToggle", "지도", tg, false);
        else
            mapT = t.Find("MapToggle").GetComponent<Toggle>();

        listT.group = tg;
        mapT.group = tg;

        return t;
    }

    static Toggle CreateLabeledToggle(Transform parent, string name, string label, ToggleGroup group, bool isOn)
    {
        var go = NewUiGo(name, parent);
        var img = go.GetComponent<Image>() ?? Undo.AddComponent<Image>(go);
        img.color = new Color(0.14f, 0.16f, 0.20f, 0.96f);
        var toggle = go.GetComponent<Toggle>() ?? Undo.AddComponent<Toggle>(go);
        toggle.targetGraphic = img;
        toggle.graphic = img;
        toggle.toggleTransition = Toggle.ToggleTransition.None;
        toggle.group = group;
        toggle.isOn = isOn;

        var le = go.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(go);
        le.minHeight = 36f;
        le.flexibleWidth = 1f;

        var labGo = NewUiGo("Label", go.transform);
        StretchFull(labGo.GetComponent<RectTransform>());
        var tmp = labGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 16;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        return toggle;
    }

    static Transform EnsureListViewRoot(Transform wm)
    {
        var t = wm.Find("ListViewRoot");
        if (t == null)
        {
            var go = NewUiGo("ListViewRoot", wm);
            t = go.transform;
        }

        var le = t.gameObject.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(t.gameObject);
        le.minHeight = 280f;
        le.flexibleHeight = 1f;

        var vlg = t.gameObject.GetComponent<VerticalLayoutGroup>() ?? Undo.AddComponent<VerticalLayoutGroup>(t.gameObject);
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = true;
        vlg.spacing = 0f;

        var panel = t.Find("CastleStocksPanel");
        if (panel == null)
        {
            var pGo = NewUiGo("CastleStocksPanel", t);
            panel = pGo.transform;
        }

        var ple = panel.gameObject.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(panel.gameObject);
        ple.minHeight = 668f;
        ple.flexibleWidth = 1f;
        ple.flexibleHeight = 1f;

        var pimg = panel.gameObject.GetComponent<Image>() ?? Undo.AddComponent<Image>(panel.gameObject);
        pimg.color = new Color(0.08f, 0.10f, 0.14f, 0.92f);

        var pv = panel.gameObject.GetComponent<VerticalLayoutGroup>() ?? Undo.AddComponent<VerticalLayoutGroup>(panel.gameObject);
        pv.padding = new RectOffset(10, 10, 10, 10);
        pv.spacing = 8f;
        pv.childControlWidth = true;
        pv.childControlHeight = true;
        pv.childForceExpandWidth = true;
        pv.childForceExpandHeight = true;

        if (panel.Find("Title") == null)
        {
            var titleGo = NewUiGo("Title", panel);
            var tmp = titleGo.AddComponent<TextMeshProUGUI>();
            tmp.text = "Castle Stocks";
            tmp.fontSize = 24;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            var tle = titleGo.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(titleGo);
            tle.minHeight = 36f;
            tle.flexibleWidth = 1f;
        }

        EnsureFilterTabs(panel);
        EnsureCastleStocksScroll(panel);

        return t;
    }

    static void EnsureFilterTabs(Transform panel)
    {
        var tabsRoot = panel.Find("FilterTabs");
        if (tabsRoot == null)
        {
            var go = NewUiGo("FilterTabs", panel);
            tabsRoot = go.transform;
        }

        var h = tabsRoot.gameObject.GetComponent<HorizontalLayoutGroup>() ??
                Undo.AddComponent<HorizontalLayoutGroup>(tabsRoot.gameObject);
        h.padding = new RectOffset(2, 2, 3, 3);
        h.spacing = 3f;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = false;

        var tle = tabsRoot.gameObject.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(tabsRoot.gameObject);
        tle.minHeight = 36f;

        var specs = new (string goName, WorldMarketCastleListFilter filter, string label)[]
        {
            ("FilterTab_All", WorldMarketCastleListFilter.All, "전체"),
            ("FilterTab_My", WorldMarketCastleListFilter.MyHoldings, "보유"),
            ("FilterTab_War", WorldMarketCastleListFilter.War, "전쟁 중"),
            ("FilterTab_Event", WorldMarketCastleListFilter.Event, "이벤트"),
            ("FilterTab_Premium", WorldMarketCastleListFilter.Premium, "우량"),
            ("FilterTab_Attn", WorldMarketCastleListFilter.Attention, "요주의·B~D"),
        };

        var buttons = new Button[specs.Length];
        for (int i = 0; i < specs.Length; i++)
        {
            var spec = specs[i];
            Button btn;
            var existing = tabsRoot.Find(spec.goName);
            if (existing == null)
                btn = CreateFilterTabButton(tabsRoot, spec.goName, spec.label);
            else
                btn = existing.GetComponent<Button>();
            buttons[i] = btn;
        }

        var tabBar = tabsRoot.gameObject.GetComponent<WorldMarketFilterTabBar>() ??
                     Undo.AddComponent<WorldMarketFilterTabBar>(tabsRoot.gameObject);

        var list = panel.GetComponentInChildren<WorldMarketCastleVirtualList>(true);
        var so = new SerializedObject(tabBar);
        so.FindProperty("castleList").objectReferenceValue = list;
        var tabsProp = so.FindProperty("tabs");
        tabsProp.arraySize = specs.Length;
        for (int i = 0; i < specs.Length; i++)
        {
            var el = tabsProp.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("button").objectReferenceValue = buttons[i];
            el.FindPropertyRelative("filter").enumValueIndex = (int)specs[i].filter;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Button CreateFilterTabButton(Transform parent, string name, string label)
    {
        var go = NewUiGo(name, parent);
        var img = go.GetComponent<Image>() ?? Undo.AddComponent<Image>(go);
        img.color = new Color(0.14f, 0.16f, 0.20f, 0.96f);
        var btn = go.GetComponent<Button>() ?? Undo.AddComponent<Button>(go);
        btn.targetGraphic = img;
        var le = go.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(go);
        le.minHeight = 28f;
        le.flexibleWidth = 1f;

        var lab = NewUiGo("Label", go.transform);
        StretchFull(lab.GetComponent<RectTransform>());
        var tmp = lab.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 15;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.78f, 0.80f, 0.84f);
        tmp.raycastTarget = false;
        return btn;
    }

    static void EnsureCastleStocksScroll(Transform panel)
    {
        var scrollTf = panel.Find("CastleStocksScroll");
        if (scrollTf == null)
        {
            var go = NewUiGo("CastleStocksScroll", panel);
            scrollTf = go.transform;
        }

        var sle = scrollTf.gameObject.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(scrollTf.gameObject);
        sle.minHeight = 240f;
        sle.flexibleHeight = 1f;

        var sImg = scrollTf.gameObject.GetComponent<Image>() ?? Undo.AddComponent<Image>(scrollTf.gameObject);
        sImg.color = new Color(0f, 0f, 0f, 0.08f);

        var vpTf = scrollTf.Find("Viewport");
        if (vpTf == null)
        {
            var vpGo = NewUiGo("Viewport", scrollTf);
            vpTf = vpGo.transform;
            StretchFull(vpTf.GetComponent<RectTransform>());
            var vpImg = vpGo.GetComponent<Image>() ?? Undo.AddComponent<Image>(vpGo);
            vpImg.color = Color.white;
            Undo.AddComponent<Mask>(vpGo);
        }

        var contentTf = vpTf.Find("Content");
        if (contentTf == null)
        {
            var cGo = NewUiGo("Content", vpTf);
            contentTf = cGo.transform;
            var crt = contentTf.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.sizeDelta = new Vector2(0f, 0f);
        }

        var scroll = scrollTf.gameObject.GetComponent<ScrollRect>() ?? Undo.AddComponent<ScrollRect>(scrollTf.gameObject);
        scroll.viewport = vpTf.GetComponent<RectTransform>();
        scroll.content = contentTf.GetComponent<RectTransform>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 1f;

        var vList = scrollTf.gameObject.GetComponent<WorldMarketCastleVirtualList>() ??
                    Undo.AddComponent<WorldMarketCastleVirtualList>(scrollTf.gameObject);
        var vSo = new SerializedObject(vList);
        vSo.FindProperty("scrollRect").objectReferenceValue = scroll;
        vSo.FindProperty("content").objectReferenceValue = contentTf;
        vSo.FindProperty("cellStride").floatValue = 232f;
        vSo.FindProperty("poolBufferRows").intValue = 2;
        vSo.FindProperty("deferPoolUntilViewportValid").boolValue = true;

        var filterArea = panel.Find("FilterTabs") as RectTransform;
        vSo.FindProperty("filterChipsReservedArea").objectReferenceValue = filterArea;

        var titleTmp = panel.Find("Title")?.GetComponent<TextMeshProUGUI>();
        vSo.FindProperty("listHeaderText").objectReferenceValue = titleTmp;

        GameObject template = null;
        var existingTpl = contentTf.Find("CastleStockCardTemplate");
        if (existingTpl != null)
            template = existingTpl.gameObject;
        else
            template = BuildCastleCardTemplate(contentTf);

        vSo.FindProperty("cellTemplate").objectReferenceValue = template;
        vSo.ApplyModifiedPropertiesWithoutUndo();
    }

    static GameObject BuildCastleCardTemplate(Transform content)
    {
        var root = NewUiGo("CastleStockCardTemplate", content);
        root.SetActive(false);

        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(100f, 228f);

        var bg = root.GetComponent<Image>() ?? Undo.AddComponent<Image>(root);
        bg.color = new Color(0.10f, 0.12f, 0.16f, 0.98f);
        bg.raycastTarget = true;

        var btn = root.GetComponent<Button>() ?? Undo.AddComponent<Button>(root);
        btn.targetGraphic = bg;

        var le = root.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(root);
        le.minHeight = 228f;
        le.preferredHeight = 228f;

        if (root.GetComponent<WorldMarketCastleCardView>() == null)
            Undo.AddComponent<WorldMarketCastleCardView>(root);

        var vlg = root.GetComponent<VerticalLayoutGroup>() ?? Undo.AddComponent<VerticalLayoutGroup>(root);
        vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.spacing = 4f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        CreateGlossOverlay(root.transform);
        BuildMainRow(root.transform);
        BuildStakeGauge(root.transform);
        BuildFullStretchImage(root.transform, "DisasterOverlay", new Color(0.85f, 0.2f, 0.2f, 0.25f), false);
        BuildFullStretchImage(root.transform, "WarTint", new Color(0.9f, 0.25f, 0.2f, 0.12f), false);

        return root;
    }

    static void CreateGlossOverlay(Transform parent)
    {
        var go = NewUiGo("GlossOverlay", parent);
        StretchFull(go.GetComponent<RectTransform>());
        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 0.94f, 0.78f, 0.09f);
        img.raycastTarget = false;
        go.SetActive(false);
    }

    static void BuildMainRow(Transform cardRoot)
    {
        var main = NewUiGo("MainRow", cardRoot);
        var mle = main.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(main);
        mle.minHeight = 210f;
        mle.preferredHeight = 212f;
        mle.flexibleWidth = 1f;
        var mh = main.GetComponent<HorizontalLayoutGroup>() ?? Undo.AddComponent<HorizontalLayoutGroup>(main);
        mh.spacing = 6f;
        mh.childControlWidth = true;
        mh.childControlHeight = true;
        mh.childForceExpandWidth = true;
        mh.childForceExpandHeight = true;

        BuildZone1(main.transform);
        BuildZone2(main.transform);
        BuildZone3(main.transform);
        BuildZone4(main.transform);
    }

    static void BuildZone1(Transform mainRow)
    {
        var z1 = NewUiGo("Zone1", mainRow);
        var z1h = z1.GetComponent<HorizontalLayoutGroup>() ?? Undo.AddComponent<HorizontalLayoutGroup>(z1);
        z1h.spacing = 4f;
        z1h.childControlWidth = true;
        z1h.childControlHeight = true;
        z1h.childForceExpandWidth = true;
        z1h.childForceExpandHeight = true;

        var z1row = NewUiGo("Z1Row", z1.transform);
        var z1rowH = z1row.GetComponent<HorizontalLayoutGroup>() ?? Undo.AddComponent<HorizontalLayoutGroup>(z1row);
        z1rowH.spacing = 6f;
        z1rowH.childControlWidth = true;
        z1rowH.childControlHeight = true;
        z1rowH.childForceExpandWidth = true;
        z1rowH.childForceExpandHeight = true;

        var gradeBar = NewUiGo("GradeAccentBar", z1row.transform);
        var gbRt = gradeBar.GetComponent<RectTransform>();
        gbRt.sizeDelta = new Vector2(6f, 0f);
        var gbLe = gradeBar.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(gradeBar);
        gbLe.minWidth = 6f;
        gbLe.preferredWidth = 6f;
        gbLe.flexibleWidth = 0f;
        var gbImg = gradeBar.GetComponent<Image>() ?? Undo.AddComponent<Image>(gradeBar);
        gbImg.color = new Color(0.3f, 0.55f, 0.95f, 1f);

        var nameCol = NewUiGo("NameColumn", z1row.transform);
        var nv = nameCol.GetComponent<VerticalLayoutGroup>() ?? Undo.AddComponent<VerticalLayoutGroup>(nameCol);
        nv.spacing = 2f;
        nv.childControlWidth = true;
        nv.childControlHeight = true;
        nv.childForceExpandWidth = true;
        nv.childForceExpandHeight = false;
        var ncle = nameCol.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(nameCol);
        ncle.flexibleWidth = 1f;

        var castleId = NewUiGo("CastleIdLine", nameCol.transform);
        var idTmp = castleId.AddComponent<TextMeshProUGUI>();
        idTmp.text = "지역 · 상태";
        idTmp.fontSize = 14;
        idTmp.color = new Color(0.55f, 0.58f, 0.64f);
        idTmp.raycastTarget = false;

        var nameRow = NewUiGo("NameRow", nameCol.transform);
        var nrh = nameRow.GetComponent<HorizontalLayoutGroup>() ?? Undo.AddComponent<HorizontalLayoutGroup>(nameRow);
        nrh.spacing = 4f;
        nrh.childAlignment = TextAnchor.MiddleLeft;
        nrh.childControlHeight = true;
        nrh.childControlWidth = true;
        nrh.childForceExpandWidth = true;

        var gradeBadge = NewUiGo("GradeBadge", nameRow.transform);
        var gTmp = gradeBadge.AddComponent<TextMeshProUGUI>();
        gTmp.text = "A";
        gTmp.fontSize = 16;
        gTmp.fontStyle = FontStyles.Bold;
        gTmp.color = Color.white;
        gTmp.raycastTarget = false;
        var gLe = gradeBadge.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(gradeBadge);
        gLe.minWidth = 28f;

        var cName = NewUiGo("CastleName", nameRow.transform);
        var cnTmp = cName.AddComponent<TextMeshProUGUI>();
        cnTmp.text = "성 이름";
        cnTmp.fontSize = 20;
        cnTmp.fontStyle = FontStyles.Bold;
        cnTmp.color = Color.white;
        cnTmp.raycastTarget = false;
        var cnLe = cName.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(cName);
        cnLe.flexibleWidth = 1f;

        var icons = NewUiGo("StatusIcons", nameRow.transform);
        var ih = icons.GetComponent<HorizontalLayoutGroup>() ?? Undo.AddComponent<HorizontalLayoutGroup>(icons);
        ih.spacing = 4f;
        CreateStatusIcon(icons.transform, "IconWar", new Color(0.95f, 0.35f, 0.35f));
        CreateStatusIcon(icons.transform, "IconDisaster", new Color(0.5f, 0.65f, 1f));
        CreateStatusIcon(icons.transform, "IconFavorable", new Color(0.45f, 0.85f, 0.55f));
    }

    static void CreateStatusIcon(Transform parent, string name, Color c)
    {
        var go = NewUiGo(name, parent);
        var img = go.GetComponent<Image>() ?? Undo.AddComponent<Image>(go);
        img.color = c;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(22f, 22f);
        var le = go.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(go);
        le.minWidth = 22f;
        le.preferredWidth = 22f;
        go.SetActive(false);
    }

    static void BuildZone2(Transform mainRow)
    {
        var z2 = NewUiGo("Zone2", mainRow);
        var z2v = z2.GetComponent<VerticalLayoutGroup>() ?? Undo.AddComponent<VerticalLayoutGroup>(z2);
        z2v.spacing = 4f;
        z2v.childControlWidth = true;
        z2v.childControlHeight = true;
        z2v.childForceExpandWidth = true;
        var z2le = z2.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(z2);
        z2le.flexibleWidth = 1.2f;
        z2le.minWidth = 120f;

        var buyLabel = NewUiGo("BuyLabel", z2.transform);
        var bl = buyLabel.AddComponent<TextMeshProUGUI>();
        bl.text = "입성료";
        bl.fontSize = 16;
        bl.color = new Color(0.48f, 0.51f, 0.56f);
        bl.raycastTarget = false;

        var buyBg = NewUiGo("BuyPriceBg", z2.transform);
        var bbImg = buyBg.GetComponent<Image>() ?? Undo.AddComponent<Image>(buyBg);
        bbImg.color = new Color(0.06f, 0.08f, 0.12f, 0.95f);
        var priceGo = NewUiGo("BuyPrice", buyBg.transform);
        StretchFull(priceGo.GetComponent<RectTransform>());
        var bp = priceGo.AddComponent<TextMeshProUGUI>();
        bp.text = "0";
        bp.fontSize = 34;
        bp.fontStyle = FontStyles.Bold;
        bp.alignment = TextAlignmentOptions.Right;
        bp.color = Color.white;
        bp.raycastTarget = false;

        var sent = NewUiGo("SentRow", z2.transform);
        var sh = sent.GetComponent<HorizontalLayoutGroup>() ?? Undo.AddComponent<HorizontalLayoutGroup>(sent);
        sh.spacing = 6f;
        sh.childAlignment = TextAnchor.MiddleLeft;

        var arrow = NewUiGo("Arrow", sent.transform);
        var ar = arrow.AddComponent<TextMeshProUGUI>();
        ar.text = "▲";
        ar.fontSize = 18;
        ar.raycastTarget = false;

        var chg = NewUiGo("ChangePct", sent.transform);
        var ct = chg.AddComponent<TextMeshProUGUI>();
        ct.text = "+0.0%";
        ct.fontSize = 16;
        ct.raycastTarget = false;
        var ctle = chg.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(chg);
        ctle.flexibleWidth = 1f;

        var cause = NewUiGo("CauseTag", sent.transform);
        var ctag = cause.AddComponent<TextMeshProUGUI>();
        ctag.text = "";
        ctag.fontSize = 13;
        ctag.color = new Color(0.6f, 0.65f, 0.72f);
        ctag.raycastTarget = false;

        var sparkHost = NewUiGo("SparklineHost", z2.transform);
        var shLe = sparkHost.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(sparkHost);
        shLe.minHeight = 36f;
        shLe.flexibleWidth = 1f;
        StretchFull(sparkHost.GetComponent<RectTransform>());
        var sparkGo = NewUiGo("Sparkline", sparkHost.transform);
        StretchFull(sparkGo.GetComponent<RectTransform>());
        sparkGo.AddComponent<UIMiniSparklineGraphic>();
    }

    static void BuildZone3(Transform mainRow)
    {
        var z3 = NewUiGo("Zone3Personal", mainRow);
        var z3img = z3.GetComponent<Image>() ?? Undo.AddComponent<Image>(z3);
        z3img.color = new Color(0.08f, 0.10f, 0.14f, 0.9f);
        var z3v = z3.GetComponent<VerticalLayoutGroup>() ?? Undo.AddComponent<VerticalLayoutGroup>(z3);
        z3v.spacing = 4f;
        z3v.childControlWidth = true;
        var z3le = z3.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(z3);
        z3le.minWidth = 132f;
        z3le.flexibleWidth = 1f;

        var roiBox = NewUiGo("RoiBox", z3.transform);
        var roiTextGo = NewUiGo("RoiText", roiBox.transform);
        StretchFull(roiTextGo.GetComponent<RectTransform>());
        var roi = roiTextGo.AddComponent<TextMeshProUGUI>();
        roi.text = "ROI —";
        roi.fontSize = 17;
        roi.fontStyle = FontStyles.Bold;
        roi.color = new Color(0.85f, 0.92f, 1f);
        roi.raycastTarget = false;

        var troops = NewUiGo("TroopsLine", z3.transform);
        var tt = troops.AddComponent<TextMeshProUGUI>();
        tt.text = "병력 —";
        tt.fontSize = 15;
        tt.raycastTarget = false;

        var stake = NewUiGo("StakeLine", z3.transform);
        var st = stake.AddComponent<TextMeshProUGUI>();
        st.text = "지분 —";
        st.fontSize = 15;
        st.raycastTarget = false;
    }

    static void BuildZone4(Transform mainRow)
    {
        var z4 = NewUiGo("Zone4Actions", mainRow);
        var z4v = z4.GetComponent<VerticalLayoutGroup>() ?? Undo.AddComponent<VerticalLayoutGroup>(z4);
        z4v.spacing = 6f;
        z4v.childControlWidth = true;
        var z4le = z4.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(z4);
        z4le.minWidth = 100f;
        z4le.flexibleWidth = 0.85f;

        CreateActionButton(z4.transform, "DeployButton", "투입", new Color(0.16f, 0.48f, 0.32f, 0.98f));
        CreateActionButton(z4.transform, "RecallButton", "회수", new Color(0.42f, 0.22f, 0.22f, 0.96f));

        var dist = NewUiGo("DistanceHint", z4.transform);
        var dt = dist.AddComponent<TextMeshProUGUI>();
        dt.text = "";
        dt.fontSize = 11;
        dt.color = new Color(0.68f, 0.72f, 0.78f);
        dt.alignment = TextAlignmentOptions.Center;
        dt.raycastTarget = false;
    }

    static void CreateActionButton(Transform parent, string name, string label, Color c)
    {
        var go = NewUiGo(name, parent);
        var img = go.GetComponent<Image>() ?? Undo.AddComponent<Image>(go);
        img.color = c;
        var btn = go.GetComponent<Button>() ?? Undo.AddComponent<Button>(go);
        btn.targetGraphic = img;
        var le = go.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(go);
        le.minHeight = 42f;
        le.preferredHeight = 46f;
        le.flexibleWidth = 1f;
        var lab = NewUiGo("Label", go.transform);
        StretchFull(lab.GetComponent<RectTransform>());
        var tmp = lab.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 15;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
    }

    static void BuildStakeGauge(Transform cardRoot)
    {
        var bar = NewUiGo("StakeGaugeBar", cardRoot);
        var ble = bar.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(bar);
        ble.minHeight = 10f;
        var bImg = bar.GetComponent<Image>() ?? Undo.AddComponent<Image>(bar);
        bImg.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
        var fill = NewUiGo("Fill", bar.transform);
        StretchFull(fill.GetComponent<RectTransform>());
        var fImg = fill.GetComponent<Image>() ?? Undo.AddComponent<Image>(fill);
        fImg.type = Image.Type.Filled;
        fImg.fillMethod = Image.FillMethod.Horizontal;
        fImg.color = new Color(1f, 0.82f, 0.35f, 0.45f);
        fImg.fillAmount = 0.35f;
        fImg.raycastTarget = false;
    }

    static void BuildFullStretchImage(Transform parent, string name, Color c, bool raycast)
    {
        var go = NewUiGo(name, parent);
        StretchFull(go.GetComponent<RectTransform>());
        var img = go.AddComponent<Image>();
        img.color = c;
        img.raycastTarget = raycast;
        go.SetActive(false);
    }

    static Transform EnsureMapViewRoot(Transform wm)
    {
        var t = wm.Find("MapViewRoot");
        if (t == null)
        {
            var go = NewUiGo("MapViewRoot", wm);
            t = go.transform;
        }

        var le = t.gameObject.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(t.gameObject);
        le.minHeight = 360f;
        le.flexibleHeight = 1f;

        var scrollTf = t.Find("WorldMapScroll");
        if (scrollTf == null)
        {
            var sGo = NewUiGo("WorldMapScroll", t);
            scrollTf = sGo.transform;
        }

        StretchFull(scrollTf.GetComponent<RectTransform>());

        var sImg = scrollTf.gameObject.GetComponent<Image>() ?? Undo.AddComponent<Image>(scrollTf.gameObject);
        sImg.color = new Color(0.07f, 0.08f, 0.10f, 1f);

        var sle = scrollTf.gameObject.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(scrollTf.gameObject);
        sle.minHeight = 320f;
        sle.flexibleWidth = 1f;
        sle.flexibleHeight = 1f;

        var vpTf = scrollTf.Find("Viewport");
        if (vpTf == null)
        {
            var vpGo = NewUiGo("Viewport", scrollTf);
            vpTf = vpGo.transform;
            StretchFull(vpTf.GetComponent<RectTransform>());
            var vpImg = vpGo.GetComponent<Image>() ?? Undo.AddComponent<Image>(vpGo);
            vpImg.color = Color.white;
            Undo.AddComponent<Mask>(vpGo);
        }

        var mapContentTf = vpTf.Find("MapContent");
        if (mapContentTf == null)
        {
            var mc = NewUiGo("MapContent", vpTf);
            mapContentTf = mc.transform;
            var mrt = mapContentTf.GetComponent<RectTransform>();
            mrt.anchorMin = Vector2.zero;
            mrt.anchorMax = Vector2.zero;
            mrt.pivot = Vector2.zero;
            mrt.sizeDelta = new Vector2(2200f, 2200f);
        }
        else
        {
            var mrt = mapContentTf.GetComponent<RectTransform>();
            if (mrt.sizeDelta.sqrMagnitude < 100f)
                mrt.sizeDelta = new Vector2(2200f, 2200f);
        }

        var mapBg = mapContentTf.Find("MapBackground");
        if (mapBg == null)
        {
            var bg = NewUiGo("MapBackground", mapContentTf);
            StretchFull(bg.GetComponent<RectTransform>());
            var img = bg.AddComponent<Image>();
            img.color = new Color(0.11f, 0.13f, 0.16f, 1f);
            img.raycastTarget = false;
        }

        var scroll = scrollTf.gameObject.GetComponent<ScrollRect>() ?? Undo.AddComponent<ScrollRect>(scrollTf.gameObject);
        scroll.content = mapContentTf.GetComponent<RectTransform>();
        scroll.viewport = vpTf.GetComponent<RectTransform>();
        scroll.horizontal = true;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;

        var zoom = scrollTf.gameObject.GetComponent<WorldMarketMapScrollZoom>() ??
                   Undo.AddComponent<WorldMarketMapScrollZoom>(scrollTf.gameObject);
        var zSo = new SerializedObject(zoom);
        zSo.FindProperty("scrollRect").objectReferenceValue = scroll;
        zSo.FindProperty("zoomTarget").objectReferenceValue = mapContentTf;
        zSo.FindProperty("minScale").floatValue = 0.45f;
        zSo.FindProperty("maxScale").floatValue = 2.75f;
        zSo.FindProperty("wheelZoomSensitivity").floatValue = 0.11f;
        zSo.FindProperty("defaultZoom").floatValue = 1.75f;
        zSo.FindProperty("useLegacyMouseWheelZoom").boolValue = true;
        zSo.FindProperty("legacyWheelMultiplier").floatValue = 0.42f;
        zSo.ApplyModifiedPropertiesWithoutUndo();

        if (scrollTf.gameObject.GetComponent<WorldMarketMapViewController>() == null)
            Undo.AddComponent<WorldMarketMapViewController>(scrollTf.gameObject);

        return t;
    }

    static void WireMapBranch(Transform mapRoot, WorldMarketCastleVirtualList list)
    {
        var scrollTf = mapRoot.Find("WorldMapScroll");
        if (scrollTf == null) return;
        var scroll = scrollTf.GetComponent<ScrollRect>();
        var mapContent = scroll != null && scroll.content != null ? scroll.content : null;
        var ctrl = scrollTf.GetComponent<WorldMarketMapViewController>();
        if (ctrl == null) return;

        var so = new SerializedObject(ctrl);
        so.FindProperty("mapScroll").objectReferenceValue = scroll;
        so.FindProperty("mapContent").objectReferenceValue = mapContent;
        so.FindProperty("listSyncTarget").objectReferenceValue = list;
        so.FindProperty("mapWorldMax").floatValue = 1000f;
        so.FindProperty("mapMargin").floatValue = 40f;
        so.FindProperty("mapContentMinSize").floatValue = 2200f;
        so.FindProperty("focusHomeCastleWhenMapOpens").boolValue = true;
        so.FindProperty("invertVerticalFocusScroll").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static WorldMarketCastleVirtualList WireListBranch(Transform listRoot)
    {
        var panel = listRoot.Find("CastleStocksPanel");
        var scrollTf = panel != null ? panel.Find("CastleStocksScroll") : null;
        return scrollTf != null ? scrollTf.GetComponent<WorldMarketCastleVirtualList>() : null;
    }

    static void WireViewModeController(Transform wm, GameObject mapRoot, GameObject listRoot, Transform viewRow)
    {
        var ctrl = wm.GetComponent<WorldMarketViewModeController>();
        if (ctrl == null) return;

        var listToggle = viewRow.Find("ListToggle")?.GetComponent<Toggle>();
        var mapToggle = viewRow.Find("MapToggle")?.GetComponent<Toggle>();
        var panel = listRoot.transform.Find("CastleStocksPanel")?.gameObject;

        var so = new SerializedObject(ctrl);
        so.FindProperty("mapViewRoot").objectReferenceValue = mapRoot;
        so.FindProperty("listViewRoot").objectReferenceValue = listRoot;
        so.FindProperty("warMapViewRoot").objectReferenceValue = null;
        so.FindProperty("mapToggle").objectReferenceValue = mapToggle;
        so.FindProperty("listToggle").objectReferenceValue = listToggle;
        so.FindProperty("warToggle").objectReferenceValue = null;
        so.FindProperty("listOnlyControlsRoot").objectReferenceValue = panel;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static GameObject NewUiGo(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "WorldMarket layout: " + name);
        go.transform.SetParent(parent, false);
        return go;
    }

    static void StretchFull(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }
}
#endif
