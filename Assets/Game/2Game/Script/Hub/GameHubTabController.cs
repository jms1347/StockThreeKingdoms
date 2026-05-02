using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    [Tooltip("Portfolio 탭 전용(스크롤 리스트 등). 비우면 Portfolio 탭에서 본영 패널을 유지하고 아래 요약 오버레이만 사용.")]
    [SerializeField] GameObject portfolioPanel;

    [Header("포트폴리오 요약 오버레이 (portfolioPanel 미할당 시)")]
    [SerializeField] GameObject portfolioSummaryOverlay;
    [SerializeField] TextMeshProUGUI portfolioSummaryText;

    [Header("시작 탭")]
    [SerializeField] string initialTabId = "Home";

    [Header("본영 + 천하 동시 표시")]
    [Tooltip("켜면 Home 또는 Market 탭일 때 본영·천하 패널을 둘 다 켭니다. TabContent에서 두 Canvas를 반반(좌/우) 배치하세요.")]
    [SerializeField] bool simultaneousHomeAndWorldPanels;

    GlobalUIManager _gui;

    void Awake()
    {
        EnsureTabPanelReferencesFromHierarchy();
    }

    /// <summary>
    /// 씬에서 직렬화 참조가 빠졌을 때 TabContent 아래 프리팹 루트를 자동으로 찾습니다.
    /// (예: GameHub_HomeCanvas, GameHub_WorldCanvas …)
    /// </summary>
    void EnsureTabPanelReferencesFromHierarchy()
    {
        Transform tabContent = transform.Find("TabContent");
        if (tabContent == null)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                var c = transform.GetChild(i);
                if (c != null && c.name == "TabContent")
                {
                    tabContent = c;
                    break;
                }
            }
        }

        if (tabContent == null)
            return;

        TryAssignTabPanel(ref homeTerritoryPanel, tabContent, "GameHub_HomeCanvas");
        TryAssignTabPanel(ref worldMarketPanel, tabContent, "GameHub_WorldCanvas");
        TryAssignTabPanel(ref newsPanel, tabContent, "GameHub_NewsCanvas");
        TryAssignTabPanel(ref portfolioPanel, tabContent, "GameHub_PortfolioCanvas");
        TryAssignTabPanel(ref ordersPanel, tabContent, "GameHub_OrdersCanvas");
    }

    static void TryAssignTabPanel(ref GameObject slot, Transform parent, string childName)
    {
        if (slot != null || parent == null) return;
        var t = parent.Find(childName);
        if (t != null)
            slot = t.gameObject;
    }

    void Start()
    {
        FixHubRootCanvasScales();
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

    /// <summary>허브 프리팹에 Canvas 루트 scale 0이 들어가면 UI 히트가 전부 죽을 수 있어 보정합니다.</summary>
    static void FixHubRootCanvasScale(GameObject root)
    {
        if (root == null) return;
        var canvas = root.GetComponent<Canvas>();
        if (canvas == null) return;
        var rt = canvas.transform as RectTransform;
        if (rt != null && rt.localScale.sqrMagnitude < 1e-8f)
            rt.localScale = Vector3.one;
    }

    void FixHubRootCanvasScales()
    {
        FixHubRootCanvasScale(homeTerritoryPanel);
        FixHubRootCanvasScale(worldMarketPanel);
        FixHubRootCanvasScale(newsPanel);
        FixHubRootCanvasScale(ordersPanel);
        FixHubRootCanvasScale(portfolioPanel);
    }

    void BindScreenSpaceCameras()
    {
        var cam = Camera.main;
        if (cam == null) return;

        foreach (var root in new[] { homeTerritoryPanel, worldMarketPanel, newsPanel, ordersPanel, portfolioPanel })
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
        bool showPortfolio = false;

        switch (tabId)
        {
            case "Home":
                showHome = true;
                break;
            case "Market":
                showWorld = true;
                break;
            case "Portfolio":
                if (portfolioPanel != null)
                    showPortfolio = true;
                else
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
        SetActiveSafe(portfolioPanel, showPortfolio);

        if (simultaneousHomeAndWorldPanels && (tabId == "Home" || tabId == "Market"))
        {
            SetActiveSafe(homeTerritoryPanel, true);
            SetActiveSafe(worldMarketPanel, true);
        }

        bool portfolioTab = tabId == "Portfolio";
        if (portfolioTab && showPortfolio)
        {
            var pm = portfolioPanel != null
                ? portfolioPanel.GetComponentInChildren<UserPortfolioManager>(true)
                : null;
            pm?.Refresh();
        }

        if (portfolioSummaryOverlay != null)
        {
            if (portfolioTab && portfolioPanel == null)
            {
                portfolioSummaryOverlay.SetActive(true);
                FillPortfolioSummaryOverlay();
            }
            else if (portfolioSummaryOverlay.activeSelf)
                portfolioSummaryOverlay.SetActive(false);
        }

        if (showNews && newsPanel != null)
            RefreshNewsFeedUnder(newsPanel);
    }

    void FillPortfolioSummaryOverlay()
    {
        if (portfolioSummaryText == null) return;
        var dm = DataManager.InstanceOrNull;
        if (dm == null || !dm.IsStateReady || dm.castleStateDataMap == null)
        {
            portfolioSummaryText.text = "데이터 준비 중…";
            return;
        }

        long totalSoldiers = 0;
        float domSum = 0f;
        int domCount = 0;
        var sb = new StringBuilder();
        foreach (var kv in dm.castleStateDataMap)
        {
            var st = kv.Value;
            if (st == null || st.userDeployedTroops <= 0) continue;
            int cap = Mathf.Max(1, st.maxGarrison);
            float dom = st.userDeployedTroops / (float)cap * 100f;
            totalSoldiers += st.userDeployedTroops;
            domSum += dom;
            domCount++;
            sb.AppendLine($"{st.id}: 병 {st.userDeployedTroops:N0} · 지배 {dom:0.#}%");
        }

        string head = domCount > 0
            ? $"총 주둔 <b>{totalSoldiers:N0}</b>명 · 거점 <b>{domCount}</b>성 · 평균 지배력 <b>{domSum / domCount:0.#}%</b>\n\n"
            : "보유 성 주둔이 없습니다.\n\n";
        portfolioSummaryText.text = head + (sb.Length > 0 ? sb.ToString().TrimEnd() : "");
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

        var dm = DataManager.InstanceOrNull;
        long soldiers = dm != null && dm.IsStateReady
            ? UserPortfolioManager.GetTotalOwnedSoldiers(dm)
            : gm.currentUser.soldierCount;
        gui.SetTopBarNumbers(gm.currentGold, soldiers);
    }
}
