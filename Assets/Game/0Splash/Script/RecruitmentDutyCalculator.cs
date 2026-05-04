using System;
using UnityEngine;

/// <summary>
/// 성별 징집·입성·해산 AI 가변 수수료(Recruitment Duty). 전쟁 면제·과부하 고세 등.
/// 본영 이주(<see cref="WorldHqTravelHud"/>·MP) 경로와 무관합니다.
/// </summary>
public static class RecruitmentDutyCalculator
{
    const float MaxFeePercent = 20f;
    const float OverloadFillStart = 0.8f;

    /// <summary>입성·징집 총액에 곱할 계수 (1 + fee/100).</summary>
    public static double DeployCostMultiplier(float feePercent) =>
        1d + Mathf.Clamp(feePercent, 0f, MaxFeePercent) / 100d;

    /// <summary>회군·해산 지급액에 곱할 계수 (1 - fee/100).</summary>
    public static double RecallPayoutMultiplier(float feePercent) =>
        Math.Max(0d, 1d - Mathf.Clamp(feePercent, 0f, MaxFeePercent) / 100d);

    /// <returns>0.0 ~ 20.0 (소수 첫째 자리)</returns>
    public static float CalculateRecruitmentFee(DataManager dm, string castleId, out string reason)
    {
        reason = "";
        if (dm == null || string.IsNullOrWhiteSpace(castleId))
        {
            reason = "데이터 없음";
            return 5f;
        }

        castleId = castleId.Trim();
        if (!dm.castleStateDataMap.TryGetValue(castleId, out var s) || s == null)
        {
            reason = "성 상태 없음";
            return 5f;
        }

        if (s.isWar)
        {
            reason = "전쟁 중 — 징집·입성 의무 면제";
            return 0f;
        }

        var ast = dm.CalculateCastleActiveStats(s);
        float peak = Mathf.Max(ast.Power, ast.Intel, ast.Charm, ast.Infamy);
        // 무력이 네 스탯 중 최고이면서 일정 수준 이상 → 외부 병력 유입 유도(의무 0%).
        bool powerGovernor = peak > 1f && Mathf.Abs(ast.Power - peak) < 0.01f && ast.Power >= 52f;
        if (powerGovernor)
        {
            reason = "무력 중심 태수 — 외부 병력 유입 유도(의무 면제)";
            return 0f;
        }

        dm.castleMasterDataMap.TryGetValue(castleId, out var master);

        int cap = Mathf.Max(1, s.maxGarrison);
        int totalTroops = Mathf.Max(0, s.userDeployedTroops) + Mathf.Max(0, s.currentAiGarrison);
        float fill = totalTroops / (float)cap;

        float overloadRamp = 0f;
        if (fill >= OverloadFillStart)
            overloadRamp = Mathf.Lerp(0f, MaxFeePercent, Mathf.InverseLerp(OverloadFillStart, 1f, Mathf.Min(fill, 1f)));

        float reduction = 0f;
        if (fill < 0.32f)
        {
            reduction += 6f;
            reason = "방어 병력 부족 — 징집·입성 비용 인하";
        }

        if (master != null && HostileNeighborHasMoreTroops(dm, s, master))
        {
            reduction += 5f;
            if (string.IsNullOrEmpty(reason))
                reason = "인접 적 세력 압박 — 비용 인하";
        }

        var div = dm.CalculateFinalDividend(s);
        float overloadPenalty = 0f;
        if (div.IsOverloaded)
        {
            overloadPenalty += 8f;
            if (string.IsNullOrEmpty(reason))
                reason = "정원 과부하 — 거래 비용 상승";
        }

        float civilianStress = 0f;
        int initPop = master != null ? Mathf.Max(1, master.initPopulation) : 1000;
        float popRatio = s.currentPopulation / (float)initPop;
        if (popRatio < 0.18f && s.currentPopulation > 0)
        {
            civilianStress += 5f;
            if (string.IsNullOrEmpty(reason))
                reason = "백성 기반 약화 — 징집 제한(고율)";
        }

        float peacePressure = 0f;
        if (!s.isWar && fill > 0.55f && fill < 0.78f && overloadRamp < 0.01f)
        {
            peacePressure = 3f;
            if (string.IsNullOrEmpty(reason))
                reason = "평시 유지 — 군비 부담 조정";
        }

        float fee = overloadRamp + overloadPenalty + civilianStress + peacePressure - reduction;

        // 태수·주둔 합산 성향: 악명형은 통제·고율 쪽, 매력형은 완화.
        if (ast.Infamy > ast.Charm + 8f)
            fee += 2.5f;
        else if (ast.Charm > ast.Infamy + 8f)
            fee -= 2f;

        fee = Mathf.Clamp(fee, 0f, MaxFeePercent);
        fee = Mathf.Round(fee * 10f) / 10f;

        if (string.IsNullOrEmpty(reason))
        {
            if (fee >= 10f)
                reason = "과부하·수요 압력 — 고율 구간";
            else if (fee < 1f)
                reason = "징집 권장 구간";
            else
                reason = "평시 거래 의무";
        }

        return fee;
    }

    static bool HostileNeighborHasMoreTroops(DataManager dm, CastleStateData s, CastleMasterData master)
    {
        if (master == null) return false;
        var adj = master.GetAdjacentIds();
        if (adj == null || adj.Count == 0) return false;

        int ours = dm.EstimateCastleTotalGarrisonTroops(s.id);
        for (int i = 0; i < adj.Count; i++)
        {
            string nid = adj[i]?.Trim();
            if (string.IsNullOrEmpty(nid)) continue;
            if (!dm.castleStateDataMap.TryGetValue(nid, out var n) || n == null) continue;
            if (!IsHostileLordPair(s.currentLord, n.currentLord)) continue;
            int nt = dm.EstimateCastleTotalGarrisonTroops(nid);
            if (nt > ours + Mathf.Max(8, ours / 10))
                return true;
        }

        return false;
    }

    static bool IsHostileLordPair(Faction a, Faction b)
    {
        if (a == Faction.NONE || b == Faction.NONE) return false;
        return a != b;
    }
}
