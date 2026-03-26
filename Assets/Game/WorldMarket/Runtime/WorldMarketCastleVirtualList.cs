using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 천하 탭 성 카드 가상 스크롤(오브젝트 풀). 화면에 보이는 행 수 + 소량 버퍼만 인스턴스화.
/// OnStateTicked 시 정렬 키가 같으면 보이는 셀만 Rebind, 순서가 바뀌면 콘텐츠 높이·스크롤만 갱신.
/// </summary>
[DisallowMultipleComponent]
public class WorldMarketCastleVirtualList : MonoBehaviour
{
    [SerializeField] ScrollRect scrollRect;
    [SerializeField] RectTransform content;
    [SerializeField] GameObject cellTemplate;

    [Tooltip("필터 칩(전체/내 투자/전쟁/등급)을 나중에 붙일 수 있도록 빈 영역")]
    [SerializeField] RectTransform filterChipsReservedArea;

    [Tooltip("선택) 패널 제목에 Castle Stocks (표시/총)")]
    [SerializeField] TextMeshProUGUI listHeaderText;

    [Tooltip("카드 한 줄 높이 + 여백")]
    [SerializeField] float cellStride = 180f;

    [Tooltip("뷰포트에 보이는 줄 수 + 이 값만큼 풀에 여유")]
    [SerializeField] int poolBufferRows = 2;

    [SerializeField] WorldMarketCastleListFilter currentFilter = WorldMarketCastleListFilter.All;

    [Header("Diagnostics (PD)")]
    [Tooltip("RefreshData 시 정렬 개수·뷰포트·콘텐츠 크기·풀 상태를 로그")]
    [SerializeField] bool logListDiagnostics;

    [Tooltip("뷰포트 높이가 0일 때 풀 생성을 지연(최대 90프레임). 끄면 즉시 폴백 높이로 풀 생성")]
    [SerializeField] bool deferPoolUntilViewportValid = true;

    readonly List<string> _orderedIds = new List<string>();
    readonly List<string> _scratchOrder = new List<string>();
    readonly List<WorldMarketCastleCardView> _pool = new List<WorldMarketCastleCardView>();

    bool _inited;
    float _contentWidth;
    Coroutine _lateRefreshRoutine;
    Coroutine _deferredVisibleRoutine;
    Coroutine _waitViewportRoutine;
    bool _catchUpRefreshOnce;

    public WorldMarketCastleListFilter CurrentFilter => currentFilter;

    void ResolveScrollReferences()
    {
        if (scrollRect == null)
            scrollRect = GetComponent<ScrollRect>();
        if (scrollRect == null)
            scrollRect = GetComponentInParent<ScrollRect>();
        if (scrollRect != null && (content == null || content != scrollRect.content))
            content = scrollRect.content as RectTransform;
    }

    /// <summary>
    /// 인스펙터에 listHeaderText가 비어 있으면 부모 패널의 "Title" TMP를 찾아 연결합니다.
    /// (위저드가 만든 CastleStocksPanel/Title — 런타임 갱신이 되지 않던 경우 방지)
    /// </summary>
    void EnsureListHeaderResolved()
    {
        if (listHeaderText != null) return;
        for (var t = transform.parent; t != null; t = t.parent)
        {
            var titleTf = t.Find("Title");
            if (titleTf == null) continue;
            listHeaderText = titleTf.GetComponent<TextMeshProUGUI>();
            if (listHeaderText != null) return;
        }
    }

    float GetViewportHeight()
    {
        ResolveScrollReferences();
        if (scrollRect == null || scrollRect.viewport == null)
            return 0f;
        return scrollRect.viewport.rect.height;
    }

    /// <summary>필터 칩 UI에서 호출. 순서·콘텐츠 전체 재구성.</summary>
    public void SetFilter(WorldMarketCastleListFilter filter)
    {
        if (currentFilter == filter) return;
        currentFilter = filter;
        RefreshData();
    }

    void Awake()
    {
        ResolveScrollReferences();
        StripLegacyLayout();
    }

    void OnEnable()
    {
        _catchUpRefreshOnce = true;
        TrySubscribeDataManager();
        RefreshData();
        if (_lateRefreshRoutine != null)
            StopCoroutine(_lateRefreshRoutine);
        _lateRefreshRoutine = StartCoroutine(CoLateRefreshUntilData());
    }

    void OnDisable()
    {
        if (_lateRefreshRoutine != null)
        {
            StopCoroutine(_lateRefreshRoutine);
            _lateRefreshRoutine = null;
        }

        if (_deferredVisibleRoutine != null)
        {
            StopCoroutine(_deferredVisibleRoutine);
            _deferredVisibleRoutine = null;
        }

        if (_waitViewportRoutine != null)
        {
            StopCoroutine(_waitViewportRoutine);
            _waitViewportRoutine = null;
        }

        var dm = DataManager.InstanceOrNull;
        if (dm != null)
        {
            dm.OnStateDataReady -= HandleStateDataReady;
            dm.OnStateTicked -= HandleStateTicked;
        }
    }

    IEnumerator CoLateRefreshUntilData()
    {
        for (int i = 0; i < 120; i++)
        {
            yield return null;
            TrySubscribeDataManager();
            RefreshData();
            if (_orderedIds.Count > 0)
            {
                _lateRefreshRoutine = null;
                yield break;
            }
        }

        _lateRefreshRoutine = null;

        var dm = DataManager.InstanceOrNull;
        if (_orderedIds.Count == 0 && dm != null && !dm.IsReady)
        {
            Debug.LogWarning("[WorldMarketCastleVirtualList] DataManager가 아직 초기화되지 않았습니다. 로컬 SO로 초기화를 시도합니다.");
            dm.InitializeAllData();
            RefreshData();
        }
        else if (_orderedIds.Count == 0 && dm != null && dm.IsReady && !dm.IsStateReady)
        {
            // 마스터만 로드된 채 InitializeStateData가 끝나지 않은 비정상 상태(예외 등) 복구
            Debug.LogWarning("[WorldMarketCastleVirtualList] IsReady이나 IsStateReady가 false입니다. InitializeStateData()를 다시 시도합니다.");
            dm.InitializeStateData();
            RefreshData();
        }
    }

    void TrySubscribeDataManager()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null) return;
        dm.OnStateDataReady -= HandleStateDataReady;
        dm.OnStateTicked -= HandleStateTicked;
        dm.OnStateDataReady += HandleStateDataReady;
        dm.OnStateTicked += HandleStateTicked;

        if (_catchUpRefreshOnce && dm.IsStateReady)
        {
            _catchUpRefreshOnce = false;
            RefreshData();
        }
    }

    void HandleStateDataReady()
    {
        if (logListDiagnostics)
            Debug.Log("[WorldMarketCastleVirtualList] OnStateDataReady → RefreshData()");
        RefreshData();
    }

    void HandleStateTicked()
    {
        var dm = DataManager.InstanceOrNull;
        if (content == null || dm == null || !dm.IsStateReady)
            return;

        if (!_inited)
        {
            RefreshData();
            return;
        }

        BuildOrderedIds(dm, _scratchOrder);
        if (SameSequence(_orderedIds, _scratchOrder))
        {
            UpdateVisible();
            UpdateListHeader(dm);
        }
        else
        {
            bool hadData = _orderedIds.Count > 0;
            float norm = hadData && scrollRect != null ? scrollRect.verticalNormalizedPosition : 1f;
            _orderedIds.Clear();
            _orderedIds.AddRange(_scratchOrder);
            ApplyLayoutAfterOrderChange(dm, preserveScrollNorm: hadData, verticalNorm: norm);
        }
    }

    static bool SameSequence(List<string> a, List<string> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
                return false;
        }

        return true;
    }

    void BuildOrderedIds(DataManager dm, List<string> into)
    {
        into.Clear();
        if (dm != null && dm.IsStateReady)
            into.AddRange(dm.GetOrderedWorldCastleIds(currentFilter));
    }

    void Start()
    {
        TrySubscribeDataManager();
        InitPoolIfNeeded();
        RefreshData();
        if (scrollRect != null)
            scrollRect.onValueChanged.AddListener(_ => UpdateVisible());
    }

    void StripLegacyLayout()
    {
        if (content == null) return;
        var groups = content.GetComponents<LayoutGroup>();
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] != null)
                Destroy(groups[i]);
        }

        var csf = content.GetComponent<ContentSizeFitter>();
        if (csf != null) Destroy(csf);
    }

    /// <summary>
    /// Content 가로가 아직 0일 때 스트레치 앵커 자식은 너비 0으로 겹쳐 보이므로, 뷰포트 폭으로 폴백합니다.
    /// </summary>
    float ResolveRowWidth()
    {
        Canvas.ForceUpdateCanvases();
        float w = content != null ? content.rect.width : 0f;
        if (w < 2f)
            w = _contentWidth;
        if (w < 2f && scrollRect != null && scrollRect.viewport != null)
            w = scrollRect.viewport.rect.width;
        if (w < 2f)
            w = 400f;
        return w;
    }

    /// <summary>
    /// 카드 루트 HorizontalLayoutGroup이 SetLayout에서 루트 높이를 자식 선호치(0)로 덮어쓰는 것을 완화합니다.
    /// </summary>
    void ConfigurePooledCellRoot(GameObject go)
    {
        if (go == null) return;
        var hlg = go.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null)
        {
            hlg.childControlHeight = false;
            hlg.childForceExpandHeight = false;
        }

        float minH = Mathf.Max(40f, cellStride - 6f);
        var le = go.GetComponent<LayoutElement>();
        if (le != null && le.minHeight < minH)
            le.minHeight = minH;
    }

    /// <summary>가상 스크롤 행: 상단 기준, 명시적 너비(스트레치 금지로 width=0 버그 방지).</summary>
    static void LayoutPoolRowAtIndex(RectTransform rt, int dataIndex, float rowWidth, float stride, float rowHeightMargin = 6f)
    {
        if (rt == null) return;
        float h = Mathf.Max(40f, stride - rowHeightMargin);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(Mathf.Max(40f, rowWidth), h);
        rt.anchoredPosition = new Vector2(0f, -dataIndex * stride);
        rt.localScale = Vector3.one;
    }

    void InitPoolIfNeeded()
    {
        if (_inited || cellTemplate == null || content == null) return;

        float viewH = GetViewportHeight();
        if (deferPoolUntilViewportValid && viewH < 1f)
        {
            if (logListDiagnostics)
                Debug.LogWarning($"[WorldMarketCastleVirtualList] 뷰포트 높이={viewH:F1} → 풀 생성을 다음 레이아웃까지 지연합니다.");
            if (_waitViewportRoutine == null)
                _waitViewportRoutine = StartCoroutine(CoWaitViewportAndInitPool());
            return;
        }

        if (viewH < 1f)
            viewH = 600f;

        CreatePoolCells(viewH);
        _inited = true;
    }

    void CreatePoolCells(float viewH)
    {
        int visibleRows = Mathf.CeilToInt(viewH / Mathf.Max(40f, cellStride));
        int need = visibleRows + Mathf.Max(0, poolBufferRows);
        need = Mathf.Clamp(need, 8, 12);

        cellTemplate.SetActive(false);
        float rowW = ResolveRowWidth();
        for (int i = 0; i < need; i++)
        {
            var go = Instantiate(cellTemplate, content, false);
            go.SetActive(true);
            ConfigurePooledCellRoot(go);
            var view = go.GetComponent<WorldMarketCastleCardView>();
            if (view == null) view = go.AddComponent<WorldMarketCastleCardView>();
            LayoutPoolRowAtIndex(go.GetComponent<RectTransform>(), 0, rowW, cellStride);
            _pool.Add(view);
        }
    }

    IEnumerator CoWaitViewportAndInitPool()
    {
        for (int f = 0; f < 90; f++)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            ResolveScrollReferences();
            float vh = GetViewportHeight();
            if (logListDiagnostics && f > 0 && f % 15 == 0)
                Debug.Log($"[WorldMarketCastleVirtualList] 뷰포트 대기 frame={f} viewport.h={vh:F1}");
            if (vh >= 1f)
                break;
        }

        _waitViewportRoutine = null;

        if (_inited || cellTemplate == null || content == null)
            yield break;

        float viewH = GetViewportHeight();
        if (viewH < 1f)
        {
            Debug.LogWarning("[WorldMarketCastleVirtualList] 뷰포트가 여전히 0에 가깝습니다. 폴백 높이(600)로 풀을 생성합니다.");
            viewH = 600f;
        }

        CreatePoolCells(viewH);
        _inited = true;

        if (logListDiagnostics)
            Debug.Log($"[WorldMarketCastleVirtualList] 지연 풀 생성 완료 pool={_pool.Count} viewport.h={GetViewportHeight():F1}");

        RefreshData();
    }

    /// <summary>목록·콘텐츠 높이·스크롤 갱신 진입점 (PD: RefreshList에 해당)</summary>
    public void RefreshData()
    {
        ResolveScrollReferences();
        EnsureListHeaderResolved();
        if (content == null)
        {
            Debug.LogWarning("[WorldMarketCastleVirtualList] ScrollRect 또는 Content가 없습니다. 인스펙터에서 ScrollRect·Content를 지정하세요.");
            return;
        }

        StripLegacyLayout();
        InitPoolIfNeeded();

        var dm = DataManager.InstanceOrNull;
        _orderedIds.Clear();
        BuildOrderedIds(dm, _orderedIds);

        int dmOrderedCount = 0;
        if (dm != null && dm.IsStateReady)
            dmOrderedCount = dm.GetOrderedWorldCastleIds(currentFilter).Count;

        if (logListDiagnostics)
        {
            float vh = GetViewportHeight();
            int masterN = dm != null && dm.castleMasterDataMap != null ? dm.castleMasterDataMap.Count : -1;
            int stateN = dm != null && dm.castleStateDataMap != null ? dm.castleStateDataMap.Count : -1;
            bool ready = dm != null && dm.IsReady;
            bool stateReady = dm != null && dm.IsStateReady;
            Debug.Log(
                $"[WorldMarketCastleVirtualList] RefreshData | UI orderedIds={_orderedIds.Count} | " +
                $"GetOrderedCount(참조용)={dmOrderedCount} | dm IsReady={ready} IsStateReady={stateReady} | " +
                $"masterMap={masterN} stateMap={stateN} | " +
                $"viewport.h={vh:F1} | content.sizeDelta.y={content.sizeDelta.y:F1} | content.rect={content.rect.width:F0}x{content.rect.height:F0} | " +
                $"pool={_pool.Count} _inited={_inited} filter={currentFilter}");
            if (_orderedIds.Count == 0)
            {
                if (dm == null)
                    Debug.LogWarning("[WorldMarketCastleVirtualList] 원인: DataManager 인스턴스 없음 → BuildOrderedIds가 비움.");
                else if (!stateReady)
                    Debug.LogWarning("[WorldMarketCastleVirtualList] 원인: IsStateReady==false → BuildOrderedIds가 비움(상태 초기화 전 프레임이거나 InitializeStateData 미호출).");
                else if (stateN == 0)
                    Debug.LogWarning("[WorldMarketCastleVirtualList] 원인: castleStateDataMap 비어 있음 → GetOrderedWorldCastleIds가 빈 목록.");
                else if (dmOrderedCount == 0)
                    Debug.LogWarning("[WorldMarketCastleVirtualList] 원인: 필터/정렬 결과 0건(filter=" + currentFilter + ").");
            }

            if (vh < 1f)
                Debug.LogWarning("[WorldMarketCastleVirtualList] 뷰포트 높이가 0에 가깝습니다. 레이아웃/마스크/부모 활성화를 확인하세요.");
        }

        UpdateListHeader(dm);
        ApplyLayoutAfterOrderChange(dm, preserveScrollNorm: false, verticalNorm: 1f);
    }

    void ApplyLayoutAfterOrderChange(DataManager dm, bool preserveScrollNorm, float verticalNorm)
    {
        Canvas.ForceUpdateCanvases();
        _contentWidth = content.rect.width;

        float viewH = GetViewportHeight();
        float contentH = _orderedIds.Count * cellStride + 8f;
        float totalH = Mathf.Max(viewH, contentH);

        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(content.sizeDelta.x, totalH);

        if (logListDiagnostics)
            Debug.Log($"[WorldMarketCastleVirtualList] Content 높이 강제: sizeDelta.y={totalH:F0} (= max(viewport {viewH:F0}, items×stride {contentH:F0}))");

        if (scrollRect != null)
        {
            if (preserveScrollNorm)
                scrollRect.verticalNormalizedPosition = Mathf.Clamp01(verticalNorm);
            else
                scrollRect.verticalNormalizedPosition = 1f;
        }

        Canvas.ForceUpdateCanvases();
        // Content 전체 ForceRebuild는 카드 루트 HLG가 sizeDelta로 잡은 행 높이를 0에 가깝게 덮어쓸 수 있음(가상 리스트와 충돌).
        if (scrollRect != null && scrollRect.viewport != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.viewport);

        UpdateVisible();

        if (_deferredVisibleRoutine != null)
            StopCoroutine(_deferredVisibleRoutine);
        _deferredVisibleRoutine = StartCoroutine(CoDeferredUpdateVisible());
    }

    IEnumerator CoDeferredUpdateVisible()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (scrollRect != null && scrollRect.viewport != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.viewport);

        yield return null;
        Canvas.ForceUpdateCanvases();
        if (scrollRect != null && scrollRect.viewport != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.viewport);

        if (content != null)
        {
            _contentWidth = Mathf.Max(_contentWidth, content.rect.width);
            float vh = GetViewportHeight();
            float contentH = _orderedIds.Count * cellStride + 8f;
            float totalH = Mathf.Max(vh, contentH);
            content.sizeDelta = new Vector2(content.sizeDelta.x, totalH);
        }

        if (logListDiagnostics)
            Debug.Log($"[WorldMarketCastleVirtualList] 지연 갱신 후 viewport.h={GetViewportHeight():F1} content.w={content.rect.width:F0}");

        UpdateVisible();
        _deferredVisibleRoutine = null;
    }

    void UpdateListHeader(DataManager dm)
    {
        if (listHeaderText == null) return;
        int listed = _orderedIds.Count;
        int total = 0;
        if (dm != null && dm.castleStateDataMap != null)
            total = dm.castleStateDataMap.Count;
        total = Mathf.Max(total, listed);
        listHeaderText.text = $"Castle Stocks ({listed}/{total})";
    }

    void UpdateVisible()
    {
        ResolveScrollReferences();
        if (!_inited || content == null)
            return;

        if (scrollRect == null)
        {
            Debug.LogWarning("[WorldMarketCastleVirtualList] ScrollRect가 없어 목록을 그릴 수 없습니다.");
            return;
        }

        // Vertical ScrollRect: 스크롤에 따라 anchoredPosition.y 부호가 환경마다 달라질 수 있음
        float scrolled = Mathf.Abs(content.anchoredPosition.y);
        int first = Mathf.FloorToInt(scrolled / Mathf.Max(1f, cellStride));
        first = Mathf.Max(0, first - 1);

        float rowW = ResolveRowWidth();
        _contentWidth = Mathf.Max(_contentWidth, rowW);

        for (int i = 0; i < _pool.Count; i++)
        {
            int idx = first + i;
            var cell = _pool[i];
            if (cell == null) continue;

            var rt = cell.transform as RectTransform;
            if (idx >= _orderedIds.Count)
            {
                cell.gameObject.SetActive(false);
                continue;
            }

            cell.gameObject.SetActive(true);
            ConfigurePooledCellRoot(cell.gameObject);
            LayoutPoolRowAtIndex(rt, idx, rowW, cellStride);
            cell.Bind(_orderedIds[idx]);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Diagnostics — Instantiate all rows (no pool, test only)")]
    void DebugEagerInstantiateAllRows()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("플레이 모드에서만 실행하세요.");
            return;
        }

        ResolveScrollReferences();
        if (content == null || cellTemplate == null)
        {
            Debug.LogError("content 또는 cellTemplate 없음");
            return;
        }

        foreach (var c in _pool)
            if (c != null) Destroy(c.gameObject);
        _pool.Clear();
        _inited = false;

        var dm = DataManager.InstanceOrNull;
        var ids = dm != null && dm.IsStateReady
            ? dm.GetOrderedWorldCastleIds(currentFilter)
            : new List<string>();

        cellTemplate.SetActive(false);
        for (int i = 0; i < ids.Count; i++)
        {
            var go = Instantiate(cellTemplate, content, false);
            go.SetActive(true);
            var view = go.GetComponent<WorldMarketCastleCardView>();
            if (view == null) view = go.AddComponent<WorldMarketCastleCardView>();
            var rt = go.GetComponent<RectTransform>();
            float rw = ResolveRowWidth();
            LayoutPoolRowAtIndex(rt, i, rw, cellStride);
            view.Bind(ids[i]);
        }

        float totalH = Mathf.Max(GetViewportHeight(), ids.Count * cellStride + 8f);
        content.sizeDelta = new Vector2(content.sizeDelta.x, totalH);
        Canvas.ForceUpdateCanvases();
        Debug.Log($"[WorldMarketCastleVirtualList] 테스트: {ids.Count}개 직접 생성. 보이면 가상 스크롤 '보이는 구간' 로직 문제, 안 보이면 마스크/알파/레이어 의심.");
    }
#endif
}
