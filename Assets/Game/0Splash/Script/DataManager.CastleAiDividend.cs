using System;
using UnityEngine;

public partial class DataManager
{
    /// <summary>
    /// 주간 배당 지급 직전 — 매력형은 풀 일부를 민심에 재투자, 악명형은 풀을 금고로 전용(유저 배당 축소).
    /// </summary>
    internal void ApplyAiCastleWeeklyDividendPolicy(CastleStateData s, CastleDividendPreview preview)
    {
        if (s == null || s.accumulatedDividendPool <= 0L) return;
        if (!CastleAmmCore.IsInitialized(s) || s.currentAiGarrison <= 0) return;

        CastleActiveStats st = CalculateCastleActiveStats(s);
        long pool = s.accumulatedDividendPool;

        if (st.Infamy > st.Charm && st.Infamy >= 35f)
        {
            float pct = Mathf.Clamp(0.07f + st.Infamy * 0.0011f, 0.05f, 0.32f);
            long skim = (long)Math.Floor(pool * pct);
            if (skim < 1L) return;

            skim = Math.Min(skim, pool);
            s.accumulatedDividendPool = pool - skim;

            int g = Mathf.Max(1, s.currentAiGarrison);
            long newR = Math.Min(long.MaxValue - skim, s.goldReserve + skim);
            s.constantK = (double)newR * g;
            s.goldReserve = newR;

            long nowUnix = TimeManager.GetUnixNow();
            if (s.lastAiDividendCutNewsUnix <= 0L || nowUnix - s.lastAiDividendCutNewsUnix >= 86400L * 5L)
            {
                s.lastAiDividendCutNewsUnix = nowUnix;
                string govName = GetGovernorDisplayName(s);
                string hl = $"태수 {govName}의 탐욕으로 배당금 삭감";
                string body =
                    $"{GetCastleDisplayName(s.id)}에서 군비 확충을 위해 성실 배당 재원이 일부 전용되었습니다.";
                var item = NewsManager.BuildWorldNewsItem(WorldNewsFeedKind.Breaking, "", s.id.Trim(), hl, body, true);
                item.relatedCastleIdsRaw = s.id.Trim();
                AddNewsItem(item);
            }

            return;
        }

        if (st.Charm > st.Infamy && st.Charm >= 48f)
        {
            float pct = Mathf.Clamp(0.04f + st.Charm * 0.00075f, 0.025f, 0.20f);
            long reinvest = (long)Math.Floor(pool * pct);
            if (reinvest < 1L) return;

            reinvest = Math.Min(reinvest, pool);
            s.accumulatedDividendPool = pool - reinvest;

            float bump = Mathf.Clamp(reinvest / 450f, 1.5f, 16f);
            s.ApplySentimentDelta(bump);
        }
    }
}
