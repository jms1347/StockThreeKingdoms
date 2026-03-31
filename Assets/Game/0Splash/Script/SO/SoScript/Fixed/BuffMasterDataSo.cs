using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// 이벤트 <see cref="EventMasterData.buffCodes"/>에서 참조되는 BuffCode 한 행. <see cref="CurveType"/>·<see cref="BuffMasterData.durationDays"/>로
/// 일차 분할 적용(<see cref="WorldEventBuffApplier"/>). 영문: Buff row keyed by BuffCode; spreads across UTC days for volatility.
/// </summary>
public enum CastleStatType
{
    None = 0,
    CastleValue = 1,
    PriceValue = 2,
    SentimentRecovery = 3,
    PopulationGrowth = 4,
    WarAttackLossReduction = 5,
    WarDefenseLossReduction = 6,
    DividendBonus = 7,
    DividendMultiplier = 8,
    TradeLock = 10,
}

/// <summary>Google sheet column C: English or int enum, or exact Korean label (see TryParseKoreanExact).</summary>
public static class CastleStatTypeSheetParser
{
    public static bool TryParse(string raw, out CastleStatType statType)
    {
        statType = CastleStatType.None;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        string t = raw.Trim();

        if (Enum.TryParse(t, true, out statType) && Enum.IsDefined(typeof(CastleStatType), statType))
            return true;

        if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) &&
            Enum.IsDefined(typeof(CastleStatType), n))
        {
            statType = (CastleStatType)n;
            return true;
        }

        return TryParseKoreanExact(t, out statType);
    }

    static bool TryParseKoreanExact(string t, out CastleStatType statType)
    {
        statType = CastleStatType.None;
        if (t == "\uC131 \uAC00\uCE58") { statType = CastleStatType.CastleValue; return true; }
        if (t == "\uC131 \uC561\uBA74\uAC00") { statType = CastleStatType.PriceValue; return true; }
        if (t == "\uBBFC\uC2EC") { statType = CastleStatType.SentimentRecovery; return true; }
        if (t == "\uBC31\uC131\uC218") { statType = CastleStatType.PopulationGrowth; return true; }
        if (t == "\uACF5\uACA9\uC2DC \uBCD1\uC0AC \uC190\uC2E4\uB960") { statType = CastleStatType.WarAttackLossReduction; return true; }
        if (t == "\uC218\uBE44\uC2DC \uBCD1\uC0AC \uC190\uC2E4\uB960") { statType = CastleStatType.WarDefenseLossReduction; return true; }
        if (t == "\uBC30\uB2F9\uAE08(\uD569)") { statType = CastleStatType.DividendBonus; return true; }
        if (t == "\uBC30\uB2F9\uAE08(\uACE1)") { statType = CastleStatType.DividendMultiplier; return true; }
        if (t == "\uAC70\uB798 \uC815\uC9C0") { statType = CastleStatType.TradeLock; return true; }
        return false;
    }
}

/// <summary>
/// Per-day weight shape: Instant/Linear flat; Exponential heavier late; Logarithmic heavier early.
/// </summary>
public enum CurveType
{
    None = 0,
    /// <summary>Spread effect across days; each day random in [0, cap] (default).</summary>
    Instant = 1,
    /// <summary>Flat weights; each day random in [0, cap].</summary>
    Linear = 2,
    /// <summary>Later days get larger share of total (accel).</summary>
    Exponential = 3,
    /// <summary>Early days get larger share, then taper (decel).</summary>
    Logarithmic = 4
}

[System.Serializable]
public class BuffMasterData
{
    public string id;
    public string name;
    /// <summary>Castle stat column to affect.</summary>
    public CastleStatType statType;
    /// <summary>How daily magnitude is weighted over durationDays.</summary>
    public CurveType curveType;
    /// <summary>
    /// Total scale. Each UTC day sample uses cap ~ 2*|value|*normalizedWeight; draw ~ Uniform(0,cap)*sign(value)
    /// so expected cumulative magnitude ~ |value|. Negative value: negative direction from zero.
    /// </summary>
    public float value;
    /// <summary>Number of UTC day buckets. 1 = only the sample at event confirm.</summary>
    public int durationDays = 1;
    public string description;
}

[CreateAssetMenu(fileName = "BuffMasterDataSo", menuName = "ScriptableObject/BuffMasterDataSo")]
public class BuffMasterDataSo : ScriptableObject
{
    public List<BuffMasterData> list = new List<BuffMasterData>();
}
