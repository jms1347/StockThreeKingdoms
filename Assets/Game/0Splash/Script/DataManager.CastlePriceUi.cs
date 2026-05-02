using System.Collections.Generic;
using UnityEngine;

public partial class DataManager
{
    /// <summary>
    /// 요약 패널 7일 추이용 시세 스냅샷(비영속 UI 파생값). 민심 7일 곡선과 전일 종가를 결합해 형태를 만듭니다.
    /// </summary>
    public List<float> GetCastlePriceSeries7DayForUi(string castleId)
    {
        var result = new List<float>(7);
        if (string.IsNullOrWhiteSpace(castleId) || castleStateDataMap == null)
        {
            for (int i = 0; i < 7; i++)
                result.Add(1000f);
            return result;
        }

        castleId = castleId.Trim();
        if (!castleStateDataMap.TryGetValue(castleId, out var s) || s == null)
        {
            float q = Mathf.Max(1f, EvaluateCastleQuoteForCastle(castleId));
            for (int i = 0; i < 7; i++)
                result.Add(q);
            return result;
        }

        SyncCastleMarketPricesFromFormula(castleId);
        float spot = Mathf.Max(1f, CalculateCastleQuote(s));
        float anchor = s.buyPricePrevDayClose > 0.5f ? s.buyPricePrevDayClose : spot * 0.94f;

        var sent = s.historySentiment7Day;
        if (sent == null || sent.Count < 7)
        {
            for (int i = 0; i < 7; i++)
            {
                float t = i / 6f;
                result.Add(Mathf.Max(1f, Mathf.Lerp(anchor * 0.92f, spot, t)));
            }

            result[6] = spot;
            return result;
        }

        float minS = sent[0];
        float maxS = sent[0];
        for (int i = 1; i < 7; i++)
        {
            minS = Mathf.Min(minS, sent[i]);
            maxS = Mathf.Max(maxS, sent[i]);
        }

        float rng = Mathf.Max(4f, maxS - minS);
        for (int i = 0; i < 7; i++)
        {
            float sn = (sent[i] - minS) / rng;
            float t = i / 6f;
            float mix = Mathf.Lerp(anchor, spot, t);
            float wave = 1f + (sn - 0.5f) * 0.14f;
            result.Add(Mathf.Max(1f, mix * wave));
        }

        result[6] = spot;
        return result;
    }

    /// <summary>호가·징집 활동량 프록시(병력 규모 기반). 절대 거래 로그가 아닌 PD용 시각 지표.</summary>
    public long GetCastleTradingVolumeTroopProxy(string castleId)
    {
        if (string.IsNullOrWhiteSpace(castleId) || castleStateDataMap == null)
            return 0L;
        if (!castleStateDataMap.TryGetValue(castleId.Trim(), out var s) || s == null)
            return 0L;

        long flow = (long)Mathf.Max(0, s.userDeployedTroops) + (long)Mathf.Max(0, s.currentAiGarrison);
        long mult = s.isWar ? 420L : 260L;
        long vol = flow * mult;
        if (vol < 800L) vol = 800L;
        if (vol > 2000000000L) vol = 2000000000L;
        return vol;
    }

    /// <summary>7일 시리즈에서 최고·최저 시세.</summary>
    public static void GetMinMaxFromPriceSeries(IReadOnlyList<float> series, out float min, out float max)
    {
        min = float.MaxValue;
        max = float.MinValue;
        if (series == null || series.Count == 0)
        {
            min = max = 0f;
            return;
        }

        for (int i = 0; i < series.Count; i++)
        {
            float v = series[i];
            if (v < min) min = v;
            if (v > max) max = v;
        }

        if (min > max)
            min = max = 0f;
    }
}
