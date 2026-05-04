using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>본영 탭 UI — 성벽 수거형 경제, 하단 3서브탭(내정·군사·행군).</summary>
[RequireComponent(typeof(HomeController))]
public class HomeUIController : MonoBehaviour
{
    static readonly Color LocalGoldDebtColor = new Color(1f, 0f, 0f);
    static readonly Color LocalGoldPositiveColor = Color.white;

    [Header("자원 (로컬 미러)")]
    public TextMeshProUGUI goldText;
    [Tooltip("주머니~만축 슬라이더 (선택)")]
    public Slider goldFillSlider;
    public TextMeshProUGUI farmWorkersText;

    [Header("주머니 배너")]
    public TextMeshProUGUI pocketBannerText;

    [Header("레거시·에디터 마법사 (HomeSceneLayoutWizard)")]
    [Tooltip("구 레이아웃 창고 수치 텍스트. 비우면 pocketBannerText만 사용.")]
    public TextMeshProUGUI marketAccumulateText;
    [Tooltip("구 레이아웃 창고 슬라이더. 비우면 goldFillSlider만 사용.")]
    public Slider marketAccumulateSlider;
    [Tooltip("구 수동 수거 버튼 — 신규 경제에서는 미사용(성벽 수거). 직렬화 호환용.")]
    public Button collectMarketButton;
    public TextMeshProUGUI supplyLabelText;
    [Tooltip("구 징집 진입 버튼. 비우면 서브탭·직접 패널만 사용.")]
    public Button recruitSoldierButton;

    [Header("내정 강화 (Tab 1 — Building)")]
    public TextMeshProUGUI laborLabelText;
    public Button laborUpgradeButton;
    public TextMeshProUGUI marketLabelText;
    public Button marketUpgradeButton;
    public TextMeshProUGUI logisticsLabelText;
    public Button logisticsUpgradeButton;
    public TextMeshProUGUI warehouseLabelText;
    public Button warehouseUpgradeButton;

    [Header("성벽")]
    public Button gateButton;

    [Header("서브탭 (하단 3분할)")]
    [Tooltip("내정 강화 탭")]
    public Button buildingTabButton;
    [Tooltip("군사 모집 탭")]
    public Button militaryTabButton;
    [Tooltip("행군 준비 탭")]
    public Button marchingTabButton;
    public GameObject buildingPanel;
    public GameObject militaryPanel;
    public GameObject marchingPanel;

    [Header("군사 모집 (Tab 2 — 주가 단일)")]
    public TextMeshProUGUI militaryStockPriceText;
    public Slider recruitSlider;
    public TextMeshProUGUI recruitCostText;
    public TextMeshProUGUI recruitUpkeepPreviewText;
    public TextMeshProUGUI dischargeExpectGoldText;
    public Button recruitPlus1KButton;
    public Button recruitPlus10KButton;
    public Button recruitMaxButton;
    public Button recruitConfirmButton;
    public Button dischargeConfirmButton;

    [Header("만보기")]
    public Image pedometerGaugeFill;
    public TextMeshProUGUI pedometerStepsText;
    public Button[] stepRewardButtons = new Button[4];
    public TextMeshProUGUI[] stepRewardLabels = new TextMeshProUGUI[4];

    [Header("창고 연출")]
    public CollectionManager collectionManager;

    [Header("방문객 팝업 (옵션)")]
    public RectTransform visitorPopupRoot;
    public TextMeshProUGUI visitorPopupTitleText;
    public TextMeshProUGUI visitorPopupBodyText;
    public Button visitorPopupCloseButton;

    [Header("숫자 롤링")]
    public float resourceRollDuration = 0.42f;

    HomeController _controller;
    double _displayGold;
    Tweener _goldRollTween;
    /// <summary>0=내정, 1=군사, 2=행군</summary>
    int _activeSubTabIndex;

    void Awake()
    {
        _controller = GetComponent<HomeController>();
        if (collectionManager == null)
            collectionManager = GetComponent<CollectionManager>();
    }

    void OnEnable()
    {
        SubscribeEvents();
        SubscribeVisitorEvents();
        SubscribeStepEvents();
        PushGlobalTopBar();
    }

    void Start()
    {
        if (_controller == null) return;
        if (gateButton == null)
            gateButton = transform.Find("MainWallButton")?.GetComponent<Button>() ??
                       transform.Find("GateButton")?.GetComponent<Button>();
        ResolveMissingRefs();
        ResolveSubTabRefsIfLegacy();
        ResolveMilitaryUiRefs();
        FixHomeCanvasScaleIfBroken();
        FixLegacyVerticalTmpFromContentSizeFitter();
        EnsureUiInputInfrastructure();
        SubscribeEvents();
        SubscribeVisitorEvents();
        SubscribeStepEvents();
        RefreshAllUI();
        BindButtons();
        BindSubTabs();
        BindMilitary();
        EnsureVisitorPopupIfNeeded();
        StartCoroutine(UpdatePocketUiCoroutine());
        StartCoroutine(CoRefreshMilitaryWhenDataReady());
    }

    void OnDestroy()
    {
        _goldRollTween?.Kill();
    }

    void OnDisable()
    {
        UnsubscribeEvents();
        UnsubscribeVisitorEvents();
        UnsubscribeStepEvents();
    }

    void ResolveMissingRefs()
    {
        if (warehouseLabelText == null)
            warehouseLabelText =
                transform.Find("FunctionTabs/PanelsRoot/BuildingPanel/UpgradeGrid/WarehouseUpgradePanel/WarehouseLabelText")
                    ?.GetComponent<TextMeshProUGUI>()
                ?? transform.Find("WarehousePanelsRow/WarehouseRow/WarehouseLabelText")
                    ?.GetComponent<TextMeshProUGUI>();
        if (warehouseUpgradeButton == null)
            warehouseUpgradeButton =
                transform.Find("FunctionTabs/PanelsRoot/BuildingPanel/UpgradeGrid/WarehouseUpgradePanel/WarehouseButtons/WarehouseUpgradeButton")
                    ?.GetComponent<Button>()
                ?? transform.Find("WarehousePanelsRow/WarehouseRow/WarehouseUpgradeButton")
                    ?.GetComponent<Button>();
    }

    /// <summary>옛 2탭 프리팹: FunctionTabs/TabButtons 등에서 3번째 탭 참조 보강.</summary>
    void ResolveSubTabRefsIfLegacy()
    {
        if (buildingTabButton == null)
            buildingTabButton = transform.Find("FunctionTabs/TabBuildingButton")?.GetComponent<Button>()
                ?? transform.Find("FunctionTabs/BuildingTabButton")?.GetComponent<Button>();
        if (buildingPanel == null)
            buildingPanel = transform.Find("FunctionTabs/PanelsRoot/BuildingPanel")?.gameObject
                ?? transform.Find("FunctionTabs/Panels/BuildingPanel")?.gameObject
                ?? transform.Find("FunctionTabs/BuildingPanel")?.gameObject;
        if (militaryTabButton == null)
            militaryTabButton = transform.Find("FunctionTabs/TabMilitaryButton")?.GetComponent<Button>();
        if (marchingTabButton == null)
            marchingTabButton = transform.Find("FunctionTabs/TabMarchingButton")?.GetComponent<Button>();
        if (militaryPanel == null)
            militaryPanel = transform.Find("FunctionTabs/PanelsRoot/MilitaryPanel")?.gameObject
                ?? transform.Find("FunctionTabs/Panels/MilitaryPanel")?.gameObject
                ?? transform.Find("FunctionTabs/MilitaryPanel")?.gameObject;
        if (marchingPanel == null)
            marchingPanel = transform.Find("FunctionTabs/PanelsRoot/MarchingPanel")?.gameObject
                ?? transform.Find("FunctionTabs/Panels/MarchingPanel")?.gameObject
                ?? transform.Find("FunctionTabs/MarchingPanel")?.gameObject;
        if (militaryStockPriceText == null)
            militaryStockPriceText = militaryPanel != null
                ? militaryPanel.transform.Find("MilitaryStockPriceText")?.GetComponent<TextMeshProUGUI>()
                : null;
    }

    void ResolveMilitaryUiRefs()
    {
        if (militaryPanel == null) return;
        Transform mp = militaryPanel.transform;
        if (recruitSlider == null)
            recruitSlider = mp.Find("RecruitSlider")?.GetComponent<Slider>();
        if (militaryStockPriceText == null)
            militaryStockPriceText = mp.Find("MilitaryStockPriceText")?.GetComponent<TextMeshProUGUI>();
        if (recruitCostText == null)
            recruitCostText = mp.Find("RecruitCostText")?.GetComponent<TextMeshProUGUI>();
        if (recruitUpkeepPreviewText == null)
            recruitUpkeepPreviewText = mp.Find("RecruitUpkeepPreviewText")?.GetComponent<TextMeshProUGUI>();
        if (dischargeExpectGoldText == null)
            dischargeExpectGoldText = mp.Find("DischargeExpectGoldText")?.GetComponent<TextMeshProUGUI>();
        if (recruitPlus1KButton == null)
            recruitPlus1KButton = mp.Find("QuickRow/RecruitPlus1KButton")?.GetComponent<Button>();
        if (recruitPlus10KButton == null)
            recruitPlus10KButton = mp.Find("QuickRow/RecruitPlus10KButton")?.GetComponent<Button>();
        if (recruitMaxButton == null)
            recruitMaxButton = mp.Find("QuickRow/RecruitMaxButton")?.GetComponent<Button>();
        if (recruitConfirmButton == null)
            recruitConfirmButton = mp.Find("RecruitActionRow/RecruitConfirmButton")?.GetComponent<Button>();
        if (dischargeConfirmButton == null)
            dischargeConfirmButton = mp.Find("RecruitActionRow/DischargeConfirmButton")?.GetComponent<Button>();
    }

    IEnumerator CoRefreshMilitaryWhenDataReady()
    {
        for (int i = 0; i < 240; i++)
        {
            var dm = DataManager.InstanceOrNull;
            if (dm != null && dm.IsStateReady)
                break;
            yield return null;
        }
        RefreshMilitaryPreview();
    }

    void SubscribeEvents()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm == null) return;
        gm.OnGoldChanged -= OnGoldChangedHandler;
        gm.OnGoldChanged += OnGoldChangedHandler;
    }

    void UnsubscribeEvents()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm == null) return;
        gm.OnGoldChanged -= OnGoldChangedHandler;
    }

    void SubscribeVisitorEvents()
    {
        if (_controller == null) return;
        _controller.VisitorEventRaised -= OnVisitorEventRaised;
        _controller.VisitorEventRaised += OnVisitorEventRaised;
    }

    void UnsubscribeVisitorEvents()
    {
        if (_controller == null) return;
        _controller.VisitorEventRaised -= OnVisitorEventRaised;
    }

    void SubscribeStepEvents()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm == null) return;
        gm.OnStepsChanged -= OnStepsTodayChangedHandler;
        gm.OnStepsChanged += OnStepsTodayChangedHandler;
    }

    void UnsubscribeStepEvents()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm == null) return;
        gm.OnStepsChanged -= OnStepsTodayChangedHandler;
    }

    void OnStepsTodayChangedHandler(int _) => RefreshPedometerUI();

    void OnGoldChangedHandler(double gold)
    {
        RollGoldDisplay(gold);
        RefreshMilitaryPreview();
        RefreshStrategicUpgradeLocks();
    }

    IEnumerator UpdatePocketUiCoroutine()
    {
        var wait = new WaitForSeconds(0.15f);
        while (true)
        {
            yield return wait;
            if (_controller == null) continue;
            double pending = _controller.ComputePendingMarketGold();
            double maxP = _controller.GetMarketMaxCapacity();
            var bannerTmp = pocketBannerText != null ? pocketBannerText : marketAccumulateText;
            if (bannerTmp != null)
            {
                if (pending >= 1d)
                    bannerTmp.text = $"💰 {pending:N0} Gold 수거 가능";
                else
                    bannerTmp.text = "";
            }

            var fillSlider = goldFillSlider != null ? goldFillSlider : marketAccumulateSlider;
            if (fillSlider != null && maxP > 0)
                fillSlider.value = Mathf.Clamp01((float)(pending / maxP));
        }
    }

    void BindButtons()
    {
        if (gateButton != null)
        {
            gateButton.onClick.RemoveAllListeners();
            var hold = gateButton.GetComponent<GateButtonHold>() ?? gateButton.gameObject.AddComponent<GateButtonHold>();
            hold.controller = _controller;
            hold.collectionManager = collectionManager;
        }

        WireHoldRepeat(laborUpgradeButton, () =>
        {
            _controller?.UpgradeLabor();
            UpdateLaborUI();
        });
        WireHoldRepeat(marketUpgradeButton, () =>
        {
            _controller?.UpgradeMarket();
            DataManager.InstanceOrNull?.RefreshHomeCastleMaxGarrisonFromUserBuildings();
            UpdateMarketUI();
        });
        WireHoldRepeat(warehouseUpgradeButton, () =>
        {
            _controller?.UpgradeWarehouse();
            UpdateWarehouseUI();
            GlobalUIManager.InstanceOrNull?.RefreshTopBarFromGameManager();
        });
        WireHoldRepeat(logisticsUpgradeButton, () =>
        {
            _controller?.UpgradeLogistics();
            UpdateLogisticsUI();
            RefreshMilitaryPreview();
        });

        if (stepRewardButtons != null && _controller != null)
        {
            for (int i = 0; i < stepRewardButtons.Length; i++)
            {
                if (stepRewardButtons[i] == null) continue;
                int idx = i;
                stepRewardButtons[i].onClick.AddListener(() =>
                {
                    if (_controller.ClaimStepReward(idx))
                    {
                        RefreshPedometerUI();
                        UpdateGoldUI(GameManager.InstanceOrNull?.currentGold ?? 0d, instant: true);
                    }
                });
            }
        }
    }

    void BindSubTabs()
    {
        if (buildingTabButton != null)
        {
            buildingTabButton.onClick.RemoveAllListeners();
            buildingTabButton.onClick.AddListener(() => SetFunctionSubTab(0));
        }
        if (militaryTabButton != null)
        {
            militaryTabButton.onClick.RemoveAllListeners();
            militaryTabButton.onClick.AddListener(() => SetFunctionSubTab(1));
        }
        if (marchingTabButton != null)
        {
            marchingTabButton.onClick.RemoveAllListeners();
            marchingTabButton.onClick.AddListener(() => SetFunctionSubTab(2));
        }
        SetFunctionSubTab(_activeSubTabIndex);
    }

    /// <param name="tabIndex">0=내정 강화, 1=군사 모집, 2=행군 준비</param>
    void SetFunctionSubTab(int tabIndex)
    {
        _activeSubTabIndex = Mathf.Clamp(tabIndex, 0, 2);
        bool b0 = _activeSubTabIndex == 0;
        bool b1 = _activeSubTabIndex == 1;
        bool b2 = _activeSubTabIndex == 2;

        if (buildingPanel != null) buildingPanel.SetActive(b0);
        if (militaryPanel != null) militaryPanel.SetActive(b1);
        if (marchingPanel != null) marchingPanel.SetActive(b2);
        if (b1)
            RefreshMilitaryPreview();
    }

    void BindMilitary()
    {
        if (recruitSlider != null)
        {
            recruitSlider.wholeNumbers = true;
            recruitSlider.minValue = 0;
            recruitSlider.onValueChanged.RemoveListener(OnRecruitSliderValueChanged);
            recruitSlider.onValueChanged.AddListener(OnRecruitSliderValueChanged);
        }
        if (recruitPlus1KButton != null)
        {
            recruitPlus1KButton.onClick.RemoveAllListeners();
            recruitPlus1KButton.onClick.AddListener(() => AddRecruitSlider(1000));
        }
        if (recruitPlus10KButton != null)
        {
            recruitPlus10KButton.onClick.RemoveAllListeners();
            recruitPlus10KButton.onClick.AddListener(() => AddRecruitSlider(10000));
        }
        if (recruitMaxButton != null)
        {
            recruitMaxButton.onClick.RemoveAllListeners();
            recruitMaxButton.onClick.AddListener(SetRecruitSliderMax);
        }
        if (recruitConfirmButton != null)
        {
            recruitConfirmButton.onClick.RemoveAllListeners();
            recruitConfirmButton.onClick.AddListener(ConfirmRecruitStockPrice);
        }
        if (dischargeConfirmButton != null)
        {
            dischargeConfirmButton.onClick.RemoveAllListeners();
            dischargeConfirmButton.onClick.AddListener(ConfirmDischargeStockPrice);
        }

        RefreshMilitaryPreview();
    }

    void OnRecruitSliderValueChanged(float _) => SnapRecruitSlider();

    void SnapRecruitSlider()
    {
        if (recruitSlider == null) return;
        float max = recruitSlider.maxValue;
        int step = max >= 100f ? 100 : 1;
        int vmin = Mathf.RoundToInt(recruitSlider.minValue);
        int vmax = Mathf.RoundToInt(max);
        int v = Mathf.RoundToInt(recruitSlider.value / step) * step;
        v = Mathf.Clamp(v, vmin, vmax);
        recruitSlider.SetValueWithoutNotify(v);
        RefreshMilitaryPreview();
    }

    void AddRecruitSlider(int delta)
    {
        if (recruitSlider == null) return;
        int v = Mathf.RoundToInt(recruitSlider.value) + delta;
        recruitSlider.value = v;
        SnapRecruitSlider();
    }

    void SetRecruitSliderMax()
    {
        var dm = DataManager.InstanceOrNull;
        string hid = dm?.HomeCastleId?.Trim();
        if (string.IsNullOrEmpty(hid) || !RecruitController.TryBuildStockPriceQuote(hid, out var q)) return;
        recruitSlider.maxValue = q.MaxRecruitable;
        recruitSlider.value = q.MaxRecruitable;
        SnapRecruitSlider();
    }

    void RefreshMilitaryPreview()
    {
        var dm = DataManager.InstanceOrNull;
        var gm = GameManager.InstanceOrNull;
        string hid = dm?.HomeCastleId?.Trim();

        float unit = 0f;
        int maxRec = 0;
        var qInit = default(RecruitController.RecruitQuote);
        bool quoteOk = !string.IsNullOrEmpty(hid) && dm != null && dm.IsStateReady &&
                       RecruitController.TryBuildStockPriceQuote(hid, out qInit);
        if (quoteOk)
        {
            unit = qInit.UnitPrice;
            maxRec = qInit.MaxRecruitable;
        }

        if (militaryStockPriceText != null)
        {
            if (!quoteOk)
                militaryStockPriceText.text = dm != null && !dm.IsStateReady
                    ? "도시 데이터 로딩 중…"
                    : "본영 성 시세: — (거점 확인)";
            else
                militaryStockPriceText.text = $"현재 도시 병사 시세: {unit:N2} G";
        }

        if (recruitSlider != null && dm != null && !string.IsNullOrEmpty(hid))
        {
            recruitSlider.minValue = maxRec >= 100 ? 100f : 0f;
            float maxVal = Mathf.Max((float)maxRec, recruitSlider.minValue);
            if (maxVal <= recruitSlider.minValue)
                maxVal = recruitSlider.minValue + 100f;
            recruitSlider.maxValue = maxVal;
            if (recruitSlider.value > recruitSlider.maxValue)
                recruitSlider.value = recruitSlider.maxValue;
            if (recruitSlider.value < recruitSlider.minValue)
                recruitSlider.value = recruitSlider.minValue;
        }

        int amount = recruitSlider != null ? Mathf.RoundToInt(recruitSlider.value) : 0;
        amount = Mathf.RoundToInt(amount / 100f) * 100;

        if (!string.IsNullOrEmpty(hid) && RecruitController.TryBuildStockPriceQuote(hid, out var qq))
            unit = qq.UnitPrice;

        double cost = unit * amount;
        if (recruitCostText != null)
            recruitCostText.text = $"예상 비용: {cost:N0} G";

        double deltaDaily = EconomyManager.ComputeDailyUpkeepGoldForAdditionalSoldiers(amount);
        if (recruitUpkeepPreviewText != null)
        {
            recruitUpkeepPreviewText.richText = true;
            recruitUpkeepPreviewText.text =
                $"징집 후 일일 유지비 증가: <color=#FF4444>-{Utils.AbbreviateScore(deltaDaily)} / Day</color>";
        }

        long expectDischarge = (long)Math.Floor(unit * amount * 0.95d);
        if (dischargeExpectGoldText != null)
            dischargeExpectGoldText.text =
                amount > 0 ? $"예상 환급: {expectDischarge:N0} G (시세의 95%, 수수료 5%)" : "예상 환급: —";

        bool canRecruit = false;
        if (gm != null && amount >= 100 && dm != null && dm.IsStateReady && !string.IsNullOrEmpty(hid) &&
            RecruitController.TryBuildStockPriceQuote(hid, out var qx))
            canRecruit = gm.currentGold >= cost && amount <= qx.MaxRecruitable && gm.CanSpendStrategicPurchases;

        if (recruitConfirmButton != null)
            recruitConfirmButton.interactable = canRecruit;

        int have = 0;
        if (dm != null && dm.IsStateReady && !string.IsNullOrEmpty(hid) &&
            dm.castleStateDataMap.TryGetValue(hid, out var st) && st != null)
            have = Mathf.Max(0, st.userDeployedTroops);

        bool canDischarge = amount >= 100 && amount <= have && dm != null && dm.IsStateReady;
        if (dischargeConfirmButton != null)
            dischargeConfirmButton.interactable = canDischarge;
    }

    void ConfirmRecruitStockPrice()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null || !dm.IsStateReady || recruitSlider == null) return;
        string hid = dm.HomeCastleId?.Trim();
        if (string.IsNullOrEmpty(hid)) return;
        int amount = Mathf.RoundToInt(recruitSlider.value);
        amount = Mathf.RoundToInt(amount / 100f) * 100;
        if (amount < 100) return;
        dm.TryRecruitHomeSoldiersAtStockPrice(hid, amount, out _, out _);
        UpdateFarmWorkersUI();
        PushGlobalTopBar();
        RefreshMilitaryPreview();
    }

    void ConfirmDischargeStockPrice()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null || !dm.IsStateReady || recruitSlider == null) return;
        string hid = dm.HomeCastleId?.Trim();
        if (string.IsNullOrEmpty(hid)) return;
        int amount = Mathf.RoundToInt(recruitSlider.value);
        amount = Mathf.RoundToInt(amount / 100f) * 100;
        if (amount < 100) return;
        dm.TryDischargeHomeSoldiersAtStockPrice(hid, amount, out _, out _);
        UpdateFarmWorkersUI();
        PushGlobalTopBar();
        RefreshMilitaryPreview();
    }

    void WireHoldRepeat(Button btn, Action tick)
    {
        if (btn == null || tick == null) return;
        btn.onClick.RemoveAllListeners();
        var hr = btn.GetComponent<ButtonHoldRepeat>() ?? btn.gameObject.AddComponent<ButtonHoldRepeat>();
        hr.Configure(tick);
    }

    void RefreshAllUI()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm == null) return;
        UpdateGoldUI(gm.currentGold, instant: true);
        UpdateFarmWorkersUI();
        PushGlobalTopBar();
        UpdateLaborUI();
        UpdateMarketUI();
        UpdateWarehouseUI();
        UpdateLogisticsUI();
        RefreshPedometerUI();
        RefreshMilitaryPreview();
        RefreshStrategicUpgradeLocks();
    }

    void RefreshStrategicUpgradeLocks()
    {
        var gm = GameManager.InstanceOrNull;
        bool allow = gm != null && gm.CanSpendStrategicPurchases;
        if (laborUpgradeButton != null) laborUpgradeButton.interactable = allow;
        if (marketUpgradeButton != null) marketUpgradeButton.interactable = allow;
        if (warehouseUpgradeButton != null) warehouseUpgradeButton.interactable = allow;
        if (logisticsUpgradeButton != null) logisticsUpgradeButton.interactable = allow;
    }

    void RollGoldDisplay(double target)
    {
        if (goldText == null) return;
        _goldRollTween?.Kill();
        double start = _displayGold;
        float p = 0f;
        _goldRollTween = DOTween.To(() => p, x =>
        {
            p = x;
            float u = Mathf.Clamp01(x);
            double v = start + (target - start) * u;
            _displayGold = v;
            goldText.text = FormatGoldDisplay(v);
            goldText.color = v < 0d ? LocalGoldDebtColor : LocalGoldPositiveColor;
        }, 1f, resourceRollDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true);
    }

    static string FormatGoldDisplay(double gold)
    {
        if (Math.Abs(gold - Math.Round(gold)) < 1e-6)
            return $"{Math.Round(gold):N0}";
        return $"{gold:N2}";
    }

    void UpdateGoldUI(double gold, bool instant)
    {
        if (goldText == null) return;
        if (instant)
        {
            _goldRollTween?.Kill();
            _displayGold = gold;
            goldText.text = FormatGoldDisplay(gold);
            goldText.color = gold < 0d ? LocalGoldDebtColor : LocalGoldPositiveColor;
        }
        else
            RollGoldDisplay(gold);
    }

    void RefreshPedometerUI()
    {
        var u = GameManager.InstanceOrNull?.currentUser;
        if (u == null) return;
        int steps = u.stepsToday;
        if (pedometerGaugeFill != null)
            pedometerGaugeFill.fillAmount = Mathf.Clamp01(steps / 10000f);
        if (pedometerStepsText != null)
            pedometerStepsText.text = $"{steps:N0} / 10,000";

        HomeController.ReadStepMissionRows(out var milestones, out var rewards);
        if (u.stepRewardsClaimed == null || u.stepRewardsClaimed.Length != milestones.Length)
            u.stepRewardsClaimed = new bool[milestones.Length];

        for (int i = 0; i < milestones.Length && i < stepRewardButtons.Length; i++)
        {
            var btn = stepRewardButtons[i];
            if (btn == null) continue;
            bool claimed = u.stepRewardsClaimed[i];
            bool canClaim = steps >= milestones[i] && !claimed;
            btn.interactable = canClaim;
            if (i < stepRewardLabels.Length && stepRewardLabels[i] != null)
            {
                int rw = i < rewards.Length ? rewards[i] : 0;
                string state = claimed ? "완료" : canClaim ? "수령" : "";
                stepRewardLabels[i].text = state.Length > 0
                    ? $"{milestones[i]:N0}\n+{rw} MP\n{state}"
                    : $"{milestones[i]:N0}\n+{rw} MP";
            }
        }
    }

    void UpdateFarmWorkersUI()
    {
        if (farmWorkersText == null) return;
        var dm = DataManager.InstanceOrNull;
        long n = dm != null && dm.IsStateReady ? UserPortfolioManager.GetTotalOwnedSoldiers(dm) : 0L;
        farmWorkersText.text = n.ToString("N0");
    }

    void PushGlobalTopBar()
    {
        var gm = GameManager.InstanceOrNull;
        var gui = GlobalUIManager.InstanceOrNull;
        if (gm?.currentUser == null || gui == null) return;
        long soldiers = DataManager.InstanceOrNull != null && DataManager.InstanceOrNull.IsStateReady
            ? UserPortfolioManager.GetTotalOwnedSoldiers(DataManager.InstanceOrNull)
            : gm.currentUser.soldierCount;
        gui.SetTopBarNumbers(gm.currentGold, soldiers);
    }

    void UpdateLaborUI()
    {
        var gm = GameManager.InstanceOrNull;
        if (laborLabelText == null || gm?.currentUser == null) return;
        int lv = Mathf.Max(1, gm.currentUser.laborLevel);
        double tap = lv * 5d;
        double cost = HomeController.UpgradeCost(HomeEconomyConfig.LaborBaseCost, lv);
        laborLabelText.text =
            $"노동력 Lv.{lv}\n성벽 탭 시 +{tap:F0} Gold (노동)\n업그레이드 {cost:F0} G";
    }

    void UpdateMarketUI()
    {
        var gm = GameManager.InstanceOrNull;
        if (marketLabelText == null || gm?.currentUser == null) return;
        int lv = gm.currentUser.marketLevel;
        double rate = lv * HomeController.MarketGoldPerLevelPerSec;
        double cost = HomeController.UpgradeCost(HomeEconomyConfig.MarketBaseCost, lv);
        marketLabelText.text =
            $"시장 Lv.{lv}\n주머니 초당 누적: +{rate:F0} × 시간\n업그레이드 {cost:F0} G";
    }

    void UpdateLogisticsUI()
    {
        var gm = GameManager.InstanceOrNull;
        if (logisticsLabelText == null || gm?.currentUser == null) return;
        int lv = gm.currentUser.farmLevel;
        double c = HomeController.GetLogisticsUpgradeGoldCost(lv);
        double pct = 0d;
        var dm = DataManager.InstanceOrNull;
        if (dm != null && dm.IsReady)
        {
            var d = dm.GetLevelData(lv);
            if (d != null) pct = Math.Max(0d, Math.Min(50d, d.logisticsDiscountRate));
        }

        logisticsLabelText.text =
            $"병참 Lv.{lv}\n일일 유지비 −{pct:0.#}%\n업그레이드 {c:F0} G";
    }

    void UpdateWarehouseUI()
    {
        if (warehouseLabelText == null) return;
        double cap = HomeController.ResolveMarketPocketGoldCap();
        string capLine = cap > 0d ? $"8h 만축 주머니 상한: {cap:N0} G" : "시트 한도 확인 (창고·시장)";
        warehouseLabelText.text = $"창고 Lv.{GameManager.InstanceOrNull?.currentUser?.warehouseLevel ?? 0}\n{capLine}";
    }

    void OnVisitorEventRaised(string title, string body)
    {
        EnsureVisitorPopupIfNeeded();
        if (visitorPopupRoot == null)
        {
            Debug.Log($"[방문객] {title}: {body}");
            return;
        }
        if (visitorPopupTitleText != null)
            visitorPopupTitleText.text = string.IsNullOrWhiteSpace(title) ? "방문" : title;
        if (visitorPopupBodyText != null)
            visitorPopupBodyText.text = string.IsNullOrWhiteSpace(body) ? "" : body;
        visitorPopupRoot.gameObject.SetActive(true);
    }

    void EnsureVisitorPopupIfNeeded()
    {
        if (visitorPopupRoot != null) return;
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        var rootGo = new GameObject("VisitorEventPopup", typeof(RectTransform), typeof(Image));
        visitorPopupRoot = rootGo.GetComponent<RectTransform>();
        visitorPopupRoot.SetParent(canvas.transform, false);
        visitorPopupRoot.anchorMin = visitorPopupRoot.anchorMax = new Vector2(0.5f, 0.5f);
        visitorPopupRoot.sizeDelta = new Vector2(520f, 280f);
        rootGo.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.94f);
        visitorPopupTitleText = CreateTmp(visitorPopupRoot, "Title", 26, new Vector2(0f, 96f));
        visitorPopupBodyText = CreateTmp(visitorPopupRoot, "Body", 20, new Vector2(0f, 16f));
        visitorPopupBodyText.enableWordWrapping = true;
        visitorPopupBodyText.rectTransform.sizeDelta = new Vector2(460f, 140f);
        var closeGo = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
        closeGo.transform.SetParent(visitorPopupRoot, false);
        var crt = closeGo.GetComponent<RectTransform>();
        crt.sizeDelta = new Vector2(140f, 44f);
        crt.anchoredPosition = new Vector2(0f, -104f);
        visitorPopupCloseButton = closeGo.GetComponent<Button>();
        visitorPopupCloseButton.onClick.AddListener(() => visitorPopupRoot.gameObject.SetActive(false));
        visitorPopupRoot.gameObject.SetActive(false);
    }

    static TextMeshProUGUI CreateTmp(RectTransform parent, string name, float size, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        var rt = tmp.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(460f, 48f);
        return tmp;
    }

    void EnsureUiInputInfrastructure()
    {
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.gameObject.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();
    }

    void FixHomeCanvasScaleIfBroken()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        var rt = canvas.transform as RectTransform;
        if (rt != null && rt.localScale.sqrMagnitude < 1e-8f)
            rt.localScale = Vector3.one;
    }

    /// <summary>
    /// 에디터 마법사 구버전: TMP에 horizontal Unconstrained CSF 가 있으면 레이아웃에서 폭이 0에 가까워져 한 글자씩 세로로 쌓입니다.
    /// </summary>
    void FixLegacyVerticalTmpFromContentSizeFitter()
    {
        var list = GetComponentsInChildren<ContentSizeFitter>(true);
        foreach (var csf in list)
        {
            if (csf == null || csf.horizontalFit != ContentSizeFitter.FitMode.Unconstrained) continue;
            var rt = csf.transform as RectTransform;
            Destroy(csf);
            if (rt == null) continue;
            float ay = rt.anchorMin.y, by = rt.anchorMax.y;
            rt.anchorMin = new Vector2(0f, ay);
            rt.anchorMax = new Vector2(1f, by);
            var le = rt.GetComponent<LayoutElement>();
            if (le != null)
            {
                if (le.flexibleWidth < 0.01f) le.flexibleWidth = 1f;
                le.minWidth = Mathf.Max(le.minWidth, 120f);
            }
        }
    }

    public void PunchLocalGoldText(float strength = 0.12f, float duration = 0.2f, int vibrato = 7)
    {
        if (goldText == null) return;
        var rt = goldText.rectTransform;
        rt.DOKill();
        rt.localScale = Vector3.one;
        rt.DOPunchScale(Vector3.one * strength, duration, vibrato, 0.5f).SetUpdate(true);
    }
}
