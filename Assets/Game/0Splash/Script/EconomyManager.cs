using System;
using UnityEngine;

/// <summary>
/// 매일 현지 시각 낮 12시 병사 유지비 정산 및 오프라인 소급 적용.
/// <see cref="GameManager"/>와 동일한 오브젝트에 붙습니다.
/// </summary>
[DisallowMultipleComponent]
public class EconomyManager : MonoBehaviour
{
    const string PrefsLastSettledNoonTicks = "Economy.LastSettledMaintenanceNoonTicks";

    /// <summary>유저 보유 병사 1명당 일일 유지비 (금화).</summary>
    [SerializeField]
    double maintenanceGoldPerSoldierPerDay = 1d;

    [Header("본영 개편: 실시간 유지비")]
    [Tooltip("끄면(기본): 병사 유지비가 초당 HUD 금화에서 차감됩니다. 켜면 예전처럼 낮 12시 정산만 사용합니다.")]
    [SerializeField]
    bool useLegacyNoonMaintenance;

    static EconomyManager _instanceOrNull;

    float _nextMaintenancePollUnscaled;

    public static EconomyManager InstanceOrNull => _instanceOrNull;

    public double MaintenanceGoldPerSoldierPerDay => Math.Max(0d, maintenanceGoldPerSoldierPerDay);

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
        if (useLegacyNoonMaintenance)
            ProcessMaintenanceCatchUp(triggerSave: true);
        GlobalUIManager.InstanceOrNull?.RefreshTopBarFromGameManager();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus)
        {
            if (useLegacyNoonMaintenance)
                ProcessMaintenanceCatchUp(triggerSave: true);
            GlobalUIManager.InstanceOrNull?.RefreshTopBarFromGameManager();
        }
    }

    void Update()
    {
        if (!useLegacyNoonMaintenance)
            ApplyRealtimeSoldierUpkeep(Time.unscaledDeltaTime);

        if (Time.unscaledTime < _nextMaintenancePollUnscaled)
            return;
        _nextMaintenancePollUnscaled = Time.unscaledTime + 1f;

        if (useLegacyNoonMaintenance)
            ProcessMaintenanceCatchUp(triggerSave: false);
        GlobalUIManager.InstanceOrNull?.RefreshMaintenanceHudFromEconomy();
    }

    /// <summary>기획식: upkeep = (troops × 0.1) × (1 − min(farmLevel×0.0025, 0.5)) × dt 초당 금화 차감(0 미만 금지).</summary>
    void ApplyRealtimeSoldierUpkeep(float dt)
    {
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null || dt <= 0f)
            return;

        int logisticsLevel = Mathf.Max(0, gm.currentUser.farmLevel);
        float discount = Mathf.Min(logisticsLevel * 0.0025f, 0.5f);
        long troops = ResolveSoldierHeadcountForMaintenance();
        double upkeep = troops * 0.1 * (1.0 - discount) * dt;
        if (upkeep <= 0d || double.IsNaN(upkeep) || double.IsInfinity(upkeep))
            return;

        double g = gm.currentUser.gold;
        double next = g - upkeep;
        if (next < 0d)
            next = 0d;
        if (next >= g)
            return;
        gm.currentGold = next;
    }

    /// <summary>상단 유지비 미리보기(HUD)용 초당 차감 예상치.</summary>
    public static double ComputeRealtimeUpkeepGoldPerSecond()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null)
            return 0d;
        int logisticsLevel = Mathf.Max(0, gm.currentUser.farmLevel);
        float discount = Mathf.Min(logisticsLevel * 0.0025f, 0.5f);
        long troops = ResolveSoldierHeadcountForMaintenance();
        return troops * 0.1 * (1.0 - discount);
    }

    /// <summary>징집 미리보기: 추가 병사 delta명에 대한 초당 유지비 증가(주가 단순 모집 UI).</summary>
    public static double ComputeRealtimeUpkeepDeltaPerSecondForSoldiers(int deltaSoldiers)
    {
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null || deltaSoldiers <= 0)
            return 0d;
        int logisticsLevel = Mathf.Max(0, gm.currentUser.farmLevel);
        float discount = Mathf.Min(logisticsLevel * 0.0025f, 0.5f);
        return deltaSoldiers * 0.1 * (1.0 - discount);
    }

    public bool UsesLegacyNoonMaintenance => useLegacyNoonMaintenance;

    /// <summary>다음 낮 12시까지 남은 시간 문자열.</summary>
    public static string FormatCountdownUntilNextLocalNoon()
    {
        var next = NextLocalNoonAfter(DateTime.Now);
        var span = next - DateTime.Now;
        if (span.TotalSeconds < 0)
            span = TimeSpan.Zero;
        if (span.TotalDays >= 1d)
            return $"{(int)span.TotalHours}시간 {span.Minutes}분";
        if (span.TotalHours >= 1d)
            return $"{span.Hours}시간 {span.Minutes}분";
        return $"{Mathf.Max(0, span.Minutes)}분 {Mathf.Max(0, span.Seconds)}초";
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
        // 기획: 병참 할인은 레벨 누적으로 최대 50%까지만 허용.
        pct = System.Math.Max(0d, System.Math.Min(50d, pct));
        return System.Math.Max(0d, 1d - pct / 100d);
    }

    /// <summary>현재 시점 다음 정산 예정 금화 (보유 병사 × 일일 단가 × 병참 유지비 할인).</summary>
    public double ComputeNextSettlementGold()
    {
        long soldiers = ResolveSoldierHeadcountForMaintenance();
        var inst = InstanceOrNull;
        double rate = inst != null ? inst.MaintenanceGoldPerSoldierPerDay : 1d;
        double raw = soldiers * rate;
        return raw * ResolveLogisticsMaintenanceMultiplier();
    }

    static DateTime NextLocalNoonAfter(DateTime instant)
    {
        var noonToday = new DateTime(instant.Year, instant.Month, instant.Day, 12, 0, 0, DateTimeKind.Local);
        if (instant < noonToday)
            return noonToday;
        return noonToday.AddDays(1);
    }

    /// <summary>첫 실행 시 PlayerPrefs 미설정이면 어제 정오를 마지막 정산 시점으로 간주합니다.</summary>
    static DateTime LoadOrCreateLastSettledNoon()
    {
        if (PlayerPrefs.HasKey(PrefsLastSettledNoonTicks) &&
            long.TryParse(PlayerPrefs.GetString(PrefsLastSettledNoonTicks), out long ticks))
        {
            try
            {
                var dt = DateTime.SpecifyKind(new DateTime(ticks), DateTimeKind.Local);
                return new DateTime(dt.Year, dt.Month, dt.Day, 12, 0, 0, DateTimeKind.Local);
            }
            catch (ArgumentOutOfRangeException)
            {
                /* fallthrough */
            }
        }

        var yesterday = DateTime.Today.AddDays(-1);
        return new DateTime(yesterday.Year, yesterday.Month, yesterday.Day, 12, 0, 0, DateTimeKind.Local);
    }

    static void SaveLastSettledNoon(DateTime noonInstant)
    {
        var normalized = new DateTime(noonInstant.Year, noonInstant.Month, noonInstant.Day, 12, 0, 0,
            DateTimeKind.Local);
        PlayerPrefs.SetString(PrefsLastSettledNoonTicks, normalized.Ticks.ToString());
        PlayerPrefs.Save();
    }

    /// <summary>마지막 정산 처리일 이후 경과한 모든 낮 12시 시각마다 유지비를 차감합니다.</summary>
    public void ProcessMaintenanceCatchUp(bool triggerSave)
    {
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null) return;

        double rate = MaintenanceGoldPerSoldierPerDay;
        if (double.IsNaN(rate) || double.IsInfinity(rate))
            return;

        DateTime lastSettled = LoadOrCreateLastSettledNoon();
        DateTime due = lastSettled.AddDays(1);
        var now = DateTime.Now;
        bool any = false;

        while (due <= now)
        {
            long soldiers = ResolveSoldierHeadcountForMaintenance();
            double cost = rate > 0d ? soldiers * rate : 0d;
            cost *= ResolveLogisticsMaintenanceMultiplier();
            if (cost != 0d && !double.IsNaN(cost) && !double.IsInfinity(cost))
            {
                gm.AddGold(-cost);
                any = true;
            }

            lastSettled = new DateTime(due.Year, due.Month, due.Day, 12, 0, 0, DateTimeKind.Local);
            SaveLastSettledNoon(lastSettled);
            due = lastSettled.AddDays(1);
        }

        if (any && triggerSave)
            gm.SaveUserData();
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

    void OnValidate()
    {
        maintenanceGoldPerSoldierPerDay = Math.Max(0d, maintenanceGoldPerSoldierPerDay);
    }
}
