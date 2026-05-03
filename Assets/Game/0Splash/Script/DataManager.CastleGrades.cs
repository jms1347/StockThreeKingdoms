using System;
using System.Collections.Generic;
using UnityEngine;

public enum GradeSpeculationSignal : byte
{
    None = 0,
    PromotionAlert = 1,
    DemotionRisk = 2
}

public partial class DataManager
{
    const int CastleGradeLockSettlements = 3;
    const float GradeSpeculationEdgePercent = 0.02f;
    const float GradeSpeculationMaxAbsFactor = 0.15f;
    const float GradeSpeculationRefreshIntervalSeconds = 900f;

    readonly struct GradeBand
    {
        public readonly float Start;
        public readonly float End;

        public GradeBand(float start, float end)
        {
            Start = start;
            End = end;
        }
    }

    readonly struct CastleMarketCapRow
    {
        public readonly CastleStateData State;
        public readonly double MarketCap;

        public CastleMarketCapRow(CastleStateData state, double marketCap)
        {
            State = state;
            MarketCap = marketCap;
        }
    }

    sealed class GradeSpeculationSnapshot
    {
        public readonly GradeSpeculationSignal Signal;
        public readonly float Factor;

        public GradeSpeculationSnapshot(GradeSpeculationSignal signal, float factor)
        {
            Signal = signal;
            Factor = factor;
        }
    }

    Dictionary<string, GradeSpeculationSnapshot> _gradeSpeculationByCastleId;
    float _nextGradeSpeculationRefreshUnscaled = -1f;

    Grade ResolveCastleGradeForCalc(CastleStateData s, CastleMasterData master)
    {
        if (s == null) return master != null ? master.grade : Grade.D;
        bool hasRuntime = Enum.IsDefined(typeof(Grade), s.runtimeGrade);
        return hasRuntime ? s.runtimeGrade : (master != null ? master.grade : Grade.D);
    }

    public Grade GetCastleRuntimeGrade(string castleId)
    {
        if (string.IsNullOrWhiteSpace(castleId))
            return Grade.D;
        castleId = castleId.Trim();
        if (!castleStateDataMap.TryGetValue(castleId, out var s) || s == null)
            return Grade.D;
        castleMasterDataMap.TryGetValue(castleId, out var m);
        return ResolveCastleGradeForCalc(s, m);
    }

    internal void EnsureRuntimeGradeDefaults(CastleStateData s, CastleMasterData master)
    {
        if (s == null) return;
        if (!Enum.IsDefined(typeof(Grade), s.runtimeGrade))
            s.runtimeGrade = master != null ? master.grade : Grade.D;
        if (s.gradeLockRemainingSettlements < 0)
            s.gradeLockRemainingSettlements = 0;
        if (s.lastGradeChange < -1 || s.lastGradeChange > 1)
            s.lastGradeChange = 0;
    }

    internal void TickGradeSpeculation(float unscaledTime)
    {
        if (!IsStateReady || castleStateDataMap == null || castleStateDataMap.Count == 0)
            return;

        if (_nextGradeSpeculationRefreshUnscaled < 0f)
            _nextGradeSpeculationRefreshUnscaled = unscaledTime + 6f;
        if (unscaledTime < _nextGradeSpeculationRefreshUnscaled)
            return;

        _nextGradeSpeculationRefreshUnscaled = unscaledTime + GradeSpeculationRefreshIntervalSeconds;
        RefreshGradeSpeculationSnapshot();
    }

    public void ReevaluateCastleGradesWeekly()
    {
        if (!IsStateReady || castleStateDataMap == null || castleStateDataMap.Count == 0) return;

        var rows = new List<CastleMarketCapRow>(castleStateDataMap.Count);
        foreach (var kv in castleStateDataMap)
        {
            var s = kv.Value;
            if (s == null || string.IsNullOrWhiteSpace(s.id)) continue;
            if (!castleMasterDataMap.TryGetValue(s.id.Trim(), out var m) || m == null) continue;

            EnsureRuntimeGradeDefaults(s, m);
            float quote = Mathf.Max(0.01f, CalculateCastleQuoteWithoutSpeculation(s));
            int pop = Mathf.Max(1, s.currentPopulation);
            rows.Add(new CastleMarketCapRow(s, quote * pop));
        }

        if (rows.Count == 0) return;
        rows.Sort((a, b) => b.MarketCap.CompareTo(a.MarketCap));

        bool anyChanged = false;
        long now = TimeManager.GetUnixNow();
        int n = rows.Count;
        for (int i = 0; i < n; i++)
        {
            var s = rows[i].State;
            if (s == null) continue;

            Grade target = GradeFromPercentile(i / (float)n);
            Grade current = s.runtimeGrade;

            if (s.gradeLockRemainingSettlements > 0)
            {
                s.gradeLockRemainingSettlements -= 1;
                s.lastGradeChange = 0;
                continue;
            }

            if (target == current)
            {
                s.lastGradeChange = 0;
                continue;
            }

            s.runtimeGrade = target;
            s.lastGradeChange = target < current ? 1 : -1;
            s.lastGradeChangeUnix = now;
            s.gradeLockRemainingSettlements = CastleGradeLockSettlements;
            anyChanged = true;

            ApplyGradeCapacityToCastle(s);
        }

        RefreshGradeSpeculationSnapshot();

        if (!anyChanged) return;
        RecalculateAllPrices();
        _stateDirty = true;
        FlushLiveScriptableObjects();
        OnStateTicked?.Invoke();
    }

    public bool TryGetGradeSpeculation(string castleId, out GradeSpeculationSignal signal, out float factor)
    {
        signal = GradeSpeculationSignal.None;
        factor = 0f;
        if (string.IsNullOrWhiteSpace(castleId) || _gradeSpeculationByCastleId == null)
            return false;
        if (!_gradeSpeculationByCastleId.TryGetValue(castleId.Trim(), out var snap) || snap == null)
            return false;

        signal = snap.Signal;
        factor = snap.Factor;
        return signal != GradeSpeculationSignal.None && Mathf.Abs(factor) > 1e-4f;
    }

    float GetCastleGradeTransitionFactor(string castleId)
    {
        if (!TryGetGradeSpeculation(castleId, out _, out float factor))
            return 0f;
        return factor;
    }

    void RefreshGradeSpeculationSnapshot()
    {
        if (!IsStateReady || castleStateDataMap == null || castleStateDataMap.Count == 0)
            return;

        if (_gradeSpeculationByCastleId == null)
            _gradeSpeculationByCastleId = new Dictionary<string, GradeSpeculationSnapshot>(StringComparer.Ordinal);
        _gradeSpeculationByCastleId.Clear();

        var rows = new List<CastleMarketCapRow>(castleStateDataMap.Count);
        foreach (var kv in castleStateDataMap)
        {
            var s = kv.Value;
            if (s == null || string.IsNullOrWhiteSpace(s.id)) continue;
            if (!castleMasterDataMap.TryGetValue(s.id.Trim(), out var m) || m == null) continue;
            EnsureRuntimeGradeDefaults(s, m);
            float quote = Mathf.Max(0.01f, CalculateCastleQuoteWithoutSpeculation(s));
            int pop = Mathf.Max(1, s.currentPopulation);
            rows.Add(new CastleMarketCapRow(s, quote * pop));
        }

        if (rows.Count == 0) return;
        rows.Sort((a, b) => b.MarketCap.CompareTo(a.MarketCap));

        int n = rows.Count;
        for (int i = 0; i < n; i++)
        {
            var s = rows[i].State;
            if (s == null || string.IsNullOrWhiteSpace(s.id)) continue;

            float pct = i / (float)n;
            EvaluateSpeculationForGrade(s.runtimeGrade, pct, out var signal, out float factor);
            _gradeSpeculationByCastleId[s.id.Trim()] = new GradeSpeculationSnapshot(signal, factor);
        }
    }

    static void EvaluateSpeculationForGrade(Grade g, float percentile, out GradeSpeculationSignal signal, out float factor)
    {
        signal = GradeSpeculationSignal.None;
        factor = 0f;

        GradeBand band = GetBandForGrade(g);

        float upDist = float.PositiveInfinity;
        float downDist = float.PositiveInfinity;
        if (g != Grade.SS)
            upDist = Mathf.Abs(percentile - band.Start);
        if (g != Grade.D)
            downDist = Mathf.Abs(percentile - band.End);

        bool nearUp = upDist <= GradeSpeculationEdgePercent;
        bool nearDown = downDist <= GradeSpeculationEdgePercent;
        if (!nearUp && !nearDown)
            return;

        if (nearUp && (!nearDown || upDist <= downDist))
        {
            float closeness = 1f - Mathf.Clamp01(upDist / GradeSpeculationEdgePercent);
            signal = GradeSpeculationSignal.PromotionAlert;
            factor = closeness * GradeSpeculationMaxAbsFactor;
            return;
        }

        float downCloseness = 1f - Mathf.Clamp01(downDist / GradeSpeculationEdgePercent);
        signal = GradeSpeculationSignal.DemotionRisk;
        factor = -downCloseness * GradeSpeculationMaxAbsFactor;
    }

    void ApplyGradeCapacityToCastle(CastleStateData s)
    {
        if (s == null || string.IsNullOrWhiteSpace(s.id)) return;
        if (!castleMasterDataMap.TryGetValue(s.id.Trim(), out var master) || master == null) return;

        int occupied = Mathf.Max(0, s.userDeployedTroops) + Mathf.Max(0, s.currentAiGarrison);
        float mul = GradeCapacityMultiplier(s.runtimeGrade);
        int targetCap = Mathf.Max(2, Mathf.RoundToInt(Mathf.Max(2, master.maxTroops) * mul));
        targetCap = Mathf.Max(targetCap, occupied);

        if (targetCap == s.maxGarrison) return;
        s.maxGarrison = targetCap;
        if (CastleAmmCore.IsInitialized(s))
            CastleAmmCore.ResyncGoldReserveFromK(s);
        else
            EnsureCastleAmmForState(s, master);
    }

    static Grade GradeFromPercentile(float p)
    {
        p = Mathf.Clamp01(p);
        if (p < 0.05f) return Grade.SS;
        if (p < 0.15f) return Grade.S;
        if (p < 0.35f) return Grade.A;
        if (p < 0.60f) return Grade.B;
        if (p < 0.85f) return Grade.C;
        return Grade.D;
    }

    static GradeBand GetBandForGrade(Grade g)
    {
        switch (g)
        {
            case Grade.SS: return new GradeBand(0f, 0.05f);
            case Grade.S: return new GradeBand(0.05f, 0.15f);
            case Grade.A: return new GradeBand(0.15f, 0.35f);
            case Grade.B: return new GradeBand(0.35f, 0.60f);
            case Grade.C: return new GradeBand(0.60f, 0.85f);
            default: return new GradeBand(0.85f, 1f);
        }
    }

    static float GradeCapacityMultiplier(Grade g)
    {
        switch (g)
        {
            case Grade.SS: return 1.20f;
            case Grade.S: return 1.10f;
            case Grade.A: return 1.00f;
            case Grade.B: return 0.95f;
            case Grade.C: return 0.90f;
            default: return 0.85f;
        }
    }
}
