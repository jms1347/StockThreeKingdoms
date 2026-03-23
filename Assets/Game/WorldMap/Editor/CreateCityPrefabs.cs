#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ThreeKingdoms.WorldMap;

namespace ThreeKingdoms.WorldMap.EditorTools
{
    public static class CreateCityPrefabs
    {
        const string PrefabDir = "Assets/Prefabs";
        const string ResourcesDir = "Assets/Game/WorldMap/Resources/WorldMap";

        [MenuItem("StockThreeKingdoms/천하/성 노드·오버레이 프리팹 생성", false, 0)]
        public static void CreatePrefabs()
        {
            var font = TMP_Settings.defaultFontAsset;
            if (font == null)
            {
                EditorUtility.DisplayDialog("TMP", "TMP_Settings에 기본 폰트가 없습니다. TextMesh Pro를 임포트하고 TMP Settings를 확인하세요.", "OK");
                return;
            }

            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(ResourcesDir);

            var uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var cityRoot = BuildCityNode(font, uiSprite);
            var overlayRoot = BuildCityDetailOverlay(font, uiSprite);

            string cityPath = Path.Combine(PrefabDir, "CityNode.prefab").Replace('\\', '/');
            string overlayPath = Path.Combine(PrefabDir, "CityDetailOverlay.prefab").Replace('\\', '/');

            PrefabUtility.SaveAsPrefabAsset(cityRoot, cityPath);
            PrefabUtility.SaveAsPrefabAsset(overlayRoot, overlayPath);

            Object.DestroyImmediate(cityRoot);
            Object.DestroyImmediate(overlayRoot);

            string resCity = Path.Combine(ResourcesDir, "CityNode.prefab").Replace('\\', '/');
            string resOverlay = Path.Combine(ResourcesDir, "CityDetailOverlay.prefab").Replace('\\', '/');
            AssetDatabase.CopyAsset(cityPath, resCity);
            AssetDatabase.CopyAsset(overlayPath, resOverlay);

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Three Kingdoms", $"프리팹 생성 완료:\n{cityPath}\n{overlayPath}\n\nResources 복사:\n{resCity}\n{resOverlay}", "OK");
        }

        [MenuItem("StockThreeKingdoms/천하/City Database 에셋 생성", false, 11)]
        public static void CreateDatabaseAsset()
        {
            string dir = "Assets/Game/WorldMap/Data";
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "CityDatabase.asset").Replace('\\', '/');
            if (AssetDatabase.LoadAssetAtPath<CityDatabase>(path) != null)
            {
                EditorUtility.DisplayDialog("City Database", "이미 존재합니다: " + path, "OK");
                return;
            }

            var db = ScriptableObject.CreateInstance<CityDatabase>();
            db.ResetToDefaultFifty();
            AssetDatabase.CreateAsset(db, path);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = db;
            EditorUtility.DisplayDialog("City Database", "50개 기본 도시로 채워진 CityDatabase를 생성했습니다.", "OK");
        }

        static GameObject BuildCityNode(TMP_FontAsset font, Sprite uiSprite)
        {
            var root = new GameObject("CityNode", typeof(RectTransform));
            var rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(220f, 132f);

            var bg = root.AddComponent<Image>();
            bg.sprite = uiSprite;
            bg.type = Image.Type.Sliced;
            bg.color = new Color32(0x2B, 0x4A, 0x6F, 0xFF);

            var btn = root.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.ColorTint;

            root.AddComponent<LayoutElement>().ignoreLayout = false;

            var war = CreateUIChild(root.transform, "WarBorder", true);
            var warRt = war.GetComponent<RectTransform>();
            Stretch(warRt, 0, 0, 0, 0);
            var warImg = war.GetComponent<Image>();
            warImg.sprite = uiSprite;
            warImg.type = Image.Type.Sliced;
            warImg.color = new Color(1f, 0.35f, 0.12f, 0f);
            var outline = war.AddComponent<Outline>();
            outline.effectColor = new Color32(0xFF, 0x55, 0x22, 0x00);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;

            var glow = CreateUIChild(root.transform, "VolumetricGlow", true);
            var glowRt = glow.GetComponent<RectTransform>();
            glowRt.anchorMin = new Vector2(0f, 0.72f);
            glowRt.anchorMax = new Vector2(1f, 1f);
            glowRt.offsetMin = Vector2.zero;
            glowRt.offsetMax = Vector2.zero;
            var glowImg = glow.GetComponent<Image>();
            glowImg.sprite = uiSprite;
            glowImg.type = Image.Type.Simple;
            glowImg.color = new Color(0.55f, 0.82f, 1f, 0.14f);
            glowImg.raycastTarget = false;

            var stack = CreateUIChild(root.transform, "TextStack", false);
            var stackRt = stack.GetComponent<RectTransform>();
            Stretch(stackRt, 8, 8, 8, 8);
            var vlg = stack.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = 4f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;

            var nameGo = CreateTMP(stack.transform, "CityName", font, 26, FontStyles.Bold, TextAlignmentOptions.Center, new Color32(0xF2, 0xF4, 0xFF, 0xFF));
            var sentGo = CreateTMP(stack.transform, "Sentiment", font, 20, FontStyles.Normal, TextAlignmentOptions.Center, new Color32(0xC8, 0xD4, 0xEC, 0xFF));
            var chGo = CreateTMP(stack.transform, "ChangeRate", font, 20, FontStyles.Bold, TextAlignmentOptions.Center, new Color32(0x7D, 0xE8, 0xA8, 0xFF));

            var node = root.AddComponent<CityNode>();
            var so = new SerializedObject(node);
            so.FindProperty("background").objectReferenceValue = bg;
            so.FindProperty("warBorder").objectReferenceValue = warImg;
            so.FindProperty("volumetricGlow").objectReferenceValue = glowImg;
            so.FindProperty("warOutline").objectReferenceValue = outline;
            so.FindProperty("cityName").objectReferenceValue = nameGo.GetComponent<TextMeshProUGUI>();
            so.FindProperty("sentiment").objectReferenceValue = sentGo.GetComponent<TextMeshProUGUI>();
            so.FindProperty("changeRate").objectReferenceValue = chGo.GetComponent<TextMeshProUGUI>();
            so.FindProperty("button").objectReferenceValue = btn;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        static GameObject BuildCityDetailOverlay(TMP_FontAsset font, Sprite uiSprite)
        {
            var root = new GameObject("CityDetailOverlay", typeof(RectTransform));
            var rootRt = root.GetComponent<RectTransform>();
            StretchFull(rootRt);

            var rootCg = root.AddComponent<CanvasGroup>();
            rootCg.alpha = 0f;
            rootCg.blocksRaycasts = false;

            var dim = CreateUIChild(root.transform, "Dim", true);
            StretchFull(dim.GetComponent<RectTransform>());
            var dimImg = dim.GetComponent<Image>();
            dimImg.sprite = uiSprite;
            dimImg.color = new Color(0f, 0f, 0f, 0.5f);
            var dimBtn = dim.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.transition = Selectable.Transition.None;
            var dimCg = dim.AddComponent<CanvasGroup>();

            var panel = CreateUIChild(root.transform, "PanelRoot", false);
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0f, 0f);
            panelRt.anchorMax = new Vector2(1f, 0.333f);
            panelRt.pivot = new Vector2(0.5f, 0f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;

            var panelBg = CreateUIChild(panel.transform, "PanelBg", true);
            StretchFull(panelBg.GetComponent<RectTransform>());
            var panelBgImg = panelBg.GetComponent<Image>();
            panelBgImg.sprite = uiSprite;
            panelBgImg.type = Image.Type.Sliced;
            panelBgImg.color = new Color32(0x12, 0x14, 0x1E, 0xF5);

            var rim = CreateUIChild(panel.transform, "TopRimGlow", true);
            var rimRt = rim.GetComponent<RectTransform>();
            rimRt.anchorMin = new Vector2(0f, 1f);
            rimRt.anchorMax = new Vector2(1f, 1f);
            rimRt.pivot = new Vector2(0.5f, 1f);
            rimRt.sizeDelta = new Vector2(0f, 3f);
            rimRt.anchoredPosition = Vector2.zero;
            var rimImg = rim.GetComponent<Image>();
            rimImg.sprite = uiSprite;
            rimImg.color = new Color(0.45f, 0.75f, 1f, 0.35f);
            rimImg.raycastTarget = false;

            float pad = 28f;
            var header = CreateUIChild(panel.transform, "Header", false);
            var headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.sizeDelta = new Vector2(-pad * 2f, 56f);
            headerRt.anchoredPosition = new Vector2(0f, -16f);
            var hlg = header.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.padding = new RectOffset(0, 0, 0, 0);
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;

            var title = CreateTMP(header.transform, "CityTitle", font, 34, FontStyles.Bold, TextAlignmentOptions.Left, Color.white);
            var titleLe = title.AddComponent<LayoutElement>();
            titleLe.flexibleWidth = 1f;
            var warBadge = CreateTMP(header.transform, "WarBadge", font, 22, FontStyles.Bold, TextAlignmentOptions.Center, new Color32(0xFF, 0x88, 0x66, 0xFF));
            var warLe = warBadge.AddComponent<LayoutElement>();
            warLe.preferredWidth = 120f;

            CreateLabeledRow(panel.transform, "GovernorRow", "태수", "GovernorName", font, pad, -84f, out _, out var govNameTmp);
            CreateLabeledRow(panel.transform, "PopulationRow", "백성", "PopValue", font, pad, -124f, out _, out var popTmp);

            var chartRow = CreateUIChild(panel.transform, "ChartRow", false);
            var chartRt = chartRow.GetComponent<RectTransform>();
            chartRt.anchorMin = new Vector2(0f, 1f);
            chartRt.anchorMax = new Vector2(1f, 1f);
            chartRt.pivot = new Vector2(0.5f, 1f);
            chartRt.sizeDelta = new Vector2(-pad * 2f, 72f);
            chartRt.anchoredPosition = new Vector2(0f, -168f);
            var chartV = chartRow.AddComponent<VerticalLayoutGroup>();
            chartV.spacing = 8f;
            chartV.childAlignment = TextAnchor.UpperLeft;

            var chartLabelGo = CreateTMP(chartRow.transform, "ChartLabel", font, 20, FontStyles.Normal, TextAlignmentOptions.Left, new Color32(0xA8, 0xB4, 0xCC, 0xFF));
            chartLabelGo.GetComponent<TextMeshProUGUI>().text = "민심";

            var barBg = CreateUIChild(chartRow.transform, "ChartBarBg", true);
            var barBgRt = barBg.GetComponent<RectTransform>();
            barBgRt.sizeDelta = new Vector2(0f, 18f);
            var leBar = barBg.AddComponent<LayoutElement>();
            leBar.minHeight = 18f;
            leBar.preferredHeight = 18f;
            leBar.flexibleWidth = 1f;
            var barBgImg = barBg.GetComponent<Image>();
            barBgImg.sprite = uiSprite;
            barBgImg.type = Image.Type.Sliced;
            barBgImg.color = new Color32(0x20, 0x24, 0x30, 0xFF);

            var fill = CreateUIChild(barBg.transform, "ChartFill", true);
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(4f, 3f);
            fillRt.offsetMax = new Vector2(-4f, -3f);
            var fillImg = fill.GetComponent<Image>();
            fillImg.sprite = uiSprite;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = 0;
            fillImg.color = new Color32(0x4A, 0x9E, 0xD8, 0xFF);
            fillImg.fillAmount = 0.65f;

            var actions = CreateUIChild(panel.transform, "ActionRow", false);
            var actRt = actions.GetComponent<RectTransform>();
            actRt.anchorMin = new Vector2(0f, 0f);
            actRt.anchorMax = new Vector2(1f, 0f);
            actRt.pivot = new Vector2(0.5f, 0f);
            actRt.sizeDelta = new Vector2(-pad * 2f, 72f);
            actRt.anchoredPosition = new Vector2(0f, 28f);
            var alg = actions.AddComponent<HorizontalLayoutGroup>();
            alg.spacing = 20f;
            alg.childAlignment = TextAnchor.MiddleCenter;
            alg.childControlWidth = true;
            alg.childControlHeight = true;
            alg.childForceExpandWidth = true;
            alg.padding = new RectOffset(0, 0, 0, 0);

            var supBtn = CreateActionButton(actions.transform, "SupportBtn", "SUPPORT", font, uiSprite);
            var wdwBtn = CreateActionButton(actions.transform, "WithdrawBtn", "WITHDRAW", font, uiSprite);

            var overlay = root.AddComponent<CityDetailOverlay>();
            var oSo = new SerializedObject(overlay);
            oSo.FindProperty("panelRoot").objectReferenceValue = panelRt;
            oSo.FindProperty("rootCanvasGroup").objectReferenceValue = rootCg;
            oSo.FindProperty("dimCanvasGroup").objectReferenceValue = dimCg;
            oSo.FindProperty("dimImage").objectReferenceValue = dimImg;
            oSo.FindProperty("cityTitle").objectReferenceValue = title.GetComponent<TextMeshProUGUI>();
            oSo.FindProperty("warBadge").objectReferenceValue = warBadge.GetComponent<TextMeshProUGUI>();
            oSo.FindProperty("governorName").objectReferenceValue = govNameTmp;
            oSo.FindProperty("populationValue").objectReferenceValue = popTmp;
            oSo.FindProperty("chartLabel").objectReferenceValue = chartLabelGo.GetComponent<TextMeshProUGUI>();
            oSo.FindProperty("chartFill").objectReferenceValue = fillImg;
            oSo.FindProperty("supportButton").objectReferenceValue = supBtn.GetComponent<Button>();
            oSo.FindProperty("withdrawButton").objectReferenceValue = wdwBtn.GetComponent<Button>();
            oSo.FindProperty("dimButton").objectReferenceValue = dimBtn;
            oSo.FindProperty("supportLabel").objectReferenceValue = supBtn.GetComponentInChildren<TextMeshProUGUI>();
            oSo.FindProperty("withdrawLabel").objectReferenceValue = wdwBtn.GetComponentInChildren<TextMeshProUGUI>();
            oSo.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        static void CreateLabeledRow(Transform parent, string rowName, string labelText, string valueName, TMP_FontAsset font, float pad, float yFromTop, out TextMeshProUGUI labelTmp, out TextMeshProUGUI valueTmp)
        {
            var row = CreateUIChild(parent, rowName, false);
            var rt = row.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(-pad * 2f, 36f);
            rt.anchoredPosition = new Vector2(0f, yFromTop);
            var h = row.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 16f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = false;

            var lab = CreateTMP(row.transform, "Label", font, 20, FontStyles.Normal, TextAlignmentOptions.Left, new Color32(0x88, 0x94, 0xA8, 0xFF));
            lab.GetComponent<TextMeshProUGUI>().text = labelText;
            var labLe = lab.AddComponent<LayoutElement>();
            labLe.preferredWidth = 72f;
            labelTmp = lab.GetComponent<TextMeshProUGUI>();

            var val = CreateTMP(row.transform, valueName, font, 22, FontStyles.Bold, TextAlignmentOptions.Left, new Color32(0xEE, 0xF2, 0xFF, 0xFF));
            valueTmp = val.GetComponent<TextMeshProUGUI>();
        }

        static GameObject CreateActionButton(Transform parent, string name, string label, TMP_FontAsset font, Sprite uiSprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = uiSprite;
            img.type = Image.Type.Sliced;
            img.color = new Color32(0x24, 0x2C, 0x3C, 0xFF);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 56f;
            le.flexibleWidth = 1f;

            var tmpGo = CreateTMP(go.transform, "Label", font, 22, FontStyles.Bold, TextAlignmentOptions.Center, new Color32(0xD0, 0xE8, 0xFF, 0xFF));
            StretchFull(tmpGo.GetComponent<RectTransform>());
            tmpGo.GetComponent<TextMeshProUGUI>().text = label;
            return go;
        }

        static GameObject CreateUIChild(Transform parent, string name, bool image)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            if (image) go.AddComponent<Image>();
            return go;
        }

        static GameObject CreateTMP(Transform parent, string name, TMP_FontAsset font, float size, FontStyles style, TextAlignmentOptions align, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = align;
            tmp.color = color;
            tmp.raycastTarget = false;
            tmp.text = name;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200f, 36f);
            return go;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void Stretch(RectTransform rt, float l, float r, float t, float b)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
        }
    }
}
#endif
