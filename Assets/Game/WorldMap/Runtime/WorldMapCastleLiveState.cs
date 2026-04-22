using System;
using System.Collections.Generic;

/// <summary>월드맵 마커용 — <see cref="DataManager"/> 라이브/저장 성 상태와 소문 임박 여부.</summary>
public static class WorldMapCastleLiveState
{
    public struct Snapshot
    {
        public bool isWar;
        public bool isDisaster;
        public bool isFavorableEvent;
        /// <summary>소문 확정 전 등 — 행군·사건 임박 UI에 사용.</summary>
        public bool pendingRumor;

        public bool AnyFlag => isWar || isDisaster || isFavorableEvent || pendingRumor;
    }

    public static bool TryGet(DataManager dm, string castleMasterId, out Snapshot s)
    {
        s = default;
        if (dm == null || string.IsNullOrWhiteSpace(castleMasterId)) return false;

        var cid = castleMasterId.Trim();
        if (dm.TryGetLiveCastleState(cid, out var live) && live != null)
        {
            s.isWar = live.isWar;
            s.isDisaster = live.isDisaster;
            s.isFavorableEvent = live.isFavorableEvent;
        }
        else if (dm.castleStateDataMap != null &&
                 dm.castleStateDataMap.TryGetValue(cid, out var st) &&
                 st != null)
        {
            s.isWar = st.isWar;
            s.isDisaster = st.isDisaster;
            s.isFavorableEvent = st.isFavorableEvent;
        }

        s.pendingRumor = HasPendingRumor(dm, cid);
        return true;
    }

    /// <summary>DataManager 플래그와 월드맵 로컬 시뮬(<see cref="Castle"/>)을 합칩니다.</summary>
    public static void MergeSnapshot(DataManager dm, string masterId, Castle castle, out Snapshot s)
    {
        s = default;
        if (dm != null && !string.IsNullOrWhiteSpace(masterId))
            TryGet(dm, masterId.Trim(), out s);

        if (castle != null)
        {
            s.isWar |= castle.SimWarDays > 0;
            if (WorldMapWarManager.InstanceOrNull != null)
                s.isWar |= WorldMapWarManager.InstanceOrNull.IsCastleInAnyWar(castle);
            s.isDisaster |= castle.SimDisasterDays > 0;
            s.isFavorableEvent |= castle.SimFavorableDays > 0;
            s.pendingRumor |= castle.SimRumorDays > 0;
        }
    }

    static bool HasPendingRumor(DataManager dm, string castleId)
    {
        if (dm?.pendingRumorWorldEvents == null) return false;
        var cid = (castleId ?? "").Trim();
        if (string.IsNullOrEmpty(cid)) return false;

        for (int i = 0; i < dm.pendingRumorWorldEvents.Count; i++)
        {
            var p = dm.pendingRumorWorldEvents[i];
            if (p == null) continue;
            var ids = ParseAffectedCastleIds(p);
            for (int j = 0; j < ids.Count; j++)
            {
                if (string.Equals((ids[j] ?? "").Trim(), cid, StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }

    static List<string> ParseAffectedCastleIds(PendingRumorWorldEvent p)
    {
        var list = new List<string>();
        if (!string.IsNullOrWhiteSpace(p.affectedCastleIdsRaw))
        {
            var parts = p.affectedCastleIdsRaw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                var id = parts[i].Trim();
                if (!string.IsNullOrEmpty(id)) list.Add(id);
            }
        }

        if (list.Count == 0 && !string.IsNullOrWhiteSpace(p.targetCastleId))
            list.Add(p.targetCastleId.Trim());
        return list;
    }
}
