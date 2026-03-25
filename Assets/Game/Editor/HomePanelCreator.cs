using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using UnityEditor;

/// <summary>
/// 본영 탭용 LaborPanel, MarketPanel, FarmPanel, SupplyPanel 자동 생성.
/// 메뉴: StockThreeKingdoms/Home/홈패널 만들기
/// </summary>
public static class HomePanelCreator
{
    const float ContentTopInset = 160f;    // GlobalUI TopBar(140) + 여유
    const float ContentBottomInset = 180f; // GlobalUI BottomTabBar(160) + 여유

    [MenuItem("StockThreeKingdoms/Home/홈패널 만들기", false, 0)]
    public static void CreateHomePanels()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
        }

        RectTransform parent = EnsureContentRoot(canvas);
        GameObject root = new GameObject("HomePanels");
        root.transform.SetParent(parent, false);

        Image rootBg = root.AddComponent<Image>();
        rootBg.color = new Color(0, 0, 0, 0);
        RectTransform rootRect = root.transform as RectTransform;
        if (rootRect != null)
        {
            rootRect.anchorMin = new Vector2(0, 0);
            rootRect.anchorMax = new Vector2(1, 1);
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
        }

        VerticalLayoutGroup rootLayout = root.AddComponent<VerticalLayoutGroup>();
        rootLayout.spacing = 16;
        rootLayout.padding = new RectOffset(0, 0, 0, 0);
        rootLayout.childControlHeight = true;
        rootLayout.childControlWidth = true;
        rootLayout.childForceExpandHeight = false;
        rootLayout.childForceExpandWidth = true;

        // ResourceBar는 GlobalUIManager 탑바로 대체합니다.
        CreateButton(root.transform, "GateButton", "대문 터치 (금화 획득)");
        CreateLaborPanel(root.transform);
        CreateMarketPanel(root.transform);
        CreateFarmPanel(root.transform);
        CreateSupplyPanel(root.transform);

        AddScriptsAndWireReferences(root);
        EnsureManagersExist();

        Selection.activeGameObject = root;
        Undo.RegisterCreatedObjectUndo(root, "Create Home Panels");
        Debug.Log("[HomePanelCreator] 본영 패널 생성 완료. 씬 저장 후 플레이해보세요.");
    }

    static RectTransform EnsureContentRoot(Canvas canvas)
    {
        var parent = canvas.transform as RectTransform;
        var t = parent.Find("ContentRoot");
        RectTransform rt;
        if (t == null)
        {
            var go = new GameObject("ContentRoot", typeof(RectTransform));
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

    static void AddScriptsAndWireReferences(GameObject root)
    {
        root.AddComponent<HomeController>();
        HomeUIController ui = root.AddComponent<HomeUIController>();

        Transform t = root.transform;
        ui.goldText = null;
        ui.grainText = null;
        ui.farmWorkersText = null;
        ui.gateButton = t.Find("GateButton")?.GetComponent<Button>();

        ui.laborLabelText = t.Find("LaborPanel/LaborLabelText")?.GetComponent<TextMeshProUGUI>();
        ui.laborUpgradeButton = t.Find("LaborPanel/LaborUpgradeButton")?.GetComponent<Button>();

        ui.marketLabelText = t.Find("MarketPanel/MarketLabelText")?.GetComponent<TextMeshProUGUI>();
        ui.marketAccumulateText = t.Find("MarketPanel/MarketAccumulateText")?.GetComponent<TextMeshProUGUI>();
        ui.marketAccumulateSlider = t.Find("MarketPanel/MarketAccumulateSlider")?.GetComponent<Slider>();
        ui.marketUpgradeButton = t.Find("MarketPanel/MarketButtons/MarketUpgradeButton")?.GetComponent<Button>();
        ui.collectMarketButton = t.Find("MarketPanel/MarketButtons/CollectMarketButton")?.GetComponent<Button>();

        ui.farmLabelText = t.Find("FarmPanel/FarmLabelText")?.GetComponent<TextMeshProUGUI>();
        ui.farmAccumulateText = t.Find("FarmPanel/FarmAccumulateText")?.GetComponent<TextMeshProUGUI>();
        ui.farmAccumulateSlider = t.Find("FarmPanel/FarmAccumulateSlider")?.GetComponent<Slider>();
        ui.farmUpgradeButton = t.Find("FarmPanel/FarmButtons/FarmUpgradeButton")?.GetComponent<Button>();
        ui.collectFarmButton = t.Find("FarmPanel/FarmButtons/CollectFarmButton")?.GetComponent<Button>();

        ui.supplyLabelText = t.Find("SupplyPanel/SupplyLabelText")?.GetComponent<TextMeshProUGUI>();
        ui.hireFarmWorkerButton = t.Find("SupplyPanel/SupplyButtons/HireFarmWorkerButton")?.GetComponent<Button>();
        ui.buyGrainButton = t.Find("SupplyPanel/SupplyButtons/BuyGrainButton")?.GetComponent<Button>();

        EditorUtility.SetDirty(ui);
    }

    static void EnsureManagersExist()
    {
        if (Object.FindObjectOfType<GameManager>() == null)
        {
            GameObject gm = new GameObject("GameManager");
            gm.AddComponent<GameManager>();
            Undo.RegisterCreatedObjectUndo(gm, "Create GameManager");
        }
        if (Object.FindObjectOfType<DataManager>() == null)
        {
            GameObject dm = new GameObject("DataManager");
            dm.AddComponent<DataManager>();
            Undo.RegisterCreatedObjectUndo(dm, "Create DataManager");
        }
        if (Object.FindObjectOfType<GoogleSheetManager>() == null)
        {
            GameObject gsm = new GameObject("GoogleSheetManager");
            gsm.AddComponent<GoogleSheetManager>();
            Undo.RegisterCreatedObjectUndo(gsm, "Create GoogleSheetManager");
        }
    }

    static GameObject CreatePanel(Transform parent, string name, string title)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);

        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        RectTransform rect = panel.transform as RectTransform;
        if (rect != null) rect.sizeDelta = new Vector2(0, 140);

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8;
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        GameObject titleObj = CreateText(panel.transform, "Title", title, 18, FontStyles.Bold);
        return panel;
    }

    static GameObject CreateText(Transform parent, string name, string content, int fontSize, FontStyles style = FontStyles.Normal)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = Color.white;

        LayoutElement le = obj.AddComponent<LayoutElement>();
        le.preferredHeight = fontSize + 8;
        return obj;
    }

    static Button CreateButton(Transform parent, string name, string label)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        Image img = obj.AddComponent<Image>();
        img.color = new Color(0.3f, 0.5f, 0.8f);

        Button btn = obj.AddComponent<Button>();

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(obj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 14;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        LayoutElement le = obj.AddComponent<LayoutElement>();
        le.preferredHeight = 36;
        return btn;
    }

    static Slider CreateSlider(Transform parent, string name)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        Image trackImg = obj.AddComponent<Image>();
        trackImg.color = new Color(0.2f, 0.2f, 0.2f);

        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(obj.transform, false);
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1, 0.75f);
        fillAreaRect.offsetMin = new Vector2(5, 0);
        fillAreaRect.offsetMax = new Vector2(-5, 0);

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.2f, 0.8f, 0.2f);

        Slider slider = obj.AddComponent<Slider>();
        slider.fillRect = fillRect;
        slider.minValue = 0;
        slider.maxValue = 1;
        slider.value = 0;

        LayoutElement le = obj.AddComponent<LayoutElement>();
        le.preferredHeight = 24;
        return slider;
    }

    // ResourceBar는 GlobalUIManager 탑바로 대체합니다.

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
        CreateText(panel.transform, "MarketAccumulateText", "0 / 0", 12);
        CreateSlider(panel.transform, "MarketAccumulateSlider");
        GameObject btnRow = new GameObject("MarketButtons");
        btnRow.transform.SetParent(panel.transform, false);
        HorizontalLayoutGroup hlg = btnRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childForceExpandWidth = true;
        CreateButton(btnRow.transform, "CollectMarketButton", "수거");
        CreateButton(btnRow.transform, "MarketUpgradeButton", "업그레이드");
    }

    static void CreateFarmPanel(Transform parent)
    {
        GameObject panel = CreatePanel(parent, "FarmPanel", "농장 (초당 식량)");
        CreateText(panel.transform, "FarmLabelText", "초당 식량 자동 생산\n(Level 0)\n비용: 80 Gold", 14);
        CreateText(panel.transform, "FarmAccumulateText", "0 / 0", 12);
        CreateSlider(panel.transform, "FarmAccumulateSlider");
        GameObject btnRow = new GameObject("FarmButtons");
        btnRow.transform.SetParent(panel.transform, false);
        HorizontalLayoutGroup hlg = btnRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childForceExpandWidth = true;
        CreateButton(btnRow.transform, "CollectFarmButton", "수거");
        CreateButton(btnRow.transform, "FarmUpgradeButton", "업그레이드");
    }

    static void CreateSupplyPanel(Transform parent)
    {
        GameObject panel = CreatePanel(parent, "SupplyPanel", "보급");
        CreateText(panel.transform, "SupplyLabelText", "(농장 인력: 최대 0명 고용 가능)\n(식량: 최대 0 구매 가능)", 12);
        GameObject btnRow = new GameObject("SupplyButtons");
        btnRow.transform.SetParent(panel.transform, false);
        HorizontalLayoutGroup hlg = btnRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childForceExpandWidth = true;
        CreateButton(btnRow.transform, "HireFarmWorkerButton", "농장 인력 고용 (100G)");
        CreateButton(btnRow.transform, "BuyGrainButton", "식량 구매 (2G)");
    }
}
