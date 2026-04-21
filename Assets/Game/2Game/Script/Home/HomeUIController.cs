using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

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

    [Header("업그레이드 UI - 병참 (유지비 할인)")]
    [FormerlySerializedAs("farmLabelText")]
    public TextMeshProUGUI logisticsLabelText;
    [FormerlySerializedAs("farmUpgradeButton")]
    public Button logisticsUpgradeButton;

    [Header("보급 UI")]
    public TextMeshProUGUI supplyLabelText;

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

    [Header("숫자 롤링")]
    public float resourceRollDuration = 0.42f;

    HomeController _controller;
    double _displayGold;
    Tweener _goldRollTween;

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
        PushGlobalTopBar();
    }

    void Start()
    {
        if (_controller == null) return;

        if (gateButton == null)
            gateButton = transform.Find("GateButton")?.GetComponent<Button>();

        FixHomeCanvasScaleIfBroken();
        EnsureUiInputInfrastructure();

        SubscribeEvents();
        SubscribeStepEvents();

        RefreshAllUI();
        BindButtons();

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
        UpdateLogisticsUI();
        UpdateSupplyUI();
        RefreshStrategicUpgradeButtons();
        RefreshPedometerUI();
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

    void RefreshPedometerUI()
    {
        var u = GameManager.InstanceOrNull?.currentUser;
        if (u == null) return;

        int steps = u.stepsToday;
        if (pedometerGaugeFill != null)
            pedometerGaugeFill.fillAmount = Mathf.Clamp01(steps / 10000f);
        if (pedometerStepsText != null)
            pedometerStepsText.text = $"{steps:N0} / 10,000";

        if (u.stepRewardsClaimed == null || u.stepRewardsClaimed.Length != HomeController.StepMilestones.Length)
            u.stepRewardsClaimed = new bool[HomeController.StepMilestones.Length];

        for (int i = 0; i < HomeController.StepMilestones.Length && i < stepRewardButtons.Length; i++)
        {
            var btn = stepRewardButtons[i];
            if (btn == null) continue;
            bool claimed = u.stepRewardsClaimed[i];
            bool canClaim = steps >= HomeController.StepMilestones[i] && !claimed;
            btn.interactable = canClaim;

            if (i < stepRewardLabels.Length && stepRewardLabels[i] != null)
            {
                int rw = i < HomeController.StepRewardMarchPoints.Length ? HomeController.StepRewardMarchPoints[i] : 0;
                string state = claimed ? "완료" : canClaim ? "수령" : "";
                stepRewardLabels[i].text = state.Length > 0
                    ? $"{HomeController.StepMilestones[i]:N0}\n+{rw} MP\n{state}"
                    : $"{HomeController.StepMilestones[i]:N0}\n+{rw} MP";
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

    void RefreshStrategicUpgradeButtons()
    {
        var gm = GameManager.InstanceOrNull;
        bool allow = gm != null && gm.CanSpendStrategicPurchases;
        if (laborUpgradeButton != null) laborUpgradeButton.interactable = allow;
        if (marketUpgradeButton != null) marketUpgradeButton.interactable = allow;
        if (logisticsUpgradeButton != null) logisticsUpgradeButton.interactable = allow;
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
        double discNow = Math.Min(0.5d, lv * 0.02d) * 100d;
        double discNext = Math.Min(0.5d, (lv + 1) * 0.02d) * 100d;
        double cost = HomeController.UpgradeCost(HomeController.LogisticsBaseCost, lv);

        logisticsLabelText.text =
            $"병참 — 보유 병사 일일 유지비 감소\n(Level {lv})\n" +
            $"현재 할인: {discNow:F0}% -> 다음: {discNext:F0}%\n" +
            $"비용: {cost:F0} Gold";
    }

    void UpdateSupplyUI()
    {
        if (supplyLabelText == null) return;

        supplyLabelText.text =
            "병사는 <b>천하</b> 탭에서 AI 수비군을 매수하세요.\n" +
            "재화는 <b>금화</b>만 사용합니다.";
    }
}
