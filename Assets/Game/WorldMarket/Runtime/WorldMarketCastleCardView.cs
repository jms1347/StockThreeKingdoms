using System.Text;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>천하 탭 성 카드 1개 — 종목 시세판 스타일 바인딩.</summary>
public class WorldMarketCastleCardView : MonoBehaviour
{
    static readonly Color RiseColor = new Color(0.95f, 0.28f, 0.28f, 1f);
    static readonly Color FallColor = new Color(0.32f, 0.52f, 0.95f, 1f);
    static readonly Color BuyBoxUp = new Color(0.20f, 0.14f, 0.14f, 0.96f);
    static readonly Color BuyBoxDown = new Color(0.14f, 0.16f, 0.24f, 0.96f);
    static readonly Color InvestOutline = new Color(1f, 0.82f, 0.35f, 0.92f);

    [Header("텍스트 (비우면 자식 경로로 탐색)")]
    [SerializeField] TextMeshProUGUI gradeBadgeText;
    [SerializeField] TextMeshProUGUI castleNameText;
    [SerializeField] TextMeshProUGUI castleIdText;
    [SerializeField] TextMeshProUGUI buyLabelText;
    [SerializeField] TextMeshProUGUI buyPriceText;
    [SerializeField] TextMeshProUGUI sentimentArrowText;
    [SerializeField] TextMeshProUGUI sentimentChangeText;
    [SerializeField] TextMeshProUGUI troopsText;
    [SerializeField] TextMeshProUGUI roiText;
    [SerializeField] TextMeshProUGUI stakeText;

    [Header("가격 표시")]
    [SerializeField, Min(18f)] float largeBuyPriceFontSize = 34f;

    [Header("이미지")]
    [SerializeField] Image cardBackgroundImage;
    [SerializeField] Image glossOverlayImage;
    [SerializeField] Image portraitImage;
    [SerializeField] Image buyPriceBackground;
    [SerializeField] TextMeshProUGUI portraitInitialText;

    [Header("레이아웃")]
    [SerializeField] RectTransform userInvestPanel;

    Outline _outline;
    Color _normalCardColor;
    bool _cachedColors;
    readonly StringBuilder _badgeSb = new StringBuilder(32);

    void Awake()
    {
        _outline = GetComponent<Outline>();
        TryAutoWire();
        CacheDefaultColors();
    }

    void OnDisable()
    {
        transform.DOKill(false);
    }

    void CacheDefaultColors()
    {
        if (_cachedColors) return;
        if (cardBackgroundImage == null)
            cardBackgroundImage = GetComponent<Image>();
        if (cardBackgroundImage != null)
            _normalCardColor = cardBackgroundImage.color;
        _cachedColors = cardBackgroundImage != null;
    }

    void TryAutoWire()
    {
        if (gradeBadgeText == null) gradeBadgeText = transform.Find("Left/GradeBadge")?.GetComponent<TextMeshProUGUI>();
        if (castleNameText == null) castleNameText = transform.Find("Left/CastleName")?.GetComponent<TextMeshProUGUI>();
        if (castleIdText == null) castleIdText = transform.Find("Left/CastleIdLine")?.GetComponent<TextMeshProUGUI>();
        if (buyLabelText == null) buyLabelText = transform.Find("Left/BuyRow/BuyLabel")?.GetComponent<TextMeshProUGUI>();
        if (buyPriceText == null) buyPriceText = transform.Find("Left/BuyRow/BuyPriceBg/BuyPrice")?.GetComponent<TextMeshProUGUI>();
        if (buyPriceBackground == null) buyPriceBackground = transform.Find("Left/BuyRow/BuyPriceBg")?.GetComponent<Image>();
        if (sentimentArrowText == null) sentimentArrowText = transform.Find("Left/SentRow/Arrow")?.GetComponent<TextMeshProUGUI>();
        if (sentimentChangeText == null) sentimentChangeText = transform.Find("Left/SentRow/ChangePct")?.GetComponent<TextMeshProUGUI>();
        if (troopsText == null) troopsText = transform.Find("Left/InvestRow/TroopsLine")?.GetComponent<TextMeshProUGUI>();
        if (roiText == null) roiText = transform.Find("Left/InvestRow/RoiLine")?.GetComponent<TextMeshProUGUI>();
        if (stakeText == null) stakeText = transform.Find("Left/InvestRow/StakeLine")?.GetComponent<TextMeshProUGUI>();
        if (portraitImage == null) portraitImage = transform.Find("Governor/Portrait")?.GetComponent<Image>();
        if (portraitInitialText == null) portraitInitialText = transform.Find("Governor/PortraitInitial")?.GetComponent<TextMeshProUGUI>();
        if (userInvestPanel == null)
        {
            var inv = transform.Find("Left/InvestRow");
            if (inv != null) userInvestPanel = inv as RectTransform;
        }

        if (glossOverlayImage == null)
        {
            var g = transform.Find("GlossOverlay");
            if (g != null) glossOverlayImage = g.GetComponent<Image>();
        }

        if (cardBackgroundImage == null)
            cardBackgroundImage = GetComponent<Image>();
    }

    public void Bind(string castleId)
    {
        transform.DOKill(false);
        TryAutoWire();
        CacheDefaultColors();

        var dm = DataManager.InstanceOrNull;
        if (dm == null || string.IsNullOrWhiteSpace(castleId)) return;

        castleId = castleId.Trim();
        if (!dm.castleStateDataMap.TryGetValue(castleId, out var st) || st == null) return;

        dm.castleMasterDataMap.TryGetValue(castleId, out var master);

        string dispName = master != null && !string.IsNullOrWhiteSpace(master.name) ? master.name : castleId;
        Grade g = master?.grade ?? Grade.D;

        _badgeSb.Clear();
        if (st.isWar) _badgeSb.Append("<color=#ff6b6b>[전쟁]</color> ");
        if (st.isDisaster) _badgeSb.Append("<color=#ffb347>[재해]</color> ");
        if (castleNameText != null)
        {
            castleNameText.richText = true;
            castleNameText.text = $"{_badgeSb}{dispName} <size=85%>({castleId})</size>";
        }

        if (castleIdText != null) castleIdText.text = master?.region ?? "";

        if (gradeBadgeText != null)
        {
            gradeBadgeText.text = g.ToString();
            gradeBadgeText.color = GradeAccentColor(g);
        }

        if (buyLabelText != null) buyLabelText.text = "매수가";

        float buy = st.currentBuyPrice;
        if (buyPriceText != null)
        {
            buyPriceText.fontSize = largeBuyPriceFontSize;
            buyPriceText.text = $"{buy:N0}";
        }

        bool hasPct = TryComputeSentimentPercentChange(st, out float pctChg);
        bool up = pctChg > 0f;
        bool flat = !hasPct || Mathf.Abs(pctChg) < 0.0005f;

        if (sentimentArrowText != null)
        {
            sentimentArrowText.text = flat ? "—" : (up ? "▲" : "▼");
            sentimentArrowText.color = flat ? new Color(0.55f, 0.58f, 0.64f) : (up ? RiseColor : FallColor);
        }

        if (sentimentChangeText != null)
        {
            if (!hasPct)
                sentimentChangeText.text = "심리 —";
            else
                sentimentChangeText.text = $"{(up ? "+" : "")}{pctChg:F2}%";
            sentimentChangeText.color = flat ? new Color(0.65f, 0.68f, 0.74f) : (up ? RiseColor : FallColor);
        }

        if (buyPriceBackground != null)
            buyPriceBackground.color = flat ? new Color(0.16f, 0.17f, 0.20f, 0.95f) : (up ? BuyBoxUp : BuyBoxDown);

        bool invested = st.IsUserInvested;
        if (userInvestPanel != null)
            userInvestPanel.gameObject.SetActive(invested);

        int maxG = master?.maxGarrison ?? 0;
        float stake = maxG > 0 ? Mathf.Clamp01(st.userDeployedTroops / (float)maxG) * 100f : 0f;

        if (troopsText != null)
            troopsText.text = invested ? $"내 병력 {st.userDeployedTroops:N0}명" : "";

        if (stakeText != null)
            stakeText.text = invested && maxG > 0 ? $"지분(주둔 대비) {stake:F1}%" : "";

        if (roiText != null)
        {
            if (invested && st.averagePurchasePrice > 0.0001f)
            {
                float roi = (st.currentBuyPrice - st.averagePurchasePrice) / st.averagePurchasePrice * 100f;
                roiText.text = $"수익률 {(roi >= 0 ? "+" : "")}{roi:F1}%";
                roiText.color = roi >= 0f ? RiseColor : FallColor;
            }
            else if (invested)
            {
                roiText.text = "수익률 —";
                roiText.color = new Color(0.7f, 0.72f, 0.78f, 1f);
            }
            else
            {
                roiText.text = "";
            }
        }

        BindPortrait(dm, st, dispName);

        ApplyCardChrome(st, invested);
    }

    static bool TryComputeSentimentPercentChange(CastleStateData st, out float pct)
    {
        pct = 0f;
        var h = st.sentimentHistory;
        if (h == null || h.Count < 2) return false;
        float prev = h[h.Count - 2];
        float last = h[h.Count - 1];
        if (Mathf.Abs(prev) < 0.01f)
            return false;
        pct = (last - prev) / Mathf.Abs(prev) * 100f;
        return true;
    }

    void BindPortrait(DataManager dm, CastleStateData st, string castleDisplayName)
    {
        string gid = st.currentGovernorId;
        GeneralMasterData gen = null;
        if (!string.IsNullOrWhiteSpace(gid))
            gen = dm.GetGeneralMasterData(gid);

        if (portraitImage != null)
        {
            portraitImage.sprite = null;
            portraitImage.color = new Color(0.15f, 0.16f, 0.20f, 1f);
        }

        if (portraitInitialText != null)
        {
            string source;
            if (gen != null && !string.IsNullOrWhiteSpace(gen.name))
                source = gen.name.Trim();
            else if (!string.IsNullOrWhiteSpace(castleDisplayName))
                source = castleDisplayName.Trim();
            else
                source = "?";

            portraitInitialText.text = FirstChar(source);
            portraitInitialText.color = new Color(0.85f, 0.88f, 0.93f, 1f);
            portraitInitialText.gameObject.SetActive(true);
        }
    }

    static string FirstChar(string s)
    {
        if (string.IsNullOrEmpty(s)) return "?";
        return s.Substring(0, 1);
    }

    void ApplyCardChrome(CastleStateData st, bool invested)
    {
        bool war = st.isWar;

        if (glossOverlayImage != null)
        {
            bool glossOn = invested && !war;
            glossOverlayImage.gameObject.SetActive(glossOn);
            if (glossOn)
                glossOverlayImage.color = new Color(1f, 0.94f, 0.78f, 0.09f);
        }

        if (cardBackgroundImage != null)
        {
            if (invested && !war)
                cardBackgroundImage.color = new Color(0.14f, 0.13f, 0.11f, 0.99f);
            else
                cardBackgroundImage.color = _normalCardColor;
        }

        if (_outline == null) return;

        if (war)
        {
            _outline.enabled = true;
            _outline.effectDistance = new Vector2(3.5f, -3.5f);
            var bright = new Color(1f, 0.2f, 0.22f, 0.95f);
            var dim = new Color(0.5f, 0.06f, 0.08f, 0.35f);
            _outline.effectColor = bright;
            var seq = DOTween.Sequence();
            seq.Append(DOTween.To(() => _outline.effectColor, c => _outline.effectColor = c, dim, 0.48f).SetEase(Ease.InOutSine));
            seq.Append(DOTween.To(() => _outline.effectColor, c => _outline.effectColor = c, bright, 0.48f).SetEase(Ease.InOutSine));
            seq.SetLoops(-1);
            seq.SetTarget(gameObject);
        }
        else if (invested)
        {
            _outline.enabled = true;
            _outline.effectColor = InvestOutline;
            _outline.effectDistance = new Vector2(3f, -3f);
        }
        else
        {
            _outline.enabled = false;
        }
    }

    static Color GradeAccentColor(Grade grade)
    {
        switch (grade)
        {
            case Grade.SS: return new Color(1f, 0.82f, 0.35f, 1f);
            case Grade.S: return new Color(0.95f, 0.55f, 0.30f, 1f);
            case Grade.A: return new Color(0.78f, 0.80f, 0.85f, 1f);
            case Grade.B: return new Color(0.60f, 0.72f, 0.95f, 1f);
            default: return new Color(0.55f, 0.58f, 0.64f, 1f);
        }
    }
}
