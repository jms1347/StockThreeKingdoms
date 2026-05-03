using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct CastleDividendPreview
{
    public readonly float InternalStat;
    public readonly float BaseProduction;
    public readonly float EfficiencyAfterTroopLoad;
    public readonly float OverloadRatio;
    public readonly float FinalDividend;
    public readonly int TotalTroops;
    public readonly int Population;
    public readonly int MaxCapacity;

    public bool IsOverloaded => TotalTroops > MaxCapacity;
    public float FinalEfficiencyPercent => BaseProduction <= 0.0001f ? 0f : Mathf.Clamp(FinalDividend / BaseProduction, 0f, 1f) * 100f;
    public float ExpectedYieldPercentPerTroopPrice => TotalTroops <= 0 ? 0f : FinalDividend / TotalTroops;

    public CastleDividendPreview(
        float internalStat,
        float baseProduction,
        float efficiencyAfterTroopLoad,
        float overloadRatio,
        float finalDividend,
        int totalTroops,
        int population,
        int maxCapacity)
    {
        InternalStat = internalStat;
        BaseProduction = baseProduction;
        EfficiencyAfterTroopLoad = efficiencyAfterTroopLoad;
        OverloadRatio = overloadRatio;
        FinalDividend = finalDividend;
        TotalTroops = totalTroops;
        Population = population;
        MaxCapacity = maxCapacity;
    }
}

public partial class DataManager
{
    /// <summary>장수 내정 기여치: 정치+통솔(현재 데이터셋: 지력+무력).</summary>
    public static float GetGeneralInternalStat(GeneralMasterData g)
    {
        if (g == null) return 0f;
        return Mathf.Max(0, g.intel) + Mathf.Max(0, g.power);
    }

    /// <summary>방랑 장수(<see cref="GeneralMasterData.initialCastleId"/> 비어 있음)는 주둔 10% 합산에서 제외합니다.</summary>
    static bool IsWanderingGeneralForActiveStats(GeneralMasterData g) =>
        g == null || string.IsNullOrWhiteSpace(g.initialCastleId);

    /// <summary>태수 100% + 주둔 장수 10% 합산(방랑 제외) — 무력·지력·매력·악명.</summary>
    public CastleActiveStats CalculateCastleActiveStats(CastleStateData s)
    {
        var stats = new CastleActiveStats();
        if (s == null || string.IsNullOrWhiteSpace(s.id))
            return stats;

        string govId = s.currentGovernorId?.Trim();
        if (!string.IsNullOrEmpty(govId) && generalMasterDataMap.TryGetValue(govId, out var gov) && gov != null)
        {
            stats.Power += Mathf.Max(0, gov.power);
            stats.Intel += Mathf.Max(0, gov.intel);
            stats.Charm += Mathf.Max(0, gov.charm);
            stats.Infamy += Mathf.Clamp(gov.infamy, 0, 100);
        }

        foreach (string gid in EnumerateResidentGeneralIds(s))
        {
            if (string.IsNullOrWhiteSpace(gid)) continue;
            string id = gid.Trim();
            if (string.Equals(id, govId, StringComparison.Ordinal)) continue;
            if (!generalMasterDataMap.TryGetValue(id, out var g) || g == null) continue;
            if (IsWanderingGeneralForActiveStats(g)) continue;

            stats.Power += Mathf.Max(0, g.power) * 0.1f;
            stats.Intel += Mathf.Max(0, g.intel) * 0.1f;
            stats.Charm += Mathf.Max(0, g.charm) * 0.1f;
            stats.Infamy += Mathf.Clamp(g.infamy, 0, 100) * 0.1f;
        }

        return stats;
    }

    /// <summary>태수 100% + 주둔 장수 10% 합산.</summary>
    public float CalculateFinalInternalStat(CastleStateData s)
    {
        if (s == null) return 0f;
        float total = 0f;
        string govId = s.currentGovernorId?.Trim();
        if (!string.IsNullOrEmpty(govId) && generalMasterDataMap.TryGetValue(govId, out var gov) && gov != null)
            total += GetGeneralInternalStat(gov);

        foreach (string gid in EnumerateResidentGeneralIds(s))
        {
            if (string.IsNullOrWhiteSpace(gid)) continue;
            string id = gid.Trim();
            if (string.Equals(id, govId, StringComparison.Ordinal)) continue;
            if (!generalMasterDataMap.TryGetValue(id, out var g) || g == null) continue;
            total += GetGeneralInternalStat(g) * 0.1f;
        }

        return Mathf.Max(0f, total);
    }

    IEnumerable<string> EnumerateResidentGeneralIds(CastleStateData s)
    {
        if (s == null || string.IsNullOrWhiteSpace(s.id))
            yield break;

        if (s.residentGeneralIds != null && s.residentGeneralIds.Count > 0)
        {
            for (int i = 0; i < s.residentGeneralIds.Count; i++)
                yield return s.residentGeneralIds[i];
            yield break;
        }

        string cid = s.id.Trim();
        foreach (var kv in generalMasterDataMap)
        {
            var g = kv.Value;
            if (g == null || string.IsNullOrWhiteSpace(g.initialCastleId)) continue;
            if (string.Equals(g.initialCastleId.Trim(), cid, StringComparison.Ordinal))
                yield return g.id;
        }
    }

    public CastleDividendPreview CalculateFinalDividend(CastleStateData s)
    {
        if (s == null || string.IsNullOrWhiteSpace(s.id))
            return new CastleDividendPreview(0f, 0f, 0.2f, 1f, 0f, 0, 0, 0);

        if (!castleMasterDataMap.TryGetValue(s.id.Trim(), out var master) || master == null)
            return new CastleDividendPreview(0f, 0f, 0.2f, 1f, 0f, 0, Mathf.Max(1, s.currentPopulation), Mathf.Max(1, s.maxGarrison));

        float internalStat = CalculateFinalInternalStat(s);
        Grade g = ResolveCastleGradeForCalc(s, master);
        float gradeW = GradeWeight(g);
        float sentimentMul = Mathf.Clamp(s.currentSentiment / 100f, 0f, 2f);
        float popMul = EvaluatePopulationEconomyMultiplier(s.currentPopulation);
        float baseProd = GetEconomyBaseValuePerTroop(master) * gradeW * sentimentMul * popMul * internalStat;

        int totalTroops = Mathf.Max(0, s.userDeployedTroops) + Mathf.Max(0, s.currentAiGarrison);
        int population = Mathf.Max(1, s.currentPopulation);
        int maxCap = Mathf.Max(1, s.maxGarrison);

        float efficiency = Mathf.Clamp(1f - (totalTroops / (float)population), 0.2f, 1f);
        float step1 = baseProd * efficiency;

        float overloadRatio = 1f;
        if (totalTroops > maxCap)
        {
            overloadRatio = Mathf.Clamp01(maxCap / (float)totalTroops);
            step1 *= overloadRatio * overloadRatio;
        }

        float finalDividend = Mathf.Max(0f, step1);
        return new CastleDividendPreview(internalStat, baseProd, efficiency, overloadRatio, finalDividend, totalTroops, population, maxCap);
    }

    public float CalculateExpectedDividendYieldPercent(CastleStateData s)
    {
        if (s == null) return 0f;
        float price = Mathf.Max(0.01f, s.currentBuyPrice);
        var p = CalculateFinalDividend(s);
        if (p.TotalTroops <= 0) return 0f;
        float perTroopDividend = p.FinalDividend / p.TotalTroops;
        return Mathf.Max(0f, perTroopDividend / price * 100f);
    }

    /// <summary>
    /// 과부하 시 (정원/병력)² 등이 반영된 주간 생산(금) 추정. UI·리포트용.
    /// </summary>
    public float GetEstimatedDividend(string castleId)
    {
        if (string.IsNullOrWhiteSpace(castleId) ||
            !castleStateDataMap.TryGetValue(castleId.Trim(), out var s) || s == null)
            return 0f;
        return CalculateFinalDividend(s).FinalDividend;
    }
}
