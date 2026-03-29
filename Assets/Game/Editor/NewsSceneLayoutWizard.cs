#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// NewsScene(천하 속보/이벤트 피드) UI 자동 생성.
/// 메뉴: StockThreeKingdoms/Layout/NewsTab/…
/// </summary>
public static class NewsSceneLayoutWizard
{
    public const string ScenePath = "Assets/Game/0Scene/NewsScene.unity";

    const string MenuPath = "StockThreeKingdoms/Layout/NewsTab/NewsScene 레이아웃 자동 생성";
    const float ContentTopInset = 160f;
    const float ContentBottomInset = 180f;

    [MenuItem(MenuPath, false, 0)]
    public static void CreateNewsSceneLayout()
    {
        EnsureSceneFileExists();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var canvasObj = new GameObject("Canvas");
            Undo.RegisterCreatedObjectUndo(canvasObj, "Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(es, "EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        var canvasRt = canvas.transform as RectTransform;
        var contentRoot = EnsureContentRoot(canvasRt);

        var existing = GameObject.Find("NewsTabRoot");
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("NewsScene",
                    "씬에 NewsTabRoot가 이미 있습니다. 새로 만들까요?", "새로 생성", "취소"))
            {
                Selection.activeGameObject = existing;
                return;
            }

            Undo.DestroyObjectImmediate(existing);
        }

        var root = new GameObject("NewsTabRoot", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        Undo.RegisterCreatedObjectUndo(root, "NewsTabRoot");
        root.transform.SetParent(contentRoot, false);
        var rootRt = root.GetComponent<RectTransform>();
        StretchFull(rootRt);
        root.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.09f, 1f);
        var rootV = root.GetComponent<VerticalLayoutGroup>();
        rootV.padding = new RectOffset(12, 12, 8, 12);
        rootV.spacing = 12f;
        rootV.childAlignment = TextAnchor.UpperCenter;
        rootV.childControlWidth = true;
        rootV.childControlHeight = true;
        rootV.childForceExpandWidth = true;
        rootV.childForceExpandHeight = false;

        CreateCategoryTabBar(root.transform);
        CreateNewsScrollArea(root.transform);

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log("[NewsSceneLayoutWizard] NewsTab 레이아웃 생성 완료. 씬을 저장(Ctrl+S)하세요.");
    }

    static void EnsureSceneFileExists()
    {
        string dir = Path.GetDirectoryName(ScenePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        if (File.Exists(ScenePath))
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
    }

    static RectTransform EnsureContentRoot(RectTransform canvasRoot)
    {
        var t = canvasRoot.Find("ContentRoot");
        RectTransform rt;
        if (t == null)
        {
            var go = new GameObject("ContentRoot", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "ContentRoot");
            go.transform.SetParent(canvasRoot, false);
            rt = go.GetComponent<RectTransform>();
            StretchFull(rt);
            rt.offsetMin = new Vector2(16f, ContentBottomInset);
            rt.offsetMax = new Vector2(-16f, -ContentTopInset);
        }
        else
            rt = t as RectTransform;

        return rt;
    }

    static void CreateCategoryTabBar(Transform parent)
    {
        var bar = new GameObject("CategoryTabBar", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(bar, "CategoryTabBar");
        bar.transform.SetParent(parent, false);
        var barLe = bar.GetComponent<LayoutElement>();
        barLe.minHeight = 96f;
        barLe.preferredHeight = 100f;
        barLe.flexibleHeight = 0f;
        var barRt = bar.GetComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0f, 1f);
        barRt.anchorMax = new Vector2(1f, 1f);
        var h = bar.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 8f;
        h.padding = new RectOffset(4, 4, 4, 4);
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = false;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;

        string[] labels = { "전체", "전쟁", "속보", "소문", "본영" };
        string[] icons = { "▦", "⚔", "📢", "?", "⌂" };
        for (int i = 0; i < labels.Length; i++)
        {
            bool selected = i == 1; // 전쟁 탭 선택 상태(목업)
            CreateTabButton(bar.transform, $"Tab_{labels[i]}", icons[i], labels[i], selected);
        }
    }

    static void CreateTabButton(Transform parent, string name, string icon, string label, bool selected)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        var le = go.GetComponent<LayoutElement>();
        le.minWidth = 108f;
        le.preferredWidth = 120f;
        le.flexibleWidth = 1f;
        le.minHeight = 88f;

        var img = go.GetComponent<Image>();
        img.sprite = TryGetUiSquareSprite();
        img.type = Image.Type.Sliced;
        img.color = selected
            ? new Color(0.55f, 0.18f, 0.18f, 1f)
            : new Color(0.42f, 0.30f, 0.22f, 1f);

        var col = go.GetComponent<Button>().colors;
        col.highlightedColor = new Color(0.65f, 0.28f, 0.22f, 1f);
        go.GetComponent<Button>().colors = col;

        var v = new GameObject("Stack", typeof(RectTransform), typeof(VerticalLayoutGroup));
        v.transform.SetParent(go.transform, false);
        StretchFull(v.GetComponent<RectTransform>());
        var vg = v.GetComponent<VerticalLayoutGroup>();
        vg.spacing = 4f;
        vg.childAlignment = TextAnchor.MiddleCenter;
        vg.childControlWidth = true;
        vg.childControlHeight = true;
        vg.padding = new RectOffset(4, 4, 6, 6);

        var iconTmp = CreateTmp(v.transform, "Icon", icon, 28, FontStyles.Normal, TextAlignmentOptions.Center,
            new Color(0.95f, 0.88f, 0.72f, 1f));
        iconTmp.GetComponent<LayoutElement>().minHeight = 32f;
        var labelTmp = CreateTmp(v.transform, "Label", label, 22, FontStyles.Bold, TextAlignmentOptions.Center,
            new Color(0.92f, 0.86f, 0.75f, 1f));
        labelTmp.GetComponent<LayoutElement>().minHeight = 28f;

        if (selected)
        {
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.85f, 0.65f, 0.25f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);
        }
    }

    static void CreateNewsScrollArea(Transform parent)
    {
        var holder = new GameObject("NewsScrollHolder", typeof(RectTransform), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(holder, "NewsScrollHolder");
        holder.transform.SetParent(parent, false);
        var holderLe = holder.GetComponent<LayoutElement>();
        holderLe.flexibleHeight = 1f;
        holderLe.minHeight = 400f;
        StretchFull(holder.GetComponent<RectTransform>());

        var scrollGo = new GameObject("NewsScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        Undo.RegisterCreatedObjectUndo(scrollGo, "NewsScroll");
        scrollGo.transform.SetParent(holder.transform, false);
        StretchFull(scrollGo.GetComponent<RectTransform>());
        scrollGo.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.07f, 0.92f);

        var scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 35f;

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        Undo.RegisterCreatedObjectUndo(viewport, "Viewport");
        viewport.transform.SetParent(scrollGo.transform, false);
        var vpRt = viewport.GetComponent<RectTransform>();
        StretchFull(vpRt);
        viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.02f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        Undo.RegisterCreatedObjectUndo(content, "Content");
        content.transform.SetParent(viewport.transform, false);
        var cRt = content.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0f, 1f);
        cRt.anchorMax = new Vector2(1f, 1f);
        cRt.pivot = new Vector2(0.5f, 1f);
        cRt.sizeDelta = new Vector2(0f, 0f);
        var cv = content.GetComponent<VerticalLayoutGroup>();
        cv.spacing = 14f;
        cv.padding = new RectOffset(10, 10, 10, 24);
        cv.childAlignment = TextAnchor.UpperCenter;
        cv.childControlWidth = true;
        cv.childControlHeight = true;
        cv.childForceExpandWidth = true;
        cv.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = cRt;
        scroll.viewport = vpRt;

        CreateNewsListRow(content.transform, "Row_EV14",
            "EV14 업성(C04) 성벽 보수 완료",
            "방어력 강화가 공시되었습니다. 인근 세력의 시선이 쏠리고 있습니다.",
            "52분 전", new Color(0.35f, 0.42f, 0.52f, 1f), true, true);
        CreateNewsListRow(content.transform, "Row_War",
            "[전쟁] 위·조조군, 신야 일대 기동",
            "한중 방면과 연계된 움직임이 포창되었습니다. 유비 세력은 대비 태세를 강화 중입니다.",
            "1시간 전", new Color(0.48f, 0.32f, 0.28f, 1f), true, true);
        CreateNewsListRow(content.transform, "Row_EV16",
            "EV16 건업 지역 교역로 개척",
            "수송로 정비로 해당 지역 시세 변동 가능성이 제기됩니다.",
            "15분 전", new Color(0.32f, 0.40f, 0.48f, 1f), true, true);

        var template = CreateNewsListRow(content.transform, "NewsListRowTemplate",
            "이벤트 제목 (런타임 바인딩)",
            "요약 두 줄까지 표시합니다. EventMasterData·WorldNews와 연결하세요.",
            "방금 전", new Color(0.25f, 0.27f, 0.32f, 1f), false, false);
        template.SetActive(false);
    }

    static GameObject CreateNewsListRow(Transform parent, string name, string title, string summary, string timeAgo,
        Color thumbColor, bool showNewBadge, bool active)
    {
        var row = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(row, name);
        row.transform.SetParent(parent, false);
        row.GetComponent<Image>().color = new Color(0.11f, 0.12f, 0.15f, 0.98f);
        var rowH = row.GetComponent<HorizontalLayoutGroup>();
        rowH.spacing = 14f;
        rowH.padding = new RectOffset(14, 14, 12, 12);
        rowH.childAlignment = TextAnchor.UpperCenter;
        rowH.childControlWidth = true;
        rowH.childControlHeight = true;
        rowH.childForceExpandWidth = false;
        rowH.childForceExpandHeight = false;
        var rowLe = row.GetComponent<LayoutElement>();
        rowLe.minHeight = 148f;
        rowLe.preferredHeight = 156f;
        rowLe.flexibleWidth = 1f;

        var thumbWrap = new GameObject("Thumbnail", typeof(RectTransform), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(thumbWrap, "Thumbnail");
        thumbWrap.transform.SetParent(row.transform, false);
        var twLe = thumbWrap.GetComponent<LayoutElement>();
        twLe.minWidth = 120f;
        twLe.preferredWidth = 128f;
        twLe.minHeight = 120f;
        twLe.preferredHeight = 120f;
        twLe.flexibleWidth = 0f;

        var thumb = new GameObject("ThumbImage", typeof(RectTransform), typeof(Image));
        thumb.transform.SetParent(thumbWrap.transform, false);
        StretchFull(thumb.GetComponent<RectTransform>());
        var thImg = thumb.GetComponent<Image>();
        thImg.sprite = TryGetUiSquareSprite();
        thImg.type = Image.Type.Simple;
        thImg.color = thumbColor;

        if (showNewBadge)
        {
            var badge = new GameObject("NewBadge", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(badge, "NewBadge");
            badge.transform.SetParent(thumbWrap.transform, false);
            var bRt = badge.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0f, 1f);
            bRt.anchorMax = new Vector2(0f, 1f);
            bRt.pivot = new Vector2(0f, 1f);
            bRt.anchoredPosition = new Vector2(4f, -4f);
            bRt.sizeDelta = new Vector2(52f, 28f);
            badge.GetComponent<Image>().color = new Color(0.85f, 0.2f, 0.2f, 1f);
            badge.GetComponent<Image>().sprite = TryGetUiSquareSprite();
            CreateTmp(badge.transform, "NewText", "NEW", 18, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        }

        var right = new GameObject("RightColumn", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(right, "RightColumn");
        right.transform.SetParent(row.transform, false);
        right.GetComponent<LayoutElement>().flexibleWidth = 1f;
        var rightV = right.GetComponent<VerticalLayoutGroup>();
        rightV.spacing = 6f;
        rightV.childAlignment = TextAnchor.UpperLeft;
        rightV.childControlWidth = true;
        rightV.childControlHeight = true;
        rightV.childForceExpandWidth = true;
        rightV.childForceExpandHeight = false;

        var titleRow = new GameObject("TitleRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        Undo.RegisterCreatedObjectUndo(titleRow, "TitleRow");
        titleRow.transform.SetParent(right.transform, false);
        var trLe = titleRow.AddComponent<LayoutElement>();
        trLe.minHeight = 40f;
        trLe.flexibleWidth = 1f;
        var trH = titleRow.GetComponent<HorizontalLayoutGroup>();
        trH.spacing = 8f;
        trH.childAlignment = TextAnchor.MiddleLeft;
        trH.childControlWidth = true;
        trH.childControlHeight = true;
        trH.childForceExpandWidth = true;
        trH.childForceExpandHeight = true;

        var titleTmp = CreateTmp(titleRow.transform, "Title", title, 26, FontStyles.Bold, TextAlignmentOptions.Left,
            new Color(0.95f, 0.93f, 0.88f, 1f));
        titleTmp.enableWordWrapping = true;
        titleTmp.overflowMode = TextOverflowModes.Ellipsis;
        titleTmp.GetComponent<LayoutElement>().flexibleWidth = 1f;

        var timeTmp = CreateTmp(titleRow.transform, "TimeAgo", timeAgo, 20, FontStyles.Normal, TextAlignmentOptions.MidlineRight,
            new Color(0.62f, 0.65f, 0.70f, 1f));
        timeTmp.GetComponent<LayoutElement>().minWidth = 120f;
        timeTmp.GetComponent<LayoutElement>().flexibleWidth = 0f;

        var bodyTmp = CreateTmp(right.transform, "Summary", summary, 22, FontStyles.Normal, TextAlignmentOptions.TopLeft,
            new Color(0.72f, 0.74f, 0.78f, 1f));
        bodyTmp.enableWordWrapping = true;
        bodyTmp.maxVisibleLines = 2;
        bodyTmp.overflowMode = TextOverflowModes.Ellipsis;
        var bodyLe = bodyTmp.GetComponent<LayoutElement>();
        bodyLe.minHeight = 56f;
        bodyLe.flexibleWidth = 1f;

        var footer = new GameObject("FooterRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        Undo.RegisterCreatedObjectUndo(footer, "FooterRow");
        footer.transform.SetParent(right.transform, false);
        footer.AddComponent<LayoutElement>().minHeight = 44f;
        var fH = footer.GetComponent<HorizontalLayoutGroup>();
        fH.childAlignment = TextAnchor.MiddleRight;
        fH.childControlWidth = true;
        fH.padding = new RectOffset(0, 0, 4, 0);

        var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(footer.transform, false);
        spacer.GetComponent<LayoutElement>().flexibleWidth = 1f;

        var detailBtn = new GameObject("DetailButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(detailBtn, "DetailButton");
        detailBtn.transform.SetParent(footer.transform, false);
        detailBtn.GetComponent<LayoutElement>().minWidth = 160f;
        detailBtn.GetComponent<LayoutElement>().preferredWidth = 180f;
        var dImg = detailBtn.GetComponent<Image>();
        dImg.sprite = TryGetUiSquareSprite();
        dImg.color = new Color(0.22f, 0.38f, 0.62f, 1f);
        CreateTmp(detailBtn.transform, "DetailLabel", "상세보기  >", 22, FontStyles.Bold, TextAlignmentOptions.Center,
            Color.white);

        row.SetActive(active);
        return row;
    }

    static TextMeshProUGUI CreateTmp(Transform parent, string name, string text, float size, FontStyles fs,
        TextAlignmentOptions align, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = fs;
        tmp.alignment = align;
        tmp.color = color;
        tmp.raycastTarget = false;
        StretchFull(go.GetComponent<RectTransform>());
        return tmp;
    }

    static Sprite TryGetUiSquareSprite()
    {
        var s = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        if (s != null) return s;
        s = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        if (s != null) return s;
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
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
