using System;
using System.Collections.Generic;
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
        int room = Mathf.Max(1, mCap - u);

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
            bonus = gm.currentUser.marketLevel * 25 + gm.currentUser.farmLevel * 15; // farmLevel = 병참

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

    static int MaxAffordableAmmBuy(CastleStateData s, double gold, float taxPercent)
    {
        if (!CastleAmmCore.IsInitialized(s) || gold < 0d) return 0;
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
            if ((double)total <= gold)
            {
                best = mid;
                lo = mid + 1;
            }
            else hi = mid - 1;
        }

        return best;
    }

    /// <summary>주어진 금화 예산(본금+관부 합산 한도)으로 살 수 있는 최대 병력 수.</summary>
    public int ComputeMaxAffordableTroopsForGoldBudget(string castleId, double goldBudget)
    {
        if (!IsStateReady || string.IsNullOrWhiteSpace(castleId) || goldBudget <= 0d)
            return 0;
        castleId = castleId.Trim();
        if (!castleStateDataMap.TryGetValue(castleId, out var s) || s == null)
            return 0;
        castleMasterDataMap.TryGetValue(castleId, out var master);
        EnsureCastleAmmForState(s, master);
        if (!CastleAmmCore.IsInitialized(s))
            return 0;
        int maxByAi = Mathf.Max(0, s.currentAiGarrison);
        if (maxByAi <= 0)
            return 0;
        float taxP = s.castleTaxRatePercent;
        int byGold = MaxAffordableAmmBuy(s, goldBudget, taxP);
        return Mathf.Min(maxByAi, byGold);
    }

    /// <summary>현재 보유 금화의 일정 비율만큼으로 즉시 AMM 매수(팝업 없음).</summary>
    public bool TryQuickBuyWithFractionOfGold(string castleId, double goldFraction)
    {
        if (goldFraction <= 0d)
            return false;
        if (goldFraction > 1d)
            goldFraction = 1d;
        var gm = GameManager.InstanceOrNull;
        if (gm == null)
            return false;
        double budget = gm.currentGold * goldFraction;
        if (budget <= 0d)
            return false;
        int n = ComputeMaxAffordableTroopsForGoldBudget(castleId, budget);
        if (n <= 0)
            return false;
        AddUserCastleDeployment(castleId, n, 0f);
        return true;
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
        double gold = gm?.currentGold ?? 0d;
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

    float CalculateCastleQuoteWithoutSpeculation(CastleStateData s)
    {
        if (s == null || string.IsNullOrWhiteSpace(s.id)) return 0f;
        EnsureCastleAmmForState(s, castleMasterDataMap.TryGetValue(s.id.Trim(), out var m) ? m : null);
        if (CastleAmmCore.IsInitialized(s))
        {
            if (s.currentAiGarrison > 0)
                return ApplyWarVolatilityMultiplier(s, Mathf.Max(0f, CastleAmmCore.GetMarginalBuyOneGoldAsFloat(s)));
            return ApplyWarVolatilityMultiplier(s, Mathf.Max(0f, CalculateBasePrice(s)));
        }

        float basePrice = CalculateBasePrice(s);
        float buffMul = 1f + GetGovernorQuoteModifier(s.currentGovernorId);
        return ApplyWarVolatilityMultiplier(s, Mathf.Max(0f, basePrice * buffMul));
    }

    float CalculateCastleQuote(CastleStateData s)
    {
        float quote = CalculateCastleQuoteWithoutSpeculation(s);
        if (s == null || string.IsNullOrWhiteSpace(s.id))
            return quote;
        float trans = GetCastleGradeTransitionFactor(s.id.Trim());
        float mul = Mathf.Clamp(1f + trans, 0.2f, 2.5f);
        return Mathf.Max(0f, quote * mul);
    }

    float ApplyWarVolatilityMultiplier(CastleStateData s, float quote)
    {
        if (s == null) return Mathf.Max(0f, quote);
        if (!s.isWar) return Mathf.Max(0f, quote);
        return Mathf.Max(0f, quote * Mathf.Max(1f, worldWarVolatilityMultiplier));
    }

    /// <summary>천하 탭: 해당 성 유저 주둔을 모두 매도(AI 환원).</summary>
    public void RecallUserCastleDeployment(string castleId) =>
        RecallUserCastleDeployment(castleId, int.MaxValue);

    /// <summary>
    /// 본영 징집(직접 모집): 금화를 지불하고 병사를 늘리며 인구·민심·주가를 즉시 반영합니다.
    /// </summary>
    public bool TryRecruitHomeSoldiers(string castleId, int recruitCount, out long totalGoldCost, out string reason)
    {
        totalGoldCost = 0;
        reason = "";
        if (!IsStateReady || string.IsNullOrWhiteSpace(castleId) || recruitCount <= 0)
        {
            reason = "입력값이 유효하지 않습니다.";
            return false;
        }

        if (!RecruitController.TryBuildQuote(castleId, out var q))
        {
            reason = "성 데이터를 불러오지 못했습니다.";
            return false;
        }

        recruitCount = Mathf.Min(recruitCount, q.MaxRecruitable);
        if (recruitCount <= 0)
        {
            reason = q.MaxByPopulation <= 0 ? "징집할 수 있는 백성이 부족합니다." :
                q.MaxByCapacity <= 0 ? "수용 가능한 병력 한도를 초과했습니다." :
                "금화가 부족합니다.";
            return false;
        }

        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null)
        {
            reason = "유저 데이터가 준비되지 않았습니다.";
            return false;
        }

        totalGoldCost = (long)Math.Ceiling(q.UnitPrice * recruitCount);
        if (!gm.UseGold(totalGoldCost))
        {
            reason = "금화가 부족합니다.";
            return false;
        }

        castleId = castleId.Trim();
        var s = castleStateDataMap[castleId];
        s.userDeployedTroops = Mathf.Max(0, s.userDeployedTroops + recruitCount);
        s.currentPopulation = Mathf.Max(0, s.currentPopulation - recruitCount);

        int popRef = Mathf.Max(1, s.currentPopulation + recruitCount);
        float sentimentDrop = RecruitController.ComputeSentimentDrop(recruitCount, popRef);
        float priceDropPct = RecruitController.ComputePriceDropPercent(recruitCount, popRef);
        s.currentSentiment = Mathf.Clamp(s.currentSentiment - sentimentDrop, 0f, 200f);
        s.currentBuyPrice = Mathf.Max(0.01f, s.currentBuyPrice * (1f - priceDropPct));

        _stateDirty = true;
        FlushLiveScriptableObjects();
        RefreshGlobalTopBarIfPossible();
        OnStateTicked?.Invoke();
        return true;
    }

    /// <summary>
    /// 본영 징집 — UI 단가를 <b>현재 주가</b>(<see cref="CastleStateData.currentBuyPrice"/>)만 사용. 인구·정원 한도는 동일 적용.
    /// </summary>
    public bool TryRecruitHomeSoldiersAtStockPrice(string castleId, int recruitCount, out long totalGoldCost, out string reason)
    {
        totalGoldCost = 0;
        reason = "";
        if (!IsStateReady || string.IsNullOrWhiteSpace(castleId) || recruitCount <= 0)
        {
            reason = "입력값이 유효하지 않습니다.";
            return false;
        }

        if (!RecruitController.TryBuildStockPriceQuote(castleId, out var q))
        {
            reason = "성 데이터를 불러오지 못했습니다.";
            return false;
        }

        recruitCount = Mathf.Min(recruitCount, q.MaxRecruitable);
        if (recruitCount <= 0)
        {
            reason = q.MaxByPopulation <= 0 ? "징집할 수 있는 백성이 부족합니다." :
                q.MaxByCapacity <= 0 ? "수용 가능한 병력 한도를 초과했습니다." :
                "금화가 부족합니다.";
            return false;
        }

        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null)
        {
            reason = "유저 데이터가 준비되지 않았습니다.";
            return false;
        }

        totalGoldCost = (long)Math.Ceiling(q.UnitPrice * recruitCount);
        if (!gm.UseGold(totalGoldCost))
        {
            reason = "금화가 부족합니다.";
            return false;
        }

        castleId = castleId.Trim();
        var s = castleStateDataMap[castleId];
        s.userDeployedTroops = Mathf.Max(0, s.userDeployedTroops + recruitCount);
        s.currentPopulation = Mathf.Max(0, s.currentPopulation - recruitCount);

        int popRef = Mathf.Max(1, s.currentPopulation + recruitCount);
        float sentimentDrop = RecruitController.ComputeSentimentDrop(recruitCount, popRef);
        float priceDropPct = RecruitController.ComputePriceDropPercent(recruitCount, popRef);
        s.currentSentiment = Mathf.Clamp(s.currentSentiment - sentimentDrop, 0f, 200f);
        s.currentBuyPrice = Mathf.Max(0.01f, s.currentBuyPrice * (1f - priceDropPct));

        _stateDirty = true;
        FlushLiveScriptableObjects();
        RefreshGlobalTopBarIfPossible();
        OnStateTicked?.Invoke();
        return true;
    }

    /// <summary>
    /// 본영 해산(매도): 보유 병사를 줄이고 금화를 환급합니다.
    /// </summary>
    public bool TryDischargeHomeSoldiers(string castleId, int dischargeCount, out long gainedGold, out string reason)
    {
        gainedGold = 0;
        reason = "";
        if (!IsStateReady || string.IsNullOrWhiteSpace(castleId) || dischargeCount <= 0)
        {
            reason = "입력값이 유효하지 않습니다.";
            return false;
        }

        castleId = castleId.Trim();
        if (!castleStateDataMap.TryGetValue(castleId, out var s) || s == null)
        {
            reason = "성 데이터를 찾을 수 없습니다.";
            return false;
        }

        int have = Mathf.Max(0, s.userDeployedTroops);
        if (have <= 0)
        {
            reason = "해산할 병사가 없습니다.";
            return false;
        }

        dischargeCount = Mathf.Min(dischargeCount, have);
        if (!RecruitController.TryBuildQuote(castleId, out var q))
        {
            reason = "가격 데이터를 계산할 수 없습니다.";
            return false;
        }

        gainedGold = (long)Math.Floor(q.UnitPrice * dischargeCount);
        var gm = GameManager.InstanceOrNull;
        if (gm == null)
        {
            reason = "유저 데이터가 준비되지 않았습니다.";
            return false;
        }

        s.userDeployedTroops = have - dischargeCount;
        if (gainedGold > 0)
            gm.AddGold(gainedGold);

        _stateDirty = true;
        FlushLiveScriptableObjects();
        RefreshGlobalTopBarIfPossible();
        OnStateTicked?.Invoke();
        return true;
    }

    /// <summary>
    /// 본영 해산 — 단가는 <see cref="CastleStateData.currentBuyPrice"/>만 사용하고, 환급액은 시세의 <b>95%</b>(수수료 5%).
    /// </summary>
    public bool TryDischargeHomeSoldiersAtStockPrice(string castleId, int dischargeCount, out long gainedGold, out string reason)
    {
        gainedGold = 0;
        reason = "";
        if (!IsStateReady || string.IsNullOrWhiteSpace(castleId) || dischargeCount <= 0)
        {
            reason = "입력값이 유효하지 않습니다.";
            return false;
        }

        castleId = castleId.Trim();
        if (!castleStateDataMap.TryGetValue(castleId, out var s) || s == null)
        {
            reason = "성 데이터를 찾을 수 없습니다.";
            return false;
        }

        int have = Mathf.Max(0, s.userDeployedTroops);
        if (have <= 0)
        {
            reason = "해산할 병사가 없습니다.";
            return false;
        }

        dischargeCount = Mathf.Min(dischargeCount, have);
        float unit = Mathf.Max(0.01f, s.currentBuyPrice);
        double gross = unit * dischargeCount;
        gainedGold = (long)Math.Floor(gross * 0.95d);

        var gm = GameManager.InstanceOrNull;
        if (gm == null)
        {
            reason = "유저 데이터가 준비되지 않았습니다.";
            return false;
        }

        s.userDeployedTroops = have - dischargeCount;
        if (gainedGold > 0)
            gm.AddGold(gainedGold);

        _stateDirty = true;
        FlushLiveScriptableObjects();
        RefreshGlobalTopBarIfPossible();
        OnStateTicked?.Invoke();
        return true;
    }

    /// <summary>유지비 미납 탈영: 환급 없이 주둔병만 제거합니다.</summary>
    public int RemoveUserTroopsForUpkeepDesertion(int totalToRemove)
    {
        if (totalToRemove <= 0) return 0;
        var gm = GameManager.InstanceOrNull;
        if (gm?.currentUser == null) return 0;

        if (!IsStateReady || castleStateDataMap == null)
        {
            long have = Math.Max(0L, gm.currentUser.soldierCount);
            int take = (int)Math.Min(have, totalToRemove);
            if (take > 0)
                gm.currentUser.soldierCount = have - take;
            return take;
        }

        var list = new List<(string id, int troops)>();
        foreach (var kv in castleStateDataMap)
        {
            var s = kv.Value;
            if (s == null) continue;
            int t = Mathf.Max(0, s.userDeployedTroops);
            if (t > 0) list.Add((kv.Key, t));
        }

        list.Sort((a, b) => b.troops.CompareTo(a.troops));

        int removed = 0;
        for (int i = 0; i < list.Count && removed < totalToRemove; i++)
        {
            string cid = list[i].id;
            int need = totalToRemove - removed;
            if (need <= 0) break;
            if (!castleStateDataMap.TryGetValue(cid, out var st) || st == null) continue;
            int haveHere = Mathf.Max(0, st.userDeployedTroops);
            int take = Mathf.Min(haveHere, need);
            if (take <= 0) continue;
            ApplyUpkeepDesertionAtCastle(st, cid, take);
            removed += take;
        }

        if (removed > 0)
        {
            _stateDirty = true;
            FlushLiveScriptableObjects();
            RefreshGlobalTopBarIfPossible();
            OnStateTicked?.Invoke();
        }

        return removed;
    }

    void ApplyUpkeepDesertionAtCastle(CastleStateData s, string castleId, int count)
    {
        if (s == null || count <= 0) return;
        castleId = castleId.Trim();
        castleMasterDataMap.TryGetValue(castleId, out var master);
        EnsureCastleAmmForState(s, master);
        int have = Mathf.Max(0, s.userDeployedTroops);
        int take = Mathf.Min(count, have);
        if (take <= 0) return;

        if (CastleAmmCore.IsInitialized(s))
            CastleAmmCore.ApplySellFill(s, take);

        s.userDeployedTroops = have - take;
        if (s.userDeployedTroops <= 0)
        {
            s.userDeployedTroops = 0;
            s.averagePurchasePrice = 0f;
        }

        s.currentBuyPrice = CalculateCastleQuote(s);
    }
}
