#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 모든 씬 공통 상단바/하단탭(5버튼) 프리팹을 자동 생성합니다.
/// 메뉴 실행 후 SingletonLoader 프리팹의 globalUiManagerPrefab에 연결해서 사용하세요.
/// </summary>
public static class GlobalUIFramePrefabCreator
{
    const string MenuPath = "StockThreeKingdoms/UI/공통 상하단 탭 프레임 프리팹 생성";
    const string PrefabDir = "Assets/Game/CommonUI/Prefabs";
    const string PrefabPath = "Assets/Game/CommonUI/Prefabs/GlobalUIManager.prefab";

    [MenuItem(MenuPath, false, 0)]
    public static void CreatePrefab()
    {
        var font = TMP_Settings.defaultFontAsset;
        if (font == null)
        {
            EditorUtility.DisplayDialog("TMP", "TMP_Settings에 기본 폰트가 없습니다. TextMesh Pro를 임포트하고 TMP Settings를 확인하세요.", "OK");
            return;
        }

        Directory.CreateDirectory(PrefabDir);

        var uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        var root = BuildGlobalUiRoot(font, uiSprite);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Global UI", $"프리팹 생성 완료:\n{PrefabPath}\n\n이 프리팹을 SingletonLoader의 globalUiManagerPrefab에 연결하세요.", "OK");
    }

    static GameObject BuildGlobalUiRoot(TMP_FontAsset font, Sprite uiSprite)
    {
        var go = new GameObject("GlobalUIManager", typeof(RectTransform));
        go.AddComponent<GlobalUIManager>();

        // Canvas
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        go.AddComponent<GraphicRaycaster>();

        // Ensure EventSystem is not created here (scene responsibility)

        var rootRt = go.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        // TopBar
        var topBar = new GameObject("TopBar", typeof(RectTransform), typeof(Image));
        topBar.transform.SetParent(go.transform, false);
        var topRt = topBar.GetComponent<RectTransform>();
        topRt.anchorMin = new Vector2(0, 1);
        topRt.anchorMax = new Vector2(1, 1);
        topRt.pivot = new Vector2(0.5f, 1);
        topRt.sizeDelta = new Vector2(0, 140f);
        topRt.anchoredPosition = Vector2.zero;
        var topImg = topBar.GetComponent<Image>();
        topImg.sprite = uiSprite;
        topImg.type = Image.Type.Sliced;
        topImg.color = new Color(0.10f, 0.12f, 0.16f, 0.94f);

        var topLayout = topBar.AddComponent<HorizontalLayoutGroup>();
        topLayout.padding = new RectOffset(18, 18, 14, 14);
        topLayout.spacing = 14f;
        topLayout.childAlignment = TextAnchor.MiddleLeft;
        topLayout.childControlWidth = true;
        topLayout.childControlHeight = true;
        topLayout.childForceExpandWidth = true;

        var userBox = new GameObject("UserBox", typeof(RectTransform));
        userBox.transform.SetParent(topBar.transform, false);
        userBox.AddComponent<LayoutElement>().preferredWidth = 380f;
        var userTmp = CreateTMP(userBox.transform, "UserNameText", font, 30, FontStyles.Bold, TextAlignmentOptions.Left, Color.white);
        userTmp.text = "User: ZhugeMaster01";

        var statBox = new GameObject("StatBox", typeof(RectTransform));
        statBox.transform.SetParent(topBar.transform, false);
        statBox.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var statLayout = statBox.AddComponent<HorizontalLayoutGroup>();
        statLayout.spacing = 24f;
        statLayout.childAlignment = TextAnchor.MiddleCenter;
        statLayout.childControlWidth = false;
        statLayout.childControlHeight = true;
        statLayout.childForceExpandWidth = false;

        var assetsTmp = CreateTMP(statBox.transform, "TotalAssetsText", font, 28, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.96f, 0.88f, 0.35f, 1f));
        assetsTmp.text = "Total Assets: 1,500,000 Gold";
        var foodTmp = CreateTMP(statBox.transform, "FoodText", font, 28, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.85f, 0.90f, 1f, 1f));
        foodTmp.text = "Food: 80,000";

        // BottomTabBar
        var bottom = new GameObject("BottomTabBar", typeof(RectTransform), typeof(Image));
        bottom.transform.SetParent(go.transform, false);
        var bRt = bottom.GetComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0, 0);
        bRt.anchorMax = new Vector2(1, 0);
        bRt.pivot = new Vector2(0.5f, 0);
        bRt.sizeDelta = new Vector2(0, 160f);
        bRt.anchoredPosition = Vector2.zero;
        var bImg = bottom.GetComponent<Image>();
        bImg.sprite = uiSprite;
        bImg.type = Image.Type.Sliced;
        bImg.color = new Color(0.08f, 0.10f, 0.14f, 0.96f);

        var bl = bottom.AddComponent<HorizontalLayoutGroup>();
        bl.padding = new RectOffset(14, 14, 12, 12);
        bl.spacing = 12f;
        bl.childAlignment = TextAnchor.MiddleCenter;
        bl.childControlWidth = true;
        bl.childControlHeight = true;
        bl.childForceExpandWidth = true;
        bl.childForceExpandHeight = true;

        var homeBtn = CreateTabButton(bottom.transform, "HomeTabButton", "Home", font, uiSprite);
        var marketBtn = CreateTabButton(bottom.transform, "MarketTabButton", "Market", font, uiSprite);
        var portBtn = CreateTabButton(bottom.transform, "PortfolioTabButton", "Portfolio", font, uiSprite);
        var newsBtn = CreateTabButton(bottom.transform, "NewsTabButton", "News", font, uiSprite);
        var ordersBtn = CreateTabButton(bottom.transform, "OrdersTabButton", "Orders", font, uiSprite);

        // Wire serialized references on GlobalUIManager
        var mgr = go.GetComponent<GlobalUIManager>();
        var so = new SerializedObject(mgr);
        so.FindProperty("topBarRoot").objectReferenceValue = topRt;
        so.FindProperty("userNameText").objectReferenceValue = userTmp;
        so.FindProperty("totalAssetsText").objectReferenceValue = assetsTmp;
        so.FindProperty("foodText").objectReferenceValue = foodTmp;
        so.FindProperty("bottomTabRoot").objectReferenceValue = bRt;
        so.FindProperty("homeButton").objectReferenceValue = homeBtn;
        so.FindProperty("marketButton").objectReferenceValue = marketBtn;
        so.FindProperty("portfolioButton").objectReferenceValue = portBtn;
        so.FindProperty("newsButton").objectReferenceValue = newsBtn;
        so.FindProperty("ordersButton").objectReferenceValue = ordersBtn;
        so.ApplyModifiedPropertiesWithoutUndo();

        return go;
    }

    static Button CreateTabButton(Transform parent, string name, string label, TMP_FontAsset font, Sprite uiSprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().sprite = uiSprite;
        go.GetComponent<Image>().type = Image.Type.Sliced;
        go.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.24f, 0.95f);

        var le = go.GetComponent<LayoutElement>();
        le.minHeight = 120f;
        le.flexibleWidth = 1f;

        var tmp = CreateTMP(go.transform, "Label", font, 28, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        tmp.text = label;
        var rt = tmp.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return go.GetComponent<Button>();
    }

    static TextMeshProUGUI CreateTMP(Transform parent, string name, TMP_FontAsset font, float size, FontStyles style, TextAlignmentOptions align, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }
}
#endif

