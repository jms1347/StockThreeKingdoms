using System;
using System.Collections.Generic;
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
    static readonly Color HqOutline = new Color(1f, 0.88f, 0.42f, 0.95f);

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
    [SerializeField] Button hqMoveButton;
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
    [Tooltip("투자 구역 배경(없으면 Zone3 루트에서 Image 탐색). 수익/손실 틴트.")]
    [SerializeField] Image roiZoneBackdropImage;
    [Tooltip("전쟁 시 재생할 UI/월드 파티클(선택).")]
    [SerializeField] ParticleSystem warBurstParticles;

    float _rollingBuyPrice = -1f;
    string _lastBoundCastleForPrice;
    float _lastBoundBuyPrice = -1f;

    string _boundCastleId;
    Outline _outline;
    Color _normalCardColor;
    Color? _roiBackdropBaseColor;
    bool _cachedColors;
    Sequence _warPulseSeq;
    Sequence _favorablePulseSeq;
    Tweener _warShakeTweener;
    GameObject _hqBadgeGo;

    void Awake()
    {
        _outline = GetComponent<Outline>();
        TryAutoWire();
        EnsureHqMoveButtonUi();
        EnsureHqBadgeUi();
        CacheDefaultColors();
        WireActionButtons();
        WireCardOpenDetailButton();
    }

    void WireCardOpenDetailButton()
    {
        var openBtn = GetComponent<Button>();
        if (openBtn == null) return;
        openBtn.onClick.RemoveListener(OnCastleCardOpenDetail);
        openBtn.onClick.AddListener(OnCastleCardOpenDetail);
    }

    void OnCastleCardOpenDetail()
    {
        if (string.IsNullOrWhiteSpace(_boundCastleId)) return;
        WorldMarketCastleDetailPopup.OpenCastle(_boundCastleId.Trim());
    }

    void WireActionButtons()
    {
        if (deployButton != null)
        {
            deployButton.onClick.RemoveListener(OnDeployClicked);
            deployButton.onClick.AddListener(OnDeployClicked);
        }

        if (hqMoveButton != null)
        {
            hqMoveButton.onClick.RemoveListener(OnHqMoveClicked);
            hqMoveButton.onClick.AddListener(OnHqMoveClicked);
        }

        if (recallButton != null)
        {
            recallButton.onClick.RemoveListener(OnRecallClicked);
            recallButton.onClick.AddListener(OnRecallClicked);
        }
    }

    void EnsureHqMoveButtonUi()
    {
        if (hqMoveButton != null) return;
        var z4 = transform.Find("MainRow/Zone4Actions");
        if (z4 == null) return;
        var existing = z4.Find("HqMoveButton");
        if (existing != null)
        {
            hqMoveButton = existing.GetComponent<Button>();
            return;
        }

        var deployTf = z4.Find("DeployButton");
        int insertIndex = deployTf != null ? deployTf.GetSiblingIndex() + 1 : 0;
        var go = new GameObject("HqMoveButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(z4, false);
        go.transform.SetSiblingIndex(insertIndex);
        go.GetComponent<Image>().color = new Color(0.22f, 0.38f, 0.62f, 0.98f);
        var le = go.GetComponent<LayoutElement>();
        le.minHeight = 42f;
        le.preferredHeight = 46f;
        le.flexibleWidth = 1f;
        le.flexibleHeight = 0f;
        var btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;

        var lab = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        lab.transform.SetParent(go.transform, false);
        var tmp = lab.GetComponent<TextMeshProUGUI>();
        tmp.text = "본영 이주";
        tmp.fontSize = 15;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        var lrt = lab.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        hqMoveButton = btn;
    }

    void OnHqMoveClicked()
    {
        if (string.IsNullOrWhiteSpace(_boundCastleId)) return;
        bool ensured = false;
        Transform t = transform;
        for (int i = 0; i < 16 && t != null; i++, t = t.parent)
        {
            if (t.name != "WorldMarketRoot") continue;
            WorldHqTravelHud.EnsureUnderWorldMarketRoot(t);
            ensured = true;
            break;
        }

        if (!ensured)
            Debug.LogWarning("[WorldMarketCastleCardView] 부모에 WorldMarketRoot가 없어 이동 HUD를 생성하지 못했습니다.");

        if (WorldHqTravelHud.InstanceOrNull == null)
        {
            Debug.LogWarning("[WorldMarketCastleCardView] WorldHqTravelHud가 없습니다. 천하 패널이 WorldMarketRoot 아래에 있는지 확인하세요.");
            return;
        }

        WorldHqTravelHud.InstanceOrNull.TryBeginTravelTo(_boundCastleId.Trim());
    }

    void EnsureHqBadgeUi()
    {
        if (_hqBadgeGo != null) return;
        var nr = transform.Find("MainRow/Zone1/Z1Row/NameColumn/NameRow") ?? transform.Find("Left/NameRow");
        if (nr == null) return;
        var existing = nr.Find("HqBadge");
        if (existing != null)
        {
            _hqBadgeGo = existing.gameObject;
            return;
        }

        var go = new GameObject("HqBadge", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(nr, false);
        go.transform.SetSiblingIndex(1);
        var le = go.GetComponent<LayoutElement>();
        le.flexibleWidth = 0f;
        le.minWidth = 32f;
        le.preferredWidth = 36f;
        le.preferredHeight = 22f;
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = "HQ";
        tmp.fontSize = 14;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(0.95f, 0.82f, 0.35f, 1f);
        tmp.alignment = TextAlignmentOptions.Center;
        go.SetActive(false);
        _hqBadgeGo = go;
    }

    void OnDeployClicked()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null || string.IsNullOrWhiteSpace(_boundCastleId)) return;
        if (!dm.castleStateDataMap.TryGetValue(_boundCastleId.Trim(), out var st) || st == null) return;
        int troops = dm.ComputeMaxDeployTroopsForCastle(_boundCastleId.Trim());
        if (troops <= 0) return;
        if (quickDeployTroopFixed > 0)
            troops = Mathf.Min(quickDeployTroopFixed, troops);
        dm.AddUserCastleDeployment(_boundCastleId.Trim(), troops,
            dm.EvaluateBuyPriceForCastle(_boundCastleId.Trim()));
    }

    void OnRecallClicked()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null || string.IsNullOrWhiteSpace(_boundCastleId)) return;
        dm.RecallUserCastleDeployment(_boundCastleId.Trim());
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
        _favorablePulseSeq?.Kill();
        _favorablePulseSeq = null;
        _warShakeTweener?.Kill();
        _warShakeTweener = null;
        // 카드 루트 anchoredPosition은 WorldMarketCastleVirtualList가 행 인덱스마다 설정함.
        // 여기서 복원하면 매 Bind마다 (0,0)으로 덮여 전부 한곳에 겹침.
        if (warTintImage != null)
            warTintImage.gameObject.SetActive(false);
        if (warBurstParticles != null)
        {
            warBurstParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            warBurstParticles.gameObject.SetActive(false);
        }
        if (statusIconFavorable != null)
        {
            statusIconFavorable.transform.DOKill();
            statusIconFavorable.transform.localScale = Vector3.one;
        }
    }

    void CacheDefaultColors()
    {
        if (!_cachedColors)
        {
            if (cardBackgroundImage == null)
                cardBackgroundImage = GetComponent<Image>();
            if (cardBackgroundImage != null)
            {
                _normalCardColor = cardBackgroundImage.color;
                _cachedColors = true;
            }
        }

        if (roiZoneBackdropImage != null && !_roiBackdropBaseColor.HasValue)
            _roiBackdropBaseColor = roiZoneBackdropImage.color;
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
        if (roiZoneBackdropImage == null && zone3PersonalRoot != null)
            roiZoneBackdropImage = zone3PersonalRoot.GetComponent<Image>();
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
        if (hqMoveButton == null)
            hqMoveButton = transform.Find($"{z4}/HqMoveButton")?.GetComponent<Button>();

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
        EnsureHqMoveButtonUi();
        EnsureHqBadgeUi();
        WireActionButtons();
        CacheDefaultColors();

        var dm = DataManager.InstanceOrNull;
        if (dm == null || string.IsNullOrWhiteSpace(castleId)) return;

        castleId = castleId.Trim();
        _boundCastleId = castleId;

        dm.castleMasterDataMap.TryGetValue(castleId, out var master);
        dm.castleStateDataMap.TryGetValue(castleId, out var st);
        bool hasLive = dm.TryGetLiveCastleState(castleId, out var live);

        if (!hasLive && st == null)
        {
            if (hqMoveButton != null)
                hqMoveButton.gameObject.SetActive(false);
            return;
        }

        int population = hasLive ? live.currentPopulation : st.currentPopulation;
        float sentiment = hasLive ? live.currentSentiment : st.currentSentiment;
        bool isWar = hasLive ? live.isWar : st.isWar;
        bool isDisaster = hasLive ? live.isDisaster : st.isDisaster;
        bool isFavorable = hasLive ? live.isFavorableEvent : st.isFavorableEvent;
        float buyPrice = hasLive ? live.currentBuyPrice : st.currentBuyPrice;

        dm.TryGetUserCastleStock(castleId, out var userStock);
        bool hasStockFromSo = userStock != null && userStock.troopCount > 0;
        bool hasStockFromMap = st != null && st.IsUserInvested;
        bool hasStock = hasStockFromSo || hasStockFromMap;
        int troopCount = hasStockFromSo ? userStock.troopCount : (st != null ? st.userDeployedTroops : 0);

        string dispName = dm.GetCastleDisplayName(castleId);
        if (string.IsNullOrEmpty(dispName) || dispName == "성")
        {
            if (master != null && !string.IsNullOrWhiteSpace(master.name) && !CastleDisplayLabels.LooksLikeRegionOrCastleCode(master.name))
                dispName = master.name.Trim();
            else if (master != null && !string.IsNullOrWhiteSpace(master.regionId) && !CastleDisplayLabels.LooksLikeRegionOrCastleCode(master.regionId))
                dispName = master.regionId.Trim();
            else
                dispName = "성";
        }
        string regionLine = dm.GetCastleRegionSubtitle(castleId);
        Faction lord = Faction.NONE;
        if (hasLive && live != null) lord = live.currentLord;
        else if (st != null) lord = st.currentLord;
        string occLine = lord == Faction.NONE ? "중립" : $"{DataManager.GetFactionLordShortLabel(lord)} 점령";
        bool isHqHome = !string.IsNullOrWhiteSpace(dm.HomeCastleId)
                        && string.Equals(dm.HomeCastleId.Trim(), castleId, StringComparison.Ordinal);
        Grade g = master?.grade ?? Grade.D;

        if (castleNameText != null)
        {
            castleNameText.richText = false;
            castleNameText.text = dispName;
            castleNameText.fontStyle = FontStyles.Bold;
        }

        if (castleIdText != null)
        {
            castleIdText.text = string.IsNullOrEmpty(regionLine) ? occLine : $"{regionLine} · {occLine}";
            castleIdText.color = new Color(0.55f, 0.58f, 0.64f, 1f);
        }

        if (statusIconWar != null)
            statusIconWar.SetActive(isWar);
        if (statusIconDisaster != null)
            statusIconDisaster.SetActive(isDisaster);
        if (statusIconFavorable != null)
            statusIconFavorable.SetActive(isFavorable);

        if (gradeBadgeText != null)
        {
            gradeBadgeText.text = g.ToString();
            gradeBadgeText.color = GradeAccentColor(g);
        }

        if (gradeAccentBarImage != null)
            gradeAccentBarImage.color = GradeAccentColor(g);

        if (buyLabelText != null)
            buyLabelText.text = "매수가";

        bool scrollRefreshSamePrice = !string.IsNullOrEmpty(_lastBoundCastleForPrice)
                                      && string.Equals(_lastBoundCastleForPrice, castleId, StringComparison.Ordinal)
                                      && Mathf.Abs(_lastBoundBuyPrice - buyPrice) < 0.5f;
        SetBuyPriceAnimated(buyPrice, scrollRefreshSamePrice);

        ResolveTrendUiFromLive(live, st, out bool trendUp, out bool trendFlat, out float pctDisplay, out bool riskDown);

        if (sentimentArrowText != null)
        {
            sentimentArrowText.text = trendFlat ? "—" : (trendUp ? "▲" : "▼");
            sentimentArrowText.color = trendFlat
                ? new Color(0.55f, 0.58f, 0.64f)
                : (trendUp ? RiseColor : FallColor);
        }

        if (sentimentChangeText != null)
        {
            bool hasSent = st != null && TryComputeSentimentPercentChange(st, out _);
            if (riskDown && !hasSent)
                sentimentChangeText.text = isWar ? "교전 리스크" : "재해 리스크";
            else if (!riskDown && trendFlat)
                sentimentChangeText.text = $"민심 {sentiment:0.#}";
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
        {
            if (st != null && st.populationHistory != null && st.sentimentHistory != null
                         && st.populationHistory.Count > 0 && st.sentimentHistory.Count > 0)
                sparklineGraphic.SetHistories(st.populationHistory, st.sentimentHistory);
            else
                sparklineGraphic.SetHistories(new List<int> { population }, new List<float> { sentiment });
        }

        if (zone3PersonalRoot != null)
            zone3PersonalRoot.gameObject.SetActive(true);

        if (recallButton != null)
            recallButton.gameObject.SetActive(hasStock);

        bool hqTravelBusy = dm.HasPendingHqMove
                            || (WorldHqTravelHud.InstanceOrNull != null
                                && WorldHqTravelHud.InstanceOrNull.IsHqTravelAnimating);
        if (hqMoveButton != null)
        {
            hqMoveButton.gameObject.SetActive(!isHqHome && !hqTravelBusy);
            var hqLbl = hqMoveButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (hqLbl != null)
                hqLbl.text = "본영 이주";
        }

        int maxG = master?.maxTroops ?? 0;
        float stake = maxG > 0 ? Mathf.Clamp01(troopCount / (float)maxG) * 100f : 0f;

        if (troopsText != null)
        {
            troopsText.text = hasStock ? $"{troopCount:N0}명" : "";
            troopsText.color = PersonalGold;
        }

        if (stakeText != null)
        {
            stakeText.text = hasStock && maxG > 0 ? $"지분 {stake:F1}%" : "";
            stakeText.color = PersonalGoldDim;
        }

        Transform stakeBarRoot = stakeGaugeFillImage != null ? stakeGaugeFillImage.transform.parent : null;
        if (stakeBarRoot != null)
            stakeBarRoot.gameObject.SetActive(hasStock && maxG > 0);

        if (stakeGaugeFillImage != null && hasStock && maxG > 0)
            stakeGaugeFillImage.fillAmount = Mathf.Clamp01(troopCount / (float)maxG);

        if (deployButton != null)
            deployButton.interactable = dm.ComputeMaxDeployTroopsForCastle(castleId) > 0;

        if (roiText != null)
        {
            if (!hasStock)
            {
                roiText.text = "미투자";
                roiText.color = PersonalGoldDim;
                ResetRoiZoneTint();
            }
            else if (dm.TryGetCastleRoiSellBasis(castleId, out float roiSell))
            {
                roiText.text = $"{(roiSell >= 0 ? "+" : "")}{roiSell:F1}%";
                roiText.color = roiSell > 0.001f ? RiseColor : (roiSell < -0.001f ? FallColor : PersonalGoldDim);
                ApplyRoiZoneTint(roiSell);
            }
            else
            {
                roiText.text = "—";
                roiText.color = PersonalGoldDim;
                ResetRoiZoneTint();
            }
        }

        ApplyCardChrome(isWar, isDisaster, isFavorable, hasStock, isHqHome);

        _lastBoundCastleForPrice = castleId;
        _lastBoundBuyPrice = buyPrice;
    }

    void ApplyRoiZoneTint(float roiSell)
    {
        if (roiZoneBackdropImage == null) return;
        if (!_roiBackdropBaseColor.HasValue)
            _roiBackdropBaseColor = roiZoneBackdropImage.color;
        var baseC = _roiBackdropBaseColor.Value;
        if (roiSell > 0.001f)
        {
            roiZoneBackdropImage.color = Color.Lerp(baseC, new Color(0.42f, 0.14f, 0.12f, 0.55f), 0.55f);
        }
        else if (roiSell < -0.001f)
        {
            roiZoneBackdropImage.color = Color.Lerp(baseC, new Color(0.10f, 0.18f, 0.38f, 0.52f), 0.55f);
        }
        else
            roiZoneBackdropImage.color = baseC;
    }

    void ResetRoiZoneTint()
    {
        if (roiZoneBackdropImage == null || !_roiBackdropBaseColor.HasValue) return;
        roiZoneBackdropImage.color = _roiBackdropBaseColor.Value;
    }

    void SetBuyPriceAnimated(float buy, bool suppressTweenForScrollRefresh = false)
    {
        if (buyPriceText == null) return;
        buyPriceText.fontSize = largeBuyPriceFontSize;

        if (suppressTweenForScrollRefresh)
        {
            _rollingBuyPrice = buy;
            buyPriceText.text = $"{Mathf.RoundToInt(buy):N0} Gold";
            return;
        }

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

    void ResolveTrendUiFromLive(CastleStateSo.CastleLiveStateEntry live, CastleStateData st, out bool up, out bool flat, out float pctOut, out bool riskDown)
    {
        bool war = live != null ? live.isWar : (st != null && st.isWar);
        bool disaster = live != null ? live.isDisaster : (st != null && st.isDisaster);
        riskDown = war || disaster;
        pctOut = 0f;
        float pctChg = 0f;
        bool hasSent = st != null && TryComputeSentimentPercentChange(st, out pctChg);

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
        if (st == null) return false;
        var h = st.sentimentHistory;
        if (h == null || h.Count < 2) return false;
        float prev = h[h.Count - 2];
        float last = h[h.Count - 1];
        if (Mathf.Abs(prev) < 0.01f)
            return false;
        pct = (last - prev) / Mathf.Abs(prev) * 100f;
        return true;
    }

    void ApplyCardChrome(bool war, bool disaster, bool favorableEvent, bool hasUserStock, bool isHqHome)
    {
        KillWarEffects();

        if (_hqBadgeGo != null)
            _hqBadgeGo.SetActive(isHqHome);

        if (disasterOverlayImage != null)
        {
            disasterOverlayImage.gameObject.SetActive(disaster);
            if (disaster)
                disasterOverlayImage.color = new Color(0.04f, 0.05f, 0.08f, 0.42f);
        }

        if (glossOverlayImage != null)
        {
            bool glossOn = hasUserStock && !war && !disaster;
            glossOverlayImage.gameObject.SetActive(glossOn);
            if (glossOn)
                glossOverlayImage.color = new Color(1f, 0.94f, 0.78f, 0.09f);
        }

        if (cardBackgroundImage != null)
        {
            if (hasUserStock && !war && !disaster)
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

            var rt = transform as RectTransform;
            if (rt != null)
            {
                _warShakeTweener = rt.DOShakeAnchorPos(1.05f, new Vector2(2.2f, 1.6f), 10, 90f, false, true)
                    .SetLoops(-1)
                    .SetUpdate(true)
                    .SetTarget(gameObject);
            }

            if (warBurstParticles != null)
            {
                warBurstParticles.gameObject.SetActive(true);
                warBurstParticles.Play(true);
            }
        }
        else if (warTintImage != null)
            warTintImage.gameObject.SetActive(false);

        if (!war && favorableEvent && statusIconFavorable != null && statusIconFavorable.activeSelf)
        {
            var tr = statusIconFavorable.transform;
            tr.DOKill();
            tr.localScale = Vector3.one;
            _favorablePulseSeq = DOTween.Sequence();
            _favorablePulseSeq.Append(tr.DOScale(1.12f, 0.38f).SetEase(Ease.InOutSine));
            _favorablePulseSeq.Append(tr.DOScale(1f, 0.38f).SetEase(Ease.InOutSine));
            _favorablePulseSeq.SetLoops(-1);
            _favorablePulseSeq.SetTarget(gameObject);
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
        else if (isHqHome)
        {
            _outline.enabled = true;
            _outline.effectColor = HqOutline;
            _outline.effectDistance = new Vector2(3.2f, -3.2f);
        }
        else if (hasUserStock)
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
