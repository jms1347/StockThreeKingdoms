using System.Collections.Generic;
using UnityEngine;

/// <summary>태수가 출정(행군)할 때 성에 남은 장수 중 다음 태수를 임명합니다.</summary>
public static class WorldMapGovernorSuccession
{
    /// <summary>출정 장수가 현재 태수이면, 주둔 장수 중 등급·ID 순으로 승계합니다.</summary>
    public static void TryHandOverWhenGovernorMarches(string marchingGeneralId, Castle fromCastle, CountryColorProvider colors)
    {
        if (fromCastle == null || string.IsNullOrWhiteSpace(fromCastle.MasterId)) return;
        if (string.IsNullOrWhiteSpace(marchingGeneralId)) return;
        marchingGeneralId = marchingGeneralId.Trim();

        if (!IsCurrentGovernor(marchingGeneralId, fromCastle))
            return;

        var dm = DataManager.InstanceOrNull;
        var successor = WorldMapGeneralRoster.FindBestGovernorSuccessorAtCastle(fromCastle.MasterId.Trim(), marchingGeneralId, dm);
        if (successor == null)
        {
            fromCastle.ClearGovernorToProvisional();
            if (dm != null)
                dm.SetWorldMapCastleGovernorId(fromCastle.MasterId.Trim(), string.Empty);
            fromCastle.GetComponent<CastleWorldHud>()?.Refresh();
            return;
        }

        fromCastle.InstallGovernor(successor);
        if (dm != null)
            dm.SetWorldMapCastleGovernorId(fromCastle.MasterId.Trim(), successor.id.Trim());
        fromCastle.GetComponent<CastleWorldHud>()?.Refresh();
    }

    static bool IsCurrentGovernor(string generalId, Castle castle)
    {
        if (!string.Equals(generalId, castle.GovernorGeneralId, System.StringComparison.OrdinalIgnoreCase))
        {
            var dm = DataManager.InstanceOrNull;
            if (dm?.castleStateDataMap == null || string.IsNullOrWhiteSpace(castle.MasterId)) return false;
            if (!dm.castleStateDataMap.TryGetValue(castle.MasterId.Trim(), out var st) || st == null) return false;
            return string.Equals(generalId, st.currentGovernorId?.Trim(), System.StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }
}
