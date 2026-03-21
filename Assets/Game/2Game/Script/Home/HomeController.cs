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

    /// <summary>만보기 목표 걸음 수 (2k, 5k, 7k, 10k)</summary>
    public static readonly int[] StepMilestones = { 2000, 5000, 7000, 10000 };

    /// <summary>목표별 식량 보상</summary>
    public static readonly int[] StepRewardGrain = { 100, 250, 400, 600 };

    static double GetUnixTime() => DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;

    /// <summary> 길게 누르기 시 소수 금화 누적 (프레임마다 정수로 전환) </summary>
    double _gateHoldRemainder;

    /// <summary> 클릭당 금화 </summary>
    public double GoldPerClick
    {
        get
        {
            var gm = GameManager.InstanceOrNull;
            return BaseGoldPerClick + ((gm?.clickPowerLevel ?? 1) * ExtraValuePerLaborLevel);
        }
    }

    /// <summary> 업그레이드 비용 </summary>
    public static double UpgradeCost(double baseCost, int level) =>
        baseCost * Math.Pow(UpgradeCostMult, level);

    /// <summary> 시장 창고 현재 누적량 (Timestamp 기반 동적 계산) </summary>
    public double CurrentMarketAccumulated
    {
        get
        {
            var gm = GameManager.InstanceOrNull;
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
            var gm = GameManager.InstanceOrNull;
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
        var gm = GameManager.InstanceOrNull;
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
        var gm = GameManager.InstanceOrNull;
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
        var gm = GameManager.InstanceOrNull;
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
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null) return 0;
        int lv = gm.currentUser.farmLevel;
        if (DataManager.Instance != null && DataManager.Instance.IsReady)
        {
            var d = DataManager.Instance.GetLevelData(lv);
            if (d != null && d.farmMaxCapacity > 0) return d.farmMaxCapacity;
        }
        return gm.GetAutoIncomeValue(lv) * (gm.balance.vaultHours * 3600);
    }

    /// <summary> 대문 터치 (탭 1회) </summary>
    public void OnGateClick()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm == null) return;
        gm.AddGold((long)GoldPerClick);
    }

    /// <summary>
    /// 대문 길게 누르기 — holdDuration에 따라 가속.
    /// 0~0.5초: 느리게(0.3x) → 0.5~2초: 가속(0.3x→2x) → 2초+: 일정속도(2x).
    /// </summary>
    public void OnGateHoldFrame(float holdDuration)
    {
        var gm = GameManager.InstanceOrNull;
        if (gm == null) return;

        float scale;
        if (holdDuration < 0.5f)
            scale = 0.3f;
        else if (holdDuration < 2f)
            scale = Mathf.Lerp(0.3f, 2f, (holdDuration - 0.5f) / 1.5f);
        else
            scale = 2f;

        double rate = GoldPerClick * scale;
        double add = rate * Time.deltaTime + _gateHoldRemainder;
        long whole = (long)Math.Floor(add);
        _gateHoldRemainder = add - whole;
        if (whole > 0) gm.AddGold(whole);
    }

    /// <summary> 손을 떼면 소수 누적 초기화 </summary>
    public void OnGateHoldEnd()
    {
        _gateHoldRemainder = 0;
    }

    public void UpgradeLabor()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm == null) return;
        double cost = UpgradeCost(LaborBaseCost, gm.clickPowerLevel);
        if (gm.UseGold((long)cost))
            gm.clickPowerLevel++;
    }

    public void UpgradeMarket()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm == null) return;
        double cost = UpgradeCost(MarketBaseCost, gm.autoIncomeLevel);
        if (gm.UseGold((long)cost))
            gm.autoIncomeLevel++;
    }

    public void UpgradeFarm()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm == null) return;
        double cost = UpgradeCost(FarmBaseCost, gm.currentUser.farmLevel);
        if (gm.UseGold((long)cost))
            gm.currentUser.farmLevel++;
    }

    public void HireFarmWorkers(int count)
    {
        var gm = GameManager.InstanceOrNull;
        if (gm == null || count <= 0) return;
        int maxAfford = (int)(gm.currentGold / FarmWorkerCost);
        int actual = Mathf.Min(count, maxAfford);
        if (actual > 0 && gm.UseGold(actual * FarmWorkerCost))
            gm.currentUser.soldierCount += actual;
    }

    public void BuyGrain(int count)
    {
        var gm = GameManager.InstanceOrNull;
        if (gm == null || count <= 0) return;
        int maxAfford = (int)(gm.currentGold / GrainCost);
        int actual = Mathf.Min(count, maxAfford);
        if (actual > 0 && gm.UseGold(actual * GrainCost))
            gm.AddGrain(actual);
    }

    public int GetMaxAffordableFarmWorkers()
    {
        var gm = GameManager.InstanceOrNull;
        return gm != null ? (int)(gm.currentGold / FarmWorkerCost) : 0;
    }

    public int GetMaxAffordableGrain()
    {
        var gm = GameManager.InstanceOrNull;
        return gm != null ? (int)(gm.currentGold / GrainCost) : 0;
    }

    public void CollectMarketGold()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null) return;
        double acc = CurrentMarketAccumulated;
        if (acc <= 0) return;
        gm.AddGold((long)acc);
        gm.currentUser.lastMarketCollectTime = GetUnixTime();
    }

    public void CollectFarmGrain()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null) return;
        double acc = CurrentFarmAccumulated;
        if (acc <= 0) return;
        gm.AddGrain((long)acc);
        gm.currentUser.lastFarmCollectTime = GetUnixTime();
    }

    /// <summary>
    /// 창고(시장 금화 + 농장 식량) 수거를 비행 연출 후 입금으로 처리.
    /// requireActivePiles: true면 CollectionManager에 켜진 더미가 1개 이상 있어야 함 (대문).
    /// </summary>
    public bool TryFlyCollectFromWarehouse(CollectionManager cm, bool requireActivePiles)
    {
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null || cm == null) return false;
        if (cm.IsFlyBusy) return false;

        if (requireActivePiles && !cm.HasActivePileVisual()) return false;

        long totalGold = (long)CurrentMarketAccumulated;
        long totalGrain = (long)CurrentFarmAccumulated;
        if (totalGold <= 0 && totalGrain <= 0) return false;

        double now = GetUnixTime();
        gm.currentUser.lastMarketCollectTime = now;
        gm.currentUser.lastFarmCollectTime = now;

        cm.PlayFlyEffect(totalGold, totalGrain);
        return true;
    }

    /// <summary>
    /// 만보기 분기 보상. 해당 목표 걸음을 넘었고 미수령이면 식량 지급.
    /// </summary>
    public bool ClaimStepReward(int milestoneIndex)
    {
        if (milestoneIndex < 0 || milestoneIndex >= StepMilestones.Length) return false;

        var gm = GameManager.InstanceOrNull;
        var u = gm?.currentUser;
        if (gm == null || u == null) return false;

        if (u.stepRewardsClaimed == null || u.stepRewardsClaimed.Length != StepMilestones.Length)
            u.stepRewardsClaimed = new bool[StepMilestones.Length];

        int need = StepMilestones[milestoneIndex];
        if (u.stepsToday < need) return false;
        if (u.stepRewardsClaimed[milestoneIndex]) return false;

        gm.AddGrain(StepRewardGrain[milestoneIndex]);
        u.stepRewardsClaimed[milestoneIndex] = true;
        gm.SaveUserData();
        return true;
    }

#if UNITY_EDITOR
    void Update()
    {
        // 에디터에서 만보기 테스트: F9 +500, F10 +2000
        if (Input.GetKeyDown(KeyCode.F9))
        {
            var gm = GameManager.InstanceOrNull;
            if (gm?.currentUser != null)
            {
                gm.currentUser.stepsToday += 500;
                gm.currentUser.dailyStepCount = gm.currentUser.stepsToday;
            }
        }
        if (Input.GetKeyDown(KeyCode.F10))
        {
            var gm = GameManager.InstanceOrNull;
            if (gm?.currentUser != null)
            {
                gm.currentUser.stepsToday += 2000;
                gm.currentUser.dailyStepCount = gm.currentUser.stepsToday;
            }
        }
    }
#endif
}
