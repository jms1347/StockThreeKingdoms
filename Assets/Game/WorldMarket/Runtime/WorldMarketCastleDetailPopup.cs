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
    [SerializeField] TextMeshProUGUI buyPriceBigText;
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
            headerTitleText.text = $"{disp}  [{g}]";

        if (buyPriceBigText != null)
            buyPriceBigText.text = $"{Mathf.RoundToInt(buy):N0} G";
        if (sellPriceText != null)
            sellPriceText.text = $"매도 {Mathf.RoundToInt(sell):N0} G";

        float pct = dm.CalculateChangeRate24h(st);
        if (changePctText != null)
        {
            bool up = pct > 0.001f;
            bool flat = Mathf.Abs(pct) < 0.02f;
            changePctText.text = flat ? "0.00%" : $"{(up ? "+" : "")}{pct:F2}%";
            changePctText.color = flat ? new Color(0.7f, 0.72f, 0.76f) : (up ? RiseColor : FallColor);
        }

        int maxCap = Mathf.Max(1, master?.maxTroops ?? 1);
        if (chart7Dual != null)
            chart7Dual.SetSeries(st.historyPopulation7Day, st.historySentiment7Day, maxCap);

        if (popStatText != null)
            popStatText.text = maxCap > 0
                ? $"인구 {pop:N0} / 수용 {maxCap:N0}"
                : $"인구 {pop:N0}";

        if (sentimentStatText != null)
            sentimentStatText.text = $"민심 {sentiment:0.#} / 100";

        int totalMil = dm.EstimateCastleTotalGarrisonTroops(_castleId);
        if (militaryStatText != null)
            militaryStatText.text = $"군사력 {totalMil:N0} / {maxCap:N0}";

        float intrinsic = dm.EvaluateBasePriceForCastle(_castleId);
        float baseVal = Mathf.Max(1f, master?.baseValue ?? 1f);
        float assetRatio = intrinsic / baseVal;
        if (assetStatText != null)
            assetStatText.text = $"자산 위치 약 {Mathf.Clamp(assetRatio / 2.5f, 0f, 1f) * 100f:0}% (내재/액면 {assetRatio:0.##}×)";

        float popN = maxCap > 0 ? Mathf.Clamp01(pop / (float)maxCap) : 0f;
        float sentN = Mathf.Clamp01(sentiment / 100f);
        float milN = maxCap > 0 ? Mathf.Clamp01(totalMil / (float)maxCap) : 0f;
        float assetN = Mathf.Clamp01(assetRatio / 2.5f);

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
        Faction govFac = gen != null ? GovernorFactionFromGeneral(gen) : Faction.NONE;
        Color frameCol = govFac != Faction.NONE ? FactionAccentColor(govFac) : facCol;
        if (governorPortraitFrameImage != null)
            governorPortraitFrameImage.color = Color.Lerp(frameCol, new Color(0.08f, 0.09f, 0.12f, 1f), 0.35f);

        string govName = "—";
        if (gen != null && !string.IsNullOrWhiteSpace(gen.name))
            govName = gen.name.Trim();
        if (governorNameText != null)
            governorNameText.text = $"태수 {govName}";
        if (governorFactionText != null)
            governorFactionText.text = DataManager.GetFactionLordShortLabel(lord);
        bool hasPortraitSprite = gen != null && gen.governorPortrait != null;
        if (governorPortraitImage != null)
        {
            if (hasPortraitSprite)
            {
                governorPortraitImage.sprite = gen.governorPortrait;
                governorPortraitImage.color = Color.white;
            }
            else
            {
                governorPortraitImage.sprite = null;
                governorPortraitImage.color = Color.Lerp(facCol, new Color(0.12f, 0.13f, 0.16f), 0.55f);
            }
        }

        if (governorInitialText != null)
        {
            governorInitialText.gameObject.SetActive(!hasPortraitSprite);
            governorInitialText.text = string.IsNullOrWhiteSpace(govName) || govName == "—"
                ? "?"
                : govName.Substring(0, Math.Min(1, govName.Length));
        }

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

        _deployMaxThisOpen = ComputeQuickDeployTroops(st, master);
        if (deployButton != null)
            deployButton.interactable = _deployMaxThisOpen > 0;

        bool isHq = !string.IsNullOrWhiteSpace(dm.HomeCastleId)
                    && string.Equals(dm.HomeCastleId.Trim(), _castleId, StringComparison.Ordinal);
        bool travelInProgress = dm.HasPendingHqMove
                                || (WorldHqTravelHud.InstanceOrNull != null
                                    && WorldHqTravelHud.InstanceOrNull.IsHqTravelAnimating);
        float cost = isHq ? 0f : dm.CalculateStepCost(dm.HomeCastleId, _castleId);
        float gauge = dm.TravelGaugePoints;
        int syncedSteps = dm.PortfolioSyncedStepCount;
        int stepEq = dm.GetTravelCostStepEquivalent(cost);
        bool canRelocate = !isHq && cost > 0f && cost < float.MaxValue * 0.25f;

        if (!isHq && cost > 0f && cost < float.MaxValue * 0.25f)
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
            if (travelInProgress)
                relocateHintText.text = "본영 이주 중입니다…";
            else if (isHq)
                relocateHintText.text = "";
            else if (cost >= float.MaxValue * 0.25f)
                relocateHintText.text = "거리를 계산할 수 없습니다.";
            else
                relocateHintText.text =
                    $"이주까지 {cost:N0}pt 필요 — 시간·걸음으로 게이지를 채우면 이동 완료 (현재 {gauge:N0} / {cost:N0}) · 만보기 누적 {syncedSteps:N0}보";
        }
    }

    static Faction GovernorFactionFromGeneral(GeneralMasterData gen)
    {
        if (gen == null || string.IsNullOrWhiteSpace(gen.initialNationId)) return Faction.NONE;
        string raw = gen.initialNationId.Trim();
        if (int.TryParse(raw, out int n) && Enum.IsDefined(typeof(Faction), n))
            return (Faction)n;
        if (Enum.TryParse(raw, true, out Faction f))
            return f;
        return Faction.NONE;
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

    static int ComputeQuickDeployTroops(CastleStateData st, CastleMasterData master)
    {
        if (st == null) return 0;
        int cap = master != null ? master.maxTroops : 5000;
        int v = Mathf.Max(1, Mathf.RoundToInt(cap * 0.10f));
        return Mathf.Clamp(v, 1, Mathf.Max(1, cap - st.userDeployedTroops));
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

    void OnDeployOpen()
    {
        if (deployDialogRoot == null || deploySlider == null) return;
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
        if (deploySliderValueText != null)
            deploySliderValueText.text = $"투입 병력: {Mathf.RoundToInt(v):N0}명";
    }

    void OnDeployConfirm()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null || string.IsNullOrWhiteSpace(_castleId) || deploySlider == null) return;
        if (!dm.castleStateDataMap.TryGetValue(_castleId, out var st) || st == null) return;
        int n = Mathf.RoundToInt(deploySlider.value);
        if (n <= 0) return;
        dm.AddUserCastleDeployment(_castleId, n, st.currentBuyPrice);
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
        pRt.anchorMin = new Vector2(0.06f, 0.08f);
        pRt.anchorMax = new Vector2(0.94f, 0.92f);
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

        var header = new GameObject("Header", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        header.transform.SetParent(panel.transform, false);
        header.GetComponent<LayoutElement>().minHeight = 40f;
        var hh = header.GetComponent<HorizontalLayoutGroup>();
        hh.childAlignment = TextAnchor.MiddleLeft;
        hh.childControlWidth = true;
        hh.childForceExpandWidth = true;
        headerTitleText = CreateTmp(header.transform, "Title", "", 22, FontStyles.Bold, TextAlignmentOptions.Left);
        headerTitleText.color = Color.white;
        var titleLe = headerTitleText.GetComponent<LayoutElement>();
        titleLe.flexibleWidth = 1f;
        var closeGo = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        closeGo.transform.SetParent(header.transform, false);
        closeGo.GetComponent<Image>().color = new Color(0.2f, 0.22f, 0.28f, 0.95f);
        closeButton = closeGo.GetComponent<Button>();
        closeGo.GetComponent<LayoutElement>().minWidth = 40f;
        closeGo.GetComponent<LayoutElement>().preferredWidth = 44f;
        var cx = CreateTmp(closeGo.transform, "X", "✕", 20, FontStyles.Bold, TextAlignmentOptions.Center);
        cx.color = new Color(0.85f, 0.87f, 0.9f, 1f);
        StretchFull(cx.rectTransform);

        var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(LayoutElement));
        scrollGo.transform.SetParent(panel.transform, false);
        scrollGo.GetComponent<Image>().color = new Color(0.07f, 0.08f, 0.10f, 0.5f);
        scrollGo.GetComponent<LayoutElement>().flexibleHeight = 1f;
        scrollGo.GetComponent<LayoutElement>().minHeight = 200f;
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
        cv.spacing = 12;
        cv.childControlWidth = true;
        cv.childForceExpandWidth = true;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = vpRt;
        scroll.content = cRt;
        scroll.vertical = true;
        scroll.horizontal = false;

        buyPriceBigText = CreateTmp(content.transform, "BuyBig", "0 G", 36, FontStyles.Bold, TextAlignmentOptions.Left);
        buyPriceBigText.color = Color.white;
        sellPriceText = CreateTmp(content.transform, "Sell", "매도 0 G", 16, FontStyles.Normal, TextAlignmentOptions.Left);
        sellPriceText.color = new Color(0.65f, 0.68f, 0.74f);
        changePctText = CreateTmp(content.transform, "Chg", "+0.00%", 20, FontStyles.Bold, TextAlignmentOptions.Left);

        var chartHost = new GameObject("ChartHost", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        chartHost.transform.SetParent(content.transform, false);
        chartHost.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.92f);
        chartHost.GetComponent<LayoutElement>().minHeight = 150f;
        chartHost.GetComponent<LayoutElement>().preferredHeight = 160f;
        var chartGo = new GameObject("PopSentChart", typeof(RectTransform), typeof(UIPopSentiment7DayChart));
        chartGo.transform.SetParent(chartHost.transform, false);
        StretchFull(chartGo.GetComponent<RectTransform>());
        chart7Dual = chartGo.GetComponent<UIPopSentiment7DayChart>();

        popStatText = CreateTmp(content.transform, "PopLine", "인구", 15, FontStyles.Normal, TextAlignmentOptions.Left);
        popStatText.color = new Color(0.78f, 0.8f, 0.84f);
        popGaugeSlider = CreateReadOnlySliderGauge(content.transform, "PopGauge",
            new Color(0.55f, 0.32f, 0.28f, 0.95f));

        sentimentStatText = CreateTmp(content.transform, "SentLine", "민심", 15, FontStyles.Normal, TextAlignmentOptions.Left);
        sentimentStatText.color = new Color(0.78f, 0.8f, 0.84f);
        sentimentGaugeSlider = CreateReadOnlySliderGauge(content.transform, "SentGauge",
            new Color(0.92f, 0.78f, 0.35f, 0.95f));

        militaryStatText = CreateTmp(content.transform, "MilLine", "군사력", 15, FontStyles.Normal, TextAlignmentOptions.Left);
        militaryStatText.color = new Color(0.78f, 0.8f, 0.84f);
        militaryGaugeSlider = CreateReadOnlySliderGauge(content.transform, "MilGauge",
            new Color(0.32f, 0.52f, 0.88f, 0.95f));

        assetStatText = CreateTmp(content.transform, "AssetLine", "자산", 15, FontStyles.Normal, TextAlignmentOptions.Left);
        assetStatText.color = new Color(0.78f, 0.8f, 0.84f);
        assetGaugeSlider = CreateReadOnlySliderGauge(content.transform, "AssetGauge",
            new Color(0.28f, 0.62f, 0.42f, 0.95f));

        var govRow = new GameObject("GovernorRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        govRow.transform.SetParent(content.transform, false);
        govRow.GetComponent<LayoutElement>().minHeight = 64f;
        var gh = govRow.GetComponent<HorizontalLayoutGroup>();
        gh.spacing = 12;
        gh.childAlignment = TextAnchor.MiddleLeft;
        var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
        portraitGo.transform.SetParent(govRow.transform, false);
        var prt = portraitGo.GetComponent<RectTransform>();
        prt.sizeDelta = new Vector2(56f, 56f);
        governorPortraitFrameImage = portraitGo.GetComponent<Image>();
        governorPortraitFrameImage.color = new Color(0.35f, 0.28f, 0.22f, 1f);

        var faceGo = new GameObject("Face", typeof(RectTransform), typeof(Image));
        faceGo.transform.SetParent(portraitGo.transform, false);
        var faceRt = faceGo.GetComponent<RectTransform>();
        faceRt.anchorMin = new Vector2(0.1f, 0.1f);
        faceRt.anchorMax = new Vector2(0.9f, 0.9f);
        faceRt.offsetMin = Vector2.zero;
        faceRt.offsetMax = Vector2.zero;
        governorPortraitImage = faceGo.GetComponent<Image>();
        governorPortraitImage.color = new Color(0.25f, 0.28f, 0.35f, 1f);

        var initGo = CreateTmp(portraitGo.transform, "Initial", "?", 22, FontStyles.Bold, TextAlignmentOptions.Center);
        governorInitialText = initGo;
        governorInitialText.color = Color.white;
        StretchFull(initGo.rectTransform);
        var govCol = new GameObject("GovTextCol", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        govCol.transform.SetParent(govRow.transform, false);
        govCol.GetComponent<LayoutElement>().flexibleWidth = 1f;
        var gv = govCol.GetComponent<VerticalLayoutGroup>();
        gv.spacing = 2;
        gv.childAlignment = TextAnchor.UpperLeft;
        var bandGo = new GameObject("FactionBand", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        bandGo.transform.SetParent(govCol.transform, false);
        factionBandImage = bandGo.GetComponent<Image>();
        var bandLe = bandGo.GetComponent<LayoutElement>();
        bandLe.minHeight = 4f;
        bandLe.preferredHeight = 4f;
        bandLe.flexibleWidth = 1f;
        StretchFull(bandGo.GetComponent<RectTransform>());
        governorNameText = CreateTmp(govCol.transform, "GovName", "태수 —", 17, FontStyles.Bold, TextAlignmentOptions.Left);
        governorFactionText = CreateTmp(govCol.transform, "GovFac", "위", 14, FontStyles.Normal, TextAlignmentOptions.Left);
        governorFactionText.color = new Color(0.65f, 0.68f, 0.74f);

        var pfGo = new GameObject("PortfolioBox", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        pfGo.transform.SetParent(content.transform, false);
        portfolioBox = pfGo.GetComponent<RectTransform>();
        portfolioBox.GetComponent<Image>().color = new Color(0.14f, 0.12f, 0.10f, 0.95f);
        portfolioBox.GetComponent<LayoutElement>().minHeight = 72f;
        portfolioText = CreateTmp(portfolioBox.transform, "PfText", "", 16, FontStyles.Normal, TextAlignmentOptions.Left);
        StretchWithPadding(portfolioText.rectTransform, 12f);

        var footer = new GameObject("Footer", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        footer.transform.SetParent(panel.transform, false);
        footer.GetComponent<LayoutElement>().minHeight = 120f;
        var fv = footer.GetComponent<VerticalLayoutGroup>();
        fv.spacing = 8;
        fv.childControlWidth = true;
        fv.childForceExpandWidth = true;

        var fpRow = new GameObject("Footprints", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        fpRow.transform.SetParent(footer.transform, false);
        fpRow.GetComponent<LayoutElement>().minHeight = 22f;
        var fph = fpRow.GetComponent<HorizontalLayoutGroup>();
        fph.spacing = 8;
        fph.childAlignment = TextAnchor.MiddleCenter;
        fph.childControlWidth = false;
        fph.childForceExpandWidth = false;
        footprintIcon1 = CreateTmp(fpRow.transform, "F1", "\u00B7", 22, FontStyles.Bold, TextAlignmentOptions.Center);
        footprintIcon2 = CreateTmp(fpRow.transform, "F2", "\u00B7\u00B7", 22, FontStyles.Bold, TextAlignmentOptions.Center);
        footprintIcon3 = CreateTmp(fpRow.transform, "F3", "\u00B7\u00B7\u00B7", 22, FontStyles.Bold, TextAlignmentOptions.Center);
        footprintIcon1.color = new Color(0.85f, 0.78f, 0.55f, 1f);
        footprintIcon2.color = footprintIcon1.color;
        footprintIcon3.color = footprintIcon1.color;
        ApplyFootprintTier(0);

        relocateHintText = CreateTmp(footer.transform, "RelocateHint", "", 13, FontStyles.Normal, TextAlignmentOptions.Center);
        relocateHintText.color = new Color(0.72f, 0.76f, 0.82f);
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
        okGo.GetComponent<LayoutElement>().minHeight = 46f;
        confirmCloseButton = okGo.GetComponent<Button>();
        var okTmp = CreateTmp(okGo.transform, "Lbl", "확인 / 닫기", 16, FontStyles.Bold, TextAlignmentOptions.Center);
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
        deploySliderValueText = CreateTmp(box.transform, "SliderLabel", "투입 병력: 0", 16, FontStyles.Bold, TextAlignmentOptions.Center);

        var sgo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sgo.transform.SetParent(box.transform, false);
        sgo.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 28f);
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
        row.GetComponent<LayoutElement>().minHeight = 18f;
        row.GetComponent<LayoutElement>().preferredHeight = 20f;
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
        go.GetComponent<LayoutElement>().minHeight = 48f;
        go.GetComponent<LayoutElement>().flexibleWidth = 1f;
        var btn = go.GetComponent<Button>();
        var tmp = CreateTmp(go.transform, "Lbl", label, 15, FontStyles.Bold, TextAlignmentOptions.Center);
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
