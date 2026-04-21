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
    public const double LaborBaseCost = 50;
    public const double MarketBaseCost = 100;
    /// <summary>병참(구 농장 레벨, <see cref="UserData.farmLevel"/>) 업그레이드 비용 기준.</summary>
    public const double LogisticsBaseCost = 80;

    /// <summary>만보기 목표 걸음 수 (2k, 5k, 7k, 10k)</summary>
    public static readonly int[] StepMilestones = { 2000, 5000, 7000, 10000 };

    /// <summary>목표별 행군 포인트(MP) 보상</summary>
    public static readonly int[] StepRewardMarchPoints = { 200, 500, 700, 1000 };

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

    /// <summary> 시장 창고 현재 누적량 (수거 시각 이후 경과 초 × 초당 생산, MaxCap 한도) </summary>
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
            double raw = elapsedSec * ratePerSec;
            double maxCap = GetMarketMaxCapacity();
            return Math.Min(raw, maxCap > 0 ? maxCap : double.MaxValue);
        }
    }

    public bool IsMarketProducing() => GetMarketValuePerSec() > 0;

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

    /// <summary> 대문 터치 (탭 1회) </summary>
    public void OnGateClick()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm == null) return;
        gm.AddGold(GoldPerClick);
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
        if (oldLevel <= 0)
        {
            gm.currentUser.lastMarketCollectTime = NowUnixSeconds();
            gm.SaveUserData();
        }
    }

    /// <summary>병참 업그레이드 — <see cref="UserData.farmLevel"/> 증가, 일일 병사 유지비 비율 감소.</summary>
    public void UpgradeLogistics()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null) return;
        int lv = gm.currentUser.farmLevel;
        double cost = UpgradeCost(LogisticsBaseCost, lv);
        if (!gm.UseGold((long)cost)) return;

        gm.currentUser.farmLevel++;
        gm.SaveUserData();
        DataManager.InstanceOrNull?.RefreshHomeCastleMaxGarrisonFromUserBuildings();
        GlobalUIManager.InstanceOrNull?.RefreshMaintenanceHudFromEconomy();
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

    /// <summary>
    /// 시장 창고 수거를 비행 연출 후 입금으로 처리.
    /// </summary>
    public bool TryFlyCollectFromWarehouse(CollectionManager cm, bool requireActivePiles)
    {
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null || cm == null) return false;
        if (cm.IsFlyBusy) return false;

        if (requireActivePiles && !cm.HasActivePileVisual()) return false;

        long totalMarket = (long)CurrentMarketAccumulated;
        if (totalMarket <= 0) return false;

        if (!cm.HasActivePileVisual()) return false;

        cm.PlayFlyEffect(totalMarket, 0, () =>
        {
            long now = NowUnixSeconds();
            if (totalMarket > 0) gm.currentUser.lastMarketCollectTime = now;
            gm.SaveUserData();
        });
        return true;
    }

    /// <summary>
    /// 만보기 분기 보상. 해당 목표 걸음을 넘었고 미수령이면 MP 지급.
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

        int reward = milestoneIndex < StepRewardMarchPoints.Length ? StepRewardMarchPoints[milestoneIndex] : 0;
        gm.AddMarchPoints(reward);
        u.stepRewardsClaimed[milestoneIndex] = true;
        gm.SaveUserData();
        return true;
    }

#if UNITY_EDITOR
    void Update()
    {
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

        if (Input.GetKeyDown(KeyCode.F12) && gm?.currentUser != null)
        {
            var u = gm.currentUser;
            if (u.marketLevel <= 0)
                Debug.Log("[Editor 창고] 시장 레벨이 0이면 누적이 0으로 보일 수 있습니다.");

            long now = NowUnixSeconds();
            if (u.lastMarketCollectTime <= 0) u.lastMarketCollectTime = now;

            u.lastMarketCollectTime -= 3600;
            gm.SaveUserData();
            Debug.Log("[Editor 창고] 시장 창고 +1시간(=lastMarketCollectTime - 3600s)");
        }
    }
#endif
}
