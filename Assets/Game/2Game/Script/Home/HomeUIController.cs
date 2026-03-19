using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    private HomeController _controller;

    void Start()
    {
        _controller = GetComponent<HomeController>();
        if (_controller == null) return;

        SubscribeEvents();
        RefreshAllUI();
        BindButtons();

        StartCoroutine(UpdateAccumulateUICoroutine());
    }

    void OnDisable()
    {
        UnsubscribeEvents();
    }

    void SubscribeEvents()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.OnGoldChanged += OnGoldChangedHandler;
        GameManager.Instance.OnGrainChanged += OnGrainChangedHandler;
    }

    void UnsubscribeEvents()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.OnGoldChanged -= OnGoldChangedHandler;
        GameManager.Instance.OnGrainChanged -= OnGrainChangedHandler;
    }

    void OnGoldChangedHandler(long gold)
    {
        UpdateGoldUI(gold);
        UpdateSupplyUI();
    }

    void OnGrainChangedHandler(long grain)
    {
        UpdateGrainUI(grain);
    }

    System.Collections.IEnumerator UpdateAccumulateUICoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.2f);
            if (_controller == null) continue;

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
        }
    }

    void BindButtons()
    {
        if (gateButton != null)
            gateButton.onClick.AddListener(() => { _controller?.OnGateClick(); });
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
            hireFarmWorkerButton.onClick.AddListener(() => { _controller?.HireFarmWorkers(1); UpdateFarmWorkersUI(GameManager.Instance?.currentUser?.soldierCount ?? 0); });
        if (buyGrainButton != null)
            buyGrainButton.onClick.AddListener(() => _controller?.BuyGrain(1));
    }

    void RefreshAllUI()
    {
        if (GameManager.Instance == null) return;
        UpdateGoldUI(GameManager.Instance.currentGold);
        UpdateGrainUI(GameManager.Instance.currentGrain);
        UpdateFarmWorkersUI(GameManager.Instance.currentUser?.soldierCount ?? 0);
        UpdateLaborUI();
        UpdateMarketUI();
        UpdateFarmUI();
        UpdateSupplyUI();
    }

    void UpdateGoldUI(long gold)
    {
        if (goldText != null) goldText.text = gold.ToString("N0");
    }

    void UpdateGrainUI(long grain)
    {
        if (grainText != null) grainText.text = grain.ToString("N0");
    }

    void UpdateFarmWorkersUI(long farmWorkers)
    {
        if (farmWorkersText != null) farmWorkersText.text = farmWorkers.ToString("N0");
    }

    void UpdateLaborUI()
    {
        if (laborLabelText == null || _controller == null || GameManager.Instance == null) return;

        int lv = GameManager.Instance.clickPowerLevel;
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
        if (marketLabelText == null || _controller == null || GameManager.Instance == null) return;

        int lv = GameManager.Instance.autoIncomeLevel;
        double current = lv <= 0 ? 0 : GameManager.Instance.GetAutoIncomeValue(lv);
        double next = lv <= 0 ? 1 : GameManager.Instance.GetAutoIncomeValue(lv + 1);
        double cost = HomeController.UpgradeCost(HomeController.MarketBaseCost, lv);

        marketLabelText.text =
            $"초당 금화 자동 생산량 상승\n(Level {lv})\n" +
            $"현재: +{current:F0} Gold/Sec -> 다음: +{next:F0} Gold/Sec\n" +
            $"비용: {cost:F0} Gold";
    }

    void UpdateFarmUI()
    {
        if (farmLabelText == null || _controller == null || GameManager.Instance == null) return;

        int lv = GameManager.Instance.currentUser?.farmLevel ?? 0;
        double current = lv <= 0 ? 0 : GameManager.Instance.GetAutoIncomeValue(lv);
        double next = lv <= 0 ? 1 : GameManager.Instance.GetAutoIncomeValue(lv + 1);
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
