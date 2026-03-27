using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>천하 탭 성 카드 — 4구역 MTS 전광판 (식별 / 시세·스파크라인 / 내투자 / 투입·회수).</summary>
public class WorldMarketCastleCardView : MonoBehaviour
{
    static readonly Color RiseColor = new Color(0.95f, 0.28f, 0.28f, 1f);
    static readonly Color FallColor = new Color(0.32f, 0.52f, 0.95f, 1f);
    static readonly Color RoiFallReadable = new Color(0.62f, 0.78f, 1f, 1f);
    static readonly Color PersonalGold = new Color(1f, 0.88f, 0.48f, 1f);
    static readonly Color PersonalGoldDim = new Color(0.85f, 0.78f, 0.55f, 1f);
    static readonly Color BuyBoxUp = new Color(0.20f, 0.14f, 0.14f, 0.96f);
    static readonly Color BuyBoxDown = new Color(0.14f, 0.16f, 0.24f, 0.96f);
    static readonly Color InvestOutline = new Color(1f, 0.82f, 0.35f, 0.92f);

    [Header("1구역 · 식별")]
    [SerializeField] TextMeshProUGUI gradeBadgeText;
    [SerializeField] TextMeshProUGUI castleNameText;
    [SerializeField] TextMeshProUGUI castleIdText;
    [SerializeField] Image gradeAccentBarImage;
    [SerializeField] GameObject statusIconWar;
    [SerializeField] GameObject statusIconDisaster;
    [SerializeField] GameObject statusIconFavorable;

    [Header("2구역 · 시세")]
    [SerializeField] TextMeshProUGUI buyLabelText;
    [SerializeField] TextMeshProUGUI buyPriceText;
    [SerializeField] TextMeshProUGUI sentimentArrowText;
    [SerializeField] TextMeshProUGUI sentimentChangeText;
    [SerializeField] UIMiniSparklineGraphic sparklineGraphic;

    [Header("3구역 · 내 투자 (비우면 MainRow/Zone3Personal 탐색)")]
    [SerializeField] RectTransform zone3PersonalRoot;
    [SerializeField] TextMeshProUGUI roiText;
    [SerializeField] TextMeshProUGUI troopsText;
    [SerializeField] TextMeshProUGUI stakeText;

    [Header("4구역 · 액션")]
    [SerializeField] Button deployButton;
    [SerializeField] Button recallButton;

    [Header("가격 롤링")]
    [SerializeField, Min(18f)] float largeBuyPriceFontSize = 34f;

    [Header("투입 비율")]
    [SerializeField] int quickDeployTroopFixed = 0;
    [SerializeField, Range(0.02f, 0.5f)] float quickDeployGarrisonRatio = 0.10f;

    [Header("연출")]
    [SerializeField] Image cardBackgroundImage;
    [SerializeField] Image glossOverlayImage;
    [SerializeField] Image buyPriceBackground;
    [SerializeField] Image stakeGaugeFillImage;
    [SerializeField] Image disasterOverlayImage;
    [SerializeField] Image warTintImage;

    float _rollingBuyPrice = -1f;

    string _boundCastleId;
    Outline _outline;
    Color _normalCardColor;
    bool _cachedColors;
    Sequence _warPulseSeq;

    void Awake()
    {
        _outline = GetComponent<Outline>();
        TryAutoWire();
        CacheDefaultColors();
        WireActionButtons();
    }

    void WireActionButtons()
    {
        if (deployButton != null)
        {
            deployButton.onClick.RemoveListener(OnDeployClicked);
            deployButton.onClick.AddListener(OnDeployClicked);
        }

        if (recallButton != null)
        {
            recallButton.onClick.RemoveListener(OnRecallClicked);
            recallButton.onClick.AddListener(OnRecallClicked);
        }
    }

    void OnDeployClicked()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null || string.IsNullOrWhiteSpace(_boundCastleId)) return;
        if (!dm.castleStateDataMap.TryGetValue(_boundCastleId.Trim(), out var st) || st == null) return;
        dm.castleMasterDataMap.TryGetValue(_boundCastleId.Trim(), out var master);
        int troops = ComputeQuickDeployTroops(st, master);
        if (troops <= 0) return;
        dm.AddUserCastleDeployment(_boundCastleId.Trim(), troops, st.currentBuyPrice);
    }

    void OnRecallClicked()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null || string.IsNullOrWhiteSpace(_boundCastleId)) return;
        dm.RecallUserCastleDeployment(_boundCastleId.Trim());
    }

    int ComputeQuickDeployTroops(CastleStateData st, CastleMasterData master)
    {
        if (quickDeployTroopFixed > 0)
            return quickDeployTroopFixed;
        int cap = master != null ? master.maxTroops : 5000;
        int v = Mathf.Max(1, Mathf.RoundToInt(cap * quickDeployGarrisonRatio));
        return Mathf.Clamp(v, 1, Mathf.Max(1, cap - st.userDeployedTroops));
    }

    void OnDisable()
    {
        transform.DOKill(false);
        KillWarEffects();
    }

    void KillWarEffects()
    {
        _warPulseSeq?.Kill();
        _warPulseSeq = null;
        if (warTintImage != null)
            warTintImage.gameObject.SetActive(false);
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
        TextMeshProUGUI Tmp(string path) => transform.Find(path)?.GetComponent<TextMeshProUGUI>();
        Image Img(string path) => transform.Find(path)?.GetComponent<Image>();
        T FindComp<T>(string path) where T : Component => transform.Find(path)?.GetComponent<T>();

        // 위저드: Zone1 → Z1Row → NameColumn → NameRow (Z1Row 빠지면 Find 실패 → 이름·등급 미갱신)
        const string z1 = "MainRow/Zone1/Z1Row/NameColumn";
        const string z2 = "MainRow/Zone2";
        const string z3 = "MainRow/Zone3Personal";
        const string z4 = "MainRow/Zone4Actions";

        if (gradeAccentBarImage == null)
            gradeAccentBarImage = Img("MainRow/Zone1/Z1Row/GradeAccentBar") ?? Img("GradeRail");
        if (gradeBadgeText == null) gradeBadgeText = Tmp($"{z1}/NameRow/GradeBadge") ?? Tmp("Left/NameRow/GradeBadge");
        if (castleNameText == null)
            castleNameText = Tmp($"{z1}/NameRow/CastleName") ?? Tmp("Left/NameRow/CastleName");
        if (castleIdText == null) castleIdText = Tmp($"{z1}/CastleIdLine") ?? Tmp("Left/CastleIdLine");

        if (statusIconWar == null)
            statusIconWar = transform.Find($"{z1}/NameRow/StatusIcons/IconWar")?.gameObject
                            ?? transform.Find("Left/NameRow/StatusIcons/IconWar")?.gameObject;
        if (statusIconDisaster == null)
            statusIconDisaster = transform.Find($"{z1}/NameRow/StatusIcons/IconDisaster")?.gameObject
                                 ?? transform.Find("Left/NameRow/StatusIcons/IconDisaster")?.gameObject;
        if (statusIconFavorable == null)
            statusIconFavorable = transform.Find($"{z1}/NameRow/StatusIcons/IconFavorable")?.gameObject
                                  ?? transform.Find("Left/NameRow/StatusIcons/IconFavorable")?.gameObject;

        if (buyLabelText == null) buyLabelText = Tmp($"{z2}/BuyLabel") ?? Tmp("Left/MidRow/PriceBlock/BuyLabel");
        if (buyPriceText == null)
            buyPriceText = Tmp($"{z2}/BuyPriceBg/BuyPrice") ?? Tmp("Left/MidRow/PriceBlock/BuyPriceBg/BuyPrice");
        if (buyPriceBackground == null)
            buyPriceBackground = Img($"{z2}/BuyPriceBg") ?? Img("Left/MidRow/PriceBlock/BuyPriceBg");
        if (sentimentArrowText == null)
            sentimentArrowText = Tmp($"{z2}/SentRow/Arrow") ?? Tmp("Left/MidRow/PriceBlock/SentRow/Arrow");
        if (sentimentChangeText == null)
            sentimentChangeText = Tmp($"{z2}/SentRow/ChangePct") ?? Tmp("Left/MidRow/PriceBlock/SentRow/ChangePct");
        if (sparklineGraphic == null)
            sparklineGraphic = FindComp<UIMiniSparklineGraphic>($"{z2}/SparklineHost/Sparkline");

        if (zone3PersonalRoot == null)
            zone3PersonalRoot = transform.Find(z3) as RectTransform ?? transform.Find("Left/MidRow/PersonalBlock") as RectTransform;
        if (roiText == null)
            roiText = Tmp($"{z3}/RoiBox/RoiText") ?? Tmp("Left/MidRow/PersonalBlock/RoiLine");
        if (troopsText == null)
            troopsText = Tmp($"{z3}/TroopsLine") ?? Tmp("Left/MidRow/PersonalBlock/TroopsLine");
        if (stakeText == null)
            stakeText = Tmp($"{z3}/StakeLine") ?? Tmp("Left/MidRow/PersonalBlock/StakeLine");

        if (deployButton == null)
            deployButton = transform.Find($"{z4}/DeployButton")?.GetComponent<Button>()
                           ?? transform.Find("Governor/QuickDeploy")?.GetComponent<Button>()
                           ?? transform.Find("Left/MidRow/QuickDeploy")?.GetComponent<Button>();
        if (recallButton == null)
            recallButton = transform.Find($"{z4}/RecallButton")?.GetComponent<Button>();

        if (stakeGaugeFillImage == null)
            stakeGaugeFillImage = Img("StakeGaugeBar/Fill");
        if (disasterOverlayImage == null)
            disasterOverlayImage = Img("DisasterOverlay");
        if (warTintImage == null)
            warTintImage = Img("WarTint");

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
        WireActionButtons();
        CacheDefaultColors();

        var dm = DataManager.InstanceOrNull;
        if (dm == null || string.IsNullOrWhiteSpace(castleId)) return;

        castleId = castleId.Trim();
        _boundCastleId = castleId;
        if (!dm.castleStateDataMap.TryGetValue(castleId, out var st) || st == null) return;

        dm.castleMasterDataMap.TryGetValue(castleId, out var master);

        string dispName = master != null && !string.IsNullOrWhiteSpace(master.name) ? master.name : castleId;
        Grade g = master?.grade ?? Grade.D;

        if (castleNameText != null)
        {
            castleNameText.richText = false;
            castleNameText.text = dispName;
            castleNameText.fontStyle = FontStyles.Bold;
        }

        if (castleIdText != null)
        {
            string region = "";
            if (master != null && !string.IsNullOrWhiteSpace(master.regionId))
            {
                var reg = dm.GetRegionMasterData(master.regionId.Trim());
                region = reg != null && !string.IsNullOrWhiteSpace(reg.sectorName) ? reg.sectorName.Trim() : master.regionId.Trim();
            }
            castleIdText.text = string.IsNullOrEmpty(region) ? $"ID: {castleId}" : $"{region} · ID: {castleId}";
            castleIdText.color = new Color(0.55f, 0.58f, 0.64f, 1f);
        }

        if (statusIconWar != null)
            statusIconWar.SetActive(st.isWar);
        if (statusIconDisaster != null)
            statusIconDisaster.SetActive(st.isDisaster);
        if (statusIconFavorable != null)
            statusIconFavorable.SetActive(st.isFavorableEvent);

        if (gradeBadgeText != null)
        {
            gradeBadgeText.text = g.ToString();
            gradeBadgeText.color = GradeAccentColor(g);
        }

        if (gradeAccentBarImage != null)
            gradeAccentBarImage.color = GradeAccentColor(g);

        if (buyLabelText != null)
            buyLabelText.text = "매수가";

        SetBuyPriceAnimated(st.currentBuyPrice);

        ResolveTrendUi(st, out bool trendUp, out bool trendFlat, out float pctDisplay, out bool riskDown);

        if (sentimentArrowText != null)
        {
            sentimentArrowText.text = trendFlat ? "—" : (trendUp ? "▲" : "▼");
            sentimentArrowText.color = trendFlat
                ? new Color(0.55f, 0.58f, 0.64f)
                : (trendUp ? RiseColor : FallColor);
        }

        if (sentimentChangeText != null)
        {
            bool hasSent = TryComputeSentimentPercentChange(st, out _);
            if (riskDown && !hasSent)
                sentimentChangeText.text = st.isWar ? "교전 리스크" : "재해 리스크";
            else if (!riskDown && trendFlat)
                sentimentChangeText.text = "변동 —";
            else
                sentimentChangeText.text = $"{(trendUp ? "+" : "")}{pctDisplay:F2}%";
            sentimentChangeText.color = !riskDown && trendFlat
                ? new Color(0.65f, 0.68f, 0.74f)
                : (trendUp ? RiseColor : FallColor);
        }

        if (buyPriceBackground != null)
            buyPriceBackground.color = trendFlat
                ? new Color(0.16f, 0.17f, 0.20f, 0.95f)
                : (trendUp ? BuyBoxUp : BuyBoxDown);

        if (sparklineGraphic != null)
            sparklineGraphic.SetHistories(st.populationHistory, st.sentimentHistory);

        bool invested = st.IsUserInvested;
        if (zone3PersonalRoot != null)
            zone3PersonalRoot.gameObject.SetActive(invested);

        if (recallButton != null)
            recallButton.gameObject.SetActive(invested);

        int maxG = master?.maxTroops ?? 0;
        float stake = maxG > 0 ? Mathf.Clamp01(st.userDeployedTroops / (float)maxG) * 100f : 0f;

        if (troopsText != null)
        {
            troopsText.text = invested ? $"{st.userDeployedTroops:N0}명" : "";
            troopsText.color = PersonalGold;
        }

        if (stakeText != null)
        {
            stakeText.text = invested && maxG > 0 ? $"지분 {stake:F1}%" : "";
            stakeText.color = PersonalGoldDim;
        }

        Transform stakeBarRoot = stakeGaugeFillImage != null ? stakeGaugeFillImage.transform.parent : null;
        if (stakeBarRoot != null)
            stakeBarRoot.gameObject.SetActive(invested && maxG > 0);

        if (stakeGaugeFillImage != null && invested && maxG > 0)
            stakeGaugeFillImage.fillAmount = Mathf.Clamp01(st.userDeployedTroops / (float)maxG);

        if (roiText != null)
        {
            if (invested && st.averagePurchasePrice > 0.0001f)
            {
                float roi = (st.currentBuyPrice - st.averagePurchasePrice) / st.averagePurchasePrice * 100f;
                roiText.text = $"{(roi >= 0 ? "+" : "")}{roi:F1}%";
                roiText.color = roi >= 0f ? PersonalGold : RoiFallReadable;
            }
            else if (invested)
            {
                roiText.text = "—";
                roiText.color = PersonalGoldDim;
            }
            else
            {
                roiText.text = "";
            }
        }

        ApplyCardChrome(st, invested);
    }

    void SetBuyPriceAnimated(float buy)
    {
        if (buyPriceText == null) return;
        buyPriceText.fontSize = largeBuyPriceFontSize;
        if (_rollingBuyPrice < 0f)
        {
            _rollingBuyPrice = buy;
            buyPriceText.text = $"{Mathf.RoundToInt(buy):N0} Gold";
            return;
        }

        if (Mathf.Abs(_rollingBuyPrice - buy) < 0.5f)
        {
            _rollingBuyPrice = buy;
            buyPriceText.text = $"{Mathf.RoundToInt(buy):N0} Gold";
            return;
        }

        float v = _rollingBuyPrice;
        DOTween.To(() => v, x =>
        {
            v = x;
            buyPriceText.text = $"{Mathf.RoundToInt(x):N0} Gold";
        }, buy, 0.22f).SetEase(Ease.OutQuad).SetTarget(this).OnComplete(() => _rollingBuyPrice = buy);
    }

    void ResolveTrendUi(CastleStateData st, out bool up, out bool flat, out float pctOut, out bool riskDown)
    {
        riskDown = st.isWar || st.isDisaster;
        pctOut = 0f;
        bool hasSent = TryComputeSentimentPercentChange(st, out float pctChg);

        if (riskDown)
        {
            up = false;
            flat = false;
            pctOut = hasSent ? pctChg : -1f;
            return;
        }

        if (!hasSent)
        {
            up = true;
            flat = true;
            return;
        }

        up = pctChg > 0f;
        flat = Mathf.Abs(pctChg) < 0.0005f;
        pctOut = pctChg;
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

    void ApplyCardChrome(CastleStateData st, bool invested)
    {
        bool war = st.isWar;
        bool disaster = st.isDisaster;

        KillWarEffects();

        if (disasterOverlayImage != null)
        {
            disasterOverlayImage.gameObject.SetActive(disaster);
            if (disaster)
                disasterOverlayImage.color = new Color(0.04f, 0.05f, 0.08f, 0.42f);
        }

        if (glossOverlayImage != null)
        {
            bool glossOn = invested && !war && !disaster;
            glossOverlayImage.gameObject.SetActive(glossOn);
            if (glossOn)
                glossOverlayImage.color = new Color(1f, 0.94f, 0.78f, 0.09f);
        }

        if (cardBackgroundImage != null)
        {
            if (invested && !war && !disaster)
                cardBackgroundImage.color = new Color(0.14f, 0.13f, 0.11f, 0.99f);
            else
                cardBackgroundImage.color = _normalCardColor;
        }

        if (war && warTintImage != null)
        {
            warTintImage.gameObject.SetActive(true);
            warTintImage.color = new Color(0.55f, 0.1f, 0.1f, 0f);
            _warPulseSeq = DOTween.Sequence();
            _warPulseSeq.Append(DOTween.To(() => warTintImage.color.a, a =>
            {
                var c = warTintImage.color;
                c.a = a;
                warTintImage.color = c;
            }, 0.14f, 0.55f).SetEase(Ease.InOutSine));
            _warPulseSeq.Append(DOTween.To(() => warTintImage.color.a, a =>
            {
                var c = warTintImage.color;
                c.a = a;
                warTintImage.color = c;
            }, 0f, 0.55f).SetEase(Ease.InOutSine));
            _warPulseSeq.SetLoops(-1);
            _warPulseSeq.SetTarget(gameObject);
        }
        else if (warTintImage != null)
            warTintImage.gameObject.SetActive(false);

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
            case Grade.C: return new Color(0.72f, 0.76f, 0.82f, 1f);
            case Grade.D: return new Color(0.58f, 0.60f, 0.65f, 1f);
            default: return new Color(0.55f, 0.58f, 0.64f, 1f);
        }
    }
}
