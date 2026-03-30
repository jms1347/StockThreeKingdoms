using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>NewsScene의 뉴스 리스트를 <see cref="DataManager.worldNews"/>와 동기화하고 상세 팝업을 연결합니다.</summary>
public class NewsSceneFeedController : MonoBehaviour
{
    public static Action<string> RequestOpenCastle;
    public static Action RequestFocusWorldMarket;

    [SerializeField] NewsDetailPopup detailPopup;
    [SerializeField] Transform listContent;
    [SerializeField] GameObject rowTemplate;
    [SerializeField] int maxRows = 40;

    DataManager _dm;
    readonly List<GameObject> _spawnedRows = new List<GameObject>();

    void OnEnable()
    {
        _dm = DataManager.InstanceOrNull;
        if (_dm != null)
            _dm.OnNewsAdded += OnNewsAdded;
        RefreshList();
    }

    void OnDisable()
    {
        if (_dm != null)
            _dm.OnNewsAdded -= OnNewsAdded;
    }

    void OnNewsAdded(WorldNewsItem _) => RefreshList();

    void Awake()
    {
        AutoWireIfNeeded();
    }

    void AutoWireIfNeeded()
    {
        if (listContent == null)
        {
            var t = transform.Find("NewsScrollHolder/NewsScroll/Viewport/Content");
            if (t != null) listContent = t;
        }
        if (rowTemplate == null && listContent != null)
        {
            var tr = listContent.Find("NewsListRowTemplate");
            if (tr != null) rowTemplate = tr.gameObject;
        }
        if (detailPopup == null && transform.parent != null)
        {
            var ov = transform.parent.Find("NewsDetailOverlay");
            if (ov != null)
                detailPopup = ov.GetComponent<NewsDetailPopup>();
        }
    }

    public void RefreshList()
    {
        AutoWireIfNeeded();
        if (listContent == null || rowTemplate == null) return;

        foreach (var go in _spawnedRows)
        {
            if (go != null)
                Destroy(go);
        }
        _spawnedRows.Clear();

        _dm = DataManager.InstanceOrNull;
        if (_dm?.worldNews == null || _dm.worldNews.Count == 0) return;

        int start = Mathf.Max(0, _dm.worldNews.Count - maxRows);
        for (int i = _dm.worldNews.Count - 1; i >= start; i--)
        {
            var item = _dm.worldNews[i];
            if (item == null) continue;
            var row = Instantiate(rowTemplate, listContent);
            row.name = $"NewsRow_{i}";
            row.SetActive(true);
            _spawnedRows.Add(row);
            BindRow(row, item);
        }
    }

    void BindRow(GameObject row, WorldNewsItem item)
    {
        var title = row.transform.Find("RightColumn/TitleRow/Title")?.GetComponent<TextMeshProUGUI>();
        if (title != null)
            title.text = item.GetEffectiveDetailTitle();

        var time = row.transform.Find("RightColumn/TitleRow/TimeAgo")?.GetComponent<TextMeshProUGUI>();
        if (time != null)
            time.text = FormatRelativeTime(item.unixTime);

        var summary = row.transform.Find("RightColumn/Summary")?.GetComponent<TextMeshProUGUI>();
        if (summary != null)
        {
            string s = item.GetEffectiveSummaryForList();
            if (string.IsNullOrWhiteSpace(s))
                s = item.GetEffectiveDetailBody();
            summary.text = s;
        }

        var detailBtn = row.transform.Find("RightColumn/FooterRow/DetailButton")?.GetComponent<Button>();
        if (detailBtn != null)
        {
            detailBtn.onClick.RemoveAllListeners();
            var captured = item;
            detailBtn.onClick.AddListener(() =>
            {
                if (detailPopup != null)
                    detailPopup.Show(captured, _dm);
            });
        }
    }

    static string FormatRelativeTime(long unix)
    {
        long now = TimeManager.GetUnixNow();
        long d = Math.Max(0, now - unix);
        if (d < 60) return "방금 전";
        if (d < 3600) return $"{d / 60}분 전";
        if (d < 86400) return $"{d / 3600}시간 전";
        return $"{d / 86400}일 전";
    }
}
