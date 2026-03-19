using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using UnityEditor;

/// <summary>
/// 본영 탭용 LaborPanel, MarketPanel, FarmPanel, SupplyPanel 자동 생성.
/// 메뉴: Tools > 주식삼국지 > 본영 패널 생성
/// </summary>
public static class HomePanelCreator
{
    [MenuItem("Tools/주식삼국지/본영 패널 생성")]
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

        RectTransform parent = canvas.transform as RectTransform;
        GameObject root = new GameObject("HomePanels");
        root.transform.SetParent(parent, false);

        RectTransform rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0, 0);
        rootRect.anchorMax = new Vector2(1, 1);
        rootRect.offsetMin = new Vector2(20, 20);
        rootRect.offsetMax = new Vector2(-20, -20);

        VerticalLayoutGroup rootLayout = root.AddComponent<VerticalLayoutGroup>();
        rootLayout.spacing = 16;
        rootLayout.padding = new RectOffset(0, 0, 0, 0);
        rootLayout.childControlHeight = true;
        rootLayout.childControlWidth = true;
        rootLayout.childForceExpandHeight = false;
        rootLayout.childForceExpandWidth = true;

        CreateResourceBar(root.transform);
        CreateLaborPanel(root.transform);
        CreateMarketPanel(root.transform);
        CreateFarmPanel(root.transform);
        CreateSupplyPanel(root.transform);

        Selection.activeGameObject = root;
        Undo.RegisterCreatedObjectUndo(root, "Create Home Panels");
        Debug.Log("[HomePanelCreator] LaborPanel, MarketPanel, FarmPanel, SupplyPanel 생성 완료. HomeUIController에 연결해주세요.");
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

        RectTransform objRect = obj.AddComponent<RectTransform>();

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

    static void CreateResourceBar(Transform parent)
    {
        GameObject bar = new GameObject("ResourceBar");
        bar.transform.SetParent(parent, false);

        RectTransform rect = bar.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, 60);

        HorizontalLayoutGroup hlg = bar.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 16;
        hlg.padding = new RectOffset(0, 0, 0, 0);
        hlg.childForceExpandWidth = true;

        CreateText(bar.transform, "GoldText", "금화: 0", 16);
        CreateText(bar.transform, "GrainText", "식량: 0", 16);
        CreateText(bar.transform, "FarmWorkersText", "농장인력: 0", 16);

        LayoutElement le = bar.AddComponent<LayoutElement>();
        le.preferredHeight = 60;

        CreateButton(parent, "GateButton", "대문 터치 (금화 획득)");
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
