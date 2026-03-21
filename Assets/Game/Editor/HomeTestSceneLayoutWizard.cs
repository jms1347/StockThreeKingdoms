#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// TestScene을 연 뒤 메뉴에서 실행: TMP 스케일, 버튼 MinHeight, 중앙 3패널 가로 배치,
/// 만보기 패널, 성문 앞 더미 16개, 비행 루트, CollectionManager·HomeUIController 연결.
/// </summary>
public static class HomeTestSceneLayoutWizard
{
    const float TmpScale = 1.65f;
    const int TmpMin = 36;
    const int TmpMax = 96;
    const float ButtonMinHeight = 120f;
    const string MenuPath = "StockThreeKingdoms/Home/Setup TestScene Layout (HomePanels)";

    [MenuItem(MenuPath)]
    static void Run()
    {
        var hp = GameObject.Find("HomePanels");
        if (hp == null)
        {
            EditorUtility.DisplayDialog("Home Layout", "씬에 HomePanels 오브젝트가 없습니다. TestScene을 연 뒤 다시 실행하세요.", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(hp, "Home Layout Setup");

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

        var resourceBar = hp.transform.Find("ResourceBar");
        if (resourceBar != null)
        {
            var le = resourceBar.GetComponent<LayoutElement>() ?? resourceBar.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 96f;
            le.preferredHeight = 120f;
        }

        EnsureCenterRow(hp.transform);
        var ped = EnsurePedometerPanel(hp.transform);
        var pileDock = EnsurePileDock(hp.transform);
        var flyRoot = EnsureFlyIconsRoot(hp.transform);

        var cm = hp.GetComponent<CollectionManager>() ?? hp.gameObject.AddComponent<CollectionManager>();
        cm.homeController = hc;
        cm.flyIconsRoot = flyRoot;
        cm.pileArea = pileDock;
        EnsureFlyIconTemplates(cm, flyRoot);
        if (cm.poolSize < 10) cm.poolSize = 12;

        var goldRt = ui != null && ui.goldText != null ? ui.goldText.rectTransform : FindTmpRect(hp.transform, "GoldText");
        var grainRt = ui != null && ui.grainText != null ? ui.grainText.rectTransform : FindTmpRect(hp.transform, "GrainText");
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

        EditorSceneManager.MarkSceneDirty(hp.scene);
        Debug.Log("[HomeTestSceneLayoutWizard] 완료. 저장(Ctrl+S)하세요.");
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
        var refs = new PedometerRefs { buttons = new Button[4], labels = new TextMeshProUGUI[4] };
        Transform ped = homeRoot.Find("PedometerPanel");
        RectTransform pedRt;
        if (ped == null)
        {
            var go = new GameObject("PedometerPanel", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "Pedometer");
            go.transform.SetParent(homeRoot, false);
            pedRt = go.GetComponent<RectTransform>();
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

            var titleGo = CreateTmp("PedometerTitle", pedRt, "만보기 (목표: 10,000보)", 40, FontStyles.Bold);
            refs.stepsText = CreateTmp("StepsCount", pedRt, "0 / 10,000", 36, FontStyles.Normal);

            var gaugeBgGo = new GameObject("GaugeBackground", typeof(RectTransform), typeof(Image));
            gaugeBgGo.transform.SetParent(pedRt, false);
            var gBgRt = gaugeBgGo.GetComponent<RectTransform>();
            gBgRt.sizeDelta = new Vector2(0, 36);
            var gBgImg = gaugeBgGo.GetComponent<Image>();
            gBgImg.color = new Color(0.2f, 0.2f, 0.25f, 1f);
            gBgImg.type = Image.Type.Simple;
            var gLe = gaugeBgGo.AddComponent<LayoutElement>();
            gLe.minHeight = 40f;
            gLe.preferredHeight = 44f;
            gLe.flexibleWidth = 1f;

            var fillGo = new GameObject("GaugeFill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(gBgRt, false);
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

            int[] miles = { 2000, 5000, 7000, 10000 };
            var row = new GameObject("MilestoneRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(pedRt, false);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.sizeDelta = new Vector2(0, 120);
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 8f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            var rowLe = row.AddComponent<LayoutElement>();
            rowLe.minHeight = 130f;

            for (int i = 0; i < 4; i++)
            {
                var btnGo = new GameObject($"StepReward_{miles[i]}", typeof(RectTransform), typeof(Image), typeof(Button));
                btnGo.transform.SetParent(row.transform, false);
                var btnRt = btnGo.GetComponent<RectTransform>();
                btnRt.sizeDelta = new Vector2(0, 120);
                var btnImg = btnGo.GetComponent<Image>();
                btnImg.color = new Color(0.25f, 0.35f, 0.5f, 1f);
                var btn = btnGo.GetComponent<Button>();
                btn.targetGraphic = btnImg;
                var btnLe = btnGo.AddComponent<LayoutElement>();
                btnLe.minHeight = 120f;
                btnLe.flexibleWidth = 1f;
                var lbl = CreateTmp("Label", btnRt, $"{miles[i]:N0}보\n보상", 28, FontStyles.Normal);
                lbl.alignment = TextAlignmentOptions.Center;
                refs.buttons[i] = btn;
                refs.labels[i] = lbl;
            }
        }
        else
        {
            pedRt = ped as RectTransform;
            var fillTr = pedRt.Find("GaugeBackground/GaugeFill");
            refs.gaugeFill = fillTr != null ? fillTr.GetComponent<Image>() : pedRt.GetComponentInChildren<Image>(true);
            refs.stepsText = pedRt.Find("StepsCount")?.GetComponent<TextMeshProUGUI>();
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

        pedRt.SetAsLastSibling();
        return refs;
    }

    static TextMeshProUGUI CreateTmp(string name, Transform parent, string text, int size, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = Mathf.Clamp(Mathf.RoundToInt(size * TmpScale), TmpMin, TmpMax);
        tmp.fontStyle = style;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 44f;
        le.flexibleWidth = 1f;
        return tmp;
    }

    static RectTransform EnsurePileDock(Transform homeRoot)
    {
        Transform gate = homeRoot.Find("GateButton");
        Transform dock = homeRoot.Find("PileDock");
        RectTransform dockRt;
        if (dock == null)
        {
            var go = new GameObject("PileDock", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "PileDock");
            go.transform.SetParent(homeRoot, false);
            dockRt = go.GetComponent<RectTransform>();
            dockRt.anchorMin = new Vector2(0.5f, 0.5f);
            dockRt.anchorMax = new Vector2(0.5f, 0.5f);
            dockRt.pivot = new Vector2(0.5f, 0.5f);
            dockRt.sizeDelta = new Vector2(520, 140);
            dockRt.anchoredPosition = new Vector2(0, -40f);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 140f;
            if (gate != null)
                go.transform.SetSiblingIndex(gate.GetSiblingIndex() + 1);
        }
        else
            dockRt = dock as RectTransform;

        return dockRt;
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

    /// <summary>
    /// 비행 풀용 템플릿(씬 참조). Instantiate 원본으로만 쓰이며 비활성 유지.
    /// </summary>
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

        Transform grainTr = flyRoot.Find("FlyGrainTemplate");
        if (grainTr == null)
        {
            var go = new GameObject("FlyGrainTemplate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "FlyGrainTemplate");
            go.transform.SetParent(flyRoot, false);
            ConfigureFlyTemplate(go, new Color(0.5f, 0.85f, 0.35f, 1f));
            grainTr = go.transform;
        }

        Undo.RecordObject(cm, "Fly icon templates");
        if (cm.flyingGoldPrefab == null)
            cm.flyingGoldPrefab = goldTr.gameObject;
        if (cm.flyingGrainPrefab == null)
            cm.flyingGrainPrefab = grainTr.gameObject;
    }

    static void ConfigureFlyTemplate(GameObject go, Color color)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(48f, 48f);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        go.SetActive(false);
    }

    static void AssignPiles(CollectionManager cm, RectTransform dock)
    {
        var goldRow = dock.Find("GoldPilesRow");
        var grainRow = dock.Find("GrainPilesRow");
        if (goldRow == null)
        {
            goldRow = new GameObject("GoldPilesRow", typeof(RectTransform), typeof(HorizontalLayoutGroup)).transform;
            goldRow.SetParent(dock, false);
            var h = goldRow.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 4f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = false;
            h.childControlHeight = false;
            var rt = goldRow.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0.55f);
            rt.anchorMax = new Vector2(1, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        if (grainRow == null)
        {
            grainRow = new GameObject("GrainPilesRow", typeof(RectTransform), typeof(HorizontalLayoutGroup)).transform;
            grainRow.SetParent(dock, false);
            var h = grainRow.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 4f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = false;
            h.childControlHeight = false;
            var rt = grainRow.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0.45f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
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
                var img = go.GetComponent<Image>();
                img.color = c;
                var rt = go.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(52, 52);
            }
            else
                go = ch.gameObject;
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
