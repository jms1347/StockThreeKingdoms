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

        // 좌측: 프로필(절반)
        var profileBox = new GameObject("ProfileBox", typeof(RectTransform));
        profileBox.transform.SetParent(topBar.transform, false);
        var profileLe = profileBox.AddComponent<LayoutElement>();
        profileLe.flexibleWidth = 1f;
        profileLe.minWidth = 320f;

        var profileLayout = profileBox.AddComponent<HorizontalLayoutGroup>();
        profileLayout.spacing = 12f;
        profileLayout.childAlignment = TextAnchor.MiddleLeft;
        profileLayout.childControlWidth = false;
        profileLayout.childControlHeight = true;
        profileLayout.childForceExpandWidth = false;

        var avatar = new GameObject("AvatarIcon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        avatar.transform.SetParent(profileBox.transform, false);
        var avatarImg = avatar.GetComponent<Image>();
        avatarImg.sprite = uiSprite;
        avatarImg.type = Image.Type.Sliced;
        avatarImg.color = new Color(0.20f, 0.24f, 0.32f, 0.95f);
        var avatarLe = avatar.GetComponent<LayoutElement>();
        avatarLe.preferredWidth = 84f;
        avatarLe.preferredHeight = 84f;
        avatar.AddComponent<RectMask2D>();

        var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image), typeof(Outline));
        portraitGo.transform.SetParent(avatar.transform, false);
        portraitGo.transform.SetAsFirstSibling();
        var portraitImg = portraitGo.GetComponent<Image>();
        portraitImg.sprite = uiSprite;
        portraitImg.type = Image.Type.Sliced;
        portraitImg.preserveAspect = true;
        portraitImg.raycastTarget = false;
        portraitImg.color = new Color(0.25f, 0.28f, 0.32f, 1f);
        var portraitRt = portraitGo.GetComponent<RectTransform>();
        portraitRt.anchorMin = Vector2.zero;
        portraitRt.anchorMax = Vector2.one;
        portraitRt.offsetMin = new Vector2(4f, 4f);
        portraitRt.offsetMax = new Vector2(-4f, -4f);
        var portraitOutline = portraitGo.GetComponent<Outline>();
        portraitOutline.effectColor = new Color(0.4f, 0.45f, 0.5f, 0.75f);
        portraitOutline.effectDistance = new Vector2(0.9f, -0.9f);

        var profileColumn = new GameObject("ProfileTextColumn", typeof(RectTransform));
        profileColumn.transform.SetParent(profileBox.transform, false);
        var colV = profileColumn.AddComponent<VerticalLayoutGroup>();
        colV.spacing = 6f;
        colV.padding = new RectOffset(0, 0, 2, 0);
        colV.childAlignment = TextAnchor.UpperLeft;
        colV.childControlWidth = true;
        colV.childControlHeight = true;
        colV.childForceExpandWidth = true;
        colV.childForceExpandHeight = false;
        var colLe = profileColumn.AddComponent<LayoutElement>();
        colLe.flexibleWidth = 1f;
        colLe.minWidth = 160f;

        var titleBadge = new GameObject("TitleBadge", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
        titleBadge.transform.SetParent(profileColumn.transform, false);
        var titleBgImg = titleBadge.GetComponent<Image>();
        titleBgImg.sprite = uiSprite;
        titleBgImg.type = Image.Type.Sliced;
        titleBgImg.color = new Color(0.22f, 0.24f, 0.28f, 0.88f);
        var titleBadgeLe = titleBadge.GetComponent<LayoutElement>();
        titleBadgeLe.minHeight = 28f;
        titleBadgeLe.preferredHeight = 30f;
        titleBadgeLe.flexibleWidth = 1f;
        var titleHg = titleBadge.GetComponent<HorizontalLayoutGroup>();
        titleHg.padding = new RectOffset(10, 10, 4, 6);
        titleHg.childAlignment = TextAnchor.MiddleLeft;
        titleHg.childControlWidth = true;
        titleHg.childControlHeight = true;
        titleHg.childForceExpandWidth = true;
        titleHg.childForceExpandHeight = true;

        var titleTmpGo = new GameObject("TitleBadgeText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleTmpGo.transform.SetParent(titleBadge.transform, false);
        var titleTmp = titleTmpGo.GetComponent<TextMeshProUGUI>();
        titleTmp.font = font;
        titleTmp.fontSize = 18f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Left;
        titleTmp.color = new Color(0.92f, 0.93f, 0.95f);
        titleTmp.text = "평민";
        titleTmp.enableAutoSizing = true;
        titleTmp.fontSizeMin = 14;
        titleTmp.fontSizeMax = 18;
        titleTmp.overflowMode = TextOverflowModes.Ellipsis;
        titleTmp.raycastTarget = false;
        var titleTmpRt = titleTmpGo.GetComponent<RectTransform>();
        titleTmpRt.anchorMin = Vector2.zero;
        titleTmpRt.anchorMax = Vector2.one;
        titleTmpRt.offsetMin = Vector2.zero;
        titleTmpRt.offsetMax = Vector2.zero;

        var titleBadgeOutline = titleBadge.AddComponent<Outline>();
        titleBadgeOutline.effectColor = new Color(0.45f, 0.48f, 0.52f, 0.75f);
        titleBadgeOutline.effectDistance = new Vector2(0.65f, -0.65f);

        var userTmp = CreateTMP(profileColumn.transform, "UserNameText", font, 30, FontStyles.Bold, TextAlignmentOptions.Left, Color.white);
        userTmp.text = "ZhugeMaster01";
        userTmp.enableAutoSizing = true;
        userTmp.fontSizeMin = 18;
        userTmp.fontSizeMax = 30;
        userTmp.overflowMode = TextOverflowModes.Ellipsis;
        var userLe = userTmp.gameObject.AddComponent<LayoutElement>();
        userLe.flexibleWidth = 1f;
        userLe.minWidth = 140f;

        // 중앙: 위치 + 금화 + 병력 + 유지비 미리보기
        var resourceBox = new GameObject("CenterResourceBox", typeof(RectTransform));
        resourceBox.transform.SetParent(topBar.transform, false);
        var resLe = resourceBox.AddComponent<LayoutElement>();
        resLe.flexibleWidth = 1f;
        resLe.minWidth = 280f;

        var resV = resourceBox.AddComponent<VerticalLayoutGroup>();
        resV.spacing = 6f;
        resV.childAlignment = TextAnchor.MiddleRight;
        resV.childControlWidth = true;
        resV.childControlHeight = true;
        resV.childForceExpandWidth = true;
        resV.childForceExpandHeight = true;

        var locationTmp = CreateTMP(resourceBox.transform, "LocationText", font, 22, FontStyles.Bold, TextAlignmentOptions.Right, new Color(0.9f, 0.92f, 0.95f));
        locationTmp.text = "낙양";
        locationTmp.enableAutoSizing = true;
        locationTmp.fontSizeMin = 16;
        locationTmp.fontSizeMax = 24;

        var assetsRow = CreateIconValueRow(resourceBox.transform, "AssetsRow", uiSprite, font, new Color(0.96f, 0.88f, 0.35f, 1f), out var assetsTmp);
        assetsTmp.text = "1.5M";
        var soldiersRow = CreateIconValueRow(resourceBox.transform, "SoldiersRow", uiSprite, font, new Color(0.70f, 1f, 0.75f, 1f), out var soldiersTmp);
        soldiersTmp.text = "0명";
        var maintAmtRow =
            CreateIconValueRow(resourceBox.transform, "MaintenancePreviewRow", uiSprite, font,
                new Color(0.85f, 0.92f, 1f, 1f), out var maintAmtTmp);
        maintAmtTmp.text = "다음 정산 예정: —";
        var maintCdRow =
            CreateIconValueRow(resourceBox.transform, "MaintenanceCountdownRow", uiSprite, font,
                new Color(0.78f, 0.84f, 0.92f, 1f), out var maintCdTmp);
        maintCdTmp.text = "정산까지: —";

        // 우측: MP 단독
        var rightMp = new GameObject("RightMarchColumn", typeof(RectTransform));
        rightMp.transform.SetParent(topBar.transform, false);
        var rLe = rightMp.AddComponent<LayoutElement>();
        rLe.minWidth = 120f;
        rLe.preferredWidth = 140f;
        var rV = rightMp.AddComponent<VerticalLayoutGroup>();
        rV.childAlignment = TextAnchor.MiddleRight;
        rV.childControlWidth = true;
        rV.childControlHeight = true;
        var mpTmp = CreateTMP(rightMp.transform, "MarchPointsText", font, 28, FontStyles.Bold, TextAlignmentOptions.Right, Color.white);
        mpTmp.text = "0 MP";
        mpTmp.enableAutoSizing = true;
        mpTmp.fontSizeMin = 18;
        mpTmp.fontSizeMax = 32;

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
        so.FindProperty("locationText").objectReferenceValue = locationTmp;
        so.FindProperty("totalAssetsText").objectReferenceValue = assetsTmp;
        so.FindProperty("soldiersText").objectReferenceValue = soldiersTmp;
        so.FindProperty("maintenancePreviewText").objectReferenceValue = maintAmtTmp;
        so.FindProperty("maintenanceCountdownText").objectReferenceValue = maintCdTmp;
        so.FindProperty("userPortraitImage").objectReferenceValue = portraitImg;
        so.FindProperty("titleBadgeBackground").objectReferenceValue = titleBgImg;
        so.FindProperty("titleBadgeText").objectReferenceValue = titleTmp;
        so.FindProperty("titleBadgeOutline").objectReferenceValue = titleBadgeOutline;
        so.FindProperty("avatarPortraitOutline").objectReferenceValue = portraitOutline;
        so.FindProperty("marchPointsText").objectReferenceValue = mpTmp;
        so.FindProperty("bottomTabRoot").objectReferenceValue = bRt;
        so.FindProperty("homeButton").objectReferenceValue = homeBtn;
        so.FindProperty("marketButton").objectReferenceValue = marketBtn;
        so.FindProperty("portfolioButton").objectReferenceValue = portBtn;
        so.FindProperty("newsButton").objectReferenceValue = newsBtn;
        so.FindProperty("ordersButton").objectReferenceValue = ordersBtn;
        so.ApplyModifiedPropertiesWithoutUndo();

        return go;
    }

    static GameObject CreateIconValueRow(Transform parent, string name, Sprite uiSprite, TMP_FontAsset font, Color valueColor, out TextMeshProUGUI valueTmp)
    {
        var row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 10f;
        h.padding = new RectOffset(0, 0, 0, 0);
        h.childAlignment = TextAnchor.MiddleRight;
        h.childControlWidth = false;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;

        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        icon.transform.SetParent(row.transform, false);
        var img = icon.GetComponent<Image>();
        img.sprite = uiSprite;
        img.type = Image.Type.Sliced;
        img.color = new Color(0.18f, 0.22f, 0.30f, 0.95f);
        var le = icon.GetComponent<LayoutElement>();
        le.preferredWidth = 34f;
        le.preferredHeight = 34f;

        valueTmp = CreateTMP(row.transform, "ValueText", font, 28, FontStyles.Bold, TextAlignmentOptions.Right, valueColor);
        valueTmp.text = "0";
        valueTmp.enableAutoSizing = true;
        valueTmp.fontSizeMin = 16;
        valueTmp.fontSizeMax = 28;
        valueTmp.overflowMode = TextOverflowModes.Ellipsis;
        valueTmp.raycastTarget = false;

        var vLe = valueTmp.gameObject.AddComponent<LayoutElement>();
        vLe.minWidth = 140f;
        vLe.flexibleWidth = 1f;

        return row;
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

