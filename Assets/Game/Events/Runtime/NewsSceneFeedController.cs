using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>NewsScene <c>NewsTabRoot</c> 피드. <see cref="DataManager.worldNews"/>와 상세 팝업을 연동합니다.</summary>
public class NewsSceneFeedController : MonoBehaviour
{
    [SerializeField] NewsDetailPopup detailPopup;
    [SerializeField] RectTransform listContent;
    [SerializeField] GameObject rowTemplate;
    [Tooltip("비우면 자식에서 CategoryTabBar를 찾습니다. 자식 순서: 전체→전쟁→속보→소문→본영 (= WorldNewsFeedKind 0~4)")]
    [SerializeField] RectTransform categoryTabBarRoot;
    [SerializeField] int maxRows = 40;
    [Tooltip("0=전체, 1~4 = WorldNewsFeedKind (탭과 동기화)")]
    [SerializeField] int listFilter;

    readonly List<GameObject> _rows = new List<GameObject>();
    readonly List<Button> _categoryButtons = new List<Button>();
    readonly List<Image> _categoryTabImages = new List<Image>();
    readonly List<Color> _categoryTabBaseColors = new List<Color>();
    ScrollRect _listScroll;
    bool _categoryTabsWired;

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
    }

    void OnNewsAddedHandler(WorldNewsItem _) => RebuildList();

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
        if (detailPopup == null && transform.parent != null)
            detailPopup = transform.parent.GetComponentInChildren<NewsDetailPopup>(true);
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
        h.childControlWidth = true;
        h.childForceExpandWidth = true;
        h.childControlHeight = true;
        h.childForceExpandHeight = true;
        h.spacing = Mathf.Max(6f, h.spacing);
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
            if (le != null)
            {
                le.flexibleWidth = 1f;
                le.minWidth = Mathf.Min(le.minWidth > 0 ? le.minWidth : 88f, 88f);
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
        sorted.Sort((a, b) => b.unixTime.CompareTo(a.unixTime));

        int shown = 0;
        for (int i = 0; i < sorted.Count && shown < maxRows; i++)
        {
            var item = sorted[i];
            if (!PassesFilter(item, dm))
                continue;

            var row = Instantiate(rowTemplate, listContent);
            row.name = "NewsRow_" + shown;
            row.SetActive(true);
            BindRow(row, item);
            _rows.Add(row);
            shown++;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
        UpdateCategoryTabVisuals();
    }

    void BindRow(GameObject row, WorldNewsItem item)
    {
        var headline = FindTmpUnder(row.transform, "Headline");
        if (headline != null)
            headline.text = item.GetEffectiveDetailTitle();

        var summary = FindTmpUnder(row.transform, "Summary");
        if (summary != null)
        {
            string s = item.GetEffectiveSummaryForList();
            summary.text = string.IsNullOrEmpty(s) ? item.text : s;
        }

        var debuff = FindTmpUnder(row.transform, "DebuffHint");
        if (debuff != null)
            debuff.text = item.debuffIconsHint ?? "";

        void OpenDetail()
        {
            if (detailPopup != null)
                detailPopup.Show(item);
        }

        var detailBtnGo = FindDeep(row.transform, "DetailButton");
        if (detailBtnGo != null)
        {
            var detailBtn = detailBtnGo.GetComponent<Button>();
            if (detailBtn != null)
            {
                detailBtn.onClick.RemoveAllListeners();
                detailBtn.onClick.AddListener(OpenDetail);
            }
        }

        var btn = row.GetComponent<Button>();
        if (btn == null)
        {
            btn = row.AddComponent<Button>();
            btn.targetGraphic = row.GetComponent<Image>() ?? row.GetComponentInChildren<Graphic>(true);
        }

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OpenDetail);
    }

    /// <summary>0=전체, 1=전쟁, 2=속보(팩트), 3=소문(비시스템 전체), 4=본영(본성 연관 + 속보)</summary>
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
                return ItemHasWarTag(item);
            case (int)WorldNewsFeedKind.Breaking:
                return ItemIsFactBreaking(item);
            case (int)WorldNewsFeedKind.Rumor:
                return !IsSystemNewsItem(item);
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

    static bool ItemHasWarTag(WorldNewsItem item)
    {
        return !string.IsNullOrEmpty(item.text) && item.text.Contains("[전쟁]");
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

    static bool ItemIsBreakingForHqFeed(WorldNewsItem item) => ItemIsFactBreaking(item);

    bool PassesHeadquartersTab(WorldNewsItem item, DataManager dm)
    {
        if (ItemIsBreakingForHqFeed(item))
            return true;
        if (dm == null || string.IsNullOrWhiteSpace(dm.HomeCastleId))
            return false;
        return ItemReferencesHomeCastle(item, dm.HomeCastleId.Trim(), dm);
    }

    static bool ItemReferencesHomeCastle(WorldNewsItem item, string homeCastleId, DataManager dm)
    {
        if (string.IsNullOrWhiteSpace(homeCastleId))
            return false;
        string id = homeCastleId.Trim();

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
            item.detailTitle ?? "",
            item.detailBody ?? "",
            item.detailSubline ?? "");
        if (hay.IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (dm != null)
        {
            string disp = dm.GetCastleDisplayName(id);
            if (!string.IsNullOrWhiteSpace(disp))
            {
                string d = disp.Trim();
                if (d.Length > 0 && hay.IndexOf(d, StringComparison.Ordinal) >= 0)
                    return true;
            }
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
