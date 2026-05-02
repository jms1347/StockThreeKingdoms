#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 천하(월드 마켓) UI — <b>권장 계층</b> (GlobalUIManager는 별도 프리팹, 본 캔버스는 ContentRoot 안에만 그립니다):
/// <code>
/// GameHub_WorldCanvas (Canvas + CanvasScaler, 전체 스트레치)
/// └─ ContentRoot (좌우·상하 여백 = 글로벌 탑/바텀 탭 영역 제외)
///    └─ WorldMarketRoot (VLG)
///       ├─ FactionMarketSharePanel
///       ├─ ViewModeRow (리스트/지도 토글)
///       ├─ ListViewRoot (flex) … CastleStocksPanel(필터·가상 스크롤)
///       ├─ MapViewRoot (flex, 토글로 표시) … 지도 스크롤
///       └─ CityDetailPanel (모달, LayoutElement.ignoreLayout, 형제 맨 뒤)
/// </code>
/// 메뉴 한 번으로 캔버스 루트·ContentRoot·지도/리스트 분할·형제 순서를 맞춥니다.
/// </summary>
public static class GameHubWorldMarketLayoutOrchestrator
{
    public const string GameHubWorldPrefabPath = "Assets/Game/0Scene/GameHub/GameHub_WorldCanvas.prefab";

    const float ContentTopInset = 200f;
    const float ContentBottomInset = 180f;
    const float ContentHorizontalPadding = 20f;

    const string MenuPath = "StockThreeKingdoms/Layout/천하 메뉴/① 천하 허브 레이아웃 구축 (GameHub_WorldCanvas)";

    [MenuItem(MenuPath, false, 10)]
    public static void BuildGameHubWorldMarketLayout()
    {
        var root = PrefabUtility.LoadPrefabContents(GameHubWorldPrefabPath);
        try
        {
            var canvasRt = root.GetComponent<RectTransform>();
            if (canvasRt != null)
            {
                canvasRt.localScale = Vector3.one;
                canvasRt.anchorMin = Vector2.zero;
                canvasRt.anchorMax = Vector2.one;
                canvasRt.pivot = new Vector2(0.5f, 0.5f);
                canvasRt.anchoredPosition = Vector2.zero;
                canvasRt.offsetMin = Vector2.zero;
                canvasRt.offsetMax = Vector2.zero;
            }

            var scaler = root.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 1920f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            var contentRoot = root.transform.Find("ContentRoot") as RectTransform;
            if (contentRoot != null)
            {
                contentRoot.anchorMin = Vector2.zero;
                contentRoot.anchorMax = Vector2.one;
                contentRoot.pivot = new Vector2(0.5f, 0.5f);
                contentRoot.anchoredPosition = Vector2.zero;
                contentRoot.offsetMin = new Vector2(ContentHorizontalPadding, ContentBottomInset);
                contentRoot.offsetMax = new Vector2(-ContentHorizontalPadding, -ContentTopInset);
            }

            var wm = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "WorldMarketRoot");
            if (wm == null)
            {
                EditorUtility.DisplayDialog("천하 레이아웃", "WorldMarketRoot를 찾지 못했습니다.", "확인");
                return;
            }

            if (!WorldMarketMapSplitMigrationWizard.EnsureMapListSplit(wm, out string err))
            {
                EditorUtility.DisplayDialog("천하 레이아웃", err ?? "지도·리스트 분할 실패", "확인");
                return;
            }

            NormalizeWorldMarketRootChildren(wm);

            PrefabUtility.SaveAsPrefabAsset(root, GameHubWorldPrefabPath);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(GameHubWorldPrefabPath);
            EditorUtility.DisplayDialog("천하 레이아웃",
                "GameHub_WorldCanvas 프리팹을 저장했습니다.\n" +
                "· Canvas 루트 스케일/스트레치\n" +
                "· ContentRoot 여백 (글로벌 탑/바텀 탭)\n" +
                "· 지도/리스트 분할 및 WorldMarketRoot 형제 순서",
                "확인");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
    }

    static void NormalizeWorldMarketRootChildren(Transform wm)
    {
        string[] ordered =
        {
            "FactionMarketSharePanel",
            "ViewModeRow",
            "ListViewRoot",
            "MapViewRoot",
        };

        int idx = 0;
        foreach (var name in ordered)
        {
            var t = wm.Find(name);
            if (t != null)
                t.SetSiblingIndex(idx++);
        }

        var city = wm.Find("CityDetailPanel");
        if (city != null)
            city.SetAsLastSibling();
    }
}
#endif
