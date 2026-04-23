#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// WorldMapScene, Castle 프리팹, MockCastleData 에셋을 생성·갱신합니다.
/// 메뉴: StockTK / Build WorldMap Scene
/// </summary>
public static class WorldMapSceneBuilder
{
    const string WorldMapRoot = "Assets/Game/WorldMap";
    const string PrefabFolder = WorldMapRoot + "/Prefabs";
    const string DataFolder = WorldMapRoot + "/Data";
    const string PrefabPath = PrefabFolder + "/CastleWorldMap.prefab";
    const string DataPath = DataFolder + "/MockCastleData.asset";
    const string ScenePath = "Assets/Game/0Scene/WorldMapScene.unity";
    const string CastleMasterSoPath = "Assets/Game/0Splash/Script/SO/SoData/Fixed/CastleMasterDataSo.asset";
    const string GeneralMasterSoPath = "Assets/Game/0Splash/Script/SO/SoData/Fixed/GeneralMasterDataSo.asset";

    static readonly string[] TmpFontCandidates =
    {
        "Assets/TextMesh Pro/Resources/Fonts & Materials/esamanru Medium SDF.asset",
        "Assets/TextMesh Pro/Resources/Fonts & Materials/PretendardVariable SDF.asset",
        "Assets/TextMesh Pro/Resources/Fonts & Materials/NEXONLv1GothicRegular SDF.asset",
    };

    [MenuItem("StockTK/Build WorldMap Scene", false, 200)]
    public static void BuildWorldMapScene()
    {
        EnsureFolder(WorldMapRoot);
        EnsureFolder(PrefabFolder);
        EnsureFolder(DataFolder);
        EnsureFolder("Assets/Game/0Scene");

        var mockData = CreateOrLoadMockCastleData();
        var castlePrefab = CreateOrUpdateCastlePrefab();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        ConfigureMainCamera();
        EnsureEventSystem();

        var mapRoot = new GameObject("MapRoot");
        var managers = new GameObject("Managers");
        managers.AddComponent<WorldTimeManager>();
        var colors = managers.AddComponent<CountryColorProvider>();
        var map = managers.AddComponent<MapManager>();

        CreateWorldMapCanvas(out var dayHud, out var detailPanel);

        WireMapManager(map, mockData, castlePrefab, mapRoot.transform, colors, detailPanel, dayHud);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettingsIfMissing(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[WorldMapSceneBuilder] 완료: {ScenePath} (CastleMasterDataSo=시트 동기화 소스, 드래그 팬·성 클릭·일 틱 확인)");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        var leaf = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(leaf))
            AssetDatabase.CreateFolder(parent, leaf);
    }

    static CastleData CreateOrLoadMockCastleData()
    {
        var existing = AssetDatabase.LoadAssetAtPath<CastleData>(DataPath);
        if (existing != null)
        {
            existing.rows = MockCastleDataProvider.BuildDefaultRows();
            EditorUtility.SetDirty(existing);
            return existing;
        }

        var data = ScriptableObject.CreateInstance<CastleData>();
        data.rows = MockCastleDataProvider.BuildDefaultRows();
        AssetDatabase.CreateAsset(data, DataPath);
        return data;
    }

    static GameObject CreateOrUpdateCastlePrefab()
    {
        var root = new GameObject("Castle");
        var col = root.AddComponent<BoxCollider2D>();
        col.size = new Vector2(2.2f, 2.2f);

        var icon = new GameObject("Icon");
        icon.transform.SetParent(root.transform, false);
        var sr = icon.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 10;

        var labelGo = new GameObject("NameLabel");
        labelGo.transform.SetParent(root.transform, false);
        labelGo.transform.localPosition = new Vector3(0f, 0.95f, 0f);
        var tmp = labelGo.AddComponent<TextMeshPro>();
        tmp.text = "성";
        tmp.fontSize = 4.2f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = false;
        var font = ResolveTmpFont();
        if (font != null)
            tmp.font = font;

        var castle = root.AddComponent<Castle>();
        var soCastle = new SerializedObject(castle);
        soCastle.FindProperty("spriteRenderer").objectReferenceValue = sr;
        soCastle.FindProperty("nameLabel").objectReferenceValue = tmp;
        soCastle.ApplyModifiedPropertiesWithoutUndo();

        root.AddComponent<GovernorAI>();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }

    static TMP_FontAsset ResolveTmpFont()
    {
        foreach (var p in TmpFontCandidates)
        {
            var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p);
            if (f != null) return f;
        }

        return null;
    }

    static void ConfigureMainCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        if (!cam.CompareTag("MainCamera"))
            cam.tag = "MainCamera";
        cam.orthographic = true;
        // MockCastleDataProvider 맵 범위(X 약 ±11, Y 약 -7~6.5) 전체가 들어오도록 시야 확보
        cam.orthographicSize = 8.25f;
        cam.transform.position = new Vector3(-0.25f, -0.35f, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.1f, 0.14f, 1f);
        if (cam.GetComponent<WorldMapCameraPanController>() == null)
            cam.gameObject.AddComponent<WorldMapCameraPanController>();
    }

    static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    static Canvas CreateWorldMapCanvas(out TMP_Text dayHud, out CastleDetailPanel detailPanel)
    {
        var canvasGo = new GameObject("WorldMapCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var topBar = CreateUiRect(canvasGo.transform, "TopBar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -56f), new Vector2(0f, 0f));
        var topImg = topBar.gameObject.AddComponent<Image>();
        topImg.color = new Color(0f, 0f, 0f, 0.55f);
        var dayGo = CreateUiRect(topBar, "DayHud", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, -22f), new Vector2(400f, 44f));
        dayHud = dayGo.gameObject.AddComponent<TextMeshProUGUI>();
        dayHud.text = "시뮬레이션 Day — (10초 = 1일)";
        dayHud.fontSize = 28f;
        dayHud.alignment = TextAlignmentOptions.Left;
        var dayFont = ResolveTmpFont();
        if (dayFont != null)
            dayHud.font = dayFont;

        var hint = CreateUiRect(canvasGo.transform, "Hint", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 40f));
        var hintTmp = hint.gameObject.AddComponent<TextMeshProUGUI>();
        hintTmp.text = "빈 곳 드래그: 지도 이동 · 휠: 줌 · 성 클릭: 상세 · AI가 인접 적성으로 출정하면 행군 마커가 도로를 따라 이동합니다. 마커를 누르면 장수·병력을 볼 수 있습니다.";
        hintTmp.fontSize = 20f;
        hintTmp.alignment = TextAlignmentOptions.Center;
        hintTmp.color = new Color(1f, 1f, 1f, 0.75f);
        if (dayFont != null)
            hintTmp.font = dayFont;

        var sheetRt = CreateUiRect(canvasGo.transform, "CastleDetailSheet", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-420f, 24f), new Vector2(-24f, -80f));
        var panelBg = sheetRt.gameObject.AddComponent<Image>();
        panelBg.color = new Color(0.06f, 0.07f, 0.09f, 0.94f);
        var outline = sheetRt.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.45f, 0.55f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        var layoutGo = new GameObject("DetailContent", typeof(RectTransform));
        layoutGo.transform.SetParent(sheetRt, false);
        var layoutRt = layoutGo.GetComponent<RectTransform>();
        StretchFull(layoutRt);
        layoutRt.offsetMin = new Vector2(20f, 20f);
        layoutRt.offsetMax = new Vector2(-20f, -20f);
        var vlg = layoutGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 10f;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandHeight = false;

        var title = CreateTmpRow(layoutGo.transform, "Title", 32f, FontStyles.Bold);
        var castleId = CreateTmpRow(layoutGo.transform, "CastleId", 22f, FontStyles.Normal);
        var country = CreateTmpRow(layoutGo.transform, "Country", 22f, FontStyles.Normal);
        var governor = CreateTmpRow(layoutGo.transform, "Governor", 22f, FontStyles.Normal);
        var army = CreateTmpRow(layoutGo.transform, "Army", 22f, FontStyles.Normal);
        var population = CreateTmpRow(layoutGo.transform, "Population", 22f, FontStyles.Normal);
        var sentiment = CreateTmpRow(layoutGo.transform, "Sentiment", 22f, FontStyles.Normal);
        var value = CreateTmpRow(layoutGo.transform, "Value", 22f, FontStyles.Normal);
        var generals = CreateTmpMultilineRow(layoutGo.transform, "Generals", 20f, dayFont, 100f);
        var genMove = CreateTmpMultilineRow(layoutGo.transform, "GeneralMovement", 20f, dayFont, 72f);
        var supHint = CreateTmpRow(layoutGo.transform, "SiegeSupportHint", 20f, FontStyles.Italic);

        var supBtnGo = new GameObject("SiegeSupportButton", typeof(RectTransform));
        supBtnGo.transform.SetParent(layoutGo.transform, false);
        var supBtnRt = supBtnGo.GetComponent<RectTransform>();
        supBtnRt.sizeDelta = new Vector2(0f, 44f);
        var supBtnLe = supBtnGo.AddComponent<LayoutElement>();
        supBtnLe.minHeight = 44f;
        var supBtnImg = supBtnGo.AddComponent<Image>();
        supBtnImg.color = new Color(0.32f, 0.42f, 0.28f, 1f);
        var supBtn = supBtnGo.AddComponent<Button>();
        supBtn.targetGraphic = supBtnImg;
        var supLabelGo = new GameObject("Label", typeof(RectTransform));
        supLabelGo.transform.SetParent(supBtnGo.transform, false);
        StretchFull(supLabelGo.GetComponent<RectTransform>());
        var supLabel = supLabelGo.AddComponent<TextMeshProUGUI>();
        supLabel.text = "인접 수비 지원";
        supLabel.alignment = TextAlignmentOptions.Center;
        supLabel.fontSize = 20f;
        if (dayFont != null)
            supLabel.font = dayFont;
        supBtnGo.SetActive(false);

        var atkHint = CreateTmpRow(layoutGo.transform, "SiegeAttackSupportHint", 20f, FontStyles.Italic);

        var atkBtnGo = new GameObject("SiegeAttackSupportButton", typeof(RectTransform));
        atkBtnGo.transform.SetParent(layoutGo.transform, false);
        atkBtnGo.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 44f);
        var atkBtnLe = atkBtnGo.AddComponent<LayoutElement>();
        atkBtnLe.minHeight = 44f;
        var atkBtnImg = atkBtnGo.AddComponent<Image>();
        atkBtnImg.color = new Color(0.42f, 0.28f, 0.22f, 1f);
        var atkBtn = atkBtnGo.AddComponent<Button>();
        atkBtn.targetGraphic = atkBtnImg;
        var atkLabelGo = new GameObject("Label", typeof(RectTransform));
        atkLabelGo.transform.SetParent(atkBtnGo.transform, false);
        StretchFull(atkLabelGo.GetComponent<RectTransform>());
        var atkLabel = atkLabelGo.AddComponent<TextMeshProUGUI>();
        atkLabel.text = "인접 공격 지원";
        atkLabel.alignment = TextAlignmentOptions.Center;
        atkLabel.fontSize = 20f;
        if (dayFont != null)
            atkLabel.font = dayFont;
        atkBtnGo.SetActive(false);

        var closeGo = new GameObject("CloseButton", typeof(RectTransform));
        closeGo.transform.SetParent(layoutGo.transform, false);
        var closeRt = closeGo.GetComponent<RectTransform>();
        closeRt.sizeDelta = new Vector2(0f, 48f);
        var closeLe = closeGo.AddComponent<LayoutElement>();
        closeLe.minHeight = 48f;
        var closeImg = closeGo.AddComponent<Image>();
        closeImg.color = new Color(0.25f, 0.35f, 0.5f, 1f);
        var closeBtn = closeGo.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        var closeLabelGo = new GameObject("Label", typeof(RectTransform));
        closeLabelGo.transform.SetParent(closeGo.transform, false);
        var closeLabelRt = closeLabelGo.GetComponent<RectTransform>();
        StretchFull(closeLabelRt);
        var closeLabel = closeLabelGo.AddComponent<TextMeshProUGUI>();
        closeLabel.text = "닫기";
        closeLabel.alignment = TextAlignmentOptions.Center;
        closeLabel.fontSize = 22f;
        if (dayFont != null)
            closeLabel.font = dayFont;

        sheetRt.gameObject.SetActive(false);

        detailPanel = canvasGo.AddComponent<CastleDetailPanel>();

        var so = new SerializedObject(detailPanel);
        so.FindProperty("panelRoot").objectReferenceValue = sheetRt.gameObject;
        so.FindProperty("closeButton").objectReferenceValue = closeBtn;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("castleIdText").objectReferenceValue = castleId;
        so.FindProperty("countryText").objectReferenceValue = country;
        so.FindProperty("governorText").objectReferenceValue = governor;
        so.FindProperty("armyText").objectReferenceValue = army;
        so.FindProperty("populationText").objectReferenceValue = population;
        so.FindProperty("sentimentText").objectReferenceValue = sentiment;
        so.FindProperty("valueText").objectReferenceValue = value;
        so.FindProperty("generalsText").objectReferenceValue = generals;
        so.FindProperty("generalMovementText").objectReferenceValue = genMove;
        so.FindProperty("siegeSupportHintText").objectReferenceValue = supHint;
        so.FindProperty("siegeSupportButton").objectReferenceValue = supBtn;
        so.FindProperty("siegeAttackSupportHintText").objectReferenceValue = atkHint;
        so.FindProperty("siegeAttackSupportButton").objectReferenceValue = atkBtn;
        so.ApplyModifiedPropertiesWithoutUndo();

        BuildMarchTroopInfoPopup(canvasGo.transform, dayFont);

        return canvasGo.GetComponent<Canvas>();
    }

    static RectTransform CreateUiRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return rt;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void BuildMarchTroopInfoPopup(Transform canvasTf, TMP_FontAsset font)
    {
        var root = new GameObject("MarchTroopInfoPopup", typeof(RectTransform));
        root.transform.SetParent(canvasTf, false);
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 0f);
        rootRt.anchorMax = new Vector2(0.5f, 0f);
        rootRt.pivot = new Vector2(0.5f, 0f);
        rootRt.sizeDelta = new Vector2(520f, 200f);
        rootRt.anchoredPosition = new Vector2(0f, 128f);

        var popup = root.AddComponent<MarchTroopInfoPopup>();

        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        var panelRt = panel.GetComponent<RectTransform>();
        StretchFull(panelRt);
        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0.08f, 0.09f, 0.11f, 0.96f);
        var outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(0.45f, 0.55f, 0.65f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        var infoGo = new GameObject("Info", typeof(RectTransform));
        infoGo.transform.SetParent(panel.transform, false);
        var infoRt = infoGo.GetComponent<RectTransform>();
        infoRt.anchorMin = new Vector2(0f, 0.28f);
        infoRt.anchorMax = new Vector2(1f, 1f);
        infoRt.offsetMin = new Vector2(16f, 8f);
        infoRt.offsetMax = new Vector2(-16f, -12f);
        var infoTmp = infoGo.AddComponent<TextMeshProUGUI>();
        infoTmp.fontSize = 22f;
        infoTmp.alignment = TextAlignmentOptions.TopLeft;
        infoTmp.color = Color.white;
        if (font != null)
            infoTmp.font = font;

        var closeGo = new GameObject("CloseMarchInfo", typeof(RectTransform));
        closeGo.transform.SetParent(panel.transform, false);
        var closeRt = closeGo.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(0f, 0f);
        closeRt.anchorMax = new Vector2(1f, 0.28f);
        closeRt.offsetMin = new Vector2(16f, 10f);
        closeRt.offsetMax = new Vector2(-16f, 8f);
        var closeImg = closeGo.AddComponent<Image>();
        closeImg.color = new Color(0.28f, 0.38f, 0.5f, 1f);
        var closeBtn = closeGo.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        var closeLabelGo = new GameObject("Label", typeof(RectTransform));
        closeLabelGo.transform.SetParent(closeGo.transform, false);
        var closeLabelRt = closeLabelGo.GetComponent<RectTransform>();
        StretchFull(closeLabelRt);
        var closeLabel = closeLabelGo.AddComponent<TextMeshProUGUI>();
        closeLabel.text = "닫기";
        closeLabel.alignment = TextAlignmentOptions.Center;
        closeLabel.fontSize = 20f;
        if (font != null)
            closeLabel.font = font;

        var pso = new SerializedObject(popup);
        pso.FindProperty("panelRoot").objectReferenceValue = panel;
        pso.FindProperty("infoText").objectReferenceValue = infoTmp;
        pso.FindProperty("closeButton").objectReferenceValue = closeBtn;
        pso.ApplyModifiedPropertiesWithoutUndo();

        panel.SetActive(false);
    }

    static TMP_Text CreateTmpRow(Transform parent, string name, float fontSize, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, fontSize + 10f);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = fontSize + 10f;
        le.flexibleWidth = 1f;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;
        var font = ResolveTmpFont();
        if (font != null)
            tmp.font = font;
        return tmp;
    }

    static TMP_Text CreateTmpMultilineRow(Transform parent, string name, float fontSize, TMP_FontAsset font,
        float minHeight)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = minHeight;
        le.flexibleWidth = 1f;
        le.preferredHeight = minHeight;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Normal;
        tmp.color = new Color(0.95f, 0.96f, 0.98f, 1f);
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;
        if (font != null)
            tmp.font = font;
        else
        {
            var f = ResolveTmpFont();
            if (f != null) tmp.font = f;
        }

        return tmp;
    }

    static void WireMapManager(
        MapManager map,
        CastleData data,
        GameObject castlePrefab,
        Transform castleParent,
        CountryColorProvider colors,
        CastleDetailPanel detail,
        TMP_Text dayHud)
    {
        var mapComp = castlePrefab.GetComponent<Castle>();
        if (mapComp == null)
        {
            Debug.LogError("[WorldMapSceneBuilder] Castle 프리팹에 Castle 컴포넌트가 없습니다.");
            return;
        }

        var castleSo = AssetDatabase.LoadAssetAtPath<CastleMasterDataSo>(CastleMasterSoPath);
        var generalSo = AssetDatabase.LoadAssetAtPath<GeneralMasterDataSo>(GeneralMasterSoPath);
        if (castleSo == null)
            Debug.LogWarning($"[WorldMapSceneBuilder] CastleMaster SO를 찾지 못했습니다: {CastleMasterSoPath}");
        if (generalSo == null)
            Debug.LogWarning($"[WorldMapSceneBuilder] GeneralMaster SO를 찾지 못했습니다: {GeneralMasterSoPath}");

        var so = new SerializedObject(map);
        so.FindProperty("useCastleDataOverride").boolValue = false;
        so.FindProperty("castleDataSet").objectReferenceValue = data;
        so.FindProperty("castleMasterSo").objectReferenceValue = castleSo;
        so.FindProperty("generalMasterSo").objectReferenceValue = generalSo;
        so.FindProperty("castlePrefab").objectReferenceValue = mapComp;
        so.FindProperty("castleParent").objectReferenceValue = castleParent;
        so.FindProperty("countryColorProvider").objectReferenceValue = colors;
        so.FindProperty("detailPanel").objectReferenceValue = detail;
        so.FindProperty("dayHudText").objectReferenceValue = dayHud;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void AddToBuildSettingsIfMissing(string scenePath)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == scenePath))
            return;
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
