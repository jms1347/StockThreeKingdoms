using System;
using System.Collections.Generic;
using UnityEngine;

public partial class DataManager
{
    long _lastUtcDayBucket = -1;

    void TickCastleDailyHistoryRollover()
    {
        if (!IsStateReady || castleStateDataMap == null) return;
        long day = TimeManager.GetUnixNow() / 86400L;
        if (_lastUtcDayBucket < 0)
        {
            _lastUtcDayBucket = day;
            foreach (var kv in castleStateDataMap)
            {
                castleMasterDataMap.TryGetValue(kv.Key, out var master);
                EnsureCastleHistorySeeded(kv.Value, master);
            }

            return;
        }

        if (day <= _lastUtcDayBucket) return;

        foreach (var kv in castleStateDataMap)
        {
            var s = kv.Value;
            if (s == null) continue;
            castleMasterDataMap.TryGetValue(s.id, out var master);
            PushHistory7(s.historyPopulation7Day, s.currentPopulation);
            PushHistory7(s.historySentiment7Day, s.currentSentiment);
            float px = CalculateBuyPrice(s);
            s.buyPricePrevDayClose = px;
        }

        _lastUtcDayBucket = day;
        _stateDirty = true;
    }

    /// <summary>전일 종가(또는 마지막 일괄 스냅샷) 대비 매수가 등락률(%).</summary>
    public float CalculateChangeRate24h(CastleStateData s)
    {
        if (s == null) return 0f;
        float cur = CalculateBuyPrice(s);
        float @ref = s.buyPricePrevDayClose;
        if (@ref < 0.5f) return 0f;
        return (cur - @ref) / @ref * 100f;
    }

    public float EvaluateBuyPriceForCastle(string castleId)
    {
        if (string.IsNullOrWhiteSpace(castleId) || !castleStateDataMap.TryGetValue(castleId.Trim(), out var s) || s == null)
            return 0f;
        return CalculateBuyPrice(s);
    }

    public float EvaluateSellPriceForCastle(string castleId)
    {
        if (string.IsNullOrWhiteSpace(castleId) || !castleStateDataMap.TryGetValue(castleId.Trim(), out var s) || s == null)
            return 0f;
        return CalculateSellPrice(s);
    }

    /// <summary>이주 비용 포인트(명세 CalculateStepCost와 동일 의미).</summary>
    public float CalculateStepCost(string homeCastleId, string targetCastleId)
    {
        string prev = _homeCastleId;
        try
        {
            _homeCastleId = homeCastleId ?? "";
            return GetTravelCostPoints(targetCastleId);
        }
        finally
        {
            _homeCastleId = prev;
        }
    }

    public void SyncCastleMarketPricesFromFormula(string castleId)
    {
        if (string.IsNullOrWhiteSpace(castleId) || !castleStateDataMap.TryGetValue(castleId.Trim(), out var s) || s == null)
            return;
        s.currentBuyPrice = CalculateBuyPrice(s);
        s.currentSellPrice = CalculateSellPrice(s);
    }

    /// <summary>스프레드·버프 반영 전 내재가(액면가×인구·민심·등급).</summary>
    public float EvaluateBasePriceForCastle(string castleId)
    {
        if (string.IsNullOrWhiteSpace(castleId) || !castleStateDataMap.TryGetValue(castleId.Trim(), out var s) || s == null)
            return 0f;
        return CalculateBasePrice(s);
    }

    /// <summary>
    /// 모든 성의 (내재가 ÷ 성 기준가) 비율 통계. 자산 막대를 천하 분포 기준으로 나누는 데 사용.
    /// </summary>
    public bool TryGetWorldCastleAssetRatioStats(out float minRatio, out float maxRatio, out float meanRatio, out int count)
    {
        minRatio = maxRatio = meanRatio = 0f;
        count = 0;
        if (!IsStateReady || castleStateDataMap == null || castleMasterDataMap == null)
            return false;

        float sum = 0f;
        minRatio = float.PositiveInfinity;
        maxRatio = float.NegativeInfinity;

        foreach (var kv in castleStateDataMap)
        {
            var s = kv.Value;
            if (s == null || string.IsNullOrWhiteSpace(s.id)) continue;
            if (!castleMasterDataMap.TryGetValue(s.id.Trim(), out var m) || m == null) continue;

            float intrinsic = CalculateBasePrice(s);
            if (intrinsic < 0f) intrinsic = 0f;
            float baseVal = Mathf.Max(1f, m.baseValue);
            float r = intrinsic / baseVal;

            count++;
            sum += r;
            if (r < minRatio) minRatio = r;
            if (r > maxRatio) maxRatio = r;
        }

        if (count == 0)
            return false;

        meanRatio = sum / count;
        if (minRatio > maxRatio)
            minRatio = maxRatio = meanRatio;
        if (!float.IsFinite(minRatio) || !float.IsFinite(maxRatio))
        {
            minRatio = maxRatio = meanRatio;
        }

        return true;
    }

    /// <summary>주둔 추정(AI) + <see cref="CastleStateData.userDeployedTroops"/> 합. 상한은 <see cref="CastleMasterData.maxTroops"/>.</summary>
    public int EstimateCastleTotalGarrisonTroops(string castleId)
    {
        if (string.IsNullOrWhiteSpace(castleId) || !castleStateDataMap.TryGetValue(castleId.Trim(), out var s) || s == null)
            return 0;
        if (!castleMasterDataMap.TryGetValue(s.id, out var m) || m == null)
            return Mathf.Max(0, s.userDeployedTroops);

        int cap = Mathf.Max(1, m.maxTroops);
        float popRatio = Mathf.Clamp01(s.currentPopulation / (float)cap);
        int ai = Mathf.Clamp(Mathf.RoundToInt(cap * (0.28f + 0.55f * popRatio)), 0, cap);
        return Mathf.Min(cap, ai + Mathf.Max(0, s.userDeployedTroops));
    }

    internal static void PushHistory7(List<float> list, float value)
    {
        if (list == null) return;
        list.Add(value);
        while (list.Count > 7)
            list.RemoveAt(0);
    }

    internal void EnsureCastleHistorySeeded(CastleStateData s, CastleMasterData master)
    {
        if (s == null) return;
        if (s.historyPopulation7Day == null) s.historyPopulation7Day = new List<float>();
        if (s.historySentiment7Day == null) s.historySentiment7Day = new List<float>();

        if (s.historyPopulation7Day.Count >= 7 && s.historySentiment7Day.Count >= 7)
            return;

        int cap = master?.maxTroops ?? Mathf.Max(1, s.currentPopulation);
        int seed = unchecked(s.id.GetHashCode() ^ s.currentPopulation);
        var rnd = new System.Random(seed);
        float sent = s.currentSentiment;
        int pop = s.currentPopulation;

        while (s.historyPopulation7Day.Count < 7)
        {
            int idx = s.historyPopulation7Day.Count;
            float t = idx / 6f;
            float popJ = pop * (0.92f + 0.16f * (float)rnd.NextDouble()) * (0.85f + t * 0.15f);
            s.historyPopulation7Day.Add(Mathf.Clamp(popJ, 0f, cap * 1.05f));
            float sentJ = Mathf.Clamp(sent + (float)(rnd.NextDouble() * 14 - 7 + (idx - 3) * 2f), 5f, 100f);
            s.historySentiment7Day.Add(sentJ);
        }

        while (s.historyPopulation7Day.Count > 7) s.historyPopulation7Day.RemoveAt(0);
        while (s.historySentiment7Day.Count > 7) s.historySentiment7Day.RemoveAt(0);

        if (s.buyPricePrevDayClose < 0.5f)
            s.buyPricePrevDayClose = CalculateBuyPrice(s);
    }
}
