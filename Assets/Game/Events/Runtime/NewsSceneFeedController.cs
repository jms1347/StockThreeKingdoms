using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>NewsScene <c>NewsTabRoot</c> 피드. <see cref="DataManager.worldNews"/>와 상세 팝업을 연동합니다.</summary>
public class NewsSceneFeedController : MonoBehaviour
{
    [SerializeField] NewsDetailPopup detailPopup;
    [SerializeField] RectTransform listContent;
    [SerializeField] GameObject rowTemplate;
    [Tooltip("비우면 자식에서 CategoryTabBar를 찾습니다. 자식 순서: 전체→전쟁→속보(팩트)→소문→본영 (= WorldNewsFeedKind 0~4)")]
    [SerializeField] RectTransform categoryTabBarRoot;
    [SerializeField] int maxRows = 40;
    [Tooltip("0=전체, 1~4 = WorldNewsFeedKind (탭과 동기화)")]
    [SerializeField] int listFilter;

    static readonly string[] CategoryTabLabels =
    {
        "전체",
        "전쟁",
        "속보(팩트)",
        "소문",
        "본영"
    };

    readonly List<GameObject> _rows = new List<GameObject>();
    readonly List<Button> _categoryButtons = new List<Button>();
    readonly List<Image> _categoryTabImages = new List<Image>();
    readonly List<Color> _categoryTabBaseColors = new List<Color>();
    ScrollRect _listScroll;
    bool _categoryTabsWired;
    Coroutine _debouncedRebuildCo;

    [Tooltip("같은 프레임에 뉴스가 여러 건 쌓일 때 리스트 전체 재구성을 한 번으로 묶습니다.")]
    [SerializeField] float rebuildDebounceSeconds = 0.12f;

    void Awake() => ResolveRefs();

    void OnEnable()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm != null)
            dm.OnNewsAdded += OnNewsAddedHandler;
        EnsureCategoryTabBarLayout();
        WireCategoryTabs();
        ClampListFilterToTabCount();
        UpdateCategoryTabVisuals();
        RebuildList();
        StartCoroutine(CoRetryWhenDataReady());
    }

    void OnDisable()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm != null)
            dm.OnNewsAdded -= OnNewsAddedHandler;
        if (_debouncedRebuildCo != null)
        {
            StopCoroutine(_debouncedRebuildCo);
            _debouncedRebuildCo = null;
        }
    }

    void OnNewsAddedHandler(WorldNewsItem _) => RequestDebouncedRebuild();

    void RequestDebouncedRebuild()
    {
        if (!isActiveAndEnabled) return;
        if (_debouncedRebuildCo != null)
            StopCoroutine(_debouncedRebuildCo);
        _debouncedRebuildCo = StartCoroutine(CoDebouncedRebuild());
    }

    IEnumerator CoDebouncedRebuild()
    {
        float wait = Mathf.Max(0.02f, rebuildDebounceSeconds);
        yield return new WaitForSecondsRealtime(wait);
        _debouncedRebuildCo = null;
        RebuildList();
    }

    IEnumerator CoRetryWhenDataReady()
    {
        for (int i = 0; i < 45; i++)
        {
            yield return null;
            var dm = DataManager.InstanceOrNull;
            if (dm != null && dm.IsReady)
            {
                RebuildList();
                yield break;
            }
        }
    }

    void ResolveRefs()
    {
        if (detailPopup == null)
        {
            // NewsDetailOverlay는 NewsTabRoot 형제(ContentRoot 직하)에 있어 parent 한 단계로는 안 잡힘.
            for (Transform t = transform; t != null; t = t.parent)
            {
                var p = t.GetComponentInChildren<NewsDetailPopup>(true);
                if (p != null)
                {
                    detailPopup = p;
                    break;
                }
            }
        }

        if (detailPopup == null)
            detailPopup = UnityEngine.Object.FindFirstObjectByType<NewsDetailPopup>(FindObjectsInactive.Include);

        if (_listScroll == null)
            _listScroll = GetComponentInChildren<ScrollRect>(true);
        if (listContent == null && _listScroll != null)
            listContent = _listScroll.content;

        if (rowTemplate == null)
        {
            var t = FindDeep(transform, "NewsListRowTemplate");
            if (t != null)
                rowTemplate = t.gameObject;
        }

        if (detailPopup != null)
            detailPopup.ResolveRefs();

        if (categoryTabBarRoot == null)
        {
            var t = FindDeep(transform, "CategoryTabBar");
            if (t != null)
                categoryTabBarRoot = t as RectTransform;
        }
    }

    void EnsureCategoryTabBarLayout()
    {
        if (categoryTabBarRoot == null) return;
        var h = categoryTabBarRoot.GetComponent<HorizontalLayoutGroup>();
        if (h == null) return;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true;
        h.childForceExpandWidth = true;
        h.childControlHeight = true;
        h.childForceExpandHeight = true;
        h.spacing = Mathf.Clamp(h.spacing, 4f, 10f);
        h.padding.left = Mathf.Max(h.padding.left, 4);
        h.padding.right = Mathf.Max(h.padding.right, 4);
        for (int i = 0; i < categoryTabBarRoot.childCount; i++)
        {
            var rt = categoryTabBarRoot.GetChild(i) as RectTransform;
            if (rt == null) continue;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var le = rt.GetComponent<LayoutElement>();
            if (le == null)
                le = rt.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.minWidth = Mathf.Max(le.minWidth, 76f);
            le.minHeight = Mathf.Max(le.minHeight, 44f);

            var btn = rt.GetComponent<Button>();
            if (btn != null)
            {
                var label = rt.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null && i < CategoryTabLabels.Length)
                {
                    label.text = CategoryTabLabels[i];
                    label.alignment = TextAlignmentOptions.Midline;
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 16;
                    label.fontSizeMax = 22;
                    label.margin = new Vector4(4f, 2f, 4f, 2f);
                }
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(categoryTabBarRoot);
    }

    void WireCategoryTabs()
    {
        if (categoryTabBarRoot == null || _categoryTabsWired)
            return;

        _categoryButtons.Clear();
        _categoryTabImages.Clear();
        _categoryTabBaseColors.Clear();

        for (int i = 0; i < categoryTabBarRoot.childCount; i++)
        {
            var btn = categoryTabBarRoot.GetChild(i).GetComponent<Button>();
            if (btn == null)
                continue;

            var img = btn.targetGraphic as Image;
            if (img == null)
                img = btn.GetComponent<Image>();

            int tabIndex = _categoryButtons.Count;
            _categoryButtons.Add(btn);
            _categoryTabImages.Add(img);
            if (img != null)
                _categoryTabBaseColors.Add(img.color);
            else
                _categoryTabBaseColors.Add(Color.white);

            btn.transition = Selectable.Transition.None;
            btn.onClick.RemoveAllListeners();
            int filter = tabIndex;
            btn.onClick.AddListener(() => SelectCategoryFilter(filter));
        }

        _categoryTabsWired = _categoryButtons.Count > 0;
    }

    void ClampListFilterToTabCount()
    {
        if (_categoryButtons.Count <= 0)
            return;
        if (listFilter < 0 || listFilter >= _categoryButtons.Count)
            listFilter = 0;
    }

    /// <summary>카테고리 탭 선택(0=전체 … 4=본영). UI·리스트·스크롤을 갱신합니다.</summary>
    public void SelectCategoryFilter(int filterIndex)
    {
        if (_categoryButtons.Count > 0)
        {
            if (filterIndex < 0 || filterIndex >= _categoryButtons.Count)
                filterIndex = 0;
        }
        else if (filterIndex < 0 || filterIndex > (int)WorldNewsFeedKind.Headquarters)
            filterIndex = 0;

        listFilter = filterIndex;
        UpdateCategoryTabVisuals();
        RebuildList();
        ScrollListToTop();
    }

    void UpdateCategoryTabVisuals()
    {
        if (_categoryTabImages.Count == 0)
            return;

        for (int i = 0; i < _categoryTabImages.Count; i++)
        {
            var img = _categoryTabImages[i];
            if (img == null) continue;
            Color baseC = i < _categoryTabBaseColors.Count ? _categoryTabBaseColors[i] : img.color;
            bool sel = i == listFilter;
            img.color = sel ? Brighten(baseC, 1.28f) : Dim(baseC, 0.62f);
        }
    }

    static Color Brighten(Color c, float m)
    {
        return new Color(
            Mathf.Clamp01(c.r * m),
            Mathf.Clamp01(c.g * m),
            Mathf.Clamp01(c.b * m),
            c.a);
    }

    static Color Dim(Color c, float m)
    {
        return new Color(c.r * m, c.g * m, c.b * m, Mathf.Clamp01(c.a * 0.92f));
    }

    void ScrollListToTop()
    {
        if (_listScroll == null) return;
        _listScroll.StopMovement();
        _listScroll.verticalNormalizedPosition = 1f;
    }

    public void RebuildList()
    {
        ResolveRefs();
        if (categoryTabBarRoot != null)
        {
            if (!_categoryTabsWired || _categoryButtons.Count == 0)
            {
                EnsureCategoryTabBarLayout();
                WireCategoryTabs();
                ClampListFilterToTabCount();
            }
        }

        if (listContent == null || rowTemplate == null)
            return;

        foreach (var go in _rows)
        {
            if (go != null)
                Destroy(go);
        }
        _rows.Clear();

        var dm = DataManager.InstanceOrNull;
        if (dm == null || dm.worldNews == null || dm.worldNews.Count == 0)
            return;

        var sorted = new List<WorldNewsItem>(dm.worldNews);
        sorted.Sort((a, b) =>
        {
            int c = b.unixTime.CompareTo(a.unixTime);
            if (c != 0) return c;
            return string.Compare(a.eventId ?? "", b.eventId ?? "", StringComparison.Ordinal);
        });

        int shown = 0;
        for (int i = 0; i < sorted.Count && shown < maxRows; i++)
        {
            var item = sorted[i];
            if (!PassesFilter(item, dm))
                continue;

            var row = Instantiate(rowTemplate, listContent);
            row.name = "NewsRow_" + shown;
            row.SetActive(true);
            BindRow(row, item, dm);
            _rows.Add(row);
            shown++;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
        UpdateCategoryTabVisuals();
        ScrollListToTop();
    }

    void BindRow(GameObject row, WorldNewsItem item, DataManager dm)
    {
        // 템플릿은 "Headline" 또는 TitleRow 안의 "Title"을 씁니다(이름 불일치 시 플레이스홀더가 남음).
        var headline = FindTmpUnder(row.transform, "Headline") ?? FindTmpUnder(row.transform, "Title");
        if (headline != null)
        {
            string t = item.GetEffectiveDetailTitle();
            headline.text = NewsFormatter.ApplyNewsDisplayTextExpansions(dm, t);
        }

        var timeAgo = FindTmpUnder(row.transform, "TimeAgo");
        if (timeAgo != null)
            timeAgo.text = FormatListTimeAgo(item, dm);

        var summary = FindTmpUnder(row.transform, "Summary");
        if (summary != null)
        {
            string s = item.GetEffectiveSummaryForList();
            if (string.IsNullOrEmpty(s)) s = item.text ?? "";
            summary.text = NewsFormatter.ApplyNewsDisplayTextExpansions(dm, s);
        }

        var debuff = FindTmpUnder(row.transform, "DebuffHint");
        if (debuff != null)
            debuff.text = item.debuffIconsHint ?? "";

        void OpenDetail()
        {
            if (detailPopup != null)
                detailPopup.Show(item);
        }

        foreach (var detailBtn in row.GetComponentsInChildren<Button>(true))
        {
            if (detailBtn == null || detailBtn.gameObject.name != "DetailButton") continue;
            detailBtn.onClick.RemoveAllListeners();
            detailBtn.onClick.AddListener(OpenDetail);
            detailBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            detailBtn.transform.SetAsLastSibling();
        }

        // 행 전체 Button은 ScrollRect·자식 DetailButton과 레이캐스트가 겹치면 상세가 안 뜰 수 있어 쓰지 않음.
        var rowBtn = row.GetComponent<Button>();
        if (rowBtn != null)
        {
            rowBtn.onClick.RemoveAllListeners();
            Destroy(rowBtn);
        }
    }

    static string FormatListTimeAgo(WorldNewsItem item, DataManager dm)
    {
        if (item == null) return "";
        long now = TimeManager.GetUnixNow();
        long dt = Math.Max(0L, now - item.unixTime);
        string rel = dt < 60 ? "방금 전"
            : dt < 3600 ? $"{dt / 60}분 전"
            : dt < 86400 ? $"{dt / 3600}시간 전"
            : $"{dt / 86400}일 전";
        if (string.IsNullOrWhiteSpace(item.relatedCastleIdsRaw))
            return rel;
        string raw = item.relatedCastleIdsRaw.Trim();
        string expanded = dm != null ? NewsFormatter.ApplyNewsDisplayTextExpansions(dm, raw) : raw;
        return $"{rel} · 관련: {expanded}";
    }

    /// <summary>0=전체, 1=전쟁, 2=속보(팩트), 3=소문, 4=본영</summary>
    bool PassesFilter(WorldNewsItem item, DataManager dm)
    {
        if (item == null)
            return false;
        if (string.IsNullOrWhiteSpace(item.text)
            && string.IsNullOrWhiteSpace(item.detailTitle)
            && string.IsNullOrWhiteSpace(item.detailBody)
            && string.IsNullOrWhiteSpace(item.headline)
            && string.IsNullOrWhiteSpace(item.bodyContent))
            return false;

        switch (listFilter)
        {
            case (int)WorldNewsFeedKind.All:
                return !IsSystemNewsItem(item);
            case (int)WorldNewsFeedKind.War:
                return ItemHasWarTag(item, dm);
            case (int)WorldNewsFeedKind.Breaking:
                return ItemIsFactBreaking(item);
            case (int)WorldNewsFeedKind.Rumor:
                return ItemIsRumorFeedItem(item);
            case (int)WorldNewsFeedKind.Headquarters:
                return PassesHeadquartersTab(item, dm);
            default:
                return true;
        }
    }

    static bool IsSystemNewsItem(WorldNewsItem item)
    {
        if (item.newsKind == (byte)WorldNewsFeedKind.System)
            return true;
        string t = (item.text ?? "").TrimStart();
        return t.StartsWith("[INIT]", StringComparison.Ordinal)
               || t.StartsWith("[LOAD]", StringComparison.Ordinal);
    }

    static bool ItemHasWarTag(WorldNewsItem item, DataManager dm)
    {
        if (item == null) return false;
        if (item.newsKind == (byte)WorldNewsFeedKind.War) return true;
        const string tag = "[전쟁]";
        if (!string.IsNullOrEmpty(item.text) && item.text.Contains(tag)) return true;
        if (!string.IsNullOrEmpty(item.headline) && item.headline.Contains(tag)) return true;
        if (!string.IsNullOrEmpty(item.bodyContent) && item.bodyContent.Contains(tag)) return true;
        if (string.Equals(item.debuffIconsHint?.Trim(), tag, StringComparison.Ordinal)) return true;

        return false;
    }

    /// <summary>소문 탭: 시스템 제외, 팩트 속보 제외, 소문 성격 기사만.</summary>
    static bool ItemIsRumorFeedItem(WorldNewsItem item)
    {
        if (item == null || IsSystemNewsItem(item)) return false;
        if (ItemIsFactBreaking(item)) return false;
        if (item.isRumorContent) return true;
        if (item.newsKind == (byte)WorldNewsFeedKind.Rumor) return true;
        string tx = (item.text ?? "").TrimStart();
        return tx.StartsWith("[소문]", StringComparison.Ordinal) || (item.text ?? "").Contains("[소문]");
    }

    static bool ItemIsFactBreaking(WorldNewsItem item)
    {
        if (item.isRumorContent)
            return false;
        if (!string.IsNullOrEmpty(item.text) && item.text.Contains("[소문]"))
            return false;
        if (!string.IsNullOrEmpty(item.text) && item.text.Contains("[속보]"))
            return true;
        return string.Equals(item.debuffIconsHint, "[속보]", StringComparison.Ordinal);
    }

    bool PassesHeadquartersTab(WorldNewsItem item, DataManager dm)
    {
        if (dm == null || string.IsNullOrWhiteSpace(dm.HomeCastleId))
            return false;
        return ItemReferencesHomeCastle(item, dm.HomeCastleId.Trim(), dm);
    }

    static bool ItemReferencesHomeCastle(WorldNewsItem item, string homeCastleId, DataManager dm)
    {
        if (string.IsNullOrWhiteSpace(homeCastleId))
            return false;
        string id = homeCastleId.Trim();

        if (!string.IsNullOrWhiteSpace(item.targetCastleId)
            && string.Equals(item.targetCastleId.Trim(), id, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(item.relatedCastleIdsRaw))
        {
            var parts = item.relatedCastleIdsRaw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.Equals(parts[i].Trim(), id, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        string hay = string.Concat(
            item.text ?? "",
            item.headline ?? "",
            item.bodyContent ?? "",
            item.detailTitle ?? "",
            item.detailBody ?? "",
            item.detailSubline ?? "");
        try
        {
            if (Regex.IsMatch(hay, @"\b" + Regex.Escape(id) + @"\b",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                return true;
        }
        catch (ArgumentException)
        {
            // 잘못된 ID — 건너뜀
        }

        return false;
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t == null) return null;
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var r = FindDeep(t.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    static TextMeshProUGUI FindTmpUnder(Transform root, string name)
    {
        var tr = FindDeep(root, name);
        return tr != null ? tr.GetComponent<TextMeshProUGUI>() : null;
    }
}
