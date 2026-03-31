using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// 뉴스 템플릿 플레이스홀더를 런타임 데이터로 치환합니다.
/// 키: <c>{Castle}</c>, <c>{CastleId}</c>, <c>{Region}</c>, <c>{Governor}</c>, <c>{Item}</c>, <c>{Value}</c>
/// </summary>
public static class NewsFormatter
{
    /// <summary>기사 UI용 최종 문자열. <paramref name="buff"/>가 없으면 <c>{Item}</c>/<c>{Value}</c>는 빈 문자열로 둡니다.</summary>
    public static string FormatNews(string template, string castleId, DataManager dm, BuffMasterData buff = null)
    {
        if (string.IsNullOrEmpty(template)) return template ?? "";

        string s = ApplyCastleGovernorRegion(template, castleId, dm);

        if (buff != null)
        {
            s = s.Replace("{Item}", GetCastleStatTypeDisplayKo(buff.statType));
            s = s.Replace("{Value}", FormatBuffValueForNews(buff));
        }
        else
        {
            s = s.Replace("{Item}", "");
            s = s.Replace("{Value}", "");
        }

        return s;
    }

    static string ApplyCastleGovernorRegion(string raw, string castleId, DataManager dm)
    {
        string cid = (castleId ?? "").Trim();
        string castleDisp = dm != null && !string.IsNullOrEmpty(cid) ? dm.GetCastleDisplayName(cid) : cid;
        if (string.IsNullOrWhiteSpace(castleDisp)) castleDisp = cid;

        string region = "";
        if (dm != null && !string.IsNullOrEmpty(cid) && dm.castleMasterDataMap.TryGetValue(cid, out var cm) && cm != null)
        {
            if (dm.TryGetRegionByCastleId(cid, out var rm) && rm != null && !string.IsNullOrWhiteSpace(rm.sectorName))
                region = rm.sectorName.Trim();
            else if (!string.IsNullOrWhiteSpace(cm.regionId))
                region = cm.regionId.Trim();
        }

        string governor = "";
        if (dm != null && !string.IsNullOrEmpty(cid) && dm.castleStateDataMap.TryGetValue(cid, out var st) && st != null
            && !string.IsNullOrWhiteSpace(st.currentGovernorId))
        {
            var g = dm.GetGeneralMasterData(st.currentGovernorId);
            if (g != null && !string.IsNullOrWhiteSpace(g.name))
                governor = g.name.Trim();
            else
                governor = st.currentGovernorId.Trim();
        }

        return raw
            .Replace("{Castle}", castleDisp)
            .Replace("{CastleId}", cid)
            .Replace("{Region}", region)
            .Replace("{Governor}", governor);
    }

    /// <summary><see cref="CastleStatType"/>을 뉴스용 짧은 한글 라벨로.</summary>
    public static string GetCastleStatTypeDisplayKo(CastleStatType t)
    {
        switch (t)
        {
            case CastleStatType.CastleValue: return "성 가치(시세)";
            case CastleStatType.PriceValue: return "액면가";
            case CastleStatType.SentimentRecovery: return "민심";
            case CastleStatType.PopulationGrowth: return "인구";
            case CastleStatType.WarAttackLossReduction: return "공격 전투 손실";
            case CastleStatType.WarDefenseLossReduction: return "방어 전투 손실";
            case CastleStatType.DividendBonus: return "배당(정액)";
            case CastleStatType.DividendMultiplier: return "배당(배율)";
            case CastleStatType.TradeLock: return "거래";
            default: return t.ToString();
        }
    }

    /// <summary>버프 수치를 기사 문구용으로 요약 (%, 정수 등).</summary>
    public static string FormatBuffValueForNews(BuffMasterData b)
    {
        if (b == null) return "";

        switch (b.statType)
        {
            case CastleStatType.CastleValue:
            case CastleStatType.PopulationGrowth:
                return $"{b.value * 100f:0.#}%";
            case CastleStatType.DividendMultiplier:
                if (b.value >= 1f)
                    return $"+{(b.value - 1f) * 100f:0.#}%";
                return $"{b.value * 100f:0.#}%";
            case CastleStatType.TradeLock:
                return b.value >= 0.5f ? "거래 정지" : "정상";
            case CastleStatType.PriceValue:
            case CastleStatType.SentimentRecovery:
            case CastleStatType.DividendBonus:
            case CastleStatType.WarAttackLossReduction:
            case CastleStatType.WarDefenseLossReduction:
                return b.value.ToString("0.##", CultureInfo.InvariantCulture);
            default:
                return b.value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>이벤트 행의 <see cref="EventMasterData.buffCodes"/> 중 첫 유효 버프.</summary>
    public static BuffMasterData TryGetFirstBuffForEvent(DataManager dm, EventMasterData ev)
    {
        if (dm == null || ev?.buffCodes == null) return null;
        for (int i = 0; i < ev.buffCodes.Count; i++)
        {
            string c = ev.buffCodes[i];
            if (string.IsNullOrWhiteSpace(c)) continue;
            var b = dm.GetBuffMasterData(c.Trim());
            if (b != null) return b;
        }

        return null;
    }

    /// <summary>버프 코드 목록에서 첫 유효 <see cref="BuffMasterData"/>.</summary>
    public static BuffMasterData TryGetFirstBuffFromCodes(DataManager dm, System.Collections.Generic.IList<string> codes)
    {
        if (dm == null || codes == null) return null;
        for (int i = 0; i < codes.Count; i++)
        {
            string c = codes[i];
            if (string.IsNullOrWhiteSpace(c)) continue;
            var b = dm.GetBuffMasterData(c.Trim());
            if (b != null) return b;
        }

        return null;
    }
}
