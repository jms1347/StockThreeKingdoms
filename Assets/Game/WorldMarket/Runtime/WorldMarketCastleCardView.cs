using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>천하 탭 성 카드 1개 바인딩 (다크 MTS 스타일).</summary>
public class WorldMarketCastleCardView : MonoBehaviour
{
    static readonly Color RiseColor = new Color(0.95f, 0.35f, 0.35f, 1f);
    static readonly Color FallColor = new Color(0.38f, 0.58f, 0.95f, 1f);
    static readonly Color BuyBoxUp = new Color(0.18f, 0.42f, 0.28f, 0.95f);
    static readonly Color BuyBoxDown = new Color(0.20f, 0.32f, 0.52f, 0.95f);

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

    [Header("이미지")]
    [SerializeField] Image portraitImage;
    [SerializeField] Image buyPriceBackground;
    [SerializeField] TextMeshProUGUI portraitInitialText;

    Outline _outline;

    void Awake()
    {
        _outline = GetComponent<Outline>();
        TryAutoWire();
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
    }

    public void Bind(string castleId)
    {
        TryAutoWire();
        var dm = DataManager.InstanceOrNull;
        if (dm == null || string.IsNullOrWhiteSpace(castleId)) return;

        castleId = castleId.Trim();
        if (!dm.castleStateDataMap.TryGetValue(castleId, out var st) || st == null) return;

        dm.castleMasterDataMap.TryGetValue(castleId, out var master);

        string dispName = master != null && !string.IsNullOrWhiteSpace(master.name) ? master.name : castleId;
        Grade g = master?.grade ?? Grade.D;
        if (castleNameText != null) castleNameText.text = $"{dispName} ({castleId})";
        if (castleIdText != null) castleIdText.text = master?.region ?? "";

        if (gradeBadgeText != null)
        {
            gradeBadgeText.text = g.ToString();
            gradeBadgeText.color = GradeAccentColor(g);
        }

        if (buyLabelText != null) buyLabelText.text = "매수";

        float buy = st.currentBuyPrice;
        if (buyPriceText != null) buyPriceText.text = $"{buy:N0}";

        float sentDelta = ComputeSentimentDelta(st);
        bool up = sentDelta >= 0f;
        if (sentimentArrowText != null)
        {
            sentimentArrowText.text = Mathf.Approximately(sentDelta, 0f) ? "—" : (up ? "▲" : "▼");
            sentimentArrowText.color = Mathf.Approximately(sentDelta, 0f) ? Color.gray : (up ? RiseColor : FallColor);
        }

        if (sentimentChangeText != null)
        {
            sentimentChangeText.text = $"심리 {sentDelta:+0.0;-0.0;0} pts";
            sentimentChangeText.color = Mathf.Approximately(sentDelta, 0f) ? Color.gray : (up ? RiseColor : FallColor);
        }

        if (buyPriceBackground != null)
            buyPriceBackground.color = up ? BuyBoxUp : BuyBoxDown;

        int maxG = master?.maxGarrison ?? 0;
        float stake = maxG > 0 ? Mathf.Clamp01(st.userDeployedTroops / (float)maxG) * 100f : 0f;

        if (troopsText != null)
            troopsText.text = st.IsUserInvested ? $"내 병력: {st.userDeployedTroops:N0}명" : "내 병력: —";

        if (stakeText != null)
            stakeText.text = maxG > 0 ? $"지분(주둔 대비): {stake:F1}%" : "";

        if (roiText != null)
        {
            if (st.IsUserInvested && st.averagePurchasePrice > 0.0001f)
            {
                float roi = (st.currentBuyPrice - st.averagePurchasePrice) / st.averagePurchasePrice * 100f;
                roiText.text = $"수익률: {(roi >= 0 ? "+" : "")}{roi:F1}%";
                roiText.color = roi >= 0f ? RiseColor : FallColor;
            }
            else
            {
                roiText.text = "수익률: —";
                roiText.color = new Color(0.7f, 0.72f, 0.78f, 1f);
            }
        }

        BindPortrait(dm, st);

        if (_outline != null)
            _outline.enabled = st.IsUserInvested;
    }

    static float ComputeSentimentDelta(CastleStateData st)
    {
        var h = st.sentimentHistory;
        if (h == null || h.Count < 2) return 0f;
        return h[h.Count - 1] - h[h.Count - 2];
    }

    void BindPortrait(DataManager dm, CastleStateData st)
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
            string name = gen != null && !string.IsNullOrWhiteSpace(gen.name) ? gen.name : "무";
            portraitInitialText.text = name.Length > 0 ? name.Substring(0, 1) : "?";
            portraitInitialText.color = new Color(0.85f, 0.88f, 0.93f, 1f);
        }
    }

    static Color GradeAccentColor(Grade g)
    {
        switch (g)
        {
            case Grade.SS: return new Color(1f, 0.82f, 0.35f, 1f);
            case Grade.S: return new Color(0.95f, 0.55f, 0.30f, 1f);
            case Grade.A: return new Color(0.78f, 0.80f, 0.85f, 1f);
            case Grade.B: return new Color(0.60f, 0.72f, 0.95f, 1f);
            default: return new Color(0.55f, 0.58f, 0.64f, 1f);
        }
    }
}
