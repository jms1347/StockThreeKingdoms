using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// 본영 화면 담당. GameManager.OnGoldChanged/OnGrainChanged 구독, 코루틴으로 창고 UI 갱신.
/// </summary>
[RequireComponent(typeof(HomeController))]
public class HomeUIController : MonoBehaviour
{
    [Header("자원 표시")]
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI grainText;
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

    [Header("업그레이드 UI - 농장")]
    public TextMeshProUGUI farmLabelText;
    public TextMeshProUGUI farmAccumulateText;
    public Slider farmAccumulateSlider;
    public Button farmUpgradeButton;
    public Button collectFarmButton;

    [Header("보급 UI")]
    public TextMeshProUGUI supplyLabelText;
    public Button hireFarmWorkerButton;
    public Button buyGrainButton;

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

    private HomeController _controller;
    long _displayGold;
    long _displayGrain;
    Tweener _goldRollTween;
    Tweener _grainRollTween;

    void Start()
    {
        _controller = GetComponent<HomeController>();
        if (_controller == null) return;

        if (collectionManager == null)
            collectionManager = GetComponent<CollectionManager>();

        // gateButton 참조가 Inspector에서 빠진 경우 자동 탐색
        if (gateButton == null)
            gateButton = transform.Find("GateButton")?.GetComponent<Button>();

        SubscribeEvents();
        RefreshAllUI();
        BindButtons();

        StartCoroutine(UpdateAccumulateUICoroutine());
    }

    void OnDestroy()
    {
        _goldRollTween?.Kill();
        _grainRollTween?.Kill();
    }

    void OnDisable()
    {
        UnsubscribeEvents();
    }

    void SubscribeEvents()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm == null) return;
        gm.OnGoldChanged += OnGoldChangedHandler;
        gm.OnGrainChanged += OnGrainChangedHandler;
    }

    void UnsubscribeEvents()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm == null) return;
        gm.OnGoldChanged -= OnGoldChangedHandler;
        gm.OnGrainChanged -= OnGrainChangedHandler;
    }

    void OnGoldChangedHandler(long gold)
    {
        RollGoldDisplay(gold);
        UpdateSupplyUI();
    }

    void OnGrainChangedHandler(long grain)
    {
        RollGrainDisplay(grain);
    }

    System.Collections.IEnumerator UpdateAccumulateUICoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.2f);
            if (_controller == null || GameManager.InstanceOrNull == null) continue;

            double mAcc = _controller.CurrentMarketAccumulated;
            double mMax = _controller.GetMarketMaxCapacity();
            if (marketAccumulateText != null)
            {
                marketAccumulateText.text = mMax > 0 ? $"{mAcc:F0} / {mMax:F0}" : "0 / 0";
                marketAccumulateText.color = (mMax > 0 && mAcc >= mMax) ? Color.red : Color.white;
            }
            if (marketAccumulateSlider != null && mMax > 0)
                marketAccumulateSlider.value = (float)Math.Min(1.0, mAcc / mMax);

            double fAcc = _controller.CurrentFarmAccumulated;
            double fMax = _controller.GetFarmMaxCapacity();
            if (farmAccumulateText != null)
            {
                farmAccumulateText.text = fMax > 0 ? $"{fAcc:F0} / {fMax:F0}" : "0 / 0";
                farmAccumulateText.color = (fMax > 0 && fAcc >= fMax) ? Color.red : Color.white;
            }
            if (farmAccumulateSlider != null && fMax > 0)
                farmAccumulateSlider.value = (float)Math.Min(1.0, fAcc / fMax);

            RefreshPedometerUI();
        }
    }

    void BindButtons()
    {
        if (gateButton == null)
        {
            Debug.LogWarning("[HomeUIController] gateButton이 연결되지 않았습니다. Inspector에서 GateButton을 할당하세요.");
        }
        else
        {
            var hold = gateButton.GetComponent<GateButtonHold>();
            if (hold == null) hold = gateButton.gameObject.AddComponent<GateButtonHold>();
            hold.controller = _controller;
            hold.collectionManager = collectionManager;
        }
        if (laborUpgradeButton != null)
            laborUpgradeButton.onClick.AddListener(() => { _controller?.UpgradeLabor(); UpdateLaborUI(); });
        if (marketUpgradeButton != null)
            marketUpgradeButton.onClick.AddListener(() => { _controller?.UpgradeMarket(); UpdateMarketUI(); });
        if (collectMarketButton != null)
            collectMarketButton.onClick.AddListener(() => _controller?.CollectMarketGold());
        if (farmUpgradeButton != null)
            farmUpgradeButton.onClick.AddListener(() => { _controller?.UpgradeFarm(); UpdateFarmUI(); });
        if (collectFarmButton != null)
            collectFarmButton.onClick.AddListener(() => _controller?.CollectFarmGrain());
        if (hireFarmWorkerButton != null)
            hireFarmWorkerButton.onClick.AddListener(() => { _controller?.HireFarmWorkers(1); UpdateFarmWorkersUI(GameManager.InstanceOrNull?.currentUser?.soldierCount ?? 0); });
        if (buyGrainButton != null)
            buyGrainButton.onClick.AddListener(() => _controller?.BuyGrain(1));

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
                        UpdateGrainUI(GameManager.InstanceOrNull?.currentGrain ?? 0, instant: true);
                    }
                });
            }
        }
    }

    void RefreshAllUI()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm == null) return;
        UpdateGoldUI(gm.currentGold, instant: true);
        UpdateGrainUI(gm.currentGrain, instant: true);
        UpdateFarmWorkersUI(gm.currentUser?.soldierCount ?? 0);
        UpdateLaborUI();
        UpdateMarketUI();
        UpdateFarmUI();
        UpdateSupplyUI();
        RefreshPedometerUI();
    }

    void RollGoldDisplay(long target)
    {
        if (goldText == null) return;
        _goldRollTween?.Kill();
        long start = _displayGold;
        float p = 0f;
        _goldRollTween = DOTween.To(() => p, x =>
        {
            p = x;
            float u = Mathf.Clamp01(x);
            _displayGold = (long)(start + (target - start) * (double)u);
            goldText.text = _displayGold.ToString("N0");
        }, 1f, resourceRollDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true);
    }

    void RollGrainDisplay(long target)
    {
        if (grainText == null) return;
        _grainRollTween?.Kill();
        long start = _displayGrain;
        float p = 0f;
        _grainRollTween = DOTween.To(() => p, x =>
        {
            p = x;
            float u = Mathf.Clamp01(x);
            _displayGrain = (long)(start + (target - start) * (double)u);
            grainText.text = _displayGrain.ToString("N0");
        }, 1f, resourceRollDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true);
    }

    void UpdateGoldUI(long gold, bool instant)
    {
        if (goldText == null) return;
        if (instant)
        {
            _goldRollTween?.Kill();
            _displayGold = gold;
            goldText.text = gold.ToString("N0");
        }
        else
            RollGoldDisplay(gold);
    }

    void UpdateGrainUI(long grain, bool instant)
    {
        if (grainText == null) return;
        if (instant)
        {
            _grainRollTween?.Kill();
            _displayGrain = grain;
            grainText.text = grain.ToString("N0");
        }
        else
            RollGrainDisplay(grain);
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
                int grain = i < HomeController.StepRewardGrain.Length ? HomeController.StepRewardGrain[i] : 0;
                string state = claimed ? "(수령완료)" : canClaim ? "탭하여 수령" : "(미달성)";
                stepRewardLabels[i].text = $"{HomeController.StepMilestones[i]:N0}보\n+{grain} 식량\n{state}";
            }
        }
    }

    void UpdateFarmWorkersUI(long farmWorkers)
    {
        if (farmWorkersText != null) farmWorkersText.text = farmWorkers.ToString("N0");
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

    void UpdateFarmUI()
    {
        var gm = GameManager.InstanceOrNull;
        if (farmLabelText == null || _controller == null || gm == null) return;

        int lv = gm.currentUser?.farmLevel ?? 0;
        double current = lv <= 0 ? 0 : gm.GetAutoIncomeValue(lv);
        double next = lv <= 0 ? 1 : gm.GetAutoIncomeValue(lv + 1);
        double cost = HomeController.UpgradeCost(HomeController.FarmBaseCost, lv);

        farmLabelText.text =
            $"초당 식량 자동 생산량 상승\n(Level {lv})\n" +
            $"현재: +{current:F0} Grain/Sec -> 다음: +{next:F0} Grain/Sec\n" +
            $"비용: {cost:F0} Gold";
    }

    void UpdateSupplyUI()
    {
        if (supplyLabelText == null || _controller == null) return;

        int maxFarmWorkers = _controller.GetMaxAffordableFarmWorkers();
        int maxGrain = _controller.GetMaxAffordableGrain();

        supplyLabelText.text =
            $"(농장 인력: 최대 {maxFarmWorkers}명 고용 가능)\n" +
            $"(식량: 최대 {maxGrain} 구매 가능)";
    }
}
