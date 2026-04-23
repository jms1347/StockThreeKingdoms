using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>월드맵 장수 위치·행군(출정/공성) 추적. <see cref="DataManager"/> 초기 배치·태수와 동기화합니다.</summary>
public static class WorldMapGeneralRoster
{
    sealed class MarchBinding
    {
        public Castle From;
        public Castle To;
        public MarchingTroopMarker Marker;
    }

    static readonly Dictionary<string, string> AtCastleByGeneral =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    static readonly Dictionary<string, MarchBinding> MarchingByGeneral =
        new Dictionary<string, MarchBinding>(StringComparer.OrdinalIgnoreCase);

    public static void RebuildFromDataManager(DataManager dm)
    {
        var keepMarching = new List<KeyValuePair<string, MarchBinding>>(MarchingByGeneral);
        AtCastleByGeneral.Clear();
        MarchingByGeneral.Clear();
        for (int i = 0; i < keepMarching.Count; i++)
        {
            var kv = keepMarching[i];
            if (kv.Value?.Marker != null)
                MarchingByGeneral[kv.Key] = kv.Value;
        }

        if (dm == null)
            return;

        if (dm.castleStateDataMap != null)
        {
            foreach (var kv in dm.castleStateDataMap)
            {
                var st = kv.Value;
                if (st == null || string.IsNullOrWhiteSpace(st.currentGovernorId)) continue;
                var gid = st.currentGovernorId.Trim();
                if (string.IsNullOrEmpty(gid)) continue;
                if (MarchingByGeneral.ContainsKey(gid)) continue;
                AtCastleByGeneral[gid] = kv.Key.Trim();
            }
        }

        if (dm.generalMasterDataMap == null) return;
        foreach (var kv in dm.generalMasterDataMap)
        {
            var g = kv.Value;
            if (g == null || string.IsNullOrWhiteSpace(g.id)) continue;
            var gid = g.id.Trim();
            if (MarchingByGeneral.ContainsKey(gid)) continue;
            if (AtCastleByGeneral.ContainsKey(gid)) continue;
            if (string.IsNullOrWhiteSpace(g.initialCastleId)) continue;
            AtCastleByGeneral[gid] = g.initialCastleId.Trim();
        }
    }

    public static void BeginMarch(string generalId, Castle from, Castle to, MarchingTroopMarker marker)
    {
        if (string.IsNullOrWhiteSpace(generalId) || from == null || to == null || marker == null) return;
        generalId = generalId.Trim();
        MarchingByGeneral[generalId] = new MarchBinding { From = from, To = to, Marker = marker };
        AtCastleByGeneral.Remove(generalId);
    }

    public static void NotifyMarchMarkerDestroyed(MarchingTroopMarker marker)
    {
        if (marker == null) return;
        string found = null;
        foreach (var kv in MarchingByGeneral)
        {
            if (kv.Value?.Marker == marker)
            {
                found = kv.Key;
                break;
            }
        }

        if (found == null) return;
        MarchingByGeneral.Remove(found);
        // 도착 처리 없이 파괴된 경우(씬 전환 등) — 원래 성으로 복귀시키지 않고 위치 미상으로 둠
    }

    /// <summary>공성이 시작되면 주장수는 수비 성 부지에 합류한 것으로 봅니다.</summary>
    public static void NotifySiegeEngaged(string attackerGovernorId, Castle defender)
    {
        if (defender == null || string.IsNullOrWhiteSpace(defender.MasterId)) return;
        if (string.IsNullOrWhiteSpace(attackerGovernorId)) return;
        var gid = attackerGovernorId.Trim();
        MarchingByGeneral.Remove(gid);
        AtCastleByGeneral[gid] = defender.MasterId.Trim();
    }

    public static void NotifySiegeEndAttackerVictory(string attackerGovernorId, Castle defenderCastle)
    {
        if (defenderCastle == null || string.IsNullOrWhiteSpace(defenderCastle.MasterId)) return;
        if (string.IsNullOrWhiteSpace(attackerGovernorId)) return;
        var gid = attackerGovernorId.Trim();
        MarchingByGeneral.Remove(gid);
        AtCastleByGeneral[gid] = defenderCastle.MasterId.Trim();
    }

    public static void NotifySiegeEndDefenderVictory(string attackerGovernorId, Castle attackerCastle)
    {
        if (attackerCastle == null || string.IsNullOrWhiteSpace(attackerCastle.MasterId)) return;
        if (string.IsNullOrWhiteSpace(attackerGovernorId)) return;
        var gid = attackerGovernorId.Trim();
        MarchingByGeneral.Remove(gid);
        AtCastleByGeneral[gid] = attackerCastle.MasterId.Trim();
    }

    /// <summary>함락 시 구 태수는 공격 성으로 끌려간 것으로 처리합니다.</summary>
    public static void NotifyDefenderGovernorCaptured(string oldDefenderGovernorId, Castle attackerCastle)
    {
        if (attackerCastle == null || string.IsNullOrWhiteSpace(attackerCastle.MasterId)) return;
        if (string.IsNullOrWhiteSpace(oldDefenderGovernorId)) return;
        var gid = oldDefenderGovernorId.Trim();
        MarchingByGeneral.Remove(gid);
        AtCastleByGeneral[gid] = attackerCastle.MasterId.Trim();
    }

    public static void NotifyMarchReturnedHome(string generalId, Castle home)
    {
        if (home == null || string.IsNullOrWhiteSpace(home.MasterId)) return;
        if (string.IsNullOrWhiteSpace(generalId)) return;
        var gid = generalId.Trim();
        MarchingByGeneral.Remove(gid);
        AtCastleByGeneral[gid] = home.MasterId.Trim();
    }

    public static int CountGeneralsStationedAt(string castleMasterId)
    {
        if (string.IsNullOrWhiteSpace(castleMasterId)) return 0;
        var cid = castleMasterId.Trim();
        int n = 0;
        foreach (var kv in AtCastleByGeneral)
        {
            if (string.Equals(kv.Value, cid, StringComparison.OrdinalIgnoreCase))
                n++;
        }

        return n;
    }

    public static void AppendGeneralsSummary(Castle castle, StringBuilder sb)
    {
        if (castle == null || sb == null) return;
        var dm = DataManager.InstanceOrNull;
        if (dm?.generalMasterDataMap == null || string.IsNullOrWhiteSpace(castle.MasterId))
        {
            sb.AppendLine("장수: (데이터 없음)");
            return;
        }

        var cid = castle.MasterId.Trim();
        var lines = new List<(string id, string name, int sort)>(16);
        string govId = castle.GovernorGeneralId;
        if (string.IsNullOrWhiteSpace(govId) &&
            dm.castleStateDataMap != null &&
            dm.castleStateDataMap.TryGetValue(cid, out var st) &&
            st != null &&
            !string.IsNullOrWhiteSpace(st.currentGovernorId))
            govId = st.currentGovernorId.Trim();

        foreach (var kv in dm.generalMasterDataMap)
        {
            var g = kv.Value;
            if (g == null || string.IsNullOrWhiteSpace(g.id)) continue;
            var gid = g.id.Trim();

            if (MarchingByGeneral.TryGetValue(gid, out var march))
            {
                if (march?.To != null && string.Equals(march.To.MasterId, cid, StringComparison.OrdinalIgnoreCase))
                    lines.Add((gid, $"{g.name}(행군→{march.To.DisplayCastleName})", 1));
                continue;
            }

            if (!AtCastleByGeneral.TryGetValue(gid, out var at) ||
                !string.Equals(at, cid, StringComparison.OrdinalIgnoreCase))
                continue;

            bool isGov = !string.IsNullOrWhiteSpace(govId) &&
                         string.Equals(gid, govId, StringComparison.OrdinalIgnoreCase);
            string label = isGov ? $"{g.name} [태수]" : g.name;
            lines.Add((gid, label, isGov ? 0 : 2));
        }

        lines.Sort((a, b) =>
        {
            int c = a.sort.CompareTo(b.sort);
            return c != 0 ? c : string.Compare(a.name, b.name, StringComparison.Ordinal);
        });

        if (lines.Count == 0)
        {
            sb.AppendLine("장수: 주둔 장수 없음(마스터/배치 확인)");
            return;
        }

        int stayCount = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].sort != 1)
                stayCount++;
        }

        sb.AppendLine($"머무르는 장수 (총 {stayCount}명):");
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].sort == 1)
                continue;
            sb.AppendLine($" · {lines[i].name}");
        }

        bool anyInbound = false;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].sort != 1) continue;
            if (!anyInbound)
                sb.AppendLine("이 성으로 행군 중인 장수:");
            anyInbound = true;
            sb.AppendLine($" · {lines[i].name}");
        }
    }

    public static void AppendMovementSummary(Castle castle, StringBuilder sb)
    {
        if (castle == null || sb == null || string.IsNullOrWhiteSpace(castle.MasterId)) return;
        var cid = castle.MasterId.Trim();
        var dm = DataManager.InstanceOrNull;
        if (dm?.generalMasterDataMap == null) return;

        bool any = false;
        foreach (var kv in MarchingByGeneral)
        {
            var bind = kv.Value;
            if (bind?.From == null || bind.To == null) continue;
            if (!string.Equals(bind.From.MasterId, cid, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(bind.To.MasterId, cid, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!dm.generalMasterDataMap.TryGetValue(kv.Key, out var g) || g == null) continue;
            any = true;
            if (string.Equals(bind.From.MasterId, cid, StringComparison.OrdinalIgnoreCase))
                sb.AppendLine($" · {g.name}: 이 성 출발 → {bind.To.DisplayCastleName}");
            else
                sb.AppendLine($" · {g.name}: {bind.From.DisplayCastleName}에서 이 성으로 행군");
        }

        if (!any)
            sb.AppendLine("장수 이동: 없음");
    }

    public static string ResolveMarchingGovernorId(Castle fromCastle)
    {
        if (fromCastle == null) return string.Empty;
        if (!string.IsNullOrWhiteSpace(fromCastle.GovernorGeneralId))
            return fromCastle.GovernorGeneralId.Trim();
        var dm = DataManager.InstanceOrNull;
        if (dm?.castleStateDataMap == null || string.IsNullOrWhiteSpace(fromCastle.MasterId)) return string.Empty;
        if (!dm.castleStateDataMap.TryGetValue(fromCastle.MasterId.Trim(), out var st) || st == null)
            return string.Empty;
        return string.IsNullOrWhiteSpace(st.currentGovernorId) ? string.Empty : st.currentGovernorId.Trim();
    }

    /// <summary>성에 주둔(행군 제외) 중인 장수 중 태수 승계 후보. 등급(숫자 작을수록 상위) → ID 순.</summary>
    public static GeneralMasterData FindBestGovernorSuccessorAtCastle(string castleMasterId, string excludeGeneralId,
        DataManager dm)
    {
        if (string.IsNullOrWhiteSpace(castleMasterId) || dm?.generalMasterDataMap == null) return null;
        var cid = castleMasterId.Trim();
        var ex = string.IsNullOrWhiteSpace(excludeGeneralId) ? string.Empty : excludeGeneralId.Trim();

        var list = new List<GeneralMasterData>(8);
        foreach (var kv in dm.generalMasterDataMap)
        {
            var g = kv.Value;
            if (g == null || string.IsNullOrWhiteSpace(g.id)) continue;
            var gid = g.id.Trim();
            if (string.Equals(gid, ex, StringComparison.OrdinalIgnoreCase)) continue;
            if (MarchingByGeneral.ContainsKey(gid)) continue;
            if (!AtCastleByGeneral.TryGetValue(gid, out var at) ||
                !string.Equals(at, cid, StringComparison.OrdinalIgnoreCase))
                continue;
            list.Add(g);
        }

        if (list.Count == 0) return null;
        list.Sort((a, b) =>
        {
            int c = a.grade.CompareTo(b.grade);
            return c != 0 ? c : string.CompareOrdinal(a.id, b.id);
        });
        return list[0];
    }
}
