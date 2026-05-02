#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HomeScene(구 TestScene) 레이아웃을 "한 번에" 자동 생성/수정합니다.
/// - GlobalUI(탑바/탭바) 영역을 제외하기 위해 Canvas 아래 ContentRoot를 사용합니다.
/// - HomePanels 생성 + 기존 TestScene 기능(만보기 패널, 더미 더미들, 비행 루트/템플릿, CollectionManager 바인딩 등)을 유지합니다.
/// </summary>
public static class HomeSceneLayoutWizard
{
    const string MenuPath = "StockThreeKingdoms/Layout/Home/HomeScene 레이아웃 자동 생성(전체)";
    const string MenuPathV2 = "StockThreeKingdoms/Layout/Home/본영 레이아웃 v2 (3탭·수거형) 생성·교체";

    const float ContentTopInset = 200f;    // GlobalUI TopBar(180) + 여유
    const float ContentBottomInset = 180f; // GlobalUI BottomTabBar(160) + 여유

    const float TmpScale = 1.4f;
    const int TmpMin = 28;
    const int TmpMax = 84;
    const float ButtonMinHeight = 120f;

    [MenuItem(MenuPath, false, 0)]
    static void Run()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        RectTransform contentRoot = EnsureContentRoot(canvas);
        EnsureHomeSceneBootstrapper(canvas);

        // HomePanels 생성/재생성
        var existing = GameObject.Find("HomePanels");
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("HomeScene Layout", "씬에 HomePanels가 이미 있습니다. 새로 만들까요?", "새로 생성", "기존 유지"))
            {
                SetupHomePanels(existing);
                Selection.activeGameObject = existing;
                return;
            }
            Undo.DestroyObjectImmediate(existing);
        }

        GameObject hp = CreateHomePanels(contentRoot);
        SetupHomePanels(hp);

        EditorSceneManager.MarkSceneDirty(hp.scene);
        Selection.activeGameObject = hp;
        Debug.Log("[HomeSceneLayoutWizard] 완료. 저장(Ctrl+S)하세요.");
    }

    /// <summary>기존 HomePanels를 제거하고 본영 v2(주머니·성벽·3서브탭) 레이아웃을 새로 만듭니다.</summary>
    [MenuItem(MenuPathV2, false, 11)]
    static void RunBenYeomLayoutV2()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("본영 v2", "씬에 Canvas가 없습니다.", "확인");
            return;
        }

        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        RectTransform contentRoot = EnsureContentRoot(canvas);
        EnsureHomeSceneBootstrapper(canvas);

        Transform existingHp = contentRoot.Find("HomePanels");
        if (existingHp != null)
        {
            if (!EditorUtility.DisplayDialog("본영 레이아웃 v2",
                    "ContentRoot 아래의 기존 HomePanels를 삭제하고 새 레이아웃을 생성합니다. 계속할까요?",
                    "삭제 후 생성", "취소"))
                return;
            Undo.DestroyObjectImmediate(existingHp.gameObject);
        }

        GameObject hp = CreateHomePanelsV2(contentRoot);
        SetupHomePanelsV2(hp);

        EditorSceneManager.MarkSceneDirty(hp.scene);
        Selection.activeGameObject = hp;
        Debug.Log("[HomeSceneLayoutWizard] 본영 v2 생성 완료. 저장(Ctrl+S)하세요.");
    }

    static GameObject CreateHomePanelsV2(RectTransform parent)
    {
        GameObject root = new GameObject("HomePanels", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        Undo.RegisterCreatedObjectUndo(root, "Create HomePanels v2");
        root.transform.SetParent(parent, false);
        root.GetComponent<Image>().color = new Color(0, 0, 0, 0);

        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        var rootLayout = root.GetComponent<VerticalLayoutGroup>();
        rootLayout.spacing = 14;
        rootLayout.padding = new RectOffset(10, 10, 8, 8);
        rootLayout.childControlHeight = true;
        rootLayout.childControlWidth = true;
        rootLayout.childForceExpandHeight = false;
        rootLayout.childForceExpandWidth = true;

        CreateLocalResourceMirrorRow(root.transform);
        CreatePocketBannerRow(root.transform);
        CreateGoldPileDockRow(root.transform);
        CreateMainWallButton(root.transform);
        CreateFunctionTabsBlock(root.transform);

        var upgradeGrid = root.transform.Find("FunctionTabs/PanelsRoot/BuildingPanel/UpgradeGrid");
        if (upgradeGrid != null)
        {
            CreateLaborPanel(upgradeGrid);
            CreateMarketPanel(upgradeGrid);
            CreateLogisticsPanel(upgradeGrid);
            CreateWarehouseUpgradePanel(upgradeGrid);
        }

        AddScriptsAndWireReferencesV2(root);
        EnsureManagersExist();

        return root;
    }

    static void CreateLocalResourceMirrorRow(Transform parent)
    {
        var row = new GameObject("ResourceBar", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(row, "ResourceBar");
        row.transform.SetParent(parent, false);
        var h = row.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 12f;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        row.GetComponent<LayoutElement>().minHeight = 52f;

        var gold = CreateTmp("GoldText", row.transform, "금화: 0", 32, FontStyles.Bold);
        gold.alignment = TextAlignmentOptions.Left;
        var leG = gold.gameObject.GetComponent<LayoutElement>() ?? gold.gameObject.AddComponent<LayoutElement>();
        leG.flexibleWidth = 2f;

        var troops = CreateTmp("FarmWorkersText", row.transform, "병력: 0", 28, FontStyles.Normal);
        troops.alignment = TextAlignmentOptions.Right;
        var leT = troops.gameObject.GetComponent<LayoutElement>() ?? troops.gameObject.AddComponent<LayoutElement>();
        leT.flexibleWidth = 1f;
    }

    /// <summary>시장 창고 UI 없이 금화 더미(8개)만 배치 — 수거는 GateButton.</summary>
    static void CreateGoldPileDockRow(Transform parent)
    {
        var row = new GameObject("GoldPileDock", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(row, "GoldPileDock");
        row.transform.SetParent(parent, false);
        var h = row.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 6f;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = true;
        var le = row.GetComponent<LayoutElement>();
        le.minHeight = 40f;
        le.preferredHeight = 44f;
        EnsurePilesGrid(row.transform, "PilesGrid");
    }

    static void CreatePocketBannerRow(Transform parent)
    {
        var row = new GameObject("PocketBannerRow", typeof(RectTransform), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(row, "PocketBannerRow");
        row.transform.SetParent(parent, false);
        row.GetComponent<LayoutElement>().minHeight = 44f;
        var tmp = CreateTmp("PocketBannerText", row.transform, "", 26, FontStyles.Italic);
        tmp.color = new Color(1f, 0.92f, 0.45f);
        tmp.alignment = TextAlignmentOptions.Center;
    }

    static void CreateMainWallButton(Transform parent)
    {
        var btn = CreateButton(parent, "GateButton", "성벽 터치 · 수거");
        var le = btn.GetComponent<LayoutElement>() ?? btn.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 160f;
        le.preferredHeight = 180f;
    }

    static void CreateFunctionTabsBlock(Transform parent)
    {
        var ft = new GameObject("FunctionTabs", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(ft, "FunctionTabs");
        ft.transform.SetParent(parent, false);
        var ftLe = ft.GetComponent<LayoutElement>();
        ftLe.flexibleHeight = 3f;
        ftLe.minHeight = 420f;
        var ftV = ft.GetComponent<VerticalLayoutGroup>();
        ftV.spacing = 10f;
        ftV.childControlWidth = true;
        ftV.childControlHeight = true;
        ftV.childForceExpandWidth = true;
        ftV.childForceExpandHeight = false;

        var tabBar = new GameObject("TabBar", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(tabBar, "TabBar");
        tabBar.transform.SetParent(ft.transform, false);
        var h = tabBar.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 8f;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        tabBar.GetComponent<LayoutElement>().minHeight = 110f;

        CreateButton(tabBar.transform, "TabBuildingButton", "내정 강화");
        CreateButton(tabBar.transform, "TabMilitaryButton", "군사 모집");
        CreateButton(tabBar.transform, "TabMarchingButton", "행군 준비");

        var panelsRoot = new GameObject("PanelsRoot", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(panelsRoot, "PanelsRoot");
        panelsRoot.transform.SetParent(ft.transform, false);
        var prLe = panelsRoot.GetComponent<LayoutElement>();
        prLe.flexibleHeight = 2f;
        prLe.minHeight = 300f;
        var prV = panelsRoot.GetComponent<VerticalLayoutGroup>();
        prV.spacing = 0f;
        prV.childAlignment = TextAnchor.UpperCenter;
        prV.childControlWidth = true;
        prV.childControlHeight = true;
        prV.childForceExpandWidth = true;
        prV.childForceExpandHeight = true;

        var building = new GameObject("BuildingPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(building, "BuildingPanel");
        building.transform.SetParent(panelsRoot.transform, false);
        building.GetComponent<Image>().color = new Color(0.14f, 0.16f, 0.22f, 0.95f);
        var bv = building.GetComponent<VerticalLayoutGroup>();
        bv.spacing = 0f;
        bv.padding = new RectOffset(8, 8, 8, 8);
        bv.childAlignment = TextAnchor.UpperCenter;
        bv.childControlWidth = true;
        bv.childControlHeight = true;
        bv.childForceExpandWidth = true;
        bv.childForceExpandHeight = true;
        var bLe = building.GetComponent<LayoutElement>();
        bLe.minHeight = 280f;
        bLe.flexibleHeight = 1f;
        building.SetActive(true);

        var grid = new GameObject("UpgradeGrid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(grid, "UpgradeGrid");
        grid.transform.SetParent(building.transform, false);
        var gridComp = grid.GetComponent<GridLayoutGroup>();
        gridComp.cellSize = new Vector2(320f, 200f);
        gridComp.spacing = new Vector2(10f, 10f);
        gridComp.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridComp.constraintCount = 2;
        gridComp.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridComp.childAlignment = TextAnchor.UpperCenter;
        var gLe = grid.GetComponent<LayoutElement>();
        gLe.flexibleHeight = 1f;
        gLe.flexibleWidth = 1f;
        gLe.minHeight = 420f;

        var military = new GameObject("MilitaryPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(military, "MilitaryPanel");
        military.transform.SetParent(panelsRoot.transform, false);
        military.GetComponent<Image>().color = new Color(0.14f, 0.18f, 0.22f, 0.96f);
        var mv = military.GetComponent<VerticalLayoutGroup>();
        mv.spacing = 10f;
        mv.padding = new RectOffset(14, 14, 12, 12);
        mv.childControlWidth = true;
        mv.childControlHeight = true;
        var mLe = military.GetComponent<LayoutElement>();
        mLe.minHeight = 320f;
        mLe.flexibleHeight = 1f;
        military.SetActive(false);
        mv.childForceExpandWidth = true;

        CreateTmp("MilitaryStockPriceText", military.transform, "현재 도시 병사 시세: — G", 28, FontStyles.Bold);
        CreateSlider(military.transform, "RecruitSlider");
        CreateTmp("RecruitCostText", military.transform, "예상 비용: —", 26, FontStyles.Normal);
        CreateTmp("RecruitUpkeepPreviewText", military.transform, "징집 후 유지비 변화: —", 26, FontStyles.Normal);
        CreateTmp("DischargeExpectGoldText", military.transform, "예상 환급: —", 26, FontStyles.Normal);

        var quickRow = new GameObject("QuickRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        Undo.RegisterCreatedObjectUndo(quickRow, "QuickRow");
        quickRow.transform.SetParent(military.transform, false);
        var qh = quickRow.GetComponent<HorizontalLayoutGroup>();
        qh.spacing = 8f;
        qh.childForceExpandWidth = true;
        qh.childControlWidth = true;
        CreateButton(quickRow.transform, "RecruitPlus1KButton", "+1K");
        CreateButton(quickRow.transform, "RecruitPlus10KButton", "+10K");
        CreateButton(quickRow.transform, "RecruitMaxButton", "MAX");

        var actionRow = new GameObject("RecruitActionRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        Undo.RegisterCreatedObjectUndo(actionRow, "RecruitActionRow");
        actionRow.transform.SetParent(military.transform, false);
        var ah = actionRow.GetComponent<HorizontalLayoutGroup>();
        ah.spacing = 12f;
        ah.childForceExpandWidth = true;
        ah.childControlWidth = true;
        CreateButton(actionRow.transform, "RecruitConfirmButton", "징집하기");
        CreateButton(actionRow.transform, "DischargeConfirmButton", "해산하기");

        var marching = new GameObject("MarchingPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(marching, "MarchingPanel");
        marching.transform.SetParent(panelsRoot.transform, false);
        marching.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.2f, 0.94f);
        var marchV = marching.GetComponent<VerticalLayoutGroup>();
        marchV.spacing = 10f;
        marchV.padding = new RectOffset(12, 12, 10, 12);
        marchV.childAlignment = TextAnchor.UpperCenter;
        marchV.childControlWidth = true;
        marchV.childControlHeight = true;
        marchV.childForceExpandWidth = true;
        marchV.childForceExpandHeight = false;
        var marchLe = marching.GetComponent<LayoutElement>();
        marchLe.minHeight = 280f;
        marchLe.flexibleHeight = 1f;
        marching.SetActive(false);
    }

    static void SetupHomePanelsV2(GameObject hp)
    {
        if (hp == null) return;
        Undo.RegisterFullObjectHierarchyUndo(hp, "HomePanels v2 Setup");

        var ui = hp.GetComponent<HomeUIController>();
        var hc = hp.GetComponent<HomeController>();

        ScaleAllTmpUnder(hp.transform);
        EnsureLayoutElementsOnButtons(hp.transform);

        EnsureWarehouseRowPlacement(hp.transform);

        Transform marchingPanel = hp.transform.Find("FunctionTabs/PanelsRoot/MarchingPanel");
        var ped = marchingPanel != null ? EnsurePedometerPanel(marchingPanel) : EnsurePedometerPanel(hp.transform);

        var flyRoot = EnsureFlyIconsRoot(hp.transform);
        var cm = hp.GetComponent<CollectionManager>() ?? hp.gameObject.AddComponent<CollectionManager>();
        cm.homeController = hc;
        cm.flyIconsRoot = flyRoot;
        EnsureFlyIconTemplates(cm, flyRoot);
        if (cm.poolSize < 10) cm.poolSize = 12;

        var gui = GlobalUIManager.InstanceOrNull;
        cm.goldFlyTarget = gui != null ? gui.AssetsTarget : null;

        AssignPilesNearWarehouseLabels(cm, hp.transform);
        cm.pileBurstRoot = hp.transform.Find("GateButton")?.GetComponent<RectTransform>()
            ?? hp.transform.Find("GoldPileDock") as RectTransform;
        EditorUtility.SetDirty(cm);

        if (ui != null)
        {
            var so = new SerializedObject(ui);
            so.FindProperty("collectionManager").objectReferenceValue = cm;
            WireHomeUiSerializedV2(so, hp.transform, ped);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorUtility.SetDirty(ui);
        EditorUtility.SetDirty(hp);
    }

    static void WireHomeUiSerializedV2(SerializedObject so, Transform root, PedometerRefs ped)
    {
        void SetObj(string prop, Object o)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.objectReferenceValue = o;
        }

        SetObj("goldText", root.Find("ResourceBar/GoldText")?.GetComponent<TextMeshProUGUI>());
        SetObj("farmWorkersText", root.Find("ResourceBar/FarmWorkersText")?.GetComponent<TextMeshProUGUI>());
        SetObj("goldFillSlider", null);
        SetObj("pocketBannerText", root.Find("PocketBannerRow/PocketBannerText")?.GetComponent<TextMeshProUGUI>());
        SetObj("marketAccumulateText", null);
        SetObj("marketAccumulateSlider", null);
        SetObj("gateButton", root.Find("GateButton")?.GetComponent<Button>());

        SetObj("buildingTabButton", root.Find("FunctionTabs/TabBar/TabBuildingButton")?.GetComponent<Button>());
        SetObj("militaryTabButton", root.Find("FunctionTabs/TabBar/TabMilitaryButton")?.GetComponent<Button>());
        SetObj("marchingTabButton", root.Find("FunctionTabs/TabBar/TabMarchingButton")?.GetComponent<Button>());
        SetObj("buildingPanel", root.Find("FunctionTabs/PanelsRoot/BuildingPanel")?.gameObject);
        SetObj("militaryPanel", root.Find("FunctionTabs/PanelsRoot/MilitaryPanel")?.gameObject);
        SetObj("marchingPanel", root.Find("FunctionTabs/PanelsRoot/MarchingPanel")?.gameObject);

        var bg = root.Find("FunctionTabs/PanelsRoot/BuildingPanel/UpgradeGrid");
        SetObj("laborLabelText", bg?.Find("LaborPanel/LaborLabelText")?.GetComponent<TextMeshProUGUI>());
        SetObj("laborUpgradeButton", bg?.Find("LaborPanel/LaborUpgradeButton")?.GetComponent<Button>());
        SetObj("marketLabelText", bg?.Find("MarketPanel/MarketLabelText")?.GetComponent<TextMeshProUGUI>());
        SetObj("marketUpgradeButton", bg?.Find("MarketPanel/MarketButtons/MarketUpgradeButton")?.GetComponent<Button>());
        SetObj("logisticsLabelText", bg?.Find("LogisticsPanel/LogisticsLabelText")?.GetComponent<TextMeshProUGUI>());
        SetObj("logisticsUpgradeButton", bg?.Find("LogisticsPanel/LogisticsButtons/LogisticsUpgradeButton")?.GetComponent<Button>());
        SetObj("warehouseLabelText", bg?.Find("WarehouseUpgradePanel/WarehouseLabelText")?.GetComponent<TextMeshProUGUI>());
        SetObj("warehouseUpgradeButton", bg?.Find("WarehouseUpgradePanel/WarehouseButtons/WarehouseUpgradeButton")?.GetComponent<Button>());

        var mp = root.Find("FunctionTabs/PanelsRoot/MilitaryPanel");
        SetObj("militaryStockPriceText", mp?.Find("MilitaryStockPriceText")?.GetComponent<TextMeshProUGUI>());
        SetObj("recruitSlider", mp?.Find("RecruitSlider")?.GetComponent<Slider>());
        SetObj("recruitCostText", mp?.Find("RecruitCostText")?.GetComponent<TextMeshProUGUI>());
        SetObj("recruitUpkeepPreviewText", mp?.Find("RecruitUpkeepPreviewText")?.GetComponent<TextMeshProUGUI>());
        SetObj("dischargeExpectGoldText", mp?.Find("DischargeExpectGoldText")?.GetComponent<TextMeshProUGUI>());
        SetObj("recruitPlus1KButton", mp?.Find("QuickRow/RecruitPlus1KButton")?.GetComponent<Button>());
        SetObj("recruitPlus10KButton", mp?.Find("QuickRow/RecruitPlus10KButton")?.GetComponent<Button>());
        SetObj("recruitMaxButton", mp?.Find("QuickRow/RecruitMaxButton")?.GetComponent<Button>());
        SetObj("recruitConfirmButton", mp?.Find("RecruitActionRow/RecruitConfirmButton")?.GetComponent<Button>());
        SetObj("dischargeConfirmButton", mp?.Find("RecruitActionRow/DischargeConfirmButton")?.GetComponent<Button>());

        if (ped.gaugeFill != null)
            SetObj("pedometerGaugeFill", ped.gaugeFill);
        if (ped.stepsText != null)
            SetObj("pedometerStepsText", ped.stepsText);
        var arr = so.FindProperty("stepRewardButtons");
        if (arr != null && ped.buttons != null)
        {
            arr.arraySize = 4;
            for (int i = 0; i < 4; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = ped.buttons[i];
        }
        var labels = so.FindProperty("stepRewardLabels");
        if (labels != null && ped.labels != null)
        {
            labels.arraySize = 4;
            for (int i = 0; i < 4; i++)
                labels.GetArrayElementAtIndex(i).objectReferenceValue = ped.labels[i];
        }
    }

    static void AddScriptsAndWireReferencesV2(GameObject root)
    {
        if (root.GetComponent<HomeController>() == null)
            root.AddComponent<HomeController>();
        if (root.GetComponent<HomeUIController>() == null)
            root.AddComponent<HomeUIController>();
        if (root.GetComponent<CollectionManager>() == null)
            root.AddComponent<CollectionManager>();
        EditorUtility.SetDirty(root);
    }

    static void EnsureHomeSceneBootstrapper(Canvas canvas)
    {
        var existing = Object.FindObjectOfType<HomeSceneBootstrapper>();
        if (existing != null) return;

        var go = new GameObject("HomeSceneBootstrapper");
        Undo.RegisterCreatedObjectUndo(go, "Create HomeSceneBootstrapper");
        go.transform.SetParent(canvas.transform, false);
        var b = go.AddComponent<HomeSceneBootstrapper>();

        // Prefab 자동 연결(에디터에서만)
        b.globalUiManagerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/CommonUI/Prefabs/GlobalUIManager.prefab");
        b.gameManagerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/0Splash/Prefab/GameManager.prefab");
        b.dataManagerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/0Splash/Prefab/DataManager.prefab");
        b.googleSheetManagerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/0Splash/Prefab/GoogleSheetManager.prefab");

        EditorUtility.SetDirty(b);
    }

    static RectTransform EnsureContentRoot(Canvas canvas)
    {
        var parent = canvas.transform as RectTransform;
        var t = parent.Find("ContentRoot");
        RectTransform rt;
        if (t == null)
        {
            var go = new GameObject("ContentRoot", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create ContentRoot");
            go.transform.SetParent(parent, false);
            rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(20f, ContentBottomInset);
            rt.offsetMax = new Vector2(-20f, -ContentTopInset);
        }
        else
            rt = t as RectTransform;
        return rt;
    }

    static GameObject CreateHomePanels(RectTransform parent)
    {
        GameObject root = new GameObject("HomePanels", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        Undo.RegisterCreatedObjectUndo(root, "Create HomePanels");
        root.transform.SetParent(parent, false);

        var rootBg = root.GetComponent<Image>();
        rootBg.color = new Color(0, 0, 0, 0);

        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0, 0);
        rootRect.anchorMax = new Vector2(1, 1);
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        var rootLayout = root.GetComponent<VerticalLayoutGroup>();
        rootLayout.spacing = 18;
        rootLayout.padding = new RectOffset(8, 8, 8, 8);
        rootLayout.childControlHeight = true;
        rootLayout.childControlWidth = true;
        rootLayout.childForceExpandHeight = false;
        rootLayout.childForceExpandWidth = true;

        // GateButton은 상단에 먼저 배치(홈의 핵심 액션)
        CreateResourceBar(root.transform); // 현재는 GateButton만 생성

        CreateLaborPanel(root.transform);
        CreateMarketPanel(root.transform);
        CreateWarehouseUpgradePanel(root.transform);
        CreateLogisticsPanel(root.transform);
        CreateWarehouseRow(root.transform);
        CreateSupplyPanel(root.transform);
        CreateRecruitPanel(root.transform);

        AddScriptsAndWireReferences(root);
        EnsureManagersExist();

        return root;
    }

    static void SetupHomePanels(GameObject hp)
    {
        if (hp == null) return;
        Undo.RegisterFullObjectHierarchyUndo(hp, "HomePanels Setup");

        var ui = hp.GetComponent<HomeUIController>();
        var hc = hp.GetComponent<HomeController>();
        var vlg = hp.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            vlg.spacing = 24f;
            vlg.padding = new RectOffset(12, 12, 12, 12);
        }

        ScaleAllTmpUnder(hp.transform);
        EnsureLayoutElementsOnButtons(hp.transform);

        // ResourceBar는 GlobalUI로 대체 (HomePanels 내부 생성/표시 안 함)

        EnsureCenterRow(hp.transform);
        EnsureWarehouseRowPlacement(hp.transform);
        var ped = EnsurePedometerPanel(hp.transform);
        var flyRoot = EnsureFlyIconsRoot(hp.transform);

        var cm = hp.GetComponent<CollectionManager>() ?? hp.gameObject.AddComponent<CollectionManager>();
        cm.homeController = hc;
        cm.flyIconsRoot = flyRoot;
        EnsureFlyIconTemplates(cm, flyRoot);
        if (cm.poolSize < 10) cm.poolSize = 12;

        var gui = GlobalUIManager.InstanceOrNull;
        var goldRt = gui != null ? gui.AssetsTarget : null;
        cm.goldFlyTarget = goldRt;

        AssignPilesNearWarehouseLabels(cm, hp.transform);
        EditorUtility.SetDirty(cm);

        if (ui != null)
        {
            var so = new SerializedObject(ui);
            so.FindProperty("collectionManager").objectReferenceValue = cm;
            if (ped.gaugeFill != null)
                so.FindProperty("pedometerGaugeFill").objectReferenceValue = ped.gaugeFill;
            if (ped.stepsText != null)
                so.FindProperty("pedometerStepsText").objectReferenceValue = ped.stepsText;
            var arr = so.FindProperty("stepRewardButtons");
            arr.arraySize = 4;
            for (int i = 0; i < 4; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = ped.buttons[i];
            var labels = so.FindProperty("stepRewardLabels");
            labels.arraySize = 4;
            for (int i = 0; i < 4; i++)
                labels.GetArrayElementAtIndex(i).objectReferenceValue = ped.labels[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void EnsureWarehouseRowPlacement(Transform homeRoot)
    {
        var row = homeRoot.Find("WarehouseRow");
        if (row == null) return;

        // CenterPanelsRow 바로 아래에 오도록 위치 조정
        var center = homeRoot.Find("CenterPanelsRow");
        if (center != null)
            row.SetSiblingIndex(center.GetSiblingIndex() + 1);
    }

    // ---- 아래부터는 기존 HomePanelCreator / HomeTestSceneLayoutWizard 로직 통합(필요 최소만) ----

    static void EnsureManagersExist()
    {
        if (Object.FindObjectOfType<GameManager>() == null)
        {
            GameObject gm = new GameObject("GameManager");
            Undo.RegisterCreatedObjectUndo(gm, "Create GameManager");
            gm.AddComponent<GameManager>();
        }
        if (Object.FindObjectOfType<DataManager>() == null)
        {
            GameObject dm = new GameObject("DataManager");
            Undo.RegisterCreatedObjectUndo(dm, "Create DataManager");
            dm.AddComponent<DataManager>();
        }
        if (Object.FindObjectOfType<GoogleSheetManager>() == null)
        {
            GameObject gsm = new GameObject("GoogleSheetManager");
            Undo.RegisterCreatedObjectUndo(gsm, "Create GoogleSheetManager");
            gsm.AddComponent<GoogleSheetManager>();
        }
    }

    static void AddScriptsAndWireReferences(GameObject root)
    {
        if (root.GetComponent<HomeController>() == null)
            root.AddComponent<HomeController>();

        HomeUIController ui = root.GetComponent<HomeUIController>();
        if (ui == null) ui = root.AddComponent<HomeUIController>();

        Transform t = root.transform;
        ui.goldText = t.Find("ResourceBar/GoldText")?.GetComponent<TextMeshProUGUI>();
        ui.farmWorkersText = t.Find("ResourceBar/FarmWorkersText")?.GetComponent<TextMeshProUGUI>();
        ui.gateButton = t.Find("GateButton")?.GetComponent<Button>();

        ui.laborLabelText = t.Find("LaborPanel/LaborLabelText")?.GetComponent<TextMeshProUGUI>();
        ui.laborUpgradeButton = t.Find("LaborPanel/LaborUpgradeButton")?.GetComponent<Button>();

        ui.marketLabelText = t.Find("MarketPanel/MarketLabelText")?.GetComponent<TextMeshProUGUI>();
        ui.marketAccumulateText = t.Find("WarehouseRow/WarehousePanelsRow/MarketWarehouse/MarketAccumulateText")?.GetComponent<TextMeshProUGUI>();
        ui.marketAccumulateSlider = t.Find("WarehouseRow/WarehousePanelsRow/MarketWarehouse/MarketAccumulateSlider")?.GetComponent<Slider>();
        ui.marketUpgradeButton = t.Find("MarketPanel/MarketButtons/MarketUpgradeButton")?.GetComponent<Button>();
        ui.collectMarketButton = t.Find("WarehouseRow/CollectAllWarehouseButton")?.GetComponent<Button>();
        ui.warehouseLabelText = t.Find("WarehouseUpgradePanel/WarehouseLabelText")?.GetComponent<TextMeshProUGUI>();
        ui.warehouseUpgradeButton = t.Find("WarehouseUpgradePanel/WarehouseButtons/WarehouseUpgradeButton")?.GetComponent<Button>();

        ui.logisticsLabelText = t.Find("LogisticsPanel/LogisticsLabelText")?.GetComponent<TextMeshProUGUI>()
            ?? t.Find("FarmPanel/FarmLabelText")?.GetComponent<TextMeshProUGUI>();
        ui.logisticsUpgradeButton = t.Find("LogisticsPanel/LogisticsButtons/LogisticsUpgradeButton")?.GetComponent<Button>()
            ?? t.Find("FarmPanel/FarmButtons/FarmUpgradeButton")?.GetComponent<Button>();

        ui.supplyLabelText = t.Find("SupplyPanel/SupplyLabelText")?.GetComponent<TextMeshProUGUI>();
        ui.recruitSoldierButton = t.Find("RecruitPanel/RecruitSoldierButton")?.GetComponent<Button>();

        EditorUtility.SetDirty(ui);
    }

    static void ScaleAllTmpUnder(Transform root)
    {
        var tmps = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var t in tmps)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(t.fontSize * TmpScale), TmpMin, TmpMax);
            Undo.RecordObject(t, "TMP scale");
            t.fontSize = n;
            EditorUtility.SetDirty(t);
        }
    }

    static void EnsureLayoutElementsOnButtons(Transform root)
    {
        var buttons = root.GetComponentsInChildren<Button>(true);
        foreach (var b in buttons)
        {
            var le = b.GetComponent<LayoutElement>() ?? b.gameObject.AddComponent<LayoutElement>();
            Undo.RecordObject(le, "Button LayoutElement");
            le.minHeight = Mathf.Max(le.minHeight, ButtonMinHeight);
            EditorUtility.SetDirty(le);
        }
    }

    static void CreateResourceBar(Transform parent)
    {
        // ResourceBar는 GlobalUIManager 탑바로 대체합니다.
        var btn = CreateButton(parent, "GateButton", "대문 터치 (금화 획득)");
        var le = btn.GetComponent<LayoutElement>() ?? btn.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 140f;
        le.preferredHeight = 160f;
    }

    static GameObject CreatePanel(Transform parent, string name, string title)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        Undo.RegisterCreatedObjectUndo(panel, name);
        panel.transform.SetParent(parent, false);
        panel.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        panel.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 140);
        var layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8;
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        CreateText(panel.transform, "Title", title, 18, FontStyles.Bold);
        return panel;
    }

    static void CreateLaborPanel(Transform parent)
    {
        GameObject panel = CreatePanel(parent, "LaborPanel", "노동력 (클릭당 금화)");
        CreateText(panel.transform, "LaborLabelText", "클릭당 금화 획득량 상승\n(Level 1)\n비용: 50 Gold", 14);
        CreateButton(panel.transform, "LaborUpgradeButton", "노동력 업그레이드");
    }

    static void CreateMarketPanel(Transform parent)
    {
        GameObject panel = CreatePanel(parent, "MarketPanel", "시장 (초당 금화)");
        CreateText(panel.transform, "MarketLabelText", "초당 금화 자동 생산\n(Level 0)\n비용: 100 Gold", 14);
        GameObject btnRow = new GameObject("MarketButtons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        Undo.RegisterCreatedObjectUndo(btnRow, "MarketButtons");
        btnRow.transform.SetParent(panel.transform, false);
        var hlg = btnRow.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        var upgrade = CreateButton(btnRow.transform, "MarketUpgradeButton", "업그레이드");
        var ule = upgrade.GetComponent<LayoutElement>() ?? upgrade.gameObject.AddComponent<LayoutElement>();
        ule.minHeight = 140f;
        ule.preferredHeight = 160f;
        ule.flexibleHeight = 1f;
    }

    static void CreateLogisticsPanel(Transform parent)
    {
        GameObject panel = CreatePanel(parent, "LogisticsPanel", "병참 (유지비 할인)");
        CreateText(panel.transform, "LogisticsLabelText", "보유 병사 일일 유지비 감소\n(Level 0)\n비용: 80 Gold", 14);
        GameObject btnRow = new GameObject("LogisticsButtons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        Undo.RegisterCreatedObjectUndo(btnRow, "LogisticsButtons");
        btnRow.transform.SetParent(panel.transform, false);
        var hlg = btnRow.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        var upgrade = CreateButton(btnRow.transform, "LogisticsUpgradeButton", "업그레이드");
        var ule = upgrade.GetComponent<LayoutElement>() ?? upgrade.gameObject.AddComponent<LayoutElement>();
        ule.minHeight = 140f;
        ule.preferredHeight = 160f;
        ule.flexibleHeight = 1f;
    }

    static void CreateWarehouseUpgradePanel(Transform parent)
    {
        GameObject panel = CreatePanel(parent, "WarehouseUpgradePanel", "창고 (저장 한도)");
        CreateText(panel.transform, "WarehouseLabelText", "시장 창고 최대 저장량 상승\n(Level 0)\n비용: 90 Gold", 14);
        GameObject btnRow = new GameObject("WarehouseButtons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        Undo.RegisterCreatedObjectUndo(btnRow, "WarehouseButtons");
        btnRow.transform.SetParent(panel.transform, false);
        var hlg = btnRow.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        var upgrade = CreateButton(btnRow.transform, "WarehouseUpgradeButton", "업그레이드");
        var ule = upgrade.GetComponent<LayoutElement>() ?? upgrade.gameObject.AddComponent<LayoutElement>();
        ule.minHeight = 140f;
        ule.preferredHeight = 160f;
        ule.flexibleHeight = 1f;
    }

    static void CreateWarehouseRow(Transform parent)
    {
        var row = new GameObject("WarehouseRow", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(row, "WarehouseRow");
        row.transform.SetParent(parent, false);

        var le = row.AddComponent<LayoutElement>();
        le.minHeight = 220f;
        le.flexibleWidth = 1f;

        // 위: 시장/농장 2패널(가로), 아래: 회수버튼(전체폭)
        var v = row.AddComponent<VerticalLayoutGroup>();
        // 수거 버튼이 너무 아래로 떨어져 보이지 않게 간격을 줄임
        v.spacing = 6f;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        // 수거 버튼은 1개만: 시장/농장 창고를 한 번에 수거
        var panelsRow = new GameObject("WarehousePanelsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(panelsRow, "WarehousePanelsRow");
        panelsRow.transform.SetParent(row.transform, false);
        var h = panelsRow.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 16f;
        h.childAlignment = TextAnchor.UpperCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = false;
        var panelsLe = panelsRow.GetComponent<LayoutElement>();
        panelsLe.flexibleWidth = 1f;

        CreateWarehousePanel(panelsRow.transform, "MarketWarehouse", "시장 창고", "MarketAccumulateText", "MarketAccumulateSlider", null);
        CreateWarehouseCollectAllButton(row.transform);
    }

    static void CreateWarehousePanel(Transform parent, string name, string title, string accTextName, string sliderName, string collectBtnName)
    {
        var panel = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(panel, name);
        panel.transform.SetParent(parent, false);

        var img = panel.GetComponent<Image>();
        img.color = new Color(0.12f, 0.14f, 0.2f, 0.92f);

        var le = panel.GetComponent<LayoutElement>();
        le.flexibleWidth = 1f;
        le.minWidth = 260f;
        le.minHeight = 220f;

        var v = panel.GetComponent<VerticalLayoutGroup>();
        v.spacing = 10f;
        v.padding = new RectOffset(14, 14, 12, 12);
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        // 헤더: 라벨 + (라벨 옆 빈 공간에) 2열 더미 그리드
        var header = new GameObject("HeaderRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(header, "HeaderRow");
        header.transform.SetParent(panel.transform, false);
        var h = header.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 10f;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = false;
        header.GetComponent<LayoutElement>().minHeight = 44f;

        var titleTmp = CreateText(header.transform, "Title", title, 16, FontStyles.Bold);
        var titleLe = titleTmp.GetComponent<LayoutElement>() ?? titleTmp.gameObject.AddComponent<LayoutElement>();
        titleLe.flexibleWidth = 1f;
        titleLe.minWidth = 120f;

        EnsurePilesGrid(header.transform, "PilesGrid");

        CreateText(panel.transform, accTextName, "0 / 0", 14);
        CreateSlider(panel.transform, sliderName);

        if (!string.IsNullOrEmpty(collectBtnName))
        {
            var btn = CreateButton(panel.transform, collectBtnName, "수거");
            var btnLe = btn.GetComponent<LayoutElement>() ?? btn.gameObject.AddComponent<LayoutElement>();
            btnLe.minHeight = 110f;
            btnLe.preferredHeight = 120f;
        }
    }

    static void CreateWarehouseCollectAllButton(Transform warehouseRow)
    {
        if (warehouseRow == null) return;
        var existing = warehouseRow.Find("CollectAllWarehouseButton")?.GetComponent<Button>();
        if (existing != null) return;

        var btn = CreateButton(warehouseRow, "CollectAllWarehouseButton", "창고 수거");
        var le = btn.GetComponent<LayoutElement>() ?? btn.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 110f;
        le.preferredHeight = 120f;
        le.flexibleWidth = 1f;
    }

    static void CreateSupplyPanel(Transform parent)
    {
        GameObject panel = CreatePanel(parent, "SupplyPanel", "보급");
        CreateText(panel.transform, "SupplyLabelText", "병사는 천하 탭에서 매수합니다.\n재화는 금화만 사용합니다.", 12);
    }

    static void CreateRecruitPanel(Transform parent)
    {
        GameObject panel = CreatePanel(parent, "RecruitPanel", "병사 징집/해산");
        CreateText(panel.transform, "RecruitHintText", "병사 모집/해산 팝업을 엽니다.", 13);
        var btn = CreateButton(panel.transform, "RecruitSoldierButton", "병사 징집");
        var le = btn.GetComponent<LayoutElement>() ?? btn.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 120f;
        le.preferredHeight = 136f;
    }

    static GameObject CreateText(Transform parent, string name, string content, int fontSize, FontStyles style = FontStyles.Normal)
    {
        // ContentSizeFitter horizontal Unconstrained 는 부모 폭이 0일 때 TMP가 한 글자씩 세로로 쌓이는 원인이 됨.
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(obj, name);
        obj.transform.SetParent(parent, false);
        var tmp = obj.GetComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = Color.white;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;

        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, Mathf.Max(72f, fontSize * 4f));

        var le = obj.GetComponent<LayoutElement>();
        le.flexibleWidth = 1f;
        le.minWidth = 200f;
        le.minHeight = Mathf.Max(fontSize * 2 + 24, 56f);
        le.preferredHeight = le.minHeight;
        return obj;
    }

    static Button CreateButton(Transform parent, string name, string label)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(obj, name);
        obj.transform.SetParent(parent, false);
        obj.GetComponent<Image>().color = new Color(0.3f, 0.5f, 0.8f);
        var tmpGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(tmpGo, "BtnText");
        tmpGo.transform.SetParent(obj.transform, false);
        var rt = tmpGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var tmp = tmpGo.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 14;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        obj.GetComponent<LayoutElement>().preferredHeight = 36;
        return obj.GetComponent<Button>();
    }

    static Slider CreateSlider(Transform parent, string name)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Slider), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(root, name);
        root.transform.SetParent(parent, false);

        var bg = root.GetComponent<Image>();
        bg.color = new Color(0.22f, 0.24f, 0.28f, 1f);

        var slider = root.GetComponent<Slider>();
        slider.transition = Selectable.Transition.ColorTint;
        slider.navigation = Navigation.defaultNavigation;
        slider.direction = Slider.Direction.LeftToRight;

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(fillArea, "Fill Area");
        fillArea.transform.SetParent(root.transform, false);
        var fillAreaRt = fillArea.GetComponent<RectTransform>();
        fillAreaRt.anchorMin = new Vector2(0f, 0.2f);
        fillAreaRt.anchorMax = new Vector2(1f, 0.8f);
        fillAreaRt.offsetMin = new Vector2(10f, 0f);
        fillAreaRt.offsetMax = new Vector2(-10f, 0f);

        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(fillGo, "Fill");
        fillGo.transform.SetParent(fillArea.transform, false);
        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        var fillImg = fillGo.GetComponent<Image>();
        fillImg.color = new Color(0.35f, 0.65f, 1f, 1f);

        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(handleArea, "Handle Slide Area");
        handleArea.transform.SetParent(root.transform, false);
        var haRt = handleArea.GetComponent<RectTransform>();
        haRt.anchorMin = Vector2.zero;
        haRt.anchorMax = Vector2.one;
        haRt.offsetMin = new Vector2(10f, 0f);
        haRt.offsetMax = new Vector2(-10f, 0f);

        var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(handleGo, "Handle");
        handleGo.transform.SetParent(handleArea.transform, false);
        var handleRt = handleGo.GetComponent<RectTransform>();
        handleRt.anchorMin = new Vector2(0f, 0.5f);
        handleRt.anchorMax = new Vector2(0f, 0.5f);
        handleRt.pivot = new Vector2(0.5f, 0.5f);
        handleRt.sizeDelta = new Vector2(28f, 28f);
        var handleImg = handleGo.GetComponent<Image>();
        handleImg.color = new Color(1f, 0.92f, 0.45f, 1f);

        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.targetGraphic = handleImg;

        var le = root.GetComponent<LayoutElement>();
        le.minHeight = 40f;
        le.preferredHeight = 44f;
        le.flexibleWidth = 1f;

        var rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0f, 1f);
        rootRt.anchorMax = new Vector2(1f, 1f);
        rootRt.pivot = new Vector2(0.5f, 1f);
        rootRt.sizeDelta = new Vector2(0f, 44f);

        return slider;
    }

    // ---- 아래는 기존 HomeTestSceneLayoutWizard의 나머지 유틸(센터행/만보기/더미/비행/더미아이콘) ----
    // 길어서 원본을 유지하고 싶다면 추후 분리해도 됩니다.

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform t in root)
        {
            var f = FindDeep(t, name);
            if (f != null) return f;
        }
        return null;
    }

    static void EnsureCenterRow(Transform homeRoot)
    {
        Transform labor = FindDeep(homeRoot, "LaborPanel");
        Transform market = FindDeep(homeRoot, "MarketPanel");
        Transform warehouse = FindDeep(homeRoot, "WarehouseUpgradePanel");
        Transform farm = FindDeep(homeRoot, "LogisticsPanel");
        if (farm == null) farm = FindDeep(homeRoot, "FarmPanel");
        if (labor == null || market == null || warehouse == null || farm == null) return;

        Transform existing = homeRoot.Find("CenterPanelsRow");
        RectTransform rowRt;
        if (existing != null)
            rowRt = existing as RectTransform;
        else
        {
            var go = new GameObject("CenterPanelsRow", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Center Row");
            go.transform.SetParent(homeRoot, false);
            rowRt = go.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0, 0);
            rowRt.anchorMax = new Vector2(1, 1);
            rowRt.sizeDelta = Vector2.zero;

            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 16f;
            h.childAlignment = TextAnchor.UpperCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = false;
            var le = go.AddComponent<LayoutElement>();
            le.flexibleHeight = 2f;
            le.minHeight = 320f;
        }

        void Reparent(Transform t)
        {
            if (t == null) return;
            Undo.SetTransformParent(t, rowRt, "Center Row Reparent");
            var childLe = t.GetComponent<LayoutElement>() ?? t.gameObject.AddComponent<LayoutElement>();
            childLe.flexibleWidth = 1f;
            childLe.minWidth = 200f;
        }

        Reparent(labor);
        Reparent(market);
        Reparent(warehouse);
        Reparent(farm);

        Transform gate = homeRoot.Find("GateButton");
        rowRt.SetParent(homeRoot, false);
        if (gate != null)
            rowRt.SetSiblingIndex(gate.GetSiblingIndex() + 1);
    }

    struct PedometerRefs
    {
        public Image gaugeFill;
        public TextMeshProUGUI stepsText;
        public Button[] buttons;
        public TextMeshProUGUI[] labels;
    }

    static PedometerRefs EnsurePedometerPanel(Transform homeRoot)
    {
        // 원본 로직이 길어서 그대로 유지: 기존 파일을 삭제하기 전에 이 파일에 포함한 형태.
        // (여기선 핵심 참조만 유지)
        var refs = new PedometerRefs { buttons = new Button[4], labels = new TextMeshProUGUI[4] };
        Transform ped = homeRoot.Find("PedometerPanel");
        if (ped == null)
        {
            var go = new GameObject("PedometerPanel", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "Pedometer");
            go.transform.SetParent(homeRoot, false);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.12f, 0.14f, 0.2f, 0.92f);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 220f;
            le.flexibleWidth = 1f;
            le.flexibleHeight = 1f;
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(16, 16, 12, 12);
            v.spacing = 12f;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandHeight = false;

            CreateTmp("PedometerTitle", go.transform, "만보기 (목표: 10,000보)", 40, FontStyles.Bold);
            refs.stepsText = CreateTmp("StepsCount", go.transform, "0 / 10,000", 36, FontStyles.Normal);

            var gaugeBgGo = new GameObject("GaugeBackground", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            Undo.RegisterCreatedObjectUndo(gaugeBgGo, "GaugeBG");
            gaugeBgGo.transform.SetParent(go.transform, false);
            var gaugeBgRt = gaugeBgGo.GetComponent<RectTransform>();
            gaugeBgRt.sizeDelta = new Vector2(0, 36);
            gaugeBgGo.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f, 1f);
            var gLe = gaugeBgGo.GetComponent<LayoutElement>();
            gLe.minHeight = 40f;
            gLe.preferredHeight = 44f;
            gLe.flexibleWidth = 1f;

            var fillGo = new GameObject("GaugeFill", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(fillGo, "GaugeFill");
            fillGo.transform.SetParent(gaugeBgRt, false);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(4, 4);
            fillRt.offsetMax = new Vector2(-4, -4);
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 0f;
            fillImg.color = new Color(0.3f, 0.85f, 0.45f, 1f);
            refs.gaugeFill = fillImg;

            // 보상 버튼 4개 (간단 버전)
            var row = new GameObject("MilestoneRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            Undo.RegisterCreatedObjectUndo(row, "MilestoneRow");
            row.transform.SetParent(go.transform, false);
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 8f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;

            int[] miles = { 2000, 5000, 7000, 10000 };
            for (int i = 0; i < 4; i++)
            {
                var btnGo = new GameObject($"StepReward_{miles[i]}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                Undo.RegisterCreatedObjectUndo(btnGo, "StepReward");
                btnGo.transform.SetParent(row.transform, false);
                btnGo.GetComponent<Image>().color = new Color(0.25f, 0.35f, 0.5f, 1f);
                var btnLe = btnGo.GetComponent<LayoutElement>();
                btnLe.minHeight = 120f;
                btnLe.flexibleWidth = 1f;
                var lbl = CreateTmp("Label", btnGo.transform, $"{miles[i]:N0}보\n보상", 28, FontStyles.Normal);
                lbl.alignment = TextAlignmentOptions.Center;
                refs.buttons[i] = btnGo.GetComponent<Button>();
                refs.labels[i] = lbl;
            }
        }
        else
        {
            var pedRt = ped as RectTransform;
            refs.stepsText = pedRt.Find("StepsCount")?.GetComponent<TextMeshProUGUI>();
            refs.gaugeFill = pedRt.Find("GaugeBackground/GaugeFill")?.GetComponent<Image>();
            int[] miles = { 2000, 5000, 7000, 10000 };
            for (int i = 0; i < 4; i++)
            {
                var t = pedRt.Find($"StepReward_{miles[i]}");
                if (t != null)
                {
                    refs.buttons[i] = t.GetComponent<Button>();
                    refs.labels[i] = t.Find("Label")?.GetComponent<TextMeshProUGUI>();
                }
            }
        }
        return refs;
    }

    static TextMeshProUGUI CreateTmp(string name, Transform parent, string text, int size, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = Mathf.Clamp(Mathf.RoundToInt(size * TmpScale), TmpMin, TmpMax);
        tmp.fontStyle = style;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, 48f);

        var le = go.GetComponent<LayoutElement>();
        le.flexibleWidth = 1f;
        le.minWidth = 120f;
        le.minHeight = 44f;
        return tmp;
    }

    static RectTransform EnsurePilesGrid(Transform parent, string name)
    {
        var existing = parent.Find(name) as RectTransform;
        if (existing != null) return existing;

        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.GridLayoutGroup), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        // 8개를 2행(가로 2줄)로 배치하려면 4열 폭이 필요
        rt.sizeDelta = new Vector2(124f, 44f);

        var grid = go.GetComponent<UnityEngine.UI.GridLayoutGroup>();
        grid.cellSize = new Vector2(18f, 18f);
        grid.spacing = new Vector2(6f, 6f);
        grid.startCorner = UnityEngine.UI.GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = UnityEngine.UI.GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.MiddleCenter;
        // “2열”이 아니라 “2행”으로 보이게 (Row 2개 고정)
        grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedRowCount;
        grid.constraintCount = 2;

        var le = go.GetComponent<LayoutElement>();
        le.minWidth = 124f;
        le.minHeight = 44f;
        le.preferredWidth = 124f;
        le.preferredHeight = 44f;
        le.flexibleWidth = 0f;
        le.flexibleHeight = 0f;
        return rt;
    }

    static RectTransform EnsureFlyIconsRoot(Transform homeRoot)
    {
        Canvas canvas = homeRoot.GetComponentInParent<Canvas>();
        if (canvas == null) return null;
        Transform t = canvas.transform.Find("FlyIconsRoot");
        if (t != null) return t as RectTransform;
        var go = new GameObject("FlyIconsRoot", typeof(RectTransform), typeof(CanvasRenderer));
        Undo.RegisterCreatedObjectUndo(go, "FlyRoot");
        go.transform.SetParent(canvas.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        go.transform.SetAsLastSibling();
        return rt;
    }

    static void EnsureFlyIconTemplates(CollectionManager cm, RectTransform flyRoot)
    {
        if (cm == null || flyRoot == null) return;
        Transform goldTr = flyRoot.Find("FlyGoldTemplate");
        if (goldTr == null)
        {
            var go = new GameObject("FlyGoldTemplate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "FlyGoldTemplate");
            go.transform.SetParent(flyRoot, false);
            ConfigureFlyTemplate(go, new Color(1f, 0.85f, 0.2f, 1f));
            goldTr = go.transform;
        }
        if (cm.flyingGoldPrefab == null) cm.flyingGoldPrefab = goldTr.gameObject;
    }

    static void ConfigureFlyTemplate(GameObject go, Color color)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        // 창고 더미 아이콘(18x18)과 동일 크기로 통일
        rt.sizeDelta = new Vector2(18f, 18f);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        go.SetActive(false);
    }

    static void AssignPilesNearWarehouseLabels(CollectionManager cm, Transform homeRoot)
    {
        if (cm == null || homeRoot == null) return;

        var dockGrid = homeRoot.Find("GoldPileDock/PilesGrid") as RectTransform;
        if (dockGrid != null)
        {
            cm.goldPiles = EnsurePileIcons(dockGrid, "GoldPile", new Color(1f, 0.85f, 0.15f, 1f));
            cm.pileArea = dockGrid;
        }

        var marketGrid = homeRoot.Find("WarehouseRow/WarehousePanelsRow/MarketWarehouse/HeaderRow/PilesGrid") as RectTransform;
        if (marketGrid != null && (cm.goldPiles == null || cm.goldPiles.Length == 0))
        {
            cm.goldPiles = EnsurePileIcons(marketGrid, "GoldPile", new Color(1f, 0.85f, 0.15f, 1f));
            cm.pileArea = marketGrid;
        }

        if (cm.goldPiles == null || cm.goldPiles.Length == 0)
        {
            var legacyDock = homeRoot.Find("PileDock") as RectTransform;
            var goldRow = legacyDock != null ? legacyDock.Find("GoldPilesRow") : null;
            if (goldRow != null)
                cm.goldPiles = EnsurePileIcons(goldRow, "GoldPile", new Color(1f, 0.85f, 0.15f, 1f));
            if (cm.pileArea == null && legacyDock != null)
                cm.pileArea = legacyDock;
        }
    }

    static GameObject[] EnsurePileIcons(Transform row, string baseName, Color c)
    {
        var list = new List<GameObject>();
        for (int i = 0; i < 8; i++)
        {
            string nm = $"{baseName}_{i}";
            Transform ch = row.Find(nm);
            GameObject go;
            if (ch == null)
            {
                go = new GameObject(nm, typeof(RectTransform), typeof(Image));
                Undo.RegisterCreatedObjectUndo(go, "Pile");
                go.transform.SetParent(row, false);
                go.GetComponent<Image>().color = c;
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(18, 18);
            }
            else go = ch.gameObject;
            go.SetActive(false);
            list.Add(go);
        }
        return list.ToArray();
    }

    static RectTransform FindTmpRect(Transform root, string endsWith)
    {
        var tmps = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var t in tmps)
        {
            if (t.gameObject.name.IndexOf(endsWith, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return t.rectTransform;
        }
        return null;
    }
}
#endif

