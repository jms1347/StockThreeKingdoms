using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
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

    static DataManager _expandCacheDm;
    static int _expandCacheCastleCount;
    static List<(Regex rx, string rep)> _expandRegexes;

    static void EnsureCastleExpandRegexCache(DataManager dm)
    {
        int n = dm?.castleStateDataMap?.Count ?? 0;
        if (_expandRegexes != null && ReferenceEquals(_expandCacheDm, dm) && _expandCacheCastleCount == n)
            return;

        _expandCacheDm = dm;
        _expandCacheCastleCount = n;
        _expandRegexes = new List<(Regex, string)>();
        if (dm?.castleStateDataMap == null) return;

        var ids = new List<string>();
        foreach (var k in dm.castleStateDataMap.Keys)
        {
            string t = k?.Trim();
            if (!string.IsNullOrEmpty(t)) ids.Add(t);
        }

        ids.Sort((a, b) => b.Length.CompareTo(a.Length));
        const RegexOptions RxOpt = RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled;
        foreach (var id in ids)
        {
            string disp = dm.GetCastleDisplayName(id);
            if (string.IsNullOrWhiteSpace(disp) || string.Equals(disp.Trim(), id, StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                var rx = new Regex(@"\b" + Regex.Escape(id) + @"\b", RxOpt);
                _expandRegexes.Add((rx, disp.Trim()));
            }
            catch (ArgumentException)
            {
                // 잘못된 패턴 — 건너뜀
            }
        }
    }

    /// <summary>기사 UI에 표시할 때 성 ID(C04 등)를 마스터 표기 이름으로 치환합니다(단어 경계).</summary>
    public static string ExpandKnownCastleIdsInText(DataManager dm, string text)
    {
        if (dm == null || string.IsNullOrEmpty(text)) return text;

        EnsureCastleExpandRegexCache(dm);
        if (_expandRegexes == null || _expandRegexes.Count == 0) return text;

        string s = text;
        for (int i = 0; i < _expandRegexes.Count; i++)
        {
            var pair = _expandRegexes[i];
            s = pair.rx.Replace(s, pair.rep);
        }

        return s;
    }

    static Regex _evCodeRx;

    /// <summary>제목·본문에 남은 <c>EV14</c> 형태 이벤트 코드를 마스터 표시 이름으로 치환합니다.</summary>
    public static string ExpandEventCodesInText(DataManager dm, string text)
    {
        if (dm == null || string.IsNullOrEmpty(text)) return text;
        if (_evCodeRx == null)
            _evCodeRx = new Regex(@"\b(EV[0-9A-Z]+)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);
        return _evCodeRx.Replace(text, m =>
        {
            string eid = m.Groups[1].Value;
            var ev = dm.GetEventMasterData(eid);
            if (ev != null && !string.IsNullOrWhiteSpace(ev.name))
                return ev.name.Trim();
            return m.Value;
        });
    }

    /// <summary>뉴스 한 줄 표시용: 성 ID 치환 후 이벤트 코드 치환.</summary>
    public static string ApplyNewsDisplayTextExpansions(DataManager dm, string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return ExpandEventCodesInText(dm, ExpandKnownCastleIdsInText(dm, text));
    }
}
