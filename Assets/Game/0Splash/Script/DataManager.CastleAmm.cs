using System;
using UnityEngine;

public partial class DataManager
{
    /// <summary>로드·신규 생성 직후 모든 성에 AMM 필드를 채우거나 K 기준으로 R을 정렬합니다.</summary>
    public void EnsureAllCastlesAmmInitialized()
    {
        // InitializeStateData()에서 IsStateReady=true 이전에도 호출되므로 맵 존재만 검사합니다.
        if (castleStateDataMap == null) return;
        foreach (var kv in castleStateDataMap)
        {
            var s = kv.Value;
            if (s == null) continue;
            castleMasterDataMap.TryGetValue(s.id, out var m);
            EnsureCastleAmmForState(s, m);
        }
    }

    internal void EnsureCastleAmmForState(CastleStateData s, CastleMasterData m)
    {
        if (s == null) return;
        int baseCap = m != null ? Mathf.Max(2, m.maxTroops) : Mathf.Max(2, s.maxGarrison);
        if (s.maxGarrison <= 0)
            s.maxGarrison = baseCap;
        s.maxGarrison = Mathf.Max(2, s.maxGarrison);

        if (CastleAmmCore.IsInitialized(s))
        {
            CastleAmmCore.ResyncGoldReserveFromK(s);
            return;
        }

        int mCap = s.maxGarrison;
        int u = Mathf.Max(0, s.userDeployedTroops);
        int room = mCap - u;
        if (room < 1)
        {
            s.userDeployedTroops = Mathf.Max(0, mCap - 1);
            u = s.userDeployedTroops;
            room = mCap - u;
        }

        int targetHalf = Mathf.Max(1, mCap / 2);
        int g = Mathf.Min(targetHalf, room);
        g = Mathf.Max(1, g);
        float bpF = CalculateBasePrice(s);
        double bp = Math.Max(1.0, (double)bpF);
        double r = g * bp;
        double k = r * g;
        s.currentAiGarrison = g;
        s.constantK = k;
        s.goldReserve = (long)Math.Round(r);
    }

    /// <summary>본영 시장·농장 레벨에 따라 본거지 성의 <see cref="CastleStateData.maxGarrison"/>만 증가시킵니다. AI 병력은 일일 리젠으로만 늘어납니다.</summary>
    public void RefreshHomeCastleMaxGarrisonFromUserBuildings()
    {
        if (!IsStateReady || string.IsNullOrWhiteSpace(_homeCastleId)) return;
        string hid = _homeCastleId.Trim();
        if (!castleStateDataMap.TryGetValue(hid, out var s) || s == null) return;
        if (!castleMasterDataMap.TryGetValue(hid, out var m) || m == null) return;

        var gm = GameManager.InstanceOrNull;
        int baseCap = Mathf.Max(2, m.maxTroops);
        int bonus = 0;
        if (gm?.currentUser != null)
            bonus = gm.currentUser.marketLevel * 25 + gm.currentUser.farmLevel * 15;

        int target = Mathf.Max(s.maxGarrison, baseCap + bonus);
        if (target == s.maxGarrison) return;
        s.maxGarrison = target;
        if (CastleAmmCore.IsInitialized(s))
            CastleAmmCore.ResyncGoldReserveFromK(s);
        else
            EnsureCastleAmmForState(s, m);
        _stateDirty = true;
        FlushLiveScriptableObjects();
        OnStateTicked?.Invoke();
    }

    /// <summary>
    /// 게임 일자마다 AI 주둔 조정 및 <see cref="CastleManager"/> 징병.
    /// </summary>
    internal void TickAiGarrisonRegenForNewGameDay()
    {
        if (!IsStateReady || castleStateDataMap == null) return;
        bool any = false;
        foreach (var kv in castleStateDataMap)
        {
            var s = kv.Value;
            if (s == null || !CastleAmmCore.IsInitialized(s)) continue;
            int u = Mathf.Max(0, s.userDeployedTroops);
            int g = Mathf.Max(0, s.currentAiGarrison);
            int cap = Mathf.Max(2, s.maxGarrison);
            int maxAiAllowed = Mathf.Max(0, cap - u);
            int targetHalf = Mathf.Max(1, cap / 2);
            int desiredAi = Mathf.Min(targetHalf, maxAiAllowed);

            if (maxAiAllowed <= 0)
                continue;

            if (g > desiredAi)
            {
                int ng = g - 1;
                if (ng <= 0)
                {
                    s.currentAiGarrison = 0;
                    s.goldReserve = 0;
                }
                else
                {
                    s.currentAiGarrison = ng;
                    s.goldReserve = (long)Math.Round(s.constantK / ng);
                }

                any = true;
                continue;
            }

            int room = cap - u - g;
            int maxFromPop = Mathf.Max(0, s.currentPopulation - 1);

            if (g <= 0)
            {
                if (CastleManager.TryRestoreMinimumAiGarrisonForMarket(s, room, maxFromPop))
                    any = true;
                continue;
            }

            castleMasterDataMap.TryGetValue(s.id, out var master);
            if (CastleManager.TryRecruitAiGarrison(this, s, master, u, g, cap, out _, out _))
                any = true;
        }

        if (any)
            _stateDirty = true;
    }

    static int MaxAffordableAmmBuy(CastleStateData s, long gold, float taxPercent)
    {
        if (!CastleAmmCore.IsInitialized(s) || gold <= 0L) return 0;
        int hi = Mathf.Max(0, s.currentAiGarrison);
        int lo = 1;
        int best = 0;
        if (hi < 1) return 0;
        while (lo <= hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (!CastleAmmCore.TryComputeBuyGoldPrincipal(s, mid, out long principal)) { hi = mid - 1; continue; }
            double rate = Mathf.Clamp(taxPercent, 0f, 500f) / 100.0;
            long tax = (long)Math.Round(principal * rate);
            long total = principal + tax;
            if (total <= gold)
            {
                best = mid;
                lo = mid + 1;
            }
            else hi = mid - 1;
        }

        return best;
    }

    /// <summary>AI 수비군 매수 한도: 현재 AI 보유 수(빈 정원은 매물 아님).</summary>
    public DeployTroopCapBreakdown ComputeDeployTroopCapBreakdown(string castleId)
    {
        if (!IsStateReady || string.IsNullOrWhiteSpace(castleId))
            return new DeployTroopCapBreakdown(0, 0, 0, 0);
        castleId = castleId.Trim();
        if (!castleStateDataMap.TryGetValue(castleId, out var s) || s == null)
            return new DeployTroopCapBreakdown(0, 0, 0, 0);
        castleMasterDataMap.TryGetValue(castleId, out var master);
        EnsureCastleAmmForState(s, master);

        if (!CastleAmmCore.IsInitialized(s))
            return new DeployTroopCapBreakdown(0, 0, 0, 0);

        int maxByAi = Mathf.Max(0, s.currentAiGarrison);
        var gm = GameManager.InstanceOrNull;
        long gold = gm?.currentGold ?? 0L;
        float taxP = s.castleTaxRatePercent;
        int maxByGold = maxByAi > 0 ? MaxAffordableAmmBuy(s, gold, taxP) : 0;
        int final = Mathf.Min(maxByAi, maxByGold);
        const int poolUnused = int.MaxValue;
        return new DeployTroopCapBreakdown(final, poolUnused, maxByGold, maxByAi);
    }

    /// <summary>AMM 매수: 금화(본금+관부) 차감 후 <see cref="CastleStateData.userDeployedTroops"/> 증가. 글로벌 병사 풀은 사용하지 않습니다.</summary>
    public void AddUserCastleDeployment(string castleId, int additionalTroops, float pricePerTroopIgnored)
    {
        if (!IsStateReady || string.IsNullOrWhiteSpace(castleId) || additionalTroops <= 0) return;
        castleId = castleId.Trim();
        if (!castleStateDataMap.TryGetValue(castleId, out var s) || s == null) return;
        castleMasterDataMap.TryGetValue(castleId, out var master);
        EnsureCastleAmmForState(s, master);
        if (!CastleAmmCore.IsInitialized(s)) return;

        int maxByAi = Mathf.Max(0, s.currentAiGarrison);
        additionalTroops = Mathf.Min(additionalTroops, maxByAi);
        if (additionalTroops <= 0) return;

        if (!CastleAmmCore.TryComputeBuyGoldPrincipal(s, additionalTroops, out long principalGold)) return;
        double taxRate = Mathf.Clamp(s.castleTaxRatePercent, 0f, 500f) / 100.0;
        long taxGold = (long)Math.Round(principalGold * taxRate);
        long goldCost = principalGold + taxGold;
        float effectivePerTroop = additionalTroops > 0 ? (float)(goldCost / (double)additionalTroops) : 0f;

        var gmSpend = GameManager.InstanceOrNull;
        if (goldCost > 0L && (gmSpend == null || !gmSpend.UseGold(goldCost)))
            return;

        if (taxGold > 0L)
        {
            double sumPool = (double)s.accumulatedDividendPool + taxGold;
            s.accumulatedDividendPool = sumPool >= long.MaxValue ? long.MaxValue : (long)sumPool;
        }

        int u = s.userDeployedTroops;
        CastleAmmCore.ApplyBuyFill(s, additionalTroops);
        long newTotal = (long)u + additionalTroops;
        if (newTotal > int.MaxValue) newTotal = int.MaxValue;
        if (u <= 0)
            s.averagePurchasePrice = effectivePerTroop;
        else
        {
            double sumCost = s.averagePurchasePrice * u + effectivePerTroop * additionalTroops;
            s.averagePurchasePrice = (float)(sumCost / newTotal);
        }

        s.userDeployedTroops = (int)newTotal;

        _stateDirty = true;
        s.currentBuyPrice = CalculateCastleQuote(s);
        FlushLiveScriptableObjects();
        RefreshGlobalTopBarIfPossible();
        OnStateTicked?.Invoke();
    }

    /// <summary>AMM 매도: 지급 금화 = R − K/(G+n), AI 병력 복귀.</summary>
    public bool TryComputeRecallGoldPayout(string castleId, int troops, out long unitFaceGold, out long totalGold)
    {
        unitFaceGold = totalGold = 0;
        if (!IsStateReady || string.IsNullOrWhiteSpace(castleId) || troops <= 0) return false;
        castleId = castleId.Trim();
        if (!castleStateDataMap.TryGetValue(castleId, out var s) || s == null) return false;
        castleMasterDataMap.TryGetValue(castleId, out var master);
        EnsureCastleAmmForState(s, master);
        if (!CastleAmmCore.IsInitialized(s)) return false;
        if (CastleAmmCore.TryComputeSellGoldProceeds(s, troops, out long recv))
        {
            totalGold = Math.Max(0L, recv);
            unitFaceGold = troops > 0 ? Math.Max(0L, totalGold / troops) : 0L;
            return true;
        }

        if (s.currentAiGarrison <= 0)
        {
            float face = EvaluateBasePriceForCastle(castleId);
            totalGold = Math.Max(0L, (long)Math.Floor(face * troops));
            unitFaceGold = troops > 0 ? Math.Max(0L, totalGold / troops) : 0L;
            return true;
        }

        return false;
    }

    public void RecallUserCastleDeployment(string castleId, int troopsToRecall)
    {
        if (!IsStateReady || string.IsNullOrWhiteSpace(castleId) || troopsToRecall <= 0) return;
        castleId = castleId.Trim();
        if (!castleStateDataMap.TryGetValue(castleId, out var s) || s == null) return;
        castleMasterDataMap.TryGetValue(castleId, out var master);
        EnsureCastleAmmForState(s, master);
        if (!CastleAmmCore.IsInitialized(s)) return;

        int have = s.userDeployedTroops;
        if (have <= 0) return;
        int recall = Mathf.Min(troopsToRecall, have);
        if (recall <= 0) return;

        long payoutGold;
        if (!CastleAmmCore.TryComputeSellGoldProceeds(s, recall, out payoutGold))
        {
            if (s.currentAiGarrison > 0)
                return;
            float face = EvaluateBasePriceForCastle(castleId);
            payoutGold = Math.Max(0L, (long)Math.Floor(face * recall));
        }

        CastleAmmCore.ApplySellFill(s, recall);
        s.userDeployedTroops = have - recall;
        if (s.userDeployedTroops <= 0)
        {
            s.userDeployedTroops = 0;
            s.averagePurchasePrice = 0f;
        }

        var gm = GameManager.InstanceOrNull;
        if (payoutGold > 0L && gm != null)
            gm.AddGold(payoutGold);

        _stateDirty = true;
        s.currentBuyPrice = CalculateCastleQuote(s);
        FlushLiveScriptableObjects();
        RefreshGlobalTopBarIfPossible();
        OnStateTicked?.Invoke();
    }

    /// <summary>n명 매수 시 본금·관부·합계(AMM 일괄식).</summary>
    public bool TryComputeDeployGoldBreakdown(string castleId, int troops, out long principalGold, out long taxGold, out long totalGold)
    {
        principalGold = taxGold = totalGold = 0;
        if (!IsStateReady || string.IsNullOrWhiteSpace(castleId) || troops <= 0) return false;
        castleId = castleId.Trim();
        if (!castleStateDataMap.TryGetValue(castleId, out var s) || s == null) return false;
        castleMasterDataMap.TryGetValue(castleId, out var master);
        EnsureCastleAmmForState(s, master);
        if (!CastleAmmCore.IsInitialized(s)) return false;
        if (!CastleAmmCore.TryComputeBuyGoldPrincipal(s, troops, out principalGold)) return false;
        double rate = Mathf.Clamp(s.castleTaxRatePercent, 0f, 500f) / 100.0;
        taxGold = (long)Math.Round(principalGold * rate);
        totalGold = principalGold + taxGold;
        return true;
    }

    float CalculateCastleQuote(CastleStateData s)
    {
        if (s == null || string.IsNullOrWhiteSpace(s.id)) return 0f;
        EnsureCastleAmmForState(s, castleMasterDataMap.TryGetValue(s.id.Trim(), out var m) ? m : null);
        if (CastleAmmCore.IsInitialized(s))
        {
            if (s.currentAiGarrison > 0)
                return Mathf.Max(0f, CastleAmmCore.GetMarginalBuyOneGoldAsFloat(s));
            return Mathf.Max(0f, CalculateBasePrice(s));
        }

        float basePrice = CalculateBasePrice(s);
        float buffMul = 1f + GetGovernorQuoteModifier(s.currentGovernorId);
        return Mathf.Max(0f, basePrice * buffMul);
    }

    /// <summary>천하 탭: 해당 성 유저 주둔을 모두 매도(AI 환원).</summary>
    public void RecallUserCastleDeployment(string castleId) =>
        RecallUserCastleDeployment(castleId, int.MaxValue);
}
