using System;
using UnityEngine;

/// <summary>
/// 본영 징집/해산의 가격·영향 시뮬레이션 계산 전용 컨트롤러.
/// </summary>
public static class RecruitController
{
    public const float MinSentimentCoeff = 0.8f;
    public const float MaxSentimentCoeff = 1.5f;
    public const float SentimentDropWeight = 42f;
    public const float PriceDropWeight = 0.28f;

    public readonly struct RecruitQuote
    {
        public readonly string CastleId;
        public readonly float BasePrice;
        public readonly float SentimentCoeff;
        public readonly float ScarcityCoeff;
        public readonly float UnitPrice;
        public readonly int Population;
        public readonly float Sentiment;
        public readonly int TotalCastleSoldiers;
        public readonly int MaxByPopulation;
        public readonly int MaxByCapacity;
        public readonly int MaxByGold;

        public int MaxRecruitable => Mathf.Max(0, Mathf.Min(MaxByPopulation, Mathf.Min(MaxByCapacity, MaxByGold)));

        public RecruitQuote(
            string castleId,
            float basePrice,
            float sentimentCoeff,
            float scarcityCoeff,
            float unitPrice,
            int population,
            float sentiment,
            int totalCastleSoldiers,
            int maxByPopulation,
            int maxByCapacity,
            int maxByGold)
        {
            CastleId = castleId;
            BasePrice = basePrice;
            SentimentCoeff = sentimentCoeff;
            ScarcityCoeff = scarcityCoeff;
            UnitPrice = unitPrice;
            Population = population;
            Sentiment = sentiment;
            TotalCastleSoldiers = totalCastleSoldiers;
            MaxByPopulation = maxByPopulation;
            MaxByCapacity = maxByCapacity;
            MaxByGold = maxByGold;
        }
    }

    public readonly struct RecruitImpactPreview
    {
        public readonly int RecruitCount;
        public readonly int PostPopulation;
        public readonly float SentimentDrop;
        public readonly float PriceDropPercent;
        public readonly float PostSentiment;
        public readonly float PostPrice;

        public RecruitImpactPreview(
            int recruitCount,
            int postPopulation,
            float sentimentDrop,
            float priceDropPercent,
            float postSentiment,
            float postPrice)
        {
            RecruitCount = recruitCount;
            PostPopulation = postPopulation;
            SentimentDrop = sentimentDrop;
            PriceDropPercent = priceDropPercent;
            PostSentiment = postSentiment;
            PostPrice = postPrice;
        }
    }

    public static bool TryBuildQuote(string castleId, out RecruitQuote quote)
    {
        quote = default;
        var dm = DataManager.InstanceOrNull;
        var gm = GameManager.InstanceOrNull;
        if (dm == null || !dm.IsStateReady || gm?.currentUser == null || string.IsNullOrWhiteSpace(castleId))
            return false;
        castleId = castleId.Trim();
        if (!dm.castleStateDataMap.TryGetValue(castleId, out var s) || s == null)
            return false;

        float basePrice = Mathf.Max(0.01f, s.currentBuyPrice);
        float sentimentCoeff = ComputeSentimentCoeff(s.currentSentiment);
        int totalSoldiers = Mathf.Max(0, dm.EstimateCastleTotalGarrisonTroops(castleId));
        int population = Mathf.Max(0, s.currentPopulation);
        float scarcityCoeff = ComputeScarcityCoeff(totalSoldiers, population);
        float unitPrice = Mathf.Max(1f, basePrice * sentimentCoeff * scarcityCoeff);

        int maxByPopulation = Mathf.Max(0, population);
        // 기획 변경: 수용량 기반 모집 제한 해제(과부하는 배당 효율 페널티로 처리).
        int maxByCapacity = int.MaxValue;
        int maxByGold = Mathf.Max(0, (int)Math.Floor(gm.currentGold / unitPrice));

        quote = new RecruitQuote(
            castleId, basePrice, sentimentCoeff, scarcityCoeff, unitPrice,
            population, s.currentSentiment, totalSoldiers,
            maxByPopulation, maxByCapacity, maxByGold);
        return true;
    }

    /// <summary>본영 UI: 징집 단가를 현재 주가(<see cref="CastleStateData.currentBuyPrice"/>)만 사용.</summary>
    public static bool TryBuildStockPriceQuote(string castleId, out RecruitQuote quote)
    {
        quote = default;
        var dm = DataManager.InstanceOrNull;
        var gm = GameManager.InstanceOrNull;
        if (dm == null || !dm.IsStateReady || gm?.currentUser == null || string.IsNullOrWhiteSpace(castleId))
            return false;
        castleId = castleId.Trim();
        if (!dm.castleStateDataMap.TryGetValue(castleId, out var s) || s == null)
            return false;

        float stock = Mathf.Max(0.01f, s.currentBuyPrice);
        int totalSoldiers = Mathf.Max(0, dm.EstimateCastleTotalGarrisonTroops(castleId));
        int population = Mathf.Max(0, s.currentPopulation);
        int maxByPopulation = Mathf.Max(0, population);
        // 기획 변경: 수용량 기반 모집 제한 해제(과부하는 배당 효율 페널티로 처리).
        int maxByCapacity = int.MaxValue;
        int maxByGold = Mathf.Max(0, (int)Math.Floor(gm.currentGold / stock));

        quote = new RecruitQuote(
            castleId, stock, 1f, 1f, stock,
            population, s.currentSentiment, totalSoldiers,
            maxByPopulation, maxByCapacity, maxByGold);
        return true;
    }

    public static RecruitImpactPreview BuildImpactPreview(string castleId, int recruitCount)
    {
        var dm = DataManager.InstanceOrNull;
        if (dm == null || !dm.IsStateReady || string.IsNullOrWhiteSpace(castleId) ||
            !dm.castleStateDataMap.TryGetValue(castleId.Trim(), out var s) || s == null)
            return default;

        recruitCount = Mathf.Max(0, recruitCount);
        int maxPopRef = ResolveMaxPopulationReference(dm, s);
        float sentimentDrop = ComputeSentimentDrop(recruitCount, maxPopRef);
        float priceDropPercent = ComputePriceDropPercent(recruitCount, maxPopRef);
        int postPop = Mathf.Max(0, s.currentPopulation - recruitCount);
        float postSent = Mathf.Clamp(s.currentSentiment - sentimentDrop, 0f, 200f);
        float postPrice = Mathf.Max(0.01f, s.currentBuyPrice * (1f - priceDropPercent));

        return new RecruitImpactPreview(recruitCount, postPop, sentimentDrop, priceDropPercent, postSent, postPrice);
    }

    public static float ComputeSentimentCoeff(float currentSentiment)
    {
        float c = MaxSentimentCoeff - (currentSentiment / 200f);
        return Mathf.Clamp(c, MinSentimentCoeff, MaxSentimentCoeff);
    }

    public static float ComputeScarcityCoeff(int totalSoldiers, int population)
    {
        float pop = Mathf.Max(1f, population);
        return 1f + Mathf.Max(0f, totalSoldiers) / pop;
    }

    public static float ComputeSentimentDrop(int recruitCount, int maxPopulationRef)
    {
        if (recruitCount <= 0) return 0f;
        float maxPop = Mathf.Max(1f, maxPopulationRef);
        return Mathf.Clamp((recruitCount / maxPop) * SentimentDropWeight, 0f, 90f);
    }

    public static float ComputePriceDropPercent(int recruitCount, int maxPopulationRef)
    {
        if (recruitCount <= 0) return 0f;
        float maxPop = Mathf.Max(1f, maxPopulationRef);
        return Mathf.Clamp01((recruitCount / maxPop) * PriceDropWeight);
    }

    static int ResolveMaxPopulationReference(DataManager dm, CastleStateData s)
    {
        int cur = Mathf.Max(1, s.currentPopulation);
        if (dm != null && dm.castleMasterDataMap != null && dm.castleMasterDataMap.TryGetValue(s.id, out var m) && m != null)
            return Mathf.Max(cur, Mathf.Max(1, m.initPopulation));
        return cur;
    }
}
