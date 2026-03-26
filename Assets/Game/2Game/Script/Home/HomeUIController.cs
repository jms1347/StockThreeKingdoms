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
        SubscribeStepEvents();
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
        UnsubscribeStepEvents();
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

    void SubscribeStepEvents()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm == null) return;
        gm.OnStepsChanged += OnStepsTodayChangedHandler;
    }

    void UnsubscribeStepEvents()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm == null) return;
        gm.OnStepsChanged -= OnStepsTodayChangedHandler;
    }

    void OnStepsTodayChangedHandler(int _) => RefreshPedometerNow();

    void OnGoldChangedHandler(long gold)
    {
        RollGoldDisplay(gold);
        PushGlobalTopBar();
        UpdateSupplyUI();
    }

    void OnGrainChangedHandler(long grain)
    {
        RollGrainDisplay(grain);
        PushGlobalTopBar();
    }

    System.Collections.IEnumerator UpdateAccumulateUICoroutine()
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

            double fAcc = _controller.CurrentFarmAccumulated;
            double fMax = _controller.GetFarmMaxCapacity();
            if (collectionManager != null && collectionManager.IsFlyBusy) fAcc = 0;
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
        if (hireFarmWorkerButton == null)
            hireFarmWorkerButton = transform.Find("SupplyPanel/SupplyButtons/HireFarmWorkerButton")?.GetComponent<Button>();
        if (buyGrainButton == null)
            buyGrainButton = transform.Find("SupplyPanel/SupplyButtons/BuyGrainButton")?.GetComponent<Button>();

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
        WireHoldRepeat(laborUpgradeButton, () =>
        {
            _controller?.UpgradeLabor();
            UpdateLaborUI();
            UpdateSupplyUI();
        });
        WireHoldRepeat(marketUpgradeButton, () =>
        {
            _controller?.UpgradeMarket();
            UpdateMarketUI();
            UpdateSupplyUI();
        });
        if (collectMarketButton != null)
            collectMarketButton.onClick.AddListener(() =>
                _controller?.TryFlyCollectFromWarehouse(collectionManager, requireActivePiles: false));
        WireHoldRepeat(farmUpgradeButton, () =>
        {
            _controller?.UpgradeFarm();
            UpdateFarmUI();
            UpdateSupplyUI();
        });
        // 농장 수거는 창고 수거(단일 버튼)로 통합됨
        WireHoldRepeat(hireFarmWorkerButton, () =>
        {
            _controller?.HireFarmWorkers(1);
            UpdateFarmWorkersUI(GameManager.InstanceOrNull?.currentUser?.soldierCount ?? 0);
            UpdateSupplyUI();
            PushGlobalTopBar();
        });
        WireHoldRepeat(buyGrainButton, () =>
        {
            _controller?.BuyGrain(1);
            UpdateSupplyUI();
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
                        UpdateGrainUI(GameManager.InstanceOrNull?.currentGrain ?? 0, instant: true);
                    }
                });
            }
        }
    }

    /// <summary>탭 1회 + 길게 누르면 재화가 될 때까지 반복 (대문 홀드와 유사 UX).</summary>
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
        UpdateGrainUI(gm.currentGrain, instant: true);
        UpdateFarmWorkersUI(gm.currentUser?.soldierCount ?? 0);
        PushGlobalTopBar();
        UpdateLaborUI();
        UpdateMarketUI();
        UpdateFarmUI();
        UpdateSupplyUI();
        RefreshPedometerUI();
    }

    void RollGoldDisplay(long target)
    {
        if (goldText == null) return; // 로컬 ResourceBar를 쓰는 씬에서만
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
        if (grainText == null) return; // 로컬 ResourceBar를 쓰는 씬에서만
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
        if (goldText == null) return; // 로컬 ResourceBar를 쓰는 씬에서만
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
        if (grainText == null) return; // 로컬 ResourceBar를 쓰는 씬에서만
        if (instant)
        {
            _grainRollTween?.Kill();
            _displayGrain = grain;
            grainText.text = grain.ToString("N0");
        }
        else
            RollGrainDisplay(grain);
    }

    /// <summary>만보기 텍스트·게이지·버튼을 즉시 갱신 (에디터 테스트용 등)</summary>
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
                int grain = i < HomeController.StepRewardGrain.Length ? HomeController.StepRewardGrain[i] : 0;
                // 텍스트는 짧게(레이아웃 깨짐 방지). 라벨은 아이콘으로 대체 예정이므로 군더더기 제거.
                string state = claimed ? "완료" : canClaim ? "수령" : "";
                stepRewardLabels[i].text = state.Length > 0
                    ? $"{HomeController.StepMilestones[i]:N0}\n+{grain}\n{state}"
                    : $"{HomeController.StepMilestones[i]:N0}\n+{grain}";
            }
        }
    }

    void UpdateFarmWorkersUI(long farmWorkers)
    {
        if (farmWorkersText != null) farmWorkersText.text = farmWorkers.ToString("N0"); // 구 로컬 표시용(선택)
    }

    void PushGlobalTopBar()
    {
        var gm = GameManager.InstanceOrNull;
        var gui = GlobalUIManager.InstanceOrNull;
        if (gm?.currentUser == null || gui == null) return;

        // 홈탭은 로컬 ResourceBar 대신 GlobalUI 탑바에 표시
        string userName = gm.currentUser.userName;
        gui.SetTopBarNumbers(userName, gm.currentGold, gm.currentGrain, gm.currentUser.soldierCount);
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
            $"(병사: 최대 {maxFarmWorkers}명 모집 가능)\n" +
            $"(식량: 최대 {maxGrain} 구매 가능)";
    }
}
