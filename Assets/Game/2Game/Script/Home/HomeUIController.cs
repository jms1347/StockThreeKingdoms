using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Globalization;

/// <summary>
/// 본영 화면 담당. GameManager.OnGoldChanged 구독, 코루틴으로 창고 UI 갱신.
/// </summary>
[RequireComponent(typeof(HomeController))]
public class HomeUIController : MonoBehaviour
{
    static readonly Color LocalGoldDebtColor = new Color(1f, 0f, 0f);
    static readonly Color LocalGoldPositiveColor = Color.white;

    [Header("자원 표시")]
    public TextMeshProUGUI goldText;

    [Tooltip("천하 거점에 배치한 병력 합계(본영 표시).")]
    public TextMeshProUGUI farmWorkersText;

    [Header("업그레이드 UI - 노동력")]
    public TextMeshProUGUI laborLabelText;
    public Button laborUpgradeButton;

    [Header("업그레이드 UI - 시장")]
    public TextMeshProUGUI marketLabelText;
    public TextMeshProUGUI marketAccumulateText;
    public Slider marketAccumulateSlider;
    public Button marketUpgradeButton;
    public Button collectMarketButton;

    [Header("업그레이드 UI - 창고")]
    public TextMeshProUGUI warehouseLabelText;
    public Button warehouseUpgradeButton;

    [Header("업그레이드 UI - 병참 (유지비 할인)")]
    [FormerlySerializedAs("farmLabelText")]
    public TextMeshProUGUI logisticsLabelText;
    [FormerlySerializedAs("farmUpgradeButton")]
    public Button logisticsUpgradeButton;

    [Header("보급 UI")]
    public TextMeshProUGUI supplyLabelText;
    public Button recruitSoldierButton;

    [Header("대문 터치")]
    public Button gateButton;

    [Header("만보기")]
    public Image pedometerGaugeFill;
    public TextMeshProUGUI pedometerStepsText;

    [Tooltip("2k, 5k, 7k, 10k 순서")]
    public Button[] stepRewardButtons = new Button[4];

    public TextMeshProUGUI[] stepRewardLabels = new TextMeshProUGUI[4];

    [Header("창고 연출")]
    public CollectionManager collectionManager;

    [Header("방문객 이벤트 팝업 (옵션)")]
    public RectTransform visitorPopupRoot;
    public TextMeshProUGUI visitorPopupTitleText;
    public TextMeshProUGUI visitorPopupBodyText;
    public Button visitorPopupCloseButton;

    [Header("징집 팝업 (옵션)")]
    public RectTransform recruitPopupRoot;
    public TMP_InputField recruitCountInput;
    public TextMeshProUGUI recruitPricePerUnitText;
    public TextMeshProUGUI recruitExpectedCostText;
    public TextMeshProUGUI recruitMaintenanceDeltaText;
    public Button recruitPlus100Button;
    public Button recruitPlus1KButton;
    public Button recruitPlus10KButton;
    public Button recruitMaxButton;
    public Button recruitConfirmButton;
    public Button recruitCancelButton;
    public TextMeshProUGUI recruitHeaderTitleText;
    public TextMeshProUGUI recruitHeaderAssetText;
    public TextMeshProUGUI recruitPostPopulationText;
    public TextMeshProUGUI recruitEconomicShockText;
    public TextMeshProUGUI recruitWarningText;
    public Button discharge10PctButton;
    public Button discharge50PctButton;
    public Button discharge100PctButton;
    public TextMeshProUGUI dischargeExpectedGoldText;
    public TextMeshProUGUI dischargeMaintenanceReliefText;
    public Button dischargeConfirmButton;

    [Header("숫자 롤링")]
    public float resourceRollDuration = 0.42f;

    HomeController _controller;
    double _displayGold;
    Tweener _goldRollTween;
    int _recruitCount;
    int _dischargeCount;

    void Awake()
    {
        _controller = GetComponent<HomeController>();
        if (collectionManager == null)
            collectionManager = GetComponent<CollectionManager>();
    }

    void OnEnable()
    {
        SubscribeEvents();
        SubscribeStepEvents();
        SubscribeVisitorEvents();
        PushGlobalTopBar();
    }

    void Start()
    {
        if (_controller == null) return;

        if (gateButton == null)
            gateButton = transform.Find("GateButton")?.GetComponent<Button>();
        ResolveHomeUpgradeReferencesIfMissing();

        FixHomeCanvasScaleIfBroken();
        EnsureUiInputInfrastructure();

        SubscribeEvents();
        SubscribeStepEvents();
        SubscribeVisitorEvents();

        RefreshAllUI();
        BindButtons();
        EnsureVisitorPopupIfNeeded();
        EnsureRecruitPopupIfNeeded();

        StartCoroutine(UpdateAccumulateUICoroutine());
    }

    void OnDestroy()
    {
        _goldRollTween?.Kill();
    }

    void OnDisable()
    {
        UnsubscribeStepEvents();
        UnsubscribeEvents();
        UnsubscribeVisitorEvents();
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

    void OnGateButtonClickFallback()
    {
        if (gateButton == null) return;
        gateButton.GetComponent<GateButtonHold>()?.OnGateTapFromButton();
    }

    void EnsureUiInputInfrastructure()
    {
        if (EventSystem.current == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();
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
        if (rt == null) return;
        if (rt.localScale.sqrMagnitude < 1e-8f)
            rt.localScale = Vector3.one;
    }

    void OnStepsTodayChangedHandler(int _) => RefreshPedometerNow();

    void OnGoldChangedHandler(double gold)
    {
        RollGoldDisplay(gold);
        UpdateSupplyUI();
        RefreshStrategicUpgradeButtons();
    }

    IEnumerator UpdateAccumulateUICoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.2f);
            if (_controller == null || GameManager.InstanceOrNull == null) continue;

            double mAcc = _controller.CurrentMarketAccumulated;
            double mMax = _controller.GetMarketMaxCapacity();
            if (collectionManager != null && collectionManager.IsFlyBusy) mAcc = 0;
            if (marketAccumulateText != null)
            {
                marketAccumulateText.text = mMax > 0 ? $"{mAcc:F0} / {mMax:F0}" : "0 / 0";
                marketAccumulateText.color = (mMax > 0 && mAcc >= mMax) ? Color.red : Color.white;
            }

            if (marketAccumulateSlider != null && mMax > 0)
                marketAccumulateSlider.value = (float)Math.Min(1.0, mAcc / mMax);

            RefreshPedometerUI();
        }
    }

    void BindButtons()
    {
        if (gateButton == null)
            Debug.LogWarning("[HomeUIController] gateButton이 연결되지 않았습니다. Inspector에서 GateButton을 할당하세요.");
        else
        {
            var hold = gateButton.GetComponent<GateButtonHold>();
            if (hold == null) hold = gateButton.gameObject.AddComponent<GateButtonHold>();
            hold.controller = _controller;
            hold.collectionManager = collectionManager;
            gateButton.onClick.RemoveListener(OnGateButtonClickFallback);
            gateButton.onClick.AddListener(OnGateButtonClickFallback);
        }

        WireHoldRepeat(laborUpgradeButton, () =>
        {
            _controller?.UpgradeLabor();
            UpdateLaborUI();
            UpdateSupplyUI();
        });
        WireHoldRepeat(marketUpgradeButton, () =>
        {
            _controller?.UpgradeMarket();
            DataManager.InstanceOrNull?.RefreshHomeCastleMaxGarrisonFromUserBuildings();
            UpdateMarketUI();
            UpdateSupplyUI();
        });
        WireHoldRepeat(warehouseUpgradeButton, () =>
        {
            _controller?.UpgradeWarehouse();
            UpdateWarehouseUI();
        });
        WireHoldRepeat(logisticsUpgradeButton, () =>
        {
            _controller?.UpgradeLogistics();
            UpdateLogisticsUI();
            UpdateSupplyUI();
        });

        void CollectWarehouse()
        {
            _controller?.TryFlyCollectFromWarehouse(collectionManager, requireActivePiles: false);
        }

        if (collectMarketButton != null)
            collectMarketButton.onClick.AddListener(CollectWarehouse);

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

        if (recruitSoldierButton != null)
        {
            recruitSoldierButton.onClick.RemoveAllListeners();
            recruitSoldierButton.onClick.AddListener(OpenRecruitPopup);
        }
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
        UpdateSupplyUI();
        RefreshStrategicUpgradeButtons();
        RefreshPedometerUI();
        RefreshRecruitButtonCaption();
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

    public void RefreshPedometerNow() => RefreshPedometerUI();

    /// <summary>
    /// 창고 비행 입금 도착 타이밍에 본영 금화 텍스트를 짧게 강조합니다.
    /// </summary>
    public void PunchLocalGoldText(float strength = 0.12f, float duration = 0.2f, int vibrato = 7)
    {
        if (goldText == null) return;
        var rt = goldText.rectTransform;
        rt.DOKill();
        rt.localScale = Vector3.one;
        rt.DOPunchScale(Vector3.one * strength, duration, vibrato, 0.5f).SetUpdate(true);
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
        RefreshRecruitButtonCaption();
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

    void RefreshStrategicUpgradeButtons()
    {
        var gm = GameManager.InstanceOrNull;
        bool allow = gm != null && gm.CanSpendStrategicPurchases;
        if (laborUpgradeButton != null) laborUpgradeButton.interactable = allow;
        if (marketUpgradeButton != null) marketUpgradeButton.interactable = allow;
        if (warehouseUpgradeButton != null) warehouseUpgradeButton.interactable = allow;
        if (logisticsUpgradeButton != null) logisticsUpgradeButton.interactable = allow;
        if (recruitSoldierButton != null) recruitSoldierButton.interactable = allow;
    }

    void UpdateLaborUI()
    {
        var gm = GameManager.InstanceOrNull;
        if (laborLabelText == null || _controller == null || gm == null) return;

        int lv = gm.clickPowerLevel;
        double current = _controller.GoldPerClick;
        double next = HomeController.BaseGoldPerClick + ((lv + 1) * HomeController.ExtraValuePerLaborLevel);
        double cost = HomeController.UpgradeCost(HomeController.LaborBaseCost, lv);

        laborLabelText.text =
            $"클릭당 금화 획득량 상승\n(Level {lv})\n" +
            $"현재: +{current:F0} Gold/Tap -> 다음: +{next:F0} Gold/Tap\n" +
            $"비용: {cost:F0} Gold";
    }

    void UpdateMarketUI()
    {
        var gm = GameManager.InstanceOrNull;
        if (marketLabelText == null || _controller == null || gm == null) return;

        int lv = gm.autoIncomeLevel;
        double current = lv <= 0 ? 0 : gm.GetAutoIncomeValue(lv);
        double next = lv <= 0 ? 1 : gm.GetAutoIncomeValue(lv + 1);
        double cost = HomeController.UpgradeCost(HomeController.MarketBaseCost, lv);

        marketLabelText.text =
            $"초당 금화 자동 생산량 상승\n(Level {lv})\n" +
            $"현재: +{current:F0} Gold/Sec -> 다음: +{next:F0} Gold/Sec\n" +
            $"비용: {cost:F0} Gold";
    }

    void UpdateLogisticsUI()
    {
        var gm = GameManager.InstanceOrNull;
        if (logisticsLabelText == null || _controller == null || gm == null) return;

        int lv = gm.currentUser?.farmLevel ?? 0;
        double discNow = 0d;
        double discNext = 0d;
        if (DataManager.Instance != null && DataManager.Instance.IsReady)
        {
            var nowRule = DataManager.Instance.GetLevelData(lv);
            var nextRule = DataManager.Instance.GetLevelData(lv + 1);
            if (nowRule != null) discNow = nowRule.logisticsDiscountRate;
            if (nextRule != null) discNext = nextRule.logisticsDiscountRate;
        }

        double cost = HomeController.GetLogisticsUpgradeGoldCost(lv);

        logisticsLabelText.text =
            $"병참 — 보유 병사 일일 유지비 감소\n(Level {lv})\n" +
            $"현재 할인: {discNow:F0}% -> 다음: {discNext:F0}%\n" +
            $"비용: {cost:F0} Gold";
    }

    void UpdateWarehouseUI()
    {
        var gm = GameManager.InstanceOrNull;
        if (warehouseLabelText == null || _controller == null || gm?.currentUser == null) return;

        int lv = gm.currentUser.warehouseLevel;
        double current = _controller.GetMarketMaxCapacity();
        int nextLv = lv + 1;
        double next = current;
        if (DataManager.Instance != null && DataManager.Instance.IsReady)
        {
            var nd = DataManager.Instance.GetLevelData(nextLv);
            if (nd != null && nd.warehouseMaxCapacity > 0) next = nd.warehouseMaxCapacity;
        }
        double cost = HomeController.GetWarehouseUpgradeGoldCost(lv);

        warehouseLabelText.text =
            $"시장 창고 최대 저장량 상승\n(Level {lv})\n" +
            $"현재: {current:F0} Gold -> 다음: {next:F0} Gold\n" +
            $"비용: {cost:F0} Gold";
    }

    void OnVisitorEventRaised(string title, string body)
    {
        EnsureVisitorPopupIfNeeded();
        if (visitorPopupRoot == null)
        {
            Debug.Log($"[방문객 이벤트] {title}: {body}");
            return;
        }

        if (visitorPopupTitleText != null) visitorPopupTitleText.text = string.IsNullOrWhiteSpace(title) ? "방문객 이벤트" : title;
        if (visitorPopupBodyText != null) visitorPopupBodyText.text = string.IsNullOrWhiteSpace(body) ? "방문객이 다녀갔습니다." : body;
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
        visitorPopupRoot.anchorMin = new Vector2(0.5f, 0.5f);
        visitorPopupRoot.anchorMax = new Vector2(0.5f, 0.5f);
        visitorPopupRoot.pivot = new Vector2(0.5f, 0.5f);
        visitorPopupRoot.sizeDelta = new Vector2(520f, 280f);
        var bg = rootGo.GetComponent<Image>();
        bg.color = new Color(0.08f, 0.1f, 0.14f, 0.94f);

        visitorPopupTitleText = CreatePopupText("Title", 26, FontStyles.Bold, new Vector2(0f, 96f));
        visitorPopupBodyText = CreatePopupText("Body", 20, FontStyles.Normal, new Vector2(0f, 16f));
        if (visitorPopupBodyText != null)
        {
            visitorPopupBodyText.enableWordWrapping = true;
            visitorPopupBodyText.alignment = TextAlignmentOptions.Top;
            var rt = visitorPopupBodyText.rectTransform;
            rt.sizeDelta = new Vector2(460f, 140f);
        }

        var closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        closeGo.transform.SetParent(visitorPopupRoot, false);
        var closeRt = closeGo.GetComponent<RectTransform>();
        closeRt.anchorMin = closeRt.anchorMax = new Vector2(0.5f, 0.5f);
        closeRt.anchoredPosition = new Vector2(0f, -104f);
        closeRt.sizeDelta = new Vector2(140f, 44f);
        closeGo.GetComponent<Image>().color = new Color(0.24f, 0.36f, 0.58f, 1f);
        visitorPopupCloseButton = closeGo.GetComponent<Button>();
        visitorPopupCloseButton.onClick.RemoveAllListeners();
        visitorPopupCloseButton.onClick.AddListener(() =>
        {
            if (visitorPopupRoot != null) visitorPopupRoot.gameObject.SetActive(false);
        });
        var closeText = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        closeText.transform.SetParent(closeGo.transform, false);
        closeText.text = "닫기";
        closeText.fontSize = 20;
        closeText.alignment = TextAlignmentOptions.Center;
        closeText.color = Color.white;
        var crt = closeText.rectTransform;
        crt.anchorMin = Vector2.zero;
        crt.anchorMax = Vector2.one;
        crt.offsetMin = Vector2.zero;
        crt.offsetMax = Vector2.zero;

        visitorPopupRoot.gameObject.SetActive(false);
    }

    TextMeshProUGUI CreatePopupText(string name, float fontSize, FontStyles style, Vector2 anchoredPos)
    {
        if (visitorPopupRoot == null) return null;
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(visitorPopupRoot, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        var rt = tmp.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(460f, 48f);
        return tmp;
    }

    void ResolveHomeUpgradeReferencesIfMissing()
    {
        if (warehouseLabelText == null)
            warehouseLabelText = transform.Find("WarehousePanelsRow/WarehouseRow/WarehouseLabelText")
                ?.GetComponent<TextMeshProUGUI>();
        if (warehouseUpgradeButton == null)
            warehouseUpgradeButton = transform.Find("WarehousePanelsRow/WarehouseRow/WarehouseUpgradeButton")
                ?.GetComponent<Button>();
        if (recruitSoldierButton == null)
            recruitSoldierButton = transform.Find("RecruitSoldierButton")?.GetComponent<Button>();
    }

    void RefreshRecruitButtonCaption()
    {
        if (recruitSoldierButton == null) return;
        var tmp = recruitSoldierButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp == null) return;
        var q = GetHomeRecruitQuote();
        double unit = q?.UnitPrice ?? 0d;
        tmp.text = unit > 0d ? $"징집 (1명 {unit:N0} G)" : "징집";
    }

    RecruitController.RecruitQuote? GetHomeRecruitQuote()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null || !dm.IsStateReady) return null;
        string id = dm.HomeCastleId?.Trim();
        if (string.IsNullOrEmpty(id)) return null;
        return RecruitController.TryBuildQuote(id, out var q) ? q : null;
    }

    void OpenRecruitPopup()
    {
        EnsureRecruitPopupIfNeeded();
        if (recruitPopupRoot == null) return;
        _recruitCount = 0;
        _dischargeCount = 0;
        UpdateRecruitPopupText();
        recruitPopupRoot.gameObject.SetActive(true);
    }

    void EnsureRecruitPopupIfNeeded()
    {
        if (recruitPopupRoot != null) return;
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        var rootGo = new GameObject("RecruitSoldierPopup", typeof(RectTransform), typeof(Image));
        recruitPopupRoot = rootGo.GetComponent<RectTransform>();
        recruitPopupRoot.SetParent(canvas.transform, false);
        recruitPopupRoot.anchorMin = recruitPopupRoot.anchorMax = new Vector2(0.5f, 0.5f);
        recruitPopupRoot.pivot = new Vector2(0.5f, 0.5f);
        recruitPopupRoot.sizeDelta = new Vector2(680f, 420f);
        rootGo.GetComponent<Image>().color = new Color(0.09f, 0.11f, 0.16f, 0.96f);

        recruitHeaderTitleText = CreatePopupLabel(recruitPopupRoot, "본영 모병소", new Vector2(0f, 182f), 28, FontStyles.Bold,
            TextAlignmentOptions.Center);
        recruitHeaderTitleText.color = new Color(0.98f, 0.96f, 0.9f, 1f);
        recruitHeaderAssetText = CreatePopupLabel(recruitPopupRoot, "", new Vector2(0f, 150f), 18, FontStyles.Normal,
            TextAlignmentOptions.Center);
        recruitHeaderAssetText.color = new Color(0.84f, 0.88f, 0.94f, 1f);
        recruitPricePerUnitText = CreatePopupLabel(recruitPopupRoot, "", new Vector2(-300f, 118f), 20, FontStyles.Normal, TextAlignmentOptions.Left);
        recruitExpectedCostText = CreatePopupLabel(recruitPopupRoot, "", new Vector2(-300f, 86f), 20, FontStyles.Normal, TextAlignmentOptions.Left);
        recruitMaintenanceDeltaText = CreatePopupLabel(recruitPopupRoot, "", new Vector2(-300f, 54f), 20, FontStyles.Bold, TextAlignmentOptions.Left);
        recruitMaintenanceDeltaText.color = new Color(1f, 0.35f, 0.35f, 1f);
        recruitPostPopulationText = CreatePopupLabel(recruitPopupRoot, "", new Vector2(-300f, 22f), 19, FontStyles.Normal, TextAlignmentOptions.Left);
        recruitEconomicShockText = CreatePopupLabel(recruitPopupRoot, "", new Vector2(-300f, -10f), 19, FontStyles.Normal,
            TextAlignmentOptions.Left);
        recruitWarningText = CreatePopupLabel(recruitPopupRoot, "", new Vector2(0f, -40f), 18, FontStyles.Bold, TextAlignmentOptions.Center);
        recruitWarningText.color = new Color(1f, 0.42f, 0.38f, 1f);

        var inputGo = new GameObject("CountInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        inputGo.transform.SetParent(recruitPopupRoot, false);
        var inputRt = inputGo.GetComponent<RectTransform>();
        inputRt.anchorMin = inputRt.anchorMax = new Vector2(0.5f, 0.5f);
        inputRt.anchoredPosition = new Vector2(0f, -6f);
        inputRt.sizeDelta = new Vector2(250f, 44f);
        inputGo.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.24f, 1f);
        recruitCountInput = inputGo.GetComponent<TMP_InputField>();
        recruitCountInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        var ph = CreatePopupLabel(inputRt, "수량 입력", Vector2.zero, 20, FontStyles.Normal, TextAlignmentOptions.Center);
        ph.color = new Color(0.7f, 0.74f, 0.8f, 0.9f);
        var txt = CreatePopupLabel(inputRt, "0", Vector2.zero, 20, FontStyles.Bold, TextAlignmentOptions.Center);
        txt.color = Color.white;
        recruitCountInput.placeholder = ph;
        recruitCountInput.textComponent = txt;
        recruitCountInput.onValueChanged.AddListener(_ => SyncRecruitCountFromInput());

        recruitPlus100Button = CreatePopupButton(recruitPopupRoot, "+100", new Vector2(-240f, -74f));
        recruitPlus1KButton = CreatePopupButton(recruitPopupRoot, "+1K", new Vector2(-80f, -74f));
        recruitPlus10KButton = CreatePopupButton(recruitPopupRoot, "+10K", new Vector2(80f, -74f));
        recruitMaxButton = CreatePopupButton(recruitPopupRoot, "MAX", new Vector2(240f, -74f));
        recruitConfirmButton = CreatePopupButton(recruitPopupRoot, "징집 확정", new Vector2(220f, -160f), new Color(0.2f, 0.5f, 0.3f, 1f));
        recruitCancelButton = CreatePopupButton(recruitPopupRoot, "닫기", new Vector2(-220f, -160f), new Color(0.35f, 0.38f, 0.45f, 1f));
        dischargeConfirmButton = CreatePopupButton(recruitPopupRoot, "해산 확정", new Vector2(0f, -160f), new Color(0.62f, 0.36f, 0.22f, 1f));

        var dischargeTitle = CreatePopupLabel(recruitPopupRoot, "해산(매도)", new Vector2(0f, -108f), 20, FontStyles.Bold, TextAlignmentOptions.Center);
        dischargeExpectedGoldText = CreatePopupLabel(recruitPopupRoot, "", new Vector2(-300f, -132f), 18, FontStyles.Normal,
            TextAlignmentOptions.Left);
        dischargeMaintenanceReliefText = CreatePopupLabel(recruitPopupRoot, "", new Vector2(-300f, -154f), 18, FontStyles.Normal,
            TextAlignmentOptions.Left);
        dischargeMaintenanceReliefText.color = new Color(0.56f, 0.92f, 0.64f, 1f);
        discharge10PctButton = CreatePopupButton(recruitPopupRoot, "10%", new Vector2(80f, -120f), new Color(0.40f, 0.30f, 0.20f, 1f));
        discharge50PctButton = CreatePopupButton(recruitPopupRoot, "50%", new Vector2(200f, -120f), new Color(0.40f, 0.30f, 0.20f, 1f));
        discharge100PctButton = CreatePopupButton(recruitPopupRoot, "100%", new Vector2(320f, -120f), new Color(0.40f, 0.30f, 0.20f, 1f));

        recruitPlus100Button.onClick.AddListener(() => ChangeRecruitCount(100));
        recruitPlus1KButton.onClick.AddListener(() => ChangeRecruitCount(1000));
        recruitPlus10KButton.onClick.AddListener(() => ChangeRecruitCount(10000));
        recruitMaxButton.onClick.AddListener(SetRecruitCountToMaxAffordable);
        recruitConfirmButton.onClick.AddListener(ConfirmRecruitPurchase);
        discharge10PctButton.onClick.AddListener(() => SetDischargeRatio(0.1f));
        discharge50PctButton.onClick.AddListener(() => SetDischargeRatio(0.5f));
        discharge100PctButton.onClick.AddListener(() => SetDischargeRatio(1f));
        dischargeConfirmButton.onClick.AddListener(ConfirmDischarge);
        recruitCancelButton.onClick.AddListener(() => recruitPopupRoot.gameObject.SetActive(false));

        recruitPopupRoot.gameObject.SetActive(false);
    }

    TextMeshProUGUI CreatePopupLabel(Transform parent, string text, Vector2 pos, float size, FontStyles style, TextAlignmentOptions align)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.color = new Color(0.92f, 0.94f, 0.98f, 1f);
        var rt = tmp.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(600f, 36f);
        return tmp;
    }

    Button CreatePopupButton(Transform parent, string label, Vector2 pos, Color? bg = null)
    {
        var go = new GameObject(label + "Btn", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(130f, 42f);
        go.GetComponent<Image>().color = bg ?? new Color(0.24f, 0.35f, 0.56f, 1f);
        var btn = go.GetComponent<Button>();
        var lbl = CreatePopupLabel(go.transform, label, Vector2.zero, 18f, FontStyles.Bold, TextAlignmentOptions.Center);
        lbl.rectTransform.sizeDelta = rt.sizeDelta;
        return btn;
    }

    void SyncRecruitCountFromInput()
    {
        int.TryParse(recruitCountInput != null ? recruitCountInput.text : "0", NumberStyles.Integer, CultureInfo.InvariantCulture,
            out _recruitCount);
        _recruitCount = Mathf.Max(0, _recruitCount);
        UpdateRecruitPopupText();
    }

    void ChangeRecruitCount(int delta)
    {
        _recruitCount = Mathf.Max(0, _recruitCount + delta);
        if (recruitCountInput != null) recruitCountInput.SetTextWithoutNotify(_recruitCount.ToString());
        UpdateRecruitPopupText();
    }

    void SetRecruitCountToMaxAffordable()
    {
        var q = GetHomeRecruitQuote();
        if (!q.HasValue) return;
        _recruitCount = q.Value.MaxRecruitable;
        if (recruitCountInput != null) recruitCountInput.SetTextWithoutNotify(_recruitCount.ToString());
        UpdateRecruitPopupText();
    }

    void UpdateRecruitPopupText()
    {
        var q = GetHomeRecruitQuote();
        var gm = GameManager.InstanceOrNull;
        var dm = DataManager.InstanceOrNull;
        string homeId = dm?.HomeCastleId?.Trim() ?? "";
        long totalSoldiers = dm != null && dm.IsStateReady ? UserPortfolioManager.GetTotalOwnedSoldiers(dm) : (gm?.currentUser?.soldierCount ?? 0L);
        if (recruitHeaderTitleText != null)
        {
            string cityName = !string.IsNullOrEmpty(homeId) && dm != null ? dm.GetCastleDisplayName(homeId) : "본영";
            recruitHeaderTitleText.text = $"{cityName} 병사 모집/해산";
        }
        if (recruitHeaderAssetText != null)
            recruitHeaderAssetText.text = $"보유 자산: {(gm?.currentGold ?? 0d):N0} Gold | 총 병사: {totalSoldiers:N0}";

        float unit = q?.UnitPrice ?? 0f;
        if (recruitPricePerUnitText != null)
        {
            string coef = q.HasValue
                ? $"(민심x{q.Value.SentimentCoeff:0.00}, 희소x{q.Value.ScarcityCoeff:0.00})"
                : "";
            recruitPricePerUnitText.text = $"예상 1인당 가격: {unit:N0} Gold {coef}";
        }
        double expected = unit * _recruitCount;
        if (recruitExpectedCostText != null)
            recruitExpectedCostText.text = $"예상 비용: {expected:N0} Gold";

        double mult = EconomyManager.ResolveLogisticsMaintenanceMultiplier();
        double perSoldierPerSec = (EconomyManager.InstanceOrNull != null ? EconomyManager.InstanceOrNull.MaintenanceGoldPerSoldierPerDay : 1d) * mult /
                                  86400d;
        double deltaPerSec = perSoldierPerSec * _recruitCount;
        if (recruitMaintenanceDeltaText != null)
            recruitMaintenanceDeltaText.text = $"징집 후 예상 유지비: -{deltaPerSec:F4} G/초";

        var impact = (!string.IsNullOrEmpty(homeId) && _recruitCount > 0)
            ? RecruitController.BuildImpactPreview(homeId, _recruitCount)
            : default;
        if (recruitPostPopulationText != null)
            recruitPostPopulationText.text = $"징집 후 인구: {impact.PostPopulation:N0}";
        if (recruitEconomicShockText != null)
            recruitEconomicShockText.text = $"경제적 충격: 민심 -{impact.SentimentDrop:0.#}, 주가 -{impact.PriceDropPercent * 100f:0.##}% 예상";

        string warn = "";
        bool canRecruit = gm != null && _recruitCount > 0 && q.HasValue;
        if (canRecruit)
        {
            if (_recruitCount > q.Value.MaxByPopulation)
                warn = "징집할 수 있는 백성이 부족합니다.";
            else if (_recruitCount > q.Value.MaxByCapacity)
                warn = "성의 병력 수용 한도를 초과합니다.";
            else if (gm.currentGold < expected)
                warn = "금화가 부족합니다.";
        }
        if (recruitWarningText != null) recruitWarningText.text = warn;
        if (recruitConfirmButton != null)
            recruitConfirmButton.interactable = canRecruit && string.IsNullOrEmpty(warn);

        int have = 0;
        if (dm != null && dm.IsStateReady && !string.IsNullOrEmpty(homeId) && dm.castleStateDataMap.TryGetValue(homeId, out var st) && st != null)
            have = Mathf.Max(0, st.userDeployedTroops);
        if (_dischargeCount > have) _dischargeCount = have;
        long gain = (long)Math.Floor(unit * _dischargeCount);
        if (dischargeExpectedGoldText != null)
            dischargeExpectedGoldText.text = $"회수 금화(예상): +{gain:N0} Gold ({_dischargeCount:N0}명)";
        double relief = perSoldierPerSec * _dischargeCount;
        if (dischargeMaintenanceReliefText != null)
            dischargeMaintenanceReliefText.text = $"해산 시 초당 유지비 +{relief:F4} G 절감";
        if (dischargeConfirmButton != null)
            dischargeConfirmButton.interactable = _dischargeCount > 0;
    }

    void ConfirmRecruitPurchase()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null || !dm.IsStateReady || _recruitCount <= 0) return;
        string homeId = dm.HomeCastleId?.Trim();
        if (string.IsNullOrEmpty(homeId)) return;

        dm.TryRecruitHomeSoldiers(homeId, _recruitCount, out _, out _);
        UpdateFarmWorkersUI();
        PushGlobalTopBar();
        UpdateRecruitPopupText();
        if (recruitPopupRoot != null) recruitPopupRoot.gameObject.SetActive(false);
    }

    void SetDischargeRatio(float ratio)
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null || !dm.IsStateReady) return;
        string homeId = dm.HomeCastleId?.Trim();
        if (string.IsNullOrEmpty(homeId) || !dm.castleStateDataMap.TryGetValue(homeId, out var st) || st == null) return;
        int have = Mathf.Max(0, st.userDeployedTroops);
        _dischargeCount = Mathf.Clamp(Mathf.RoundToInt(have * Mathf.Clamp01(ratio)), 0, have);
        UpdateRecruitPopupText();
    }

    void ConfirmDischarge()
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null || !dm.IsStateReady || _dischargeCount <= 0) return;
        string homeId = dm.HomeCastleId?.Trim();
        if (string.IsNullOrEmpty(homeId)) return;
        dm.TryDischargeHomeSoldiers(homeId, _dischargeCount, out _, out _);
        UpdateFarmWorkersUI();
        PushGlobalTopBar();
        UpdateRecruitPopupText();
    }

    void UpdateSupplyUI()
    {
        if (supplyLabelText == null) return;

        supplyLabelText.text =
            "병사는 <b>징집</b> 버튼 또는 <b>천하</b> 탭에서 매수할 수 있습니다.\n" +
            "재화는 <b>금화</b>만 사용합니다.";
    }
}
