using UnityEngine;

public partial class DataManager
{
    /// <summary>월드맵 공성 승리 시 함락된 성의 <see cref="CastleStateData.currentLord"/>를 공격 세력에 맞춥니다.</summary>
    public void ApplyWorldMapSiegeConquestLord(string defenderCastleMasterId, Faction newLord)
    {
        if (!IsStateReady || string.IsNullOrWhiteSpace(defenderCastleMasterId)) return;
        var id = defenderCastleMasterId.Trim();
        if (castleStateDataMap == null || !castleStateDataMap.TryGetValue(id, out var s) || s == null) return;

        s.currentLord = newLord;
        _stateDirty = true;
        FlushLiveScriptableObjects();
        OnStateTicked?.Invoke();
    }

    /// <summary>월드맵에서 성의 <see cref="CastleStateData.currentGovernorId"/>만 갱신합니다.</summary>
    public void SetWorldMapCastleGovernorId(string castleMasterId, string governorGeneralId)
    {
        if (!IsStateReady || string.IsNullOrWhiteSpace(castleMasterId)) return;
        var id = castleMasterId.Trim();
        if (castleStateDataMap == null || !castleStateDataMap.TryGetValue(id, out var s) || s == null) return;

        s.currentGovernorId = string.IsNullOrWhiteSpace(governorGeneralId)
            ? string.Empty
            : governorGeneralId.Trim();
        s.lastDailyBuffGovernorId = "";
        s.lastDailyBuffTime = 0;
        _stateDirty = true;
        FlushLiveScriptableObjects();
        OnStateTicked?.Invoke();
    }

    /// <summary>월드맵 공성 함락 후 함락된 성의 <see cref="CastleStateData.currentGovernorId"/>를 공격측 태수로 맞춥니다.</summary>
    public void ApplyWorldMapSiegeConquestGovernor(string defenderCastleMasterId, string attackerGovernorGeneralId) =>
        SetWorldMapCastleGovernorId(defenderCastleMasterId, attackerGovernorGeneralId);

    /// <summary>월드맵 징발 등으로 <see cref="CastleStateData.currentPopulation"/>을 조정합니다.</summary>
    public void ApplyWorldMapPopulationDelta(string castleMasterId, int delta)
    {
        if (!IsStateReady || delta == 0 || string.IsNullOrWhiteSpace(castleMasterId)) return;
        var id = castleMasterId.Trim();
        if (castleStateDataMap == null || !castleStateDataMap.TryGetValue(id, out var s) || s == null) return;

        int next = Mathf.Max(0, s.currentPopulation + delta);
        if (next == s.currentPopulation) return;
        s.currentPopulation = next;
        _stateDirty = true;
        FlushLiveScriptableObjects();
        OnStateTicked?.Invoke();
    }
}
