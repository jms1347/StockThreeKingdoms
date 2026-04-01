using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 성 스탯 옵저버 허브 + 일일 뉴스 K-샘플링(요구사항 파일명 EventManager.cs, Unity 빌트인과 구분).
/// </summary>
public static class CastleWorldEventManager
{
    /// <summary>민심이 이 값 미만으로 <b>처음</b> 내려가면 <see cref="CriticalWorldEventKind.PopularRiot"/>.</summary>
    public static float SentimentRiotThreshold { get; set; } = 20f;

    /// <summary>안정도가 이 값 미만으로 <b>처음</b> 내려가면 <see cref="CriticalWorldEventKind.StabilityCollapse"/>.</summary>
    public static float StabilityCollapseThreshold { get; set; } = 20f;

    public static event Action<CastleStateData, CastleStatChangedEventArgs> GlobalOnStatChanged;
    public static event Action<CastleStateData, CriticalWorldEventArgs> GlobalOnCriticalBreach;

    internal static void RaiseGlobalStatChanged(CastleStateData castle, CastleStatChangedEventArgs args) =>
        GlobalOnStatChanged?.Invoke(castle, args);

    internal static void RaiseGlobalCritical(CastleStateData castle, CriticalWorldEventArgs args) =>
        GlobalOnCriticalBreach?.Invoke(castle, args);
}

/// <summary>
/// 매 게임 일 전 성 전수 순회 대신, 우선순위(전쟁·재해·호재) 후 K개만 일일 이벤트 롤에 사용.
/// </summary>
public static class CastleDailyEventSampler
{
    public const int MaxDailyNewsCastleSample = 3;

    const int PriorityWar = 1_000_000;
    const int PriorityDisaster = 500_000;
    const int PriorityFavorable = 100_000;
    const int TieNoise = 999;

    /// <summary>높은 우선순위 → 낮은 우선순위 순으로 정렬 후 상위 <paramref name="maxCount"/> 성.</summary>
    public static List<CastleStateData> SelectCastlesForDailyEventRoll(DataManager dm, int maxCount = MaxDailyNewsCastleSample)
    {
        var result = new List<CastleStateData>();
        if (dm == null || !dm.IsStateReady || dm.castleStateDataMap == null || maxCount <= 0)
            return result;

        var scored = new List<(CastleStateData s, int score)>();
        foreach (var kv in dm.castleStateDataMap)
        {
            var st = kv.Value;
            if (st == null || string.IsNullOrWhiteSpace(st.id))
                continue;

            int p = UnityEngine.Random.Range(0, TieNoise);
            if (st.isWar) p += PriorityWar;
            if (st.isDisaster) p += PriorityDisaster;
            if (st.isFavorableEvent) p += PriorityFavorable;
            scored.Add((st, p));
        }

        scored.Sort((a, b) => b.score.CompareTo(a.score));

        int take = Mathf.Min(maxCount, scored.Count);
        for (int i = 0; i < take; i++)
            result.Add(scored[i].s);

        return result;
    }
}
