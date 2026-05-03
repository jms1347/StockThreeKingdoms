using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 성별 AI 내정·위기·전쟁 FSM. 전쟁 의지(WarDesire)·전략 배수·승산(지력) 필터를 사용합니다.
/// </summary>
public partial class DataManager
{
    const float AiCastleStrategyIntervalSeconds = 3600f;
    const long AiWarDurationUnixSeconds = 7200L;
    const float AdjacentThreatRatio = 1.5f;

    /// <summary>선전포고: 무력+악명 합이 이 값을 넘어야 공격 후보.</summary>
    const float WarAggressionStatGate = 140f;

    /// <summary>승산 원시값 (내/적)×(지력/100) 이 1.2 이상일 때 승률 약 70% 이상으로 간주.</summary>
    const float VictoryOddsRawGate = 1.2f;

    const float MinWarDesireToDeclare = 0.11f;

    static float _nextAiCastleStrategyUnscaled = -1f;

    internal void TickAiCastleStrategy(float unscaledTime)
    {
        if (!IsStateReady || castleStateDataMap == null || castleStateDataMap.Count == 0)
            return;

        if (_nextAiCastleStrategyUnscaled < 0f)
            _nextAiCastleStrategyUnscaled = unscaledTime + 8f;
        if (unscaledTime < _nextAiCastleStrategyUnscaled)
            return;
        _nextAiCastleStrategyUnscaled = unscaledTime + AiCastleStrategyIntervalSeconds;

        long nowUnix = TimeManager.GetUnixNow();
        ResolveExpiredAiWars(nowUnix);

        foreach (var kv in castleStateDataMap)
        {
            var s = kv.Value;
            if (s == null || string.IsNullOrWhiteSpace(s.id)) continue;
            UpdateAICastleStrategy(s, nowUnix);
        }

        _stateDirty = true;
        FlushLiveScriptableObjects();
        OnStateTicked?.Invoke();
    }

    void ResolveExpiredAiWars(long nowUnix)
    {
        var processed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kv in castleStateDataMap)
        {
            var s = kv.Value;
            if (s == null || string.IsNullOrWhiteSpace(s.id)) continue;
            string sid = s.id.Trim();
            if (processed.Contains(sid)) continue;
            if (string.IsNullOrWhiteSpace(s.aiWarOpponentCastleId)) continue;
            if (s.aiWarStartUnix <= 0L) continue;
            if (nowUnix - s.aiWarStartUnix < AiWarDurationUnixSeconds) continue;

            string opp = s.aiWarOpponentCastleId.Trim();
            processed.Add(sid);
            processed.Add(opp);

            ClearAiWarPairFlags(sid, opp);
        }
    }

    void ClearAiWarPairFlags(string idA, string idB)
    {
        if (castleStateDataMap.TryGetValue(idA, out var a) && a != null)
        {
            a.isWar = false;
            a.aiWarOpponentCastleId = "";
            a.aiWarStartUnix = 0L;
            if (CastleAmmCore.IsInitialized(a))
                a.currentBuyPrice = CalculateCastleQuote(a);
        }

        if (castleStateDataMap.TryGetValue(idB, out var b) && b != null)
        {
            b.isWar = false;
            b.aiWarOpponentCastleId = "";
            b.aiWarStartUnix = 0L;
            if (CastleAmmCore.IsInitialized(b))
                b.currentBuyPrice = CalculateCastleQuote(b);
        }
    }

    /// <summary>스탯 블렌드(무력40·악명40·지력20)% × 전략 배수.</summary>
    public float CalculateWarDesire(CastleActiveStats atk, CastleStateData attackerState,
        CastleStateData defenderState, int attackerTroops, int defenderTroops)
    {
        if (attackerState == null || defenderState == null) return 0f;
        float blend = (atk.Power * 0.4f + atk.Infamy * 0.4f + atk.Intel * 0.2f) / 100f;
        float mult =
            ComputeStrategicWarMultiplier(atk, attackerState, defenderState, attackerTroops, defenderTroops);
        return Mathf.Max(0f, blend * mult);
    }

    /// <summary>상대 과부하·병력 비·내 금고 부담을 반영한 전략 배수.</summary>
    float ComputeStrategicWarMultiplier(CastleActiveStats atk, CastleStateData attacker,
        CastleStateData defender, int attackerTroops, int defenderTroops)
    {
        float m = 1f;
        float te = Mathf.Max(1f, defenderTroops);
        float ratio = attackerTroops / te;

        if (ratio > 1f)
        {
            float exploit = Mathf.Clamp01((ratio - 1f) / 1.28f);
            m *= 1f + exploit * Mathf.Clamp01(atk.Intel / 100f) * 1.38f;
        }

        var defPreview = CalculateFinalDividend(defender);
        if (defPreview.IsOverloaded)
            m *= 1.14f + Mathf.Clamp01(atk.Intel / 100f) * 0.26f;

        float quote = Mathf.Max(20f, CalculateCastleQuote(attacker));
        long roughWarCost = (long)Mathf.Max(800f, attackerTroops * quote * 0.08f);
        float goldRatio = Mathf.Clamp01(attacker.goldReserve / (float)Mathf.Max(1L, roughWarCost));
        float stress = 1f - goldRatio;
        if (stress > 0.32f)
        {
            float iq = Mathf.Clamp01(atk.Intel / 100f);
            float damp = Mathf.Lerp(1f, 0.22f, Mathf.Clamp01((stress - 0.32f) / 0.68f) * iq);
            m *= damp;
        }

        return Mathf.Clamp(m, 0.06f, 3.6f);
    }

    /// <summary>(내 병력/적 병력)×(지력/100).</summary>
    public static float ComputeVictoryOddsRaw(CastleActiveStats atk, int ourTroops, int theirTroops) =>
        ourTroops / (float)Mathf.Max(1, theirTroops) * (atk.Intel / 100f);

    /// <summary>원시 승산 1.2를 약 70% 승률로 스케일한 표시용.</summary>
    public static float EstimateStrategicWinRatePercent(CastleActiveStats atk, int ourTroops, int theirTroops)
    {
        float raw = ComputeVictoryOddsRaw(atk, ourTroops, theirTroops);
        return Mathf.Clamp(raw / VictoryOddsRawGate * 70f, 0f, 99f);
    }

    internal void UpdateAICastleStrategy(CastleStateData s, long nowUnix)
    {
        if (s == null || string.IsNullOrWhiteSpace(s.id)) return;
        if (!castleMasterDataMap.TryGetValue(s.id.Trim(), out var master) || master == null) return;

        EnsureCastleAmmForState(s, master);
        if (!CastleAmmCore.IsInitialized(s)) return;

        CastleActiveStats st = CalculateCastleActiveStats(s);
        int ourTroops = EstimateCastleTotalGarrisonTroops(s.id);

        TryInboundNeighborWarDefense(s, master, st, ourTroops);

        bool crisisDisaster = s.isDisaster;
        bool crisisThreat = TryEvaluateAdjacentThreat(s, master, ourTroops, out _);

        if (crisisDisaster || crisisThreat)
        {
            if (crisisDisaster && st.Intel >= 60f)
            {
                SpendGoldReserveForDisasterRelief(s, st.Intel);
                float relief = Mathf.Clamp(11f + (st.Intel - 60f) * 0.14f, 9f, 30f);
                s.ApplySentimentDelta(relief);

                int u = Mathf.Max(0, s.userDeployedTroops);
                int g = Mathf.Max(0, s.currentAiGarrison);
                int cap = Mathf.Max(2, s.maxGarrison);
                CastleManager.TryRecruitAiGarrison(this, s, master, u, g, cap, out int recruited, out _);

                if (recruited <= 0 && st.Intel >= 68f)
                {
                    int room = cap - u - g;
                    int maxFromPop = Mathf.Max(0, s.currentPopulation - 1);
                    int n = Mathf.Min(5, room, maxFromPop);
                    if (n > 0)
                    {
                        CastleAmmCore.ApplySellFill(s, n);
                        s.ApplyPopulationDelta(-n);
                    }
                }

                s.isDisaster = false;
            }
            else if (crisisDisaster)
            {
                s.ApplySentimentDelta(-13f);
            }

            if (crisisThreat && st.Intel >= 55f)
                TryDefensiveRecruitment(s, master, st);
            else if (crisisThreat && !crisisDisaster)
                s.ApplySentimentDelta(-11f);

            s.currentBuyPrice = CalculateCastleQuote(s);
            return;
        }

        if (st.Infamy > st.Charm)
            TryApplyInfamyLevy(s, st, nowUnix);
        else if (st.Charm > st.Power)
            TryApplyCharmFestival(s, st, nowUnix);

        TryAiFrontRunGradeTransition(s, master, st);

        TryDischargeOverloadAiTroops(s, st);

        TryDeclareAiWar(s, master, st, ourTroops, nowUnix);

        s.currentBuyPrice = CalculateCastleQuote(s);
    }

    void TryAiFrontRunGradeTransition(CastleStateData s, CastleMasterData master, CastleActiveStats st)
    {
        if (s == null || master == null) return;
        if (!TryGetGradeSpeculation(s.id, out var signal, out float factor)) return;

        int u = Mathf.Max(0, s.userDeployedTroops);
        int g = Mathf.Max(0, s.currentAiGarrison);
        int cap = Mathf.Max(2, s.maxGarrison);

        if (signal == GradeSpeculationSignal.PromotionAlert && factor > 0f && st.Intel >= 70f)
        {
            CastleManager.TryRecruitAiGarrison(this, s, master, u, g, cap, out int recruited, out _);
            if (recruited <= 0)
            {
                int room = cap - u - g;
                int maxFromPop = Mathf.Max(0, s.currentPopulation - 1);
                int n = Mathf.Min(Mathf.CeilToInt(2f + factor * 22f), room, maxFromPop);
                if (n > 0)
                {
                    CastleAmmCore.ApplySellFill(s, n);
                    s.ApplyPopulationDelta(-n);
                }
            }
        }
        else if (signal == GradeSpeculationSignal.DemotionRisk && factor < 0f && st.Infamy > st.Charm)
        {
            TryApplyInfamyLevy(s, st, TimeManager.GetUnixNow());
        }
    }

    void SpendGoldReserveForDisasterRelief(CastleStateData s, float intelStat)
    {
        if (!CastleAmmCore.IsInitialized(s) || s.currentAiGarrison <= 0 || s.goldReserve <= 50L) return;

        float pct = Mathf.Clamp(0.06f + intelStat * 0.00065f, 0.05f, 0.22f);
        long spend = (long)Mathf.Min(s.goldReserve * pct, s.goldReserve * 0.35f);
        spend = Math.Min(spend, s.goldReserve - 40L);
        if (spend < 35L) return;

        long newR = Math.Max(40L, s.goldReserve - spend);
        int g = Mathf.Max(1, s.currentAiGarrison);
        s.constantK = (double)newR * g;
        s.goldReserve = newR;
    }

    void TryInboundNeighborWarDefense(CastleStateData us, CastleMasterData ourMaster, CastleActiveStats ourStats,
        int ourTroops)
    {
        if (ourMaster == null || ourStats.Intel < 50f) return;
        var adj = ourMaster.GetAdjacentIds();
        if (adj == null || adj.Count == 0) return;

        float peakDesire = 0f;
        for (int i = 0; i < adj.Count; i++)
        {
            string nid = adj[i]?.Trim();
            if (string.IsNullOrEmpty(nid)) continue;
            if (!castleStateDataMap.TryGetValue(nid, out var neighbor) || neighbor == null) continue;
            if (!IsHostileLordPair(us.currentLord, neighbor.currentLord)) continue;

            var nStats = CalculateCastleActiveStats(neighbor);
            int nt = EstimateCastleTotalGarrisonTroops(nid);
            float desire = CalculateWarDesire(nStats, neighbor, us, nt, ourTroops);
            if (desire > peakDesire)
                peakDesire = desire;
        }

        if (peakDesire < 0.20f || ourStats.Intel < 52f) return;

        int u = Mathf.Max(0, us.userDeployedTroops);
        int g = Mathf.Max(0, us.currentAiGarrison);
        int cap = Mathf.Max(2, us.maxGarrison);
        CastleManager.TryRecruitAiGarrison(this, us, ourMaster, u, g, cap, out int recruited, out _);

        if (recruited <= 0 && peakDesire >= 0.28f && ourStats.Intel >= 58f)
        {
            int room = cap - u - g;
            int maxFromPop = Mathf.Max(0, us.currentPopulation - 1);
            int n = Mathf.Min(peakDesire >= 0.34f ? 6 : 3, room, maxFromPop);
            if (n > 0)
            {
                CastleAmmCore.ApplySellFill(us, n);
                us.ApplyPopulationDelta(-n);
            }
        }
    }

    void TryDefensiveRecruitment(CastleStateData s, CastleMasterData master, CastleActiveStats st)
    {
        int u = Mathf.Max(0, s.userDeployedTroops);
        int g = Mathf.Max(0, s.currentAiGarrison);
        int cap = Mathf.Max(2, s.maxGarrison);
        CastleManager.TryRecruitAiGarrison(this, s, master, u, g, cap, out int recruited, out _);
        if (recruited <= 0 && st.Intel >= 62f)
        {
            int room = cap - u - g;
            int maxFromPop = Mathf.Max(0, s.currentPopulation - 1);
            int n = Mathf.Min(4, room, maxFromPop);
            if (n > 0)
            {
                CastleAmmCore.ApplySellFill(s, n);
                s.ApplyPopulationDelta(-n);
            }
        }
    }

    bool TryEvaluateAdjacentThreat(CastleStateData s, CastleMasterData master, int ourTroops,
        out int maxHostileAdjacentTroops)
    {
        maxHostileAdjacentTroops = 0;
        if (s == null || master == null) return false;

        var adj = master.GetAdjacentIds();
        if (adj == null || adj.Count == 0) return false;

        for (int i = 0; i < adj.Count; i++)
        {
            string aid = adj[i]?.Trim();
            if (string.IsNullOrEmpty(aid)) continue;
            if (!castleStateDataMap.TryGetValue(aid, out var neigh) || neigh == null) continue;
            if (!IsHostileLordPair(s.currentLord, neigh.currentLord)) continue;

            int t = EstimateCastleTotalGarrisonTroops(aid);
            if (t > maxHostileAdjacentTroops)
                maxHostileAdjacentTroops = t;
        }

        if (maxHostileAdjacentTroops <= 0) return false;
        if (ourTroops < 1)
            return maxHostileAdjacentTroops >= 80;

        int threshold = Mathf.CeilToInt(ourTroops * AdjacentThreatRatio);
        return maxHostileAdjacentTroops >= threshold;
    }

    static bool IsHostileLordPair(Faction a, Faction b)
    {
        if (a == Faction.NONE || b == Faction.NONE) return false;
        return a != b;
    }

    void TryApplyInfamyLevy(CastleStateData s, CastleActiveStats st, long nowUnix)
    {
        int g = Mathf.Max(0, s.currentAiGarrison);
        if (g <= 0 || s.goldReserve <= 0L) return;

        long levy = (long)Mathf.Clamp(st.Infamy * 120f + 400f, 200f, 25000f);
        levy = Math.Min(levy, (long)(s.goldReserve * 0.18f));
        if (levy < 50L) return;

        long newR = Math.Min(long.MaxValue - levy, s.goldReserve + levy);
        s.constantK = (double)newR * g;
        s.goldReserve = newR;

        float sentPen = Mathf.Clamp(6f + st.Infamy * 0.08f, 5f, 35f);
        s.ApplySentimentDelta(-sentPen);

        bool newsCooldownOk = nowUnix <= 0L ||
                              (s.lastAiTyrannyNewsUnix <= 0L || nowUnix - s.lastAiTyrannyNewsUnix >= 3600L);
        if (newsCooldownOk)
        {
            s.lastAiTyrannyNewsUnix = nowUnix;
            string govName = GetGovernorDisplayName(s);
            string hl = $"{govName}의 폭정으로 민심 파탄";
            string body = $"{GetCastleDisplayName(s.id)}에서 가혹한 징세가 이어지고 있습니다.";
            var item = NewsManager.BuildWorldNewsItem(WorldNewsFeedKind.Breaking, "", s.id.Trim(), hl, body, true);
            item.relatedCastleIdsRaw = s.id.Trim();
            AddNewsItem(item);
        }
    }

    void TryApplyCharmFestival(CastleStateData s, CastleActiveStats st, long nowUnix)
    {
        int g = Mathf.Max(0, s.currentAiGarrison);
        if (g <= 0 || s.goldReserve <= 1L) return;

        long spend = (long)Mathf.Clamp(s.goldReserve * 0.045f, 80f, 12000f);
        spend = Math.Min(spend, s.goldReserve - 1L);
        if (spend < 40L) return;

        long newR = Math.Max(1L, s.goldReserve - spend);
        s.constantK = (double)newR * g;
        s.goldReserve = newR;

        float bump = Mathf.Clamp(4f + st.Charm * 0.06f, 3f, 18f);
        s.ApplySentimentDelta(bump);
        s.isFavorableEvent = true;

        if (nowUnix <= 0L || s.lastAiFestivalNewsUnix <= 0L || nowUnix - s.lastAiFestivalNewsUnix >= 7200L)
        {
            s.lastAiFestivalNewsUnix = nowUnix;
            string govName = GetGovernorDisplayName(s);
            string hl = $"{govName}이 백성을 위한 행사를 개최";
            string body = $"{GetCastleDisplayName(s.id)}에서 민심 회복과 안정에 무게를 둔 내정이 전해집니다.";
            var item = NewsManager.BuildWorldNewsItem(WorldNewsFeedKind.Breaking, "", s.id.Trim(), hl, body, true);
            item.relatedCastleIdsRaw = s.id.Trim();
            AddNewsItem(item);
        }
    }

    string GetGovernorDisplayName(CastleStateData s)
    {
        if (s == null || string.IsNullOrWhiteSpace(s.currentGovernorId)) return "태수";
        var g = GetGeneralMasterData(s.currentGovernorId.Trim());
        if (g != null && !string.IsNullOrWhiteSpace(g.name)) return g.name.Trim();
        return s.currentGovernorId.Trim();
    }

    /// <summary>지력·매력형 구조조정: 과부하 시 AI 병 일부 해산.</summary>
    void TryDischargeOverloadAiTroops(CastleStateData s, CastleActiveStats st)
    {
        var preview = CalculateFinalDividend(s);
        if (!preview.IsOverloaded) return;

        bool smartPair = st.Intel >= 50f && st.Charm >= 48f && st.Charm + st.Intel * 0.35f > st.Power;
        bool intelSolo = st.Intel >= 62f && st.Charm < 42f;
        if (!smartPair && !intelSolo) return;

        int excess = preview.TotalTroops - preview.MaxCapacity;
        if (excess <= 0) return;

        int ai = Mathf.Max(0, s.currentAiGarrison);
        if (ai <= 0) return;

        int take = Mathf.Clamp(excess / 2, 1, ai);
        take = Mathf.Min(take, Mathf.Max(1, ai / (smartPair ? 6 : 8)));
        CastleAmmCore.ApplyBuyFill(s, take);
        CastleAmmCore.ResyncGoldReserveFromK(s);
    }

    void TryDeclareAiWar(CastleStateData attacker, CastleMasterData attackerMaster, CastleActiveStats st,
        int ourTroops, long nowUnix)
    {
        if (attacker == null || attackerMaster == null) return;
        if (attacker.isWar && !string.IsNullOrWhiteSpace(attacker.aiWarOpponentCastleId)) return;
        if (st.Power + st.Infamy <= WarAggressionStatGate) return;
        if (ourTroops < 8) return;

        var adj = attackerMaster.GetAdjacentIds();
        if (adj == null || adj.Count == 0) return;

        string bestTarget = null;
        float bestDesire = 0f;

        for (int i = 0; i < adj.Count; i++)
        {
            string tid = adj[i]?.Trim();
            if (string.IsNullOrEmpty(tid)) continue;
            if (!castleStateDataMap.TryGetValue(tid, out var def) || def == null) continue;
            if (!IsHostileLordPair(attacker.currentLord, def.currentLord)) continue;

            int their = EstimateCastleTotalGarrisonTroops(tid);
            if (their <= 0) continue;

            float raw = ComputeVictoryOddsRaw(st, ourTroops, their);
            if (raw < VictoryOddsRawGate) continue;

            float desire = CalculateWarDesire(st, attacker, def, ourTroops, their);
            if (desire < MinWarDesireToDeclare) continue;

            if (desire > bestDesire)
            {
                bestDesire = desire;
                bestTarget = tid;
            }
        }

        if (string.IsNullOrEmpty(bestTarget)) return;

        attacker.isWar = true;
        attacker.aiWarOpponentCastleId = bestTarget;
        attacker.aiWarStartUnix = nowUnix > 0L ? nowUnix : TimeManager.GetUnixNow();

        if (castleStateDataMap.TryGetValue(bestTarget, out var defender) && defender != null)
        {
            defender.isWar = true;
            defender.aiWarOpponentCastleId = attacker.id.Trim();
            defender.aiWarStartUnix = attacker.aiWarStartUnix;
            defender.currentBuyPrice = CalculateCastleQuote(defender);
        }

        attacker.currentBuyPrice = CalculateCastleQuote(attacker);

        string aName = GetCastleDisplayName(attacker.id);
        string bName = GetCastleDisplayName(bestTarget);
        string hl = $"{aName}이(가) {bName}을(를) 침공!";
        string body = $"전략적 승산 확보 후 선전포고 — 양 성 주가 변동성이 확대되었습니다.";
        var item = NewsManager.BuildWorldNewsItem(WorldNewsFeedKind.War, "", attacker.id.Trim(), hl, body, true);
        item.relatedCastleIdsRaw = $"{attacker.id.Trim()},{bestTarget.Trim()}";
        AddNewsItem(item);
    }

    public static string DescribeCastlePersonalityBrief(CastleActiveStats st)
    {
        float w = st.Power, iq = st.Intel, c = st.Charm, inf = st.Infamy;
        if (inf >= c + 15f && inf >= iq)
            return "고압·고변동";
        if (c >= w + 10f && c >= inf)
            return "안정·우량";
        if (w + inf >= iq + c)
            return "공격·격전";
        if (iq >= w && iq >= inf)
            return "내정·효율";
        return "균형";
    }
}
