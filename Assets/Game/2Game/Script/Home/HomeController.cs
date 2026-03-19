using UnityEngine;
using System;

/// <summary>
/// 본영 탭 전용 로직 매니저. 계산·조작만 담당. GameManager를 통해 재화 변경.
/// </summary>
public class HomeController : MonoBehaviour
{
    // ---- 밸런스 상수 ----
    public const int BaseGoldPerClick = 10;
    public const int ExtraValuePerLaborLevel = 5;
    public const double UpgradeCostMult = 1.15;
    public const int FarmWorkerCost = 100;
    public const int GrainCost = 2;
    public const double LaborBaseCost = 50;
    public const double MarketBaseCost = 100;
    public const double FarmBaseCost = 80;

    static double GetUnixTime() => DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;

    /// <summary> 클릭당 금화 </summary>
    public double GoldPerClick =>
        BaseGoldPerClick + (GameManager.Instance.clickPowerLevel * ExtraValuePerLaborLevel);

    /// <summary> 업그레이드 비용 </summary>
    public static double UpgradeCost(double baseCost, int level) =>
        baseCost * Math.Pow(UpgradeCostMult, level);

    /// <summary> 시장 창고 현재 누적량 (Timestamp 기반 동적 계산) </summary>
    public double CurrentMarketAccumulated
    {
        get
        {
            var gm = GameManager.Instance;
            if (gm?.currentUser == null) return 0;
            double rate = GetMarketValuePerSec();
            if (rate <= 0) return 0;
            double now = GetUnixTime();
            double last = gm.currentUser.lastMarketCollectTime <= 0 ? now : gm.currentUser.lastMarketCollectTime;
            double elapsed = Math.Max(0, now - last);
            double maxCap = GetMarketMaxCapacity();
            return Math.Min(elapsed * rate, maxCap > 0 ? maxCap : double.MaxValue);
        }
    }

    /// <summary> 농장 창고 현재 누적량 </summary>
    public double CurrentFarmAccumulated
    {
        get
        {
            var gm = GameManager.Instance;
            if (gm?.currentUser == null) return 0;
            double rate = GetFarmValuePerSec();
            if (rate <= 0) return 0;
            double now = GetUnixTime();
            double last = gm.currentUser.lastFarmCollectTime <= 0 ? now : gm.currentUser.lastFarmCollectTime;
            double elapsed = Math.Max(0, now - last);
            double maxCap = GetFarmMaxCapacity();
            return Math.Min(elapsed * rate, maxCap > 0 ? maxCap : double.MaxValue);
        }
    }

    double GetMarketValuePerSec()
    {
        var gm = GameManager.Instance;
        if (gm?.currentUser == null) return 0;
        if (DataManager.Instance != null && DataManager.Instance.IsReady)
        {
            var d = DataManager.Instance.GetLevelData(gm.currentUser.marketLevel);
            if (d != null && d.marketValuePerSec > 0) return d.marketValuePerSec;
        }
        return gm.GetAutoIncomeValue(gm.currentUser.marketLevel);
    }

    double GetFarmValuePerSec()
    {
        var gm = GameManager.Instance;
        if (gm?.currentUser == null) return 0;
        if (DataManager.Instance != null && DataManager.Instance.IsReady)
        {
            var d = DataManager.Instance.GetLevelData(gm.currentUser.farmLevel);
            if (d != null && d.farmValuePerSec > 0) return d.farmValuePerSec;
        }
        return gm.GetAutoIncomeValue(gm.currentUser.farmLevel);
    }

    public double GetMarketMaxCapacity()
    {
        var gm = GameManager.Instance;
        if (gm?.currentUser == null) return 0;
        int lv = gm.currentUser.marketLevel;
        if (DataManager.Instance != null && DataManager.Instance.IsReady)
        {
            var d = DataManager.Instance.GetLevelData(lv);
            if (d != null && d.marketMaxCapacity > 0) return d.marketMaxCapacity;
        }
        return gm.GetAutoIncomeValue(lv) * (gm.balance.vaultHours * 3600);
    }

    public double GetFarmMaxCapacity()
    {
        var gm = GameManager.Instance;
        if (gm?.currentUser == null) return 0;
        int lv = gm.currentUser.farmLevel;
        if (DataManager.Instance != null && DataManager.Instance.IsReady)
        {
            var d = DataManager.Instance.GetLevelData(lv);
            if (d != null && d.farmMaxCapacity > 0) return d.farmMaxCapacity;
        }
        return gm.GetAutoIncomeValue(lv) * (gm.balance.vaultHours * 3600);
    }

    /// <summary> 대문 터치 </summary>
    public void OnGateClick()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.AddGold((long)GoldPerClick);
    }

    public void UpgradeLabor()
    {
        if (GameManager.Instance == null) return;
        double cost = UpgradeCost(LaborBaseCost, GameManager.Instance.clickPowerLevel);
        if (GameManager.Instance.UseGold((long)cost))
            GameManager.Instance.clickPowerLevel++;
    }

    public void UpgradeMarket()
    {
        if (GameManager.Instance == null) return;
        double cost = UpgradeCost(MarketBaseCost, GameManager.Instance.autoIncomeLevel);
        if (GameManager.Instance.UseGold((long)cost))
            GameManager.Instance.autoIncomeLevel++;
    }

    public void UpgradeFarm()
    {
        if (GameManager.Instance == null) return;
        double cost = UpgradeCost(FarmBaseCost, GameManager.Instance.currentUser.farmLevel);
        if (GameManager.Instance.UseGold((long)cost))
            GameManager.Instance.currentUser.farmLevel++;
    }

    public void HireFarmWorkers(int count)
    {
        if (GameManager.Instance == null || count <= 0) return;
        int maxAfford = (int)(GameManager.Instance.currentGold / FarmWorkerCost);
        int actual = Mathf.Min(count, maxAfford);
        if (actual > 0 && GameManager.Instance.UseGold(actual * FarmWorkerCost))
            GameManager.Instance.currentUser.soldierCount += actual;
    }

    public void BuyGrain(int count)
    {
        if (GameManager.Instance == null || count <= 0) return;
        int maxAfford = (int)(GameManager.Instance.currentGold / GrainCost);
        int actual = Mathf.Min(count, maxAfford);
        if (actual > 0 && GameManager.Instance.UseGold(actual * GrainCost))
            GameManager.Instance.AddGrain(actual);
    }

    public int GetMaxAffordableFarmWorkers() =>
        GameManager.Instance != null ? (int)(GameManager.Instance.currentGold / FarmWorkerCost) : 0;

    public int GetMaxAffordableGrain() =>
        GameManager.Instance != null ? (int)(GameManager.Instance.currentGold / GrainCost) : 0;

    public void CollectMarketGold()
    {
        if (GameManager.Instance?.currentUser == null) return;
        double acc = CurrentMarketAccumulated;
        if (acc <= 0) return;
        GameManager.Instance.AddGold((long)acc);
        GameManager.Instance.currentUser.lastMarketCollectTime = GetUnixTime();
    }

    public void CollectFarmGrain()
    {
        if (GameManager.Instance?.currentUser == null) return;
        double acc = CurrentFarmAccumulated;
        if (acc <= 0) return;
        GameManager.Instance.AddGrain((long)acc);
        GameManager.Instance.currentUser.lastFarmCollectTime = GetUnixTime();
    }
}
