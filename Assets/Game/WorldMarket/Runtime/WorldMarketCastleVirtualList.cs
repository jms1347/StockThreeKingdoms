using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 천하 탭 성 카드 가상 스크롤(오브젝트 풀). 화면에 보이는 행 수 + 소량 버퍼만 인스턴스화.
/// <see cref="DataManager.OnStateTicked"/>마다 <see cref="RefreshData(bool)"/>로 목록·가시 셀 전부 갱신(스크롤 위치 유지).
/// </summary>
[DisallowMultipleComponent]
public class WorldMarketCastleVirtualList : MonoBehaviour
{
    const string LogPrefix = "[WorldMarketCastleVirtualList]";
    const float FallbackViewportHeight = 600f;
    const float ContentHeightExtra = 8f;
    const int PoolSizeMin = 8;
    const int PoolSizeMax = 12;
    const int FramesWaitViewport = 90;
    const int FramesLateRefresh = 120;

    [SerializeField] ScrollRect scrollRect;
    [SerializeField] RectTransform content;
    [SerializeField] GameObject cellTemplate;

    [Tooltip("필터 탭 바가 차지하는 영역(레이아웃·여백 참고용)")]
    [SerializeField] RectTransform filterChipsReservedArea;

    [Tooltip("선택) 패널 제목에 Castle Stocks (표시/총)")]
    [SerializeField] TextMeshProUGUI listHeaderText;

    [Tooltip("카드 한 줄 높이 + 여백")]
    [SerializeField] float cellStride = 232f;

    [Tooltip("뷰포트에 보이는 줄 수 + 이 값만큼 풀에 여유")]
    [SerializeField] int poolBufferRows = 2;

    [SerializeField] WorldMarketCastleListFilter currentFilter = WorldMarketCastleListFilter.All;

    [Header("Diagnostics")]
    [Tooltip("RefreshData 시 정렬 개수·뷰포트·콘텐츠·풀 상태 로그")]
    [SerializeField] bool logListDiagnostics;

    [Tooltip("뷰포트 높이가 0일 때 풀 생성을 지연(최대 90프레임). 끄면 즉시 폴백 높이로 풀 생성")]
    [SerializeField] bool deferPoolUntilViewportValid = true;

    readonly List<string> _orderedIds = new List<string>();
    readonly List<WorldMarketCastleCardView> _pool = new List<WorldMarketCastleCardView>();

    bool _inited;
    float _contentWidth;
    Coroutine _lateRefreshRoutine;
    Coroutine _deferredVisibleRoutine;
    Coroutine _waitViewportRoutine;
    bool _catchUpRefreshOnce;

    public WorldMarketCastleListFilter CurrentFilter => currentFilter;

    /// <summary>필터가 바뀔 때(탭 UI 동기화용).</summary>
    public event Action<WorldMarketCastleListFilter> FilterChanged;

    void LogDiag(string message)
    {
        if (logListDiagnostics)
            Debug.Log($"{LogPrefix} {message}");
    }

    void LogWarn(string message) => Debug.LogWarning($"{LogPrefix} {message}");

    static WorldMarketCastleCardView EnsureCardViewComponent(GameObject go)
    {
        var v = go.GetComponent<WorldMarketCastleCardView>();
        return v != null ? v : go.AddComponent<WorldMarketCastleCardView>();
    }

    void RebuildViewportLayout()
    {
        if (scrollRect == null || scrollRect.viewport == null) return;
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.viewport);
    }

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

    /// <summary>필터 탭 UI에서 호출. 순서·콘텐츠 전체 재구성.</summary>
    public void SetFilter(WorldMarketCastleListFilter filter)
    {
        if (currentFilter == filter) return;
        currentFilter = filter;
        FilterChanged?.Invoke(currentFilter);
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
        TrySubscribeHomeCastleHeader();
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
            dm.OnHomeCastleChanged -= HandleHomeCastleChangedForHeader;
        }
    }

    IEnumerator CoLateRefreshUntilData()
    {
        for (int i = 0; i < FramesLateRefresh; i++)
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
            LogWarn("DataManager 미초기화 — 로컬 SO로 InitializeAllData 시도.");
            dm.InitializeAllData();
            RefreshData();
        }
        else if (_orderedIds.Count == 0 && dm != null && dm.IsReady && !dm.IsStateReady)
        {
            LogWarn("IsReady는 true인데 IsStateReady가 false — InitializeStateData 재시도.");
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

    void TrySubscribeHomeCastleHeader()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null) return;
        dm.OnHomeCastleChanged -= HandleHomeCastleChangedForHeader;
        dm.OnHomeCastleChanged += HandleHomeCastleChangedForHeader;
    }

    void HandleHomeCastleChangedForHeader()
    {
        EnsureListHeaderResolved();
        UpdateListHeader(DataManager.InstanceOrNull);
    }

    void HandleStateDataReady()
    {
        LogDiag("OnStateDataReady → RefreshData()");
        RefreshData();
    }

    void HandleStateTicked()
    {
        var dm = DataManager.InstanceOrNull;
        if (content == null || dm == null || !dm.IsStateReady)
            return;

        // OnStateTicked마다 SO 기준 목록 전체 갱신(스크롤 위치 유지).
        if (!_inited)
        {
            RefreshData(false);
            return;
        }

        RefreshData(true);
    }

    void BuildOrderedIds(DataManager dm, List<string> into)
    {
        into.Clear();
        if (dm != null && dm.IsStateReady)
            into.AddRange(dm.GetOrderedWorldCastleIds(currentFilter));
    }

    void Start()
    {
        Transform t = transform;
        for (int i = 0; i < 16 && t != null; i++, t = t.parent)
        {
            if (t.name != "WorldMarketRoot") continue;
            WorldHqTravelHud.EnsureUnderWorldMarketRoot(t);
            WorldMarketCastleDetailPopup.EnsureUnderWorldMarketRoot(t);
            break;
        }

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
            if (groups[i] == null) continue;
            // Destroy()는 프레임 말에만 제거됨 → 같은 프레임 ForceRebuild 시 VLG가 살아 있어
            // 모든 카드가 (0,0)에 겹치는 원인. 즉시 비활성화 후 제거.
            groups[i].enabled = false;
            Destroy(groups[i]);
        }

        var csf = content.GetComponent<ContentSizeFitter>();
        if (csf != null)
        {
            csf.enabled = false;
            Destroy(csf);
        }
    }

    /// <summary>풀에 없는 CastleStockCardTemplate(Clone) 제거(디버그·중복 생성 잔여물).</summary>
    void CullOrphanCardClonesUnderContent()
    {
        if (content == null || cellTemplate == null) return;

        var keep = new HashSet<Transform>();
        for (int i = 0; i < _pool.Count; i++)
        {
            if (_pool[i] != null)
                keep.Add(_pool[i].transform);
        }

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            var ch = content.GetChild(i);
            if (ch == null) continue;
            if (ch.gameObject == cellTemplate) continue;
            if (keep.Contains(ch)) continue;
            if (!ch.name.StartsWith("CastleStockCardTemplate", StringComparison.Ordinal))
                continue;
            LogDiag($"Cull orphan: {ch.name}");
            Destroy(ch.gameObject);
        }
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
    /// 카드 루트 LayoutGroup이 리빌드 시 <see cref="LayoutPoolRowAtIndex"/>로 준 sizeDelta를 덮어쓰지 않게 합니다.
    /// (VerticalLayoutGroup + childControlHeight 가 흔한 '한 줄만 보임' 원인)
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

        var vlg = go.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;
        }

        float stride = GetEffectiveCellStride();
        float minH = Mathf.Max(40f, stride);
        var le = go.GetComponent<LayoutElement>();
        if (le != null && le.minHeight < minH)
            le.minHeight = minH;
    }

    /// <summary>인스펙터 stride와 카드 템플릿 <see cref="LayoutElement"/> 높이 중 큰 값(행 간격).</summary>
    float GetEffectiveCellStride()
    {
        float s = Mathf.Max(80f, cellStride);
        if (cellTemplate == null) return s;
        var le = cellTemplate.GetComponent<LayoutElement>();
        if (le != null)
        {
            float h = le.preferredHeight > 1f ? le.preferredHeight : le.minHeight;
            if (h > 1f)
                s = Mathf.Max(s, h + 8f);
        }

        return s;
    }

    /// <summary>가상 스크롤 행: Content 기준 상단에서 dataIndex만큼 아래로 배치.</summary>
    void LayoutPoolRowAtIndex(RectTransform rt, int dataIndex, float rowWidth, float stride, float rowHeightMargin = 0f)
    {
        if (rt == null || content == null) return;
        if (rt.parent != content)
            rt.SetParent(content, false);

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
            LogDiag($"뷰포트 높이={viewH:F1} — 풀 생성을 다음 레이아웃까지 지연.");
            if (_waitViewportRoutine == null)
                _waitViewportRoutine = StartCoroutine(CoWaitViewportAndInitPool());
            return;
        }

        if (viewH < 1f)
            viewH = FallbackViewportHeight;

        CreatePoolCells(viewH);
        _inited = true;
    }

    void CreatePoolCells(float viewH)
    {
        float stride = GetEffectiveCellStride();
        int visibleRows = Mathf.CeilToInt(viewH / Mathf.Max(40f, stride));
        int need = visibleRows + Mathf.Max(0, poolBufferRows);
        need = Mathf.Clamp(need, PoolSizeMin, PoolSizeMax);

        cellTemplate.SetActive(false);
        float rowW = ResolveRowWidth();
        for (int i = 0; i < need; i++)
        {
            var go = Instantiate(cellTemplate, content, false);
            go.SetActive(true);
            ConfigurePooledCellRoot(go);
            var view = EnsureCardViewComponent(go);
            LayoutPoolRowAtIndex(go.GetComponent<RectTransform>(), 0, rowW, stride);
            _pool.Add(view);
        }

        CullOrphanCardClonesUnderContent();
    }

    IEnumerator CoWaitViewportAndInitPool()
    {
        for (int f = 0; f < FramesWaitViewport; f++)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            ResolveScrollReferences();
            float vh = GetViewportHeight();
            if (logListDiagnostics && f > 0 && f % 15 == 0)
                LogDiag($"뷰포트 대기 frame={f} viewport.h={vh:F1}");
            if (vh >= 1f)
                break;
        }

        _waitViewportRoutine = null;

        if (_inited || cellTemplate == null || content == null)
            yield break;

        float viewH = GetViewportHeight();
        if (viewH < 1f)
        {
            LogWarn($"뷰포트가 여전히 0에 가깝습니다. 폴백 높이({FallbackViewportHeight})로 풀 생성.");
            viewH = FallbackViewportHeight;
        }

        CreatePoolCells(viewH);
        _inited = true;

        LogDiag($"지연 풀 생성 완료 pool={_pool.Count} viewport.h={GetViewportHeight():F1}");

        RefreshData();
    }

    /// <summary>목록·콘텐츠 높이·스크롤 갱신 진입점 (PD: RefreshList에 해당)</summary>
    /// <param name="preserveScrollPosition">true면 스크롤 정규화 위치 유지(OnStateTicked 등).</param>
    public void RefreshData(bool preserveScrollPosition = false)
    {
        ResolveScrollReferences();
        EnsureListHeaderResolved();
        if (content == null)
        {
            LogWarn("ScrollRect 또는 Content가 없습니다. 인스펙터에서 지정하세요.");
            return;
        }

        StripLegacyLayout();
        InitPoolIfNeeded();
        CullOrphanCardClonesUnderContent();

        var dm = DataManager.InstanceOrNull;
        float scrollNorm = !preserveScrollPosition || scrollRect == null ? 1f : scrollRect.verticalNormalizedPosition;
        bool hadRows = _orderedIds.Count > 0;

        _orderedIds.Clear();
        BuildOrderedIds(dm, _orderedIds);

        if (logListDiagnostics)
        {
            float vh = GetViewportHeight();
            int masterN = dm != null && dm.castleMasterDataMap != null ? dm.castleMasterDataMap.Count : -1;
            int stateN = dm != null && dm.castleStateDataMap != null ? dm.castleStateDataMap.Count : -1;
            bool ready = dm != null && dm.IsReady;
            bool stateReady = dm != null && dm.IsStateReady;
            LogDiag(
                $"RefreshData | orderedIds={_orderedIds.Count} | dm IsReady={ready} IsStateReady={stateReady} | " +
                $"masterMap={masterN} stateMap={stateN} | viewport.h={vh:F1} | " +
                $"content.sizeDelta.y={content.sizeDelta.y:F1} | content.rect={content.rect.width:F0}x{content.rect.height:F0} | " +
                $"pool={_pool.Count} _inited={_inited} filter={currentFilter}");
            if (_orderedIds.Count == 0)
            {
                if (dm == null)
                    LogWarn("목록 0건: DataManager 없음.");
                else if (!stateReady)
                    LogWarn("목록 0건: IsStateReady==false (InitializeStateData 전/미호출).");
                else if (stateN == 0)
                    LogWarn("목록 0건: castleStateDataMap 비어 있음.");
                else
                    LogWarn("목록 0건: 필터/정렬 결과 없음 (filter=" + currentFilter + ").");
            }

            if (vh < 1f)
                LogWarn("뷰포트 높이가 0에 가깝습니다. 레이아웃·마스크·부모 활성화를 확인하세요.");
        }

        UpdateListHeader(dm);
        bool preserveNorm = preserveScrollPosition && hadRows && _orderedIds.Count > 0;
        ApplyLayoutAfterOrderChange(dm, preserveScrollNorm: preserveNorm, verticalNorm: scrollNorm);
    }

    void ApplyLayoutAfterOrderChange(DataManager dm, bool preserveScrollNorm, float verticalNorm)
    {
        Canvas.ForceUpdateCanvases();
        _contentWidth = content.rect.width;

        float stride = GetEffectiveCellStride();
        float viewH = GetViewportHeight();
        float contentH = _orderedIds.Count * stride + ContentHeightExtra;
        float totalH = Mathf.Max(viewH, contentH);

        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(content.sizeDelta.x, totalH);
        // Content에 LayoutGroup이 없음 — ForceRebuildLayoutImmediate(content)는 자식 레이아웃을 건드려
        // 행 anchoredPosition이 덮이거나 한 점에 겹칠 수 있음. 뷰포트만 갱신.
        Canvas.ForceUpdateCanvases();

        LogDiag($"Content 높이: sizeDelta.y={totalH:F0} (max viewport {viewH:F0}, items×stride {contentH:F0})");

        if (scrollRect != null)
        {
            if (preserveScrollNorm)
                scrollRect.verticalNormalizedPosition = Mathf.Clamp01(verticalNorm);
            else
                scrollRect.verticalNormalizedPosition = 1f;
        }

        Canvas.ForceUpdateCanvases();
        // Content 전체 ForceRebuild는 카드 HLG가 행 높이를 덮어쓸 수 있어 뷰포트만 갱신.
        RebuildViewportLayout();

        UpdateVisible();

        if (_deferredVisibleRoutine != null)
            StopCoroutine(_deferredVisibleRoutine);
        _deferredVisibleRoutine = StartCoroutine(CoDeferredUpdateVisible(!preserveScrollNorm));
    }

    IEnumerator CoDeferredUpdateVisible(bool healScrollToTopIfAtBottom)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            RebuildViewportLayout();
        }

        if (content != null)
        {
            _contentWidth = Mathf.Max(_contentWidth, content.rect.width);
            float vh = GetViewportHeight();
            float stride = GetEffectiveCellStride();
            float contentH = _orderedIds.Count * stride + ContentHeightExtra;
            float totalH = Mathf.Max(vh, contentH);
            content.sizeDelta = new Vector2(content.sizeDelta.x, totalH);
        }

        LogDiag($"지연 갱신 후 viewport.h={GetViewportHeight():F1} content.w={content.rect.width:F0}");

        // 콘텐츠가 짧다가 길어진 직후 ScrollRect가 norm=0(하단)에 남으면 첫 행만 보임 → 상단으로 복구
        if (healScrollToTopIfAtBottom && scrollRect != null && _orderedIds.Count > 4
            && scrollRect.verticalNormalizedPosition < 0.02f)
        {
            scrollRect.verticalNormalizedPosition = 1f;
            Canvas.ForceUpdateCanvases();
            RebuildViewportLayout();
        }

        UpdateVisible();
        _deferredVisibleRoutine = null;
    }

    void UpdateListHeader(DataManager dm)
    {
        if (listHeaderText == null) return;
        int listed = _orderedIds.Count;
        int total = 0;
        total = dm != null ? dm.GetWorldCastleUiTotalCount() : 0;
        total = Mathf.Max(total, listed);
        string hqLabel = "—";
        if (dm != null && dm.IsStateReady && !string.IsNullOrWhiteSpace(dm.HomeCastleId))
        {
            string hid = dm.HomeCastleId.Trim();
            string disp = dm.GetCastleDisplayName(hid);
            hqLabel = string.IsNullOrWhiteSpace(disp) ? hid : disp;
        }

        listHeaderText.text = $"천하 성 ({listed}/{total}) · 본영: {hqLabel}";
    }

    /// <summary>
    /// ScrollRect가 실제로 쓰는 정규화 스크롤(1=맨 위)과 콘텐츠·뷰포트 높이로 첫 행 인덱스 계산.
    /// <see cref="RectTransform.anchoredPosition"/>만 쓰면 바운드/피벗에 따라 first가 끝으로 밀려 한 줄만 보일 수 있음.
    /// </summary>
    int ComputeFirstVisibleRowIndex(float stride)
    {
        if (_orderedIds == null || _orderedIds.Count == 0)
            return 0;
        if (scrollRect == null || scrollRect.viewport == null || content == null)
            return 0;

        float vpH = Mathf.Max(1f, scrollRect.viewport.rect.height);
        float contentH = Mathf.Max(
            content.rect.height,
            _orderedIds.Count * stride + ContentHeightExtra);

        float scrollable = Mathf.Max(0f, contentH - vpH);
        if (scrollable < 1f)
            return 0;

        // Unity: verticalNormalizedPosition 1 = 상단, 0 = 하단
        float norm = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);
        float scrolledFromTop = (1f - norm) * scrollable;
        int row = Mathf.FloorToInt(scrolledFromTop / Mathf.Max(1f, stride));
        return Mathf.Max(0, row - 1);
    }

    void UpdateVisible()
    {
        ResolveScrollReferences();
        if (!_inited || content == null)
            return;

        if (scrollRect == null)
        {
            LogWarn("ScrollRect가 없어 목록을 그릴 수 없습니다.");
            return;
        }

        float stride = GetEffectiveCellStride();
        int first = ComputeFirstVisibleRowIndex(stride);

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
            LayoutPoolRowAtIndex(rt, idx, rowW, stride);
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

        float stride = GetEffectiveCellStride();
        cellTemplate.SetActive(false);
        for (int i = 0; i < ids.Count; i++)
        {
            var go = Instantiate(cellTemplate, content, false);
            go.SetActive(true);
            var view = EnsureCardViewComponent(go);
            var rt = go.GetComponent<RectTransform>();
            float rw = ResolveRowWidth();
            LayoutPoolRowAtIndex(rt, i, rw, stride);
            view.Bind(ids[i]);
        }

        float totalH = Mathf.Max(GetViewportHeight(), ids.Count * stride + ContentHeightExtra);
        content.sizeDelta = new Vector2(content.sizeDelta.x, totalH);
        Canvas.ForceUpdateCanvases();
        Debug.Log($"{LogPrefix} 테스트: {ids.Count}행 즉시 생성. 보이면 가상 스크롤 가시 범위 문제, 아니면 마스크·레이어 의심.");
    }
#endif
}
