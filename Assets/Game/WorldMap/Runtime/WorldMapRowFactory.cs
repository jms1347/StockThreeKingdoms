using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>구글 시트 → <see cref="CastleMasterData"/>와 동일 스키마의 SO/런타임 맵을 <see cref="CastleSheetRow"/>로 변환.</summary>
public static class WorldMapRowFactory
{
    public static CastleSheetRow[] FromCastleMasterDictionary(
        Dictionary<string, CastleMasterData> castles,
        Dictionary<string, GeneralMasterData> generals)
    {
        if (castles == null || castles.Count == 0) return Array.Empty<CastleSheetRow>();
        var list = new List<CastleSheetRow>(castles.Count);
        foreach (var kv in castles)
        {
            var m = kv.Value;
            if (m == null || string.IsNullOrWhiteSpace(m.id)) continue;
            var id = m.id.Trim();
            PickBestGovernorForCastle(id, generals, out var govId, out var govName);
            if (string.IsNullOrEmpty(govName)) govName = "미정";

            var faction = m.GetInitialLordFaction();
            list.Add(new CastleSheetRow
            {
                castleId = CastleIdToNumeric(id),
                castleName = CastleMapDisplayName.FromMaster(m),
                countryId = FactionToCountryId(faction),
                governorName = govName,
                governorGeneralId = govId,
                army = Mathf.Max(1, m.maxTroops / 2),
                population = Mathf.Max(0, m.initPopulation),
                publicSentiment = 55,
                castleValue = Mathf.Max(0, Mathf.RoundToInt(DataManager.GetEconomyBaseValuePerTroop(m))),
                mapPosition = WorldMapLayout.SheetMapToWorld(m.posX, m.posY),
                masterId = id,
                adjacentIdsRaw = m.adjacentIdsRaw ?? string.Empty,
                grade = m.grade,
            });
        }

        list.Sort((a, b) => string.Compare(a.castleName, b.castleName, StringComparison.Ordinal));
        return list.ToArray();
    }

    public static CastleSheetRow[] FromCastleMasterSo(CastleMasterDataSo castleSo, GeneralMasterDataSo generalSo)
    {
        if (castleSo == null || castleSo.list == null || castleSo.list.Count == 0)
            return Array.Empty<CastleSheetRow>();
        IReadOnlyList<GeneralMasterData> gens = generalSo != null && generalSo.list != null
            ? generalSo.list
            : Array.Empty<GeneralMasterData>();
        return FromLists(castleSo.list, gens);
    }

    static CastleSheetRow[] FromLists(IReadOnlyList<CastleMasterData> castles, IReadOnlyList<GeneralMasterData> generals)
    {
        var dictC = new Dictionary<string, CastleMasterData>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in castles)
        {
            if (c == null || string.IsNullOrWhiteSpace(c.id)) continue;
            dictC[c.id.Trim()] = c;
        }

        var dictG = new Dictionary<string, GeneralMasterData>(StringComparer.OrdinalIgnoreCase);
        if (generals != null)
        {
            foreach (var g in generals)
            {
                if (g == null || string.IsNullOrWhiteSpace(g.id)) continue;
                dictG[g.id.Trim()] = g;
            }
        }

        return FromCastleMasterDictionary(dictC, dictG);
    }

    /// <summary><see cref="DataManager.ApplyInitialGovernorsFromGenerals"/>와 동일: 등급 우선, 동급이면 ID 순.</summary>
    public static void PickBestGovernorForCastle(
        string castleMasterId,
        Dictionary<string, GeneralMasterData> generals,
        out string governorId,
        out string governorName)
    {
        governorId = string.Empty;
        governorName = string.Empty;
        if (string.IsNullOrWhiteSpace(castleMasterId) || generals == null) return;

        var target = castleMasterId.Trim();
        string bestId = null;
        GeneralMasterData bestG = null;

        foreach (var kv in generals)
        {
            var g = kv.Value;
            if (g == null || string.IsNullOrWhiteSpace(g.initialCastleId)) continue;
            var cid = g.initialCastleId.Trim();
            if (!string.Equals(cid, target, StringComparison.OrdinalIgnoreCase)) continue;

            if (bestG == null)
            {
                bestId = g.id.Trim();
                bestG = g;
                continue;
            }

            if (g.grade < bestG.grade)
            {
                bestId = g.id.Trim();
                bestG = g;
            }
            else if (g.grade == bestG.grade && string.CompareOrdinal(g.id, bestId) < 0)
            {
                bestId = g.id.Trim();
                bestG = g;
            }
        }

        if (bestG == null) return;
        governorId = bestId ?? string.Empty;
        governorName = string.IsNullOrEmpty(bestG.name) ? "미정" : bestG.name;
    }

    static int CastleIdToNumeric(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return 0;
        int n = 0;
        bool any = false;
        foreach (char c in id)
        {
            if (!char.IsDigit(c)) continue;
            any = true;
            n = n * 10 + (c - '0');
        }

        if (!any) n = Mathf.Abs(id.GetHashCode() % 100000);
        return n == 0 ? Mathf.Abs(id.GetHashCode() % 100000) : n;
    }

    static CountryId FactionToCountryId(Faction f)
    {
        if (f == Faction.WEI) return CountryId.Wei;
        if (f == Faction.SHU) return CountryId.Shu;
        if (f == Faction.WU) return CountryId.Wu;
        if (f == Faction.OTHERS) return CountryId.Others;
        return CountryId.Others;
    }

    /// <summary>월드맵 성 마커(<see cref="CountryId"/>)와 저장용 <see cref="Faction"/>을 맞춥니다.</summary>
    public static Faction CountryIdToFaction(CountryId id)
    {
        if (id == CountryId.Wei) return Faction.WEI;
        if (id == CountryId.Shu) return Faction.SHU;
        if (id == CountryId.Wu) return Faction.WU;
        if (id == CountryId.Others) return Faction.OTHERS;
        return Faction.OTHERS;
    }
}
