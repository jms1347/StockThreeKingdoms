#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using UnityEditor.SceneManagement;

/// <summary>
/// HomeScene(구 TestScene) 레이아웃을 "한 번에" 자동 생성/수정합니다.
/// - GlobalUI(탑바/탭바) 영역을 제외하기 위해 Canvas 아래 ContentRoot를 사용합니다.
/// - HomePanels 생성 + 기존 TestScene 기능(만보기 패널, 더미 더미들, 비행 루트/템플릿, CollectionManager 바인딩 등)을 유지합니다.
/// </summary>
public static class HomeSceneLayoutWizard
{
    const string MenuPath = "StockThreeKingdoms/Home/HomeScene 레이아웃 자동 생성(전체)";

    const float ContentTopInset = 160f;    // GlobalUI TopBar(140) + 여유
    const float ContentBottomInset = 180f; // GlobalUI BottomTabBar(160) + 여유

    const float TmpScale = 1.65f;
    const int TmpMin = 36;
    const int TmpMax = 96;
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
        CreateFarmPanel(root.transform);
        CreateWarehouseRow(root.transform); // 시장/농장 수거(창고) 2열 영역
        CreateSupplyPanel(root.transform);

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
        var pileDock = EnsurePileDock(hp.transform);
        var flyRoot = EnsureFlyIconsRoot(hp.transform);

        var cm = hp.GetComponent<CollectionManager>() ?? hp.gameObject.AddComponent<CollectionManager>();
        cm.homeController = hc;
        cm.flyIconsRoot = flyRoot;
        cm.pileArea = pileDock;
        EnsureFlyIconTemplates(cm, flyRoot);
        if (cm.poolSize < 10) cm.poolSize = 12;

        var gui = GlobalUIManager.InstanceOrNull;
        var goldRt = gui != null ? gui.AssetsTarget : null;
        var grainRt = gui != null ? gui.FoodTarget : null;
        cm.goldFlyTarget = goldRt;
        cm.grainFlyTarget = grainRt;

        AssignPiles(cm, pileDock);
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
        ui.grainText = t.Find("ResourceBar/GrainText")?.GetComponent<TextMeshProUGUI>();
        ui.farmWorkersText = t.Find("ResourceBar/FarmWorkersText")?.GetComponent<TextMeshProUGUI>();
        ui.gateButton = t.Find("GateButton")?.GetComponent<Button>();

        ui.laborLabelText = t.Find("LaborPanel/LaborLabelText")?.GetComponent<TextMeshProUGUI>();
        ui.laborUpgradeButton = t.Find("LaborPanel/LaborUpgradeButton")?.GetComponent<Button>();

        ui.marketLabelText = t.Find("MarketPanel/MarketLabelText")?.GetComponent<TextMeshProUGUI>();
        ui.marketAccumulateText = t.Find("WarehouseRow/MarketWarehouse/MarketAccumulateText")?.GetComponent<TextMeshProUGUI>();
        ui.marketAccumulateSlider = t.Find("WarehouseRow/MarketWarehouse/MarketAccumulateSlider")?.GetComponent<Slider>();
        ui.marketUpgradeButton = t.Find("MarketPanel/MarketButtons/MarketUpgradeButton")?.GetComponent<Button>();
        ui.collectMarketButton = t.Find("WarehouseRow/MarketWarehouse/CollectMarketButton")?.GetComponent<Button>();

        ui.farmLabelText = t.Find("FarmPanel/FarmLabelText")?.GetComponent<TextMeshProUGUI>();
        ui.farmAccumulateText = t.Find("WarehouseRow/FarmWarehouse/FarmAccumulateText")?.GetComponent<TextMeshProUGUI>();
        ui.farmAccumulateSlider = t.Find("WarehouseRow/FarmWarehouse/FarmAccumulateSlider")?.GetComponent<Slider>();
        ui.farmUpgradeButton = t.Find("FarmPanel/FarmButtons/FarmUpgradeButton")?.GetComponent<Button>();
        ui.collectFarmButton = t.Find("WarehouseRow/FarmWarehouse/CollectFarmButton")?.GetComponent<Button>();

        ui.supplyLabelText = t.Find("SupplyPanel/SupplyLabelText")?.GetComponent<TextMeshProUGUI>();
        ui.hireFarmWorkerButton = t.Find("SupplyPanel/SupplyButtons/HireFarmWorkerButton")?.GetComponent<Button>();
        ui.buyGrainButton = t.Find("SupplyPanel/SupplyButtons/BuyGrainButton")?.GetComponent<Button>();

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
        btnRow.GetComponent<HorizontalLayoutGroup>().spacing = 8;
        CreateButton(btnRow.transform, "MarketUpgradeButton", "업그레이드");
    }

    static void CreateFarmPanel(Transform parent)
    {
        GameObject panel = CreatePanel(parent, "FarmPanel", "농장 (초당 식량)");
        CreateText(panel.transform, "FarmLabelText", "초당 식량 자동 생산\n(Level 0)\n비용: 80 Gold", 14);
        GameObject btnRow = new GameObject("FarmButtons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        Undo.RegisterCreatedObjectUndo(btnRow, "FarmButtons");
        btnRow.transform.SetParent(panel.transform, false);
        btnRow.GetComponent<HorizontalLayoutGroup>().spacing = 8;
        CreateButton(btnRow.transform, "FarmUpgradeButton", "업그레이드");
    }

    static void CreateWarehouseRow(Transform parent)
    {
        var row = new GameObject("WarehouseRow", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(row, "WarehouseRow");
        row.transform.SetParent(parent, false);

        var le = row.AddComponent<LayoutElement>();
        le.minHeight = 220f;
        le.flexibleWidth = 1f;

        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 16f;
        h.childAlignment = TextAnchor.UpperCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = false;

        CreateWarehousePanel(row.transform, "MarketWarehouse", "시장 창고", "MarketAccumulateText", "MarketAccumulateSlider", "CollectMarketButton");
        CreateWarehousePanel(row.transform, "FarmWarehouse", "농장 창고", "FarmAccumulateText", "FarmAccumulateSlider", "CollectFarmButton");
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

        CreateText(panel.transform, "Title", title, 16, FontStyles.Bold);
        CreateText(panel.transform, accTextName, "0 / 0", 14);
        CreateSlider(panel.transform, sliderName);

        var btn = CreateButton(panel.transform, collectBtnName, "수거");
        var btnLe = btn.GetComponent<LayoutElement>() ?? btn.gameObject.AddComponent<LayoutElement>();
        btnLe.minHeight = 110f;
        btnLe.preferredHeight = 120f;
    }

    static void CreateSupplyPanel(Transform parent)
    {
        GameObject panel = CreatePanel(parent, "SupplyPanel", "보급");
        CreateText(panel.transform, "SupplyLabelText", "(농장 인력: 최대 0명 고용 가능)\n(식량: 최대 0 구매 가능)", 12);
        GameObject btnRow = new GameObject("SupplyButtons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        Undo.RegisterCreatedObjectUndo(btnRow, "SupplyButtons");
        btnRow.transform.SetParent(panel.transform, false);
        btnRow.GetComponent<HorizontalLayoutGroup>().spacing = 8;
        CreateButton(btnRow.transform, "HireFarmWorkerButton", "농장 인력 고용 (100G)");
        CreateButton(btnRow.transform, "BuyGrainButton", "식량 구매 (2G)");
    }

    static GameObject CreateText(Transform parent, string name, string content, int fontSize, FontStyles style = FontStyles.Normal)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement), typeof(ContentSizeFitter));
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

        var csf = obj.GetComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var le = obj.GetComponent<LayoutElement>();
        le.minHeight = fontSize + 10;
        le.preferredHeight = -1;
        le.flexibleHeight = 0;
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
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Slider), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(obj, name);
        obj.transform.SetParent(parent, false);
        obj.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f);
        obj.GetComponent<LayoutElement>().preferredHeight = 24;
        return obj.GetComponent<Slider>();
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
        Transform farm = FindDeep(homeRoot, "FarmPanel");
        if (labor == null || market == null || farm == null) return;

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

            int[] miles = { 2000, 5000, 7000, 10000 };
            for (int i = 0; i < 4; i++)
            {
                var btnGo = new GameObject($"StepReward_{miles[i]}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                Undo.RegisterCreatedObjectUndo(btnGo, "StepReward");
                btnGo.transform.SetParent(row.transform, false);
                btnGo.GetComponent<Image>().color = new Color(0.25f, 0.35f, 0.5f, 1f);
                btnGo.GetComponent<LayoutElement>().minHeight = 120f;
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
        go.GetComponent<LayoutElement>().minHeight = 44f;
        return tmp;
    }

    static RectTransform EnsurePileDock(Transform homeRoot)
    {
        Transform dock = homeRoot.Find("PileDock");
        if (dock != null) return dock as RectTransform;
        var go = new GameObject("PileDock", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "PileDock");
        go.transform.SetParent(homeRoot, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(520, 140);
        rt.anchoredPosition = new Vector2(0, -40f);
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
        // 기존 CollectionManager가 템플릿을 요구 (골드/그레인)
        Transform goldTr = flyRoot.Find("FlyGoldTemplate");
        if (goldTr == null)
        {
            var go = new GameObject("FlyGoldTemplate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "FlyGoldTemplate");
            go.transform.SetParent(flyRoot, false);
            ConfigureFlyTemplate(go, new Color(1f, 0.85f, 0.2f, 1f));
            goldTr = go.transform;
        }
        Transform grainTr = flyRoot.Find("FlyGrainTemplate");
        if (grainTr == null)
        {
            var go = new GameObject("FlyGrainTemplate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "FlyGrainTemplate");
            go.transform.SetParent(flyRoot, false);
            ConfigureFlyTemplate(go, new Color(0.5f, 0.85f, 0.35f, 1f));
            grainTr = go.transform;
        }
        if (cm.flyingGoldPrefab == null) cm.flyingGoldPrefab = goldTr.gameObject;
        if (cm.flyingGrainPrefab == null) cm.flyingGrainPrefab = grainTr.gameObject;
    }

    static void ConfigureFlyTemplate(GameObject go, Color color)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(48f, 48f);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        go.SetActive(false);
    }

    static void AssignPiles(CollectionManager cm, RectTransform dock)
    {
        if (cm == null || dock == null) return;
        var goldRow = dock.Find("GoldPilesRow");
        if (goldRow == null)
        {
            goldRow = new GameObject("GoldPilesRow", typeof(RectTransform), typeof(HorizontalLayoutGroup)).transform;
            Undo.RegisterCreatedObjectUndo(goldRow.gameObject, "GoldPilesRow");
            goldRow.SetParent(dock, false);
        }
        var grainRow = dock.Find("GrainPilesRow");
        if (grainRow == null)
        {
            grainRow = new GameObject("GrainPilesRow", typeof(RectTransform), typeof(HorizontalLayoutGroup)).transform;
            Undo.RegisterCreatedObjectUndo(grainRow.gameObject, "GrainPilesRow");
            grainRow.SetParent(dock, false);
        }
        cm.goldPiles = EnsurePileIcons(goldRow, "GoldPile", new Color(1f, 0.85f, 0.15f, 1f));
        cm.grainPiles = EnsurePileIcons(grainRow, "GrainPile", new Color(0.45f, 0.75f, 0.25f, 1f));
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
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(52, 52);
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

