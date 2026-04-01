using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// IEventTrigger 기반 행을 SO 테이블로 묶는 예시(기획이 시트를 쓰는 경우 FixedSo와 병합 가능).
/// </summary>
[CreateAssetMenu(fileName = "WorldEventTriggerTable", menuName = "StockTK/World Event Trigger Table", order = 50)]
public class WorldEventTriggerTableSo : ScriptableObject
{
    [Tooltip("조건형 / 정기 뉴스 행. 런타임은 <see cref=\"EventTriggerRowRuntime\"/>로 변환해 사용.")]
    public List<EventTriggerRow> rows = new List<EventTriggerRow>();
}

[Serializable]
public class EventTriggerRow
{
    public string triggerId;
    [Tooltip("true면 ShouldEvaluateToday에서 조건 검사")]
    public bool isConditional;
    [Tooltip("EventMasterData.id 등 외부 시스템과 연결")]
    public string linkedEventMasterId;
    [Tooltip("ConditionMaster condId — DataManager 조건 라이브러리와 매칭")]
    public string primaryConditionId;
    [Tooltip("정기 뉴스일 때만: N일마다 1회 같은 식의 간이 주기(0이면 매일 후보)")]
    public int evaluateEveryNDays = 1;
}

/// <summary>SO 행을 인터페이스로 쓰기 위한 얇은 런타임 래퍼(샘플).</summary>
public sealed class EventTriggerRowRuntime : IEventTrigger
{
    readonly EventTriggerRow _row;

    public EventTriggerRowRuntime(EventTriggerRow row) => _row = row;

    public string TriggerId => _row?.triggerId ?? "";

    public bool IsConditional => _row != null && _row.isConditional;

    public bool ShouldEvaluateToday(DataManager dm, CastleStateData castle, int utcDayBucket)
    {
        if (_row == null || castle == null || dm == null) return false;
        if (_row.evaluateEveryNDays > 1 && utcDayBucket % _row.evaluateEveryNDays != 0)
            return false;
        if (!_row.isConditional)
            return true;
        if (string.IsNullOrWhiteSpace(_row.primaryConditionId))
            return true;
        return EventConditionEvaluator.IsConditionMet(dm, castle, _row.primaryConditionId.Trim());
    }
}
