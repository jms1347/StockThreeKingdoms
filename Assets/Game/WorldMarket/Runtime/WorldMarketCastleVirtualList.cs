using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 성 카드 가상 스크롤: 보이는 개수만 풀링하여 50+ 성도 가볍게 표시.
/// (전용 Loop Scroll 패키지 없이 ScrollRect + 풀)
/// </summary>
[DisallowMultipleComponent]
public class WorldMarketCastleVirtualList : MonoBehaviour
{
    [SerializeField] ScrollRect scrollRect;
    [SerializeField] RectTransform content;
    [SerializeField] GameObject cellTemplate;

    [Tooltip("카드 한 줄 높이 + 여백에 맞춤")]
    [SerializeField] float cellStride = 180f;

    [SerializeField] int poolExtra = 6;

    readonly List<string> _orderedIds = new List<string>();
    readonly List<WorldMarketCastleCardView> _pool = new List<WorldMarketCastleCardView>();

    bool _inited;
    float _contentWidth;

    void Awake()
    {
        if (scrollRect == null)
            scrollRect = GetComponent<ScrollRect>();
        if (scrollRect != null && content == null)
            content = scrollRect.content;

        StripLegacyLayout();
    }

    void OnEnable()
    {
        SubscribeDataManager();
        RefreshData();
    }

    void OnDisable()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm != null)
        {
            dm.OnStateDataReady -= HandleData;
            dm.OnStateTicked -= HandleData;
        }
    }

    void SubscribeDataManager()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null) return;
        dm.OnStateDataReady -= HandleData;
        dm.OnStateTicked -= HandleData;
        dm.OnStateDataReady += HandleData;
        dm.OnStateTicked += HandleData;
    }

    void HandleData() => RefreshData();

    void StripLegacyLayout()
    {
        if (content == null) return;
        var vlg = content.GetComponent<VerticalLayoutGroup>();
        if (vlg != null) Destroy(vlg);
        var csf = content.GetComponent<ContentSizeFitter>();
        if (csf != null) Destroy(csf);
    }

    void Start()
    {
        InitPoolIfNeeded();
        RefreshData();
        if (scrollRect != null)
            scrollRect.onValueChanged.AddListener(_ => UpdateVisible());
    }

    void InitPoolIfNeeded()
    {
        if (_inited || cellTemplate == null || content == null) return;

        float viewH = scrollRect != null && scrollRect.viewport != null
            ? scrollRect.viewport.rect.height
            : 800f;
        int need = Mathf.CeilToInt(viewH / Mathf.Max(40f, cellStride)) + poolExtra;
        need = Mathf.Clamp(need, 8, 24);

        cellTemplate.SetActive(false);
        for (int i = 0; i < need; i++)
        {
            var go = Instantiate(cellTemplate, content, false);
            go.SetActive(true);
            var view = go.GetComponent<WorldMarketCastleCardView>();
            if (view == null) view = go.AddComponent<WorldMarketCastleCardView>();
            StretchTopFullWidth(go.GetComponent<RectTransform>());
            _pool.Add(view);
        }

        _inited = true;
    }

    static void StretchTopFullWidth(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
    }

    public void RefreshData()
    {
        if (content == null) return;

        InitPoolIfNeeded();

        var dm = DataManager.InstanceOrNull;
        _orderedIds.Clear();
        if (dm != null && dm.IsStateReady)
            _orderedIds.AddRange(dm.GetOrderedWorldCastleIds());

        Canvas.ForceUpdateCanvases();
        _contentWidth = content.rect.width;

        float totalH = Mathf.Max(
            scrollRect != null && scrollRect.viewport != null ? scrollRect.viewport.rect.height : 0f,
            _orderedIds.Count * cellStride + 8f);

        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(content.sizeDelta.x, totalH);

        UpdateVisible();
    }

    void UpdateVisible()
    {
        if (!_inited || content == null || scrollRect == null) return;

        float y = content.anchoredPosition.y;
        int first = Mathf.FloorToInt(Mathf.Max(0f, y) / Mathf.Max(1f, cellStride));
        first = Mathf.Max(0, first - 1);

        float w = content.rect.width > 2f ? content.rect.width : _contentWidth;

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
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, cellStride - 6f);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
            rt.anchoredPosition = new Vector2(0f, -idx * cellStride);

            cell.Bind(_orderedIds[idx]);
        }
    }
}
