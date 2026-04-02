using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 매주 로컬 월요일 06:00 이후 첫 체크 시 주간 배당. 세이브의 <see cref="CastleStateSavePayload.lastWeeklyDividendPaidAnchorUnix"/>로 중복 방지.
/// </summary>
public static class DividendManager
{
    public const string PlayerPrefsLastAnchorUnix = "stk3k_last_div_anchor_unix";
    public const string PlayerPrefsMaxLocalTicks = "stk3k_div_max_local_ticks";

    public static event Action<long, IReadOnlyList<DividendPayoutLine>> OnWeeklyDividendPaid;

    static float _nextCheckUnscaled = -1f;
    const float CheckIntervalSeconds = 45f;

    /// <summary>게임 루프에서 주기 호출(내부 쿨다운).</summary>
    public static void Tick(DataManager dm, float unscaledTime)
    {
        if (dm == null || !dm.IsStateReady) return;
        if (_nextCheckUnscaled < 0f)
            _nextCheckUnscaled = unscaledTime + 2f;
        if (unscaledTime < _nextCheckUnscaled) return;
        _nextCheckUnscaled = unscaledTime + CheckIntervalSeconds;
        TryProcessWeeklyDividend(dm);
    }

    /// <summary>상태 준비 직후 한 번 호출해도 됩니다.</summary>
    public static void TryProcessWeeklyDividend(DataManager dm)
    {
        if (dm == null || !dm.IsStateReady) return;
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null) return;

        DateTime localNow = DateTime.Now;
        if (!TryAdvancePlausibleLocalClock(localNow))
        {
            Debug.LogWarning("[DividendManager] 로컬 시각 역행이 감지되어 이번 배당 검사를 건너뜁니다.");
            return;
        }

        long dueAnchorUnix = GetLastPassedMondaySixAmLocalUnix(localNow);
        long lastPaid = dm.LastWeeklyDividendPaidAnchorUnix;
        if (PlayerPrefs.HasKey(PlayerPrefsLastAnchorUnix) &&
            long.TryParse(PlayerPrefs.GetString(PlayerPrefsLastAnchorUnix), out var prefsAnchor))
            lastPaid = Math.Max(lastPaid, prefsAnchor);

        if (dueAnchorUnix <= lastPaid)
            return;

        var lines = new List<DividendPayoutLine>(8);
        long total = 0L;

        foreach (var kv in dm.castleStateDataMap)
        {
            var s = kv.Value;
            if (s == null || string.IsNullOrWhiteSpace(s.id)) continue;

            long beforePool = s.accumulatedDividendPool;
            long payout = s.DistributeUserDividendShare();
            if (payout > 0L)
            {
                total += payout;
                string name = dm.GetCastleDisplayName(s.id);
                if (string.IsNullOrWhiteSpace(name)) name = s.id.Trim();
                lines.Add(new DividendPayoutLine(s.id.Trim(), name, payout, beforePool));
            }
        }

        if (total > 0L)
            gm.AddGold(total);

        dm.SetLastWeeklyDividendPaidAnchorUnix(dueAnchorUnix);
        dm.MarkCastleStateDirty();
        PlayerPrefs.SetString(PlayerPrefsLastAnchorUnix, dueAnchorUnix.ToString());
        PlayerPrefs.Save();

        dm.FlushLiveScriptableObjects();
        dm.RequestWorldUiRefresh();

        string reason =
            $"anchor_unix={dueAnchorUnix} (로컬 월요일 06:00), 성 {lines.Count}건, 합계 {total:N0} 금";
        Debug.Log($"[DividendManager] 주간 배당 처리: {reason}");

        OnWeeklyDividendPaid?.Invoke(total, lines);

        if (UI_DividendReport.InstanceOrNull != null)
            UI_DividendReport.InstanceOrNull.ShowReport(total, lines);
        else if (total > 0L)
            Debug.Log(
                "[DividendManager] UI_DividendReport 없음 — 씬에 패널을 추가하면 성별 배당 요약을 볼 수 있습니다.");
    }

    /// <summary>로컬 기준, 현재 시각이 포함된 주에서 "이미 지난" 가장 최근 월요일 06:00의 Unix 초.</summary>
    public static long GetLastPassedMondaySixAmLocalUnix(DateTime localNow)
    {
        DateTime date = localNow.Date;
        int daysFromMonday = ((int)localNow.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        DateTime weekMonday = date.AddDays(-daysFromMonday);
        DateTime monSix = weekMonday.AddHours(6d);
        if (localNow < monSix)
            monSix = monSix.AddDays(-7);

        TimeSpan offset = TimeZoneInfo.Local.GetUtcOffset(monSix);
        var dto = new DateTimeOffset(monSix, offset);
        return dto.ToUnixTimeSeconds();
    }

    /// <summary>단조 증가하는 로컬 Ticks를 유지. 1시간 이상 되돌아가면 false.</summary>
    static bool TryAdvancePlausibleLocalClock(DateTime localNow)
    {
        long ticksNow = localNow.Ticks;
        long maxTicks = 0L;
        if (long.TryParse(PlayerPrefs.GetString(PlayerPrefsMaxLocalTicks, "0"), out var stored))
            maxTicks = stored;

        if (ticksNow < maxTicks - TimeSpan.TicksPerHour)
            return false;

        if (ticksNow > maxTicks)
        {
            PlayerPrefs.SetString(PlayerPrefsMaxLocalTicks, ticksNow.ToString());
            PlayerPrefs.Save();
        }

        return true;
    }
}

/// <summary>배당 UI·로그용 한 줄.</summary>
[Serializable]
public readonly struct DividendPayoutLine
{
    public readonly string castleId;
    public readonly string castleDisplayName;
    public readonly long gold;
    public readonly long poolBefore;

    public DividendPayoutLine(string castleId, string castleDisplayName, long gold, long poolBefore)
    {
        this.castleId = castleId;
        this.castleDisplayName = castleDisplayName;
        this.gold = gold;
        this.poolBefore = poolBefore;
    }
}
