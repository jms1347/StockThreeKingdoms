#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 포트폴리오 탭용 Canvas 프리팹(헤더·전쟁 스크롤·일반 스크롤·스트레스 오버레이)을 생성하고
/// <see cref="UserPortfolioManager"/> 레퍼런스를 자동 연결합니다.
/// 메뉴: StockThreeKingdoms/UI/포트폴리오 패널 프리팹 생성
/// </summary>
public static class PortfolioPanelPrefabCreator
{
    const string MenuPath = "StockThreeKingdoms/UI/포트폴리오 패널 프리팹 생성";
    const string PrefabDir = "Assets/Game/0Scene/GameHub";
    const string PrefabPath = PrefabDir + "/GameHub_PortfolioCanvas.prefab";

    static readonly Color BgDark = new Color(0.02f, 0.03f, 0.08f, 1f);
    static readonly Color PanelStrip = new Color(0.06f, 0.08f, 0.14f, 0.98f);
    static readonly Color WarStrip = new Color(0.12f, 0.04f, 0.06f, 0.55f);
    static readonly Color StressTint = new Color(0.85f, 0.05f, 0.05f, 0.22f);

    [MenuItem(MenuPath, false, 40)]
    public static void CreatePrefab()
    {
        var font = TMP_Settings.defaultFontAsset;
        if (font == null)
        {
            EditorUtility.DisplayDialog("TMP", "TMP_Settings에 기본 폰트가 없습니다.", "OK");
            return;
        }

        if (!AssetDatabase.IsValidFolder(PrefabDir))
        {
            var parent = "Assets/Game/0Scene";
            if (AssetDatabase.IsValidFolder(parent))
                AssetDatabase.CreateFolder(parent, "GameHub");
        }

        Directory.CreateDirectory(PrefabDir.Replace('/', Path.DirectorySeparatorChar));

        var root = BuildPortfolioCanvas(font);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        EditorUtility.DisplayDialog(
            "Portfolio Panel",
            $"프리팹 생성 완료:\n{PrefabPath}\n\nGameScene의 GameHub → TabContent 아래에 인스턴스하고,\nGameHubTabController의 portfolioPanel에 연결하세요.",
            "OK");
    }

    static GameObject BuildPortfolioCanvas(TMP_FontAsset font)
    {
        var go = new GameObject("GameHub_PortfolioCanvas", typeof(RectTransform));
        var rootRt = go.GetComponent<RectTransform>();
        StretchFull(rootRt);

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();

        var mgr = go.AddComponent<UserPortfolioManager>();

        // 배경
        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(go.transform, false);
        StretchFull(bg.GetComponent<RectTransform>());
        bg.transform.SetAsFirstSibling();
        bg.GetComponent<Image>().color = BgDark;
        bg.GetComponent<Image>().raycastTarget = false;

        // 메인 컬럼 (글로벌 탑/바텀 여백)
        var main = new GameObject("MainColumn", typeof(RectTransform), typeof(VerticalLayoutGroup));
        main.transform.SetParent(go.transform, false);
        var mainRt = main.GetComponent<RectTransform>();
        mainRt.anchorMin = Vector2.zero;
        mainRt.anchorMax = Vector2.one;
        mainRt.offsetMin = new Vector2(16f, 168f);
        mainRt.offsetMax = new Vector2(-16f, -148f);
        var mainV = main.GetComponent<VerticalLayoutGroup>();
        mainV.padding = new RectOffset(0, 0, 0, 0);
        mainV.spacing = 10f;
        mainV.childAlignment = TextAnchor.UpperCenter;
        mainV.childControlHeight = true;
        mainV.childControlWidth = true;
        mainV.childForceExpandHeight = true;
        mainV.childForceExpandWidth = true;

        // --- Header TotalStats ---
        var header = new GameObject("HeaderTotalStats", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        header.transform.SetParent(main.transform, false);
        header.GetComponent<Image>().color = PanelStrip;
        header.GetComponent<Image>().sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        header.GetComponent<Image>().type = Image.Type.Sliced;
        var headerLe = header.AddComponent<LayoutElement>();
        headerLe.minHeight = 120f;
        headerLe.preferredHeight = 120f;
        headerLe.flexibleHeight = 0f;

        var headerV = header.GetComponent<VerticalLayoutGroup>();
        headerV.padding = new RectOffset(14, 14, 12, 12);
        headerV.spacing = 6f;
        headerV.childAlignment = TextAnchor.UpperLeft;
        headerV.childControlWidth = true;
        headerV.childControlHeight = true;
        headerV.childForceExpandWidth = true;

        var tmpSoldiers = CreateTmp(header.transform, "HeaderTotalSoldiers", font, 26, FontStyles.Bold,
            TextAlignmentOptions.Left, new Color(0.85f, 0.9f, 0.95f));
        tmpSoldiers.text = "총 병력 —";

        var tmpPnl = CreateTmp(header.transform, "HeaderUnrealizedPnL", font, 24, FontStyles.Normal,
            TextAlignmentOptions.Left, new Color(0.35f, 1f, 0.55f));
        tmpPnl.text = "미실현 손익 —";

        var tmpMaint = CreateTmp(header.transform, "HeaderMaintenance", font, 22, FontStyles.Normal,
            TextAlignmentOptions.Left, new Color(0.75f, 0.78f, 0.85f));
        tmpMaint.text = "12시 예상 유지비 —";

        // --- War zone ---
        var warRoot = new GameObject("WarZoneRoot", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(Image));
        warRoot.transform.SetParent(main.transform, false);
        warRoot.GetComponent<Image>().color = WarStrip;
        warRoot.GetComponent<Image>().sprite = header.GetComponent<Image>().sprite;
        warRoot.GetComponent<Image>().type = Image.Type.Sliced;
        var warRootV = warRoot.GetComponent<VerticalLayoutGroup>();
        warRootV.padding = new RectOffset(10, 10, 8, 10);
        warRootV.spacing = 8f;
        warRootV.childControlHeight = true;
        warRootV.childControlWidth = true;
        warRootV.childForceExpandWidth = true;
        warRootV.childForceExpandHeight = true;
        var warRootLe = warRoot.AddComponent<LayoutElement>();
        warRootLe.minHeight = 200f;
        warRootLe.preferredHeight = 280f;
        warRootLe.flexibleHeight = 0.35f;

        var warTitle = CreateTmp(warRoot.transform, "WarZoneTitle", font, 22, FontStyles.Bold,
            TextAlignmentOptions.Left, new Color(1f, 0.45f, 0.45f));
        warTitle.text = "⚔ 전쟁 구역 · 즉시 대응";
        var warTitleLe = warTitle.gameObject.AddComponent<LayoutElement>();
        warTitleLe.minHeight = 32f;

        ScrollRect warScroll;
        RectTransform warContent;
        BuildScrollRect(warRoot.transform, "WarZoneScroll", out warScroll, out warContent, 160f, 1f);

        // --- General ---
        var genLabel = CreateTmp(main.transform, "GeneralSectionTitle", font, 20, FontStyles.Bold,
            TextAlignmentOptions.Left, new Color(0.35f, 1f, 0.65f));
        genLabel.text = "일반 포지션";
        var genLabelLe = genLabel.gameObject.AddComponent<LayoutElement>();
        genLabelLe.minHeight = 28f;

        ScrollRect genScroll;
        RectTransform genContent;
        BuildScrollRect(main.transform, "GeneralScroll", out genScroll, out genContent, 280f, 1f);

        // --- Stress overlay (최상위) ---
        var stress = new GameObject("StressOverlay", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        stress.transform.SetParent(go.transform, false);
        StretchFull(stress.GetComponent<RectTransform>());
        stress.transform.SetAsLastSibling();
        var stressCg = stress.GetComponent<CanvasGroup>();
        stressCg.alpha = 0f;
        stressCg.blocksRaycasts = false;
        stressCg.interactable = false;
        var stressImg = stress.GetComponent<Image>();
        stressImg.color = StressTint;
        stressImg.raycastTarget = false;

        // UserPortfolioManager 연결
        var so = new SerializedObject(mgr);
        so.FindProperty("headerTotalSoldiersText").objectReferenceValue = tmpSoldiers;
        so.FindProperty("headerUnrealizedPnLText").objectReferenceValue = tmpPnl;
        so.FindProperty("headerMaintenanceText").objectReferenceValue = tmpMaint;
        so.FindProperty("warZoneRoot").objectReferenceValue = warRoot;
        so.FindProperty("warZoneScroll").objectReferenceValue = warScroll;
        so.FindProperty("warZoneContent").objectReferenceValue = warContent;
        so.FindProperty("generalScrollRect").objectReferenceValue = genScroll;
        so.FindProperty("generalContent").objectReferenceValue = genContent;
        so.FindProperty("stressOverlay").objectReferenceValue = stressCg;
        so.FindProperty("stressTintImage").objectReferenceValue = stressImg;
        so.FindProperty("stressTintColor").colorValue = StressTint;
        so.ApplyModifiedPropertiesWithoutUndo();

        warRoot.SetActive(false);

        return go;
    }

    /// <param name="flexibleHeight">VerticalLayoutGroup 내에서 남는 높이 비율(1 = 확장).</param>
    static GameObject BuildScrollRect(Transform parent, string name, out ScrollRect scroll,
        out RectTransform content, float minHeight, float flexibleHeight)
    {
        var scrollGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollGo.transform.SetParent(parent, false);
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0, 0);
        scrollRt.anchorMax = new Vector2(1, 1);
        scrollRt.sizeDelta = Vector2.zero;
        scrollRt.anchoredPosition = Vector2.zero;

        scrollGo.GetComponent<Image>().color = new Color(0.03f, 0.04f, 0.08f, 0.9f);
        scrollGo.GetComponent<Image>().sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        scrollGo.GetComponent<Image>().type = Image.Type.Sliced;

        var scrollLe = scrollGo.AddComponent<LayoutElement>();
        scrollLe.minHeight = minHeight;
        scrollLe.flexibleHeight = flexibleHeight;

        scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(scrollGo.transform, false);
        var vpRt = viewport.GetComponent<RectTransform>();
        StretchFull(vpRt);
        vpRt.offsetMin = new Vector2(4f, 4f);
        vpRt.offsetMax = new Vector2(-4f, -4f);
        viewport.GetComponent<Image>().color = new Color(0, 0, 0, 0.01f);
        scroll.viewport = vpRt;

        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
        contentGo.transform.SetParent(viewport.transform, false);
        content = contentGo.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0, 0);

        var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = 6f;
        vlg.padding = new RectOffset(6, 6, 6, 6);
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;

        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = content;

        return scrollGo;
    }

    static TextMeshProUGUI CreateTmp(Transform parent, string name, TMP_FontAsset font, float size, FontStyles style,
        TextAlignmentOptions align, Color color)
    {
        var o = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        o.transform.SetParent(parent, false);
        var tmp = o.GetComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.color = color;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = true;
        var rt = o.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.sizeDelta = new Vector2(0, size + 12f);
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
