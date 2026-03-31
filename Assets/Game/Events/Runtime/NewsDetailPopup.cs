using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>NewsScene <c>NewsDetailOverlay</c> 상세 패널. 인스펙터 참조가 비어 있으면 이름으로 자동 탐색합니다.</summary>
public class NewsDetailPopup : MonoBehaviour
{
    [SerializeField] Button dimmerButton;
    [SerializeField] Button closeButton;
    [Tooltip("비우면 WorldEventCenter의 NewsTemplateSo를 사용합니다.")]
    [SerializeField] NewsTemplateSo newsTemplateOverride;
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
    [SerializeField] TextMeshProUGUI reporterScriptText;
    [SerializeField] GameObject reporterSection;
    [SerializeField] TextMeshProUGUI trustLineText;

    bool _wired;

    void Awake() => ResolveRefs();

    public void ResolveRefs()
    {
        if (dimmerButton == null)
            dimmerButton = FindDeep(transform, "Dimmer")?.GetComponent<Button>();
        if (closeButton == null)
            closeButton = FindDeep(transform, "CloseButton")?.GetComponent<Button>();

        Transform popup = FindDeep(transform, "PopupRoot") ?? transform;
        if (headerTitleText == null)
            headerTitleText = FindTmpUnder(popup, "Title");
        if (headlineText == null)
            headlineText = FindTmpUnder(transform, "Headline");
        if (sublineText == null)
            sublineText = FindTmpUnder(transform, "Subline") ?? FindTmpUnder(transform, "Summary");
        if (bodyText == null)
            bodyText = FindTmpUnder(transform, "Body");

        if (heroImage == null)
            heroImage = FindDeep(transform, "HeroImage")?.GetComponent<Image>();

        if (reportSection == null)
        {
            var t = FindDeep(transform, "DetailSectionLabel") ?? FindDeep(transform, "ReportSection");
            if (t != null) reportSection = t.parent != null ? t.parent.gameObject : t.gameObject;
        }

        if (impactRangeText == null)
            impactRangeText = FindTmpUnder(transform, "ImpactRange") ?? FindTmpUnder(transform, "ImpactRangeText");
        if (debuffHintText == null)
            debuffHintText = FindDeep(transform, "DebuffHint")?.GetComponentInChildren<TextMeshProUGUI>(true);
        if (statLine1Text == null)
            statLine1Text = FindTmpUnder(transform, "StatLine1");
        if (statLine2Text == null)
            statLine2Text = FindTmpUnder(transform, "StatLine2");
        if (durationText == null)
            durationText = FindTmpUnder(transform, "Duration") ?? FindTmpUnder(transform, "DurationText");

        if (castleButtonStrip == null)
        {
            var go = FindDeep(transform, "CastleButtonStrip");
            if (go != null) castleButtonStrip = go as RectTransform;
        }

        if (reporterScriptText == null)
            reporterScriptText = FindTmpUnder(transform, "ReporterScript");
        if (trustLineText == null)
            trustLineText = FindTmpUnder(transform, "TrustLine");
        if (reporterSection == null)
        {
            var rs = FindDeep(transform, "ReporterSection");
            if (rs != null) reporterSection = rs.gameObject;
        }

        WireButtonsOnce();
    }

    void WireButtonsOnce()
    {
        if (_wired) return;
        _wired = true;
        if (dimmerButton != null)
            dimmerButton.onClick.AddListener(Hide);
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);
    }

    public void Show(WorldNewsItem item)
    {
        if (item == null) return;
        ResolveRefs();
        gameObject.SetActive(true);

        var dm = DataManager.InstanceOrNull;
        string cid = (item.targetCastleId ?? "").Trim();
        string castleDisp = dm != null && !string.IsNullOrEmpty(cid) ? dm.GetCastleDisplayName(cid) : cid;
        if (string.IsNullOrWhiteSpace(castleDisp)) castleDisp = cid;

        string title = NewsTemplateSo.GetFormattedContent(item.GetEffectiveDetailTitle(), cid, dm);
        string body = NewsTemplateSo.GetFormattedContent(item.GetEffectiveDetailBody(), cid, dm);
        if (headerTitleText != null) headerTitleText.text = title;
        if (headlineText != null) headlineText.text = title;
        if (sublineText != null)
            sublineText.text = string.IsNullOrWhiteSpace(item.detailSubline)
                ? FormatSubline(item)
                : NewsTemplateSo.GetFormattedContent(item.detailSubline.Trim(), cid, dm);
        if (bodyText != null) bodyText.text = body;

        var templates = newsTemplateOverride != null ? newsTemplateOverride : WorldEventCenter.InstanceOrNull?.NewsTemplates;
        NewsTemplateEntry te = null;
        bool hasTpl = templates != null && !string.IsNullOrWhiteSpace(item.eventId)
                      && templates.TryGet(item.eventId, out te) && te != null;
        bool debunked = item.isDebunked;

        if (reporterSection != null)
            reporterSection.SetActive(hasTpl && !debunked);
        if (reporterScriptText != null)
            reporterScriptText.text = !debunked && hasTpl && !string.IsNullOrWhiteSpace(te.reporterScript)
                ? NewsTemplateSo.GetFormattedContent(te.reporterScript.Trim(), cid, dm)
                : "";

        if (heroImage != null)
        {
            if (!debunked && hasTpl && te.reporterIcon != null)
            {
                heroImage.sprite = te.reporterIcon;
                heroImage.gameObject.SetActive(true);
            }
            else
                heroImage.gameObject.SetActive(false);
        }

        if (trustLineText != null)
        {
            if (debunked)
                trustLineText.text = "소문은 허위로 판명되었습니다.";
            else if (TryGetHomeGovernorIntel(dm, out int intel) && intel >= 90)
            {
                int trust = Mathf.Clamp(55 + intel / 2, 70, 99);
                trustLineText.text = $"신뢰도: {trust}%";
            }
            else
                trustLineText.text = "정보 불분명";
        }

        bool hasReport = !string.IsNullOrWhiteSpace(item.statLine1)
                         || !string.IsNullOrWhiteSpace(item.statLine2)
                         || !string.IsNullOrWhiteSpace(item.durationText)
                         || !string.IsNullOrWhiteSpace(item.impactRangeText)
                         || !string.IsNullOrWhiteSpace(item.debuffIconsHint);
        if (reportSection != null)
            reportSection.SetActive(hasReport);

        if (impactRangeText != null)
            impactRangeText.text = item.impactRangeText ?? "";
        if (debuffHintText != null)
            debuffHintText.text = item.debuffIconsHint ?? "";
        if (statLine1Text != null)
            statLine1Text.text = item.statLine1 ?? "";
        if (statLine2Text != null)
            statLine2Text.text = item.statLine2 ?? "";
        if (durationText != null)
            durationText.text = item.durationText ?? "";

        if (castleButtonStrip != null)
            castleButtonStrip.gameObject.SetActive(false);
    }

    public void Hide() => gameObject.SetActive(false);

    static bool TryGetHomeGovernorIntel(DataManager dm, out int intel)
    {
        intel = 0;
        if (dm == null || string.IsNullOrWhiteSpace(dm.HomeCastleId)) return false;
        if (!dm.castleStateDataMap.TryGetValue(dm.HomeCastleId.Trim(), out var st) || st == null)
            return false;
        if (string.IsNullOrWhiteSpace(st.currentGovernorId)) return false;
        var g = dm.GetGeneralMasterData(st.currentGovernorId);
        if (g == null) return false;
        intel = g.intel;
        return true;
    }

    static string FormatSubline(WorldNewsItem item)
    {
        long now = TimeManager.GetUnixNow();
        long dt = now - item.unixTime;
        string rel = dt < 60 ? "방금 전"
            : dt < 3600 ? $"{dt / 60}분 전"
            : dt < 86400 ? $"{dt / 3600}시간 전"
            : $"{dt / 86400}일 전";
        if (!string.IsNullOrWhiteSpace(item.relatedCastleIdsRaw))
            return $"{rel} · 관련: {item.relatedCastleIdsRaw.Trim()}";
        return rel;
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
