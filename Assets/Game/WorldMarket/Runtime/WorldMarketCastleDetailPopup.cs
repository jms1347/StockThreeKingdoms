using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>천하 탭 MTS 스타일 성 상세 팝업 — 마스터·라이브 상태·포트폴리오 바인딩.</summary>
[DisallowMultipleComponent]
public class WorldMarketCastleDetailPopup : MonoBehaviour
{
    public static WorldMarketCastleDetailPopup InstanceOrNull { get; private set; }

    static readonly Color RiseColor = new Color(0.95f, 0.28f, 0.28f, 1f);
    static readonly Color FallColor = new Color(0.32f, 0.52f, 0.95f, 1f);

    [SerializeField] CanvasGroup rootCanvasGroup;
    [SerializeField] RectTransform panelRoot;
    [SerializeField] Button closeButton;
    [SerializeField] Button dimCloseButton;
    [SerializeField] TextMeshProUGUI headerTitleText;
    [SerializeField] TextMeshProUGUI headerSubtitlePrefixText;
    [SerializeField] TextMeshProUGUI headerSubtitleText;
    [SerializeField] Image factionSubtitleSwatchImage;
    [SerializeField] TextMeshProUGUI buyCaptionText;
    [SerializeField] TextMeshProUGUI buyPriceBigText;
    [SerializeField] TextMeshProUGUI sellCaptionText;
    [SerializeField] TextMeshProUGUI sellPriceText;
    [SerializeField] TextMeshProUGUI changePctText;
    [SerializeField] UIPopSentiment7DayChart chart7Dual;
    [SerializeField] TextMeshProUGUI popStatText;
    [SerializeField] Slider popGaugeSlider;
    [SerializeField] TextMeshProUGUI sentimentStatText;
    [SerializeField] Slider sentimentGaugeSlider;
    [SerializeField] TextMeshProUGUI militaryStatText;
    [SerializeField] Slider militaryGaugeSlider;
    [SerializeField] TextMeshProUGUI assetStatText;
    [SerializeField] Slider assetGaugeSlider;
    [SerializeField] Image factionBandImage;
    [SerializeField] Image governorPortraitFrameImage;
    [SerializeField] Image governorPortraitImage;
    [SerializeField] TextMeshProUGUI governorInitialText;
    [SerializeField] TextMeshProUGUI governorNameText;
    [SerializeField] TextMeshProUGUI governorFactionText;
    [SerializeField] RectTransform portfolioBox;
    [SerializeField] TextMeshProUGUI portfolioText;
    [SerializeField] Button deployButton;
    [SerializeField] Button recallButton;
    [SerializeField] Button relocateButton;
    [SerializeField] TextMeshProUGUI relocateHintText;
    [SerializeField] TextMeshProUGUI footprintIcon1;
    [SerializeField] TextMeshProUGUI footprintIcon2;
    [SerializeField] TextMeshProUGUI footprintIcon3;
    [SerializeField] Button confirmCloseButton;
    [SerializeField] RectTransform deployDialogRoot;
    [SerializeField] Slider deploySlider;
    [SerializeField] TextMeshProUGUI deploySliderValueText;
    [SerializeField] Button deployConfirmButton;
    [SerializeField] Button deployCancelButton;

    const string GaugeTweenId = "CastleDetailGauges";

    string _castleId;
    DataManager _dm;
    int _deployMaxThisOpen;
    bool _playOpenGaugeAnim;

    void Awake()
    {
        InstanceOrNull = this;
        BuildUiIfNeeded();
        WireButtons();
        DisableHeaderCloseButton();
        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (InstanceOrNull == this)
            InstanceOrNull = null;
        DOTween.Kill(GaugeTweenId);
        UnhookDm();
    }

    void WireButtons()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }

        if (dimCloseButton != null)
        {
            dimCloseButton.onClick.RemoveListener(Close);
            dimCloseButton.onClick.AddListener(Close);
        }

        if (deployButton != null)
        {
            deployButton.onClick.RemoveListener(OnDeployOpen);
            deployButton.onClick.AddListener(OnDeployOpen);
        }

        if (recallButton != null)
        {
            recallButton.onClick.RemoveListener(OnRecallClicked);
            recallButton.onClick.AddListener(OnRecallClicked);
        }

        if (relocateButton != null)
        {
            relocateButton.onClick.RemoveListener(OnRelocateClicked);
            relocateButton.onClick.AddListener(OnRelocateClicked);
        }

        if (deployConfirmButton != null)
        {
            deployConfirmButton.onClick.RemoveListener(OnDeployConfirm);
            deployConfirmButton.onClick.AddListener(OnDeployConfirm);
        }

        if (deployCancelButton != null)
        {
            deployCancelButton.onClick.RemoveListener(OnDeployCancel);
            deployCancelButton.onClick.AddListener(OnDeployCancel);
        }

        if (deploySlider != null)
            deploySlider.onValueChanged.RemoveListener(OnDeploySlider);

        if (confirmCloseButton != null)
        {
            confirmCloseButton.onClick.RemoveListener(Close);
            confirmCloseButton.onClick.AddListener(Close);
        }
    }

    void DisableHeaderCloseButton()
    {
        if (closeButton == null) return;
        closeButton.onClick.RemoveAllListeners();
        closeButton.gameObject.SetActive(false);
    }

    public static void OpenCastle(string castleId)
    {
        if (InstanceOrNull == null) return;
        InstanceOrNull.Open(castleId);
    }

    public void Open(string castleId)
    {
        if (string.IsNullOrWhiteSpace(castleId)) return;
        var dm = DataManager.InstanceOrNull;
        if (dm == null || !dm.IsStateReady) return;

        _castleId = castleId.Trim();
        dm.SyncCastleMarketPricesFromFormula(_castleId);
        _playOpenGaugeAnim = true;
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = 1f;
            rootCanvasGroup.interactable = true;
            rootCanvasGroup.blocksRaycasts = true;
        }

        HookDm();
        Refresh();
    }

    public void Close()
    {
        DOTween.Kill(GaugeTweenId);
        if (deployDialogRoot != null)
            deployDialogRoot.gameObject.SetActive(false);
        UnhookDm();
        gameObject.SetActive(false);
        _castleId = null;
    }

    void HookDm()
    {
        UnhookDm();
        _dm = DataManager.InstanceOrNull;
        if (_dm == null) return;
        _dm.OnStateTicked += OnDmDirty;
        _dm.OnTravelGaugeChanged += OnDmDirty;
        _dm.OnHomeCastleChanged += OnDmDirty;
    }

    void UnhookDm()
    {
        if (_dm == null) return;
        _dm.OnStateTicked -= OnDmDirty;
        _dm.OnTravelGaugeChanged -= OnDmDirty;
        _dm.OnHomeCastleChanged -= OnDmDirty;
        _dm = null;
    }

    void OnDmDirty() => Refresh();

    void Refresh()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null || !dm.IsStateReady || string.IsNullOrWhiteSpace(_castleId)) return;

        dm.SyncCastleMarketPricesFromFormula(_castleId);

        dm.castleMasterDataMap.TryGetValue(_castleId, out var master);
        dm.castleStateDataMap.TryGetValue(_castleId, out var st);

        if (st == null)
        {
            Close();
            return;
        }

        dm.castleMasterDataMap.TryGetValue(st.id, out var masterResolved);
        if (masterResolved != null) master = masterResolved;

        int pop = st.currentPopulation;
        float sentiment = st.currentSentiment;
        float buy = dm.EvaluateBuyPriceForCastle(_castleId);
        float sell = dm.EvaluateSellPriceForCastle(_castleId);
        Faction lord = st.currentLord;
        string govId = st.currentGovernorId;

        string disp = dm.GetCastleDisplayName(_castleId);
        if (string.IsNullOrWhiteSpace(disp) && master != null)
            disp = master.name;
        if (string.IsNullOrWhiteSpace(disp))
            disp = _castleId;
        Grade g = master?.grade ?? Grade.D;

        if (headerTitleText != null)
            headerTitleText.text = disp;
        Color subCol = new Color(0.62f, 0.66f, 0.72f, 1f);
        string factionLine = DataManager.GetFactionLordDisplayLabel(lord);
        if (headerSubtitlePrefixText != null && factionSubtitleSwatchImage != null && headerSubtitleText != null)
        {
            headerSubtitlePrefixText.text = $"등급  [{g}]    ·    ";
            headerSubtitlePrefixText.color = subCol;
            headerSubtitleText.text = factionLine;
            headerSubtitleText.color = subCol;
            factionSubtitleSwatchImage.color = FactionAccentColor(lord);
            SyncFactionSubtitleSwatchSize();
        }
        else if (headerSubtitleText != null)
        {
            headerSubtitleText.text = $"등급  [{g}]    ·    {factionLine}";
            headerSubtitleText.color = subCol;
        }

        if (buyCaptionText != null)
            buyCaptionText.text = "입성비";
        if (buyPriceBigText != null)
            buyPriceBigText.text = $"{Mathf.RoundToInt(buy):N0} G";
        if (sellCaptionText != null)
            sellCaptionText.text = "퇴성비";
        if (sellPriceText != null)
            sellPriceText.text = $"{Mathf.RoundToInt(sell):N0} G";

        float pct = dm.CalculateChangeRate24h(st);
        if (changePctText != null)
        {
            bool up = pct > 0.001f;
            bool flat = Mathf.Abs(pct) < 0.02f;
            changePctText.text = flat ? "0.00%" : $"{(up ? "+" : "")}{pct:F2}%";
            changePctText.color = flat ? new Color(0.7f, 0.72f, 0.76f) : (up ? RiseColor : FallColor);
        }

        if (chart7Dual != null)
            chart7Dual.SetSeries(st.historyPopulation7Day, st.historySentiment7Day);

        float popAxis = PopulationGaugeAxisMax(pop, st.historyPopulation7Day);
        if (popStatText != null)
            popStatText.text =
                $"인구 {pop:N0}  (막대 100% 기준치 약 {Mathf.RoundToInt(popAxis):N0})";

        if (sentimentStatText != null)
            sentimentStatText.text = $"민심 {Mathf.RoundToInt(sentiment)} (기준 100, 범위 0-200)";

        int troopCap = Mathf.Max(1, master?.maxTroops ?? 1);
        int totalMil = dm.EstimateCastleTotalGarrisonTroops(_castleId);
        if (militaryStatText != null)
            militaryStatText.text = $"군사력 {totalMil:N0} / {troopCap:N0}";

        float intrinsic = dm.EvaluateBasePriceForCastle(_castleId);
        float baseVal = Mathf.Max(1f, master?.baseValue ?? 1f);
        float assetRatio = intrinsic / baseVal;
        float assetBarN = NormalizeAssetBar(assetRatio, dm, out var worldAssetHint);
        if (assetStatText != null)
        {
            int assetBarPct = Mathf.RoundToInt(Mathf.Clamp01(assetBarN) * 100f);
            assetStatText.text = worldAssetHint.Length > 0
                ? $"자산: 기준가 대비 내재가 {assetRatio:0.#}배입니다. {worldAssetHint} 초록 막대는 천하 분포에서의 상대 위치({assetBarPct}%)입니다."
                : $"자산: 기준가 대비 내재가 {assetRatio:0.#}배입니다. 초록 막대는 고정 눈금(2.5배=100%) 기준 {assetBarPct}%입니다.";
        }

        float popN = popAxis > 1e-4f ? Mathf.Clamp01(pop / popAxis) : 0f;
        float sentN = Mathf.Clamp01(0.5f + 0.5f * ((sentiment - 100f) / 100f));
        float milN = Mathf.Clamp01(totalMil / (float)troopCap);
        float assetN = Mathf.Clamp01(assetBarN);

        if (_playOpenGaugeAnim)
        {
            _playOpenGaugeAnim = false;
            PlayGaugesIntro(popN, sentN, milN, assetN);
        }
        else
            SetSliderGaugesImmediate(popN, sentN, milN, assetN);

        Color facCol = FactionAccentColor(lord);
        if (factionBandImage != null)
            factionBandImage.color = facCol;

        GeneralMasterData gen = null;
        if (!string.IsNullOrWhiteSpace(govId))
            gen = dm.GetGeneralMasterData(govId);

        string govName = "—";
        if (gen != null && !string.IsNullOrWhiteSpace(gen.name))
            govName = gen.name.Trim();
        if (governorNameText != null)
            governorNameText.text = $"태수 {govName}";

        dm.TryGetUserCastleStock(_castleId, out var stock);
        int troops = stock != null && stock.troopCount > 0 ? stock.troopCount : st.userDeployedTroops;
        float avg = stock != null && stock.troopCount > 0 ? stock.averagePurchasePrice : st.averagePurchasePrice;
        bool invested = troops > 0;
        if (portfolioText != null)
        {
            if (!invested)
                portfolioText.text = "내 병력 없음 · 투입해 세력에 편승하세요.";
            else if (dm.TryGetCastleRoiSellBasis(_castleId, out float roi))
                portfolioText.text =
                    $"보유 {troops:N0}명 · 평단 {Mathf.RoundToInt(avg):N0} G · 수익률 {(roi >= 0 ? "+" : "")}{roi:F1}%";
            else
                portfolioText.text = $"보유 {troops:N0}명 · 평단 {Mathf.RoundToInt(avg):N0} G";
        }

        if (portfolioBox != null)
            portfolioBox.gameObject.SetActive(true);

        if (recallButton != null)
            recallButton.interactable = invested;

        _deployMaxThisOpen = dm.ComputeMaxDeployTroopsForCastle(_castleId);
        if (deployButton != null)
            deployButton.interactable = _deployMaxThisOpen > 0;

        bool isHq = !string.IsNullOrWhiteSpace(dm.HomeCastleId)
                    && string.Equals(dm.HomeCastleId.Trim(), _castleId, StringComparison.Ordinal);
        bool travelInProgress = dm.HasPendingHqMove
                                || (WorldHqTravelHud.InstanceOrNull != null
                                    && WorldHqTravelHud.InstanceOrNull.IsHqTravelAnimating);
        string cid = (_castleId ?? "").Trim();
        bool pendingMoveToThisCastle = dm.HasPendingHqMove
                                       && string.Equals(dm.PendingHqMoveTargetId.Trim(), cid,
                                           StringComparison.Ordinal);
        float cost = isHq ? 0f : dm.CalculateStepCost(dm.HomeCastleId, _castleId);
        float gauge = dm.TravelGaugePoints;
        int syncedSteps = dm.PortfolioSyncedStepCount;
        bool canRelocate = !isHq && cost > 0f && cost < float.MaxValue * 0.25f;

        SetRelocateProgressBlockVisible(pendingMoveToThisCastle);
        if (pendingMoveToThisCastle && !isHq && cost > 0f && cost < float.MaxValue * 0.25f)
        {
            float dist = dm.GetDistance(dm.HomeCastleId, _castleId);
            ApplyFootprintTier(RelocationFootprintTier(dist));
        }
        else
            ApplyFootprintTier(0);

        if (relocateButton != null)
        {
            relocateButton.gameObject.SetActive(!isHq);
            relocateButton.interactable = canRelocate && !travelInProgress;
            var rLbl = relocateButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (rLbl != null)
                rLbl.text = "HQ 본영 이주";
        }

        if (relocateHintText != null)
        {
            if (!pendingMoveToThisCastle)
                relocateHintText.text = "";
            else if (cost >= float.MaxValue * 0.25f)
                relocateHintText.text = "거리를 계산할 수 없습니다.";
            else
                relocateHintText.text =
                    $"이주까지 {cost:N0}pt 필요 — 시간·걸음으로 게이지를 채우면 이동 완료 (현재 {gauge:N0} / {cost:N0}) · 만보기 누적 {syncedSteps:N0}보";
        }
    }

    /// <summary>이주 게이지·만보기 안내와 발자국 행은, 본영 이주 <b>목적지가 이 성</b>일 때만 표시합니다.</summary>
    void SetRelocateProgressBlockVisible(bool visible)
    {
        if (relocateHintText != null)
            relocateHintText.gameObject.SetActive(visible);
        Transform footprintRow = footprintIcon1 != null ? footprintIcon1.transform.parent : null;
        if (footprintRow != null)
            footprintRow.gameObject.SetActive(visible);
    }

    /// <summary>
    /// 인구 절대 상한은 없음. 슬라이더·문구의 「기준 최대」는 <b>현재 인구</b>와 <b>7일 이력</b> 중 최댓값에 8% 여유를 더한 값(최소 1)입니다.
    /// </summary>
    static float PopulationGaugeAxisMax(int currentPop, IReadOnlyList<float> history7)
    {
        float m = Mathf.Max(1f, currentPop);
        if (history7 != null)
        {
            for (int i = 0; i < history7.Count; i++)
                m = Mathf.Max(m, history7[i]);
        }

        return Mathf.Max(m * 1.08f, 1f);
    }

    static int RelocationFootprintTier(float mapDistance)
    {
        if (mapDistance < 0f || float.IsNaN(mapDistance)) return 1;
        if (mapDistance < 180f) return 1;
        if (mapDistance < 420f) return 2;
        return 3;
    }

    void ApplyFootprintTier(int tier)
    {
        if (footprintIcon1 != null) footprintIcon1.gameObject.SetActive(tier >= 1);
        if (footprintIcon2 != null) footprintIcon2.gameObject.SetActive(tier >= 2);
        if (footprintIcon3 != null) footprintIcon3.gameObject.SetActive(tier >= 3);
    }

    void SetSliderGaugesImmediate(float popN, float sentN, float milN, float assetN)
    {
        if (popGaugeSlider != null) popGaugeSlider.SetValueWithoutNotify(popN);
        if (sentimentGaugeSlider != null) sentimentGaugeSlider.SetValueWithoutNotify(sentN);
        if (militaryGaugeSlider != null) militaryGaugeSlider.SetValueWithoutNotify(milN);
        if (assetGaugeSlider != null) assetGaugeSlider.SetValueWithoutNotify(assetN);
    }

    void PlayGaugesIntro(float popN, float sentN, float milN, float assetN)
    {
        DOTween.Kill(GaugeTweenId);
        SetSliderGaugesImmediate(0f, 0f, 0f, 0f);
        var seq = DOTween.Sequence().SetId(GaugeTweenId);
        if (popGaugeSlider != null)
            seq.Join(DOTween.To(() => popGaugeSlider.value, x => popGaugeSlider.SetValueWithoutNotify(x), popN, 0.52f)
                .SetEase(Ease.OutCubic).SetId(GaugeTweenId));
        if (sentimentGaugeSlider != null)
            seq.Join(DOTween.To(() => sentimentGaugeSlider.value, x => sentimentGaugeSlider.SetValueWithoutNotify(x), sentN, 0.52f)
                .SetEase(Ease.OutCubic).SetId(GaugeTweenId));
        if (militaryGaugeSlider != null)
            seq.Join(DOTween.To(() => militaryGaugeSlider.value, x => militaryGaugeSlider.SetValueWithoutNotify(x), milN, 0.52f)
                .SetEase(Ease.OutCubic).SetId(GaugeTweenId));
        if (assetGaugeSlider != null)
            seq.Join(DOTween.To(() => assetGaugeSlider.value, x => assetGaugeSlider.SetValueWithoutNotify(x), assetN, 0.52f)
                .SetEase(Ease.OutCubic).SetId(GaugeTweenId));
    }

    static Color FactionAccentColor(Faction f)
    {
        switch (f)
        {
            case Faction.WEI: return new Color(0.85f, 0.32f, 0.28f, 1f);
            case Faction.SHU: return new Color(0.35f, 0.72f, 0.42f, 1f);
            case Faction.WU: return new Color(0.35f, 0.52f, 0.92f, 1f);
            case Faction.OTHERS: return new Color(0.75f, 0.62f, 0.38f, 1f);
            default: return new Color(0.55f, 0.58f, 0.64f, 1f);
        }
    }

    /// <summary>
    /// 자산 막대: 천하 성들의 (내재가/기준가) 최저~최고로 정규화. 분포가 너무 좁으면 평균 대비(2×평균=막대 꽉 참)로 대체.
    /// </summary>
    static float NormalizeAssetBar(float assetRatio, DataManager dm, out string worldHint)
    {
        worldHint = "";
        if (dm == null || !dm.TryGetWorldCastleAssetRatioStats(out float minR, out float maxR, out float meanR, out int count))
            return Mathf.Clamp01(assetRatio / 2.5f);

        worldHint = $"천하 {count}개 성 평균 {meanR:0.#}배(최저 {minR:0.#}~최고 {maxR:0.#}).";

        float spread = maxR - minR;
        float relSpread = meanR > 1e-3f ? spread / meanR : spread;
        if (count >= 2 && spread > 1e-6f && relSpread > 0.015f)
            return Mathf.Clamp01((assetRatio - minR) / spread);

        float denom = Mathf.Max(meanR * 2f, 0.01f);
        return Mathf.Clamp01(assetRatio / denom);
    }

    void SyncFactionSubtitleSwatchSize()
    {
        if (factionSubtitleSwatchImage == null) return;
        float fs = 17f;
        if (headerSubtitleText != null)
            fs = headerSubtitleText.fontSize;
        else if (headerSubtitlePrefixText != null)
            fs = headerSubtitlePrefixText.fontSize;
        var le = factionSubtitleSwatchImage.GetComponent<LayoutElement>();
        if (le == null) return;
        le.minWidth = le.preferredWidth = fs;
        le.minHeight = le.preferredHeight = fs;
    }

    void OnDeployOpen()
    {
        if (deployDialogRoot == null || deploySlider == null) return;
        if (_deployMaxThisOpen <= 0) return;
        deployDialogRoot.SetAsLastSibling();
        deployDialogRoot.gameObject.SetActive(true);
        deploySlider.wholeNumbers = true;
        deploySlider.minValue = 1;
        deploySlider.maxValue = Mathf.Max(1, _deployMaxThisOpen);
        deploySlider.SetValueWithoutNotify(Mathf.Max(1, _deployMaxThisOpen / 2));
        deploySlider.onValueChanged.RemoveListener(OnDeploySlider);
        deploySlider.onValueChanged.AddListener(OnDeploySlider);
        OnDeploySlider(deploySlider.value);
    }

    void OnDeploySlider(float v)
    {
        if (deploySliderValueText == null) return;
        int n = Mathf.RoundToInt(v);
        var dm = DataManager.InstanceOrNull;
        float buy = dm != null && !string.IsNullOrWhiteSpace(_castleId)
            ? dm.EvaluateBuyPriceForCastle(_castleId)
            : 0f;
        long cost = (long)Mathf.RoundToInt(buy) * n;
        deploySliderValueText.text = cost > 0L
            ? $"투입 병력: {n:N0}명 · 비용 {cost:N0} G"
            : $"투입 병력: {n:N0}명";
    }

    void OnDeployConfirm()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null || string.IsNullOrWhiteSpace(_castleId) || deploySlider == null) return;
        if (!dm.castleStateDataMap.TryGetValue(_castleId, out var st) || st == null) return;
        int n = Mathf.RoundToInt(deploySlider.value);
        if (n <= 0) return;
        dm.AddUserCastleDeployment(_castleId, n, dm.EvaluateBuyPriceForCastle(_castleId));
        deployDialogRoot.gameObject.SetActive(false);
        Refresh();
    }

    void OnDeployCancel()
    {
        if (deployDialogRoot != null)
            deployDialogRoot.gameObject.SetActive(false);
    }

    void OnRecallClicked()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null || string.IsNullOrWhiteSpace(_castleId)) return;
        dm.RecallUserCastleDeployment(_castleId);
        Refresh();
    }

    void OnRelocateClicked()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null || string.IsNullOrWhiteSpace(_castleId)) return;
        if (!dm.TryValidateHqMove(_castleId, out _, out var relocateErr))
        {
            Debug.LogWarning("[CastleDetailPopup] 본영 이주 불가: " + relocateErr);
            return;
        }

        bool ensuredHud = false;
        Transform wm = transform;
        for (int i = 0; i < 10 && wm != null; i++, wm = wm.parent)
        {
            if (wm.name != "WorldMarketRoot")
                continue;
            WorldHqTravelHud.EnsureUnderWorldMarketRoot(wm);
            ensuredHud = true;
            break;
        }

        if (!ensuredHud)
            Debug.LogWarning("[CastleDetailPopup] WorldMarketRoot를 찾지 못했습니다.");

        if (WorldHqTravelHud.InstanceOrNull == null)
        {
            Debug.LogWarning("[CastleDetailPopup] WorldHqTravelHud가 없습니다.");
            return;
        }

        WorldHqTravelHud.InstanceOrNull.TryBeginTravelTo(_castleId);
        Refresh();
    }

    void BuildUiIfNeeded()
    {
        if (panelRoot != null) return;

        var canvasRt = transform as RectTransform;
        if (canvasRt == null) return;

        rootCanvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (rootCanvasGroup == null)
            rootCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image), typeof(Button));
        dim.transform.SetParent(transform, false);
        StretchFull(dim.GetComponent<RectTransform>());
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.58f);
        dimCloseButton = dim.GetComponent<Button>();
        dimCloseButton.transition = Selectable.Transition.None;

        var panel = new GameObject("DetailPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        panel.transform.SetParent(transform, false);
        var pRt = panel.GetComponent<RectTransform>();
        pRt.anchorMin = new Vector2(0.04f, 0.04f);
        pRt.anchorMax = new Vector2(0.96f, 0.95f);
        pRt.offsetMin = Vector2.zero;
        pRt.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.09f, 0.10f, 0.13f, 0.99f);
        panelRoot = pRt;
        var pv = panel.GetComponent<VerticalLayoutGroup>();
        pv.padding = new RectOffset(16, 16, 12, 12);
        pv.spacing = 10;
        pv.childControlWidth = true;
        pv.childControlHeight = true;
        pv.childForceExpandWidth = true;
        pv.childForceExpandHeight = false;

        governorPortraitFrameImage = null;
        governorPortraitImage = null;
        governorInitialText = null;

        var headerCard = new GameObject("HeaderCard", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        headerCard.transform.SetParent(panel.transform, false);
        var headerCardImg = headerCard.GetComponent<Image>();
        headerCardImg.color = new Color(0.05f, 0.10f, 0.17f, 0.97f);
        headerCardImg.raycastTarget = false;
        var headerCardLe = headerCard.GetComponent<LayoutElement>();
        headerCardLe.minHeight = 168f;
        headerCardLe.preferredHeight = 176f;
        headerCardLe.flexibleHeight = 0f;
        var hcv = headerCard.GetComponent<VerticalLayoutGroup>();
        hcv.padding = new RectOffset(14, 14, 12, 12);
        hcv.spacing = 12;
        hcv.childAlignment = TextAnchor.UpperLeft;
        hcv.childControlWidth = true;
        hcv.childControlHeight = true;
        hcv.childForceExpandWidth = true;

        var header = new GameObject("Header", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        header.transform.SetParent(headerCard.transform, false);
        var headerLe = header.GetComponent<LayoutElement>();
        headerLe.minHeight = 56f;
        headerLe.preferredHeight = 60f;
        var hh = header.GetComponent<HorizontalLayoutGroup>();
        hh.spacing = 12;
        hh.childAlignment = TextAnchor.MiddleLeft;
        hh.childControlWidth = true;
        hh.childControlHeight = true;
        hh.childForceExpandWidth = false;
        hh.childForceExpandHeight = true;

        var titleCol = new GameObject("TitleCol", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        titleCol.transform.SetParent(header.transform, false);
        titleCol.GetComponent<LayoutElement>().flexibleWidth = 1f;
        titleCol.GetComponent<LayoutElement>().minWidth = 120f;
        var tvg = titleCol.GetComponent<VerticalLayoutGroup>();
        tvg.childAlignment = TextAnchor.UpperLeft;
        tvg.spacing = 4;
        tvg.childControlWidth = true;
        tvg.childForceExpandWidth = true;

        headerTitleText = CreateTmp(titleCol.transform, "Title", "", 24, FontStyles.Bold, TextAlignmentOptions.Left);
        headerTitleText.color = Color.white;

        const float subFont = 17f;
        var subRow = new GameObject("SubTitleRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        subRow.transform.SetParent(titleCol.transform, false);
        var subH = subRow.GetComponent<HorizontalLayoutGroup>();
        subH.childAlignment = TextAnchor.MiddleLeft;
        subH.spacing = 6f;
        subH.padding = new RectOffset(0, 0, 0, 0);
        subH.childControlWidth = true;
        subH.childControlHeight = true;
        subH.childForceExpandWidth = false;
        subH.childForceExpandHeight = false;
        subRow.GetComponent<LayoutElement>().flexibleWidth = 1f;

        headerSubtitlePrefixText = CreateTmp(subRow.transform, "SubPrefix", "", subFont, FontStyles.Normal, TextAlignmentOptions.Left);
        headerSubtitlePrefixText.color = new Color(0.68f, 0.72f, 0.78f, 1f);
        var prefixCsf = headerSubtitlePrefixText.gameObject.AddComponent<ContentSizeFitter>();
        prefixCsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        prefixCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var prefixLe = headerSubtitlePrefixText.GetComponent<LayoutElement>();
        prefixLe.flexibleWidth = 0f;

        var swatchGo = new GameObject("FactionSwatch", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        swatchGo.transform.SetParent(subRow.transform, false);
        factionSubtitleSwatchImage = swatchGo.GetComponent<Image>();
        factionSubtitleSwatchImage.sprite = WorldMarketPieChartUI.GetSquareUiSprite();
        factionSubtitleSwatchImage.type = Image.Type.Simple;
        factionSubtitleSwatchImage.raycastTarget = false;
        var swLe = swatchGo.GetComponent<LayoutElement>();
        swLe.flexibleWidth = 0f;
        swLe.flexibleHeight = 0f;
        swLe.minWidth = swLe.preferredWidth = subFont;
        swLe.minHeight = swLe.preferredHeight = subFont;

        headerSubtitleText = CreateTmp(subRow.transform, "SubFaction", "", subFont, FontStyles.Normal, TextAlignmentOptions.Left);
        headerSubtitleText.color = new Color(0.68f, 0.72f, 0.78f, 1f);
        var facLe = headerSubtitleText.GetComponent<LayoutElement>();
        facLe.flexibleWidth = 1f;
        facLe.minWidth = 40f;

        var govWrap = new GameObject("GovernorHeaderBox", typeof(RectTransform), typeof(LayoutElement));
        govWrap.transform.SetParent(header.transform, false);
        var gwLe = govWrap.GetComponent<LayoutElement>();
        gwLe.minWidth = 200f;
        gwLe.preferredWidth = 268f;
        gwLe.flexibleWidth = 0f;
        gwLe.minHeight = 56f;

        var govBg = new GameObject("GovBg", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        govBg.transform.SetParent(govWrap.transform, false);
        StretchFull(govBg.GetComponent<RectTransform>());
        govBg.GetComponent<Image>().color = new Color(0.07f, 0.09f, 0.13f, 1f);
        var gvg = govBg.GetComponent<VerticalLayoutGroup>();
        gvg.padding = new RectOffset(12, 12, 10, 10);
        gvg.spacing = 8;
        gvg.childAlignment = TextAnchor.UpperLeft;
        gvg.childControlWidth = true;
        gvg.childForceExpandWidth = true;

        var bandGo = new GameObject("FactionBand", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        bandGo.transform.SetParent(govBg.transform, false);
        factionBandImage = bandGo.GetComponent<Image>();
        var bandLe = bandGo.GetComponent<LayoutElement>();
        bandLe.minHeight = 5f;
        bandLe.preferredHeight = 5f;
        bandLe.flexibleWidth = 1f;
        StretchFull(bandGo.GetComponent<RectTransform>());

        governorFactionText = null;

        governorNameText = CreateTmp(govBg.transform, "GovName", "태수 —", 19, FontStyles.Bold, TextAlignmentOptions.Left);
        governorNameText.color = Color.white;

        closeButton = null;

        var priceRow = new GameObject("PriceRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        priceRow.transform.SetParent(headerCard.transform, false);
        priceRow.GetComponent<LayoutElement>().minHeight = 64f;
        var prh = priceRow.GetComponent<HorizontalLayoutGroup>();
        prh.spacing = 8;
        prh.childAlignment = TextAnchor.MiddleLeft;
        prh.childControlWidth = true;
        prh.childForceExpandWidth = true;
        prh.padding = new RectOffset(0, 0, 4, 0);

        var buyStack = new GameObject("BuyStack", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        buyStack.transform.SetParent(priceRow.transform, false);
        buyStack.GetComponent<LayoutElement>().flexibleWidth = 1f;
        var bsv = buyStack.GetComponent<VerticalLayoutGroup>();
        bsv.spacing = 2;
        bsv.childAlignment = TextAnchor.UpperLeft;
        bsv.childControlWidth = true;
        bsv.childForceExpandWidth = true;

        buyCaptionText = CreateTmp(buyStack.transform, "BuyCap", "입성비", 19, FontStyles.Bold, TextAlignmentOptions.Left);
        buyCaptionText.color = new Color(0.72f, 0.78f, 0.88f, 1f);
        buyPriceBigText = CreateTmp(buyStack.transform, "BuyBig", "0 G", 34, FontStyles.Bold, TextAlignmentOptions.Left);
        buyPriceBigText.color = Color.white;

        var sellStack = new GameObject("SellStack", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        sellStack.transform.SetParent(priceRow.transform, false);
        sellStack.GetComponent<LayoutElement>().flexibleWidth = 1f;
        var ssv = sellStack.GetComponent<VerticalLayoutGroup>();
        ssv.spacing = 2;
        ssv.childAlignment = TextAnchor.UpperRight;
        ssv.childControlWidth = true;
        ssv.childForceExpandWidth = true;

        sellCaptionText = CreateTmp(sellStack.transform, "SellCap", "퇴성비", 19, FontStyles.Bold, TextAlignmentOptions.TopRight);
        sellCaptionText.color = new Color(0.72f, 0.78f, 0.88f, 1f);
        sellPriceText = CreateTmp(sellStack.transform, "SellVal", "0 G", 34, FontStyles.Bold, TextAlignmentOptions.TopRight);
        sellPriceText.color = new Color(0.88f, 0.90f, 0.94f, 1f);

        changePctText = CreateTmp(headerCard.transform, "Chg", "+0.00%", 22, FontStyles.Bold, TextAlignmentOptions.Left);
        changePctText.color = new Color(0.7f, 0.72f, 0.76f, 1f);

        var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(LayoutElement));
        scrollGo.transform.SetParent(panel.transform, false);
        scrollGo.GetComponent<Image>().color = new Color(0.07f, 0.08f, 0.10f, 0.5f);
        var scrollLe = scrollGo.GetComponent<LayoutElement>();
        scrollLe.flexibleHeight = 1f;
        scrollLe.flexibleWidth = 1f;
        scrollLe.minHeight = 400f;
        scrollLe.preferredHeight = 460f;
        var scroll = scrollGo.GetComponent<ScrollRect>();
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollGo.transform, false);
        var vpRt = viewport.GetComponent<RectTransform>();
        StretchFull(vpRt);
        viewport.GetComponent<Image>().color = Color.white;
        viewport.GetComponent<Mask>().showMaskGraphic = false;
        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var cRt = content.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0f, 1f);
        cRt.anchorMax = new Vector2(1f, 1f);
        cRt.pivot = new Vector2(0.5f, 1f);
        cRt.sizeDelta = new Vector2(0f, 0f);
        var cv = content.GetComponent<VerticalLayoutGroup>();
        cv.spacing = 14;
        cv.childControlWidth = true;
        cv.childForceExpandWidth = true;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = vpRt;
        scroll.content = cRt;
        scroll.vertical = true;
        scroll.horizontal = false;

        var chartHost = new GameObject("ChartHost", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        chartHost.transform.SetParent(content.transform, false);
        chartHost.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.92f);
        var chartLe = chartHost.GetComponent<LayoutElement>();
        chartLe.minHeight = 248f;
        chartLe.preferredHeight = 264f;
        chartLe.flexibleHeight = 0f;
        var chartGo = new GameObject("PopSentChart", typeof(RectTransform), typeof(UIPopSentiment7DayChart));
        chartGo.transform.SetParent(chartHost.transform, false);
        StretchFull(chartGo.GetComponent<RectTransform>());
        chart7Dual = chartGo.GetComponent<UIPopSentiment7DayChart>();

        popStatText = CreateTmp(content.transform, "PopLine", "인구", 25, FontStyles.Normal, TextAlignmentOptions.Left);
        popStatText.color = new Color(0.78f, 0.8f, 0.84f);
        popGaugeSlider = CreateReadOnlySliderGauge(content.transform, "PopGauge",
            new Color(0.55f, 0.32f, 0.28f, 0.95f));

        sentimentStatText = CreateTmp(content.transform, "SentLine", "민심", 25, FontStyles.Normal, TextAlignmentOptions.Left);
        sentimentStatText.color = new Color(0.78f, 0.8f, 0.84f);
        sentimentGaugeSlider = CreateReadOnlySliderGauge(content.transform, "SentGauge",
            new Color(0.92f, 0.78f, 0.35f, 0.95f));

        militaryStatText = CreateTmp(content.transform, "MilLine", "군사력", 25, FontStyles.Normal, TextAlignmentOptions.Left);
        militaryStatText.color = new Color(0.78f, 0.8f, 0.84f);
        militaryGaugeSlider = CreateReadOnlySliderGauge(content.transform, "MilGauge",
            new Color(0.32f, 0.52f, 0.88f, 0.95f));

        assetStatText = CreateTmp(content.transform, "AssetLine", "자산", 25, FontStyles.Normal, TextAlignmentOptions.Left);
        assetStatText.color = new Color(0.78f, 0.8f, 0.84f);
        assetGaugeSlider = CreateReadOnlySliderGauge(content.transform, "AssetGauge",
            new Color(0.28f, 0.62f, 0.42f, 0.95f));

        var pfGo = new GameObject("PortfolioBox", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        pfGo.transform.SetParent(content.transform, false);
        portfolioBox = pfGo.GetComponent<RectTransform>();
        portfolioBox.GetComponent<Image>().color = new Color(0.14f, 0.12f, 0.10f, 0.95f);
        portfolioBox.GetComponent<LayoutElement>().minHeight = 100f;
        portfolioText = CreateTmp(portfolioBox.transform, "PfText", "", 26, FontStyles.Normal, TextAlignmentOptions.Left);
        StretchWithPadding(portfolioText.rectTransform, 12f);

        var footer = new GameObject("Footer", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        footer.transform.SetParent(panel.transform, false);
        var footLe = footer.GetComponent<LayoutElement>();
        footLe.minHeight = 268f;
        footLe.preferredHeight = 276f;
        footLe.flexibleHeight = 0f;
        var fv = footer.GetComponent<VerticalLayoutGroup>();
        fv.spacing = 10;
        fv.childControlWidth = true;
        fv.childForceExpandWidth = true;

        var fpRow = new GameObject("Footprints", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        fpRow.transform.SetParent(footer.transform, false);
        fpRow.GetComponent<LayoutElement>().minHeight = 36f;
        var fph = fpRow.GetComponent<HorizontalLayoutGroup>();
        fph.spacing = 8;
        fph.childAlignment = TextAnchor.MiddleCenter;
        fph.childControlWidth = false;
        fph.childForceExpandWidth = false;
        footprintIcon1 = CreateTmp(fpRow.transform, "F1", "\u00B7", 30, FontStyles.Bold, TextAlignmentOptions.Center);
        footprintIcon2 = CreateTmp(fpRow.transform, "F2", "\u00B7\u00B7", 30, FontStyles.Bold, TextAlignmentOptions.Center);
        footprintIcon3 = CreateTmp(fpRow.transform, "F3", "\u00B7\u00B7\u00B7", 30, FontStyles.Bold, TextAlignmentOptions.Center);
        footprintIcon1.color = new Color(0.85f, 0.78f, 0.55f, 1f);
        footprintIcon2.color = footprintIcon1.color;
        footprintIcon3.color = footprintIcon1.color;
        ApplyFootprintTier(0);

        var hintGo = new GameObject("RelocateHint", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        hintGo.transform.SetParent(footer.transform, false);
        relocateHintText = hintGo.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            relocateHintText.font = TMP_Settings.defaultFontAsset;
        relocateHintText.fontSize = 24;
        relocateHintText.fontStyle = FontStyles.Normal;
        relocateHintText.alignment = TextAlignmentOptions.Center;
        relocateHintText.color = new Color(0.78f, 0.82f, 0.88f, 1f);
        relocateHintText.enableWordWrapping = true;
        relocateHintText.overflowMode = TextOverflowModes.Overflow;
        var hintLe = hintGo.GetComponent<LayoutElement>();
        hintLe.minHeight = 88f;
        hintLe.preferredHeight = 96f;
        hintLe.flexibleWidth = 1f;
        var hintRt = hintGo.GetComponent<RectTransform>();
        hintRt.sizeDelta = new Vector2(0f, 96f);

        var btnRow = new GameObject("BtnRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        btnRow.transform.SetParent(footer.transform, false);
        btnRow.GetComponent<LayoutElement>().minHeight = 52f;
        var bh = btnRow.GetComponent<HorizontalLayoutGroup>();
        bh.spacing = 8;
        bh.childControlWidth = true;
        bh.childForceExpandWidth = true;
        relocateButton = CreateFooterBtn(btnRow.transform, "HQ 본영 이주", new Color(0.22f, 0.38f, 0.62f));
        {
            var cb = relocateButton.colors;
            cb.disabledColor = new Color(0.45f, 0.46f, 0.48f, 0.55f);
            relocateButton.colors = cb;
        }

        deployButton = CreateFooterBtn(btnRow.transform, "병사 투입", new Color(0.16f, 0.48f, 0.32f));
        recallButton = CreateFooterBtn(btnRow.transform, "병사 회군", new Color(0.82f, 0.42f, 0.22f));

        var okGo = new GameObject("HwagInClose", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        okGo.transform.SetParent(footer.transform, false);
        okGo.GetComponent<Image>().color = new Color(0.28f, 0.22f, 0.16f, 0.98f);
        okGo.GetComponent<LayoutElement>().minHeight = 50f;
        okGo.GetComponent<LayoutElement>().preferredHeight = 50f;
        confirmCloseButton = okGo.GetComponent<Button>();
        var okTmp = CreateTmp(okGo.transform, "Lbl", "확인 / 닫기", 22, FontStyles.Bold, TextAlignmentOptions.Center);
        okTmp.color = new Color(0.95f, 0.92f, 0.85f, 1f);
        StretchFull(okTmp.rectTransform);

        BuildDeployDialog(transform);
    }

    void BuildDeployDialog(Transform root)
    {
        deployDialogRoot = new GameObject("DeployDialog", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        deployDialogRoot.SetParent(root, false);
        StretchFull(deployDialogRoot);
        deployDialogRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
        deployDialogRoot.gameObject.SetActive(false);

        var box = new GameObject("Box", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        box.transform.SetParent(deployDialogRoot, false);
        var boxRt = box.GetComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(300f, 200f);
        boxRt.anchoredPosition = Vector2.zero;
        box.GetComponent<Image>().color = new Color(0.12f, 0.13f, 0.17f, 0.99f);
        var bv = box.GetComponent<VerticalLayoutGroup>();
        bv.padding = new RectOffset(16, 16, 16, 16);
        bv.spacing = 12;
        bv.childControlWidth = true;
        deploySliderValueText = CreateTmp(box.transform, "SliderLabel", "투입 병력: 0", 24, FontStyles.Bold, TextAlignmentOptions.Center);

        var sgo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sgo.transform.SetParent(box.transform, false);
        sgo.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 34f);
        deploySlider = sgo.GetComponent<Slider>();
        deploySlider.minValue = 1;
        deploySlider.maxValue = 100;
        deploySlider.wholeNumbers = true;
        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(sgo.transform, false);
        StretchFull(bg.GetComponent<RectTransform>());
        bg.GetComponent<Image>().color = new Color(0.2f, 0.22f, 0.28f, 1f);
        var fillA = new GameObject("Fill Area", typeof(RectTransform));
        fillA.transform.SetParent(sgo.transform, false);
        StretchFull(fillA.GetComponent<RectTransform>());
        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillA.transform, false);
        var fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        var fillImg = fill.GetComponent<Image>();
        fillImg.color = new Color(0.28f, 0.55f, 0.38f, 1f);
        deploySlider.fillRect = fillRt;

        var handleSlide = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleSlide.transform.SetParent(sgo.transform, false);
        StretchFull(handleSlide.GetComponent<RectTransform>());
        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(handleSlide.transform, false);
        var hRt = handle.GetComponent<RectTransform>();
        hRt.sizeDelta = new Vector2(18f, 22f);
        hRt.anchorMin = new Vector2(0f, 0.5f);
        hRt.anchorMax = new Vector2(0f, 0.5f);
        hRt.pivot = new Vector2(0.5f, 0.5f);
        var hImg = handle.GetComponent<Image>();
        hImg.color = new Color(0.92f, 0.93f, 0.96f, 1f);
        deploySlider.handleRect = hRt;
        deploySlider.targetGraphic = hImg;

        var hBtn = new GameObject("BtnRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        hBtn.transform.SetParent(box.transform, false);
        hBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 40f);
        var hhg = hBtn.GetComponent<HorizontalLayoutGroup>();
        hhg.spacing = 12;
        hhg.childControlWidth = true;
        hhg.childForceExpandWidth = true;
        deployCancelButton = CreateFooterBtn(hBtn.transform, "취소", new Color(0.35f, 0.36f, 0.4f));
        deployConfirmButton = CreateFooterBtn(hBtn.transform, "확인", new Color(0.22f, 0.45f, 0.62f));
    }

    static TextMeshProUGUI CreateTmp(Transform parent, string name, string text, float size, FontStyles fs,
        TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = fs;
        tmp.alignment = align;
        tmp.color = Color.white;
        return tmp;
    }

    static Slider CreateReadOnlySliderGauge(Transform parent, string name, Color fillCol)
    {
        var row = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<LayoutElement>().minHeight = 28f;
        row.GetComponent<LayoutElement>().preferredHeight = 34f;
        var sgo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sgo.transform.SetParent(row.transform, false);
        StretchFull(sgo.GetComponent<RectTransform>());
        var slider = sgo.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;

        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(sgo.transform, false);
        StretchFull(bg.GetComponent<RectTransform>());
        bg.GetComponent<Image>().color = new Color(0.18f, 0.19f, 0.22f, 0.95f);

        var fillA = new GameObject("Fill Area", typeof(RectTransform));
        fillA.transform.SetParent(sgo.transform, false);
        StretchFull(fillA.GetComponent<RectTransform>());
        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillA.transform, false);
        var fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        var fillImg = fill.GetComponent<Image>();
        fillImg.color = fillCol;
        slider.fillRect = fillRt;

        var handleSlide = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleSlide.transform.SetParent(sgo.transform, false);
        handleSlide.SetActive(false);

        return slider;
    }

    static Button CreateFooterBtn(Transform parent, string label, Color bg)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = bg;
        var le = go.GetComponent<LayoutElement>();
        le.minHeight = 48f;
        le.preferredHeight = 48f;
        le.flexibleWidth = 1f;
        var btn = go.GetComponent<Button>();
        var tmp = CreateTmp(go.transform, "Lbl", label, 20, FontStyles.Bold, TextAlignmentOptions.Center);
        tmp.color = Color.white;
        StretchFull(tmp.rectTransform);
        return btn;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void StretchWithPadding(RectTransform rt, float p)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(p, p);
        rt.offsetMax = new Vector2(-p, -p);
    }

    /// <summary><see cref="WorldMarketRoot"/> 최상위에 풀스크린 오버레이로 한 번만 붙입니다.</summary>
    public static void EnsureUnderWorldMarketRoot(Transform worldMarketRoot)
    {
        if (worldMarketRoot == null) return;
        if (worldMarketRoot.GetComponentInChildren<WorldMarketCastleDetailPopup>(true) != null)
            return;

        var go = new GameObject("CastleDetailPopup", typeof(RectTransform), typeof(LayoutElement), typeof(WorldMarketCastleDetailPopup));
        var rt = go.GetComponent<RectTransform>();
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        go.transform.SetParent(worldMarketRoot, false);
        go.transform.SetAsLastSibling();
        StretchFull(rt);
    }
}
