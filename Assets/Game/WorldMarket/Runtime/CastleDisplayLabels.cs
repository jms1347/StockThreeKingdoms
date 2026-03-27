using System.Text.RegularExpressions;

/// <summary>
/// 천하·지도 UI용: 마스터의 name/regionId가 R01·C01 같은 코드일 때 지역 마스터 등으로 사람이 읽을 이름을 고릅니다.
/// </summary>
public static class CastleDisplayLabels
{
    static readonly Regex RxRegionCode = new Regex(@"^R\d{2}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex RxCastleId = new Regex(@"^C\d{2}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool LooksLikeRegionOrCastleCode(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        string t = s.Trim();
        return RxRegionCode.IsMatch(t) || RxCastleId.IsMatch(t);
    }

    /// <param name="linkedByCastleId">Region SO에서 성 ID로 묶인 지역.</param>
    /// <param name="linkedByRegionIdField">master.regionId가 Rxx일 때 해당 RegionMasterData.</param>
    public static string GetCastleTitle(CastleMasterData master, RegionMasterData linkedByCastleId, RegionMasterData linkedByRegionIdField)
    {
        if (master == null) return "";

        string n = (master.name ?? "").Trim();
        string ridField = (master.regionId ?? "").Trim();

        if (!string.IsNullOrEmpty(n) && !LooksLikeRegionOrCastleCode(n))
            return n;
        if (!string.IsNullOrEmpty(ridField) && !LooksLikeRegionOrCastleCode(ridField))
            return ridField;

        if (linkedByCastleId != null && !string.IsNullOrWhiteSpace(linkedByCastleId.sectorName))
            return ShortenSectorName(linkedByCastleId.sectorName.Trim());

        if (RxRegionCode.IsMatch(ridField) && linkedByRegionIdField != null && !string.IsNullOrWhiteSpace(linkedByRegionIdField.sectorName))
            return ShortenSectorName(linkedByRegionIdField.sectorName.Trim());

        return "성";
    }

    /// <summary>부제: 지역(섹터)명. 제목과 동일하면 빈 문자열.</summary>
    public static string GetRegionSubtitle(CastleMasterData master, RegionMasterData linkedByCastleId, RegionMasterData linkedByRegionIdField, string castleTitle)
    {
        string sector = "";
        if (linkedByCastleId != null && !string.IsNullOrWhiteSpace(linkedByCastleId.sectorName))
            sector = ShortenSectorName(linkedByCastleId.sectorName.Trim());
        else if (master != null)
        {
            string ridField = (master.regionId ?? "").Trim();
            if (RxRegionCode.IsMatch(ridField) && linkedByRegionIdField != null && !string.IsNullOrWhiteSpace(linkedByRegionIdField.sectorName))
                sector = ShortenSectorName(linkedByRegionIdField.sectorName.Trim());
        }

        if (string.IsNullOrEmpty(sector)) return "";
        if (string.Equals(sector, castleTitle, System.StringComparison.Ordinal)) return "";
        return sector;
    }

    public static string ShortenSectorName(string sectorName)
    {
        if (string.IsNullOrWhiteSpace(sectorName)) return "";
        int i = sectorName.IndexOf('(');
        if (i > 0)
            return sectorName.Substring(0, i).TrimEnd();
        return sectorName.Trim();
    }
}
