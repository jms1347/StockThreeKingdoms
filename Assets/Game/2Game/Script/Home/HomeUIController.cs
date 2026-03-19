using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Home 탭 View Controller.
/// TextMeshProUGUI와 Button만 관리. 수치 연산은 하지 않음.
/// UserData Action 이벤트 구독 → UI 갱신.
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
    private HomeUserData _data;

    private Action<int> _onLaborChanged;
    private Action<int> _onMarketChanged;
    private Action<int> _onFarmChanged;
    private Action<long> _onGoldForSupply;

    void Start()
    {
        _controller = GetComponent<HomeController>();
        if (_controller == null) return;

        _data = _controller.Data;
        if (_data == null) return;

        SubscribeEvents();
        _data.NotifyAll();
        BindButtons();

        GameManager.Instance?.RaiseAccumulatedMarketChanged();
        GameManager.Instance?.RaiseAccumulatedFarmChanged();
    }

    void OnDisable()
    {
        UnsubscribeEvents();
    }

    void SubscribeEvents()
    {
        if (_data == null) return;
        _data.OnGoldChanged += OnGoldChangedHandler;
        _data.OnGrainChanged += UpdateGrainUI;
        _data.OnFarmWorkersChanged += UpdateFarmWorkersUI;
        _data.OnLaborLevelChanged += OnLaborLevelChangedHandler;
        _data.OnMarketLevelChanged += OnMarketLevelChangedHandler;
        _data.OnFarmLevelChanged += OnFarmLevelChangedHandler;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnAccumulatedMarketChanged += OnAccumulatedMarketChangedHandler;
            GameManager.Instance.OnAccumulatedFarmChanged += OnAccumulatedFarmChangedHandler;
        }
    }

    void UnsubscribeEvents()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnAccumulatedMarketChanged -= OnAccumulatedMarketChangedHandler;
            GameManager.Instance.OnAccumulatedFarmChanged -= OnAccumulatedFarmChangedHandler;
        }
        if (_data == null) return;
        _data.OnGoldChanged -= OnGoldChangedHandler;
        _data.OnGrainChanged -= UpdateGrainUI;
        _data.OnFarmWorkersChanged -= UpdateFarmWorkersUI;
        _data.OnLaborLevelChanged -= OnLaborLevelChangedHandler;
        _data.OnMarketLevelChanged -= OnMarketLevelChangedHandler;
        _data.OnFarmLevelChanged -= OnFarmLevelChangedHandler;
    }

    void OnAccumulatedMarketChangedHandler(double accumulated, double maxCap)
    {
        if (marketAccumulateText != null)
        {
            marketAccumulateText.text = maxCap > 0 ? $"{accumulated:F0} / {maxCap:F0}" : "0 / 0";
            marketAccumulateText.color = (maxCap > 0 && accumulated >= maxCap) ? Color.red : Color.white;
        }
        if (marketAccumulateSlider != null && maxCap > 0)
            marketAccumulateSlider.value = (float)(accumulated / maxCap);
    }

    void OnAccumulatedFarmChangedHandler(double accumulated, double maxCap)
    {
        if (farmAccumulateText != null)
        {
            farmAccumulateText.text = maxCap > 0 ? $"{accumulated:F0} / {maxCap:F0}" : "0 / 0";
            farmAccumulateText.color = (maxCap > 0 && accumulated >= maxCap) ? Color.red : Color.white;
        }
        if (farmAccumulateSlider != null && maxCap > 0)
            farmAccumulateSlider.value = (float)(accumulated / maxCap);
    }

    void OnGoldChangedHandler(long gold)
    {
        UpdateGoldUI(gold);
        UpdateSupplyUI(gold);
    }

    void OnLaborLevelChangedHandler(int _) => UpdateLaborUI();
    void OnMarketLevelChangedHandler(int _) => UpdateMarketUI();
    void OnFarmLevelChangedHandler(int _) => UpdateFarmUI();

    void BindButtons()
    {
        if (gateButton != null)
            gateButton.onClick.AddListener(() => _controller?.OnGateClick());
        if (laborUpgradeButton != null)
            laborUpgradeButton.onClick.AddListener(() => _controller?.UpgradeLabor());
        if (marketUpgradeButton != null)
            marketUpgradeButton.onClick.AddListener(() => _controller?.UpgradeMarket());
        if (collectMarketButton != null)
            collectMarketButton.onClick.AddListener(() => _controller?.CollectMarketGold());
        if (farmUpgradeButton != null)
            farmUpgradeButton.onClick.AddListener(() => _controller?.UpgradeFarm());
        if (collectFarmButton != null)
            collectFarmButton.onClick.AddListener(() => _controller?.CollectFarmGrain());
        if (hireFarmWorkerButton != null)
            hireFarmWorkerButton.onClick.AddListener(() => _controller?.HireFarmWorkers(1));
        if (buyGrainButton != null)
            buyGrainButton.onClick.AddListener(() => _controller?.BuyGrain(1));
    }

    void UpdateGoldUI(long gold)
    {
        if (goldText != null)
            goldText.text = gold.ToString("N0");
    }

    void UpdateGrainUI(long grain)
    {
        if (grainText != null)
            grainText.text = grain.ToString("N0");
    }

    void UpdateFarmWorkersUI(long farmWorkers)
    {
        if (farmWorkersText != null)
            farmWorkersText.text = farmWorkers.ToString("N0");
    }

    void UpdateLaborUI()
    {
        if (laborLabelText == null || _data == null) return;

        int lv = _data.LaborLevel;
        double current = _data.GoldPerClick;
        double next = HomeUserData.BaseGoldPerClick + ((lv + 1) * HomeUserData.ExtraValuePerLaborLevel);
        double cost = HomeUserData.UpgradeCost(HomeUserData.LaborBaseCost, lv);

        laborLabelText.text =
            $"클릭당 금화 획득량 상승\n(Level {lv})\n" +
            $"현재: +{current:F0} Gold/Tap -> 다음: +{next:F0} Gold/Tap\n" +
            $"비용: {cost:F0} Gold";
    }

    void UpdateMarketUI()
    {
        if (marketLabelText == null || _data == null) return;

        int lv = _data.MarketLevel;
        double current = _data.GoldPerSec;
        double next = lv <= 0 ? 1 : 2 + lv;
        double cost = HomeUserData.UpgradeCost(HomeUserData.MarketBaseCost, lv);

        marketLabelText.text =
            $"초당 금화 자동 생산량 상승\n(Level {lv})\n" +
            $"현재: +{current:F0} Gold/Sec -> 다음: +{next:F0} Gold/Sec\n" +
            $"비용: {cost:F0} Gold";
    }

    void UpdateFarmUI()
    {
        if (farmLabelText == null || _data == null) return;

        int lv = _data.FarmLevel;
        double current = _data.GrainPerSec;
        double next = lv <= 0 ? 1 : 2 + lv;
        double cost = HomeUserData.UpgradeCost(HomeUserData.FarmBaseCost, lv);

        farmLabelText.text =
            $"초당 식량 자동 생산량 상승\n(Level {lv})\n" +
            $"현재: +{current:F0} Grain/Sec -> 다음: +{next:F0} Grain/Sec\n" +
            $"비용: {cost:F0} Gold";
    }

    void UpdateSupplyUI(long _)
    {
        if (supplyLabelText == null || _controller == null) return;

        int maxFarmWorkers = _controller.GetMaxAffordableFarmWorkers();
        int maxGrain = _controller.GetMaxAffordableGrain();

        supplyLabelText.text =
            $"(농장 인력: 최대 {maxFarmWorkers}명 고용 가능)\n" +
            $"(식량: 최대 {maxGrain} 구매 가능)";
    }
}
