using System;
using UnityEngine;

public partial class DataManager
{
    public event Action OnHomeCastleChanged;
    public event Action OnTravelGaugeChanged;

    public string HomeCastleId => _homeCastleId ?? "";
    public float TravelGaugePoints => _travelGaugePoints;
    /// <summary><see cref="UserPortfolioSo.currentStepCount"/>와 동기화된 만보기 누적(표시용).</summary>
    public int PortfolioSyncedStepCount => _lastStepCountSyncedForGauge;
    public float TravelGaugeVisualCap => Mathf.Max(1000f, travelGaugeVisualCap);

    public bool HasPendingHqMove => !string.IsNullOrWhiteSpace(_pendingHqMoveCastleId);
    public string PendingHqMoveTargetId => _pendingHqMoveCastleId ?? "";
    public float PendingHqMoveCostPoints => _pendingHqMoveCost;

    public void SetPendingHqMove(string targetCastleId, float costPoints)
    {
        _pendingHqMoveCastleId = (targetCastleId ?? "").Trim();
        _pendingHqMoveCost = Mathf.Max(0f, costPoints);
    }

    public void ClearPendingHqMove()
    {
        _pendingHqMoveCastleId = "";
        _pendingHqMoveCost = 0f;
    }

    /// <summary>이동 게이지가 이주 비용 이상이면 차감·본영 갱신 후 대기 상태를 해제합니다.</summary>
    public void TryCompletePendingHqMoveIfReady()
    {
        if (!IsStateReady || string.IsNullOrWhiteSpace(_pendingHqMoveCastleId)) return;
        if (_travelGaugePoints + 1e-3f < _pendingHqMoveCost) return;

        string tid = _pendingHqMoveCastleId.Trim();
        float c = _pendingHqMoveCost;
        ClearPendingHqMove();
        ApplyHqMoveAfterTravel(tid, c);
    }

    /// <summary>지도 좌표(posX,posY) 기준 유클리드 거리. 마스터가 없으면 -1.</summary>
    public float GetDistance(string castleIdA, string castleIdB)
    {
        if (string.IsNullOrWhiteSpace(castleIdA) || string.IsNullOrWhiteSpace(castleIdB)) return -1f;
        castleIdA = castleIdA.Trim();
        castleIdB = castleIdB.Trim();
        if (string.Equals(castleIdA, castleIdB, StringComparison.Ordinal)) return 0f;
        if (!castleMasterDataMap.TryGetValue(castleIdA, out var ma) || ma == null) return -1f;
        if (!castleMasterDataMap.TryGetValue(castleIdB, out var mb) || mb == null) return -1f;
        float dx = ma.posX - mb.posX;
        float dy = ma.posY - mb.posY;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    public float GetTravelCostPoints(string targetCastleId)
    {
        if (string.IsNullOrWhiteSpace(_homeCastleId)) return 0f;
        float d = GetDistance(_homeCastleId, targetCastleId);
        if (d < 0f) return float.MaxValue;
        return Mathf.Max(0f, d / 100f * travelPointsPer100Distance);
    }

    /// <summary>이동 포인트를 만보기 충전 규칙(1걸음당 travelPointsPerStep)으로 환산한 보수적 걸음 수(올림).</summary>
    public int GetTravelCostStepEquivalent(float costPoints)
    {
        if (costPoints <= 0f || travelPointsPerStep <= 1e-5f) return 0;
        return Mathf.Max(0, Mathf.CeilToInt(costPoints / travelPointsPerStep));
    }

    public bool TryValidateHqMove(string targetCastleId, out float costPoints, out string error)
    {
        costPoints = 0f;
        error = null;
        if (!IsStateReady)
        {
            error = "데이터 준비 중입니다.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(targetCastleId))
        {
            error = "목표 성이 없습니다.";
            return false;
        }

        targetCastleId = targetCastleId.Trim();
        if (string.IsNullOrWhiteSpace(_homeCastleId))
        {
            error = "본영이 설정되지 않았습니다.";
            return false;
        }

        if (string.Equals(_homeCastleId.Trim(), targetCastleId, StringComparison.Ordinal))
        {
            error = "이미 본영입니다.";
            return false;
        }

        if (!castleMasterDataMap.ContainsKey(targetCastleId))
        {
            error = "알 수 없는 성입니다.";
            return false;
        }

        costPoints = GetTravelCostPoints(targetCastleId);
        if (costPoints >= float.MaxValue * 0.5f)
        {
            error = "거리를 계산할 수 없습니다.";
            return false;
        }

        return true;
    }

    /// <summary>연출 종료 후 호출 — 게이지 차감·본영 갱신·SO 반영.</summary>
    public void ApplyHqMoveAfterTravel(string targetCastleId, float costPoints)
    {
        if (!IsStateReady || string.IsNullOrWhiteSpace(targetCastleId)) return;
        targetCastleId = targetCastleId.Trim();
        _travelGaugePoints = Mathf.Max(0f, _travelGaugePoints - Mathf.Max(0f, costPoints));
        _homeCastleId = targetCastleId;
        _stateDirty = true;
        FlushLiveScriptableObjects();
        OnHomeCastleChanged?.Invoke();
        OnTravelGaugeChanged?.Invoke();
        RequestWorldUiRefresh();
    }

    public float GetTravelGaugeFillNormalized()
    {
        float cap = TravelGaugeVisualCap;
        return cap > 1e-4f ? Mathf.Clamp01(_travelGaugePoints / cap) : 0f;
    }

    public static string GetFactionLordShortLabel(Faction f)
    {
        switch (f)
        {
            case Faction.WEI: return "위";
            case Faction.SHU: return "촉";
            case Faction.WU: return "오";
            case Faction.OTHERS: return "기타";
            default: return "중립";
        }
    }

    void LoadWorldPortfolioHqFromSo()
    {
        _homeCastleId = "";
        _travelGaugePoints = 0f;
        _lastStepCountSyncedForGauge = 0;
        if (userPortfolioLiveSo == null) return;
        _homeCastleId = (userPortfolioLiveSo.homeCastleId ?? "").Trim();
        _travelGaugePoints = Mathf.Max(0f, userPortfolioLiveSo.travelGaugePoints);
        _lastStepCountSyncedForGauge = Mathf.Max(0, userPortfolioLiveSo.currentStepCount);
        ClearPendingHqMove();
    }

    void EnsureDefaultHomeCastleIfEmpty()
    {
        if (!string.IsNullOrWhiteSpace(_homeCastleId) && castleMasterDataMap.ContainsKey(_homeCastleId.Trim()))
            return;

        var ids = GetOrderedWorldCastleIds();
        if (ids != null && ids.Count > 0)
        {
            _homeCastleId = ids[0];
            return;
        }

        foreach (var kv in castleMasterDataMap)
        {
            if (kv.Value == null || string.IsNullOrWhiteSpace(kv.Value.id)) continue;
            _homeCastleId = kv.Value.id.Trim();
            return;
        }
    }

    void HookGameManagerStepsForTravelGauge()
    {
        if (_gameManagerStepsHooked) return;
        var gm = GameManager.InstanceOrNull;
        if (gm == null) return;
        gm.OnStepsChanged += HandleStepsForTravelGauge;
        _gameManagerStepsHooked = true;
        if (gm.currentUser != null)
            HandleStepsForTravelGauge(gm.currentUser.stepsToday);
    }

    void OnDestroy()
    {
        var gm = GameManager.InstanceOrNull;
        if (gm != null && _gameManagerStepsHooked)
            gm.OnStepsChanged -= HandleStepsForTravelGauge;
        _gameManagerStepsHooked = false;
    }

    void HandleStepsForTravelGauge(int stepsToday)
    {
        if (!IsStateReady) return;
        stepsToday = Mathf.Max(0, stepsToday);
        if (stepsToday < _lastStepCountSyncedForGauge)
        {
            _lastStepCountSyncedForGauge = stepsToday;
            _stateDirty = true;
            return;
        }

        int delta = stepsToday - _lastStepCountSyncedForGauge;
        if (delta <= 0) return;
        _travelGaugePoints += delta * travelPointsPerStep;
        _lastStepCountSyncedForGauge = stepsToday;
        _stateDirty = true;
        OnTravelGaugeChanged?.Invoke();
        TryCompletePendingHqMoveIfReady();
    }

    void TickTravelGaugeIdle(float dtUnscaled)
    {
        if (!IsStateReady || dtUnscaled <= 0f) return;
        float add = dtUnscaled / 60f * travelIdlePointsPerMinute;
        if (add <= 0f) return;
        _travelGaugePoints += add;
        _stateDirty = true;
        OnTravelGaugeChanged?.Invoke();
        TryCompletePendingHqMoveIfReady();
    }
}
