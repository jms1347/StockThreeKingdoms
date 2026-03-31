using System.Collections.Generic;
using UnityEngine;

/// <summary><see cref="EventMasterData.buffCodes"/>를 <see cref="CastleStateData"/>에 반영합니다.</summary>
public static class WorldEventBuffApplier
{
    public static void ApplyBuffCodesToCastle(DataManager dm, CastleStateData s, IList<string> buffCodes)
    {
        if (dm == null || s == null || buffCodes == null) return;
        for (int i = 0; i < buffCodes.Count; i++)
        {
            string code = buffCodes[i];
            if (string.IsNullOrWhiteSpace(code)) continue;
            var b = dm.GetBuffMasterData(code.Trim());
            if (b == null) continue;
            ApplyOne(s, b);
        }
    }

    /// <summary>월드 이벤트 확정 시 버프 코드를 <see cref="CastleStateData.activeBuffs"/>에 누적합니다.</summary>
    public static void RegisterActiveBuffCodes(CastleStateData s, IList<string> buffCodes, string sourceEventId,
        int appliedUtcDay)
    {
        if (s == null || buffCodes == null) return;
        if (s.activeBuffs == null)
            s.activeBuffs = new List<ActiveBuffEntry>();
        string eid = sourceEventId ?? "";
        for (int i = 0; i < buffCodes.Count; i++)
        {
            string code = buffCodes[i];
            if (string.IsNullOrWhiteSpace(code)) continue;
            s.activeBuffs.Add(new ActiveBuffEntry
            {
                buffCode = code.Trim(),
                sourceEventId = eid,
                appliedUtcDay = appliedUtcDay
            });
        }
    }

    static void ApplyOne(CastleStateData s, BuffMasterData b)
    {
        switch (b.statType)
        {
            case CastleStatType.SentimentRecovery:
                s.currentSentiment = Mathf.Clamp(s.currentSentiment + b.value, 0f, 200f);
                break;
            case CastleStatType.PopulationGrowth:
                s.currentPopulation = Mathf.Max(1, Mathf.RoundToInt(s.currentPopulation * (1f + b.value)));
                break;
            case CastleStatType.CastleValue:
                s.currentBuyPrice = Mathf.Max(0.01f, s.currentBuyPrice * (1f + b.value));
                break;
            case CastleStatType.PriceValue:
                s.currentBuyPrice = Mathf.Max(0.01f, s.currentBuyPrice + b.value);
                break;
            default:
                break;
        }
    }
}
