using UnityEngine;

/// <summary>
/// 한 씬에서 GlobalUIManager 하단 탭과 본영·천하·뉴스 UI 루트를 연결합니다.
/// 탭 순서(왼쪽→오른쪽): Portfolio, Market(천하), Home(본영·중앙), News, Orders.
/// </summary>
public class GameHubTabController : MonoBehaviour
{
    [Header("탭별 콘텐츠 루트 (Canvas 또는 그 상위 GameObject)")]
    [SerializeField] GameObject homeTerritoryPanel;
    [SerializeField] GameObject worldMarketPanel;
    [SerializeField] GameObject newsPanel;

    [Tooltip("5번째(Orders) 탭 전용. 비우면 Orders 시 본영과 동일하게 유지.")]
    [SerializeField] GameObject ordersPanel;

    [Header("시작 탭")]
    [SerializeField] string initialTabId = "Home";

    GlobalUIManager _gui;

    void Start()
    {
        BindScreenSpaceCameras();
        _gui = GlobalUIManager.InstanceOrNull;
        if (_gui != null)
            _gui.TabSelected += OnGlobalTabSelected;

        ApplyTab(string.IsNullOrEmpty(initialTabId) ? "Home" : initialTabId);
        RefreshGlobalTopBar();
    }

    void OnDestroy()
    {
        if (_gui != null)
            _gui.TabSelected -= OnGlobalTabSelected;
    }

    void BindScreenSpaceCameras()
    {
        var cam = Camera.main;
        if (cam == null) return;

        foreach (var root in new[] { homeTerritoryPanel, worldMarketPanel, newsPanel, ordersPanel })
        {
            if (root == null) continue;
            var canvases = root.GetComponentsInChildren<Canvas>(true);
            foreach (var c in canvases)
            {
                if (c.renderMode == RenderMode.ScreenSpaceCamera)
                    c.worldCamera = cam;
            }
        }
    }

    void OnGlobalTabSelected(string tabId)
    {
        ApplyTab(tabId);
        RefreshGlobalTopBar();
    }

    void ApplyTab(string tabId)
    {
        bool showHome = false;
        bool showWorld = false;
        bool showNews = false;
        bool showOrders = false;

        switch (tabId)
        {
            case "Home":
                showHome = true;
                break;
            case "Market":
                showWorld = true;
                break;
            case "Portfolio":
                showHome = true;
                break;
            case "News":
                showNews = true;
                break;
            case "Orders":
                if (ordersPanel != null)
                    showOrders = true;
                else
                    showHome = true;
                break;
            default:
                showHome = true;
                break;
        }

        SetActiveSafe(homeTerritoryPanel, showHome);
        SetActiveSafe(worldMarketPanel, showWorld);
        SetActiveSafe(newsPanel, showNews);
        SetActiveSafe(ordersPanel, showOrders);

        if (showNews && newsPanel != null)
            RefreshNewsFeedUnder(newsPanel);
    }

    /// <summary>GameScene 등에서 뉴스 패널이 켜질 때 NewsScene 레이아웃(피드)을 즉시 갱신합니다.</summary>
    static void RefreshNewsFeedUnder(GameObject newsRoot)
    {
        if (newsRoot == null) return;
        var feeds = newsRoot.GetComponentsInChildren<NewsSceneFeedController>(true);
        for (int i = 0; i < feeds.Length; i++)
        {
            if (feeds[i] != null)
                feeds[i].RebuildList();
        }
    }

    static void SetActiveSafe(GameObject go, bool on)
    {
        if (go == null) return;
        if (go.activeSelf != on)
            go.SetActive(on);
    }

    void RefreshGlobalTopBar()
    {
        var gm = GameManager.InstanceOrNull;
        var gui = GlobalUIManager.InstanceOrNull;
        if (gm?.currentUser == null || gui == null) return;

        gui.SetTopBarNumbers(
            gm.currentUser.userName,
            gm.currentGold,
            gm.currentGrain,
            gm.currentUser.soldierCount);
    }
}
