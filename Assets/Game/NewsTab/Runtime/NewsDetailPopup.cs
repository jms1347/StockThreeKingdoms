using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>뉴스 탭 상세보기 모달 — <see cref="WorldNewsItem"/> 바인딩.</summary>
public class NewsDetailPopup : MonoBehaviour
{
    [SerializeField] GameObject root;
    [SerializeField] Button dimmerButton;
    [SerializeField] Button closeButton;
    [SerializeField] TextMeshProUGUI headerTitleText;
    [SerializeField] TextMeshProUGUI headlineText;
    [SerializeField] TextMeshProUGUI sublineText;
    [SerializeField] Image heroImage;
    [SerializeField] TextMeshProUGUI bodyText;
    [SerializeField] GameObject reportSection;
    [SerializeField] TextMeshProUGUI impactRangeText;
    [SerializeField] TextMeshProUGUI debuffHintText;
    [SerializeField] TextMeshProUGUI statLine1Text;
    [SerializeField] TextMeshProUGUI statLine2Text;
    [SerializeField] TextMeshProUGUI durationText;
    [SerializeField] RectTransform castleButtonStrip;
    [SerializeField] GameObject castleButtonPrefab;

    static readonly Regex CastleIdInParens = new Regex(@"\(([Cc]\d+)\)", RegexOptions.Compiled);

    void Awake()
    {
        if (root == null)
            root = gameObject;
        TryBindHierarchy();
        if (dimmerButton != null)
            dimmerButton.onClick.AddListener(Hide);
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);
    }

    void TryBindHierarchy()
    {
        if (headlineText != null) return;

        Transform content = transform.Find("PopupRoot/ScrollView/Viewport/Content");
        if (content == null) return;

        dimmerButton = transform.Find("Dimmer")?.GetComponent<Button>();
        headerTitleText = content.Find("HeaderTitle")?.GetComponent<TextMeshProUGUI>();
        headlineText = content.Find("HeadlineRow/Headline")?.GetComponent<TextMeshProUGUI>();
        sublineText = content.Find("Subline")?.GetComponent<TextMeshProUGUI>();
        heroImage = content.Find("HeroImage")?.GetComponent<Image>();
        bodyText = content.Find("Body")?.GetComponent<TextMeshProUGUI>();
        reportSection = content.Find("ReportBlock")?.gameObject;
        impactRangeText = content.Find("ReportBlock/ImpactRange")?.GetComponent<TextMeshProUGUI>();
        debuffHintText = content.Find("ReportBlock/DebuffHint")?.GetComponent<TextMeshProUGUI>();
        statLine1Text = content.Find("ReportBlock/StatLine1")?.GetComponent<TextMeshProUGUI>();
        statLine2Text = content.Find("ReportBlock/StatLine2")?.GetComponent<TextMeshProUGUI>();
        durationText = content.Find("ReportBlock/Duration")?.GetComponent<TextMeshProUGUI>();
        castleButtonStrip = content.Find("CastleButtonStrip") as RectTransform;
        closeButton = content.Find("CloseButton")?.GetComponent<Button>();
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);
    }

    public void Show(WorldNewsItem item, DataManager dm)
    {
        if (item == null || root == null) return;

        if (headerTitleText != null)
            headerTitleText.text = "속보";

        if (headlineText != null)
            headlineText.text = item.GetEffectiveDetailTitle();

        if (sublineText != null)
            sublineText.text = BuildSubline(item, dm);

        if (bodyText != null)
            bodyText.text = item.GetEffectiveDetailBody();

        if (heroImage != null)
            heroImage.color = GuessHeroTint(item);

        bool hasReport = !string.IsNullOrWhiteSpace(item.impactRangeText) ||
                         !string.IsNullOrWhiteSpace(item.debuffIconsHint) ||
                         !string.IsNullOrWhiteSpace(item.statLine1) ||
                         !string.IsNullOrWhiteSpace(item.statLine2) ||
                         !string.IsNullOrWhiteSpace(item.durationText);
        if (reportSection != null)
            reportSection.SetActive(hasReport);

        if (impactRangeText != null)
            impactRangeText.text = string.IsNullOrWhiteSpace(item.impactRangeText) ? "—" : item.impactRangeText.Trim();
        if (debuffHintText != null)
            debuffHintText.text = string.IsNullOrWhiteSpace(item.debuffIconsHint) ? "—" : item.debuffIconsHint.Trim();
        if (statLine1Text != null)
            statLine1Text.text = string.IsNullOrWhiteSpace(item.statLine1) ? "" : item.statLine1.Trim();
        if (statLine2Text != null)
            statLine2Text.text = string.IsNullOrWhiteSpace(item.statLine2) ? "" : item.statLine2.Trim();
        if (durationText != null)
            durationText.text = string.IsNullOrWhiteSpace(item.durationText) ? "" : item.durationText.Trim();

        if (statLine1Text != null)
            statLine1Text.gameObject.SetActive(!string.IsNullOrWhiteSpace(statLine1Text.text));
        if (statLine2Text != null)
            statLine2Text.gameObject.SetActive(!string.IsNullOrWhiteSpace(statLine2Text.text));
        if (durationText != null)
            durationText.gameObject.SetActive(!string.IsNullOrWhiteSpace(durationText.text));

        RebuildCastleButtons(item, dm);

        root.SetActive(true);
        transform.SetAsLastSibling();
    }

    static string BuildSubline(WorldNewsItem item, DataManager dm)
    {
        if (!string.IsNullOrWhiteSpace(item.detailSubline))
            return item.detailSubline.Trim();

        string rel = FormatRelativeTime(item.unixTime);
        var ids = CollectCastleIds(item);
        if (ids.Count == 0)
            return rel;

        var parts = new List<string>();
        foreach (var id in ids)
        {
            string name = ResolveCastleLabel(dm, id);
            parts.Add($"{name}({id})");
        }
        return $"{rel} · 관련 성: {string.Join(", ", parts)}";
    }

    static List<string> CollectCastleIds(WorldNewsItem item)
    {
        var set = new HashSet<string>();
        if (!string.IsNullOrWhiteSpace(item.relatedCastleIdsRaw))
        {
            foreach (var p in item.relatedCastleIdsRaw.Split(','))
            {
                var t = p.Trim();
                if (t.Length > 0)
                    set.Add(t.ToUpperInvariant());
            }
        }
        string blob = (item.text ?? "") + " " + (item.detailBody ?? "") + " " + (item.detailTitle ?? "");
        foreach (Match m in CastleIdInParens.Matches(blob))
            set.Add(m.Groups[1].Value.ToUpperInvariant());
        return new List<string>(set);
    }

    void RebuildCastleButtons(WorldNewsItem item, DataManager dm)
    {
        if (castleButtonStrip == null) return;
        for (int i = castleButtonStrip.childCount - 1; i >= 0; i--)
            Destroy(castleButtonStrip.GetChild(i).gameObject);

        var ids = CollectCastleIds(item);
        if (ids.Count == 0)
        {
            castleButtonStrip.gameObject.SetActive(false);
            return;
        }

        castleButtonStrip.gameObject.SetActive(true);

        foreach (var cid in ids)
        {
            CreateCastleJumpButton(cid, dm);
        }

        var allBtn = castleButtonPrefab != null
            ? Instantiate(castleButtonPrefab, castleButtonStrip)
            : CreateDefaultStripButton();
        if (allBtn != null)
        {
            var tmp = allBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.text = "모두 확인";
            var b = allBtn.GetComponent<Button>();
            if (b != null)
                b.onClick.AddListener(() =>
                {
                    Hide();
                    NewsSceneFeedController.RequestFocusWorldMarket?.Invoke();
                });
        }
    }

    GameObject CreateDefaultStripButton()
    {
        var go = new GameObject("CastleJumpBtn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(castleButtonStrip, false);
        var le = go.GetComponent<LayoutElement>();
        le.minHeight = 52f;
        le.preferredHeight = 52f;
        le.flexibleWidth = 1f;
        var img = go.GetComponent<Image>();
        img.color = new Color(0.28f, 0.22f, 0.18f, 1f);
        return go;
    }

    void CreateCastleJumpButton(string castleId, DataManager dm)
    {
        GameObject go = castleButtonPrefab != null
            ? Instantiate(castleButtonPrefab, castleButtonStrip)
            : CreateDefaultStripButton();
        string name = ResolveCastleLabel(dm, castleId);
        var tmp = go.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
            tmp.text = $"{name}({castleId}) 확인";
        var img = go.GetComponent<Image>();
        if (img != null && castleButtonPrefab == null)
            img.color = new Color(0.22f, 0.38f, 0.55f, 1f);

        var b = go.GetComponent<Button>();
        if (b != null)
        {
            string id = castleId;
            b.onClick.AddListener(() =>
            {
                Hide();
                NewsSceneFeedController.RequestOpenCastle?.Invoke(id);
            });
        }
    }

    static string ResolveCastleLabel(DataManager dm, string castleId)
    {
        if (dm == null) return castleId;
        string d = dm.GetCastleDisplayName(castleId);
        return string.IsNullOrWhiteSpace(d) ? castleId : d.Trim();
    }

    static Color GuessHeroTint(WorldNewsItem item)
    {
        string t = (item.text ?? "") + (item.detailTitle ?? "");
        if (t.Contains("[전쟁]") || t.Contains("[WAR]"))
            return new Color(0.45f, 0.22f, 0.2f, 1f);
        if (t.Contains("[재해]") || t.Contains("[DISASTER]"))
            return new Color(0.25f, 0.28f, 0.38f, 1f);
        if (t.Contains("[배당]") || t.Contains("[BOOM]"))
            return new Color(0.28f, 0.4f, 0.28f, 1f);
        return new Color(0.32f, 0.36f, 0.42f, 1f);
    }

    static string FormatRelativeTime(long unix)
    {
        long now = TimeManager.GetUnixNow();
        long d = now - unix;
        if (d < 60) return "방금 전";
        if (d < 3600) return $"{d / 60}분 전";
        if (d < 86400) return $"{d / 3600}시간 전";
        return $"{d / 86400}일 전";
    }
}
