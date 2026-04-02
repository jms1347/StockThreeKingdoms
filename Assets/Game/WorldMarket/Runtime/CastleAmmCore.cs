using System;
using UnityEngine;

/// <summary>
/// 성별 AI 수비군 AMM (constant product K = goldReserve × currentAiGarrison, K는 고정 double).
/// 매물은 <b>AI가 보유한 병력(currentAiGarrison)</b>뿐이며, 빈 정원(Max−U−G)만큼 자동 매수되지 않습니다.
/// G=0(풀 매수) 후에는 매수 불가, 일일 징병 등으로 G가 다시 생기면 매수 재개.
/// </summary>
public static class CastleAmmCore
{
    /// <summary>K가 잡힌 성 상태. G=0(금고 소진)도 유효한 '풀 매수' 상태입니다.</summary>
    public static bool IsInitialized(CastleStateData s) =>
        s != null && s.constantK > 0.0 && s.maxGarrison > 0;

    /// <summary>신규: AI = min(Max/2, Max−유저) 명, R≈G×단가, K=R×G.</summary>
    public static void InitializeAmm(CastleStateData s, int maxGarrison, int userDeployedTroops, double basePricePerTroop)
    {
        if (s == null) return;
        int m = Mathf.Max(2, maxGarrison);
        int u = Mathf.Max(0, userDeployedTroops);
        int room = m - u;
        if (room < 1)
        {
            u = Mathf.Max(0, m - 1);
            room = m - u;
        }

        int targetHalf = Mathf.Max(1, m / 2);
        int g = Mathf.Min(targetHalf, room);
        g = Mathf.Max(1, g);
        double bp = Math.Max(1.0, basePricePerTroop);
        double r = g * bp;
        double k = r * g;
        s.maxGarrison = m;
        s.currentAiGarrison = g;
        s.constantK = k;
        s.goldReserve = (long)Math.Round(r);
    }

    public static void ResyncGoldReserveFromK(CastleStateData s)
    {
        if (s == null || s.constantK <= 0.0) return;
        if (s.currentAiGarrison <= 0)
        {
            s.goldReserve = 0;
            return;
        }

        s.goldReserve = (long)Math.Round(s.constantK / s.currentAiGarrison);
    }

    public static bool TryComputeBuyGoldPrincipal(CastleStateData s, int buyAmount, out long principalGold)
    {
        principalGold = 0;
        if (!IsInitialized(s) || buyAmount <= 0) return false;
        int g = s.currentAiGarrison;
        if (g <= 0) return false;
        if (buyAmount > g) return false;

        if (buyAmount == g)
        {
            principalGold = Math.Max(0L, s.goldReserve);
            return true;
        }

        double r = s.goldReserve;
        double pay = s.constantK / (g - buyAmount) - r;
        if (!double.IsFinite(pay) || pay <= 0.0) return false;
        principalGold = (long)Math.Ceiling(pay);
        return true;
    }

    public static bool TryComputeSellGoldProceeds(CastleStateData s, int sellAmount, out long goldReceived)
    {
        goldReceived = 0;
        if (!IsInitialized(s) || sellAmount <= 0) return false;
        int g = s.currentAiGarrison;
        if (g <= 0) return false;

        double r = s.goldReserve;
        double newR = s.constantK / (g + sellAmount);
        if (!double.IsFinite(newR)) return false;
        double recv = r - newR;
        if (!double.IsFinite(recv)) return false;
        goldReceived = (long)Math.Floor(Math.Max(0.0, recv));
        return true;
    }

    public static void ApplyBuyFill(CastleStateData s, int buyAmount)
    {
        if (s == null || buyAmount <= 0) return;
        int g = s.currentAiGarrison;
        if (buyAmount > g) return;
        int gn = g - buyAmount;
        s.currentAiGarrison = gn;
        if (gn <= 0)
            s.goldReserve = 0;
        else
            s.goldReserve = (long)Math.Round(s.constantK / gn);
    }

    public static void ApplySellFill(CastleStateData s, int sellAmount)
    {
        if (s == null || sellAmount <= 0) return;
        int gn = s.currentAiGarrison + sellAmount;
        s.currentAiGarrison = gn;
        s.goldReserve = gn > 0 ? (long)Math.Round(s.constantK / gn) : 0L;
    }

    public static float GetMarginalBuyOneGoldAsFloat(CastleStateData s)
    {
        if (!IsInitialized(s) || s.currentAiGarrison <= 0) return 0f;
        int g = s.currentAiGarrison;
        if (g == 1)
            return (float)Math.Max(0.0, (double)s.goldReserve);

        double k = s.constantK;
        double r = s.goldReserve;
        double pay1 = k / (g - 1) - r;
        if (!double.IsFinite(pay1)) return 0f;
        return (float)Math.Max(0.0, pay1);
    }
}
