using UnityEngine;

/// <summary>월드맵 일일 징병 — 인구·성 가치(가치금)·태수·동성 장수 수에 따른 병력·금전 소모 및 징발 부담(민심·인구).</summary>
public static class WorldMapRecruitCalculator
{
    public readonly struct RecruitLedger
    {
        public readonly int Recruit;
        public readonly int ValueCost;
        public readonly int PopulationLoss;
        public readonly int SentimentLoss;

        public RecruitLedger(int recruit, int valueCost, int populationLoss, int sentimentLoss)
        {
            Recruit = recruit;
            ValueCost = valueCost;
            PopulationLoss = populationLoss;
            SentimentLoss = sentimentLoss;
        }
    }

    /// <summary>징병 1회분(병력, 가치금 소모, 백성 감소, 민심 감소)을 동일 공식 체계로 계산합니다.</summary>
    public static RecruitLedger ComputeRecruitLedger(Castle castle, GeneralMasterData governorOrNull)
    {
        if (castle == null)
            return new RecruitLedger(0, 0, 0, 0);

        int pop = Mathf.Max(1, castle.Population);
        int val = Mathf.Max(1, castle.CastleValue);
        var dm = DataManager.InstanceOrNull;
        if (dm?.castleStateDataMap != null &&
            !string.IsNullOrWhiteSpace(castle.MasterId) &&
            dm.castleStateDataMap.TryGetValue(castle.MasterId.Trim(), out var st) &&
            st != null)
            pop = Mathf.Max(pop, st.currentPopulation);

        float popFactor = Mathf.Sqrt(pop / 6500f);
        float valFactor = Mathf.Sqrt(val / 3200f);
        int statAvg = 55;
        if (governorOrNull != null)
            statAvg = Mathf.Clamp((governorOrNull.power + governorOrNull.intel + governorOrNull.charm) / 3, 20, 120);

        float statFactor = 0.62f + statAvg / 130f;
        int others = Mathf.Max(0, WorldMapGeneralRoster.CountGeneralsStationedAt(castle.MasterId) - 1);
        float companion = 1f + others * 0.045f;

        int recruit = Mathf.RoundToInt(34f * popFactor * valFactor * statFactor * companion);
        recruit = Mathf.Clamp(recruit, 14, 240);

        int maxByPopulation = Mathf.Max(0, Mathf.FloorToInt((pop - 80f) / 0.92f));
        if (maxByPopulation > 0)
            recruit = Mathf.Min(recruit, maxByPopulation);
        else
            recruit = 0;

        if (recruit < 1)
            return new RecruitLedger(0, 0, 0, 0);

        int cost = Mathf.RoundToInt(95f * (recruit / 42f) * (1.15f - (statFactor - 0.62f) * 0.35f));
        cost = Mathf.Clamp(cost, 60, 520);

        int popLoss = Mathf.Max(1, Mathf.RoundToInt(recruit * 0.92f));
        popLoss = Mathf.Min(popLoss, Mathf.Max(0, pop - 40));

        float p = Mathf.Max(200f, pop);
        float intensity = recruit / p;
        int sentimentLoss = Mathf.Clamp(2 + Mathf.RoundToInt(intensity * 95f + recruit / 32f), 2, 26);

        return new RecruitLedger(recruit, cost, popLoss, sentimentLoss);
    }
}
