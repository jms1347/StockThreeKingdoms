using System.Collections.Generic;
using UnityEngine;

/// <summary><see cref="EventMasterData.buffCodes"/>를 <see cref="CastleStateData"/>에 반영합니다.
/// <see cref="BuffMasterData.durationDays"/>가 1보다 크면 확정 시 1일차 분량만 즉시 적용하고, UTC 일이 바뀔 때마다 나머지 일차를 난수로 나눠 적용합니다.</summary>
public static class WorldEventBuffApplier
{
    /// <summary>버프 코드를 즉시 1일차까지 반영하고, 필요 시 <see cref="CastleStateData.activeBuffs"/>에 멀티데이 항목을 등록합니다.</summary>
    public static void ApplyBuffCodesToCastle(DataManager dm, CastleStateData s, IList<string> buffCodes,
        string sourceEventId, int appliedUtcDay)
    {
        RegisterActiveBuffCodes(dm, s, buffCodes, sourceEventId, appliedUtcDay);
    }

    /// <summary>레거시 호출: 이벤트 메타 없이 1일차만 적용·등록.</summary>
    public static void ApplyBuffCodesToCastle(DataManager dm, CastleStateData s, IList<string> buffCodes)
    {
        RegisterActiveBuffCodes(dm, s, buffCodes, "", 0);
    }

    /// <summary>UTC 일 버킷이 바뀐 뒤 호출: 진행 중인 버프의 다음 일차 분량을 적용합니다.</summary>
    public static void TickActiveBuffsForNewUtcDay(DataManager dm, int newUtcDay)
    {
        if (dm?.castleStateDataMap == null) return;

        foreach (var kv in dm.castleStateDataMap)
        {
            var s = kv.Value;
            if (s?.activeBuffs == null || s.activeBuffs.Count == 0) continue;

            for (int i = s.activeBuffs.Count - 1; i >= 0; i--)
            {
                var e = s.activeBuffs[i];
                if (e.totalDurationDays <= 0) continue;

                if (e.completedDayCount >= e.totalDurationDays)
                {
                    s.activeBuffs.RemoveAt(i);
                    continue;
                }

                var b = dm.GetBuffMasterData(e.buffCode);
                if (b == null)
                {
                    s.activeBuffs.RemoveAt(i);
                    continue;
                }

                int nextDay = e.completedDayCount + 1;
                ApplyBuffDailySample(s, b, nextDay, e.totalDurationDays);
                e.completedDayCount = nextDay;

                if (e.completedDayCount >= e.totalDurationDays)
                    s.activeBuffs.RemoveAt(i);
            }
        }
    }

    /// <summary>월드 이벤트 확정 시 버프 코드를 처리하고, 멀티데이인 경우 <see cref="CastleStateData.activeBuffs"/>에 등록합니다.</summary>
    public static void RegisterActiveBuffCodes(DataManager dm, CastleStateData s, IList<string> buffCodes,
        string sourceEventId, int appliedUtcDay)
    {
        if (dm == null || s == null || buffCodes == null) return;
        if (s.activeBuffs == null)
            s.activeBuffs = new List<ActiveBuffEntry>();

        string eid = sourceEventId ?? "";
        for (int i = 0; i < buffCodes.Count; i++)
        {
            string code = buffCodes[i];
            if (string.IsNullOrWhiteSpace(code)) continue;
            var b = dm.GetBuffMasterData(code.Trim());
            if (b == null) continue;

            int totalDays = b.durationDays < 1 ? 1 : b.durationDays;

            ApplyBuffDailySample(s, b, 1, totalDays);

            if (totalDays <= 1)
                continue;

            s.activeBuffs.Add(new ActiveBuffEntry
            {
                buffCode = code.Trim(),
                sourceEventId = eid,
                appliedUtcDay = appliedUtcDay,
                totalDurationDays = totalDays,
                completedDayCount = 1
            });
        }
    }

    /// <summary>일차 <paramref name="dayIndex"/> / <paramref name="totalDays"/>에 해당하는 난수 분량을 스탯에 반영합니다.</summary>
    public static void ApplyBuffDailySample(CastleStateData s, BuffMasterData b, int dayIndex, int totalDays)
    {
        if (s == null || b == null) return;
        if (b.statType == CastleStatType.None) return;
        if (Mathf.Abs(b.value) < 1e-8f) return;

        totalDays = Mathf.Max(1, totalDays);
        dayIndex = Mathf.Clamp(dayIndex, 1, totalDays);

        float w = GetNormalizedDayWeight(b.curveType, dayIndex, totalDays);
        float sign = b.value >= 0f ? 1f : -1f;
        float cap = 2f * Mathf.Abs(b.value) * w;
        float delta = Random.Range(0f, cap) * sign;

        switch (b.statType)
        {
            case CastleStatType.SentimentRecovery:
                s.currentSentiment = Mathf.Clamp(s.currentSentiment + delta, 0f, 200f);
                break;
            case CastleStatType.PriceValue:
                s.currentBuyPrice = Mathf.Max(0.01f, s.currentBuyPrice + delta);
                break;
            case CastleStatType.PopulationGrowth:
            {
                float slice = b.value * w;
                float mulDelta = Random.Range(0f, 2f * Mathf.Abs(slice)) * Mathf.Sign(slice);
                s.currentPopulation = Mathf.Max(1, Mathf.RoundToInt(s.currentPopulation * (1f + mulDelta)));
                break;
            }
            case CastleStatType.CastleValue:
            {
                float slice = b.value * w;
                float mulDelta = Random.Range(0f, 2f * Mathf.Abs(slice)) * Mathf.Sign(slice);
                s.currentBuyPrice = Mathf.Max(0.01f, s.currentBuyPrice * (1f + mulDelta));
                break;
            }
            default:
                break;
        }
    }

    static float RawDayWeight(CurveType curve, int dayIndex, int totalDays)
    {
        switch (curve)
        {
            case CurveType.Exponential:
                return dayIndex;
            case CurveType.Logarithmic:
                return totalDays - dayIndex + 1;
            case CurveType.Instant:
            case CurveType.Linear:
            case CurveType.None:
            default:
                return 1f;
        }
    }

    static float GetNormalizedDayWeight(CurveType curve, int dayIndex, int totalDays)
    {
        float sum = 0f;
        for (int d = 1; d <= totalDays; d++)
            sum += RawDayWeight(curve, d, totalDays);
        if (sum < 1e-8f) return 1f / totalDays;
        return RawDayWeight(curve, dayIndex, totalDays) / sum;
    }
}
