using System;
using UnityEngine;

/// <summary>성 AI 주둔 징병·AMM 연동. 시장 공급(G)과 호가(R=K/G)를 함께 다룹니다.</summary>
public static class CastleManager
{
    const string LogTag = "[CastleManager][AI징병]";

    static readonly BalanceConfig FallbackAiRecruitBalance = new BalanceConfig();

    public enum AiRecruitTriggerKind
    {
        None = 0,
        InfamyBypass,
        NeighborTroopPressure,
        WarContext,
        PopulationVacancy,
        MarketRestoreOne
    }

    /// <summary>
    /// AI 병력 0일 때 시장 재개용 1명 복구. 백성 2명 이상(징발 후 최소 1 유지) 필요.
    /// </summary>
    public static bool TryRestoreMinimumAiGarrisonForMarket(CastleStateData s, int room, int maxFromPop)
    {
        if (s == null || !CastleAmmCore.IsInitialized(s)) return false;
        if (s.currentAiGarrison > 0) return false;
        if (room < 1 || maxFromPop < 1) return false;

        CastleAmmCore.ApplySellFill(s, 1);
        s.ApplyPopulationDelta(-1);
        Debug.Log(
            $"{LogTag} {s.id}: 시장 재생 +1 AI (조건 무시), 백성−1 · R={s.goldReserve} G={s.currentAiGarrison} K={s.constantK:0.##}",
            null);
        return true;
    }

    /// <summary>
    /// 대규모 징병: (Str+Int+Cha)×100/3 및 정원 비율 상한, 정원 여유·백성 한도 적용.
    /// AMM: K 유지, G 증가에 따라 R=K/G로 <see cref="CastleStateData.goldReserve"/> 재계산(<see cref="CastleAmmCore.ApplySellFill"/>).
    /// </summary>
    public static bool TryRecruitAiGarrison(DataManager dm, CastleStateData s, CastleMasterData master, int u, int g,
        int cap, out int recruited, out AiRecruitTriggerKind triggerKind)
    {
        recruited = 0;
        triggerKind = AiRecruitTriggerKind.None;

        if (dm == null || s == null || !CastleAmmCore.IsInitialized(s)) return false;
        if (string.IsNullOrWhiteSpace(s.currentGovernorId)) return false;

        var gen = dm.GetGeneralMasterData(s.currentGovernorId.Trim());
        if (gen == null) return false;

        var balance = ResolveBalance() ?? FallbackAiRecruitBalance;
        int batchRaw = ComputeRecruitBatchSizeFromStats(gen);
        int batchCapMax = Mathf.Max(1, Mathf.RoundToInt(cap * Mathf.Clamp01(balance.aiRecruitBatchCap)));
        int batch = Mathf.Min(batchRaw, batchCapMax);

        int room = cap - u - g;
        int maxFromPop = Mathf.Max(0, s.currentPopulation - 1);
        int n = Mathf.Min(batch, room, maxFromPop);
        if (n < 1) return false;

        if (!IsRecruitmentNeeded(dm, s, master, gen, u, g, cap, balance, out triggerKind))
            return false;

        CastleAmmCore.ApplySellFill(s, n);
        s.ApplyPopulationDelta(-n);
        recruited = n;

        Debug.Log(
            $"{LogTag} {s.id}: +{n} AI ({FormatTrigger(triggerKind)}), 백성−{n} · R={s.goldReserve} G={s.currentAiGarrison} K={s.constantK:0.##}",
            null);
        return true;
    }

    static string FormatTrigger(AiRecruitTriggerKind k) =>
        k switch
        {
            AiRecruitTriggerKind.InfamyBypass => "악명(조건 생략)",
            AiRecruitTriggerKind.NeighborTroopPressure => "인접 병력 위협",
            AiRecruitTriggerKind.WarContext => "전쟁/전쟁뉴스",
            AiRecruitTriggerKind.PopulationVacancy => "백성·주둔 공백",
            AiRecruitTriggerKind.MarketRestoreOne => "시장 재생(1명)",
            _ => k.ToString()
        };

    public static int ComputeRecruitBatchSizeFromStats(GeneralMasterData gen)
    {
        if (gen == null) return 1;
        long sum = (long)gen.power + gen.intel + gen.charm;
        long v = sum * 100L / 3L;
        if (v < 1L) v = 1L;
        if (v > int.MaxValue) return int.MaxValue;
        return (int)v;
    }

    /// <summary>악명 OR 인접 위협 OR 전쟁 맥락 OR 백성·공백 조건 중 하나.</summary>
    public static bool IsRecruitmentNeeded(DataManager dm, CastleStateData s, CastleMasterData master,
        GeneralMasterData gen, int u, int g, int cap, BalanceConfig balance, out AiRecruitTriggerKind triggerKind)
    {
        triggerKind = AiRecruitTriggerKind.None;
        if (gen == null || balance == null) return false;

        int infamyTh = Mathf.Clamp(balance.aiRecruitInfamyBypassThreshold, 0, 100);
        if (gen.infamy >= infamyTh)
        {
            triggerKind = AiRecruitTriggerKind.InfamyBypass;
            return true;
        }

        float neighRatio = Mathf.Max(1.01f, balance.aiRecruitNeighborTroopPressureRatio);
        int neighGap = Mathf.Max(0, balance.aiRecruitNeighborTroopAbsoluteGap);
        if (EvaluateNeighborTroopPressure(dm, s?.id, master, neighRatio, neighGap))
        {
            triggerKind = AiRecruitTriggerKind.NeighborTroopPressure;
            return true;
        }

        float lookback = Mathf.Max(60f, balance.aiRecruitWarNewsLookbackSeconds);
        if (EvaluateWarContext(dm, s, lookback))
        {
            triggerKind = AiRecruitTriggerKind.WarContext;
            return true;
        }

        float popPressure = Mathf.Clamp01(balance.aiRecruitPopulationPressureRatio);
        float vacantRatio = Mathf.Clamp01(balance.aiRecruitMinVacantGarrisonRatio);
        if (EvaluatePopulationVacancyPressure(s, u, g, cap, popPressure, vacantRatio))
        {
            triggerKind = AiRecruitTriggerKind.PopulationVacancy;
            return true;
        }

        return false;
    }

    static BalanceConfig ResolveBalance() => GameManager.InstanceOrNull?.balance;

    static bool EvaluateNeighborTroopPressure(DataManager dm, string castleId, CastleMasterData master, float ratio,
        int absoluteGap)
    {
        if (dm == null || string.IsNullOrWhiteSpace(castleId)) return false;
        int our = dm.EstimateCastleTotalGarrisonTroops(castleId.Trim());
        int neighborMax = 0;
        if (master != null)
        {
            var adj = master.GetAdjacentIds();
            if (adj != null)
            {
                for (int i = 0; i < adj.Count; i++)
                {
                    string aid = adj[i]?.Trim();
                    if (string.IsNullOrEmpty(aid)) continue;
                    int t = dm.EstimateCastleTotalGarrisonTroops(aid);
                    if (t > neighborMax) neighborMax = t;
                }
            }
        }

        if (neighborMax <= 0) return false;
        if (our < 1) return neighborMax >= absoluteGap;
        if (neighborMax - our >= absoluteGap) return true;
        return neighborMax >= Mathf.CeilToInt(our * ratio);
    }

    static bool EvaluateWarContext(DataManager dm, CastleStateData s, float lookbackSeconds)
    {
        if (s != null && s.isWar) return true;
        long now = TimeManager.GetUnixNow();
        long cutoff = now - (long)lookbackSeconds;
        string cid = (s?.id ?? "").Trim();
        if (string.IsNullOrEmpty(cid)) return false;
        var news = dm.worldNews;
        if (news == null || news.Count == 0) return false;
        for (int i = news.Count - 1; i >= 0; i--)
        {
            var w = news[i];
            if (w == null || w.unixTime < cutoff) continue;
            if (!WorldNewsItemTouchesCastle(w, cid)) continue;
            if (WorldNewsTextLooksLikeWar(w)) return true;
        }

        return false;
    }

    static bool EvaluatePopulationVacancyPressure(CastleStateData s, int u, int g, int cap, float popPressureRatio,
        float vacantRatio)
    {
        if (s == null || cap <= 0) return false;
        float popPerCap = s.currentPopulation / (float)cap;
        if (popPerCap < popPressureRatio) return false;
        int empty = cap - u - g;
        if (empty <= 0) return false;
        return empty / (float)cap >= vacantRatio;
    }

    static bool WorldNewsItemTouchesCastle(WorldNewsItem w, string castleId)
    {
        if (w == null || string.IsNullOrWhiteSpace(castleId)) return false;
        castleId = castleId.Trim();
        if (string.Equals((w.targetCastleId ?? "").Trim(), castleId, StringComparison.Ordinal))
            return true;
        if (!string.IsNullOrWhiteSpace(w.relatedCastleIdsRaw))
        {
            var parts = w.relatedCastleIdsRaw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.Equals(parts[i].Trim(), castleId, StringComparison.Ordinal))
                    return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(w.headline) && w.headline.IndexOf(castleId, StringComparison.Ordinal) >= 0)
            return true;
        if (!string.IsNullOrWhiteSpace(w.text) && w.text.IndexOf(castleId, StringComparison.Ordinal) >= 0)
            return true;
        if (!string.IsNullOrWhiteSpace(w.bodyContent) &&
            w.bodyContent.IndexOf(castleId, StringComparison.Ordinal) >= 0)
            return true;
        return false;
    }

    static bool WorldNewsTextLooksLikeWar(WorldNewsItem w)
    {
        string h = $"{w?.headline}\n{w?.bodyContent}\n{w?.text}";
        if (string.IsNullOrWhiteSpace(h)) return false;
        return h.IndexOf("전쟁", StringComparison.Ordinal) >= 0
               || h.IndexOf("전투", StringComparison.Ordinal) >= 0
               || h.IndexOf("침공", StringComparison.Ordinal) >= 0
               || h.IndexOf("공성", StringComparison.Ordinal) >= 0
               || h.IndexOf("출병", StringComparison.Ordinal) >= 0
               || h.IndexOf("war", StringComparison.OrdinalIgnoreCase) >= 0
               || h.IndexOf("battle", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
