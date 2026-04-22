using UnityEngine;

/// <summary>월드맵용 성 표시 이름. <see cref="CastleDisplayLabels"/> 규칙과 맞춥니다.</summary>
public static class CastleMapDisplayName
{
    public static string FromMaster(CastleMasterData m)
    {
        if (m == null) return string.Empty;
        var n = (m.name ?? string.Empty).Trim();
        var r = (m.regionId ?? string.Empty).Trim();

        if (!string.IsNullOrEmpty(n) && !CastleDisplayLabels.LooksLikeRegionOrCastleCode(n))
            return n;
        if (!string.IsNullOrEmpty(r) && !CastleDisplayLabels.LooksLikeRegionOrCastleCode(r))
            return r;
        if (!string.IsNullOrEmpty(r))
            return r;
        if (!string.IsNullOrEmpty(n))
            return n;
        return string.IsNullOrEmpty(m.id) ? string.Empty : m.id.Trim();
    }
}
