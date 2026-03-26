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

    static long NowUnixSeconds() => TimeManager.GetUnixNow();

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

    /// <summary> 시장 창고 현재 누적량 (1분 단위 Floor × 분당 생산, MaxCap 한도) </summary>
    public double CurrentMarketAccumulated
    {
        get
        {
            var gm = GameManager.InstanceOrNull;
            if (gm?.currentUser == null) return 0;
            double ratePerSec = GetMarketValuePerSec();
            if (ratePerSec <= 0) return 0;
            gm.EnsureWarehouseBaselines();
            long now = NowUnixSeconds();
            long last = gm.currentUser.lastMarketCollectTime;
            if (last <= 0) return 0;
            long elapsedSec = Math.Max(0, now - last);
            long wholeMinutes = elapsedSec / 60;
            double perMinute = ratePerSec * 60.0;
            double raw = wholeMinutes * perMinute;
            double maxCap = GetMarketMaxCapacity();
            return Math.Min(raw, maxCap > 0 ? maxCap : double.MaxValue);
        }
    }

    /// <summary> 농장 창고 현재 누적량 (1분 단위 Floor × 분당 생산, MaxCap 한도) </summary>
    public double CurrentFarmAccumulated
    {
        get
        {
            var gm = GameManager.InstanceOrNull;
            if (gm?.currentUser == null) return 0;
            double ratePerSec = GetFarmValuePerSec();
            if (ratePerSec <= 0) return 0;
            gm.EnsureWarehouseBaselines();
            long now = NowUnixSeconds();
            long last = gm.currentUser.lastFarmCollectTime;
            if (last <= 0) return 0;
            long elapsedSec = Math.Max(0, now - last);
            long wholeMinutes = elapsedSec / 60;
            double perMinute = ratePerSec * 60.0;
            double raw = wholeMinutes * perMinute;
            double maxCap = GetFarmMaxCapacity();
            return Math.Min(raw, maxCap > 0 ? maxCap : double.MaxValue);
        }
    }

    public bool IsMarketProducing() => GetMarketValuePerSec() > 0;

    public bool IsFarmProducing() => GetFarmValuePerSec() > 0;

    /// <summary>시장 창고 기준 마지막 수거 이후 경과 초 (주머니 단계용).</summary>
    public long GetMarketElapsedSeconds()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null || !IsMarketProducing()) return 0;
        gm.EnsureWarehouseBaselines();
        long now = NowUnixSeconds();
        long last = gm.currentUser.lastMarketCollectTime;
        if (last <= 0) return 0;
        return Math.Max(0, now - last);
    }

    /// <summary>농장 창고 기준 마지막 수거 이후 경과 초 (주머니 단계용).</summary>
    public long GetFarmElapsedSeconds()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null || !IsFarmProducing()) return 0;
        gm.EnsureWarehouseBaselines();
        long now = NowUnixSeconds();
        long last = gm.currentUser.lastFarmCollectTime;
        if (last <= 0) return 0;
        return Math.Max(0, now - last);
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
        if (gm?.currentUser == null) return;
        int oldLevel = gm.autoIncomeLevel;
        double cost = UpgradeCost(MarketBaseCost, oldLevel);
        if (!gm.UseGold((long)cost)) return;

        gm.autoIncomeLevel++;
        // 생산 없음(0) → 첫 가동(1): 로드 시각이 아니라 '업그레이드한 지금'부터 누적
        if (oldLevel <= 0)
        {
            gm.currentUser.lastMarketCollectTime = NowUnixSeconds();
            gm.SaveUserData();
        }
    }

    public void UpgradeFarm()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null) return;
        int oldLevel = gm.currentUser.farmLevel;
        double cost = UpgradeCost(FarmBaseCost, oldLevel);
        if (!gm.UseGold((long)cost)) return;

        gm.currentUser.farmLevel++;
        if (oldLevel <= 0)
        {
            gm.currentUser.lastFarmCollectTime = NowUnixSeconds();
            gm.SaveUserData();
        }
    }

    /// <summary>병사 모집 (금화 차감 후 병사 수 증가).</summary>
    public void HireSoldiers(int count) => HireFarmWorkers(count);

    public void HireFarmWorkers(int count)
    {
        var gm = GameManager.InstanceOrNull;
        if (gm == null || count <= 0 || gm.currentUser == null) return;
        int maxAfford = (int)(gm.currentGold / FarmWorkerCost);
        int actual = Mathf.Min(count, maxAfford);
        if (actual <= 0) return;
        if (!gm.UseGold(actual * FarmWorkerCost)) return;
        gm.currentUser.soldierCount += actual;
        gm.SaveUserData();
    }

    public void BuyGrain(int count)
    {
        var gm = GameManager.InstanceOrNull;
        if (gm == null || count <= 0 || gm.currentUser == null) return;
        int maxAfford = (int)(gm.currentGold / GrainCost);
        int actual = Mathf.Min(count, maxAfford);
        if (actual <= 0) return;
        if (!gm.UseGold(actual * GrainCost)) return;
        gm.AddGrain(actual);
        gm.SaveUserData();
    }

    public int GetMaxAffordableSoldiers() => GetMaxAffordableFarmWorkers();

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
        gm.currentUser.lastMarketCollectTime = NowUnixSeconds();
    }

    public void CollectFarmGrain()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null) return;
        double acc = CurrentFarmAccumulated;
        if (acc <= 0) return;
        gm.AddGrain((long)acc);
        gm.currentUser.lastFarmCollectTime = NowUnixSeconds();
    }

    /// <summary>
    /// 창고(시장 금화 + 농장 식량) 수거를 비행 연출 후 입금으로 처리.
    /// requireActivePiles: true면 CollectionManager에 켜진 더미가 1개 이상 있어야 함 (대문).
    /// lastCollectTime은 모든 비행 OnComplete 후 갱신됩니다.
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

        int goldPiles = cm.CountActiveGoldPiles();
        int grainPiles = cm.CountActiveGrainPiles();
        if (totalGold > 0 && goldPiles <= 0) return false;
        if (totalGrain > 0 && grainPiles <= 0) return false;

        cm.PlayFlyEffect(totalGold, totalGrain, () =>
        {
            long now = NowUnixSeconds();
            if (totalGold > 0) gm.currentUser.lastMarketCollectTime = now;
            if (totalGrain > 0) gm.currentUser.lastFarmCollectTime = now;
            gm.SaveUserData();
        });
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
        // 에디터 전용 만보기 테스트 (Play 모드 + Game 뷰 포커스 권장)
        // F9: +500보, F10: +2000보, F11: 걸음·보상 상태 콘솔 로그
        // F12: 시장+농장 창고 +1시간(빠른 시간여행)
        var gm = GameManager.InstanceOrNull;

        if (Input.GetKeyDown(KeyCode.F9) && gm?.currentUser != null)
        {
            gm.currentUser.stepsToday += 500;
            gm.currentUser.dailyStepCount = gm.currentUser.stepsToday;
            gm.OnStepsChanged?.Invoke(gm.currentUser.stepsToday);
            Debug.Log($"[Editor 만보기] stepsToday = {gm.currentUser.stepsToday} (F9 +500)");
        }
        if (Input.GetKeyDown(KeyCode.F10) && gm?.currentUser != null)
        {
            gm.currentUser.stepsToday += 2000;
            gm.currentUser.dailyStepCount = gm.currentUser.stepsToday;
            gm.OnStepsChanged?.Invoke(gm.currentUser.stepsToday);
            Debug.Log($"[Editor 만보기] stepsToday = {gm.currentUser.stepsToday} (F10 +2000)");
        }
        if (Input.GetKeyDown(KeyCode.F11) && gm?.currentUser != null)
        {
            var u = gm.currentUser;
            var c = u.stepRewardsClaimed;
            string r = c != null && c.Length >= 4
                ? $"{c[0]},{c[1]},{c[2]},{c[3]}"
                : "(배열 없음)";
            Debug.Log($"[Editor 만보기] stepsToday={u.stepsToday}, 보상수령=[{r}]");
        }

        // --- 창고 누적 더미 아이콘 테스트용(빠른 시간여행) ---
        // 누적량 계산은 now - lastCollectTime 기준이라,
        // lastCollectTime을 3600초만 과거로 보내면 "1시간만큼" 누적/아이콘이 즉시 증가합니다.
        if (Input.GetKeyDown(KeyCode.F12) && gm?.currentUser != null)
        {
            var u = gm.currentUser;
            if (u.marketLevel <= 0 || u.farmLevel <= 0)
                Debug.Log("[Editor 창고] 시장/농장 레벨이 0이면 해당 자원은 누적이 0으로 보일 수 있습니다. 레벨업 후 테스트하세요.");

            long now = NowUnixSeconds();
            if (u.lastFarmCollectTime <= 0) u.lastFarmCollectTime = now;
            if (u.lastMarketCollectTime <= 0) u.lastMarketCollectTime = now;

            u.lastMarketCollectTime -= 3600;
            u.lastFarmCollectTime -= 3600;
            gm.SaveUserData();
            Debug.Log("[Editor 창고] 시장+농장 +1시간(=lastCollectTime - 3600s)");
        }
    }
#endif
}
