using System;
using UnityEngine;

/// <summary>
/// 매일 현지 시각 자정(00:00) 병사 유지비 일일 정산, 미납 시 탈영, 오프라인 소급 적용.
/// <see cref="GameManager"/>와 동일한 오브젝트에 붙습니다.
/// </summary>
[DisallowMultipleComponent]
public class EconomyManager : MonoBehaviour
{
    const string PrefsLastSettlementTicks = "Economy.LastDailyMaintenanceSettlementTicks";
    const string PrefsLegacyNoonTicks = "Economy.LastSettledMaintenanceNoonTicks";

    /// <summary>유저 보유 병사 1명당 일일 유지비 (금화). 최종 = 병사×단가×(1−병참할인).</summary>
    [SerializeField]
    double maintenanceGoldPerSoldierPerDay = 1d;

    static EconomyManager _instanceOrNull;

    float _nextMaintenancePollUnscaled;

    public static EconomyManager InstanceOrNull => _instanceOrNull;

    public double MaintenanceGoldPerSoldierPerDay => Math.Max(0d, maintenanceGoldPerSoldierPerDay);

    /// <summary>일일 정산이 1회 이상 처리되었을 때 (팝업·연출용).</summary>
    public event Action<DailySettlementReport> DailySettlementCompleted;

    void Awake()
    {
        _instanceOrNull = this;
    }

    void OnDestroy()
    {
        if (_instanceOrNull == this)
            _instanceOrNull = null;
    }

    void Start()
    {
        ProcessMaintenanceCatchUp();
        GlobalUIManager.InstanceOrNull?.RefreshTopBarFromGameManager();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus)
        {
            ProcessMaintenanceCatchUp();
            GlobalUIManager.InstanceOrNull?.RefreshTopBarFromGameManager();
        }
    }

    void Update()
    {
        if (Time.unscaledTime < _nextMaintenancePollUnscaled)
            return;
        _nextMaintenancePollUnscaled = Time.unscaledTime + 1f;

        ProcessMaintenanceCatchUp();
        GlobalUIManager.InstanceOrNull?.RefreshMaintenanceHudFromEconomy();
    }

    /// <summary>징집 미리보기: 추가 병사 delta명에 대한 일일 유지비 증가액.</summary>
    public static double ComputeDailyUpkeepGoldForAdditionalSoldiers(int deltaSoldiers)
    {
        if (deltaSoldiers <= 0) return 0d;
        var inst = InstanceOrNull;
        double rate = inst != null ? inst.MaintenanceGoldPerSoldierPerDay : 1d;
        return deltaSoldiers * rate * ResolveLogisticsMaintenanceMultiplier();
    }

    /// <summary>기본 유지비에 곱할 계수. <see cref="LevelRuleData.logisticsDiscountRate"/>% 만큼 감면.</summary>
    public static double ResolveLogisticsMaintenanceMultiplier()
    {
        var gm = GameManager.InstanceOrNull;
        int logisticsLv = gm?.currentUser?.farmLevel ?? 0;
        var dm = DataManager.InstanceOrNull;
        if (dm == null || !dm.IsReady) return 1d;
        var d = dm.GetLevelData(logisticsLv);
        if (d == null) return 1d;
        double pct = d.logisticsDiscountRate;
        if (double.IsNaN(pct) || double.IsInfinity(pct)) return 1d;
        pct = Math.Max(0d, Math.Min(50d, pct));
        return Math.Max(0d, 1d - pct / 100d);
    }

    /// <summary>다음 정산 시점에 차감될 총 유지비(현재 병력·병참 기준).</summary>
    public double ComputeNextSettlementGold()
    {
        long soldiers = ResolveSoldierHeadcountForMaintenance();
        var inst = InstanceOrNull;
        double rate = inst != null ? inst.MaintenanceGoldPerSoldierPerDay : 1d;
        return soldiers * rate * ResolveLogisticsMaintenanceMultiplier();
    }

    /// <summary>병사 1명당 일일 유지비(할인 적용 후).</summary>
    public static double ComputePerSoldierDailyUpkeepGold()
    {
        var inst = InstanceOrNull;
        double rate = inst != null ? inst.MaintenanceGoldPerSoldierPerDay : 1d;
        return rate * ResolveLogisticsMaintenanceMultiplier();
    }

    /// <summary>다음 현지 자정까지 남은 시간.</summary>
    public static TimeSpan TimeUntilNextLocalMidnight()
    {
        var next = NextLocalMidnightAfter(DateTime.Now);
        var span = next - DateTime.Now;
        return span.TotalSeconds < 0 ? TimeSpan.Zero : span;
    }

    /// <summary>다음 현지 자정까지 HH:MM:SS.</summary>
    public static string FormatCountdownUntilNextDailySettlementHms()
    {
        var span = TimeUntilNextLocalMidnight();
        int total = (int)Math.Floor(Math.Max(0d, span.TotalSeconds));
        int h = total / 3600;
        int m = (total % 3600) / 60;
        int s2 = total % 60;
        return $"{h:00}:{m:00}:{s2:00}";
    }

    static DateTime NextLocalMidnightAfter(DateTime instant)
    {
        return instant.Date.AddDays(1);
    }

    static DateTime LoadOrCreateLastSettlementInstant()
    {
        if (TryReadSettlementTicks(out DateTime dt))
            return dt;

        MigrateLegacyNoonPrefsIfNeeded();

        if (TryReadSettlementTicks(out dt))
            return dt;

        var yesterday = DateTime.Today.AddDays(-1);
        return new DateTime(yesterday.Year, yesterday.Month, yesterday.Day, 0, 0, 0, DateTimeKind.Local);
    }

    static bool TryReadSettlementTicks(out DateTime instant)
    {
        instant = default;
        if (!PlayerPrefs.HasKey(PrefsLastSettlementTicks))
            return false;
        if (!long.TryParse(PlayerPrefs.GetString(PrefsLastSettlementTicks), out long ticks))
            return false;
        try
        {
            instant = DateTime.SpecifyKind(new DateTime(ticks), DateTimeKind.Local);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    /// <summary>레거시 정오 정산 키를 자정 기준으로 1회 이관합니다.</summary>
    static void MigrateLegacyNoonPrefsIfNeeded()
    {
        if (!PlayerPrefs.HasKey(PrefsLegacyNoonTicks))
            return;
        if (PlayerPrefs.HasKey(PrefsLastSettlementTicks))
            return;

        if (!long.TryParse(PlayerPrefs.GetString(PrefsLegacyNoonTicks), out long ticks))
            return;
        try
        {
            var noon = DateTime.SpecifyKind(new DateTime(ticks), DateTimeKind.Local);
            var migrated = new DateTime(noon.Year, noon.Month, noon.Day, 0, 0, 0, DateTimeKind.Local);
            PlayerPrefs.SetString(PrefsLastSettlementTicks, migrated.Ticks.ToString());
            PlayerPrefs.Save();
        }
        catch (ArgumentOutOfRangeException)
        {
            /* ignore */
        }
    }

    static void SaveLastSettlementInstant(DateTime instant)
    {
        var normalized = new DateTime(instant.Year, instant.Month, instant.Day, 0, 0, 0, DateTimeKind.Local);
        PlayerPrefs.SetString(PrefsLastSettlementTicks, normalized.Ticks.ToString());
        PlayerPrefs.Save();
    }

    /// <summary>마지막 정산 이후 지난 모든 자정마다 유지비를 차감합니다. 미납분은 병사 탈영으로 처리합니다.</summary>
    public void ProcessMaintenanceCatchUp()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null) return;

        double rate = MaintenanceGoldPerSoldierPerDay;
        if (double.IsNaN(rate) || double.IsInfinity(rate))
            return;

        double perSoldier = rate * ResolveLogisticsMaintenanceMultiplier();
        if (double.IsNaN(perSoldier) || double.IsInfinity(perSoldier))
            return;

        DateTime lastSettled = LoadOrCreateLastSettlementInstant();
        DateTime due = lastSettled.Date.AddDays(1);
        var now = DateTime.Now;

        int totalDays = 0;
        double totalGoldDeducted = 0d;
        int totalDeserted = 0;

        while (due <= now)
        {
            int desertedThisDay;
            double deductedThisDay;
            RunSingleDailySettlement(perSoldier, out deductedThisDay, out desertedThisDay);

            totalDays++;
            totalGoldDeducted += deductedThisDay;
            totalDeserted += desertedThisDay;

            lastSettled = new DateTime(due.Year, due.Month, due.Day, 0, 0, 0, DateTimeKind.Local);
            SaveLastSettlementInstant(lastSettled);
            due = lastSettled.Date.AddDays(1);
        }

        if (totalDays > 0)
        {
            gm.SaveUserData();

            var report = new DailySettlementReport
            {
                DaysSettled = totalDays,
                TotalGoldDeducted = totalGoldDeducted,
                TotalTroopsDeserted = totalDeserted,
                ResultingGold = gm.currentGold,
                ResultingSoldiers = ResolveSoldierHeadcountForMaintenance()
            };
            DailySettlementCompleted?.Invoke(report);
        }
    }

    void RunSingleDailySettlement(double perSoldierCost, out double goldDeducted, out int troopsDeserted)
    {
        goldDeducted = 0d;
        troopsDeserted = 0;

        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null) return;

        if (perSoldierCost <= 0d)
            return;

        long soldiers = ResolveSoldierHeadcountForMaintenance();
        double cost = soldiers * perSoldierCost;
        if (cost <= 0d || double.IsNaN(cost) || double.IsInfinity(cost))
            return;

        double gold = gm.currentUser.gold;
        if (gold >= cost)
        {
            gm.AddGold(-cost);
            goldDeducted = cost;
            return;
        }

        long maxAffordable = (long)Math.Floor((gold + 1e-9) / perSoldierCost);
        if (maxAffordable < 0) maxAffordable = 0;
        long toRemove = soldiers - maxAffordable;
        if (toRemove > 0)
        {
            int remove = toRemove > int.MaxValue ? int.MaxValue : (int)toRemove;
            var dm = DataManager.InstanceOrNull;
            int removed = dm != null ? dm.RemoveUserTroopsForUpkeepDesertion(remove) : 0;
            troopsDeserted = removed;

            soldiers = ResolveSoldierHeadcountForMaintenance();
            cost = soldiers * perSoldierCost;
        }

        gold = gm.currentUser.gold;
        double pay = Math.Min(cost, gold);
        if (pay > 0d)
            gm.AddGold(-pay);
        goldDeducted = pay;
    }

    static long ResolveSoldierHeadcountForMaintenance()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null) return 0L;

        var dm = DataManager.InstanceOrNull;
        if (dm != null && dm.IsStateReady)
            return UserPortfolioManager.GetTotalOwnedSoldiers(dm);

        return Math.Max(0L, gm.currentUser.soldierCount);
    }

    /// <summary>성별 AI 징집·입성 의무율(0~20%). 상태 틱에서 갱신된 캐시를 우선합니다.</summary>
    public static float CalculateRecruitmentFee(string castleId)
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null || !dm.IsStateReady || string.IsNullOrWhiteSpace(castleId))
            return 0f;
        castleId = castleId.Trim();
        if (dm.castleStateDataMap.TryGetValue(castleId, out var s) && s != null)
            return Mathf.Clamp(s.recruitmentFee, 0f, 20f);
        return RecruitmentDutyCalculator.CalculateRecruitmentFee(dm, castleId, out _);
    }

    void OnValidate()
    {
        maintenanceGoldPerSoldierPerDay = Math.Max(0d, maintenanceGoldPerSoldierPerDay);
    }
}

/// <summary>연속 정산(오프라인)을 묶어 보고할 때 사용합니다.</summary>
public struct DailySettlementReport
{
    public int DaysSettled;
    public double TotalGoldDeducted;
    public int TotalTroopsDeserted;
    public double ResultingGold;
    public long ResultingSoldiers;
}
