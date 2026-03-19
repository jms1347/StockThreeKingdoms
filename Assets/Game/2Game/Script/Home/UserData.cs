using System;

/// <summary>
/// Home 탭용 유저 데이터 모델. GameManager.currentUser(저장용)와 동기화.
/// 각 데이터 변동 시 Action 이벤트를 발생시킨다. (Observer 패턴)
/// </summary>
public class HomeUserData
{
    // ---- Action 이벤트 (데이터 변동 시 호출) ----
    public Action<long> OnGoldChanged;
    public Action<long> OnGrainChanged;
    public Action<long> OnFarmWorkersChanged;
    public Action<int> OnLaborLevelChanged;
    public Action<int> OnMarketLevelChanged;
    public Action<int> OnFarmLevelChanged;

    // ---- 밸런스 상수 (계산식용) ----
    public const int BaseGoldPerClick = 10;
    public const int ExtraValuePerLaborLevel = 5;
    public const double UpgradeCostMult = 1.15;
    public const int FarmWorkerCost = 100;
    public const int GrainCost = 2;

    // 노동력/시장/농장 업그레이드 기본 비용
    public const double LaborBaseCost = 50;
    public const double MarketBaseCost = 100;
    public const double FarmBaseCost = 80;

    private readonly UserData _data;  // GameManager.currentUser 참조

    public HomeUserData(UserData persistData)
    {
        _data = persistData ?? new UserData();
    }

    /// <summary> 클릭당 금화 = BaseGold(10) + (LaborLevel × 5) </summary>
    public double GoldPerClick =>
        BaseGoldPerClick + (LaborLevel * ExtraValuePerLaborLevel);

    /// <summary> 시장 초당 금화 생산량 </summary>
    public double GoldPerSec => MarketLevel <= 0 ? 0 : 1 + MarketLevel;
    /// <summary> 농장 초당 식량 생산량 </summary>
    public double GrainPerSec => FarmLevel <= 0 ? 0 : 1 + FarmLevel;

    /// <summary> 업그레이드 비용 = BaseCost × 1.15^Level </summary>
    public static double UpgradeCost(double baseCost, int level) =>
        baseCost * Math.Pow(UpgradeCostMult, level);

    // ---- 프로퍼티 (Set 시 _data 갱신 + 해당 이벤트 Invoke) ----
    public long Gold
    {
        get => _data.gold;
        set { _data.gold = Math.Max(0, value); OnGoldChanged?.Invoke(_data.gold); }
    }

    public long Grain
    {
        get => _data.grain;
        set { _data.grain = Math.Max(0, value); OnGrainChanged?.Invoke(_data.grain); }
    }

    public long FarmWorkers
    {
        get => _data.soldierCount;
        set { _data.soldierCount = Math.Max(0, value); OnFarmWorkersChanged?.Invoke(_data.soldierCount); }
    }

    public int LaborLevel
    {
        get => Math.Max(1, _data.laborLevel);
        set { _data.laborLevel = Math.Max(1, value); OnLaborLevelChanged?.Invoke(_data.laborLevel); }
    }

    public int MarketLevel
    {
        get => Math.Max(0, _data.marketLevel);
        set { _data.marketLevel = Math.Max(0, value); OnMarketLevelChanged?.Invoke(_data.marketLevel); }
    }

    public int FarmLevel
    {
        get => Math.Max(0, _data.farmLevel);
        set { _data.farmLevel = Math.Max(0, value); OnFarmLevelChanged?.Invoke(_data.farmLevel); }
    }

    /// <summary> 시장 창고 누적 금화 </summary>
    public double AccumulatedMarketGold => _data.accumulatedMarketGold;

    /// <summary> 농장 창고 누적 식량 </summary>
    public double AccumulatedFarmGrain => _data.accumulatedFarmGrain;

    public void SetAccumulatedMarketGold(double value)
    {
        _data.accumulatedMarketGold = Math.Max(0, value);
        GameManager.Instance?.RaiseAccumulatedMarketChanged();
    }

    public void SetAccumulatedFarmGrain(double value)
    {
        _data.accumulatedFarmGrain = Math.Max(0, value);
        GameManager.Instance?.RaiseAccumulatedFarmChanged();
    }

    /// <summary> 현재 시장 레벨 기준 창고 MAX치 </summary>
    public double GetMarketMaxCapacity()
    {
        if (GameManager.Instance != null && DataManager.Instance != null && DataManager.Instance.IsReady)
        {
            var d = DataManager.Instance.GetLevelData(MarketLevel);
            if (d != null && d.marketMaxCapacity > 0) return d.marketMaxCapacity;
        }
        return GameManager.Instance != null ? GameManager.Instance.GetMarketMaxCapacity() : 0;
    }

    /// <summary> 현재 농장 레벨 기준 창고 MAX치 </summary>
    public double GetFarmMaxCapacity()
    {
        if (GameManager.Instance != null && DataManager.Instance != null && DataManager.Instance.IsReady)
        {
            var d = DataManager.Instance.GetLevelData(FarmLevel);
            if (d != null && d.farmMaxCapacity > 0) return d.farmMaxCapacity;
        }
        return GameManager.Instance != null ? GameManager.Instance.GetFarmMaxCapacity() : 0;
    }

    /// <summary> 로드 직후 UI 초기화용. 모든 이벤트를 한 번씩 호출. </summary>
    public void NotifyAll()
    {
        OnGoldChanged?.Invoke(_data.gold);
        OnGrainChanged?.Invoke(_data.grain);
        OnFarmWorkersChanged?.Invoke(_data.soldierCount);
        OnLaborLevelChanged?.Invoke(LaborLevel);
        OnMarketLevelChanged?.Invoke(MarketLevel);
        OnFarmLevelChanged?.Invoke(FarmLevel);
    }
}
