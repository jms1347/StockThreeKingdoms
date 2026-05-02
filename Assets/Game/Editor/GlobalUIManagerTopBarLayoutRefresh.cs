#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GlobalUIManager 상단바(자원: 금·병·유지비·MP) 레이아웃을 모바일/천하 탭에 맞게 보정합니다.
/// 메뉴 실행 후 프리팹을 저장하세요.
/// </summary>
public static class GlobalUIManagerTopBarLayoutRefresh
{
    public const string PrefabPath = "Assets/Game/CommonUI/Prefabs/GlobalUIManager.prefab";

    [MenuItem("StockThreeKingdoms/Layout/GlobalUIManager 상단바 레이아웃 갱신 (자원 탑바)")]
    public static void Run()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            int n = 0;

            var rtRoot = root.GetComponent<RectTransform>();
            if (rtRoot != null && rtRoot.localScale.sqrMagnitude < 1e-6f)
            {
                rtRoot.localScale = Vector3.one;
                n++;
            }

            var scaler = root.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 1920f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                n++;
            }

            var topBar = root.transform.Find("TopBar");
            if (topBar != null)
            {
                var topRt = topBar as RectTransform;
                if (topRt != null)
                {
                    topRt.sizeDelta = new Vector2(topRt.sizeDelta.x, 180f);
                    n++;
                }

                var hlg = topBar.GetComponent<HorizontalLayoutGroup>();
                if (hlg != null)
                {
                    hlg.padding = new RectOffset(14, 14, 12, 12);
                    hlg.spacing = 12f;
                    hlg.childAlignment = TextAnchor.MiddleCenter;
                    hlg.childControlWidth = true;
                    hlg.childControlHeight = true;
                    hlg.childForceExpandWidth = true;
                    hlg.childForceExpandHeight = true;
                    n++;
                }
            }

            var center = root.transform.Find("TopBar/CenterResourceBox");
            if (center != null)
            {
                var le = center.GetComponent<LayoutElement>();
                if (le != null)
                {
                    le.minWidth = 220f;
                    le.flexibleWidth = 2.5f;
                    n++;
                }

                var vlg = center.GetComponent<VerticalLayoutGroup>();
                if (vlg != null)
                {
                    vlg.spacing = 4f;
                    vlg.childAlignment = TextAnchor.UpperCenter;
                    vlg.childControlWidth = true;
                    vlg.childForceExpandWidth = true;
                    n++;
                }
            }

            var profile = root.transform.Find("TopBar/ProfileBox");
            if (profile != null)
            {
                var le = profile.GetComponent<LayoutElement>();
                if (le != null)
                {
                    le.minWidth = 260f;
                    le.flexibleWidth = 1f;
                    n++;
                }
            }

            var right = root.transform.Find("TopBar/RightMarchColumn");
            if (right != null)
            {
                var le = right.GetComponent<LayoutElement>();
                if (le != null)
                {
                    le.minWidth = 108f;
                    le.preferredWidth = 120f;
                    n++;
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"[GlobalUIManagerTopBarLayoutRefresh] 완료 ({n}항목 조정). 경로: {PrefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
    }
}
#endif
